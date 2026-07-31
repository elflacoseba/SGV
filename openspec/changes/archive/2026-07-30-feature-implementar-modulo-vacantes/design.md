# Design: Implementar el módulo de Vacantes

## Technical Approach

Slice C (API + Web básico) siguiendo el patrón de `Ocupaciones`: capa Contracts (leaf) → Aplicación (servicios + validadores) → Infra (repository + mappers) → API (controllers) → Web (Integration ApiClient + páginas). Dominio (`Vacante`, `EstadoVacante`, `HistorialEstadoVacante`), persistencia (`*Entity`, `*Configuracion`, `DbSet`s) y seed (`DatosSemilla` bloque `20000000-…`) ya existen — este change crea la capa de aplicación, contratos y UI. El alcance operativo de Vacantes es CRU: creación y consultas, con cambio de estado y cierre mediante PATCH de estado. Sin migración EF (las tablas `Vacantes`, `HistorialEstadosVacante`, `EstadosVacante` ya están en el `ModelSnapshot`). Se respeta `ErrorCategoria` canon (sin enum legacy `[Obsolete]`).

## Architecture Decisions

- **D-0 — Sin endpoints PUT/DELETE**: el alcance implementado es CRU (creación y consultas); el cambio de estado y cierre se realizan mediante `PATCH /{id}/estado`.

| ID | Opción | Tradeoff | Decisión |
|----|--------|----------|----------|
| D-1 | `VacanteCommandResult` con `ErrorCategoria` directo | Enum legacy `[Obsolete]` (patrón Ocupaciones) / directo canon | **Directo canon** — sin deuda; `VacanteError(ErrorCategoria, code, message)` |
| D-2 | Segmento `abiertas|cerradas|todas` via join `EstadoVacante.EsTerminal` | Proxy `FechaCierre == null` / join a catálogo | **Join `EsTerminal`** — fiel al spec ("terminal = Cubierta o Cancelada"); `FechaCierre` sólo para display |
| D-3 | `EstadosVacanteController` dedicado | Endpoint dentro de `VacantesController` | **Dedicado** — patrón `NivelesCargoController`/`TiposDocumentoController` |
| D-4 | Constante rol mutación `RolesSgvMutacion = Administrador,GestorVacantes` |Solo `Administrador` (Ocupaciones) | **Ambos roles** — PB-1 asumida; parametrizable en una sola constante |
| D-5 | Historial como owned navigation en misma `SaveChangesAsync` | Tabla independiente con UoW manual | **Mismo `DbContext`/UoW** — `Vacante.CambiarEstado` agrega a `_historialEstados` (EF tracked); atomicidad provista por EF en una transacción |
| D-6 | Catálogo `EstadoVacante` solo lectura vía `IEstadoVacanteServicioConsulta` | Gestión de catálogo | **Solo lectura** — catálogo inmutable; `GET /api/v1/estados-vacante` ordenado por `Orden` |

## Data Flow

```
Web Page ──> IVacanteApiClient ──HTTP──> VacantesController
                                              │
                          IVacanteServicioComandos / IVacanteServicioConsulta
                                              │
                                     IVacanteRepository (EF)
                                              │
             SgvDbContext ──> Vacantes / HistorialEstadosVacante / EstadosVacante
```

Cambio de estado: `Controller` → `ServicioComandos.CambiarEstadoAsync` → `repo.GetByIdForUpdateAsync` (tracked, con `Include(HistorialEstados).ThenInclude(EstadoAnterior/Nuevo)`) → `Vacante.CambiarEstado(estadoNuevoId, usuarioId, motivo, cerrar: estadoNuevo.EsTerminal)` (muta `EstadoVacanteId` + `FechaCierre` si terminal + agrega `HistorialEstadoVacante` a la colección) → `unitOfWork.SaveChangesAsync` (una transacción persiste vacante + nuevo row de historial).

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs` | Create | `Base="api/v1/vacantes"`, `EstadosVacanteBase="api/v1/estados-vacante"`, sort whitelist |
| `src/SGV.Contracts/Vacantes/Comandos/{CrearVacanteRequest,CambiarEstadoVacanteRequest}.cs` | Create | Records wire |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteCommandResult.cs` | Create | `VacanteError(ErrorCategoria,code,msg)` + Success/Failure |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/{VacanteDto,VacanteDetailDto,HistorialEstadoVacanteDto,EstadoVacanteDto}.cs` | Create | DTOs consumer-safe (sin campos auditoría) |
| `src/SGV.Contracts/Vacantes/Consultas/{VacanteListQuery,VacanteSegmentoListado}.cs` | Create | Query + enum (Abiertas/Cerradas/Todas) |
| `src/SGV.Aplicacion/Vacantes/Consultas/{IVacanteRepository,IVacanteServicioConsulta,IEstadoVacanteServicioConsulta}.cs` + impls | Create | Consultas transaccionales |
| `src/SGV.Aplicacion/Vacantes/Comandos/{IVacanteServicioComandos,VacanteServicioComandos}.cs` + `Validaciones/` | Create | `CrearAsync`, `CambiarEstadoAsync` + `IConstraintViolationDetector` + `FluentValidation` |
| `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` | Create | Hereda `ReadOnlyRepository<VacanteEntity,Vacante>`; `Include(Puesto).Include(EstadoVacante).Include(HistorialEstados).ThenInclude(EstadoAnterior/EstadoNuevo)`; `ExistsAbiertaByPuestoAsync` (join `EsTerminal==false`) |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Modify | `ToDomain(VacanteEntity)`, `ToDomain(EstadoVacanteEntity)`, `ToDomain(HistorialEstadoVacanteEntity)` |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modify | `ToEntity(Vacante)`, `UpdateEntity` |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modify | `AddScoped` de `IVacanteRepository`, `IVacanteServicioConsulta`, `IVacanteServicioComandos`, `IEstadoVacanteServicioConsulta` |
| `src/SGV.Api/Controllers/VacantesController.cs` | Create | `[ApiController][Authorize]`; GET/GET{id}/POST/PATCH{id}/estado |
| `src/SGV.Api/Controllers/EstadosVacanteController.cs` | Create | `[Authorize] GET api/v1/estados-vacante` |
| `src/SGV.Web/Integration/Vacantes/{IVacanteApiClient,VacanteApiClient,VacanteInputModel,VacanteListItemViewModel,VacanteDetailViewModel}.cs` | Create | ApiClient (`ListarAsync`/`ObtenerPorIdAsync`/`CrearAsync`/`CambiarEstadoAsync`/`ListarEstadosAsync`) + VMs |
| `src/SGV.Web/Program.cs` | Modify | `AddHttpClient<IVacanteApiClient,VacanteApiClient>` (10s, paralelo a Ocupaciones) |
| `src/SGV.Web/Pages/Organizacion/Vacantes/{Index,Create,Edit,Details}.cshtml(.cs)` | Create | PageModels `[Authorize]`; mutaciones re-validan rol → `Forbid()`; PRG a Details |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modify | Grupo "Vacantes" tras Ocupaciones; subítem "Nueva" gated por `esAdministrador \|\| User.IsInRole(GestorVacantes)` |
| `docs/decisiones-implementacion.md` | Modify | Añadir row `20000000-…` `EstadoVacante` al mapa de bloques GUID |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicio*Tests.cs` | Create | Unit (crear, conflict puesto-abierta, terminal inmutable, atomicidad, segmentación) |
| `tests/SGV.Tests/Api/VacantesControllerTests.cs` | Create | Integration (201/400/403/404/409/401, detalle+historial, segmento no mezcla) |
| `tests/SGV.Tests/Web/Vacantes*Smoke.cs` | Create | Index carga; Create redirige 403 sin rol |

## Interfaces / Contracts

```csharp
public sealed record VacanteDto(Guid Id, Guid PuestoId, string PuestoNombre,
    Guid EstadoVacanteId, string EstadoVacanteNombre,
    DateTime FechaApertura, DateTime? FechaCierre, string Motivo, string? Observaciones);
public sealed record VacanteDetailDto(Guid Id, Guid PuestoId, string PuestoNombre,
    Guid EstadoVacanteId, string EstadoVacanteNombre, DateTime FechaApertura,
    DateTime? FechaCierre, string Motivo, string? Observaciones,
    IReadOnlyList<HistorialEstadoVacanteDto> Historial);
public sealed record HistorialEstadoVacanteDto(string? EstadoAnteriorNombre,
    string EstadoNuevoNombre, DateTime ChangedAt, string? ChangedByUserId, string? Motivo);
public sealed record EstadoVacanteDto(Guid Id, string Codigo, string Nombre, int Orden, bool EsTerminal);
public sealed record CrearVacanteRequest(Guid PuestoId, Guid EstadoVacanteId, DateTime FechaApertura, string Motivo, string? Observaciones);
public sealed record CambiarEstadoVacanteRequest(Guid EstadoVacanteId, string? Motivo);
public enum VacanteSegmentoListado { Abiertas=0, Cerradas=1, Todas=2 }
public sealed record VacanteError(ErrorCategoria Categoria, string Code, string Message);
public sealed record VacanteCommandResult(bool IsSuccess, VacanteDetailDto? Value, VacanteError? Error,
    IReadOnlyDictionary<string,string[]>? FieldErrors=null);
```

## Testing Strategy

| Layer | Qué | Cómo |
|-------|-----|------|
| Unit | `CrearAsync` (validación, PuestoId inexistente→404/V, conflict puesto con vacante abierta→409), `CambiarEstadoAsync` (terminal setea FechaCierre + inserta historial, terminal inmutable→409, atomicidad rollback), segmentos no mezclan | Fakes de `IVacanteRepository`/`IPuestoRepository` (patrón `FakeOcupacionWriteRepository`) |
| Integration | VacantesController: 201/400/403(PB-1)/401/404/409; `GET /estados-vacante` 4 estados 200; `?status=invalido` normalize→abiertas | `ApiWebApplicationFactory` + Fakes (`RemoveService`/`AddSingleton`) — paridad `OcupacionesControllerTests` |
| Async DB | Repository segmento + atomicidad historial (opcional) | `[MySqlFact]` en `VacanteRepositoryQueryTests` |
| Web | Index carga (200), Create sin rol → `Forbid()`/`/error/403`, catálogos fallan → estado recuperable | `SgvWebApplicationFactory` |

## Threat Matrix

N/A — sin routing/shell/subprocess/VCS/PR automation/process-integration boundary.

## Migration / Rollout

**Sin migración EF**: tablas `Vacantes`, `HistorialEstadosVacante`, `EstadosVacante` ya existen en `SgvDbContextModelSnapshot`. Sólo se actualiza `docs/decisiones-implementacion.md` (mapa bloques GUID `20000000-…`).
**Rollback**: revertir commits de la rama `feature/implementar-modulo-vacantes`; desregistrar 4 servicios en `DependencyInjection.cs`; eliminar `VacantesController`/`EstadosVacanteController`, `Integration/Vacantes/`, `Pages/Organizacion/Vacantes/`, entrada `_Sidenav`; restaurar mapa GUID. No toca `develop` ni `main`.

## Slicing (sugerido para apply)

- **Slice 1 (backend)**: Contracts → Mappers → `VacanteRepository` → Aplicación → Controllers + tests API + docs GUID. PR auto-contenido.
- **Slice 2 (web)**: `Integration/Vacantes/` + Pages + `_Sidenav` + smoke tests. Consume Slice 1.

## PB-1 a PB-5 — Supuestos y confirmación

| PB | Supuesto del diseño | Cómo confirmar/parametrizar antes de apply |
|----|---------------------|-------------------------------------------|
| PB-1 | Mutaciones requieren `Administrador` **o** `GestorVacantes` | Constante `RolesSgvMutacion` centralizada; cambiar un solo literal si negocio reduce a solo `Administrador`. Validar con stakeholder antes de apply. |
| PB-2 | Sin "Crear Vacante" en `Puestos/Details` | Confirmar; Slice 2 web explícitamente no lo añade. |
| PB-3 | `Motivo` opcional al cerrar (spec assume) | Si negocio lo exige → `CambiarEstadoVacanteRequestValidator` agrega `.When(EstadoNuevo.EsTerminal)`. Flag de config. |
| PB-4 | `Details` muestra `HistorialEstadoVacante` | Confirmar; ya implementado en `VacanteDetailDto`. |
| PB-5 | Default `abiertas` | Confirmar; controlador normaliza `string.IsNullOrEmpty(status) \|\| invalid → Abiertas`. |

## Open Questions

- [ ] **OQ-1 (BLOCKING para Edit web)**: `Vacante` (dominio) **no expone setter de `Observaciones`**. La spec web (`vacante-web` REQ "Edit permite cambiar … Observaciones") requiere editarlas. Opciones: (a) añadir `Vacante.ActualizarObservaciones(string?)` al dominio en este change, o (b) limitar Edit web a solo "cambiar estado" (sin Observaciones). **Decisión necesaria antes de tasks**. — *Señala que la spec asume capacidad que el dominio actual no provee.*
- [ ] OQ-2: ¿`EstadoVacanteConstantes.cs` en `Catalogos/`? La regla operativa de bloques GUID lo pide; hoy las constantes viven en `DatosSemilla` directas. Recomendado: crear `src/SGV.Infraestructura/Persistencia/Catalogos/EstadoVacanteConstantes.cs` + test paridad (patrón `DatosSemilla_*_SeedIdsMatchConstantes`).
- [ ] OQ-3: ¿`PATCH /vacantes/{id}/estado` incluye `Observaciones` en el request o va en canal separado? Spec mgmt sólo menciona `EstadoVacanteId` + `Motivo`. Diseño asume `CambiarEstadoVacanteRequest(EstadoVacanteId, Motivo?)`.

## Notes para tasks/apply

- `IConstraintViolationDetector` ya registrado ( usado por Ocupaciones ) — reutilizar.
- `IUnitOfWork` ya registrado; reutilizar.
- El subítem "Nueva" en `_Sidenav` debe usar `esGestorOVacio = esAdministrador || User.IsInRole(RolesSgv.GestorVacantes)` (contrario a Ocupaciones que sólo usa `esAdministrador`). Sin gate por rol para "Listado" (cualquier autenticado ve Index).
- No mezclar segmentos jamás: repository `Where` por `EsTerminal` excluye el otro conjunto — RED test obligatorio.