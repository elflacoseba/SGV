# Proposal: Endurecer autorización de los controllers restantes del API

## Why

La auditoría de seguridad de julio 2026 (issue #96) detectó que el endurecimiento aplicado sobre `CargosController` (archive `2026-07-01-2026-07-01-cargos-crear-autorizacion-admin`) y `PuestosController` (issue #90 ya cerrado) queda incompleto: tres controllers mutantes (`PersonasController`, `OcupacionesController`, `UnidadesOrganizativasController`) todavía exponen `POST/PUT/PATCH/DELETE` sin autenticación ni control de rol, y los dos controllers read-only (`NivelesCargoController`, `TipoUnidadesOrganizativasController`) son los únicos endpoints públicos anónimos que sobreviven una vez que se introduzca una fallback policy global.

Si esto no se corrige antes de seguir sumando módulos (próximos slices web/API previstos en julio/agosto), el blast radius es doble: (a) cualquier futuro controller nuevo podría colarse sin autorización y arrastrar la deuda de seguridad a todo el API, y (b) las pruebas de integración nuevas seguirían escritas contra un modelo de "API abierta" que ya no representa la postura de seguridad vigente para datos sensibles (personas, ocupaciones, estructura organizativa). Cerrar esto en un mismo change reduce el riesgo de inconsistencias entre tests, docs y policies.

## What Changes

### Código de producción (`src/SGV.Api/`)
- `Controllers/PersonasController.cs`: agregar `using Microsoft.AspNetCore.Authorization;` y `using SGV.Aplicacion.Seguridad;`, decorar la clase con `[Authorize]` y aplicar `[Authorize(Roles = RolesSgv.Administrador)]` en `Create`, `Update`, `Delete`, `Reactivate`, `AsignarSkill`, `QuitarSkill`.
- `Controllers/OcupacionesController.cs`: misma mecánica que Personas en `Create`, `Update`, `Finalizar`, `Reactivar`, `Delete`.
- `Controllers/UnidadesOrganizativasController.cs`: misma mecánica en `Create`, `Update`, `UpdatePadre`, `Reactivar`, `Delete`.
- `Controllers/NivelesCargoController.cs`: agregar `[Authorize]` a nivel clase (read-only autenticado).
- `Controllers/TipoUnidadesOrganizativasController.cs`: agregar `[Authorize]` a nivel clase (read-only autenticado).
- `Controllers/AuthController.cs`: decorar la acción `Login` con `[AllowAnonymous]` para que sobreviva la fallback policy global.
- `Program.cs`: introducir una fallback policy `AuthorizationPolicy` con `RequireAuthenticatedUser()` aplicada con `FallBackPolicy` en `AddAuthorization`, junto al setup actual de `AddAuthentication`/`AddJwtBearer`. Esta policy es la garantía de default-deny para futuros controllers.

### Tests de integración (`tests/SGV.Tests/Api/`)
- `ApiWebApplicationFactory.cs`: ya expone `CreateAdminClient()`/`CreateNonAdminClient()` reutilizables; sin cambios nuevos (el patrón está vigente desde el cierre del change de cargos).
- `PersonasControllerTests.cs`, `OcupacionesControllerTests.cs`, `UnidadesOrganizativasControllerTests.cs`, `NivelesCargoControllerTests.cs`, `TipoUnidadesOrganizativasControllerTests.cs`: migrar de `factory.CreateClient()` a `factory.CreateAdminClient()` para los casos `2xx`, agregar cobertura explícita de `401` (sin header) y `403` (header no-admin) en mutaciones, y replicar el test `[Fact] Controller_HasAuthorizeAttribute` que ya vive en `CargosControllerTests`. `OcupacionesControllerTests` debe además reemplazar `IClassFixture<WebApplicationFactory<SGV.Api.Program>>` por el fixture de `ApiWebApplicationFactory`.

### Specs (`openspec/specs/` y deltas)
- **Reescribir** `openspec/specs/sgv-readonly-api/spec.md` para reflejar el nuevo invariante: el único endpoint explícitamente anónimo es `/api/v1/auth/login`. `NivelesCargo` y `TipoUnidadesOrganizativas` pasan a requerir autenticación.
- **DELTA** sobre `persona-management`: añadir requisito de autorización `401/403/2xx` espejo de `cargo-management` líneas 259–280.
- **DELTA** sobre `unidad-organizativa-crud`: añadir requisito de autorización `401/403/2xx` para CRUD y mutaciones de padre/reactivar.
- **DELTA** sobre `nivel-cargo-catalog`: añadir requisito de lectura autenticada (anónimo→`401`, autenticado→`2xx`).
- **DELTA** sobre `tipo-unidad-organizativa-catalog`: idem anterior.
- **DELTA** sobre `sgv-persistence-architecture` o `jwt-signing-key-validation` solo si surge una consecuencia derivada; no se asume a priori.

### Documentación
- `docs/decisiones-implementacion.md`: actualizar la sección de seguridad/auth con la fallback policy, la regla `[Authorize]` por defecto y la única excepción `[AllowAnonymous]` en `AuthController`. Citar como precedente `2026-07-01-2026-07-01-cargos-crear-autorizacion-admin` e issue #90 (Puestos).

## Out of Scope / Non-goals

- **No tocar `src/SGV.Api/Controllers/PuestosController.cs`** ni sus tests ni su spec; ese cambio ya está cerrado (issue #90) y se referencia solo como precedente.
- **No modificar el modelo de JWT, Identity ni `RolesSgv`** (`identity-user-role-management`, `jwt-signing-key-validation`).
- **No introducir policies nominales nuevas** en `Program.cs`; se mantiene el patrón literal `[Authorize(Roles = RolesSgv.Administrador)]` ya vigente en `UsuariosController` y `CargosController`.
- **No tocar el flujo de `ApiBearerTokenHandler` en `SGV.Web`**: el bridge cookie→JWT ya cubre a los consumidores web.
- **No alterar la separación de capas Dominio/Aplicacion**: la autorización se aplica solo en la capa API (composition root).
- **No absorber el cambio de fallback policy** en otro change aparte; queda dentro de este PR como decisión explícita de scope.

## Capabilities

> Contrato entre esta propuesta y la fase `sdd-spec`. Cada item de Modified Capabilities genera un delta spec en `openspec/changes/2026-07-09-agregar-autorizacion-api-restantes/specs/{name}/spec.md`.

### New Capabilities
- None.

### Modified Capabilities
- `persona-management`: añadir requisito de autorización (mutaciones solo `Administrador`, lecturas autenticadas).
- `unidad-organizativa-crud`: añadir requisito de autorización (mutaciones solo `Administrador`, lecturas autenticadas).
- `nivel-cargo-catalog`: cambiar de acceso anónimo a autenticado en lecturas.
- `tipo-unidad-organizativa-catalog`: cambiar de acceso anónimo a autenticado en lecturas.
- `sgv-readonly-api`: reescribir la regla "No Authentication Requirement" para que la única excepción sea `/api/v1/auth/login`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Api/Program.cs` | Modified | Fallback policy `RequireAuthenticatedUser` aplicada vía `AddAuthorization`/`FallbackPolicy`. |
| `src/SGV.Api/Controllers/PersonasController.cs` | Modified | `[Authorize]` + overrides admin en 6 acciones. |
| `src/SGV.Api/Controllers/OcupacionesController.cs` | Modified | `[Authorize]` + overrides admin en 5 acciones. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modified | `[Authorize]` + overrides admin en 5 acciones. |
| `src/SGV.Api/Controllers/NivelesCargoController.cs` | Modified | `[Authorize]` a nivel clase. |
| `src/SGV.Api/Controllers/TipoUnidadesOrganizativasController.cs` | Modified | `[Authorize]` a nivel clase. |
| `src/SGV.Api/Controllers/AuthController.cs` | Modified | `[AllowAnonymous]` en `Login`. |
| `tests/SGV.Tests/Api/PersonasControllerTests.cs` | Modified | Migrar a `CreateAdminClient`, agregar 401/403. |
| `tests/SGV.Tests/Api/OcupacionesControllerTests.cs` | Modified | Cambiar fixture y migrar a `CreateAdminClient`, agregar 401/403. |
| `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Modified | Migrar a `CreateAdminClient` (~32 calls), agregar 401/403. |
| `tests/SGV.Tests/Api/NivelesCargoControllerTests.cs` | Modified | Agregar caso anónimo→`401`. |
| `tests/SGV.Tests/Api/TipoUnidadesOrganizativasControllerTests.cs` | Modified | Agregar caso anónimo→`401`. |
| `openspec/specs/persona-management/spec.md` | Modified | Nuevo requisito de autorización. |
| `openspec/specs/unidad-organizativa-crud/spec.md` | Modified | Nuevo requisito de autorización. |
| `openspec/specs/nivel-cargo-catalog/spec.md` | Modified | Lectura autenticada. |
| `openspec/specs/tipo-unidad-organizativa-catalog/spec.md` | Modified | Lectura autenticada. |
| `openspec/specs/sgv-readonly-api/spec.md` | Modified | Regla de acceso anónimo reescrita. |
| `docs/decisiones-implementacion.md` | Modified | Sección seguridad/auth actualizada. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Tests existentes rompen al introducir fallback policy porque no envían header de autenticación | Alta | Migrar todos los tests a `CreateAdminClient()` en el mismo PR; ejecutar `dotnet test` antes de marcar el change verificado. |
| Consumidores externos anónimos (si los hay) que leían `NivelesCargo` o `TipoUnidadesOrganizativas` empiezan a recibir `401` | Media | Documentar el cambio en `docs/decisiones-implementacion.md`; la lectura autenticada es la nueva postura para catálogos sensibles. |
| Olvido de `[AllowAnonymous]` en `AuthController` una vez aplicada la fallback policy global | Media | Cubrir con test explícito `Login_WithoutCredentials_Succeeds` que ya existe en la suite; agregar variante `Login_Anonymous_DoesNotReturn401`. |
| Drift entre spec delta y código si solo se protege a nivel clase sin `RolesSgv.Administrador` en mutantes | Baja | Reusar la constante `RolesSgv.Administrador` (sin literales); los tests detectan cualquier acción mutante sin override. |
| Review budget de 400 líneas excedido al combinar Program.cs + 5 controllers + 5 test files + 5 deltas | Media–Alta | Forecast fino en `sdd-tasks`; si supera 400, partir en chained PRs (capa web/lectura vs capa mutación API). |

## Rollback Plan

1. Revertir los atributos `[Authorize]` y `[Authorize(Roles = RolesSgv.Administrador)]` agregados en los 5 controllers de producción.
2. Remover la `FallbackPolicy` de `Program.cs` y devolver la inicialización de autorización al estado previo.
3. Revertir los tests al uso de `factory.CreateClient()` (sin header) y descartar los casos `401/403` agregados.
4. Revertir `docs/decisiones-implementacion.md` y los deltas en `openspec/specs/` (vía `git revert <commit>` sobre los archivos correspondientes). El reverso del spec requiere reescribir el requisito "No Authentication Requirement" al estado previo a este change.
5. Reejecutar `dotnet test SGV.slnx` para confirmar que la suite queda verde con el acceso anónimo restaurado.

## Dependencies

- `RolesSgv.Administrador` sembrado en Identity (vigente desde `identity-user-role-management`).
- `ApiWebApplicationFactory` con `FakeAuthenticationDefaults`/`FakeAuthenticationHandler` (vigente desde el change archivado de Cargos).
- `ApiBearerTokenHandler` en `src/SGV.Web/Integration/` ya cubre a la shell web como consumidor autenticado.
- Precedente directo: archive `2026-07-01-2026-07-01-cargos-crear-autorizacion-admin` (patrón idéntico sobre Cargos).
- Precedente paralelo de Puestos: archive `2026-07-08-implementa-edicion-puesto-frontend` (issue #90 cerrado).

## Success Criteria

- [ ] Toda mutación en `PersonasController`, `OcupacionesController` y `UnidadesOrganizativasController` responde `401` sin credenciales y `403` con autenticado sin rol `Administrador`.
- [ ] Las mismas mutaciones responden `2xx` con `CreateAdminClient()` y payload válido.
- [ ] `AuthController.Login` sigue accesible sin credenciales tras activar la fallback policy global (`200` con credenciales válidas).
- [ ] `GET /api/v1/niveles-cargo*` y `GET /api/v1/tipos-unidad-organizativa*` pasan a responder `401` anónimo y `2xx` autenticado.
- [ ] `dotnet test SGV.slnx` queda 100% verde con la nueva matriz de autorización.
- [ ] `docs/decisiones-implementacion.md` documenta la regla `[Authorize]` por defecto y la única excepción `[AllowAnonymous]` en Login.
- [ ] `sgv-readonly-api/spec.md` queda reescrito de forma que la única excepción de acceso anónimo sea `/api/v1/auth/login`.
- [ ] No se introducen nuevas policies nominales; se usa `RolesSgv.Administrador` literal tal como en `UsuariosController`/`CargosController`.