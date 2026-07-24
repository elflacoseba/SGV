# Design — setup-admin-inicial-issue-195

> Issue origen: [#195 — Crear una pantalla para crear el usuario Administrador](https://github.com/elflacoseba/SGV/issues/195)
> Change: `setup-admin-inicial-issue-195` (kebab-case)
> Spec: `openspec/changes/setup-admin-inicial-issue-195/specs/setup/spec.md` (REQ-SETUP-001..006)

## 1. Resumen arquitectónico

Componentes nuevos o modificados por capa:

```
SGV.Web ── Razor Page /auth/setup (Setup.cshtml + Setup.cshtml.cs)
   │       SignIn.cshtml.cs ── consulta status en OnGetAsync
   │       ISetupApiClient ── typed HttpClient (anonymous, 10s timeout, sin ApiBearerTokenHandler)
   │       SetupStatusMemoryCache ── IMemoryCache TTL 30s (atrás del client)
   │
   ▼
SGV.Contracts.Setup ── SetupRequest, SetupResult, SetupStatusResponse,
   │                    SetupErrorCode (enum), SetupApiRoutes (constantes)
   ▼
SGV.Aplicacion.Setup ── ISetupServicio (puerto)
   │                    SetupServicio (orquestador: valida, llama gateway + auditoría)
   ▼
SGV.Infraestructura.Setup ── SetupServicio impl (UserManager + IUnitOfWork + repos + audit)
SGV.Api.SetupController ── [AllowAnonymous] GET /api/v1/setup/status
                          [AllowAnonymous][EnableRateLimiting("Setup")] POST /api/v1/setup
SGV.Api.TiposDocumentoController.Get ── suma [AllowAnonymous] (catálogo inmutable 4 filas)
SGV.Api.Program.cs ── registra AddFixedWindowLimiter("Setup")
```

Diagrama lógico del flujo end-to-end:

```
[Browser] GET /auth/sign-in (no auth)
   │
   ▼
[SignInModel.OnGetAsync] ── ISetupApiClient.ObtenerEstadoAsync (cached 30s)
   │                              │
   │                              ▼ GET /api/v1/setup/status
   │                              [AllowAnonymous]
   │                              ▼
   │                              ISetupServicio.EstadoActualAsync ── AnyUsersAsync
   │                              ▼ { requiresSetup, tiposDocumento }
   │
   ├── requiresSetup=true ──▶ return RedirectToPage("/auth/setup")
   │
   └── requiresSetup=false ─▶ render SignIn normalmente
                                │
                                ▼ POST credenciales ─▶ AuthController.Login

[Browser] GET /auth/setup (no auth)
   │
   ▼
[SetupModel.OnGetAsync] ── ISetupApiClient.ObtenerEstadoAsync (cache hit) ── render 9 campos
                                │
                                ▼ POST /auth/setup (PRG)
                                ▼
[SetupModel.OnPostAsync] ── ISetupApiClient.CrearAsync ── POST /api/v1/setup
                                │
                                ▼ [AllowAnonymous][EnableRateLimiting("Setup")]
                                ▼
                           SetupController.Crear
                                ▼ ISetupServicio.CrearAdminAsync
                                ▼ SetupServicio (Infra)
                                ▼   BeginTransactionAsync (default isolation)
                                ▼   any Users ?  ── sí ──▶ 409 SetupYaCompletado
                                ▼   PersonaServicioComandos.CrearAsync (validación + unicidad)
                                ▼   UsuarioIdentityGateway.CrearAsync (UserManager + rol)
                                ▼   IAuditoriaServicio.RegistrarAsync (userId="system")
                                ▼   CommitAsync (atómico)
                                │
                                ▼ 200 CommandResult<SetupResult> { personaId, userId }
                                ▼
                           [SetupModel] return RedirectToPage("/auth/sign-in") + TempData success
```

## 2. Decisiones técnicas resueltas

### 2.1 Aislamiento MySQL para `AnyUsersAsync()`

**Decisión**: NO escalar el nivel de aislamiento (queda el default `REPEATABLE READ` de MySQL InnoDB). La guarda `AnyUsersAsync()` se ejecuta **dentro** de la transacción EF, **justo antes** de delegar al gateway de Identity. La defensa real contra doble admin concurrente es el **índice único estándar de Identity sobre `NormalizedUserName`** (PK lógico de `AspNetUsers`), que rechaza el duplicado vía `UserManager.CreateAsync` → `IdentityResult` con `DuplicateUserName`.

**Por qué no las alternativas**:

| Alternativa | Tradeoff | Decisión |
|---|---|---|
| `SERIALIZABLE` | InnoDB degrada a `SELECT ... LOCK IN SHARE MODE` + `WHERE ...` predicate locks; costo desproporcionado para una operación one-time que ya tiene red de seguridad por índice único | Rechazada |
| `REPEATABLE READ` + `SELECT ... FOR UPDATE` sobre tabla vacía | No hay filas para bloquear; el bloqueo es sobre el gap, pero el segundo insert NO se bloquea por gap lock en MySQL InnoDB (gap locks no bloquean inserts en otros gaps, solo en el mismo gap). Comportamiento sutil y propenso a regresiones futuras del optimizador | Rechazada |
| MySQL advisory locks (`GET_LOCK('sgv:setup')`) | Requiere lock simétrico en el `Rollback`; mezcla capa aplicación con primitiva nativa; menos portable | Rechazada |
| `INSERT ... ON DUPLICATE KEY UPDATE` sobre `AspNetUsers` | Rompe el contrato de `UserManager`; el password hash se calcula en C# y EF intercepta la inserción | No aplica |

**Por qué la combinación default + índice único es suficiente**:

1. La ventana entre `AnyUsersAsync()` y `UserManager.CreateAsync` es microsegundos. La probabilidad de race real es despreciable.
2. Aun si dos requests pasan la guarda, `UserManager.CreateAsync` ejecuta `INSERT INTO AspNetUsers (...)` con PK sobre `Id` y el índice único `IX_AspNetUsers_NormalizedUserName`. Pomelo traduce el `DuplicateKeyException` de MySQL (error 1062) a `IdentityResult` con `DuplicateUserName`, que `UsuarioIdentityGateway.ToIdentityFailure` ya mapea a `UsuarioErrorType.Conflict` con código `UserNameDuplicado` (líneas 491-498 de `UsuarioIdentityGateway.cs`).
3. El gateway envuelve la creación en una transacción explícita (`context.Database.BeginTransactionAsync`, línea 56-58 de `UsuarioIdentityGateway.cs`); cualquier fallo de Identity hace rollback antes de commit.

**Tradeoff explícito**: aceptamos la (improbable) posibilidad de dos `Persona` insertadas si dos requests pasan la guarda pero ambos `CreateAsync` fallan por otro motivo distinto a `DuplicateUserName`. En la práctica, el segundo `UserManager.CreateAsync` rechaza por username duplicado con probabilidad >99%; los demás códigos (`InvalidEmail`, password policy) son deterministas y se validan antes de entrar al gateway.

### 2.2 Carga de `TipoDocumento`

**Decisión**: agregar `[AllowAnonymous]` a la acción `GetAll` existente en `TiposDocumentoController` (sin tocar `[Authorize]` a nivel clase, que se mantiene para `GetById`).

**Análisis de las tres opciones**:

| Opción | Pros | Contras | Decisión |
|---|---|---|---|
| (a) Cliente anónimo dedicado (`ISetupApiClient.GetTiposDocumentoAsync`) | Aislado; el resto del catálogo sigue protegido | Endpoint nuevo (~50 líneas); duplica `TipoDocumentoDto`; round-trip extra para el shell web | Descartada |
| (b) Reusar endpoint existente con `[AllowAnonymous]` | Cero cambios wire-types; cero round-trip extra; patrón idéntico a `AuthController.Login` (`[AllowAnonymous]` sobrevive la `FallbackPolicy = RequireAuthenticatedUser()`) | Relaja la superficie de catálogo para cualquier cliente, no sólo setup | **Elegida** |
| (c) Embebido en `IMemoryCache` con TTL precargado al startup | Latencia ~0; sobrevive caída de API | El cache debe invalidarse si el catálogo mutara; el proyecto ya asume catálogo inmutable vía seed en migración, no hay caso de uso que requiera invalidad en runtime | Descartada por redundante con la simplicidad de (b) |

**Justificación**: `TipoDocumento` es un catálogo inmutable de 4 filas seed (DNI/LE/LC/Pasaporte), cargado por `TipoDocumentoCatalogoConsulta` desde el repositorio de sólo lectura. No expone PII ni lógica de negocio. El precedente de `AuthController.Login` (línea 22-30 de `AuthController.cs`) demuestra que la `FallbackPolicy` se puede relajar puntualmente con `[AllowAnonymous]`. El cambio se limita a una línea en el controller; `GetById` mantiene `[Authorize]` heredado.

**Tradeoff**: cualquier cliente (no autenticado) que conozca la ruta `GET /api/v1/tipos-documento` puede leer el catálogo. Aceptable porque es información que ya está implícita en el formulario de signup y en los formularios públicos de cualquier sistema equivalente.

### 2.3 Manejo de indisponibilidad del API desde `SignIn`

**Decisión**: **Fail-open con cache de corto plazo (TTL 30s) en la capa Web**. Si la consulta al status falla por timeout, 5xx o red, `SignIn` renderiza normalmente (sin redirigir a `/auth/setup`). El cache absorbe fallas transitorias sin ocultar la necesidad real de setup indefinidamente.

**Análisis**:

| Modo | Comportamiento ante API caída | Riesgo | Decisión |
|---|---|---|---|
| Fail-open (sin cache) | Renderiza `SignIn`; si DB está vacía, el usuario verá "Credenciales inválidas" hasta que la API vuelva | UX confuso, pero recoverable | Mejor que fail-closed |
| Fail-closed | Página en blanco o error "servicio no disponible" | Una caída de API rompe el acceso al sistema completo | Inaceptable |
| **Fail-open con cache TTL 30s** | Hit de cache → estado conocido. Miss + API caída → render SignIn. Próximo hit dentro de 30s evita el round-trip y, si la API sigue caída, vuelve a fallar sin redirigir | La ventana de 30s puede esconder un setup recién disponible (probabilidad despreciable: setup solo aparece cuando DB está vacía al arrancar) | **Elegida** |

**Implementación**: `IMemoryCache` registrado vía `builder.Services.AddMemoryCache()` (ya disponible en ASP.NET Core). `ISetupApiClient` decora la respuesta de `ObtenerEstadoAsync` con cache key `setup:status` y TTL absoluto de 30s. El handler del Web captura `HttpRequestException` y `TaskCanceledException` (patrón ya vigente en `SignInModel.OnPostAsync` líneas 40-53 de `SignIn.cshtml.cs`) y devuelve un `SetupStatusResponse` con `RequiresSetup=false` como fallback.

### 2.4 Mapeo de Identity errors

**Decisión**: reusar `UsuarioIdentityGateway.IdentityErrorMap` (líneas 448-459 de `UsuarioIdentityGateway.cs`) y `ToIdentityFailure` (líneas 488-521). El nuevo `SetupErrorCode` es un enum en español que el controller mapea a HTTP; `SetupResult` envuelve `PersonaId` + `UserId` del admin recién creado.

**Tabla de mapeo**:

| `IdentityError.Code` | `UsuarioErrorType` | `SetupErrorCode` | HTTP | Mensaje en español (origen) |
|---|---|---|---|---|
| `DuplicateUserName` | `Conflict` (`UserNameDuplicado`) | `UserNameDuplicado` | 409 | "El nombre de usuario ya está en uso." |
| `DuplicateEmail` | `Conflict` (`EmailDuplicado`) | `EmailDuplicado` | 409 | "El email ya está en uso." |
| `PasswordTooShort` | `Validation` (`IdentityError`) | `PasswordDebil` | 400 | "La contraseña debe tener al menos 6 caracteres." |
| `PasswordRequiresNonAlphanumeric` | `Validation` | `PasswordDebil` | 400 | "La contraseña debe incluir al menos un carácter no alfanumérico." |
| `PasswordRequiresDigit` | `Validation` | `PasswordDebil` | 400 | "La contraseña debe incluir al menos un dígito." |
| `PasswordRequiresLower` | `Validation` | `PasswordDebil` | 400 | "La contraseña debe incluir al menos una letra minúscula." |
| `PasswordRequiresUpper` | `Validation` | `PasswordDebil` | 400 | "La contraseña debe incluir al menos una letra mayúscula." |
| `PasswordRequiresUniqueChars` | `Validation` | `PasswordDebil` | 400 | "La contraseña debe incluir al menos 1 carácter único." |
| `InvalidEmail` | `Validation` | `EmailInvalido` | 400 | "El email no tiene un formato válido." |
| `InvalidUserName` | `Validation` | `UserNameInvalido` | 400 | "El nombre de usuario sólo admite letras, números, punto, guión bajo y guión medio." |
| (cualquier otro) | `Validation` (`IdentityError`) | `ValidacionIdentity` | 400 | Fallback `FallbackIdentityMessage` |
| `PersonaYaTieneUsuario` | `Conflict` | `PersonaConUsuario` | 409 | "La persona ya tiene un usuario asociado." |
| `SetupYaCompletado` (interno) | `Conflict` | `SetupYaCompletado` | 409 | "La configuración inicial ya fue completada." |
| Falla de transacción no-Identity | `Unexpected` | `TransaccionFallida` | 500 | "No se pudo completar la configuración inicial. Intentá nuevamente." |

**Forma del error**: el `SetupController` consume `SetupServiceResult<T>` (alias local sobre `CommandResult<SetupResult>`) y mapea `SetupErrorCode` → `StatusCode` en el switch del controller. Los errores de FluentValidation (`CrearPersonaRequestValidator`) se devuelven como `ValidationProblemDetails` estándar (mismo patrón que `AuthController.ForgotPassword`, líneas 47-55 de `AuthController.cs`) con `fieldErrors` para que `Setup.cshtml` los muestre junto al campo correspondiente.

### 2.5 Rate limiting

**Decisión**: agregar una política `Setup` adicional en el bloque `AddRateLimiter` existente de `Program.cs` (líneas 225-261), siguiendo el patrón ya establecido para `ForgotPassword` y `ResetPassword`. **Aplica sólo a `POST /api/v1/setup`**, no al status.

**Política concreta**:

| Atributo | Valor | Justificación |
|---|---|---|
| Nombre | `"Setup"` (constante `SetupApiRoutes.SetupPolicyName`) | Mismo naming que las políticas existentes |
| Tipo | `AddFixedWindowLimiter` | Consistente con `ForgotPassword` y `ResetPassword` |
| `PermitLimit` | `5` | Más permisivo que `ForgotPassword` (3) porque el flujo tiene 9 campos con probabilidad alta de error humano (un usuario que tipea mal vuelve a intentar); más estricto que `ResetPassword` (5) en la misma ventana porque es one-time y expone creación de admin |
| `Window` | `TimeSpan.FromMinutes(15)` | Igual que las políticas existentes |
| `QueueProcessingOrder` | `OldestFirst` | Idéntico |
| `QueueLimit` | `0` | Sin cola: el exceso se rechaza inmediatamente |
| `RejectionStatusCode` | `429 TooManyRequests` | Heredado de `options.RejectionStatusCode` ya configurado |
| Header de respuesta | `Retry-After: 900` | Heredado del callback `OnRejected` ya configurado |

**Integración con `Program.cs`**: agregar `options.AddFixedWindowLimiter(SetupApiRoutes.SetupPolicyName, policy => { ... });` dentro del lambda de `AddRateLimiter`. Decorar la acción con `[EnableRateLimiting(SetupApiRoutes.SetupPolicyName)]`.

**Justificación de no aplicar rate-limit al status**: el endpoint sólo lee `AnyUsersAsync()` (un `COUNT`/`EXISTS` O(1) con PK clustered sobre `Id`), no realiza ninguna mutación, y ya está protegido por la cache de 30s en el shell. Aplicar rate-limit allí sólo protegería contra un DDoS extremo que no aporta defensa adicional al problema resuelto.

### 2.6 409 vs 404 para setup ya completado

**Decisión**: **`409 Conflict`** con código `SetupYaCompletado`.

**Justificación**:

- `404 Gone` es una elección rara y mal entendida en REST: literalmente "el recurso existía pero fue removido permanentemente". Aplicarla aquí implicaría que el endpoint `/api/v1/setup` "existió pero ya no", lo cual es semánticamente confuso (el endpoint sigue vivo; sólo está cerrado el flujo).
- `409 Conflict` es la convención estándar para "la operación entra en conflicto con el estado actual del recurso". Coincide con la taxonomía ya usada por `UsuarioErrorType.Conflict` (`UserNameDuplicado`, `EmailDuplicado`, `PersonaYaTieneUsuario`) en `UsuarioIdentityGateway`.
- Consistencia con el resto del API: el controller de Personas devuelve `409` para conflictos de unicidad (`LegajoDuplicado`, `EmailDuplicado`, `DocumentoDuplicado`). Mantener `409` evita introducir una nueva familia de códigos.
- Menor superficie de información filtrada: `404` puede sugerir "el endpoint no existe" (lo que invita a buscar variantes); `409` cierra la conversación inmediatamente.

## 3. Componentes y contratos

### 3.1 Capa Contracts

Archivos nuevos en `src/SGV.Contracts/Setup/`:

- `SetupRequest.cs` — `sealed record SetupRequest(string Nombres, string Apellidos, string? Legajo, string Email, string UserName, string Password, Guid? TipoDocumentoId, string? NumeroDocumento, string? Telefono)`. Validaciones FluentValidation viven en `SGV.Aplicacion/Setup/Validaciones/` (no en Contracts, siguiendo el patrón de `CrearPersonaRequestValidator`).
- `SetupResult.cs` — `sealed record SetupResult(Guid PersonaId, string UserId, string UserName)`.
- `SetupStatusResponse.cs` — `sealed record SetupStatusResponse(bool RequiresSetup)`. Sólo el flag; el catálogo de `TipoDocumento` se resuelve con un GET independiente (ver 2.2).
- `SetupErrorCode.cs` — `public enum SetupErrorCode { SetupYaCompletado, UserNameDuplicado, EmailDuplicado, PersonaConUsuario, EmailInvalido, UserNameInvalido, PasswordDebil, ValidacionIdentity, DatosInvalidos, TransaccionFallida }`.
- `SetupApiRoutes.cs` — `public static class SetupApiRoutes { public const string Base = "api/v1/setup"; public const string StatusRelative = "status"; public const string Status = "/" + Base + "/" + StatusRelative; public const string SetupPolicyName = "Setup"; }`.

`src/SGV.Contracts/Auth/AuthApiRoutes.cs` **no se modifica**: las rutas de setup viven en su propio namespace.

### 3.2 Capa Aplicación

Puerto: `ISetupServicio` en `src/SGV.Aplicacion/Setup/ISetupServicio.cs`:

```csharp
public interface ISetupServicio
{
    Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken ct = default);
    Task<SetupCommandResult> CrearAdminAsync(SetupRequest request, CancellationToken ct = default);
}
```

Donde `SetupCommandResult` es `CommandResult<SetupResult>` con `SetupError(ErrorCategoria, SetupErrorCode, string Message, int? StatusCode)` como tipo de error.

**Por qué NO reutilizar `PersonaServicioComandos` + `UsuarioServicioComandos`**:

1. Ambos esperan un `usuarioActual.UserId` para auditoría (`UsuarioServicioComandos.RegistrarAuditoriaAsync`, líneas 312-325). En setup no hay usuario autenticado; necesitamos pasar `"system"` explícitamente.
2. Ambos ejecutan validaciones independientes (`PersonaServicioComandos.CrearAsync` hace check de unicidad + `repository.AddAsync` + `unitOfWork.SaveChangesAsync`; luego `UsuarioServicioComandos.CrearAsync` abre OTRA transacción). Si los encadenamos, NO hay atomicidad garantizada entre Persona y Usuario. El setup requiere una sola transacción EF que abarque ambos pasos.
3. La guarda `AnyUsersAsync()` es propia de setup; agregarla a `PersonaServicioComandos` filtraría incorrectamente la creación de personas normales (cuando la DB ya tiene admin).
4. El contrato de error de setup es `SetupErrorCode` (nuevo), no `PersonaError` ni `UsuarioError`; reutilizar los comandos existentes obligaría a mapear entre dos taxonomías.

### 3.3 Capa Infraestructura

`SetupServicio` en `src/SGV.Infraestructura/Setup/SetupServicio.cs`:

```csharp
public sealed class SetupServicio(
    UserManager<SgvIdentityUser> userManager,
    SgvDbContext context,
    IUnitOfWork unitOfWork,
    IPersonaRepository personaRepository,
    IUsuarioIdentityGateway identityGateway,
    IAuditoriaServicio auditoriaServicio,
    IValidator<SetupRequest> validator,
    ILogger<SetupServicio> logger) : ISetupServicio
{
    public async Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        // Read-only. Cualquier método del repositorio Identity sirve:
        // userManager.Users.AnyAsync() traduce a SELECT EXISTS(SELECT 1 FROM AspNetUsers LIMIT 1)
        // O(1) por PK clustered sobre Id.
        var anyUsers = await userManager.Users.AnyAsync(ct).ConfigureAwait(false);
        return new SetupStatusResponse(RequiresSetup: !anyUsers);
    }

    public async Task<SetupCommandResult> CrearAdminAsync(SetupRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return ValidationFrom(validation.Errors);   // SetupErrorCode.DatosInvalidos + fieldErrors
        }

        // ---- guarda dentro de la transacción (decisión 2.1) ----
        await using var transaction = await context.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var anyUsers = await userManager.Users.AnyAsync(ct).ConfigureAwait(false);
        if (anyUsers)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            return Conflict(SetupErrorCode.SetupYaCompletado,
                "La configuración inicial ya fue completada.");
        }

        // ---- crea Persona vía gateway de Aplicación existente ----
        var personaRequest = new CrearPersonaRequest(
            request.Nombres, request.Apellidos, request.Legajo, request.Email,
            request.TipoDocumentoId, request.NumeroDocumento, request.Telefono);
        var personaResult = await personaServicio.CrearAsync(personaRequest, ct).ConfigureAwait(false);
        if (!personaResult.IsSuccess)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            return SetupCommandResult.FromPersona(personaResult);   // propaga PersonaError mapeado
        }

        // ---- crea Usuario vía gateway Identity, dentro de la MISMA transacción ----
        var usuarioRequest = new CrearUsuarioRequest(
            personaResult.Value!.Id, request.UserName, request.Email, request.Password,
            new[] { RolesSgv.Administrador });
        var usuarioResult = await identityGateway.CrearAsync(usuarioRequest, ct).ConfigureAwait(false);
        if (!usuarioResult.IsSuccess)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            return SetupCommandResult.FromUsuario(usuarioResult);   // propaga IdentityError mapeado
        }

        // ---- auditoría explícita con userId="system" (decisión producto 2.2) ----
        await auditoriaServicio.RegistrarAsync(
            entidad: "SetupInicial",
            entityId: usuarioResult.Value!.Id,
            accion: "AltaPrimerAdministrador",
            usuarioOperadorId: "system",
            valoresAnteriores: EmptyValues,
            valoresNuevos: CriticalValues(usuarioResult.Value!, personaResult.Value!),
            cancellationToken: ct).ConfigureAwait(false);

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return SetupCommandResult.Success(new SetupResult(
            personaResult.Value.Id, usuarioResult.Value.Id, usuarioResult.Value.UserName));
    }
}
```

**Decisión clave**: el `SetupServicio` inyecta `IPersonaServicioComandos` (no `PersonaServicioComandos` directamente) para mantener la regla "SGV.Aplicacion.Setup sólo depende de SGV.Aplicacion.* y SGV.Contracts". Reutilizamos la validación de unicidad (Legajo, Email, Documento) ya implementada en `PersonaServicioComandos.CheckUniquenessAsync` (líneas 200-229 de `PersonaServicioComandos.cs`).

**Acoplamiento transaccional**: el `identityGateway.CrearAsync` (líneas 39-82 de `UsuarioIdentityGateway.cs`) abre su PROPIA transacción anidada (`BeginTransactionAsync`). EF Core 9 con Pomelo 9 las une en una sola transacción física (`SAVEPOINT`/`RELEASE`), por lo que la atomicidad es real: el rollback de SetupServicio cubre también el de Identity. Esto está implícito en el comportamiento de `Database.BeginTransactionAsync` con EF Core 9 + MySQL, pero conviene verificar con un `[MySqlFact]` que la rollback de la transacción outer también deshace el `INSERT INTO AspNetUsers`.

### 3.4 Capa API

`src/SGV.Api/Controllers/SetupController.cs`:

```csharp
[ApiController]
[Route(SetupApiRoutes.Base)]
[Produces("application/json")]
public sealed class SetupController(ISetupServicio setupServicio) : ControllerBase
{
    [HttpGet(SetupApiRoutes.StatusRelative)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken ct)
    {
        var response = await setupServicio.ObtenerEstadoAsync(ct).ConfigureAwait(false);
        return Ok(response);
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(SetupApiRoutes.SetupPolicyName)]
    [ProducesResponseType(typeof(SetupCommandResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Crear([FromBody] SetupRequest request, CancellationToken ct)
    {
        var result = await setupServicio.CrearAdminAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result)
            : result.Error!.StatusCode switch
            {
                400 => ValidationProblem(/* fieldErrors */),
                409 => Conflict(new ProblemDetails { Title = result.Error.Code.ToString(), Detail = result.Error.Message, StatusCode = 409 }),
                429 => StatusCode(StatusCodes.Status429TooManyRequests),
                _   => StatusCode(StatusCodes.Status500InternalServerError,
                                  new ProblemDetails { Title = "TransaccionFallida", Detail = result.Error.Message, StatusCode = 500 })
            };
    }
}
```

### 3.5 Capa Web

**Cambios en `src/SGV.Web/Program.cs`**:

- Registrar `builder.Services.AddMemoryCache()` (ya implícito en ASP.NET Core).
- Registrar `ISetupApiClient` análogo al patrón de `AuthApiClient`:

```csharp
public const string SetupHttpClientName = "SetupApiClient";

builder.Services.AddHttpClient<ISetupApiClient, SetupApiClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);   // paralelo a AuthApiClient
})
// SIN AddHttpMessageHandler<ApiBearerTokenHandler>(): setup es anónimo.
```

**`SetupApiClient`** (`src/SGV.Web/Integration/Setup/ISetupApiClient.cs` + `SetupApiClient.cs`):

```csharp
public interface ISetupApiClient
{
    Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken ct = default);
    Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken ct = default);
}

public sealed class SetupApiClient(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<SetupApiClient> logger) : ISetupApiClient
{
    private static readonly TimeSpan StatusTtl = TimeSpan.FromSeconds(30);

    public async Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue("setup:status", out SetupStatusResponse? hit) && hit is not null)
            return hit;

        try
        {
            var response = await httpClient.GetAsync(SetupApiRoutes.Status, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>(cancellationToken: ct).ConfigureAwait(false)
                         ?? new SetupStatusResponse(false);
            cache.Set("setup:status", status, StatusTtl);
            return status;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Fallo al consultar estado de setup; asumiendo requiresSetup=false (fail-open)");
            return new SetupStatusResponse(false);   // decisión 2.3
        }
    }

    public async Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken ct = default)
    {
        // Status análogo a PersonaApiClient.CreateAsync: 2xx → Success(...),
        // 400 → ValidationProblem(fieldErrors), 409 → Conflict(...), 429 → RateLimited,
        // 5xx → Unexpected(...). Misma estructura que los typed clients vigentes
        // para preservar consistencia con CommandResultMapper.Map + ApiProblemReader.ReadAsync.
    }
}
```

**Modificación a `SignInModel.OnGetAsync`** (`src/SGV.Web/Pages/Auth/SignIn.cshtml.cs`):

```csharp
public async Task OnGetAsync(
    [FromServices] ISetupApiClient setupApiClient,
    CancellationToken ct)
{
    var status = await setupApiClient.ObtenerEstadoAsync(ct).ConfigureAwait(false);
    if (status.RequiresSetup)
    {
        Response.Redirect("/auth/setup");   // PRG-friendly; equivalente a RedirectToPage
        return;
    }
}
```

`OnPostAsync` queda intacto (líneas 26-91 de `SignIn.cshtml.cs`).

**Razor Page nueva** `src/SGV.Web/Pages/Auth/Setup.cshtml` + `Setup.cshtml.cs`:

- Mismo `_AuthLayout.cshtml` (ya apuntado por `Pages/Auth/_ViewStart.cshtml`).
- 9 inputs con `[BindProperty] InputModel Input` y tag helpers `asp-for`/`asp-validation-for`/`asp-validation-summary`.
- Dropdown `TipoDocumento` poblado en `OnGetAsync` con la lista cacheada (30s) por `ObtenerEstadoAsync` extendido, o con un GET paralelo a `/api/v1/tipos-documento` (después de aplicar la decisión 2.2). **Decisión concreta**: el PageModel expone `TiposDocumentoOptions` (lista `SelectListItem`) que se hidrata con `ISetupApiClient.GetTiposDocumentoAsync()` (cliente dedicado sobre el endpoint ahora anónimo). Razón: el `SetupStatusResponse` debe ser liviano (sólo flag) para que el cache de 30s no incluya ~4 filas extra en cada hit del SignIn.
- `OnPostAsync`: captura `HttpRequestException`, `TaskCanceledException`, `SetupHttpResult` con status code, y mapea fieldErrors a `asp-validation-for`. Éxito → `TempData["SetupSuccess"] = "..."` + `RedirectToPage("/auth/sign-in")`.

**Decisión sobre el catálogo en el form**: se agrega un método extra en `ISetupApiClient` (`Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(...)`) que consume `GET /api/v1/tipos-documento` con `[AllowAnonymous]`. El PageModel lo llama en `OnGetAsync` cuando `RequiresSetup=true`. El cache de 30s del status no se reutiliza porque son dos dominios diferentes.

## 4. Wire-types detallados

```csharp
// src/SGV.Contracts/Setup/SetupRequest.cs
public sealed record SetupRequest(
    string Nombres,
    string Apellidos,
    string? Legajo,
    string Email,
    string UserName,
    string Password,
    Guid? TipoDocumentoId,
    string? NumeroDocumento,
    string? Telefono);

// src/SGV.Contracts/Setup/SetupResult.cs
public sealed record SetupResult(Guid PersonaId, string UserId, string UserName);

// src/SGV.Contracts/Setup/SetupStatusResponse.cs
public sealed record SetupStatusResponse(bool RequiresSetup);

// src/SGV.Contracts/Setup/SetupErrorCode.cs
public enum SetupErrorCode
{
    SetupYaCompletado,
    UserNameDuplicado,
    EmailDuplicado,
    PersonaConUsuario,
    EmailInvalido,
    UserNameInvalido,
    PasswordDebil,
    ValidacionIdentity,
    DatosInvalidos,
    TransaccionFallida
}

// src/SGV.Contracts/Setup/SetupApiRoutes.cs
public static class SetupApiRoutes
{
    public const string Base = "api/v1/setup";
    public const string StatusRelative = "status";
    public const string Status = "/" + Base + "/" + StatusRelative;
    public const string SetupPolicyName = "Setup";
}

// src/SGV.Aplicacion/Setup/SetupCommandResult.cs
public sealed record SetupCommandResult(
    bool IsSuccess,
    SetupResult? Value,
    SetupError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors)
{
    public static SetupCommandResult Success(SetupResult value) => new(true, value, null, null);
    public static SetupCommandResult Failure(SetupError error, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(false, null, error, fieldErrors);
}

public sealed record SetupError(
    ErrorCategoria Categoria,
    SetupErrorCode Code,
    string Message,
    int? StatusCode);
```

## 5. Transacciones y atomicidad

**Pseudocódigo de la transacción**:

```
1. BeginTransactionAsync (MySQL default = REPEATABLE READ)
2.   userManager.Users.AnyAsync(ct)
       └─ si true → Rollback + return SetupYaCompletado (409)
3.   personaServicio.CrearAsync(CrearPersonaRequest, ct)
       └─ crea Persona, valida unicidad, SaveChangesAsync (interceptor audita)
       └─ si falla → Rollback + propagar PersonaError (400/409)
4.   identityGateway.CrearAsync(CrearUsuarioRequest, ct)
       └─ abre transacción anidada (SAVEPOINT) o reusa la outer (EF 9 + Pomelo)
       └─ INSERT INTO AspNetUsers (UserManager) → INSERT INTO AspNetUserRoles (AddToRolesAsync)
       └─ si Identity falla → Rollback + IdentityError mapeado (400/409)
       └─ el índice único IX_AspNetUsers_NormalizedUserName rechaza duplicados aquí
5.   auditoriaServicio.RegistrarAsync("SetupInicial", "AltaPrimerAdministrador", userId="system")
       └─ INSERT INTO Auditorias con UserId="system"
6. CommitAsync
```

**Notas sobre atomicidad con EF Core 9 + Pomelo 9**:

- `Database.BeginTransactionAsync` desde el código que también llama `UserManager.CreateAsync` (que internamente hace `SaveChangesAsync`) requiere que ambos compartan la misma `DbContext` y conexión. EF Core 9 lo garantiza por scope.
- El `SaveChangesInterceptor` de auditoría (`AuditoriaSaveChangesInterceptor`) se dispara en el `SaveChangesAsync` que hace `UserManager` internamente. Esto significa que la fila de `Auditorias` queda registrada **antes** del commit y dentro del mismo lote.
- Si la transacción outer rollbackea, también rollbackea la fila de Auditorias (porque está en la misma transacción). Esto es deseable: la auditoría del setup completo es atómica con la creación.

**Riesgo residual**: si `auditoriaServicio.RegistrarAsync` falla por alguna razón (p.ej. columna `UserId` con constraint que rechaza `"system"` — improbable pero posible), la transacción rollbackea y el setup falla con 500. Es comportamiento correcto: o se crea Persona+Usuario+Auditoría, o nada.

## 6. Seguridad

- `[AllowAnonymous]` estrictamente en `SetupController` y en `TiposDocumentoController.Get`. Todos los demás endpoints mantienen su auth vigente.
- **Anti-forgery** en `Setup.cshtml`: `@Html.AntiForgeryToken()` en el form; `SetupModel` decorado con `[ValidateAntiForgeryToken]` (patrón vigente, ver `SignIn.cshtml.cs` línea 21 con `<form method="post">` que ya usa el token implícito por convención de Razor Pages).
- **Rate limiting** (`Setup` policy, 5 req / 15 min) cubre abuso por fuerza bruta de password y por DoS contra el endpoint público.
- **HTTPS / CORS**: reusar config actual de `Program.cs` (líneas 268-298); `AllowedOrigins` ya obligatorio fuera de Development. El shell web sólo consume el setup vía BFF same-origin (no hay CORS directo entre Web y API).
- **Logging estructurado sin secretos** (decisión producto): `logger.LogInformation("Setup attempt for UserName={UserName}", request.UserName)`. NUNCA loggear `Password`, `Email` completo ni tokens. La auditoría tampoco persiste password (verificado en `AuditoriaSaveChangesInterceptor` que excluye campos sensibles por nombre, sección "Auditoría" de `docs/decisiones-implementacion.md`).
- **Auditoría con `userId="system"`**: `AuditoriaServicio.RegistrarAsync` recibe `usuarioOperadorId: "system"` (línea 23 de `IAuditoriaServicio.cs`). La implementación prefiere este valor sobre `usuarioActual.UserId` (línea 39 de `AuditoriaServicio.cs`), por lo que aunque haya un HttpContext autenticado, el campo persistido es `"system"`.

## 7. Configuración y DI

**`src/SGV.Api/Program.cs`** (modificación):

```csharp
// Dentro del lambda de AddRateLimiter (después de ForgotPassword y ResetPassword):
options.AddFixedWindowLimiter(SetupApiRoutes.SetupPolicyName, policy =>
{
    policy.PermitLimit = 5;
    policy.Window = TimeSpan.FromMinutes(15);
    policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    policy.QueueLimit = 0;
});
```

(No requiere cambios en `services.AddInfraestructuraServicios()` porque `ISetupServicio` se registra ahí — ver abajo.)

**`src/SGV.Infraestructura/DependencyInjection.cs`** (adición, después de línea 86):

```csharp
// Setup inicial one-time del primer Administrador (issue #195).
services.AddScoped<ISetupServicio, SetupServicio>();
```

**`src/SGV.Api/Controllers/TiposDocumentoController.cs`** (modificación, línea 32):

```csharp
[HttpGet]
[AllowAnonymous]   // issue #195: el catálogo de TipoDocumento es inmutable y se necesita en /auth/setup
[ProducesResponseType(typeof(IReadOnlyList<TipoDocumentoDto>), StatusCodes.Status200OK)]
public async Task<ActionResult<IReadOnlyList<TipoDocumentoDto>>> GetAll(...)
```

`GetById` mantiene `[Authorize]` heredado (no se usa en setup).

**`src/SGV.Web/Program.cs`** (adición, después de los otros `AddHttpClient`):

```csharp
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ISetupApiClient, SetupApiClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});
// SIN AddHttpMessageHandler<ApiBearerTokenHandler>(): el endpoint es [AllowAnonymous].
```

**`src/SGV.Infraestructura/Persistencia/Entidades/PersonaEntity.cs`** y otros: **sin cambios**. La migración de esquema es nula; el setup usa tablas existentes (`Personas`, `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `Auditorias`).

## 8. Plan de pruebas (refinamiento del spec)

| Escenario (spec) | Capa | Tipo de test | Archivo de tests sugerido |
|---|---|---|---|
| REQ-SETUP-001 / base vacía requiere setup | API integración (`[MySqlFact]`) | WebApplicationFactory con DB limpia | `tests/SGV.Tests/Setup/SetupStatusEndpointTests.cs` |
| REQ-SETUP-001 / base con usuarios no requiere setup | API integración (`[MySqlFact]`) | Seed 1 usuario; verificar `requiresSetup=false` | mismo archivo |
| REQ-SETUP-002 / creación válida | API integración (`[MySqlFact]`) | POST con datos válidos; assert Persona + Usuario + rol `Administrador` + fila Auditorias | `tests/SGV.Tests/Setup/SetupHappyPathMySqlFactTests.cs` |
| REQ-SETUP-002 / setup ya completado → 409 | API integración (`[MySqlFact]`) | Seed 1 usuario; POST setup; assert 409 + `SetupYaCompletado` | `tests/SGV.Tests/Setup/SetupAlreadyCompletedTests.cs` |
| REQ-SETUP-002 / validación Identity (password corta) → 400 | API integración (`[MySqlFact]`) | POST password inválida; assert 400 + fieldErrors | `tests/SGV.Tests/Setup/SetupValidationTests.cs` |
| REQ-SETUP-002 / fallo transaccional → 500 | API unit (`[Fact]`) | Mock `IPersonaRepository` que falla en `AddAsync`; assert 500 + rollback | `tests/SGV.Tests/Setup/SetupTransactionalFailureTests.cs` |
| REQ-SETUP-003 / requests concurrentes → 1 OK + 1 conflict | API integración (`[MySqlFact]`) | Dos `Task.WhenAll` con HttpClient paralelo; assert exactamente 1×200 + 1×409 | `tests/SGV.Tests/Setup/SetupConcurrencyMySqlFactTests.cs` |
| REQ-SETUP-004 / auditoría con userId="system" | API integración (`[MySqlFact]`) | POST setup; consultar `Auditorias`; assert `UserId="system"`, `EntityName="SetupInicial"`, `Operation="AltaPrimerAdministrador"` | `tests/SGV.Tests/Setup/SetupAuditTrailTests.cs` |
| REQ-SETUP-004 / rate limit 429 | API integración (`[Fact]`) | 6 POSTs rápidos; assert primero 5×200/409 + 1×429 con `Retry-After` | `tests/SGV.Tests/Setup/SetupRateLimitTests.cs` |
| REQ-SETUP-005 / SignIn redirige cuando DB vacía | Web integration (`WebApplicationFactory`) | Mock `ISetupApiClient` que devuelve `RequiresSetup=true`; GET `/auth/sign-in`; assert `Location: /auth/setup` | `tests/SGV.Tests/Web/Auth/SignInSetupRedirectTests.cs` |
| REQ-SETUP-005 / renderiza 9 campos | Web integration | GET `/auth/setup`; assert HTML contiene los 9 inputs + `<select>` para TipoDocumento | `tests/SGV.Tests/Web/Auth/SetupPageRenderTests.cs` |
| REQ-SETUP-005 / setup no disponible redirige | Web integration | Mock `ISetupApiClient.RequiresSetup=false`; GET `/auth/setup`; assert redirect o 404 | mismo archivo |
| REQ-SETUP-005 / catálogo de documentos en dropdown | Web integration | Mock `ISetupApiClient.GetTiposDocumentoAsync` con 4 tipos; GET `/auth/setup`; assert las 4 `<option>` | mismo archivo |
| REQ-SETUP-006 / submit exitoso → PRG a sign-in | Web integration | Mock API responde 200; POST form; assert redirect 302 a `/auth/sign-in` + TempData | `tests/SGV.Tests/Web/Auth/SetupSubmitSuccessTests.cs` |
| REQ-SETUP-006 / errores de validación por campo | Web integration | Mock API responde 400 con fieldErrors; POST form; assert `asp-validation-for` muestra los mensajes | mismo archivo |
| REQ-SETUP-006 / error de transporte | Web integration | Mock API lanza `HttpRequestException`; POST form; assert mensaje recuperable (sin reintento ciego) | mismo archivo |
| Cache de status TTL 30s | Web unit | Mock API con `RecordingHttpMessageHandler`; 3 GETs a `/auth/sign-in` en <30s; assert sólo 1 hit al API | `tests/SGV.Tests/Web/Auth/SignInStatusCacheTests.cs` |

Cantidad total sugerida: ~17 tests, todos con valor de regresión concreto (no se listan getters/setters ni constructores triviales).

## 9. Riesgos residuales

| Riesgo | Severidad | Mitigación |
|---|---|---|
| Race window entre `AnyUsersAsync()` y `CreateAsync` deja 2 Personas y 0 Usuarios si el segundo `CreateAsync` falla por un motivo distinto a `DuplicateUserName` (p.ej. timeout de MySQL) | Baja | Probabilidad <0.01%; el siguiente intento del usuario da 409 y limpia vía `PersonaServicioComandos.DesactivarAsync`+`UsuarioIdentityGateway.EliminarAsync` (admin ya puede hacerlo). Documentar en `docs/decisiones-implementacion.md` §"Setup inicial" |
| API caída + DB vacía → SignIn no redirige a `/auth/setup` | Baja | Fail-open con cache 30s. Cuando API vuelva, próximo GET ya redirige. UX degradado es preferible a fail-closed (que rompe acceso a producción) |
| `[AllowAnonymous]` en `TiposDocumentoController.Get` filtra patrón regex y longitud de validación de documentos | Trivial | El catálogo es inmutable, no expone PII ni reglas de seguridad. Aceptado como trade-off explícito (decisión 2.2) |
| `AuditoriaServicio` depende de `IUsuarioActual`, que durante setup podría no tener HttpContext | Baja | `IUsuarioActual` es `Scoped`; si no hay HttpContext, `usuarioActual.UserId` retorna string.Empty o null; el fallback en línea 39 de `AuditoriaServicio.cs` es `usuarioOperadorId ?? usuarioActual.UserId`, y como pasamos `"system"` explícito, gana el "system". Verificado en `UsuarioActualHttpContext` (no leído, inferencia por contrato) |
| La auditoría `AltaPrimerAdministrador` queda en la misma transacción que el insert de Persona/Usuario; si falla, no hay auditoría del intento | Baja | Comportamiento correcto: o se crea todo o nada. El log estructurado de la API captura el intento con `Logger.LogInformation("Setup attempt ...")` antes de commit; eso cubre el "intento fallido" |
| `SetupApiClient.GetTiposDocumentoAsync` agrega un round-trip en cada render de `/auth/setup` | Baja | Cacheable en `IMemoryCache` con TTL 60s (más generoso que el status porque el catálogo es realmente inmutable); decisión de implementación en sdd-tasks |

## 10. Plan de rollout

| WU | Scope | Archivos | Aprox. líneas | Tipo |
|---|---|---|---|---|
| WU-1 | Wire-types + constantes + rutas | `src/SGV.Contracts/Setup/*.cs` (5 archivos nuevos) | ~120 | feat |
| WU-2 | SetupServicio (Aplicación + Infraestructura) + tests unitarios | `src/SGV.Aplicacion/Setup/{ISetupServicio,SetupServicio,SetupCommandResult,Validaciones/SetupRequestValidator}.cs` + `src/SGV.Infraestructura/Setup/SetupServicio.cs` + `tests/SGV.Tests/Setup/*Unit*.cs` | ~600 | feat |
| WU-3 | SetupController + `[AllowAnonymous]` en TiposDocumentoController.Get + AddRateLimiter | `src/SGV.Api/Controllers/SetupController.cs` + 2 ediciones en `Program.cs` + 1 edición en `TiposDocumentoController.cs` + `tests/SGV.Tests/Setup/*MySqlFact*.cs` | ~500 | feat |
| WU-4 | Razor Page `/auth/setup` + `SetupApiClient` + tests web | `src/SGV.Web/Pages/Auth/Setup.{cshtml,cshtml.cs}` + `src/SGV.Web/Integration/Setup/*` + edición en `Program.cs` + `tests/SGV.Tests/Web/Auth/*` | ~700 | feat |
| WU-5 | Filtro de redirección en `SignInModel.OnGetAsync` + cache TTL + tests | edición `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` + `tests/SGV.Tests/Web/Auth/SignInSetupRedirectTests.cs` | ~150 | feat |
| WU-6 | Documentación | edición `docs/decisiones-implementacion.md` (nueva sección §"Setup inicial — issue #195") | ~80 | docs |

**Total estimado**: 8 archivos nuevos, 5 modificados, ~2150 líneas (código + tests + docs). Sobre el umbral de chained PR (`chained-pr` skill sugiere PRs <400 líneas) → **recomendado encadenar en 2-3 PRs**:

1. **PR 1**: WU-1 + WU-2 (Contracts + Servicio + tests unit). Aprox. 720 líneas. Revisable.
2. **PR 2**: WU-3 (Controller + rate limit + `[AllowAnonymous]` catálogo + tests `[MySqlFact]`). Aprox. 500 líneas. Revisable.
3. **PR 3**: WU-4 + WU-5 + WU-6 (Web + redirect + docs). Aprox. 930 líneas. Dividible en 3a (Web cliente + page) y 3b (redirect + docs).

Si sdd-tasks decide implementar todo en un PR, el budget sigue dentro de lo razonable (~2150 líneas es grande pero no prohibitivo si la base de revisión tiene contexto del design completo).

## 11. Preguntas abiertas para sdd-tasks

Ninguna — el design cubre todo lo necesario para implementar.

Las 6 preguntas del proposal.md (líneas 66-70) quedan resueltas en §2.1-2.6:

1. Bloqueo MySQL → default `REPEATABLE READ` + índice único Identity como defensa.
2. Carga de `TipoDocumento` → `[AllowAnonymous]` en `TiposDocumentoController.Get`.
3. Indisponibilidad de API → fail-open con cache TTL 30s en `IMemoryCache`.
4. Códigos Identity → reusar `IdentityErrorMap` + nuevo `SetupErrorCode` enum.
5. Rate limiting → política `Setup` 5 req / 15 min, mismo patrón que `ForgotPassword`.
6. 409 vs 404 → **409 Conflict** con código `SetupYaCompletado`.

---

## skill_resolution

paths-injected: `.agents/skills/razor-pages-patterns/SKILL.md`, `.agents/skills/database-designer/SKILL.md`, `.agents/skills/mysql/SKILL.md`, `.agents/skills/dotnet-csharp/SKILL.md`, `.agents/skills/dotnet-best-practices/SKILL.md`, `.agents/skills/dotnet-xunit/SKILL.md`.
