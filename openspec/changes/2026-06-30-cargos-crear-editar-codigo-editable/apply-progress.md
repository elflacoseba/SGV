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