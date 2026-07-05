# Diseño técnico — `habilidades-navegacion-cargos`

> Espejo del change `cargos-navegacion-habilidades` desde la perspectiva **Habilidad → Cargos**. Todos los artefactos base (exploración, propuesta, 3 delta specs) están en `openspec/changes/habilidades-navegacion-cargos/`. Este diseño no introduce migraciones ni cambios de dominio; es un cambio de lectura/proyección + navegación.

## 1. Contexto

- **Pre-flight**: `interactive`, artifact store `openspec`, `chained_pr_strategy=ask-on-risk`, `review_budget_lines=400`.
- **Entradas leases**: `exploration.md`, `proposal.md`, `specs/habilidad-web-listado-detalle-baja/spec.md`, `specs/habilidad-management/spec.md`, `specs/skill-cargo-query-contract/spec.md`.
- **Espejo de referencia**: `openspec/changes/cargos-navegacion-habilidades/{design,apply-progress}.md` y los contratos vigentes de `CargosController.GetSkills` (`src/SGV.Api/Controllers/CargosController.cs:221-295`).
- **Decisiones locked** (no se re-argumentan en este documento): página `Habilidades/Cargos` readonly; dos CTAs por fila (`Cargo/Details` general, `Cargos/Habilidades` admin-only); segmento `status=activas|eliminadas`; botón de entrada solo en `Habilidades/Index` activas; página accesible a cualquier autenticado; sin migración ni cambios de dominio; PR único salvo `ask-on-risk`.

## 2. Decisiones arquitectónicas

### 2.1 DTO readonly: `SkillCargoDetailDto`

- **Path**: `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/SkillCargoDetailDto.cs`.
- **Forma** (espejo de `CargoSkillDetailDto` en `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs:14-38`):

```csharp
public sealed record SkillCargoDetailDto(
    CargoDto Cargo,
    NivelHabilidadDto Nivel)
{
    public Guid CargoId { get; init; }
    public Guid NivelRequeridoId { get; init; }
    public decimal Ponderacion { get; init; }
    public bool EsObligatoria { get; init; }
}
```

- **Justificación**: usar `record` con un constructor primario `(Cargo, Nivel)` y exponer campos del vínculo como `init` permite que la proyección EF Core use los miembros init sin romper el call site de 2-argumentos. Reusar `CargoDto` (id, Codigo, Nombre, NivelId, NivelNombre) evita duplicar shape; los campos del vínculo cierran el contrato con la página (peso, obligatorio, nivel requerido). Para soportar `status=eliminadas`, añadir un campo derivado `Cargo.IsDeleted` envuelto en una proyección (no expandir `CargoDto` que es compartido).

- **Alternativa descartada**: reciclar `CargoDto` a secas — quedaría sin los campos del vínculo y el binding en la UI perdería `NivelRequeridoId`/`Ponderacion`/`EsObligatoria` que la spec marca como requeridos. Otra opción habría sido exponer un `SkillCargoFullDto` con `CargoHabilidad` adentro, pero duplica info del padre y contamina el contrato.

### 2.2 Query record: `HabilidadCargosListQuery`

- **Path**: `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadCargosListQuery.cs`.

```csharp
public sealed record HabilidadCargosListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    HabilidadSegmentoListado Segmento);
```

- **Justificación**: usar el enum existente `HabilidadSegmentoListado` (`Activas`/`Eliminadas`) garantiza consistencia con el resto del módulo de Habilidades (`SkillsController.GetConsulta` línea 101-103). El record es POJO-like para no contaminar el dominio.

### 2.3 Servicio de consulta nuevo: `ISkillCargoServicioConsulta`

- **Path interfaz**: `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoServicioConsulta.cs`.
- **Path impl**: `src/SGV.Aplicacion/Habilidades/Consultas/SkillCargoServicioConsulta.cs`.

```csharp
public interface ISkillCargoServicioConsulta
{
    Task<PagedResult<SkillCargoDetailDto>> ListarCargosAsync(
        Guid habilidadId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default);
}
```

- **Justificación**: a diferencia del espejo cargo→skills (`ICargoSkillServicio.ListAsync` no paginado — `src/SGV.Aplicacion/Organizacion/Comandos/ICargoSkillServicio.cs:13`), este lado sí se pagina: una habilidad común puede estar en muchos cargos y la página readonly debe soportar búsqueda/orden/filtro; la spec `skill-cargo-query-contract` exige `PagedResult<T>` con `page/pageSize/search/sort/status`. Lo metemos en el namespace `Habilidades` (es consulta desde la perspectiva de la habilidad) y no en `Organizacion` (mantiene simetría conceptual con `IHabilidadServicioConsulta`).

### 2.4 Repositorio nuevo: `ISkillCargoRepository` + impl

- **Path interfaz**: `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoRepository.cs`.
- **Path impl EF**: `src/SGV.Infraestructura/Persistencia/Repositorios/SkillCargoRepository.cs`.

```csharp
public interface ISkillCargoRepository : IReadOnlyRepository<CargoHabilidad>
{
    Task<(IReadOnlyList<SkillCargoDetailDto> Items, int TotalCount)> ListDetailedBySkillIdAsync(
        Guid habilidadId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default);
}
```

- **Justificación**: la firma devuelve `(Items, TotalCount)` ya proyectados al DTO — el servicio no necesita volver a tocar EF Core. `AsNoTracking()` en la impl garantiza solo-lectura. La entidad `CargoHabilidad` ya existe y no se modifica (`src/SGV.Dominio/Habilidades/CargoHabilidad.cs`).
- **Gotcha Pomelo conocida**: el repo no debe `OrderBy` sobre `new SkillCargoDetailDto(...)` directo (proveniente de `CargoDto`/`NivelHabilidadDto` records posicionales) porque Pomelo no traduce esa expresión. Regla de la implementación: ordenar sobre `CargoEntity` o `CargoHabilidadEntity` (campos nativos: `Codigo`, `Nombre`) y proyectar al DTO **después** del orden. Esta es exactamente la decisión que se cerró en `cargos-navegacion-habilidades` (ver `apply-progress.md`).
- **Segmento**: la elección entre `Activas` y `Eliminadas` se materializa vía el estado del `Cargo` (que hereda de `EntidadAuditable.IsDeleted` en `src/SGV.Dominio/Comun/EntidadAuditable.cs:13`). No hay `IsDeleted` en `CargoHabilidad`, así que el filtro aplica a `Cargo`.

### 2.5 Endpoint nuevo: `SkillsController.GetCargosAsync`

- **Path**: `src/SGV.Api/Controllers/SkillsController.cs` (nuevo método, mismo controller).
- **Ruta**: `GET /api/v1/skills/{skillId:guid}/cargos`.
- **Atributos**: `[HttpGet("{skillId:guid}/cargos")]` con `[Authorize]` heredado a nivel de controller (línea 17) — `Get`-only, igual que `GetConsulta`. La `spec skill-cargo-query-contract` exige cualquier autenticado, no admin.

```csharp
[HttpGet("{skillId:guid}/cargos")]
[ProducesResponseType(typeof(PagedResult<SkillCargoDetailDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<PagedResult<SkillCargoDetailDto>>> GetCargos(
    Guid skillId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? sort = null,
    [FromQuery] string? status = null,
    CancellationToken cancellationToken = default)
{
    var habilidad = await _servicio.GetByIdAsync(skillId, cancellationToken);
    if (habilidad is null) return NotFound();

    var normalizedPage = page < 1 ? 1 : page;
    var normalizedPageSize = pageSize < 1 ? 20 : Math.Min(100, pageSize);
    var segmento = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
        ? HabilidadSegmentoListado.Eliminadas
        : HabilidadSegmentoListado.Activas;

    var query = new HabilidadCargosListQuery(normalizedPage, normalizedPageSize, search, sort, segmento);
    var result = await _skillCargoServicio.ListarCargosAsync(skillId, query, cancellationToken);
    return Ok(result);
}
```

- **Justificación**: el controller reusa `_servicio.GetByIdAsync` para distinguir 404 (skill inexistente) vs 200 con lista vacía, exactamente como pide la spec `skill-cargo-query-contract`. La normalización de `page/pageSize/status` ocurre en el controller (no en el record) y es consistente con el patrón `SkillsController.GetConsulta` (líneas 98-105, comentario explícito sobre `CRITICAL-01`).
- **Inyección**: añadir `ISkillCargoServicioConsulta _skillCargoServicio` por constructor al lado de `_servicio`/`_comandos`.
- **Mapeo errores**: 5xx se deja al middleware global de la API; 401 viene del filtro de autorización.

### 2.6 Cliente tipado: `IHabilidadApiClient.GetCargosAsync`

- **Path interfaz**: `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` (línea 51, junto a `QueryAsync`).
- **Path impl**: `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs`.

```csharp
// interfaz
Task<PagedResult<SkillCargoDetailDto>> GetCargosAsync(
    Guid skillId,
    HabilidadCargosListQuery query,
    CancellationToken cancellationToken = default);
```

- **Justificación**: nuevo método siguiendo el patrón `QueryAsync` (`HabilidadApiClient.cs:122-133`) que ya construye un URI con `page`, `pageSize`, `search`, `sort`, `status`. La diferencia: ruta `"{BaseRoute}/{skillId}/cargos"` y el `skillId` viaja como segmento de ruta, no como query.
- **Política de errores** (consistente con el resto del cliente y con la skill `web-apiclient-transport-failure-coverage` ya aplicada en el repo):
  - `200` → `PagedResult<SkillCargoDetailDto>` (vacío si no hay items).
  - `401`/`404` → propagar como excepción / devolver null según el call site de la página; el PageModel decide cómo renderizar (la spec exige distinguir "no existe" vs "vacío", lo abordamos en el PageModel).

### 2.7 Razor Page: `Pages/Organizacion/Habilidades/Cargos.cshtml` + `.cs`

- **Ruta**: `/organizacion/habilidades/{id:guid}/cargos` (espejo de `/organizacion/cargos/{id:guid}/habilidades`).
- **Modelo**: `HabilidadesCargosModel` en `Habilidades/Cargos.cshtml.cs`.
- **PageModel**:

```csharp
public sealed class HabilidadesCargosModel(
    IHabilidadApiClient habilidadApiClient,
    ILogger<HabilidadesCargosModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    public IReadOnlyList<HabilidadCargoListItemViewModel> Items { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int CurrentPage { get; private set; } = 1;
    public int TotalPages { get; private set; }
    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);
    public bool IsDeletedView => string.Equals(Status, "eliminadas", StringComparison.OrdinalIgnoreCase);
    public string? HabilidadNombre { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // 1) Validar skill vía `GetByIdAsync` (404 si no existe).
        // 2) Mapear Status a HabilidadSegmentoListado; fallar a Activas si inválido.
        // 3) Llamar `GetCargosAsync(skillId, query)`.
        // 4) Mapear DTO -> ViewModel; construir TotalPages y CurrentPage.
        // 5) Si el cliente devolviera 404 en el subrecurso pero la skill existe, fallback a collection vacía.
    }
}
```

- **Justificación**:
  - `OnGetAsync` distingue 404 de "vacío": si `GetByIdAsync` devuelve null → `PageRedirect("/Organizacion/Habilidades/Index")` o `NotFound()` (revisar convención del repo — `Habilidades/Details.cshtml.cs` y `Cargos/Details` son el precedente).
  - El binding `BindProperty(SupportsGet = true)` permite leer desde query y se enlaza al helper `BuildCargosRouteValues` del Index.
  - `EsAdministrador` con `User.IsInRole(RolesSgv.Administrador)` reutiliza el helper ya presente en `Pages/Organizacion/Cargos/Habilidades.cshtml.cs` (líneas 122-127 de ese archivo). Eso evita crear un helper nuevo.
- **Vista (`Cargos.cshtml`)**:
  - Header con nombre de la habilidad + breadcrumb "Habilidades / {nombre}".
  - Toggle `Activas|Eliminadas` (`BuildToggleSegmentoRouteValues`, copia del patrón en `Habilidades/Index.cshtml.cs:96-114`).
  - Tabla con columnas: `Código`, `Nombre`, `Nivel`, `Acciones`. Cada acción con dos botones:
    - `ti ti-eye` (info) → `Cargo/Details`. **Siempre visible**.
    - `ti ti-edit` (warning) → `Cargos/Habilidades` (gestión de habilidades del cargo). **Solo si `Model.EsAdministrador`**.
  - Estado vacío: mensaje "No hay cargos asociados en el segmento X."
  - Paginación con preservación de `Search`, `Sort`, `Status`.

### 2.8 Entry point en `Habilidades/Index`

- **Path vista**: `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` (bloque de acciones, entre `Detalle` y `Editar`).
- **Path PageModel**: `Habilidades/Index.cshtml.cs` — añadir helper `BuildCargosRouteValues(id)`:

```csharp
public RouteValueDictionary BuildCargosRouteValues(Guid id) => new()
{
    ["id"] = id,
    ["p"] = CurrentPage,
    ["search"] = Search,
    ["sort"] = Sort,
    ["status"] = Segmento,
};
```

- **Botón**:

```html
@if (!Model.IsDeletedView)
{
    <a class="btn btn-primary btn-icon btn-sm rounded-circle"
       href="@Url.Page("/Organizacion/Habilidades/Cargos", Model.BuildCargosRouteValues(item.Id))"
       data-bs-toggle="tooltip" data-bs-title="Cargos" aria-label="Cargos de @item.Nombre">
        <i class="ti ti-briefcase fs-lg"></i>
    </a>
}
```

- **Justificación**: copia textual del patrón de `Pages/Organizacion/Cargos/Index.cshtml:163-168` (botón de estrellas hacia `Cargos/Habilidades`). Mismo color `btn-primary`, mismo icono redondo, mismo `ti ti-*` para tooltip. Visibilidad limitada a `!IsDeletedView` cumple la spec `habilidad-web-listado-detalle-baja` (la vista eliminadas solo debe mostrar `Reactivar`).

### 2.9 Permisos y gating `Administrador`

- La página `Cargos.cshtml` se monta sin chequeo de rol: la autorización a nivel de controller (`[Authorize]`) cubre la rama autenticada general. Cualquier autenticado puede:
  - Navegar a la página.
  - Ver la tabla de cargos.
  - Hacer click en `Cargo/Details`.
- El botón "Gestionar Habilidades del Cargo" **se renderiza solo si `Model.EsAdministrador`** (helper `User.IsInRole(RolesSgv.Administrador)`). Los no-admin ven la página y la grilla sin ese botón, evitando el `403` que produciría `Cargos/Habilidades`.
- Justificación documentada en `exploration.md` sección "Riesgos y áreas de incertidumbre" — primera viñeta.

### 2.10 Segmentación `activas|eliminadas`

- El PageModel normaliza el query string `status`: si llega `null`, vacío, o cualquier valor que no sea `eliminadas`, cae a `activas` (idéntico a `Habilidades/Index.cshtml.cs:240-246` y al controller `SkillsController.GetConsulta:101-103`).
- Toggle Activas|Eliminadas en la vista usa `BuildToggleSegmentoRouteValues("eliminadas"|null)`.
- Backend (controller `SkillsController.GetCargosAsync`) repite la normalización antes del `HabilidadSegmentoListado`.

## 3. Diagrama de flujo (ASCII)

```
[Browser - Habilidades/Index fila activa]
    |
    | click "Cargos"
    v
GET /organizacion/habilidades/{id}/cargos?p=&search=&sort=&status=
    |
    | PageModel.OnGetAsync
    v
HabilidadApiClient.GetCargosAsync(id, HabilidadCargosListQuery)
    |
    | HttpClient GET
    v
GET /api/v1/skills/{id}/cargos?... (bearer token vía ApiBearerTokenHandler)
    |
    v
SkillsController.GetCargosAsync
    | 1) GetByIdAsync(id) → 404 si null
    | 2) Normaliza page/pageSize/status
    | 3) SkillCargoServicioConsulta.ListarCargosAsync(id, query)
    v
SkillCargoRepository.ListDetailedBySkillIdAsync (EF Core, AsNoTracking)
    |
    v
SQL: SELECT ... FROM CargoHabilidad JOIN Cargo JOIN Habilidad JOIN NivelHabilidad
     WHERE CargoHabilidad.HabilidadId = @id AND Cargo.IsDeleted = ?segmento
     ORDER BY Cargo.Codigo ASC   -- ordena ANTES de proyectar al DTO
     LIMIT @pageSize OFFSET (@page - 1) * @pageSize
```

## 4. Modelo de datos — sin migración

```
Entidad nueva o modificada          Cambio
-----------------                   ------
Habilidad                           sin cambios
Cargo                               sin cambios
CargoHabilidad (join)               sin cambios
NivelHabilidad                      sin cambios
NivelCargo                          sin cambios
```

Se confirmó en `src/SGV.Dominio/Habilidades/CargoHabilidad.cs:12-40` y `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs:11-34` que la unicidad `{CargoId, HabilidadId}` y check `Ponderacion > 0` siguen siendo el contrato vigente y NO se modifican. El nuevo subrecurso **solo lee** del join y de las tablas relacionadas.

## 5. Plan de tests

Principio rector del repo (`AGENTS.md`): "calidad > cantidad", tests significativos, sin redundancia trivial.

### 5.1 API — `tests/SGV.Tests/Api/HabilidadesCargosControllerTests.cs`

Cubre el controller nuevo. Mínimo:
- **200 paginado con datos**: 3 cargos asociados en `activas`, `page=1`, `pageSize=20` → `Items.Length == 3`, `TotalCount == 3`, `Page == 1`, `PageSize == 20`. Cada item con `Cargo.Codigo`, `Cargo.Nombre`, `Nivel`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria`.
- **200 vacío**: habilidad existente sin cargos → `Items.Length == 0`, `TotalCount == 0`.
- **404 skill inexistente**: con Guid.NewGuid() → `404`.
- **401 sin token**: con WebApplicationFactory sin `AddBearerToken()` → `401`.
- **Status inválido cae a activas**: `?status=archivo` → `200` con datos de activas, NO `400`.
- **Paginación**: 12 cargos totales, `pageSize=5`, `page=2` → 5 items, `TotalCount == 12`, `Page == 2`.
- **Sort**: `?sort=codigo_desc` → primer item con `Codigo` mayor que el segundo.
- **Filtro eliminadas**: crear cargo soft-deleted + un cargo activo + una asociación al skill → `?status=eliminadas` devuelve solo el cargo soft-deleted.

Tests con `WebApplicationFactory<Program>` + token; usan `TestSgvDbContextFactory` cuando aplique (ver gotcha MySQL abajo).

### 5.2 Aplicación/Repositorio — tests de query

**Importante**: hoy `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` tiene 12 tests `[MySqlFact]` caídos por un bug conocido en la migración inicial (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`) — issue #59. **Los nuevos tests de `SkillCargoRepository` deben usar `UseInMemoryDatabase` o el harness equivalente** que ya exista en el repo, no `[MySqlFact]`, hasta que ese issue se cierre. Si no hay harness InMemory maduro, los tests del repositorio se cubren por medio de los tests del controller (5.1) y los del PageModel (5.3) — sin tests redundantes.

Cubre como mínimo:
- **Repository**: filtro por segmento (activas/eliminadas), orden, paginación.
- **Servicio**: contrato de `ListarCargosAsync` (devuelve lo que el repo produce, sin lógica adicional).

### 5.3 Web — tests Razor

- **Nuevo**: `tests/SGV.Tests/Web/Habilidad/HabilidadesCargosModelTests.cs`.
  - `OnGetAsync` con skill existente + 2 cargos → renderiza `Items.Length == 2`, `TotalPages == 1`, `EsAdministrador` consistente con el usuario del test.
  - `OnGetAsync` con skill inexistente → `NotFound` o redirect a Index (según convención final del repo).
  - `OnGetAsync` con `status=archivo` → resuelve a `activas` (verifica que el PageModel lo normaliza).
  - Verifica que `EsAdministrador` se mapea correctamente desde `User.IsInRole` para 3 escenarios: Administrador=true, Usuario-no-admin=false, anónimo=sin-pruebas-sin-autenticarse.
- **Extender**: `tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPageTests.cs` (línea 44-76).
  - Agregar `Index_ActiveRow_ExposesCargosButton` — verifica que el botón está en filas activas.
  - `Index_DeletedRow_HidesCargosButton` — verifica que NO está en filas eliminadas.

## 6. Tareas propuestas (orden)

> `sdd-tasks` expandirá cada item en tareas committables. Estas ya están ordenadas topológicamente y permiten PR single (`ask-on-risk` revisará forecast en `sdd-tasks`).

| # | Capa | Tarea | Depende de |
|---|---|---|---|
| 1 | Aplicación | Crear `SkillCargoDetailDto` y `HabilidadCargosListQuery` en `Habilidades/Consultas/Dtos`. | — |
| 2 | Aplicación | Crear `ISkillCargoServicioConsulta` + `SkillCargoServicioConsulta` con `ListarCargosAsync`. | 1 |
| 3 | Infraestructura | Crear `ISkillCargoRepository` + impl `SkillCargoRepository` con `ListDetailedBySkillIdAsync` (EF Core, `AsNoTracking`, ordena antes de proyectar). | 1 |
| 4 | API | Agregar `SkillsController.GetCargosAsync` con normalización + 404/200; inyectar `ISkillCargoServicioConsulta` en el constructor. | 2 |
| 5 | Web/Integración | Extender `IHabilidadApiClient` + `HabilidadApiClient` con `GetCargosAsync`. | 1 |
| 6 | Web/Vista | Crear `Pages/Organizacion/Habilidades/Cargos.cshtml` + `.cs` (PageModel con OnGetAsync, mapeo DTO→ViewModel, toggle, tabla, paginación, gating admin). | 5, 4 (para contrato) |
| 7 | Web/Index | Agregar helper `BuildCargosRouteValues` en `Habilidades/Index.cshtml.cs`; sumar botón Cargos en `Index.cshtml` (entre Detalle y Editar; solo `!IsDeletedView`). | 6 |
| 8 | Tests | Tests del controller `HabilidadesCargosControllerTests` (8 escenarios por §5.1). | 4 |
| 9 | Tests | Tests del PageModel `HabilidadesCargosModelTests` (§5.3 primer bullet) + extensión `HabilidadesIndexPageTests` (§5.3 segundo bullet). | 6, 7 |
| 10 | Tests | Si existe harness InMemory maduro, tests del repositorio y servicio; si no, omitir y mantener cobertura por controller + page. | 3, 2 |
| 11 | Hardening | Validar `dotnet build SGV.slnx`, `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"`, `bun install` + `bun run build` en `src/SGV.Web`. | 1..10 |

Total: 11 tasks. PR único salvo que el forecast acumulado en `sdd-tasks` supere 400 líneas o el riesgo se eleve.

## 7. Mapeo spec → tasks

| Spec / escenario | Task que lo cubre |
|---|---|
| `habilidad-web-listado-detalle-baja` — Vista activas muestra acciones del catálogo activo | 7 |
| `habilidad-web-listado-detalle-baja` — Navegación a cargos preserva contexto | 7 |
| `habilidad-web-listado-detalle-baja` — Vista eliminadas muestra solo reactivación | 7 |
| `habilidad-management` — Habilidad existente devuelve colección paginada | 4, 8 |
| `habilidad-management` — Habilidad existente sin cargos devuelve vacío | 4, 8 |
| `habilidad-management` — Habilidad inexistente devuelve no encontrado | 4, 8 |
| `habilidad-management` — Operaciones write no disponibles | 4 (controller no expone writes) |
| `habilidad-management` — Lecturas autenticadas exitosas | 4 |
| `skill-cargo-query-contract` — Respuesta paginada y enriquecida | 1, 4 |
| `skill-cargo-query-contract` — Colección vacía | 4, 8 |
| `skill-cargo-query-contract` — Status inválido cae a activas | 4, 8 |
| `skill-cargo-query-contract` — 401 sin token | 4, 8 |
| `skill-cargo-query-contract` — 404 skill inexistente | 4, 8 |
| `skill-cargo-query-contract` — Alcance acotado | 4, 6 |

## 8. Riesgos técnicos restantes

| # | Riesgo | Mitigación |
|---|---|---|
| 1 | Pomelo no traduce `OrderBy` sobre projections a records posicionales (issue conocido en `cargos-navegacion-habilidades`). | Regla explícita en task 3: ordenar sobre entidades (`CargoEntity.Codigo`), proyectar al DTO en un `Select` posterior. Test del controller con sort prueba esto end-to-end. |
| 2 | `[MySqlFact] OcupacionRepositoryTests` falla por issue #59 (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`). Los nuevos tests podrían compartir ese código de migración. | Task 10 explícitamente excluye `[MySqlFact]` para esta superficie; usa InMemory o queda cubierto via controller + page. |
| 3 | `Habilidades/Details` sigue readonly y sin botón hacia cargos. Riesgo de scope creep por simetría con `Cargos/Details`. | Cambio declarado explícitamente como **fuera de alcance** en `proposal.md` y en la spec `habilidad-web-listado-detalle-baja`. No se toca `Details` en este PR. |
| 4 | El cliente web podría devolver 404 con cuerpo que hoy se mapea como excepción; la página tiene que distinguir "skill inexistente" de "lista vacía". | Task 6: el PageModel valida primero con `GetByIdAsync` antes de invocar `GetCargosAsync`. Si skill existe, lista vacía es `200`. Si skill no existe, redirect o `NotFound` con mensaje claro. Tests cubren ambos casos. |

## 9. Evidencia (referencias cruzadas)

- `openspec/changes/habilidades-navegacion-cargos/exploration.md` — espejo declarado, subrecurso faltante, capacidades impactadas, restricciones del repo.
- `openspec/changes/habilidades-navegacion-cargos/proposal.md` — decisiones locked.
- `openspec/changes/habilidades-navegacion-cargos/specs/habilidad-web-listado-detalle-baja/spec.md` — requirement de CTA `Cargos` y visibilidad por segmento.
- `openspec/changes/habilidades-navegacion-cargos/specs/habilidad-management/spec.md` — requirement de subrecurso y autorización.
- `openspec/changes/habilidades-navegacion-cargos/specs/skill-cargo-query-contract/spec.md` — contract readonly.
- `openspec/changes/cargos-navegacion-habilidades/design.md` — espejo de diseño.
- `src/SGV.Api/Controllers/SkillsController.cs:15-108` — patrón de controller y consultas.
- `src/SGV.Api/Controllers/CargosController.cs:221-295` — espejo del subrecurso `GET /api/v1/cargos/{cargoId}/skills`.
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs:14-38` — patrón de DTO enriquecido (record + init).
- `src/SGV.Aplicacion/Organizacion/Consultas/ICargoSkillRepository.cs:14-29` — contrato del repositorio espejo.
- `src/SGV.Aplicacion/Organizacion/Comandos/ICargoSkillServicio.cs:13` — firma `ListAsync` (no paginada — diferencia intencional).
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoDto.cs:6-12` — shape de `CargoDto` reusado.
- `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadServicioConsulta.cs:14-27` — patrón de servicio de consulta.
- `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs:47-57` — patrón de query async del cliente.
- `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs:122-173` — implementación modelo (URI building, `PagedResult<T>`).
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml:160-168` — patrón de botón rojo redondo + tooltip para entry points.
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs:122-127,157-162,244-252,342-347` — gating admin con `User.IsInRole`.
- `src/SGV.Dominio/Habilidades/CargoHabilidad.cs:12-40` — entidad de join.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs:11-34` — índice único `{CargoId, HabilidadId}`.
- `src/SGV.Dominio/Comun/EntidadAuditable.cs:13` — base `IsDeleted` para soft-delete de Cargo.
- `tests/SGV.Tests/Api/CargoSkillControllerTests.cs:87-207` — estrategia de tests API del espejo.
- `tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPageTests.cs:44-76` — base de tests web del Index de Habilidades.

---

**Próximo paso recomendado**: `sdd-tasks`, que expandirá los 11 items de §6 en tareas committables, estimará el tamaño del PR y disparará la gate `ask-on-risk` si la suma de líneas > 400 o si el riesgo efectivo sube.
