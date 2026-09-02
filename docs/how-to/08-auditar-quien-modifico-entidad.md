# H-02-08 — Auditar quién modificó una entidad

Hay que reconstruir quién tocó una entidad (cargo, persona, vacante, etc.) en una ventana de tiempo o aislar los cambios de un request específico. El listado admin-only `/auditorias` consume `GET /api/v1/auditorias` y permite drilldown por `Id` hasta el detalle con `OldValuesJson`/`NewValuesJson`.

---

## Prerrequisitos

- Sesión iniciada como `Administrador` (la página `/auditorias` está protegida con `[Authorize(Roles = RolesSgv.Administrador)]`).
- Algún dato para buscar: nombre de la entidad, nombre de usuario, ventana temporal o `CorrelationId` capturado de un log de request.

---

## Paso 1 — Abrir el listado

Navegá a <http://localhost:5266/auditorias>. La página carga `GET /api/v1/auditorias` sin filtros (orden default `fecha_desc`, `pageSize=20`).

**Verificación:** la tabla muestra columnas `Fecha`, `Entidad`, `Operación`, `Usuario`, `Correlación`. La toolbar horizontal arriba tiene los filtros combinables.

---

## Paso 2 — Filtrar por nombre de entidad

El `<select>` de `Entidad` se hidrata desde `GET /api/v1/auditorias/filter-options` y trae los `EntityName` distintos persistidos (top 100 alfabético). Si el endpoint falla, la vista cae a `<input type="search">` con un banner info no bloqueante.

Elegí, por ejemplo, `Cargo` o `Persona` y enviá.

**Verificación:** la URL queda con `?entityName=Cargo&...`. La grilla muestra sólo filas con `EntityName == "Cargo"`. Las operaciones típicas que vas a ver: `Alta`, `Modificacion`, `BajaLogica`, `BloqueoUsuario`, `DesbloqueoUsuario`.

---

## Paso 3 — Acotar por fecha y/o usuario

Combiná los filtros: `dateFrom`, `dateTo`, `userName`. El listado pagina con `{10, 20, 50, 100}` filas y clamp server-side a `[1, 100]` (`AuditoriaServicioConsulta.MaxPageSize`).

**Verificación:** el contador `Total: N` arriba a la derecha refleja los filtros. Si `DateFrom > DateTo`, la API responde `400` con `ProblemDetails` (`El rango de fechas es inválido: ...`).

---

## Paso 4 — Aislar por `CorrelationId`

Si tenés el `CorrelationId` del request sospechoso (sale en cada log estructurado bajo `CorrelationId`), pegalo en el input `Correlación` y enviá. La query usa `correlationId` exacto.

**Verificación:** la grilla muestra exclusivamente las filas emitidas por ese request. Una mutación individual suele disparar una sola fila de auditoría (la del `SaveChangesAsync`), pero un comando complejo con varias entidades puede producir varias filas que comparten el `CorrelationId`.

---

## Paso 5 — Drilldown al detalle

Hacé clic en **Ver detalle** sobre la fila objetivo. La navegación va a `/auditorias/details/{id}` y carga `GET /api/v1/auditorias/{id}`.

**Verificación:** la página `Details` muestra los tres bloques JSON (`ChangedPropertiesJson`, `OldValuesJson`, `NewValuesJson`) en `<pre>` con `bg-light p-2`. La cabecera lleva `EntityName`, `EntityId`, `Operation`, `OccurredAt`, `UserId`/`UserName`, `CorrelationId`.

> Los bloques JSON filtran campos sensibles en el interceptor: `Password`, `Token`, `SecurityStamp`, `ConcurrencyStamp` nunca aparecen (ver `AuditoriaSaveChangesInterceptor.EsCampoSensible`).

---

## Paso 6 — Correlacionar con logs

Con el `CorrelationId` a mano, filtrá los logs de la API en la ventana de tiempo:

```bash
grep "<correlation-id>" /var/log/sgv/api.log
```

**Verificación:** la línea de log del request problemático aparece con el mismo GUID en el campo `CorrelationId`. Esto cierra el loop entre la vista de auditoría y la traza de runtime (request, exception si la hubo, response code).

---

## Troubleshooting

- **El listado queda vacío tras filtrar**: los filtros se contradicen. Empezá sin filtros y agregálos uno por uno. El link "Limpiar" de la toolbar resetea todo a `fecha_desc` con `pageSize=20`.
- **El `<select>` de Entidad aparece como `<input type="search">` con banner amarillo**: `GET /api/v1/auditorias/filter-options` falló (timeout, 5xx, red). La grilla sigue funcionando con el input manual.
- **El detalle devuelve 404**: el `Id` no existe en `Auditorias`. Verificá que venís de una fila real (los `Id` son `Guid`; un copy-paste malformado rompe el binder de ruta).
- **Falta la fila esperada**: el cambio se hizo desde un endpoint que escribe auditoría explícita (no por interceptor). El listado los incluye todos vía la columna `Operacion`, pero filtrá por nombre de la operación custom si la conocés.

---

## Referencias

- `src/SGV.Api/Controllers/AuditoriasController.cs` — `GET`, `GET /{id:guid}`, `GET /filter-options`.
- `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` — LINQ + paginación + filtros sensibles.
- `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` — captura `Added`/`Modified`/`Deleted` y filtra sensibles.
- `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` — filtros y orden server-side.
- `../tutorials/02-primera-mutacion-unidad-organizativa.md` — paso 5 muestra cómo verificar una fila propia.
- [R-03-03](../reference/03-wire-types-contracts.md) — Referencia del
  wire `AuditoriaListQuery` y `AuditoriaDetalleDto` y demás records del
  módulo Auditoria.
