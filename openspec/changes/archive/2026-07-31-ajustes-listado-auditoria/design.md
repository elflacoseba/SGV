# Design: Ajustes al listado de auditoría (issue #248)

## Enfoque técnico

Extensión incremental con **DTO separado para detalle** (Approach A de la propuesta). El listado conserva `AuditoriaDto` (sin `EntityId`, con `UserName?` por LEFT JOIN) y nunca expone `OldValuesJson`/`NewValuesJson`. El nuevo `AuditoriaDetalleDto` es la única superficie del wire que porta `EntityId`, old/new y `UserName`. El sort se resuelve server-side con `switch` sobre `Sort`; la shell normaliza `PageSize` al conjunto `{10,20,50,100}` y crea `Details` que consume `GetDetalleAsync`. Se entrega en **2 slices stacked-to-main** hoy con contrato wire final en el A.

## Decisiones de arquitectura

| # | Decisión | Tradeoff | Elección |
|---|----------|----------|----------|
| D-1 | Exposición de old/new + `EntityId` | Un solo DTO con flag de proyección (riesgo de fuga, rompe D-2) vs. dos tipos físicos (más tipos) | **DTO separado `AuditoriaDetalleDto`** en `SGV.Contracts.Auditoria`; D-2 cerrado por tipo, no por convención |
| D-2 | Sort server-side | OrderBy arbitrario por string vs. `switch` enum de claves conocidas | `switch` expresión sobre `Sort` con **default `fecha_desc`** si es null/vacío/inválido (sin 400); `ThenByDescending(Id)` como desempate determinista |
| D-3 | `UserName` del actor | Subquery `AspNetUsers` vs. LEFT JOIN en la misma query | **LEFT JOIN** con `DefaultIfEmpty()` proyectando `UserName`; fallback `"—"` en la proyección cuando no hay fila |
| D-4 | Normalización de `Sort`/`PageSize` | Rechazar input inválido vs. degradar a default | `Sort` no reconocido → `fecha_desc` (sin error); `PageSize` fuera de `{10,20,50,100}` → `20` en shell; API mantiene clamp `1–100` |
| D-5 | Tipo de `OccurredAt` | `DateTimeOffset` (spec detalle) vs. `DateTime` (dominio/entity/tabla/DTO vigente) | **`DateTime`** en ambos DTOs para consistencia con `AuditoriaEntity.OccurredAt`, el dominio y la columna MySQL. La delta spec de detalle que indica `DateTimeOffset` se interpreta como `DateTime` (ver Open Questions) |
| D-6 | API `GetById` | Mantener `AuditoriaDto` vs. cambiar retorno a `AuditoriaDetalleDto` | Cambiar retorno a `AuditoriaDetalleDto`; breaking change admin-only documentado |
| D-7 | Cliente `ObtenerPorIdAsync` | Renombrar vs. agregar paralelo | **Renombrar a `GetDetalleAsync`** que retorna `AuditoriaDetalleDto?`; el método actual no es consumido por `Index` (sólo `QueryAsync`), por lo que rename es seguro |

## Flujo de datos

```
Index OnGetAsync ──AuditoriaListQuery(Sort,CorrelationId,PageSize)──▶ AuditoriaApiClient.BuildQueryUri
        │                                                              │ QueryAsync
        ▼                                                              ▼
   AuditoriasController.Get ──▶ AuditoriaServicioConsulta.QueryAsync
                                  │ AsNoTracking + switch(Sort) + LEFT JOIN AspNetUsers
                                  ▼
                            PagedResult<AuditoriaDto>  (sin EntityId/old/new; con UserName)

Details OnGetAsync(id) ──GetDetalleAsync──▶ AuditoriasController.GetById ──▶ GetDetalleDtoAsync
                                             │ AsNoTracking + proyección completa
                                             ▼
                                       AuditoriaDetalleDto (EntityId + old/new + UserName)
```

## Cambios por archivo

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | Modificar | Agregar `Sort?` (`string?`), `CorrelationId?` (`Guid?`); `PageSize` default 20 |
| `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` | Modificar | Quitar `EntityId`; agregar `UserName?`; firma: `(Id,EntityName,Operation,OccurredAt,UserId,UserName,ChangedPropertiesJson,CorrelationId)` |
| `src/SGV.Contracts/Auditoria/AuditoriaDetalleDto.cs` | Crear | `(Id,EntityName,EntityId,Operation,OccurredAt,UserId,UserName,CorrelationId,ChangedPropertiesJson,OldValuesJson?,NewValuesJson?)` |
| `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | Modificar | Agregar `GetDetalleDtoAsync(Guid,CancellationToken):AuditoriaDetalleDto?` |
| `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | Modificar | `switch` dinámico de `Sort`; LEFT JOIN `AspNetUsers.UserName` con `DefaultIfEmpty()` y coalesce `"—"`; filtro exacto `CorrelationId`; nuevo `GetDetalleDtoAsync` con proyección completa |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/AuditoriaConfiguracion.cs` | Modificar | Agregar índice covering `HasIndex(e => new { e.CorrelationId, e.OccurredAt })` (ya existe índice simple por `CorrelationId`) |
| Migración EF Core (`Persistencia/Migraciones`) | Crear | `dotnet ef migrations add IndiceAuditoriaCorrelationIdOccurredAt` |
| `src/SGV.Api/Controllers/AuditoriasController.cs` | Modificar | Propagar `sort`/`correlationId` (binding `[FromQuery] AuditoriaListQuery`); `GetById` retorna `AuditoriaDetalleDto`; docs HTTP |
| `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | Modificar | Renombrar `ObtenerPorIdAsync` → `GetDetalleAsync(Guid):AuditoriaDetalleDto?` |
| `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | Modificar | `GetDetalleAsync` deserializa `AuditoriaDetalleDto`, 404→`null`; `BuildQueryUri` agrega `sort` y `correlationId` |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml` | Modificar (Slice A: hotfix; Slice B: rediseño) | **Slice A**: quitar columna/celda `EntityId` para compilar. **Slice B**: filtros horizontales (toolbar estilo `Cargos/Index`), `<th>` ordenables con indicador `asc/desc`, `<select>` pageSize 10/20/50/100, `Operation` a texto plano sin badge |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | Modificar | **Slice B**: bind `Sort`, `CorrelationId`, `PageSize` (normaliza a `{10,20,50,100}`→20); `BuildSortRouteValues` (reset `p=1`, preserva `pageSize`); `BuildPagedRouteValues` incluye `sort` y `pageSize` variable (no más `DefaultPageSize` hardcodeado) |
| `src/SGV.Web/Pages/Auditorias/Details.cshtml` + `.cshtml.cs` | Crear | Ruta `/auditorias/details?id={guid}`, `[Authorize(Roles="Administrador")]`, consume `GetDetalleAsync`, `<pre class="bg-light p-2">` para old/new JSON; estados legibles para 404 y fallo de transporte preserving `id` |
| `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | Modificar | sort dinámico (default + cada clave + inválido), `CorrelationId` exacto, LEFT JOIN `UserName` (resuelto y fallback `"—"`), `GetDetalleDtoAsync` con old/new + `EntityId` |
| `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | Modificar | `GetById` retorna `AuditoriaDetalleDto` 200/404; 401/403 detalle; `sort` propagado; `AuditoriaDto` sin `EntityId`/old/new por reflexión |
| `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs` + `AuditoriasIndexTests.cs` | Modificar (Slice A: hotfix; Slice B: ampliar) | **Slice A**: actualizar `MakeAuditoriaDto` (sin `EntityId`, +`UserName`), fake `ObtenerPorId*` → `GetDetalle*` con `AuditoriaDetalleDto`. **Slice B**: tests sort/reset page/selector pageSize/`Details` |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | Crear (Slice B) | Details 200 `<pre>`, 404 legible, fallo de transporte, no-admin 403 |
| `docs/decisiones-implementacion.md` | Modificar | D-5 bis (enriquecimiento `UserName`), D-6 (sort server-side), D-7 (detalle admin con old/new) |

## Interfaces / Contratos

```csharp
// AuditoriaListQuery (record): agrega Sort?, CorrelationId?; PageSize default 20
// AuditoriaDto (record): (Guid Id, string EntityName, string Operation, DateTime OccurredAt,
//   string? UserId, string? UserName, string? ChangedPropertiesJson, Guid? CorrelationId)
// AuditoriaDetalleDto (record): (Guid Id, string EntityName, string EntityId, string Operation,
//   DateTime OccurredAt, string? UserId, string? UserName, Guid? CorrelationId,
//   string? ChangedPropertiesJson, string? OldValuesJson, string? NewValuesJson)
```

Sort dinámico (no-obvio): `switch (query.Sort) { "fecha_asc" =>.OrderBy(a=>a.OccurredAt), "fecha_desc"=>OrderByDescending, "entidad_asc"=>OrderBy(a=>a.EntityName), … _ =>OrderByDescending(a=>a.OccurredAt) }` seguido de `.ThenByDescending(a=>a.Id)` como tiebreak permanente. El LEFT JOIN usa `from u in context.Set<IdentityUser>().Where(u=>u.Id==a.UserId).DefaultIfEmpty()` (o `GroupJoin`) y proyecta `UserName: u!=null?u.UserName:"—"`.

## Estrategia de testing

| Capa | Qué probar | Cómo |
|------|-----------|-----|
| Aplicación/Persistencia | sort (default/clave/inválido), `CorrelationId` exacto, `UserName` resuelto/fallback, `GetDetalleDtoAsync` old/new+`EntityId`, DTO listado sin old/new/EntityId (serialización) | `[MySqlFact]` con `TestSgvDbContextFactory` + fixtures in-memory (`AuditoriaServicioConsulta` real) |
| API | `GetById` 200 `AuditoriaDetalleDto`/404/401/403; `sort` propagado; reflexión sin `EntityId`/old/new en `AuditoriaDto` | `ApiWebApplicationFactory` + `FakeAuditoriaServicioConsulta` |
| Web | sort resetea `p=1`; selector pageSize; `BuildPagedRouteValues`/`BuildSortRouteValues` preservan sort/pageSize; `Details` 200 `<pre>`/404 legible/transporte/no-admin | `SgvWebApplicationFactory` + `FakeAuditoriaApiClient` |

## Matriz de amenazas

N/A — no routing, shell, subprocess, VCS/PR automation, executable classification ni process integration. El cambio es CRUD/persistencia/UI sobre endpoints ya autorizados (`Administrador`).

## Migración / Rollout

Migración EF Core **sólo agregar índice** `IX_Auditorias_CorrelationId_OccurredAt` covering `(CorrelationId, OccurredAt)` para evitar `Using filesort` en `sort=correlacion_desc` + filtro por `CorrelationId`. **No se altera esquema de columnas** de `Auditorias` (LEFT JOIN reusa `AspNetUsers`). Pre-validación `EXPLAIN` con datos reales vía `[MySqlFact]`; si el plan muestra `Using filesort` confirmar índice. Rollback: revertir Contracts/servicio/controller + `dotnet ef migrations remove` (sin cambios de columna, reversible sin pérdida de datos).

## Corte de PR (stacked-to-main, 2 slices)

### Slice A — backend + contrato wire final + compat mínima web
- Contracts (`AuditoriaListQuery`, `AuditoriaDto`, `AuditoriaDetalleDto`), Aplicación (`IAuditoriaServicioConsulta` + `GetDetalleDtoAsync`), Infraestructura (servicio con sort/LEFT JOIN/`GetDetalleDtoAsync`, `AuditoriaConfiguracion` + índice, migración), API (`AuditoriasController`), Web/Integration (`IAuditoriaApiClient`/`AuditoriaApiClient`: `GetDetalleAsync` + `BuildQueryUri` con sort/correlationId).
- **Hotfix de compat (mínimo, para compilar tras merge a main)**: `Index.cshtml` quitar columna/celda `@item.EntityId`; `MakeAuditoriaDto`/`FakeAuditoriaApiClient` actualizar firma (sin `EntityId`, +`UserName`, rename `ObtenerPorId`→`GetDetalle`).
- Tests: Aplicación (`AuditoriaServicioConsultaTests`) + API (`AuditoriasControllerTests`).

### Slice B — UI web + Details + tests Web nuevos
- `Index.cshtml` rediseño completo (filtros horizontales, `<th>` ordenables, `<select>` pageSize, sin badge), `Index.cshtml.cs` (`Sort`/`CorrelationId`/`PageSize` + `BuildSortRouteValues` + `BuildPagedRouteValues` variable), `Details.cshtml`/`.cshtml.cs` (nuevos), tests Web (`AuditoriasIndexTests` ampliados + `AuditoriasDetailsTests`).

### Dependencia B → A
Slice B depende del **contrato wire final** que deja Slice A: `AuditoriaListQuery` con `Sort`/`CorrelationId`, `AuditoriaDto` con `UserName` y sin `EntityId`, `AuditoriaDetalleDto`, `IAuditoriaApiClient.GetDetalleAsync` y `BuildQueryUri` propagando sort/correlationId.

### Repo compilable entre PRs
- Slice A incluye los **hotfixes de compat** que eliminan la única referencia runtime al campo quitado (`@item.EntityId`, `MakeAuditoriaDto`), por lo que `main` compila y la suite pasa tras el merge de A **antes** de que exista B.
- Slice B basa su rama en la rama de A (stacked). Tras el merge de A a main, B rebasea limpio; no hay ventana de incompatibilidad en `main` (el corte de tipo `AccountDto` ya transitó por A). Siliconado en PR ambos via `elflacoseba/SGV#248`.
- **Riesgo intermedio**: si `Index.cshtml` tuviera otras referencias residuales a `EntityId`/`badge` no detectadas, compilarían en A pero romperían visualmente; mitigado por review del hotfix y por la inspección de CodeGraph (blast radius cubrió los 24 callers de `AuditoriaDto`).

## Open Questions

- [ ] Confirmar con producto que la delta spec `auditoria-detalle` que indica `OccurredAt` como `DateTimeOffset` se interpreta como `DateTime` (consistencia con dominio/entity/tabla). Decisión tentativa D-5: `DateTime`.
- [ ] UX del fallback de `UserName` `"—"` vs. `UserId` crudo entre paréntesis (espec. développement recomienda `"—"`; la spec lo confirma).
- [ ] Validar `EXPLAIN` real del nuevo índice `(CorrelationId, OccurredAt)` con dataset representativo antes de cerrar migration.