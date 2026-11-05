# Historial de Prompts - Iurii Galkin
**Rol:** Tarea 10 - Coordinación, Arquitectura Integral, Diagramas C4 y Suite E2E  
**Rama:** `feature/10-architecture-docs`

### Contexto y Desafíos Reales
Andamiaje inicial del repositorio, coordinación de 10 tareas desacopladas, transición de RabbitMQ a Azure Service Bus, migración de diagramas C4 a Mermaid y automatización de pruebas de integración con caos.

### Prompts Clave Utilizados

#### Prompt 1: Scaffolding Multicontenedor y Migración de Broker
> "Inicialmente armé el docker-compose con RabbitMQ, pero Facundo me avisó por WhatsApp que el profesor exige usar Azure Service Bus. ¿Cómo reestructuro el `docker-compose.yml` para soportar tanto Azurite local como la connection string real de Azure mediante variables de entorno, y cómo configuro el rango de puertos dinámicos `5102-5110:5102` en Booking para permitir escalar réplicas con `--scale booking-api=2`?"

#### Prompt 2: Migración de Diagramas C4 PlantUML a Mermaid en Markdown
> "Teníamos diagramas C4 en formato PlantUML (.puml) pero requieren Java y plugins externos en VS Code. La cátedra va a evaluar directamente sobre GitHub. ¿Cómo traduzco los modelos de Contexto, Contenedores y Componentes a diagramas nativos de Mermaid (`flowchart TB`) con estilos contrastantes adaptables a Dark y Light Mode?"

#### Prompt 3: Suite Automatizada de Integración E2E y Caos (Bash Anti-Mock)
> "Necesito armar un script en Bash (`scripts/test-e2e-scenarios.sh`) que ejecute el Happy Path completo (obtención de token OAuth2, consulta de catálogo 200, reserva asíncrona 202 y sondeo hasta confirmación) y luego ejecute una prueba de caos físico deteniendo el contenedor (`docker stop seats-payment-grpc`) para validar que Booking active el Circuit Breaker de Polly y libere el asiento retenido sin colapsar Kestrel."
