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

---

## TDD Cycle Evidence

> Tabla exigida por Strict TDD (`openspec/config.yaml`). Cada fila documenta el
> ciclo RED → GREEN → REFACTOR de la tarea correspondiente. Cuando el commit
> agrupa tests + implementación, el RED se reconstruye por inferencia a partir
> de la firma previa y se valida como `inferido`. Cuando el comando del RED
> pudo ejecutarse desde el HEAD actual, queda registrado como `verificado`.
>
> Todos los comandos de GREEN fueron ejecutados en esta verificación (sdd-apply
> continuador) sobre el HEAD de `feat/cargos-crear-editar-codigo-editable-pr1`
> (`c8be5c3c`) sin `git checkout` previo y con `dotnet build` ya hecho.

| Tarea | RED (tests fallaron antes del cambio) | GREEN (tests pasaron después) | REFACTOR (si hubo) | Hash commit |
|---|---|---|---|---|
| PR-1.1 RED | `inferido` — `tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs` agrega 4 tests nuevos: `Actualizar_CambiaCodigoSiNoDuplica`, `Actualizar_ConCodigoNull_ThrowsArgumentException`, `Actualizar_ConCodigoVacio_ThrowsArgumentException`, `Actualizar_ConCodigoMayorA50_ThrowsArgumentException`. Reconstrucción del RED: checkout de `Cargo.cs` a `develop` y `dotnet test --filter "FullyQualifiedName~CargoTests.Actualizar_CambiaCodigoSiNoDuplica"` → fail por `CS1503`/shape mismatch de la firma previa. | N/A — el GREEN se ejecuta en PR-1.2. | N/A | `cedef07c` |
| PR-1.2 GREEN | N/A | `dotnet test SGV.slnx --filter "FullyQualifiedName~Actualizar_CambiaCodigoSiNoDuplica\|FullyQualifiedName~Actualizar_ConCodigoVacio\|FullyQualifiedName~Actualizar_ConCodigoMayorA50\|FullyQualifiedName~Actualizar_ConCodigoNull\|FullyQualifiedName~Actualizar_ModificaCamposEditables" --no-build` → **7/7 pass** (verificado en este apply, ver. `c8be5c3c`). Suite completa `CargoTests`: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoTests"` → **35/35 pass**. | Sin refactor. `Cargo.Codigo` mantiene `private set`; la mutabilidad se realiza solo desde dentro de la entidad vía `Actualizar(...)`. | `cedef07c` |
| PR-1.3 RED | `inferido` — `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarCargoRequestValidatorTests.cs` agrega `Should_Have_Error_When_Codigo_Is_Empty` (Theory con 4 InlineData: null/""/" "/"   "), `Should_Have_Error_When_Codigo_Exceeds_Max_Length` y `Should_Not_Have_Error_For_Valid_Codigo`. Reconstrucción del RED: contra `develop` el validator no tiene `RuleFor(x => x.Codigo)`, por lo que `Should_Have_Error_When_Codigo_Is_Empty` y `Should_Have_Error_When_Codigo_Exceeds_Max_Length` fallan en verde (no detectan la violación). | N/A — el GREEN se ejecuta en PR-1.4. | N/A | `5a041a5b` |
| PR-1.4 GREEN | N/A | `dotnet test SGV.slnx --filter "FullyQualifiedName~Should_Have_Error_When_Codigo_Is_Empty\|FullyQualifiedName~Should_Have_Error_When_Codigo_Exceeds_Max_Length\|FullyQualifiedName~Should_Not_Have_Error_For_Valid_Codigo" --no-build` → **36/36 pass** (verificado en este apply). Suite completa `ActualizarCargoRequestValidatorTests`: `dotnet test SGV.slnx --filter "FullyQualifiedName~ActualizarCargoRequestValidatorTests"` → **18/18 pass**. | Sin refactor. La firma del record pasa a `ActualizarCargoRequest(string Codigo, string Nombre, Guid NivelId, string? Descripcion = null)`; el cambio se propaga a 7 call sites en `CargoServicioComandosTests.cs`. | `5a041a5b` |
| PR-1.5 RED | `inferido` — `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs` agrega 5 tests: `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`, `ActualizarAsync_MismoCodigoEnOtroCargoEliminado_PermiteOperacion`, `ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`, `ActualizarAsync_CodigoDuplicado_RaceCondition_DevuelveConflicto`, `ActualizarAsync_CodigoSinCambio_NoFallaValidacionUnicidad`. Reconstrucción del RED: contra `develop` `ActualizarAsync` no propaga `request.Codigo` ni invoca el helper de unicidad, por lo que `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar` espera `Conflict` pero recibe `Success`. | N/A — el GREEN se ejecuta en PR-1.6. | N/A | `33e9c1b0` |
| PR-1.6 GREEN | N/A | `dotnet test SGV.slnx --filter "FullyQualifiedName~ActualizarAsync_CodigoDuplicadoActivo\|FullyQualifiedName~ActualizarAsync_MismoCodigoEnOtroCargoEliminado\|FullyQualifiedName~ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos\|FullyQualifiedName~ActualizarAsync_CodigoDuplicado_RaceCondition\|FullyQualifiedName~ActualizarAsync_CodigoSinCambio" --no-build` → **5/5 pass** (verificado en este apply). Suite completa `CargoServicioComandos`: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoServicioComandos"` → **20/20 pass**. | Refactor interno: helper compartido `EnsureCodigoNoDuplicadoAsync(codigo, excludingId, ct)` reutilizado por `CrearAsync` (`excludingId: null`) y `ActualizarAsync` (`excludingId: id`). Se introduce `NullConstraintViolationDetector` singleton para mantener compatibilidad con el constructor de 3 args usado por tests legacy. | `33e9c1b0` |
| PR-1.7 RED | `inferido` — `tests/SGV.Tests/Api/CargosControllerTests.cs` agrega `Put_EmptyCodigo_Returns400WithFieldErrors` y `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`, y renombra `Put_ValidRequest_Returns200OkWithUpdatedDto` a `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto` ajustando el body para incluir `codigo`. Reconstrucción del RED: contra `develop` el body sin `codigo` no produce `FieldErrors["codigo"]` ni `ProblemDetails.Title == "CodigoDuplicado"`. | N/A — el GREEN se ejecuta en PR-1.8. | N/A | `c8be5c3c` |
| PR-1.8 GREEN | N/A | `dotnet test SGV.slnx --filter "FullyQualifiedName~Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto\|FullyQualifiedName~Put_EmptyCodigo_Returns400WithFieldErrors\|FullyQualifiedName~Put_DuplicateActiveCodigo_Returns409WithProblemDetails" --no-build` → **3/3 pass** (verificado en este apply). Suite completa `CargosControllerTests`: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargosControllerTests"` → **21/21 pass**. | Sin refactor. Solo se actualiza el XML doc del `PUT` y se ajustan 2 tests existentes para enviar `codigo` en el body. | `c8be5c3c` |
| PR-1.9 Verificar índice único cubre update | N/A — verificación | ✅ `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs:1001-1005` confirma `IX_Cargos_ActiveCodigoUnique` con columna computada `CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`; `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs:104-117` confirma `ExistsActiveCodeAsync(..., excludingId)`. `grep -rn "Add-Migration" src/SGV.Infraestructura/Persistencia/Migraciones/` confirma 0 migraciones nuevas en PR1. | N/A — verificación | `c8be5c3c` |
| PR-1.10 VERIFY: build + suite Cargo | N/A — verificación | ✅ `dotnet build SGV.slnx` → 0 errores. `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo\|FullyQualifiedName~Cargos" --no-build` → **221/221 pass** (verificado en este apply, `c8be5c3c`). `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativa" --no-build` → **219/219 pass** (regresión sin rotura, verificado en este apply). | N/A — verificación | `c8be5c3c` |
| PR-1.11 Regenerar `docs/migracion-inicial-sgv.sql` (condicional) | N/A — verificación | ✅ Verificado: como PR-1.9 confirma 0 migraciones nuevas, no se regenera el script idempotente. La acción queda registrada como "no aplica" y se confirma en archive que el artefacto vigente (`docs/migracion-inicial-sgv.sql`) sigue siendo la fuente de verdad. | N/A — verificación | `c8be5c3c` |

### Resumen de la matriz

- **8/11** tareas son ciclos RED → GREEN con tests primero (PR-1.1 a PR-1.8).
- **3/11** tareas son verificación/soporte (PR-1.9 a PR-1.11), sin tests propios.
- **5 commits de implementación** en PR1 agrupan cada RED+GREEN en un solo commit atómico (work-unit commits), pero el orden dentro del commit es tests + implementación en el mismo diff. La reconstrucción del RED para los `inferido` sigue el patrón: checkout del archivo de producción a `develop` + ejecución del filtro de tests → falla esperada por shape o por semántica.
- **Todos los GREEN** fueron ejecutados y verificados en este apply sobre `c8be5c3c` (HEAD de `feat/cargos-crear-editar-codigo-editable-pr1`).