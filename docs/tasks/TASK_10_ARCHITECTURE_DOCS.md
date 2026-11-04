# 📋 Especificación Técnica: Tarea 10
**Rama Git:** `feature/10-architecture-docs`  
**Módulo:** `docs/`  
**Tipo:** Arquitectura de Software y Dossier de Entrega  

## 1. Alcance y Responsabilidad
Diseñar los diagramas de arquitectura de software para cada uno de los microservicios, documentar la frontera de consistencia eventual y compilar la memoria de prompts de la entrega.

## 2. Requisitos de Cátedra
- **Requisito 3.i:** Realizar un diagrama de arquitectura de software de CADA API.
- **Requisito 3.g:** Señalar en el diagrama dónde el sistema se vuelve eventualmente consistente.
- **Requisito 1:** Completar y justificar la tabla de componentes (ISO/IEC/IEEE 42010).
- **Requisito 2:** Historial de prompts de los integrantes del grupo (`docs/prompts_memory.md`).

## 3. Entregables Concretos
1. **Diagrama de Availability API:** Diagrama de capas mostrando Clean Architecture (Entities -> UseCases -> Controllers/Gateways).
2. **Diagramas de Booking y Payment:** Diagramas N-Layer (Presentation -> Business -> Data Access).
3. **Diagrama General del Sistema:** Diagrama de despliegue con la frontera AP (Eventual Consistency) marcada explícitamente entre Availability y Broker.
4. **Archivo `docs/prompts_memory.md`:** Consolidación de los prompts utilizados por cada integrante para cumplir el Punto 2.

## 4. Criterios de Aceptación (DoD)
- [ ] Existen los diagramas específicos para cada una de las APIs en formato legible (`.png` o Mermaid en Markdown).
- [ ] La tabla de componentes y el punto de consistencia eventual están claramente explicitados en la documentación.

## 5. Defensa Oral
- **¿Qué diferencia conceptual hay entre estilo y patrón en la entrega?**  
  *Respuesta:* El estilo arquitectónico es Microservicios (sistemas desacoplados); los patrones son Clean Architecture y N-Layer (organización interna del código de cada API).
