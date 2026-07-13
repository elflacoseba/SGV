# Design: Taxonomía única de errores para `CommandResult` y clientes HTTP de Web

Change: `2026-07-13-taxonomia-errores-commandresult` · Issue: #125 · Modo híbrido.

## 1. Resumen del enfoque

Una sola categoría `ErrorCategoria` definida como `enum` append-only en `src/SGV.Contracts/Comun/ErrorCategoria.cs` (manteniendo `SGV.Contracts` como leaf — verificado: `src/SGV.Contracts/SGV.Contracts.csproj` sólo referencia `Microsoft.IdentityModel.Tokens 8.14.0`, sin `ProjectReference`). Cada uno de los seis `*Error` records vigentes (`HabilidadError`, `CargoError`, `PuestoError`, `UnidadOrganizativaError`, `CargoSkillError`, `UsuarioError`) y los cinco `*DeleteResult` ganan `Categoria: ErrorCategoria`; los enums `*ErrorType` vigentes se marcan `[Obsolete("Use ErrorCategoria")]` durante el ciclo del change y se eliminan al archivar. La conversión entre los enums viejos y `ErrorCategoria` se hace **nombre-a-nombre** mediante `switch` expressions explícitos — no por ordinal (ver §2.1). Un único helper `CommandResultMapper.Map(HttpResponseMessage, ApiProblemReader.Result)` en `src/SGV.Web/Integration/Common/CommandResultMapper.cs` reemplaza las cinco matrices privadas de los clientes. `IAuthSessionRedirector.TryRedirectToLogin` (con guard `IsLocalUrl`) permite que los `PageModel` invoquen un redirect seguro cuando reciben `Categoria.Unauthorized`. `ApiResults` gana un `MapCategoria(ErrorCategoria)` exhaustivo y los `*DeleteResult` se alinean exponiendo `Categoria` y `StatusCode`. Las excepciones nativas (`HttpRequestException`, `TaskCanceledException`) **siguen propagándose** desde los clientes HTTP, sin convertirse a `Categoria.Transport`, preservando `web-apiclient-transport-contract`.

## 2. Modelo de datos

### 2.1 Enum `ErrorCategoria` — conversión por nombre, NO por ordinal

```csharp
namespace SGV.Contracts.Comun;

/// <summary>
/// Categoría semántica de fallo devuelta por los *CommandResult y *DeleteResult
/// de SGV.Contracts. Append-only: NO reordenar ni reasignar ordinales.
/// </summary>
public enum ErrorCategoria
{
    NotFound = 0,      // HTTP 404
    Conflict = 1,      // HTTP 409
    Validation = 2,    // HTTP 400/422 (con FieldErrors opcional)
    Unauthorized = 3,  // HTTP 401
    Forbidden = 4,     // HTTP 403
    Transport = 5,     // HTTP 408/5xx/502/503/504 desde HttpResponseMessage
    Unexpected = 6     // Cualquier otro status no exitoso (incluye 3xx)
}
```

**F1 — Diferencia de ordinales entre `ErrorCategoria` y los enums vigentes.** Los enums vigentes tienen los ordinales siguientes (verificados contra `src/SGV.Contracts/Organizacion/Comandos/CargoSkillCommandResult.cs` y los demás archivos):

| Variante | `ErrorCategoria` | `CargoSkillErrorType` | `HabilidadErrorType` | `CargoErrorType` | `PuestoErrorType` | `UnidadOrganizativaErrorType` | `UsuarioErrorType` |
|---|---|---|---|---|---|---|---|
| NotFound | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Conflict | 1 | 2 | 1 | 1 | 1 | 1 | 1 |
| Validation | 2 | 1 | 2 | 2 | 2 | 2 | 2 |
| Unauthorized | 3 | 3 | — | — | — | — | 3 |
| Forbidden | 4 | 4 | — | — | — | — | — |
| Transport | 5 | 5 | — | — | — | — | — |
| Infrastructure | — | — | 3 | — | — | — | — |
| Unexpected | 6 | — | — | — | — | — | — |

**Sólo NotFound/Unauthorized/Forbidden/Transport coinciden por ordinal**. `Validation` y `Conflict` están invertidos entre `CargoSkillErrorType` y `ErrorCategoria`; `HabilidadErrorType.Infrastructure` (3) no tiene equivalente directo en `ErrorCategoria` (mapea a `Transport = 5`).

**Prohibido** el cast `(ErrorCategoria)(int)cargoSkillErrorType.Validation` o cualquier conversión implícita ordinal entre los dos enums. Toda traducción se hace por nombre con `switch expression` exhaustivo:

```csharp
public static ErrorCategoria ToCategoria(HabilidadErrorType type) => type switch
{
    HabilidadErrorType.NotFound       => ErrorCategoria.NotFound,
    HabilidadErrorType.Conflict       => ErrorCategoria.Conflict,
    HabilidadErrorType.Validation     => ErrorCategoria.Validation,
    HabilidadErrorType.Infrastructure => ErrorCategoria.Transport, // 5xx upstream
    _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Sin mapping"),
};

public static HabilidadErrorType ToTipo(ErrorCategoria categoria) => categoria switch
{
    ErrorCategoria.NotFound     => HabilidadErrorType.NotFound,
    ErrorCategoria.Conflict     => HabilidadErrorType.Conflict,
    ErrorCategoria.Validation   => HabilidadErrorType.Validation,
    ErrorCategoria.Transport    => HabilidadErrorType.Infrastructure,
    ErrorCategoria.Unauthorized => throw new NotSupportedException("HabilidadErrorType no tiene Unauthorized."),
    ErrorCategoria.Forbidden    => throw new NotSupportedException("HabilidadErrorType no tiene Forbidden."),
    ErrorCategoria.Unexpected   => throw new NotSupportedException("HabilidadErrorType no tiene Unexpected."),
};
```

La misma pareja de funciones se provee para `CargoErrorType`, `PuestoErrorType`, `UnidadOrganizativaErrorType`, `UsuarioErrorType` y `CargoSkillErrorType`, todas en `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs`. La simetría `ToCategoria`/`ToTipo` permite round-trip en código y tests. Tests RED explícitos en §11 verifican que para cada variante de `CargoSkillErrorType` (6) y de `ErrorCategoria` (7), el round-trip preserva el nombre semántico cuando existe equivalente.

### 2.2 Forma final de los `*Error` records

Cada record gana una propiedad `Categoria: ErrorCategoria` y conserva verbatim `Code`, `Message` y `FieldErrors` cuando aplique. Para los **cinco** records que hoy no exponen `StatusCode` (`CargoError`, `PuestoError`, `UnidadOrganizativaError`, `CargoSkillError`, `UsuarioError`), se agrega `StatusCode: int?` opcional para preservar metadata de diagnóstico. `HabilidadError` ya tiene `StatusCode`, así que no cambia su firma:

```csharp
public sealed record HabilidadError(
    HabilidadErrorType Type,            // [Obsolete] durante el change
    string Code,
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);
```

Decisión de diseño: **NO se reemplaza `Type` por `Categoria`** en el mismo release. La coexistencia mantiene source-compat para callers que aún ramifican por `HabilidadErrorType.Infrastructure` o `CargoSkillErrorType.Transport`. XML doc indica que `Categoria` es la fuente de verdad para código nuevo y `Type` queda como puente hacia el archive.

### 2.3 Equivalencias `Categoria ↔ Código HTTP`

| `ErrorCategoria` | Status HTTP observable | Default `Code` | Default `Message` (español) |
|---|---|---|---|
| `NotFound` | 404 | `NotFound` | `Recurso no encontrado.` |
| `Conflict` | 409 | `Conflict` | `Conflicto.` |
| `Validation` | 400/422 | `ValidationError` (con FieldErrors) o `BadRequest` (sin FieldErrors) | `Uno o más campos son inválidos.` o `Solicitud inválida.` |
| `Unauthorized` | 401 | `Unauthorized` | `Su sesión expiró. Vuelva a iniciar sesión.` |
| `Forbidden` | 403 | `Forbidden` | `Acceso denegado.` |
| `Transport` | 408/500/502/503/504 | `TransportError` | `El servicio no respondió correctamente. Intentá nuevamente.` |
| `Unexpected` | resto no 2xx (incluye 3xx) | `Unexpected` | `Respuesta inesperada del servidor.` |

`StatusCode` se preserva verbatim en todos los casos como metadata.

### 2.4 `[Obsolete]` sobre los enums previos

```csharp
[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria. Will be removed in the archive of change 2026-07-13.")]
public enum HabilidadErrorType { NotFound, Conflict, Validation, Infrastructure }
```

Política de transición:
- **Slice 1**: marcar los 6 enums como `[Obsolete]`, agregar `Categoria` a los 6 `*Error` records Y a los 5 `*DeleteResult`. El código de producción existente sigue compilando.
- **Slice 2–3**: actualizar todos los call sites para usar `Categoria`. Los enums obsoletos siguen existiendo como puente.
- **Archive**: eliminar los enums. Evaluar remoción de `HabilidadError.StatusCode` si no quedó ningún consumidor.

### 2.5 `*DeleteResult`: doble modelo preservando `StatusCode`

Los cinco `*DeleteResult` (`HabilidadDeleteResult`, `CargoDeleteResult`, `PuestoDeleteResult`, `UnidadOrganizativaDeleteResult`, `CargoSkillDeleteResult`) convergen al shape canónico:

```csharp
public sealed record CargoSkillDeleteResult(
    bool Succeeded,
    ErrorCategoria Categoria,           // nuevo
    HttpStatusCode? StatusCode,         // preservado verbatim (cambia de non-nullable → nullable en PuestoDeleteResult)
    string? Code,
    string? Message);
```

Notas:
- `PuestoDeleteResult.StatusCode` actualmente es non-nullable (`HttpStatusCode`); se unifica a `HttpStatusCode?` para coincidir con los otros cuatro y absorber el caso "204 sin status code" sin inconsistencias.
- `Categoria` queda con valor `default` (`NotFound = 0`) cuando `Succeeded == true` y se popula con la categoría del status HTTP cuando `Succeeded == false`.

**F2 — Decisión de slice.** `Categoria` y `StatusCode: HttpStatusCode?` se agregan a los 5 `*DeleteResult` **en Slice 1**, junto con la adición de `Categoria` a los 6 `*Error` records. Esto entrega el contrato completo de DeleteResults en Slice 1 y elimina la dependencia oculta de Slice 3 sobre Slice 4. Slice 4 queda con sólo `ApiResults.MapCategoria` exhaustivo y los tests de DeleteResultContract. Ver §11.

## 3. Capa de aplicación

`SGV.Aplicacion` ya consume `SGV.Contracts` y no añade nuevas dependencias. La capa de aplicación produce los `*Error` records que ya tenían firma pública. No se introduce un mapper HTTP→categoría en Aplicación: la matriz status→categoría es un detalle del borde Web. Cuando la capa de Aplicación quiere emitir un `*Error` con `Categoria`, instancia el record con la categoría ya resuelta — la decisión de qué `Categoria` emitir corresponde al caso de uso, no al mapper HTTP.

No se mueven a la nueva taxonomía: `PersonaCommandResult`, `PersonaSkillCommandResult`, `OcupacionCommandResult` (viven en `SGV.Aplicacion`, fuera del scope).

## 4. Capa de API

### 4.1 `ApiResults` exhaustivo por categoría

`ApiResults` mantiene las firmas existentes (`ToProblemResult(CargoError, …)`, etc.) por compat, **y agrega un único switch exhaustivo** centralizado:

```csharp
private static int MapCategoria(ErrorCategoria categoria) => categoria switch
{
    ErrorCategoria.Validation   => StatusCodes.Status400BadRequest,
    ErrorCategoria.NotFound     => StatusCodes.Status404NotFound,
    ErrorCategoria.Conflict     => StatusCodes.Status409Conflict,
    ErrorCategoria.Unauthorized => StatusCodes.Status401Unauthorized,
    ErrorCategoria.Forbidden    => StatusCodes.Status403Forbidden,
    ErrorCategoria.Transport    => StatusCodes.Status503ServiceUnavailable,
    ErrorCategoria.Unexpected   => StatusCodes.Status500InternalServerError,
};
```

**F3 — Exhaustiveidad verificada en runtime, no en compile.** El repo no tiene `TreatWarningsAsErrors` en `Directory.Build.props` ni en los `.csproj` (verificado). CS8509 y CS8524 son **warnings**, no errores: agregar un valor nuevo al enum degrada silenciosamente a 400 Bad Request hasta que los tests rojos lo detecten. La protección es doble:

1. **Switch expression SIN `default:`** → CS8524 emitido como warning; tests rojos si alguien lo introduce.
2. **`[Theory]` parametrizada contra `Enum.GetValues<ErrorCategoria>()`** en `ApiResultsTests` que exige un `Status` ≥ 400 específico por categoría (ver §11, Slice 4). Cualquier nuevo valor sin rama cae en `Validation` y los tests lo cachan.
3. **`TreatWarningsAsErrors` queda fuera del alcance de #125** y se propone como follow-up en `decisiones-implementacion.md`.

Los métodos `MapCargoStatus`, `MapPuestoStatus`, etc. vigentes se conservan (compat con firmas existentes) y delegan a `MapCategoria` mapeando `*ErrorType` → `ErrorCategoria` con el switch explícito de §2.1. Esto evita reordenar firmas y reduce el blast radius.

### 4.2 Tests exhaustivos

`tests/SGV.Tests/Api/Infrastructure/Results/ApiResultsTests.cs` gana una `[Theory]` parametrizada contra `Enum.GetValues<ErrorCategoria>()` que exige un `Status` ≥ 400 específico para cada categoría. Esto cierra el riesgo de "switch silenciosamente degradando a 400": si alguien agrega `ErrorCategoria.Redirected = 7` y olvida una rama, el test rojo lo detecta.

## 5. Mapper compartido HTTP → `CommandError`

### 5.1 Nombre y ubicación

`CommandResultMapper` (estático, en `src/SGV.Web/Integration/Common/CommandResultMapper.cs`). Coherente con `ApiProblemReader`/`TransportFailureClassifier`.

### 5.2 Surface pública

```csharp
public static class CommandResultMapper
{
    public static (ErrorCategoria Categoria, string Code, string Message, int? StatusCode) Map(
        HttpResponseMessage response,
        ApiProblemReader.Result problem);
}
```

### 5.3 Combinación con `ApiProblemReader` y `TransportFailureClassifier`

`CommandResultMapper.Map` **sólo opera sobre `HttpResponseMessage`**, no sobre excepciones. `ApiProblemReader` parsea el body; `TransportFailureClassifier` clasifica excepciones nativas en `PageModel`; `CommandResultMapper` une ambos en el output. Lo único que el change agrega a `TransportFailureClassifier` es:

```csharp
public static bool IsDnsFailure(HttpRequestException exception)
{
    ArgumentNullException.ThrowIfNull(exception);
    return exception.InnerException is SocketException se
        && se.SocketErrorCode == SocketError.NameResolutionFailure;
}
```

### 5.4 Tabla de mapeo HTTP → categoría

| Status HTTP | `ErrorCategoria` | Default `Code` | Default `Message` |
|---|---|---|---|
| 200/201/204 | (no entra al mapper) | — | — |
| 400, 422 con FieldErrors | `Validation` | `parsed.Title ?? "ValidationError"` | `parsed.Detail ?? "Uno o más campos son inválidos."` |
| 400, 422 sin FieldErrors | `Validation` | `parsed.Title ?? "BadRequest"` | `parsed.Detail ?? "Solicitud inválida."` |
| 401 | `Unauthorized` | `parsed.Title ?? "Unauthorized"` | `parsed.Detail ?? "Su sesión expiró. Vuelva a iniciar sesión."` |
| 403 | `Forbidden` | `parsed.Title ?? "Forbidden"` | `parsed.Detail ?? "Acceso denegado."` |
| 404 | `NotFound` | `parsed.Title ?? "NotFound"` | `parsed.Detail ?? "Recurso no encontrado."` |
| 408, 500, 502, 503, 504 | `Transport` | `parsed.Title ?? "TransportError"` | `parsed.Detail ?? "El servicio no respondió correctamente. Intentá nuevamente."` |
| 409 | `Conflict` | `parsed.Title ?? "Conflict"` | `parsed.Detail ?? "Conflicto."` |
| Otro (3xx, 1xx, status desconocido) | `Unexpected` | `parsed.Title ?? "Unexpected"` | `parsed.Detail ?? "Respuesta inesperada del servidor."` |

Defaults en español congruentes con la UI vigente (cada copy se extrajo literalmente de los call sites actuales: "Intentá nuevamente" en Cargo/Habilidad/CargoSkill, "Su sesión expiró" en CargoSkill, "Acceso denegado" en CargoSkill).

## 6. `IAuthSessionRedirector`

### 6.1 Firma, contrato y guard anti open-redirect

```csharp
namespace SGV.Web.Integration.Common;

/// <summary>
/// Helper inyectable que traduce Categoria.Unauthorized en una redirección a
/// /auth/sign-in?returnUrl=... en lugar de mostrar el formulario con un
/// mensaje inline. La decisión queda en el PageModel para mantener
/// simetría con el resto de la frontera de auth.
/// </summary>
public interface IAuthSessionRedirector
{
    /// <summary>
    /// Si existe HttpContext y <paramref name="returnUrl"/> es local, emite
    /// un RedirectResult a /auth/sign-in con returnUrl preservado. Si el
    /// returnUrl NO es local (URL absoluta, protocolo distinto, o path
    /// externo), se ignora silenciosamente para mitigar open-redirect.
    /// Devuelve el IActionResult si redirigió, o null si no hay contexto
    /// (tests sin host).
    /// </summary>
    IActionResult? TryRedirectToLogin(string? returnUrl = null);
}

internal sealed class AuthSessionRedirector(
    IHttpContextAccessor accessor,
    IUrlHelperFactory urlHelperFactory) : IAuthSessionRedirector
{
    public IActionResult? TryRedirectToLogin(string? returnUrl = null)
    {
        var ctx = accessor.HttpContext;
        if (ctx is null) return null;

        var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
            && IsLocalUrl(returnUrl, ctx)
            ? returnUrl
            : null;

        var urlHelper = urlHelperFactory.GetUrlHelper(new ActionContext(
            ctx, ctx.GetRouteData(), ctx.GetEndpoint() ?? (Endpoint?)null));
        var target = safeReturnUrl is null
            ? urlHelper.Page("/Auth/SignIn") ?? "/auth/sign-in"
            : urlHelper.Page("/Auth/SignIn", new { returnUrl = safeReturnUrl }) ?? "/auth/sign-in";

        return new RedirectResult(target);
    }

    private static bool IsLocalUrl(string url, HttpContext ctx)
    {
        // Guard defensivo: rechaza URLs absolutas y paths con scheme externo.
        if (url.StartsWith("//", StringComparison.Ordinal)) return false;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute.IsLoopback || string.Equals(
                absolute.Host,
                ctx.Request.Host.Host,
                StringComparison.OrdinalIgnoreCase);
        return url.StartsWith("/", StringComparison.Ordinal)
            && !url.StartsWith("//", StringComparison.Ordinal)
            && !url.StartsWith("/\\", StringComparison.Ordinal);
    }
}
```

**Limitación documentada**: si el request original era POST, el redirect pierde form data. Los `PageModel` deben guardar el form state via TempData si esperan re-presentar el formulario tras login. Esta limitación es **preservada** por el comportamiento actual (no es regresión).

### 6.2 Uso en PageModel

```csharp
if (result.Error?.Categoria == ErrorCategoria.Unauthorized)
{
    var redirect = authRedirector.TryRedirectToLogin(returnUrl: Request.Path);
    if (redirect is not null) return redirect;
}
```

`Forbidden` NO redirige: el shell debe mostrar "Acceso denegado" (la página `/error/403` ya cubre este flujo desde middleware).

### 6.3 Registro en DI

```csharp
builder.Services.AddHttpContextAccessor();   // ya existe
builder.Services.AddScoped<IUrlHelperFactory, UrlHelperFactory>();
builder.Services.AddScoped<IAuthSessionRedirector, AuthSessionRedirector>();
```

`Scoped` porque el helper usa `IHttpContextAccessor` y `IUrlHelperFactory` (ambos scoped).

## 7. Cambios por cliente HTTP

Sin cambios estructurales respecto al diseño original; los clientes consumen `CommandResultMapper.Map` y construyen el record de dominio específico (`new HabilidadError(HabilidadErrorType.NotFound, code, message, status, categoria)`, etc.). Para `CargoApiClient.UpsertSkillAsync` se añade el mapeo `ErrorCategoria → CargoSkillErrorType` vía `ErrorCategoriaMappers.ToTipo` (§2.1).

## 8. Cambios en UI / PageModel

### 8.1 Switches exhaustivos por PageModel

**F3 — Cobertura de exhaustividad por PageModel.** Cada `PageModel` que ramifica por `Categoria` debe tener un test que enumere las 7 variantes y asserte que el switch cubre cada una (sin default silencioso). Esto compensa la falta de `TreatWarningsAsErrors` en el repo: el switch NO tiene `default:` y los tests verifican que cada `Categoria` cae en una rama explícita. Para categorías no anticipadas en el flujo (p.ej. `Categoria.Unauthorized` en un POST de Create), el switch debe lanzar `SwitchExpressionException` para no degradar silenciosamente. Plantilla:

```csharp
var message = result.Error?.Categoria switch
{
    ErrorCategoria.NotFound     => "El cargo ya no está disponible.",
    ErrorCategoria.Conflict     => $"No se pudo eliminar el cargo. {result.Error.Message}",
    ErrorCategoria.Transport    => "No se pudo eliminar el cargo. Intentá nuevamente.",
    ErrorCategoria.Unexpected   => "No se pudo eliminar el cargo. Intentá nuevamente.",
    ErrorCategoria.Validation   => result.Error.Message,
    ErrorCategoria.Unauthorized => throw new SwitchExpressionException(
        $"Unauthorized no se espera en OnPostQuitar; use IAuthSessionRedirector antes."),
    ErrorCategoria.Forbidden    => "No tiene permisos para realizar esta operación.",
    _ => throw new SwitchExpressionException($"Unhandled categoria: {result.Error?.Categoria}")
};
```

**Lista canónica de PageModels a inyectar `IAuthSessionRedirector`** (verificado contra `find src/SGV.Web/Pages -name "*.cshtml.cs"` y `grep -l OnPost`):

| Dominio | PageModels con `OnPost*` que consumen `*CommandResult`/`*DeleteResult` | Count |
|---|---|---|
| Cargos | `Index`, `Create`, `Edit`, `Habilidades` | 4 |
| Puestos | `Index`, `Create`, `Edit` | 3 |
| UnidadesOrganizativas | `Index`, `Create`, `Edit`, `Details` | 4 |
| Habilidades | `Index`, `Create`, `Edit` | 3 |
| **Total** | | **14** |

NOTA: NO existen `Cargos/Reactivate.cshtml.cs` ni `Habilidades/Reactivate.cshtml.cs` ni `Puestos/Reactivate.cshtml.cs` ni `UnidadesOrganizativas/Reactivate.cshtml.cs` como archivos separados — la reactivación por PRG ocurre dentro de `Index.cshtml.cs` (`OnPostReactivar`). `Habilidades/Cargos.cshtml.cs` y `UnidadesOrganizativas/Organigrama.cshtml.cs` son read-only (no usan `*CommandResult` ni reciben `Categoria`).

### 8.2 Preservación de asociación a `FieldErrors`

`CargoPostResultMapper.TryMap` y `PuestoPostResultMapper.TryMap` siguen funcionando porque `result.FieldErrors` está intacto.

### 8.3 Unificación de copy

| Categoría | Copy canónica (en español) |
|---|---|
| `Transport` | "No se pudo contactar al servicio. Intentá nuevamente." |
| `Unauthorized` | "Su sesión expiró. Vuelva a iniciar sesión." (acompañado de redirect) |
| `Forbidden` | "No tiene permisos para realizar esta operación." |
| `Conflict` (en Delete) | `$"No se pudo eliminar el recurso. {message}".Trim()` |
| `NotFound` | "El recurso ya no está disponible." |
| `Unexpected` | "Respuesta inesperada del servidor." |

Estas cadenas se exponen como `public const string` en `src/SGV.Web/Pages/Common/PageFeedback.cs`.

### 8.4 Eliminación de filtros manuales

`Habilidades/Create.cshtml.cs` tiene `catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is OperationCanceledException)` — ese filtro NO se elimina: es la propagación nativa de `web-apiclient-transport-contract`. Lo que se elimina es el `switch (error.Type)` interno, no el catch.

## 9. Cambios en migraciones

**No.** La taxonomía es interna a `SGV.Contracts` y `SGV.Web`. No toca entidades de dominio (`SGV.Dominio`), no agrega columnas, no cambia índices únicos ni soft delete.

## 10. Cambios en auditoría

**No-impacto explícito.** `InterceptorAuditoria` captura entidades EF, no tipos de `SGV.Contracts`. La nueva propiedad `Categoria` no se serializa a la tabla `Auditorias`.

## 11. Estrategia de pruebas (orden test-first)

`strict_tdd: true`: cada bloque de código de producción va precedido por tests rojos. El orden siguiente es mandatorio.

**F2 — Reordenamiento de slices.** La adición de `Categoria` y `StatusCode` a los 5 `*DeleteResult` se mueve a **Slice 1** (antes estaba en Slice 4). Esto elimina la dependencia oculta de Slice 3 sobre Slice 4. Grafo de dependencias resultante:

```
Slice 1 (contratos: enum + 6 Error records + 5 DeleteResult + mappers ErrorCategoria↔ErrorType)
   └── Slice 2 (CommandResultMapper + IsDnsFailure + 4 clientes Web)
          ├── Slice 3 (IAuthSessionRedirector + UI en 14 PageModels)
          └── Slice 4 (ApiResults.MapCategoria exhaustivo + tests DeleteResultContract)
```

Slice 4 sólo agrega tests y el switch exhaustivo en `ApiResults`; los contracts de `*DeleteResult` ya están en Slice 1.

### 11.1 Slice 1 — Contratos, categorías y DeleteResult shape

1. **RED**: `tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs` (nuevo). Casos:
   - `Enum_HasSevenVariantsInOrder` enumerando `Enum.GetValues<ErrorCategoria>()` y assertando el orden 0..6.
   - `ContractsProject_HasNoProjectReferences_AndStaysLeaf` parseando el `.csproj`.
2. **RED**: `tests/SGV.Tests/Contracts/ErrorCategoriaMappersTests.cs` (nuevo):
   - **Round-trip nombre-a-nombre para cada enum**: por cada valor de `CargoSkillErrorType` (6), `ToCategoria` debe producir el `ErrorCategoria` con el mismo nombre semántico; por cada valor de `ErrorCategoria` (7), `ToTipo` debe producir el `CargoSkillErrorType` con el mismo nombre (o lanzar `NotSupportedException` si no hay equivalente). Tests análogos para `HabilidadErrorType` (4), `CargoErrorType` (3), `PuestoErrorType` (3), `UnidadOrganizativaErrorType` (3), `UsuarioErrorType` (4).
   - `CargoSkillErrorType_Validation_MapsToCategoriaValidation_NotConflict` (regression explícito del ordinal invertido).
3. **RED**: tests que los 6 `*Error` records exponen `Categoria`. Cada uno es un test que construye el record con `Categoria = X` y assertea el round-trip.
4. **RED**: tests que los 5 `*DeleteResult` exponen `Categoria` y `StatusCode` nullable. Cada uno cubre `Succeeded == true` (204 → `Categoria == default`) y `Succeeded == false` (con status no exitoso → `Categoria` poblado).
5. **GREEN**: crear `ErrorCategoria.cs`, `ErrorCategoriaMappers.cs`, agregar `Categoria` a los 6 records, agregar `Categoria` y `StatusCode: HttpStatusCode?` a los 5 `*DeleteResult`, marcar enums `[Obsolete]`.

### 11.2 Slice 2 — Mapper único

1. **RED**: `tests/SGV.Tests/Web/Common/CommandResultMapperTests.cs` con `[Theory]` parametrizada que itera cada fila de la matriz REQ-2 más 5 status atípicos (300, 418, 999, 226, 507).
2. **RED**: extender `HttpClientExceptionScenarios.TransportExceptionData` con una fila `["DnsFailure", () => new HttpRequestException("name resolution", new SocketException((int)SocketError.NameResolutionFailure)), typeof(HttpRequestException)]`.
3. **RED**: `TransportFailureClassifierTests::IsDnsFailure_NameResolutionFailure_ReturnsTrue` + `IsDnsFailure_NonSocketInner_ReturnsFalse` + `IsDnsFailure_NullInner_ReturnsFalse`.
4. **RED (F8)**: tests de cancelación cooperativa y timeout vs cancel externa — ver §11.5.
5. **GREEN**: crear `CommandResultMapper.cs` con la matriz de §5.4 y `IsDnsFailure` en `TransportFailureClassifier.cs`.
6. **GREEN**: migrar `HabilidadApiClient.ToCommandResultAsync`, `CargoApiClient.ToCommandResultAsync`, `PuestosApiClient.ToCommandResultAsync`, `UnidadOrganizativaApiClient.ToCommandResultAsync`, `CargoApiClient.ToSkillCommandResultAsync`, `CargoApiClient.MapSkillError` (eliminar) y `ReadSkillProblemAsync` (eliminar) → usar `CommandResultMapper.Map`.
7. **GREEN**: extender `HabilidadApiClientTests`, `CargoApiClientBasicTests`, `CargoSkillApiClientTests`, `PuestosApiClientTests`, `UnidadOrganizativaApiClientTests` con casos 401, 403, 5xx (500/502/503), 408. Reusar `RecordingHandler` para inyectar el status y ProblemDetails correspondiente.

### 11.3 Slice 3 — `IAuthSessionRedirector` + UI

1. **RED**: `tests/SGV.Tests/Web/Common/AuthSessionRedirectorTests.cs`:
   - `TryRedirectToLogin_NoHttpContext_ReturnsNull`
   - `TryRedirectToLogin_WithLocalPath_PreservesReturnUrl`
   - `TryRedirectToLogin_WithAbsoluteExternalUrl_DropsReturnUrl_RedirectsToLogin` (F9 — open-redirect guard)
   - `TryRedirectToLogin_WithProtocolRelativeUrl_DropsReturnUrl_RedirectsToLogin` (F9 — `//evil.example.com` rechazado)
   - `TryRedirectToLogin_WithLoopbackAbsoluteUrl_PreservesReturnUrl`
   - `TryRedirectToLogin_EmptyPath_OmitsReturnUrl`
2. **RED (F3)**: smoke test exhaustivo por PageModel con `WebApplicationFactory`: para cada uno de los **14 PageModels** identificados en §8.1, un test que enumera `Enum.GetValues<ErrorCategoria>()` y assertea que el switch cubre cada variante sin default silencioso (vía aserción de tipo sobre `IActionResult` retornado).
3. **RED**: smoke test de redirect: POST contra `Habilidades/Create` con mock 401 → `RedirectResult("/auth/sign-in?returnUrl=…")`, NO render del formulario.
4. **GREEN**: crear `IAuthSessionRedirector.cs` + `AuthSessionRedirector.cs`, registrar en DI, inyectar en los **14 PageModels** de §8.1.
5. **GREEN**: migrar los switches en los 14 PageModels a `switch (Categoria)` exhaustivo.

### 11.4 Slice 4 — `ApiResults` exhaustivo + DeleteResult tests

1. **RED**: extender `CargoSkillDeleteResultContractTests.cs` con asserts sobre `Categoria` y `StatusCode` nullable (los contracts ya vienen de Slice 1).
2. **RED**: tests análogos para `HabilidadDeleteResult`, `CargoDeleteResult`, `PuestoDeleteResult`, `UnidadOrganizativaDeleteResult`.
3. **RED**: extender `ApiResultsTests.cs` con `[Theory]` enumerando `ErrorCategoria` y exigiendo un `Status` ≥ 400 específico por categoría.
4. **GREEN**: en `ApiResults.cs`, agregar `MapCategoria(ErrorCategoria)` y migrar las firmas `Map*Status` a delegar (preservando compat con signatures existentes).

### 11.5 Cobertura de cancelación cooperativa y timeout (F8)

Tests RED explícitos en `Slice 2`:

- **Cancelación cooperativa con token pre-cancelado**: test parametrizado para los cinco clientes (`HabilidadApiClient`, `CargoApiClient`, `PuestosApiClient`, `UnidadOrganizativaApiClient`, `CargoApiClient.DeleteSkillAsync`) que invoca cada método con `new CancellationToken(canceled: true)` y assertea que `OperationCanceledException`/`TaskCanceledException` se propaga sin convertirse a `Categoria.Transport`. Nombre del test: `*ApiClient_*Method_PreCanceledToken_PropagatesOperationCanceledException`.
- **Timeout interno vs cancelación externa**: test parametrizado que distingue dos casos:
  1. **Timeout interno** (generado por `HttpClient.Timeout`): el cliente debe propagar la excepción nativa (`TaskCanceledException` con `InnerException = TimeoutException`).
  2. **Cancelación externa** (token del caller cancelado): el cliente debe propagar la `OperationCanceledException`/`TaskCanceledException` con `CancellationToken` linkeado.
  Nombre del test: `*ApiClient_*Method_HttpClientTimeoutVsCallerCancellation_PropagatesDistinctExceptions`.
- Los tests existentes `HabilidadApiClientTests.WhenHttpClientReturnsTimeout_PropagatesTaskCanceledException`, `CargoApiClientBasicTests.CancelationTokenPreCanceled_PropagatesOperationCanceledException`, `PuestosApiClientTests.WhenHttpClientTimeout_PropagatesTaskCanceledException`, `UnidadOrganizativaApiClientTests.PreCanceledToken_PropagatesOperationCanceledException` (todos pre-existentes) ya cubren los casos básicos; los nuevos tests RED extienden la cobertura a los cinco clientes y separan explícitamente timeout-interno de cancel-externa.

## 12. Plan de entrega detallado

### Forecast por slice (metodología: conteo manual de archivos × líneas estimadas)

Metodología: cada archivo nuevo cuenta como Adiciones totales; cada archivo modificado se estima en función del cambio (Adiciones + Modificaciones); Eliminaciones cuentan como negativos. Total = suma neta.

| Slice | Archivos | Adiciones | Modificaciones | Eliminaciones | Subtotal |
|---|---|---|---|---|---|
| 1 — `ErrorCategoria` + 6 `*Error` + 5 `*DeleteResult` + `ErrorCategoriaMappers` + 9 contract tests | 2 nuevos, 11 modificados | ~210 | ~140 | 0 | ~350 |
| 2 — `CommandResultMapper` + `IsDnsFailure` + 5 clientes Web + cancel tests | 1 nuevo, 9 modificados | ~360 | ~220 | ~50 | ~630 |
| 3 — `IAuthSessionRedirector` + 14 PageModels + open-redirect guards + smoke exhaustivo | 2 nuevos, 14 modificados | ~280 | ~300 | ~20 | ~600 |
| 4 — `ApiResults.MapCategoria` + tests exhaustivos DeleteResult/ApiResults | 0 nuevos, 7 modificados | ~120 | ~150 | 0 | ~270 |
| **Total** | **5 nuevos, 41 modificados** | **~970** | **~810** | **~70** | **~1850** |

**Reconciliation con proposal §8**: el proposal original declaró ~2130 (producción ~1430 + tests ~700); el design anterior declaró ~1600 (producción ~900 + tests ~700). El nuevo forecast unificado es **~1850** (producción ~1110 + tests ~740). Las diferencias se explican por:
- **+250** (producción): 14 PageModels (no 12) + `ErrorCategoriaMappers.cs` no contemplado + 5 nuevas filas de tests en Slice 2 (cancel/timeout).
- **−100** (producción): forecast anterior sobrestimaba el refactor de cada cliente (~30 líneas vs ~50 reales medidas).
- **+50** (tests): exhaustividad por PageModel (F3) + round-trip mappers (F1) + open-redirect (F9).

`tasks.md` debe usar este único número (~1850). Si en la fase de tasks se ajusta, se documenta la justificación.

- **Decision needed before apply**: Yes (definir si Slice 4 entra en este PR o se difiere a follow-up).
- **Chained PRs recommended**: Yes.
- **400-line budget risk**: **High** si single-PR; **Low-Medium** si chained de ~400–600 por slice.
- **Delivery strategy cacheada**: `auto-forecast` → requiere `auto-chain` con 4 PRs chained.

### Decisión de chain strategy (sugerencia, no definitiva)

**Feature Branch Chain** (PR stacked sobre la rama del PR anterior). Razones:
1. **Dependencias secuenciales**: Slice 2 depende de Slice 1, Slice 3 depende de Slice 2, Slice 4 depende de Slice 1 y 2.
2. **Tamaño por slice**: cada uno cae en 270–630 líneas, dentro del budget razonable.
3. **Reversibilidad**: si Slice 3 (UI) tiene problemas de UX, se puede hacer revert del merge del branch sin tocar Slices 1-2 ya mergeados.

El orquestador debe confirmar esta sugerencia con el usuario antes de invocar `sdd-tasks`.

### Detalle por slice

**Slice 1** (~350 líneas) — `ErrorCategoria` + 6 `*Error` + 5 `*DeleteResult` + mappers
- Archivos: `src/SGV.Contracts/Comun/ErrorCategoria.cs` (nuevo), `ErrorCategoriaMappers.cs` (nuevo), 6 `*CommandResult.cs` modificados, 5 `*DeleteResult.cs` modificados, `tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs` (nuevo), `ErrorCategoriaMappersTests.cs` (nuevo), extensiones en 5 `*DeleteResultContractTests.cs`.
- Verificación: `dotnet test --filter ErrorCategoriaTests|ErrorCategoriaMappersTests|DeleteResultContractTests` + `dotnet build SGV.slnx` sin warnings nuevos.

**Slice 2** (~630 líneas) — Mapper HTTP único + cancel/timeout
- Archivos: `src/SGV.Web/Integration/Common/CommandResultMapper.cs` (nuevo), `TransportFailureClassifier.cs` (modificado, `+IsDnsFailure`), 5 clientes Web (modificados: `HabilidadApiClient`, `CargoApiClient` × 2, `PuestosApiClient`, `UnidadOrganizativaApiClient`), `tests/SGV.Tests/Web/Common/CommandResultMapperTests.cs` (nuevo), `TransportFailureClassifierTests.cs` (extendido), `HttpClientExceptionScenarios.cs` (extendido), 5 archivos `*ApiClientTests.cs` (extendidos con 401/403/5xx/408/DNS/pre-canceled/timeout-vs-cancel).
- Verificación: `dotnet test --filter CommandResultMapperTests|HabilidadApiClientTests|CargoApiClientBasicTests|CargoSkillApiClientTests|PuestosApiClientTests|UnidadOrganizativaApiClientTests`.

**Slice 3** (~600 líneas) — `IAuthSessionRedirector` + UI en 14 PageModels + open-redirect
- Archivos: `src/SGV.Web/Integration/Common/IAuthSessionRedirector.cs` (nuevo), `AuthSessionRedirector.cs` (nuevo), `Program.cs` (+5 líneas), **14** `*.cshtml.cs` PageModels modificados, `tests/SGV.Tests/Web/Common/AuthSessionRedirectorTests.cs` (nuevo, incluye 6 casos), 14 smoke tests exhaustivos por PageModel.
- Verificación: `dotnet test --filter AuthSessionRedirectorTests` + smoke tests + build verde.

**Slice 4** (~270 líneas) — `ApiResults` exhaustivo + DeleteResult tests finales
- Archivos: `src/SGV.Api/Infrastructure/Results/ApiResults.cs` (modificado, +`MapCategoria`), 5 `*DeleteResultContractTests.cs` (extensiones finales).
- Verificación: `dotnet test --filter ApiResultsTests|DeleteResultContractTests`.

## 13. Matriz de riesgos del diseño

| Riesgo | Severidad | Mitigación |
|---|---|---|
| Compatibilidad ordinal `ErrorCategoria` ↔ enums vigentes | HIGH | Mapeo por nombre con `ErrorCategoriaMappers` (round-trip tests en Slice 1). Cast `(int)` prohibido por convención, detectado por code review. |
| Switches en PageModel degradan silenciosamente categorías nuevas (sin `TreatWarningsAsErrors`) | HIGH | Tests exhaustivos por PageModel (F3): cada uno enumera las 7 variantes y assertea que el switch cubre cada una sin default. Slice 3 agrega 14 smoke tests parametrizados. |
| Open-redirect en `TryRedirectToLogin(returnUrl)` | MED | Guard `IsLocalUrl` en `AuthSessionRedirector` (§6.1); tests RED explícitos para URLs absolutas, protocol-relative, loopback. |
| Chained PR divergence entre slices | HIGH | Feature-branch-chain donde PR #2 apunta a la rama del PR #1, etc. Documentado en `tasks.md`. |
| Source-breaking de los 6 enums sin coordinación | MED | `[Obsolete]` con `error: false` durante el ciclo; eliminado al archivar. |
| Ordinales de `CargoSkillErrorType`: si alguien reordena | MED | Comentario XML explícito "do not reorder" + `ErrorCategoriaTests.OrdinalIsFixedAtTransport=5`. |
| Default silencioso en PageModel para categorías nuevas | MED | `SwitchExpressionException` o `throw new ArgumentOutOfRangeException(...)` en cada switch exhaustivo de PageModel. |
| Cambio en `HabilidadError.StatusCode` rompe logging | LOW | Mantener el campo, agregar `StatusCode` opcional en los otros 4 records. No eliminar campos existentes. |
| Cambio en comportamiento de redirect a login (POST pierde form data) | LOW | Documentado en §6.1. Tests existentes preservan el comportamiento vigente. |
| Persona/Ocupacion/PersonaSkill quedan fuera | LOW | Documentado como follow-up en `decisiones-implementacion.md`. |
| `PuestoDeleteResult.StatusCode` pasa de `HttpStatusCode` a `HttpStatusCode?` | LOW | Cambio source-compatible para la mayoría de los call sites. `Puestos/Index.cshtml.cs` se actualiza explícitamente. |
| Cuerpo de ProblemDetails atípico (HTML en 5xx) | LOW | `ApiProblemReader` absorbe `JsonException` y devuelve `Title/Detail null`. `CommandResultMapper.Map` cae en defaults. |

## 14. Cumplimiento de contratos vigentes

### 14.1 `web-apiclient-transport-contract` (vigente + delta)

- **Propagar fallos nativos de transporte** → PRESERVADO. `CommandResultMapper.Map` opera sólo sobre `HttpResponseMessage`. Los clientes siguen propagando `HttpRequestException`/`TaskCanceledException` nativas.
- **Respetar cancelación cooperativa del consumidor** → PRESERVADO. Ningún cliente cambia el comportamiento de token pre-cancelado. Cobertura explícita de timeout-interno vs cancel-externa en §11.5.
- **IPuestosApiClient propaga fallos nativos de transporte** → PRESERVADO.
- **IPuestosApiClient respeta cancelación cooperativa** → PRESERVADO.
- **IPuestosApiClient traduce ProblemDetails a resultados tipados** → PRESERVADO. La traducción usa ahora `CommandResultMapper.Map` (delta explícito en `openspec/changes/.../specs/web-apiclient-transport-contract/spec.md`). Los códigos `CodigoDuplicado`, `UnidadOrganizativaNoExiste`, `CargoNoExiste`, `PuestoSuperiorNoExiste`, `PuestoSuperiorInvalido` se preservan verbatim porque `parsed.Title ?? defaults` los respeta.

### 14.2 Specs de dominio

- El cambio es de superficie interna de los `*CommandResult`. Los behavior contracts (CRUD, validaciones, listados `activas|eliminadas`, reactivación por PRG, autorización `Administrador`, soft delete) no se ven afectados. NO se crean deltas sobre `cargo-management`, `puesto-management`, `habilidad-management`, `unidad-organizativa-crud`, `identity-user-role-management`.

### 14.3 Capability `commandresult-error-taxonomy`

La capability ya tiene spec base en `openspec/specs/commandresult-error-taxonomy/spec.md` (creada durante este change, antes de la fase archive). El delta en `openspec/changes/2026-07-13-taxonomia-errores-commandresult/specs/commandresult-error-taxonomy/spec.md` replica el contenido de la spec base con el `## ADDED Requirements` block; en el archive, la spec base absorbe el contenido y el delta queda vacío. Redacción coherente con la convención OpenSpec: el delta sólo agrega; la spec base contiene la especificación estable.

### 14.4 Decisiones del repo (`docs/decisiones-implementacion.md`)

- **Proveedor MySQL único** → no afectado.
- **Soft delete con columnas generadas** → no afectado.
- **Identity string keys** → no afectado.
- **Auditoría** → no afectada (justificación en §10).
- **Default-deny autorización API** → no afectada.
- **Cookie/CORS por ambiente** → no afectada.
- **Paralelismo de tests** → no afectado. Las nuevas filas `DnsFailure`, `PreCanceledToken`, `TimeoutVsCancellation` suman tres filas a `TransportExceptionData`; el paralelismo `maxParallelThreads: 4` sigue siendo suficiente.

## 15. Verificación de `SGV.Contracts` como leaf

Confirmado en `src/SGV.Contracts/SGV.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.14.0" />
  </ItemGroup>
</Project>
```

No contiene `ProjectReference`. `ErrorCategoria` y `ErrorCategoriaMappers` pueden vivir ahí sin invertir el grafo `Dominio ← Aplicacion ← Contracts ← {Api, Web}`. Test RED explícito en §11.1 (`ContractsProject_HasNoProjectReferences_AndStaysLeaf`) blinda la invariante.