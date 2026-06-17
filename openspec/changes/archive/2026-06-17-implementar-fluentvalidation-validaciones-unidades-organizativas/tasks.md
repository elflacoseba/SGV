# Tasks: Implementar FluentValidation para validaciones de UnidadesOrganizativas

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas cambiadas estimadas | 380-520 |
| Riesgo de superar 400 líneas | Alto |
| PRs encadenados recomendados | Sí |
| Split sugerido | PR 1 validadores+DI+tests base → PR 2 servicio+errores+API tests |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Paquetes, DI, validadores create/update y tests RED/GREEN de validators | PR 1 | Primera tajada pensada para quedar bajo 400 líneas |
| 2 | Short-circuit en servicio, field errors y mapeo HTTP con tests | PR 2 | Depende de PR 1 |

## Phase 1: Foundation / Paquete y DI

- [x] 1.1 Agregar `FluentValidation` y `FluentValidation.DependencyInjectionExtensions` en `src/SGV.Aplicacion/SGV.Aplicacion.csproj` sin incorporar auto-validación MVC.
- [x] 1.2 Crear `src/SGV.Aplicacion/DependencyInjection.cs` con `AddAplicacionServicios()` y registro de validadores desde el assembly de Aplicación.
- [x] 1.3 Actualizar `src/SGV.Api/Program.cs` para usar `AddAplicacionServicios()` antes o junto a `AddInfraestructuraServicios()`; verificación: `dotnet build`.

## Phase 2: Validadores de requests (TDD)

- [x] 2.1 RED: crear `tests/SGV.Tests/Aplicacion/Organizacion/CrearUnidadOrganizativaRequestValidatorTests.cs` con `[Theory]`/`[Fact]` para `codigo`, `nombre`, `descripcion`, `tipoUnidadOrganizativaId` y vigencia.
- [x] 2.2 GREEN: crear `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/CrearUnidadOrganizativaRequestValidator.cs` con reglas de shape y nombres de campo camelCase.
- [x] 2.3 RED: crear `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarUnidadOrganizativaRequestValidatorTests.cs` cubriendo shape inválido y request válido de update.
- [x] 2.4 GREEN: crear `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarUnidadOrganizativaRequestValidator.cs`; verificación: correr tests de validators.

## Phase 3: Servicio de comandos y mapping de errores

- [x] 3.1 RED: extender `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` para probar short-circuit sin repositorios ni `SaveChangesAsync` en create/update inválidos.
- [x] 3.2 GREEN: modificar `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` para inyectar `IValidator<TRequest>` y validar antes de duplicados, tipo, padre o carga por update.
- [x] 3.3 Modificar `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` para transportar `FieldErrors` internos sin romper conflictos o not-found existentes.
- [x] 3.4 Verificación: `dotnet test --filter "UnidadOrganizativaServicioComandosTests|CrearUnidadOrganizativaRequestValidatorTests|ActualizarUnidadOrganizativaRequestValidatorTests"`.

## Phase 4: API y contrato HTTP

- [x] 4.1 RED: ajustar `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` para exigir `400` con `errors[codigo]`/`errors[nombre]` y mantener `409` para duplicado.
- [x] 4.2 GREEN: modificar `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` para mapear `FieldErrors` a `ValidationProblemDetails` y conservar `ProblemDetails` actual para negocio.
- [x] 4.3 Verificación: `dotnet test --filter UnidadesOrganizativasControllerTests`.

## Phase 5: Cierre y verificación final

- [x] 5.1 Ejecutar `dotnet test` para validar escenarios de spec: errores por campo, short-circuit y separación entre shape y conflicto.
- [x] 5.2 Revisar diff final contra el presupuesto; si supera ~400 líneas, aplicar los work units propuestos como PRs encadenados.

## Phase 6: Remediación (verify-report CRITICAL)

> Hallazgos de `verify-report.md` que bloquean el archive. Solo remediación: no rehace PR 1/PR 2.

- [x] 6.1 RED: instrumentar `FakeUnidadOrganizativaWriteRepository` con contadores por método y añadir tests que afirmen (a) claves `FieldErrors` en camelCase (`codigo`, `nombre`, `tipoUnidadOrganizativaId`) y (b) cero llamadas a `ExistsActiveCodeAsync`, `GetByIdAsync`, `GetByIdForUpdateAsync`, `IsDescendantAsync`, `AddAsync`/`Guardar` y `UpdateAsync` para `CrearAsync` con shape inválido.
- [x] 6.2 RED: añadir tests equivalentes para `ActualizarAsync` (incluye `GetByIdForUpdateCallCount == 0` y `IsDescendantCallCount == 0`) cubriendo `codigo` vacío, `nombre` vacío y `TipoUnidadOrganizativaId` `Guid.Empty`.
- [x] 6.3 GREEN: en `UnidadOrganizativaServicioComandos`, transformar `ValidationFailure.PropertyName` a camelCase al construir `FieldErrors` (decisión: transformación local en el servicio; no se configura FluentValidation global para no afectar otros validators del proyecto).
- [x] 6.4 Verificación: `dotnet test` completo y `--filter UnidadOrganizativaServicioComandosTests|UnidadesOrganizativasControllerTests`.
