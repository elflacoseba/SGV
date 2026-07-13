# Proposal — Taxonomía única de errores para `CommandResult` y clientes HTTP de Web

Change: `2026-07-13-taxonomia-errores-commandresult` · Issue: #125 (`tech-debt`, `refactor`, `contracts`)
Modo de artefacto: híbrido (Engram topic_key + filesystem en `openspec/changes/.../`)

## 1. Intent

Hoy conviven al menos cinco taxonomías paralelas para expresar el "mismo" fallo HTTP dentro del shell `SGV.Web`:

- `HabilidadErrorType.Infrastructure` (con `StatusCode` crudo) absorbe 401/403/408/5xx sin distinción de causa.
- `CargoCommandResult`/`PuestoCommandResult`/`UnidadOrganizativaCommandResult` colapsan 401, 403 y 5xx en `Validation` con magic code `Unexpected`.
- `CargoSkillCommandResult` ya distingue `Unauthorized`, `Forbidden`, `Transport` (es la aproximación más cercana al objetivo, pero no tiene `Unexpected` ni repositorio compartido).
- `MapSkillError` es un mapper privado dentro de `CargoApiClient` que duplica la matriz status→categoría.
- `CargoDeleteResult`/`PuestoDeleteResult`/`HabilidadDeleteResult`/`UnidadOrganizativaDeleteResult`/`CargoSkillDeleteResult` exponen `StatusCode/Code/Message` y son consumidos por switch case directo sobre el código HTTP.

El resultado es que cada cliente HTTP repite su propia matriz de clasificación, los `PageModel` ramifican con `if (ex is X) ...` divergentes, y el mismo status produce un mensaje distinto para el usuario según el dominio (problema de `#120` y parientes). Esta inconsistencia vuelve imposible razonar sobre errores desde un solo lugar, aumenta la superficie de bugs (un switch nuevo sin rama para 401 degrada silenciosamente a `Validation`) y bloquea extender los switches con nuevos códigos sin escribir tests de regresión por cliente.

**Para quién y para qué**:

- *Developers backend* (Aplicación/Api): una sola taxonomía documentada en `SGV.Contracts/Comun/` que les dice qué categorías puede producir cada caso de uso, sin que `Aplicacion` conozca HTTP.
- *Developers Web* (clientes HTTP tipados + `PageModel`): un único helper de mapeo HTTP→categoría con defaults centralizados de `Code`/`Message`, testeable de forma exhaustiva con una matriz.
- *Operadores / soporte*: statuses preservados como metadata de diagnóstico en logs mientras la categoría semántica se usa en UI.
- *Reviewers*: eliminado el switch repetido en cinco clientes, la cobertura de tests de mapeo deja de depender del cliente concreto.

## 2. Alcance

### Incluido

1. **Nueva categoría común** `ErrorCategoria` en `src/SGV.Contracts/Comun/ErrorCategoria.cs` con variantes:
   - `NotFound`, `Conflict`, `Validation` (semánticas de dominio),
   - `Unauthorized`, `Forbidden` (control de acceso),
   - `Transport`, `Unexpected` (fallos recuperables/inesperados del backend).
2. **Migración de los 6 `*CommandResult` vigentes** en `SGV.Contracts` a un error cuya categoría es `ErrorCategoria`:
   - `HabilidadCommandResult`
   - `CargoCommandResult`
   - `PuestoCommandResult`
   - `UnidadOrganizativaCommandResult`
   - `CargoSkillCommandResult`
   - `UsuarioCommandResult` (en `UsuarioContracts.cs`)
3. **Preservación del type-safety por dominio**: cada resultado conserva su record de error específico (`HabilidadError`, `CargoError`, etc.) con campos `Code`/`Message`/`FieldErrors` propios; la categoría viene como propiedad del error y se alinea al enum común.
4. **Helper único de mapeo HTTP→categoría** en `src/SGV.Web/Integration/Common/CommandResultMapper.cs` que consume `ApiProblemReader.Result` y devuelve una tupla `(ErrorCategoria categoria, string code, string message, int? statusCode)`. Default de code/message centralizado.
5. **Migración de los 5 `*DeleteResult`** a la misma taxonomía: `StatusCode` se conserva como metadata de diagnóstico (no se borra, se relega) y se agrega `Categoria`.
6. **Migración de los clientes HTTP** de Web al helper único:
   - `HabilidadApiClient`
   - `CargoApiClient` (Cargo + CargoSkill)
   - `PuestosApiClient`
   - `UnidadOrganizativaApiClient`
   - `AuthApiClient` (manteniendo la semántica especial "401 en login → credenciales inválidas" — ver Suposiciones).
7. **Helper `AuthSessionRedirector`** en `src/SGV.Web/Integration/Common/` que el `PageModel` consulta cuando recibe `ErrorCategoria.Unauthorized` para decidir redirigir a login.
8. **Cierre de gaps de tests** explícitos por cliente principal:
   - 401, 403, 5xx (al menos 500, 502, 503), 408, timeouts, cancelación, DNS (`HttpRequestException` con `SocketError.NameResolutionFailure`).
   - Al menos un caso por cada uno de los seis clientes administrativos (mínimo: matriz status→categoría por cliente principal).

### Fuera de alcance (explícito)

- **`PersonaCommandResult`/`PersonaSkillCommandResult`/`OcupacionCommandResult`** (viven en `SGV.Aplicacion`). Solo exponen `NotFound`/`Conflict`/`Validation` hoy; no impactan flujos administrativos y la superficie a migrar sumaría otro bloque sin valor inmediato. Se documentan como follow-up en la sección 9.
- **Reescritura del shape `CommandResult<T>` a un genérico único** (enfoque 2 del explore). Excede el problema de #125 y amplifica innecesariamente el diff; el type-safety por dominio sigue siendo valioso.
- **`AuthApiClient` 401 → credenciales inválidas**: el helper común lo respeta (no se fuerza al mapper para login). El mapper NO debe usarse en `AuthApiClient.LoginAsync` — ese cliente conserva su semántica.
- **Cambios en `SGV.Api/Infrastructure/Results/ApiResults.cs` matriz enum→HTTP**: necesario para que la nueva categoría `Unauthorized/Forbidden/Transport/Unexpected` llegue al cliente HTTP con el status correcto, pero el alcance es **mínimo**: extender el switch para que `ErrorCategoria` se mapee al status HTTP correcto (ver Riesgos). No refactor general de `ApiResults`.
- **Cambios de comportamiento del handler de middleware (401/403, redirect a login desde middleware)**: la redirección se decide en el `PageModel` vía el helper `AuthSessionRedirector`. El comportamiento observable por el usuario cambia (mejora), pero la causa raíz de "sesión expirada" queda en el borde Web.
- **Eliminación de `HabilidadError.StatusCode`**: solo se deja de alimentar (lo conserva `ApiProblemReader.Result.StatusCode`). Mantener el campo evita source-breaking adicional sobre `HabilidadError` y respeta la línea "los `CommandResult` no son wire types públicos" (ningún controller actual lo serializa).
- **Nuevos flujos de UI que aún no existen** (notificaciones toast, banners específicos por categoría, etc.).
- **Tests E2E con browser real**: no aplica al cambio de taxonomía.

## 3. Suposiciones explícitas (decisiones tomadas en modo auto)

| # | Supuesto | Justificación | Riesgo si falla |
|---|----------|---------------|-----------------|
| S1 | `SGV.Aplicacion` puede consumir `ErrorCategoria` desde `SGV.Contracts` sin invertir el grafo de dependencias | El grafo actual es `Dominio ← Aplicacion ← Contracts ← {Api, Web}`. `Contracts` es leaf hoy, pero NO es incompatible con `Aplicacion` consumiendo un enum de `Contracts` — solo prohíbe que `Contracts` importe cualquier otro proyecto. | Si en realidad `Contracts` ya depende de algo (no verificado), hay que reubicar `ErrorCategoria` en `Dominio` o en `Aplicacion`. Mitigación: revisar `.csproj` antes de `sdd-design`. |
| S2 | `TransportFailureClassifier` puede extenderse con un caso DNS sin cambiar su API pública | El helper ya clasifica `HttpRequestException` en general; añadir detección por mensaje o por `SocketException` interna no rompe consumidores. | Si su contrato tiene `params object[]` y los tests existentes asumen shape específico, agregar overload nuevo o nuevo método estático `IsDnsFailure(HttpRequestException)`. |
| S3 | Los PageModels aceptarán un parámetro nuevo `IAuthSessionRedirector` por inyección sin reventar el patrón actual (que es `IServiceProvider` o constructor primario) | Los `PageModel` actuales ya toman sus dependencias por constructor; agregar un colaborador más es compatible. | Si algún `PageModel` usa `ActivatorUtilities.CreateInstance`, hay que revisar el constructor. |
| S4 | `strict_tdd: true` del repo permite redactar los tests primero en la fase `sdd-spec`/`sdd-tasks` antes del código | Es el flujo vigente del repo (ver `openspec/config.yaml` y AGENTS.md). | Si la entrega del change requiere primero código, el slice 1 debe invertir el orden pero documentar la justificación. |
| S5 | El orquestador aceptará chained PRs (≥2) en lugar de un único PR grande | El `explore` lo recomienda y el forecast de líneas (sección 8) supera 400. | Si el orquestador decide single-PR, el slice final debe ser dividido en una segunda pasada. |
| S6 | Los magic codes actuales (`CodigoDuplicado`, `UnidadOrganizativaNoExiste`, etc.) se preservan verbatim | La spec `web-apiclient-transport-contract` los fija como contractuales, y la UI los aprovecha para FieldErrors. El cambio es de categoría, no de code. | Si los códigos cambian, los `PageModel` y `FieldErrors` quedan inconsistentes — el test de aceptación contractual debe protegerlos. |
| S7 | El redirect de login al detectar sesión expirada es política del caller, no del mapper | La redirección depende de contexto (qué `PageModel` la dispara, qué ruta es pública vs. protegida, si el usuario llegó por deep-link). Mantenerla en el caller evita acoplar el helper a `HttpContext`. | Si se decide policy cross-cutting, hay que migrarlo a un middleware posterior — trabajo fuera de alcance. |
| S8 | `AuthApiClient.LoginAsync` queda fuera del mapper común | El explore lo señala explícitamente: "401 en login significa credenciales inválidas", no comando administrativo. Forzarlo al mapper degrade el contrato de auth. | Si en el futuro `AuthApiClient` crece a `RefreshAsync`/`LogoutAsync`, esos métodos sí pasan por el mapper (no es alcance de #125). |

## 4. Criterios de aceptación (mapeo a AC de #125)

Cada criterio es testeable e independiente:

- **AC1 — Taxonomía única.** Dado cualquier `*CommandResult` vigente de los 6 listados; cuando el caso de uso falla con una causa HTTP; entonces `commandResult.Error.Categoria` es un valor de `ErrorCategoria` (NotFound/Conflict/Validation/Unauthorized/Forbidden/Transport/Unexpected). Cubierto por tests parametrizados `Theory` en `tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs` (uno por cada combinación status→categoría) y tests por cliente.
- **AC2 — Mapper HTTP único.** Dado un `HttpResponseMessage` con un status code (400/401/403/404/408/409/422/500/502/503/504/otro); cuando `CommandResultMapper.Map(HttpResponseMessage, ApiProblemReader.Result)` se invoca; entonces devuelve `(ErrorCategoria, code, message, status)` consistente con la matriz del explore: `400→Validation`, `401→Unauthorized`, `403→Forbidden`, `404→NotFound`, `408/5xx→Transport`, `409→Conflict`, resto→`Unexpected`. Cubierto por `CommandResultMapperTests` parametrizado.
- **AC3 — Propagación nativa intacta.** Dado un cliente HTTP tipado en ejecución; cuando `HttpClient` emite `TaskCanceledException` o `HttpRequestException` (incluyendo por DNS); entonces la excepción nativa se propaga al consumidor, no se convierte a `CommandResult.Transport`. Cubierto por tests existentes (`HabilidadApiClientTests`, `CargoApiClientBasicTests`, `PuestosApiClientTests`, `UnidadOrganizativaApiClientTests`) y un test nuevo `TransportFailureClassifierTests.WhenHttpRequestExceptionWithNameResolutionFailure_ReturnsDnsFailureTrue`.
- **AC4 — DNS explícito.** Dado `HttpRequestException` envolviendo `SocketException` con `SocketError.NameResolutionFailure`; cuando `TransportFailureClassifier.IsDnsFailure(ex)` se evalúa; entonces retorna `true`. Cubierto por test nuevo.
- **AC5 — Helpers unificados en PageModels.** Dado un `PageModel` que invoca un cliente HTTP de los 6; cuando el cliente devuelve un `*CommandResult.Failure`; entonces el `PageModel` ramifica por `ErrorCategoria` (no por `ErrorType` específico del dominio) y ya no contiene `if (ex is HttpRequestException)` ni `if (ex is TaskCanceledException)` manuales. Cubierto por `tests/SGV.Tests/Web/Organizacion/**/PageModelErrorHandlingTests.cs`.
- **AC6 — `*DeleteResult` con categoría.** Dado un delete HTTP respondiendo con 404/409/500; cuando el cliente devuelve `*DeleteResult`; entonces `DeleteResult.Categoria` es `NotFound`/`Conflict`/`Transport`/`Unexpected` respectivamente y `StatusCode` se preserva como metadata. Cubierto por tests de cada `*DeleteResult`.
- **AC7 — `Unauthorized` redirige a login.** Dado un `PageModel` que recibe `*CommandResult.Failure(ErrorCategoria.Unauthorized)` de un recurso protegido; cuando renderiza la respuesta; entonces invoca `IAuthSessionRedirector.TryRedirectToLogin()` antes de mostrar el mensaje inline. Cubierto por test de integración del PageModel.
- **AC8 — `AuthApiClient` intacto.** Dado `AuthApiClient.LoginAsync`; cuando la API responde 401; entonces retorna `null` (semántica de credenciales inválidas) sin pasar por `CommandResultMapper`. Cubierto por `WebAuthenticationTests.LoginAsync_WhenApiReturnsUnauthorized_ReturnsNull` (existente).
- **AC9 — `ApiResults` exhaustivo.** Dado un caso de uso que devuelve `*CommandResult.Failure(ErrorCategoria categoria)`; cuando el controller responde; entonces `ApiResults.ToProblemResult` mapea a `UnauthorizedProblem`/`ForbiddenProblem`/`ServerErrorProblem` según la categoría (no degradar a 400). Cubierto por test unitario parametrizado de `ApiResults`.
- **AC10 — Matriz exhaustiva.** Dado un status code arbitrario no cubierto por la matriz (300, 418, 999); cuando el mapper procesa; entonces el resultado es `ErrorCategoria.Unexpected` y `status` se preserva. Cubierto por `[Theory]` con datos atípicos.

## 5. Estrategia técnica propuesta

### 5.1 Fase 1 — Contratos y categorías (slice 1)

1. Crear `src/SGV.Contracts/Comun/ErrorCategoria.cs` con `public enum ErrorCategoria { NotFound, Conflict, Validation, Unauthorized, Forbidden, Transport, Unexpected }`. Comentario XML que documente cada variante y la matriz status→categoría.
2. Para cada uno de los 6 `*CommandResult`, **migrar su enum `*ErrorType` a un campo/propiedad de tipo `ErrorCategoria`** en lugar de reemplazarlo. Estrategia concreta: introducir el campo `Categoria` en el record `*Error`, dejar el enum actual con `[Obsolete("Use Categoria")]` por un release, marcar los `*ErrorType` como `internal` y eliminarlos en un commit posterior. Para `CargoSkillCommandResult` (que ya tiene variantes alineadas), mapear 1-a-1 y mantener `Transport` como ordinal existente — sin reordenar.
3. Mantener el campo `Code` (string) y `Message` (string) del record `*Error`; agregar opcionalmente `StatusCode: int?` solo si la migración lo requiere para diagnóstico (empezar por `HabilidadError` que ya lo tiene; documentar como internal API).
4. Tests RED-GREEN: contrato de invariantes + test parametrizado que cada `*ErrorType` antiguo mapea al `ErrorCategoria` esperado.

### 5.2 Fase 2 — Helper único de mapeo (slice 2)

1. Crear `src/SGV.Web/Integration/Common/CommandResultMapper.cs` con superficie:
   ```csharp
   public static class CommandResultMapper
   {
       public static (ErrorCategoria Categoria, string Code, string Message, int? StatusCode) Map(
           HttpResponseMessage response,
           ApiProblemReader.Result problem);
   }
   ```
2. Reutilizar `ApiProblemReader.Result` (no volver a parsear el body).
3. Matriz:
   - `400, 422 → Validation` (mantener `FieldErrors` del `ValidationProblemDetails` aparte; el mapper solo emite categoría+code+message).
   - `401 → Unauthorized`
   - `403 → Forbidden`
   - `404 → NotFound`
   - `408, 500, 502, 503, 504 → Transport`
   - `409 → Conflict`
   - resto no-2xx → `Unexpected`
4. Tests exhaustivos parametrizados con `[Theory]` y `[InlineData]` (uno por status code + 5 status codes atípicos).
5. Migrar los 4 clientes administrativos para usar `CommandResultMapper.Map` en vez de sus matrices privadas:
   - `HabilidadApiClient.MapCommandError`
   - `CargoApiClient.MapCargoError` y `MapSkillError` (unificando ambos en el helper).
   - `PuestosApiClient.MapPuestoError`
   - `UnidadOrganizativaApiClient.MapUnidadOrganizativaError`
6. AuthApiClient se queda con su propia lógica de 401.

### 5.3 Fase 3 — AuthSessionRedirector + consumidores UI (slice 3)

1. Crear `src/SGV.Web/Integration/Common/IAuthSessionRedirector.cs` e implementación por defecto en `AuthSessionRedirector.cs`.
2. Registrar en DI con scope compatible con `PageModel`.
3. Migrar los `PageModel` a ramificar por `ErrorCategoria`:
   - `Organizacion/Cargos/{Create,Edit,Reactivate}Model.cs`
   - `Organizacion/Puestos/{Create,Edit,Reactivate}Model.cs`
   - `Organizacion/UnidadesOrganizativas/{Create,Edit,Reactivate}Model.cs`
   - `Habilidades/{Create,Edit,Reactivate}Model.cs`
   - `Organizacion/Cargos/Habilidades/{Create,Edit,Delete,...}Model.cs`
4. Eliminar filtros manuales `if (ex is HttpRequestException)` que ahora reemplazan el switch a `CommandResult.Transport` vía `Categoria`.

### 5.4 Fase 4 — `*DeleteResult` y ApiResults (puede ir como slice 4 o como sub-slice en slice 1)

1. Extender `*DeleteResult` con `Categoria: ErrorCategoria` preservando `StatusCode`.
2. Actualizar `src/SGV.Api/Infrastructure/Results/ApiResults.cs` para incluir `Unauthorized`, `Forbidden`, `Transport`, `Unexpected` en la matriz enum→HTTP. Test exhaustivo de fallback.

### 5.5 Estrategia de tests

- **Tests de contrato** (`tests/SGV.Tests/Contracts/`): uno por `*ErrorType` que afirma el mapeo al nuevo `ErrorCategoria`.
- **Tests de mapper** (`tests/SGV.Tests/Web/Common/CommandResultMapperTests.cs`): `[Theory]` con cada status code × cada cliente.
- **Tests de cliente**: extender `HabilidadApiClientTests`, `CargoApiClientBasicTests`, `CargoSkillApiClientTests`, `PuestosApiClientTests`, `UnidadOrganizativaApiClientTests` con casos 401, 403, DNS explícito.
- **Tests de integración PageModel**: smoke tests que verifican que `Categoria.Unauthorized` redirige vía `IAuthSessionRedirector`.

### 5.6 Compatibilidad

- **Source-breaking**: sí. Enums `*ErrorType` marcados `[Obsolete]` durante el ciclo del change; eliminados al archivar.
- **Wire-breaking**: no. Los controllers no serializan los enums a `ProblemDetails`. La matriz de status HTTP se preserva.
- **DB-breaking**: no.

## 6. Mapeo de archivos

| Tipo | Path | Cambio |
|------|------|--------|
| Crear | `src/SGV.Contracts/Comun/ErrorCategoria.cs` | Definición del enum común |
| Modificar | `src/SGV.Contracts/Habilidades/Comandos/HabilidadCommandResult.cs` | `HabilidadErrorType` → `ErrorCategoria`; `HabilidadError` agrega `Categoria` |
| Modificar | `src/SGV.Contracts/Organizacion/Comandos/CargoCommandResult.cs` | `CargoErrorType` → `ErrorCategoria` |
| Modificar | `src/SGV.Contracts/Organizacion/Comandos/PuestoCommandResult.cs` | `PuestoErrorType` → `ErrorCategoria` |
| Modificar | `src/SGV.Contracts/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` | `UOErrorType` → `ErrorCategoria` |
| Modificar | `src/SGV.Contracts/Organizacion/Comandos/CargoSkillCommandResult.cs` | Mantener ordinales; mapear a `ErrorCategoria` |
| Modificar | `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | `UsuarioCommandResult`: `UsuarioErrorType` → `ErrorCategoria` |
| Modificar | `src/SGV.Contracts/Organizacion/Comandos/CargoSkillDeleteResult.cs` | Agregar `Categoria` preservando `StatusCode` |
| Modificar | `src/SGV.Web/Integration/{Habilidades,Organizacion}/*DeleteResult.cs` (4 archivos) | `Categoria` + preserva `StatusCode` |
| Crear | `src/SGV.Web/Integration/Common/CommandResultMapper.cs` | Mapper único HTTP→categoría |
| Modificar | `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` | Usar `CommandResultMapper` |
| Modificar | `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` | Unificar `MapCargoError` + `MapSkillError` en `CommandResultMapper` |
| Modificar | `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs` | Usar `CommandResultMapper` |
| Modificar | `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | Usar `CommandResultMapper` |
| Crear | `src/SGV.Web/Integration/Common/IAuthSessionRedirector.cs` + `AuthSessionRedirector.cs` | Helper de redirect |
| Modificar | `src/SGV.Web/Program.cs` (o `Startup.cs`) | Registrar `IAuthSessionRedirector` en DI |
| Modificar | `src/SGV.Web/Pages/Organizacion/{Cargos,Puestos,UnidadesOrganizativas,Habilidades}/**/*.cshtml.cs` (todos los `PageModel` afectados) | Switch por `Categoria`, llamar `IAuthSessionRedirector` en `Unauthorized` |
| Modificar | `src/SGV.Web/Integration/Common/TransportFailureClassifier.cs` | Agregar `IsDnsFailure(HttpRequestException)` |
| Modificar | `src/SGV.Api/Infrastructure/Results/ApiResults.cs` | Matriz enum→HTTP exhaustiva (incluye `Unauthorized`/`Forbidden`/`Transport`/`Unexpected`) |
| Crear | `tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs` | Contrato parametrizado de la taxonomía |
| Crear | `tests/SGV.Tests/Web/Common/CommandResultMapperTests.cs` | Tests del mapper |
| Modificar | `tests/SGV.Tests/Web/Common/TransportFailureClassifierTests.cs` | Caso DNS explícito |
| Modificar | `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` | Casos 401, 403, DNS |
| Modificar | `tests/SGV.Tests/Web/Organizacion/Cargo/CargoApiClientBasicTests.cs` | Casos 401, 403, DNS |
| Modificar | `tests/SGV.Tests/Web/Organizacion/Cargo/CargoSkillApiClientTests.cs` | Caso DNS + `Unexpected` |
| Modificar | `tests/SGV.Tests/Web/Organizacion/Puesto/PuestosApiClientTests.cs` | Casos 401, 403, DNS |
| Modificar | `tests/SGV.Tests/Web/Organizacion/UnidadOrganizativa/UnidadOrganizativaApiClientTests.cs` | DNS, 403, transporte nativo |
| Modificar | `tests/SGV.Tests/Api/ApiResultsTests.cs` | Matriz exhaustiva con nuevas categorías |

Total estimado: ~22 archivos modificados + 3 archivos nuevos. Líneas estimadas: ver sección 8.

## 7. Impacto en consumers (tests, fakes, UI)

### Tests

- Los tests existentes que referencian `HabilidadErrorType.Infrastructure` o `CargoErrorType.Validation` deben actualizarse al nuevo `ErrorCategoria`.
- Tests que verifican `StatusCode` en `HabilidadError` siguen válidos (campo preservado como metadata).
- Tests que verifican `Categorias` de `*DeleteResult` que comparan `StatusCode` crudo deben migrar a `Categoria` o aceptar el campo nuevo sin romper.

### Fakes / mocks

- `WebClientLease` y los `*ApiClient` fakes no cambian de signature (solo cambia el contenido del error que producen); los tests que arman responses HTTP siguen usando los mismos builders.

### UI / Razor Pages

- Las páginas que muestran `@Model.ErrorMessage` no cambian de API pública.
- Las páginas que ramifican por `if (Model.IsConflict)` deben migrar a `if (Model.Categoria == ErrorCategoria.Conflict)`.
- Mensajes de UI centralizados: "No se pudo contactar el servicio" para `Transport`, "Su sesión expiró" para `Unauthorized` (con redirect), "Acceso denegado" para `Forbidden`, mensaje de `ProblemDetails.Detail` para `Unexpected`.

### Consumers externos (no-API-surface)

- Ningún cambio visible desde clientes externos: el API sigue exponiendo `ProblemDetails`/`ValidationProblemDetails`. Los `*CommandResult` y `*DeleteResult` son internos a Aplicación/Web aunque vivan en `Contracts`.

## 8. Plan de entrega y forecast del presupuesto

### Forecast de líneas modificadas/agregadas (rough cut, refinado en `sdd-tasks`)

| Slice | Componente | Adiciones | Modificaciones | Eliminaciones | Subtotal |
|-------|-----------|-----------|----------------|---------------|----------|
| Slice 1 | `ErrorCategoria.cs` + 6 `*CommandResult.cs` | ~180 | ~120 | 0 | ~300 |
| Slice 2 | `CommandResultMapper.cs` + 4 clientes Web | ~250 | ~180 | ~50 (matrices privadas) | ~480 |
| Slice 3 | `IAuthSessionRedirector` + PageModels (~12 archivos) | ~120 | ~260 | ~40 (filtros manuales) | ~420 |
| Slice 4 | `*DeleteResult` x5 + `ApiResults` | ~80 | ~120 | ~30 | ~230 |
| Tests | Tests nuevos + extensiones | ~400 | ~250 | ~50 | ~700 |
| **Total** | | **~1030** | **~930** | **~170** | **~2130** |

- **Decision needed before apply**: Yes (definir si slice 4 entra en este PR o se difiere a follow-up).
- **Chained PRs recommended**: Yes.
- **400-line budget risk**: **High** si se intenta single-PR; **Low-Medium** si se aplica en chained PRs de ~400–600 líneas cada uno.
- **Delivery strategy cacheada**: `auto-forecast`. El orquestador, con este pronóstico, debe elegir `auto-chain` y fragmentar en 3–4 PRs chained.
- **Orden de merges sugerido**: Slice 1 (contratos) → Slice 2 (mapper + clientes, depende del 1) → Slice 3 (UI, depende del 2) → Slice 4 (DeleteResult + ApiResults, depende del 1, independiente del 2 y 3). Puede ir merged en paralelo con slice 2 si los tests están bien aislados.

### Hitos de aceptación por PR

- **Slice 1**: build limpio + matriz parametrizada pasa + enums antiguos `[Obsolete]` sin warnings en código de producción.
- **Slice 2**: `dotnet test --filter CommandResultMapperTests` verde + 4 clientes con sus tests actualizados.
- **Slice 3**: smoke tests de UI + redirect probado.
- **Slice 4**: `ApiResults` exhaustivo + delete tests verdes.

## 9. Riesgos y mitigaciones

| Riesgo | Severidad | Mitigación |
|--------|-----------|-----------|
| **Compatibility break entre `*ErrorType` (origen) y `ErrorCategoria` (destino)**: si un valor cae en default branch | HIGH | Tests parametrizados por `(ErrorType origen, ErrorCategoria esperado)` por cada dominio. Bloquea el PR con `[Theory]` rojo si queda desalineado. |
| **Chained PR divergence entre slices**: slice 2 depende de slice 1; si el orden de merge falla, slice 2 queda contra HEAD desactualizado | HIGH | Fijar slice 1 como PR base; los demás target su rama. Documentar en `tasks.md`. Si se rompe, retarget a rama previa hasta que el diff quede limpio (ver §E del skill `sdd-phase-common`). |
| **`ApiResults` switch silenciosamente degradando 401/403 a 400**: agregar nueva rama olvidada | HIGH | Test unitario que enumera TODOS los `ErrorCategoria` y exige un status ≥ 400 específico. Cobertura de líneas 100% en el switch. |
| **Source-breaking de los 6 enums sin coordinación**: callers viejos rompen en build | MED | Marcar `[Obsolete("Use Categoria")]` durante un ciclo; eliminar al archivar el change. Documentar en `docs/decisiones-implementacion.md` el paso de transición. |
| **Ordinales de `CargoSkillCommandResult`**: si alguien reordena el enum existente, la persistencia/logs históricos pierden significado | MED | Comentario XML y constraint explícito en el código: "do not reorder". Code review checklist en `sdd-apply` para verificar invariantes. |
| **Default silencioso en `PageModel`**: cualquier `Categoria` no manejada cae en `default:` del switch y muestra mensaje genérico | MED | Checklist explícito: cada switch en `PageModel` cubre las 7 variantes (`throw new SwitchExpressionNotHandledException(categoria)` para forzar revisión). Tests verifican que ningún switch tiene branch vacía. |
| **Cambio en `HabilidadError.StatusCode` rompe logging**: si un observador downstream lee `StatusCode` directamente, queda ambiguo | LOW | Mantener el campo, solo dejar de alimentarlo en producción. Migrar a `ApiProblemReader.Result.StatusCode` como metadata común. Documentar en `decisiones-implementacion.md`. |
| **Cambio en comportamiento de redirect a login**: usuarios que antes veían mensaje inline ahora son redirigidos (UX puede sorprender) | LOW | Mensaje inline para `Unauthorized` se mantiene como feedback visible antes del redirect; el redirect usa `Redirect("/auth/sign-in?returnUrl=...")` (path real verificado en `src/SGV.Web/Program.cs` `LoginPath` y `Pages/Auth/SignIn.cshtml` `@page`). Guard `IsLocalUrl` en `AuthSessionRedirector` mitiga open-redirect. Documentar en commit message. |
| **Persona/Ocupacion/PersonaSkill quedan fuera**: inconsistencia visible entre `SGV.Contracts` y `SGV.Aplicacion` durante un tiempo | LOW | Documentar el follow-up en `decisiones-implementacion.md` con título "Issue #125 — pendiente migrar `PersonaCommandResult`/`PersonaSkillCommandResult`/`OcupacionCommandResult`". |
| **`AuthSessionRedirector` introduce dependencia circular con `IAuthService`**: si el helper necesita `IAuthService.SignOutAsync` y se inyecta en PageModels que ya tienen `IAuthService` | LOW | Diseño: `IAuthSessionRedirector` recibe por constructor solo `IHttpContextAccessor` + `IAuthService` (que ya existe). Misma dependencia existente en PageModels. Validar en `sdd-design` que no haya ciclo. |
| **Decisión arquitectural no obvia — invertir leaf de `SGV.Contracts`**: si `PersonaCommandResult` debiera migrarse también, podría argumentarse que `Contracts` necesita reordenar dependencias | CRITICAL | NO se ejecuta esa decisión. Documentada en Riesgos como **CRITICAL pre-flight**: si el orquestador decide que `Persona/Ocupacion` entran en este change, debe frenar la cadena, reabrir el slice 1 y replantear el grafo. Recomendación: NO invertir leaf. |

## 10. Próximo paso concreto (handoff a `sdd-spec`)

1. **Crear nueva spec**: `openspec/specs/commandresult-error-taxonomy/spec.md` con:
   - `## ADDED Requirements`:
     - "Common error categoria enum": enum `ErrorCategoria`, semántica de cada variante, contrato append-only.
     - "HTTP→categoria mapping": matriz 400/401/403/404/408/409/5xx/otro → categoría; preservación de `StatusCode` y `FieldErrors`.
     - "Domain-specific errors preserve identity": cada record de error de los 6 dominios sigue exponiendo `Code`/`Message`/`FieldErrors` específicos; `Categoria` viene del enum común.
     - "Delete results expose categoria": los 5 `*DeleteResult` exponen `Categoria` y preservan `StatusCode` como metadata.
     - "Auth login semantics preserved": `AuthApiClient.LoginAsync` mantiene semántica "401 → null".
   - `## MODIFIED Requirements` (sobre `web-apiclient-transport-contract`): agregar un requisito que diga "los clientes HTTP tipados MUST usar el mapper común para traducir respuestas, preservando la propagación nativa de excepciones ya especificada".
2. **Delta specs por dominio** (opcional, recomendado en slice 3+4): una delta por cada `*CommandResult` que documente el cambio de API pública interna (`*ErrorType` → `Categoria`). Si sdd-spec lo considera overhead, basta con la spec nueva.
3. **Pasar el control** a `sdd-design` con la lista de archivos a tocar como input arquitectónico (sección 6).

**Capacities contract** (consumido por `sdd-spec`):

- **New capabilities**:
  - `commandresult-error-taxonomy`: taxonomía común de errores para `*CommandResult` y `*DeleteResult`, helper único HTTP→categoría, helper de redirect a login.
- **Modified capabilities**: None (los specs de dominio existentes no se ven afectados a nivel de behavior contract — el cambio es de surface interna).

## 11. Rollback plan

- **Slice 1 rollback**: reintroducir los enums `*ErrorType` con sus valores originales y `[Obsolete("Use Categoria")]` invertido. Los enums no se eliminaron, solo se marcaron. Eliminar `ErrorCategoria.cs`. El grafo de dependencias queda intacto.
- **Slice 2 rollback**: restaurar las matrices privadas de cada cliente (`MapCargoError`, `MapSkillError`, etc.). El helper común se elimina sin afectar a otros. No hay tablas ni datos afectados.
- **Slice 3 rollback**: quitar la inyección de `IAuthSessionRedirector` en PageModels. Restaurar los `if (ex is ...)` manuales (están en el mismo commit, no se eliminaron prematuramente). El redirect deja de ocurrir — comportamiento idéntico al previo.
- **Slice 4 rollback**: restaurar `*DeleteResult` al shape original (sin `Categoria`). Revertir `ApiResults` a la matriz previa. La cobertura de tests queda como estaba.
- **Estrategia**: cada slice es independiente en merge/revert. Los tests cubren todas las transiciones. La rama `develop` queda funcional con o sin el change.
- **Plan B si el rollback falla**: revertir el merge commit del slice afectado con `git revert -m 1 <merge-sha>` (cherry-pick reverse), re-correr `dotnet test SGV.slnx` para validar estado limpio.

## 12. Dependencias

- **Grafo de proyecto**: `SGV.Aplicacion` ya depende de `SGV.Contracts`; la nueva `ErrorCategoria` no invierte el grafo. Verificación previa en `.csproj` (sdd-design).
- **NuGet**: ninguno nuevo.
- **Otras issues**: ninguna bloqueante. #120 y #121 ya archivadas; #96 archivada. La taxonomía es compatible con la postura `default-deny` y la matriz cookie/CORS.
- **Pre-flight cache verificado**: `execution_mode=auto`, `artifact_store.mode=hybrid`, `delivery_strategy=auto-forecast`, `review_budget_lines=400`. Forecast entregado (sección 8) anticipa `400-line budget risk: High` → requiere `auto-chain`.
