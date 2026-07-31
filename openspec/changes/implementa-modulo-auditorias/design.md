# Design: Implementa el módulo de Auditorias

## Technical Approach

Construir la **capa de lectura** de auditoría reutilizando la tabla `Auditorias` y el `SgvDbContext.Auditorias` ya existentes. Se añade un read-port en `SGV.Aplicacion`, una implementación EF directa en `SGV.Infraestructura` (mismo patrón que la escritura `AuditoriaServicio`), un controller admin-only, un cliente HTTP y una Razor Page de solo lectura. La proyección `AuditoriaEntity → AuditoriaDto` omitirá `OldValuesJson`/`NewValuesJson` por construcción, garantizando que la PII nunca atraviese las fronteras wire. Las consultas no se auditan porque el interceptor opera sólo en `SavingChanges` (escritura).

## Architecture Decisions

### D-1: Implementación del servicio de consulta en Infraestructura, no en Aplicación

| Opción | Tradeoff | Decisión |
|---|---|---|
| Impl en `SGV.Aplicacion` (lo que dice el proposal) | `Aplicacion` no puede depender de EF/`SgvDbContext` (rompe grafo `Aplicacion ← Infraestructura`) — Clean Architecture | ❌ Rejected |
| Impl en `SGV.Infraestructura`, port en `SGV.Aplicacion` | Replica exacta del par `IAuditoriaServicio`(App)/`AuditoriaServicio`(Infra). EF directo sin repositorio, sin romper capas | ✅ |

**Rationale**: la evidencia real (`CargoServicioConsulta`, `UnidadOrganizativaServicioConsulta`) usa repositorio, no EF directo. `Aplicacion` declara sólo el port; la impl EF vive en `Infraestructura`. El precedente del propio módulo — `AuditoriaServicio` escritura — confirma el patrón. El proposal ubicó la impl en Aplicación; se corrige aquí para preservar la separación de capas sin añadir un repositorio superfluo.

### D-2: Proyección segura (sin old/new values)

| Opción | Tradeoff | Decisión |
|---|---|---|
| `AuditoriaDto` con todos los campos | Riesgo de fuga de PII | ❌ |
| `AuditoriaDto` con metadatos + `ChangedPropertiesJson` | Expone "qué cambió" sin valores | ✅ |

**Rationale**: `MapToDto` es un `Select` explícito campo-a-campo desde la entidad; el compilador garantiza que `OldValuesJson`/`NewValuesJson` nunca se copian. No hay mapeo automático (AutoMapper/`ProjectTo`) que pudiera arrastrarlos. La proyección se hace **dentro** de `AuditoriaServicioConsulta` (en la IQueryable con `Select` antes de materializar) para que las columnas sensibles ni siquiera viajen desde MySQL en el listado.

### D-3: Orden determinista, paginación y validación de rangos

| Aspecto | Decisión |
|---|---|
| Orden por defecto | `ORDER BY OccurredAt DESC, Id DESC` (Id como tiebreaker determinista — índice PK cubre) |
| Paginación | `Page >= 1`, `PageSize` clampeado a `[1,100]` |
| Rango fechas | `DateFrom <= DateTo`; si `DateFrom > DateTo`, el servicio lanza `ArgumentException` y el controller responde `400 Validation` con un contrato observable coherente (mensaje explícito de rango invertido); NO se devuelve conjunto vacío |
| Filtros | `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId` (todos opcionales) |

### D-4: No-auditoría de consultas

Las consultas no invocan `SaveChanges`/`SaveChangesAsync`; el `AuditoriaSaveChangesInterceptor.SavingChanges` no se dispara en lecturas `AsNoTracking()`. No se requiere lógica especial: el diseño garantiza por construcción que leer `Auditorias` no genera registros.

### D-5: `UserId` crudo en v1; enriquecimiento con nombre fuera de alcance

| Opción | Tradeoff | Decisión |
|---|---|---|
| `UserId` enriquecido (JOIN con `AspNetUsers` para exponer nombre) | Requiere JOIN contra Identity y nuevo índice; acopla lectura de auditoría al esquema de usuarios; coste de query y mantenimiento | ❌ (futuro) |
| `UserId` crudo (string tal cual se persistió) | Sin JOIN, sin nuevo índice, sin acoplamiento; el cliente resuelve el nombre si lo necesita | ✅ v1 |

**Rationale**: v1 expone `UserId` tal cual vive en `Auditorias.UserId`. El enriquecimiento con nombre legible queda explícitamente fuera de alcance y se reserva para una evolución posterior (v2+), donde se evaluará JOIN, caché o proyección desnormalizada. Esta decisión cierra la pregunta previa y mantiene el alcance de lectura sin tocar la escritura ni el esquema de Identity.

## Data Flow

```
 Admin (cookie) → Pages/Auditorias/Index (OnGetAsync)
        │                    │
        │                    └─ IAuditoriaApiClient.QueryAsync (HttpClient + ApiBearerTokenHandler → JWT)
        ▼                                          │
 AuditoriasController [Authorize(Roles=Admin)]      ▼
        │                                  GET /api/v1/auditorias?...&page&pageSize
        ▼                                          │
 IAuditoriaServicioConsulta              SGV.Api (bearer)
        │                                          │
        ▼                                          ▼
 AuditoriaServicioConsulta (Infra) → SgvDbContext.Auditorias
        │  .AsNoTracking().Where(...).OrderBy(OccurredAt DESC, Id DESC)
        │  .Select(AuditoriaDto { ... sin Old/New })   ← proyección segura
        ▼
 PagedResult<AuditoriaDto> → Api → Web → tabla Razor (server-side pagination)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` | Create | Record: `Id, EntityName, EntityId, Operation, OccurredAt, UserId, ChangedPropertiesJson, CorrelationId` |
| `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | Create | Record: `Page, PageSize, EntityName?, Operation?, DateFrom?, DateTo?, UserId?` |
| `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | Create | Port: `QueryAsync`, `GetByIdAsync` |
| `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | Create | Impl EF directa con `SgvDbContext`, proyección `Select` segura, filtros, orden determinista |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modify | `services.AddScoped<IAuditoriaServicioConsulta, AuditoriaServicioConsulta>()` |
| `src/SGV.Api/Controllers/AuditoriasController.cs` | Create | `GET /api/v1/auditorias` + `GET /{id:guid}`, `[Authorize(Roles = RolesSgv.Administrador)]` |
| `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | Create | `QueryAsync`, `ObtenerPorIdAsync` |
| `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | Create | HttpClient con `EnsureSuccessStatusCode`; 404→`null`; NotFound distinguible |
| `src/SGV.Web/Program.cs` | Modify | `AddHttpClient<IAuditoriaApiClient, AuditoriaApiClient>(...).AddHttpMessageHandler(ApiBearerTokenHandler)` |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml{,.cs}` | Create | PageModel `[Authorize(Roles=RolesSgv.Administrador)]`, tabla paginada + sidebar filtros |
| `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | Create | Unit: filtros, orden determinista, rango inválido, proyección sin PII |
| `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | Create | Integración: 401 sin creds, 403 non-admin, 200 admin, paginación, detalle 404 |
| `tests/SGV.Tests/Web/AuditoriasIndexTests.cs` | Create | Seam PageModel: transporte/401/403→estado de error recuperable |
| `docs/decisiones-implementacion.md` | Modify | Documenta módulo transversal de auditoría (lectura) |

## Interfaces / Contracts

```csharp
// SGV.Contracts/Auditoria/AuditoriaDto.cs
public sealed record AuditoriaDto(
    Guid Id, string EntityName, string EntityId, string Operation,
    DateTime OccurredAt, string? UserId, string? ChangedPropertiesJson,
    Guid? CorrelationId);

public sealed record AuditoriaListQuery(
    int Page = 1, int PageSize = 20,
    string? EntityName = null, string? Operation = null,
    DateTime? DateFrom = null, DateTime? DateTo = null, string? UserId = null);

// SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs
public interface IAuditoriaServicioConsulta {
    Task<PagedResult<AuditoriaDto>> QueryAsync(AuditoriaListQuery q, CancellationToken ct = default);
    Task<AuditoriaDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
```

Proyección segura (no-obvia): `_context.Auditorias.AsNoTracking().Where(...).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Select(a => new AuditoriaDto(a.Id, a.EntityName, a.EntityId, a.Operation, a.OccurredAt, a.UserId, a.ChangedPropertiesJson, a.CorrelationId))` — los campos `OldValuesJson`/`NewValuesJson` no aparecen en el `Select`, por lo que EF no los proyecta en SQL.

## Testing Strategy

| Layer | What | How |
|-------|------|-----|
| Unit (Dominio/App) | `AuditoriaServicioConsulta`: filtros, orden determinista, `DateFrom>DateTo`→400, DTO sin PII | `[MySqlFact]` (DB real) con fixture sembrada; verifica que `AuditoriaDto` no expone old/new |
| Integration (API) | `AuditoriasController`: 401 sin creds, 403 non-admin, 200+paginación admin, 404 detalle | `ApiWebApplicationFactory` + `CreateAdminClient`/`CreateNonAdminClient` (mismo fixture que `CargosControllerTests`) |
| Web (seam) | PageModel: fallo transporte/401/403 → estado de error recuperable | `SgvWebApplicationFactory` reemplazando `IAuditoriaApiClient` |

## Threat Matrix

N/A — no routing/shell/subprocess/VCS/executable-classification/process-integration. El límite relevante (no recursión de auditoría) está garantizado por construcción: la lectura no invoca `SaveChanges`, por lo que `AuditoriaSaveChangesInterceptor.SavingChanges` no se dispara. RED test: una consulta `QueryAsync` no inserta filas en `Auditorias`.

## Migration / Rollout

No se requiere migración: la tabla `Auditorias` ya existe con índices adecuados (`EntityName+EntityId+OccurredAt`, `UserId+OccurredAt`, `CorrelationId`). El filtro `UserId + OccurredAt` (orden: igualdad `UserId`, rango `OccurredAt`, sort `OccurredAt`) usa el índice leftmost. La escritura existente no se modifica. Rollback: eliminar los archivos nuevos (sin tocar `AuditoriaEntity`/interceptor/servicio de escritura).

## Review Workload Forecast (slices 400-line budget)

| Slice | Contenido | Líneas est. |
|---|---|---|
| S1 — Contracts + port + impl + tests unit | `AuditoriaDto`, `AuditoriaListQuery`, `IAuditoriaServicioConsulta`, `AuditoriaServicioConsulta`, unit tests, DI | ~180 |
| S2 — Controller + API integration tests | `AuditoriasController`, `AuditoriasControllerTests` | ~150 |
| S3 — Web client + PageModel/UI + web tests | `IAuditoriaApiClient`, `AuditoriaApiClient`, `Index.cshtml{.cs}`, Web Program.cs, web tests, docs | ~200 |

**Decision needed before apply**: Sí (delivery_strategy `ask-always`). **Chained PRs recommended**: Sí (Medium risk; cada slice es autónomo, verificable y reversible; S1 es base de S2/S3). **400-line budget risk**: Medium.

## Open Questions

- Ninguna. La pregunta sobre `UserId` crudo vs enriquecido quedó cerrada en D-5: v1 mantiene `UserId` crudo; el enriquecimiento con nombre queda fuera de alcance (evolución futura v2+).