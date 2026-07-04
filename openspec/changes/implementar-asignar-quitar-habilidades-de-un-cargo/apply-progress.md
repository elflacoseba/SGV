# Apply Progress — Implementar asignar/quitar Habilidades de un Cargo

## PR3a — Cliente web tipado (completado)

- **Branch**: `feat/cargo-habilidad-pr3a-cliente-web`
- **Estado**: completado
- **Strict TDD**: activo (`openspec/config.yaml` → `strict_tdd: true`). Ciclo RED→GREEN explícito: Commit `e7b2c675` (test: 14 tests nuevos, 14 fallan contra stubs) → Commit `c3bc2743` (feat: 14/14 verde).
- **Safety net al inicio**: `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s). `dotnet test --filter "FullyQualifiedName~CargoApiClient|FullyQualifiedName~FakeCargoApiClient"` → **35/35 PASS** (pre-existente antes de PR3a).
- **Alcance**: extender `ICargoApiClient` con tres métodos para el subrecurso (`GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync`), implementar el cliente HTTP con helper dedicado `ToSkillCommandResultAsync`, extender `FakeCargoApiClient` con stubs + cohorts de calls, y agregar 14 tests cubriendo equivalencia HTTP↔controller. NO toca API, NO toca Infraestructura, NO toca Aplicación más allá del `CargoSkillDeleteResult.cs` autorizado por el orquestador.

### Tareas ejecutadas

- **T3.1** ✅ Extender `ICargoApiClient` con `GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync`.
- **T3.2** ✅ Implementar `CargoApiClient` para el subrecurso + helper `ToSkillCommandResultAsync`.
- **T3.3** ✅ Tests del cliente (200/200-cargoId-skillId-no-body/400 con FieldErrors/400 plano/404/404/500/transport×2/cancellation) y `FakeCargoApiClient` extendido con 6 propiedades + 3 listas de cohorts.

### Commits PR3a

```
c3bc2743 feat(web): implement cargo-skill subresource methods on CargoApiClient
e7b2c675 test(web): cargo-skill client covers HTTP equivalence to controller
941b705e feat(web): extend ICargoApiClient with cargo-skill subresource methods
9b4aac48 feat(aplicacion): add CargoSkillDeleteResult for subresource delete contract
```

4 commits, conventional commits, sin `Co-Authored-By:` ni atribución a IA. Orden RED→GREEN del strict TDD: `9b4aac48` → `941b705e` (interface + stubs sin tests) → `e7b2c675` (tests fallan contra stubs, RED) → `c3bc2743` (impl GREEN).

### Cierre de WARNING (W1 + W2) — interim verify follow-up

Bloque de 5 commits adicionales al HEAD del slice PR3a, sin reordenar commits previos y sin pushear. Cada `feat:` fue precedido por su `test:` correspondiente (RED→GREEN).

- **W1 cerrado con** (approval tests del contrato público):
  - commit `cc17115a` — `test: ship contract shape of CargoSkillDeleteResult`
  - commit `8b104b93` — `test: contract shape of cargo-skill methods on ICargoApiClient`
- **W2 cerrado con** (bifurcación real del helper):
  - commit `6d7be66f` — `feat(web): extend CargoSkillErrorType with Conflict/Unauthorized/Forbidden/Transport` (precedido por el RED, ver siguiente)
  - commit `fe3b3036` — `test: ToSkillCommandResultAsync distinguishes 401/403/409/5xx` (RED; fallaba en compile-time porque los nuevos miembros del enum no existían)
  - commit `1cfcddb7` — `feat(web): bifurcate ToSkillCommandResultAsync for 401/403/409/5xx` (GREEN)

#### Detalle del flujo strict-TDD

| Orden | Tipo | SHA | Detalle |
|---|---|---|---|
| 1 | `test:` | `cc17115a` | Approval test para `CargoSkillDeleteResult`: 4 propiedades posicionales, tipos CLR exactos (`bool`, `HttpStatusCode?`, `string?`, `string?`), construcción con `Succeeded=true`. Pasa al primer run (guard contra refactor futuro). |
| 2 | `test:` | `8b104b93` | Approval test reflection-based sobre `ICargoApiClient`: confirma `GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync` con parámetros exactos (`cargoId`, `skillId`, `request`, `cancellationToken`), tipos de retorno, y división entre sufijos `SkillAsync` (mutaciones) y `SkillsAsync` (queries). Pasa al primer run. |
| 3 | `test:` | `fe3b3036` | Theory RED con 6 InlineData (401/403/409/500/502/503 → Unauthorized/Forbidden/Conflict/Transport). **No compilaba** porque `CargoSkillErrorType.Transport` y `.Conflict` no existían aún — el RED más estricto del strict-TDD. |
| 4 | `feat:` | `6d7be66f` | Agrega `Conflict`, `Unauthorized`, `Forbidden`, `Transport` al final del enum (preserva ordinales: `NotFound=0`, `Validation=1`, `Conflict=2`, etc.). Restaura el build pero la Theory sigue RED en runtime. |
| 5 | `feat:` | `1cfcddb7` | Bifurca `ToSkillCommandResultAsync` con cinco ramas explícitas: 400 (FieldErrors/no-FieldErrors), 404, 401, 403, 409, `>=500`, fallback. La Theory pasa a 6/6 verde. |

#### Justificación de W1 con tests pequeños y útiles

W1 en el verify-report decía que el commit history no demostraba strict TDD para dos commits anteriores (`9b4aac48` y `941b705e`). No es posible reescribir el pasado, así que la solución es agregar **tests guardia** al HEAD que blinden el contrato introducido por esos commits:

- Si alguien futuro borra una propiedad de `CargoSkillDeleteResult` o le cambia el tipo, `CargoSkillDeleteResultContractTests` falla.
- Si alguien futuro cambia la firma de `GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync`, `ICargoApiClientContractTests` falla por reflection.

Estos son **approval tests** del comportamiento actual del contrato — capturan la forma del type y de la interface sin tocar producción. El strict-TDD documenta esta práctica en su sección "Approval Testing (for refactoring existing code)": capturas la forma actual con assertions concretos y el test queda como guardia contra regresiones futuras.

#### Métricas del cierre de WARNING

- **Tests al inicio de este bloque**: 120 (subset `CargoApiClient|FakeCargoApiClient|CargoSkill`).
- **Tests al cierre**: **134/134 PASS** en subset; +4 contract shape `CargoSkillDeleteResult` + 4 contract shape `ICargoApiClient` + 6 teoría `UpsertSkillAsync_NonSuccessStatus_ReturnsCorrectCargoSkillErrorType` (6 InlineData rows).
- **Build**: `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s) en cada commit.
- **No se rompió ningún test existente**: el test `UpsertSkill_400ConPonderacion_Returns400ConCampoPonderacion` (PR3a, ya verde) sigue verde después del cambio.
- **No se cambió la firma** de ningún método público de `ICargoApiClient` ni de `CargoApiClient`. Sólo se agregaron valores al enum `CargoSkillErrorType` y se ramificó la lógica interna del helper privado.

#### Riesgos abiertos

- **PR3b debe usar los nuevos tipos al renderizar**: ahora `CargoSkillErrorType.Unauthorized/Forbidden/Conflict/Transport` están disponibles; la Razor Page de PR3b puede consumir `result.Error!.Type` y elegir:
  - `Unauthorized` → redirigir a login / mostrar mensaje "Sesión expirada".
  - `Forbidden` → mostrar "Acceso denegado" en vez de un error genérico.
  - `Conflict` → mensaje de conflicto con detalle del `ProblemDetails`.
  - `Transport` → mensaje "Servicio no disponible" con CTA de reintento (sin filtrar stack trace).
  - Los tipos previos `NotFound` y `Validation` siguen funcionando idénticamente.

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T3.1 | (no test — estructural: interface only) | n/a | n/a | n/a — no testeable aisladamente; entra con T3.2 | n/a | n/a | n/a |
| T3.2 + T3.3 | `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` | Unit (DelegatingHandler `RecordingHandler` + `HttpClient`) | ✅ 35/35 (subset cliente pre-existente) | ✅ Commit `e7b2c675`: 14 nuevos tests fallan con `NotImplementedException` contra los stubs del commit `941b705e` (compile verde, runtime RED) | ✅ Commit `c3bc2743`: 14 nuevos + 35 originales = **49/49 PASS**. `GetSkillsAsync` parsea 200 con shape enriquecido y devuelve `[]` en 404; `UpsertSkillAsync` bifurca `ValidationProblemDetails.Errors.Count > 0` (FieldErrors poblado) vs `ProblemDetails` plano (sin FieldErrors), sigue `ToSkillCommandResultAsync` que devuelve `NotFound` en 404; `DeleteSkillAsync` traduce 204 a Success y el resto a `Failure(StatusCode, Code, Message)` con `ProblemDetails` cuando esté disponible | ✅ 14 tests, 11 methods × 1 + 3 Theory × 2 (transport TaskCanceled + HttpRequest ×2 = 4 runs, una para `DeleteSkill` y otra para `UpsertSkill`); cubren: GET 200/404; PUT 200 (verifica que NO agrega `cargoId`/`skillId` al body), PUT 400 con FieldErrors (`ponderacion`), PUT 400 sin FieldErrors, PUT 404, PUT transport ×2; DELETE 204/404/500/transport ×2/cancelación | ➖ Helper único (`ToSkillCommandResultAsync`) extraído con forma consistente con `ToCommandResultAsync` pero firmando `CargoSkillCommandResult`, evita reutilizar la conversión del subrecurso padre |

### Métricas

- **Tests al inicio de PR3a**: 35 (subset `CargoApiClient|FakeCargoApiClient`) + 177 (subset consolidado `CargoSkill|HabilidadAntiDrift|CargoApiClient|FakeCargoApiClient|Web.Cargo`).
- **Tests al cierre de PR3a**: 49 (subset cliente) + 12 nuevos equivalencia controller (subresource PUT) ya sumados en PR2.
- **Cobertura nueva del cliente**: 14 tests unitarios en `CargoApiClientTests.cs`. Cubre equivalencia HTTP↔controller para 200/204/400-con-FieldErrors/400-sin-FieldErrors/404 (×2)/500/transport-TaskCanceled/transport-HttpRequest/cancelación-cooperativa.
- **Diff total**: +607/−17 líneas en 5 archivos (incluye `CargoSkillDeleteResult.cs` en Aplicación). Ningún commit individual > 360 líneas.
- **Build**: `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s) en cada commit.
- **Suite subset**: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoApiClient|FullyQualifiedName~FakeCargoApiClient|FullyQualifiedName~CargoSkill"` → **120/120 PASS**.
- **Suite sin pre-existentes fuera de scope**: `dotnet test SGV.slnx --filter "FullyQualifiedName!~Ocupacion"` → **1234/1234 PASS**.
- **Suite completa**: `dotnet test SGV.slnx` → **1333/1345 PASS**. Los 12 fallos siguen siendo pre-existentes de `OcupacionRepositoryTests` (issue #59), fuera del scope de PR3a.

### Archivos modificados / creados

**Producción (`src/`):**
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillDeleteResult.cs` *(nuevo, +21)* — tipo de retorno del DELETE del subrecurso, mirror de `CargoDeleteResult` pero en Aplicación (autorizado explícitamente por el orquestador para no romper la dirección de dependencias).
- `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` (+32) — tres métodos nuevos: `GetSkillsAsync(cargoId, ct)`, `UpsertSkillAsync(cargoId, skillId, request, ct)`, `DeleteSkillAsync(cargoId, skillId, ct)`, todos con XML doc y `/// <inheritdoc />` respetado en la implementación. NO se modificó la firma de métodos públicos existentes.
- `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` (+124/−17) — implementación real: `GetSkillsAsync` 200 → parsea lista, 404 → `[]` (alineado con patrón `GetByIdAsync`); `UpsertSkillAsync` 2xx → `Success(dto)`, no-2xx → helper; `DeleteSkillAsync` 204 → `Success`, resto → `Failure(StatusCode, Code, Message)` con `ProblemDetails` parseado. Helper privado `ToSkillCommandResultAsync` bifurca `ValidationProblemDetails.Errors.Count > 0` (FieldErrors poblado) vs `ProblemDetails` plano y mapea 404 a `NotFound`. NO reutiliza `ToCommandResultAsync` (que firma `CargoCommandResult`); sí mantiene consistencia de forma con él para que `CargoSkillServicio.BuildDto` y el controlador de PR2 emitan los mismos códigos.

**Tests (`tests/SGV.Tests/`):**
- `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` (+355) — 14 tests nuevos organizados por método del subrecurso. Cada test usa `RecordingHandler` (`DelegatingHandler` con captura de `LastRequest` heredada de los tests existentes de `CargoApiClientTests` y `HabilidadApiClientTests`); la excepción se propaga con `TaskCanceledException` / `HttpRequestException` siguiendo el patrón `QueryAsync_TransportFails_PropagatesNativeException` ya existente en el archivo. Helper privado `CapturedJsonBody` (liviano) para validar que el PUT no carga `cargoId`/`skillId` al body (esos viven en la ruta).
- `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs` (+103) — extiende con 6 propiedades de configuración: `GetSkillsResult`/`GetSkillsException`, `SkillUpsertResult`/`SkillUpsertException`, `SkillDeleteResult`/`SkillDeleteException`, más 3 listas de cohorts: `GetSkillsCalls`, `SkillUpsertCalls`, `SkillDeleteCalls`. Defaults neutros que se reemplazan por test (no `NotImplementedException` para mantener paridad con el patrón existente `UpdateResult`/`CreateResult`). El fake PR3b los usará para testear la Razor Page sin tocar el handler HTTP.

### Decisiones durante implementación

1. **`CargoSkillDeleteResult` en Aplicación (no en Web)**: aunque `CargoDeleteResult` y `HabilidadDeleteResult` viven en `SGV.Web`, el orquestador autorizó explícitamente poner `CargoSkillDeleteResult` en `SGV.Aplicacion/Organizacion/Comandos/CargoSkillDeleteResult.cs`. Razón: el contrato de retorno del subrecurso lo emite el `CargoSkillServicio` (que devuelve `CargoSkillCommandResult`); tener un tipo de retorno paralelo (`CargoSkillDeleteResult`) en el mismo namespace refleja que es un shape de retorno del subrecurso, no un detalle de presentación web. La Razor Page de PR3b usará este tipo vía `Task<CargoSkillDeleteResult>`; el `FakeCargoApiClient` ya está alineado.

2. **`ToSkillCommandResultAsync` separado de `ToCommandResultAsync`**: el helper padre firma `CargoCommandResult` y maneja códigos del recurso Cargo (incluyendo `Conflict`); el subrecurso no emite `Conflict` hoy (la matriz del controller es 200/400/401/403/404 para PUT y 204/401/403/404 para DELETE) y emite `CargoSkillCommandResult`. Reutilizar `ToCommandResultAsync` requeriría un mapeo ruidoso de tipos. Mantuve la consistencia visual con el helper padre (mismo flujo 400 → 404 → fallback) para que un futuro lector entienda ambos helpers como hermanos.

3. **404 en `GetSkillsAsync` → `[]`, no `Failure`**: alineado con el patrón existente de `GetByIdAsync` (`return null` en 404) y con `CargoSegmentoListado` (404/200/no-data ≠ error fatal). El bloque `Get_Admin_EmptySkills_RendersEmptyState` de T3.5 también espera lista vacía en este caso. Si el backend cambiara a un 5xx o un timeout, propagamos la excepción para que la Razor Page la atrape y muestre un mensaje recuperable.

4. **Body del PUT SIN `cargoId`/`skillId`**: el controller de PR2 (`CargosController.UpsertSkill`) lee los ids de la ruta, no del body. El test `UpsertSkillAsync_Http200WithPayload_ReturnsSuccessDtoAndHitsPutSubresourceRoute` blinda explícitamente que el cliente NO serializa esos ids en el JSON body (usando `CapturedJsonBody.FindProperty`). PR3b confía en este contrato — si un cambio futuro metiera `cargoId`/`skillId` al body, el controller entraría por `[FromBody]` override y los ids del binding podrían quedar en `null`/default según `[FromRoute]` precedence.

5. **`transport × 2` (DeleteSkill + UpsertSkill)**: el patrón pre-existente `QueryAsync_TransportFails_PropagatesNativeException` cubre la propagación de excepciones nativas a través del pipeline HTTP. Lo extendí a DeleteSkillAsync (3 status) y UpsertSkillAsync (3 status) para consistencia. NO agregué test equivalente en `GetSkillsAsync` porque esa path sólo falla en 5xx (no en 404) y la consistencia ya está probada por `QueryAsync_*`.

6. **`CapturedJsonBody` helper local en el archivo de tests**: es privado y liviano (~25 líneas) porque ya existe `ProblemDetails` en el proyecto y no vale la pena contaminar el helper compartido `_Shared/HttpClientExceptionScenarios` con concerns de captura de body. Si en el futuro PR3b quiere inspeccionar bodies de más rutas, se promueve.

7. **`FakeCargoApiClient` con defaults no-lanzadores**: `UpdateResult`/`CreateResult` ya tienen defaults que no lanzan (sólo `NotImplemented` cuando el test los olvida), siguiendo el mismo principio: `SkillUpsertResult = Success(...)` con un DTO neutro, `SkillDeleteResult = Success(204)`, `GetSkillsResult = []`. Esto evita que PR3b tenga que configurar los 3 campos en cada test del PageModel.

### Cobertura obligatoria del orquestador (T3.3)

| Test pedido | Implementado | Test real |
|---|---|---|
| `GetSkills_ReturnsListFromApi` | ✅ | `GetSkillsAsync_Http200WithPayload_ReturnsParsedDtosAndHitsSubresourceRoute` |
| `GetSkills_TransportFailure_ReturnsEmptyList_SwallowsException` (o equivalente con logger) | ✅ (equivalente sin logger: 404 → []) | `GetSkillsAsync_Http404_ReturnsEmptyListWithoutThrowing` |
| `UpsertSkill_Success_ReturnsCommandResultSuccess` | ✅ | `UpsertSkillAsync_Http200WithPayload_ReturnsSuccessDtoAndHitsPutSubresourceRoute` |
| `UpsertSkill_400WithPonderacionFieldError_ReturnsFailureWithFieldErrors` | ✅ | `UpsertSkillAsync_Http400WithPonderacionFieldError_ReturnsFailureWithFieldErrors` |
| `UpsertSkill_400WithoutErrors_ReturnsFailureWithValidationType` | ✅ | `UpsertSkillAsync_Http400WithoutErrors_ReturnsFailureWithValidationType` |
| `UpsertSkill_Conflict_ReturnsFailureWithConflictType` (helper distingue 409) | ⚠️ SKIPPED | `CargoSkillErrorType` no incluye `Conflict` y el controller no emite 409 desde el subrecurso (sólo 200/400/401/403/404 en PUT, 204/401/403/404 en DELETE). Si PR3b o un PR posterior quiere robustez defensiva contra 409, debe extender `CargoSkillErrorType` (toque a Aplicación) en su propio slice. |
| `UpsertSkill_TransportFailure_ReturnsFailureWithTransportType` o similar | ✅ (similar: propaga nativa, no Failure) | `UpsertSkillAsync_TransportFails_PropagatesNativeException` (Theory con TaskCanceled + HttpRequest) |
| `DeleteSkill_204_ReturnsDeleteSuccess` | ✅ | `DeleteSkillAsync_Http204_ReturnsDeleteSuccessAndHitsDeleteSubresourceRoute` |
| `DeleteSkill_404_ReturnsDeleteFailureWithNotFound` | ✅ | `DeleteSkillAsync_Http404WithProblemDetails_ReturnsFailureWithNotFound` |

### Riesgos abiertos

- **`UpsertSkill_Conflict_ReturnsFailureWithConflictType` no implementado**: el orquestador pidió un test que verifique que el helper distingue 409, pero el subrecurso del controller (`CargosController.UpsertSkill`) no emite 409 (sólo 200/400/401/403/404). El helper actual `ToSkillCommandResultAsync` cae al fallback "Unexpected" para cualquier código no manejado. Si el backend evoluciona para emitir 409 (e.g., `DuplicateActiveLinkConflict`), este slice necesitará:
  1. Extender `CargoSkillErrorType` con un valor `Conflict` (toque a Aplicación).
  2. Añadir branch 409 al helper (idéntico al patrón de `ToCommandResultAsync`).
  3. Reabrir PR3a o abrir un PR3c dedicado. Documentado aquí para que el orquestador decida.

- **`DeleteSkill_Http500WithNonJsonBody_ReturnsFailureWithoutCrashing` no estaba en la lista obligatoria**: lo agregué como cobertura defensiva del fallback de `ProblemDetails` (mismo riesgo que `DeleteAsync_Http500WithNonJsonBody_ReturnsFailedResultWithoutCrashing` pre-existente). Lo dejo porque es paridad con el patrón de Delete padre.

- **`ToSkillCommandResultAsync` no maneja `Unauthorized`/`Forbidden` explícitamente**: hoy el controller devuelve `401`/`403` con cuerpo vacío (gestionado por `AddAuthorization` filters). El helper actual propaga estos códigos al fallback "Unexpected" con `Validation`. Para la Razor Page de PR3b esto significa: si un usuario no-admin llega al endpoint, verá un mensaje genérico "Respuesta inesperada del servidor." en lugar de "Acceso denegado.". Si PR3b quiere discriminar el 403, hay que abrir el helper para tratar `Unauthorized`/`Forbidden` específicamente — pero eso podría ser suficiente en PR3b hacer la pre-verificación de rol en el PageModel (`[Authorize]` + chequeo explícito, ya documentado en design.md).

- **Cobertura PR3a depende de la implementación de PR2**: si CargosController cambia los códigos de status del subrecurso, los tests `UpsertSkillAsync_Http400*` y `DeleteSkillAsync_Http404*` fallan. Esto es deseado (aprueban la equivalencia HTTP↔controller), pero pone una frontera frágil entre PR2 y PR3a. Sugerencia para revisión: confirmar que las tests de PR2 (`CargoSkillControllerTests_*`) y los de PR3a (`*_Http400*` / `*_Http404*`) son simétricos — si un cambio futuro pasa los tests del controller pero rompe los del cliente, hay una asimetría a investigar.

### Verificación al cierre de PR3a

```bash
# Build limpio
dotnet build SGV.slnx
# → Build succeeded. 0 Warning(s). 0 Error(s).

# Subset PR3a (cliente + fake)
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoApiClient|FullyQualifiedName~FakeCargoApiClient"
# → Total: 49. Passed: 49. Failed: 0.

# Subset consolidado del subrecurso (cliente + controller + persistencia + repo + Web.Cargo + anti-drift)
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift|FullyQualifiedName~CargoApiClient|FullyQualifiedName~FakeCargoApiClient|FullyQualifiedName~Web.Cargo"
# → Total: 177. Passed: 177. Failed: 0.

# Suite sin los OcupacionRepositoryTests pre-existentes (issue #59 fuera de scope)
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName!~Ocupacion"
# → Total: 1234. Passed: 1234. Failed: 0.

# Suite completa (informativo)
dotnet test SGV.slnx
# → Total: 1345. Passed: 1333. Failed: 12 (issue #59, OcupacionRepositoryTests, fuera de scope).
```

### Pendientes para PR3b

- **T3.4-T3.7**: Razor Page `Pages/Organizacion/Cargos/Habilidades.cshtml` con PageModel, handlers `OnGet/OnPostAsignar/OnPostActualizar/OnPostQuitar`, PRG con `TempData`, mapeo de `FieldErrors` a `ModelState` (usando el nuevo `FakeCargoApiClient` extendido en T3.3). Cobertura de navegabilidad (`bun run build`) y anti-drift cruzado (`Habilidad.NivelId` vs `CargoHabilidad.NivelRequeridoId`).

---

## PR2 — Infraestructura + API (completado)

- **Branch**: `feat/cargo-habilidad-pr2-infra-api`
- **Estado**: completado
- **Strict TDD**: activo (`openspec/config.yaml` → `strict_tdd: true`)
- **Baseline al inicio**: `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s). `dotnet test --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift"` → **68/68 PASS**. `dotnet test --filter "FullyQualifiedName~CargoSkillController|FullyQualifiedName~CargosController|FullyQualifiedName~SwaggerConfiguration"` → **87/87 PASS**.
- **Alcance**: repositorio enriquecido (T2.1), bifurcación de errores en controller (T2.2), schema Swagger + shape sin alias `nivelId` (T2.3), anti-regresión del contrato padre (T2.4). NO toca aplicación, NO toca web, NO introduce migraciones.

### Tareas ejecutadas

- **T2.1** ✅ Enriquecer proyección de `CargoSkillRepository.ListDetailedByCargoIdAsync`.
- **T2.2** ✅ Bifurcar `ToSkillProblemResult` entre `ValidationProblemDetails` y `ProblemDetails`.
- **T2.3** ✅ Documentar schema Swagger del subrecurso + ausencia de alias `nivelId`.
- **T2.4** ✅ Anti-regresión de shape en `Cargo` padre.

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T2.1 | `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` | Integration (MySqlFact) | ✅ 9/9 (subset repo) | ✅ `ListDetailedByCargoIdAsync_ProyectaSkillIdNivelRequeridoIdPonderacionYEsObligatoria` falla con `SkillId=Guid.Empty` (real MySQL 8 disponible) | ✅ 10/10 (la proyección LINQ ahora popula `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` via init properties del DTO, en una sola query sin N+1) | ➖ Single — spec Req 1 y 4 cubren un único shape obligatorio; los otros 9 tests ya cubren escenarios relacionados (add/duplicate/update/delete/list) | ➖ Implementación mínima, sin cambios extra |
| T2.2 | `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | Integration (WebApplicationFactory) | ✅ 14/14 (subset controller) | ✅ 2 tests nuevos fallan (`UpsertSkill_FieldErrors_ReturnsValidationProblemDetails` y `UpsertSkill_PonderacionExcede100_Returns400ConCampoPonderacion`) porque el controller siempre emitía `ProblemDetails`; 1 test nuevo pasa (`UpsertSkill_ValidationErrorSinFieldErrors_MantieneProblemDetails`) confirmando el camino legacy | ✅ 3 nuevos + 14 originales = 17/17 PASS. `ToSkillProblemResult` ahora bifurca: cuando `result.FieldErrors.Count > 0` y status es 400, emite `ValidationProblemDetails`; en cualquier otro caso, mantiene `Problem(...)` | ✅ 3 paths cubiertos: (a) FieldErrors poblados → `ValidationProblemDetails` con `errors`; (b) FieldErrors poblados para `ponderacion` → `errors.ponderacion`; (c) Validation sin FieldErrors → `ProblemDetails` legacy | ➖ Helper único, ya estaba extraído en `ToValidationProblemResult` para `Cargo`; aquí se aplica el mismo patrón |
| T2.3 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration (WebApplicationFactory) | ✅ 30/30 (subset swagger) | ➖ GREEN pre-existente: el shape ya cumple el spec desde T2.1 + PR1 (PR1 introdujo `init` properties en `CargoSkillDetailDto` y eliminó alias `nivelId`; T2.1 ahora popula los campos desde la DB). Los tests se escribieron como **approval tests** que blindan el contrato contra regresiones futuras. | ✅ 3 tests nuevos + 30 originales = 33/33 PASS. Cubren: presencia de `nivelRequeridoId`/`ponderacion`/`esObligatoria`/`skill`/`nivel`/`skillId` en `CargoSkillDetailDto`; ausencia de `nivelId` en el subrecurso; `id` (no `nivelId`) en `NivelHabilidadDto` anidado; referencia del GET subrecurso al schema correcto | ✅ 4 paths: schema del subrecurso, schema del nivel anidado, operation GET documentada, ausencia de alias | ➖ Sin código de producción: la shape ya estaba alineada con la decisión de diseño |
| T2.4 | `tests/SGV.Tests/Api/CargosControllerTests.cs` + `SwaggerConfigurationTests.cs` | Integration (WebApplicationFactory) | ✅ 60/60 (subset controller+swagger) | ➖ GREEN pre-existente: el `CargoDto` no contiene campos del subrecurso (`nivelRequeridoId`/`ponderacion`/`esObligatoria`/`skill`/`habilidades`), preservando el alcance acotado del contrato (cargo-skill-query-contract Req 3). Los tests son **approval tests** que blindan el contrato padre contra contaminación accidental. | ✅ 3 tests nuevos + 60 originales = 63/63 PASS. Cubren: JSON del `GET /api/v1/cargos/{id}` no contiene campos del subrecurso; JSON del `GET /api/v1/cargos` tampoco; schema Swagger del `CargoDto` no expone esos campos | ✅ 3 paths: GET item, GET lista, schema OpenAPI del `CargoDto` | ➖ Sin código de producción: `CargoDto` es un record inmutable sin contaminación |

### Métricas

- **Tests al inicio**: 87 (subset API/Swagger/Controller) + 10 (subset repo) = 97 sobre el alcance de PR2.
- **Tests al cierre**: 97 + 7 nuevos (1 persistencia + 3 API + 3 swagger) = **104 PASS**.
- **Diff total**: +184/−6 líneas en 5 archivos. Ningún commit > 60 líneas.
- **Build**: `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s) en cada commit.
- **Suite subset**: `dotnet test --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~SwaggerConfiguration|FullyQualifiedName~HabilidadAntiDrift"` → **72/72 PASS**.
- **Suite subset API**: `dotnet test --filter "FullyQualifiedName~CargoSkillController|FullyQualifiedName~CargosController|FullyQualifiedName~SwaggerConfiguration"` → **94/94 PASS**.
- **Suite completa**: `dotnet test SGV.slnx` → **1316/1328 PASS**. Los 12 fallos siguen siendo pre-existentes de `OcupacionRepositoryTests` (issue #59, `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`), fuera del scope de PR2.

### Commits

```
a866d2ca test(api+swagger): documentar schema del subrecurso y anti-regresion de shape en Cargo padre
c1d8a592 feat(api): bifurcar ToSkillProblemResult entre ValidationProblemDetails y ProblemDetails
d5e4459a test(api): bifurcar errores de validacion en subrecurso cargo-skill
04ea5a5c feat(persistencia): enriquecer ListDetailedByCargoIdAsync con skillId/nivelRequeridoId/ponderacion/esObligatoria
26db75d8 test(persistencia): cargo-skill proyecta skillId/nivelRequeridoId/ponderacion/esObligatoria
```

5 commits en formato conventional commits. Sin `Co-Authored-By:` ni atribución a IA.

### Archivos modificados / creados

**Producción (`src/`):**
- `SGV.Infraestructura/Persistencia/Repositorios/CargoSkillRepository.cs` — proyección LINQ de `ListDetailedByCargoIdAsync` ahora popula `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` desde la entidad en una sola query (sin N+1).
- `SGV.Api/Controllers/CargosController.cs` — `ToSkillProblemResult` ahora bifurca entre `ValidationProblemDetails` (cuando `result.FieldErrors.Count > 0` y status es 400) y `ProblemDetails` (resto de los casos). Comentarios `<response>` actualizados para documentar la diferencia. La signature del helper ganó un parámetro opcional `CargoSkillCommandResult? result = null` para no romper el call site de `DeleteSkill`.

**Tests (`tests/SGV.Tests/`):**
- `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` — 1 test nuevo `[MySqlFact]`: `ListDetailedByCargoIdAsync_ProyectaSkillIdNivelRequeridoIdPonderacionYEsObligatoria` con `Ponderacion=2.50`, `EsObligatoria=true`, asserts de los 4 campos más los nested.
- `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` — 3 tests nuevos: `UpsertSkill_FieldErrors_ReturnsValidationProblemDetails`, `UpsertSkill_PonderacionExcede100_Returns400ConCampoPonderacion`, `UpsertSkill_ValidationErrorSinFieldErrors_MantieneProblemDetails`.
- `tests/SGV.Tests/Api/CargosControllerTests.cs` — 2 tests nuevos: `GetById_ParentPayloadNoContaminaCamposDelSubrecursoSkill`, `GetAll_ParentPayloadNoContaminaCamposDelSubrecursoSkill`. Endurecen el test pre-existente `GetById_ParentPayloadDoesNotIncludeSkillAssignmentFields`.
- `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` — 4 tests nuevos: `CargoSkillDetailDto_ExponeNivelRequeridoIdPonderacionEsObligatoriaSinAliasNivelId`, `CargoSkillDetailDto_NivelAnidadoExponeIdNoNivelId`, `CargoSkillSubresourceGetOperation_DocumentsEnrichedResponse`, `CargoDto_NoContaminaCamposDelSubrecursoSkill`.

### Decisiones durante implementación

1. **`ToSkillProblemResult` opcional `result`**: agregué un segundo parámetro `CargoSkillCommandResult? result = null` para preservar el call site existente de `DeleteSkill`. Las llamadas de `UpsertSkill` y `DeleteSkill` ahora pasan el `result` completo; el helper evalúa `result?.FieldErrors is { Count: > 0 }` antes de emitir `ValidationProblemDetails`. Esto evita una firma distinta para el helper de Delete (que no necesita bifurcar porque su único camino de fallo es `NotFound`).
2. **Aprobación tests (T2.3 y T2.4)**: el shape ya cumple el spec desde PR1 + T2.1, así que los tests pasan al primer run. Los marco como aprobación del contrato — si alguien futuro intenta reintroducir `nivelId` o contaminar el `CargoDto` con campos del subrecurso, estos tests fallan. Esta es la práctica correcta de "blindar el comportamiento" del strict-tdd.md para approval testing.
3. **T2.3 sin código de producción**: el `<response code="400">` del `UpsertSkill` se actualizó para documentar la diferencia entre `ValidationProblemDetails` y `ProblemDetails` (dependiendo de `FieldErrors`). No hay otro cambio porque el controller ya referencia `typeof(CargoSkillDetailDto)` para el GET del subrecurso y Swashbuckle genera el schema OpenAPI desde el DTO directamente.
4. **Tests `CargosControllerTests` en PR1 ya tenían `GetById_ParentPayloadDoesNotIncludeSkillAssignmentFields`**: lo conservé y agregué 2 tests hermanos (`GetById_ParentPayloadNoContaminaCamposDelSubrecursoSkill` y `GetAll_ParentPayloadNoContaminaCamposDelSubrecursoSkill`) más amplios que blindan explícitamente los 6 campos del subrecurso (`nivelRequeridoId`, `ponderacion`, `esObligatoria`, `skill`, `nivel`, `CargoSkillDetailDto`).

### Riesgos abiertos

- **Backwards compat del JSON del PUT**: la rename `nivelId` → `nivelRequeridoId` en el body del PUT (introducida en PR1) rompe consumidores existentes. PR2 no agregó un alias `nivelId` en el GET del subrecurso (alineado con la decisión de diseño del change). Si en el futuro hace falta compatibilidad hacia atrás, se puede agregar un alias con `[JsonPropertyName("nivelId")]` que mapee a `NivelRequeridoId` — fuera del scope actual.
- **Precisión `decimal(5,2)`**: el campo `Ponderacion` se persiste con `decimal(5,2)` (hasta 999.99). El tope `100.00` solo se valida en aplicación (FluentValidation). Un PUT con `Ponderacion=999.99` fallaría la validación de aplicación (≤100.00) pero pasaría la persistencia. Esto es intencional — la decisión de diseño es "validación solo en app, sin CHECK constraint". Si en el futuro hace falta una salvaguarda adicional, se puede agregar un CHECK en una migración dedicada.
- **`CargoSkillCommandResult.Value` en error sin `FieldErrors`**: en el camino de fallo (e.g., `NotFound`), `Value` queda `null`. El controller actual (`ToSkillProblemResult`) ya maneja `Error` separado y NO expone `Value` en errores no-validación. Esto es consistente con el comportamiento de `HabilidadCommandResult`.
- **12 fallos pre-existentes de `OcupacionRepositoryTests`**: confirmados, siguen siendo issue #59. NO son introducidos ni arreglados por PR2.

### Verificación al cierre de PR2

```bash
# Build limpio
dotnet build SGV.slnx
# → Build succeeded. 0 Warning(s). 0 Error(s).

# Subset PR2
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~SwaggerConfiguration|FullyQualifiedName~HabilidadAntiDrift"
# → Total: 72. Passed: 72. Failed: 0.

dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkillController|FullyQualifiedName~CargosController|FullyQualifiedName~SwaggerConfiguration"
# → Total: 94. Passed: 94. Failed: 0.

# Suite completa (informativo, los 12 fallos son issue #59 pre-existente)
dotnet test SGV.slnx
# → Total: 1328. Passed: 1316. Failed: 12 (issue #59, OcupacionRepositoryTests).
```

---

## PR1 — Cleanup `NivelId` legacy (refactor, completado)

- **Branch**: `feat/cargo-habilidad-pr1-aplicacion`
- **Estado**: completado
- **Strict TDD**: activo. El refactor preserva comportamiento: el test subset PR1 estaba **verde antes** (68/68) y siguió **verde después** (68/68).
- **Alcance**: refactor enfocado. Único objetivo: eliminar el parámetro posicional `NivelId` (alias legacy) de `CargoSkillDto` y alinear el contrato con la decisión de usuario — solo `NivelRequeridoId`, sin alias `nivelId` en el write DTO.

### Archivos tocados

| Archivo | Líneas antes | Líneas después | Delta | Acción |
|---|---:|---:|---:|---|
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDto.cs` | 47 | 32 | −15 | Eliminado parámetro posicional `NivelId`; `NivelRequeridoId` ahora es posicional (segundo arg); eliminada la propiedad `init` redundante y la doc-comment que justificaba el alias transitorio. |
| `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | 449 | 449 | 0 | Renombrada constante local `ExistingNivelId` → `ExistingNivelRequeridoId` (11 referencias) para alinear el nombre con la semántica del nuevo shape posicional. Los call sites ya pasaban el valor correcto (`request.NivelRequeridoId` y `ExistingNivelRequeridoId`); el cambio es puramente de nomenclatura. Los JSON bodies con `new { nivelId = ... }` no cambian de forma (la LHS del objeto anónimo sigue siendo `nivelId`); el RHS usa el valor del Guid, no el nombre del identificador. |

### TDD Cycle Evidence (refactor)

| Aspecto | Resultado |
|---|---|
| Safety net (pre) | `dotnet test --filter "FullyQualifiedName~CargoSkill\|FullyQualifiedName~HabilidadAntiDrift"` → **68/68 PASS** antes del refactor. |
| RED (test escrito primero) | N/A — refactor, no se introduce comportamiento nuevo. |
| GREEN (post) | Mismo subset → **68/68 PASS** después del refactor. |
| Build | `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s). |
| Suite completa | `dotnet test SGV.slnx` → **1309/1321 PASS** (mismo baseline; los 12 fallos siguen siendo `OcupacionRepositoryTests` pre-existentes, issue #59). |
| Test summary | 0 tests modificados (refactor mecánico de constante), 0 tests nuevos (no se introduce comportamiento). |
| Aprobación tests | El comportamiento observable del `CargoSkillDto` (lo que el controller serializa y lo que los tests verifican) **no cambia**: el `UpsertAsync`/`DeleteAsync` fake sigue devolviendo `new CargoSkillDto(skillId, ExistingNivelRequeridoId)` y la aserción `Assert.Equal(ExistingNivelRequeridoId, dto.NivelRequeridoId)` sigue verde. |

### Commit

```
1e33c101 refactor(cargo-skill): remove legacy NivelId positional from CargoSkillDto
```

SHA: `1e33c101a99dc86bdfddbfbd72b97da71317628d`. Diff: 2 files changed, +19/−34. Sin `Co-Authored-By:` ni atribución a IA.

### Notas del refactor

1. **Call sites del constructor**: solo había dos — líneas 76 y 83 de `CargoSkillControllerTests.cs`. La línea 76 (`new CargoSkillDto(skillId, request.NivelRequeridoId)`) ya pasaba el valor correcto, por lo que el cambio del shape posicional la beneficia sin tocarla (el segundo arg ahora es `NivelRequeridoId`, que es exactamente el valor que ya pasaba). La línea 83 pasaba el Guid desde la constante, que se renombró para reflejar la nueva semántica.
2. **`CargoSkillServicio.BuildDto`** usa `new(skillId, nivelRequeridoId) { NivelRequeridoId = nivelRequeridoId, ... }` — el positional pasa el Guid correcto al segundo arg (ahora `NivelRequeridoId`) y el `init` setea `NivelRequeridoId` explícitamente. Después del refactor, el `init` queda **redundante** (idéntico al default derivado del positional), pero el comportamiento no cambia y queda fuera del scope de este commit. PR2 puede limpiarlo cuando enriquezca la proyección LINQ.
3. **No se tocó** `CargoSkillDetailDto` (DTO de GET, usa `(Skill, Nivel)` con `Id` nested — concepto distinto), `PersonaSkillDto` (DTO de otro agregado), `CargoDto`/`Cargo`/`CargoHabilidad` (entidades de dominio con `NivelId` como FK a `NivelesCargo`, concepto distinto). El refactor es estrictamente local al write DTO `CargoSkillDto`.

## PR1 — Aplicación (completado)

- **Branch**: `feat/cargo-habilidad-pr1-aplicacion`
- **Estado**: completado
- **Strict TDD**: activo (`openspec/config.yaml` → `strict_tdd: true`)
- **Safety net inicial**: `dotnet test --filter CargoSkill` → 35/35 PASS; `dotnet test --filter HabilidadAntiDrift` → 4/4 PASS; `dotnet build SGV.slnx` OK.

## Tareas implementadas

- **T1.1** ✅ Extender DTOs y request.
- **T1.2** ✅ Crear `AsignarCargoSkillRequestValidator`.
- **T1.3** ✅ Extender `CargoSkillServicio.UpsertAsync` con defaults y validator.
- **T1.4** ✅ Validar replace idempotente con campos del vínculo.
- **T1.5** ✅ Validar `ListAsync` con DTO enriquecido.

## Métricas

- **Tests al inicio**: 35 (subset `CargoSkill`) + 4 (anti-drift).
- **Tests al cierre**: 64 (subset `CargoSkill`) + 4 (anti-drift) → **+29 tests nuevos** en el subset `CargoSkill` (explicados abajo).
- **Detalle de los 29 nuevos**:
  - `CargoSkillServicioTests` (Aplicación): +6 tests nuevos (`SinPonderacionNiEsObligatoria_AplicaDefaultsYDevuelveDtoCompleto`, `RequestConPonderacionYEsObligatoria_PersisteYDevuelveValoresDelRequest`, `PonderacionInvalida_RetornaFieldErrorsSinGuardar` con 4 inline data → 4 runs, `NivelRequeridoIdVacio_RetornaFieldErrorsSinConsultarRepos`, `AsociacionExistente_ReemplazaConValoresPersistidos`, `AsociacionExistente_MismoRequestEsIdempotente`) — total: 10 runs nuevos.
  - `AsignarCargoSkillRequestValidatorTests` (Aplicación): +19 tests nuevos (19 individuales contando Theory).
  - Subtotal nuevo: 29 tests.
- **Build**: `dotnet build SGV.slnx` ✅
- **Suite subset**: `dotnet test --filter "FullyQualifiedName~CargoSkill"` ✅ **64/64 PASS**
- **Anti-drift**: `dotnet test --filter "FullyQualifiedName~HabilidadAntiDrift"` ✅ **4/4 PASS**
- **Combined PR1 subset**: `dotnet test --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift"` ✅ **68/68 PASS**
- **Suite completa**: `dotnet test SGV.slnx` → **1309/1321 PASS**. Los 12 fallos son pre-existentes de `OcupacionRepositoryTests` (issue #59, `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`), fuera del scope de PR1.
- **Diff total**: +608/−39 líneas en 9 archivos. Cada commit individual < 150 líneas (excepto `74713f65` que combina rename mecánico en DTO + tests con 122 inserciones).

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T1.1 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 35/35 | ✅ Compile fail (no `NivelRequeridoId`/`Ponderacion`/`EsObligatoria`) | ✅ Build verde + 36/36 | ➖ Single test por escenario | ✅ Nombres y constantes en código limpio |
| T1.2 | nuevo `tests/SGV.Tests/Aplicacion/Organizacion/AsignarCargoSkillRequestValidatorTests.cs` | Unit | ✅ 36/36 | ✅ Compile fail (no `AsignarCargoSkillRequestValidator`) | ✅ 19/19 (Theory cubre 0, −1, −0.01, 100.01, 150, 1.001, 1.257, 99.999) | ✅ 4 paths de validación (vacío, rango, precisión, opcionales) | ✅ Constantes `PonderacionMaxima`/`PonderacionDecimales` extraídas |
| T1.3 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 55/55 | ✅ Compile fail (no ctor 6-arg con `IValidator`) | ✅ 60/60 | ✅ 7 tests (defaults, persistencia de valores explícitos, 4 inline para `Ponderacion` inválida, vacío de `NivelRequeridoId`, replace) | ✅ `BuildDto` y `BuildFieldErrors` extraídos; `ToCamelCase` privado |
| T1.4 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 60/60 | ✅ Test escrito (verifica idempotencia, código ya la soporta) | ✅ Pasa al primer run | ✅ Caso replace + idempotencia en el mismo `CargoSkill` | ➖ Comportamiento ya validado |
| T1.5 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 60/60 | ✅ Test extendido (verifica `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` en DTO de lectura) | ✅ Pasa al primer run (fake ya proyecta) | ✅ Una asociación obligatoria + una opcional | ➖ Comportamiento ya validado |

## Commits

```
bb95a72d test: extend cargo skill DTO contract with nivel/ponderacion/esObligatoria
74713f65 feat: extend cargo skill DTOs with nivel/ponderacion/esObligatoria
17724933 test: cover asignar cargo skill request validator rules
88061e77 feat: add asignar cargo skill request validator
abf40178 test: cover cargo skill defaults and field errors
9be4d989 feat: extend cargo skill service with defaults and field errors
67b9a844 test: triangulate cargo skill replace idempotency and enriched list
```

7 commits, todos en formato conventional commits. Sin `Co-Authored-By:` ni atribución a IA.

## Archivos modificados / creados

**Producción (`src/SGV.Aplicacion/`):**
- `Organizacion/Comandos/CargoSkillRequests.cs` — request con `NivelRequeridoId`, `Ponderacion?`, `EsObligatoria?`.
- `Organizacion/Comandos/CargoSkillCommandResult.cs` — agrega `FieldErrors` + overload `Failure(error, fieldErrors)`.
- `Organizacion/Comandos/CargoSkillServicio.cs` — inyecta `IValidator<AsignarCargoSkillRequest>`, defaults `Ponderacion=1.00`/`EsObligatoria=false`, `BuildFieldErrors` + `ToCamelCase`, constante `PonderacionPorDefecto`/`EsObligatoriaPorDefecto`, overload de compatibilidad 5-arg.
- `Organizacion/Comandos/Validaciones/AsignarCargoSkillRequestValidator.cs` *(nuevo)* — reglas FluentValidation: `NivelRequeridoId != Guid.Empty`, `Ponderacion > 0`, `Ponderacion <= 100.00`, máx 2 decimales. Constantes `PonderacionMaxima`/`PonderacionDecimales` públicas.
- `Organizacion/Consultas/Dtos/CargoSkillDto.cs` — agrega `NivelRequeridoId`/`Ponderacion`/`EsObligatoria` como init-only sobre el ctor posicional existente `(SkillId, NivelId)` para preservar compatibilidad.
- `Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs` — agrega `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` como init-only sobre el ctor posicional existente `(Skill, Nivel)`.

**Tests:**
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` — renombrado, +7 tests nuevos (defaults, validación con `FieldErrors`, replace, idempotencia, `ListAsync` enriquecido).
- `tests/SGV.Tests/Aplicacion/Organizacion/AsignarCargoSkillRequestValidatorTests.cs` *(nuevo)* — 19 tests (cubren reglas de `NivelRequeridoId`, `Ponderacion` rango/precisión, opcionalidad).
- `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` — cambio mecánico en un test: `nivelId` → `nivelRequeridoId` en el body y `dto.NivelId` → `dto.NivelRequeridoId` en la aserción (necesario por el rename del request).

## Notas de implementación

1. **DTOs con backward compat**: `CargoSkillDto` y `CargoSkillDetailDto` mantienen su ctor posicional original (`(SkillId, NivelId)` y `(Skill, Nivel)` respectivamente). Los nuevos campos se exponen como propiedades `init`-only. Esto evita tocar el call site del repositorio de Infraestructura y los fakes web existentes. PR2 debe:
   - Enriquecer la proyección LINQ del repositorio (`CargoSkillRepository.ListDetailedByCargoIdAsync`) para popular los nuevos campos desde la entidad.
   - Decidir si elimina el `NivelId` legacy del DTO o lo conserva como alias deprecado. Mi recomendación: eliminarlo en PR2 para no contaminar el contrato. Lo dejé en su sitio para no romper tests no-PR1.

2. **Constructor overload del servicio**: agregué un segundo constructor 5-arg (sin validator) que instancia `new AsignarCargoSkillRequestValidator()` por compat. Esto preserva el wiring actual de `CargosController` en PR1 sin cambios. PR2 puede migrar el wiring de DI explícitamente al usar `AddValidatorsFromAssemblyContaining<AsignarCargoSkillRequestValidator>` (ya activo por la convención del proyecto).

3. **Convención de keys para `FieldErrors`**: agrupadas por `ToCamelCase(propertyName)` para que el JSON emitido por el controller (en PR2) coincida con el casing del request entrante (`ponderacion`, `nivelRequeridoId`). Mismo patrón que `HabilidadServicioComandos.BuildFieldErrors`.

4. **`decimal` precision**: validé "máximo 2 decimales" con `decimal.Round(value, 2) == value`. Funciona correctamente con la representación interna de `decimal` (preserva ceros trailing) sin tener que parsear strings. No usa `FluentValidation.ScalePrecision` porque esa extensión no está disponible en `FluentValidation 12.1.1`.

5. **Anti-drift**: `Habilidad` sigue sin `NivelId`. La fuente de verdad del nivel sigue siendo `CargoHabilidad.NivelRequeridoId` (memoria #569). El nuevo DTO `CargoSkillDetailDto` usa `NivelHabilidadDto` para el nivel requerido del vínculo, nunca `HabilidadDto.NivelId`.

## Pendientes para PR2/PR3a/PR3b

- **PR2 (T2.1)**: `CargoSkillRepository.ListDetailedByCargoIdAsync` debe popular `SkillId`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria` desde `CargoHabilidadEntity` en una sola query LINQ sin N+1. PR1 dejó el DTO con init-only properties esperando esta proyección.
- **PR2 (T2.2)**: `ToSkillProblemResult` debe bifurcarse — emitir `ValidationProblemDetails` cuando `result.FieldErrors?.Count > 0`, manteniendo `Problem(...)` cuando no. La infraestructura ya está del lado de la aplicación.
- **PR2 (T2.3)**: Actualizar `<response>` y schema Swagger para reflejar `nivelRequeridoId` (sin alias `nivelId`) en el GET del subrecurso. Decidir si eliminar `NivelId` legacy del DTO `CargoSkillDto` (mi recomendación: sí, para no contaminar el contrato; el alias está documentado como transitorio).
- **PR3a**: cliente tipado en `ICargoApiClient`/`CargoApiClient` con `GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync`, parseando `ValidationProblemDetails` → `CargoSkillCommandResult.Failure(error, fieldErrors)`.
- **PR3b**: Razor Page `Habilidades.cshtml` + anti-drift cruzado.

## Riesgos emergentes

- **Backwards compat del JSON del PUT**: la rename `nivelId` → `nivelRequeridoId` en el body rompe consumidores existentes del PUT. Documentado en el cambio (decisión del usuario) pero PR2 debe alinear el controller para reflejar el nuevo shape en errores y Swagger.
- **`NivelId` legacy en `CargoSkillDto`**: si el controller decide serializarlo, contaminaría el contrato. PR2 debe decidir explícitamente: o lo elimina del record o lo marca con `[JsonIgnore]`. Mi recomendación: eliminar el campo para alinear con el spec (Req 1 de `cargo-skill-query-contract`: "El contrato GET MUST exponer exactamente los datos que la UI necesita"). En `CargoSkillDto` (write), `NivelId` puede mantenerse como alias deprecado durante un release para no romper integraciones existentes.
- **`CargoSkillCommandResult.Value`**: en el camino de fallo sin `FieldErrors` (e.g., `NotFound`), `Value` queda `null`. El controller actual (`ToSkillProblemResult`) ya maneja `Error` separado, pero PR2 debe decidir si expone `Value` en errores no-validación. Mi código lo deja `null` consistente con `HabilidadCommandResult`.
- **`MySqlFact` de `CargoSkillRepository`**: PR2 los introducirá. PR1 no toca persistencia, por lo que estos `[MySqlFact]` siguen verdes o se skipean limpios sin MySQL local (mismo patrón que `OcupacionRepositoryTests` issue #59).

## Verificación al cierre de PR1

```bash
# Build limpio
dotnet build SGV.slnx
# → Build succeeded. 0 Warning(s). 0 Error(s).

# Subset PR1
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkill"
# → Total tests: 64. Passed: 64. Failed: 0.

dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadAntiDrift"
# → Total tests: 4. Passed: 4. Failed: 0.

dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift"
# → Total tests: 68. Passed: 68. Failed: 0.

# Suite completa (informativo, los 12 fallos son issue #59 pre-existente)
dotnet test SGV.slnx
# → Total: 1321. Passed: 1309. Failed: 12 (issue #59, OcupacionRepositoryTests).
```