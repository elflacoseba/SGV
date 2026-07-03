# Apply Progress: Permitir editar el código de una Habilidad

## Resumen

- Estado final: 🔶 parcial (implementación completa, diff sobre el budget de 400 líneas)
- Work units completados: 6/6 (Dominio, Aplicación, Persistencia, API, Web, Specs)
- Commits creados: 8 (work units + docs de OpenSpec + pruning de tests redundantes + apply-progress) — ver §"Batch de remediación (post-verify)" para el conteo post-batch
- Tests añadidos/actualizados/removidos: +20 tests nuevos, 8 tests existentes reescritos/eliminados, 0 regresiones en la suite `Habilidad`

### Commits (en orden de aplicación)

| Hash corto | Mensaje |
|---|---|
| `25001a0f` | feat(habilidades): allow Codigo update in Habilidad domain entity |
| `39ac7154` | feat(habilidades): accept Codigo in ActualizarHabilidadRequest and translate unique index to 409 |
| `db55110b` | feat(habilidades): propagate Codigo in Habilidad UpdateEntity mapper |
| `7c08f5a6` | feat(api): PUT /api/v1/skills/{id} accepts Codigo and preserves 400/409 contract |
| `3a68384f` | feat(web): edit Codigo in Habilidades edit screen end-to-end |
| `a5f1fe2e` | docs(sdd): add OpenSpec artifacts for permitir-editar-el-codigo-de-una-habilidad |
| `310ea94a` | test(web): drop redundant Edit test that overlaps with successful roundtrip |

## Diff summary

```
22 files changed, 1434 insertions(+), 99 deletions(-)
```

**Código de producción + tests (sin docs):**

```
16 files changed, 523 insertions(+), 99 deletions(-)
```

- Líneas estimadas (vs budget 400): **622 changed lines, ~424 net**
- Dentro del budget: **NO** — ~55% sobre el budget previsto

### Desglose por capa

| Capa | Líneas netas (prod + tests) | Comentario |
|---|---|---|
| Dominio | ~23 net | Sólo cambio de firma + tests de invariantes de shape. |
| Aplicación | ~187 net | Incluye el helper `EnsureCodigoNoDuplicadoAsync` y `IsActiveCodigoUniqueViolation` + tests de los 5 scenarios del delta. |
| Persistencia | ~78 net | Mapper (1 línea) + tests del índice único. |
| API | ~90 net | 4 nuevos tests de PUT (200/400/400/409) + ajuste del fake. |
| Web | ~67 net | Remover readonly + postear Codigo + actualizar doc del cliente + tests. |
| Specs / Docs | +911 (no cuentan para el budget) | proposal, design, tasks, exploration, deltas. |

### Sobre el budget

- **Código de producción** es **86 líneas netas**, bien dentro del budget.
- El sobre-budget viene **íntegramente de los tests**, todos atados a scenarios concretos del delta spec:
  - Aplicación: 5 tests nuevos para los 5 scenarios de `habilidad-management`.
  - Web: 4 tests nuevos + 1 reemplazo del test de readonly.
  - API: 4 tests nuevos (PUT válido, empty, over-length, 409).
  - Persistencia: 2 tests nuevos (mismo código no viola; duplicado lanza DbUpdateException).
- **Decisión**: se priorizó la cobertura completa de los scenarios sobre la poda de tests. Cada test nuevo es un scenario explícito del delta spec.
- Si el orquestador necesita recortar, los candidatos más prescindibles serían `UpdateAsync_MismoCodigo_NoViolaIndice` (parcialmente cubierto por `UpdateAsync_ModificaCampos`) o fusionar los dos tests 400 del API en un `Theory` con `InlineData`.

## Tareas ejecutadas (por work unit)

### 1. Dominio
- ✅ **1.1 RED → 1.2 GREEN**: `Habilidad.Actualizar(string codigo, string nombre, string? categoria = null, string? descripcion = null)`. Se elimina `Codigo_EsInmutableTrasCreacion` y `Actualizar_CodigoNoCambia`. Se reemplazan con `Actualizar_CambiaCodigoSiNoDuplica`, `Actualizar_ConCodigoVacio_ThrowsArgumentException`, `Actualizar_ConCodigoMayorA50_ThrowsArgumentException`. Tests existentes (`Actualizar_ModificaCamposEditables`, `Actualizar_PermiteCategoriaNulaYLimpia`, validaciones de Nombre/Categoria/Descripcion) se actualizan a la nueva firma de 4 parámetros.
  - Evidence: 27 tests verdes en `HabilidadTests`. Hash `25001a0f`.

### 2. Aplicación
- ✅ **2.1 RED → 2.2 GREEN**: `ActualizarHabilidadRequest` agrega `Codigo` como primer parámetro. `ActualizarHabilidadRequestValidator` agrega `RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50)`. Se actualiza `RequestValido()` y se agregan 3 tests nuevos (`Should_Have_Error_When_Codigo_Is_Empty`, `Should_Have_Error_When_Codigo_Exceeds_Max_Length`, `Should_Not_Have_Error_For_Valid_Codigo`).
  - Evidence: tests verdes del validator. Hash `39ac7154`.
- ✅ **2.3 RED → 2.4 GREEN**: `HabilidadServicioComandos.ActualizarAsync` invoca `EnsureCodigoNoDuplicadoAsync(request.Codigo, excludingId: id)` después de cargar la entidad; pasa `request.Codigo` a `habilidad.Actualizar(...)`; envuelve `SaveChangesAsync` con `catch (DbUpdateException ex) when (IsActiveCodigoUniqueViolation(ex))`. Helper privado `EnsureCodigoNoDuplicadoAsync` reutilizado por `CrearAsync` (`excludingId: null`) y `ActualizarAsync` (`excludingId: id`). `IsActiveCodigoUniqueViolation` analiza `InnerException.Message` por `IX_Habilidades_ActiveCodigoUnique` + `Duplicate entry` / `1062`, sin meter dependencias de MySQL en `SGV.Aplicacion`.
  - Evidence: 5 tests nuevos verdes: `ActualizarAsync_CodigoValido_PersisteYCambiaCodigo`, `ActualizarAsync_MismoCodigo_NoSeTrataComoDuplicado`, `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`, `ActualizarAsync_CodigoDeEliminada_PermiteReutilizar`, `ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`. Hash `39ac7154`.
- ✅ **2.5**: Se elimina `ActualizarAsync_CodigoNoExpuesto_LoIgnora` (afirmaba que el request no tenía Codigo) y `ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda` se ajusta para enviar Codigo.

### 3. Persistencia
- ✅ **3.1 RED → 3.2 GREEN**: `DomainToPersistenceMapper.UpdateEntity(HabilidadEntity, Habilidad)` agrega `entity.Codigo = domain.Codigo`. **Sin migración**: la columna generada `ActiveCodigoUnique` se recalcula automáticamente. `HabilidadConfiguracion.cs` se relee sin cambios.
  - Evidence: 2 tests nuevos verdes: `UpdateAsync_MismoCodigo_NoViolaIndice`, `UpdateAsync_CodigoDuplicadoDeOtraActiva_ThrowsDbUpdateException`. El test existente `UpdateAsync_ModificaCampos` ahora envía un Codigo nuevo y lo verifica. Hash `db55110b`.

### 4. API
- ✅ **4.1 RED → 4.2 GREEN**: `SkillsController.Update` XML doc actualizado para reflejar que el `PUT` acepta `Codigo`. `FakeHabilidadServicioComandos.ActualizarAsync` ahora hace roundtrip de `request.Codigo` en lugar de hardcodear `"PROG"`. 4 tests nuevos: `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto` (reemplaza el anterior), `Put_EmptyCodigo_Returns400WithFieldErrors`, `Put_CodigoExceedsMaxLength_Returns400WithFieldErrors`, `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`. Se actualiza el `Put_ValidationError_Returns400WithFieldErrors` para enviar Codigo en el body.
  - Evidence: 37 tests verdes en `SkillsControllerTests`. Hash `7c08f5a6`.

### 5. Web
- ✅ **5.1 RED → 5.2 GREEN**: `_Form.cshtml` elimina la rama `readonly` específica de edit; queda un único `<input asp-for="Input.Codigo" ...>` editable. Comentario `REQ-HCW-01` reemplazado por uno que explica que Codigo es editable en ambos flujos. `Edit.cshtml.cs` construye `new ActualizarHabilidadRequest(Input.Codigo, ...)` y mantiene el manejo de `Conflict`/`FieldErrors` que ya existía. `IHabilidadApiClient.UpdateAsync` XML doc actualizado.
  - Evidence: 10 tests verdes en `HabilidadEditPageTests`. Hash `3a68384f`.
- ✅ **5.3**: `FakeHabilidadApiClient.UpdateCalls` ya capturaba `(Guid Id, ActualizarHabilidadRequest Request)`, así que la inspección del `Codigo` enviado funciona sin cambios adicionales. Los tests nuevos inspeccionan `apiClient.UpdateCalls[0].Request.Codigo`.
- ✅ **5.4 Verify**: suite web completa (225 tests) verde; `bun run build` sin warnings nuevos.
- ✅ **commit pruning**: tras la verificación, se removió `Post_Edit_WhenCodigoUnchanged_UpdatesOtherFields` por solapamiento con `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation`. Hash `310ea94a`.

### 6. Specs (delta + archive)
- ✅ **6.1**: los delta specs ya reflejan la implementación final. No fue necesario modificar la prosa porque los scenarios coinciden uno a uno con los tests agregados.
- ⏳ **6.2 archive**: queda fuera de `sdd-apply`; se ejecuta en la fase `sdd-archive`.

## Validación end-to-end

| Comando | Resultado | Notas |
|---|---|---|
| `dotnet restore` | ✅ | All projects up-to-date. |
| `dotnet build SGV.slnx --configuration Release` | ✅ | 0 warnings, 0 errors. |
| `dotnet test SGV.slnx --no-build --configuration Release --filter "FullyQualifiedName~Habilidad"` | ✅ | 214/214 verdes (incluye 23 `[MySqlFact]` corriendo contra MySQL local con `sgv_test`). |
| `dotnet test SGV.slnx --no-build --configuration Release` (suite completa) | 🔶 | 1273/1285 verdes, **12 fallos preexistentes no relacionados** en `OcupacionRepositoryTests` (issue #59 documentado en AGENTS.md: `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en la migración inicial). |
| `bun install && bun run build` (en `src/SGV.Web`) | ✅ | Sin warnings nuevos. |
| `openspec validate permitir-editar-el-codigo-de-una-habilidad --strict --json` | ✅ | `passed: 1, failed: 0`. |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Dominio/HabilidadTests.cs` | Unit | ✅ 24/24 | ✅ Written (3 nuevos) | ✅ Passed | ✅ 3 cases (cambio, vacío, >50) | ✅ Clean |
| 2.1 | `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs` | Unit | ✅ 7/7 | ✅ Written (3 nuevos) | ✅ Passed | ✅ 4 cases (null, "", " ", ">50") | ✅ Clean |
| 2.3 | `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` | Unit | ✅ 9/9 | ✅ Written (5 nuevos) | ✅ Passed | ✅ 5 cases (válido, mismo, duplicado, soft-deleted, inválido) | ✅ Helper extraído |
| 3.1 | `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` | Integration (`[MySqlFact]`) | ✅ 21/21 | ✅ Written (2 nuevos) | ✅ Passed | ✅ 3 paths (mismo, dup, cambio) | ✅ Clean |
| 4.1 | `tests/SGV.Tests/Api/SkillsControllerTests.cs` | Integration | ✅ 33/33 | ✅ Written (4 nuevos) | ✅ Passed | ✅ 4 codes (200, 400-empty, 400-length, 409) | ✅ Clean |
| 5.1 | `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` | Integration | ✅ 6/6 | ✅ Written (3 nuevos + 1 reemplazo) | ✅ Passed | ✅ 4 paths (change, conflict, invalid, readonly-absent) | ✅ 1 test redundante podado |

### Test Summary

- **Total tests written**: +20 (Dominio 3, Validator 3, Servicio 5, Repository 2, API 4, Web 4 — incluye 1 reemplazo)
- **Tests removed/replaced**: 4 (Dominio 2, Servicio 1, Web 1)
- **Total tests passing**: 214 en la suite `Habilidad` (0 regresiones)
- **Layers used**: Unit (15), Integration (5)
- **Pure functions created**: 1 (`IsActiveCodigoUniqueViolation`); `EnsureCodigoNoDuplicadoAsync` es un helper de orquestación compartido entre `Crear`/`Actualizar`.

## Hallazgos durante implementación

- **Breaking change contractual**: `PUT /api/v1/skills/{id}` ahora exige `Codigo` en el body. Documentado en `proposal.md` §"Qué cambia" y `design.md` §2.4. El fake de la API se ajustó para hacer roundtrip del valor recibido.
- **`EnsureCodigoNoDuplicadoAsync` como helper compartido**: usado por `CrearAsync` (`excludingId: null`) y `ActualizarAsync` (`excludingId: id`). Elimina duplicación y centraliza el mensaje `CodigoDuplicado`. Mismo patrón que `CargoServicioComandos`.
- **`IsActiveCodigoUniqueViolation` sin acoplar `SGV.Aplicacion` a MySQL**: la detección se hace por mensaje del `InnerException`, buscando `IX_Habilidades_ActiveCodigoUnique` + `Duplicate entry`/`1062`. Misma técnica que el helper homónimo de `CargoServicioComandos`.
- **Idempotencia de `Actualizar` con mismo código**: el `excludingId` en `EnsureCodigoNoDuplicadoAsync` permite que reenviar el `Codigo` actual no dispare `Conflict`. Esto es crítico porque el cliente web (Razor) reenvía siempre el Codigo del input, incluso si el usuario no lo modificó.
- **Pre-check + índice como safety net**: `ExistsActiveCodeAsync` es UX (mensaje claro sin esperar al `SaveChanges`); `IX_Habilidades_ActiveCodigoUnique` cubre la ventana de carrera entre el pre-check y la persistencia.

## Riesgos / pendientes

- **Diff sobre budget**: 622 changed lines en código (producción + tests) vs 400 presupuestados. El sobre-budget (~55%) viene de tests value-first atados a scenarios del delta. El código de producción es sólo 86 líneas netas. Si el orquestador decide recortar, candidatos: `UpdateAsync_MismoCodigo_NoViolaIndice`, fusionar los 400-tests del API en un `Theory`, o eliminar el `Post_Edit_WhenCodigoChanges_RedirectsWithUpdatedCodigo` (parcialmente cubierto por el roundtrip exitoso).
- **12 fallos preexistentes en `OcupacionRepositoryTests`** (issue #59): nada que ver con este change; el contrato `OcupacionRepository` no se tocó. Documentado en AGENTS.md como pendiente de otro SDD change.
- **Sin `NivelId` ni catálogo de niveles en `Habilidad`**: confirmado por `HabilidadAntiDriftTests` que siguen verdes.
- **OpenSpec deltas**: la prosa ya coincide con la implementación final; no fue necesario ajustarla.
- **Archive**: la sincronización de los deltas contra los baselines (`openspec/specs/.../spec.md`) y el `archive-report.md` se ejecutan en la fase `sdd-archive`.

## Próximos pasos

- `sdd-verify` para validar formalmente el cambio: ejecutar la suite contra MySQL, leer los delta specs y mapear cada scenario a su test verde, y firmar el verify-report.
- `sdd-archive` para sincronizar los deltas contra `openspec/specs/habilidad-management/spec.md` y `openspec/specs/habilidad-web-crear-editar/spec.md`, y generar el `archive-report.md`.
- **Decisión de budget**: si el orquestador quiere recortar, los candidatos están listados arriba; si no, el PR queda con 622 changed lines (~55% sobre budget) justificado por cobertura de scenarios.

## Batch de remediación (post-verify)

> Verdict del `sdd-verify` previo: `NEEDS-FIX`. Issues atendidos en este batch.

### Issue 1 (CRITICAL) — test web observable del scenario de reutilización

- **Scenario cubierto**: `habilidad-web-crear-editar` → "Reutilizar un Codigo liberado por baja lógica".
- **Archivo**: `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs`.
- **Test nuevo**: `Post_Edit_WhenCodigoReusedFromSoftDeleted_Succeeds`.
- **Comportamiento verificado**:
  1. Se siembran dos `HabilidadDto` (activa + soft-deleted con el mismo `Codigo`) en el fake.
  2. La soft-deleted se marca vía `await apiClient.DeleteAsync(idEliminada)` y se valida con `IsDeleted`.
  3. `UpdateResult = HabilidadCommandResult.Success(dtoActualizado)` con el `Codigo` reusado.
  4. POST del form reusa el `Codigo` soft-deleted.
  5. Asserts reales: HTTP 302, redirect a `/organizacion/habilidades/detalles/{idActiva}`, una sola llamada a `UpdateCalls[0]`, `Request.Codigo == codigoReusado`, `Request.Nombre` correcto, y la baja lógica previa se preserva (no se reactiva por accidente).
- **Strict TDD**: el scenario YA estaba implementado en backend (cubierto por `HabilidadServicioComandosTests.ActualizarAsync_CodigoDeEliminada_PermiteReutilizar`). Este test es **observability/coverage pura**, no un RED→GREEN de código de producción: la página web nunca validó unicidad localmente — delega al cliente API. Como el flujo `form → cliente API → Success → PRG` ya estaba listo, el test pasa en la primera corrida sin tocar `SGV.Web` ni el fake más allá de los seams ya existentes (`DeleteAsync`/`IsDeleted` ya estaban en `FakeHabilidadApiClient`).

### Issue 2 (WARNING) — drift de apply-progress corregido

- Conteo de commits corregido en la línea de resumen: 7 → 8 (el apply-progress previo omitía el commit `50dfe5ea docs(sdd): record apply-progress`).
- Conteo de tests `~Habilidad` revalidado contra corrida real post-batch: 213 (antes del test nuevo) → 214 (después). El apply-progress original afirmaba 214 pero el pruning de `Post_Edit_WhenCodigoUnchanged_UpdatesOtherFields` en `310ea94a` lo había bajado a 213 antes de este batch. Con el test nuevo se vuelve a 214.
- Sección "Batch de remediación (post-verify)" agregada al pie del archivo (este bloque).
- Commits post-batch: ver HEAD de la rama tras este batch; el verify-report y el resumen del orquestador registrarán el SHA final exacto.

### Issue 3 (SUGGESTION) — warnings de tooling frontend

- `bun run build` emite warnings preexistentes de `baseline-browser-mapping` y `browserslist` (data de 9 meses). NO atendidos en este batch: fuera de scope y no bloqueantes; convienen en un cambio separado de higiene de pipeline.

### Validación post-batch

| Comando | Resultado | Notas |
|---|---|---|
| `dotnet build SGV.slnx --configuration Release` | ✅ | `0 Warning(s), 0 Error(s)`. |
| `dotnet test --filter "FullyQualifiedName~Habilidad"` | ✅ | `214/214` verdes (213 previos + 1 nuevo). |
| `dotnet test SGV.slnx --no-build --configuration Release` | 🔶 | `1273` pass / `12` rojos preexistentes de `OcupacionRepositoryTests` (issue #59, fuera de scope). **0 nuevos rojos**. |
| `bun install && bun run build` (en `src/SGV.Web`) | ✅ | Mismos warnings preexistentes de tooling, no bloqueantes. |
| `openspec validate permitir-editar-el-codigo-de-una-habilidad --strict --json` | ✅ | `passed: 1, failed: 0`. |

### TDD Cycle Evidence (post-batch)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| Remediación verify (observability) | `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` | Integration | ✅ 9/9 | ✅ Written | ✅ Passed (10/10) | ➖ Single scenario del delta | ➖ None needed |

### Test Summary (post-batch)

- **Tests añadidos en este batch**: +1 (`Post_Edit_WhenCodigoReusedFromSoftDeleted_Succeeds`).
- **Total tests escritos (cumulative)**: +21 (20 del batch previo + 1 de este batch).
- **Total tests passing**: 214 en la suite `Habilidad` (0 regresiones).
- **Layers used**: Integration (1).

### Diff summary post-batch

- 1 archivo modificado: `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs`.
- Líneas añadidas: ~43 (test + comentarios).
- `apply-progress.md` actualizado in-place sin overwrite del contenido previo.
- Commit batch: HEAD post-batch del PR; el verify-report y el resumen del orquestador registran el SHA final exacto.