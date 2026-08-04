# Exploration: 2026-07-31-ajustes-listado-auditoria

## Current State

El módulo de auditoría está implementado y funcional:

- **API** (`AuditoriasController`): `GET /api/v1/auditorias` + `GET /api/v1/auditorias/{id:guid}`, ambos `[Authorize(Roles = Administrador)]`. El orden es fijo `OccurredAt DESC, Id DESC`. La paginación usa `page` y `pageSize` en querystring.
- **Contracts**: `AuditoriaListQuery` tiene filtros para `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId`. `AuditoriaDto` expone 8 campos incluyendo `EntityId` y `ChangedPropertiesJson`; **NO expone** `OldValuesJson` ni `NewValuesJson` (D-2 vigente).
- **Aplicación/Infraestructura**: `IAuditoriaServicioConsulta` / `AuditoriaServicioConsulta` con `AsNoTracking`, proyección segura campo-a-campo.
- **Web (Index)**: filtros en **sidebar izquierda**, paginación Anterior/Siguiente/Primera/Última (sin números de página), `PageSize` fijo en 20 (hardcodeado en `BuildPagedRouteValues`).
- **Web (API Client)**: `IAuditoriaApiClient` / `AuditoriaApiClient` con `BuildQueryUri` que serializa todos los filtros existentes.

D-2 está cerrado por construcción: el `Select` EF de `AuditoriaServicioConsulta` enumera campo-a-campo y jamás incluye `OldValuesJson`/`NewValuesJson`. La entity `AuditoriaEntity` y la tabla `Auditorias` tienen todos los campos incluyendo `OldValuesJson`/`NewValuesJson`.

**Lo que la issue pide que NO existe todavía:**

| Requisito | Estado actual | Gap |
|---|---|---|
| Filtros horizontales (no sidebar) | Filtros en sidebar | Requiere revisión UI completa de Index.cshtml |
| Ordenamiento server-side por encabezados | Orden fijo `OccurredAt DESC, Id DESC` | Requiere `sort` en `AuditoriaListQuery` + lógica EF |
| Quitar columna `EntityId` del listado | `EntityId` presente en `AuditoriaDto` y tabla | Requiere decisión de exponerlo en detalle |
| Filtro exacto por `CorrelationId` | No existe en `AuditoriaListQuery` | Requiere agregar `CorrelationId` al query |
| Selector 10/20/50/100 + números de página | `PageSize` fijo = 20; sin page numbers | Requiere UI de paginación nueva |
| Página de detalle con old/new values | `GetById` devuelve `AuditoriaDto` sin old/new | Requiere DTO separado para detalle + página nueva |
| LEFT JOIN Identity para `UserName` | `UserId` crudo en `AuditoriaDto` | Requiere join con `AspNetUsers` y nuevo campo en DTO |
| Quitar badge de Operation | `<span class="badge badge-soft-info">` presente | Cambio cosmético en la vista |

## Affected Areas

| Archivo | Cambio requerido |
|---|---|
| `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | Agregar `Sort?` (string estilo `fecha_desc`), `CorrelationId?` (Guid), expandir PageSize a selector 10/20/50/100 |
| `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` | Quitar `EntityId` del DTO de listado (mantenerlo para la página de detalle); agregar `UserName?` desde LEFT JOIN |
| `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | Nuevo método `GetDetalleAsync` que devuelva `AuditoriaDetalleDto` (o overload con parámetro de proyección) |
| `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | Implementar sort dinámico (switch sobre expresión sort), LEFT JOIN con `AspNetUsers` para `UserName`, filtro exacto por `CorrelationId`, nuevo método `GetDetalleAsync` con proyección que incluye `OldValuesJson`/`NewValuesJson` |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/AuditoriaConfiguracion.cs` | Verificar/crear índice compuesto para `CorrelationId` + `OccurredAt` si no existe |
| `src/SGV.Api/Controllers/AuditoriasController.cs` | Propagar `sort` y `correlationId` al servicio; endpoint detalle se mantiene igual pero devuelve DTO enriquecido |
| `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | Agregar `GetDetalleAsync`; expandir `BuildQueryUri` para sort y correlationId |
| `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | Implementar los nuevos métodos |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml` | Migrar filtros de sidebar a panel horizontal sobre la tabla; agregar `<th>` ordenables; agregar `<select>` de pageSize; quitar columna `EntityId`; quitar badge de Operation |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | Agregar `Sort`, `CorrelationId`; cambiar `BuildPagedRouteValues` para incluir sort; nuevo `BuildSortRouteValues`; actualizar `OnGetAsync`; soportar `pageSize` variable |
| `src/SGV.Web/Pages/Auditorias/Details.cshtml` (nuevo) | Página de detalle para Administrador con todos los campos + OldValuesJson/NewValuesJson renderizados como JSON preformateado |
| `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` (nuevo) | PageModel de detalle, consume `GetDetalleAsync` |
| `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | Nuevos tests: sort, correlationId filter, LEFT JOIN UserName, `GetDetalleAsync` incluye old/new |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` | Actualizar assertions para nuevos query params, cubrir selector pageSize |
| `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | Cubrir nuevos params de query |
| `docs/decisiones-implementacion.md` | Documentar D-5 bis (UserName enrichment), D-6 (ordenamiento server-side), D-7 (detalle admin con old/new) |

## Approaches

### Approach A: Extensión incremental con DTO separado para detalle

Mantener `AuditoriaDto` como wire de listado (sin `EntityId`, con `UserName?`Nullable). Crear internamente en `SGV.Contracts.Auditoria` (o en `SGV.Aplicacion`) un `AuditoriaDetalleDto` que incluya `OldValuesJson` y `NewValuesJson`. El endpoint de detalle de la API y la página Details consumen este DTO enriquecido. La separación física de tipos cierra D-2 por construcción sin dependencias de autorización en tiempo de compilación.

**Pros:**
- D-2 cerrado por tipo, no por convención ni código procedural
- Backward compatible: el listado existente no cambia su forma
- Fácil de testear: test de tipo (reflexión) para `AuditoriaDto` sin old/new fields
- La página Details tiene su propio DTO y su propia autorización

**Cons:**
- Dos tipos de DTO para la misma entidad (pero es intencional y explícito)
- Requiere agregar método en la interfaz del servicio y en la implementación

**Complexity:** Medium

### Approach B: Single DTO con flag de proyección

Un único `AuditoriaDto` que siempre incluya `UserName` y optionally `OldValuesJson`/`NewValuesJson`. La diferencia entre listado y detalle se controla en el servicio (proyección condicional) y se filma con tests.

**Pros:**
- Un solo tipo en Contracts

**Cons:**
- Mayor riesgo de fuga accidental de old/new values si alguien agrega un endpoint nuevo y olvida la proyección
- Rompe D-2 que fue diseñado específicamente para evitar exposición de old/new values en todo el wire

**Complexity:** Low (apariencia) pero alto riesgo de regressión en seguridad de datos

## Recommendation

**Approach A** (DTO separado para detalle). La separación física de tipos es la defensa más robusta contra fugas de PII. El esfuerzo adicional de mantener dos DTOs es marginal comparado con el riesgo de un future bug que exponga `OldValuesJson` a través del wire. El patrón ya existe en el codebase: otros módulos tienen DTOs de listado vs. DTOs de detalle.

## Risks

1. **LEFT JOIN para UserName**: la entity `AuditoriaEntity.UserId` es `string?` y la tabla `AspNetUsers` tiene `Id` como string. El join es directo. Si el usuario fue eliminado de Identity, el LEFT JOIN devuelve null y `UserName` queda null → decidir cómo mostrar en la UI: "¿Usuario eliminado?", "—", o el UserId entre paréntesis.
2. **Índice MySQL para CorrelationId + OccurredAt**: la query actual tiene `ORDER BY OccurredAt DESC, Id DESC`. Si se agrega ordenamiento por columna y filtro por `CorrelationId`, un índice covering `(CorrelationId, OccurredAt DESC)` o `(OccurredAt DESC, CorrelationId)` puede ser necesario. Requiere verificar el plan `EXPLAIN` con datos reales.
3. **PageSize variable**: `BuildPagedRouteValues` actualmente hardcodea `pageSize = DefaultPageSize` (20). Cambiar a variable requiere re-renderizar el selector y que los enlaces de paginación propaguen el valor elegido. También se necesita en `BuildSortRouteValues`.
4. **EntityId en el detalle**: si se quita de `AuditoriaDto` de listado, la página de detalle lo necesita para saber qué recurso se auditó. Mantener en el `AuditoriaDetalleDto`.
5. **Cambio de contract API**: quitar `EntityId` de `AuditoriaDto` es un breaking change menor si hay consumidores externos (no se esperan en un admin-only endpoint, pero es una decisión de versioning).

## Decisiones de producto requeridas antes del proposal

1. **Fallback visual para UserName faltante**: cuando el LEFT JOIN no encuentra el usuario, ¿mostrar "—" (en blanco), el `UserId` crudo, o "Usuario desconocido"? Recomendación: mostrar "—" si null.
2. **UX de detalle JSON (OldValuesJson/NewValuesJson)**: opciones: (a) `<pre class="bg-light p-2">` preformateado monospaciado; (b) JSON pretty-printed con `<pre>`; (c) Tabla key-value lado a lado. Recomendación: opción (a) como minimum viable, mejorar después.
3. **Ruta de la página de detalle**: ¿`/auditorias/{id:guid}` (directa) o `/auditorias/details?id={guid}`? Recomendación: `/auditorias/details?id={guid}` para mantener consistencia con el routing del resto del shell.
4. **Columnas ordenables y orden por defecto**: propuesta: solo `Fecha` ordenable (`fecha_asc`/`fecha_desc`). Los demás campos (Entidad, Operación, Usuario) son de alta cardinalidad y el orden por ellos no tiene sentido práctico en una auditoría. ¿Se incluye Correlación como ordenable?
5. **Conservación de estado al ordenar**: ¿cambiar sort resetea page a 1 o preserva la página actual? Recomendación: reset a page 1, igual que hace Cargo al cambiar `status`.
6. **Exposición de OldValuesJson/NewValuesJson en el endpoint detalle**: ¿usar DTO separado (`AuditoriaDetalleDto`) o switch de proyección? Recomendación: DTO separado (Approach A).
7. **EntityId en el listado**: ¿quitar del `AuditoriaDto` (requiere cambio de contract API) o solo ocultarlo visualmente con `d-none`? Recomendación: quitar del DTO y del contract.
8. **Badge de Operation**: ¿solo quitar el `<span class="badge">` pero mantener el texto, o cambiar a texto plano sin badge?
