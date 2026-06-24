## MODIFIED Requirements

### Requirement: Historial de Ocupaciones
El sistema DEBE persistir ocupaciones históricas de persona a puesto con fecha de inicio, fecha de finalización y tipo de asignación controlado. Una Persona DEBE poder mantener múltiples ocupaciones activas simultáneamente cuando correspondan a Puestos distintos. El sistema DEBE conservar una sola ocupación activa por Puesto y DEBE impedir más de una ocupación activa para la misma combinación Persona + Puesto.

#### Scenario: Ocupación vigente de un puesto
- **WHEN** una ocupación no tiene fecha de finalización
- **THEN** esa ocupación DEBE representar el ocupante vigente del Puesto

#### Scenario: Ocupaciones concurrentes en puestos distintos
- **WHEN** una misma Persona registra dos ocupaciones activas en Puestos diferentes
- **THEN** el sistema DEBE permitir ambas ocupaciones simultáneamente

#### Scenario: Duplicado activo por persona y puesto
- **WHEN** una misma Persona intenta registrar más de una ocupación activa para el mismo Puesto
- **THEN** el sistema DEBE rechazar la segunda ocupación activa

#### Scenario: Duplicado activo por puesto
- **WHEN** dos Personas distintas intentan mantener ocupaciones activas sobre el mismo Puesto
- **THEN** el sistema DEBE rechazar la segunda ocupación activa

#### Scenario: Ocupación anterior
- **WHEN** una ocupación tiene fecha de finalización y se consulta el historial del Puesto para una fecha dentro de su rango
- **THEN** el sistema DEBE identificar la Persona que ocupaba el Puesto en ese momento

## ADDED Requirements

### Requirement: Tipo de asignación enumerado en ocupaciones
El sistema DEBE persistir `TipoAsignacion` como un valor enumerado controlado y DEBE almacenarlo en base de datos usando el valor numérico fijo del enum. La primera versión DEBE soportar exactamente `Permanente`, `Interina` y `Temporal`, y los valores numéricos asociados DEBEN permanecer estables para no corromper datos persistidos.

#### Scenario: Persistencia numérica del tipo de asignación
- **WHEN** se persiste una ocupación con un `TipoAsignacion` válido
- **THEN** la base de datos DEBE almacenar el valor numérico correspondiente del enum y no una representación textual

#### Scenario: Migración de valores textuales conocidos
- **WHEN** existen registros históricos con `TipoAsignacion` textual igual a `Permanente`, `Interina` o `Temporal`
- **THEN** la migración DEBE convertir cada valor al número correspondiente del enum sin perder semántica

#### Scenario: Valor legacy desconocido
- **WHEN** la migración encuentra un valor histórico de `TipoAsignacion` fuera del conjunto soportado
- **THEN** el sistema DEBE fallar explícitamente o requerir normalización intencional antes de completar la migración
