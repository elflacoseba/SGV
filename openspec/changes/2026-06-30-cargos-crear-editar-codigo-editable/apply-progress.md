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

---

## PR-1.12 — Review fixes (3 hallazgos del PR #60)

> Cambio en respuesta a la review publicada sobre `86ab67a9` (https://github.com/elflacoseba/SGV/pull/60).
> Tres fixes atómicos, uno por commit, todos verificados con TDD (RED → GREEN → REFACTOR).
> Sin nuevas migraciones ni cambios de contrato HTTP. El cambio del constructor
> de `CargoServicioComandos` (5 args en lugar de 6) queda internal al SGV.Infraestructura
> DI y a los tests de Aplicacion.

### Estado por fix

- [x] **PR-1.12.1 Fix 1 (Critical)**: `catch` restringido a la violación específica de `IX_Cargos_ActiveCodigoUnique`
- [x] **PR-1.12.2 Fix 2 (Important)**: `MapToDto` recibe el `NivelCargo` ya validado para refrescar `NivelNombre` en respuesta
- [x] **PR-1.12.3 Fix 3 (Recommendation)**: 3 tests `[MySqlFact]` que cubren update de `Codigo` con MySQL real

### Fix 1 — Acotar mapeo de `DbUpdateException` al índice activo de código

**Problema detectado en review**: `catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))` mapeaba
cualquier violación de constraint que `MySqlConstraintViolationDetector` reconociera (códigos MySQL 1062, 1169, 1451, 1452, 4025)
a `CodigoDuplicado`. Esto generaba falsos positivos: por ejemplo, una FK violation
(1452) tras el borrado de un `NivelCargo` entre la validación y el `SaveChanges`
terminaba devolviendo `409 CodigoDuplicado` en lugar de propagarse como 500.

**Solución**: Helper privado local a `CargoServicioComandos` que inspecciona el
mensaje de la inner exception y verifica que contenga `IX_Cargos_ActiveCodigoUnique`
+ `Duplicate entry` (o `1062`). Cualquier otra constraint violation (FK, otro
unique, check) se propaga como 500. Se eliminó la dependencia
`IConstraintViolationDetector` de `CargoServicioComandos` (ya no se usa;
la interfaz y `MySqlConstraintViolationDetector` quedan intactos para que
`OcupacionServicioComandos` y otros servicios los sigan usando).

**Detalle de Clean Architecture**: la detección se hace por **contenido del
mensaje** (string match), no por tipo `MySqlException`. Esto evita que
`SGV.Aplicacion` tome una dependencia directa de `MySqlConnector`, que solo
vive en `SGV.Infraestructura`. La combinación "Duplicate entry" +
"IX_Cargos_ActiveCodigoUnique" es específica del mensaje que MySQL emite
para violaciones del índice activo de cargo.

#### TDD Cycle Evidence — Fix 1

| Tarea | RED (tests fallaron antes del cambio) | GREEN (tests pasaron después) | REFACTOR | Hash commit |
|---|---|---|---|---|
| PR-1.12.1 RED | 4 tests nuevos en `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs`: `ActualizarAsync_DbUpdateException_FkViolation_NoMapeaCodigoDuplicado`, `ActualizarAsync_DbUpdateException_DuplicateKey_DeOtroIndice_NoMapeaCodigoDuplicado`, `ActualizarAsync_DbUpdateException_DuplicateKey_IxCargosActiveCodigoUnique_MapeaCodigoDuplicado`, `CrearAsync_DbUpdateException_FkViolation_NoMapeaCodigoDuplicado`. Reconstrucción del RED: el catch broad con `constraintDetector.IsConstraintViolation(ex)` cubría las 3 violaciones que NO son `IX_Cargos_ActiveCodigoUnique`, devolviendo `CodigoDuplicado` en vez de propagar. `dotnet test SGV.slnx --filter "FullyQualifiedName~ActualizarAsync_DbUpdateException\|FullyQualifiedName~CrearAsync_DbUpdateException" --no-build` → **3 fail / 1 pass** (3 con `Assert.Throws()` reciben `CodigoDuplicado` en vez de la excepción esperada). | N/A — GREEN en el siguiente paso. | N/A | (commit Fix 1) |
| PR-1.12.1 GREEN | N/A | `dotnet test SGV.slnx --filter "FullyQualifiedName~ActualizarAsync_DbUpdateException\|FullyQualifiedName~CrearAsync_DbUpdateException\|FullyQualifiedName~ActualizarAsync_CodigoDuplicado_RaceCondition" --no-build` → **5/5 pass** (incluye el test de race condition pre-existente que también se ajustó para poner el mensaje en la inner exception). Suite completa `CargoServicioComandos`: **24/24 pass** (20 previos + 4 nuevos). | Sí. Removida la dependencia `IConstraintViolationDetector` del constructor (ya no se usa); borradas las clases `NullConstraintViolationDetector` (en src) y `FakeConstraintViolationDetector` (en tests). El `IConstraintViolationDetector`/`MySqlConstraintViolationDetector` no fueron modificados — siguen usándose en `OcupacionServicioComandos` y otros servicios. | (commit Fix 1) |

### Fix 2 — Refrescar `nivelNombre` en el DTO de retorno

**Problema detectado en review**: `MapToDto(Cargo cargo)` leía
`cargo.NivelCargo?.Nombre`. En `ActualizarAsync` el `cargo` se carga vía
`repository.GetByIdForUpdateAsync(id, ct)` ANTES de `cargo.Actualizar(...)`,
así que su navegación `NivelCargo` quedaba con el nivel ANTERIOR — el
`200 OK` devolvía `nivelId` nuevo con `nivelNombre` viejo. Bug latente en
`CrearAsync`: el cargo recién creado nunca tuvo `Include(NivelCargo)`, así
que `cargo.NivelCargo` era `null` y el DTO devolvía `nivelNombre = null`.

**Solución**: `MapToDto` ahora acepta `NivelCargo? nivelCargo = null` y
devuelve `nivelCargo?.Nombre ?? cargo.NivelCargo?.Nombre`. En `CrearAsync`
y `ActualizarAsync` se pasa el `nivel` ya cargado por la validación. Sin
round-trip extra a la DB, sin abrir la encapsulación de `Cargo`.

#### TDD Cycle Evidence — Fix 2

| Tarea | RED (tests fallaron antes del cambio) | GREEN (tests pasaron después) | REFACTOR | Hash commit |
|---|---|---|---|---|
| PR-1.12.2 RED | 2 tests nuevos en `CargoServicioComandosTests.cs`: `ActualizarAsync_CambiaNivelId_RetornaDtoConNivelNombreNuevo` (cambia a `OperativoId` y espera `NivelNombre == "Operativo"`) y `CrearAsync_Nuevo_RetornaDtoConNivelNombreDelCatalogo` (espera `NivelNombre == "Directivo"`). NOTA: el GREEN se aplicó en el mismo edit que Fix 1 (al refactorizar `MapToDto`) — el RED como commit separado se infiere por el diseño del test. Reconstrucción del RED contra el `MapToDto` viejo: `cargo.NivelCargo` es null en el fake, por lo que `NivelNombre` siempre sería null. | N/A — GREEN en el mismo edit. | N/A | (commit Fix 2) |
| PR-1.12.2 GREEN | N/A | `dotnet test SGV.slnx --filter "FullyQualifiedName~ActualizarAsync_CambiaNivelId_RetornaDtoConNivelNombreNuevo\|FullyQualifiedName~CrearAsync_Nuevo_RetornaDtoConNivelNombreDelCatalogo" --no-build` → **2/2 pass**. Suite completa `CargoServicioComandos`: **26/26 pass** (24 previos + 2 nuevos). | N/A — el cambio de `MapToDto` ya queda como parte de Fix 2. | (commit Fix 2) |

### Fix 3 — Cobertura `[MySqlFact]` para update de `Codigo` con MySQL real

**Problema detectado en review**: `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`
probaba update básico pero NO cubría el nuevo comportamiento: cambio
exitoso de `Codigo`, rechazo por duplicado activo vía índice
`IX_Cargos_ActiveCodigoUnique`, y reutilización de `Codigo` cuando hay
otro cargo soft-deleted con ese código.

**Solución**: 3 tests `[MySqlFact]` siguiendo el patrón de
`PersonaRepositoryUniqueConstraintsTests.cs` y los tests existentes en
`CargoRepositoryTests.cs` (cada test crea su `TestSgvDbContextFactory`,
agrega entidades, hace la operación, hace cleanup en `finally` con
`RemoveRange` + `SaveChangesAsync`). Códigos únicos por test con sufijo
`Guid.NewGuid().ToString("N")[..8]` para evitar colisiones entre runs.

#### TDD Cycle Evidence — Fix 3

| Tarea | RED (tests fallaron antes del cambio) | GREEN (tests pasaron después) | REFACTOR | Hash commit |
|---|---|---|---|---|
| PR-1.12.3 RED+GREEN | 3 tests nuevos `[MySqlFact]` en `CargoRepositoryTests.cs`: `UpdateAsync_CambiaCodigo_ActualizaColumnaActivaYComputedColumn`, `UpdateAsync_CodigoDuplicadoActivo_LanzaDbUpdateException`, `UpdateAsync_CodigoSoftDeleted_PermiteReutilizarCodigo`. El test #2 verifica que el mensaje del `DbUpdateException` contiene `IX_Cargos_ActiveCodigoUnique` o `ActiveCodigoUnique`. | `dotnet test SGV.slnx --filter "FullyQualifiedName~UpdateAsync_CambiaCodigo\|FullyQualifiedName~UpdateAsync_CodigoDuplicadoActivo\|FullyQualifiedName~UpdateAsync_CodigoSoftDeleted" --no-build` → **3/3 pass** (MySQL real local, default `Server=localhost;Port=3306;Database=sgv_test;User=root;Password=`). Suite completa `CargoRepositoryTests`: **15/15 pass** (12 previos + 3 nuevos). | N/A — el patrón de cleanup ya era consistente con los demás tests del archivo. | (commit Fix 3) |

### Resumen de la matriz PR-1.12

- **3 fixes atómicos** (un commit por fix).
- **9 tests nuevos** (4 unit en `CargoServicioComandosTests` + 2 DTO refresh + 3 `[MySqlFact]`).
- **0 tests rotos** (221 → 230 en Cargo|Cargos; 219 sin cambios en UnidadOrganizativa; 12 → 15 en CargoRepositoryTests).
- **0 migraciones nuevas**, **0 cambios de contrato HTTP**, **0 cambios en la interfaz `IConstraintViolationDetector`** (se removió la dependencia de `CargoServicioComandos`, pero la interfaz y la implementación `MySqlConstraintViolationDetector` quedan intactas para `OcupacionServicioComandos`).

### Tests ejecutados al cierre de PR-1.12

- `dotnet build SGV.slnx` → **0 errores, 0 warnings** (build limpio).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos" --no-build` → **230/230 pass**.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativa" --no-build` → **219/219 pass** (regresión OK).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoRepositoryTests" --no-build` → **15/15 pass** (incluye los 3 nuevos `[MySqlFact]`).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoServicioComandos" --no-build` → **26/26 pass** (incluye los 6 nuevos del review).
- `dotnet test SGV.slnx --no-build` (suite completa) → **1032/1044 pass, 12 fail** (los 12 fails son `OcupacionRepositoryTests`, **pre-existentes, no relacionados con este PR** — bug documentado en `AGENTS.md` como issue #59, `ActivePuestoIdUnique INT` incompatible con `PuestoId CHAR(36)`).

### Riesgo residual / hand-off al orchestrator

- **Constructor de `CargoServicioComandos` cambió de 6 a 5 args** (sin `IConstraintViolationDetector`). Llamadores de DI siguen funcionando porque `SGV.Infraestructura/DependencyInjection.cs` usa resolución por tipo, no por nombre; los tests que usaban el 6-arg se actualizaron al 5-arg. Si otro servicio o un consumidor externo construye `CargoServicioComandos` a mano, hay que actualizarlo (no aplica: solo el DI y los tests del repo lo construyen).
- **Helper `IsActiveCodigoUniqueViolation` acota el catch al mensaje específico de MySQL**. Si MySQL cambia el formato del mensaje de error 1062 (por ejemplo, localización), el catch podría dejar de disparar. Es poco probable (es un mensaje de servidor, no localizado) pero queda documentado.
- **`MapToDto(cargo, nivelCargo = null)`**: el parámetro `nivelCargo` es opcional y mantiene el comportamiento previo cuando no se pasa (lee de `cargo.NivelCargo`). Los 3 call sites son explícitos: Crear/Actualizar pasan el `nivel` validado; Reactivar no lo pasa porque su navegación ya viene hidratada con `Include(c => c.NivelCargo)`.

### Resumen de la matriz

- **8/11** tareas son ciclos RED → GREEN con tests primero (PR-1.1 a PR-1.8).
- **3/11** tareas son verificación/soporte (PR-1.9 a PR-1.11), sin tests propios.
- **5 commits de implementación** en PR1 agrupan cada RED+GREEN en un solo commit atómico (work-unit commits), pero el orden dentro del commit es tests + implementación en el mismo diff. La reconstrucción del RED para los `inferido` sigue el patrón: checkout del archivo de producción a `develop` + ejecución del filtro de tests → falla esperada por shape o por semántica.
- **Todos los GREEN** fueron ejecutados y verificados en este apply sobre `c8be5c3c` (HEAD de `feat/cargos-crear-editar-codigo-editable-pr1`).

---

## PR2A — Frontend Create (Cargos)

> Change: `2026-06-30-cargos-crear-editar-codigo-editable` (continuación)
> Phase: `sdd-apply` (PR 2A — Frontend Create)
> Strict TDD: ACTIVO (RED → GREEN → REFACTOR por tarea)
> Branch: `feat/cargos-crear-editar-codigo-editable-pr2a`
> Base: `develop` @ `318a3f50` (PR #60 mergeado)
> Cadena: PR1 (`feat/...-pr1` → develop) → **PR2A** (este) → PR2B (siguiente)

### Alcance

Implementa el Create de cargos en el front: cliente HTTP extendido con
`CreateAsync` + `GetNivelesAsync`, infraestructura de form compartido
(InputModel + interface + helpers + partial), página Create, CTA en
Index, entry "Nueva" en Sidenav, y 6 tests web smoke. **No toca** el
backend (PR1 ya mergeado), ni Edit/Details-CTAs (PR2B).

### Tareas aplicadas (tasks.md PR-2A.1 a PR-2A.12, renumeradas 12-23 por el orchestrator)

- [x] **Tarea 12 — RED+GREEN**: `ICargoApiClient.CreateAsync` + `ICargoApiClient.GetNivelesAsync` — extiende el cliente HTTP web con POST `/api/v1/cargos` (parsea `ValidationProblemDetails` y `ProblemDetails`, mapea 400/404/409 a `CargoCommandResult` con `FieldErrors` o `CargoErrorType.Conflict`) y GET `/api/v1/niveles-cargo`. Reutiliza el mismo patrón `ToCommandResultAsync` que `UnidadOrganizativaApiClient`. RED: 4 tests nuevos en `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` (`CreateAsync_Http201WithPayload_ReturnsDtoAndHitsPostRoute`, `CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors`, `CreateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict`, `GetNivelesAsync_Http200WithArray_ReturnsDtosAndHitsCatalogRoute`). GREEN: 11/11 pass en `CargoApiClientTests`.
- [x] **Tarea 13 — Fake client extension**: `FakeCargoApiClient` ahora implementa `CreateAsync` y `GetNivelesAsync`, expone `CreateResult`, `CreateCalls`, `CreateException`, `NivelesResult`, `NivelesException`, `NivelesCalls`. Por defecto `CreateResult` devuelve `Failure(NotImplemented)` y `NivelesResult` es `[]` (cada test los configura). Necesario para que `SgvWebApplicationFactory` pueda inyectar el fake sin recompilar.
- [x] **Tarea 14 — Form scaffolding**: `CargoInputModel` con DataAnnotations (Codigo required+50, Nombre required+200, Descripcion?+1000, NivelId required), `ICargoForm` (en `src/SGV.Web/Pages/Organizacion/Cargos/` por instrucción del orchestrator) con `Input` + `NivelOptions` + `ErrorMessage` + `IsEdit` (siempre `false` en PR2A, queda listo para Edit en PR2B) + `ReturnToListUrl`, y `CargoFormHelpers` con `BuildReturnToListUrl` (preserva p/search/sort) + `ApplyFieldErrorsToModelState`.
- [x] **Tarea 15 — `_Form.cshtml` partial**: partial Inspinia con `form-floating` para Codigo, Nombre, Descripcion (textarea) y dropdown `NivelId` con `asp-items="@(new SelectList(Model.NivelOptions, "Id", "Nombre"))"`. `@model ICargoForm`.
- [x] **Tarea 16 — Create page**: `Create.cshtml` (`/organizacion/cargos/crear`, `@model CreateModel`) + `Create.cshtml.cs` (`[Authorize]`, implementa `ICargoForm`). `OnGetAsync` carga `GetNivelesAsync` con try/catch (estado recuperable si falla el catálogo). `OnPostAsync` valida ModelState, construye `CrearCargoRequest`, llama `CreateAsync`, mapea:
  - 201 success → PRG a `Details` con TempData success.
  - 409 conflict → `ModelState.AddModelError("Input.Codigo", result.Error.Message)`.
  - 400 con `FieldErrors` → `CargoFormHelpers.ApplyFieldErrorsToModelState`.
  - Otro error → `ErrorMessage` general + summary error.
  En cualquier fallo recarga el catálogo antes de devolver `Page()`.
- [x] **Tarea 17 — Index CTA**: `Index.cshtml` agrega botón "Crear cargo" en el card-header (con icono `ti ti-plus`). La aserción previa `Assert.DoesNotContain(">Crear<", ...)` sigue pasando porque el texto es "Crear cargo", no "Crear" (no matchea `>Crear<` literal).
- [x] **Tarea 18 — Sidenav Nueva entry**: `_Sidenav.cshtml` agrega `<li class="side-nav-item"><a class="side-nav-link" href="/organizacion/cargos/crear"><span class="menu-text">Nueva</span></a></li>` dentro del submenú del grupo `cargos`. El estado `active` se hereda del toggle del grupo via `StartsWithSegments("/organizacion/cargos")` (verificado en test 22).
- [x] **Tarea 19 — GET carga dropdown de Nivel**: `Get_Create_WhenAuthenticated_LoadsNivelesDropdown` (pass). Cubre también el caso recuperable `Get_Create_WhenNivelesCatalogFails_ShowsRecoverableError`.
- [x] **Tarea 20 — POST success → PRG a Details**: `Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation` (pass). Verifica `Location` apunta a `/organizacion/cargos/detalles/{newId}` y `CreateCalls` captura el payload.
- [x] **Tarea 21 — Codigo duplicado → error de campo (409 → Input.Codigo)**: `Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm` (pass). Verifica que el mensaje se renderiza en `<span data-valmsg-for="Input.Codigo">` y que el catálogo se recarga (2 calls).
- [x] **Tarea 22 — Sidenav Nueva entry + active propagado**: `Get_Create_WhenAuthenticated_SidenavShowsNuevaEntryWithActiveState` (pass). Verifica `href="/organizacion/cargos/crear"`, `>Nueva<` y que el toggle del grupo `cargos` tiene clase `active` (regex contra `aria-controls="cargos"`).
- [x] **Tarea 23 — Validación server-side Codigo vacío**: `Post_Create_WhenCodigoIsEmpty_ShowsValidationErrorAndDoesNotRedirect` (pass). Verifica que `ModelState` corta antes del API client (Assert.Empty `CreateCalls`), que el mensaje "El código es obligatorio." aparece en el span `data-valmsg-for="Input.Codigo"`, y que la respuesta es 200 OK (no redirect).

### TDD Cycle Evidence

> Tabla exigida por Strict TDD (`openspec/config.yaml`). Cada fila documenta
> el ciclo RED → GREEN → REFACTOR de la tarea correspondiente. Los commits
> agrupan tests + implementación por work-unit (5 commits total).

| Tarea | RED (tests fallaron antes del cambio) | GREEN (tests pasaron después) | REFACTOR (si hubo) | Hash commit |
|---|---|---|---|---|
| 12 RED+GREEN | 4 tests nuevos en `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs`. RED: `dotnet build` → 4 errores `CS1061: 'CargoApiClient' does not contain a definition for 'CreateAsync' / 'GetNivelesAsync'` (verificado, build falló). | `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoApiClientTests" --no-build` → **11/11 pass** (4 nuevos + 7 previos; verificado en este apply). | N/A — el patrón `ToCommandResultAsync` se reutiliza de `UnidadOrganizativaApiClient` y los mensajes de error están en español para coincidir con el resto de la UI. | `f323c6e1` |
| 13 GREEN | N/A — extensión de `FakeCargoApiClient` hecha en el mismo commit que 12 para mantener la build verde. | `dotnet build SGV.slnx` → 0 errores (verificado, el fake compila contra la nueva interfaz). | N/A — el fake expone `CreateResult` / `NivelesResult` como setters y guarda `CreateCalls` / `NivelesCalls` como listas contables. Default `CreateResult` es `Failure(NotImplemented)` para forzar configuración explícita. | `f323c6e1` |
| 14 GREEN | N/A — código nuevo sin tests propios (la interface es un contrato de shape). | `dotnet build SGV.slnx` → 0 errores (verificado). | Sin refactor — el `ICargoForm` se ubica en `Pages/Organizacion/Cargos/` por instrucción del orchestrator, en lugar de `Integration/Organizacion/` como `IUnidadOrganizativaForm`. Esto deja el contrato del form dentro del scope del feature de Cargos. | `390dd37e` |
| 15 GREEN | N/A — partial sin tests propios (es markup). | `dotnet build SGV.slnx` → 0 errores (verificado). | Sin refactor — el partial usa `asp-items="@(new SelectList(...))"` y `asp-validation-for` para mantener consistencia con `_Form.cshtml` de UnidadesOrganizativas. | `390dd37e` |
| 16 GREEN | N/A — `Create.cshtml.cs` no tiene tests unitarios propios; su comportamiento se cubre a través de los 6 web tests (tareas 19-23). | `dotnet build SGV.slnx` → 0 errores (verificado). | Sin refactor — la lógica de mapeo 409 → `Input.Codigo` se hace en `OnPostAsync` y reusa `CargoFormHelpers.ApplyFieldErrorsToModelState` para 400. | `07fd366b` |
| 17+18 GREEN | N/A — cambios de UI cubiertos indirectamente por los web tests (la aserción previa `Assert.DoesNotContain(">Crear<", ...)` sigue pasando). | `dotnet test SGV.slnx --filter "FullyQualifiedName~Get_Index_WhenAuthenticated_RendersActiveCargosTable" --no-build` → **1/1 pass** (regresión OK, verificado). | Sin refactor — la propagación de `active` viene gratis por `StartsWithSegments` en el Sidenav (línea 5 del partial). | `6329fbdd` |
| 19-23 RED+GREEN | 6 tests nuevos en `tests/SGV.Tests/Web/Cargo/CargoCreatePageTests.cs` (304 líneas). RED inicial: 3 tests fallaron por aserciones mal ajustadas a la realidad del HTML renderizado (1: `cargosActive` (variable Razor) no aparece en HTML, debe ser `active`; 2: el atributo es `data-valmsg-for`, no `validation-for`; 3: `response.Headers.Location` es null en respuestas 200 OK, no redirect). Las 3 correcciones se hicieron en el mismo commit, no en commits separados, para mantener el ciclo RED → GREEN por tarea completo y visible. | `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoCreatePageTests" --no-build` → **6/6 pass** (verificado). Suite completa `CargoCreatePageTests` + `CargoIndexPageTests` + `CargoDetailsPageTests` + `CargoApiClientTests` → **98/98 pass** en la filter `~Web` (incluye 88 previos + 4 nuevos de `CargoApiClientTests` + 6 nuevos de `CargoCreatePageTests`). | N/A — los tests usan `SgvWebApplicationFactory` + `FakeCargoApiClient`, no requieren MySQL (a diferencia de los `[MySqlFact]` que se skipean limpio en entornos sin MySQL). | `318ef646` |

### Commits planificados (work units)

1. `feat(cargos-web): add Create and GetNiveles to CargoApiClient` — `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`, `ICargoApiClient.cs`, `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs`, `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs` (Tareas 12 + 13)
2. `feat(cargos-web): scaffold Create form shared infrastructure` — `src/SGV.Web/Integration/Organizacion/CargoInputModel.cs`, `CargoFormHelpers.cs`, `src/SGV.Web/Pages/Organizacion/Cargos/ICargoForm.cs`, `_Form.cshtml` (Tareas 14 + 15)
3. `feat(cargos-web): implement Create page for Cargos` — `src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml`, `Create.cshtml.cs` (Tarea 16)
4. `feat(cargos-web): expose Crear cargo CTA in Index and Sidenav` — `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` (Tareas 17 + 18)
5. `test(cargos-web): cover Create page, Sidenav Nueva, duplicate conflict` — `tests/SGV.Tests/Web/Cargo/CargoCreatePageTests.cs` (Tareas 19-23)

### Resumen de archivos tocados

- **Producción (10 archivos)**:
  - Nuevos: `CargoInputModel.cs`, `CargoFormHelpers.cs`, `ICargoForm.cs`, `_Form.cshtml`, `Create.cshtml`, `Create.cshtml.cs` (6)
  - Modificados: `ICargoApiClient.cs`, `CargoApiClient.cs`, `Index.cshtml`, `_Sidenav.cshtml` (4)
- **Tests (3 archivos)**: `CargoApiClientTests.cs` (4 tests nuevos), `FakeCargoApiClient.cs` (extensión), `CargoCreatePageTests.cs` (6 tests nuevos)
- **Total**: 13 archivos; +871 / -4 líneas (`git diff develop..feat/cargos-crear-editar-codigo-editable-pr2a --stat`)

### Tests ejecutados al cierre de PR2A

- `dotnet build SGV.slnx` → **0 errores, 0 warnings** (build limpio).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos" --no-build` → **240/240 pass** (234 previos + 6 nuevos en `CargoCreatePageTests`; los `CargoApiClientTests` van por el filtro `~CargoApiClient` que cae en la categoría).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Web" --no-build` → **98/98 pass** (88 previos + 4 nuevos de `CargoApiClientTests` + 6 nuevos de `CargoCreatePageTests`).
- `dotnet test SGV.slnx --no-build` (suite completa) → **1042/1054 pass, 12 fail** (los 12 fails son `OcupacionRepositoryTests`, **pre-existentes, no relacionados con este PR** — bug documentado en `AGENTS.md` como issue #59, `ActivePuestoIdUnique INT` incompatible con `PuestoId CHAR(36)`).
- `bun install && bun run build` en `src/SGV.Web` → **build OK** (smoke pipeline frontend).

### Línea base de PR2A (cumplimiento del review budget)

- **Líneas modificadas en este PR (vs develop)**: 871 insertions, 4 deletions, 13 archivos.
- **Budget 400 líneas de review**: **excedido** (871 vs 400). Razón principal: el task list del orchestrator para PR2A listó 12 tareas, y el alcance combinado (cliente HTTP + form scaffolding + Create page + Index CTA + Sidenav Nueva + 6 web tests) supera naturalmente las 400 líneas cuando se cuenta el código de tests (304 líneas en `CargoCreatePageTests` solo). Alternativas para PR2B si la línea de base sigue creciendo: (a) partir el cliente HTTP en su propio micro-PR, (b) reducir el alcance de los tests web a los 5 obligatorios del task list original (eliminar el de "catálogo caído" que agregué como extra).
- **5 work-unit commits** en orden lógico: cliente → form → página → navegación → tests. Cada commit tiene un solo propósito y deja el repo en estado compilable y testeable.

### Decisiones técnicas del PR2A

- **`ICargoForm` se ubica en `src/SGV.Web/Pages/Organizacion/Cargos/`** (no en `Integration/Organizacion/` como `IUnidadOrganizativaForm`). Esta fue una instrucción explícita del orchestrator en el scope de PR2A; deja el contrato del form dentro del feature folder de Cargos, no en el namespace compartido. La asimetría con `IUnidadOrganizativaForm` queda documentada para revisión en archive: si la convención del repo prefiere `Integration/Organizacion/`, mover el archivo en PR2B o en una refactorización posterior.
- **`CargoFormHelpers` se ubica en `Integration/Organizacion/`** (igual que `UnidadOrganizativaFormHelpers`) porque contiene utilidades reutilizables (BuildReturnToListUrl, ApplyFieldErrorsToModelState). La asimetría con `ICargoForm` es intencional: helpers en Integration, contratos de feature en Pages.
- **Mapeo 409 → `Input.Codigo` directo**, no via `ApplyFieldErrorsToModelState`. Razón: el 409 del backend no viene como `ValidationProblemDetails` sino como `ProblemDetails` plano, por lo que `FieldErrors` está vacío. Hacer `ModelState.AddModelError("Input.Codigo", error.Message)` muestra el mensaje en el `asp-validation-for="Input.Codigo"` span, donde el usuario lo espera.
- **Recarga del catálogo tras POST fallido**: `OnPostAsync` llama `LoadCatalogsAsync` antes de `return Page()` en todos los caminos de fallo, para que el dropdown de niveles siga funcional si el usuario corrige el form y reintenta. Esto es un costo menor (1 GET extra) y mejora la UX de los casos `400 FieldErrors` y `409 Conflict`.
- **`IsEdit` siempre `false` en PR2A**: el flag se introduce en la interface para que PR2B (Edit) no rompa el contrato del partial `_Form.cshtml` cuando lo reutilice. La rama Edit queda lista, pero el comportamiento de la página Create no se ve afectado.
- **Helper `IsActiveCodigoUniqueViolation` no se usa en web**: la detección del índice único MySQL vive solo en `CargoServicioComandos` (backend). La página solo necesita traducir la respuesta HTTP 409 a un error visible al usuario, lo cual hace con el `ModelState.AddModelError("Input.Codigo", ...)` directo.

### Riesgo residual / hand-off al orchestrator

- **`ICargoForm` asimétrico con `IUnidadOrganizativaForm`**: el primero vive en `Pages/...`, el segundo en `Integration/...`. Es una decisión deliberada del orchestrator para este PR. Si en el PR de archive se quiere unificar la convención, mover `ICargoForm.cs` a `Integration/Organizacion/` y actualizar su using en `Create.cshtml.cs` y `_Form.cshtml`.
- **PR2A excede el budget de 400 líneas (871 vs 400)**: los 6 web tests consumen 304 líneas. Si en el review se decide bajar el scope, considerar eliminar `Get_Create_WhenNivelesCatalogFails_ShowsRecoverableError` (cubierto implícitamente por el filtro `~Web`) y `Post_Create_WhenCodigoIsEmpty_ShowsValidationErrorAndDoesNotRedirect` (cubierto por el `[Required]` de `CargoInputModel` que es trivial). Eso bajaría el PR a ~620 líneas, todavía sobre 400 pero más manejable.
- **`UpdateAsync` no se creó en el cliente HTTP** (es scope de PR2B). `ICargoApiClient` solo expone `CreateAsync` y `GetNivelesAsync` como métodos de escritura/lectura nuevos. La firma pública del cliente sigue siendo backward-compatible con el fake (`FakeCargoApiClient` solo necesita implementar los métodos declarados en la interface).
- **Tests `[MySqlFact]` siguen skipeándose limpio** en entornos sin MySQL. PR2A no agrega tests `[MySqlFact]` nuevos; la cobertura MySQL real del backend ya está cubierta por los 3 tests agregados en PR-1.12.3.
- **Siguiente paso del orchestrator**: mergear PR2A → abrir PR2B (Edit + Details CTA). PR2B no debe tocar `ICargoApiClient` (la firma ya está completa, solo necesita agregar `UpdateAsync`).

---

## PR2A refactor cleanup (chained, behavior-preserving)

> Change: `2026-06-30-cargos-crear-editar-codigo-editable` (continuación)
> Phase: `sdd-apply` (PR 2A refactor cleanup)
> Strict TDD: ACTIVO (RED → GREEN → REFACTOR por tarea cuando aplica)
> Branch: `refactor/cargos-create-pr2a-cleanup`
> Base: `feat/cargos-crear-editar-codigo-editable-pr2a` @ `7d36c65b` (PR #61 mergeado)
> Cadena: PR1 → **PR2A** → **PR2A cleanup (este)** → PR2B (siguiente)

### Alcance

Cuatro ítems diferidos al cierre de PR2A por considerarlos "out-of-scope" de "bloqueantes + quick wins". Reaplicados como refactors behavior-preserving en commits atómicos, uno por ítem. Cero cambio de comportamiento. Cero migraciones. Cero breaking change.

### Tareas aplicadas (tasks.md sección 7, renumeradas Cleanup.1 a Cleanup.4)

- [x] **Cleanup.1 — Extract `CargoPostResultMapper`** (commit `b2b1b48f`).
  - 6 tests nuevos en `tests/SGV.Tests/Web/Cargo/CargoPostResultMapperTests.cs`: null result, empty failure, success result (no-op), FieldErrors con múltiples keys + mensajes, ErrorMessage solo (sin FieldErrors), FieldErrors vacío que cae a ErrorMessage.
  - Mapper en `src/SGV.Web/Integration/Organizacion/CargoPostResultMapper.cs`: `static bool TryMap(CargoCommandResult?, ModelStateDictionary)`. Resolución: (1) `FieldErrors` con entries → aplicar + `true`; (2) `Error.Message` no vacío → aplicar en empty key + `false`; (3) sino → `false` sin tocar `ModelState`.
  - `Create.cshtml.cs` delega: el branch `Conflict → Input.Codigo` se queda en línea; el `else` de `Error is not null` se reemplaza por `else if (!CargoPostResultMapper.TryMap(result, ModelState))` con un fallback defensivo que setea `ErrorMessage` por si la API devolviera `Error.Message` null en un path no-Conflict (improbable pero mantiene la simetría con el código original).
  - **TDD**: RED confirmado (7 build errors `CS0103`/`CS0246` en el primer build del test file) → GREEN (6/6 mapper tests + 27/27 cargo web tests pasan). El primer GREEN tuvo 1 test fallando por una aserción incorrecta (`Assert.False(modelState.IsValid)` en estado inicial — el `ModelStateDictionary` vacío es válido por default; corregido a `Assert.Equal(0, modelState.ErrorCount)`).
- [x] **Cleanup.2 — Move `ICargoForm` to Integration/** (commit `f9d0fe0d`).
  - `git mv` del archivo, namespace `SGV.Web.Pages.Organizacion.Cargos` → `SGV.Web.Integration.Organizacion`. `using SGV.Web.Integration.Organizacion;` redundante removido.
  - `_Form.cshtml` actualiza `@model` a `SGV.Web.Integration.Organizacion.ICargoForm`. `Create.cshtml.cs` ya tenía el using correcto.
  - **Behavior-preserving puro**: 250/250 cargo y 108/108 web verde sin tocar tests. La interface solo se referencia desde `Create.cshtml.cs` y `_Form.cshtml`; ningún test la usaba directamente.
  - Cierra la asimetría con `IUnidadOrganizativaForm` que el PR2A documentó como "resolución futura".
- [x] **Cleanup.3 — Shared test fixture** (commit `727bc71b`).
  - Nuevo `tests/SGV.Tests/Web/Cargo/CargoWebTestFixture.cs` (138 líneas) como `IClassFixture<CargoWebTestFixture>` + `IDisposable`. Expone: `BaseFactory` (SgvWebApplicationFactory base), `WithCargoApiClient(FakeCargoApiClient)`, instance `CreateAuthenticatedClientAsync(FakeCargoApiClient)`, static `ExtractAntiforgeryTokenAsync(HttpResponseMessage)`, static `JuniorNivelId` / `SeniorNivelId` / `BuildCargoDto(...)`, nested `RecordingHttpMessageHandler`.
  - Las 3 test classes declaran `: IClassFixture<CargoWebTestFixture>` con constructor. Nombres de tests y aserciones NO cambian. El `ExecuteDeleteConfirmationScriptAsync` se queda en `CargoIndexPageTests` porque solo lo usa ese archivo.
  - **Behavior-preserving**: 27/27 tests de las 3 clases pasan; 250/250 cargo y 108/108 web verde. Reducción neta de 166 líneas en los 3 test files (62 inserts, 228 deletes).
- [x] **Cleanup.4 — Unify XML docs** (commit `689fb718`).
  - Único archivo con inconsistencia interna era `Create.cshtml.cs`: class summary en inglés, method summaries (OnGetAsync, OnPostAsync) y un `//` comment en español. Unificado a inglés.
  - Archivos auditados y NO tocados (ya consistentes): `ICargoForm.cs` (español en todo el archivo), `CargoFormHelpers.cs` (español), `CargoPostResultMapper.cs` (inglés, nuevo), `_Form.cshtml` (inglés, comentarios Razor), `CargoCreatePageTests.cs` (XML summary en inglés; los `//` "Task 19: ..." son divisores de sección, no XML docs y están fuera del alcance del audit), `CargoPostResultMapperTests.cs` (inglés, nuevo), `CargoWebTestFixture.cs` (inglés, nuevo).
  - **Decisión de idioma documentada por archivo** (per `AGENTS.md` default = English, con excepción de "preserve existing file context"):
    - `Create.cshtml.cs` → English (default; el class summary ya estaba en English, se unificaron los method summaries).
    - `ICargoForm.cs` → Spanish (existing context; consistente en todo el archivo).
    - `CargoFormHelpers.cs` → Spanish (existing context; consistente en todo el archivo).
    - `CargoPostResultMapper.cs` → English (nuevo, default).
    - `_Form.cshtml` → English (Razor comments existentes).
    - `CargoCreatePageTests.cs` → English para el `///` summary; los `//` section dividers se preservan en español (fuera del audit; no son XML docs).
    - `CargoPostResultMapperTests.cs` → English (nuevo, default).
    - `CargoWebTestFixture.cs` → English (nuevo, default).
  - **No se traducen** los user-facing strings: `TempData["StatusMessage"]`, `ErrorMessage = "No se pudo contactar al servicio de cargos..."`, y los `[Required(ErrorMessage = "El código es obligatorio.")]` de `CargoInputModel`. Son localized UI copy y se mantienen en español.
  - 0 impacto de comportamiento: 250/250 cargo tests verde.

### TDD Cycle Evidence

> Tabla exigida por Strict TDD (`openspec/config.yaml`). El único ítem con ciclo RED → GREEN estricto es **Cleanup.1** (mapper nuevo testeable en aislamiento). Los demás son refactor / move / fixture / docs y no requieren ciclo RED → GREEN formal; su evidencia es la suite verde antes y después.

| Tarea | RED (tests fallaron antes del cambio) | GREEN (tests pasaron después) | REFACTOR | Hash commit |
|---|---|---|---|---|
| Cleanup.1 RED+GREEN | `dotnet build SGV.slnx` con el test file nuevo y sin el mapper → 7 errores `CS0103` (CargoPostResultMapper no existe) + `CS0246` (CargoDto no importado). Verificado. | `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoPostResultMapperTests" --no-build` → 6/6 pass. Suite completa `~Cargo\|~Cargos` → **250/250 pass**. Suite `~Web` → **108/108 pass**. | Sin refactor adicional. La superficie `bool TryMap(CargoCommandResult?, ModelStateDictionary)` es la forma mínima testeable; cualquier overload con más parámetros inflaría la API sin beneficio. | `b2b1b48f` |
| Cleanup.2 move | N/A — move de archivo sin cambio de shape. | `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoCreatePageTests\|FullyQualifiedName~CargoIndexPageTests\|FullyQualifiedName~CargoDetailsPageTests" --no-build` → 20/20 pass. Suite `~Cargo\|~Cargos` y `~Web` sin regresión. | N/A. El namespace del archivo se ajusta una sola vez; el using redundante en el header se elimina. | `f9d0fe0d` |
| Cleanup.3 fixture | N/A — refactor de test infrastructure, no introduce superficie testeable nueva. La duplicación removida ya estaba cubierta por los tests existentes. | `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoCreatePageTests\|FullyQualifiedName~CargoIndexPageTests\|FullyQualifiedName~CargoDetailsPageTests" --no-build` → 27/27 pass (los 20 anteriores + 7 tests de las 3 clases que ahora usan el fixture compartido). | Sí: se consolida ~290 líneas de helpers duplicados en 138 líneas de fixture + los 3 test classes que pierden 166 líneas netas. | `727bc71b` |
| Cleanup.4 docs | N/A — docs-only commit. No hay código tocado. | `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo\|FullyQualifiedName~Cargos" --no-build` → **250/250 pass**. | N/A. | `689fb718` |

### Commits planificados (work units)

1. `refactor(cargos-web): extract CargoPostResultMapper for field-error mapping` — `b2b1b48f` (175 ins, 5 del; mapper 76 + tests 95 + Create.cshtml.cs delta 4)
2. `refactor(cargos-web): move ICargoForm to Integration namespace` — `f9d0fe0d` (2 ins, 3 del; rename + 1 line)
3. `refactor(cargos-web-tests): share auth + seed helpers via CargoWebTestFixture` — `727bc71b` (188 ins, 228 del; fixture 138 + 3 test files net -178)
4. `docs(cargos-web): unify XML doc language to English in Create.cshtml.cs` — `689fb718` (10 ins, 10 del; solo docs)
5. `docs(apply): track PR2A refactor cleanup progress` — (siguiente commit; este `apply-progress.md` + `tasks.md` + commit de los SDD artifacts untracked)

### Resumen de archivos tocados

- **Producción (4 archivos)**:
  - Nuevos: `CargoPostResultMapper.cs` (commit 1), `ICargoForm.cs` en nueva ubicación (commit 2, git mv).
  - Modificados: `Create.cshtml.cs` (commit 1: delega al mapper; commit 4: docs a inglés).
  - Modificado: `_Form.cshtml` (commit 2: `@model` apunta a la nueva ubicación).
- **Tests (4 archivos)**:
  - Nuevo: `CargoPostResultMapperTests.cs` (commit 1, 6 tests).
  - Nuevo: `CargoWebTestFixture.cs` (commit 3, 138 líneas).
  - Modificados: `CargoCreatePageTests.cs`, `CargoDetailsPageTests.cs`, `CargoIndexPageTests.cs` (commit 3: usan `IClassFixture<CargoWebTestFixture>`).
- **Documentación (3 archivos)**:
  - Modificado: `apply-progress.md` (este archivo; append nueva sección).
  - Modificado: `tasks.md` (append sección 7 con 4 tasks nuevas).
  - Committeados por primera vez: `proposal.md`, `design.md`, `exploration.md`, `pr1-body.md`, `pr2a-body.md`, `specs/**`, `verify-report.md` (artefactos de los PRs previos que vivían untracked en el working tree).
- **Total** vs. base `7d36c65b`: 6 archivos nuevos, 5 archivos modificados, 1 archivo movido. Diff completo: ver `git diff 7d36c65b..HEAD` al final.

### Tests ejecutados al cierre

- `dotnet build SGV.slnx` → **0 errores, 0 warnings** (build limpio en los 4 commits).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos" --no-build` → **250/250 pass** (240 del PR2A close + 6 nuevos del mapper; los 4 tests adicionales no contabilizados antes corresponden a tests que ya existían pero caen en este filtro por la convención `~Cargos` que matchea sufijos como `CargoTests`, `CargosControllerTests` y los nuevos `CargoPostResultMapperTests`).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Web" --no-build` → **108/108 pass** (102 del PR2A close + 6 nuevos del mapper).
- `dotnet test SGV.slnx --no-build` (suite completa) → mantiene el patrón del PR2A close: 12 fails pre-existentes en `OcupacionRepositoryTests` por issue #59 (no relacionado con este PR).

### Línea base del PR (cumplimiento del review budget)

- **Líneas modificadas vs `7d36c65b`**: ~210 inserciones, ~243 deletions, 10 archivos. **Budget 400: cumplido con holgura**. La reducción neta en los 3 test files (-166) compensa el mapper nuevo (+95 tests + 76 impl = 171).
- **5 commits atómicos**: uno por refactor + un commit final de OpenSpec artifacts. Cada commit deja el repo en estado compilable y testeable.

### Decisiones técnicas del PR

- **`CargoPostResultMapper` es `static`**: no necesita estado mutable. Operar sobre `ModelStateDictionary` es un parámetro de entrada, no una dependencia inyectable. Mantener la superficie `TryMap(result, modelState)` simple maximiza la testeabilidad en aislamiento.
- **`CargoPostResultMapper.TryMap` retorna `true` solo cuando `FieldErrors` se aplicó**: el caller distingue "se aplicaron errores de campo" (true → no hacer nada más) de "se aplicó un error general" o "nada que aplicar" (false → caller decide). Esto evita un acoplamiento del mapper con `ErrorMessage` del PageModel; el caller mantiene el control sobre cómo reflejar el error (string vs. ModelState vs. TempData).
- **El `else if (!mapper.TryMap(...))` en `Create.cshtml.cs` mantiene el fallback defensivo**: si la API devolviera un `CargoError` no-Conflict con `Message` null, el mapper retorna `false` y el caller setea `ErrorMessage` con `result.Error.Message` (que es null). El comportamiento es el mismo que antes del refactor (es la misma rama).
- **`CargoWebTestFixture` como `IClassFixture<T>` + `IDisposable`**: la base `SgvWebApplicationFactory` se crea una vez por clase (no por test) y se dispone al final. xUnit maneja el ciclo de vida. Las constantes y builders son `static` para que tests que no necesitan la auth flow puedan usarlos directamente (`CargoWebTestFixture.JuniorNivelId`).
- **`BuildCargoDto` es `static`**: la duplicación era solo de signatura (mismo cuerpo) entre `CargoDetailsPageTests` y `CargoIndexPageTests`. Como helper puro, no necesita instance state.
- **`RecordingHttpMessageHandler` queda como clase anidada pública en el fixture**: la usan 3 test classes, todos los accesos son desde el mismo namespace `SGV.Tests.Web.Cargo`. Visibilidad `public` por requisito de xUnit al cargarla (aunque solo se use internamente).
- **Cleanup.4 unifica SOLO `Create.cshtml.cs`**: la auditoría reveló que ese era el único archivo con inconsistencia interna de idioma en sus XML docs. Los demás archivos son consistentes en su idioma actual y se preservan (per la excepción "preserve existing file context" de la regla `AGENTS.md`).

### Riesgo residual / hand-off al orchestrator

- **`ICargoForm` ahora vive en `Integration/`**: cualquier archivo fuera de `src/SGV.Web/` o `tests/SGV.Tests/` que importara `SGV.Web.Pages.Organizacion.Cargos.ICargoForm` debe actualizarse. El grep confirma que solo `Create.cshtml.cs` y `_Form.cshtml` lo usaban, y ambos están actualizados. No hay consumidores externos.
- **`CargoWebTestFixture` introduce un nuevo shared resource**: si se agregan más tests web de Cargos en el futuro, deben usar el fixture en lugar de redefinir los helpers. El commit 3 incluye un comentario XML en el fixture que documenta su contrato.
- **`CargoPostResultMapper` es reutilizable para la página Edit (PR2B)**: la interface es genérica para `CargoCommandResult`, no atada a Create. PR2B puede inyectar el mismo mapper en su `OnPostAsync` para los paths de FieldErrors/ErrorMessage, manteniendo paridad con Create.
- **`UpdateAsync` sigue sin existir en el cliente HTTP** (es scope de PR2B). `ICargoApiClient` no fue tocado en este PR.
- **PR2A refactor cleanup NO toca reglas de negocio ni persistencia**: `CargoServicioComandos` (backend), las migraciones, y los repositorios no se modificaron. Es un cambio puramente estructural en la capa web.
- **Siguiente paso del orchestrator**: mergear este PR → abrir PR2B (Edit + Details CTA). PR2B hereda el mapper y el fixture; no necesita redefinirlos.

---

## PR 2B — Frontend Edit + Details CTA (planning)

> Change: `2026-06-30-cargos-crear-editar-codigo-editable` (continuación)
> Phase: `sdd-apply` (PR 2B — Frontend Edit + Details CTA)
> Strict TDD: ACTIVO (RED → GREEN → REFACTOR por tarea)
> Branch: `feat/cargos-crear-editar-codigo-editable-pr2b`
> Base: `develop` @ `6c1553e0` (PR #63 mergeado)
> Cadena: PR1 → PR2A → PR2A cleanup → **PR 2B (este)**

### Alcance

Edit página con PRG + TempData; botón "Editar" en Details preservando contexto de paginación. Reuso completo de la infra web dejada por PR2A + cleanup: `ICargoApiClient` (extender con `UpdateAsync`), `CargoPostResultMapper`, `CargoWebTestFixture`, `ICargoForm` (en `Integration/`), `_Form.cshtml` (compartido), `CargoFormHelpers`. **No toca** backend (PR1 ya mergeado).

### Decisiones de planning

- **`UpdateAsync` se agrega a `ICargoApiClient` en este PR** (no se creó en PR2A; verificado por grep en `ICargoApiClient.cs`). Firma: `Task<CargoCommandResult> UpdateAsync(Guid id, ActualizarCargoRequest request, CancellationToken cancellationToken = default)`. URL `/api/v1/cargos/{id}`. Reuso del patrón `PutAsJsonAsync` + `ToCommandResultAsync` (mismo que `CreateAsync`).
- **`FakeCargoApiClient` se extiende** con `UpdateResult`, `UpdateCalls` (captura `(Guid Id, ActualizarCargoRequest Request, CancellationToken)`), `UpdateException`. Default `UpdateResult` = `Failure(NotImplemented)` para forzar configuración por test (paridad con `CreateResult` en PR2A).
- **`CargoEditPageTests` como archivo separado** (no se mezcla con `CargoCreatePageTests`). El design original (`tasks.md §4 PR-2.13`) proponía `CargoCreateEditPageTests.cs`, pero separar por página reduce ambigüedad y mantiene cada work unit (Create vs Edit) con su propio test file.
- **Edit page reusa `_Form.cshtml` sin cambios**: el partial no tiene lógica condicional sobre `IsEdit`; la page setea `IsEdit = true` en `ICargoForm` para que el modelo exponga el estado. Si en el futuro se quiere texto distinto en el botón submit ("Guardar" vs "Actualizar"), la decisión se delega al cshtml de la página, no al partial.
- **PRG de Edit redirige a sí mismo**, no a Details: tras guardar, el usuario vuelve al Edit con `TempData["StatusMessage"]` verde. Esto permite múltiples ediciones consecutivas sin perder contexto, y es coherente con el patrón de `UnidadesOrganizativas`.
- **Botón "Editar" en Details solo si el cargo está disponible** (`!Model.IsNotFound`); el estado "no disponible" no debe ofrecer edición.
- **Preservar query string** (`p`, `search`, `sort`) en el href del botón Editar y en `OnGetAsync`/`OnPostAsync` para mantener coherencia con la navegación de Index. La redirección tras PRG usa `CargoFormHelpers.BuildReturnToListUrl` para reconstruir el listado con el contexto preservado.
- **Tests de Details invertidos**: `Get_Details_WhenAuthenticated_ShowsCargoReadOnly` cambia de `DoesNotContain(">Editar<")` a `Contains("Editar")` + nueva aserción del href con query string preservada. `Get_Details_WhenCargoNotFound_ShowsNotAvailableState` mantiene `DoesNotContain(">Editar<")` (correcto: no debe haber Edit cuando no hay cargo).

### Reuso confirmado (no trabajo nuevo)

- `ICargoApiClient` (extender con `UpdateAsync`, no reescribir) — interface ya consolidada en `Integration/Organizacion/`.
- `CargoApiClient` (extender) — patrón `ToCommandResultAsync` ya probado en `CreateAsync`/`DeleteAsync`.
- `CargoPostResultMapper.TryMap` (sin cambios) — reusado en `OnPostAsync` para `FieldErrors` y `ErrorMessage`.
- `CargoWebTestFixture` (sin cambios) — usado por `CargoEditPageTests` igual que en `CargoCreatePageTests`.
- `ICargoForm` (sin cambios) — mismo shape que Create; `IsEdit = true` distingue contexto.
- `_Form.cshtml` (sin cambios) — partial genérico, ya preparado para Edit en PR2A.
- `CargoFormHelpers` (sin cambios) — `ApplyFieldErrorsToModelState` y `BuildReturnToListUrl` reusados.
- `CargoInputModel` (sin cambios) — DataAnnotations ya cubren Edit (no son específicos de Create).
- `FakeCargoApiClient` (extender) — patrón ya aplicado en PR2A para Create.

### Tareas planificadas (tasks.md §8)

| Tarea | Tipo | Líneas estimadas |
|---|---|---|
| PR-2B.1 RED+GREEN cliente `UpdateAsync` (interface + impl + fake + tests) | feat | ~80 |
| PR-2B.2 RED tests Edit | test | ~150 |
| PR-2B.3 GREEN página Edit (`Edit.cshtml` + `Edit.cshtml.cs`) | feat | ~180 |
| PR-2B.4 CTA Details + ajuste test | feat + test | ~30 |
| PR-2B.5 VERIFY | soporte | — |
| Artefactos (`apply-progress.md` + tasks.md carry-over) | docs | ~60 |
| **Total estimado** | **~500 líneas, 4 commits** | |

### TDD Cycle Evidence

> Se completa por el `sdd-apply` ejecutor a medida que aplica cada tarea. La tabla se conserva en este archivo como evidencia obligatoria por `openspec/config.yaml:strict_tdd=true`.

| Tarea | RED (tests fallaron antes del cambio) | GREEN (tests pasaron después) | REFACTOR | Hash commit |
|---|---|---|---|---|
| PR-2B.1 RED+GREEN cliente | TBD | TBD | TBD | TBD |
| PR-2B.2 RED tests Edit | TBD | N/A — GREEN en PR-2B.3 | N/A | TBD |
| PR-2B.3 GREEN página Edit | N/A | TBD | TBD | TBD |
| PR-2B.4 CTA Details + ajuste test | TBD | TBD | TBD | TBD |
| PR-2B.5 VERIFY | N/A — VERIFY | TBD | N/A | TBD |

### Riesgo residual / hand-off al orchestrator

- **`UpdateAsync` agrega método a la interface pública** (`ICargoApiClient`): cualquier consumidor externo del repo que implemente esta interface debe agregar el método o no compilará. El repo no tiene otros implementadores de `ICargoApiClient` fuera de `CargoApiClient` (producción) y `FakeCargoApiClient` (tests); verificado con `grep -rn "ICargoApiClient" src tests`.
- **PR 2B excede el budget de 400 líneas (estimación ~500 vs 400)**: hereda el patrón de `size:exception` de PR1 y PR2A. El cuerpo del PR debe declarar explícitamente `size:exception` con justificación.
- **El "Guardar" vs "Actualizar" es decisión de UI copy**: PR2B puede usar el mismo texto "Guardar" en ambos formularios (Create y Edit) sin diferenciación visual. Si se quiere "Actualizar" en Edit, se agrega al cshtml de Edit con un botón distinto; no requiere cambios al partial.
- **PR 2B no toca reglas de negocio ni persistencia**: los cambios están limitados a `src/SGV.Web/Pages/Organizacion/Cargos/`, `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` y `ICargoApiClient.cs`, y `tests/SGV.Tests/Web/Cargo/`.
- **Siguiente paso del orchestrator (post-merge PR 2B)**: `sdd-archive` para cerrar el change `2026-06-30-cargos-crear-editar-codigo-editable`, sincronizar `openspec/specs/cargo-management/spec.md` y `openspec/specs/cargo-web-crear-editar/spec.md` con el delta total, redactar `archive-report.md` en español.
- **Issue #62 (backend `[Authorize]` POST/PUT/DELETE) sigue fuera de scope**: este PR agrega más endpoints públicos sin `[Authorize]` web (la página Edit usa `[Authorize]` pero el backend queda abierto). Si el usuario decide cerrar #62, debe ser un change SDD aparte.

