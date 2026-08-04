# Design: Issue #253 — Auditoría drill-down pierde userName

## Enfoque técnico

Cambio quirúrgico en la capa Web (`SGV.Web`) para alinear el binding del detalle de auditoría con el query string que ya genera el listado. `IndexModel.BuildDetailsRouteValues` emite `userName = UserName` (línea 307 de `Index.cshtml.cs`), pero `DetailsModel.OnGetAsync` bindea `[FromQuery(Name = "userId")]` y expone `UserId`, por lo que el filtro se pierde en el drill-down y en el back-link. Se renombra la propiedad y el binding a `UserName`/`userName`, se actualiza `BuildBackUrl()` y se agrega un test de regresión de round-trip. Mapea 1:1 con los requisitos de la spec `auditoria-drilldown-username-filter`: binding correcto, back-link preserva `userName`, navegación directa sin filtro sigue funcionando, y test de round-trip cubre ambos extremos.

## Decisiones de arquitectura

### Decisión: Renombrar propiedad + binding (no aliasar)

**Elección**: `UserId` → `UserName` (propiedad), `[FromQuery(Name = "userId")]` → `[FromQuery(Name = "userName")]`, `userId = UserId` → `userName = UserName` en `BuildBackUrl()`.
**Alternativas consideradas**: Mantener `UserId` y agregar un segundo binding `userName` (alias); renombrar solo el `[FromQuery]` sin tocar la propiedad.
**Rationale**: Ningún consumidor interno referencia `DetailsModel.UserId` (verificado vía grep del módulo `Auditorias`; `Details.cshtml` usa `detalle.UserId`/`detalle.UserName` del DTO, no de la PageModel). El alias duplicaría estado y rompería la simetría con `IndexModel.UserName`. El renombrado limpio refleja la realidad semántica — el shell de auditoría filtra por nombre legible, no por GUID técnico — y se alinea con el contrato `AuditoriaListQuery.UserName` que ya consume `IndexModel`.

### Decisión: Back-link usa `userName` como route-value key

**Elección**: `BuildBackUrl()` emite `userName = UserName`, coincidiendo con el binding `[FromQuery] string? userName` de `IndexModel.OnGetAsync`.
**Alternativas consideradas**: Enviar `userId` y dejar que `IndexModel` lo re-mapee.
**Rationale**: `IndexModel` no acepta `userId`; su único binding de usuario es `userName`. Cualquier otra clave se ignora silenciosamente y el filtro se pierde. La simetría de clave es lo que cierra el round-trip.

| Opción | Tradeoff | Decisión |
|--------|----------|----------|
| Renombrar a `UserName`/`userName` | Toca 4 puntos en 1 archivo | ✅ Adoptada |
| Alias dual `userId`+`userName` | Duplica estado, inconsistencia | ❌ Rechazada |
| Solo renombrar binding | Propiedad `UserId` miente sobre el dato | ❌ Rechazada |

## Flujo de datos

```
Index (filtro userName=jperez)
   │  BuildDetailsRouteValues(id)  ──→  ?id=…&userName=jperez
   ▼
Details.OnGetAsync  [FromQuery(Name="userName")]  ──→  UserName = "jperez"
   │  BuildBackUrl()  ──→  /auditorias?…&userName=jperez
   ▼
Index.OnGetAsync  [FromQuery] string? userName  ──→  filtro preservado
```

Navegación directa sin `userName`: el parámetro opcional queda `null`, `BuildBackUrl()` no transporta la clave (o la envía vacía, que `IndexModel.Normalize` colapsa a `null`). Sin errores.

## Cambios de archivo

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` | Modify | Renombrar `UserId`→`UserName` (línea 107-108), `[FromQuery(Name="userId")]`→`[FromQuery(Name="userName")]` (línea 151), `userId=UserId`→`userName=UserName` (línea 128), y `UserId = Normalize(userId)`→`UserName = Normalize(userName)` (línea 162). Actualizar doc-comment línea 107. |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | Modify | Agregar test de round-trip: dado `userName=jperez` en query string, `OnGetAsync` lo bindea y `BuildBackUrl()` lo preserva; y dado ausencia de `userName`, el back-link no introduce valor espurio. |

`Index.cshtml.cs` **no se toca** (ya emite `userName` correctamente). `Details.cshtml` no se toca (usa propiedades del DTO `detalle`, no de la PageModel). Nada en API, contratos ni persistencia.

## Interfaces / contratos

Sin cambios en interfaces públicas ni wire-types. El renombrado es interno a la PageModel. `IAuditoriaApiClient`, `AuditoriaDetalleDto` y `AuditoriaListQuery` NO se alteran.

## Estrategia de testing

| Capa | Qué se testea | Cómo |
|------|---------------|------|
| Integración Web | Round-trip `userName`: query string → binding → back-link | `[Fact]` en `AuditoriasDetailsTests` contra `SgvWebApplicationFactory` + `FakeAuditoriaApiClient`; GET `/auditorias/details?id=…&userName=jperez`, decodificar HTML, afirmar que el `href` del back-link contiene `userName=jperez`. |
| Integración Web | Navegación directa sin `userName` no rompe y back-link sin valor espurio | `[Fact]` análogo sin `userName` en query string; afirmar ausencia de `userName=` con valor no vacío en el back-link. |

Se usan los helpers existentes `CreateAuditoriaLeaseAsync` y `MakeAuditoriaDetalleDto`. No se agregan fixtures nuevas. Nada de API/persistencia (sin `MySqlFact`).

## Matriz de amenazas

N/A — no se toca routing, shell, subprocesses, VCS/PR automation, clasificación de ejecutables ni integración de procesos. Es un renombrado de binding dentro de una Razor Page existente.

## Migración / rollout

No se requiere migración. Sin feature flags. El cambio es retrocompatible: las URLs antiguas con `userId=…` ya no bindeaban nada útil (el dato se perdía), y las URLs nuevas con `userName` son las que `IndexModel` ya genera.

## Preguntas abiertas

- [ ] ¿Convendrá, en un cambio futuro, alinear el doc-comment de `Index.cshtml.cs` línea 20 (lista `<c>DateTo</c>, <c>UserId</c>, <c>CorrelationId</c>`) que aún menciona `UserId`? Fuera de scope de este fix; se documenta como drift menor.