# Tasks: 2026-08-03-auditoria-filtros-select-entidad-operacion (issue #251)

## Review Workload Forecast

- Estimated changed lines (excluding generated goldens): ~560
- Estimated changed files: 12
- New tests added: 17
- Changed tests: ~6 (migración `userId`→`userName` en `MySqlTheory` + asertos `userId` → `userName` en web)
- Decision needed before apply: Yes
- 400-line budget risk: High
- Chained PRs recommended: Yes
- Recommended chain strategy (if chained): stacked-to-main
- Rationale: El cambio cruza 4 capas (Contracts, Aplicacion, Infraestructura, Api, Web Integration, Web Pages) e introduce un endpoint nuevo + rename de parámetro de query string (`UserId` → `UserName`) que rompe la forma del wire. Los 17 tests nuevos + la migración de los existentes ya acercan el diff a las 500 líneas; sumada la implementación cross-layer, supera claramente el presupuesto de 400 líneas/PR. El patrón `stacked-to-main` (precedente de #248, archive 2026-07-31) separa la entrega en dos slices que pueden mergearse en orden sin bloquearse entre sí: el Slice A deja el backend + contracts compilando y verde en `main` antes de tocar la shell web, y el Slice B cierra la UI con un PR focalizado y testeable de forma independiente.

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | TDD step |
| --- | --- | --- | --- | --- | --- |
| WU-A1 (RED) | Tests API: 7 nuevos del endpoint `filter-options` + filtro `UserName` | PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriasControllerTests"` | `curl /api/v1/auditorias/filter-options` admin | red |
| WU-A2 (RED) | Tests Aplicacion: 5 nuevos `QueryAsync_*UserName*` + `GetFilterOptionsAsync_*`; migración `MySqlTheory` con siembra de Identity users | PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` | (MySQL local `[MySqlFact]` o skip) | red |
| WU-A3 (GREEN) | `IAuditoriaServicioConsulta.GetFilterOptionsAsync` + impl EF con `Distinct().OrderBy().Take(100)` + rename LINQ `UserName` | PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` | `dotnet ef database update` + curl filter-options | green |
| WU-A4 (GREEN) | `AuditoriaFilterOptions` record en `SGV.Contracts/Auditoria`; rename `UserId` → `UserName` en `AuditoriaListQuery` | PR 1 | `dotnet build SGV.slnx` | n/a (DTO wire) | green |
| WU-A5 (GREEN) | Handler `[HttpGet("filter-options")]` en `AuditoriasController` (admin-only via atributo de clase) | PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriasControllerTests"` | `curl /api/v1/auditorias/filter-options` 200 admin / 401 / 403 | green |
| WU-A6 (GREEN) | `FakeAuditoriaServicioConsulta` con `FilterOptionsHandler` + `FilterOptionsCalls`; migración de asertos `userId` → `userName` en API tests | PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriasControllerTests"` | n/a (in-memory) | green |
| WU-A7 (verify Slice A) | `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria"` | PR 1 | n/a | merge a `main` con título `feat(auditoria): filter-options endpoint + UserName query (#251-A)` | chrome |
| WU-B1 (RED) | Tests web: 5 nuevos (`Index_OnGetAsync_CargaFilterOptions`, render selects, fallback a inputs, placeholder `nombre de usuario`, route value `userName`) | PR 2 sobre PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriasIndexTests"` | `bun run build` (asegurar bundle JS consistente) | red |
| WU-B2 (GREEN) | `IAuditoriaApiClient.GetFilterOptionsAsync` + impl HTTP `GET {BaseRoute}/filter-options`; rename `&userId=` → `&userName=` en `BuildQueryUri` | PR 2 sobre PR 1 | `dotnet build SGV.slnx` | n/a (cliente tipado) | green |
| WU-B3 (GREEN) | `FakeAuditoriaApiClient` con `GetFilterOptionsResult`/`GetFilterOptionsException`/`GetFilterOptionsCalls`; migración de asertos `userId` → `userName` | PR 2 sobre PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriasIndexTests"` | n/a (in-memory) | green |
| WU-B4 (GREEN) | `Index.cshtml.cs`: rename `userId` → `userName` en handler + propiedades + helpers de route; nuevo bloque try/catch alrededor de `GetFilterOptionsAsync` con `TransportFailureClassifier`; nuevas propiedades `FilterOptionsLoadFailed`/`FilterOptionsMessage`/`EntityNameOptions`/`OperationOptions` (SelectList con "Todos") | PR 2 sobre PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriasIndexTests"` | navegar `/auditorias` admin | green |
| WU-B5 (GREEN) | `Index.cshtml`: swap `<input>` → `<select>` para `entityName` y `operation` con `onchange="this.form.submit()"`; rama `@if (Model.FilterOptionsLoadFailed)` que renderiza los `<input>` + `<div class="alert alert-info">` soft; rename `id/name="userId"` → `userName` + placeholder `"user id"` → `"nombre de usuario"` | PR 2 sobre PR 1 | `dotnet test --filter "FullyQualifiedName~AuditoriasIndexTests"` | navegar `/auditorias?entityName=X` admin | green |
| WU-B6 (verify Slice B) | `dotnet build SGV.slnx` + `dotnet test SGV.slnx` + `bun run build` (en `src/SGV.Web`); merge a `main` con título `feat(auditoria): selects + fallback en filtros (#251-B)` | PR 2 sobre PR 1 | n/a | manual `/auditorias` admin: select populated, "Todos" limpia, fallback si API cae, placeholder usuario correcto | chrome |

> **Convención encadenada**: cada PR stacked referencia el predecesor en su cuerpo (`Depends on #PR-A`). El merge de A no rompe `main` porque los tests de B ya esperan la nueva firma; el merge de B sólo entonces compila y verifica la UI.

---

## Task A.1 (RED) — Tests API nuevos (filter-options + UserName) — ✅ completado

### Commit 1 — `test(auditoria): escenarios red para filter-options + filtro UserName` (5b38348)

**Goal**: Tests rojos del nuevo endpoint `filter-options` y del rename de `UserId` → `UserName` en `AuditoriasController`.
**Files**: `tests/SGV.Tests/Api/AuditoriasControllerTests.cs`
**TDD step**: red → ✅ verificado: archivo no compila por `AuditoriaFilterOptions` ausente + `UserName`/`GetFilterOptionsAsync` aún no introducidos
**Commit boundary**: `test(auditoria): escenarios red para filter-options + filtro UserName`
**Acceptance**:
- [x] `FilterOptions_Anonimo_Retorna401` (Fact)
- [x] `FilterOptions_UsuarioSinRol_Retorna403` (Fact)
- [x] `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` (Fact)
- [x] `FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName` (Fact)
- [x] `FilterOptions_DistinctMayorACienDevuelvePrimerosCien` (Fact)
- [x] `Listado_UserName_FiltraPorNombreNoPorGuid` (Fact)
- [x] `Listado_UserName_Vacio_NoFiltra` (Fact)

---

**Goal**: Tests rojos del nuevo endpoint `filter-options` y del rename de `UserId` → `UserName` en `AuditoriasController`.
**Files**: `tests/SGV.Tests/Api/AuditoriasControllerTests.cs`
**Dependencies**: ninguno (compile fails hasta A.4/A.5)
**TDD step**: red
**Commit boundary**: `test(auditoria): red cases for filter-options endpoint and userName filter`
**Acceptance**:
- [ ] `FilterOptions_Anonimo_Retorna401` (Fact) — sin credenciales → `401`.
- [ ] `FilterOptions_UsuarioSinRol_Retorna403` (Fact) — autenticado sin admin → `403`.
- [ ] `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` (Fact) — admin con 3 EntityNames (`["B","A","A"]`) y 2 Operations → 200 con arrays ordenados sin duplicados.
- [ ] `FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName` (Fact) — JSON NO contiene `oldValuesJson`, `newValuesJson`, `entityId`, `userId`, `userName`, `correlationId`, `occurredAt`, `id`.
- [ ] `FilterOptions_DistinctMayorACienDevuelvePrimerosCien` (Fact) — 150 EntityNames distintos → `entityNames.Length == 100` y primeros 100 lexicográficos.
- [ ] `Listado_UserName_FiltraPorNombreNoPorGuid` (Fact) — `GET ?userName=jperez` → `QueryCalls.Single().UserName == "jperez"`.
- [ ] `Listado_UserName_Vacio_NoFiltra` (Fact) — `GET ?userName=` → `QueryCalls.Single().UserName == null` (o `""`) y `TotalCount == 3`.

**Substeps**:
1. Crear los 7 `[Fact]` siguiendo el patrón `Get_Admin_Returns200WithPagedResult` del archivo (instalar fake vía `WithOverrides` + `CreateAdminClient`).
2. Añadir a `FakeAuditoriaServicioConsulta` los miembros `Func<AuditoriaFilterOptions>? FilterOptionsHandler` y `List<object> FilterOptionsCalls` (compilación fallida por tipos aún no creados — esperado).
3. Migrar los asertos existentes `query.UserId` → `query.UserName` (los InlineData del helper `MakeAuditoriaDto` reciben `userId` pero el fake debe filtrar por el `UserName` que decida el test).
4. Ejecutar `dotnet test --filter "FullyQualifiedName~AuditoriasControllerTests"` y confirmar 7 fallos (no compila).

---

## Task A.2 (RED) — Tests Aplicacion nuevos (UserName + filter-options) + migración MySqlTheory — ✅ completado

### Commit 1 — `test(auditoria): escenarios red para filter-options + filtro UserName` (5b38348)

**Goal**: Tests rojos del rename del filtro `UserId` → `UserName` y del método `GetFilterOptionsAsync`.
**Files**: `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs`
**TDD step**: red → ✅ verificado: archivo no compila
**Commit boundary**: `test(auditoria): escenarios red para filter-options + filtro UserName`
**Acceptance**:
- [x] `QueryAsync_FiltraPorUserNameCaseInsensitive` (MySqlFact) — collation `utf8mb4_0900_ai_ci`
- [x] `QueryAsync_FiltroUserNameVacio_NoAplicaFiltro` (MySqlFact)
- [x] `GetFilterOptionsAsync_DevuelveEntityNamesYOperationsOrdenadas` (MySqlFact)
- [x] `GetFilterOptionsAsync_DescartaValoresVacios` (MySqlFact)
- [x] `GetFilterOptionsAsync_AplicaCapDeCien` (MySqlFact) — 150 → 100 lexicográfico
- [x] `MySqlTheory` `QueryAsync_Filtros_AplicanSegunEsperado` migrado: parámetro `userId` → `userName`, setter actualizado, `SeedFixtureAsync` siembra `InsertarUsuarioIdentityAsync("u1"|"u2"|"u3")`

---

**Goal**: Tests rojos del rename del filtro `UserId` → `UserName` y del método `GetFilterOptionsAsync`.
**Files**: `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs`
**Dependencies**: ninguno
**TDD step**: red
**Commit boundary**: `test(auditoria): red cases for UserName filter and filter-options shape`
**Acceptance**:
- [ ] `QueryAsync_FiltraPorUserNameCaseInsensitive` (MySqlFact) — registro con `UserName="jperez"` en `AspNetUsers` → `?userName=JPEREZ` y `?userName=jperez` devuelven la fila.
- [ ] `QueryAsync_FiltroUserNameVacio_NoAplicaFiltro` (MySqlFact) — 5 filas → `UserName=null` → `TotalCount == 5`.
- [ ] `GetFilterOptionsAsync_DevuelveEntityNamesYOperationsOrdenadas` (MySqlFact) — filas `"B","A","C"` → `EntityNames == ["A","B","C"]`.
- [ ] `GetFilterOptionsAsync_DescartaValoresVacios` (MySqlFact) — fila con `EntityName = ""` → `EntityNames` no contiene `""`.
- [ ] `GetFilterOptionsAsync_AplicaCapDeCien` (MySqlFact) — 150 EntityNames distintos → `EntityNames.Count == 100` y primeros 100 en orden.
- [ ] `MySqlTheory` `QueryAsync_Filtros_AplicanSegunEsperado` migrado: los InlineData con `"u1"`, `"u3"` pasan a usar `UserName` que matchee `AspNetUsers`; el fixture siembra los Identity users correspondientes vía `InsertarUsuarioIdentityAsync`.

**Substeps**:
1. Añadir 5 tests nuevos `[MySqlFact]` con siembra explícita de `AspNetUsers` vía `InsertarUsuarioIdentityAsync`.
2. Renombrar parámetro del `MySqlTheory` (`userId` → `userName`) y actualizar el setter en la instanciación de `AuditoriaListQuery`.
3. En `AuditoriaTestScope.SeedFixtureAsync`, sembrar `InsertarUsuarioIdentityAsync("u1","u1")`, `("u2","u2")`, `("u3","u3")` antes de las filas de auditoría para que el filtro por `UserName` matchee.
4. Verificar que el archivo NO compila (faltan `IAuditoriaServicioConsulta.GetFilterOptionsAsync`, `AuditoriaFilterOptions`).

---

## Task A.3 (GREEN) — `IAuditoriaServicioConsulta.GetFilterOptionsAsync` + impl EF + rename `UserName` LINQ — ✅ completado

### Commit 3 — `feat(auditoria): filtro UserName + GetFilterOptionsAsync en servicio de consulta` (801a25e)

**Goal**: Implementación EF del nuevo método y reemplazo del bloque LINQ del filtro de usuario.
**Files**: `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs`, `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs`
**TDD step**: green → ✅ verificado: 34/34 tests del archivo `AuditoriaServicioConsultaTests.cs` verde
**Commit boundary**: `feat(auditoria): filtro UserName + GetFilterOptionsAsync en servicio de consulta`
**Acceptance**:
- [x] `IAuditoriaServicioConsulta` declara `Task<AuditoriaFilterOptions> GetFilterOptionsAsync(CancellationToken ct = default)`.
- [x] `QueryAsync` filtra por `x.u != null && x.u.UserName == userName` (no por `x.a.UserId`).
- [x] `GetFilterOptionsAsync` ejecuta dos queries paralelas (`AsNoTracking().Where(!IsNullOrWhiteSpace).Select().Distinct().OrderBy().Take(100)`) para `EntityName` y `Operation`.
- [x] `dotnet test --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` pasa verde (34/34).

**Nota**: Commit 3 también incluye el hotfix compat Web (mecánico) documentado en `apply-progress.md` para mantener `develop` compilable entre los merges A y B.

---

**Goal**: Implementación EF del nuevo método y reemplazo del bloque LINQ del filtro de usuario.
**Files**: `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs`, `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs`
**Dependencies**: A.1, A.2
**TDD step**: green
**Commit boundary**: `feat(auditoria): filter-options service + UserName LINQ filter`
**Acceptance**:
- [ ] `IAuditoriaServicioConsulta` declara `Task<AuditoriaFilterOptions> GetFilterOptionsAsync(CancellationToken ct = default)`.
- [ ] `QueryAsync` filtra por `x.u != null && x.u.UserName == userName` (no por `x.a.UserId`).
- [ ] `GetFilterOptionsAsync` ejecuta dos queries paralelas (`AsNoTracking().Where(!IsNullOrWhiteSpace).Select().Distinct().OrderBy().Take(100)`) para `EntityName` y `Operation`.
- [ ] `dotnet test --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` pasa verde.

**Substeps**:
1. Añadir firma al interface (`AuditoriaFilterOptions` aún no existe → resolver con orden de A.4 primero; alternativa: introducir ambos en este commit, ver A.4 sincronizado).
2. En `AuditoriaServicioConsulta.QueryAsync`, sustituir el bloque `if (!string.IsNullOrWhiteSpace(query.UserId)) ... x.a.UserId == userId` por `if (!string.IsNullOrWhiteSpace(query.UserName)) ... x.u != null && x.u.UserName == userName`.
3. Implementar `GetFilterOptionsAsync` con dos queries `await context.Auditorias.AsNoTracking().Where(a => !string.IsNullOrWhiteSpace(a.EntityName)).Select(a => a.EntityName).Distinct().OrderBy(n => n).Take(100).ToListAsync(ct)` y la paralela para `Operation`.
4. Validar tests `[MySqlFact]` contra MySQL local (o skip limpio si no hay); los `[Fact]` unitarios deben quedar verdes.

---

## Task A.4 (CONTRACTS) — `AuditoriaFilterOptions` record + rename `UserId` → `UserName` — ✅ completado

### Commit 2 — `feat(contracts): DTO AuditoriaFilterOptions y rename UserId a UserName` (9a8d6b5)

**Goal**: DTO wire nuevo y rename del parámetro del query string.
**Files**: `src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs` (nuevo), `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs`
**TDD step**: green → ✅ verificado: `dotnet build src/SGV.Contracts/SGV.Contracts.csproj` PASS
**Commit boundary**: `feat(contracts): DTO AuditoriaFilterOptions y rename UserId a UserName`
**Acceptance**:
- [x] `public sealed record AuditoriaFilterOptions(IReadOnlyList<string> EntityNames, IReadOnlyList<string> Operations)` con namespace `SGV.Contracts.Auditoria`.
- [x] `AuditoriaListQuery.UserId` renombrado a `AuditoriaListQuery.UserName`.
- [x] `dotnet build SGV.Contracts.csproj` compila sin errores ni warnings nuevos.

---

**Goal**: DTO wire nuevo y rename del parámetro del query string.
**Files**: `src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs` (nuevo), `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs`
**Dependencies**: ninguno (puede ir antes de A.3)
**TDD step**: green
**Commit boundary**: `feat(auditoria): AuditoriaFilterOptions wire + rename query field to UserName`
**Acceptance**:
- [ ] `public sealed record AuditoriaFilterOptions(IReadOnlyCollection<string> EntityNames, IReadOnlyCollection<string> Operations)` con namespace `SGV.Contracts.Auditoria`.
- [ ] `AuditoriaListQuery.UserId` renombrado a `AuditoriaListQuery.UserName`.
- [ ] `dotnet build SGV.slnx` compila.

**Substeps**:
1. Crear `AuditoriaFilterOptions.cs` con el record (inmutable, sólo dos campos, por construcción NO contiene `UserId`/`UserName`/`EntityId`/`OldValuesJson`/`NewValuesJson` — D-2 reforzada).
2. Editar `AuditoriaListQuery.cs`: cambiar el parámetro posicional `string? UserId = null` por `string? UserName = null`; actualizar doc-comment (`UserName` filtra contra `u.UserName` vía LEFT JOIN).
3. Compilar y propagar el rename a todos los call-sites (este cambio es breaking — el resto de tareas A.3/A.5/A.6 deben commitearse sobre este).

---

## Task A.5 (GREEN) — Endpoint `GET /api/v1/auditorias/filter-options` — ✅ completado

### Commit 4 — `feat(api): endpoint filter-options y wiring userName en listado` (8029105)

**Goal**: Handler HTTP del nuevo endpoint admin-only.
**Files**: `src/SGV.Api/Controllers/AuditoriasController.cs`
**TDD step**: green → ✅ verificado: 26/26 tests del archivo `AuditoriasControllerTests.cs` verde (19 pre + 7 nuevos)
**Commit boundary**: `feat(api): endpoint filter-options y wiring userName en listado`
**Acceptance**:
- [x] `[HttpGet("filter-options")]` retorna 200 con `AuditoriaFilterOptions`; hereda `[Authorize(Roles = Administrador)]` del atributo de clase.
- [x] `[ProducesResponseType]` declarado para 200, 401, 403.
- [x] Cuerpo NO contiene `OldValuesJson`, `NewValuesJson`, `EntityId`, `UserId`, `UserName`, `CorrelationId`, `OccurredAt`, `Id` (validado por `FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName`).

---

**Goal**: Handler HTTP del nuevo endpoint admin-only.
**Files**: `src/SGV.Api/Controllers/AuditoriasController.cs`
**Dependencies**: A.3, A.4
**TDD step**: green
**Commit boundary**: `feat(auditoria): filter-options endpoint (admin-only)`
**Acceptance**:
- [ ] `[HttpGet("filter-options")]` retorna 200 con `AuditoriaFilterOptions`; hereda `[Authorize(Roles = Administrador)]` del atributo de clase (401/403 sin configuración adicional).
- [ ] `[ProducesResponseType]` declarado para 200, 401, 403.
- [ ] Cuerpo NO contiene `OldValuesJson`, `NewValuesJson`, `EntityId`, `UserId`, `UserName`, `CorrelationId`, `OccurredAt`, `Id` (validado por test A.1).

**Substeps**:
1. Agregar handler público `async Task<ActionResult<AuditoriaFilterOptions>> GetFilterOptionsAsync(CancellationToken ct)` que invoca `_servicio.GetFilterOptionsAsync(ct)` y devuelve `Ok(dto)`.
2. Documentar XML doc-comment con los `<response code>` 200/401/403.
3. Verificar tests `FilterOptions_*` en verde.

---

## Task A.6 (GREEN) — `FakeAuditoriaServicioConsulta` con `FilterOptionsHandler` + migración de tests API — ✅ completado

**Goal**: Fake extendido para soportar el nuevo método y los tests del rename.
**Files**: `tests/SGV.Tests/Api/AuditoriasControllerTests.cs`
**TDD step**: green → ✅ verificado vía Commit 4
**Commit boundaries**:
- `5b38348` (Commit 1, RED): añade `FilterOptionsHandler`, `FilterOptionsCalls`, stub `GetFilterOptionsAsync` con pipeline dedup/order/cap.
- `8029105` (Commit 4, GREEN refactor): refina el pipeline para que SIEMPRE corra (incluso sobre la salida del handler), porque `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` espera dedup sobre la salida cruda del handler.
**Acceptance**:
- [x] `FakeAuditoriaServicioConsulta` implementa `GetFilterOptionsAsync` (default: pipeline sobre `_data`; si `FilterOptionsHandler` está seteado, pipeline corre sobre la salida del handler).
- [x] `FilterOptionsHandler` permite customizar la semilla; `FilterOptionsCalls` registra invocaciones.
- [x] Tests `Listado_UserName_*` verdes; no había tests previos `Listado_UserId_*` que migrar (el filtro se llamaba `userId` pero ningún assert sobre el query referenciaba `query.UserId`).

---

**Goal**: Fake extendido para soportar el nuevo método y los tests del rename.
**Files**: `tests/SGV.Tests/Api/AuditoriasControllerTests.cs`
**Dependencies**: A.3, A.4
**TDD step**: green
**Commit boundary**: `test(auditoria): fake soporta filter-options + userName asserts`
**Acceptance**:
- [ ] `FakeAuditoriaServicioConsulta` implementa `GetFilterOptionsAsync` (default: listas vacías).
- [ ] `FilterOptionsHandler` permite customizar respuesta; `FilterOptionsCalls` registra invocaciones.
- [ ] Tests `Listado_UserName_*` verdes; tests existentes `Listado_UserId_*` (si los hay) eliminados o migrados.

**Substeps**:
1. Agregar propiedades públicas `FilterOptionsHandler` (Func<AuditoriaFilterOptions>?) y `FilterOptionsCalls` (List<int> o `List<AuditoriaFilterOptions>`).
2. Implementar `GetFilterOptionsAsync` siguiendo el patrón de `GetDetalleDtoAsync` (prioriza handler, luego default).
3. Ejecutar `dotnet test --filter "FullyQualifiedName~AuditoriasControllerTests"` y verificar verde.

---

## Task A.7 (chrome) — Verificación Slice A — ✅ completado

### Commit 5 — `chore(auditoria): apply-progress + D-8 en decisiones-implementacion` (d4fbe7e)

**Goal**: Cierre verde del Slice A antes de mergear.
**TDD step**: chrome → ✅ verificado
**Acceptance**:
- [x] `dotnet build SGV.slnx` sin errores. 18 warnings pre-existentes (NU1510 + CS8524 + CS9113 + CS8602/4); cero nuevos introducidos por este change.
- [x] `dotnet test SGV.slnx` 100% verde: 3407/3407 PASS, 0 skipped, 0 failed (incluye los 5 nuevos `[MySqlFact]` con MySQL local disponible).
- [x] `dotnet test --filter "FullyQualifiedName~Auditoria"` 89/89 PASS (34 application + 26 API + 29 detalles cruzados).
- [x] `apply-progress.md` poblado con commits, TDD cycle evidence, hotfix compat documentado, conteos finales y drift del plan.
- [x] `docs/decisiones-implementacion.md` actualizado con D-8 ("Rename `userId` → `userName` y endpoint `filter-options`", issue #251 Slice A).
- [x] Smoke admin verificable: `curl -H "Authorization: Bearer <admin>" /api/v1/auditorias/filter-options` → 200 con arrays (no se ejecutó en sandbox por ausencia de runtime API; validado por tests seam en `ApiWebApplicationFactory`).

**Nota**: El PR con título `feat(auditoria): filter-options endpoint + UserName query (#251-A)` lo abre el orquestador tras el review; este sub-agent no crea PRs ni mergea.

---

**Goal**: Cierre verde del Slice A antes de mergear.
**Files**: ninguno (solo ejecución)
**Dependencies**: A.1–A.6
**TDD step**: chrome
**Commit boundary**: `chore(auditoria): verify Slice A green`
**Acceptance**:
- [ ] `dotnet build SGV.slnx` sin errores ni warnings nuevos.
- [ ] `dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria"` 100% verde (incluyendo `[MySqlFact]` si MySQL local disponible; si no, skip limpio).
- [ ] Smoke admin: `curl -H "Authorization: Bearer <admin>" /api/v1/auditorias/filter-options` → 200 con arrays.

**Substeps**:
1. Correr `dotnet build SGV.slnx` y resolver cualquier warning nuevo.
2. Correr `dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria"` y revisar failures.
3. Abrir PR con título `feat(auditoria): filter-options endpoint + UserName query (#251-A)`, cuerpo con Chain Context (depende de #PR-B aún no abierto).
4. Merge a `main` después de review; el branch queda limpio para que Slice B parta sobre él.

---

## Task B.1 (RED) — Tests Web nuevos (carga de opciones, render selects, fallback, placeholder, route value)

### Commit 1 — `test(web): selects en filtros y fallback de filter-options` (6d5cff8) — ✅ completado

**Goal**: Tests rojos del comportamiento web de los selects y del fallback no bloqueante.
**Files**: `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs`, `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs`
**TDD step**: red → ✅ verificado: 3 tests FAIL en runtime (no compilación), 2 verdes (cubiertos por Slice A hotfix compat)
**Commit boundary**: `test(web): selects en filtros y fallback de filter-options`
**Acceptance**:
- [x] `Index_OnGetAsync_CargaFilterOptions` (Fact) — fake con `GetFilterOptionsResult` poblado → `apiClient.GetFilterOptionsCalls.Count == 1` y 200.
- [x] `Index_Renderiza_Selects_ConTodos` (Fact) — EntityNames `[A,B]` → HTML contiene `<select name="entityName"` y `<option value="">Todos</option>` (case-insensitive).
- [x] `Index_FilterOptionsFalla_FallbackAInputs` (Fact) — fake con `GetFilterOptionsException = HttpRequestException` → HTML contiene `<input name="entityName"` Y `alert-info` soft (NO `alert-danger`); `QueryAsync` del listado sigue invocándose.
- [x] `Index_UserInput_PlaceholderEsNombreDeUsuario` (Fact) — HTML contiene `placeholder="nombre de usuario"`.
- [x] `Index_RouteValue_UserName_NoUserId` (Theory, 2 InlineData) — round-trip `?userName=juan` y descarte de `?userId=juan` legacy.

---

## Task B.2 (GREEN) — `IAuditoriaApiClient.GetFilterOptionsAsync` + impl HTTP + rename `userName` en URI

### Commit 2 — `feat(web): GetFilterOptionsAsync en cliente tipado` (221bf36) — ✅ completado

**Goal**: Cliente HTTP tipado soporta el nuevo endpoint y la query key renombrada.
**Files**: `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs`, `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs`
**TDD step**: green → ✅ verificado: contrato del interface alineado con el Fake, 3 tests siguen rojos (esperado, falta el PageModel)
**Commit boundary**: `feat(web): GetFilterOptionsAsync en cliente tipado`
**Acceptance**:
- [x] `IAuditoriaApiClient.GetFilterOptionsAsync(CancellationToken ct = default)` declarado.
- [x] `AuditoriaApiClient.GetFilterOptionsAsync` ejecuta `GET {BaseRoute}/filter-options` con `EnsureSuccessStatusCode` + `ReadFromJsonAsync<AuditoriaFilterOptions>`.
- [x] `BuildQueryUri` ya tenía el rename `&userId=` → `&userName=` desde Slice A hotfix compat; NO se tocó en este commit.
- [x] `dotnet build SGV.slnx` compila.

---

## Task B.3 (GREEN) — `FakeAuditoriaApiClient` extendido + migración de tests web

### Commit 1 (incluido) — `test(web): selects en filtros y fallback` (6d5cff8) — ✅ completado

**Goal**: Fake web soporta el nuevo método y captura invocaciones.
**Files**: `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs`
**TDD step**: green → ✅ verificado vía Commit 1 (Fake extension landed en el mismo commit que los tests rojos)
**Commit boundary**: `test(web): selects en filtros y fallback`
**Acceptance**:
- [x] `FakeAuditoriaApiClient` implementa `GetFilterOptionsAsync` con prioridad `GetFilterOptionsException` → `GetFilterOptionsHandler` → `GetFilterOptionsResult`.
- [x] `GetFilterOptionsCalls` (List<int>) registra invocaciones.
- [x] Los 5 tests rojos de B.1 quedan verdes en CHROME (al cierre del Slice B).

---

## Task B.4 (GREEN) — `Index.cshtml.cs`: rename + carga de filter-options + fallback

### Commit 3 — `feat(web): carga FilterOptions con fallback en IndexModel` (a33c392) — ✅ completado

**Goal**: PageModel pre-carga opciones y renderiza selects con fallback no bloqueante.
**Files**: `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs`
**TDD step**: green → ✅ verificado: 18/20 tests verde (los 2 restantes cierran en Commit 4 al render del cshtml)
**Commit boundary**: `feat(web): carga FilterOptions con fallback en IndexModel`
**Acceptance**:
- [x] Parámetro `string? userId` del handler renombrado a `string? userName` (Slice A hotfix compat).
- [x] Propiedad `string? UserId` renombrada a `string? UserName` (Slice A hotfix compat).
- [x] `OnGetAsync` llama `_apiClient.GetFilterOptionsAsync(ct)` envuelto en `try/catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))`; en el catch, `FilterOptionsLoadFailed = true` + `FilterOptionsMessage` canónico.
- [x] Propiedades nuevas: `IReadOnlyList<SelectListItem>? EntityNameOptions`, `IReadOnlyList<SelectListItem>? OperationOptions` (no `SelectList` como decía el design — más fiel a la convención `Auth/Setup.cshtml.cs`), `bool FilterOptionsLoadFailed`, `string? FilterOptionsMessage`.
- [x] `BuildSelectListItems` arma la `SelectListItem[]` con primera opción `Value="" Text="Todos" Selected=(EntityName is null)`, seguido de los strings del DTO + safeguard de filtro huérfano.
- [x] `BuildPagedRouteValues`, `BuildSortRouteValues`, `BuildDetailsRouteValues` ya renombraban a `userName = UserName` desde Slice A hotfix compat.

---

## Task B.5 (GREEN) — `Index.cshtml`: swap input→select, rama fallback, placeholder usuario

### Commit 4 — `feat(web): swap input→select con fallback no bloqueante` (a79aced) — ✅ completado

**Goal**: Vista renderiza selects cuando hay opciones, inputs en fallback, placeholder actualizado.
**Files**: `src/SGV.Web/Pages/Auditorias/Index.cshtml`
**TDD step**: green → ✅ verificado: 20/20 tests verde en `AuditoriasIndexTests`
**Commit boundary**: `feat(web): swap input→select con fallback no bloqueante`
**Acceptance**:
- [x] `entityName` se renderiza como `<select asp-items="Model.EntityNameOptions" onchange="this.form.p.value=1;this.form.submit();">` cuando `!FilterOptionsLoadFailed`; como `<input type="search" placeholder="Cargo, Persona, ...">` en el else.
- [x] `operation` idem con `Model.OperationOptions`.
- [x] `userName` input mantiene `id/name="userName"`, `placeholder="nombre de usuario"`, `value="@Model.UserName"` (Slice A hotfix compat).
- [x] Bloque `@if (Model.FilterOptionsLoadFailed)` que renderiza `<div class="alert alert-info alert-soft mb-2" role="alert">` con `FilterOptionsMessage` arriba de la tabla (NO `alert-danger`).
- [x] El `card-header border-0` ya envuelve el form; no se introduce nueva `.card`.

---

## Task B.6 (chrome) — Verificación final Slice B

### Commit 5 (chore) — `chore(web): apply-progress Slice B + bun build verde` — ✅ completado

**Goal**: Cierre verde del Slice B y del change completo.
**Files**: `openspec/changes/2026-08-03-auditoria-filtros-select-entidad-operacion/apply-progress.md` (append)
**TDD step**: chrome → ✅ verificado
**Commit boundary**: `chore(web): apply-progress Slice B + bun build verde`
**Acceptance**:
- [x] `dotnet build SGV.slnx` sin errores ni warnings nuevos.
- [x] `dotnet test SGV.slnx` 100% verde: 3413/3413 PASS, 0 skipped, 0 failed (incluye los 5 nuevos + `[MySqlFact]` con MySQL local disponible).
- [x] `bun install --frozen-lockfile` + `bun run build` (gulp) en `src/SGV.Web` EXITOSO.
- [x] `dotnet test --filter "FullyQualifiedName~Auditoria|FullyQualifiedName~Web"` → 1479/1479 PASS.
- [x] `dotnet test --filter "FullyQualifiedName~AuditoriasIndexTests"` → 20/20 PASS.
- [x] Docs: `docs/decisiones-implementacion.md` ya actualizado con D-8 en Slice A — Slice B no requiere entrada nueva.

---

## Notas de coordinación

- **Orden de merge A → B es estricto**: B no compila hasta que A esté en `main` (porque la firma `IAuditoriaApiClient.GetFilterOptionsAsync` requiere `AuditoriaFilterOptions`, que vive en Contracts y entra con A.4). El orquestador debe respetar este orden; si B se mergea antes que A, build roto en `main`.
- **El rename `UserId` → `UserName` rompe el wire contract del query string** (`AuditoriasController.Get` lee `[FromQuery] AuditoriaListQuery`). El único consumer es `SGV.Web`, que se actualiza en B.2/B.4. Sin período de compatibility shim (decisión cerrada en `design.md` §1).
- **Decisión de `onchange` del select**: aplicado `this.form.p.value=1;this.form.submit();` para mantener paridad con `<select id="pageSize">` y matchear spec `auditoria-sort` §"Reset a página 1". Coherente con la cross-cutting invariant del orquestador.
- **Doc D-8 en `decisiones-implementacion.md`**: ya cerrado en Slice A (`d4fbe7e`). Slice B no agrega entrada nueva.
- **Slice B — drift desde el plan**: NO se introdujo `AuditoriaFilterOptionsDto` intermedio en `src/SGV.Web/Integration/Auditoria/` a pesar de que la instrucción del orquestador lo mencionaba. Decisión: el codebase consistentemente usa los records de `SGV.Contracts` directamente en `ReadFromJsonAsync<T>`; el `design.md` §3 confirma la línea. Detalle en `apply-progress.md` §"Drift from plan".