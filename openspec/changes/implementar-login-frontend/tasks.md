# Tasks: Implementar login frontend

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 450-650 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 auth contract+cliente+tests RED/GREEN -> PR 2 UI/login shell+logout+tests |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Base de integración auth web | PR 1 | rutas compartidas, opciones, cliente tipado, fábrica de pruebas |
| 2 | Flujo login/logout y dashboard protegido | PR 2 | depende de PR 1; layout auth, página login, logout, shell |

## Phase 1: Base de integración

- [x] 1.1 RED: crear `tests/SGV.Tests/Web/WebAuthenticationTests.cs` con escenarios de login público, redirect anónimo a `/`, login 401 y logout inválida sesión.
- [x] 1.2 GREEN: crear `src/SGV.Api/Contracts/AuthApiRoutes.cs` y ajustar `src/SGV.Api/Controllers/AuthController.cs` para reutilizar rutas públicas.
- [x] 1.3 GREEN: actualizar `src/SGV.Web/SGV.Web.csproj`, `src/SGV.Web/Program.cs`, `src/SGV.Web/appsettings*.json`, `src/SGV.Web/Integration/Auth/SgvApiOptions.cs` y `AuthApiClient.cs` con referencia a `SGV.Api`, cookie auth y `HttpClient` tipado.
- [x] 1.4 REFACTOR: extender `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` para override de `HttpMessageHandler`/servicios sin acoplar los tests a red real.

## Phase 2: Login y logout

- [x] 2.1 RED: agregar asserts de UX en `WebAuthenticationTests.cs` para ocultar register/forgot-password y mostrar error de autenticación.
- [x] 2.2 GREEN: crear `src/SGV.Web/Pages/Auth/SignIn.cshtml`, `SignIn.cshtml.cs`, `_ViewStart.cshtml` y `Pages/Shared/_AuthLayout.cshtml` usando Inspinia `Auth/SignIn` con PRG y validación server-side.
- [x] 2.3 GREEN: crear `src/SGV.Web/Pages/Auth/Logout.cshtml.cs` con handler POST-only y `SignOutAsync`.
- [x] 2.4 REFACTOR: encapsular mapeo de claims/token del login en una unidad pequeña reutilizable dentro de `src/SGV.Web/Integration/Auth/` o `Pages/Auth/`.

## Phase 3: Shell protegido

- [ ] 3.1 RED: ajustar `tests/SGV.Tests/Web/WebShellSmokeTests.cs` para que `/` redirija anónimo, renderice dashboard vacío autenticado y muestre logout.
- [ ] 3.2 GREEN: modificar `src/SGV.Web/Pages/Index.cshtml` y `Index.cshtml.cs` para requerir `[Authorize]` y mostrar dashboard vacío inicial.
- [ ] 3.3 GREEN: modificar `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` para exponer logout autenticado sin account-management adicional.
- [ ] 3.4 REFACTOR: revisar nombres, mensajes y rutas para que los PageModels consuman solo endpoints centralizados.

## Phase 4: Verificación

- [ ] 4.1 Ejecutar `dotnet test SGV.slnx --filter Web` o equivalente validando todos los escenarios Given/When/Then del cambio.
- [ ] 4.2 Ejecutar `dotnet build SGV.slnx` y registrar cualquier ajuste menor necesario sin ampliar alcance.
