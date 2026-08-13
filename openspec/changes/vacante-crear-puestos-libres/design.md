# Design: vacante-crear-puestos-libres

## Resumen ejecutivo

El change introduce un endpoint de consulta dedicado `GET /api/v1/puestos/disponibles` que devuelve Puestos activos sin Ocupación vigente (`IsDeleted=0 AND FechaFin IS NULL`) **ni** Vacante abierta (`IsDeleted=0 AND FechaCierre IS NULL`), implementado como nuevo método `ListarDisponiblesAsync` en `IPuestoRepository`/`IPuestoServicioConsulta` con dos `NOT EXISTS` correlacionados en LINQ. El dropdown del formulario `Vacantes/Create` pasa a consumirlo mediante un nuevo método `ListarPuestosDisponiblesAsync` en `IVacanteApiClient`. **No se modifica ningún agregado de dominio, ningún migración de datos, ni la validación backend N1/`ActivePuestoIdUnique`**: el filtro es puramente una preocupación de query (defense-in-depth UX), y la fuente de verdad sigue siendo el backend.

## Capa de dominio

**Sin cambios.** `Puesto` (`src/SGV.Dominio/Organizacion/Puesto.cs`), `Ocupacion` (`src/SGV.Dominio/Ocupaciones/Ocupacion.cs`) y `Vacante` (`src/SGV.Dominio/Vacantes/Vacante.cs`) permanecen intactos.

- `Ocupacion.EsVigente` ya define `FechaFin is null && !IsDeleted` (línea 76) — el filtro LINQ replica esa semántica sobre la entidad de persistencia, sin reusar la property del agregado para evitar hidratar Ocupaciones.
- `Vacante.FechaCierre` se setea vía `CambiarEstado(..., cerrar: true)` (línea 50); "abierta" = `FechaCierre is null && !IsDeleted`.
- La noción de "disponible" **no es una regla de dominio**: es una proyección de query del estado cruzado Puesto↔Ocupacion↔Vacante. Confirmado en `exploration.md` §"Dominio".

## Capa de aplicación

### Decisiones

| Decisión | Alternativa | Rationale |
|---|---|---|
| Método nuevo `ListarDisponiblesAsync` en `IPuestoServicioConsulta` e `IPuestoRepository` | Flag `bool soloLibres` en `ListAsync`/`ListAllAsync` | Blast-radius: `ListAsync` tiene 2 callers (controller `GetAll`, tests); `ListAllAsync` (`IReadOnlyRepository<Puesto>`) tiene 4 implementadores (real + 3 fakes). Un flag obligaría a todos los fakes a manejar el parámetro y ensuciaría el contrato `IReadOnlyRepository`. Un método dedicado deja el contrato existente sin tocar y aísla la query de disponibilidad. Validado por exploration §"Contract breach". |
| `ListarDisponiblesAsync` en el repo (no extender `QueryAsync`) | Reutilizar `QueryAsync(search, page, …)` con `segmento=Disponibles` | `QueryAsync` es paginada/segmentada activas-vs-eliminadas; "disponibles" es una tercera dimensión ortogonal. Combinar rompe la semántica del segmento existente. |
| Mapeo a `PuestoDto` vía `MapToDto` existente | Nuevo mapper | Devuelve el mismo DTO shape que `GET /api/v1/puestos` (REQ-PTO-DISP-001, escenario "shape idéntico"). `PuestoDto` no expone estado de Ocupación — no se modifica. |

### Firmas

```csharp
// IPuestoServicioConsulta.cs
Task<IReadOnlyList<PuestoDto>> ListarDisponiblesAsync(CancellationToken cancellationToken = default);

// IPuestoRepository.cs
Task<IReadOnlyList<Puesto>> ListarDisponiblesAsync(CancellationToken cancellationToken = default);
```

`PuestoServicioConsulta.ListarDisponiblesAsync` es delegador puro: `repo.ListarDisponiblesAsync(ct)` → `Select(MapToDto).ToList()`.

### Blast radius de la interfaz (`IPuestoRepository`)

Implementadores a actualizar (mecánico, throw `NotSupportedException()` en los fakes salvo el de consulta):

| Implementador | Archivo | Acción |
|---|---|---|
| `PuestoRepository` (real) | `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` | Implementa la query |
| `FakePuestoRepository` | `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs:317` | Delega a `Datos` (para que los unit tests del servicio funcionen) |
| `FakePuestoWriteRepository` | `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioComandosTests.cs:510` | `throw new NotSupportedException()` |
| `FakePuestoWriteRepository` | `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs:1294` | `throw new NotSupportedException()` |

## Capa de persistencia

**Sin cambios en entidades** (`PuestoEntity`, `OcupacionEntity`, `VacanteEntity`). Verificado: `PuestoEntity` ya expone `List<OcupacionEntity> Ocupaciones` (línea 30) y `List<VacanteEntity> Vacantes` (línea 32), por lo que la query LINQ puede usar navigation-based correlated subqueries y mantiene el patrón del `ReadOnlyRepository` existente (que ya `Include`-a `UnidadOrganizativa` + `Cargo`).

### Query EF Core (`PuestoRepository.ListarDisponiblesAsync`)

```csharp
public async Task<IReadOnlyList<Puesto>> ListarDisponiblesAsync(CancellationToken ct = default)
{
    var entities = await Context.Set<PuestoEntity>()
        .AsNoTracking()
        .Where(p => p.IsActive && !p.IsDeleted)
        .Where(p => !p.Ocupaciones.Any(o => !o.IsDeleted && o.FechaFin == null))
        .Where(p => !p.Vacantes.Any(v => !v.IsDeleted && v.FechaCierre == null))
        .Include(p => p.UnidadOrganizativa)
        .Include(p => p.Cargo)
        .OrderBy(p => p.Nombre)
        .ThenBy(p => p.Codigo)
        .ToListAsync(ct);

    return entities.Select(MapToDomain).ToArray();
}
```

- **No reutiliza `base.Query`**: este último filtra solo `IsActive`; necesitan su propia raíz para añadir los dos `NOT EXISTS` con `AsNoTracking`.
- Usa `p.Ocupaciones.Any`/`p.Vacantes.Any` (nav collections) en lugar de `_context.Set<…>()` para mantener fidelidad con el mapeo del repo y dejar que EF Core traduzca subconsultas correlacionadas.
- El `(OrderBy Nombre, ThenBy Codigo)` replica el orden vigente de `ListAllAsync` (línea 30-31) — consistencia visual en el dropdown.
- La proyección a dominio vía `MapToDomain` (=`PersistenceToDomainMapper.ToDomain`) replica el patrón de `ListAllAsync`/`QueryAsync`; el `Include` de `UnidadOrganizativa` + `Cargo` es necesario porque `MapToDto` lee `entity.UnidadOrganizativa.Nombre` y `entity.Cargo.Nombre`.

Se traduce a SQL `NOT EXISTS (SELECT 1 FROM Ocupaciones WHERE …)` / `NOT EXISTS (SELECT 1 FROM Vacantes WHERE …)` con la semántica exacta de la decisión del usuario.

### Análisis de índices

| Tabla | Índices existentes relevantes | ¿Cubre el `NOT EXISTS`? |
|---|---|---|
| `Ocupaciones` | `IX (PuestoId, FechaInicio, FechaFin)` (línea 72 de `OcupacionConfiguracion.cs`); único sobre computed `ActivePuestoIdUnique` (`FechaFin IS NULL AND IsDeleted=0 → PuestoId`) | Sí — leftmost prefix `PuestoId` basta para el lookup correlacionado; el conjunto candidato por Puesto es chico (una a unas pocas Ocupaciones). |
| `Vacantes` | `IX (PuestoId)` (línea 47 de `VacanteConfiguracion.cs`); único `IX_Vacantes_ActivePuestoIdUnique` computed | Sí — mismo razonamiento. |

**Decisión: sin migración de índices en este change.** Los índices con prefijo `PuestoId` cubren el lookup del `NOT EXISTS`; el conjunto post-lookup por PuestoId es trivial. Un composite `(PuestoId, IsDeleted, FechaFin)` / `(PuestoId, IsDeleted, FechaCierre)` podría ahorrar el filter post-lookup, pero el beneficio es marginal dada la cardinalidad esperada (pocas Ocupaciones/Vacantes por Puesto) y en `Ocupaciones` ya existe la columna computed-indexed que el optimizador podría elegir en planes complejos. **Flag de follow-up**: si se detectan planes costosos con `EXPLAIN` bajo carga, se filaría como cambio separado.

## Capa de API

Nuevo action en `PuestosController` (controlador existente `[Route("api/v1/puestos")]`, ya `[[Authorize]]`):

```csharp
/// <summary>Obtiene los puestos disponibles (sin Ocupación vigente ni Vacante abierta).</summary>
[HttpGet("disponibles")]
[ProducesResponseType(typeof(IReadOnlyList<PuestoDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<IReadOnlyList<PuestoDto>>> GetDisponibles(CancellationToken ct)
{
    var result = await _servicio.ListarDisponiblesAsync(ct);
    return Ok(result);
}
```

- **Route**: `GET /api/v1/puestos/disponibles` — sub-recurso. `disponibles` se resuelve antes que `{id:guid}` por la route table (literal vs Guid constraint), sin colisión.
- **Auth**: hereda `[Authorize]` del controlador — cualquier autenticado (igual que `GetAll`). Cero cambio de rol.
- `PuestoDto`: **sin cambios**. Confirmed en `exploration.md` y `SGV.Contracts/Organizacion/Consultas/Dtos/PuestoDto.cs`.
- **`GET /api/v1/puestos` no se toca** — backward compat preservado (REQ-PTO-DISP-001 escenario "GET sin cambios").

## Capa de integración web

### `IVacanteApiClient` / `VacanteApiClient`

```csharp
// IVacanteApiClient.cs (nuevo método)
Task<IReadOnlyList<PuestoDto>> ListarPuestosDisponiblesAsync(CancellationToken cancellationToken = default);
```

### `VacanteApiClient.ListarPuestosDisponiblesAsync` se construye como:

```csharp
public async Task<IReadOnlyList<PuestoDto>> ListarPuestosDisponiblesAsync(CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    var response = await httpClient.GetAsync(PuestosDisponiblesRoot, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<IReadOnlyList<PuestoDto>>(ct).ConfigureAwait(false) ?? [];
}
```

- **Route const**: añadir `public const string PuestosDisponiblesRoot = PuestosRoot + "/disponibles";` en `VacanteApiRoutes` (`src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs`, línea ~18) — mantiene el patrón de constantes del archivo.
- Reutiliza el `HttpClient` registrado (con `ApiBearerTokenHandler`) — sin tocar DI.
- Espejo estructural de `ListarPuestosAsync` (`VacanteApiClient.cs:82-96`).
- **`ListarPuestosAsync` se preserva**: otros consumers (potenciales + fakes en tests) lo siguen usando. No se marca obsoleto en este change.

## Capa Razor Pages

- **`Create.cshtml.cs` línea 232**: único cambio funcional — reemplazar `vacanteApiClient.ListarPuestosAsync(cancellationToken)` por `vacanteApiClient.ListarPuestosDisponiblesAsync(cancellationToken)`. Nombres de variable (`Puestos`, `PuestosReady`) y bloque `try/catch` de `TransportFailureClassifier` **sin cambios** — el path de "falla la carga de catálogos" sigue idéntico (escenario spec "Falla la carga de catálogos").
- **`Create.cshtml`**: **sin cambios** — el `<select>` itera `Model.Puestos` sin importar qué método lo pobló; no hay markup atado a "todos activos".

## Capa de tests

| Layer | Qué | Cómo | Archivo |
|---|---|---|---|
| Unit servicio | `ListarDisponiblesAsync` llama repo 1×; mapea entidades→DTOs vía `MapToDto`; vacío cuando no hay disponibles | `FakePuestoRepository.Datos` poblado; verificar `ListarDisponiblesAsync` invocado | `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs` (+ método en `FakePuestoRepository`) |
| Unit repo (MySQL) | Excluye soft-deleted; excluye puestos con Ocupación vigente; excluye puestos con Vacante abierta; caso combinado excluido por Ocupación; incluye Vacante Cubierta + Ocupación finalizada | `[MySqlFact]` discretos (precedente `PuestoRepositoryQueryAsyncTests`) | `tests/SGV.Tests/Persistencia/PuestoRepositoryListarDisponiblesTests.cs` (nuevo) |
| API | `GetDisponibles` 200 con `IReadOnlyList<PuestoDto>`; 401 sin auth | `ApiWebApplicationFactory` (o suite controller existente) | `tests/SGV.Tests/Api/PuestosControllerTests.cs` si existe; si no, seguir suite VacantesController como patrón |
| Web client | Happy 200 + ruta `/api/v1/puestos/disponibles`; 500 non-JSON → `HttpRequestException`; token pre-cancelado → `OperationCanceledException`; transport fails → native | Espejo de `VacanteApiClientListarPuestosTests` | `tests/SGV.Tests/Web/Vacantes/VacanteApiClientListarPuestosDisponiblesTests.cs` (nuevo) |
| Web page smoke | `Get_Create_WhenMutationRole_RendersFormWithCatalogs` pasa con la nueva llamada a `ListarPuestosDisponiblesAsync` (1×); `FakeVacanteApiClient` extendido con `ListarPuestosDisponiblesResult` | Actualizar fake + aserción | `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` (extender `FakeVacanteApiClient`) |

**Convención de nombres a confirmar en tasks**: para los `[MySqlFact]` del repo, el precedente `PuestoRepositoryQueryAsyncTests` usa métodos separados (no `[Theory]+[InlineData]`) — cada escenario con su setup de datos. Follow-up: confirmar nombres finales siguiendo `QueryAsync_MySql_*`.

## Archivos afectados

| Archivo | Tipo |
|---|---|
| `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoServicioConsulta.cs` | Modified |
| `src/SGV.Aplicacion/Organizacion/Consultas/PuestoServicioConsulta.cs` | Modified |
| `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoRepository.cs` | Modified |
| `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` | Modified |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modified |
| `src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs` | Modified (const `PuestosDisponiblesRoot`) |
| `src/SGV.Web/Integration/Vacantes/IVacanteApiClient.cs` | Modified |
| `src/SGV.Web/Integration/Vacantes/VacanteApiClient.cs` | Modified |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` | Modified (línea 232) |
| `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs` | Modified (+ `FakePuestoRepository`) |
| `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioComandosTests.cs` | Modified (stub en `FakePuestoWriteRepository`) |
| `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` | Modified (stub en `FakePuestoWriteRepository`) |
| `tests/SGV.Tests/Persistencia/PuestoRepositoryListarDisponiblesTests.cs` | New |
| `tests/SGV.Tests/Web/Vacantes/VacanteApiClientListarPuestosDisponiblesTests.cs` | New |
| `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` | Modified |

> Nota: el `proposal.md` listaba `IPuestoRepository` en `Dominio/Organizacion/` — verificación real lo halla en `Aplicacion/Organizacion/Consultas/IPuestoRepository.cs`. Lo confirmado en este design prevalece.

## Migración de datos

**No se requiere migración de datos.** Las columnas y constraints existentes (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`, `ActiveCodigoUnique`) cubren la query. Si el análisis de índices en §persistencia identifica un composite faltante que afecte materialmente el performance, se fila como follow-up change (`vacante-disponibles-index-tuning`, hipotético).

## Plan de rollback

1. Revertir la línea 232 de `Create.cshtml.cs` → `ListarPuestosAsync`.
2. Eliminar action `GetDisponibles` de `PuestosController`.
3. Eliminar `ListarDisponiblesAsync` de `IPuestoServicioConsulta`, `PuestoServicioConsulta`, `IPuestoRepository`, `PuestoRepository`.
4. Eliminar `ListarPuestosDisponiblesAsync` de `IVacanteApiClient`, `VacanteApiClient`; revertir const `PuestosDisponiblesRoot` en `VacanteApiRoutes`.
5. Revertir stubs en fakes y eliminar tests nuevos.

**Sin migración de datos que revertir.** Tests reverteen naturalmente. Cero efecto sobre otros módulos u otros consumers de `ListarPuestosAsync` (no se tocó).

## Open questions para tasks

- Nombres finales de los `[MySqlFact]` por escenario en el repo (precedente: `QueryAsync_MySql_<Context>_<Behavior>`).
- ¿Un único `[MySqlFact]` con setup combinado por escenario o un método por caso? `PuestoRepositoryQueryAsyncTests` usa un método por escenario — se seguirá ese precedente.
- Si existe `tests/SGV.Tests/Api/PuestosControllerTests.cs` (no verificado en este pase) o si los tests de API van en la suite `VacantesControllerTests`/`ApiWebApplicationFactory`. Confirmar al iniciar tasks.
- Cuantificar líneas estimadas: ~70 producción + 40 fakes/stubs ≈ **110 líneas cambiadas**; tests ≈ **220 líneas** (repo `[MySqlFact]` × 5 + web client × 4 + smoke adaptations).