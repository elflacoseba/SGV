# Verify Report (post-remediación): Permitir editar el código de una Habilidad

## Resumen ejecutivo

- **Verdict**: `READY-FOR-MERGE`
- **Fecha**: 2026-07-03
- **HEAD**: `306478d9`
- **Base branch**: develop
- **Commits**: 9
- **Diff stats**: `+1678/-99 en 23 files` (vs verify previo: `+103/-0`, mismos 23 files)
- **Tamaño**: 667 líneas ejecutables total (`162` producción + `505` tests). Vs verify previo: `622` → `667` (`+45`, consistente con el test web agregado y su soporte mínimo).

## Cierre de issues previos

### Issue 1 (CRITICAL) — Test web faltante
- Estado: ✅ **Resuelto**.
- Test agregado: `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` :: `Post_Edit_WhenCodigoReusedFromSoftDeleted_Succeeds`.
- Evidence:
  - `grep` confirma la presencia del test.
  - `dotnet test SGV.slnx --no-build --configuration Release --filter "FullyQualifiedName~Habilidad"` → `214/214`.
  - El test ejerce el path observable completo: baja lógica en el fake, POST desde Edit con `Input.Codigo` reutilizado, `UpdateCalls[0].Request.Codigo == codigoReusado`, HTTP `302`, redirect a `Details` y preservación del estado eliminado de la habilidad previa.

### Issue 2 (WARNING) — Drift apply-progress.md
- Estado: ✅ **Resuelto**.
- Conteo de commits: `7 → 8 → 9` verificado con `git rev-list --count origin/develop..HEAD`.
- Conteo de tests `~Habilidad`: `213 → 214` verificado por corrida real post-batch.
- Sección `## Batch de remediación (post-verify)` presente en `apply-progress.md` y alineada con el estado actual del branch.

### Issue 3 (SUGGESTION) — bun build warnings
- Estado: ⚠️ **Pendiente, fuera de scope**.
- `bun run build` sigue pasando, pero mantiene warnings preexistentes de tooling (`baseline-browser-mapping`, `browserslist`). No bloquea el merge de este change.

## Resumen por categoría

### A. Consistencia de artefactos SDD
- [PASS] Proposal coherente
- [PASS] Spec delta ↔ implementación (todos los scenarios cubiertos con tests runtime verdes)
- [PASS] Design ↔ código
- [PASS] Tasks cumplidas

### B. Evidencia strict TDD
- [PASS] RED→GREEN→REFACTOR documentado
- [PASS] Conventional commits sin Co-Authored-By
- [PASS] Sin tests de bajo valor
- [PASS] Cobertura observable del scenario `Reutilizar un Codigo liberado por baja lógica`

### C. Comportamiento runtime
| Comando | Resultado | Notas |
|---|---|---|
| `dotnet restore SGV.slnx` | ✅ | All projects up-to-date. |
| `dotnet build SGV.slnx --configuration Release` | ✅ | `0 Warning(s), 0 Error(s)`. |
| `dotnet test SGV.slnx --no-build --configuration Release --filter "FullyQualifiedName~Habilidad"` | ✅ `214/214` | El nuevo test web está incluido. |
| `dotnet test SGV.slnx --no-build --configuration Release` | 🔶 `1273 pass / 12 fail preexistentes / 0 nuevos` | Fallos exactamente en `OcupacionRepositoryTests` (issue #59). |
| `bun install && bun run build` | ✅ | Build OK con warnings preexistentes de tooling frontend, fuera de scope. |
| `openspec validate permitir-editar-el-codigo-de-una-habilidad --strict --json` | ✅ | `passed: 1, failed: 0`. |

### D. Cumplimiento de no-goals
- ✅ Sin `NivelId` / catálogo de niveles.
- ✅ Sin migración nueva.
- ✅ Baselines `openspec/specs/**/spec.md` intactos.
- ✅ No se copió `Cargo.Nivel`.

### E. Diff total
- Diff branch completo: `1777` líneas changed (`+1678/-99`).
- Diff ejecutable + tests: `667` líneas changed (`162` producción, `505` tests).
- Size:exception registrada y justificada por cobertura value-first de scenarios. Ratio prod:tests = `162:505` (< `10:1`).

## Detalle por scenario (mapeo ↔ tests)

### Delta spec: `habilidad-web-crear-editar`

- **Scenario**: `Editar Codigo de una Habilidad existente`
  - **Tests**:
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenCodigoChanges_RedirectsWithUpdatedCodigo`
  - **Cobertura**: ✅

- **Scenario**: `Editar otros campos sin cambiar Codigo`
  - **Tests**:
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation`
  - **Cobertura**: ✅

- **Scenario**: `Codigo inválido en edición`
  - **Tests**:
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenInvalidCodigo_ShowsValidationErrorAndKeepsForm`
    - `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs` → `Should_Have_Error_When_Codigo_Is_Empty`
    - `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs` → `Should_Have_Error_When_Codigo_Exceeds_Max_Length`
    - `tests/SGV.Tests/Api/SkillsControllerTests.cs` → `Put_EmptyCodigo_Returns400WithFieldErrors`
    - `tests/SGV.Tests/Api/SkillsControllerTests.cs` → `Put_CodigoExceedsMaxLength_Returns400WithFieldErrors`
  - **Cobertura**: ✅

- **Scenario**: `Codigo duplicado de otra Habilidad activa`
  - **Tests**:
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenConflictOnCodigo_ReturnsFieldError`
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`
    - `tests/SGV.Tests/Api/SkillsControllerTests.cs` → `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`
  - **Cobertura**: ✅

- **Scenario**: `Reutilizar un Codigo liberado por baja lógica`
  - **Tests**:
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenCodigoReusedFromSoftDeleted_Succeeds`
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_CodigoDeEliminada_PermiteReutilizar`
  - **Cobertura**: ✅

- **Scenario**: `Edit muestra Codigo editable`
  - **Tests**:
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `EditPage_MuestraCodigoEditable`
  - **Cobertura**: ✅

- **Scenario**: `Edit exitoso con cambio de Codigo mantiene PRG`
  - **Tests**:
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenCodigoChanges_RedirectsWithUpdatedCodigo`
    - `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenCodigoReusedFromSoftDeleted_Succeeds`
  - **Cobertura**: ✅

### Delta spec: `habilidad-management`

- **Scenario**: `Update con Codigo de otra Habilidad activa`
  - **Tests**:
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`
    - `tests/SGV.Tests/Api/SkillsControllerTests.cs` → `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`
  - **Cobertura**: ✅

- **Scenario**: `Update con el mismo Codigo actual`
  - **Tests**:
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_MismoCodigo_NoSeTrataComoDuplicado`
    - `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` → `UpdateAsync_MismoCodigo_NoViolaIndice`
  - **Cobertura**: ✅

- **Scenario**: `Update con Codigo de una Habilidad eliminada`
  - **Tests**:
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_CodigoDeEliminada_PermiteReutilizar`
  - **Cobertura**: ✅

- **Scenario**: `Actualización exitosa con cambio de Codigo`
  - **Tests**:
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_CodigoValido_PersisteYCambiaCodigo`
    - `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` → `UpdateAsync_ModificaCampos`
    - `tests/SGV.Tests/Api/SkillsControllerTests.cs` → `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto`
  - **Cobertura**: ✅

- **Scenario**: `Actualización exitosa sin cambiar Codigo`
  - **Tests**:
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda`
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_MismoCodigo_NoSeTrataComoDuplicado`
  - **Cobertura**: ✅

- **Scenario**: `Codigo inválido en update`
  - **Tests**:
    - `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs` → `Should_Have_Error_When_Codigo_Is_Empty`
    - `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs` → `Should_Have_Error_When_Codigo_Exceeds_Max_Length`
    - `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`
    - `tests/SGV.Tests/Api/SkillsControllerTests.cs` → `Put_EmptyCodigo_Returns400WithFieldErrors`
    - `tests/SGV.Tests/Api/SkillsControllerTests.cs` → `Put_CodigoExceedsMaxLength_Returns400WithFieldErrors`
  - **Cobertura**: ✅

## Hallazgos

### CRITICAL
- Ninguno.

### WARNING
- `bun run build` mantiene warnings preexistentes de tooling frontend (`baseline-browser-mapping`, `browserslist`). No bloquea este change.
- Sigue faltando un unit test específico del servicio que simule la carrera de `DbUpdateException` para `IX_Habilidades_ActiveCodigoUnique`; hoy la traducción está implementada y el comportamiento funcional queda cubierto por pre-check + repositorio, pero la carrera exacta no tiene test aislado de aplicación.

### SUGGESTION
- Atender la higiene de tooling frontend en un change separado.
- Agregar en otro batch un test aislado del catch `DbUpdateException` para blindar la traducción del race-condition.

## Conclusión

La remediación cerró el gap REAL que bloqueaba este change: ahora existe evidencia runtime del scenario web `Reutilizar un Codigo liberado por baja lógica`, el drift de `apply-progress.md` quedó corregido y la rama mantiene exactamente los mismos `12` rojos preexistentes de `OcupacionRepositoryTests`, sin regresiones nuevas.

**Verdict final**: `READY-FOR-MERGE`.

Comandos recomendados para el orquestador:

```bash
git push origin HEAD
gh pr create --base develop --fill
```
