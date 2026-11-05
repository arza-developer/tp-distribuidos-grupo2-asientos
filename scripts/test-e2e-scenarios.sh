#!/usr/bin/env bash
set -euo pipefail

G="\033[0;32m"; R="\033[0;31m"; Y="\033[1;33m"; B="\033[0;34m"; N="\033[0m"
AUTH_URL="http://localhost:5104"
AVAILABILITY_URL="http://localhost:5101"
BOOKING_URL="http://localhost:5102"
EVENT_ID="3fa85f64-5717-4562-b3fc-2c963f66afa6"
USER_ID="7bb85f64-1111-2222-3333-2c963f66af99"
OTHER_USER="9dd07a86-7777-8888-9999-4e185a88c1bb"

PASS=0; FAIL=0

cleanup() {
    docker start seats-payment-grpc seats-auth-server >/dev/null 2>&1 || true
}
trap cleanup EXIT

log_pass() { echo -e "  ${G}[PASS]${N} $1"; PASS=$((PASS+1)); }
log_fail() { echo -e "  ${R}[FAIL]${N} $1"; FAIL=$((FAIL+1)); }
log_info() { echo -e "  ${B}[INFO]${N} $1"; }

echo -e "${B}====================================================================${N}"
echo -e "${B} SUITE DE PRUEBAS DISTRIBUIDAS E2E (Requisito 3.j)                 ${N}"
echo -e "${B}====================================================================${N}"

echo -n "Esperando disponibilidad de servicios (Availability, AuthServer, Booking)..."
READY=false
for i in {1..30}; do
    if curl -s "$AVAILABILITY_URL/api/seats" >/dev/null 2>&1 && \
       curl -s -X POST "$AUTH_URL/connect/token" >/dev/null 2>&1 && \
       curl -s "$BOOKING_URL/api/bookings/00000000-0000-0000-0000-000000000000" >/dev/null 2>&1; then
       docker logs tp-distribuidos-grupo2-asientos-booking-api-1 2>&1 | grep -q "Escuchando el topic de reservas" && \
        READY=true
        break
    fi
    sleep 1
    echo -n "."
done

if [ "$READY" = true ]; then
    echo -e " ${G}[OK]${N}"
    sleep 2
else
    echo -e "\n${R}[ERROR] Timeout: los servicios no respondieron en 30s.${N}"
    exit 1
fi

echo -e "\n${Y}>>> 1. AUTENTICACION Y CONTROL DE ACCESO (OAuth2 / OIDC)${N}"
TOKEN=$(curl -s -X POST "$AUTH_URL/connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=cliente1&password=cliente1-dev" \
  | grep -o '"access_token":[^,}]*' | cut -d: -f2- | tr -d ' "')

if [ -n "$TOKEN" ]; then
    log_pass "Emision de token JWT para cliente1 (sub: $USER_ID)"
else
    log_fail "No se pudo obtener token JWT"
    exit 1
fi

echo -e "\n${Y}>>> 2. CONTRATOS DE API Y VALIDACIONES DE NEGOCIO${N}"
S_CAT=$(curl -s -o /dev/null -w "%{http_code}" "$AVAILABILITY_URL/api/seats")
[ "$S_CAT" -eq 200 ] && log_pass "Catalogo publico responde 200 OK" || log_fail "Catalogo no dio 200 ($S_CAT)"

S_401=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$AVAILABILITY_URL/api/reservations" \
  -H "Content-Type: application/json" \
  -d "{\"eventId\": \"$EVENT_ID\", \"seatNumber\": \"A-1\", \"userId\": \"$USER_ID\", \"price\": 15000.0}")
[ "$S_401" -eq 401 ] && log_pass "Solicitud sin credenciales rechazada con 401" || log_fail "Sin token no dio 401 ($S_401)"

S_403=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$AVAILABILITY_URL/api/reservations" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"eventId\": \"$EVENT_ID\", \"seatNumber\": \"A-1\", \"userId\": \"$OTHER_USER\", \"price\": 15000.0}")
[ "$S_403" -eq 403 ] && log_pass "Discrepancia de usuario en token rechazada con 403" || log_fail "Usuario ajeno no dio 403 ($S_403)"

S_409=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$AVAILABILITY_URL/api/reservations" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"eventId\": \"$EVENT_ID\", \"seatNumber\": \"A-3\", \"userId\": \"$USER_ID\", \"price\": 18000.0}")
[ "$S_409" -eq 409 ] && log_pass "Asiento ocupado (A-3) rechazado con 409 Conflict" || log_fail "Asiento ocupado no dio 409 ($S_409)"

echo -e "\n${Y}>>> 3. PROCESAMIENTO ASINCRONO Y CONSISTENCIA EVENTUAL${N}"
SB_CONN="${SERVICEBUS_CONNECTION_STRING:-}"
if [ -z "$SB_CONN" ] && [ -f .env ]; then
    SB_CONN=$(grep -E "^SERVICEBUS_CONNECTION_STRING=" .env | head -1 | cut -d= -f2- | tr -d '"\r')
fi

if [[ "$SB_CONN" =~ ^Endpoint=sb:// ]]; then
    log_info "SERVICEBUS_CONNECTION_STRING presente. Ejecutando flujo online con Azure Service Bus..."
    RESP_H1=$(curl -s -w "\n%{http_code}" -X POST "$AVAILABILITY_URL/api/reservations" \
      -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
      -d "{\"eventId\": \"$EVENT_ID\", \"seatNumber\": \"A-1\", \"userId\": \"$USER_ID\", \"price\": 15000.0}")
    BODY_H1=$(echo "$RESP_H1" | sed '$d')
    CODE_H1=$(echo "$RESP_H1" | tail -n1)

    if [ "$CODE_H1" -eq 202 ]; then
        log_pass "POST /api/reservations responde 202 Accepted (asiento A-1)"
        RES_ID=$(echo "$BODY_H1" | grep -o '"reservationId":"[^"]*' | cut -d'"' -f4 || echo "")
        
        if [ -n "$RES_ID" ]; then
            log_info "Sondeando estado de reserva $RES_ID..."
            CONFIRMED=false
            ATTEMPT=1
            INTERVAL=1
            TOTAL_WAITED=0
            while [ $TOTAL_WAITED -lt 50 ]; do
                sleep $INTERVAL
                TOTAL_WAITED=$((TOTAL_WAITED + INTERVAL))
                POLL_ST=$(curl -s -H "Authorization: Bearer $TOKEN" "$AVAILABILITY_URL/api/reservations/$RES_ID" | grep -o '"status":"[^"]*' | cut -d'"' -f4 || echo "")
                log_info "  Intento $ATTEMPT (+${INTERVAL}s, acum: ${TOTAL_WAITED}s) -> Estado: '$POLL_ST'"
                if [ "$POLL_ST" = "Confirmed" ] || [ "$POLL_ST" = "Confirmada" ]; then
                    CONFIRMED=true
                    break
                fi
                ATTEMPT=$((ATTEMPT + 1))
                INTERVAL=$((INTERVAL + 1))
            done

            if [ "$CONFIRMED" = true ]; then
                log_pass "Reserva confirmada tras procesamiento asincrono"
            else
                log_fail "Timeout de espera tras ${TOTAL_WAITED}s (estado actual: '$POLL_ST')"
            fi
        else
            log_fail "La respuesta 202 no incluyo reservationId"
        fi
    else
        log_fail "POST /api/reservations no devolvio 202 ($CODE_H1)"
    fi
else
    log_info "Sin conexion a broker. Verificando fail-safe offline..."
    RESP_F6=$(curl -s -w "\n%{http_code}" -X POST "$AVAILABILITY_URL/api/reservations" \
      -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
      -d "{\"eventId\": \"$EVENT_ID\", \"seatNumber\": \"A-1\", \"userId\": \"$USER_ID\", \"price\": 15000.0}")
    CODE_F6=$(echo "$RESP_F6" | tail -n1)
    [ "$CODE_F6" -eq 503 ] && log_pass "Sin broker responde 503 Service Unavailable" || log_fail "No dio 503 ($CODE_F6)"
fi

echo -e "\n${Y}>>> 4. TOLERANCIA A FALLOS (CHAOS PATH - Requisito 3.j)${N}"
echo "Deteniendo contenedor seats-payment-grpc..."
docker stop seats-payment-grpc >/dev/null 2>&1
log_info "Contenedor de pagos detenido."

S_CHAOS=$(curl -s -o /dev/null -w "%{http_code}" "$AVAILABILITY_URL/api/seats")
[ "$S_CHAOS" -eq 200 ] && log_pass "Aislamiento: catalogo responde 200 OK con pagos caido" || log_fail "Catalogo no respondio 200 ($S_CHAOS)"

if [[ "$SB_CONN" =~ ^Endpoint=sb:// ]]; then
    log_info "Solicitando reserva de A-2 con servicio de pago caido..."
    RESP_CH=$(curl -s -w "\n%{http_code}" -X POST "$AVAILABILITY_URL/api/reservations" \
      -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
      -d "{\"eventId\": \"$EVENT_ID\", \"seatNumber\": \"A-2\", \"userId\": \"$USER_ID\", \"price\": 15000.0}")
    CODE_CH=$(echo "$RESP_CH" | tail -n1)
    BODY_CH=$(echo "$RESP_CH" | sed '$d')

    if [ "$CODE_CH" -eq 202 ]; then
        CH_ID=$(echo "$BODY_CH" | grep -o '"reservationId":"[^"]*' | cut -d'"' -f4 || echo "")
        log_info "Verificando que la reserva permanezca retenida y no se confirme sin cobro..."
        NOT_CONFIRMED=true
        for i in {1..5}; do
            sleep 1
            POLL_CH=$(curl -s -H "Authorization: Bearer $TOKEN" "$AVAILABILITY_URL/api/reservations/$CH_ID" | grep -o '"status":"[^"]*' | cut -d'"' -f4 || echo "")
            if [ "$POLL_CH" = "Confirmed" ] || [ "$POLL_CH" = "Confirmada" ]; then
                NOT_CONFIRMED=false
                break
            fi
        done
        [ "$NOT_CONFIRMED" = true ] && log_pass "Reserva no confirmada con pasarela caida (estado: '$POLL_CH')" || log_fail "Inconsistencia: reserva confirmada con pago caido"
    else
        log_fail "No devolvio 202 en prueba de fallos ($CODE_CH)"
    fi
fi

echo "Restaurando seats-payment-grpc..."
docker start seats-payment-grpc >/dev/null 2>&1
sleep 2
log_pass "Servicio de pagos restaurado"

echo -e "\n${B}====================================================================${N}"
echo -e " RESUMEN: ${G}$PASS OK${N}, ${R}$FAIL FALLOS${N}"
echo -e "${B}====================================================================${N}"

[ $FAIL -eq 0 ] && exit 0 || exit 1
