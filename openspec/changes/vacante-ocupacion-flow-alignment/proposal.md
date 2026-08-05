# Propuesta: vacante-ocupacion-flow-alignment

## Why

Hoy el modelo permite estados incoherentes: un **Puesto con Ocupación vigente y Vacante Abierta simultáneamente**. Las entidades `Vacante` y `Ocupacion` viven independientes, sin FK cruzada, y los servicios de aplicación no se validan entre sí. Esto rompe la regla de negocio que un Puesto ocupado no debería tener vacante abierta, y que toda Ocupación debería nacer de una Vacante Cubierta. El gap se hizo visible al intentar conectar el flujo Puesto → Vacante → Ocupación antes de construir el módulo de Selección (Postulaciones).

## What Changes

### Cambios en dominio

- `src/SGV.Dominio/Ocupaciones/Ocupacion.cs`: agregar propiedad `VacanteId` nullable (Guid?), con navegación a `Vacante`. Constructor y `Reconstitute` ajustados.

### Cambios en aplicación

- `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs`:
  - **N1**: `CrearAsync` agrega check `ExistsActiveOcupacionByPuestoAsync(PuestoId)` → 409 `PuestoOcupado` si existe Ocupación activa para el mismo Puesto.
  - **N2**: `CambiarEstadoAsync` al transición a `Cubierta` recibe `PersonaId` (de Postulación ganadora) y crea automáticamente la `Ocupacion` con `VacanteId` seteado y `EsVigente = true`.

- `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs`:
  - **N3**: `CrearAsync` agrega check `ExistsAbiertaByPuestoAsync(PuestoId)` → 409 `PuestoSinVacanteAbierta` si no existe Vacante abierta para el Puesto.
  - **Q2**: `ReactivarAsync` agrega check de Vacante no Cancelada → 409 si la Vacante asociada está Cancelada.

### Cambios en infraestructura

- Nueva migración EF `AddVacanteIdToOcupaciones`: agrega columna `VacanteId` nullable a `Ocupaciones` con FK a `Vacantes`. `ON DELETE RESTRICT`. Índice único existente sobre `PuestoId` activo se mantiene — `NULL` en `VacanteId` no rompe la unicidad (MySQL permite múltiples `NULL` en unique index).
- Las Ocupaciones históricas quedan con `VacanteId = NULL` (no se migran datos).
- `VacanteServicioComandos` necesita inyectar `IOcupacionRepository` para ejecutar N2 (cruce de frontera de servicio).

### Cambios en specs

- `openspec/specs/vacante-management/spec.md`: actualizar requisito `Crear Vacante` con escenario N1; agregar requisito `CambiarEstado` con escenario N2.
- `openspec/specs/web-ocupaciones-crear-editar/spec.md`: actualizar para reflejar N3 y Q2.
- `openspec/specs/web-ocupaciones-navegacion-contextual/spec.md`: ajustar navegación entre Vacante y Ocupacion.

### Cambios en tests

- `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` (línea 47-64): adaptar test de `CrearAsync` directo para esperar 409 `PuestoSinVacanteAbierta` (N3).
- Nuevos tests para N1, N2 y Q2 en los archivos de test correspondientes de `VacanteServicioComandos` y `OcupacionServicioComandos`.

## Impact

| Área | Impacto | Descripción |
|------|---------|-------------|
| `VacanteServicioComandos` | Modificado | N1 (rechazo por Ocupación activa) + N2 (crear Ocupación al Cubrir) |
| `OcupacionServicioComandos` | Modificado | N3 (rechazo sin Vacante abierta) + Q2 (chequeo de Vacante Cancelada en Reactivar) |
| `Ocupacion` (dominio) | Modificada | Nueva propiedad `VacanteId` nullable |
| `IOcupacionRepository` | Modificado | Nuevo método para `Create` usado por N2 |
| API endpoints | Afectados | `POST /api/v1/vacantes`, `POST /api/v1/ocupaciones`, `PATCH /api/v1/vacantes/{id}/estado` |
| Migración BD | Nueva | `AddVacanteIdToOcupaciones` |
| Roles de autorización | Sin cambio | `Administrador` y `GestorVacantes` siguen siendo los actores de mutación |
| Compatibilidad | Respetada | Ocupaciones históricas con `VacanteId = NULL`; no se rompen queries existentes |

## Non-Goals

- **Módulo de Selección (Postulaciones)**: las Postulaciones son un change separado. N2 consume `PersonaId` de una Postulación, pero Selección no se implementa aquí.
- **Backfill de `VacanteId` en Ocupaciones históricas**: las Ocupaciones pre-existente quedan con `VacanteId = NULL` intencionalmente.
- **Reporte de Ocupaciones sin `VacanteId`**: no se construye en este change.
- **Cancelación masiva de Vacantes**: fuera de scope.
- **Workflow de aprobaciones**: el modelo de roles se mantiene plano (`Administrador` / `GestorVacantes`).

## Acceptance Criteria

- [ ] **N1 — Rechazo por Ocupación activa**: DADO un Puesto con Ocupación activa, CUANDO se invoca `CrearVacante` para ese Puesto, ENTONCES la API responde `409 Conflict` con código `PuestoOcupado`.
- [ ] **N3 — Rechazo sin Vacante abierta**: DADO un Puesto sin Vacante abierta, CUANDO se invoca `CrearOcupacion` para ese Puesto, ENTONCES la API responde `409 Conflict` con código `PuestoSinVacanteAbierta`.
- [ ] **N2 — Cubrir crea Ocupación**: DADO una Vacante Abierta con una Postulación ganadora que tiene `PersonaId`, CUANDO se transiciona la Vacante a `Cubierta`, ENTONCES se crea automáticamente una Ocupacion con `VacanteId` seteado y `EsVigente == true`.
- [ ] **Q2 — Reactivación rechaza si Vacante Cancelada**: DADO una Ocupacion cuya Vacante asociada fue Cancelada, CUANDO se invoca `ReactivarOcupacion`, ENTONCES la API responde `409 Conflict`.
- [ ] **Q1 — Finalizar no reopen**: DADO una Vacante Cubierta con Ocupacion derived, CUANDO se invoca `FinalizarOcupacion`, ENTONCES la Vacante permanece `Cubierta` (no se reabre).
- [ ] **Migración idempotente**: la migración `AddVacanteIdToOcupaciones` se aplica sin errores en DB limpia y en DB con Ocupaciones pre-existentes.
- [ ] **Constraint único preservado**: el índice único existente sobre `PuestoId` activo no se rompe con la nueva columna nullable.
- [ ] **Tests adaptados pasan**: `OcupacionServicioComandosTests` línea 47-64 pasa con el nuevo comportamiento N3; todos los tests existentes siguen verdes.

## Risks

| Riesgo | Likelihood | Mitigation |
|--------|------------|------------|
| Test de `CrearOcupacion` directo (línea 47-64) necesita adaptación | Alta | El test existente se modifica para esperar `409 PuestoSinVacanteAbierta`; no requiere nuevo archivo. |
| Cruce de frontera de servicio: `VacanteServicioComandos` necesita `IOcupacionRepository` para N2 | Media | `IOcupacionRepository` ya existe; se inyecta como dependencia adicional. Solo afecta la composición en el controller/host. |
| N2 no es testeable hasta que exista Selección (Postulaciones) | Media | Se escribe test unitario aislado con mock de `IPostulacionRepository` o se posterga el test de integración N2 hasta que Selección exista; el código N2 se implementa completo. |
| Ocupaciones históricas quedan con `VacanteId = NULL` | Baja | Es intencional; queries que cruzan Vacante ↔ Ocupacion deben handlear `NULL`. |
| MySQL unique index con `NULL` múltiple | Baja | MySQL (InnoSQL) trata múltiples `NULL` como distintos en unique index; no rompe el constraint existente. |

## Open Questions

**Ninguna.** Las 5 preguntas originadas en la exploración (#1706) fueron resueltas en las decisiones (#1707): N1=A, N2=A, N3=A, N4=A, Q1=NO, Q2=SÍ, Q3=NO, Q4=independiente, Q5=N3 absoluto. El change está listo para la fase de specs.
