# Design: Endurecer autorización de los controllers restantes del API

## Enfoque técnico

Default-deny global en `Program.cs` + decoraciones explícitas en controllers. Una sola excepción anónima en `POST /api/v1/auth/login` vía `[AllowAnonymous]`. Patrón vigente en `CargosController`, `PuestosController`, `SkillsController` y `NivelesHabilidadController`. Specs: `persona-management`, `unidad-organizativa-crud`, `nivel-cargo-catalog`, `tipo-unidad-organizativa-catalog`, `sgv-readonly-api`.

## Decisiones de arquitectura

| Decisión | Opción | Tradeoffs | Elección |
|---|---|---|---|
| Política global | (a) `FallbackPolicy` en `AddAuthorization`; (b) `[Authorize]` por controller; (c) middleware custom | (a) atrapa controllers nuevos, una sola línea en host; (b) repite y abre olvidos; (c) invierte default-deny | **(a)** `AuthorizationPolicy { RequireAuthenticatedUser() }` en `AuthorizationOptions.FallbackPolicy` |
| Excepción anónima | (a) `[AllowAnonymous]`; (b) endpoint fuera de `MapControllers` | (a) simétrico con resto de la API; (b) rompe contrato REST unificado | **(a)** decoración única en `Login` |
| Rol admin | (a) `[Authorize(Roles = RolesSgv.Administrador)]` por acción; (b) policy nominal `RequireAdmin` | (a) vigente en Cargos/Puestos; (b) indirección sin valor | **(a)** patrón literal |
| Tests 401/403 | (a) `[Theory] + [InlineData]`; (b) un `[Fact]` por mutación | (a) replica `CargosControllerTests.cs:319-339`; (b) duplica ~30 métodos | **(a)** un `[Theory]` por controller mutante |

## Flujo de datos

```
HTTP request -> JwtBearer auth -> Authorization middleware
   -> FallbackPolicy.RequireAuthenticatedUser (sin auth -> 401)
   -> [AllowAnonymous] bypass
   -> [Authorize(Roles=Administrador)] (rol ausente -> 403)
   -> Controller action
```

## Cambios de archivos

| Archivo | Acción | Resumen |
|---|---|---|
| `src/SGV.Api/Program.cs` | Modificar | Reemplazar `AddAuthorization()` por `AddAuthorization(opts => opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())` |
| `src/SGV.Api/Controllers/PersonasController.cs` | Modificar | `[Authorize]` clase; admin override en `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill` |
| `src/SGV.Api/Controllers/OcupacionesController.cs` | Modificar | `[Authorize]` clase; admin override en `Create`, `Update`, `Finalize`, `Reactivate`, `Delete` |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modificar | `[Authorize]` clase; admin override en `Create`, `Update`, `ChangeParent`, `Delete`, `Reactivate` |
| `src/SGV.Api/Controllers/NivelesCargoController.cs` | Modificar | `[Authorize]` clase (sin overrides) |
| `src/SGV.Api/Controllers/TipoUnidadesOrganizativasController.cs` | Modificar | `[Authorize]` clase |
| `src/SGV.Api/Controllers/AuthController.cs` | Modificar | `[AllowAnonymous]` en `Login` |
| `tests/SGV.Tests/Api/PersonasControllerTests.cs` | Modificar | Migrar ~14 llamantes a `CreateAdminClient`; invertir chequeo `Authorize`; `[Theory]` 401; `[Fact]` 403 por mutación |
| `tests/SGV.Tests/Api/OcupacionesControllerTests.cs` | Modificar | Eliminar `IClassFixture<WebApplicationFactory<SGV.Api.Program>>` y `_factory` muerto; migrar a `ApiWebApplicationFactory`; `[Theory]` 401; 403 por mutación; invertir chequeo |
| `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Modificar | Migrar ~32 llamantes; `[Theory]` 401; 403 por mutación; invertir chequeo |
| `tests/SGV.Tests/Api/NivelesCargoControllerTests.cs` | Modificar | 401 anónimo en `GetAll`/`GetById`; invertir chequeo |
| `tests/SGV.Tests/Api/TipoUnidadesOrganizativasControllerTests.cs` | Modificar | 401 anónimo; invertir chequeo |
| `tests/SGV.Tests/Api/AuthControllerTests.cs` | Modificar | Agregar `Login_AnonymousHeaderless_Returns200` |
| `docs/decisiones-implementacion.md` | Modificar | Entrada "Default-deny global y `[AllowAnonymous]` único en Login" |

## Contratos / interfaces

Sin interfaces nuevas. Reutiliza `AuthorizeAttribute`, `AllowAnonymousAttribute`, `AuthorizationPolicyBuilder` y la constante `RolesSgv.Administrador`.

## Estrategia de tests

| Capa | Alcance | Enfoque |
|---|---|---|
| Integración API | 401 anónimo (`[Theory]` por método), 403 no-admin (`[Fact]` por mutación), `2xx` admin (`CreateAdminClient`), `Controller_HasAuthorizeAttribute` por reflexión, Login accesible sin credenciales | `ApiWebApplicationFactory` + `CreateAdminClient/CreateNonAdminClient`. Migrar `CreateClient()` en `2xx` antes de activar fallback |
| Compatibilidad web | `SgvWebApplicationFactory` confirma flujo cookie→JWT | `ApiBearerTokenHandler` ya inyecta `Authorization: Bearer` |

## Plan de implementación y partición (review budget 400 líneas)

Cada slice debe dejar la suite verde por sí sola; por eso **los tests 401/403 nunca pueden entrar antes que el endurecimiento del controller que los hace verdaderos**. La secuencia segura es endurecer por vertical slice (controller + tests del mismo recurso) y dejar la `FallbackPolicy` global para el final, cuando ya no queden controllers anónimos accidentales.

**(PR-A)** `PersonasController` + `PersonasControllerTests`: agregar `[Authorize]` de clase, overrides admin en las 6 mutaciones, migrar asserts `2xx` a `CreateAdminClient()`, agregar `[Theory]` de `401` y cobertura `403` no-admin. Este slice es autocontenido y replica exactamente el patrón de `CargosController`.

**(PR-B)** `OcupacionesController` + `UnidadesOrganizativasController` + sus tests: migrar `OcupacionesControllerTests` a `ApiWebApplicationFactory`, cambiar `CreateClient()` por `CreateAdminClient()` donde corresponda, agregar matriz `401/403`, y endurecer ambos controllers en el mismo PR. Si el volumen de `UnidadesOrganizativasControllerTests` (~32 llamadas) empuja el diff por encima del budget, este slice se divide en **PR-B1 Ocupaciones** y **PR-B2 Unidades Organizativas**.

**(PR-C)** `NivelesCargoController` + `TipoUnidadesOrganizativasController` + `Program.cs` + `AuthController` + docs/tests de catálogos: agregar `[Authorize]` a los dos catálogos read-only, activar `FallbackPolicy` global en `Program.cs`, marcar `AuthController.Login` con `[AllowAnonymous]`, agregar `401` anónimo y `2xx` autenticado en los tests de catálogos, y actualizar `docs/decisiones-implementacion.md`. La `FallbackPolicy` entra recién acá porque para este punto todos los controllers fuera de `Login` ya quedaron explícitamente protegidos.

Si el forecast final queda por debajo de 400 líneas, `sdd-tasks` puede fusionar **PR-A + PR-B** en un único PR de mutaciones y dejar **PR-C** como cierre de postura global. Si supera el budget, la válvula natural es separar `Ocupaciones` de `Unidades Organizativas`, no adelantar tests antes del código.

## Compatibilidad y migración

`SGV.Web` no se rompe: `ApiBearerTokenHandler` inyecta `Authorization: Bearer <jwt>` desde el login (smoke `tests/SGV.Tests/Web/`). `NivelesCargo` y `TipoUnidadesOrganizativas` pasan de anónimos a autenticados: sin token devuelven 401 — documentado en `decisiones-implementacion.md` y spec `sgv-readonly-api`. Sin migración de datos ni feature flags; `[AllowAnonymous]` en `Login` preserva el único endpoint público.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Tests `2xx` fallan tras activar fallback porque usan `CreateClient()` | Migrar a `CreateAdminClient` en PR-B antes de `dotnet test SGV.slnx` como gate |
| Olvido de override admin en una mutación | `[Theory]` cubriendo cada método HTTP mutante + chequeo de reflexión |
| `[AllowAnonymous]` se omite y `Login` queda 401 | `Login_AnonymousHeaderless_Returns200` + existente `Login_WithValidCredentials_ReturnsAccessToken` |
| Consumidores externos leían catálogos y rompen | Documentar en decisiones-implementacion y spec `sgv-readonly-api` |

## Non-goals

No tocar `PuestosController`. No crear policies nominales nuevas. No modificar `RolesSgv`, JWT, Identity ni `ApiBearerTokenHandler`. No feature flags ni migración de datos.

## Rollback

Revertir atributos en los 5 controllers, remover `FallbackPolicy` de `Program.cs`, revertir deltas en `openspec/specs/`, revertir `decisiones-implementacion.md`, `git revert <commit>` por archivo. Reejecutar `dotnet test SGV.slnx` para confirmar suite verde.