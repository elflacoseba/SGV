# Design: Filtrar estado Cubierta del dropdown de edición de Vacante

## Technical Approach

Extender el record posicional `EstadoVacanteDto` (hoy con 5 params: `Id, Codigo, Nombre, Orden, EsTerminal`) con un **6to** parámetro `bool EsCubierta` (corrige la cuenta del proposal, que decía "quinto"). Poblarlo en `MapToDto` desde `estado.EsCubierta` — campo ya existente en `EstadoVacante` (línea 35 del dominio). Filtrar `.Where(s => !s.EsCubierta)` en `EditModel.LoadStatesAsync` antes de asignar a `EstadosVacante`. Esto mapea a los requirements delta `vacante-web`: MODIFIED "Edit permite cambiar estado…" (dropdown sin Cubierta) y ADDED "Cubierta no es destino directo desde Edit".

## Architecture Decisions

### Decision: 6to parámetro posicional en el record existente
**Choice**: agregar `bool EsCubierta` al final de `EstadoVacanteDto`.
**Alternatives considered**: (a) record separado `EstadoVacanteEditDto`; (b) propiedad no-posicional adicional; (c) endpoint dedicado `/estados-vacante/editables`.
**Rationale**: añadir un parámetro posicional al final es no-rompedura a nivel wire (System.Text.Json tolera campos extra al deserializar en clientes viejos). Un DTO o endpoint separado es desproporcionado para un flag de UI. La contrapartida: cualquier ctor call existente con 5 args deja de compilar — afecta exactamente a dos sembrados de tests (`FakeVacanteApiClient.BuildStates()` y `FakeEstadoVacanteServicioConsulta` en `ApiWebApplicationFactory`), que deben actualizarse (cambio obligado, no opcional).

### Decision: Dónde aplicar el filtro
**Choice**: en `EditModel.LoadStatesAsync` antes de asignar `EstadosVacante`.
**Alternatives considered**: (a) `if` dentro del `foreach` en `Edit.cshtml`; (b) parámetro `soloEditables` en `EstadoVacanteServicioConsulta`; (c) filtrar en el controller de API.
**Rationale**: el PageModel es el owner de la presentación Edit; el catálogo del servicio sigue siendo "completo" (otros consumidores pueden necesitar Cubierta para lógica de transición o reportes). Filtrar en la vista expone el option al cliente y reproduce la trampa UX que justamente queremos eliminar. Filtrar en backend/API acopla un caso de uso UI a un servicio genérico.

### Decision: Actualizar los dos sembrados de tests (no opcional)
**Choice**: pasar `EsCubierta=true` solo para `Cubierta` y `false` para el resto en `BuildStates()` y en `FakeEstadoVacanteServicioConsulta`.
**Alternatives considered**: agregar overload con flag; builder fluido nuevo.
**Rationale**: el cambio es de una línea por estado en cada fake; no justifica nueva API. `FakeEstadoVacanteServicioConsulta` ya sembraba 4 estados con el ctor actual — basta añadir el sexto arg. El test `Estados_GetAll_Returns200WithFourStates` solo verifica `Count==4`, sigue verde sin cambios.

## Data Flow

```
GET /organizacion/vacantes/editar/{id}:
  EditModel.OnGetAsync(id)
    → LoadCurrentAsync(id)                          # GET /api/v1/vacantes/{id}
    → LoadStatesAsync(ct)                           # GET /api/v1/estados-vacante
        → EstadoVacanteServicioConsulta.ListarAsync()
            → MapToDto(estado)                      # ahora incluye EsCubierta
        → IList<EstadoVacanteDto>
    → EstadosVacante = estados.Where(s => !s.EsCubierta).ToList()   # FILTRO
    → return Page()
  Edit.cshtml renderiza <select> con options no cubiertos.

POST mismo path:
  Input.EstadoVacanteId ∈ {Abierta, EnSeleccion, Cancelada}
  → CambiarEstadoAsync (PATCH) no recibe destino Cubierta desde el form
  → el guard de PersonaId nunca se dispara desde esta UI.
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/EstadoVacanteDto.cs` | Modify | Agregar `bool EsCubierta` como 6to parámetro posicional. |
| `src/SGV.Aplicacion/Vacantes/Consultas/EstadoVacanteServicioConsulta.cs` | Modify | Poblar `EsCubierta` en `MapToDto`. |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs` | Modify | Filtrar `.Where(s => !s.EsCubierta)` en `LoadStatesAsync`. |
| `tests/SGV.Tests/Web/Vacantes/FakeVacanteApiClient.cs` | Modify | Actualizar `BuildStates()` con 6to arg (Cubierta=true, resto false). Required build. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modify | Actualizar seed de `FakeEstadoVacanteServicioConsulta` con 6to arg. Required build. |
| `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` | Modify | Nuevo `[Fact]` que verifica que el GET de Edit NO contiene option Cubierta y SÍ contiene Cancelada. |

## Interfaces / Contracts

`EstadoVacanteDto` queda:

```csharp
public sealed record EstadoVacanteDto(
    Guid Id,
    string Codigo,
    string Nombre,
    int Orden,
    bool EsTerminal,
    bool EsCubierta);
```

`MapToDto` en `EstadoVacanteServicioConsulta`:

```csharp
private static EstadoVacanteDto MapToDto(EstadoVacante e) =>
    new(e.Id, e.Codigo, e.Nombre, e.Orden, e.EsTerminal, e.EsCubierta);
```

`LoadStatesAsync` cambia una línea:

```csharp
EstadosVacante = (await vacanteApiClient.ListarEstadosAsync(cancellationToken))
    .Where(s => !s.EsCubierta).ToList();
CatalogsReady = true;
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (DTO/map) | `MapToDto` propaga `EsCubierta` desde la entidad | xUnit nuevo en `tests/SGV.Tests/Aplicacion/Vacantes/` si no existe cobertura del mapper. |
| Integration (web) | GET a Edit con catálogo mixto NO renderiza option Cubierta; SÍ renderiza Cancelada | xUnit nuevo en `VacantesCreateEditForbidTests` reusando `FakeVacanteApiClient.BuildStates()` actualizado. |
| Regresión | Tests existentes siguen verdes | `dotnet test SGV.slnx`. |

## Threat Matrix

N/A — no hay routing nuevo, shell, subprocess, VCS/PR automation ni clasificación de ejecutables. El cambio es UI-only sobre un DTO in-memory y un `Where` LINQ.

## Migration / Rollout

No migration required. Sin schema change. Backward-compatible a nivel wire: el campo nuevo va al final del JSON; los clientes que no lo conocen lo ignoran al deserializar.

## Open Questions

- ¿Hay otros consumers del `EstadoVacanteDto` (fuera de `SGV.Web` y los dos fakes identificados) que asuman la firma de 5 params? El grep de `new EstadoVacanteDto(` solo halló el mapper; los dos fakes están inventariados arriba. No se esperan otros, pero conviene confirmar en `sdd-tasks` antes del apply.
- ¿El DTO se loggea/serializa a JSON en algún sitio donde el flag nuevo filtre PII? `EsCubierta` es flag de dominio — no se espera PII.