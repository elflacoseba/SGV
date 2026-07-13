# Especificación de taxonomía de errores para `CommandResult` y clientes HTTP

## Purpose

Definir una única taxonomía de categorías de error compartida por todos los
`*CommandResult` y `*DeleteResult` de `SGV.Contracts`, junto al helper único
`CommandResultMapper` que traduce respuestas HTTP a esa taxonomía, de modo que
los clientes HTTP tipados de `SGV.Web` y sus `PageModel` puedan razonar sobre
los fallos desde un único lugar.

## Requirements

### REQ-1 — Enum común `ErrorCategoria`

El sistema MUST exponer `public enum ErrorCategoria` en
`src/SGV.Contracts/Comun/ErrorCategoria.cs` con siete variantes y este orden:

| # | Variante | Significado |
|---|----------|-------------|
| 0 | `NotFound` | Recurso inexistente (HTTP 404). |
| 1 | `Conflict` | Conflicto de unicidad/estado (HTTP 409). |
| 2 | `Validation` | Datos inválidos (HTTP 400/422), con `FieldErrors` opcional. |
| 3 | `Unauthorized` | Sesión ausente o credencial inválida (HTTP 401). |
| 4 | `Forbidden` | Autenticado sin permiso (HTTP 403). |
| 5 | `Transport` | Falla de transporte o 5xx del backend. |
| 6 | `Unexpected` | Cualquier otro status no exitoso. |

`SGV.Contracts` MUST seguir siendo leaf: el enum no puede importar de
ningún otro proyecto del grafo.

#### Scenario: Enum expone siete variantes con ordinales fijos

- GIVEN el assembly `SGV.Contracts` compilado
- WHEN un test enumera `Enum.GetValues<ErrorCategoria>()`
- THEN MUST obtener exactamente 7 valores en el orden 0..6 definido arriba.

#### Scenario: `SGV.Contracts` permanece leaf

- GIVEN el `.csproj` de `SGV.Contracts`
- WHEN se inspeccionan sus `ProjectReference`
- THEN MUST no tener ninguna referencia a otros proyectos del grafo.

### REQ-2 — Matriz HTTP→`ErrorCategoria`

El sistema MUST traducir cada `HttpResponseMessage` no exitoso a la categoría
mediante la siguiente matriz:

| Status HTTP | `ErrorCategoria` |
|-------------|------------------|
| 400, 422 | `Validation` |
| 401 | `Unauthorized` |
| 403 | `Forbidden` |
| 404 | `NotFound` |
| 408, 500, 502, 503, 504 | `Transport` |
| 409 | `Conflict` |
| Otro no 2xx (incluye 3xx) | `Unexpected` |

`FieldErrors` MUST preservarse cuando el body es un
`ValidationProblemDetails`; `StatusCode` MUST preservarse como metadata de
diagnóstico en todos los casos.

#### Scenario: 401 se mapea a `Unauthorized` con status preservado

- GIVEN un `HttpResponseMessage` con status 401 y `ProblemDetails` válido
- WHEN se aplica la matriz
- THEN la categoría MUST ser `Unauthorized`
- AND el `StatusCode` MUST ser `401`.

#### Scenario: Status atípico cae en `Unexpected` sin perder status

- GIVEN un `HttpResponseMessage` con status 418
- WHEN se aplica la matriz
- THEN la categoría MUST ser `Unexpected`
- AND el `StatusCode` MUST ser `418`.

### REQ-3 — Errores de dominio exponen `Categoria`

Cada record de error de los seis `*CommandResult` (`HabilidadError`,
`CargoError`, `PuestoError`, `UnidadOrganizativaError`, `CargoSkillError`,
`UsuarioError`) MUST exponer una propiedad `Categoria: ErrorCategoria`
obtenida del mapper. La identidad por dominio (`Code`, `Message`,
`FieldErrors` cuando aplique) MUST preservarse verbatim.

#### Scenario: `HabilidadError` expone `Categoria`

- GIVEN un `HabilidadCommandResult.Failure`
- WHEN se inspecciona su `Error`
- THEN MUST tener `Categoria` ∈ {`NotFound`, `Conflict`, `Validation`,
  `Unauthorized`, `Forbidden`, `Transport`, `Unexpected`}
- AND `Code`/`Message` MUST coincidir con el `ProblemDetails` del backend
  o con el default documentado del mapper.

### REQ-4 — `CommandResultMapper` único en `SGV.Web`

`SGV.Web` MUST exponer un helper estático
`CommandResultMapper.Map(HttpResponseMessage, ApiProblemReader.Result)` en
`src/SGV.Web/Integration/Common/` que devuelve
`(ErrorCategoria, string Code, string Message, int? StatusCode)`. Los cuatro
clientes administrativos (`HabilidadApiClient`, `CargoApiClient` para Cargo
y CargoSkill, `PuestosApiClient`, `UnidadOrganizativaApiClient`) MUST
delegar a este helper en lugar de mantener su propia matriz.

#### Scenario: Helper centraliza la matriz

- GIVEN un `HttpResponseMessage` con status 403
- WHEN se invoca `CommandResultMapper.Map`
- THEN MUST devolver `(Forbidden, "Forbidden", "Acceso denegado.", 403)`.

#### Scenario: Cliente usa el helper

- GIVEN un test con `HttpMessageHandler` mockeado que responde 403
- WHEN `HabilidadApiClient.UpdateAsync` procesa la respuesta
- THEN el `HabilidadCommandResult.Failure` resultante MUST tener
  `Categoria == ErrorCategoria.Forbidden`.

### REQ-5 — `IAuthSessionRedirector` para `Unauthorized`

`SGV.Web` MUST exponer `IAuthSessionRedirector.TryRedirectToLogin(returnUrl)`
cuya implementación por defecto emite
`Redirect("/auth/sign-in?returnUrl=...")` cuando existe `HttpContext` y el
`returnUrl` supera el guard `IsLocalUrl` (rechaza URLs absolutas externas y
protocol-relative `//host/path`). Si el `returnUrl` no es local, el redirect
se emite sin query string para mitigar open-redirect. Los `PageModel`
de recursos protegidos MUST invocarlo antes de mostrar mensaje inline
cuando el resultado trae `Categoria == Unauthorized`.

#### Scenario: `Unauthorized` redirige

- GIVEN un `PageModel` con `IAuthSessionRedirector` inyectado y un resultado
  `*CommandResult.Failure(Categoria = Unauthorized)`
- WHEN se procesa el resultado en `OnPostAsync`
- THEN el `PageModel` MUST invocar `TryRedirectToLogin`
- AND MUST NO renderizar el formulario con mensaje inline.

### REQ-6 — `ApiResults` exhaustivo por categoría

`SGV.Api/Infrastructure/Results/ApiResults.cs` MUST mapear cada
`ErrorCategoria` a un status HTTP coherente: `Validation→400`,
`NotFound→404`, `Conflict→409`, `Unauthorized→401`, `Forbidden→403`,
`Transport→503`, `Unexpected→500`. Agregar una nueva categoría sin rama
debe romper tests, no degradar silenciosamente.

#### Scenario: Categoría `Transport` produce 503

- GIVEN un `HabilidadError` con `Categoria == Transport`
- WHEN `ApiResults.ToProblemResult(error)` se invoca
- THEN el `ObjectResult` MUST tener `StatusCode == 503`.

### REQ-7 — `*DeleteResult` exponen `Categoria`

Cada `*DeleteResult` (`HabilidadDeleteResult`, `CargoDeleteResult`,
`PuestoDeleteResult`, `UnidadOrganizativaDeleteResult`,
`CargoSkillDeleteResult`) MUST exponer `Categoria: ErrorCategoria` y
preservar `StatusCode` como metadata. `Succeeded` MUST ser `true` solo
para 204.

#### Scenario: Delete 409 expone `Categoria=Conflict`

- GIVEN un `HabilidadApiClient.DeleteAsync` con backend respondiendo 409
- WHEN se obtiene el `HabilidadDeleteResult`
- THEN MUST tener `Succeeded == false`
- AND `Categoria == ErrorCategoria.Conflict`
- AND `StatusCode == 409`.

### REQ-8 — Preservación de contrato de transporte

`CommandResultMapper` y los clientes HTTP MUST respetar
`web-apiclient-transport-contract`: `HttpRequestException` y
`TaskCanceledException` se propagan nativas, no se convierten a
`CommandResult.Transport`. `AuthApiClient.LoginAsync` MUST mantener su
semántica "401→null" y NO usar el mapper común.

#### Scenario: `HttpRequestException` se propaga sin conversión

- GIVEN un cliente HTTP con un `HttpMessageHandler` que lanza
  `HttpRequestException` por DNS
- WHEN invoca `CreateAsync`
- THEN la excepción MUST propagarse al consumidor.

#### Scenario: `LoginAsync` 401 retorna `null`

- GIVEN `AuthApiClient.LoginAsync` y un backend que responde 401
- WHEN se procesa la respuesta
- THEN MUST retornar `null` (credenciales inválidas).

### REQ-9 — Cobertura de tests por cliente

`tests/SGV.Tests/` MUST cubrir para los cinco clientes administrativos
(`Habilidad`, `Cargo`, `CargoSkill`, `Puesto`, `UnidadOrganizativa`):
401, 403, al menos 5xx (500, 502, 503), 408, timeout, cancelación
cooperativa y DNS explícito. El helper MUST tener un `[Theory]` que
exhiba la matriz completa REQ-2.

#### Scenario: `CommandResultMapperTests` cubre toda la matriz

- GIVEN la suite `CommandResultMapperTests`
- WHEN se ejecuta
- THEN MUST haber al menos un `[InlineData]` por cada fila de la matriz
  REQ-2 + cinco status atípicos.
