# R-03-10 — Taxonomía de errores (CommandResult / ErrorCategoria)

Referencia de la taxonomía unificada de errores que atraviesan `SGV.Api` (HTTP) y `SGV.Web` (clientes tipados). La fuente de verdad es `SGV.Contracts.Comun.ErrorCategoria`; los enums legacy `*ErrorType` se traducen vía `ErrorCategoriaMappers` y se mapean a HTTP status con `ApiResults`.

## `ErrorCategoria` (enum)

`SGV.Contracts.Comun.ErrorCategoria`. **Append-only**: no reordenar ni reasignar ordinales.

| Ordinal | Variante | HTTP status típico | Notas |
| --- | --- | --- | --- |
| 0 | `NotFound` | 404 | Recurso inexistente |
| 1 | `Conflict` | 409 | Conflicto de unicidad/estado |
| 2 | `Validation` | 400/422 | Datos inválidos; `FieldErrors` opcional |
| 3 | `Unauthorized` | 401 | Sesión ausente o credencial inválida |
| 4 | `Forbidden` | 403 | Autenticado sin permiso |
| 5 | `Transport` | 408/500/502/503/504 | Falla de transporte o 5xx |
| 6 | `Unexpected` | Otros no 2xx | Incluye 3xx/1xx |

## Matriz `ErrorCategoria` → HTTP status (API)

Definida en `ApiResults.MapCategoria` (`src/SGV.Api/Infrastructure/Results/ApiResults.cs`).

| Categoría | Status |
| --- | --- |
| `Validation` | 400 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `Transport` | 503 |
| `Unexpected` | 500 |

> ⚠️ A verificar: el `MapCategoria` del API mapea `Transport` a `503`. En el `CommandResultMapper` del Web, `Transport` se asigna para status `408/500/502/503/504`. La asimetría es intencional: el API siempre responde 503 cuando declara la categoría como transport; el Web acepta un set más amplio porque los códigos reales que recibe de la API pueden incluir 500/502/504.

## Wire shape — `ProblemDetails` vs `ValidationProblemDetails`

`ApiResults` produce uno de dos shapes según haya `FieldErrors`:

| Shape | `Status` | `Title` | `Detail` | `errors` | Extensión |
| --- | --- | --- | --- | --- | --- |
| `ProblemDetails` | código HTTP | `Code` (string) del error | `Message` del error | n/a | `traceId` |
| `ValidationProblemDetails` | 400 | `Code` del error | `Message` del error | `Dictionary<string, string[]>` poblado o vacío | `traceId` |

`traceId` se calcula con `Activity.Current?.Id ?? HttpContext.TraceIdentifier`. El header `Retry-After` lo agrega `RateLimiter.OnRejected`, no `ApiResults`.

## Overloads de `ApiResults.ToProblemResult`

| Error tipado | Categoría resuelta vía | Mapeo status |
| --- | --- | --- |
| `CargoError` | `CargoError.Categoria` o `ErrorCategoriaMappers.ToCategoria(CargoErrorType)` | Ver matriz |
| `CargoSkillError` | análogo | idem |
| `HabilidadError` | análogo | idem |
| `PuestoError` | análogo | idem |
| `UnidadOrganizativaError` | análogo | idem |
| `OcupacionError` | `OcupacionError.Categoria` directo | idem |
| `VacanteError` | `VacanteError.Categoria` directo | idem |
| `PersonaError` | `ErrorCategoriaMappers.ToCategoria(PersonaErrorType)` | idem |
| `PersonaSkillError` | análogo con fallback legacy `Type` | idem |
| `UsuarioError` | análogo con fallback legacy `Type` | idem |
| `string code + string detail + IReadOnlyDictionary<...>?` | n/a (caller provee) | 400 si `ValidationProblemDetails` |

Cuando el error tiene `Categoria=Unexpected` y `StatusCode=null`, el mapper cae al mapper por `Type` legacy. Esto preserva la matriz histórica en tests pre-`#102`.

## Mappers legacy (`ErrorCategoriaMappers`)

Switch expressions exhaustivos nombre-a-nombre (NO por ordinal) entre cada enum `*ErrorType` legacy y `ErrorCategoria`. Los enums legacy están marcados `[Obsolete]` durante la capability `commandresult-error-taxonomy` y serán eliminados al archivar.

| Legacy enum | Variantes legacy | Mapeo a `ErrorCategoria` |
| --- | --- | --- |
| `CargoErrorType` | `NotFound`, `Conflict`, `Validation`, `Unauthorized`, `Forbidden`, `Transport`, `Unexpected` | 1-a-1 |
| `CargoSkillErrorType` | `NotFound`, `Validation`, `Conflict`, `Unauthorized`, `Forbidden`, `Transport` | 1-a-1; sin `Unexpected` |
| `HabilidadErrorType` | `NotFound`, `Conflict`, `Validation`, `Infrastructure` | `Infrastructure → Transport` |
| `PuestoErrorType` | `NotFound`, `Conflict`, `Validation` | sólo esas 3; sin `Unauthorized`/`Forbidden`/`Transport`/`Unexpected` |
| `UnidadOrganizativaErrorType` | idem | idem |
| `PersonaErrorType` | 1-a-1 con todas | — |
| `PersonaSkillErrorType` | `NotFound`, `Validation` | sólo esas 2 |
| `UsuarioErrorType` | `NotFound`, `Conflict`, `Validation`, `Unauthorized` | sin `Forbidden`/`Transport`/`Unexpected` |

## `ApiResults` y bifurcación `ProblemDetails` / `ValidationProblemDetails`

Patrón vigente (issue #102):

```csharp
if (result.FieldErrors is { Count: > 0 })
    return ApiResults.ToValidationProblemResult(result.Error!, result.FieldErrors, HttpContext);
return ApiResults.ToProblemResult(result.Error!, HttpContext);
```

Aplicado en controllers: `CargosController`, `CargoSkill` (subrecurso), `HabilidadesController`, `PersonasController`, `PuestosController`, `UnidadesOrganizativasController`, `OcupacionesController`, `VacantesController`, `UsuariosController`, `AuthController` (válidos para `ForgotPassword`/`ResetPassword`/`ChangePassword`/`ValidateResetToken`).

Para el subrecurso `CargoSkill` el shape histórico exigía `ValidationProblemDetails` aunque no haya `FieldErrors`: el controller rama explícitamente cuando `Error.Type == Validation && FieldErrors.Count > 0`; en otro caso, `ProblemDetails` para preservar la forma del wire pre-issue-#102.

## `AuthController` y `SetupController`

`AuthController` (excepto `Refresh`) corre `FluentValidation` sobre el request y, si falla, agrega errores a `ModelState` antes de devolver `ValidationProblem(ModelState)`. `SetupController` mantiene su propio switch sobre `SetupErrorCode` con mapeo:

| `SetupErrorCode` | HTTP |
| --- | --- |
| `SetupYaCompletado`, `UserNameDuplicado`, `EmailDuplicado`, `LegajoDuplicado`, `DocumentoDuplicado`, `PersonaConUsuario` | 409 |
| `EmailInvalido`, `UserNameInvalido`, `PasswordDebil`, `ValidacionIdentity`, `DatosInvalidos` | 400 |
| `TransaccionFallida` | 500 |
| `error.StatusCode` (si está poblado) tiene precedencia | idem |

## Clientes tipados (Web) — `CommandResultMapper`

`src/SGV.Web/Integration/Common/CommandResultMapper.cs` traduce una respuesta HTTP no exitosa (con `ApiProblemReader.Result` ya parseado) a la tupla `(ErrorCategoria, Code, Message, StatusCode)`.

| HTTP status | Categoría | Default `Code` | Default `Message` |
| --- | --- | --- | --- |
| 400 / 422 | `Validation` | `BadRequest` | `Solicitud inválida.` |
| 401 | `Unauthorized` | `Unauthorized` | `Su sesión expiró. Vuelva a iniciar sesión.` |
| 403 | `Forbidden` | `Forbidden` | `Acceso denegado.` |
| 404 | `NotFound` | `NotFound` | `Recurso no encontrado.` |
| 408 | `Transport` | `TransportError` | `El servicio no respondió correctamente. Intentá nuevamente.` |
| 409 | `Conflict` | `Conflict` | `Conflicto.` |
| 500, 502, 503, 504 | `Transport` | `TransportError` | idem |
| resto | `Unexpected` | `Unexpected` | `Respuesta inesperada del servidor.` |

`ApiProblemReader.Result.Title`/`Detail` tienen precedencia cuando vienen poblados (los emite el API). `CommandResultMapper` sólo opera sobre `HttpResponseMessage`; NO captura `HttpRequestException`/`TaskCanceledException`. Esas excepciones se propagan al `PageModel`, que las clasifica con `TransportFailureClassifier.IsDnsFailure` para discriminar DNS-failures de timeouts.

## `DeleteResultMapper`

Variante de `CommandResultMapper` para endpoints de soft-delete / delete físico. Misma matriz, pero aplicado al `Delete*Result` que viven como records (`CargoSkillDeleteResult`, `PersonaSkillDeleteResult`, `PersonaDeleteResult`).

## `TransportFailureClassifier`

Discrimina fallas de transporte recuperables:

| Falla | Categoría efectiva | Uso en UI |
| --- | --- | --- |
| `HttpRequestException` con `SocketException`/`DnsError` | `Transport` con flag DNS | Mensaje "verificá la conexión" |
| `TaskCanceledException` con `clientCancelled=true` | `Unexpected` | Sin banner |
| `TaskCanceledException` con `clientCancelled=false` | `Transport` (timeout) | Banner genérico |

## `AuthSessionRedirector`

Traduce `ErrorCategoria.Unauthorized` (proveniente de un cliente tipado del Web) a una redirección a `/auth/sign-in` con guard anti open-redirect: sólo redirige a paths internos (`/`-prefixed, no `//`-prefixed).

## Referencias

- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- Tutorial: [Primera mutación de unidad organizativa](../tutorials/02-primera-mutacion-unidad-organizativa.md)
- How-to: [Operar flujo de recuperación de contraseña](../how-to/02-operar-flujo-recuperacion-contrasena.md)
- R-03-01 — Mapa de APIs HTTP (códigos por endpoint)
- R-03-06 — Pipeline middleware API (cómo `ApiResults` aplica `traceId`)
- R-03-07 — Pipeline arranque Web (cómo `CommandResultMapper` se invoca desde los clientes tipados)
