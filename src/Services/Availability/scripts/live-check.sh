#!/usr/bin/env bash
# Verificación en vivo de Availability (escenarios feliz y de falla, req. 3.j) contra el docker compose del repo:
# auth-server + external-idp + booking-api + availability, con el Service Bus real configurado en .env (SERVICEBUS_CONNECTION_STRING).
#
#   bash src/Services/Availability/scripts/live-check.sh
#
# - Sin SERVICEBUS_CONNECTION_STRING válido corre solo los escenarios que no necesitan broker y lo informa.
# - Nunca imprime el secreto. Al terminar baja SOLO los servicios que levantó y borra el volumen de Availability.
# - Los mensajes de prueba se publican en el topic real del docente (1 por ejecución con broker).
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../../.." && pwd)"
cd "$ROOT"

AV=http://localhost:5101
AUTH=http://localhost:5104
EVENT=3fa85f64-5717-4562-b3fc-2c963f66afa6
USER_ID=7bb85f64-1111-2222-3333-2c963f66af99
OTHER_USER=9dd07a86-7777-8888-9999-4e185a88c1bb
SERVICES="auth-server booking-api availability external-idp"
SESSION_COOKIE=".Availability.Session"
VOLUME="$(basename "$ROOT" | tr 'A-Z' 'a-z')_availability-data"
PASS=0; FAIL=0; SKIP=0; RESULTS=()

die() { echo "ERROR: $*" >&2; exit 2; }
command -v docker >/dev/null || die "Falta Docker."
command -v curl   >/dev/null || die "Falta curl."
docker info >/dev/null 2>&1  || die "El daemon de Docker no responde."
[ -f docker-compose.yml ]    || die "No se encontró docker-compose.yml en $ROOT."
for port in 5101 5104; do
  (exec 3<>/dev/tcp/127.0.0.1/$port) 2>/dev/null && die "El puerto $port ya está en uso: bajá el stack actual antes de correr el script."
done

# ¿Hay un connection string de Service Bus con forma válida? (se valida la forma, nunca se imprime)
SB="${SERVICEBUS_CONNECTION_STRING:-}"
if [ -z "$SB" ] && [ -f .env ]; then
  SB="$(grep -E '^SERVICEBUS_CONNECTION_STRING=' .env | head -1 | cut -d= -f2- | tr -d '\r')"
fi
BROKER=0
case "$SB" in Endpoint=sb://*SharedAccessKeyName=*SharedAccessKey=*) BROKER=1;; esac
if [ $BROKER -eq 1 ]; then
  echo "Service Bus: connection string con forma válida (no se muestra)."
else
  echo "AVISO: sin SERVICEBUS_CONNECTION_STRING válido; se omiten los escenarios que requieren broker."
fi

TMP="$(mktemp -d)"
cleanup() {
  echo; echo "Limpieza: bajando los servicios levantados por el script..."
  docker compose rm -sfv $SERVICES >/dev/null 2>&1
  docker volume rm "$VOLUME" >/dev/null 2>&1
  rm -rf "$TMP"
}
trap cleanup EXIT

record() { # nombre esperado obtenido
  if [ "$2" = "$3" ]; then PASS=$((PASS+1)); RESULTS+=("PASS  $1 ($3)")
  else FAIL=$((FAIL+1)); RESULTS+=("FAIL  $1 (esperado $2, obtenido $3)"); fi
}
skip() { SKIP=$((SKIP+1)); RESULTS+=("SKIP  $1 ($2)"); }
code() { curl -s -o "$TMP/body" -w '%{http_code}' --max-time 20 "$@"; }
json() { sed -n "s/.*\"$1\":\"\([^\"]*\)\".*/\1/p" "$TMP/body" | head -1; }
token_of() { sed 's/.*"access_token":"\([^"]*\)".*/\1/'; }
login() { curl -s --max-time 20 -X POST $AUTH/connect/token -d "grant_type=password&username=cliente1&password=cliente1-dev" | token_of; }
reserve() { # token asiento userId
  code -X POST $AV/api/reservations -H "Authorization: Bearer $1" -H 'Content-Type: application/json' \
       -d "{\"eventId\":\"$EVENT\",\"seatNumber\":\"$2\",\"userId\":\"$3\",\"price\":15000.0}"
}
seat_available() { curl -s $AV/api/seats | grep -o "\"seatNumber\":\"$1\"[^}]*" | grep -o '"isAvailable":[a-z]*' | cut -d: -f2; }
wait_health() {
  for _ in $(seq 1 40); do
    [ "$(curl -s -o /dev/null -w '%{http_code}' $AV/health)" = 200 ] && return 0
    sleep 2
  done
  return 1
}
# Valor del header X-Correlation-ID que devuelve el catálogo cuando se le envía <id> en la request.
corr_header_for() {
  curl -s -D - -o /dev/null -H "X-Correlation-ID: $1" $AV/api/seats | tr -d '\r' | sed -n 's/^[Xx]-[Cc]orrelation-[Ii][Dd]: //p'
}
get_status() { code $AV/api/reservations/$1 -H "Authorization: Bearer $TOKEN"; }

# ---- Login federado (Authorization Code + PKCE vía Auth Server → IdP externo) -------------------------------------
# Recorre el flujo como un navegador, con curl: Availability → Auth Server → IdP (login) → form_post al Auth Server →
# vuelta a Availability con el code. Availability nunca habla con el IdP. Las credenciales del IdP (simulador de
# desarrollo) se leen de su appsettings y no se imprimen.
tok() { grep -o '__RequestVerificationToken[^>]*' | head -1 | sed 's/.*value="\([^"]*\)".*/\1/'; }
header_location() { grep -i '^location:' | head -1 | tr -d '\r' | cut -d' ' -f2; }
idp_password() {
  sed -n "/\"Email\": \"$1@/,/\"Subject\"/p" "$ROOT/src/Services/ExternalIdentityProvider.Api/appsettings.json" \
    | sed -n 's/.*"Password": "\([^"]*\)".*/\1/p'
}
# federated_login <jar de Availability> <usuario del IdP>  →  CB_URL (vuelta con el code) y AS_CODE (HTTP del callback del Auth Server)
federated_login() {
  local jav="$1" user="$2" af start authz page iaf res idtoken st
  CB_URL=""; AS_CODE=""
  af="$(curl -s -c "$jav" $AV/Account/Login | tok)"
  start="$(curl -s -i -b "$jav" -c "$jav" -X POST $AV/Account/LoginExternal --data-urlencode "__RequestVerificationToken=$af" | header_location)"
  authz="$(curl -s -i "$start" | header_location)"
  rm -f "$TMP/jidp"
  page="$(curl -s -c "$TMP/jidp" "$authz")"; iaf="$(echo "$page" | tok)"
  res="$(curl -s -b "$TMP/jidp" -c "$TMP/jidp" -X POST "$authz" --data-urlencode "Email=${user}@ejemplo.com" \
        --data-urlencode "Password=$(idp_password "$user")" --data-urlencode "__RequestVerificationToken=$iaf")"
  idtoken="$(echo "$res" | sed -n 's/.*name="id_token" value="\([^"]*\)".*/\1/p' | head -1)"
  st="$(echo "$res" | sed -n 's/.*name="state" value="\([^"]*\)".*/\1/p' | head -1)"
  [ -n "$idtoken" ] || return 0
  AS_CODE="$(curl -s -i -o "$TMP/ascb" -w '%{http_code}' -X POST $AUTH/external/callback \
            --data-urlencode "id_token=$idtoken" --data-urlencode "state=$st")"
  CB_URL="$(header_location < "$TMP/ascb")"
}
wait_auth() { for _ in $(seq 1 30); do [ "$(curl -s -o /dev/null -w '%{http_code}' $AUTH/)" = 200 ] && return 0; sleep 2; done; return 1; }

echo "Levantando auth-server, external-idp, booking-api y availability (build incluido)..."
docker compose rm -sfv availability >/dev/null 2>&1
docker volume rm "$VOLUME" >/dev/null 2>&1
if [ $BROKER -eq 1 ]; then
  docker compose up -d --build availability >/dev/null 2>&1
else
  SERVICEBUS_CONNECTION_STRING="" docker compose up -d --build availability auth-server external-idp >/dev/null 2>&1
fi
wait_health || die "Availability no respondió /health."
TOKEN="$(login)"
[ -n "$TOKEN" ] || die "No se pudo obtener el token de cliente1 en el Auth Server."

echo "Ejecutando escenarios..."
# ---- Fallos que no dependen del broker ---------------------------------------------------------------------------
record "F1 POST sin token → 401" 401 "$(code -X POST $AV/api/reservations -H 'Content-Type: application/json' -d '{}')"
SERVICE_TOKEN="$(curl -s -X POST $AUTH/connect/token -d 'grant_type=client_credentials&client_id=availability&client_secret=availability-secret-dev' | token_of)"
record "F2 rol equivocado (token de servicio) → 403" 403 "$(reserve "$SERVICE_TOKEN" A-1 $USER_ID)"
record "F3 userId ajeno al del token → 403" 403 "$(reserve "$TOKEN" A-1 $OTHER_USER)"
record "F4 asiento inexistente → 404" 404 "$(reserve "$TOKEN" Z-9 $USER_ID)"
record "F5 asiento no disponible (A-3 sembrado) → 409" 409 "$(reserve "$TOKEN" A-3 $USER_ID)"
record "C1 catálogo público → 200" 200 "$(code $AV/api/seats)"

# ---- Correlación (req. 3.f, librería compartida SeatsReservation.Observability) ----------------------------------
record "C2 el X-Correlation-ID entrante vuelve en la respuesta" abc-123 "$(corr_header_for 'abc-123')"
UNSAFE_ECHO="$(corr_header_for '<script>x</script>')"
case "$UNSAFE_ECHO" in ""|*script*) C3=no;; *) C3=ok;; esac
record "C3 un id con caracteres inseguros se reemplaza por uno generado" ok "$C3"

# ---- Login federado por el IdP externo (no requiere broker) -------------------------------------------------------
ANA_ID=9dd07a86-7777-8888-9999-4e185a88c1bb   # usuario de Availability asociado al sub externo ext-1001
rm -f "$TMP/jav1"; federated_login "$TMP/jav1" ana
record "E1 el IdP devuelve el id_token al Auth Server y este redirige a Availability con un code" 302 "${AS_CODE:-sin-id_token}"
case "$CB_URL" in http://localhost:5101/Account/Callback\?code=*) CB_OK=ok;; *) CB_OK="vuelta inesperada";; esac
record "E2 la vuelta apunta a /Account/Callback de Availability con code y state" ok "$CB_OK"
CB_PATH="${CB_URL#http://localhost:5101}"
record "E3 Availability canjea el code y abre sesión (302 a /)" 302 "$(code -b "$TMP/jav1" -c "$TMP/jav1" "$AV$CB_PATH")"
code -b "$TMP/jav1" $AV/Account/Profile >/dev/null
record "E4 la sesión federada corresponde al usuario de Availability (sub)" ok "$(grep -q "$ANA_ID" "$TMP/body" && echo ok || echo no)"
record "E5 repetir la misma vuelta (code y state ya usados) es rechazado" ok \
  "$(code -b "$TMP/jav1" "$AV$CB_PATH" >/dev/null; grep -q 'o no es v' "$TMP/body" && echo ok || echo no)"
rm -f "$TMP/jav2"; federated_login "$TMP/jav2" ana
BAD="$(echo "$CB_URL" | sed 's/state=[^&]*/state=alterado/')"
record "E6 state alterado en la vuelta es rechazado" ok \
  "$(code -b "$TMP/jav2" "$AV${BAD#http://localhost:5101}" >/dev/null; grep -q 'o no es v' "$TMP/body" && echo ok || echo no)"
rm -f "$TMP/jav3"; federated_login "$TMP/jav3" juan
record "E7 usuario del IdP no registrado en el sistema: el Auth Server lo rechaza (400)" 400 "${AS_CODE:-sin-id_token}"
rm -f "$TMP/jav4"; federated_login "$TMP/jav4" ana
docker compose stop auth-server >/dev/null 2>&1
record "E8 Auth Server caído en el canje: mensaje de servicio no disponible, sin sesión" ok \
  "$(code -b "$TMP/jav4" -c "$TMP/jav4" "$AV${CB_URL#http://localhost:5101}" >/dev/null; grep -q 'no est.* disponible' "$TMP/body" && ! grep -q "$SESSION_COOKIE" "$TMP/jav4" && echo ok || echo no)"
docker compose start auth-server >/dev/null 2>&1
wait_auth || die "El Auth Server no volvió."

# ---- Falla de broker (secreto vacío) → 503 y compensación del asiento ---------------------------------------------
SERVICEBUS_CONNECTION_STRING="" docker compose up -d --force-recreate availability >/dev/null 2>&1
wait_health || die "Availability no volvió."
record "F6 broker no configurado: POST → 503" 503 "$(reserve "$TOKEN" A-1 $USER_ID)"
record "F7 compensación: A-1 vuelve a estar libre" true "$(seat_available A-1)"

# ---- Flujo feliz y fallas por componente caído (requieren Service Bus real) --------------------------------------
if [ $BROKER -eq 1 ]; then
  docker compose up -d --force-recreate availability >/dev/null 2>&1
  wait_health || die "Availability no volvió."
  record "H1 reserva B-1 → 202" 202 "$(reserve "$TOKEN" B-1 $USER_ID)"
  RID="$(json reservationId)"
  STATUS=""
  for _ in $(seq 1 15); do
    get_status "$RID" >/dev/null; STATUS="$(json status)"
    [ "$STATUS" = Retained ] && break
    sleep 2
  done
  record "H2 Booking procesa y el estado llega a Retained" Retained "${STATUS:-?}"
  record "H3 el asiento B-1 queda bloqueado" false "$(seat_available B-1)"
  record "F8 reservar de nuevo B-1 → 409" 409 "$(reserve "$TOKEN" B-1 $USER_ID)"
  record "F9 GET de reserva inexistente → 404" 404 "$(get_status 00000000-0000-0000-0000-000000000001)"

  docker compose stop booking-api >/dev/null 2>&1
  record "F10 Booking caído: GET de estado → 503" 503 "$(get_status "$RID")"
  record "F11 Booking caído no libera el asiento" false "$(seat_available B-1)"
  docker compose start booking-api >/dev/null 2>&1

  docker compose stop auth-server >/dev/null 2>&1
  docker compose restart availability >/dev/null 2>&1
  wait_health || die "Availability no volvió."
  record "F12 Auth Server caído: el JWT se valida local y el token M2M falla → 503" 503 "$(get_status "$RID")"
  docker compose start auth-server >/dev/null 2>&1
else
  skip "H1-H3 flujo feliz (202, Retained, asiento bloqueado)" "sin Service Bus"
  skip "F8 doble reserva 409" "sin Service Bus"
  skip "F9 GET inexistente 404" "sin Service Bus"
  skip "F10-F11 Booking caído" "sin Service Bus"
  skip "F12 Auth Server caído" "sin Service Bus"
fi

echo; echo "================ RESULTADO ================"
printf '%s\n' "${RESULTS[@]}"
echo "-------------------------------------------"
echo "PASS=$PASS  FAIL=$FAIL  SKIP=$SKIP"
[ $FAIL -eq 0 ]
