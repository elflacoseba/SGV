# Design: Migrar campo Categoría de Habilidades a Tabla

> **Change:** `migrar-campo-categoria-habilidades-a-tabla`. **Precedentes:** `NivelCargo` (catálogo inmutable, bloque `70000000-…`), `TipoDocumento` (`2026-07-20-147-tipos-documento-catalogo`, bloque `71000000-…`, opt-in relajada), `FixActivePuestoIdUniqueType` (forward-only), `UnidadOrganizativa`/`Habilidad` patrón `Reconstitute` (issue #124), `web-apiclient-transport-contract` (transporte de errores de catálogo read-only).

## Resumen

Catálogo inmutable `CategoriasHabilidad` (4 filas `Conduccion|Tecnica|Dominio|Academica`, bloque GUID `72000000-…`). Reemplazo de `Habilidades.Categoria` (string) por FK nullable `Habilidades.CategoriaId` (`Guid?`, `char(36) NULL`, `OnDelete(Restrict)`). Issue: `HabilidadDto.Categoria` (string) → `CategoriaId` (Guid?) + `CategoriaNombre` (string?) proyectados vía `LEFT JOIN`. Variante **opt-in relajada** de REQ-SPA-EVOLUTION-001 (cuarta invocación): backfill NO aborta; los strings legacy sin match (e.g. `"Otra cosa"`) caen a `CategoriaId = NULL` con auditoría de la transición `legacy string → NULL` para remediación post-deploy. Endpoints read-only autenticados (`GET /api/v1/categorias-habilidad` + `GET /api/v1/categorias-habilidad/{id}`). Dropdown poblado en formularios de Habilidad, Cargo y Persona. BREAKING de wire en `HabilidadDto` + `CrearHabilidadRequest`/`ActualizarHabilidadRequest`.

## Decisiones arquitectónicas

### Decisión: `CategoriaId` nullable + variante opt-in relajada

| Alternativa | Tradeoff | Decisión |
|---|---|---|
| FK nullable + `legacy string → NULL` auditado | Habilita rollback parcial; falla suave; remediación post-deploy posible vía `Auditorias` | **Elegida** (cuarta invocación REQ-SPA-EVOLUTION-001, precedente `Personas.TipoDocumento` issue #147) |
| FK nullable + abortar si hay strings sucios | Más estricto; bloquea deploy si hay datos sucios | Descartada — preferir falla suave + auditoría |
| Tabla con `IsActive`/`IsDeleted` | Rompería REQ-SPA-EVOLUTION-001 #1 (catalog inmutable); parity con `NivelCargo`/`TipoDocumento` | Descartada |

### Decisión: bloque GUID `72000000-…`

| Capa | Detalle |
|---|---|
| `DniId`→`PasaporteId` bloque `71000000-…` | Reservado para `TipoDocumento` (sin cambios) |
| `DirectivoId`→`AcademicoId` bloque `70000000-…` | Reservado para `NivelCargo` (sin cambios) |
| Nuevo bloque `72000000-…` (16 posiciones) | **Reservado para `CategoriaHabilidad`**. Posiciones `…000`–`…003` sembradas; `…004`–`…00F` libres para futuro |

### Decisión: dominio inmutable — sin `Actualizar`/`Eliminar` público

| Capa | Detalle |
|---|---|
| `CategoriaHabilidad` entidad | `sealed record class : EntidadBase` (no `EntidadAuditable`: catalog no audita; paridad con `NivelCargo`/`TipoDocumento`). `Id`, `Codigo`, `Nombre` con `private set`. Constructor primario privado + factory `Reconstitute(Id, Codigo, Nombre)` (mismo patrón que `TipoDocumento`). |
| `Habilidad` cambios | Se elimina `string? Categoria` (propiedad + backing + columna). Se agrega `Guid? CategoriaId` (FK opcional) + navegación `CategoriaHabilidad? Categoria`. `CambiarDatos` y `Actualizar` reemplazan parámetro `string? categoria` por `Guid? categoriaId`. `Reconstitute` recibe `categoriaId` (y opcionalmente la navegación pre-cargada). |
| Mapper persistencia | `PersistenceToDomainMapper.ToDomain(HabilidadEntity)` deja de leer `entity.Categoria`; pasa la `CategoriaId` y `entity.CategoriaHabilidad` (si la navegación se hidrata). `DomainToPersistenceMapper.ToEntity`/`UpdateEntity` escriben `CategoriaId` (no `Categoria`). |
| Backfill | `LEFT JOIN CategoriasHabilidad c ON LOWER(h.Categoria) = LOWER(c.Nombre)`. Filas sin match → `CategoriaId = NULL` + entrada en `Auditorias` con `Metadata = { "Origen": "Migracion.AddCategoriaHabilidadCatalog", "CategoriaOriginal": "..." }`. Después del backfill: `DROP INDEX IX_Habilidades_Categoria` + `DROP COLUMN Categoria` + `CREATE INDEX IX_Habilidades_CategoriaId`. |

### Decisión: API endpoint read-only autenticado

| Alternativa | Tradeoff | Decisión |
|---|---|---|
| `[Authorize]` a nivel clase | Heredado por sub-recursos; sin excepción | **Elegida** (paridad con `NivelesCargoController`, `AuthController` único `[AllowAnonymous]`) |
| `[AllowAnonymous]` | Rompe spec `sgv-readonly-api` y deja el catálogo expuesto a scrapers | Descartada |
| `GET /api/v1/categorias-habilidad/{id}` | Necesario para navegar al detalle | **Aceptada** (decisión del orquestador) |

### Decisión: UI dropdown en Habilidad + Cargo + Persona

- `Pages/Organizacion/Habilidades/{Create,Edit}.cshtml.cs`: `OnGetAsync` invoca `ICategoriaHabilidadApiClient.GetAllAsync()`; `_Form.cshtml` renderiza `<select asp-for="Input.CategoriaId" asp-items="@(new SelectList(categorias, "Id", "Nombre"))">` con opción "Sin categoría" como `null`.
- `Pages/Organizacion/Cargos/Habilidades.cshtml.cs` y `Pages/Personas/PersonaHabilidades.cshtml.cs`: dropdown adicional en el form "Asignar" para **filtrar** la lista de Habilidades disponibles por categoría (no para asignar a la FK de CargoHabilidad, que no existe). Sin regresión: el dropdown de skills no se desactiva.

### Decisión: taxonomía de errores

| Código | HTTP | `ErrorCategoria` | `HabilidadErrorType` |
|---|---|---|---|
| `CategoriaHabilidadNoExiste` | 400 | `Validation` | `CategoriaInexistente` (nuevo) |
| Codigo/nombre inválidos | 400 | `Validation` | `Validation` (existente) |

`HabilidadErrorType.CategoriaInexistente` se mapea bidireccionalmente en `ErrorCategoriaMappers.ToCategoria` (extender el switch existente; sin `default:`). El preset `NivelRequeridoId` en `cargo-skill-asignar-editar` es precedente directo. `HabilidadApiClient.CreateAsync`/`UpdateAsync` consume el código `CategoriaHabilidadNoExiste` del backend y lo mapea vía `CommandResultMapper.Map` a `HabilidadCommandResult.Failure(HabilidadError { Type = CategoriaInexistente, Categoria = Validation })`.

### Persistencia (EF Core + MySQL + migración + backfill)

**Migración `AddCategoriaHabilidadCatalog`** (forward-only; `Down()` lanza `NotSupportedException` precedente `FixActivePuestoIdUniqueType`):

```
1. CREATE TABLE CategoriasHabilidad (
   Id char(36) NOT NULL COLLATE ascii_general_ci,
   Codigo varchar(50) NOT NULL COLLATE ascii_general_ci,
   Nombre varchar(100) NOT NULL,
   CONSTRAINT PK_CategoriasHabilidad PRIMARY KEY (Id),
   CONSTRAINT CK_CategoriasHabilidad_Codigo CHECK (Codigo <> '')
   );
   CREATE UNIQUE INDEX IX_CategoriasHabilidad_Codigo ON CategoriasHabilidad(Codigo);

2. INSERT INTO CategoriasHabilidad (Id, Codigo, Nombre) VALUES
   ('72000000-0000-0000-0000-000000000000', 'Conduccion', 'Conducción'),
   ('72000000-0000-0000-0000-000000000001', 'Tecnica', 'Técnica'),
   ('72000000-0000-0000-0000-000000000002', 'Dominio', 'Dominio'),
   ('72000000-0000-0000-0000-000000000003', 'Academica', 'Académica');

3. Pre-flight log (no fail-loud):
   SELECT DISTINCT Categoria FROM Habilidades
   WHERE Categoria IS NOT NULL
     AND LOWER(Categoria) NOT IN (SELECT LOWER(Nombre) FROM CategoriasHabilidad);
   Devuelve conteo + ejemplos; se loguea vía SHOW ENGINE INNODB STATUS.

4. ALTER TABLE Habilidades ADD COLUMN CategoriaId char(36) NULL COLLATE ascii_general_ci;
   CREATE INDEX IX_Habilidades_CategoriaId ON Habilidades(CategoriaId);

5. Backfill (case-insensitive, los no match quedan NULL):
   UPDATE Habilidades h
   INNER JOIN CategoriasHabilidad c ON LOWER(c.Nombre) = LOWER(h.Categoria)
   SET h.CategoriaId = c.Id
   WHERE h.Categoria IS NOT NULL;

6. Auditoría del backfill (en migración, fuera del interceptor):
   INSERT INTO Auditorias (Id, UserId, OccurredAt, EntityName, EntityId, Operation, NewValuesJson)
   SELECT <uuid>, NULL, UTC_TIMESTAMP(6), 'Habilidad', h.Id, 'BackfillLegacyCategoriaToNull',
          JSON_OBJECT('Origen', 'Migracion.AddCategoriaHabilidadCatalog',
                      'CategoriaOriginal', h.Categoria)
   FROM Habilidades h
   WHERE h.Categoria IS NOT NULL AND h.CategoriaId IS NULL;

7. FK constraint:
   ALTER TABLE Habilidades ADD CONSTRAINT FK_Habilidades_CategoriasHabilidad_CategoriaId
   FOREIGN KEY (CategoriaId) REFERENCES CategoriasHabilidad(Id) ON DELETE RESTRICT;

8. DROP INDEX IX_Habilidades_Categoria ON Habilidades;
   ALTER TABLE Habilidades DROP COLUMN Categoria;
```

**`HasData` en `DatosSemilla.cs`** debe incluir **exactamente** las mismas 4 filas del `InsertData` desde `CategoriaHabilidadConstantes`. Test de paridad `DatosSemilla_CategoriaHabilidad_SeedIdsMatchConstantes` (patrón `DatosSemilla_TipoDocumento_SeedIdsMatchConstantes`).

**Script SQL idempotente** se regenera vía `dotnet ef migrations script ... --idempotent` y se commitea a `docs/migracion-inicial-sgv.sql` (regenerar tras apply).

### API / Contratos wire

**Nuevos endpoints** (`src/SGV.Api/Controllers/CategoriasHabilidadController.cs`):

```csharp
[ApiController]
[Route("api/v1/categorias-habilidad")]
[Authorize]
public class CategoriasHabilidadController : ControllerBase
{
    [HttpGet]                          // → 200 IReadOnlyList<CategoriaHabilidadDto>
    [HttpGet("{id:guid}")]              // → 200 / 404
}
```

**Wire shape** (`SGV.Contracts`):

```csharp
// src/SGV.Contracts/Habilidades/Categorias/Consultas/CategoriaHabilidadDto.cs
public sealed record CategoriaHabilidadDto(Guid Id, string Codigo, string Nombre);

// src/SGV.Contracts/Habilidades/Consultas/Dtos/HabilidadDto.cs (MODIFIED)
public sealed record HabilidadDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid? CategoriaId,        // ← reemplaza string? Categoria
    string? CategoriaNombre   // ← proyección de CategoriasHabilidad.Nombre
);

// src/SGV.Contracts/Habilidades/Comandos/HabilidadRequests.cs (MODIFIED)
public sealed record CrearHabilidadRequest(string Codigo, string Nombre, Guid? CategoriaId = null, string? Descripcion = null);
public sealed record ActualizarHabilidadRequest(string Codigo, string Nombre, Guid? CategoriaId = null, string? Descripcion = null);
```

**Mappers** (`PersistenceToDomainMapper` / `HabilidadServicioConsulta`): `Habilidad → HabilidadDto` recibe `CategoriaNombre` vía `LEFT JOIN CategoriasHabilidad`. Si la navegación no se eager-loads, una proyección LINQ explícita en `HabilidadRepository.QueryAsync`/`ListAllAsync` la resuelve (`SELECT h.*, c.Nombre AS CategoriaNombre FROM Habilidades h LEFT JOIN CategoriasHabilidad c ON h.CategoriaId = c.Id`).

**Validación de `CategoriaId` en `HabilidadServicioComandos`**: si llega Guid no-null y `repository.ExisteCategoriaAsync(categoriaId)` devuelve `false` → `HabilidadCommandResult.Failure(new HabilidadError(HabilidadErrorType.CategoriaInexistente, "CategoriaHabilidadNoExiste", "La categoría indicada no existe."))` con `Categoria = Validation`. El controller lo traduce a `400` con `ValidationProblemDetails` (`errors.categoriaId = ["..."]`).

**BREAKING CHANGE**: cualquier consumidor de `HabilidadDto.Categoria` (string) debe migrar a `CategoriaId`/`CategoriaNombre`. El campo `categoria` (string) **no debe aparecer** en el JSON de salida (verificar con test `Wire_SkillsController_JsonNoIncluyeCampoCategoria_Legacy`).

### Aplicación (servicios + validadores + mappers)

**Nuevos componentes**:

```csharp
// src/SGV.Aplicacion/Habilidades/Consultas/ICategoriaHabilidadRepository.cs
public interface ICategoriaHabilidadRepository : IReadOnlyRepository<CategoriaHabilidad>
{
    Task<IReadOnlyList<CategoriaHabilidad>> ListAllAsync(CancellationToken ct = default);
    Task<CategoriaHabilidad?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

// src/SGV.Aplicacion/Habilidades/Consultas/ICategoriaHabilidadServicioConsulta.cs
public interface ICategoriaHabilidadServicioConsulta
{
    Task<IReadOnlyList<CategoriaHabilidadDto>> ListAsync(CancellationToken ct = default);
    Task<CategoriaHabilidadDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
```

**`IHabilidadRepository`** extiende con:

```csharp
Task<bool> ExistsCategoriaAsync(Guid categoriaId, CancellationToken ct = default);
// QueryAsync/ListAllAsync proyectan CategoriaNombre explícitamente
```

**Validadores** (`CrearHabilidadRequestValidator`/`ActualizarHabilidadRequestValidator`): se elimina `RuleFor(x => x.Categoria).MaximumLength(100)`. Se agrega `RuleFor(x => x.CategoriaId).NotEmpty().When(x => x.CategoriaId.HasValue)` (Guid no-empty cuando se informa). `CategoriaId` es opcional por shape.

**`HabilidadServicioComandos.CrearAsync` / `ActualizarAsync`**: invocan `await repository.ExistsCategoriaAsync(request.CategoriaId)` antes de `unitOfWork.SaveChangesAsync` cuando `request.CategoriaId.HasValue`. Si `false` → `HabilidadCommandResult.Failure(CategoriaInexistente)`.

**DI**: `CategoriaHabilidadRepository` y `CategoriaHabilidadServicioConsulta` se registran en `src/SGV.Infraestructura/DependencyInjection.cs` siguiendo el patrón `NivelCargoRepository` (read-only, no tests `IsActive`).

### Web (Razor Pages + clientes tipados)

**Nuevo cliente** (`src/SGV.Web/Integration/Habilidades/ICategoriaHabilidadApiClient.cs` + `CategoriaHabilidadApiClient.cs`):

```csharp
public interface ICategoriaHabilidadApiClient
{
    Task<IReadOnlyList<CategoriaHabilidadDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoriaHabilidadDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    // NO expone Create/Update/Delete
}
```

**Implementación**: `HttpClient` tipado con `BaseAddress` desde `SgvApiOptions`, `Timeout = 10s`, `ApiBearerTokenHandler` registrado en `Program.cs` (precedente `ICargoApiClient`). `GetByIdAsync` traduce `404` a `null` (sin excepción). `HttpRequestException`/`TaskCanceledException` se propagan al `PageModel` (clasificadas por `TransportFailureClassifier`).

**ViewModel** (`src/SGV.Web/Integration/Habilidades/`): `CategoriaHabilidadViewModel(Guid Id, string Codigo, string Nombre)` o reuso directo de `CategoriaHabilidadDto` (preferible: ya consumer-safe).

**DI** (`Program.cs`):

```csharp
builder.Services.AddHttpClient<ICategoriaHabilidadApiClient, CategoriaHabilidadApiClient>(... 10s ...)
    .AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());
```

**Formularios**:

- `src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml.cs` y `Edit.cshtml.cs`: `OnGetAsync` invoca `categoriaHabilidadApiClient.GetAllAsync()`; en `OnPostAsync` arma `CrearHabilidadRequest(... Input.CategoriaId, ...)`. Si la llamada falla (transport), `ErrorMessage = "No se pudo cargar el catálogo de categorías"` y se renderiza solo opción "Sin categoría".
- `src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml`: reemplaza el `<input asp-for="Input.Categoria">` por `<select asp-for="Input.CategoriaId" asp-items="@(new SelectList(Model.CategoriasDisponibles, "Id", "Nombre"))">` con opción vacía "Sin categoría" mapeada a `null` (helper extension `CategoriaHabilidadFormHelpers`).
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` y `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs`: filtro `CategoriaId` opcional en `OnGetAsync` para acotar la grilla de Habilidades disponibles del form "Asignar". Sin regresión: el dropdown de skill sigue operativo.
- `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml`: columna `Categoría` renderiza `item.CategoriaNombre` (badge `badge-soft-secondary`).
- `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml`: badge `CategoriaNombre` (reemplaza el `Categoria` actual).

**`HabilidadInputModel`** (`src/SGV.Web/Integration/Habilidades/HabilidadInputModel.cs`): se reemplaza `string? Categoria` por `Guid? CategoriaId`. `IHabilidadForm` requiere `IReadOnlyList<CategoriaHabilidadViewModel> CategoriasDisponibles { get; }`.

**Scripts frontend**: `bun run build` valida que el bundle sigue compilando (sin cambios JS nuevos esperados; solo se rendentiza un `<select>` server-side).

### Auditoría

- **Auditoría automática vía interceptor**: `AuditoriaSaveChangesInterceptor` captura transiciones `CategoriaId` en `Habilidad` (cubre `Create`/`Update`/`Delete`/`Reactivate`) sin código extra. Verificar que el JSON `NewValuesJson` redacte `CategoriaId` como Guid explícito.
- **Auditoría del backfill (migración)**: el interceptor NO captura cambios que pasan por `migrationBuilder.Sql(...)`. Por eso el step 6 de la migración inserta manualmente en `Auditorias` una fila por cada `Habilidad` legacy sucia que quedó en `NULL`. `Origen = "Migracion.AddCategoriaHabilidadCatalog"` + `CategoriaOriginal` en el JSON es la firma reproducible para queries de remediación.
- **Inmutabilidad del catálogo**: `CategoriasHabilidadController` no expone endpoints de escritura. No hay `ICategoriaHabilidadRepository.Add/Update/Delete`. Garantía estructural: las filas seedeadas no son tocables en runtime.

### Performance / Índices

- `IX_Habilidades_CategoriaId` cubre `JOIN` y filtros por categoría en `QueryAsync`/`ListAllAsync`.
- `IX_CategoriasHabilidad_Codigo` único activo sobre catálogo (paridad con `IX_NivelesCargo_Codigo`).
- `EXPLAIN` validado en sandbox del dev: `SELECT * FROM Habilidades h LEFT JOIN CategoriasHabilidad c ON h.CategoriaId = c.Id WHERE c.Nombre LIKE '%tecnica%'` debe usar `IX_Habilidades_CategoriaId` + `PK_CategoriasHabilidad` (LEFT JOIN sin filtro usa index nested-loop o hash join).
- Columna `Categoria` legacy (`varchar(100)`) se elimina del heap; el espacio se devuelve tras `OPTIMIZE TABLE Habilidades` (fuera de scope; el operador decide cuándo).
- `ORDER BY c.Nombre` en `GET /api/v1/categorias-habilidad` no requiere índice (4 filas en memoria).

### Compatibilidad y migración de clientes

**BREAKING CHANGE en `SGV.Contracts`**:

| Archivo | Cambio |
|---|---|
| `HabilidadDto.cs` | `string? Categoria` → `Guid? CategoriaId` + `string? CategoriaNombre` |
| `HabilidadRequests.cs` | `string? Categoria` → `Guid? CategoriaId` en `CrearHabilidadRequest` y `ActualizarHabilidadRequest` |
| `HabilidadListItemViewModel.cs` | `string? Categoria` → `IReadOnlyList<CategoriaHabilidadOpcion>` o `string? CategoriaNombre` (mantener fuente de verdad en DTO) |

**Call sites internos** que recompilan:

- `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` (eliminar `MapCategoriaToType` para `ErrorCategoria.NotFound` → `HabilidadErrorType.NotFound`; sigue mapeando `CategoriaInexistente`).
- `src/SGV.Web/Integration/Habilidades/HabilidadInputModel.cs` (reemplazar `Categoria` por `CategoriaId`).
- `src/SGV.Web/Integration/Habilidades/HabilidadListItemViewModel.cs` (proyectar `CategoriaNombre`).
- `src/SGV.Web/Pages/Organizacion/Habilidades/{_Form.cshtml, Index.cshtml, Details.cshtml, Create.cshtml.cs, Edit.cshtml.cs, Index.cshtml.cs}`.
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` + `.cshtml` (filtro por categoría).
- `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` + `.cshtml` (filtro por categoría).
- `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs` (mapeos `MapToDto` + `Crear/Actualizar`).
- `src/SGV.Aplicacion/Habilidades/Consultas/HabilidadServicioConsulta.cs` (mapeos `MapToDto`).
- `src/SGV.Aplicacion/Habilidades/Comandos/Validaciones/{CrearHabilidadRequestValidator,ActualizarHabilidadRequestValidator}.cs`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs` (actualizar `QueryAsync`/`ListAllAsync` para proyectar `CategoriaNombre`; actualizar `ExistsCategoriaAsync`).
- `src/SGV.Infraestructura/Persistencia/Mapeos/{PersistenceToDomainMapper,DomainToPersistenceMapper}.cs`.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs` (eliminar `HasIndex(e => e.Categoria)`; agregar navegación FK).
- `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` (`DbSet<HabilidadEntity.CategoriaHabilidad> CategoriasHabilidad`).

**Orquestación**: todos los call sites se actualizan en el mismo change. Después de mergear, build de la solución debe quedar verde (`dotnet build SGV.slnx`).

## Riesgos técnicos y mitigaciones

| Riesgo | Impacto | Probabilidad | Mitigación |
|---|---|---|---|
| BREAKING CHANGE en `HabilidadDto`/`HabilidadRequests` | Alto (clientes web deben recompilar) | Alta (es el cambio principal) | CHANGELOG explícito, título de PR `BREAKING: skill categoria → categoriaId+CategoriaNombre`, todos los call sites actualizados en el mismo change |
| Backfill con `LOWER()` falla por collation | Medio (filas correctamente seteadas quedan NULL) | Baja (default `utf8mb4_0900_ai_ci` es case-insensitive en MySQL 8) | Verificar collation por columna; documentado en `decisiones-implementacion.md` que el backfill usa `LOWER()` + collation `ascii_general_ci` en `Codigo` para idempotencia |
| Colisión GUID `72000000-…` con futuro change | Bajo (sólo bloque de 16 filas) | Baja (verificado en `exploration.md`) | Chequeo en `sdd-apply` antes de mergear: `grep -rn "72000000" src/` único seed |
| Queries legacy que filtran `WHERE Categoria LIKE '%texto%'` rotos | Medio (reportes manuales) | Media | Documentar en CHANGELOG; nueva query requiere `LEFT JOIN CategoriasHabilidad c ON h.CategoriaId = c.Id WHERE c.Nombre LIKE '%texto%'` |
| Dropdown catálogo caído al cargar form | Bajo (PRG + TempData ya manejado) | Baja | `TransportFailureClassifier` + `ErrorMessage = "No se pudo cargar el catálogo"` |
| Seed migration no idempotente si DB ya migrada | Bajo (constraint violation) | Baja | `migrationBuilder.InsertData` es idempotente bajo `Database.Migrate()`; tests `[MySqlFact]` validan segunda corrida |
| `HabilidadDto.CategoriaNombre` siempre null en test que no setea navegación | Bajo (test setup) | Media | Forzar `Include(p => p.Categoria)` en `QueryAsync`/`ListAllAsync` o proyección LINQ explícita |
| `DropColumn` rompe queries activas en deploy blue/green | Bajo (no tenemos deploy rolling) | n/a | Snapshot dump antes de aplicar migración; rollback vía backup |
| Tests web existentes con `Categoria` literal en factories | Medio (compile errors) | Alta | Migrar `fakes`/`tests` en el mismo change; cubierto por scope |

## Plan de pruebas (esbozo)

| Capa | Qué | Cómo |
|---|---|---|
| Dominio | `CategoriaHabilidad` invariantes (Codigo/Nombre required, max lengths) | xUnit puro sin DB |
| Dominio | `CategoriaHabilidad.Reconstitute` (factory interno) | xUnit + `InternalsVisibleTo("SGV.Tests")` |
| Aplicación | `ExisteCategoriaAsync` true/false | xUnit con `InMemory` provider o repo fake |
| Aplicación | FluentValidation: `CategoriaId` opcional, Guid no-empty cuando presente | xUnit |
| Aplicación | `HabilidadServicioComandos.Crear/Actualizar` con `CategoriaId` inexistente → `CategoriaInexistente` | xUnit con repo fake |
| Persistencia | `CategoriasHabilidad` seed: 4 filas, índices, FK `Restrict` | `[MySqlFact]` |
| Persistencia | Backfill de 7 habilidades (3 con match + 4 NULL) | `[MySqlFact]` |
| Persistencia | `DROP COLUMN Categoria` + `IX_Habilidades_CategoriaId` post-migración | `[MySqlFact]` |
| Persistencia | DELETE restringido en `CategoriasHabilidad` cuando hay FK | `[MySqlFact]` |
| API | `GET /api/v1/categorias-habilidad` 200 con 4 elementos (anónimo → 401) | `WebApplicationFactory` |
| API | `GET /api/v1/categorias-habilidad/{id}` 200/404 | `WebApplicationFactory` |
| API | `POST /api/v1/skills` con `CategoriaId` inexistente → 400 `ValidationProblemDetails` codigo `CategoriaHabilidadNoExiste` | `WebApplicationFactory` |
| API | `HabilidadDto` no expone `categoria` (string) en wire | snapshot JSON |
| Web | `CategoriaHabilidadApiClient` 200 OK / 401 / 503 / `HttpRequestException` | `WebApplicationFactory` + mock |
| Web | `OnGetAsync` Create/Edit carga dropdown; catálogo vacío → solo "Sin categoría" | xUnit sobre `PageModel` |
| Web | POST con `CategoriaId` inválido → muestra `fieldErrors.categoriaId` | xUnit |
| Web | Dropdown en Cargos/Personas filtra habilidades disponibles | xUnit |
| End-to-end | `bun run build` valida assets | CLI |
| Arquitectural | IL estructural: `PersistenceToDomainMapper` no usa `SetProperty`/`PropertyInfo.SetValue` para `Habilidad` (paridad con #124) | xUnit + `MethodBody.GetILAsByteArray` |

## Decisiones abiertas

- [ ] **¿Idempotencia manual del seed?** Recomendación: solo `Database.Migrate()` — paridad con `TipoDocumento`. No exponer `dotnet run seed-categorias` como subcomando.
- [ ] **¿Dropdown en Cargos/Personas reutiliza `ICategoriaHabilidadApiClient`?** Recomendación: sí (mismo cliente, no nuevo `ICargoApiClient.GetCategoriasHabilidadAsync`).
- [ ] **¿Excel export de Habilidades necesita shim `Categoria` legacy?** Recomendación: fuera de scope. Si surge, SDD futuro.
- [ ] **¿Documentación governance se agrega antes de `sdd-apply`?** Confirmado por orquestador: entrada en `docs/decisiones-implementacion.md` § "Variantes opt-in del REQ-SPA-EVOLUTION-001" citando precedente `Personas.TipoDocumento` (issue #147).
- [ ] **¿`HabilidadReconstitute` debe seguir aceptando `string? categoria` legacy?** Recomendación: NO — eliminar el parámetro del todo. Cualquier call site remanente debe reescribirse.

## Variante opt-in relajada (documentación governance)

Se documenta en `docs/decisiones-implementacion.md` una nueva subsección bajo "Decisiones de soft delete y unicidad activa" (o nueva sección "Variantes opt-in del REQ-SPA-EVOLUTION-001"):

- **Patrón**: FK nullable + orphan-tolerant + transición `legacy string → NULL` auditada con `Metadata = { Origen, CategoriaOriginal }` en `Auditorias`.
- **Precedente**: `Personas.TipoDocumento` (issue #147, segunda invocación de REQ-SPA-EVOLUTION-001 — la tercera invocación fue `2026-07-22-...`; este change `migrar-campo-categoria-habilidades-a-tabla` es la **cuarta**).
- **Decisión**: aplicar este patrón a `Habilidad.Categoria → CategoriaId` para una categoría huérfana sin match deja `CategoriaId = NULL` y registro auditable para remediación post-deploy.
- **Tradeoffs aceptados**: habilita rollback parcial si una nueva fila seed no resuelve; auditoría permite remediación post-deploy. La FK sigue `OnDelete(Restrict)` para evitar borrado de categorías en uso.

## Data Flow

```
Razor Page (Create/Edit Habilidad)
    │
    ├─ OnGetAsync → ICategoriaHabilidadApiClient.GetAllAsync()
    │                            │
    │                            ▼
    │                HttpClient → GET /api/v1/categorias-habilidad
    │                            │
    │                            ▼
    │                CategoriasHabilidadController → ICategoriaHabilidadServicioConsulta
    │                            │
    │                            ▼
    │                ICategoriaHabilidadRepository.ListAllAsync()
    │                            │
    │                            ▼
    │                SgvDbContext.CategoriasHabilidad.AsNoTracking()
    │                            │
    │                            ▼
    │                PersistenceToDomainMapper.ToDomain(CategoriaHabilidadEntity)
    │
    └─ OnPostAsync → IHabilidadApiClient.CreateAsync(CrearHabilidadRequest(Codigo, Nombre, CategoriaId, Descripcion))
                                │
                                ▼
                    HttpClient → POST /api/v1/skills
                                │
                                ▼
                    SkillsController → IHabilidadServicioComandos.CrearAsync
                                │
                                ├─ Validator.Check(CategoriaId)
                                ├─ repository.ExistsCategoriaAsync(CategoriaId) ←── si !HasValue: skip
                                │   └─ false → Failure(CategoriaInexistente, Validation)
                                │
                                ├─ new Habilidad(Codigo, Nombre, CategoriaId, Descripcion)
                                │
                                └─ repository.AddAsync + unitOfWork.SaveChangesAsync
                                       │
                                       ▼
                        INSERT INTO Habilidades (Id, Codigo, Nombre, Descripcion, CategoriaId, ...)
                                       │
                                       ▼
                        AuditoriaSaveChangesInterceptor registra cambio (Edited/NewValuesJson incluye CategoriaId)
                                │
                                ▼
                    Response: HabilidadDto { Id, Codigo, Nombre, Descripcion, CategoriaId, CategoriaNombre }
```

## Próximos pasos

`→ sdd-tasks` (descomposición en tareas implementables, manteniendo `delivery_strategy: ask-always` y `review_budget: 400`).

Tamaño estimado del change: 1 PR encadenado (backend + frontend + tests) o 2 PRs encadenados (PR1 backend + PR2 frontend) — decisión del orquestador en `sdd-tasks`.
