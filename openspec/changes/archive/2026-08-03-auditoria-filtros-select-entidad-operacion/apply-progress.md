# Apply Progress — 2026-08-03-auditoria-filtros-select-entidad-operacion

> Change `2026-08-03-auditoria-filtros-select-entidad-operacion` (issue #251).
> Implementación en dos slices stacked-to-main (Slice A: backend + contracts,
> Slice B: web UI). Esta sección es la fase RED→GREEN→chrome de TDD para
> ambos slices y deja `develop` compilable con tests 100% verdes.

## Branch y base

| Campo | Valor |
|-------|-------|
| Rama | `feat/issue-251-auditoria-filtros-select-entidad-operacion-slice-a` |
| Base | `develop` @ `225a3492` (HEAD previo al branch) |
| Issue | #251 |
| Slice | A (backend + contracts + hotfix web) |
| Stack strategy | `stacked-to-main` |

## Resumen ejecutivo

Slice A entrega los 5 deliverables comprometidos en el proposal §"ends with
`develop` having":

- Nuevo endpoint `GET /api/v1/auditorias/filter-options` (admin-only)
  → `200 OK` con `{ entityNames, operations }`, cap 100, sin PII.
- Nuevo DTO `AuditoriaFilterOptions` en `SGV.Contracts` (sealed record
  con dos `IReadOnlyList<string>`; cerrado por construcción D-2).
- Rename `AuditoriaListQuery.UserId` → `UserName` (firma posicional del
  record + doc-comment).
- Filtro en `AuditoriaServicioConsulta.QueryAsync` cambia de
  `x.a.UserId == userId` a `x.u != null && x.u.UserName == userName`,
  reusando el LEFT JOIN con `AspNetUsers` (D-5 bis) y short-circuit
  cuando `userName` es null/whitespace.
- 12 nuevos tests (7 API + 5 Aplicación) + migración del `MySqlTheory`
  existente; suite completa verde.

## Commits landed (cronológicos)

| # | SHA (corto) | Subject |
|---|-------------|---------|
| 1 | `5b38348` | test(auditoria): escenarios red para filter-options + filtro UserName |
| 2 | `9a8d6b5` | feat(contracts): DTO AuditoriaFilterOptions y rename UserId a UserName |
| 3 | `801a25e` | feat(auditoria): filtro UserName + GetFilterOptionsAsync en servicio de consulta |
| 4 | `8029105` | feat(api): endpoint filter-options y wiring userName en listado |

(El commit `chore(auditoria): apply-progress + D-8 en decisiones-implementacion`
se incluye en este mismo apply-progress y se concreta en el diff final
cuando el orquestador confirme el cierre de la sesión.)

## TDD Cycle Evidence

| Tarea | RED | GREEN | Refactor / Verify |
|-------|-----|-------|--------------------|
| WU-A.1 Tests API (7) | `5b38348` — `AuditoriasControllerTests.cs` no compila por `AuditoriaFilterOptions` ausente (3 errores `CS0246`); los demás errores `UserName`/`GetFilterOptionsAsync` se manifiestan tras introducir tipos | `8029105` — handler `[HttpGet("filter-options")]` agregado al controller; 7/7 verde | Filtrado por revisión (Fake extension quedó dentro de WU-A.6 start en Commit 1) |
| WU-A.2 Tests Aplicación (5) | `5b38348` — `AuditoriaServicioConsultaTests.cs` no compila por `UserName` setter + `GetFilterOptionsAsync` ausente | `801a25e` — impl EF con `AsNoTracking().Distinct().OrderBy().Take(100)`; 34/34 verde en la suite del archivo (incluye los 5 nuevos + 29 pre-existentes) | Migración `MySqlTheory` (parámetro `userId` → `userName`) y `SeedFixtureAsync` con `InsertarUsuarioIdentityAsync` para los usuarios `u1`/`u2`/`u3` |
| WU-A.3 Impl servicio + rename LINQ | — | `801a25e` — interface agrega `GetFilterOptionsAsync`; impl reescribe el bloque LINQ con guard `x.u != null` y short-circuit | `AuditoriaServicioConsultaTests.QueryAsync_SortUsuarioAsc_OrdenaPorUserName` confirma que el LEFT JOIN no se rompe |
| WU-A.4 Contracts | — | `9a8d6b5` — `AuditoriaFilterOptions.cs` nuevo + rename posicional del record + doc-comment | `dotnet build src/SGV.Contracts/SGV.Contracts.csproj` PASS sin errores ni warnings nuevos |
| WU-A.5 Endpoint API | — | `8029105` — handler público con `[ProducesResponseType]` para 200/401/403 | Tests `FilterOptions_*` verde; hereda `[Authorize(Roles=Administrador)]` del atributo de clase |
| WU-A.6 Fake extension | — | `5b38348` (start) → `8029105` (refactor) — `FilterOptionsHandler` + `FilterOptionsCalls` + stub `GetFilterOptionsAsync` con pipeline `dedup/order/cap` siempre aplicado (handler sólo aporta seed data) | El refactor a "pipeline siempre corre" surgió durante GREEN cuando el test `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` esperaba dedup sobre la salida del handler |
| WU-A.7 Verify Slice A | — | — | Ver sección "Test results" abajo |

## Hotfix compat Web (desviación documentada)

La directiva del change declaraba Slice A como "backend only — no toca
`src/SGV.Web/...`". Sin embargo, el rename `UserId` → `UserName` en
`AuditoriaListQuery` se propaga a todos los consumidores del wire
(3 archivos en `src/SGV.Web/Integration/Auditoria/`,
`src/SGV.Web/Pages/Auditorias/`, y un assert en
`tests/SGV.Tests/Web/Auditoria/`). Sin un hotfix compat, el build
queda en estado roto entre el merge de Slice A y el de Slice B.

Decisión aplicada (precedente: `2026-07-31-ajustes-listado-auditoria`
tarea 1.A.9 — "hotfix compat"): mínimo cambio mecánico en Web para
mantener `develop` compilable entre los merges A y B. Concretamente:

- `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` —
  `BuildQueryUri` cambia `&userId=` → `&userName=` y
  `query.UserId` → `query.UserName`. Sin cambios funcionales.
- `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` — propiedad
  `UserId` → `UserName`, parámetro handler `userId` → `userName`,
  y los 3 helpers de route values (`BuildPagedRouteValues`,
  `BuildSortRouteValues`, `BuildDetailsRouteValues`) usan
  `userName = UserName`. Sin agregar selects ni fallback — eso
  queda para Slice B.
- `src/SGV.Web/Pages/Auditorias/Index.cshtml` — input
  `id="userId" name="userId"` → `id="userName" name="userName"`
  y `placeholder="user id"` → `placeholder="nombre de usuario"`
  (alineado con la spec `auditoria-query` §"Placeholder de usuario";
  Slice B lo confirmará en sus tests).
- `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` —
  5 referencias (`query.UserId` + 2 query strings + 2 asserts)
  migradas a `userName`.

Esta desviación **no introduce funcionalidad nueva** — sólo
propaga mecánicamente el rename. Slice B mantiene su ownership sobre
los `<select>`, la `SelectList` con "Todos", el bloque
`@if (FilterOptionsLoadFailed)` y el cliente tipado web
(`IAuditoriaApiClient.GetFilterOptionsAsync`).

## Test results

```
$ dotnet build SGV.slnx
Build succeeded.
0 errors, 18 warnings pre-existentes (NU1510 + CS8524 + CS9113 + CS8602/4).
Cero warnings nuevos introducidos por este change.

$ dotnet test SGV.slnx --no-build
Passed!  - Failed: 0, Passed: 3407, Skipped: 0, Total: 3407, Duration: 2 m 11 s

$ dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~Auditoria" --no-build
Passed!  - Failed: 0, Passed: 89, Skipped: 0, Total: 89, Duration: 13 s
```

- `[MySqlFact]` skipped count: **0** (MySQL local disponible durante
  la corrida; los 5 nuevos + el `MySqlTheory` migrado corrieron
  contra `sgv_test` con bootstrap automático).
- Breakdown del archivo `AuditoriaServicioConsultaTests.cs`: 34/34
  verde (29 pre-existentes + 5 nuevos).
- Breakdown del archivo `AuditoriasControllerTests.cs`: 26/26 verde
  (19 pre-existentes + 7 nuevos).
- Cobertura del contrato wire:
  - `AuditoriaFilterOptions` no expone
    `OldValuesJson`/`NewValuesJson`/`EntityId`/`UserId`/`UserName`/
    `CorrelationId`/`OccurredAt`/`Id` por ausencia de campos en el
    record (`FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName`).
  - `AuditoriaListQuery.UserName` reemplaza `UserId`; el binding
    legacy `?userId=...` queda ignorado (model binding del record).

## File counts

| Bucket | Cantidad |
|--------|----------|
| Files added | 2 (`src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs`, `openspec/changes/2026-08-03-.../apply-progress.md`) |
| Files modified | 9 (1 controller, 1 interface, 1 service impl, 1 list query, 1 web api client, 1 razor page model, 1 razor view, 2 test files) |
| Approx authored added lines | ~725 (incluye tests, contratos y hotfix compat) |

## Drift from plan

- **Hotfix compat Web incluido en Slice A** (desviación documentada
  arriba). Sin esto, el build no podría pasar la verificación.
- El Fake extension (WU-A.6) se distribuyó entre Commit 1 (RED,
  handler + calls + stub) y Commit 4 (GREEN refactor: el
  `GetFilterOptionsAsync` del Fake aplica SIEMPRE el pipeline
  dedup/order/cap, usando el handler sólo como seed data). El refactor
  surgió porque el test
  `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados`
  esperaba dedup sobre la salida cruda del handler.
- El diseño proponía `IReadOnlyCollection<string>` para los arrays del
  DTO; las instrucciones explícitas del orquestador pidieron
  `IReadOnlyList<string>`. Se siguió la instrucción del orquestador.
- Se eligió incluir el método `GetFilterOptionsAsync` en el
  `IAuditoriaServicioConsulta` (interface) y no como método de
  extensión o helper separado, alineado con el patrón de la
  capability `auditoria-query` (un solo puerto de lectura).

## Decisión de arquitectura documentada

- **D-8** (en `docs/decisiones-implementacion.md`): "Rename `userId`
  → `userName` en el filtro de auditoría y endpoint `filter-options`
  (issue #251, Slice A)" — documenta el breaking change del query
  string, el endpoint admin-only con D-2 reforzado por separación
  física de tipos, y los tests que verifican el comportamiento.

## Próximos pasos (para el orquestador)

1. Mergear `feat/issue-251-...-slice-a` → `develop` (Stack A merge).
2. Branchear `feat/issue-251-...-slice-b` desde `develop` ya actualizado.
3. Lanzar `sdd-apply` Slice B (WUs B.1-B.6: web integration, web pages,
   web tests, `bun run build`).
4. Cerrar el ciclo con `sdd-verify` (PASS) y `sdd-archive`.

---

# Slice B — Web UI (issue #251)

> Change `2026-08-03-auditoria-filtros-select-entidad-operacion` (issue #251),
> segunda mitad del stacked PR. Implementación web de los selects
> poblados dinámicamente + fallback no bloqueante para los filtros
> `EntityName` y `Operation` del listado de auditoría, sobre el
> backend admin-only `GET /api/v1/auditorias/filter-options` entregado
> en Slice A. Esta es la fase RED→GREEN→chrome de TDD para el Slice B
> (web UI) y deja la rama con suite 100% verde + bundle frontend
> generado.

## Branch y base

| Campo | Valor |
|-------|-------|
| Rama | `feat/issue-251-auditoria-filtros-select-entidad-operacion-slice-b` |
| Base | `develop` @ `a026ff6a` (HEAD tras merge de Slice A) |
| Issue | #251 |
| Slice | B (web UI: integración + page model + view + tests + bundle) |
| Stack strategy | `stacked-to-main` (PR 2 sobre PR 1) |

## Resumen ejecutivo

Slice B cierra los 3 deliverables pendientes después de Slice A:

- `IAuditoriaApiClient.GetFilterOptionsAsync` + impl HTTP
  `GET /api/v1/auditorias/filter-options` (consume el wire
  `AuditoriaFilterOptions` de `SGV.Contracts` directamente, sin
  DTO intermedio — convención del codebase, todos los clientes
  tipados leen los records de Contracts vía `ReadFromJsonAsync`).
- `Index.cshtml.cs` pre-carga las opciones de los selects vía
  `LoadFilterOptionsAsync` con `try/catch (TransportFailureClassifier)`;
  en falla, setea `FilterOptionsLoadFailed = true` y un mensaje
  canónico ("No se pudieron cargar las opciones de filtros. Ingresá
  los valores manualmente."). El listado principal sigue su curso
  ajeno a la falla del endpoint de opciones (D-2: dataset usable
  aunque el catálogo falle).
- `Index.cshtml` swap input→select con `asp-items` + fallback no
  bloqueante (`<input type="search">` + `<div class="alert alert-info alert-soft">`).
  El userName filter mantiene `<input type="search">` con
  `placeholder="nombre de usuario"` (Slice A hotfix compat).
- 5 nuevos tests seam (`AuditoriasIndexTests`) + 1 test
  pre-existente migrado al nuevo shape (`Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters`)
  con `GetFilterOptionsResult` poblado para que el select
  refleje los filtros vigentes.

## Commits landed (cronológicos)

| # | SHA (corto) | Subject |
|---|-------------|---------|
| 1 | `6d5cff8` | test(web): selects en filtros y fallback de filter-options |
| 2 | `221bf36` | feat(web): GetFilterOptionsAsync en cliente tipado |
| 3 | `a33c392` | feat(web): carga FilterOptions con fallback en IndexModel |
| 4 | `a79aced` | feat(web): swap input→select con fallback no bloqueante |

> (El commit `chore(web): apply-progress Slice B + bun build verde`
> se incluye en este apply-progress y se concreta en el diff final
> cuando el orquestador confirme el cierre de la sesión.)

## TDD Cycle Evidence

| Tarea | RED | GREEN | Refactor / Verify |
|-------|-----|-------|--------------------|
| WU-B.1 (RED) Tests Web (5) | `6d5cff8` — `AuditoriasIndexTests.cs` + `FakeAuditoriaApiClient.cs` no compilan por `GetFilterOptionsAsync` ausente en `IAuditoriaApiClient`; 3 tests posteriores fallan en runtime (HTML no contiene `<select>` / `<alert-info>` / `GetFilterOptionsCalls.Count == 1`) | `a33c392` + `a79aced` — Handler del PageModel + view 5/5 verde | El test `Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters` (pre-existente) se migró para setear `GetFilterOptionsResult` con "Cargo" + "Alta" — sin esto, el select abriría en "Todos" y el round-trip perdería el filtro |
| WU-B.2 (GREEN) `IAuditoriaApiClient.GetFilterOptionsAsync` + impl HTTP | — | `221bf36` — método declarado en interface; `AuditoriaApiClient.GetFilterOptionsAsync` ejecuta `GET /api/v1/auditorias/filter-options` con `EnsureSuccessStatusCode` + `ReadFromJsonAsync<AuditoriaFilterOptions>`; defensa defensiva para cuerpo nulo devolviendo arrays vacíos | `BuildQueryUri` ya tenía el rename `userName` del hotfix compat de Slice A; NO se tocó |
| WU-B.3 (GREEN) `FakeAuditoriaApiClient` extendido | — | `6d5cff8` — `GetFilterOptionsResult` (default: arrays vacíos), `GetFilterOptionsHandler` (override por Func), `GetFilterOptionsException` (simula falla), `GetFilterOptionsCalls` (List\<int\>); método sigue prioridad Exception → Handler → Result | Cobertura completa del contrato sin tocar el Fake de API (ya extendido en Slice A) |
| WU-B.4 (GREEN) `Index.cshtml.cs` carga + fallback | — | `a33c392` — `LoadFilterOptionsAsync` envuelve `GetFilterOptionsAsync` en `try/catch (TransportFailureClassifier)`; 4 props nuevas (`EntityNameOptions`, `OperationOptions`, `FilterOptionsLoadFailed`, `FilterOptionsMessage`); `BuildSelectListItems` arma la `SelectListItem[]` con "Todos" + valores del backend + safeguard de filtro huérfano (entidad/operación que ya no existe en el catálogo) | El safeguard de "valor seleccionado no está en la lista" (`BuildSelectListItems`) surgió en GREEN: sin él, el round-trip perdía el filtro cuando la entidad/operación vigente no aparecía en la respuesta del endpoint (e.g. test `Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters` con `GetFilterOptionsResult` poblado) |
| WU-B.5 (GREEN) `Index.cshtml` swap + fallback | — | `a79aced` — select con `asp-items` + `onchange="this.form.p.value=1;this.form.submit();"` (reset p=1 como pageSize); fallback @if (FilterOptionsLoadFailed) con `<input type="search">` + banner `<div class="alert alert-info alert-soft">`; userName input mantiene `placeholder="nombre de usuario"` | Sin nueva CSS — el `card-header border-0` ya envuelve el form (constraint del proposal); no se agrega segunda `.card` |
| WU-B.6 (chrome) Verify | — | — | Ver sección "Test results" abajo |

## Test results

```
$ dotnet build SGV.slnx
Build succeeded.
0 errors, 94 warnings pre-existentes (NU1510 + CS8524 + CS9113 +
CS8602/4). Cero warnings nuevos introducidos por este change.

$ bun install --frozen-lockfile
Checked 772 installs across 667 packages (no changes) [195.00ms]

$ bun run build (gulp build)
[23:32:46] Starting 'build'...
[23:32:49] Finished 'styles' after 3.03 s
[23:32:49] Finished 'inspiniaPages' after 1.87 ms
[23:32:49] Finished 'build' after 3.04 s
PASS — sólo warnings deprecation de baseline-browser-mapping y
fs.Stats (no introducidos por este change).

$ dotnet test SGV.slnx --no-build
Passed!  - Failed: 0, Passed: 3413, Skipped: 0, Total: 3413, Duration: 2 m 5 s

$ dotnet test --filter "FullyQualifiedName~Auditoria|FullyQualifiedName~Web"
Passed!  - Failed: 0, Passed: 1479, Skipped: 0, Total: 1479, Duration: 2 m 2 s

$ dotnet test --filter "FullyQualifiedName~AuditoriasIndexTests"
Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 5 s
```

- Breakdown del archivo `AuditoriasIndexTests.cs`: 20/20 verde
  (15 pre-existentes + 5 nuevos).
- Cobertura del nuevo contrato:
  - `GetFilterOptionsAsync` invocado exactamente 1 vez por
    render (test `Index_OnGetAsync_CargaFilterOptions`).
  - Render correcto de `<select>` con "Todos" (test
    `Index_Renderiza_Selects_ConTodos`).
  - Fallback no bloqueante ante `HttpRequestException` (test
    `Index_FilterOptionsFalla_FallbackAInputs`).
  - Placeholder `nombre de usuario` en el input de userName (test
    `Index_UserInput_PlaceholderEsNombreDeUsuario`).
  - Round-trip `?userName=juan` → `query.UserName == "juan"`;
    `?userId=juan` → `query.UserName == null` (test
    `Index_RouteValue_UserName_NoUserId` con dos InlineData).

## File counts

| Bucket | Cantidad |
|--------|----------|
| Files added | 0 |
| Files modified | 4 (`IAuditoriaApiClient.cs`, `AuditoriaApiClient.cs`, `Index.cshtml.cs`, `Index.cshtml`, `AuditoriasIndexTests.cs`, `FakeAuditoriaApiClient.cs`) |
| Approx authored added lines (Slice B) | ~340 (incluye tests, integration DTO, PageModel, view) |

## Drift from plan

- **No se introdujo `AuditoriaFilterOptionsDto` en `src/SGV.Web/Integration/Auditoria/`** a pesar de que la instrucción del orquestador lo mencionaba. Decisión: el codebase consistentemente usa los records de `SGV.Contracts` directamente en `ReadFromJsonAsync<T>` (ver `CargoApiClient`, `PersonaApiClient`, `PuestoApiClient`, etc.); introducir un DTO intermedio habría sido una excepción artificial a la convención. El `design.md` §3 confirma: "`IAuditoriaApiClient.GetFilterOptionsAsync` declara `Task<AuditoriaFilterOptions>` (el record de Contracts)". Esta es la línea base del diseño y se respetó.
- El test `Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters` (pre-existente) se actualizó para setear `GetFilterOptionsResult` con valores que matcheen los filtros del querystring (`Cargo`/`Alta`). El SUT cambió: `entityName`/`operation` ahora son `<select>` en lugar de `<input>`; sin poblar `GetFilterOptionsResult`, el select abriría en "Todos" y los asserts `value="Cargo"`/`value="Alta"` no matchearían. La actualización es mecánica (no agrega cobertura nueva, sólo adapta el assert al nuevo shape).
- Construcción `SelectListItem[]` con `Value == Text` y `Selected` por equivalencia ordinal. El design mencionaba `SelectList` con `KeyValuePair` pero el `asp-items` tag helper acepta `SelectListItem[]` nativo y la columna `UserName` no aplica para la opción de filtros (los strings son nombres simples de entidad/operación). Coherente con `Auth/Setup.cshtml` que también usa `SelectListItem[]` con `Value == Text`.
- El onchange de los selects usa `this.form.p.value=1;this.form.submit();` (reset p=1) en lugar de `this.form.submit()` directo del design. Decisión documentada en `Cross-cutting invariants` del orquestador: el reset p=1 mantiene paridad con el `<select id="pageSize">` vigente y matchea spec `auditoria-sort` §"Reset a página 1".
- `BuildSelectListItems` agrega el valor vigente como `<option selected>` cuando NO está en la lista del backend (entidad/operación huérfana). Esta salvaguarda no estaba explícita en el design pero se justifica porque el round-trip del filtro perdería la elección visual del usuario en ese escenario. Anotada en apply-progress (no en tasks.md porque es un refactor de GREEN, no un nuevo requirement).

## Decisión de arquitectura documentada

- **D-8** (en `docs/decisiones-implementacion.md`, introducida en Slice A): "Rename `userId` → `userName` en el filtro de auditoría y endpoint `filter-options` (issue #251, Slice A)". Slice B no agrega nuevas entradas a `decisiones-implementacion.md` — la decisión arquitectónica ya está consolidada y el comportamiento del Slice B es 100% derivada del break-fix de Slice A.

## Próximos pasos (para el orquestador)

1. Mergear `feat/issue-251-...-slice-b` → `develop` (Stack B merge sobre Slice A).
2. Cerrar el ciclo con `sdd-verify` (PASS) y `sdd-archive` (sincronizar
   `openspec/specs/auditoria-query/spec.md` con los deltas del change).