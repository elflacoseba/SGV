# Proposal: 2026-07-31-ajustes-listado-auditoria

## Intent

Mejorar la usabilidad del listado de auditoría para administradores: ordenar por cualquier columna, filtrar por `CorrelationId`, mostrar `UserName` en lugar de `UserId` crudo, quitar `EntityId` del listado público, exponer un selector de `PageSize` (10/20/50/100), y crear una página de detalle que muestre `OldValuesJson`/`NewValuesJson` en formato preformateado — todo sin romper D-2 (los valores old/new nunca aparecen en el listado ni en el DTO de detalle wire público).

## Scope

### In Scope
- Agregar `Sort?` (`fecha_asc|desc`, `entidad_asc|desc`, `operacion_asc|desc`, `usuario_asc|desc`, `correlacion_asc|desc`) y `CorrelationId?` a `AuditoriaListQuery`.
- Crear `AuditoriaDetalleDto` con `EntityId`, `OldValuesJson`, `NewValuesJson` y `ChangedPropertiesJson`; `OldValuesJson`/`NewValuesJson` **NO** se exponen en el `AuditoriaDto` de listado (D-2 vigente).
- `AuditoriaDto` del listado: quitar `EntityId` del wire, agregar `UserName?` (LEFT JOIN con `AspNetUsers`; fallback "—" si es null).
- Orden server-side por las 5 columnas; default `fecha_desc`. Cambio de sort resetea a página 1.
- Selector de `PageSize` con opciones 10/20/50/100 en la UI web.
- Filtros horizontales sobre la tabla (reemplazan la sidebar actual).
- Headers `<th>` ordenables con indicadores de dirección.
- Quitar badge `<span class="badge">` de la columna `Operation`, dejando texto plano.
- Nueva página `GET /auditorias/details?id={guid}` con `<pre>` preformateado para `OldValuesJson` y `NewValuesJson`.
- Endpoint `GET /api/v1/auditorias/{id:guid}` retorna `AuditoriaDetalleDto` (DTO enriquecido, protegido por `[Authorize(Roles = Administrador)]`).
- Actualizar `IAuditoriaApiClient` y `AuditoriaApiClient`: nuevo método `GetDetalleAsync` + expandir `BuildQueryUri` para sort y `correlationId`.

### Out of Scope
- Migración de la tabla `Auditorias` (no se agregan columnas; el LEFT JOIN es sobre `AspNetUsers` existente).
- Exportación, purga o retención de datos de auditoría.
- Auditoría de la auditoría (ya está cubierto por D-4: `AsNoTracking` + no `SaveChanges`).

## Capabilities

### New Capabilities
- `auditoria-sort`: Listado de auditoría ordenable server-side por `fecha`, `entidad`, `operacion`, `usuario` y `correlacion`. Default `fecha_desc`. Cambio de sort resetea a página 1.
- `auditoria-detalle`: Página de detalle `GET /auditorias/details?id={guid}` y endpoint `GET /api/v1/auditorias/{id}` enriquecido con `AuditoriaDetalleDto` que expone `OldValuesJson`/`NewValuesJson`. Acceso `[Authorize(Roles = Administrador)]`.
- `auditoria-page-size`: Selector de `PageSize` (10/20/50/100) propagado vía querystring, preservado en enlaces de paginación y orden.

### Modified Capabilities
- `auditoria-query`: Se extiende `AuditoriaListQuery` con `Sort?` y `CorrelationId?`. `AuditoriaDto` pierde `EntityId` y gana `UserName?` (LEFT JOIN). `GetById` retorna `AuditoriaDetalleDto`. `PageSize` expandido a selector.

## Approach

**Approach A — DTO separado para detalle.** Se mantiene `AuditoriaDto` para el listado (sin `EntityId`, con `UserName?`). Se crea `AuditoriaDetalleDto` en `SGV.Contracts.Auditoria` con `EntityId`, `OldValuesJson`, `NewValuesJson` y `ChangedPropertiesJson`. El endpoint de detalle y la página `Details` consumen este DTO enriquecido. La separación física de tipos cierra D-2 por construcción: el listado nunca puede exponer old/new values aunque alguien agregue un endpoint nuevo.

**Infraestructura:**
- `AuditoriaListQuery`: agregar `Sort?` y `CorrelationId?`; `PageSize` sigue siendo `int` con clamping 1-100, la UI agrega el selector.
- `AuditoriaServicioConsulta.QueryAsync`: sort dinámico con expresión switch sobre el valor de `Sort`; LEFT JOIN con `AspNetUsers` para `UserName` (coalesce a "—" en la proyección).
- `AuditoriaServicioConsulta.GetByIdAsync`: nuevo método que proyecta `AuditoriaDetalleDto` incluyendo `EntityId`, `OldValuesJson`, `NewValuesJson`.
- `AuditoriaConfiguracion`: verificar índice covering `(CorrelationId, OccurredAt DESC)` para el nuevo filtro + sort.
- `AuditoriasController.GetById`: cambiar tipo de retorno de `AuditoriaDto` a `AuditoriaDetalleDto`.

**Web:**
- `Index.cshtml`: migrar filtros de sidebar a panel horizontal; `<th>` ordenables; `<select>` pageSize; quitar columna `EntityId` y badge de `Operation`.
- `Index.cshtml.cs`: agregar `Sort`, `CorrelationId` y `PageSize` como propiedades bindeadas; `BuildSortRouteValues`; actualizar `BuildPagedRouteValues` para incluir sort.
- `Details.cshtml` + `Details.cshtml.cs`: nueva página, autorización `[Authorize(Roles = Administrador)]`, consume `GetDetalleAsync`, renderiza JSON en `<pre>`.

## Affected Areas

| Archivo | Impacto | Descripción |
|---------|---------|-------------|
| `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | Modificado | Agregar `Sort?`, `CorrelationId?` |
| `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` | Modificado | Quitar `EntityId`; agregar `UserName?` |
| `src/SGV.Contracts/Auditoria/AuditoriaDetalleDto.cs` | Nuevo | DTO enriquecido con old/new values |
| `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | Modificado | Nuevo `GetDetalleDtoAsync` |
| `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | Modificado | Sort dinámico, LEFT JOIN, `GetDetalleDtoAsync` |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/AuditoriaConfiguracion.cs` | Modificado | Verificar/crear índice para `CorrelationId` |
| `src/SGV.Api/Controllers/AuditoriasController.cs` | Modificado | Propagar sort/correlationId; `GetById` retorna `AuditoriaDetalleDto` |
| `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | Modificado | `GetDetalleAsync`; expandir `BuildQueryUri` |
| `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | Modificado | Implementar nuevos métodos |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml` | Modificado | Filtros horizontales, sortable headers, pageSize selector, quitar EntityId y badge |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | Modificado | Soportar sort, correlationId, pageSize variable |
| `src/SGV.Web/Pages/Auditorias/Details.cshtml` | Nuevo | Página de detalle con `<pre>` para JSON |
| `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` | Nuevo | PageModel de detalle |
| `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | Modificado | Tests para sort, correlationId, LEFT JOIN, detalle con old/new |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` | Modificado | Tests para nuevos query params y pageSize selector |
| `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | Modificado | Tests para sort y `GetById` con `AuditoriaDetalleDto` |
| `docs/decisiones-implementacion.md` | Modificado | Documentar D-5 bis (enriquecimiento UserName), D-6 (ordenamiento server-side), D-7 (detalle admin con old/new) |

## Risks

| Riesgo | Probabilidad | Mitigación |
|--------|-------------|------------|
| BREAKING CHANGE: `AuditoriaDto` pierde `EntityId`. Si hay consumidores externos del endpoint detalle (no se esperan en admin-only), será un cambio visible. | Baja | Endpoint admin-only; comunicar en code review. |
| LEFT JOIN con `AspNetUsers` agrega latencia si la tabla Identity tiene muchas filas. | Baja | `UserName` se proyecta en el SELECT; índice existente en `Id` de `AspNetUsers`. Verificar con `EXPLAIN`. |
| Índice MySQL para `CorrelationId + OccurredAt` no existe. | Media | Crear migración con índice covering si `EXPLAIN` muestra `Using filesort`. |
| `BuildPagedRouteValues` hardcodea `DefaultPageSize` (20). Si se cambia a variable, los enlaces de paginación previos a la implementación pueden mantener pageSize=20 aunque el selector tenga otro valor. | Baja | Los enlaces se regeneran con cada request; el usuario感受 el cambio al hacer click. |
| D-2 se rompe si alguien agrega un endpoint nuevo sin usar `AuditoriaDetalleDto`. | Baja | La separación física de tipos hace que sea imposible por accidente; test de reflexión en `AuditoriaDto` para detectar `OldValuesJson`/`NewValuesJson`. |

## Rollback Plan

1. **Revertir API Contract**: restaurar `AuditoriaDto` original con `EntityId` y sin `UserName`; hacer que `GetById` retorne `AuditoriaDto`. Esto no requiere migración de DB.
2. **Revertir Web**: restaurar `Index.cshtml` con sidebar de filtros, `BuildPagedRouteValues` original sin sort, quitar `Details.cshtml`.
3. **Revertir Infra**: eliminar el método `GetDetalleDtoAsync`, restaurar el `OrderByDescending` fijo.
4. **DB**: ninguna migración requerida — no se alteró esquema de `Auditorias`.

## Dependencies

- Ninguna dependencia externa nueva. MySQL 8 ya está en uso.
- Requiere verificar (no crear) índice para `CorrelationId` en `AuditoriasConfiguracion`.

## Success Criteria

- [ ] `AuditoriaDto` de listado **NO** contiene `EntityId`, `OldValuesJson`, ni `NewValuesJson`.
- [ ] `AuditoriaDetalleDto` contiene `EntityId`, `OldValuesJson`, `NewValuesJson`, `ChangedPropertiesJson` y `UserName`.
- [ ] El endpoint `GET /api/v1/auditorias/{id}` retorna `AuditoriaDetalleDto` para administradores autenticados.
- [ ] `GET /auditorias/details?id={guid}` renderiza `OldValuesJson`/`NewValuesJson` en `<pre>` monospaciado.
- [ ] Los 5 encabezados de columna son ordenables con indicador visual de dirección activa.
- [ ] Cambiar sort resetea la página a 1.
- [ ] El selector de `PageSize` permite 10/20/50/100 y los enlaces de paginación preservan el valor elegido.
- [ ] `UserName` muestra "—" cuando el LEFT JOIN no encuentra el usuario.
- [ ] Tests nuevos/pacthados para sort, correlationId, LEFT JOIN UserName, y detalle con old/new values pasan.
- [ ] `dotnet build SGV.slnx` compila sin errores.
- [ ] `dotnet test SGV.slnx` pasa (tests existentes no se rompen).
