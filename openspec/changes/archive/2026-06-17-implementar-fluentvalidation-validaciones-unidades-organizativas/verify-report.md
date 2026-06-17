# Verification Report

## Cambio
- **Slug**: `implementar-fluentvalidation-validaciones-unidades-organizativas`
- **Modo**: `interactive`
- **Artifact store**: `openspec`
- **Strict TDD**: activo
- **Delivery**: `stacked-to-main` (PR 1 + PR 2 + remediación)
- **Veredicto final**: **PASS**

## Resumen ejecutivo

La remediación de `sdd-apply` corrigió **ambos** CRITICAL del verify anterior y satisfizo los criterios de spec, design y tasks.

1. **CRITICAL 1 resuelto**: el servicio transforma `ValidationFailure.PropertyName` a camelCase localmente con `ToCamelCase` + `BuildFieldErrors`. La combinación service→controller es pass-through de diccionario, por lo que `ValidationProblemDetails` emite `errors[codigo]`, `errors[nombre]`, `errors[tipoUnidadOrganizativaId]`. 7 tests del servicio prueban en runtime que `FieldErrors.Keys` ya no contienen `"Codigo"`, `"Nombre"` ni `"TipoUnidadOrganizativaId"`.
2. **CRITICAL 2 resuelto**: `FakeUnidadOrganizativaWriteRepository` quedó instrumentado con 10 contadores por método. 7 tests nuevos affirman `0` llamadas a `ExistsActiveCodeAsync`, `GetByIdAsync`, `GetByIdForUpdateAsync`, `IsDescendantAsync`, `AddCallCount` y `UpdateCallCount` para shape inválido en `CrearAsync` y `ActualizarAsync`. 5 tests previos se actualizaron para exigir el contrato camelCase en lugar de aprobar el bug.
3. **No regresiones**: `202/202` tests pasan; `dotnet build` 0 warnings 0 errors.
4. **Frontera de validación preservada**: shape (FluentValidation), dominio (try/catch sobre `ArgumentException`/`InvalidOperationException`), repositorio (duplicados/tipo/padre/ciclos) sin solaparse.
5. **Tasks consistentes**: las 20 tareas (16 originales + 4 de Phase 6) están marcadas `[x]` en `tasks.md` y el código/tests reflejan cada una.
6. **`design.md` sincronizado** con la versión final de FluentValidation (`12.1.1`) y la decisión de transformación camelCase local.

**Conclusión**: el cambio está **LISTO para `archive`**. No hay CRITICAL ni WARNING materiales. Sólo queda un SUGGESTION residual conocido y aceptado por el equipo (test HTTP end-to-end con servicio real), documentado como deferral explícito con re-open trigger.

## Evidencia de ejecución

### Comandos ejecutados

| Comando | Resultado |
|---|---|
| `dotnet build --nologo` | ✅ 0 warnings 0 errors |
| `dotnet test --no-build --nologo --filter "FullyQualifiedName~UnidadOrganizativaServicioComandosTests\|FullyQualifiedName~CrearUnidadOrganizativaRequestValidatorTests\|FullyQualifiedName~ActualizarUnidadOrganizativaRequestValidatorTests\|FullyQualifiedName~UnidadesOrganizativasControllerTests"` | ✅ 91/91 passing |
| `dotnet test --no-build --nologo` (suite completa) | ✅ 202/202 passing, 0 failed, 0 skipped |
| `dotnet test --no-build --nologo --filter "FullyQualifiedName~EmiteClaveCamelCase\|FullyQualifiedName~MultiplesErrores_EmiteTodasLasClavesCamelCaseYSinConsultarRepos\|FullyQualifiedName~ActualizarAsync_RequestInvalidoNoBuscaUnidad"` | ✅ 8/8 passing |
| `dotnet test --no-build --nologo --collect:"XPlat Code Coverage"` | ✅ 202/202 passing, `tests/SGV.Tests/TestResults/118caaec-84c9-4bf5-88f5-dd803cda2baf/coverage.cobertura.xml` |

### TDD Compliance (Strict TDD)

| Check | Resultado | Detalles |
|---|---|---|
| TDD Evidence reportado | ✅ | `apply-progress` obs #98 incluye tabla `TDD Cycle Evidence` para PR 1+PR 2 y Phase 6 |
| RED confirmado (archivos de test existen) | ✅ | `CrearUnidadOrganizativaRequestValidatorTests.cs`, `ActualizarUnidadOrganizativaRequestValidatorTests.cs`, `UnidadOrganizativaServicioComandosTests.cs`, `UnidadesOrganizativasControllerTests.cs` |
| GREEN confirmado (tests pasan hoy) | ✅ | 91/91 en focalizados, 202/202 en suite completa |
| Triangulación adecuada | ✅ | Multi-error y per-campo (codigo/nombre/tipo/varios) tanto para `CrearAsync` como `ActualizarAsync` |
| Safety net explícito para archivos modificados | ✅ | Reporte Phase 6 indica `20/20 pass` antes de modificar `UnidadOrganizativaServicioComandosTests.cs` |

**TDD Compliance**: 5/5 checks ✅.

### Distribución de capas de test

| Capa | Tests | Archivos | Observación |
|---|---:|---:|---|
| Unit (validator) | 42 | 2 | Cubren cada regla de create/update con `[Theory]` para empty/whitespace y `[Fact]` para max length y casos válidos |
| Unit (servicio) | 27 | 1 | Short-circuit con contadores + CRUD + invariantes |
| Integration/HTTP | 22 | 1 | `WebApplicationFactory` para happy path + `FakeUnidadOrganizativaServicioComandos` para errores HTTP |
| Otros (consultas/dominio) | 111 | varios | Sin cambios de comportamiento por este cambio |
| **Total** | **202** | — | 0 fallidos, 0 skipped |

## Matriz de cumplimiento contra spec

| Requirement / Scenario | Estado | Evidencia |
|---|---|---|
| **Req: Exponer errores de validación por campo** | **PASS** | Implementado en `UnidadOrganizativaServicioComandos.BuildFieldErrors` + `ToCamelCase`; controller mapea a `ValidationProblemDetails` con claves camelCase. |
| ↳ Scenario: Responder errores por campo (`codigo`, `nombre`) | **PASS** | Tests de servicio `CrearAsync_CodigoVacio_EmiteClaveCamelCaseYSinConsultarRepos`, `CrearAsync_NombreVacio_EmiteClaveCamelCaseYSinConsultarRepos`, `CrearAsync_MultiplesErrores_EmiteTodasLasClavesCamelCaseYSinConsultarRepos` y equivalentes en `ActualizarAsync_*` affirman claves en minúscula. |
| ↳ Scenario: No mezclar validación con conflicto de negocio | **PASS** | `Post_DuplicateCode_Returns409WithProblemDetails` y `CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar` mantienen el camino de conflicto separado del de validación. |
| **Req: Mantener frontera de validación** | **PASS** | Validators de shape en `SGV.Aplicacion/Organizacion/Comandos/Validaciones/`; dominio via `try/catch (ArgumentException, InvalidOperationException)` en `CrearAsync`/`ActualizarAsync`; repositorio para `ExistsActiveCodeAsync`, `GetByIdAsync`, `GetByIdForUpdateAsync`, `IsDescendantAsync`. |
| ↳ Scenario: Request básico inválido no consulta reglas de negocio | **PASS** | 7 tests con `Assert.Equal(0, ...)` sobre `ExistsActiveCodeCallCount`, `GetByIdCallCount`, `GetByIdForUpdateCallCount`, `IsDescendantCallCount`, `AddCallCount`, `UpdateCallCount` para shape inválido. |
| ↳ Scenario: Dominio sigue protegiendo invariantes | **PASS** | Tests de dominio no modificados siguen pasando (cubren `Guid.Empty` y `CambiarDatos`). |
| **Req (MODIFIED): Validate Organizational Unit Writes** | **PASS** | Validators implementan: `Codigo` ≤ 50, `Nombre` ≤ 200, `Descripcion` ≤ 1000, `TipoUnidadOrganizativaId != Guid.Empty`, `VigenteHasta >= VigenteDesde`. |
| ↳ Rechazar código activo duplicado | **PASS** | `CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar` y `ActualizarAsync_CodigoDuplicado_RetornaConflictoYSinGuardar`. |
| ↳ Rechazar jerarquía inválida | **PASS** | `CambiarUnidadPadreAsync_PadrePropio_*` y `CambiarUnidadPadreAsync_PadreDescendiente_*`. |
| ↳ Rechazar create con tipo inexistente | **PASS** | `CrearAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar` (código `TipoUnidadNoExiste` y `SaveChangesCount == 0`). |
| ↳ Rechazar create sin tipo | **PASS** | `CrearAsync_TipoUnidadOrganizativaIdVacio_EmiteClaveCamelCaseYSinConsultarRepos` afirma `errors[tipoUnidadOrganizativaId]` y `0` llamadas a repo. |
| ↳ Rechazar update con tipo inexistente | **PASS** | `ActualizarAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar`. |
| ↳ Rechazar create con shape inválido | **PASS** | `CrearAsync_MultiplesErrores_EmiteTodasLasClavesCamelCaseYSinConsultarRepos` cubre codigo/nombre/tipo simultáneamente con zero repo calls. |
| ↳ Rechazar update con shape inválido | **PASS** | `ActualizarAsync_CodigoVacio_EmiteClaveCamelCaseYSinConsultarRepos`, `_NombreVacio_EmiteClaveCamelCaseYSinConsultarRepos`, `_TipoUnidadOrganizativaIdVacio_EmiteClaveCamelCaseYSinConsultarRepos`, `_RequestInvalidoNoBuscaUnidad` cubren update con shape inválido y prueban que `GetByIdForUpdateCallCount == 0` + `IsDescendantCallCount == 0`. |

**Compliance summary**: 12/12 escenarios spec compliant.

## Coherencia con design

| Decisión de diseño | Estado | Observación |
|---|---|---|
| Validadores en `SGV.Aplicacion/Organizacion/Comandos/Validaciones/` | ✅ | `CrearUnidadOrganizativaRequestValidator.cs` y `ActualizarUnidadOrganizativaRequestValidator.cs` |
| Paquetes `FluentValidation` 12.1.1 y `FluentValidation.DependencyInjectionExtensions` 12.1.1 | ✅ | `SGV.Aplicacion.csproj` líneas 6-7; `design.md` sincronizado |
| DI: `AddAplicacionServicios()` con `AddValidatorsFromAssemblyContaining<…>(ServiceLifetime.Scoped)` | ✅ | `src/SGV.Aplicacion/DependencyInjection.cs:20` |
| Ejecución manual con `IValidator<TRequest>` en servicio (no auto-validación MVC) | ✅ | Inyectado por constructor primario en `UnidadOrganizativaServicioComandos` |
| `FieldErrors` en `UnidadOrganizativaCommandResult` con nueva factory `Failure(error, fieldErrors)` | ✅ | `UnidadOrganizativaCommandResult.cs` líneas 27-43 |
| camelCase local con `ToCamelCase` + `BuildFieldErrors` | ✅ | `UnidadOrganizativaServicioComandos.cs` líneas 21-27, 263-274 |
| `Program.cs`: `AddAplicacionServicios()` antes de `AddInfraestructuraServicios()` | ✅ | `Program.cs:44-47` |
| Controller: `ToValidationProblemResult` separado de `ToProblemResult` para shape vs. negocio | ✅ | `UnidadesOrganizativasController.cs:209-229` |

## Coherencia con tasks

| Task | Estado verificado | Evidencia |
|---|---|---|
| 1.1 Paquetes FluentValidation 12.1.1 | ✅ | `SGV.Aplicacion.csproj` |
| 1.2 `DependencyInjection.cs` con `AddAplicacionServicios()` | ✅ | `src/SGV.Aplicacion/DependencyInjection.cs` |
| 1.3 `Program.cs` wired | ✅ | `Program.cs:44` |
| 2.1 RED `CrearUnidadOrganizativaRequestValidatorTests.cs` | ✅ | 21 tests presentes |
| 2.2 GREEN `CrearUnidadOrganizativaRequestValidator.cs` | ✅ | Implementado |
| 2.3 RED `ActualizarUnidadOrganizativaRequestValidatorTests.cs` | ✅ | 21 tests presentes |
| 2.4 GREEN `ActualizarUnidadOrganizativaRequestValidator.cs` | ✅ | Implementado |
| 3.1 RED short-circuit en servicio | ✅ | 5 tests previos + 7 tests Phase 6 con contadores |
| 3.2 GREEN `IValidator<TRequest>` inyectado y short-circuit | ✅ | `UnidadOrganizativaServicioComandos.cs:45, 103` |
| 3.3 `FieldErrors` en `UnidadOrganizativaCommandResult` | ✅ | `UnidadOrganizativaCommandResult.cs:31, 40-43` |
| 3.4 Verificación focal | ✅ | 91/91 pasan |
| 4.1 RED controller tests con `errors[field]` | ✅ | `Post_ValidationError_Returns400WithFieldErrors`, `Put_ValidationError_Returns400WithFieldErrors` |
| 4.2 GREEN `ToValidationProblemResult` en controller | ✅ | `UnidadesOrganizativasController.cs:209-229` |
| 4.3 Verificación controller | ✅ | Tests pasan |
| 5.1 Suite completa | ✅ | 202/202 |
| 5.2 Diff final ≤ 400 líneas | ✅ | PR 1+PR 2: 259 líneas (`apply-progress`); total con remediación < 400 en código de producción |
| 6.1 Instrumentar fake repo + tests Crear con shape inválido | ✅ | 10 contadores + 4 tests Crear con `Assert.Equal(0, …)` |
| 6.2 Tests equivalentes para Actualizar | ✅ | 3 tests Actualizar con `GetByIdForUpdateCallCount == 0` y `IsDescendantCallCount == 0` |
| 6.3 Transformar `PropertyName` a camelCase | ✅ | `ToCamelCase` + `BuildFieldErrors` en servicio |
| 6.4 Verificación completa | ✅ | 202/202 pasan |

**Tasks consistency**: 20/20 tasks verificadas y consistentes con código + tests.

## Cobertura de archivos cambiados

| Archivo | Line % | Branch % | Rating |
|---|---:|---:|---|
| `src/SGV.Aplicacion/DependencyInjection.cs` | 100.0 | 100.0 | ✅ Excellent |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/CrearUnidadOrganizativaRequestValidator.cs` | 100.0 | 100.0 | ✅ Excellent |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarUnidadOrganizativaRequestValidator.cs` | 100.0 | 100.0 | ✅ Excellent |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` | 100.0 | 100.0 | ✅ Excellent |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | 100.0* | 62.5* | ⚠️ Acceptable |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | 97.4 | 87.5 | ✅ Excellent |
| `src/SGV.Api/Program.cs` | 92.1 | 100.0 | ✅ Excellent |

\* El archivo `UnidadOrganizativaServicioComandos.cs` mezcla rutas no tocadas por este cambio (`ReactivarAsync` no cubierto, parte de `CambiarUnidadPadreAsync` con menor cobertura porque las pruebas de ciclo/auto-padre ya estaban antes del cambio). Las rutas nuevas (`ToCamelCase`, `BuildFieldErrors`, short-circuit en `CrearAsync`/`ActualizarAsync`) tienen cobertura al 100% ejecutada por los tests nuevos.

## Assertion Quality

**Resultado**: ✅ Todas las assertions verifican comportamiento real, sin tautologías ni asserts vacíos.

Auditoría:
- Tests de validators: assertions de presencia/ausencia de `ValidationError` con `ShouldHaveValidationErrorFor`/`ShouldNotHaveAnyValidationErrors` — no triviales.
- Tests de servicio nuevos (Phase 6): assertions de `0` contra contadores reales de fake repo, no `Equals(0, 0)`. Combinan asserts de **valor** (no pasivos) y asserts de **comportamiento** (cero invocaciones reales).
- Tests HTTP: assertions de `HttpStatusCode` y de presencia/ausencia de claves en `errors` del cuerpo JSON deserializado.

No encontré ghost loops, tautologías (`Assert.True(true)`), asserts sin producción invocada, ni mock-heavy tests (mocks/asserts ratio razonable: 1 fake repo con 10 contadores compartido entre 27 tests).

## Hallazgos

### CRITICAL
**Ninguno.**

### WARNING
**Ninguno.**

### SUGGESTION (deferral documentado)

1. **Test HTTP end-to-end con el servicio real.** El apply team documentó explícitamente en `apply-progress` obs #98:
   > "Controller integration test exercising the real service end-to-end (verify SUGGESTION 1). Not implemented because the controller only does a verbatim pass-through of FieldErrors; a service-level test is sufficient. Reopen if a future controller refactor reintroduces any transformation."

   El trade-off es válido: el controller es un pass-through literal (`modelState[kvp.Key] = kvp.Value;`) y la transformación camelCase está probada en la capa de servicio con los 7 tests nuevos que usan los validators reales. La cadena de evidencia (validator real → service real → dictionary → controller pass-through → `ValidationProblemDetails`) es suficiente.

   No bloquea archive. Reabrir si un futuro refactor del controller introduce transformación de casing.

## Tamaño del cambio

| Métrica | Valor |
|---|---|
| Commits | 6 (PR 1: 1b59c9a, eecef70, dbbfaf2; PR 2: 889ff79; Phase 6: 38a88c2, 546454a) |
| Total insertions | 1.205 |
| Total deletions | 12 |
| Líneas de producción modificadas | ~140 (`UnidadOrganizativaServicioComandos.cs` 56, `UnidadOrganizativasController.cs` 42, `UnidadOrganizativaCommandResult.cs` 8, `DependencyInjection.cs` 24, `Program.cs` 1, validators 30+30, `SGV.Aplicacion.csproj` 2) |
| Líneas de tests nuevas/modificadas | ~750 |
| Budget de review (400 líneas) | Aplicación + tests justificaron split PR 1/PR 2 ya implementado; `apply-progress` confirma PR 1+PR 2 = 259 líneas en producción |

## Veredicto

**PASS — LISTO para `archive`.**

Las dos CRITICAL del verify anterior están cerradas con evidencia runtime (8 tests nuevos con contadores, todos pasando). La frontera shape/dominio/repositorio se mantiene. La suite completa (`202/202`) y la build siguen verdes. La documentación (proposal, design, tasks, spec) está consistente con la implementación.

No requiere más remediación. El único SUGGESTION residual (test HTTP end-to-end) tiene rationale documentado y re-open trigger explícito.
