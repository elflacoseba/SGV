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

- [ ] 3.1 RED: extender `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` para probar short-circuit sin repositorios ni `SaveChangesAsync` en create/update inválidos.
- [ ] 3.2 GREEN: modificar `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` para inyectar `IValidator<TRequest>` y validar antes de duplicados, tipo, padre o carga por update.
- [ ] 3.3 Modificar `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` para transportar `FieldErrors` internos sin romper conflictos o not-found existentes.
- [ ] 3.4 Verificación: `dotnet test --filter "UnidadOrganizativaServicioComandosTests|CrearUnidadOrganizativaRequestValidatorTests|ActualizarUnidadOrganizativaRequestValidatorTests"`.

## Phase 4: API y contrato HTTP

- [ ] 4.1 RED: ajustar `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` para exigir `400` con `errors[codigo]`/`errors[nombre]` y mantener `409` para duplicado.
- [ ] 4.2 GREEN: modificar `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` para mapear `FieldErrors` a `ValidationProblemDetails` y conservar `ProblemDetails` actual para negocio.
- [ ] 4.3 Verificación: `dotnet test --filter UnidadesOrganizativasControllerTests`.

## Phase 5: Cierre y verificación final

- [ ] 5.1 Ejecutar `dotnet test` para validar escenarios de spec: errores por campo, short-circuit y separación entre shape y conflicto.
- [ ] 5.2 Revisar diff final contra el presupuesto; si supera ~400 líneas, aplicar los work units propuestos como PRs encadenados.
