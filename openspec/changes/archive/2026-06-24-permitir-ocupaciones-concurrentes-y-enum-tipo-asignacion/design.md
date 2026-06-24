## Context

Hoy `SGV.Dominio.Ocupaciones.Ocupacion` modela `TipoAsignacion` como `string` libre y `SGV.Infraestructura.Persistencia.Configuraciones.OcupacionConfiguracion` impone unicidad activa por `PuestoId` y también por `PersonaId`. Esa combinación bloquea escenarios válidos donde una misma Persona ocupa simultáneamente varios Puestos distintos. El cambio debe preservar la fuente de verdad histórica de Ocupaciones, seguir siendo compatible con Pomelo/MySQL y no introducir todavía jerarquía entre ocupaciones concurrentes.

Los puntos afectados atraviesan múltiples capas:

- `src/SGV.Dominio/Ocupaciones/Ocupacion.cs`
- `src/SGV.Infraestructura/Persistencia/Entidades/OcupacionEntity.cs`
- `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs`
- migraciones y snapshot de `SgvDbContext`
- pruebas de dominio y persistencia del modelo relacional

## Goals / Non-Goals

**Goals:**
- Permitir varias Ocupaciones activas por Persona cuando correspondan a Puestos distintos.
- Mantener una sola Ocupación activa por Puesto.
- Mantener una sola Ocupación activa por combinación Persona + Puesto.
- Convertir `TipoAsignacion` a un enum controlado con `Permanente`, `Interina` y `Temporal`.
- Persistir `TipoAsignacion` como valor numérico del enum, no como texto.
- Mantener compatibilidad con MySQL/Pomelo usando estrategias de unicidad activas verificables por pruebas.
- Dejar la persistencia lista para migrar datos existentes sin volver a texto libre.

**Non-Goals:**
- No introducir ocupación principal/secundaria.
- No modelar porcentaje de dedicación, carga horaria ni compatibilidad entre ocupaciones concurrentes.
- No crear en esta fase un módulo completo de aplicación/API para administrar Ocupaciones.
- No cambiar la regla de historial (`FechaFin` nula = vigente).

## Decisions

### 1. `TipoAsignacion` pasa a enum y se persiste como entero

**Decision:** crear un enum `TipoAsignacion` en Dominio y usarlo en `Ocupacion` y `OcupacionEntity` en lugar de `string` libre, persistiéndolo como su valor numérico.

**Why:** el conjunto inicial ya está decidido y no debe quedar abierto a variantes tipográficas o semánticas inconsistentes. Además, el usuario definió explícitamente que en base de datos no debe guardarse como texto sino como el número correspondiente a la enumeración.

**How:**
- `Ocupacion` recibirá `TipoAsignacion` en su constructor y lo expondrá como propiedad tipada.
- `OcupacionEntity` también expondrá la propiedad como enum para evitar mapeos manuales innecesarios entre dominio y persistencia.
- EF Core persistirá el enum como entero, manteniendo la columna relacional como tipo numérico.
- La migración deberá transformar los valores textuales existentes (`Permanente`, `Interina`, `Temporal`) a sus equivalentes numéricos definidos por el enum.
- El valor de cada miembro del enum pasa a ser contrato persistido; por eso los miembros deberán tener valores explícitos en código y no depender del orden implícito.

**Alternatives considered:**
- **Persistir el enum como string**: descartado porque el usuario definió que no quiere texto en la tabla y porque eso ata la persistencia a los nombres de los miembros del enum.
- **Mantener string con validaciones**: descartado porque deja abierta la puerta a drift semántico y obliga a repetir validación en más capas.

### 2. La unicidad activa por Persona deja de ser global y pasa a ser por Persona + Puesto

**Decision:** eliminar la restricción de una sola ocupación activa por `PersonaId` y reemplazarla por una restricción de unicidad activa para la combinación `PersonaId + PuestoId`.

**Why:** el negocio ahora admite concurrencia por Persona entre Puestos distintos, pero sigue prohibiendo duplicar la misma asignación activa sobre el mismo Puesto.

**How:**
- Se conserva la unicidad activa por `PuestoId` para garantizar un solo ocupante vigente por Puesto.
- Se reemplaza la columna generada/índice `ActivePersonaIdUnique` por una columna generada textual del estilo `ActivePersonaPuestoUnique` que concatene `PersonaId` y `PuestoId` solo cuando la ocupación esté activa (`FechaFin IS NULL` y `IsDeleted = 0`).
- El índice único se definirá sobre esa nueva columna generada para simular un índice filtrado compatible con MySQL.

**Alternatives considered:**
- **Quitar toda restricción por Persona**: descartado porque permitiría dos ocupaciones activas duplicadas sobre el mismo Puesto para la misma Persona.
- **Índice compuesto directo con `PersonaId`, `PuestoId`, `FechaFin`, `IsDeleted`**: descartado porque no expresa correctamente la condición filtrada de “solo activas” bajo MySQL sin columnas generadas o lógica equivalente.

### 3. La unicidad activa por Puesto se conserva como regla dura del modelo

**Decision:** mantener el equivalente actual de `ActivePuestoIdUnique`.

**Why:** el usuario confirmó concurrencia por Persona en Puestos distintos, no concurrencia de varias Personas activas sobre el mismo Puesto.

**How:**
- La configuración EF seguirá generando una clave activa nula cuando la ocupación no esté vigente o esté soft-deleted.
- Las pruebas del modelo seguirán verificando que exista un índice único para la ocupación activa por Puesto.

**Alternatives considered:**
- **Permitir varias ocupaciones activas por Puesto**: descartado porque cambia el significado central del historial de ocupaciones y requeriría redefinir vacantes, cobertura y ocupante vigente.

### 4. La migración de datos será compatible con valores existentes conocidos y defensiva ante valores desconocidos

**Decision:** la migración convertirá los valores persistidos de `TipoAsignacion` desde texto libre a los valores numéricos del enum y validará que los datos existentes estén dentro del conjunto soportado.

**Why:** el esquema actual usa texto libre; aunque el diseño histórico ya sugería `Permanente`, `Interina`, `Temporal`, una migración segura no debe normalizar silenciosamente valores inesperados.

**How:**
- Si la base ya contiene los literales esperados, la migración mapeará explícitamente `Permanente`, `Interina` y `Temporal` a los enteros fijados para el enum.
- Si aparecen valores fuera del conjunto soportado, la migración deberá fallar explícitamente o normalizarlos de forma intencional y auditada dentro de la propia migración; no se aceptará coerción implícita desde el código de aplicación.
- Las pruebas de modelo cubrirán la nueva representación numérica del enum y el reemplazo de la columna generada de unicidad por Persona.

**Alternatives considered:**
- **Mapear cualquier string desconocido a `Temporal`**: descartado porque corrompe semántica de negocio.
- **Posponer el enum hasta limpiar datos manualmente**: descartado porque deja el modelo inconsistente más tiempo del necesario.

### 5. El cambio se implementa primero en dominio + persistencia + pruebas, sin exponer todavía comandos o endpoints nuevos

**Decision:** mantener el corte de implementación acotado al modelo, persistencia y pruebas.

**Why:** hoy Ocupaciones no tiene un módulo de aplicación/API estabilizado; forzar esa capa en el mismo cambio mezclaría dos problemas diferentes: corregir el modelo y diseñar la gestión operativa del módulo.

**Alternatives considered:**
- **Construir el CRUD completo de Ocupaciones ahora**: descartado porque agranda demasiado el alcance y aumenta el riesgo de fijar contratos API prematuros sobre un modelo recién corregido.

## Risks / Trade-offs

- **[Migración de datos con valores legacy no contemplados]** → Mitigación: inspeccionar/validar valores de `TipoAsignacion` durante la migración y fallar explícitamente ante datos desconocidos.
- **[Cambiar la columna generada puede romper índices o tests existentes]** → Mitigación: cubrir el modelo con tests específicos para la nueva unicidad activa por `Persona + Puesto` y preservar la unicidad activa por `Puesto`.
- **[Persistir enum como entero ata la base a los valores numéricos del enum]** → Mitigación: asignar valores explícitos a cada miembro y tratarlos como contrato persistido, evitando reordenamientos o renumeraciones sin migración explícita.
- **[El dominio sigue sin expresar dedicación o prioridad]** → Mitigación: dejar documentado que la concurrencia de esta fase es equivalente, sin jerarquía ni carga horaria.

## Migration Plan

1. Introducir el enum `TipoAsignacion` y actualizar dominio/persistencia para usarlo.
2. Reemplazar la estrategia de unicidad activa por Persona global con unicidad activa por `Persona + Puesto`.
3. Mantener o recrear la unicidad activa por `Puesto`.
4. Generar migración EF Core/Pomelo y revisar SQL resultante para garantizar compatibilidad MySQL.
5. Ejecutar pruebas de dominio y persistencia que validen:
   - múltiples ocupaciones activas por Persona en Puestos distintos
   - rechazo de duplicado activo para Persona + Puesto
   - rechazo de múltiples ocupaciones activas por Puesto
   - persistencia numérica del enum `TipoAsignacion`
6. Si la migración detecta valores no soportados de `TipoAsignacion`, detener despliegue hasta corregir/normalizar datos.

## Open Questions

- Confirmar durante la implementación si existe ya data real con variantes textuales de `TipoAsignacion` fuera de `Permanente`, `Interina`, `Temporal`.
- Confirmar el tipo numérico final de columna para el enum (`int` o variante menor) según el mapping EF generado y consistencia con el resto del modelo.
