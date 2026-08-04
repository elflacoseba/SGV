# Design: Auditoría — Filtros Select para Entidad y Operación (issue #251)

## 1. Resumen del approach

Extensión incremental del módulo de auditoría: se reutilizan el LEFT JOIN vigente con `AspNetUsers` y el patrón de proyección enum-based que ya cierra D-2 por construcción (véase el archivo `archive/2026-07-31-ajustes-listado-auditoria/design.md`). Se introduce un único endpoint read-only `GET /api/v1/auditorias/filter-options` con `DISTINCT` + `AsNoTracking` + `Take(100)` y se renombra el parámetro de filtro `UserId → UserName` (en `AuditoriaListQuery`, controller, cliente y PageModel). La shell web reemplaza los `<input>` de `entityName` y `operation` por `<select>` poblados desde el nuevo endpoint, con fallback no bloqueante a `<input>` si la carga de opciones falla. El riesgo principal es el breaking-change del query-string `userId → userName`: se acota porque el único consumidor del wire es `SGV.Web`, y ambos deployan juntos desde el mismo monomerge. Se domina documentando el rename en el PR y en `docs/decisiones-implementacion.md`, y eliminando el binding legacy en el mismo commit (sin período de compatibility shim).

## 2. Modelo de datos

No hay migración: no toca esquema, columnas ni índices. `Auditorias` y `AspNetUsers` se leen con `AsNoTracking()`.

### `AuditoriaFilterOptions` (nuevo, `src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs`)

```csharp
namespace SGV.Contracts.Auditoria;

/// Wire contract inmutable para poblar los <select> de Entidad y
/// Operación. Por construcción NO porta UserId, UserName, EntityId,
/// OldValuesJson ni NewValuesJson (D-2). Cap duro: 100 elementos
/// por array (el controller recorta; el servicio no excede).
public sealed record AuditoriaFilterOptions(
    IReadOnlyCollection<string> EntityNames,
    IReadOnlyCollection<string> Operations);
```

### `AuditoriaListQuery` (diff: rename de `UserId` → `UserName`)

Único campo que cambia: `string? UserId = null` → `string? UserName = null`. Se conserva el resto de la firma posicional (Compat con clientes por nombre).

## 3. Cambios por capa

| Capa | Archivo | Diff |
|---|---|---|
| Contracts | `src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs` | **Create**: nuevo record. |
| Contracts | `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | **Modify**: rename `UserId → UserName` en `<param>` y parámetro posicional. Actualizar doc-comment ( UserName filtra contra `u.UserName` vía LEFT JOIN). |
| Aplicación | `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | **Modify**: agrega `Task<AuditoriaFilterOptions> GetFilterOptionsAsync(CancellationToken ct = default)`. `QueryAsync` no cambia su firma (el rename vive en `AuditoriaListQuery`). |
| Infraestructura | `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | **Modify**: (a) en `QueryAsync` reemplaza el bloque `if (!string.IsNullOrWhiteSpace(query.UserId)) ... x.a.UserId == userId` por `if (!string.IsNullOrWhiteSpace(query.UserName)) ... x.u != null && x.u.UserName == userName` (short-circuit del LEFT JOIN con guard de `u != null` para no comparar null). (b) Agrega `GetFilterOptionsAsync`: dos subqueries paralelas con `AsNoTracking().Where(...!IsNullOrWhiteSpace).Select(...).Distinct().OrderBy(x => x).Take(100).ToListAsync()`. Devuelve un `AuditoriaFilterOptions` con `EntityNames` y `Operations`. |
| API | `src/SGV.Api/Controllers/AuditoriasController.cs` | **Modify**: agrega `GET /api/v1/auditorias/filter-options` con `[HttpGet("filter-options")]` + los `ProducesResponseType` (200/401/403). El controller invoca `_servicio.GetFilterOptionsAsync(ct)` y devuelve `Ok(dto)` directo. El endpoint ya hereda `[Authorize(Roles = Administrador)]` de la clase. |
| Web Integration | `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | **Modify**: agrega `Task<AuditoriaFilterOptions> GetFilterOptionsAsync(CancellationToken ct = default)`. |
| Web Integration | `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | **Modify**: implementación HTTP `GET {BaseRoute}/filter-options` con `EnsureSuccessStatusCode` + `ReadFromJsonAsync<AuditoriaFilterOptions>`. En `BuildQueryUri` rename del segmento `&userId=` → `&userName=`. |
| Web Pages | `src/SGV.Web/Pages/Auditorias/Index.cshtml` | **Modify**: reemplaza `<input>` de `entityName` y `operation` por `<select asp-for=...>` con `option value=""` "Todos" + `asp-items="Model.EntityNameOptions"` / `Model.OperationOptions`. Renombra `id/name="userId"` → `userName`, placeholder `"user id"` → `"nombre de usuario"`. Cambia `onchange` del `<select class="filter-select">` a `this.form.submit()`. Agrega bloque `@if (Model.FilterOptionsLoadFailed)` que renderiza los `<input>` y un `<div class="alert alert-info">` soft (no `alert-danger`). El envoltorio `.card` ya está presente — sólo se asegura el borde suave conservando `card-header border-0`. |
| Web Pages | `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | **Modify**: (a) `OnGetAsync` renombra param `string? userId` → `string? userName` y prop `UserId` → `UserName`. (b) Antes de invocar `QueryAsync`, llama `GetFilterOptionsAsync` envuelto en `try/catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))` que setea `FilterOptionsLoadFailed = true` y `FilterOptionsMessage` (warning soft). El catch NO lanza, NO corta la carga del listado. (c) Propiedades nuevas: `bool FilterOptionsLoadFailed`, `string? FilterOptionsMessage`, `SelectList? EntityNameOptions`, `SelectList? OperationOptions` (construidos vía `new SelectList(options, selectedValue: UserName|null)`). (d) `BuildPagedRouteValues`/`BuildSortRouteValues`/`BuildDetailsRouteValues` renombran `userId = UserId` → `userName = UserName`. |
| Tests | `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | **Modify**: nuevos tests (ver §7). El `FakeAuditoriaServicioConsulta` privado del archivo agrega `Func<AuditoriaFilterOptions>? FilterOptionsHandler` y `List<object> FilterOptionsCalls`. Los tests existentes que asumían `UserId` en el fake se migran a `UserName`. |
| Tests | `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | **Modify**: el `MySqlTheory` con `userId` se renombra a `userName` y la siembra incluye filas en `AspNetUsers` (los `MakeRow` actuales usan `UserId` string que no existe en Identity → el cambio de filtro rompe el `TotalCount` esperado); se siembran los usuarios Identity necesarios. Se agregan los 5 tests nuevos de §7. La teoría cambia el 6º InlineData de `"u1"` (UserId) a sembrar `AspNetUsers` con `UserName = "u1"`. |
| Tests | `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs` | **Modify**: agrega `AuditoriaFilterOptions? GetFilterOptionsResult`, `Exception? GetFilterOptionsException`, `List<object> GetFilterOptionsCalls` y la implementación del nuevo método del interfaz. |
| Tests | `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` | **Modify**: migración de `userId` → `userName` en asertos de route value; 5 tests nuevos de §7. |

**Invariante D-2 reforzada**: `AuditoriaFilterOptions` es un tipo físico separado sin `OldValuesJson`/`NewValuesJson`/`EntityId`/`UserId`/`UserName`. Closed-by-construction como `AuditoriaDto`.

## 4. Endpoint `filter-options`: detalle técnico

- **Método / ruta / auth**: `GET /api/v1/auditorias/filter-options` con `[HttpGet("filter-options")]`. El atributo de clase `[Authorize(Roles = RolesSgv.Administrador)]` cubre 401/403. No se agrega `[AllowAnonymous]` ni sobrescribe el rol.
- **Query shape EF Core**:
  ```csharp
  var entityNames = await context.Auditorias.AsNoTracking()
      .Where(a => !string.IsNullOrWhiteSpace(a.EntityName))
      .Select(a => a.EntityName)
      .Distinct()
      .OrderBy(n => n)
      .Take(100)
      .ToListAsync(ct);
  // operations idéntico sobre a.Operation
  return new AuditoriaFilterOptions(entityNames, operations);
  ```
  No se hace JOIN con `AspNetUsers` — el endpoint no expone usuario. Reusa el `DbSet<AuditoriaEntity>` ya mapeado.
- **Orden**: `OrderBy(n => n)` lexicográfico cliente-servidor (Pomelo lo emite como `ORDER BY ... ASC` sobre `utf8mb4_0900_ai_ci` → case-insensitive, útil para que `Cargo` y `cargo` no dupliquen gracias al `DISTINCT` que opera case-insensitive sobre el collation).
- **Cap 100**: `Take(100)` aplica DESPUÉS de `Distinct().OrderBy(...)`, de modo que los primeros 100 en orden alfabético son los que se devuelven. Si el DISTINCT devuelve ≤100, el array queda tal cual.
- **Strings vacíos**: la cláusula `Where(a => !string.IsNullOrWhiteSpace(a.EntityName))` filtra null/vacíos en la fuente antes del `Distinct`.
- **DTO mapping**: el controller construye el `AuditoriaFilterOptions` directamente con la tupla devuelta por el servicio; no hay capa de mappers.
- **Anti-leak**: el tipo físico sólo tiene `EntityNames` y `Operations` → no hay superficie tipada para `UserId`, `UserName`, `EntityId`, `OldValuesJson`, `NewValuesJson`. Test de guardrail (§7) aserta que el JSON serializado no contiene ninguno de esos nombres.

## 5. Cambio del filtro `UserId → UserName`: detalle técnico

### Diferencia LINQ

```csharp
// ANTES (QueryAsync, líneas 104-108 actuales):
if (!string.IsNullOrWhiteSpace(query.UserId))
{
    var userId = query.UserId;
    origen = origen.Where(x => x.a.UserId == userId);
}

// DESPUÉS:
if (!string.IsNullOrWhiteSpace(query.UserName))
{
    var userName = query.UserName;
    origen = origen.Where(x => x.u != null && x.u.UserName == userName);
}
```

El guard `x.u != null` es imprescindible: la lambda corre sobre el `origen` LEFT JOIN donde `u` puede ser `null` (fila huérfana). Sin el guard, EF emitiría `u.UserName = @p` con `u` null → todas las filas huérfanas se excluyen automáticamente, pero en Pomelo/MySQL existe la semántica de `NULL` en comparación que conviene hacer explícita para legibilidad y para que el test unitario `UserName_Vacio_NoFiltra` sea indiscutible.

### Short-circuit de `userName` vacío

El `if (!string.IsNullOrWhiteSpace(query.UserName))` envuelve la cláusula, así que `userName=null|""|""` no agrega ningún predicado al `IQueryable`. Eso es el escenario "Filtros omitidos no filtran" del spec base `auditoria-query`.

### Sensibilidad a mayúsculas

MySQL 8 / MariaDB con default collation `utf8mb4_0900_ai_ci` (AI = Accent-Insensitive, CI = Case-Insensitive) hace que `==` y `Contains` sobre columnas `VARCHAR` sean case-insensitive por default. El `LEFT JOIN ... u.UserName == userName` se evalúa case-insensitive en runtime, sin `ToLower()`. Precedente en el repo: el sort `usuario_asc`/`usuario_desc` ya ordena por `u.UserName` con case-insensitive (ver `AuditoriaServicioConsulta.QueryAsync_SortUsuarioAsc_OrdenaPorUserName` en el archivo de tests de aplicación vigente), confirmando que el collation por default es el motor de la case-insensitivity. No se introduce `StringComparison` en la lambda (no es soportado en expression trees de EF).

### Caché — nota futura

El `DISTINCT` sobre `Auditorias` sin índice dedicado pasa por un table scan + temp sort. A volumen actual es despreciable; si la tabla crece a órdenes de millones de filas, el costo puede tipificarse. **Fuera de scope**: cacheo en memoria con `IMemoryCache` y TTL corto. Se deja el hook abierto en `IAuditoriaServicioConsulta.GetFilterOptionsAsync` (la interface es el punto natural para implementar sliding expiration futura sin tocar el controller).

## 6. UX: toolbar en `.card`, `<select>` y fallback

### Estructura del `<div class="card">` toolbar

El template actual **ya envuelve** el `<form>` de filtros dentro de `<div class="card-header border-0">` (línea 38 de `Index.cshtml`). No se introduce una segunda `.card`. El cambio visual es:

- Conservar el envoltorio existente; asegurar un borde suave con la clase `border-soft` (estilo estándar Inspinia) en el `card-header` de filtros. Si esa utility no existe en el bundle del shell, dejar `border-0` (default actual) — no inventar CSS nuevo (constraint del proposal: *"do not invent new CSS"*). **Decisión**: mantener `card-header border-0` tal cual; el agrupamiento visual ya está resuelto por el `card-header` dentro del `card` mayor. Lo único nuevo es swap input→select.

### Forma del `<select>` (entityName / operation)

```cshtml
<div class="col-md-4 col-xl-3">
    <label class="form-label fs-xs text-muted mb-1" for="entityName">Entidad</label>
    @if (Model.FilterOptionsLoadFailed)
    {
        <input class="form-control form-control-sm" id="entityName" name="entityName"
               type="search" placeholder="Cargo, Persona, ..." value="@Model.EntityName" />
    }
    else
    {
        <select class="form-select form-select-sm filter-select" id="entityName" name="entityName"
                asp-for="EntityName" asp-items="Model.EntityNameOptions"
                onchange="this.form.submit()">
        </select>
    }
</div>
```

La `SelectList` se construye en `OnGetAsync` con una primera opción `Todos` (`value=""`) para que el usuario pueda limpiar el filtro:

```csharp
EntityNameOptions = new SelectList(
    new[] { "" }.Concat(filterOptions.EntityNames),
    dataValueField: null,  // el string mismo como value
    selectedValue: EntityName ?? "");
// se agrega manualmente `<option value="">Todos</option>` en el cshtml
// antes de asp-items, o se construye SelectList con KeyValuePair.
```

**Decisión de implementación concreta**: construir `SelectList` con `SelectListItem[]` que arranca con `Value="" Text="Todos" Selected=(EntityName is null)`, seguido de los `EntityNames` (lo que preserve "Todos" en cualquier condición).

### `onchange` auto-submit

Se elige `onchange="this.form.submit()"` directamente en el `<select>`. **Decisión documentada**: Razor Pages admin no necesita anti-forgery en `GET` (método `method="get"` ya en el form vigente), por lo que `this.form.submit()` reenvía sin token. Alternativa considerada: handler `addEventListener` inline con `<script>` al pie del partial — descartada por complejidad innecesaria (Open Question §10 resuelta por default). El `pageSize` ya usa este mismo patrón; mantener consistencia.

### Fallback no bloqueante

```cshtml
@if (Model.FilterOptionsLoadFailed)
{
    <div class="alert alert-info alert-soft mt-2" role="alert">
        No se pudieron cargar las opciones de filtros. Ingresá los valores manualmente.
    </div>
}
```

- **Vía `ViewData`**: `FilterOptionsLoadFailed` (bool) + `FilterOptionsMessage` (string) en el PageModel. No `TempData` (no es PRG; es un GET).
- **Round-trip del valor ingresado**: en el modo fallback, el `<input>` recoge `value="@Model.EntityName"` desde el route value `entityName`. En el modo select, el `selectedValue` de la `SelectList` se inicializa con `Model.EntityName` vigente, así que el `<select>` abre en la opción activa.

### Placeholder de usuario

```cshtml
<input class="form-control form-control-sm" id="userName" name="userName"
       type="search" placeholder="nombre de usuario" value="@Model.UserName" />
```

Cambia `placeholder="user id"` → `placeholder="nombre de usuario"` (string UI en español por convención del repo). Cambia `id`/`name` de `userId` → `userName` para alinearse al route value renombrado.

## 7. Tests (Strict TDD)

Cada test se escribe PRIMERO (rojo) y luego se implementa. Clasificación:
- `[Fact]`: unitario sin DB.
- `[MySqlFact]`: requiere MySQL real (skip automático si no hay conexión).

### Tests API (`AuditoriasControllerTests.cs`)

| Test | Tipo | Given/When/Then |
|---|---|---|
| `FilterOptions_Anonimo_Retorna401` | `[Fact]` | GIVEN sin credenciales / WHEN `GET /filter-options` / THEN 401 (auth corre antes del servicio). |
| `FilterOptions_UsuarioSinRol_Retorna403` | `[Fact]` | GIVEN auth sin Administrador / WHEN GET / THEN 403. |
| `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` | `[Fact]` | GIVEN fake con 3 EntityNames (`"B"`, `"A"`, `"A"`) + 2 Operations / WHEN GET / THEN 200 con `entityNames=["A","B"]` y `operations` ordenadas, sin duplicados. |
| `FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName` | `[Fact]` | GIVEN fake cualquiera / WHEN GET / THEN el JSON NO contiene `oldValuesJson`, `newValuesJson`, `entityId`, `userId`, `userName`, `correlationId`, `occurredAt`, `id`. |
| `FilterOptions_DistinctMayorACienDevuelvePrimerosCien` | `[Fact]` | GIVEN fake con 150 EntityNames distintos / WHEN GET / THEN `entityNames.Length == 100` y los primeros 100 lexicográficos. |
| `Listado_UserName_FiltraPorNombreNoPorGuid` | `[Fact]` | GIVEN fake con DTOs cuyo `UserId`="u-42" y `UserName`="jperez" / WHEN `GET /api/v1/auditorias?userName=jperez` / THEN `QueryCalls.Single().UserName == "jperez"` y resultado no vacío. |
| `Listado_UserName_Vacio_NoFiltra` | `[Fact]` | GIVEN fake con 3 DTOs / WHEN `?userName=` / THEN `QueryCalls.Single().UserName == null` (o `""` post-normalize) y `TotalCount == 3`. |

### Tests Aplicación (`AuditoriaServicioConsultaTests.cs`)

| Test | Tipo | Given/When/Then |
|---|---|---|
| `QueryAsync_FiltraPorUserNameCaseInsensitive` | `[MySqlFact]` | GIVEN registro con `UserName="jperez"` en `AspNetUsers` / WHEN `?userName=JPEREZ` y `?userName=jperez` / THEN ambos devuelven la fila. |
| `QueryAsync_FiltroUserNameVacio_NoAplicaFiltro` | `[MySqlFact]` | GIVEN 5 filas / WHEN `UserName=null` / THEN `TotalCount == 5`. |
| `GetFilterOptionsAsync_DevuelveEntityNamesYOperationsOrdenadas` | `[MySqlFact]` | GIVEN filas `"B"`, `"A"`, `"C"` / WHEN GET / THEN `EntityNames == ["A","B","C"]` y `Operations` paralelo. |
| `GetFilterOptionsAsync_DescartaValoresVacios` | `[MySqlFact]` | GIVEN filas con `EntityName = ""` persistidas / WHEN GET / THEN `EntityNames` no contiene `""`. |
| `GetFilterOptionsAsync_AplicaCapDeCien` | `[MySqlFact]` | GIVEN 150 EntityNames distintos / WHEN GET / THEN `EntityNames.Count == 100` y son los primeros en orden. |

El `MySqlTheory` existente con `userId` se migra: el campo pasa a `userName` y se siembran los `SgvIdentityUser` necesarios vía `InsertarUsuarioIdentityAsync` (helper ya presente en el scope).

### Tests Web (`AuditoriasIndexTests.cs`)

| Test | Tipo | Given/When/Then |
|---|---|---|
| `Index_OnGetAsync_CargaFilterOptions` | `[Fact]` | GIVEN fake con `GetFilterOptionsResult` poblado / WHEN GET `/auditorias` / THEN `apiClient.GetFilterOptionsCalls.Count == 1` y página 200. |
| `Index_Renderiza_Selects_ConTodos` | `[Fact]` | GIVEN fake con EntityNames `[A,B]` / WHEN GET / THEN HTML contiene `<select name="entityName"` y `<option value="">Todos</option>` (case-insensitive). |
| `Index_FilterOptionsFalla_FallbackAInputs` | `[Fact]` | GIVEN fake con `GetFilterOptionsException = HttpRequestException` / WHEN GET / THEN HTML contiene `<input name="entityName"` Y `alert-info` soft, NO `alert-danger`; `QueryAsync` del listado sigue siendo invocado y `QueryCalls.Count == 1`. |
| `Index_UserInput_PlaceholderEsNombreDeUsuario` | `[Fact]` | GIVEN fake cualquiera / WHEN GET / THEN HTML contiene `placeholder="nombre de usuario"` en el input de `userName`. |
| `Index_RouteValue_RenombradoUserIdAUserName` | `[Fact]` | GIVEN fake / WHEN GET `/auditorias?p=2&pageSize=20&userName=jperez` / THEN `QueryCalls.Single().UserName == "jperez"` y el HTML del enlace "Siguiente" contiene `userName=jperez`. |

## 8. Riesgos y mitigaciones

| Riesgo | Likelihood | Mitigación |
|---|---|---|
| Breaking-change `userId → userName` en query string | Media | Único consumidor es `SGV.Web`; deployan juntos; documentar en `docs/decisiones-implementacion.md` y en el PR summary. Sin shim (corte limpio). |
| `DISTINCT` sobre `Auditorias` sin índices dedicados | Baja ahora | `AsNoTracking()` + cap 100 por array. Footnote de caché futuro (§5). |
| PageModel rompe si endpoint `filter-options` cae | Baja | Fallback a `<input>` + mensaje soft, NO lanza; `QueryAsync` del listado sigue independientemente. |
| Renombra prop `UserId` de `AuditoriaListQuery` y rompe `FakeAuditoriaServicioConsulta` / `FakeAuditoriaApiClient` | Baja | Migrado en el mismo PR (estrictamente test-first); tests existentes que lean `query.UserId` se reescriben. |
| `.card` wrapper duplicado | Baja | Se detectó durante pre-con: el `card-header border-0` ya existe; no se agrega segunda `.card`. |

## 9. Plan de trabajo tentativo (consumo de `sdd-tasks`)

1. **Fase roja API**: tests nuevos en `AuditoriasControllerTests.cs` (los 7 de §7-API). No compila por falta de `AuditoriaFilterOptions` y de `IAuditoriaServicioConsulta.GetFilterOptionsAsync`.
2. **Fase verde API backend**: crear `AuditoriaFilterOptions`; agregar `GetFilterOptionsAsync` a la interface y a `AuditoriaServicioConsulta`; agregar el handler `[HttpGet("filter-options")]` en el controller; migrar `FakeAuditoriaServicioConsulta` con `FilterOptionsHandler`. Test → verde.
3. **Fase roja Aplicación/Infra**: tests `QueryAsync_*UserName*` + `GetFilterOptionsAsync_*` (los 5 de §7-Aplicación) + migración del `MySqlTheory` existente.
4. **Fase verde Aplicación/Infra**: rename `UserId → UserName` en `AuditoriaListQuery`; ajuste del bloque LINQ en `QueryAsync`; implementación de `GetFilterOptionsAsync` con `Distinct().OrderBy().Take(100)`.
5. **Fase roja Web Integration**: tests de `AuditoriasIndexTests` de §7-Web + migración de asertos `userId u-42 → userName`.
6. **Fase verde Web**: `IAuditoriaApiClient.GetFilterOptionsAsync` + impl HTTP; `FakeAuditoriaApiClient` extendido; `OnGetAsync` con los `try/catch` de fallback; cshtml con `<select>` + rama `@if (Model.FilterOptionsLoadFailed)` y `placeholder="nombre de usuario"`.
7. **Verificación final**: `dotnet build SGV.slnx` + `dotnet test SGV.slnx` + `bun run build` (en `src/SGV.Web`).
8. **sdd-verify**: ejecuta los tests sobre el PR; genera `verify-report.md`.

Cada paso conserva la regla test-first del `strict_tdd: true` del repo.

## 10. Decisiones abiertas / pendientes del usuario

- **Inline `<script>` vs handler `onchange`**: default aplicado (§6) — handler `onchange="this.form.submit()"` directo en el `<select>`, consistente con el `<select>` de `pageSize` vigente. Si el usuario quiere extraer un partial con script para reusabilidad futura, se refactoriza en sdd-tasks.
- **`SelectList` semántica**: default implementación `SelectListItem[]` con primer elemento `Value="" Text="Todos"`, seguido de los `EntityNames`/`Operations` planos. Si el producto prefiere `Value == Text` (sin binding separado), el cambio es trivial.

Ninguna otra decisión queda abierta: el proposal cubre los tradeoffs. `docs/decisiones-implementacion.md` se actualiza en sdd-apply con la entrada "D-8: Filtro `UserId → UserName` y endpoint `filter-options` para selects dinámicos".