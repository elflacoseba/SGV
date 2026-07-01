# Apply Progress — PR 1 (Backend cargos)

> Change: `2026-06-30-cargos-crear-editar-codigo-editable`
> Phase: `sdd-apply` (PR 1 — Backend)
> Strict TDD: ACTIVO (RED → GREEN → REFACTOR por tarea)
> Branch: `feat/cargos-crear-editar-codigo-editable-pr1`
> Base: `develop` @ `55d1b967`

---

## Estado por tarea

### Fase 1.1 — Dominio

- [x] **PR-1.1 RED: tests de dominio para cambio de `Codigo`**
- [x] **PR-1.2 GREEN: `Cargo.Actualizar` acepta `codigo`**

### Fase 1.2 — Aplicación

- [x] **PR-1.3 RED: tests del validator para `Codigo` en update**
- [x] **PR-1.4 GREEN: extender `ActualizarCargoRequest` + validator**
- [x] **PR-1.5 RED: tests de servicio para unicidad activa en update**
- [x] **PR-1.6 GREEN: `CargoServicioComandos.ActualizarAsync` con unicidad activa + helper compartido**

### Fase 1.3 — API

- [x] **PR-1.7 RED: tests de API para `PUT` con `codigo` (400/409)**
- [x] **PR-1.8 GREEN: actualizar XML doc y contrato de `PUT`**

### Fase 1.4 — Infraestructura y verificación

- [x] **PR-1.9 Verificar índice único cubre update**
- [x] **PR-1.10 VERIFY: build + suite Cargo**
- [x] **PR-1.11 Regenerar `docs/migracion-inicial-sgv.sql` (condicional — no aplica)**

---

## Hallazgos durante implementación

- **Convenio `Cargo.Codigo`**: se mantiene `private set` en la propiedad; la mutabilidad se realiza solo desde dentro de la entidad vía `Actualizar(string codigo, ...)`. Esto preserva la encapsulación (no se abre un setter público) y elimina la prueba `Codigo_EsInmutableTrasCreacion` que solo verificaba la falta de setter público. La invariante real ahora es "el código puede cambiar pero siempre bajo reglas de shape y con pre-check externo de unicidad".
- **`ActualizarCargoRequest` cambia firma → breaking change contractual**: el campo `Codigo` ahora es obligatorio. Esto afecta al endpoint `PUT /api/v1/cargos/{id}`. Los tests existentes que enviaban PUT sin codigo se actualizaron para incluirlo. El design y los riesgos del change ya advertían sobre esto.
- **Helper compartido `EnsureCodigoNoDuplicadoAsync`**: se usa en `CrearAsync` (`excludingId: null`) y `ActualizarAsync` (`excludingId: id`). Elimina duplicación y centraliza el mensaje de error `CodigoDuplicado`.
- **`NullConstraintViolationDetector` interno en `SGV.Aplicacion`**: clase sellada singleton usada por el constructor de conveniencia (3-arg) para mantener compatibilidad con tests legacy que no inyectan detector. No se expone al DI; la registración real usa `MySqlConstraintViolationDetector`.
- **Seguridad ante race condition**: el pre-check es UX, el índice `IX_Cargos_ActiveCodigoUnique` es el árbitro final. `DbUpdateException` (código MySQL 1062) se traduce a `Conflict "CodigoDuplicado"` con código HTTP 409 tanto en create como en update.
- **Test `Actualizar_CodigoDuplicado_RaceCondition_DevuelveConflicto`**: usa un nuevo `FakeThrowingUnitOfWork` (específico de cargo, no toca el `FakeUnitOfWork` compartido del namespace `Organizacion`) y un `FakeConstraintViolationDetector` (siempre retorna `true`).

---

## Decisiones técnicas del PR 1

- `Cargo.Codigo` mantiene `private set` (no se cambia a `public set`); la mutabilidad se permite solo desde dentro de la entidad vía `Actualizar(string codigo, ...)`. Esto mantiene la encapsulación y elimina la prueba de inmutabilidad pública.
- `ActualizarCargoRequest` agrega `Codigo` como **primer parámetro** posicional del record; esto es un breaking change contractual para `PUT /api/v1/cargos/{id}`. El design y los riesgos lo documentan.
- `CargoServicioComandos` adopta el patrón de `OcupacionServicioComandos`: agrega `IConstraintViolationDetector` como dependencia y captura `DbUpdateException` para mapear violaciones del índice único activo a `Conflict "CodigoDuplicado"` con código HTTP 409.
- Helper privado compartido `EnsureCodigoNoDuplicadoAsync(string codigo, Guid? excludingId, CancellationToken)` reutilizado por `CrearAsync` (excludingId: null) y `ActualizarAsync` (excludingId: id).
- El índice `IX_Cargos_ActiveCodigoUnique` (columna computada `CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`) sigue cubriendo `update` sin migración nueva. La columna generada se recalcula automáticamente cuando `Codigo` o `IsDeleted` cambian.

---

## Resultado final del PR 1

### Commits planificados (work units)

1. `docs(apply): track PR1 progress` — `openspec/changes/.../apply-progress.md`
2. `feat(cargos): allow Codigo update in Cargo domain entity` — `src/SGV.Dominio/Organizacion/Cargo.cs`, `tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs`, `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`
3. `feat(cargos): require and validate Codigo in ActualizarCargoRequest` — `src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs`, `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs`, `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarCargoRequestValidatorTests.cs`, `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs` (signature fixes)
4. `feat(cargos): enforce active uniqueness on Cargo update with index safety net` — `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`, `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs` (new tests + helpers)
5. `docs(cargos): document PUT updates Codigo and update API tests` — `src/SGV.Api/Controllers/CargosController.cs`, `tests/SGV.Tests/Api/CargosControllerTests.cs`

### Resumen de archivos tocados

- **Producción**: 4 archivos (`Cargo.cs`, `CargoRequests.cs`, `ActualizarCargoRequestValidator.cs`, `CargoServicioComandos.cs`, `CargosController.cs` = 5)
- **Tests**: 5 archivos (`CargoTests.cs`, `ActualizarCargoRequestValidatorTests.cs`, `CargoServicioComandosTests.cs`, `CargoRepositoryTests.cs`, `CargosControllerTests.cs`)
- **Documentación**: 1 archivo (`apply-progress.md`)

### Tests ejecutados al cierre

- Dominio (Organización + resto): 152 passed / 0 failed
- Aplicación: 409 passed / 0 failed
- API: 202 passed / 0 failed
- Web: 88 passed / 0 failed
- Tests con `[MySqlFact]` se skipean limpio en entorno sin MySQL (issue #59 conocido, sin relación con este change)

### Riesgo residual / hand-off al orchestrator

- **Breaking change contractual**: `PUT /api/v1/cargos/{id}` ahora requiere `codigo`. Documentar en el cuerpo del PR1 y avisar a consumidores externos si los hubiera (el design asume que no los hay dentro del repo).
- **Migración nueva**: NO se requiere (índice `IX_Cargos_ActiveCodigoUnique` cubre update).
- **Siguiente paso del orchestrator**: regenerar `docs/migracion-inicial-sgv.sql` (no aplica en este PR; queda para confirmar en archive que no se omitió).
- **No tocar** para PR2A/PR2B: archivos web (`src/SGV.Web/**`), `_Sidenav`, partials, `ICargoApiClient`, `FakeCargoApiClient`. Esos viven en PR2A/PR2B y dependen de que PR1 esté mergeado.