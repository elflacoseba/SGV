# Apply Progress — 2026-08-03-auditoria-filtros-select-entidad-operacion (Slice A)

> Change `2026-08-03-auditoria-filtros-select-entidad-operacion` (issue #251).
> Implementación backend + contracts + hotfix compat web de la primera mitad
> del stacked PR. Esta es la fase RED→GREEN→chrome de TDD para el Slice A
> (backend) y deja `develop` compilable con tests 100% verdes.

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