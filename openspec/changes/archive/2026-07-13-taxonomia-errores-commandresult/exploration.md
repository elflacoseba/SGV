## Exploración: Taxonomía de errores consistente para `CommandResult` y clientes HTTP de Web

### Estado actual

La issue #125 está confirmada contra `develop`. No existe hoy `ErrorCategoria` ni `ErrorCategory` en `SGV.Contracts`, y `src/SGV.Contracts/` tampoco tiene un directorio `Comun/`. La solución usa varias taxonomías paralelas para expresar el mismo fallo HTTP. Además, `TransportFailureClassifier` ya centraliza parte de la clasificación de excepciones, pero no clasifica respuestas HTTP y su adopción está limitada a algunos `PageModel`.

#### 1. Inventario de `CommandResult` en `SGV.Contracts`

| Resultado | Archivo | Categorías actuales | `FieldErrors` | Estado HTTP en el error | Observación |
|---|---|---|---|---|---|
| `HabilidadCommandResult` | `src/SGV.Contracts/Habilidades/Comandos/HabilidadCommandResult.cs` | `NotFound`, `Conflict`, `Validation`, `Infrastructure` | Sí | `HabilidadError.StatusCode: int?` | Es el único resultado principal que separa infraestructura y preserva el status. Agrupa 401, 403, 408, 5xx y cualquier otro status no contemplado como `Infrastructure`. |
| `CargoCommandResult` | `src/SGV.Contracts/Organizacion/Comandos/CargoCommandResult.cs` | `NotFound`, `Conflict`, `Validation` | Sí | No | El cliente Web convierte 401, 403, 5xx y otros status en `Validation` con code `Unexpected`. |
| `PuestoCommandResult` | `src/SGV.Contracts/Organizacion/Comandos/PuestoCommandResult.cs` | `NotFound`, `Conflict`, `Validation` | Sí | No | Tiene el mismo fallback `Validation/Unexpected` que Cargo. |
| `UnidadOrganizativaCommandResult` | `src/SGV.Contracts/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` | `NotFound`, `Conflict`, `Validation` | Sí | No | Preserva `ProblemDetails.Title/Detail` en el fallback, pero sigue categorizándolo como `Validation`. |
| `CargoSkillCommandResult` | `src/SGV.Contracts/Organizacion/Comandos/CargoSkillCommandResult.cs` | `NotFound`, `Validation`, `Conflict`, `Unauthorized`, `Forbidden`, `Transport` | Sí | No | Es el más cercano a la taxonomía objetivo. No tiene `Unexpected`: el fallback se representa como `Validation` con code `Unexpected`. Sus comentarios fijan ordinales append-only. |
| `UsuarioCommandResult` | `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | `NotFound`, `Conflict`, `Validation`, `Unauthorized` | No | No | Es un `CommandResult` de Contracts, pero no vive en un archivo `*CommandResult.cs`; debe incluirse al definir “todos”. |

No hay un resultado que cubra simultáneamente `Unauthorized`, `Forbidden`, `Transport` y `Unexpected`. `CargoSkillCommandResult` cubre los tres primeros; `HabilidadCommandResult` cubre infraestructura con status pero no distingue autenticación/autorización; los demás colapsan esos casos en validación.

Fuera de `SGV.Contracts` existen tres resultados adicionales en Aplicación: `PersonaCommandResult` (`NotFound/Conflict/Validation`), `PersonaSkillCommandResult` (`NotFound/Validation`) y `OcupacionCommandResult` (`NotFound/Conflict/Validation`). Como `SGV.Aplicacion` ya depende de `SGV.Contracts`, podrían adoptar una categoría común sin invertir el grafo, pero la propuesta debe declarar explícitamente si “todos los CommandResult” también los incluye.

#### 2. Clasificación actual en clientes HTTP

| Cliente / operación | Forma actual del mapeo |
|---|---|
| `HabilidadApiClient` — comandos | `400→Validation`, `404→NotFound`, `409→Conflict`; cualquier otro status, incluidos 401/403/408/5xx/3xx, se registra y devuelve como `Infrastructure/ServerError` con `StatusCode`. |
| `CargoApiClient` — comandos de Cargo | `400→Validation`, `404→NotFound`, `409→Conflict`; todo lo demás devuelve `Validation/Unexpected`. |
| `CargoApiClient` — `CargoSkill` | `MapSkillError` clasifica `400`, `404`, `401`, `403`, `409`, `>=500`; el resto cae en `Validation/Unexpected`. Es una tercera taxonomía privada dentro del mismo cliente. |
| `PuestosApiClient` — comandos | Espejo de Cargo: `400/404/409` tipados y fallback `Validation/Unexpected`. |
| `UnidadOrganizativaApiClient` — comandos | `400/404/409` tipados; otros status caen en `Validation`, preservando `ProblemDetails` o usando `Unexpected`. |
| `AuthApiClient` | `401` se traduce a `null` porque en login significa credenciales inválidas; el resto pasa por `EnsureSuccessStatusCode`. Es un caso de uso distinto y no debería forzarse dentro de la taxonomía de comandos administrativos. |
| Operaciones de consulta | En general usan `EnsureSuccessStatusCode`, por lo que 401/403/5xx se propagan como `HttpRequestException`; `404` se trata como `null` o colección vacía en algunos endpoints. |
| Operaciones de baja | `CargoDeleteResult`, `PuestoDeleteResult`, `HabilidadDeleteResult`, `UnidadOrganizativaDeleteResult` y `CargoSkillDeleteResult` exponen `StatusCode/Code/Message`; las páginas comparan directamente 404/409 y usan un fallback genérico. Son una taxonomía paralela basada en status crudo. |

`ApiProblemReader` ya resolvió la duplicación del parseo `ProblemDetails`/`ValidationProblemDetails`, pero cada cliente todavía repite la matriz status→categoría y los defaults de code/message.

#### 3. `TransportFailureClassifier`

`src/SGV.Web/Integration/Common/TransportFailureClassifier.cs` clasifica excepciones, no respuestas HTTP:

- `HttpRequestException`: sí; incluye normalmente conectividad y DNS, aunque no existe un test DNS explícito.
- `TaskCanceledException`: sí; cubre el shape habitual de timeout de `HttpClient`.
- `JsonException`: sí; es una falla de payload, no de red, pero se presenta como fallo recuperable de upstream.
- `OperationCanceledException`: sólo con `includeOperationCanceled: true`; ningún consumidor de producción activa hoy ese parámetro.
- 401, 403 y 5xx: no; son status HTTP y el helper no recibe `HttpResponseMessage` ni `HttpStatusCode`.

El helper no es usado por los clientes HTTP. Tiene 12 usos de producción, todos en `PageModel` de Cargos/Puestos: `Cargos/Create`, `Cargos/Edit`, seis rutas de `Cargos/Habilidades`, `Puestos/Create` y tres rutas de `Puestos/Edit`.

La adopción es parcial:

- `Habilidades/Create` y `Habilidades/Edit` mantienen filtros manuales; Edit evita absorber cancelación cooperativa cuando el request ya fue cancelado, mientras Create sí captura `OperationCanceledException` sin esa distinción.
- `Habilidades/Cargos` conserva un `IsTransportFailure` privado que duplica `HttpRequestException/TaskCanceledException/JsonException`.
- `UnidadesOrganizativas/Edit` mantiene otro filtro manual que captura también `OperationCanceledException`.
- Otros `PageModel` usan `catch (Exception)` amplio para cargas de listados/catálogos.

#### 4. Cobertura de tests existente y gaps

| Superficie | Cobertura vigente | Gap relevante para #125 |
|---|---|---|
| Clasificador común | `TransportFailureClassifierTests`: `HttpRequestException`, `TaskCanceledException`, `JsonException`; `OperationCanceledException` con y sin opt-in. | Sin caso DNS explícito (`HttpRequestError.NameResolutionError`/`SocketException`); no aplica a status 401/403/5xx. |
| Habilidad | `HabilidadApiClientTests`: 500/502/503/408→`Infrastructure` con status; 404/409; Query propaga `TaskCanceledException`/`HttpRequestException`; token pre-cancelado no envía. | Sin 401/403; transporte sólo en Query; sin DNS explícito. |
| Cargo principal | `CargoApiClientBasicTests`: Query propaga `TaskCanceledException`/`HttpRequestException`; cancelación pre-solicitada; baja 500 conserva status crudo. | Sin 401/403/5xx para `CargoCommandResult`; no verifica el actual `Validation/Unexpected`; sin DNS explícito. |
| CargoSkill | `CargoSkillApiClientTests`: Upsert cubre 401/403/409/500/502/503; Delete cubre 401/403/500; ambos propagan fallos nativos y Delete cubre cancelación pre-solicitada. | Sin `Unexpected` como categoría; sin DNS explícito; no hay cancelación pre-solicitada equivalente para Upsert. |
| Puesto | `PuestosApiClientTests`: los seis métodos propagan `TaskCanceledException`/`HttpRequestException` y respetan token pre-cancelado; baja 500 conserva status crudo. | Sin 401/403/5xx para `PuestoCommandResult`; sin DNS explícito. |
| Unidad Organizativa | `UnidadOrganizativaApiClientTests`: 500→`Validation/Unexpected`; 401 con ProblemDetails→`Validation` preservando texto. | Sin 403, transporte nativo, timeout, cancelación o DNS. |
| Auth | `WebAuthenticationTests.LoginAsync_WhenApiReturnsUnauthorized_ReturnsNull`. | Sin 403/5xx/transporte/cancelación/DNS; 401 es semántica de credenciales, no de comando administrativo. |

`HttpClientExceptionScenarios.TransportExceptionData` sólo tiene dos filas: un `TaskCanceledException` etiquetado como timeout y un `HttpRequestException` genérico. No prueba una resolución DNS fallida real ni distingue timeout interno de cancelación cooperativa por token.

#### 5. Taxonomías huérfanas y magic strings

- No existe `ErrorCategoria`; crear otra taxonomía sin migrar las actuales duplicaría el problema.
- `HabilidadError.StatusCode` sólo tiene consumo productivo indirecto en logs/comentarios; la única lectura explícita encontrada está en tests. El `PageModel` no ramifica por ese status.
- `MapSkillError` es un enum/mapeo privado que contiene `Unauthorized`, `Forbidden`, `Transport` y el magic code `Unexpected`.
- Los clientes principales usan magic codes divergentes: `ServerError`, `TransportError`, `Unexpected`, `BadRequest`, `NotFound` y `Conflict`.
- Los cinco `*DeleteResult` forman una taxonomía paralela basada en `StatusCode`; cuatro viven en `SGV.Web/Integration`, mientras `CargoSkillDeleteResult` vive en `SGV.Contracts`.
- `ApiProblemReader.Result` conserva `StatusCode` junto con `Title/Detail/FieldErrors`; puede alimentar un mapper común sin volver a parsear el body.
- `ApiResults` centraliza la salida del API, pero aún mantiene un switch por cada enum. Todos tienen fallback a 400; agregar una categoría y olvidar un switch degradaría silenciosamente 401/403/5xx a Bad Request.
- Mensajes de UI equivalentes están repetidos con variantes: “No se pudo contactar…”, “El servicio no respondió…”, “Intentá nuevamente”, “Su sesión expiró” y “Acceso denegado”.

#### 6. Decisiones actuales de UI

- **Create/Edit de Habilidad:** conflicto se asocia a `Input.Codigo`; `FieldErrors` se asocian a inputs; cualquier otra categoría muestra `Error.Message` global. `Infrastructure` no tiene una rama propia, por lo que su ventaja es principalmente semántica/logging.
- **Create/Edit de Cargo y Puesto:** las excepciones reconocidas por `TransportFailureClassifier` muestran feedback recuperable; `Conflict` se asocia al código o a un error global; `FieldErrors` pasan por `CargoPostResultMapper`/`PuestoPostResultMapper`. Un 401/403/5xx recibido como respuesta se convierte antes en `Validation/Unexpected`, aunque luego suele mostrarse como error global.
- **Create/Edit de Unidad Organizativa:** no hay catch uniforme alrededor de todos los comandos. Los resultados fallidos muestran `Error.Message`; 401/403/5xx quedan etiquetados como `Validation`, y fallos nativos pueden subir o ser absorbidos por catches amplios según el handler.
- **CargoSkill:** `CargoSkillFormHelpers` sí ramifica por `NotFound`, `Conflict`, `Forbidden`, `Unauthorized` y `Transport`, produciendo mensajes distintos. Pese a comentarios que hablan de redirigir a login, el código actual sólo muestra “Su sesión expiró” y vuelve a renderizar; no redirige.
- **Reactivación:** Habilidad, Cargo, Puesto y Unidad Organizativa sólo distinguen `Conflict`, `NotFound` y default. Las nuevas categorías caerían en default hasta migrar esos switches.
- **Bajas:** las páginas comparan `StatusCode` 404/409 directamente; cualquier otro status usa copy genérico.
- **Controllers API:** bifurcan éxito, `FieldErrors→ValidationProblemDetails` y resto→`ApiResults.ToProblemResult`. `CargosController` además consulta explícitamente `CargoSkillErrorType.Validation`. La autorización 401/403 normalmente ocurre en middleware antes de producir un `CommandResult` de Aplicación.

La inconsistencia sí produce ramas divergentes: CargoSkill distingue sesión, permisos y servidor; Habilidad distingue infraestructura pero la UI no la explota; Cargo/Puesto/UO etiquetan los mismos status como validación; Auth interpreta 401 como credenciales inválidas.

### Áreas afectadas

- `src/SGV.Contracts/Habilidades/Comandos/HabilidadCommandResult.cs` — outlier `Infrastructure` + `StatusCode`.
- `src/SGV.Contracts/Organizacion/Comandos/{Cargo,Puesto,UnidadOrganizativa,CargoSkill}CommandResult.cs` — enums incompatibles entre sí.
- `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` — `UsuarioCommandResult` fuera del patrón de archivos y con `Unauthorized` parcial.
- `src/SGV.Aplicacion/{Personas,Ocupaciones}/Comandos/*CommandResult.cs` — resultados adyacentes que deben entrar o quedar explícitamente fuera del alcance.
- `src/SGV.Web/Integration/Common/{ApiProblemReader,TransportFailureClassifier}.cs` — base reutilizable para un único mapeo.
- `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` — mapeo Infrastructure propio.
- `src/SGV.Web/Integration/Organizacion/{CargoApiClient,PuestosApiClient,UnidadOrganizativaApiClient}.cs` — mapeos duplicados y divergentes.
- `src/SGV.Web/Integration/{Habilidades,Organizacion}/*ListItemViewModel.cs` y `src/SGV.Contracts/Organizacion/Comandos/CargoSkillDeleteResult.cs` — resultados de baja basados en status crudo.
- `src/SGV.Web/Pages/Organizacion/{Habilidades,Cargos,Puestos,UnidadesOrganizativas}/**/*.cshtml.cs` — consumidores con switches, catches y copy divergentes.
- `src/SGV.Api/Infrastructure/Results/ApiResults.cs` y controllers de comandos — matriz enum→HTTP y bifurcación de validación.
- `tests/SGV.Tests/Web/{Common,Habilidad,Cargo,Puesto,UnidadOrganizativa}/` — matrices y gaps de transporte/status.
- `openspec/specs/web-apiclient-transport-contract/spec.md` — contrato vigente que exige propagar excepciones nativas, no convertirlas a resultado funcional.

### Enfoques

1. **Categoría común + mapper HTTP compartido, conservando errores por dominio** — crear `ErrorCategoria` en `SGV.Contracts/Comun/`; mantener `CargoError`, `PuestoError`, etc. para no perder type-safety, pero hacer que todos usen la categoría común. En Web, un helper único devuelve categoría, code/message defaults y status crudo a partir de `ApiProblemReader.Result`.
   - Pros: satisface la taxonomía única; preserva identidad por dominio; elimina los switches repetidos; respeta Clean Architecture y el carácter leaf de Contracts; permite tratar 400/401/403/404/408/409/5xx/otros de forma uniforme.
   - Contras: migración source-breaking de enums y muchos tests/fakes; requiere actualizar `ApiResults`, PageModels y resultados de baja o declarar estos últimos fuera de alcance.
   - Esfuerzo: Alto.

2. **Un único `CommandError`/`CommandResult<T>` genérico** — reemplazar también los records de error y resultados por tipos comunes.
   - Pros: máxima uniformidad de shape y menos código repetido.
   - Contras: blast radius mucho mayor; pierde protección contra mezclar errores de distintos dominios; cambia constructores, deconstruction/equality y APIs públicas; excede el problema concreto de #125.
   - Esfuerzo: Alto y no recomendado.

3. **Sólo centralizar status→enum manteniendo enums actuales** — helper compartido con adaptadores por dominio.
   - Pros: menor riesgo y menor diff inicial.
   - Contras: conserva varias taxonomías, obliga a adaptar cada categoría y no cumple el acceptance criterion de “una sola taxonomía para todos los CommandResult”.
   - Esfuerzo: Medio.

### Recomendación

Adoptar el enfoque 1 con una matriz explícita: `400→Validation`, `401→Unauthorized`, `403→Forbidden`, `404→NotFound`, `408 y 5xx→Transport`, `409→Conflict`, y cualquier otro status no exitoso→`Unexpected`. Mantener `Code` como identificador de negocio/máquina y `Message` como texto presentable.

El mapper de respuestas HTTP debe vivir en `SGV.Web/Integration/Common/`, reutilizar `ApiProblemReader` y preservar el status para logging/diagnóstico sin obligar a la capa de Aplicación a conocer HTTP. `HabilidadError.StatusCode` no tiene consumidor productivo que justifique su shape especial; la propuesta debe retirarlo o reemplazarlo por metadata común del mapper, no replicarlo en cada error de negocio.

`TransportFailureClassifier` debe seguir ocupándose de excepciones y el contrato `web-apiclient-transport-contract` debe conservarse: `HttpRequestException`/`TaskCanceledException` se propagan desde los clientes y se traducen a UX en el borde Razor. No deben convertirse silenciosamente en `CommandResult.Transport`. Sí conviene eliminar filtros manuales y probar DNS con un `HttpRequestException` de name resolution.

La propuesta debe incluir `UsuarioCommandResult` y decidir explícitamente los tres resultados de Aplicación. También debe alinear los `*DeleteResult` o declararlos como follow-up, porque mientras la UI compare status crudo seguirá existiendo una taxonomía paralela. `AuthApiClient` debería conservar su excepción semántica `401→credenciales inválidas`.

Por alcance y strict TDD, es probable superar el presupuesto de revisión de 400 líneas. Conviene planificar slices: (1) contratos + `ApiResults`; (2) mapper Web + clientes; (3) consumidores UI, DeleteResult y matriz de tests.

### Riesgos

- **Alto — compatibilidad de código:** cambiar `*ErrorType` afecta Aplicación, API, Web, fakes y una cantidad alta de tests. Aunque los `CommandResult` no se serializan directamente en los controllers actuales, son contratos públicos compartidos.
- **Alto — contrato de transporte:** convertir excepciones nativas a `Transport` rompería `openspec/specs/web-apiclient-transport-contract/spec.md` y tests existentes.
- **Alto — ordinales:** `CargoSkillErrorType` documenta ordinales append-only; reemplazarlo requiere tratar esa compatibilidad de forma deliberada, aunque no se encontró serialización directa actual.
- **Medio — cancelación cooperativa:** los filtros actuales discrepan sobre `OperationCanceledException`; una centralización ingenua puede registrar/renderizar requests ya abortados.
- **Medio — default silencioso del API:** `ApiResults` mapea enums desconocidos a 400. La migración debe hacer la matriz exhaustiva por tests para evitar degradar Unauthorized/Forbidden/Transport/Unexpected.
- **Medio — UI incompleta:** los switches de reactivación y helpers de formulario tienen default; compilarán aunque no se agreguen ramas para las nuevas categorías.
- **Medio — scope:** incluir resultados de Aplicación, resultados de baja y Auth puede superar 400 líneas; excluirlos sin documentarlo dejaría inconsistencias visibles.
- **Bajo — estado del workspace:** el working tree estaba limpio antes de crear este artefacto; no se encontraron cambios ajenos que preservar.

### Listo para propuesta

Sí. La evidencia confirma la issue y existe un camino compatible con la arquitectura actual. El orquestador debe avanzar a `propose`, fijando como invariantes: taxonomía común, mapper único de respuestas HTTP, propagación nativa de excepciones, semántica especial de login y migración explícita de consumidores/UI.
