# Tasks: Ajustes al listado de auditoría (issue #248)

## Review Workload Forecast

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 (Slice A) | Wire + hotfix | PR 1 | `dotnet test --filter "Auditoria"` | `dotnet ef database update` + `curl /api/v1/auditorias/{id}` admin | Revertir PR A + `dotnet ef migrations remove` |
| 2 (Slice B) | UI + Details + tests | PR 2 sobre PR 1 | `dotnet test --filter "Auditoria"` | `bun run build` + manual `/auditorias`, `/auditorias/details` | Revertir PR B (UI/Tests/docs) |

## Slice A — backend (PR 1)

- [x] **1.A.1 (RED)** `AuditoriaDto` sin `EntityId`/`OldValuesJson`/`NewValuesJson` (reflexión); `AuditoriaDetalleDto` los expone; `AuditoriaListQuery` con `Sort?`/`CorrelationId?`. Archivos: `AuditoriaServicioConsultaTests.cs`, `AuditoriasControllerTests.cs`.
- [x] **1.A.2 (GREEN)** Crear `AuditoriaDetalleDto.cs` (record 11 campos, `OccurredAt: DateTime`). Modificar `AuditoriaDto.cs` (quitar `EntityId`, agregar `UserName?`) y `AuditoriaListQuery.cs` (agregar `Sort?`+`CorrelationId?`).
- [x] **1.A.3 (RED)** Sort dinámico default/clave/inválido, `CorrelationId` exacto, LEFT JOIN `UserName` resuelto+fallback `"—"`, `GetDetalleDtoAsync` con old/new+`EntityId`. Archivo: `AuditoriaServicioConsultaTests.cs`.
- [x] **1.A.4 (GREEN)** Extender `IAuditoriaServicioConsulta.cs` con `GetDetalleDtoAsync`. Modificar `AuditoriaServicioConsulta.cs`: `switch(Sort)`+`ThenByDescending(Id)`, LEFT JOIN `AspNetUsers` con `DefaultIfEmpty()`+coalesce `"—"`, filtro `CorrelationId`, implementar `GetDetalleDtoAsync` con old/new+`EntityId`.
- [x] **1.A.5 (RED)** Test que valida índice compuesto en `AuditoriaConfiguracion`. Archivo: `AuditoriaServicioConsultaTests.cs`.
- [x] **1.A.6 (GREEN)** Modificar `AuditoriaConfiguracion.cs`: `HasIndex(e => new { e.CorrelationId, e.OccurredAt })`. Migración `IndiceAuditoriaCorrelationIdOccurredAt`; regenerar `docs/migracion-inicial-sgv.sql`.
- [x] **1.A.7 (RED)** API: `GetById` 200 `AuditoriaDetalleDto`/404/401/403; `sort` propagado. Archivo: `AuditoriasControllerTests.cs`.
- [x] **1.A.8 (GREEN)** Modificar `AuditoriasController.cs`: `GetById` retorna `AuditoriaDetalleDto` con `[ProducesResponseType(typeof(AuditoriaDetalleDto), StatusCodes.Status200OK)]`; `Get` propaga `Sort?`/`CorrelationId?`.
- [x] **1.A.9 (hotfix compat)** `Pages/Auditorias/Index.cshtml`: quitar `<th>ID entidad</th>`+celda `@item.EntityId`; quitar badge `Operation`; mostrar `UserName`. Renombrar `ObtenerPorIdAsync`→`GetDetalleAsync` retornando `AuditoriaDetalleDto?` en `FakeAuditoriaApiClient.cs`; ajustar `MakeAuditoriaDto` (con `UserName`, sin `EntityId`) en `AuditoriasIndexTests.cs`.
- [x] **1.A.10 (verify)** `dotnet build SGV.slnx` + `dotnet test SGV.slnx` (MySQL para `[MySqlFact]`); smoke admin `curl /api/v1/auditorias/{id}`.

## Slice B — web (PR 2, stacked sobre PR 1)

- [x] **1.B.1 (RED)** Web: sort reset `p=1`, selector pageSize 10/20/50/100, `BuildPagedRouteValues` preserva `sort`+`pageSize`, Details 200 `<pre>`/404/transporte/403. Archivos: `AuditoriasIndexTests.cs` (extender), `AuditoriasDetailsTests.cs` (nuevo).
- [x] **1.B.2 (GREEN)** Extender `IAuditoriaApiClient.cs` con `GetDetalleAsync(Guid, CancellationToken): Task<AuditoriaDetalleDto?>`. Implementar en `AuditoriaApiClient.cs`: `GetDetalleAsync` con 404→`null`; ampliar `BuildQueryUri` con `sort` y `correlationId`.
- [x] **1.B.3 (GREEN)** Rediseñar `Index.cshtml`: filtros horizontales (estilo `Habilidades/Index`); `<th>` ordenables con `GetSortIcon`/`GetSortRoute`; `<select name="pageSize">` 10/20/50/100; columna Usuario con `UserName`; `Operation` texto plano; paginación Anterior/Siguiente + números.
- [x] **1.B.4 (GREEN)** `Index.cshtml.cs`: bind `[FromQuery] Sort`, `CorrelationId`, `PageSize`; normalizar `PageSize` al set `{10,20,50,100}`→`DefaultPageSize` (20); `BuildSortRouteValues(sortKey)` (reset `p=1`, preserva filtros+`pageSize`); refactorizar `BuildPagedRouteValues` con `sort`+`pageSize` variable.
- [x] **1.B.5 (GREEN)** Crear `Details.cshtml` con `[Authorize(Roles="Administrador")]` en `.cshtml.cs`: header `EntityName`/`Operation`/`OccurredAt`/`UserName`/`CorrelationId`; old/new/changed en `<pre class="bg-light p-2">`; estado 404 legible; banner recuperable preservando `id`.
- [x] **1.B.6 (GREEN)** Crear `Details.cshtml.cs`: `OnGetAsync(Guid id, CancellationToken)` consume `GetDetalleAsync(id)`; `null`→404 legible; `TransportFailureClassifier` para banner recuperable.
- [x] **1.B.7 (verify)** `dotnet build SGV.slnx` + `dotnet test SGV.slnx` + `bun run build` en `src/SGV.Web`; manual `/auditorias?sort=entidad_asc&pageSize=50` y `/auditorias/details?id={guid}` admin/no-admin.
- [x] **1.B.8 (docs)** Documentar en `decisiones-implementacion.md`: D-5 bis (LEFT JOIN `UserName`, fallback `"—"`); D-6 (sort server-side vía `switch(Sort)`, default `fecha_desc`); D-7 (detalle admin con `AuditoriaDetalleDto` old/new+`EntityId`).