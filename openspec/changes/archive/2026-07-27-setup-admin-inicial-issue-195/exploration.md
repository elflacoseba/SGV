# Exploración — setup-admin-inicial-issue-195

> Issue origen: [#195 — Crear una pantalla para crear el usuario Administrador](https://github.com/elflacoseba/SGV/issues/195)
> Change: `setup-admin-inicial-issue-195` (kebab-case)
> Pista visual del usuario: `InspinaTemplate/Inspinia/Pages/Auth/SignUp.cshtml`

## Resumen ejecutivo

La issue #195 requiere una pantalla one-time para bootstrap del primer Administrador cuando `AspNetUsers` está vacío. El proyecto no tiene ningún concepto de "setup inicial" ni "seed de admin" hoy — la creación de usuarios requiere autenticación previa como Administrador. El flujo de creación de Persona + Usuario existe como dos operaciones separadas (primero Persona via `PersonaServicioComandos`, luego Usuario via `UsuarioServicioComandos`) y ambas están protegidas por `[Authorize]`. El cambio va a requerir: (1) un nuevo middleware/mecanismo de redirección en `SGV.Web` para detectar DB vacía, (2) un nuevo endpoint `[AllowAnonymous]` en `SGV.Api` que orqueste la creación atómica de Persona + Usuario + rol Administrador, y (3) una Razor Page en `SGV.Web` con el layout `_AuthLayout` existente. El patrón visual de `SignIn.cshtml` ya es directamente reutilizable; el template `SignUp.cshtml` de Inspinia aporta solo la estructura `input-group` y el `password-bar` que el proyecto podría incorporar opcionalmente.

## Contexto confirmado del codebase

### Autenticación actual

- **API** (`src/SGV.Api/Controllers/AuthController.cs`): rutea en `api/v1/auth/*`. `Login(LoginRequest)` → `IAuthServicio.LoginAsync()` → devuelve `LoginResponse(AccessToken, ExpiresAt)` o `401`. Todos los demás endpoints del API usan `[Authorize]` con `FallbackPolicy = RequireAuthenticatedUser()`. Las rutas `/health/*` tienen `.AllowAnonymous()` explícito.
- **Web** (`src/SGV.Web/Program.cs`): usa cookie auth con `LoginPath = "/auth/sign-in"`, `LogoutPath = "/auth/logout"`, `AccessDeniedPath = "/error/403"`. El pipeline es `UseRouting()` → `UseAuthentication()` → `UseAuthorization()`. El JWT se almacena en la cookie y se reenvía a la API vía `ApiBearerTokenHandler` (`src/SGV.Web/Integration/Auth/ApiBearerTokenHandler.cs`).
- **API middleware**: middleware custom que revalida credenciales (bloqueo/lockout) en cada request autenticado vía `RevalidatorCredenciales`.
- **Password policy** en `SGV.Api/Program.cs` líneas 129-136: `RequiredLength=6`, `RequireDigit=true`, `RequireLowercase=true`, `RequireUppercase=true`, `RequireNonAlphanumeric=true`. Los mensajes de error están mapeados en `UsuarioIdentityGateway.IdentityErrorMap` (líneas 448-459).

### Razor Pages de auth existentes

- Archivos en `src/SGV.Web/Pages/Auth/`: `SignIn.cshtml`/`.cs`, `ForgotPassword.cshtml`/`.cs`, `ResetPassword.cshtml`/`.cs`, `Logout.cshtml`/`.cs`, `_ViewStart.cshtml` (apunta a `_AuthLayout.cshtml`).
- **Patrón consistente**:
  - `_AuthLayout.cshtml` usa estructura Inspinia: `auth-box overflow-hidden align-items-center d-flex` → `container` → `row justify-content-center` → `col-xxl-4 col-md-6 col-sm-8` → `auth-brand text-center mb-4` (logo + h4 + p) → `card p-4` (form) → `d-grid` (botón submit).
  - PageModel usa `[BindProperty] InputModel Input`, `OnGet()` y `OnPostAsync(CancellationToken)`. Valida `ModelState.IsValid`, llama al typed client (`IAuthApiClient`), captura `HttpRequestException` y `TaskCanceledException`.
  - InputModel es nested `sealed class` con `[Required]` + mensajes en español.
  - El formulario CSHTML usa `@Html.AntiForgeryToken()`, `asp-validation-summary`, `asp-for` tag helpers, `asp-validation-for`.
- **Comparación con `InspinaTemplate/Inspinia/Pages/Auth/SignUp.cshtml`**:
  - Usa las mismas clases contenedoras Inspinia: `auth-box overflow-hidden align-items-center d-flex`, `card p-4`, `d-grid`, `auth-brand text-center mb-4`.
  - El template tiene una barra de fortaleza de contraseña (`password-bar my-2`) y un checkbox de términos — ambos opcionales para el setup.
  - El `input-group` que usa el template para inputs es ligeramente diferente del `mb-3` + `form-control` directo del proyecto — el proyecto actual no usa `input-group` para los fields individuales, sino directo con `class="form-control"`.
  - El template tiene un footer `Html.PartialAsync("~/Pages/Shared/Partials/_FooterScripts.cshtml")` que el proyecto ya incluye en `_AuthLayout.cshtml`.

### Creación de Persona + Usuario hoy

- **CrearPersonaRequest** (`src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs`): `(Legajo, Nombres, Apellidos, Email?, TipoDocumentoId?, NumeroDocumento?, Telefono?)`.
- **PersonaServicioComandos.CrearAsync** (`src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs`): valida con FluentValidation, chequea unicidad de Legajo/Email/Documento, crea `new Persona(nombres, apellidos, legajo, email)`, asigna Telefono/TipoDocumentoId/NumeroDocumento, persiste via `repository.AddAsync` + `unitOfWork.SaveChangesAsync`.
- **CrearUsuarioRequest** (`src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`): `(PersonaId, UserName, Email, Password, Roles)`.
- **UsuarioServicioComandos.CrearAsync** (`src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs`): valida PersonaId, email, roles, busca `personaRepository.GetByIdAsync`, delega a `identityGateway.CrearAsync`, audita.
- **UsuarioIdentityGateway.CrearAsync** (`src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs`): crea `SgvIdentityUser { UserName, Email, PersonaId }`, usa `UserManager.CreateAsync`, asigna roles con `UserManager.AddToRolesAsync`, todo dentro de una transacción explícita de EF (`context.Database.BeginTransactionAsync`).
- **Persona entity** (`src/SGV.Dominio/Personas/Persona.cs`): constructor `(nombres, apellidos, legajo?, email?)`. Tiene `IsActive` (soft-delete) e `IsDeleted` (audit). Métodos `CambiarDatos(nombres, apellidos, legajo, email, telefono)` y `CambiarDocumento(tipoDocumentoId, numeroDocumento)`.
- **SgvIdentityUser** (`src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs`): `IdentityUser` con `Guid PersonaId` adicional.
- Flujo actual: **Primero crear Persona** (POST `/api/v1/personas`), **luego crear Usuario vinculado** (POST `/api/v1/usuarios` con `PersonaId` de la persona recién creada). Ambos endpoints requieren rol `Administrador`.

### Política de password de Identity

Configurada en `src/SGV.Api/Program.cs` líneas 129-136:

```
RequireDigit = true
RequireLowercase = true
RequireUppercase = true
RequireNonAlphanumeric = true
RequiredLength = 6
```

Los mensajes de error están en español en `UsuarioIdentityGateway.IdentityErrorMap`.

### Detección de "DB vacía"

- **No existe** ninguna consulta a `AspNetUsers.AnyAsync()` ni `_userManager.Users.AnyAsync()` en el código actual.
- **No hay** health checks que expongan este estado. Los health checks existentes solo verifican conectividad con MySQL y upstream.
- La consulta sobre `AspNetUsers` es económica: el PK clustered index sobre `Id` (tipo `varchar(450)`) hace un `COUNT` o `Any` O(1).
- **Recomendación**: implementar un endpoint en la API (`GET /api/v1/setup/status` o similar) con `[AllowAnonymous]` que ejecute `_userManager.Users.AnyAsync()` y devuelva `{ requiresSetup: true/false }`. La web consulta este endpoint en cada request no autenticado (o en el middleware de redirección) y redirige a `/auth/setup` si es necesario.

### Roles y constantes

- `RolesSgv.Administrador = "Administrador"` (en `src/SGV.Contracts/Seguridad/RolesSgv.cs` línea 8).
- `AuthApiRoutes.Base = "api/v1/auth"`, `LoginRelative = "login"`, patrón: `public const string Login = "/" + Base + "/" + LoginRelative;` (en `src/SGV.Contracts/Auth/AuthApiRoutes.cs`).
- El proyecto no tiene aún una sección `SetupApiRoutes` o similar.

### Tests existentes

- **`ApiWebApplicationFactory`** (`tests/SGV.Tests/Api/ApiWebApplicationFactory.cs`): reemplaza servicios reales con fakes (`FakeAuthServicio`, `FakePersonaServicioComandos`, `FakeUsuarioServicioComandos`, etc.). Usa `FakeAuthenticationHandler` con scheme `"Test"` y tokens `"admin"`/`"user"`.
- **`SgvWebApplicationFactory`** (`tests/SGV.Tests/Web/SgvWebApplicationFactory.cs`): soporta inyección de `HttpMessageHandler` para simular la API upstream, incluyendo `WithPersonaApiClient`, `WithUsuarioApiClient` conveniences.
- **Tests existentes**: `AuthControllerTests.cs`, `UsuariosControllerTests.cs`, `PersonasControllerTests.cs`, `WebAuthenticationTests.cs` (tests de cookie auth web), `UsuariosEndToEndMySqlFactTests.cs`.
- Los tests `[MySqlFact]` ya están en uso para tests de integración con MySQL real.
- **Patrón de tests web**: usar `SgvWebApplicationFactory` con `RecordingHttpMessageHandler` y sobrescribir servicios via `WithOverrides(configurationServices, authApiHandler)`.

### Convenciones y decisiones del proyecto

- **Clean Architecture**: `Dominio` → `Aplicacion` → `Infraestructura`; `Api` como composition root.
- **SGV.Contracts es leaf** (no referencia a otros proyectos del solution). Contiene DTOs, requests, responses, rutas y constantes.
- **MySQL + Pomelo 9**: índices únicos con soft-delete usan columnas generadas. No soporta filtered indexes.
- **Fail-loud startup**: JWT signing key y connection string se validan en Build() con `OptionsValidationException`.
- **Todos los endpoints protegidos** por `FallbackPolicy = RequireAuthenticatedUser()`, excepto `/health/*` y las rutas auth (`login`, `forgot-password`, `reset-password`) que tienen `[AllowAnonymous]`.
- **Las migraciones no se ejecutan al startup**. Solo via `dotnet ef database update` o en tests con `Database.Migrate()`.
- **Auditoría**: tabla única `Auditorias` con interceptor EF. Excluye tokens/contraseñas/stamps.
- **No hay concepto de "seed admin" ni "setup one-time" en el proyecto actual.**

## Cambios previos relacionados en openspec

No hay cambios archivados que implementen setup inicial, seed de admin o bootstrap. Los más cercanos son:

- `2026-06-21-implementar-identity-usuarios-roles`: implementó Identity + roles + `UsuarioIdentityGateway`.
- `2026-07-15-implementa-modulo-usuarios`: frontend CRUD de usuarios (requiere admin logueado).
- `2026-07-14-frontend-crud-personas`: frontend CRUD de personas (requiere autenticación).

Ninguno de estos cambios contempla el chicken-and-egg entre Personas y Usuarios que la issue #195 resuelve.

## Preguntas abiertas de producto

1. **¿El setup puede ejecutarse en cualquier ambiente (Development, Staging, Production) o solo en Development?** Si se permite en producción, la pantalla de setup es un vector de ataque: cualquiera con acceso a la URL puede crear un admin. La mitigación (solo se muestra si `AspNetUsers` está vacío) es efectiva pero debe estar acompañada de logging y alertas.
2. **¿Qué pasa si la creación de Persona funciona pero la de Usuario falla (o viceversa)?** La transacción debe ser atómica: ambas operaciones dentro de la misma transacción de EF Core, o ninguna persiste. ¿Se audita el intento fallido?
3. **¿Se debe auditar la creación del primer administrador?** Dado que el setup se ejecuta sin un `usuarioActual.UserId` (no hay nadie autenticado), la auditoría actual (`auditoriaServicio.RegistrarAsync`) espera un `userId` como string. ¿Se pasa `"system"` o `null`? ¿O se omite la auditoría para el setup inicial?
4. **¿Debe haber un límite de tasa (rate limiting) en el endpoint de setup?** Si la DB está vacía, un atacante puede bombardear el endpoint. Aunque el endpoint es idempotente (409/404 después del primer éxito), un rate limit evita ataques de fuerza bruta para adivinar contraseñas.
5. **Los campos opcionales (TipoDocumento, NumeroDocumento, Teléfono) en el setup: ¿se muestran en el formulario o se omiten para simplificar?** La issue dice "opcional" para esos campos. Decisión de UX: ¿mostrarlos colapsados/ocultos tras un toggle, o ponerlos directamente visibles?

## Riesgos técnicos identificados

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| **Race condition**: dos requests simultáneos a `/auth/setup` cuando la DB está vacía pueden crear dos admins | Alta | La guarda `AnyUsersAsync()` debe ejecutarse DENTRO de la transacción que crea Persona+Usuario, con un `SELECT ... FOR UPDATE` o `REPEATABLE READ` que prevenga inserts concurrentes. Alternativa: usar un `UNIQUE` constraint sobre el primer UserName, Identity ya lo tiene en `AspNetUsers.UserName` |
| **Atomicidad transaccional**: Persona creada pero Usuario falla (o viceversa) | Alta | Todo dentro de una sola transacción EF. Si `UserManager.CreateAsync` falla, rollback de Persona también |
| **Persona no tiene soft-delete check**: el setup debería verificar que no haya Personas activas en lugar de solo `AspNetUsers` vacío | Media | La issue dice "AspNetUsers vacía". Si ya hay Personas pero ningún User, el setup igual debe permitirse (la persona sin usuario puede asignarse luego). Es el escenario correcto |
| **AspNetUsers.UserName unique index de Identity**: si el endpoint se ejecuta dos veces, Identity rechaza el UserName duplicado automáticamente | Baja | Identity ya lanza `DuplicateUserName` si se intenta crear el mismo username. Esto es la red de seguridad natural |
| **CORS / SameSite**: si el setup corre en Web pero llama a la API para verificar DB vacía, el CORS de la API en producción puede bloquear la request | Media | El endpoint debe ser `[AllowAnonymous]`. La cookie de autenticación no está presente, así que no hay problema de CORS con credenciales. Si la Web usa BFF pattern (get de la API desde servidor), no aplica CORS |
| **Persona con `IsDeleted` o `IsActive = false`**: si hay personas dadas de baja, no deben contar como "setup completado" | Baja | El chequeo debe ser exclusivamente sobre `AspNetUsers`, no sobre Personas. Es el comportamiento deseado |
| **Legajo único**: si el setup permite legajo opcional pero no se verifica unicidad (porque no hay otra persona activa con el mismo legajo), el riesgo es bajo | Baja | `PersonaServicioComandos` ya verifica unicidad de legajo activo. El setup debería reusar esa validación o la guarda de dominio |

## Recomendación para la propuesta

Arquitectónicamente, la solución más limpia es **un endpoint dedicado en la API** (`POST /api/v1/setup`) dentro de un nuevo `SetupController` con `[AllowAnonymous]` que orquesta la creación atómica de Persona + Usuario + rol Administrador en una sola transacción. Esto evita duplicar lógica de dominio y mantiene el patrón actual donde la Web es solo un cliente tipado. La Web recibe **dos piezas**: (1) un middleware o filtro de página que redirige a `/auth/setup` si la DB está vacía, ejecutado después de `UseRouting()` pero antes de la autenticación (o combinado con el pipeline existente), y (2) una Razor Page `/auth/setup` con el mismo layout `_AuthLayout` que `SignIn.cshtml`. El endpoint de setup NO debe requerir JWT bearer (es `[AllowAnonymous]`), pero su guarda de negocio (`AnyUsersAsync`) debe ejecutarse dentro de la transacción para cubrir race conditions. Se recomienda no reusar `PersonaServicioComandos` ni `UsuarioServicioComandos` directamente porque ambos esperan un `usuarioActual.UserId` para auditoría; en su lugar, crear un servicio específico `SetupServicio` (en `SGV.Aplicacion/Setup/`) que implemente `ISetupServicio` con la orquestación y auditoría con `userId = "system"`.

## Archivos clave a tocar

**Archivos nuevos:**
- `src/SGV.Contracts/Setup/SetupRequest.cs` — record con datos del formulario
- `src/SGV.Contracts/Setup/SetupResponse.cs` — record con resultado (o reusar `UsuarioDto`)
- `src/SGV.Aplicacion/Setup/ISetupServicio.cs` — puerto
- `src/SGV.Aplicacion/Setup/SetupServicio.cs` — orquestador
- `src/SGV.Infraestructura/Setup/SetupServicio.cs` — implementación con UserManager + PersonaRepository + transacción
- `src/SGV.Api/Controllers/SetupController.cs` — `[AllowAnonymous]` con guarda + `POST /api/v1/setup`
- `src/SGV.Web/Integration/Setup/SetupApiClient.cs` — typed client + interfaz `ISetupApiClient`
- `src/SGV.Web/Pages/Auth/Setup.cshtml` — Razor Page view
- `src/SGV.Web/Pages/Auth/Setup.cshtml.cs` — PageModel

**Archivos a modificar:**
- `src/SGV.Contracts/Auth/AuthApiRoutes.cs` — agregar `SetupRelative` / `Setup` constantes
- `src/SGV.Web/Program.cs` — agregar typed client registration para `SetupApiClient` (sin `ApiBearerTokenHandler`, timeout 10s)
- `src/SGV.Web/Program.cs` — agregar middleware o filtro para redirección a `/auth/setup` cuando DB vacía
- `src/SGV.Api/Program.cs` — registrar `SetupServicio` en DI (o ya se registra via `AddInfraestructuraServicios`)
- `src/SGV.Infraestructura/DependencyInjection.cs` — registrar `SetupServicio`, `ISetupServicio`
- `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` — agregar fakes setup si es necesario
- `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` — agregar `WithSetupApiClient` si necesario

## skill_resolution
paths-injected
