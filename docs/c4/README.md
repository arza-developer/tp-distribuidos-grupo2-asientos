# Modelos de Arquitectura C4 y Diagramas de Secuencia
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA  
**Formato:** Markdown nativo con sintaxis Mermaid adaptativa (Cero dependencias Java/PlantUML).

## Herramientas Recomendadas para la Visualización

1. **GitHub Web Interface (Recomendado para la Cátedra):**
   - Todos los diagramas `.md` renderizan de forma nativa directamente en GitHub.
   - Cuentan con directivas de contraste adaptativo (`#f8fafc` en texto, `#1e293b` en bloques) optimizadas tanto para **GitHub Dark Mode** como **GitHub Light Mode**.
2. **Visual Studio Code:**
   - Requiere la extensión oficial: **Markdown Preview Mermaid Support** (ID: `bierner.markdown-mermaid`).
   - Visualización instantánea con el atajo: `Ctrl + Shift + V` (o `Cmd + Shift + V` en macOS).
3. **JetBrains Rider / IntelliJ IDEA:**
   - Soporte nativo para bloques `mermaid` dentro del visor Markdown integrado.
4. **Exportación Vectorial (CLI):**
   - Utilizando la herramienta `@mermaid-js/mermaid-cli`:
     ```bash
     npx @mermaid-js/mermaid-cli -i 01_system_context.md -o 01_system_context.svg
     ```

## Índice de Modelos C4
* [C4 Nivel 1: Contexto del Sistema](01_system_context.md)
* [C4 Nivel 2: Contenedores y Topología Distribuida](02_containers.md)
* [C4 Nivel 3: Componentes de Availability (Clean Architecture)](03_availability_components.md)
* [C4 Nivel 3: Componentes de Booking API (N-Layer & Worker)](04_booking_components.md)
* [C4 Nivel 3: Componentes de Payment gRPC (Kestrel HTTP/2)](05_payment_components.md)
