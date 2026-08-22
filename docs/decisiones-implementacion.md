# Decisiones de Implementación

## Módulo transversal de Auditoría — capa de lectura (issue `implementa-modulo-auditorias`)

> Change: `implementa-modulo-auditorias`. Artefactos SDD completos en
> `openspec/changes/implementa-modulo-auditorias/`. Chain strategy:
> `stacked-to-main` con 3 PRs encadenados cuyo target operativo es
> `develop`. S1 (servicio de consulta) y S2 (controller API admin-only)
> ya mergeados; este documento resume el módulo completo (S1 + S2 + S3)
> y consolida D-1..D-5 del `design.md` para referencia de futuros PRs.

### Contexto y problema

La tabla `Auditorias` ya se persiste desde `S1` mediante
`AuditoriaSaveChangesInterceptor` + `IAuditoriaServicio.RegistrarAsync`,
pero el sistema carecía por completo de capacidad de **consulta**.
Sin una vista de auditoría transversal, los administradores no
podían rastrear quién creó/modificó/eliminó entidades del sistema.
Este change agrega la **capa de lectura pura** sin tocar la
escritura existente (interceptor, servicio de escritura, entidad,
tabla, seeders, gateway de Identity) — el módulo es puramente
aditivo y de solo lectura.

### D-1 — Implementación del servicio de consulta en Infraestructura, no en Aplicación

`SGV.Aplicacion` declara el puerto `IAuditoriaServicioConsulta`
(`QueryAsync` + `GetByIdAsync`); la impl EF directa vive en
`SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs`
como `sealed class (SgvDbContext context) : IAuditoriaServicioConsulta`.
Replica el par `IAuditoriaServicio` (escritura) sin repositorio
intermedio. `Aplicacion` queda libre de EF/`SgvDbContext`, preservando
la separación de capas del grafo
`Dominio ← Aplicacion ← Infraestructura` (proposal original ubicaba
la impl en Aplicación; se corrige acá para preservar Clean
Architecture). Registrada en DI con
`services.AddScoped<IAuditoriaServicioConsulta, AuditoriaServicioConsulta>()`.

### D-2 — Proyección wire-safe (sin old/new values)

`AuditoriaDto` (`SGV.Contracts/Auditoria/AuditoriaDto.cs`) expone 8
campos: `Id, EntityName, EntityId, Operation, OccurredAt, UserId,
ChangedPropertiesJson, CorrelationId`. Por construcción NO incluye
`OldValuesJson` ni `NewValuesJson` — el riesgo de fuga de PII a
través del wire es HIGH en el proposal original; se cierra con un
`Select` explícito campo-a-campo en el `IQueryable` de EF:

```csharp
.Select(a => new AuditoriaDto(
    a.Id, a.EntityName, a.EntityId, a.Operation, a.OccurredAt,
    a.UserId, a.ChangedPropertiesJson, a.CorrelationId))
```

El compilador garantiza que `OldValuesJson`/`NewValuesJson` jamás se
copian. No hay AutoMapper/`ProjectTo` que pudiera arrastrarlos. La
proyección se hace **antes** de materializar la lista, así que EF
emite sólo las columnas del wire contract en el `SELECT` SQL. El
test `[Fact]` puro `AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson`
verifica por reflexión que los campos prohibidos no existen en el
record. Los tests `[MySqlFact]` `QueryAsync_Proyeccion_NoContieneOldNewValuesEnSerializacion`
y `GetByIdAsync_Proyeccion_NoContieneOldNewValuesEnSerializacion`
verifican a través de `JsonSerializer.Serialize` (PascalCase) y
JSON HTTP (camelCase) que el body del listado y del detalle tampoco
contiene las variantes `oldValuesJson` / `newValuesJson` —
defense-in-depth contra un futuro `AddJsonOptions(...).UseCamelCase()`.

### D-3 — Orden determinista, paginación, validación de rangos

| Aspecto | Decisión |
|---|---|
| Orden por defecto | `ORDER BY OccurredAt DESC, Id DESC` (Id como tiebreaker determinista — el índice PK cubre) |
| Paginación | `Page >= 1`, `PageSize` clampeado a `[1, 100]` en el servicio |
| Rango fechas | `DateFrom <= DateTo`; si `DateFrom > DateTo` el servicio lanza `ArgumentException` con mensaje explícito de rango invertido, NO devuelve conjunto vacío. El controller (S2) lo mapea a `400 Validation` con `ProblemDetails` (`ApiResults.ToValidationProblemResult` con sobrecarga string-based agregada en S2). |
| Filtros | `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId` (todos opcionales) |
| Default query | `Page=1, PageSize=20` |

### D-4 — No-auditoría de consultas

Las consultas no invocan `SaveChanges`/`SaveChangesAsync`; el
`AuditoriaSaveChangesInterceptor.SavingChanges` no se dispara en
lecturas `AsNoTracking()`. Verificado por el test
`QueryAsync_NoInsertaAuditoriasNuevas` (cuenta filas antes/después
y exige igualdad). No se requiere lógica especial: el diseño
garantiza por construcción que leer `Auditorias` no genera
registros nuevos. **No hay recursión de auditoría**.

### D-5 — `UserId` crudo en v1; enriquecimiento con nombre fuera de alcance

`AuditoriaDto.UserId` se expone tal cual vive en la entidad (string
sin JOIN contra `AspNetUsers`). El enriquecimiento con nombre
legible queda explícitamente fuera de alcance del v1 y se reserva
para una evolución posterior (v2+), donde se evaluará JOIN, caché
o proyección desnormalizada. Esta decisión cierra la pregunta
previa del proposal y mantiene el alcance de lectura sin tocar
la escritura ni el esquema de Identity.

> **D-5 bis (issue #248, Slice A):** se levanta el «fuera de
> alcance» del v1 para `UserName`, manteniendo `UserId` crudo
> en el wire como clave de correlación técnica. La proyección
> usa un **LEFT JOIN contra `AspNetUsers`** resuelto con
> `DefaultIfEmpty()`; cuando el `UserId` no tiene fila en
> Identity (purga, soft-delete, huérfano, sistema), el servicio
> coalesce explícitamente a la cadena `"—"` (rayo em,
> U+2014 — consistente con el resto del wire contract que usa
> el mismo carácter para valores faltantes). Esto cierra el
> path de UX que preguntaba por el nombre legible del actor
> sin sacrificar la forma «técnica» del `UserId`. El LEFT JOIN
> reusa `AspNetUsers` y no agrega migraciones de esquema
> (D-3 cerrado por construcción). Verificado por los tests
> `[MySqlFact]` `QueryAsync_UserIdExistente_ResuelveUserNameDeIdentity`,
> `QueryAsync_UserIdInexistente_CaeAFallbackRayemEm` y
> `QueryAsync_SortUsuarioAsc_OrdenaPorUserName` (este último
> cubre que la columna "Usuario" ordena por nombre legible,
> no por `UserId` crudo, para que el operador vea el orden
> que espera).

### D-6 — Orden server-side dinámico vía `switch(Sort)` (issue #248, Slice A)

La spec `auditoria-sort` introduce cinco criterios de orden
(`fecha|entidad|operacion|usuario|correlacion` × `asc|desc`). El
sort se resuelve **server-side** con un `switch` expresión sobre
`Sort` (no con `OrderBy` por `string` arbitrario) para mantener
explícito el universo de claves válidas y dejar al motor LINQ
construir el `IOrderedQueryable` apropiado. El default es
`fecha_desc` (equivalente al orden vigente del v1),
`ThenByDescending(Id)` se aplica como **tiebreak determinista**
universal y un valor no reconocido **cae al default sin error**
para no romper la consulta por input malformado. La columna
«usuario» ordena por `UserName` (LEFT JOIN) y no por `UserId`,
cerrando por construcción la UX consistente con D-5 bis.

| `Sort`         | Columna       | Mapeo LINQ                              |
|----------------|---------------|------------------------------------------|
| `fecha_asc`    | `OccurredAt`  | `OrderBy(x => x.a.OccurredAt)`          |
| `fecha_desc`   | `OccurredAt`  | `OrderByDescending(x => x.a.OccurredAt)`|
| `entidad_asc`  | `EntityName`  | `OrderBy(x => x.a.EntityName)`          |
| `entidad_desc` | `EntityName`  | `OrderByDescending(x => x.a.EntityName)`|
| `operacion_asc`/`desc`  | `Operation`  | simétrico                                |
| `usuario_asc`/`desc`    | `UserName`   | `OrderBy(x => x.u != null ? x.u.UserName : UserNameFallback)` |
| `correlacion_asc`/`desc`| `CorrelationId` | simétrico                              |
| _otro_         | default       | `OrderByDescending(x => x.a.OccurredAt)`|

La migración EF `IndiceAuditoriaCorrelationIdOccurredAt` agrega
el índice compuesto `(CorrelationId, OccurredAt DESC)` para que
`sort=correlacion_desc` (combinable con `?correlationId=...`)
no fuerce `Using filesort` en MySQL. Verificado por tests
`[MySqlFact]` `QueryAsync_DefaultSortEs_FechaDesc`,
`QueryAsync_Sort*` (10 variantes) y por
`AuditoriasControllerTests.Get_PropagaSortAServicio`.
La shell web espeja la normalización (`Index.cshtml.cs` →
`NormalizeSort`) para reflejar el criterio vigente en los
iconos de los headers.

### D-7 — Detalle admin con `AuditoriaDetalleDto` (issue #248, Slice A + Slice B)

El endpoint `GET /api/v1/auditorias/{id}` y la page
`/auditorias/details?id={guid}` exponen **la única superficie
del sistema que arrastra `EntityId`, `OldValuesJson` y
`NewValuesJson` al wire**. La separación física de tipos
(`AuditoriaDetalleDto` vs `AuditoriaDto`) cierra D-2 por
construcción: el listado jamás puede exponer esos campos
aunque alguien agregue una propiedad al DTO equivocado.

Restricciones:

- `[Authorize(Roles = RolesSgv.Administrador)]` se aplica en
  tres frentes (controller API, page Details y sideNav),
  análogo a D-1. No-admin recibe `403 Forbidden`; anónimo
  recibe `401 Unauthorized`. La redirección a
  `/error/403` la aplica la cookie auth del shell con
  `AllowAutoRedirect=false` en los tests seam.
- El cliente HTTP tipado `IAuditoriaApiClient.GetDetalleAsync`
  mapea `404` → `null` sin lanzar, propaga nativas
  `HttpRequestException`/`TaskCanceledException`/`JsonException`
  vía `TransportFailureClassifier` para que la page la
  traduzca a un banner recuperable preservando el `id`
  consultado.
- La page Details distingue tres estados: `200 OK` con el DTO
  enriquecido (header con metadatos + tres bloques
  `<pre class="bg-light p-2">` para los JSON),
  "no encontrado" legible (404 upstream) y "transporte
  recuperable" (banner de error visible con el id preservado
  en el CTA "Volver al listado"). El `<pre>` es la única
  vía del sistema para mostrar JSON preformateado en el
  shell, alineado con el estilo de los demás detail pages.

Verificado por:

- `AuditoriaDetalleDto` no expone `OldValuesJson`/`NewValuesJson`
  por reflexión (defense-in-depth).
- `AuditoriasControllerTests.GetById_ExistingId_ReturnsAuditoriaDetalleDto`.
- `AuditoriasIndexTests` extensivos en Slice B (toolbar
  horizontal + sort headers + pageSize selector + Details
  link que preserva contexto).
- `AuditoriasDetailsTests` (Slice B, 4 tests): 200 con `<pre>`,
  404 legible, transporte recuperable, no-admin → 403.

### D-8 — Rename `userId` → `userName` en el filtro de auditoría y endpoint `filter-options` (issue #251, Slice A)

Issue #251 (change `2026-08-03-auditoria-filtros-select-entidad-operacion`,
Slice A) introduce dos cambios complementarios en el módulo
transversal de auditoría:

1. **Rename del parámetro del filtro `userId` → `userName`.** El
   listado (`GET /api/v1/auditorias`) aceptaba hasta ahora
   `?userId={guid}` comparando contra `a.UserId` (GUID técnico).
   El nuevo contrato acepta `?userName={name}` y compara contra
   `u.UserName` del LEFT JOIN con `AspNetUsers` (ya vigente desde
   el change archivado `2026-07-31-ajustes-listado-auditoria`,
   D-5 bis). La comparación es case-insensitive por el collation
   MySQL `utf8mb4_0900_ai_ci` (no requiere `ToLower()` en la
   lambda). El cambio es **breaking** para el único consumer del
   wire (`SGV.Web`); el shell web se actualiza en el mismo
   monomerge (Slice A de este change renombra la query key en
   `AuditoriaApiClient.BuildQueryUri` y la propiedad/prop de
   `IndexModel`; Slice B agrega el `<select>` de filtro). No hay
   período de compatibility shim — el binding legacy
   `?userId=...` queda ignorado por ASP.NET (model binding sólo
   mira la firma del record). Documentado en el PR summary y
   acá para referencia del reader.

2. **Endpoint admin-only `GET /api/v1/auditorias/filter-options`.**
   Devuelve `200 OK` con `AuditoriaFilterOptions`
   (`{ entityNames, operations }`) derivado de `SELECT DISTINCT
   EntityName` + `SELECT DISTINCT Operation` sobre `Auditorias`
   con `AsNoTracking()`. Arrays ordenados alfabéticamente, sin
   duplicados, sin cadenas vacías ni whitespace, con cap duro de
   100 elementos por array (recortado vía
   `Distinct().OrderBy().Take(100)`). Por construcción NO expone
   `UserId`, `UserName`, `EntityId`, `OldValuesJson`,
   `NewValuesJson`, `CorrelationId`, `OccurredAt` ni `Id` — el
   tipo `AuditoriaFilterOptions` sólo tiene dos colecciones de
   strings (D-2 reforzado por separación física de tipos, misma
   regla que `AuditoriaDto` vs `AuditoriaDetalleDto`).

Restricciones:

- El atributo de clase
  `[Authorize(Roles = RolesSgv.Administrador)]` del controller
  cubre el endpoint nuevo (401 anónimo / 403 no-admin / 200
  admin). Sin overrides ni `[AllowAnonymous]`.
- El join contra `AspNetUsers` NO entra en el endpoint
  `filter-options` (sólo expone strings de la tabla de auditoría).
  Reusa el `DbSet<AuditoriaEntity>` ya mapeado.
- `AsNoTracking()` en ambas queries garantiza D-4: no se
  persiste nada al leer.

Verificado por:

- `AuditoriasControllerTests.FilterOptions_Anonimo_Retorna401`,
  `FilterOptions_UsuarioSinRol_Retorna403`,
  `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados`,
  `FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName`,
  `FilterOptions_DistinctMayorACienDevuelvePrimerosCien`.
- `AuditoriasControllerTests.Listado_UserName_FiltraPorNombreNoPorGuid`,
  `Listado_UserName_Vacio_NoFiltra` — el query string
  `?userName=...` llega al servicio como
  `AuditoriaListQuery.UserName` y filtra correctamente; el
  parámetro vacío no aplica filtro.
- `AuditoriaServicioConsultaTests.QueryAsync_FiltraPorUserNameCaseInsensitive`,
  `QueryAsync_FiltroUserNameVacio_NoAplicaFiltro` (ambos
  `[MySqlFact]` — collation `utf8mb4_0900_ai_ci`).
- `AuditoriaServicioConsultaTests.GetFilterOptionsAsync_DevuelveEntityNamesYOperationsOrdenadas`,
  `GetFilterOptionsAsync_DescartaValoresVacios`,
  `GetFilterOptionsAsync_AplicaCapDeCien`.
- `AuditoriaFilterOptions` no expone `OldValuesJson`/
  `NewValuesJson`/`EntityId`/`UserId`/`UserName`/`CorrelationId`/
  `OccurredAt`/`Id` por ausencia de campos en el record
  (defense-in-depth).

### Capas y archivos clave

| Capa | Tipo | Archivo | Rol |
|---|---|---|---|
| Wire contract (DTO) | `record` | `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` | Wire contract seguro (D-2). |
| Wire contract (Query) | `record` | `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | Filtros + paginación. |
| Puerto (S1) | `interface` | `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | `QueryAsync` + `GetDetalleDtoAsync` + `GetFilterOptionsAsync`; lanza `ArgumentException` en rango invertido (D-3). |
| Impl EF (S1) | `sealed class` | `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | EF directa con `AsNoTracking` + `Select` seguro (D-1, D-2, D-4). |
| DI (S1) | extension | `src/SGV.Infraestructura/DependencyInjection.cs` | `AddScoped<IAuditoriaServicioConsulta, AuditoriaServicioConsulta>()`. |
| Controller (S2) | `sealed class` | `src/SGV.Api/Controllers/AuditoriasController.cs` | `[Authorize(Roles=RolesSgv.Administrador)]`; mapea `ArgumentException` → `400 Validation`. |
| Helper 4xx (S2) | `static class` | `src/SGV.Api/Infrastructure/Results/ApiResults.cs` | Sobrecarga additive `ToValidationProblemResult(string code, string detail, fieldErrors, httpContext)`. |
| Cliente HTTP (S3) | `interface` | `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | `QueryAsync` + `GetDetalleAsync` (Slice A rename `ObtenerPorId` → `GetDetalle`). |
| Cliente HTTP impl (S3) | `sealed class` | `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | `EnsureSuccessStatusCode`; 404 → `null`; propaga `HttpRequestException`/`TaskCanceledException` nativas; `BuildQueryUri` propaga `sort` + `correlationId`. |
| DI Web (S3) | extension | `src/SGV.Web/Program.cs` | `AddHttpClient<IAuditoriaApiClient, AuditoriaApiClient>(...).AddHttpMessageHandler<ApiBearerTokenHandler>()`. |
| Razor Page Index (S3) | `sealed class` | `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | `[Authorize(Roles=RolesSgv.Administrador)]` (D-1); Slice B agrega bind `Sort`/`CorrelationId`/`PageSize`, helpers `BuildSortRouteValues`/`BuildDetailsRouteValues`, normalizadores; `TransportFailureClassifier` para recuperables. |
| Razor View Index (S3) | `.cshtml` | `src/SGV.Web/Pages/Auditorias/Index.cshtml` | Slice A: sidebar filtros (EntityName, Operation, DateFrom, DateTo, UserId). Slice B: toolbar horizontal, `<th>` ordenables con `GetSortRoute`/`GetSortIcon`, `<select name="pageSize">` 10/20/50/100, columna Acciones con Details link, paginación con números + Primera/Última. |
| Razor Page Details (S3) | `sealed class` | `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` | Nueva en Slice B (D-7): `[Authorize(Roles=RolesSgv.Administrador)]`; `OnGetAsync(Guid)` consume `GetDetalleAsync`; clasifica 200 / 404 / transport failure; preserva contexto del listado para "Volver al listado". |
| Razor View Details (S3) | `.cshtml` | `src/SGV.Web/Pages/Auditorias/Details.cshtml` | Nueva en Slice B (D-7): header con metadatos + 3 bloques `<pre class="bg-light p-2">` para `ChangedPropertiesJson`, `OldValuesJson`, `NewValuesJson`; estados 404 legible y banner recuperable preservando id. |
| Sidenav (S3) | `.cshtml` | `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Top-level item `Auditorías` (ícono `ti ti-file-text`) gateado por `esAdministrador`. |
| Tests S1 | `[MySqlFact]` + `[Fact]` | `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | 15 tests: filtros, orden determinista, clamps, `DateFrom>DateTo`, JSON sin old/new, no-inserta-tras-query, LEFT JOIN `UserName` resuelto+fallback, sort dinámico 10 claves + inválido, `GetDetalleDtoAsync` con old/new + sin old (alta). |
| Tests S2 | `[Fact]` | `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | 9 tests: 401, 403, 200 shape, paginación+filtros, detalle 200/404, JSON sin old/new, `[Authorize]` reflexión, 400 rango invertido, propagación de `sort` y `correlationId`. |
| Tests S3 Index | `[Fact]` + `[Theory]` | `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` | 12 tests (Slice A: 6 base + Slice B: 6 nuevos con `InlineData`): admin 200 con tabla+paginación, lista vacía legible, error de transporte recuperable sin perder filtros, paginación preserva filtros, no-admin → 403, anónimo → redirect, pageSize selector 10/20/50/100, default 20, pageSize out-of-set normaliza, sort header resetea p=1 + preserva pageSize + filtros, paginación preserva sort+pageSize, Details link preserva contexto. |
| Tests S3 Details | `[Fact]` | `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | 4 tests (Slice B): 200 con JSON en `<pre>`, 404 legible, transport failure con banner recuperable preservando id, no-admin → 403. |
| Helper tests S3 | `sealed class` | `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs` | Fake in-memory del `IAuditoriaApiClient` para la suite seam PageModel. |
| Helper tests S3 | `sealed class` | `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | `WithAuditoriaApiClient(IAuditoriaApiClient fake)` — espejo de `WithHabilidadApiClient` / `WithVacanteApiClient`. |
| Helper tests S3 | `sealed class` | `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs` | `CreateAuditoriaLeaseAsync(IAuditoriaApiClient, adminRole)` — espejo de `CreateCargoLeaseAsync`. |

### Autorización

`[Authorize(Roles = RolesSgv.Administrador)]` se aplica en **tres
frentes**:

1. **Controller API (S2):** atributo a nivel de clase en
   `AuditoriasController`. Verificado por reflexión en
   `AuditoriasControllerTests.AuditoriasController_TieneAuthorizeAttribute`
   (D-1 a nivel HTTP). No-admin recibe `403 Forbidden`; anónimo
   recibe `401 Unauthorized`.
2. **Razor Page Web (S3):** atributo a nivel de clase en
   `IndexModel` (`/auditorias`). No-admin es redirigido a
   `/error/403`; anónimo a `/auth/sign-in` (mismo comportamiento
   que el resto de las páginas protegidas del shell). Verificado
   en `AuditoriasIndexTests.Get_Index_WhenNonAdmin_RedirectsToAccessDenied`
   y `Get_Index_WhenAnonymous_RedirectsToSignIn`.
3. **Sidenav (S3):** el item «Auditorías» se gated con
   `@if (esAdministrador)` en `_Sidenav.cshtml`. Usuarios no-admin
   NO ven el link; aun si escriben la URL `/auditorias` en el
   browser, el `[Authorize]` del PageModel los redirige a 403.

### No-objetivos del v1 (siguiendo el proposal)

- **No escritura:** el módulo es estrictamente read-only. La
  tabla `Auditorias` sigue siendo escrita por
  `AuditoriaSaveChangesInterceptor` + `IAuditoriaServicio` con
  los mismos contratos existentes. **No se modificaron** la
  escritura existente, el interceptor, el servicio de escritura,
  la entidad, los seeders ni los consumidores (`SetupServicio`,
  `PersonaServicioComandos`, `UsuarioServicioComandos`, etc.).
- **No recursión:** las consultas no disparan el interceptor (D-4
  verificado por test). El listado paginado nunca genera filas
  nuevas en `Auditorias`.
- **No enriquecimiento de UserId:** v1 expone el `UserId` crudo
  (string). El JOIN con `AspNetUsers` para resolver el nombre
  legible queda explícitamente fuera de alcance; ver D-5.
- **Sin endpoint web de detalle individual:** v1 entrega sólo el
  listado con drill-down parcial (futuro PR podrá agregar un
  modal o página de detalle que reuse `IAuditoriaApiClient.ObtenerPorIdAsync`,
  ya implementado pero no consumido por la Razor Page actual).
- **Sin exportación CSV/Excel.**
- **Sin retención, purga o archival** de registros.
- **Sin re-uso del `[MySqlFact]`** en S2 ni S3: el puerto EF ya
  está cubierto por S1 contra MySQL real; S2 y S3 corren contra
  fakes en memoria (controller y PageModel seam).

### Cobertura nueva

- **S1 (servicio + unit + integration):** 15 tests contra MySQL
  real + 1 `[Fact]` puro de reflexión. Atributos correctos
  (`[MySqlFact]`/`[MySqlTheory]` heredan el skip-on-unavailable
  de `MySqlTestDatabaseBootstrap.GetAvailability()`).
- **S2 (controller + API integration):** 9 tests `[Fact]`
  self-contained vía `FakeAuditoriaServicioConsulta`; corre 100%
  sin MySQL. Incluye `[Authorize]` por reflexión, ausencia de
  old/new en JSON HTTP (defense-in-depth en PascalCase +
  camelCase), `DateFrom>DateTo` → 400 con `ProblemDetails`.
- **S3 (web + Page + sidenav):** 6 tests `[Fact]` con
  `FakeAuditoriaApiClient` inyectado vía
  `SgvWebApplicationFactory.WithAuditoriaApiClient`. Cubre
  filtros preservados, paginación preservada, transporte
  recuperable, auth pipeline (admin/no-admin/anónimo).
- **Total tests añadidos en el change completo:** 30
  (15 S1 + 9 S2 + 6 S3).
- **Total suite verde al cierre del change:** 3364 / 3364
  (suite focal + combinada + global).

### Riesgos residuales

1. **OFFSET degrada en tablas grandes** (Likelihood High en
   proposal original). Mitigación: cursor pagination reservado
   para v2+; v1 acepta OFFSET por ser MVP.
2. **Flakiness preexistente** de la suite global: 0-5 fallos
   rotativos en `Setup.SetupConcurrencyMySqlFactTests.*` /
   `UsuariosEndToEndMySqlFactTests.*` / `VacanteRepositoryQueryTests.*`
   que NO están relacionados con este change (ver
   `apply-progress.md` §"S1 Bugfix" y §"S2"). La suite focal de
   Auditoría (S1 + S2 + S3) corre 100% estable.
3. **Sidenav no-admin visible pero el link se gated:** decisión
   de UX consistente con Usuarios (subítem dentro del grupo
   Seguridad). Si en el futuro se quiere ocultar el grupo entero
   a no-admin, basta con cambiar `@if (esAdministrador)` en
   `_Sidenav.cshtml` por una condición más amplia (e.g.,
   `esAdministrador || User.IsInRole("Auditor")` si se introduce
   un rol futuro).

## Housekeeping pre-release del módulo de Cargos (PR #287)

> PR: `https://github.com/elflacoseba/SGV/pull/287`
> Branch: `feat/housekeeping-cargos-release` (squash-mergeado a `develop`)
> Sha: `36b24562`
> Fecha: 2026-08-18

Cierra los cinco puntos pendientes del análisis release-readiness del módulo
de Cargos (Dominio, Aplicación, Infraestructura, API, Web). Cubre housekeeping
documental, un bug funcional real y dos limpiezas de contratos; ninguno es
breaking change. La suite del módulo (601 tests) pasa 100% estable.

### D-CH-01 — Tensión documental en `cargo-web-listado-detalle-baja/spec.md`

El `Purpose` original del slice inicial declaraba que el spec cubría solo
"consultar cargos activos, ver su detalle readonly y ejecutar baja lógica
sin expandirse a create, edit, skills, eliminados o reactivación". Esa
restricción quedó obsoleta tras seis cambios archivados:

- `2026-07-01-cargos-crear-editar-codigo-editable`
- `2026-07-01-cargos-crear-autorizacion-admin`
- `2026-07-02-cargos-filtro-activos-eliminados`
- `2026-07-05-habilidades-navegacion-cargos`
- `2026-07-06-cargos-navegacion-habilidades`
- `2026-07-06-implementar-asignar-quitar-habilidades-de-un-cargo`

Se agrega una nota de trazabilidad al inicio del spec referenciando los seis
cambios y se reescribe el `Purpose` para reflejar el comportamiento
consolidado. Los requisitos históricos se preservan para no perder
trazabilidad; los `REQ-CW-01..06` extienden el alcance original sin
reescribir la historia.

### D-CH-02 — Bug funcional en `ApplyActualizarFailureToModelState`

El helper `ApplyActualizarFailureToModelState` agregaba cada `FieldError`
**dos veces** al `ModelState`: una anclado a la fila editada bajo la clave
`Actualizar[skillId].Campo` y otra al `string.Empty` del summary general
del form Asignar. Resultado: el usuario veía el mismo mensaje dos veces
en pantalla — una correctamente anclado a la fila y otra incorrectamente
filtrado en el alert superior del form "Asignar habilidad" (que no tiene
relación con la fila que se está editando).

Refactor del helper para que use el mismo `keySelector` de
`ApplyFieldErrors` que ya usa `ApplyAsignarFailureToModelState`. Para los
campos del whitelist `{NivelRequeridoId, Ponderacion, EsObligatoria}` el
error se ancla a la fila; para campos fuera del whitelist (defensa contra
drift) cae al `string.Empty` para que el summary lo muestre sin anclar a
una fila incorrecta. **Cada error va exactamente a un destino** — sin
duplicación.

Tres tests actualizados para reflejar el nuevo comportamiento (ocurrencia
única, anclaje-a-fila sin summary).

### D-CH-03 — Invariante de `Cargo.Desactivar()` clarificada

`Cargo.Desactivar()` chequeaba `_puestos.Any(p => p.IsActive)`, pero la
navegación a `Puestos` no se carga en el camino de producción: el servicio
`CargoServicioComandos.DesactivarAsync` consulta la DB vía
`ICargoRepository.HasActivePuestosAsync` ANTES de invocar `Desactivar()`.
Por construcción, el chequeo de la entidad **nunca se ejecuta en runtime**.

El XML doc de `Desactivar()` se amplía para explicitar:

- La regla autoritativa "no desactivar un cargo con Puestos subordinados
  activos" vive en el servicio (consulta DB).
- El chequeo local sobre `_puestos` es **defensa secundaria** que solo
  aplica si alguien rehidrata la entidad con la nav incluida y luego
  invoca `Desactivar()` directamente sin pasar por el servicio.
- No es la regla de negocio autoritativa; confiar siempre en el servicio.

**No cambia comportamiento.** Defense-in-depth contra un futuro caller que
desactive el cargo bypassing el servicio.

### D-CH-04 — `CargoErrorType` alineado 1-a-1 con `ErrorCategoria`

Hasta ahora `CargoErrorType` solo cubría `NotFound/Conflict/Validation`
(3 variantes) y los clientes web colapsaban `Unauthorized/Forbidden/
Transport/Unexpected` a `Validation` por compat histórica con el legacy.
`CargoSkillErrorType` ya estaba alineado 1-a-1 con `ErrorCategoria`
(6 variantes); el agregado padre quedó con 3.

Expande `CargoErrorType` con `Unauthorized, Forbidden, Transport,
Unexpected`. Actualiza `ErrorCategoriaMappers.ToCategoria/ToTipoCargo`
al mapeo 1-a-1 con todas las categorías. Elimina la matriz duplicada
`MapCategoriaToLegacyType` del `CargoApiClient` y la sustituye por
`ErrorCategoriaMappers.ToTipoCargo` (**single source of truth**).

Preserva los ordinales 0/1/2 (`NotFound/Conflict/Validation`) — los nuevos
miembros se agregan al final — para no romper callers existentes que
dependan de `(int)CargoErrorType.X`.

Los call sites que ya discriminaban por `Categoria` (no por el legacy
`Type`) — `IndexModel.OnPostDelete`, `IndexModel.OnPostReactivate`,
`CargoHabilidadesPostHandlers.HandleQuitarAsync`, etc. — no se ven
afectados: siguen discriminando por la taxonomía común.

### D-CH-05 — `NivelCargo.ValorNumerico` vs `Orden` documentado

`ValorNumerico` (byte, 0..255) es histórico y se expone en el wire
(`NivelCargoDto`) para integraciones externas que lo consumen como
referencia. El rango intencionalmente cubre todo el byte porque el orden
semántico entre niveles NO lo determina `ValorNumerico` — ese rol lo cumple
`Orden` (int, comparador natural).

El XML doc de `NivelCargo`, su constructor y la propiedad se amplían para
explicitar:

- `ValorNumerico`: campo histórico en el wire, sin semántica de orden.
- `Orden`: orden semántico ascendente, determina cómo se listan y comparan
  los niveles.

**No cambia comportamiento.** Defense-in-depth contra un futuro caller que
asuma que `ValorNumerico` define la jerarquía.

### Estado de release del módulo de Cargos

Tras este PR, el módulo de Cargos queda **release-ready** sin pendientes
abiertos en código ni en docs:

- ✅ Build verde, 0 errores.
- ✅ 601 tests del módulo (Cargo + NivelCargo + ErrorCategoria) pasan.
- ✅ 8 specs sincronizadas en `openspec/specs/` (ningún cambio archivado
  pendiente de sincronizar).
- ✅ 11 cambios archivados; 0 cambios activos en `openspec/changes/`.
- ✅ Sin issues conocidas abiertas sobre este módulo.
- ✅ Gating admin unificado en tres frentes (controller, PageModel,
  helpers de form) verificado por tests.

## Integración Continua

No se utiliza GitHub CI. El repo es unipersonal en etapa de desarrollo activo; los tests que requieren MySQL (`[MySqlFact]`) no pueden ejecutarse en runners de GitHub sin una base de datos real y la suite completa tarda más de lo razonable para feedback iterativo. La validación se hace localmente con `dotnet test SGV.slnx`. Los tests `[MySqlFact]` se skipean automáticamente cuando no hay conexión MySQL disponible. El workflow `.github/workflows/ci.yml` existe como referencia pero está desactivado.

## SDK y Target Framework

Los proyectos apuntan a `net10.0` (.NET 10). El archivo `global.json` fija el SDK en `10.0.300` con roll-forward `latestMajor` para permitir compatibilidad con versiones posteriores del SDK 10.x.

## Proveedor de Base de Datos

Se utiliza Pomelo Entity Framework Core 9.x como proveedor único para MySQL 8. Los paquetes `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `Pomelo.EntityFrameworkCore.MySql` permanecen en versiones 9.x porque Pomelo 9 depende de EF Core relational `>= 9.0.0 && < 9.0.999`. SQL Server no se soporta como proveedor activo.

## Dualidad de paths: MySQL local (EF Core) + MariaDB producción (script SQL standalone)

El repo soporta dos servidores de base de datos según el ambiente:

| Ambiente | Servidor | Mecanismo de provisionamiento |
|---|---|---|
| **Local / desarrollo** | MySQL 8.x | `dotnet ef database update` (con historial en `__EFMigrationsHistory`) |
| **Producción** | MariaDB 10.11+ | `docs/migracion-inicial-sgv-mariadb.sql` (script standalone, hand-crafted) |

### Por qué dualidad y no unificación

MariaDB 10.11.x es más estricto que MySQL 8 con columnas generadas:

- **No acepta `CASE WHEN ... ELSE NULL` dentro de columnas `GENERATED ALWAYS AS` cuando la columna fuente es `CHAR(N)`** (con cualquier collation). Devuelve `Function or expression 'case when ... else NULL end' cannot be used in the GENERATED ALWAYS AS clause`. Esto afecta a las columnas GUID que el modelo define como `char(36) COLLATE ascii_general_ci` (PK/FK de `Personas`, `Postulantes`, `Ocupaciones`, etc.).
- **No permite `UNIQUE INDEX` sobre columnas `VIRTUAL`**. Solo sobre `STORED`. MySQL 8 también acepta STORED, así que el cambio no rompe el camino EF.

El primer intento de unificar el camino (que `dotnet ef migrations script` generara un script que sirviera para ambos) falla porque los `Designer.cs` de las migraciones archivadas no reflejan `stored: true` del snapshot actual — los `CREATE TABLE` de las migraciones 1-12 generan columnas CASE WHEN **VIRTUAL**, no STORED. La migración correctiva `MariaDbStoredColumnsAndCollation` (la #13) hace la transición VIRTUAL→STORED al final del pipeline, pero en una base MariaDB virgen la primera migración revienta antes de llegar ahí.

### Workaround aplicado al script MariaDB

1. **Columnas fuente `CHAR(36) COLLATE ascii_general_ci` → `VARCHAR(36) COLLATE ascii_general_ci`** en `Postulantes.PersonaId`, `Ocupaciones.PersonaId`, `Ocupaciones.PuestoId` y `Personas.TipoDocumentoId` (esta última se crea via `ALTER TABLE ADD COLUMN` después de creada la tabla base). Esto destraba el `CASE WHEN STORED` sobre esas columnas.
2. **Columnas CASE WHEN STORED generadas con `CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`** (en lugar de `COLLATE ascii_general_ci`). Evita conversiones automáticas de charset que MariaDB rechaza dentro de GENERATED ALWAYS AS.
3. **`utf8mb4_0900_ai_ci` → `utf8mb4_unicode_ci` global** (collation MySQL 8 exclusiva que MariaDB no soporta).
4. **`CONCAT()` con CAST explícito** en `Personas.ActiveDocumentoUnique`: `CONCAT(CAST(TipoDocumentoId AS CHAR CHARACTER SET utf8mb4), ':', NumeroDocumento)` para evitar el `convert(...)` automático que MariaDB rechaza.

### Limitaciones del script MariaDB (a diferencia del camino MySQL/EF)

- **No usa `__EFMigrationsHistory`** para trazabilidad. Aunque crea la tabla y registra las 13 migraciones, no se actualiza en reaplicaciones. La trazabilidad real vive en este archivo + el repo Git.
- **No incluye datos semilla** (`AgregarDatosSemillaBase`). Los seeders viven en `src/SGV.Infraestructura/Persistencia/Seeds/`.
- **`DROP TABLE IF EXISTS + CREATE` para re-ejecución idempotente**. NO usar contra una base MySQL 8 preexistente (cambia la collation).
- **Stored procedures con `DELIMITER`**: el script se aplica con el CLI `mysql` (que parsea directivas `DELIMITER`). Si se aplica programáticamente (vía `MySqlConnector`/Python), el cliente debe parsear las directivas `DELIMITER` o usar `--delimiter` en el CLI. Conectar con `Allow User Variables=true` si se usa `MySqlConnector` (los `SET @var = ...` del script lo requieren).
- **Stored procedure anidado para D7 (#263)**: la versión previa de la migración `DropSoftDeleteFromAspNetUsers` declaraba un procedure interno `__sgvApplyD7` anidado dentro del procedure `MigrationsScript` que EF Core genera para el modo `--idempotent`. MySQL rechaza esa anidación con `ERROR 1357 ("Can't drop or alter a PROCEDURE from within another stored routine")`, por lo que el script abortaba antes de aplicar el soft-delete. La versión actual ejecuta los 10 pasos de D7 en SQL directo, gated por un `@needsD7` derivado de `information_schema.COLUMNS` y ejecutado vía `PREPARE`/`EXECUTE`/`DEALLOCATE PREPARE`. El preflight fail-loud custom (`SIGNAL SQLSTATE '45000'`) se reemplazó por un `ADD UNIQUE INDEX` temporal sobre `PersonaId` (`__sgvD7_PreflightUnique`): si hay duplicados activos, MySQL aborta con `ERROR 1062` antes de cualquier operación destructiva. La barrera natural del UNIQUE INDEX es suficiente para el criterio end-to-end; el mensaje custom se pierde como trade-off aceptable.

### Validación empírica

Antes de mergear a develop, las pruebas se ejecutaron contra `sgvapi.elflacoseba.dev:3306` (MariaDB 10.11.13 real):

- ✅ CREATE TABLE con 4 columnas CASE WHEN STORED (Cargos, Personas×3, Ocupaciones×3, Postulantes) → todas aceptadas
- ✅ INSERT activo + 2 soft-deleted con mismo `Codigo`/`DNI` → conviven
- ✅ INSERT 2do activo con mismo `Codigo` → rechazado por UNIQUE INDEX
- ✅ Patrón con `FechaFin IS NULL AND IsDeleted = 0` (Ocupaciones) → funciona
- ✅ Patrón con `CONCAT(CAST(... AS CHAR CHARACTER SET utf8mb4), ':', ...)` → funciona

Si en el futuro el proyecto unifica el camino MariaDB al estilo EF puro, hay que regenerar los `Designer.cs` de las migraciones 1-12 (o agregar una mini-migración correctiva VIRTUAL→STORED al inicio del pipeline). No hay trabajo previo en esa dirección; el camino dual se mantiene por estabilidad de despliegues.

## Issue #263 — script standalone ejecutable punta a punta

> Cambio #263 cierra dos bugs que impedían aplicar
> `docs/migracion-inicial-sgv.sql` contra una base MySQL 8 limpia
> en modo `--idempotent`. La investigación partió de una afirmación de
> la issue (UPDATE sin `;` en `Ocupaciones.TipoAsignacion`) y descubrió
> un segundo bug latente en la migración `DropSoftDeleteFromAspNetUsers`
> (procedure anidado en `MigrationsScript`).

### Bug 1 — `UPDATE` sin `;` dentro del wrapper `MigrationsScript`

`dotnet ef migrations script --idempotent` envuelve cada operación de
cada migración en un stored procedure `MigrationsScript()` cuya
estructura es:

```sql
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '...') THEN

    -- cuerpo de la operación acá

    END IF;
END //
```

Una sentencia `migrationBuilder.Sql("UPDATE ...")` que **no termina
con `;`** produce dentro de ese cuerpo `UPDATE ... \n\n END IF;`, lo
que MySQL concatena hasta el próximo `;` y devuelve `ERROR 1064`. La
migración `ConvertirTipoAsignacionAEnumYActualizarUnicidad` tenía
seis UPDATE sin terminador (tres en `Up`, tres en `Down`). Fix: agregar
`;` al final de cada `migrationBuilder.Sql("UPDATE ...")` en ambos
métodos. Comportamiento EF runtime intacto.

### Bug 2 — stored procedure anidado en `MigrationsScript`

La migración `DropSoftDeleteFromAspNetUsers` (D7) declaraba un
procedure interno `__sgvApplyD7` anidado dentro del wrapper
`MigrationsScript` que EF Core genera para `--idempotent`:

```sql
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(...) THEN

    DROP PROCEDURE IF EXISTS __sgvApplyD7;        -- ❌ ERROR 1357
    CREATE PROCEDURE __sgvApplyD7()
    BEGIN
        ...
    END;

    CALL __sgvApplyD7();
    DROP PROCEDURE __sgvApplyD7;

    END IF;
END //
```

MySQL rechaza `DROP/CREATE PROCEDURE` dentro de otro stored routine
con `ERROR 1357 ("Can't drop or alter a PROCEDURE from within
another stored routine")`. Esto bloqueaba el script mucho antes del
paso destructivo. Tres opciones evaluadas (ver `apply-progress.md`
del change archivado `2026-07-16-quita-soft-delete-usuario`):

- **A — Mantener el procedure y reescribir MigrationsScript wrapper**: requiere patchear el generador de EF, fuera de alcance.
- **B — Script sin DELIMITER + sin wrapper**: requiere regenerar con otro modo, pierde idempotencia.
- **C — Reescribir D7 en SQL directo gated por `information_schema`** ✅.

### Decisión adoptada (vigente) — `C` con preflight natural

La nueva versión de `20260716120000_DropSoftDeleteFromAspNetUsers.cs`
ejecuta los **10 pasos del design D7** en SQL directo, gated por un
`@needsD7` booleano derivado de `information_schema.COLUMNS` (existe
`IsDeleted`?) y ejecutado vía `PREPARE` / `EXECUTE` / `DEALLOCATE
PREPARE`. El `IF NOT EXISTS(SELECT 1 FROM __EFMigrationsHistory WHERE
MigrationId = '...')` que EF Core sigue generando afuera del cuerpo
mantiene la idempotencia a nivel migración; el chequeo defensivo de
`@needsD7` cubre el caso de fila huérfana en el historial.

**Preflight fail-loud:** el `SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT`
custom de la versión previa se reemplaza por un `ADD UNIQUE INDEX
__sgvD7_PreflightUnique (PersonaId)` en el paso 1. Si hay duplicados
activos, MySQL devuelve `ERROR 1062` y aborta el script antes de
cualquier mutación destructiva. El índice temporal se dropea en el
paso 8 y se recrea como canónico (`IX_AspNetUsers_PersonaId`) en el
paso 9. Trade-off explícito: **se pierde el mensaje custom** del
`SIGNAL`; el `ERROR 1062` nativo de MySQL (con su mensaje estándar
"Duplicate entry ... for key '__sgvD7_PreflightUnique'") es la nueva
señal fail-loud. Suficiente para el criterio end-to-end.

| Paso | Operación |
|------|-----------|
| 1    | `ADD UNIQUE INDEX __sgvD7_PreflightUnique (PersonaId)` — preflight natural |
| 2    | `UPDATE AspNetUsers SET LockoutEnabled=1, LockoutEnd='9999-12-31 23:59:59.999999' WHERE IsDeleted = 1` |
| 3    | `DROP FOREIGN KEY FK_AspNetUsers_Personas_PersonaId` (INPLACE, LOCK=NONE) |
| 4    | `DROP INDEX IX_AspNetUsers_ActiveUserNameUnique` (INPLACE, LOCK=NONE) |
| 5    | `DROP INDEX IX_AspNetUsers_ActivePersonaIdUnique` (INPLACE, LOCK=NONE) |
| 6    | `DROP COLUMN ActiveUserNameUnique, ActivePersonaIdUnique, IsDeleted` (INPLACE, LOCK=NONE) |
| 7    | `DROP INDEX IX_AspNetUsers_PersonaId` (no-único, vigente desde `AddSoftDeleteToAspNetUsers`) (INPLACE, LOCK=NONE) |
| 8    | `DROP INDEX __sgvD7_PreflightUnique` (INPLACE, LOCK=NONE) |
| 9    | `ADD UNIQUE INDEX IX_AspNetUsers_PersonaId (PersonaId)` (INPLACE, LOCK=NONE) |
| 10   | `ADD CONSTRAINT FK_AspNetUsers_Personas_PersonaId FOREIGN KEY (PersonaId) REFERENCES Personas (Id) ON DELETE RESTRICT` (ALGORITHM=COPY) |

### Limitación documentada — migración sin Designer

`dotnet ef migrations list` detecta **17** migraciones (incluida D7);
tanto `dotnet ef database update` como el script standalone
`docs/migracion-inicial-sgv.sql` aplican las mismas **17** y dejan
idéntico end-state en `__EFMigrationsHistory`. La migración
`20260730000000_SemillaTipoUnidadOrganizativaAmpliada` carece de
`.Designer.cs` por lo que **no aparece en `dotnet ef migrations
list`**, queda **fuera del script standalone**, y sus 13 filas de
`InsertData` (Sede, Region, etc.) **no se ejecutan en ninguno de los
dos paths**. Esto es preexistente a #263, no introducido por este
cambio.

**Conteo posterior a la corrida:** ambos paths dejan **7 filas** en
`TiposUnidadOrganizativa` (las del seed original de
`20260616190624_CambiarTipoUnidadATablaTipoUnidadOrganizativa`), no
20. Los 13 registros adicionales viven sólo en
`DatosSemilla.HasData` (snapshot EF Core) y **no se materializan
automáticamente** porque la migración que los insertaría no está
registrada. `dotnet ef database update` los reportaría como un
delta de `HasData` sólo si el Designer.cs de esa migración
existiera y la registrara; sin Designer, EF considera la migración
inexistente y no emite inserts. El script standalone, análogamente,
sólo recorre las migraciones que el wrapper genera a partir del
model snapshot vigente.

**No afecta deployments reales:** producción usa el camino MariaDB
hand-crafted (`scripts/migracion-inicial-sgv-mariadb.sql`) o el
runtime `Database.Migrate()` con un Designer.cs correcto; ninguno
de esos paths pasa por esta migración huérfana. Regenerar el
`Designer.cs` de `SemillaTipoUnidadOrganizativaAmpliada` o crear
una migración correctiva que inserte los 13 registros faltantes es
**trabajo de follow-up separado**; está fuera del alcance de #263,
cuyo objetivo era hacer el script standalone ejecutable punta a
punto, no completar la detectabilidad del set completo de
migraciones.

### Cobertura nueva

- `tests/SGV.Tests/Persistencia/ScriptStandaloneSmokeMySqlFactTests.cs`
  (2 tests `[MySqlFact]`): ejecuta `docs/migracion-inicial-sgv.sql`
  completo contra una DB MySQL efímera (creada y destruida por el
  test), verifica que las 17 migraciones quedan registradas en
  `__EFMigrationsHistory` y que el end-state post-D7 es correcto
  (`IsDeleted`/`ActiveUserNameUnique`/`ActivePersonaIdUnique`
  eliminados, `IX_AspNetUsers_PersonaId` UNIQUE, FK RESTRICT).
  El segundo test verifica idempotencia aplicando el script dos veces.
  La password se inyecta vía `ProcessStartInfo.Environment["MYSQL_PWD"]`
  y el script se alimenta por stdin — la password no aparece en
  `argv` ni en `ps`.
- `tests/SGV.Tests/Persistencia/ScriptStandaloneStaticGuardTests.cs`
  (3 tests `[Fact]`): defense-in-depth que detecta los dos patrones
  de bug originales sin necesidad de MySQL real — sentencia sin `;`
  dentro de `MigrationsScript` (Bug 1) y `CREATE`/`DROP`/`CALL`
  procedure anidado (Bug 2). Si alguien reintroduce cualquiera de
  los dos patrones, el test falla con el offset aproximado.
- `tests/SGV.Tests/Persistencia/DropSoftDeleteMigracionTests.cs`
  (7 tests `[Fact]`): actualizados para reflejar el nuevo diseño —
  preflight por `__sgvD7_PreflightUnique` con verificación de
  posición (`IndexOf`) **antes** de cualquier mutación destructiva,
  reentrancia via `information_schema` + `PREPARE`/`EXECUTE`, ausencia
  del procedure interno `__sgvApplyD7`.

### Riesgos residuales

1. **Mensaje del preflight es `ERROR 1062` nativo** en vez del SIGNAL
   custom. Operadores que busquen el texto del mensaje viejo en logs
   no lo encontrarán; el mensaje nativo de MySQL es la nueva señal
   fail-loud. Aceptable: el `ADD UNIQUE INDEX` natural es la barrera
   que importa.
2. **El smoke test invoca `mysql` CLI** porque MySqlConnector no
   soporta directivas `DELIMITER` (https://mysqlconnector.net/delimiter).
   El path operativo es el mismo que usaría un operador (CLI mysql),
   no MySqlConnector. La password se pasa vía `psi.Environment["MYSQL_PWD"]`
   y el script se alimenta por `StandardInput` — la password **no
   aparece en `argv` ni en `ps`** del proceso `mysql` ni del proceso
   test runner.

## Índices Únicos con Soft Delete

MySQL no soporta índices filtrados como SQL Server. Para preservar las reglas de unicidad sobre registros activos (no eliminados), se utilizan columnas generadas (computed columns) con índices únicos. La columna generada devuelve el valor de la columna de negocio cuando el registro está activo (`IsDeleted = 0`) y `NULL` cuando está eliminado. MySQL permite múltiples `NULL` en índices únicos, lo que replica el comportamiento de los índices filtrados de SQL Server.

## Identity

Se mantiene `IdentityUser` con clave string, por lo que las columnas de auditoría que referencian usuarios usan `varchar(450)`. Esta decisión conserva el comportamiento estándar de ASP.NET Core Identity y evita personalización prematura.

## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por Puesto (`ActivePuestoIdUnique`) y una única ocupación vigente por la combinación Persona + Puesto (`ActivePersonaPuestoUnique`), mediante columnas generadas con índices únicos. Una Persona puede mantener varias ocupaciones activas simultáneas siempre que correspondan a Puestos distintos. La regla vigente de unicidad por persona simple no se aplica; una futura restricción de ese tipo requeriría reintroducir la columna `ActivePersonaIdUnique` con su índice único y la verificación correspondiente en la capa de aplicación.

## Postulantes Externos

Los postulantes externos se registran sin habilidades estructuradas en esta versión. La compatibilidad automática queda enfocada en postulantes internos vinculados a una persona.

## Auditoría

La auditoría se implementa con una tabla única `Auditorias` y un interceptor de EF Core. Se excluyen campos sensibles por nombre para evitar persistir contraseñas, tokens o stamps de seguridad en JSON.

## TestSgvDbContextFactory (separado del factory de producción)

El factory de tests (`tests/SGV.Tests/Persistencia/TestSgvDbContextFactory.cs`) es independiente de `SgvDbContextFactory`. Razones:

1. **Responsabilidades distintas:** el factory de producción está diseñado para `dotnet ef` design-time (migraciones, scripting). El de tests persigue disponibilidad inmediata.
2. **Default seguro en tests, fail-loud en producción:** `TestSgvDbContextFactory` cae a `localhost:3306;Database=sgv_test;User=root;Password=` cuando no hay configuración externa. `SgvDbContextFactory` tira `InvalidOperationException` en la misma situación — es parte de la seguridad: no exponer credenciales por defecto.
3. **Aislamiento:** los tests nunca heredan config de producción ni viceversa. Si el developer setea `ConnectionStrings__SgvDatabase`, ambos apuntan al mismo target, pero cada uno resuelve su propia cadena.
4. **El runtime de la API no usa ninguno de los dos factories:** lee `builder.Configuration.GetConnectionString("SgvDatabase")` vía DI estándar en `Program.cs`.

## SgvDbContextFactory fail-loud

El factory de producción (`src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs`) **no tiene fallback de conexión**. Si no se configura `ConnectionStrings:SgvDatabase` (vía user-secrets, env var o appsettings), lanza `InvalidOperationException` con un mensaje que orienta al developer. Históricamente tenía un placeholder `"CONEXION_STRING_AQUI"` y luego un default con credenciales hardcodeadas, ambos eliminados por razones de seguridad.

Cada developer debe configurar una vez:
```bash
dotnet user-secrets set "ConnectionStrings:SgvDatabase" \
  "Server=localhost;Port=3306;Database=SGV;User=root;Password=TU_PASSWORD" \
  --project src/SGV.Api
```
CI exporta `ConnectionStrings__SgvDatabase` directamente en `.github/workflows/ci.yml`.

## Contrato runtime MySQL — health, readiness y startup

Esta subsección documenta el contrato operativo de `SGV.Api` y `SGV.Web` respecto del runtime MySQL introducido por el change `2026-07-14-fix-126-operational-tech-debt` (issue #126). Cubre los probes de liveness y readiness, el comportamiento de `ServerVersion.AutoDetect`, el `Connection Timeout` recomendado, la separación entre los factories design-time y el runtime, y la ubicación de los secretos por ambiente. El objetivo es que cualquier operador pueda decidir qué esperar del proceso sin tener que leer el código de los composition roots.

### Liveness

`GET /health/live` en `SGV.Api` y `SGV.Web` no requiere MySQL ni contacto con la API upstream. Responde `200 OK` únicamente si el proceso está vivo y el pipeline HTTP responde. Es útil como `livenessProbe` de Kubernetes o equivalente: si el binario está en un loop de crash, el orquestador lo detecta y reinicia. La ruta está mapeada con `Predicate = _ => false` para garantizar que ningún check con side effects (DB, red) se ejecute durante liveness.

### Readiness

`GET /health/ready` en `SGV.Api` requiere MySQL alcanzable. El check abre una conexión cruda con `MySqlConnector.MySqlConnection` (sin pasar por EF Core ni por `SgvDbContext`), respeta el `CancellationToken` del orquestador, y reporta `Healthy` si la conexión abre o `Unhealthy(message)` si falla. Por construcción **no dispara `ServerVersion.AutoDetect`** ni resuelve el contexto de EF, así que el probe no introduce latencia adicional por detección de versión.

`GET /health/ready` en `SGV.Web` requiere que la API upstream responda `200` a su propio `/health/live` dentro de 3 segundos (`HttpClient.Timeout = TimeSpan.FromSeconds(3)` en el named client `SgvApiHealthProbe`, sin `ApiBearerTokenHandler` porque es un probe, no un request user-facing). Responde `503 Service Unavailable` con cuerpo JSON `status: "Unhealthy"` si algún componente está caído.

### Anonimato de los probes

Los dos probes son anónimos en API y Web. Cada `MapHealthChecks(...)` aplica `.AllowAnonymous()` explícito. La `FallbackPolicy = RequireAuthenticatedUser()` permanece intacta: solo `/health/live` y `/health/ready` son excepción. Esto preserva el default-deny vigente sobre el resto de la API y queda codificado por el delta `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md`. Los orquestadores pueden pegar a los probes sin credenciales y sin recibir `401` ni redirects a `/auth/sign-in`.

### Timeout de conexión recomendado

La connection string productiva **DEBE** incluir `Connection Timeout=5` (cinco segundos) para acotar el `Open()` de `MySqlConnector` tanto en el readiness check como en la primera apertura de `MySqlConnection` que EF Core dispara al ejecutar la primera consulta. Sin esta configuración, `MySqlConnector` cae al default de plataforma (típicamente 15 segundos), y un MySQL inalcanzable puede colgar el primer request del proceso durante ese presupuesto.

El chequeo del runtime no aborta al `Build()` si falta `Connection Timeout`: la advertencia queda cubierta por esta documentación operativa (`.NET 10` no expone `ValidateOptionsResult.Warn`, ver `design.md` §4.E). En cambio, una connection string ausente, whitespace o sin `Server=` y `Database=` sí aborta el host; y un `MySql:ServerVersion` no parseable aborta el host con `OptionsValidationException`.

### Versión de servidor MySQL (`MySql:ServerVersion`)

`ServerVersion.AutoDetect(connectionString)` quedó **descartado del runtime** por su costo operacional: abría una conexión TCP al construir las opciones del `SgvDbContext` y bloqueaba el primer request autenticado cuando MySQL estaba transitoriamente inalcanzable (visible en stack frames de `Sgv.Api.Seguridad.RevalidatorCredenciales`). En su lugar, tanto `Program.cs` (runtime) como `SgvDbContextFactory` (design-time) construyen un `MySqlServerVersion` a partir de la clave de configuración:

- **Clave**: `MySql:ServerVersion`
- **Formato**: `MAJOR.MINOR.PATCH` (ej. `8.0.36`).
- **Default**: `8.0.36`.
- **Override por ambiente**: variable de entorno `MySql__ServerVersion` (convención `__` de ASP.NET Core).
- **Validación**: parseo fail-loud tanto en `Program.cs` (throw temprano `OptionsValidationException`) como en `SgvDbContextOptionsValidator` (`IValidateOptions<DbContextOptions<SgvDbContext>>` con `ValidateOnStart`) y en `SgvDbContextFactory` (`InvalidOperationException`). Una versión malformada aborta el host antes de cualquier request.
- **Por qué no se sigue pre-calentando**: el costo quedó eliminado. No hay tráfico de red durante la construcción de opciones; el primer request que use la DB resuelve `SgvDbContext` con la versión fija y abre su conexión con el `Connection Timeout` configurado.

### Separación design-time vs runtime

- `SgvDbContextFactory` (design-time, en `src/SGV.Infraestructura/`) lee `MySql:ServerVersion` desde `appsettings*.json` + env vars (`MySql__ServerVersion`) y aplica el principio fail-loud. Sirve únicamente para `dotnet ef` (migraciones, scripting). El host de la API **no** lo invoca.
- `Program.cs` (runtime, en `src/SGV.Api`) usa `MySqlServerVersion` construido a partir de `MySql:ServerVersion` (misma fuente). El parseo valida en startup con `OptionsValidationException`; el `IValidateOptions` registrado corrobora el formato vía `ValidateOnStart`.
- Esta coexistencia no rompe el contrato de migraciones: `dotnet ef migrations` lee `SgvDbContextFactory`; la API runtime usa la registrada en DI. Los tests usan `TestSgvDbContextFactory`, completamente independiente.

### Ubicación de los secretos por ambiente

- **CI** (`mysql:8.0` service en `.github/workflows/ci.yml`): exporta `ConnectionStrings__SgvDatabase` como variable de entorno del job. ASP.NET Core convierte `__` en `:` para `IConfiguration`.
- **Local dev**: cada developer debe generar su propia connection string con `dotnet user-secrets set "ConnectionStrings:SgvDatabase" "<su conexión>" --project src/SGV.Api`. El factory de tests cae a un default seguro (`localhost:3306;Database=sgv_test;User=root;Password=`) para que la suite corra sin setup cuando MySQL está disponible; si MySQL no responde, los `[MySqlFact]` se omiten limpio.
- **Producción / staging**: secret manager del orquestador (AWS Secrets Manager, GCP Secret Manager, Azure Key Vault) inyectado como env var al arranque del pod. **Nunca commitear** la connection string productiva a git. Los archivos `appsettings*.json` versionados no contienen connection strings reales; solo config no sensible (logging, Swagger, origins, placeholder JWT dev).

> El placeholder JWT dev (`DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000`) presente en `src/SGV.Api/appsettings.Development.json` y `src/SGV.Web/appsettings.Development.json` es **solo** para development local. **NO DEBE** aparecer como valor productivo en ningún ambiente. Detectarlo en una review es trivial con `grep "DEV-PLACEHOLDER" config.json` o equivalente en el repositorio de configuración del orquestador.

### Migraciones

Las migraciones EF Core **no se ejecutan al startup** de `SGV.Api`. Corren fuera de banda, operadas por pipeline CI o por `dotnet ef database update` manual contra el ambiente destino. El único `Database.Migrate()` productivo vive en el bootstrap de tests (`tests/SGV.Tests/Persistencia/MySqlTestDatabaseBootstrap.cs:92-108`).

### Validación al startup

Si `ConnectionStrings:SgvDatabase` falta, está whitespace, o no incluye `Server=` y `Database=`, `SGV.Api` aborta el `Build()` con `Microsoft.Extensions.Options.OptionsValidationException` citando la clave `ConnectionStrings:SgvDatabase` y la causa específica. El host **no** continúa con `ServerVersion.AutoDetect` ni con el registro del contexto. Esto coincide con el patrón fail-loud vigente para JWT (`Program.cs` de ambos proyectos) y para `SgvApi:BaseUrl` en `SGV.Web`. La validación es diferida (`IValidateOptions<DbContextOptions<SgvDbContext>>`) y se ejecuta en el primer resolve del contexto vía `ValidateOnStart`; además hay un throw temprano inline en `Program.cs` para cortar antes de cualquier override de `WebApplicationFactory.ConfigureAppConfiguration`.

### Compatibilidad validada con MySQL 8.4 LTS

**Validado empíricamente** (2026-08-12) contra MySQL 8.4.11 LTS remoto en una DB efímera: las 17 migraciones del repo aplican limpias desde cero. Las 11 columnas `ActiveXxxUnique` quedan como `STORED GENERATED` y los `IX_*_Active*Unique` existen como `UNIQUE` (`NON_UNIQUE=0`). Ver `openspec/changes/archive/2026-08-12-fix-mysql84-compat/apply-progress.md`.

Restricciones y lecciones operativas derivadas:

- **UNIQUE INDEX sobre GENERATED VIRTUAL**: aceptado por MySQL 8.4.11 LTS con el mismo comportamiento de MySQL 8.0. La conversión explícita `VIRTUAL → STORED` que hace la migración `20260729145632_MariaDbStoredColumnsAndCollation` (julio 29) sigue siendo necesaria para compatibilidad con **MariaDB** (donde el UNIQUE INDEX sobre VIRTUAL sí se rechaza) y como preparación para futuros motores. No es un workaround para MySQL.
- **`migrationBuilder.Sql()` con statements multi-`;` rompe el script `--idempotent`**: el script generator de Pomelo envuelve cada `mb.Sql()` en `DROP PROCEDURE IF EXISTS MigrationsScript; DELIMITER // CREATE PROCEDURE MigrationsScript() BEGIN IF NOT EXISTS(...) THEN <contenido> END IF; END // DELIMITER ; CALL MigrationsScript(); DROP PROCEDURE;`. Cualquier `;` interno que no sea el cierre del statement migracional se interpreta como cierre del `BEGIN ... END` y produce error de sintaxis. Patrones prohibidos dentro de `mb.Sql()` cuando la migración entra al script `--idempotent`:
  - `BEGIN ... END` con statements internos separados por `;`.
  - `CREATE PROCEDURE ... BEGIN ... END` (anidado en otro procedure — ilegal en MySQL directamente, además de romper el wrapper).
  - `SET @sql := ...; PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;` (4 statements, 4 `;`).
  - Para lógica condicional idempotente en migraciones que entran al script idempotent, preferir **múltiples `mb.Sql()` separados**, cada uno con **UN SOLO** statement que internamente es atómico (e.g. `ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...` o `DROP PROCEDURE IF EXISTS ...`).

## Mapa de bloques GUID reservados por catálogo

Para que los catálogos inmutables seedeados por migración tengan IDs estables y predecibles — y para evitar colisiones accidentales entre catálogos que crezcan a futuro — el proyecto reserva bloques contiguos de 16 bits del espacio de GUIDs. Cada bloque agrupa `2^16 = 65536` filas (suficiente para catálogos pequeños/medianos); el primer byte del `Guid` (little-endian byte 0) identifica el catálogo al que pertenece la fila. Todos los IDs son persistidos como `CHAR(36) COLLATE ascii_general_ci` en MySQL.

| Bloque GUID     | Catálogo                        | Ejemplo de uso            | Constantes                       |
|-----------------|---------------------------------|---------------------------|----------------------------------|
| `60000000-…`    | `TipoUnidadOrganizativa`        | `TipoUnidadOrganizativaConstantes` | `InstitucionId`, `AreaId`, `GerenciaId`, `SedeId` |
| `70000000-…`    | `NivelCargo` (issue #141)       | `NivelCargoConstantes`    | `DirectivoId`, `OperativoId`     |
| `71000000-…`    | `TipoDocumento` (issue #147)    | `TipoDocumentoConstantes` | `DniId`, `LeId`, `LcId`, `PasaporteId` |
| `72000000-…`    | `CategoriaHabilidad` (issue migrar-campo-categoria-habilidades-a-tabla) | `CategoriaHabilidadConstantes` | `ConduccionId`, `TecnicaId`, `DominioId`, `AcademicaId` |
| `20000000-…`   | `EstadoVacante` (change `feature/implementar-modulo-vacantes`) | `EstadoVacanteConstantes` | `AbiertaId`, `EnSeleccionId`, `CubiertaId`, `CanceladaId` |
| (libre)         | Próximos catálogos              | reservado                 | —                                |

**Por qué bloques y no IDs al azar.** Los seed values se persisten tanto en `DatosSemilla.HasData` (model snapshot path) como en `InsertData` dentro de la migración EF. Un test de paridad (`DatosSemilla_*_SeedIdsMatchConstantes`) asserta que ambos lugares usen la misma source-of-truth. Si los IDs se generaran con `Guid.NewGuid()`, ese test sería frágil: cualquier `add migration` accidental movería los IDs en el snapshot sin tocar la fila viva. Con bloques reservados por catálogo, los IDs quedan explícitos en el código de constantes y el bloque de 16 bits sirve como una "etiqueta" legible del catálogo dueño.

**Por qué 16 bits y no otro tamaño.** Un bloque de 16 bits provee 65536 filas. `NivelCargo` usa 2 (Directivo, Operativo); `TipoDocumento` usa 4 (DNI, LE, LC, Pasaporte). El catálogo más grande previsto a futuro sigue quedando holgado. Si un catálogo crece más allá de 65536 filas (no previsto), se le asigna un nuevo bloque adyacente.

**Regla operativa para próximos cambios.** Cualquier catálogo inmutable nuevo DEBE:

1. Asignarse un bloque contiguo `XX000000-…` con `XX` aún no usado.
2. Declarar sus IDs en `src/SGV.Infraestructura/Persistencia/Catalogos/<Nombre>Constantes.cs` siguiendo el patrón de `NivelCargoConstantes` y `TipoDocumentoConstantes`.
3. Actualizar este mapa en `docs/decisiones-implementacion.md` y `AGENTS.md`.

Los catálogos mutables (CRUD vía API) NO usan este mapa: generan IDs con `Guid.NewGuid()` como cualquier entidad de negocio.

## Gestión de secretos JWT

`JwtOptions.SigningKey` cumple el mismo principio fail-loud que `SgvDbContextFactory`: no hay default embebido. Si la sección `Jwt:SigningKey` falta, está vacía, contiene solo whitespace o mide menos de 32 bytes UTF-8, el host **no arranca** y `Program.cs` propaga un `Microsoft.Extensions.Options.OptionsValidationException` con el mensaje `Jwt:SigningKey must be configured and ≥32 UTF-8 bytes`. Este contrato se valida en `WebApplicationFactory<TEntryPoint>.CreateClient()` vía `ValidateOnStart`, así que cualquier arranque — development, CI o producción — cae en el mismo fail-loud. Aplica tanto a `SGV.Api` como a `SGV.Web`: la API firma/valida bearer tokens y la Web valida firma, issuer, audience y lifetime antes de convertir el JWT en principal de cookie.

**Dev local.** `src/SGV.Api/appsettings.Development.json` y `src/SGV.Web/appsettings.Development.json` proveen el mismo placeholder pinned (≥32 bytes UTF-8, sufijo `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000`) para que `dotnet run` funcione sin setup adicional y ambos hosts acepten el mismo contrato JWT. Para pruebas locales con tokens propios, cada developer debe generar una clave aleatoria propia y persistirla en ambos proyectos con:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes ASCII>" --project src/SGV.Api
dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes ASCII>" --project src/SGV.Web
```

> **El placeholder dev NO es apto para producción.** Es público en el repo. Cualquier deploy que arranque con él es vulnerable a falsificación de tokens admin. La diferencia entre el placeholder y una clave real es detectable con `grep "DEV-PLACEHOLDER" config.json` en cualquier review.

**Producción / CI.** No se commitea ninguna clave. Las opciones soportadas son:

1. Variable de entorno `Jwt__SigningKey` (ASP.NET Core convierte `__` en `:` para `IConfiguration`).
2. Secret manager del proveedor (AWS Secrets Manager, GCP Secret Manager, Azure Key Vault, etc.) inyectado como env var al arranque del pod.

**Operación del secreto en GitHub Actions.** El job de tests exporta `Jwt__SigningKey` desde `secrets.JWT_SIGNING_KEY` (defense-in-depth: aunque el placeholder dev cubre el caso normal, este export garantiza que la suite no dependa de él). El valor es un secreto dedicado (≥32 bytes), independiente del placeholder dev, y se rota manualmente. Para crearlo o rotarlo:

```bash
openssl rand -base64 48
```

…y guardar el resultado en *Settings → Secrets and variables → Actions → JWT_SIGNING_KEY* del repositorio, scope `Environment: production` si aplica.

## ClockSkew de validación JWT (30 segundos)

`JwtTokenValidationParameters.Create` aplica `ClockSkew = TimeSpan.FromSeconds(30)` para tolerar drift de reloj entre hosts (API + Web + balanceadores + contenedores) sin admitir uso post-expiración de tokens. La constante vive en `src/SGV.Contracts/Seguridad/JwtTokenValidationParameters.cs` como `TokenValidationClockSkew` y se reutiliza por `SGV.Api` (middleware `JwtBearer`) y `SGV.Web` (`AuthSessionFactory` antes de aceptar claims en la cookie).

**Por qué 30 segundos y no `TimeSpan.Zero`.** El default .NET (`5 minutos`) era demasiado laxo: admitía tokens emitidos 5 minutos después de su `exp` declarada, ventana suficiente para reproducir un bearer "robado" en un ataque de replay. El valor `Zero` original generaba 401 espurios bajo drift >1s entre hosts (típico en contenedores sin NTP estricto) y producía tickets de soporte falsos. **30 segundos** absorbe drift de NTP realista y mantiene el tiempo de exposición post-`exp` en el orden de un heartbeat de monitor.

**Tests de regresión.** `tests/SGV.Tests/Seguridad/JwtRealAuthTests.TokenExpirado_DentroDelClockSkewDefault_Rechazado_401` firma un JWT con `expires = UtcNow - 1min` y verifica 401. Bajo skew 30s, un token con 60s de expiración sigue siendo rechazado (60s > 30s). Si en el futuro se sube la tolerancia, hay que actualizar este test al mismo tiempo.

**Operación.** Si en producción se observa una racha de 401 inmediatamente después del login, lo primero a verificar es la sincronía NTP entre los hosts (no la clave JWT). El skew es local a cada host; cada host debe tener `chronyc tracking` (o equivalente) reportando offset <1s contra la fuente de tiempo autoritativa.

## Inmutabilidad de `Codigo` en `UnidadOrganizativa`

`UnidadOrganizativa.Codigo` es la identidad lógica de la unidad. Una vez creada, **no puede cambiar**. El contrato se sostiene en tres capas, cada una con un mecanismo distinto pero convergente:

1. **Dominio** — `UnidadOrganizativa` es `sealed record class : EntidadAuditable` con propiedades `init`. `Codigo` se asigna únicamente en el constructor primario. Toda mutación posterior (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) devuelve una nueva instancia vía `with` y **nunca** expone `Codigo` como parámetro. El método legacy `CambiarDatos(codigo, ...)` está eliminado. La asimetría con `Puesto` (que mantiene `sealed class` con `private set`) es deliberada: no se quiere acoplar `Puesto` a esta restricción.

2. **Contrato HTTP** — `SGV.Contracts.Organizacion.Comandos.ActualizarUnidadOrganizativaRequest` no tiene `Codigo`. El binding de System.Text.Json descarta silenciosamente cualquier `codigo` extra en el body de `PUT /api/v1/unidades-organizativas/{id}`. El campo queda **fuera de contrato**: no se persiste, no se valida, no genera error. Esta propiedad aplica también a clientes maliciosos que envíen `{"codigo":"HACKED", ...}` — el servidor devuelve la unidad con su `Codigo` original intacto. La capa web refuerza la regla ocultando el input en `Edit` (PR3).

3. **Persistencia** — `PersistenceToDomainMapper.ToDomain(UnidadOrganizativaEntity)` no usa `SetProperty` / `BindingFlags.NonPublic` para `IsActive`, `UnidadPadre` ni `TipoUnidadOrganizativa`. Esas propiedades se aplican con `with { ... }` sobre el record. La razón: `PropertyInfo.SetValue` (que es lo que envuelve el helper `SetProperty`) no respeta el modifier `IsExternalInit` en runtime, así que podría saltarse el `init`-only del record. El `with` del compilador sí lo respeta y mantiene el invariante end-to-end. La suite incluye un test estructural (`ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper`) que recorre el IL del método y falla si alguien re-introduce el helper.

**Reactivación** — `ReactivarAsync` es el único flujo que sigue validando conflicto por código activo. La validación se hace contra `unidad.Codigo` (el código persistido en el record cargado), **no** contra un valor enviado por el cliente, porque el cliente nunca envía código en update. El índice único computado `ActiveCodigoUnique` (`CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`) en `UnidadOrganizativaConfiguracion` sigue siendo la red de seguridad a nivel DB.

### Generalización post #124 — `Reconstitute(...)` en las 6 entidades principales

Tras el change **#124** (archivado en `openspec/changes/archive/2026-07-13-fix-124-persistence-mapper-reconstitute/`), la estrategia `Reconstitute(...)` dejó de ser una excepción para `UnidadOrganizativa` y se extendió como **patrón único** a las 6 entidades principales que el mapper reconstituye desde persistencia:

| Entidad | `Reconstitute(...)` | Migración adicional |
|---|---|---|
| `Cargo` | `src/SGV.Dominio/Organizacion/Cargo.cs` | — |
| `Habilidad` | `src/SGV.Dominio/Habilidades/Habilidad.cs` | — |
| `Puesto` | `src/SGV.Dominio/Organizacion/Puesto.cs` | reusa `CambiarPuestoSuperior` para invariante `Id != puestoSuperiorId` |
| `Persona` | `src/SGV.Dominio/Personas/Persona.cs` | acepta `telefono` / `tipoDocumento` / `numeroDocumento` explícitos |
| `Ocupacion` | `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | replica validación `FechaFin >= FechaInicio` del ctor primario |
| `UnidadOrganizativa` | `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | migrada de `init` + `with`-returning a `private set` + `void`-return mutators para paridad total |

**Consecuencias del cambio:**

1. **`UnidadOrganizativa` pierde la asimetría `init`-only / `with`-returning** que documentaban los puntos (1) y (3) anteriores. Sus propiedades (`Codigo`, `Nombre`, `TipoUnidadOrganizativaId`, `Descripcion`, `UnidadPadreId`, `VigenteDesde`, `VigenteHasta`) ahora son `private set` (no `init`). Sus mutadores (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) retornan `void` y mutan `this` (no devuelven nueva instancia vía `with`). El test `Codigo_EsInmutableTrasCreacion` se reformuló para chequear **"setter NO público"** (sigue garantizando que `Codigo` solo se asigna dentro de la entidad), no el modifier `IsExternalInit`.
2. **`PersistenceToDomainMapper.cs` ya no usa `PropertyInfo.SetValue` ni `SetProperty<T>`**. Los 12 call sites anteriores (`SetProperty(cargo, "IsActive", ...)` etc.) fueron reemplazados por invocación directa de cada factory `X.Reconstitute(...)`. El helper `SetProperty<T>` (`PersistenceToDomainMapper.cs:225-232` pre-#124) y la directiva `using System.Reflection;` están eliminados.
3. **Asimetría con `Cargo` desaparece**: el punto (1) original aclaraba que `Puesto` (no `UnidadOrganizativa`) mantiene `private set`. Tras #124, **las 6 entidades comparten el mismo shape** (`internal Reconstitute` + `private set` + `void`-return mutators). El equipo ya no necesita recordar la excepción de UO.

**Defensa contra reintroducción de reflexión:** la suite incluye **6 tests IL estructurales** (1 por entidad, replicando el patrón de `UnidadOrganizativaRepositoryTests.cs:984-1045`) que recorren el cuerpo IL de cada `ToDomain(TEntity)` y fallan si alguien re-introduce el helper `SetProperty<T>` o cualquier llamada a `PropertyInfo.SetValue`. El de `UnidadOrganizativa` ya existía pre-#124; los otros 5 (`Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion`) son nuevos.

**`InternalsVisibleTo`** — el factory `Reconstitute(...)` es `internal static`, por lo que `SGV.Dominio.csproj` declara:

- `<InternalsVisibleTo("SGV.Tests") />` — para que los tests IL y de comportamiento puedan invocar el factory directamente.
- `<InternalsVisibleTo("SGV.Infraestructura") />` — para que `PersistenceToDomainMapper` pueda invocar el factory. **`InternalsVisibleTo` no es transitivo** entre assemblies de Clean Architecture, así que Infraestructura necesita su propia visibilidad explícita.

> **Detalle completo** (firmas exactas, orden canónico de asignaciones, lista de consumers UO actualizados, evidencia de TDD): ver `openspec/changes/archive/2026-07-13-fix-124-persistence-mapper-reconstitute/archive-report.md` y `design.md §2`.

## Patrón catálogo vs listado — Unidades Organizativas

`SGV.Web` distingue dos contratos de consumo del API de unidades organizativas según el caso de uso del lado web. Mezclarlos produce los bugs clásicos de la issue #120: catálogos truncados, round-trips sin consumidor y round-trips con `pageSize` mágico.

### El catálogo (dropdown completo)

- **Cuándo se usa** — Sólo cuando el PageModel debe renderizar un `<select>` con **todas** las UO activas (típicamente, formularios de creación).
- **Cliente tipado** — `IUnidadOrganizativaApiClient.GetAllActivasAsync()` (sin parámetros de paginación hacia el PageModel). La implementación recorre internamente páginas hasta igualar `TotalCount`, así el caller no necesita saber de `pageSize`.
- **Endpoint backend preferido** — `GET /api/v1/unidades-organizativas` (sin paginar, retorna `IReadOnlyList<UnidadOrganizativaDto>`). Para catálogos pequeños sirve; para cientos de UO, evaluar autocomplete.
- **Único consumidor vigente** — `Puestos/Create` (PR 3A). Su PageModel requiere los tres catálogos (UO, Cargos, Puestos superiores) para poblar los selects visibles.

### El listado paginado (Index / reportes)

- **Cuándo se usa** — Para vistas de tabla con buscador, filtros, ordenamiento y paginación clásica (10/25/50 por página).
- **Cliente tipado** — `IUnidadOrganizativaApiClient.QueryAsync(UnidadOrganizativaListQuery)`.
- **Endpoint backend** — `GET /api/v1/unidades-organizativas/consulta?page=...&pageSize=...`.
- **Consumidores vigentes** — `UnidadesOrganizativas/Index` (vista principal), reportes.

### Por qué Puestos/Edit no carga catálogos

`_Form.cshtml` envuelve los selects de `UnidadOrganizativaId` y `CargoId` en `@if (!Model.IsEdit) { ... }` — los campos son **inmutables** en un Puesto existente y el form de edición no los renderiza. Por construcción, ningún control visual consume `UnidadOrganizativaOptions` ni `CargoOptions` en Edit:

- `IPuestoForm.UnidadOrganizativaOptions` y `IPuestoForm.CargoOptions` permanecen inicializados como `[]` (lista vacía) porque `IPuestoForm` los exige y Edit no tiene nada que poblar.
- `EditModel` (PR 3B) recibe **únicamente** `IPuestosApiClient` + `ILogger<EditModel>` por constructor — el resto de los clientes se eliminaron en el change `2026-07-13-fix-120-uo-catalog-no-truncation` (issue #120). La firma del constructor es la primera línea de defensa contra reintroducir el dead code.
- `PuestoSuperiorOptions` **sí** se carga: ese dropdown SÍ se renderiza en Edit (es el único campo de "selección" editable de un Puesto).

### Regla operativa para próximos cambios

> **Si querés mostrar un catálogo en Edit, primero modificá `_Form.cshtml` para renderizar el select correspondiente; después habilitá la carga en el PageModel. Nunca cargues un catálogo "por las dudas" — la suite `PuestoEditLoadCatalogsTests` asserta explícitamente que `QueryCalls` y `GetAllCalls` quedan en cero.**

## Autorización del API

La API adopta una postura **default-deny** desde el change `2026-07-09-agregar-autorizacion-api-restantes` (issue #96). El patrón vigente replica los precedentes de `CargosController` (archive `2026-07-01-2026-07-01-cargos-crear-autorizacion-admin`) y `PuestosController` (issue #90).

### Reglas

1. **Fallback policy global en `Program.cs`** — `AddAuthorization(opts => opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`. Cualquier endpoint sin `[Authorize]` explícito falla cerrado con `401 Unauthorized`. Es la red de seguridad para controllers futuros: si se suma un controller nuevo sin `[Authorize]`, ya no queda público por default.
2. **Decoración explícita por controller** — Los controllers que requieren autenticación usan `[Authorize]` a nivel clase. Los controllers con mutaciones (`POST`, `PUT`, `PATCH`, `DELETE`) sobre-ponen `[Authorize(Roles = RolesSgv.Administrador)]` por acción, usando la constante `RolesSgv.Administrador` (sin literales de string repetidos). Las lecturas autenticadas (`GET`) heredan `[Authorize]` de la clase y permiten cualquier rol válido.
3. **Única excepción anónima: `AuthController.Login`** — El handler `Login` (`POST /api/v1/auth/login`) lleva `[AllowAnonymous]` explícito para sobrevivir la fallback policy global. Es la única ruta del API accesible sin credenciales; cualquier otro endpoint sin token devuelve `401`.

### Catálogos read-only autenticados

`NivelesCargoController` (`GET /api/v1/niveles-cargo*`) y `TipoUnidadesOrganizativasController` (`GET /api/v1/tipos-unidad-organizativa*`) pasan de anónimos a autenticados. Esto rompe el contrato histórico de la spec `sgv-readonly-api/spec.md`, que ahora queda reescrita para reflejar esta postura. Los consumidores externos que leían catálogos sin token deben autenticarse o recibir `401`.

### Precedentes y outliers

- **Controllers ya endurecidos** (no tocados por este change): `CargosController`, `PuestosController`, `UsuariosController`, `SkillsController`. Su `[Authorize]` sigue vigente.
- **No se introducen policies nominales nuevas**: el patrón `RolesSgv.Administrador` literal se mantiene para evitar indirección. Si en el futuro se requieren policies compuestas, se decidirá en un change separado.
- **Ventana de exposición por JWT**: el sistema valida firma, issuer, audience y lifetime del JWT pero NO reconsulta roles contra la DB por request. Un usuario cuyo rol cambia de `Administrador` a `GestorVacantes` conserva permisos de mutación hasta que su JWT expire. Esta ventana es inherente a JWT y no se aborda en este change.
- **Sub-recursos**: la decoración `[Authorize]` a nivel clase se hereda a sub-recursos anidados (e.g. `PUT /api/v1/personas/{id}/skills/{skillId}`). El sub-recurso `PersonasController.UpsertSkill`/`DeleteSkill` queda protegido automáticamente; no requiere override adicional porque la mutación ya exige `RolesSgv.Administrador` por la convención adoptada.

## Hardening defense-in-depth en SGV.Web

La shell web replica una defensa en profundidad coherente con el backend para los entry points administrativos de Organización.

### Reglas

1. **Lecturas autenticadas, mutaciones admin-only** — Los listados y detalles de `Cargos` y `Puestos` siguen accesibles para cualquier usuario autenticado, pero las operaciones de mutación (`crear`, `editar`, `eliminar`, `reactivar` y navegación a gestión de habilidades de cargo) se consideran admin-only también en la UI.
2. **UI gating explícito** — `Index.cshtml` de `Cargos` y `Puestos` MUST ocultar CTAs admin-only para usuarios autenticados sin rol `Administrador`. Esto evita affordances engañosas aunque el backend ya falle cerrado con `403`.
3. **GET restringidos con UX consistente** — `Create` y `Edit` de `Cargos` y `Puestos` redirigen a `/error/403` cuando el usuario está autenticado pero no tiene rol `Administrador`.
4. **POST restringidos con `Forbid()`** — Los handlers POST admin-only en Razor Pages devuelven `Forbid()` para no-admin. Con cookie auth, el navegador aterriza en el flujo estándar de access denied del shell.

## Hardening runtime: cookie y CORS por ambiente

`SGV.Api` y `SGV.Web` aplican una matriz de seguridad que depende del ambiente (`ASPNETCORE_ENVIRONMENT`). La matriz se valida en arranque mediante `ValidateOnStart` y fail-loud, de modo que un deploy mal configurado se surface antes de aceptar tráfico.

### Matriz ambiente ↔ seguridad

| Atributo                                | Development              | Distinto de Development |
|-----------------------------------------|--------------------------|-------------------------|
| `SGV.Web` cookie `HttpOnly`             | `true`                   | `true`                  |
| `SGV.Web` cookie `SameSite`             | `Lax`                    | `Lax`                   |
| `SGV.Web` cookie `SecurePolicy`         | `SameAsRequest`          | `Always`                |
| `SGV.Web` HSTS (`app.UseHsts()`)        | no se activa             | 30 días (default)       |
| `SGV.Web` HTTPS redirection             | `app.UseHttpsRedirection()` activo siempre | `app.UseHttpsRedirection()` activo siempre |
| `SGV.Api` CORS `AllowedOrigins`         | opcional (fallback dev)  | **obligatorio**, fail-loud si ausente o vacío |
| `SGV.Api` CORS en Production            | n/a                      | `WithOrigins(<lista>).AllowCredentials()` |
| `SGV.Api` CORS en Development (sin origins) | `SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()` (sin credenciales) | n/a |
| Combinación prohibida en cualquier CORS | `AllowAnyOrigin` + `AllowCredentials` jamás juntos | igual |

### Configuración de `AllowedOrigins` en la API

`SGV.Api` lee la sección de configuración `AllowedOrigins` como arreglo de strings. En **Production / Staging**, una sección ausente o vacía lanza `InvalidOperationException` con un mensaje que orienta al operador. En **Development**, la ausencia es tolerable y cae al fallback documentado arriba.

**Variables de entorno** (convención ASP.NET Core: `__` reemplaza a `:`):

```bash
AllowedOrigins__0=https://app.example.com
AllowedOrigins__1=https://admin.example.com
```

> **Importante**: los origins se matchean literales, sin slash final. Si configurás `https://app.example.com/`, el middleware CORS rechaza los requests reales. La regla es: protocolo + host + puerto opcional, sin path.

**Archivo `appsettings.json`** (alternativa para deploys con config-as-code):

```json
{
  "AllowedOrigins": [
    "https://app.example.com",
    "https://admin.example.com"
  ]
}
```

El comportamiento fail-loud está protegido por `tests/SGV.Tests/Api/CorsAllowedOriginsValidationTests.cs` (4 tests `[Fact]`):
- Production sin origins → `InvalidOperationException` con mensaje conteniendo `AllowedOrigins`.
- Production con origins → host arranca.
- Development sin origins → host arranca con fallback dev.
- Búsqueda estática: `src/SGV.Api/Program.cs` no contiene `AllowAnyOrigin` (la combinación prohibida es estructuralmente imposible).

### Cookie de autenticación de `SGV.Web`

El ternario en `src/SGV.Web/Program.cs` aplica la matriz según `builder.Environment.IsDevelopment()`. El chequeo vive dentro del bloque `AddCookie(...)` para que sea trivial de auditar y modificar.

```csharp
options.Cookie.HttpOnly = true;
options.Cookie.SameSite = SameSiteMode.Lax;
options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;
```

El comportamiento está protegido por `tests/SGV.Tests/Web/WebCookieAuthenticationOptionsTests.cs` (2 tests `[Fact]`): Production→`Always`, Development→`SameAsRequest`.

> **Por qué `SameAsRequest` en Development**: el equipo trabaja con `http://localhost:5266` sin TLS para iterar. `Always` bloquearía ese flujo de sign-in con un browser moderno. La cookie sigue llevando `HttpOnly` y `Lax`, así que el riesgo en dev queda acotado a robo local (no a exfiltración cross-origin).

### Reverse proxy y `UseForwardedHeaders` (pendiente de implementación)

Cuando `SGV.Api` o `SGV.Web` se sirven detrás de un reverse proxy (nginx, Traefik, ALB), el host ve la IP del proxy, no la del cliente. Sin `UseForwardedHeaders`, las redirecciones HTTPS generan URLs con `Host: proxy`, lo que rompe links absolutos y filtraciones de información.

> **Este change documenta pero NO implementa `UseForwardedHeaders`.** El snippet queda como referencia para el próximo SDD que aborde headers de proxy.

```csharp
// src/SGV.Api/Program.cs (futuro) o src/SGV.Web/Program.cs
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// Restringir a proxies conocidos. NUNCA usar KnownNetworks vacías en producción.
forwardedHeadersOptions.KnownProxies.Clear();
forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("10.0.0.1"));     // nginx primario
forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("10.0.0.2"));     // nginx secundario
// Para subredes internas (k8s pods):
// forwardedHeadersOptions.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.244.0.0"), 16));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeadersOptions.Default.ForwardedHeaders;
});

var app = builder.Build();
app.UseForwardedHeaders(forwardedHeadersOptions);
```

Reglas operativas:
- **Listar todos los proxies explícitamente**. No aceptar `X-Forwarded-*` de cualquier origen.
- **No combinar `X-Forwarded-For` con `X-Forwarded-Proto` ciegamente**: si solo te importa el protocolo (para HTTPS redirection), limitá a `XForwardedProto`.
- En **desarrollo sin proxy real**, NO registrar este middleware: aceptar headers forwarded de cualquier origen es un vector de spoofing.

### HSTS en `SGV.Web`

`app.UseHsts()` ya está activo en `src/SGV.Web/Program.cs` para cualquier ambiente distinto de `Development`. El default de 30 días es suficiente para nuestros deployments internos; si en el futuro se sube a `max-age=31536000`, ese cambio requiere un SDD separado.

## Política de paralelismo en la suite de tests

La suite `SGV.Tests` usa `xunit.runner.json` con `parallelizeTestCollections: true` y `maxParallelThreads: 4` para limitar la competencia entre colecciones de tests que comparten instancias de `WebApplicationFactory`.

### Arquitectura de aislamiento

Cada test usa `WebIntegrationFixture` como `ICollectionFixture`. El fixture administra `WebClientLease` por test, y `TestSentinel` provee contadores atómicos que reemplazaron el estado estático compartido de `AuthSessionFactory`.

La clave de la determinismo está en que `WebIntegrationFixture` **no usa estado estático**: cada host `SgvWebApplicationFactory` arranca con su propia clave JWT, su propio `AuthSessionFactory` Singleton (via DI), y su propia cookie de autenticación. No hay caché cross-test que se contamine.

### Watchers de archivos en hosts de test

El assembly de tests fija `DOTNET_USE_POLLING_FILE_WATCHER=1` mediante un module initializer antes de construir cualquier `WebApplicationFactory`. En macOS, la acumulación de hosts de la suite completa saturaba `FSEventStream`: los primeros síntomas eran 43 fallos intermitentes en UO/Puesto, seguidos por timeouts de cinco minutos al construir hosts Cargo y, finalmente, `Stack overflow` dentro de `FileSystemWatcher`. Usar polling elimina esa dependencia del watcher nativo y mantiene intacta la política de cuatro colecciones paralelas.

El tradeoff es un consumo de CPU levemente mayor mientras corre el proceso de tests y una detección de cambios de archivos menos inmediata. Es aceptable porque los hosts de integración no dependen de hot reload, el ajuste queda confinado a `SGV.Tests` y no modifica el comportamiento de API/Web en desarrollo o producción.

### Limitante de paralelismo

El límite `maxParallelThreads: 4` protege dos cosas:

1. **Saturación de hosts**: cada `WebApplicationFactory` arranca un Kestrel real. Con 4 hilos concurrentes × ~1 host por test, el límite evita que el scheduler de xUnit lance decenas de hosts simultáneos que agoten recursos del sistema o disparen `MSB4166`.

2. **Sentinel cross-collection**: `TestSentinel.AliveCount` es atómico pero compartido entre colecciones. Aunque la suite completa es determinista (3 corridas consecutivas idénticas), `maxParallelThreads: 4` reduce la ventana de preemption que puede hacer que un test individual vea un `AliveCount` distinto al esperado.

### Gate de 3 corridas

Todo cambio que toque `tests/SGV.Tests/` debe validarse con **3 corridas consecutivas de `dotnet test SGV.slnx --no-build`** en la misma máquina y commit que reporten:

- Mismo número total de tests pasados y fallados en las 3 corridas.
- Sin `MSB4166` (MSBuild node reuse crash).
- Cada corrida bajo `--no-build` para eliminar la variación de compilación.
- < 15 minutos por corrida.

Si las 3 corridas no son idénticas, el cambio reintroduce no-determinismo y no debe mergearse sin revisión y corrección.

> **Nota (issue #121, PR size:exception)**: el presente change estableció el límite `maxParallelThreads: 4` por experimentación con la suite completa de 1773 tests (3 corridas consecutivas ~42 min c/u). Equipos que reduzcan la suite deberían re-evaluar este número. Ver `openspec/changes/2026-07-11-hacer-suite-tests-determinista/verify-report.md`.

## Anti-patrón: conteos absolutos sobre tablas compartidas de `sgv_test` (issues #260 y #313)

> Issues: #260 (cerrado), #313 (resuelto). Patrón vigente desde `develop`
> post-merge del fix #313.

### Regla

**Prohibido** escribir tests `[MySqlFact]` que asserten `Assert.Equal(N, result.Count)` o `Assert.Equal(N, queryResult.TotalCount)` cuando `N > 1` y el resultado proviene de una tabla compartida de `sgv_test` (`Ocupaciones`, `Puestos`, `UnidadesOrganizativas`, `Personas`, `Cargos`, `Habilidades`, `Vacantes`, `AspNetUsers`, etc.) **sin** un filtro que aísle la consulta a las filas sembradas por el propio test.

**Permitido** (en orden de preferencia):

1. **Filtro de scope** vía parámetros del query (PersonaId, PuestoId, CargoId, búsqueda con sufijo único) — patrón `BloquearDesbloquearEliminarGatewayTests.Marker`, `PuestoRepositoryQueryAsyncTests.sufijo`, `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_FiltroPorPersonaId`. La aserción absoluta es correcta porque el resultado está acotado a las filas sembradas.
2. **Predicado relativo sobre IDs únicos** sembrados por el test — patrón adoptado por los dos fixes de #313. Útil cuando el método no acepta filtros de scope (p.ej. `ListAllIncludingHistoryAsync`):

   ```csharp
   var ownIds = new[] { active.Id, finalized.Id, deleted.Id };
   Assert.Contains(result, o => o.Id == active.Id);
   Assert.Equal(3, result.Count(o => ownIds.Contains(o.Id)));
   ```

3. **Predicado sobre `Items`** (acotado por `PageSize`) en lugar de `TotalCount` cuando el método devuelve `(Items, TotalCount)` y el `Items.Count` es ≤ `PageSize` (típicamente 20). El predicado sigue siendo relativo a IDs únicos sembrados.
4. **`Assert.InRange`** o `Assert.True(count >= N)` — válido solo cuando el contrato del método es "al menos N filas" (poco habitual).

### Rationale

`sgv_test` es una base de datos compartida: `MySqlTestDatabaseBootstrap` aplica migraciones una vez por sesión pero **no** trunca las tablas transaccionales entre tests (ver `SgvTestDatabaseCleaner.CleanAsync` — solo se invoca desde setups dedicados, no por defecto). El paralelismo entre clases serializadas por `[Collection(MySqlIntegrationCollection.Name)]` evita carreras de FK/Identity pero **no** evita que una consulta sin filtro lea filas de tests anteriores. Resultado: `Assert.Equal(3, result.Count)` falla con flakiness reproducible cuando otra clase insertó filas en `Ocupaciones` durante una corrida anterior o en el mismo slot.

`Assert.Equal(1, …)` con filtro por `Guid.NewGuid()` es seguro (las IDs son únicas). `Assert.Equal(2, …)` sin filtro sobre una tabla que recibe inserciones de otros tests es **frágil por diseño**. `Assert.Equal(N, result.Count)` sobre métodos sin filtro (p.ej. `ListAllIncludingHistoryAsync`) es **siempre** frágil.

### Audit del resto del suite (resuelto por #313)

Búsqueda por `Assert.Equal(N`, `Assert.Single`, `Assert.Empty` en `tests/SGV.Tests/Persistencia/` (snapshot al momento del fix). Resultado:

| Test | Patrón | Estado |
|---|---|---|
| `OcupacionRepositoryTests.ListAllIncludingHistoryAsync_ReturnsAllRows` | `Assert.Equal(3, result.Count)` sobre tabla sin scope | **Flaky — resuelto** (#313) |
| `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminadasYFinalizadas` | `Assert.Equal(2, result.TotalCount)` sin filtro por PersonaId | **Flaky — resuelto** (#313) |
| `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_FiltroPorPersonaId` | `Assert.Single` con filtro PersonaId | OK |
| `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_FiltroPorPuestoId` | `Assert.Single` con filtro PuestoId | OK |
| `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_FiltrosCombinadosSinCoincidencia` | PersonaId + PuestoId `Guid.NewGuid()` | OK |
| `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_Paginacion_TotalCountReflejaFiltros` | `Assert.Equal(3, result.TotalCount)` con filtro PersonaId | OK |
| `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SearchEscapaWildcardPorcentaje` | `Assert.Single` con `Search` por sufijo único | OK |
| `PuestoRepositoryQueryAsyncTests.*` | Todos usan `sufijo` como `Search` → scope automático | OK |
| `UnidadOrganizativaRepositoryQueryAsyncTests.*` | Todos usan `sufijo` como `Search` → scope automático | OK |
| `BloquearDesbloquearEliminarGatewayTests.*` | `fixture.Marker` como `Search` → scope automático | OK |
| `CargoRepositoryTests.QueryAsync_MySql_*` con `Assert.Equal(5, totalCount)`, `Assert.Equal(2, page1.Count)` etc. | Filtran por `Codigo.Contains(sufijo)` | OK |
| `VacanteRepositoryQueryTests.*` | Filtran por `Codigo.Contains(sufijo)` | OK |

Patrón vigente de scope para tests de repositorio: usar un `sufijo` (GUID truncado) como `Search` en el query, o pasar `PersonaId`/`PuestoId`/`CargoId` cuando la entidad lo permita. Tests que cubran `ListAllIncludingHistoryAsync` o equivalentes sin filtro deben usar **siempre** predicados relativos sobre IDs únicos.

### Cómo verificar

```bash
# Repetir N veces para confirmar determinismo contra sgv_test con residuos:
for i in 1 2 3; do
  dotnet test SGV.slnx --no-build \
    --filter "FullyQualifiedName~OcupacionRepositoryTests.ListAllIncludingHistoryAsync_ReturnsAllRows|FullyQualifiedName~OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas"
done

# Suite del módulo completo:
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Ocupaciones"
```

## Issue #125 — Taxonomía de errores para `CommandResult` y clientes HTTP de Web

> Change: `2026-07-13-taxonomia-errores-commandresult` (slice 1 de 4). Artefactos SDD completos en `openspec/changes/2026-07-13-taxonomia-errores-commandresult/`.

### Rationale

`SGV.Contracts` convive con cinco taxonomías paralelas para fallos HTTP: `HabilidadErrorType.Infrastructure`, `CargoCommandResult`/`PuestoCommandResult`/`UnidadOrganizativaCommandResult` (que colapsan 401/403/5xx en `Validation` con magic code `Unexpected`), `CargoSkillCommandResult` (la aproximación más cercana al objetivo pero sin repositorio compartido), `MapSkillError` privado de `CargoApiClient`, y los cinco `*DeleteResult` que exponen `StatusCode/Code/Message` sin categoría semántica. El resultado: cada cliente HTTP repite su propia matriz de clasificación, los `PageModel` ramifican con `if (ex is X)` divergentes, y el mismo status produce un mensaje distinto para el usuario según el dominio.

### Decisión

Una sola taxonomía `ErrorCategoria` definida como `enum` append-only en `src/SGV.Contracts/Comun/ErrorCategoria.cs`. Mantiene `SGV.Contracts` como leaf (verificado: `SGV.Contracts.csproj` solo referencia `Microsoft.IdentityModel.Tokens 8.14.0`). Cada uno de los seis `*Error` records (`HabilidadError`, `CargoError`, `PuestoError`, `UnidadOrganizativaError`, `CargoSkillError`, `UsuarioError`) y los cinco `*DeleteResult` ganan `Categoria: ErrorCategoria`. Los enums `*ErrorType` vigentes se marcan `[Obsolete]` durante el ciclo del change y se eliminan al archivar.

### Reglas invariantes

- **Append-only**: las variantes y sus ordinales son contrato público estable. Agregar nuevas variantes SOLO al final; NO reordenar ni reasignar ordinales.
- **Mapeo nombre-a-nombre**: la conversión entre los enums `*ErrorType` y `ErrorCategoria` se hace vía `ErrorCategoriaMappers.ToCategoria(...)` y `ToTipo<Domain>(...)`. Prohibido el cast `(ErrorCategoria)(int)type` — los ordinales NO coinciden (p.ej. `CargoSkillErrorType.Validation = 1` mientras `ErrorCategoria.Validation = 2`).
- **Round-trip simétrico**: cada `ToCategoria`/`ToTipo<Domain>` es exhaustivo (sin `default:`). Categorías sin equivalente en el dominio origen lanzan `NotSupportedException` con mensaje claro.
- **`[Obsolete]` durante el ciclo**: los enums `HabilidadErrorType`, `CargoErrorType`, `PuestoErrorType`, `UnidadOrganizativaErrorType`, `CargoSkillErrorType`, `UsuarioErrorType` se marcan con `[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria. Will be removed in the archive of change 2026-07-13.")]`. Los call sites existentes siguen compilando porque el atributo se emite como warning por defecto.
- **Eliminación al archivar**: los enums `[Obsolete]` se borran durante la fase `sdd-archive` del change `2026-07-13-taxonomia-errores-commandresult`, NO en este PR ni en los slices 2-4.

### Compatibilidad

- **Source-breaking**: NO. El nuevo parámetro `Categoria` se agrega con default `ErrorCategoria.Unexpected` a los records `*Error` para preservar source-compat. Los enums `[Obsolete]` emiten warning, no error.
- **Wire-breaking**: NO. Los controllers no serializan los enums a `ProblemDetails`. La matriz de status HTTP se preserva.
- **DB-breaking**: NO. La taxonomía es interna a `SGV.Contracts` y `SGV.Web`.

### Archivos clave

- `src/SGV.Contracts/Comun/ErrorCategoria.cs` — enum común (7 variantes, ordinales 0..6).
- `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs` — mapeos nombre-a-nombre para los 6 enums vigentes.
- `src/SGV.Contracts/*/Comandos/*CommandResult.cs` — 6 `*Error` records con `Categoria` agregado.
- `src/SGV.Contracts/Organizacion/Comandos/CargoSkillDeleteResult.cs` — `Categoria` agregado.
- `src/SGV.Web/Integration/*/...ViewModel.cs` — 4 `*DeleteResult` records con `Categoria` agregado; `PuestoDeleteResult.StatusCode` pasa de `HttpStatusCode` non-nullable a `HttpStatusCode?` nullable.

### Follow-up documentado (fuera de #125; resuelto por #208 para Ocupaciones)

- `PersonaCommandResult`, `PersonaSkillCommandResult` (viven en `SGV.Aplicacion`): no se migran en este change. Sólo exponen `NotFound`/`Conflict`/`Validation` hoy; no impactan flujos administrativos. Issue de follow-up sugerido tras archive del #125.
- `OcupacionCommandResult` (vivía en `SGV.Aplicacion`): ✅ **Migrado a `ErrorCategoria` por el change `2026-07-28-web-ocupaciones-issue-208`** (PRs #212, #213, #214, #215). Ahora vive en `SGV.Contracts/Ocupaciones/Comandos/` con `Categoria: ErrorCategoria`. El enum legacy `OcupacionErrorType` queda `[Obsolete]` como compat hasta el archivado del change #125.
- Los `ApiResults.Map*Status` de `SGV.Api/Infrastructure/Results/ApiResults.cs` se centralizan en un `MapCategoria(ErrorCategoria)` exhaustivo en el Slice 4 (issue #125, PR #4).

## Inversión del flujo Cubrir (change `invertir-flujo-cubrir`)

> Change: `invertir-flujo-cubrir`. Artefactos SDD completos en
> `openspec/changes/invertir-flujo-cubrir/`. Chain strategy:
> `stacked-to-main` con 3 PRs encadenados (S1 backend + wire, S2
> frontend Create, S3 frontend Details) hacia `develop`. Esta entrada
> documenta D-1, D-3 y D-4 del `design.md` aplicables a S1.

### Contexto y problema

El change archivado `vacante-ocupacion-flow-alignment` (2026-08-07)
implementó N2 como "Cubrir una Vacante vía
`PATCH /api/v1/vacantes/{id}/estado` crea la Ocupación derivada",
exigiendo `PersonaId` en el body. El frontend de Edit de Vacante no
expone ese campo y el dropdown ya excluye Cubierta (issue #268); el
Administrador no podía cerrar el ciclo Crear Vacante → Cubrir desde
la UI. Se invirtió el flujo: ahora "Cubrir Vacante" es un botón en el
Details de la Vacante que abre el form de `Ocupaciones/Create` con
`?vacanteId={id}`, y la creación de la Ocupación + transición a
Cubierta se materializa en `OcupacionServicioComandos.CrearAsync`
cuando el request incluye `VacanteId`.

### D-1 — Inversión del flujo Cubrir

`OcupacionServicioComandos.CrearAsync` agrega una rama cuando
`request.VacanteId.HasValue`:

1. Carga la Vacante vía `IVacanteRepository.GetByIdForUpdateAsync`.
2. Si `null` → `404 VacanteNoEncontrada`.
3. Si `EstadoVacante.EsTerminal` → `400 VacanteNoAbierta` (cubre
   Cubierta y Cancelada).
4. Si `IOcupacionRepository.ExistsActiveByVacanteAsync` → `409 VacanteYaCubierta`.
5. Si `request.PuestoId` viene vacío, se resuelve desde
   `vacante.PuestoId`; si viene poblado y no coincide → `400 PuestoIdNoCoincideConVacante`.
6. Crea la `Ocupacion` con `VacanteId` y persiste vía el mismo
   `IUnitOfWork.SaveChangesAsync` que invoca
   `vacante.CambiarEstado(Cubierta, …, cerrar: true)` +
   `vacanteRepository.RegistrarCambioEstadoAsync`. EF agrupa ambas
   escrituras en una sola transacción; el catch vigente de
   `DbUpdateException` cubre el rollback.

`VacanteServicioComandos.CambiarEstadoAsync` rechaza cualquier destino
`EsCubierta` con `400 CubrirVacanteRequiereCrearOcupacion` + mensaje
"Use el botón 'Cubrir Vacante' en el detalle de la Vacante para
crear la Ocupación derivada.". El campo legacy `PersonaId` se ignora
silenciosamente. El bloque de creación de Ocupación derivado (líneas
que instanciaban `new Ocupacion(...)`) se eliminó por completo — la
responsabilidad Cubrir ya no vive en este servicio.

### D-3 — Hidratación defensiva de `VacanteDetailDto`

`VacanteDetailDto` extiende con `OcupacionDerivadaId?: Guid` y
`PersonaAsignadaNombre?: string` (nullables, default `null`). El
servicio de consulta `VacanteServicioConsulta.ObtenerPorIdAsync`
inyecta `IOcupacionRepository` y llama
`ObtenerVigentePorVacanteAsync` **solo cuando**
`vacante.EstadoVacante?.EsCubierta == true`. Vacantes no Cubiertas
evitan el round-trip. Estados inconsistentes (Cubierta sin Ocupación)
resultan en `null`/`null` sin lanzar — el contrato defensivo protege
el endpoint.

### D-4 — Renombre del código de error legacy

`VacanteErrorCodigo.CubrirVacanteRequiereCrearOcupacion` reemplaza a
`PersonaIdRequeridoParaCubrir`. El código viejo se conserva marcado
como `[Obsolete("Use CubrirVacanteRequiereCrearOcupacion. El flujo
Cubrir vive en OcupacionServicioComandos.CrearAsync con VacanteId;
este código ya no se devuelve en runtime.")]` para no romper
clientes cacheados. Los tests nuevos referencian exclusivamente el
nombre vigente; el código `PersonaIdRequeridoParaCubrir` ya no se
devuelve en runtime post-change.

`CambiarEstadoVacanteRequest.PersonaId` queda en el record como
deprecated (XML doc actualizado para T1.30). El servicio lo ignora
silenciosamente — ningún cliente integrado puede enviar un valor
válido que cambie la semántica.



### Patrón defensivo en `usuario-persona-buscador.js` (issue #224)

> Change: `fix-persona-card-empty-state-issue-224`. Artefactos SDD completos en `openspec/changes/fix-persona-card-empty-state-issue-224/`. Spec NEW: `usuario-persona-buscador-js` (USBJS-01..03).

El script `wwwroot/js/pages/usuario-persona-buscador.js` se apega al patrón "lookup defensivo + mutación abortable": si los elementos del contrato `data-*` que la partial `_PersonaCard.cshtml` puede omitir (caso 6: `editable + PersonaDto=null + sin FallbackDisplay`) no están presentes en el DOM, las mutaciones abortan con `console.warn` en lugar de tirar `TypeError`. La selección del usuario se preserva siempre en `hiddenInput.value` y `modal.dataset.currentPersonaId` (USBJS-02).

El lookup de `empty` se hace desde `display.parentElement` (no `display`) porque la partial emite el empty state como sibling del contenedor `display` (USBJS-01).

**Decisión de no agregar Vitest/Jest**: el equipo excluye infraestructura de testing JS por scope (el fix es trivialmente detectable por inspección; los tests .NET del contrato markup son RED→GREEN verificables). Si en el futuro se introduce infra JS, será un change dedicado.

## Frontend CRUD de Personas

> Change: `2026-07-14-frontend-crud-personas`. Artefactos SDD completos en `openspec/changes/2026-07-14-frontend-crud-personas/`. Chain strategy: `feature-branch-chain` con 4 PRs encadenados contra la tracker `feat/2026-07-14-frontend-crud-personas-tracker`.

### Decisiones de diseño

#### Ruta `/personas` (sibling de `/organizacion/cargos`)

El módulo Personas NO cuelga de `/organizacion/`. La spec original proponía `/organizacion/personas`; en sesión interactiva se confirmó `/personas` como ruta directa. Razones:

1. **Personas no es un subdominio de Organización** — Cargo, Puesto y UnidadOrganizativa modelan la estructura organizacional. Persona es una entidad de dominio independiente (con sus propios skills, datos de contacto, documento) que cualquier capacidad organizacional referencia (e.g. una UnidadOrganizativa tiene responsable → Persona). Anidarla bajo `/organizacion/` confundiría el modelo mental.
2. **Coherencia con la API** — el backend expone `PersonasController` en `/api/v1/personas` (sibling de `/api/v1/cargos`, no anidado). El espejo Web debe respetar el mismo árbol.
3. **URLs estables para integraciones externas futuras** — `GET /personas/{id}` será consumido por terceros sin necesidad de conocer el detalle organizativo.

Consecuencia práctica: el directorio `Pages/Personas/` (no `Pages/Organizacion/Personas/`); la nav apunta a `/personas` con icono `ti ti-user`.

#### Wire-types movidos de `Aplicacion.Personas` a `Contracts.Personas`

Los records `PersonaDto`, `PersonaCommandResult`, `PersonaError`, `PersonaErrorType`, `CrearPersonaRequest` y `ActualizarPersonaRequest` vivían en `SGV.Aplicacion.Personas.*`. Migrar a `SGV.Contracts.Personas.*` siguiendo el precedente de Cargos (archive `2026-07-09-frontend-crud-cargos-pages`):

- **`SGV.Contracts` sigue siendo leaf** — el grafo de proyectos no cambia. `SGV.Web` ya no depende de `SGV.Aplicacion.Personas` (verificado por el grep `grep -r "SGV.Aplicacion.Personas" src/SGV.Web/` que retorna cero hits).
- **JSON shape idéntico** — los DTOs son `sealed record` con los mismos nombres y orden de propiedades; el wire format no cambia. Los tests existentes de `PersonasController` siguen pasando sin tocar.
- **Movimiento, no duplicación** — los archivos originales en `Aplicacion` se borran (no quedan copias huérfanas). Los call sites internos (`PersonaServicioComandos`, `PersonaServicioConsulta`, `PersonasController`, `PersonaSkill*`) actualizan `using SGV.Contracts.Personas.*` (revisión: 4 archivos tocados, ~12 líneas de `using` modificadas, 0 cambios de lógica).
- **`PersonaSkill*` queda en Aplicacion** — los records `PersonaSkillDto`, `PersonaSkillCommandResult`, etc. viven en `SGV.Aplicacion.Personas.Habilidades` y NO se migran en este change. Se mueven en el frontend de habilidades de persona (cambio futuro, fuera de alcance).

#### Asunción del typeahead: dataset activo <500 personas

`Pages/Personas/Shared/_PersonaTypeahead.cshtml` consume `GET /api/v1/personas` completo y filtra client-side ≥2 chars con debounce de 250 ms. La asunción operativa es que **el dataset activo típico no supera 500 personas**. Por debajo de ese umbral:

- Payload de ~100 KB para un GET sin paginación, aceptable para una carga única en `OnGetAsync` del host page.
- Filtro client-side evita round-trips HTTP por keystroke.
- Debounce evita render-cost con cada pulsación.

**Si el dataset supera las ~500 personas activas**, el primer GET pesa >100 KB y deforma la experiencia (latencia de carga, memoria retenida en el navegador). Follow-up documentado: agregar `GET /api/v1/personas/buscar?q={term}` que devuelve las N mejores coincidencias server-side, manteniendo el contrato del partial sin cambios (el JS ya espera un array de `PersonaDto`). Issue de seguimiento sugerido para cuando el `COUNT(*)` de `Personas` activas supere el umbral.

#### `MapCategoriaToLegacyType` endémico en 5 clientes (warning CS8524)

Cada `*ApiClient` de Web (`CargoApiClient`, `PuestoApiClient`, `UnidadOrganizativaApiClient`, `PersonaApiClient` y `HabilidadApiClient`) tiene un método privado `MapCategoriaToLegacyType(ErrorCategoria)` que colapsa la taxonomía común al enum histórico `*ErrorType` para preservar source-compat con call sites vigentes. **El warning CS8524 aparece en los 5 archivos** porque `ErrorCategoria` es append-only (regla del change #125): cuando se agrega una variante nueva, los 5 switches deben actualizarse simultáneamente o el compilador avisa.

El warning **es endémico y aceptado** mientras los enums `[Obsolete]` no se eliminen (archivado del change #125). Endurecerlo exigiría:

1. Invertir el flujo: que el enum `[Obsolete]` se elimine primero, y luego eliminar `MapCategoriaToLegacyType` de cada cliente.
2. O bien introducir un shared slice en `SGV.Web.Integration.Common` que centralice la conversión, eliminando la duplicación pero NO el problema del exhaustivo (la advertencia seguiría apareciendo en el lugar centralizado).

Por ahora, el equipo acepta el warning y lo trata como checklist en code review para cuando se sume una nueva variante de `ErrorCategoria`. Será resuelto naturalmente al archivar el change #125 (cuando los enums legacy se borren).

### Compatibilidad y rollback

- **Source-breaking**: NO. `SGV.Aplicacion.Personas.PersonaDto` etc. ya no existen; cualquier consumer interno que los importaba actualiza el `using`. No quedan referencias externas al repositorio SGV.
- **Wire-breaking**: NO. El JSON shape del API no cambia (mismo nombre de propiedades, mismo orden).
- **DB-breaking**: NO. Cero migraciones.
- **Rollback**: borrar `Pages/Personas/`, `Integration/Personas/`, revertir `Program.cs`, `_Sidenav.cshtml` y `using SGV.Contracts.Personas`. Cero impacto en API runtime, BD o datos.

### Archivos clave

- `src/SGV.Contracts/Personas/Consultas/Dtos/{PersonaDto,PersonaListQuery,PersonaListadoDto,PersonaSegmentoListado}.cs` — wire-types de consulta (4 archivos).
- `src/SGV.Contracts/Personas/Comandos/{CrearPersonaRequest,ActualizarPersonaRequest,PersonaErrorType,PersonaCommandResult,PersonaDeleteResult,PersonaError}.cs` — wire-types de comandos (6 archivos).
- `src/SGV.Web/Integration/Personas/{IPersonaApiClient,PersonaApiClient,PersonaInputModel,PersonaListItemViewModel,PersonaListQueryViewModel,PersonaFormHelpers,PersonaPostResultMapper,IPersonaForm,PersonaTypeaheadViewModel}.cs` — cliente HTTP + helpers (9 archivos).
- `src/SGV.Web/Pages/Personas/{Index,Create,Edit,Details}.{cshtml,cshtml.cs}` + `_Form.cshtml` + `Shared/_PersonaTypeahead.cshtml` — Razor Pages (9 archivos).
- `src/SGV.Web/Program.cs` — `AddHttpClient<IPersonaApiClient, PersonaApiClient>` con `ApiBearerTokenHandler` (10s timeout, paralelo a Cargo/Habilidad/Puesto).
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` — ítem colapsable "Personas" con icono `ti ti-user`.
- `tests/SGV.Tests/Web/Persona/{FakePersonaApiClient,PersonaWebTestFixture,FakePersonaApiClientTests,IPersonaApiClientContractTests,PersonaApiClientBasicTests,IndexPageTests,CreatePageTests,EditPageTests,DetailsPageTests,TypeaheadTests,PersonaWebSeamTests}.cs` — 11 archivos, ~80 tests web.

### PR encadenados (feature-branch-chain)

| PR | Squash | Scope | Tests netos |
|----|--------|-------|-------------|
| 1 | `5158cec6` (#143) | Backend paginado `/consulta` + wire-types | ~200 backend |
| 2 | `180b8701` (#144) | Integration client + DI + nav | 0 |
| 3 | `82a5455` (#145) | Razor Pages + typeahead | 0 |
| 4 | (este PR) | Tests web + docs | ~80 web |

Tracker PR (no-merge) mantiene el squash de los 4 PRs encadenados hasta que se decida el merge final. La cadena vive bajo `feat/2026-07-14-frontend-crud-personas-tracker` con 4 work-branches hijos. Cada PR child mantiene su diff enfocado en su work-unit y nunca apunta directo a `main` (regla del chained-pr skill).

### Follow-up documentado (fuera de este change)

- **Frontend de habilidades de persona** (`PersonaSkill*` actualmente vive en `SGV.Aplicacion.Personas.Habilidades`): cuando se sume al scope de Personas, los records deben moverse a `SGV.Contracts.Personas.Habilidades` siguiendo el mismo precedente.
- **`GET /api/v1/personas/buscar?q=`** — endpoint server-side de búsqueda rápida para el typeahead, requerido cuando el dataset activo supere las ~500 personas.
- **Gate de Edit en Details**: el page model actual muestra el botón Editar a cualquier autenticado y delega el gate al handler GET de Edit. Considerar gating visual en Details para UX consistente con Index.

## Módulo Usuarios — soft-delete de Identity con columna generada STORED

> Change: `Implementa módulo usuarios` (PR1 backend cerrado; PR2/3/4 pendientes). Artefactos SDD en `openspec/changes/Implementa módulo usuarios/`. Chain strategy: `feature-branch-chain` con 4 PRs encadenados contra tracker `feat/2026-07-15-implementa-modulo-usuarios-tracker`.

### Contexto

El módulo Usuarios extiende el comportamiento de `AspNetUsers` con baja lógica (`IsDeleted`), replicando el patrón de `Personas` / `UnidadesOrganizativas` / `Habilidades` / `Cargos`: columna `IsDeleted TINYINT(1) NOT NULL DEFAULT 0` + columna generada STORED con índice único para convivencia con soft delete (precedente archivado: `2026-07-11-fix-active-puesto-id-unique-type`).

### Restricción MySQL 8 sobre columnas generadas STORED

MySQL 8 declara `ALGORITHM=INPLACE` incompatible con la creación de una columna generada STORED durante una operación `ALTER TABLE`. El RED inicial sobre base limpia devolvió:

```
ALGORITHM=INPLACE is not supported for this operation. Try ALGORITHM=COPY.
```

La columna STORED exige `ALGORITHM=COPY`, que bloquea lecturas y escrituras sobre `AspNetUsers` durante toda la copia — proporcional al tamaño de la tabla al momento del deploy.

### Decisión adoptada

El maintainer aceptó la **opción A — Aceptar `ALGORITHM=COPY`** en sesión interactiva tras el `sdd-apply` del PR1. La migración `AddSoftDeleteToAspNetUsers` se divide en dos operaciones para minimizar la ventana de bloqueo:

1. **Paso 1 (`INPLACE, LOCK=NONE`)** — `ALTER TABLE AspNetUsers ADD COLUMN IsDeleted TINYINT(1) NOT NULL DEFAULT 0`. Online, sin bloqueo.
2. **Paso 2 (`COPY`)** — `ALTER TABLE AspNetUsers ADD COLUMN ActiveUserNameUnique VARCHAR(256) GENERATED ALWAYS AS (CASE WHEN IsDeleted=0 THEN LOWER(UserName) ELSE NULL END) STORED COLLATE utf8mb4_0900_ai_ci, ADD UNIQUE INDEX IX_AspNetUsers_ActiveUserNameUnique`. Ventana de mantenimiento; tamaño proporcional al `COUNT(*)` de `AspNetUsers` al momento del deploy.

Las alternativas evaluadas y descartadas quedan registradas en `openspec/changes/Implementa módulo usuarios/apply-progress.md`:

- **B — Cambiar a `VIRTUAL`**: viable (`ALGORITHM=INPLACE` lo soporta), pero cambia el shape del DDL aprobado y exige revisión del design.
- **C — Rediseñar el patrón** (trigger, índice condicional): fuera del alcance del change.

### Plan operativo para producción

Antes de aplicar la migración a un ambiente productivo:

1. **Medir la ventana esperada**: `SELECT COUNT(*) FROM AspNetUsers` para estimar el tiempo de copia (regla práctica: ~10 s por cada 100 K filas en hardware medio).
2. **Programar ventana de mantenimiento** con aviso a usuarios (la app puede seguir sirviendo lecturas, pero Login/SignUp/Create quedan bloqueados durante la copia).
3. **Ejecutar la migración en dos pasos** (no atómicos): primero la columna `IsDeleted` (INPLACE), luego la columna STORED y el índice (COPY).
4. **Validar post-deploy**: `SELECT COUNT(*) FROM AspNetUsers WHERE IsDeleted = 1` (debe ser 0 al inicio), `SHOW INDEX FROM AspNetUsers WHERE Key_name = 'IX_AspNetUsers_ActiveUserNameUnique'` (debe existir).

### Limitaciones conocidas

- **Índice único de Identity**: `AspNetUsers` mantiene su índice único estándar sobre `NormalizedUserName`. Si bien la columna nueva `ActiveUserNameUnique` protege la regla pedida (un mismo `UserName` no puede existir dos veces entre usuarios activos), reasignar el mismo `UserName` a un usuario reactivado mientras otro eliminado conserva `NormalizedUserName` puede seguir chocando con Identity. No se alteró ese índice porque no figura en el DDL aprobado; queda como follow-up si la regla se endurece en un change futuro.
- **Auditoría explícita**: `SgvIdentityUser` no extiende `AuditableEntityBase`, por lo que `AuditoriaSaveChangesInterceptor` no captura mutaciones sobre `AspNetUsers`. La auditoría se hace manualmente vía `IAuditoriaServicio.RegistrarAsync` desde cada handler de mutación (`CrearUsuarioHandler`, `EditarUsuarioHandler`, `DesactivarUsuarioHandler`, `ReactivarUsuarioHandler`), incluyendo diffs de `UserName`, `Email` y roles.

### Archivos clave del PR1

- Migración: `src/SGV.Infraestructura/Persistencia/Migraciones/20260715145121_AddSoftDeleteToAspNetUsers.cs` (revertida en producción por `20260716120000_DropSoftDeleteFromAspNetUsers` — el path release-readiness opta por `LockoutEnd` futuro en lugar de `IsDeleted`; ver `docs/decisiones-implementacion.md` bloque D-7).
- Script SQL release-ready: `docs/migracion-inicial-sgv.sql` (MySQL 8) y `docs/migracion-inicial-sgv-mariadb.sql` (MariaDB). No existe script aditivo para esta migración individual — el script inicial cubre el estado actual del snapshot EF.
- Modelo Identity: `src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs` + `SgvIdentityUserConfiguracion.cs`.
- Gateway: `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` (consulta paginada/segmentada sin N+1, actualización atómica, baja, reactivación).
- Aplicación: `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs` (D-01..D-04), `IAuditoriaServicio.cs`.
- API: `src/SGV.Api/Controllers/UsuariosController.cs`, `src/SGV.Api/Seguridad/UsuarioActualHttpContext.cs`.
- Contratos: `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` (`UsuarioDto` con `Nombres`/`Apellidos` agregados al final; nuevos `ActualizarUsuarioRequest`, `UsuarioListQuery`, `UsuarioListadoDto`, `UsuarioSegmentoListado`).
- Tests: 77 focalizados usuarios + 26 API + 10 MySQL gateway/migración + 2211 totales.

### PR encadenados (feature-branch-chain)

| PR | Rama | Scope | Tests netos |
|----|------|-------|-------------|
| 1 | `feat/2026-07-15-implementa-modulo-usuarios-pr1-backend` | Backend: migración + `/consulta` paginado + endpoints + auditoría | ~38 backend |
| 2 | `feat/2026-07-15-implementa-modulo-usuarios-pr2-integration` (pendiente) | Integration client + DI + sidenav | ~14 |
| 3 | `feat/2026-07-15-implementa-modulo-usuarios-pr3-paginas-listado` (pendiente) | Razor Pages Index/Details/Delete/Reactivar | ~18 |
| 4 | `feat/2026-07-15-implementa-modulo-usuarios-pr4-paginas-form` (pendiente) | Razor Pages Create/Edit + `_Form.cshtml` | ~16 |

Tracker PR (no-merge) mantiene la integración final en `feat/2026-07-15-implementa-modulo-usuarios-tracker` hasta que se decida el merge. La cadena vive bajo ese branch con 4 work-branches hijos. Cada PR child mantiene su diff enfocado en su work-unit y nunca apunta directo a `main` (regla del chained-pr skill).

### Follow-up documentado (fuera del change)

- PR2: cliente tipado `SGV.Web/Integration/Usuarios/{IUsuarioApiClient, UsuarioApiClient}` + DI.
- PR3: Razor Pages Index segmentado + Details readonly + Delete (PRG) + Reactivar (PRG).
- PR4: Razor Pages Create con dropdown Personas + Edit atómico + `_Form.cshtml` compartido + ítem colapsable "Seguridad" en `_Sidenav.cshtml` (gateado `EsAdministrador`).

## Buscador modal de Personas en Crear/Editar Usuario — manejo de `409` (D-10 corregida)

Change archivado: `2026-07-17-buscador-personas-modal` (PRs #158, #159, #160). El `design.md` archivado (D-10) proponía reflejar el `409` por carrera (persona ya con usuario activo) como `ModelState.AddModelError(string.Empty, ...)`, copiando el patrón `CodigoDuplicado` de Cargos. La implementación, en cambio, sigue `tasks.md` y la spec REQ-UCE-10: `ModelState.AddModelError("Input.PersonaId", "Esa persona ya tiene un usuario activo.")`.

### Decisión vigente

Ante `409` en POST de Crear o Edit de Usuario, el feedback se vincula al campo `PersonaId` del formulario (no al `ModelState` general). Esto preserva mejor el form para reintento y cumple REQ-UCE-10 de forma verificable por test (`Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` y equivalente Edit).

### Por qué se apartó del `design.md` original

El `design.md` archivado es audit trail del change cerrado y **no se modifica**. Esta entrada en `docs/decisiones-implementacion.md` es la referencia vigente para implementaciones futuras. La razón del apartamiento: `string.Empty` produce un error visible en el `asp-validation-summary` (mejor para errores globales del form, p.ej. conexión con el backend), mientras que `Input.PersonaId` produce el error pegado al campo, que es lo que el usuario necesita ver para entender qué dato конкретно está ocupado. La spec REQ-UCE-10 ya exigía lo segundo.

### Comportamiento a replicar en próximos cambios análogos

Cuando un error 409 (o equivalente de conflicto único) provenga de un endpoint consumido por un formulario Razor:

- **Sí** usar `ModelState.AddModelError("<nombre-del-campo>", "<mensaje-acotado-al-campo>")` para feedback pegado al input.
- **Sí** preservar el resto del form para reintento (excepto password, que nunca se preserva).
- **No** usar `string.Empty` salvo que el error sea genuinamente transversal al form.

Archivos vigentes: `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs`, `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` (ambos en `OnPostAsync` / handler equivalente).

## Cultura regional es-AR como default único (issue #191)

### Contexto

La shell web (`SGV.Web`) y la API (`SGV.Api`) operaban con la cultura del
proceso heredada del host. Tres bugs se concentraban en la gestión de
habilidades de Cargos:

1. Los banners de feedback de Bootstrap (success / warning / danger) no
   eran dismissible: el usuario no podía cerrarlos sin esperar al próximo
   redirect.
2. El input `Ponderación` (`type="number"`) aceptaba sólo `"."` como
   separador decimal (HTML5 ignora la cultura de la página). Los usuarios
   con configuración regional es-AR tipeaban `"1,50"` y el form rechazaba
   el valor al guardar.
3. La ponderación podía llegar vacía desde el form; el servicio aplicaba
   un default invisible de `1.00m`, oscureciendo el problema (la grilla
   mostraba una ponderación que el usuario nunca tipeó).

### Decisión adoptada (vigente)

- **Cultura única `es-AR`** para Web y API vía
  `AddLocalization()` + `Configure<RequestLocalizationOptions>` con
  `DefaultRequestCulture = "es-AR"` + `app.UseRequestLocalization()`
  insertado entre `UseRouting()` y `UseAuthentication()` en Web, y
  temprano en el pipeline de API (después de CORS, antes de RateLimiter).
  Es la fuente única de la cultura para render, model binding,
  validación y orden de strings.
- **JSON wire sigue invariante.** El contrato HTTP entre Web y API
  transporta decimales con `.` (default de `System.Text.Json`); la cultura
  es-AR sólo afecta la capa de presentación y los servicios que usen
  `CultureInfo.CurrentCulture` (por ejemplo, el `StringComparer.Create
  (CultureInfo.CurrentCulture, ...)` en el orden de unidades
  organizativas). No hay riesgo de romper el contrato de API.
- **`type="number"` reemplazado por `type="text" inputmode="decimal"
  pattern="[0-9]+([,][0-9]{1,2})?"`** en los inputs de Ponderación
  (grilla editable + form Asignar). HTML5 ignora la cultura en
  `type="number"` y exige `"."`; la única forma nativa de aceptar coma
  sin JS custom es migrar a `text` con `inputmode` + `pattern` +
  validación server-side.
- **`CargoSkillPonderacionRule.TryParse`** es tolerante: si el string
  tiene coma y no tiene punto, la coma es separador decimal de es-AR y
  se reemplaza por punto antes de parsear en `InvariantCulture`. Esto
  esquiva la ambigüedad de `NumberStyles.Number` en es-AR donde la coma
  es a la vez separador de miles y decimal (`"100,00"` sin contexto se
  interpreta como `10000`).
- **`AsignarCargoSkillRequestValidator.Ponderacion` ahora es
  `NotNull`** (era opcional). El form de Asignar renderiza `value="1"`
  como default HTML estático; si el usuario borra el valor, la validación
  local corta antes de invocar al backend y el servicio nunca recibe un
  payload con ponderación vacía. Se eliminó el fallback
  `request.Ponderacion ?? PonderacionPorDefecto` en `CargoSkillServicio`
  — alcanzar esa rama con `null` es ahora un error de programación
  (`ArgumentNullException` defensivo).
- **Alertas `alert-dismissible` + `btn-close`** en todos los banners de
  feedback de `Habilidades.cshtml` (patrón vigente en
  `Pages/Auth/SignIn.cshtml`). El usuario puede cerrar el banner sin
  esperar al próximo PRG.

### Invariantes preservados

- `Personas/Edit.cshtml.cs` y `Cargos/Edit.cshtml.cs` siguen usando
  `InvariantCulture` explícito para serializar `ReturnPage` (campo hidden
  de paginación): son identificadores técnicos que viajan por querystring
  y deben ser invariantes.
- `src/SGV.Api/Program.cs` mantiene `InvariantCulture` para serializar el
  header HTTP técnico `Retry-After`.

### Archivos clave del change

- `src/SGV.Web/Program.cs` — `AddLocalization` + `UseRequestLocalization`.
- `src/SGV.Api/Program.cs` — mismo setup, pipeline API.
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml` — alerts
  dismissible, input `text`/`inputmode="decimal"`, default `1`.
- `src/SGV.Web/Pages/Organizacion/Cargos/CargoSkillPonderacionRule.cs` —
  parser tolerante con normalización de coma es-AR.
- `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/AsignarCargoSkillRequestValidator.cs` —
  regla `NotNull` para `Ponderacion`.
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` —
  eliminación del fallback `PonderacionPorDefecto` (constante pública
  removida).

### Cobertura nueva

- `CargoSkillPonderacionRuleTests.TryParse_WithinRange_ReturnsValidAndParsedValue`:
  espejos es-AR (`"1,00"`, `"2,50"`, `"50,00"`, `"50,75"`, `"100,00"`).
- `CargoHabilidadesValidationTests`:
  `Post_Asignar_PonderacionConComaEsAR_GuardaCorrectamente`,
  `Post_Actualizar_PonderacionConComaEsAR_GuardaCorrectamente`,
  `Post_Asignar_PonderacionVacia_NoInvocaApiYMuestraErrorRequerido`,
  `Render_StatusMessageAlert_LlevaAlertDismissibleYBotonClose`.
- `CargoSkillControllerTests.UpsertSkill_PonderacionNull_Returns400ConMensajeObligatoria`.
- Tests actualizados al nuevo contrato:
  `AsignarCargoSkillRequestValidatorTests.Should_Have_Error_When_Ponderacion_Is_Null`,
  `CargoSkillServicioTests.UpsertAsync_SinEsObligatoria_AplicaDefaultFalseYDevuelveDtoCompleto`,
  `CargoSkillServicioTests.UpsertAsync_PonderacionNull_RetornaValidacionYSinGuardar`,
  `CargoHabilidadesLoadTests` (aserción `value="2,50"` en formato es-AR),
  `CargoHabilidadesDeleteErrorTests` (aserción `alert-dismissible`).

Plan de pruebas ejecutado: `dotnet test SGV.slnx` — 2840 / 2840 verde
(11 tests nuevos sobre la base previa de 2829).

## Variantes opt-in del REQ-SPA-EVOLUTION-001

> Change: `migrar-campo-categoria-habilidades-a-tabla`. Slice 1 / 4 de la
> capability `categoria-habilidad-catalog`. Artefactos SDD completos en
> `openspec/changes/migrar-campo-categoria-habilidades-a-tabla/`.

### Rationale

`Habilidad.Categoria` es texto libre. Se introduce el catálogo inmutable
`CategoriasHabilidad` (bloque `72000000-…`) con FK opcional
`Habilidades.CategoriaId` (Guid?). Para preservar los datos legacy que
NO matcheen ningún `Nombre` del seed (e.g. `"Otra cosa"`), la migración
NO aborta — los strings sucios caen a `CategoriaId = NULL` con
**auditoría de la transición** en la tabla `Auditorias` (columna
`NewValuesJson`) para remediación post-deploy.

### Patrón

- FK nullable (`Habilidades.CategoriaId: Guid?`).
- Backfill case-insensitive con `LOWER(h.Categoria) = LOWER(c.Nombre)`.
- Sin match → `CategoriaId = NULL` + fila en `Auditorias` con
  `Metadata = { Origen: "Migracion.AddCategoriaHabilidadCatalog", CategoriaOriginal: <valor legacy> }`.
- FK constraint `OnDelete(Restrict)` (la categoría no se borra si está en uso).
- Pre-flight NO fail-loud: lista los valores sucios para logging.
- Forward-only: `Down()` lanza `NotSupportedException` (precedente
  `FixActivePuestoIdUniqueType`).

### Precedentes (instancias documentadas de REQ-SPA-EVOLUTION-001)

| # | Catálogo                | Variante                              | Precedente (issue)        |
|---|-------------------------|---------------------------------------|---------------------------|
| 1 | `NivelCargo`            | strict (sin opt-in)                   | #141                      |
| 2 | `TipoDocumento`         | opt-in relajada (FK nullable + audit) | #147                      |
| 3 | (reservada)             | —                                     | —                         |
| 4 | `CategoriaHabilidad`    | opt-in relajada (cuarta invocación)   | migrar-campo-categoria-habilidades-a-tabla |

### Tradeoffs aceptados

- Habilita rollback parcial si una nueva fila seed no resuelve un
  valor legacy sucio.
- La auditoría permite remediación post-deploy via SQL:
  `SELECT * FROM Auditorias WHERE EntityName = 'Habilidad' AND Operation = 'BackfillLegacyCategoriaToNull'`.
- La FK sigue `OnDelete(Restrict)` para evitar borrado accidental de
  categorías en uso.

### Cobertura nueva

- `tests/SGV.Tests/Persistencia/CategoriaHabilidadConstantesTests.cs`:
  4 Guids únicos en bloque reservado `72000000-…`, semilla `HasData`
  alineada con `DatosSemilla`.
- `tests/SGV.Tests/Persistencia/CategoriaHabilidadMigracionTests.cs`
  (11 tests `[MySqlFact]`): estructura post-migración, seed, FK
  Restrict, backfill case-insensitive, variante opt-in relajada con
  auditoría, drop index/columna legacy, idempotencia, `Down()`
  forward-only.
- `tests/SGV.Tests/Api/CategoriasHabilidadControllerTests.cs`:
  integración con `WebApplicationFactory` (200/401/404/405, shape JSON).

### Archivos clave

- `src/SGV.Dominio/Habilidades/CategoriaHabilidad.cs` (sealed record +
  `Reconstitute` factory).
- `src/SGV.Dominio/Habilidades/CategoriaHabilidadRules.cs` (constantes
  de longitud: `CodigoMaxLength = 50`, `NombreMaxLength = 100`).
- `src/SGV.Infraestructura/Persistencia/Entidades/CategoriaHabilidadEntity.cs`:
  paridad con `TipoDocumentoEntity` (sin `IsActive`/`IsDeleted`).
- `src/SGV.Infraestructura/Persistencia/Catalogos/CategoriaHabilidadConstantes.cs`:
  source of truth para seeds y migración.
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260723203015_AddCategoriaHabilidadCatalog.cs`:
  migración forward-only con backfill opt-in relajado.
- `src/SGV.Api/Controllers/CategoriasHabilidadController.cs`: read-only,
  `[Authorize]`, default-deny.

## Setup inicial del primer administrador — issue #195

> Change: `setup-admin-inicial-issue-195`. Artefactos SDD completos en `openspec/changes/setup-admin-inicial-issue-195/`. Chain strategy: `feature-branch-chain` con 3 PRs encadenados contra tracker `feat/setup-admin-inicial-issue-195`. PRs [#196](https://github.com/elflacoseba/SGV/pull/196) (backend) y [#197](https://github.com/elflacoseba/SGV/pull/197) (frontend) mergeados; este PR (#3) cierra la documentación.

### Contexto y problema

La issue #195 atacó un chicken-and-egg operacional: con la base vacía ningún endpoint puede crear el primer admin porque la fallback policy vigente (`RequireAuthenticatedUser()`) rechaza sin token, y nadie tiene credenciales todavía. El fix expone un flujo de setup completamente anónimo, one-time, restringido a bases sin filas en `AspNetUsers`. Diseño completo y tradeoffs en `openspec/changes/setup-admin-inicial-issue-195/design.md`.

### Las seis decisiones técnicas

| # | Decisión | Justificación compacta |
|---|----------|------------------------|
| §2.1 | Aislamiento MySQL default (`REPEATABLE READ`) + índice único `IX_AspNetUsers_NormalizedUserName` de Identity como defensa real contra race | `SERIALIZABLE` y gap locks son sutiles en MySQL InnoDB; el índice único rechaza el duplicado vía `UserManager.CreateAsync` → `IdentityResult.DuplicateUserName`. Alternativas rechazadas: advisory locks, `INSERT ... ON DUPLICATE KEY UPDATE`. |
| §2.2 | `[AllowAnonymous]` en `SetupController` (status + post) y en `TiposDocumentoController.GetAll`; `GetById` mantiene `[Authorize]` heredado | El catálogo `TipoDocumento` es inmutable (4 filas seed). `GetAll` no expone PII. Patrón idéntico a `AuthController.Login`. |
| §2.3 | Fail-open con `IMemoryCache` TTL 30s en `SetupApiClient.ObtenerEstadoAsync` ante `HttpRequestException` / `TaskCanceledException` | Fail-closed rompería el acceso a producción ante una caída de API. El cache absorbe fallas transitorias; la UI igual permite POST y el server responde 409 si ya hay admin. |
| §2.4 | Enum `SetupErrorCode` (10 valores) reusando `UsuarioIdentityGateway.IdentityErrorMap` | Mapeo centralizado `IdentityError.Code → SetupErrorCode → HTTP`. Evita filtrar detalles de Identity al cliente. |
| §2.5 | Rate limit `AddFixedWindowLimiter("Setup", 5 req / 15 min)` aplicado sólo a `POST /api/v1/setup` (no al status) | Consistente con `ForgotPassword` (3 req) y `ResetPassword` (5 req). El status no muta y ya está protegido por cache de 30s. |
| §2.6 | `409 Conflict` con código `SetupYaCompletado` cuando `AspNetUsers` ya tiene filas | Coincide con la taxonomía vigente (`UsuarioErrorType.Conflict`); `404 Gone` es raro y semánticamente confuso para "endpoint cerrado pero vivo". |

### Desviaciones documentadas (W-001, W-002)

- **W-001 — Atomicidad best-effort.** Pomelo 9 + MySqlConnector no exponen `BeginTransactionAsync` anidados con SAVEPOINT explícito; el gateway de Identity maneja su propia transacción interna. Si `PersonaServicio.CrearAsync` ok pero `identityGateway.CrearAsync` falla, se compensa con `PersonaServicioComandos.DesactivarAsync` (soft-delete de Persona) y rollback manual. Audit es best-effort (si falla se loggea warning, no se hace rollback). Estado final siempre consistente: 1 admin válido o ninguno; una `Persona` huérfana soft-deleted queda como residuo aceptable porque el setup es one-time y la próxima vuelta encuentra `IsDeleted=1` que no cuenta para `AnyUsersAsync`. Ver `verify-report.md` §"Hallazgos WARNING".
- **W-002 — `AnyUsersAsync` se ejecuta fuera de la transacción outer.** Por la misma limitación de W-001 no hay transacción EF única que envuelva la guarda + creación. La defensa contra doble admin simultáneo es el índice único de Identity, probado por `SetupConcurrencyMySqlFactTests` (1×200 + 1×409|500).

### Fail-open con cache TTL 30s — riesgo aceptado

Si el setup completo se hace en otro nodo antes de que la cache local expire, el nodo actual puede ver `RequiresSetup=true` stale hasta 30s. La UI igual permite POST y el server responde 409 con `SetupYaCompletado`, así que la ventana de confusión UX es acotada y recuperable. Aceptable porque la probabilidad de setup concurrente entre nodos es despreciable en escenarios reales.

### Archivos clave

**PR #1 (backend, [#196](https://github.com/elflacoseba/SGV/pull/196))** — `src/SGV.Contracts/Setup/*.cs` (5 archivos), `src/SGV.Aplicacion/Setup/*.cs` (4 archivos), `src/SGV.Infraestructura/Setup/SetupServicio.cs`, `src/SGV.Api/Controllers/SetupController.cs`, ediciones en `src/SGV.Api/Program.cs` (rate limit) y `src/SGV.Api/Controllers/TiposDocumentoController.cs` (`[AllowAnonymous]`).

**PR #2 (frontend, [#197](https://github.com/elflacoseba/SGV/pull/197))** — `src/SGV.Web/Integration/Setup/{ISetupApiClient,SetupApiClient,SetupHttpResult}.cs`, `src/SGV.Web/Pages/Auth/Setup.{cshtml,cshtml.cs}`, edición en `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` (redirect), `AddMemoryCache` + `AddHttpClient<ISetupApiClient,SetupApiClient>` en `src/SGV.Web/Program.cs`.

**PR #3 (docs, este)** — esta sección de `docs/decisiones-implementacion.md`.

### Tests

- **PR #1** — 17 tests nuevos (`tests/SGV.Tests/Setup/*`): 6 unit, 11 integración `[MySqlFact]`. Cubren happy path, 409 por setup ya completado, validación 400, concurrencia 1×200 + 1×409, auditoría `userId="system"`, rate limit 429 + `Retry-After: 900`, 500 transaccional.
- **PR #2** — 27 tests nuevos (`tests/SGV.Tests/Web/Auth/Setup*` + `tests/SGV.Tests/Web/Integration/Setup/*`): render de 9 campos, dropdown `TipoDocumento`, PRG a `/auth/sign-in` con `TempData`, fieldErrors por campo, errores de transporte recuperables, cache TTL 30s verificado contra `IMemoryCache` real, redirect de `SignIn` cuando `RequiresSetup=true`.

### Riesgos residuales

1. **Persona huérfana soft-deleted** (W-001) — ventana de race entre `Persona.CrearAsync` y `identityGateway.CrearAsync` deja 0-1 Persona con `IsDeleted=1`. Probabilidad <0.01%; el siguiente intento del usuario da 409 limpio.
2. **Stale cache TTL 30s** (§2.3) — si setup completo ocurre en otro nodo, el actual puede servir `RequiresSetup=true` stale hasta 30s. UI y server lo manejan sin pérdida de datos.
3. **Auditoría best-effort** (W-001) — si `AuditoriaServicio.RegistrarAsync` falla por columna o constraint inesperada, la transacción commit no se aborta (sólo log). El log estructurado previo al commit cubre el intento. Razonable: la auditoría del setup completo debe ser atómica con la creación o nada.

### Follow-up

- **S-002 (`verify-report.md`)** — detectar `DbUpdateException` con constraint `IX_AspNetUsers_NormalizedUserName` en `SetupServicio.CrearAdminAsync` y mapear consistentemente a `SetupErrorCode.UserNameDuplicado` (hoy el race puede terminar como 409 o 500 según el path que tome Pomelo).
- **Re-autenticación automática post-setup** (out of scope original) — el usuario debe volver a `/auth/sign-in` y tipear credenciales. Mejora futura: emitir cookie/JWT directamente al completar el setup.
- **Email de verificación** (out of scope original) — el setup crea la cuenta y termina; no hay flujo de confirmación.

## Issue #273 — correcciones en el módulo "Nueva Vacante"

### Contexto y problema

La issue #273 consolidó tres correcciones puntuales reportadas por
el usuario sobre el módulo `Vacantes/Create` (`src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml`):

- **(A) Ocultar dropdown de Estado al crear.** Una vacante nueva
  SIEMPRE debe crearse en estado "Abierta". El usuario no debería
  poder elegir el estado inicial.
- **(B) Mojibake "En SelecciÃ³n".** La fila persistida del catálogo
  `EstadosVacante` (`Codigo='EnSeleccion'`) tenía el `Nombre`
  corrupto por encoding Latin-1 de bytes UTF-8. El seed estaba
  correcto (`DatosSemilla.cs:58`); el problema era de filas
  pre-existentes con charset mal negociado.
- **(C) Dropdown de Puestos ordenado alfabéticamente.** Los dropdowns
  de selección de Puesto (en `Vacantes/Create`, `Puestos/Create`,
  `Puestos/Edit` y `Ocupaciones/Create`) deben ordenar por `Nombre`
  ascendente.

### Decisiones técnicas

| # | Decisión | Justificación compacta |
|---|----------|------------------------|
| §273.1 | **Regla "vacante nueva = Abierta" en la capa de Aplicación, NO en la UI.** `VacanteServicioComandos.CrearAsync` resuelve el `EstadoVacanteId` desde el catálogo cuando el request trae `null` o `Guid.Empty`, buscando el `Codigo == "Abierta"`. Si el catálogo no contiene "Abierta", devuelve `Unexpected + EstadoVacanteInexistente`. | Centralizar la invariante en la capa de servicio la hace robusta: si alguien crea una vacante desde otro consumer (API directa, integraciones, tests), no tiene que recordar la regla. La UI sólo deja de mostrar el dropdown; el contrato `CrearVacanteRequest.EstadoVacanteId` pasa a ser `Guid?` nullable. |
| §273.2 | **ID "Abierta" se resuelve por `Codigo` en el catálogo, NO por constante hardcoded.** El servicio llama a `IEstadoVacanteRepository.GetByCodigoAsync("Abierta")` (método agregado en esta segunda iteración del change, §273.8) y usa la constante `EstadoVacanteCodigos.Abierta` expuesta desde `SGV.Contracts` (§273.9). | `SGV.Contracts` no referencia `SGV.Infraestructura`; la constante `EstadoVacanteConstantes.AbiertaId` es `internal` a Infraestructura y no es accesible desde Aplicación. Resolver por `Codigo` mantiene la capa limpia y tolera re-seeds con IDs distintos. |
| §273.3 | **Validación de `EstadoVacanteId` removida del validador.** `CrearVacanteRequestValidator` ya no rechaza `null` ni `Guid.Empty`; el campo es opcional a nivel contrato. | Toda la lógica de catálogo + estado terminal vive en el servicio. La validación previa (`NotEqual(Guid.Empty)`) rechazaba un input que el nuevo diseño quiere aceptar y resolver. |
| §273.4 | **`VacanteInputModel.EstadoVacanteId` conserva `[Required]`.** La propiedad compartida por `Create` y `Edit` mantiene el atributo para que `Edit` siga validando el cambio de estado explícito. `Create.cshtml.cs.OnPostAsync` limpia `ModelState.Remove("Input.EstadoVacanteId")` antes de validar, ya que el campo no se envía. | El `[Required]` beneficia a `Edit` (cambio de estado es requerido) sin afectar a `Create` (donde el campo se omite del form). Eliminarlo afectaría el flujo de cambio de estado. |
| §273.5 | **Migración de datos forward-only e idempotente para el mojibake.** Nueva migración `20260813120000_FixEstadoVacanteEnSeleccionEncoding` ejecuta `UPDATE EstadosVacante SET Nombre='En Selección' WHERE Codigo='EnSeleccion' AND Nombre LIKE '%Ã³%'`. `Down()` queda vacío. | El `WHERE LIKE '%Ã³%'` es la firma canónica del mojibake: bytes `0xC3 0xB3` que Latin-1 renderiza como "Ã³". Filas correctas ("En Selección" UTF-8) no se tocan, garantizando idempotencia. La detección por bytes mal codificados evita riesgos de falsos positivos sobre acentos correctos. |
| §273.6 | **Orden por defecto de `PuestoRepository.ListAllAsync` cambia a `Nombre ASC, Codigo ASC` (era `Codigo ASC`).** Aplica a TODA la app porque `ListAllAsync` alimenta `GET /api/v1/puestos`, consumido por los dropdowns de `Vacantes/Create`, `Puestos/Create`, `Puestos/Edit` y `Ocupaciones/Create`. | El orden por `Nombre` es lo que el usuario espera al escanear visualmente un dropdown; `Codigo` queda como tiebreaker estable para tests determinísticos. La paginación server-side (`QueryAsync`) sigue aceptando `?sort=codigo_asc/nombre_asc/...` explícito, sin cambio. |
| §273.7 | **`Get_Create_WhenCatalogLoadFails_ShowsRecoverableErrorAndDisablesSave` se elimina.** Ese test verificaba el alert-danger cuando fallaba `ListarEstadosAsync`; como `Create` ya no carga estados, el path es obsoleto. | Cobertura equivalente se mantiene en `Get_Create_WhenPuestoCatalogLoadFails_ShowsRecoverableErrorAndDisablesSave` (falla de `ListarPuestosAsync`). El test eliminado no aportaba valor post-cambio. |
| §273.8 | **Se agrega `IEstadoVacanteRepository.GetByCodigoAsync(string codigo)`** siguiendo el patrón de `INivelCargoRepository.GetByCodigoAsync`. `VacanteServicioComandos.CrearAsync` lo usa en lugar de `ListAllAsync` + `FirstOrDefault(c => c.Codigo == "Abierta")` para resolver el estado inicial. | Con 4 filas la diferencia es despreciable, pero el método expone la intención real ("busco por código, no traigo todo") y deja la puerta abierta a un `WHERE Codigo = ?` directo en DB en lugar de materializar el catálogo en memoria. El método agrega valor semántico más allá de la optimización. |
| §273.9 | **Constantes de códigos de catálogo se exponen en `SGV.Contracts/Vacantes/Catalogos/EstadoVacanteCodigos.cs`** (`Abierta`, `EnSeleccion`, `Cubierta`, `Cancelada`). `VacanteServicioComandos.CrearAsync` usa `EstadoVacanteCodigos.Abierta` en vez del magic string literal `"Abierta"`. | Los IDs siguen viviendo en `SGV.Infraestructura.Persistencia.Catalogos.EstadoVacanteConstantes` (single source of truth del seed), pero los códigos son parte del contrato de negocio que la capa de Aplicación necesita para resolver la regla. Mantener el ID en Infraestructura (donde se genera) y el código en Contracts (donde se consume) evita exponer detalles de seed. |
| §273.10 | **Rename local en `Create.cshtml.cs`: `LoadCatalogsAsync` → `LoadPuestosAsync` y `CatalogsReady` → `PuestosReady`.** El método y la propiedad quedan en singular porque la página sólo carga un catálogo (el de EstadosVacante se removió en §273.1). | El nombre colectivo era engañoso para un reader futuro. El cambio es interno a Create y no toca Edit (que sí carga múltiples catálogos). Las dos referencias en `Create.cshtml` (`Model.PuestosReady` en el `disabled` del dropdown y del botón Guardar) se actualizaron en paralelo. |

### Archivos clave

- `src/SGV.Contracts/Vacantes/Comandos/CrearVacanteRequest.cs` — `EstadoVacanteId` ahora es `Guid?`.
- `src/SGV.Contracts/Vacantes/Catalogos/EstadoVacanteCodigos.cs` — **nuevo** (§273.9): constantes públicas de los códigos canónicos del catálogo (`Abierta`, `EnSeleccion`, `Cubierta`, `Cancelada`).
- `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` — `CrearAsync` resuelve "Abierta" del catálogo con `GetByCodigoAsync(EstadoVacanteCodigos.Abierta)` cuando el ID viene null/empty.
- `src/SGV.Aplicacion/Vacantes/Consultas/IEstadoVacanteRepository.cs` — **getter nuevo** (`GetByCodigoAsync`) agregado en §273.8, sigue el patrón de `INivelCargoRepository`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/EstadoVacanteRepository.cs` — implementación de `GetByCodigoAsync` con `ArgumentException.ThrowIfNullOrWhiteSpace` defensivo.
- `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/CrearVacanteRequestValidator.cs` — removida la regla de validación de `EstadoVacanteId`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` — `ListAllAsync` ordena por `Nombre, Codigo`.
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260813120000_FixEstadoVacanteEnSeleccionEncoding.cs` — migración nueva, forward-only, idempotente.
- `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml` — bloque del dropdown de Estado eliminado; `Model.CatalogsReady` → `Model.PuestosReady` en los dos `disabled` (§273.10).
- `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` — removida la propiedad `EstadosVacante`, la carga de `ListarEstadosAsync()` y el binding al POST; `ModelState.Remove("Input.EstadoVacanteId")` en `OnPostAsync`. Rename local: `LoadCatalogsAsync` → `LoadPuestosAsync`, `CatalogsReady` → `PuestosReady` (§273.10).
- `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` — 4 tests nuevos (`Crear_ConEstadoVacanteIdNull_…`, `Crear_ConEstadoVacanteIdVacio_…`, `Crear_ConEstadoVacanteIdValido_UsaElIdProvisto`, `Crear_CatalogoSinEstadoAbierta_LanzaUnexpectedFailure`); el test viejo `Crear_EstadoVacanteIdVacio_RetornaValidationFailure` se reemplazó por el nuevo de resolución. `FakeEstadoVacanteRepository` ahora también implementa `GetByCodigoAsync`.
- `tests/SGV.Tests/Persistencia/EstadoVacanteRepositoryTests.cs` — **nuevo** (§273.8): 4 `[MySqlFact]` cubriendo `GetByCodigoAsync` para `Abierta`, `EnSeleccion`, código inexistente y codigo vacío.
- `tests/SGV.Tests/Persistencia/PuestoRepositoryTests.cs` — nuevo `[MySqlFact] ListAllAsync_OrdenaPorNombreAscendenteYDesempataPorCodigo`.
- `tests/SGV.Tests/Persistencia/MigracionEstadoVacanteEncodingTests.cs` — 2 nuevos `[MySqlFact]` cubriendo idempotencia de la migración.
- `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` — `Get_Create_WhenMutationRole_RendersFormWithCatalogs` ajustado (dropdown ya no aparece); nuevo `Get_Create_OmiteDropdownDeEstado`; 3 tests de POST Create dejan de enviar `Input.EstadoVacanteId`; `Get_Create_WhenCatalogLoadFails_…` (estados) eliminado.

## Issue #281 — `VigenteDesde`/`Hasta` como etiqueta informativa + filtro `vigenteEn` en UI

### Contexto y problema

El repo ya persistía `VigenteDesde`/`VigenteHasta` en `UnidadOrganizativa`
como metadata informativa (constraint `Hasta >= Desde`, sin rechazo de
operaciones). El wire type `UnidadOrganizativaQuery` ya soportaba el
filtro `vigenteEn` en la API (`GET /api/v1/unidades-organizativas/consulta?vigenteEn=YYYY-MM-DD`),
pero la UI nunca lo enviaba. La issue #281 pidió la **Opción C
(híbrido)**: hacer visible el estado de vigencia en la UI del listado,
detalle y árbol, y propagar el filtro por todos los route values.

El cambio es **no funcional**: no rechaza operaciones; sólo expone
información. El repo LINQ y el controller no se tocan.

### Decisiones técnicas

| # | Decisión | Justificación compacta |
|---|----------|------------------------|
| §281.1 | **Helper `EsVigente(DateOnly fechaReferencia)` en el dominio.** Semántica: `VigenteDesde > fechaReferencia → false`; `VigenteHasta < fechaReferencia → false`; cualquier otro caso (incluyendo ambos `null`) → `true`. Regla puramente informativa, NO rechaza operaciones. | Testeable en aislamiento sin acoplarse a `DateTime.Today`. Reutilizable desde la UI (la página inyecta `hoy`). Centralizar la regla en dominio deja lugar a un futuro uso desde API si se decidiera rechazar operaciones (no es este change). |
| §281.2 | **`VigenciaViewModel(Texto, BadgeClass)` en `SGV.Web.Integration.Organizacion`** (no en `SGV.Web.Pages`) con factory estática `Desde(DateOnly? desde, DateOnly? hasta, DateOnly hoy)`. | El record `UnidadOrganizativaListItemViewModel` ya vive en `Integration` (lo consume `IUnidadOrganizativaApiClient`); poner `VigenciaViewModel` en `Pages` crearía dependencia circular `Integration → Pages`. La convención del repo es mantener `Integration` como capa de wire/viewmodels compartidos por Pages y Api client. |
| §281.3 | **Badge CSS opcional.** Sin badge cuando la ventana contiene a `hoy` o ambos extremos son `null`; `badge-soft-warning` para "Fuera de vigencia" (`VigenteHasta < hoy`); `badge-soft-info` para "Aún no vigente" (`VigenteDesde > hoy`). Patrón copiado de `Ocupaciones/Index.cshtml:101` (badge-soft-success/warning/danger). | Inspinia provee `badge-soft-info` y `badge-soft-warning`; amarillo suave indica "vencido/atención" sin generar alarma (no es un error, sólo un rango pasado). El azul info indica "futuro" sin urgencia. |
| §281.4 | **Formato `yyyy-MM-dd` invariante para query string y formularios.** `DateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)` en el `FormatVigenteEn` del page model. | `DateOnly.ToString()` sin formato usa la cultura actual, que puede dar `dd/MM/yyyy` u otro formato dependiendo del locale del servidor/cliente. Forzar ISO evita binding fallido al volver al Index con `vigenteEn=01/15/2024` mal parseado. |
| §281.5 | **Captura de `VigenteEn` en `OnGetAsync` como parámetro, NO `[BindProperty(SupportsGet = true)]`.** | Coincide con el patrón vigente del PageModel (`search`/`sort`/`status` son parámetros del handler). El binder de ASP.NET Core 10 parsea `DateOnly?` desde `?vigenteEn=YYYY-MM-DD` sin configuración adicional. |
| §281.6 | **`vigenteEn` se propaga en TODOS los route values del Index.** `ReturnToListRouteValues`, `CreateRouteValues`, `BuildDetailsRouteValues`, `BuildEditRouteValues`, `BuildViewToggleRouteValues`, links de sort (3), links de paginación (3), forms de delete/reactivate (3) y form de search. En cada lugar se serializa con `FormatVigenteEn(VigenteEn)` (yyyy-MM-dd) o `null` cuando no hay filtro. | El filtro debe sobrevivir a cualquier interacción dentro del listado: sort, paginación, segment toggle (Activas/Eliminadas), abrir detalle/editar, ejecutar delete/reactivate y volver al listado. |
| §281.7 | **Round-trip a través de Details/Edit/Create con `returnVigenteEn`.** Patrón análogo a `returnView`/`returnStatus`/`returnSort` ya vigentes: `BuildDetailsRouteValues` y `BuildEditRouteValues` emiten `returnVigenteEn`; DetailsModel/EditModel/CreateModel aceptan `vigenteEn` y `returnVigenteEn` con fallback `returnVigenteEn ?? vigenteEn`. `BuildReturnToListUrl` agrega `vigenteEn` a la query string del URL de retorno. | Sin esta propagación, abrir el detalle desde el listado con filtro `vigenteEn=2024-01-15` y volver al listado pierde el filtro. La inconsistencia sería una regresión UX vs. el patrón ya vigente para `search`/`sort`/`status`. |
| §281.8 | **Árbol muestra "Vigencia abierta" sin badge por hoy.** `UnidadOrganizativaTreeNodeViewModel.Vigencia` se popula desde `null/null` en `MapToViewModel` (Organigrama.cshtml.cs) y `MapToTreeViewModel` (Index.cshtml.cs tree view) porque el wire type `UnidadOrganizativaTreeNodeDto` no expone `VigenteDesde`/`VigenteHasta`. | El wire type del tree es parte del contrato API y no se extiende en este change. La infraestructura (`TreeNodeViewModel` + helper) queda en su lugar para que un follow-up que extienda la API del tree habilite badges reales sin más cambios en UI. El render server-side de `_TreeNode.cshtml` ya muestra el badge cuando hay datos (preparado). |
| §281.9 | **Organigrama client-side (Google OrgChart) NO muestra badge de vigencia.** `organigrama.js` no se tocó porque (a) no expone `Vigencia` en el JSON serializado por `Organigrama.cshtml` y (b) renderizar badges en celdas de Google OrgChart requiere HTML customization que está fuera del scope "no funcional" del change. | Cuando el wire type `TreeNodeDto` extienda sus campos (§281.8) y se decida extender el JS, el badge se podrá agregar sin tocar el modelo de dominio ni los viewmodels. Reportado como follow-up. |
| §281.10 | **Se mantienen los warnings preexistentes de tests** (xUnit1031, xUnit2029, xUnit2013, EF1002) — sin cambios en archivos de tests preexistentes. | El scope del change es agregar 6 tests nuevos de `EsVigente` y nada más. No se tocan tests preexistentes. |

### Limitaciones y decisiones de producto pendientes

- **§281.8 + §281.9**: el árbol (`Organigrama` y tree view en `Index`) y el Organigrama client-side no muestran badges reales de vigencia. La UI está **preparada** (`VigenciaViewModel` en `UnidadOrganizativaTreeNodeViewModel`, helper de badge en `_TreeNode.cshtml`); sólo falta extender el wire type `UnidadOrganizativaTreeNodeDto` para que lleve `VigenteDesde`/`VigenteHasta`, y opcionalmente `organigrama.js` para renderizar en Google OrgChart. Esto **requiere decisión de producto**: ¿queremos badges reales en el árbol aunque el árbol de Google Charts no los muestre inicialmente? Si sí, hay que extender el wire type (toca API).
- **Decisión de UX (no técnica)**: ¿cómo se captura `vigenteEn` en la UI? El change implementa la captura desde query string (`?vigenteEn=YYYY-MM-DD`) pero **no agrega un control de input** en `Index.cshtml` para setear el filtro desde la UI. Las opciones son: (a) date picker en el header de filtros junto al search box, (b) presets tipo "Vigentes hoy" / "Vigentes en 2030", (c) sin control por ahora y depender de deep-links. Esto requiere decisión de producto (no resuelta en este change).

### Archivos clave

**Dominio**
- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` — XML doc de `VigenteDesde`/`VigenteHasta` aclarando uso informativo; nuevo `EsVigente(DateOnly fechaReferencia)`.

**Web Integration**
- `src/SGV.Web/Integration/Organizacion/VigenciaViewModel.cs` — **nuevo**: record `(Texto, BadgeClass)` + factory estática `Desde(DateOnly?, DateOnly?, DateOnly)`.
- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs` — `UnidadOrganizativaListItemViewModel.Vigencia` ahora es `VigenciaViewModel` (era `string`); `UnidadOrganizativaListQuery` agrega `DateOnly? VigenteEn = null`.
- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` — `BuildQueryUri` agrega parámetro `DateOnly? vigenteEn` y serializa como `vigenteEn=yyyy-MM-dd` (CultureInfo.InvariantCulture).
- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` — `BuildReturnToListUrl` agrega parámetro `string? vigenteEn` y lo agrega a la query string cuando presente.

**Web Pages**
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/UnidadOrganizativaTreeNodeViewModel.cs` — agrega `VigenciaViewModel Vigencia` al record (necesario para tree view en Index y Organigrama).
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` — propiedad `DateOnly? VigenteEn`; `VigenteEn` se agrega a `OnGetAsync`/`OnPostDeleteAsync`/`OnPostReactivateAsync`; preservado en `ReturnToListRouteValues`, `CreateRouteValues`, `BuildDetailsRouteValues` (`returnVigenteEn`), `BuildEditRouteValues` (`returnVigenteEn`), `BuildViewToggleRouteValues`; `MapToViewModel` y `MapToTreeViewModel` usan `VigenciaViewModel.Desde(...)` con `hoy = DateOnly.FromDateTime(DateTime.Today)`; helper privado `FormatVigenteEn(DateOnly?)` para serialización invariante.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` — celda `@item.Vigencia` reemplaza por badge condicional; hidden input `vigenteEn` en search/delete/reactivate forms y el form del alert de éxito; `vigenteEn` agregado a los 3 sort headers, 3 links de paginación, 2 links de segmento (Activas/Eliminadas) y al botón Crear (via `Model.CreateRouteValues`).
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml.cs` — `ReturnVigenteEn` (string) + `VigenciaViewModel? Vigencia` (calculado en `OnGetAsync` cuando `Unidad` está disponible); `OnGetAsync` acepta `vigenteEn` y `returnVigenteEn` con fallback; `OnPostReactivateAsync` lee `returnVigenteEn` del form; `ReturnToListUrl` lo propaga.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` — celda Vigencia reemplaza la lógica inline de 4 ramas por badge condicional sobre `Model.Vigencia`; hidden `returnVigenteEn` en el form de Reactivar; link Edit incluye `returnVigenteEn`.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` — `[BindProperty] string? ReturnVigenteEn` + parámetros `vigenteEn` y `returnVigenteEn` en `OnGetAsync`; `OnPostAsync` y `OnPostReactivateAsync` leen `returnVigenteEn` del form y lo propagan en los tres `RedirectToPage`.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml` — hidden `<input type="hidden" asp-for="ReturnVigenteEn">` en el form principal; hidden manual `name="returnVigenteEn"` en el form de Reactivar del alert.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml.cs` — `[BindProperty] string? ReturnVigenteEn` + parámetros `vigenteEn`/`returnVigenteEn` en `OnGetAsync`; redirect a Details propaga `returnVigenteEn`.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml` — hidden `<input type="hidden" asp-for="ReturnVigenteEn">`.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Organigrama.cshtml.cs` — `MapToViewModel` toma `DateOnly hoy` y popula `Vigencia` con `VigenciaViewModel.Desde(null, null, hoy)` (limitación §281.8).
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/_TreeNode.cshtml` — bloque del badge de tipo reagrupado en un `d-flex` junto al badge de vigencia (preparado para §281.8).

**Tests**
- `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` — 6 tests nuevos `EsVigente_*` cubriendo: sin ventana, VigenteDesde futuro (antes/después), VigenteHasta pasado, VigenteHasta futuro (incluyendo límite), rango completo (antes/dentro/después). Total de tests en el archivo: 23 (17 preexistentes + 6 nuevos).

### Validación

- `dotnet build SGV.slnx`: 0 errores, 90 warnings (todos preexistentes — xUnit1031, xUnit2029, xUnit2013, EF1002 — ninguno en archivos tocados por este change).
- `dotnet test SGV.slnx --no-build`: 3557 passed, 1 failed, 0 skipped. El único fallo (`UnidadOrganizativaRepositoryTests.IsDescendantAsync_ConCicloDirecto_LanzaCicloJerarquicoEnTiempoAcotado`) **es preexistente** (verificado con `git stash` + re-run sobre `develop` antes del change): `Assert.ThrowsAsync<InvalidOperationException>` espera `CicloJerarquico` pero el repo no lanza. Probable regresión del fix de #280 sobre el path de triggers anti-ciclo en MySQL real — **no relacionado con #281**, no se modifica.
- `[MySqlFact]` sin MySQL local: los tests con `[MySqlFact]` que requieren conexión corren contra MySQL disponible en el ambiente (CI provee `mysql:8.0`); localmente los tests que sembraban el ciclo (`IsDescendantAsync_ConCicloDirecto`) sí corren y revelan la falla preexistente.

### Decisiones D1/D2/D3 tomadas (consolidado)

- **D1 — Lógica "está vigente a fecha X":** agregada al dominio como `public bool EsVigente(DateOnly fechaReferencia)` (§281.1). Sin cambios en validaciones existentes (sigue habiendo `ValidarVigencia` que sólo verifica `Hasta >= Desde`).
- **D2 — Render de vigencia en UI:** `VigenciaViewModel(Texto, BadgeClass)` en `SGV.Web.Integration.Organizacion` (§281.2). Sin badge en ventana dentro de rango; `badge-soft-warning` para "Fuera de vigencia"; `badge-soft-info` para "Aún no vigente".
- **D3 — Captura de `VigenteEn` en IndexModel:** parámetro del handler `OnGetAsync` (§281.5), preservado en todos los route values y propagado vía `returnVigenteEn` a Details/Edit/Create. Formato `yyyy-MM-dd` invariante para evitar binding frágil (§281.4).



## Housekeeping pre-release del módulo de Personas

> PR: `https://github.com/elflacoseba/SGV/pull/292` (D-PE-01), `https://github.com/elflacoseba/SGV/pull/293` (D-PE-02), `https://github.com/elflacoseba/SGV/pull/294` (D-PE-03), `https://github.com/elflacoseba/SGV/pull/295` (D-PE-04).
> Branch base: `develop`.
> Squash commits: `72d41fc5` (D-PE-01), `2ae58e6e` (D-PE-02), `9e440418` (D-PE-03), `347b8f93` (D-PE-04).
> Fecha: 2026-08-18.

Cierra los cuatro puntos pendientes del análisis release-readiness del módulo de Personas (Dominio, Aplicación, Infraestructura, API, Web) documentado en engram (memoria `obs-b0858b96ab7e6141`, topic `decision/an-lisis-release-readiness-del-m-dulo-personas-sgv`). Cubre un bug funcional real, dos armonizaciones de contratos, una capacidad nueva server-side y una limpieza de slot muerto; ninguno es breaking change para integraciones externas. La suite del módulo pasa 100% estable en cada PR.

### D-PE-01 — JOIN denormalizado en `PersonaServicioComandos.MapToDto` (issue #288, PR #292)

`PersonaServicioComandos.MapToDto` emitía `TipoDocumentoCodigo: null` y `TipoDocumentoNombre: null` en las respuestas de POST y PUT, mientras que `PersonaServicioConsulta` sí proyectaba esos campos vía JOIN denormalizado contra `TiposDocumento` (PR2 del change #147). La inconsistencia estaba documentada como follow-up diferido en el archive-report del change `2026-07-20-147-tipos-documento-catalgo` (sección "Decisiones clave tomadas" #6 y "Follow-ups identificados" #1).

En el flujo web la inconsistencia era inocua porque Create/Edit redirigen a Details que vuelve a llamar a la API; **pero para integraciones externas que consuman la respuesta inmediata de POST/PUT el contrato era incorrecto** (DTO con campos siempre null para `TipoDocumentoCodigo` y `TipoDocumentoNombre`).

**Cambio aplicado:**

- Nuevo helper estático `SGV.Aplicacion.Personas.Consultas.TipoDocumentoLookupBuilder` (factor de `BuildAsync(catalogo, ct)` que devuelve `IReadOnlyDictionary<Guid, TipoDocumentoDto>`). Centraliza la query al catálogo para que ambos servicios — consulta y comandos — compartan la misma lógica de lookup y emitan los mismos campos denormalizados.
- `PersonaServicioComandos` ahora inyecta `ITipoDocumentoCatalogoConsulta` en el constructor primario; los 4 endpoints (`Crear`/`Actualizar`/`Desactivar`/`Reactivar`) proyectan el JOIN denormalizado en su respuesta. Constructor de back-compat (3 parámetros) usa stub vacío que mantiene los campos denormalizados en `null` (útil para tests que no necesitan ejercitar el JOIN).
- `PersonaServicioConsulta` refactorizado para reusar el nuevo helper (mismo resultado, factorizado).
- 4 tests nuevos en `PersonaServicioComandosTests`: POST con `TipoDocumentoId` poblado, POST con `null`, PUT manteniendo el tipo, PUT cambiando de tipo (DNI → Pasaporte). Todos verifican `TipoDocumentoCodigo`/`TipoDocumentoNombre` populated con los códigos canónicos del catálogo.

**Compatibilidad:** source-breaking NO. Wire-breaking NO (mejora del contrato del DTO; los campos ya existían en el shape, antes venían null). DB-breaking NO.

### D-PE-02 — `PersonaErrorType` expandido a 7 variantes alineadas 1-a-1 con `ErrorCategoria` (issue #289, PR #293)

`PersonaErrorType` (`SGV.Contracts.Personas.Comandos.PersonaErrorType`) tenía 3 variantes (`NotFound`/`Conflict`/`Validation`). Mientras tanto, `CargoErrorType` ya se había expandido a 7 variantes en D-CH-04 (housekeeping de Cargos, PR #287) y `PersonaSkillErrorType` cubría las 7. El `MapCategoriaToLegacyType` privado de `PersonaApiClient` colapsaba `Unauthorized`/`Forbidden`/`Transport`/`Unexpected` → `Validation`, generando el warning CS8524 endémico (mismo warning que Cargos tenía antes de D-CH-04).

**Cambio aplicado:**

- `PersonaErrorType` expandido de 3 a 7 variantes con ordinales explícitos: `NotFound=0`, `Conflict=1`, `Validation=2` (preservados), `Unauthorized=3`, `Forbidden=4`, `Transport=5`, `Unexpected=6` (nuevos). Preservar los ordinales 0/1/2 garantiza source-compat con callers que dependan de `(int)PersonaErrorType.X`.
- `SGV.Contracts.Comun.ErrorCategoriaMappers` ahora expone `ToCategoria(PersonaErrorType)` y `ToTipoPersona(ErrorCategoria)` con mapeo 1-a-1 exhaustivo. La matriz sigue el mismo patrón de `ToCategoria(CargoErrorType)` / `ToTipoCargo(ErrorCategoria)` (precedente D-CH-04).
- `MapCategoriaToLegacyType` privado del `PersonaApiClient` eliminado y reemplazado por `ErrorCategoriaMappers.ToTipoPersona(categoria)`. **Elimina el warning CS8524 endémico** del módulo Personas.
- 3 tests nuevos en `ErrorCategoriaMappersTests`: round-trip de las 7 variantes, undefined ordinal lanza `ArgumentOutOfRangeException`, ordinales históricos preservados (regresión explícita contra el ordinal).

**Compatibilidad:** source-breaking NO (ordinales preservados). Wire-breaking NO (el JSON shape del DTO `PersonaError` no cambia — campo `Categoria` ya es `ErrorCategoria`). DB-breaking NO.

### D-PE-03 — Typeahead server-side vía `GET /api/v1/personas/buscar?q={term}` (issue #290, PR #294)

`Shared/_PersonaTypeahead.cshtml` consumía `GET /api/v1/personas` completo (sin paginar) y filtraba client-side con debounce de 250ms. Asunción operativa documentada en `docs/decisiones-implementacion.md` (sección "Frontend CRUD de Personas"): dataset activo típico <500 personas (~100 KB de payload); por encima de ese umbral, latencia de carga y memoria retenida se degradaban. Follow-up documentado: agregar endpoint server-side.

**Cambio aplicado:**

- Nuevo endpoint `GET /api/v1/personas/buscar?q={term}&take={n}&soloSinUsuario={bool}` en `PersonasController` (cualquier usuario autenticado, lectura). Validación de `take >= 1` con 400 + ProblemDetails para inputs inválidos. Documentación Swagger completa (200/401/400).
- Nuevo método `IPersonaRepository.BuscarAsync` + implementación en `PersonaRepository` que reutiliza el predicado substring (`Legajo|Nombres|Apellidos|Email|NumeroDocumento`) y el anti-join contra `AspNetUsers.PersonaId` del método `QueryAsync`. **Cap defensivo de 100** resultados para evitar reproducir el problema del payload inicial.
- Nuevo método `IPersonaServicioConsulta.BuscarAsync` que delega al repo y aplica el JOIN denormalizado del catálogo `TiposDocumento` (mismo lookup que ya usa `ListarAsync`).
- `IPersonaApiClient.BuscarAsync` + implementación `PersonaApiClient.BuscarAsync` que construye la query string con `Uri.EscapeDataString` y deserializa la respuesta.
- `PersonaTypeaheadViewModel.AllPersonas` cambia de requerido a opcional (default `[]`); agregada propiedad `Take` (default 50) para configurar el límite del endpoint desde el host. Back-compat: hosts que aún populen `AllPersonas` siguen funcionando idénticamente.
- 5 tests nuevos en `PersonasControllerTests`: sin credenciales (401), autenticado (200 + propagación de q/take/soloSinUsuario), `take=0` (400), default `take=50`.
- 1 test nuevo en `PersonaServicioConsultaTests`: `FakePersonaRepository.BuscarAsync` con contador para assertions + filtro substring mirror del repo de producción.
- `IPersonaApiClientContractTests` actualizado de 12 a 13 métodos públicos (suma `BuscarAsync`).

**Compatibilidad:** source-breaking NO (`AllPersonas` opcional por back-compat). Wire-breaking NO (nuevo endpoint, no se modifican los existentes). DB-breaking NO.

**El cambio del partial `_PersonaTypeahead.cshtml` y el JS `personas-typeahead.js` para que el cliente web consuma el nuevo endpoint** queda como follow-up de un PR sub-siguiente (no incluido aquí para mantener este PR enfocado en backend + cliente HTTP + tests). Documentado como `D-PE-03b` (issue de seguimiento sugerido).

### D-PE-04 — Eliminar slot contextual Legajo sin uso (issue #291, PR #295)

`Create.cshtml.cs` y `Edit.cshtml.cs` tenían slots reservados para una advertencia contextual sobre Legajo (`ShowLegajoContextWarning`, `LegajoContextWarningMessage`) heredados de `IPersonaForm`, más una rama condicional en el partial `_Form.cshtml`. El flag siempre devolvía `false` y el mensaje `null` desde la implementación de #202. Patrón enchufable pero sin uso actual.

Recomendación adoptada: **eliminar el slot y la dependencia del partial** (no preemptivo). El módulo downstream que lo necesite nunca se materializó e Issue #202 (Legajo opcional) ya está cerrado. El principio "no crear interfaces únicamente para satisfacer una preferencia arquitectónica abstracta" aplica.

**Cambio aplicado:**

- `IPersonaForm`: eliminadas `ShowLegajoContextWarning` y `LegajoContextWarningMessage` con sus XML docs.
- `Create.cshtml.cs` y `Edit.cshtml.cs`: eliminadas las mismas properties con sus XML docs.
- `_Form.cshtml`: eliminada la rama condicional del `<span data-legajo-context-warning>` y la lógica de `mostrarWarning`/`mensajeWarning`. El campo Legajo queda plano.

**Compatibilidad:** source-breaking NO (las props NO son referenciadas por código de producción; sólo tests y slots muertos). Wire-breaking NO. DB-breaking NO.

**Si en el futuro un módulo downstream necesita la advertencia contextual**, se reintroduce con alcance concreto siguiendo el patrón de `Issue #202`: propiedad + rama en partial + activación desde el host. No preemptivo.

### Estado de release del módulo de Personas

Tras estos 4 PRs mergeados a `develop`, el módulo de Personas queda **release-ready** sin pendientes abiertos en código ni en docs:

- ✅ Build verde, 0 errores.
- ✅ Suite del módulo pasa 100% estable en cada PR (676 → 681 → 681 → 676 tests passing al merge de cada PR).
- ✅ 5 specs canónicos en `openspec/specs/` (persona-management, persona-skill-query-contract, persona-skill-web-management, persona-card-partial, persona-format-helper); 0 cambios activos en `openspec/changes/`.
- ✅ 4 issues de housekeeping cerrados (#288, #289, #290, #291); 0 issues pendientes sobre el módulo.
- ✅ CS8524 (warning endémico de switch no exhaustivo sobre `ErrorCategoria`) eliminado del módulo Personas.
- ✅ Compatibilidad preservada en los 4 cambios: source/wire/DB-breaking NO en ninguno.

### Follow-ups documentados (fuera de este change)

1. **D-PE-03b — Refactor del partial `_PersonaTypeahead.cshtml` + `personas-typeahead.js`** para consumir `GET /api/v1/personas/buscar` server-side (D-PE-). El backend está release-ready en PR #294; el cambio del front queda para un PR sub-siguiente que NO mezcle las dos mitades. Backlog del módulo.
2. **D-PE-05 (futuro)** — Cuando los enums legacy (`PersonaErrorType`, `CargoErrorType`, `PersonaSkillErrorType`, etc.) se eliminen al archivar el change `commandresult-error-taxonomy`, eliminar `MapCategoriaToLegacyType` de cada cliente y `ErrorCategoriaMappers.ToTipoX` correspondientes. Single source of truth queda en `ErrorCategoria`.
3. **D-PE-07 (operativo)** — Si `SELECT COUNT(*) FROM Personas WHERE IsActive` supera el umbral de 500 personas activas, auditar el tamaño del payload inicial que aún cargan hosts que pre-populan `PersonaTypeaheadViewModel.AllPersonas`. Los que sigan con la legacy deben migrar al endpoint `buscar`.

## 2026-08-18 — Vacantes Hardening

- **D-1**: `IUsuarioActual` ya existía (issue #202) — la decisión fue inyectarlo en los constructores primaires de `VacanteServicioComandos` y `OcupacionServicioComandos`, usando `NullUsuarioActual.Instance` para back-compat de tests pre-existentes. Composition root: `AddScoped<IUsuarioActual, UsuarioActualHttpContext>()` en `Program.cs:219`. Convenience constructor con `null` (back-compat) eliminado tras confirmar que todos los tests existentes cablean principal o usan el stub `FakeUsuarioActual`.
- **D-3**: Convención — input models de Razor Pages viven en `src/SGV.Web/Integration/<Módulo>/`, NO en `SGV.Contracts`. Cambio: `VacanteInputModel` spliteado en `VacanteCreateInputModel` (sin `EstadoVacanteId`) y `VacanteEditInputModel` (`EstadoVacanteId Guid?` con `[Required]`). El workaround `ModelState.Remove("Input.EstadoVacanteId")` en `Create.cshtml.cs` fue eliminado. Tres tests de reflexión (D-3) defienden la estructura contra drift futuro.
- **D-4**: En `CrearOcupacionCubriendoVacanteAsync`, la defensa atómica de BD (`IX_Ocupaciones_VacanteIdUnique`) tiene precedencia sobre la defensa lógica de `EsTerminal`. El código de error es `OcupacionErrorCodigo.VacanteYaCubierta` (409), no `VacanteErrorCodigo.EstadoTerminalInmutable`. Comportamiento funcional equivalente — una cobertura gana (2xx), la otra pierde (409). Desviación documentada en `apply-progress.md §Desviaciones del diseño → D-4.D.1`. Patrón alineado con `ActivePuestoIdUnique` ya existente en `VacanteServicioComandos`. Extensión de `IConstraintViolationDetector` con `GetUniqueConstraintName(DbUpdateException)` cubre MySQL 8 (backticks) y MariaDB (comillas) en el mensaje `Duplicate entry`.

## Release-ready: módulo de Ocupaciones

> Change de housekeeping: `2026-08-19-ocupaciones-housekeeping-release`. Cierra los 5 hallazgos del análisis release-readiness del módulo de Ocupaciones. No es breaking (source/wire/DB). Build verde, 0 errores.

### Contexto y trazabilidad

El módulo de Ocupaciones se consolidó a lo largo de seis cambios archivados que cubrieron el ciclo completo (modelo → reglas → wire → API → web → alignment con Vacantes):

- `2026-06-24-permitir-ocupaciones-concurrentes-y-enum-tipo-asignacion` — originó la regla de unicidad por Puesto y por Persona+Puesto (NO por Persona simple), e introdujo `TipoAsignacion` como enum persistido.
- `2026-06-26-implementa-modulo-ocupaciones` — el módulo base.
- `2026-07-13-fix-127-doc-ocupaciones-unicidad-persona` — fix de doc sobre la unicidad.
- `2026-07-28-web-ocupaciones-issue-208` — implementación web inicial, migración a `ErrorCategoria` (PRs #212, #213, #214, #215).
- `2026-07-29-web-ocupaciones-buscador-personas-issue-216` — buscador de Personas en `Create/Edit`.
- `2026-08-07-vacante-ocupacion-flow-alignment` — inversión del flujo Cubrir (issue #276, N2/N3).

### D-OC-HK-01 — Remoción del enum legacy `OcupacionErrorType`

Tras el archivado del change `commandresult-error-taxonomy` (PRs #212-#215) y los seis PRs posteriores, el enum legacy `OcupacionErrorType` (NotFound/Conflict/Validation) marcado `[Obsolete]` ya no tenía callers en el grafo. La rampa `#pragma warning disable CS0618` en `MapOcupacionStatus` sobrevivía solo como defensa en profundidad porque el servicio de comandos nunca devolvía `Categoria = ErrorCategoria.Unexpected`.

**Cambios:**
- `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionCommandResult.cs`: el enum `OcupacionErrorType` se elimina. El record `OcupacionError` queda con un único constructor `(ErrorCategoria, string, string)`. La rama del constructor primario obsoleto (con parámetro `Type` legacy) se elimina junto con los `#pragma warning disable CS0618` / `restore`.
- `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs`: se eliminan `ToCategoria(OcupacionErrorType)` y `ToTipoOcupacion(ErrorCategoria)` y los `#pragma` asociados. La sección "OcupacionErrorType" del archivo desaparece; los mappers de los demás enums (`Puesto`, `UnidadOrganizativa`, `Persona`, `PersonaSkill`, `Usuario`, `Cargo`, `Habilidad`) no se tocan.
- `src/SGV.Api/Infrastructure/Results/ApiResults.cs`: `MapOcupacionStatus` se reduce a `MapCategoria(error.Categoria)`. La rama legacy condicional `error.Categoria is ErrorCategoria.Unexpected ? MapCategoria(...) : MapCategoria(...)` y los `#pragma` se eliminan.

**Compatibilidad:** source-breaking para cualquier código que usara `OcupacionErrorType` o el constructor primario de `OcupacionError` con 4 argumentos. Verificado por grep que no quedan callers. Wire-breaking NO. DB-breaking NO.

**Verificado por:**
- `grep -r "OcupacionErrorType"` en `src/` y `tests/` retorna 0 hits post-cambio.
- `dotnet build SGV.slnx`: 0 errores, 96 warnings preexistentes (todos ajenos al change).
- Suite del módulo: ~9.500 líneas de tests en 17 archivos (Aplicación 1.891, Dominio 370, Web 4.696, Persistencia 1.535, Api 681).

### Estado de release del módulo de Ocupaciones

Tras este change, el módulo de Ocupaciones queda **release-ready** sin pendientes abiertos en código ni en docs:

- ✅ Build verde, 0 errores.
- ✅ ~9.500 líneas de tests del módulo pasan 100% estable.
- ✅ 6 specs canónicos en `openspec/specs/` (`web-ocupaciones-contrato-api`, `web-ocupaciones-crear-editar`, `web-ocupaciones-detalle`, `web-ocupaciones-listado`, `web-ocupaciones-navegacion-contextual`, `ocupacion-web-selector-persona-buscador`); 0 cambios activos en `openspec/changes/`.
- ✅ 6 cambios archivados trazables; 0 issues pendientes sobre el módulo.
- ✅ Defense-in-depth en 3 niveles (dominio → servicio → BD) verificado por tests.
- ✅ Taxonomía de error consolidada en `ErrorCategoria` (D-OC-HK-01 cierra la compat legacy).
- ✅ Compatibilidad preservada: source/wire/DB-breaking NO.

### Reglas de negocio cubiertas por capa

| Regla | Capa autoritativa | Test que la blinda |
|---|---|---|
| `FechaFin >= FechaInicio` | Dominio (`Ocupacion` ctor) + Check Constraint SQL (`CK_Ocupaciones_Fechas`) | `OcupacionTests` + `ModeloPersistenciaTests` |
| Persona activa + Puesto activo al crear/actualizar | Servicio (`OcupacionServicioComandos`) | `OcupacionServicioComandosTests.CrearAsync_*` |
| Una Ocupación activa por Puesto | BD (computed column `ActivePuestoIdUnique` STORED + UNIQUE) | `OcupacionGeneratedColumnRegressionTests` + `ModeloPersistenciaTests.Modelo_Ocupacion_ConservaUnicidadActivaPorPuesto` |
| Una Ocupación activa por Persona+Puesto | BD (computed column `ActivePersonaPuestoUnique` STORED + UNIQUE) | `ModeloPersistenciaTests.Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` |
| No doble cobertura de la misma Vacante | BD (constraint `IX_Ocupaciones_VacanteIdUnique`) + `IConstraintViolationDetector.GetUniqueConstraintName` | `OcupacionServicioComandosTests.CrearAsync_Cubrir_ViolacionConstraintUnica_MapeaVacanteYaCubierta` + `VacantesCubrirConcurrencyTests` (`[MySqlFact]`) |
| Alta directa requiere Vacante abierta (N3) | Servicio (`ExistsAbiertaByPuestoAsync`) | `OcupacionServicioComandosTests.CrearAsync_PuestoSinVacanteAbierta_DevuelveConflictoPuestoSinVacanteAbierta` |
| Cubrir Vacante crea Ocupación + transiciona a Cubierta (N2 invertido) | Servicio (`CrearOcupacionCubriendoVacanteAsync`) | `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_VacanteAbierta_CreaOcupacionYTransicionaVacanteACubierta` |
| Reactivar valida unicidad + estado de Vacante | Servicio (`ReactivarAsync`) | `OcupacionServicioComandosTests.ReactivarAsync_*` + `ReactivarAsync_VacanteCubierta_Exito` |
| Auto-edición prohibida cuando no está activa/finalizada | Dominio (`RequerirEditable`) | `OcupacionTests` |
| Solo Admin escribe | Controller (`[Authorize(Roles = RolesSgv.Administrador)]`) + PageModel + Sidenav | `OcupacionesControllerTests` + `OcupacionSidenavTests` |
| Vacante no se borra con Ocupaciones derivadas | BD (FK `Ocupaciones.VacanteId` con `OnDelete(Restrict)`) | `OcupacionVacanteIdPersistenciaTests.Borrar_VacanteConOcupacionesDerivadas_BloqueaPorRestrict` |

### Capas y archivos clave

| Capa | Tipo | Archivo | Rol |
|---|---|---|---|
| Dominio | `record class` | `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Entidad rica con invariantes, guard y Reconstitute. |
| Dominio | `enum` | `src/SGV.Dominio/Ocupaciones/TipoAsignacion.cs` | Enum contractual persistido (3 valores). |
| Servicio de comandos | `sealed class` | `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | CRUD + Cubrir (N2 invertido) + constraint violation mapping. |
| Servicio de consulta | `sealed class` | `src/SGV.Aplicacion/Ocupaciones/Consultas/OcupacionServicioConsulta.cs` | Query paginado server-side. |
| Validadores | `class` | `src/SGV.Aplicacion/Ocupaciones/Comandos/Validaciones/*RequestValidator.cs` | Shape de los requests (FluentValidation). |
| Repositorio | `interface` + impl | `src/SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionRepository.cs` + `src/SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs` | Puerto de lectura/escritura + impl EF. |
| Configuración EF | `class` | `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` | FK RESTRICT + 2 computed columns UNIQUE + check + índices de soporte. |
| Constraint detector | `class` | `src/SGV.Infraestructura/Persistencia/MySqlConstraintViolationDetector.cs` | Detecta `1062/1169/1451/1452/1644/4025` y devuelve nombre del índice violado. |
| Wire (DTOs) | `record` | `src/SGV.Contracts/Ocupaciones/Dtos/OcupacionDto.cs` | Wire contract inmutable. |
| Wire (Error) | `record` | `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionCommandResult.cs` + `OcupacionErrorCodigo` | Códigos de error (constantes) y resultado de comandos. |
| Wire (Query) | `record` | `src/SGV.Contracts/Ocupaciones/Consultas/OcupacionListQuery.cs` | Filtros + paginación server-side. |
| API controller | `sealed class` | `src/SGV.Api/Controllers/OcupacionesController.cs` | 7 endpoints REST con `[Authorize]` en escritura. |
| Cliente HTTP | `interface` + impl | `src/SGV.Web/Integration/Ocupaciones/IOcupacionApiClient.cs` + `OcupacionApiClient.cs` | Cliente tipado con `ToCommandResultAsync` + cobertura fina de errores. |
| Razor Pages | `class` | `src/SGV.Web/Pages/Organizacion/Ocupaciones/*.cshtml.cs` | Index/Details/Edit/Create + _Form/_CrossList. |
| Razor Pages transversales | `class` | `src/SGV.Web/Pages/Personas/PersonaOcupaciones.cshtml.cs` + `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs` | Listas contextuales (por Persona, por Puesto). |

### Riesgos residuales

1. **OFFSET degrada en tablas grandes** — riesgo conocido y aceptado en el repo (mitigación v2 con cursor pagination).
2. **`OcupacionTipoAsignacionMapper`** vive en `src/SGV.Aplicacion/Ocupaciones/` mapeando entre `SGV.Contracts.Ocupaciones.Enums.OcupacionTipoAsignacion` y `SGV.Dominio.Ocupaciones.TipoAsignacion`. Si en el futuro el dominio deja de tener su propio enum, el mapper puede colapsar a un cast directo. No urge.

## Módulo de Unidades Organizativas + Organigrama — defensa contra ciclos y dependencias MySQL-only

> Change: housekeeping release-readiness (`fix/unidades-organizativas-organigrama-housekeeping`).
> Cierra el drift técnico acumulado identificado en el análisis release-readiness
> y documenta decisiones que ya estaban dispersas en issues (#277, #278, #279,
> #280, #281, #282, #286) y código. No introduce comportamiento nuevo — sólo
> explicita el contrato vigente.

### D-UO-1 — Defensa en tres niveles contra ciclos jerárquicos

`UnidadOrganizativa.UnidadPadreId` puede formar un ciclo (A → B → A). El
módulo protege la integridad de la jerarquía en tres niveles
independientes, cada uno suficiente por sí solo y verificado por tests
(MySQL + unit):

| Nivel | Mecanismo | Cubre |
|---|---|---|
| **Dominio** | `UnidadOrganizativa.Actualizar` y `CambiarUnidadPadre` rechazan self-parent (`InvalidOperationException`). | El caso trivial A.Id == B.Id. |
| **Aplicación** | `UnidadOrganizativaServicioComandos.ActualizarAsync` y `CambiarUnidadPadreAsync` consultan `IUnidadOrganizativaRepository.IsDescendantAsync` con visited-set local (O(depth)). Si la cadena del candidato es descendiente del padre propuesto o revisa un nodo ya visitado, devuelven `Conflict "CicloJerarquico"` (HTTP 409). | Ciclos transitivos construidos en operaciones concurrentes. |
| **Persistencia (BD)** | Migración `20260816203122_AddTriggerAntiCiclosUnidadesOrganizativas` agrega triggers `BEFORE INSERT` y `BEFORE UPDATE` que ejecutan un CTE recursivo y disparan `SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico'` (MySQL error 1644) si detectan un ciclo. | Cualquier intento de sembrar un ciclo que pase los niveles anteriores (e.g. trigger deshabilitado, migración parcial, datos legados importados). |

El `MySqlConstraintViolationDetector` (en `SGV.Infraestructura.Persistencia`)
reconoce `1644` como constraint violation. El servicio de comandos lo
captura en `MapConstraintViolation` (helper privado) y mapea el
`InnerException.Message` que contiene `CicloJerarquico` al código
canónico `UnidadOrganizativaErrorCodigos.CicloJerarquico`. **El contrato
HTTP es siempre 409, nunca 500, incluso si la BD detecta el ciclo.**

### D-UO-2 — Límite de profundidad implícito en el trigger anti-ciclos

El CTE recursivo del trigger (`Migraciones/20260816203122_...`) usa la
condición `depth < 32` para acotar la recursión (límite de MySQL para
CTEs recursivas). En una jerarquía con más de 32 niveles, el trigger
cortaría antes de cerrar el ciclo y el `INSERT`/`UPDATE` pasaría. En la
práctica este límite no se alcanza en una organización real, pero el
número está implícito en el código de la migración y debe revisarse si
se migra a otro motor o se sube el límite de profundidad.

### D-UO-3 — Dependencia MySQL-only de la defensa anti-ciclos

La defensa anti-ciclos vive en tres niveles, pero el **trigger anti-ciclos
es MySQL/MariaDB only** (no portable a SQL Server, PostgreSQL, etc). El
nivel de aplicación (visited-set en `IsDescendantAsync` + chequeo en el
servicio) es portable y protege el camino crítico. Sin embargo:

- Si se levanta un entorno con `EnsureCreated()` en lugar de
  `Database.Migrate()`, los triggers NO se crean y solo quedan los
  niveles de aplicación y dominio.
- Si los triggers se deshabilitan manualmente para sembrar datos
  legados, el `DiagnosticoJerarquiaService` (exposado vía
  `GET /api/v1/unidades-organizativas/diagnostico-jerarquia`,
  rol Administrador) reporta los ciclos pre-existentes para que el
  operador pueda corregirlos antes de re-habilitar los triggers.

El check constraint `CK_UnidadesOrganizativas_UnidadPadre`
(`UnidadPadreId IS NULL OR UnidadPadreId <> Id`) sí vive en el modelo EF
y cubre el auto-parent, pero NO cubre ciclos transitivos. La defensa
contra ciclos transitivos sin los triggers requiere confiar en el nivel
de aplicación.

### D-UO-4 — Códigos de error centralizados en `UnidadOrganizativaErrorCodigos`

Los códigos canónicos del contrato wire viven en
`src/SGV.Contracts/Organizacion/Comandos/UnidadOrganizativaErrorCodigos.cs`
como `public const string`. Antes del housekeeping release-readiness,
estos códigos eran magic strings repetidos en 12 sitios del servicio
de comandos; un typo no rompía compilación. El módulo ahora importa la
constante explícitamente. Los códigos vigentes son:

`DatosInvalidos`, `UnidadNoEncontrada`, `UnidadPadreNoEncontrada`,
`TipoUnidadNoExiste`, `CodigoDuplicado`, `CicloJerarquico`,
`UnidadConHijasActivas`, `UnidadConPuestosActivos`, `PadreInactivo`,
`ReactivacionInvalida`, `RestriccionDeIntegridad`.

El valor `CicloJerarquico` es además el `MESSAGE_TEXT` literal del
SIGNAL del trigger anti-ciclos (constante `TriggerMensajeCiclo` en la
migración `20260816203122`). Si se cambia el valor del trigger, hay
que actualizar tanto la constante de la migración como
`UnidadOrganizativaErrorCodigos.CicloJerarquico` simultáneamente.

## Refresh tokens con rotación single-use y revocación familiar (change `implementa-refresh-tokens`)

> Change: `implementa-refresh-tokens`. Artefactos SDD en
> `openspec/changes/feature/implementar-refresh-tokens` (proposal,
> spec, design, tasks). Chain strategy: `stacked-to-develop` con 4 PRs
> encadenados. PR1a (#306, merged), PR1b (#307, merged), PR2a (#308,
> merged), PR3 (#309, merged), PR4 (#310, este documento):
> observabilidad + cierre del change. Este documento resume la decisión
> técnica y consolidada el settle del change.

### Contexto

La ventana de exposición de 60 minutos del JWT sin revocación
server-side quedó aceptada en el issue #97 (archivado) como decisión
de diseño. Este change reemplaza esa decisión: introduce refresh
tokens con persistencia MySQL, rotación single-use agrupada por
`FamilyId`, detección de replay que revoca la familia completa y
revocación server-side en logout. Sustituye el modelo vigente
(«token robado permanece válido hasta su expiración natural») por
uno signal-of-compromise: ante replay, todos los dispositivos del
usuario se desconectan.

### D-RT-1 — Persistencia MySQL con `RefreshTokens` y hash determinístico

`RefreshTokenEntity` vive en
`src/SGV.Infraestructura/Persistencia/Entidades/RefreshTokenEntity.cs`
y hereda `EntityBase` (PK `Guid Id`) — no PK `BIGINT UNSIGNED`
porque `AuditoriaSaveChangesInterceptor` filtra `e.Entity is
EntityBase`. La tabla `RefreshTokens` se crea vía migración
`AddRefreshTokens` con:

- `Id: CHAR(36)` PK Guid.
- `UserId: VARCHAR(450)` FK a `AspNetUsers.Id` con `ON DELETE CASCADE`.
- `FamilyId: CHAR(36)` Guid de la familia (nuevo por login).
- `TokenHash: VARCHAR(64)` SHA-256 hex (lowercase) del token plain.
- `CreatedAt`, `ExpiresAt`, `LastUsedAt`, `RevokedAt`, `ReplacedById`.
- Índices: `UNIQUE(IX_RefreshTokens_TokenHash)`, `IX_RefreshTokens_UserId`,
  `IX_RefreshTokens_FamilyId`.
- Charset `utf8mb4_0900_ai_ci` en columnas string; `datetime(6)` en
  timestamps.

`RefreshTokenHashing.ComputeSha256Hex(token)` vive en
`src/SGV.Aplicacion/Seguridad/Servicios/` y aplica
`SHA256.HashData(Encoding.UTF8.GetBytes(token))` con output hex
lowercase de 64 chars. Testeado por `RefreshTokenHasherTests`
(determinístico, regex `^[0-9a-f]{64}$`, entropía de `Generate()`).

El plain token se genera con `RandomNumberGenerator.GetBytes(32)` →
Base64Url sin padding (`TokenBytes = 32`, 256 bits de entropía).
Solo viaja en memoria; nunca se persiste ni aparece en logs.

### D-RT-2 — Auditoría con `EsCampoSensible` por convención de nombre

`AuditoriaSaveChangesInterceptor` no se modificó. `EsCampoSensible`
ya filtra cualquier propiedad cuyo nombre contenga `Token`,
`Password` o `Stamp`. Por eso:

- `TokenHash` queda excluido automáticamente (no aparece en
  `NewValuesJson`).
- La columna de relación se llama `ReplacedById` (no
  `ReplacedByTokenId`) — el nombre original caía en el filtro y
  excluía incorrectamente este campo de la auditoría.

Verificado por `RefreshTokenAuditoriaTests`: tras `AddAsync` +
`SaveChangesAsync`, el `NewValuesJson` contiene `FamilyId`, `UserId`,
`ExpiresAt`, `ReplacedById` y NO contiene `TokenHash`.

### D-RT-3 — API body-based, `SGV.Web` único emisor de `sgv.rt`

`SGV.Web` consume `SGV.Api` server-to-server vía `HttpClient`. Un
`Set-Cookie` emitido por la API muere dentro del proceso —
nunca llega al browser. Por eso:

- La API es **body-based**: `RefreshRequest(string RefreshToken)`.
- `SGV.Web` es el **único emisor** de la cookie `sgv.rt`.

El refresh cookie se gestiona íntegramente en
`src/SGV.Web/Integration/Auth/RefreshTokenCookieAccessor.cs` con
atributos `HttpOnly=true`, `SameSite=Lax`, `Path=/`. El flag
`Secure` se calcula igual que `Program.cs:54` para `sgv.auth`:
`_environment.IsDevelopment() ? isHttps : true`. La expiración
coincide con `RefreshTokenExpiresAt`.

### D-RT-4 — Rotación single-use y revocación familiar

Cada login emite un `FamilyId` Guid nuevo. El `RefreshTokenServicio`
(`src/SGV.Infraestructura/Seguridad/RefreshTokenServicio.cs`)
orquesta la rotación:

1. `RefreshAsync(plain)` calcula el hash, llama
   `IRefreshTokenRepository.TryConsumeAsync(hash, replacementId, now)`
   — un `ExecuteUpdateAsync` atómico con `WHERE TokenHash=@h AND
   RevokedAt IS NULL AND ExpiresAt > @now`.
2. Si la fila no estaba activa: distingue `Invalid` (fila no existe),
   `Expired` (expirada, **NO** se revoca la familia per
   REQ-AUTH-REFRESH-2) y `ReplayDetected` (fila revocada → revoca
   toda la familia per REQ-AUTH-REFRESH-3).
3. Si la fila era current: emite un nuevo access JWT, inserta T2
   con la misma `FamilyId`, persiste, retorna `RefreshResult.Success`.

La concurrencia se resuelve por el `UPDATE` condicional atómico, no
por `SELECT FOR UPDATE`. Si dos requests presentan T1 en paralelo,
uno gana (`Success`) y el otro pierde (`ReplayDetected` +
revocación familiar). Ver `REQ-RTM-CONCURRENCY-1` y
`RefreshTokenRepositoryConcurrentTests`.

**Trade-off documentado (R8 del design):** ante replay, el usuario
legítimo también queda desconectado. Es la señal de compromiso
buscada (OAuth RFC 6819). El ganador de la carrera con doble-pestaña
recibe `200`; sólo el perdedor gatilla la revocación.

### D-RT-5 — `RevokeAsync` cubre TODAS las familias del usuario

`RevokeAsync(userId, plainToken?)` revoca **todas** las familias
activas del usuario, no sólo la de la cookie presentada. El token
presentado sólo enriquece la auditoría cuando coincide con un
`UserId` conocido. Esto convierte el logout en un sign-out global
(superset de REQ-AUTH-LOGOUT-1): nadie con un refresh válido
emitido por el mismo `UserId` sobrevive a la revocación.

Si el usuario no tiene refresh tokens activos (sesión legacy, pre
PR1a), `RevokeAsync` es un no-op gracioso sin entrada de auditoría
(REQ-AUTH-LOGOUT-1, escenario 2).

### D-RT-6 — Lifetime absoluto 14 días, sin sliding window

`RefreshTokenOptions.RefreshTokenLifetimeDays = 14` (default) en
`appsettings.json`. `ExpiresAt = CreatedAt + 14 días` con precisión
`DATETIME(6)`. NO se implementa sliding window en v1 — la actividad
silenciosa complica debugging y auditoría. Diferido a v2.

### D-RT-7 — Rate limiting independiente en `/api/v1/auth/refresh`

Política `Refresh` en `AddRateLimiter` de `Program.cs`, partition
key por IP, ventana configurable (`RefreshToken:RateLimitPermitLimit
= 20`, `RateLimitWindowMinutes = 15`). `[EnableRateLimiting("Refresh")]`
se aplica sólo al endpoint `/refresh` — el login usa su propia
partición. Tras agotar la cuota de `Refresh`, un `POST /api/v1/auth/login`
responde normalmente (ver `AuthRefreshRateLimitTests`).

### D-RT-8 — Observabilidad con `ILogger` estructurado (PR4)

`RefreshTokenServicio` emite eventos de log estructurados a través
de `ILogger<RefreshTokenServicio>`. La observabilidad existe al
margen de la auditoría: las trazas de auditoría viven en
`Auditorias` y son inmutables; los logs son efímeros y operan-dor
oriented. Cobertura:

| Evento | Nivel | Trigger | Campos |
|---|---|---|---|
| `RefreshSuccess` | `Information` | Rotación exitosa | `UserId`, `FamilyId`, `NewTokenExpiresAt` |
| `RefreshFailure` | `Warning` | `InvalidToken` o `ExpiredToken` | `Error`, `UserId` (si conocido), `FamilyId` (si conocido) |
| `RefreshReplayDetected` | `Error` | Replay o carrera perdida | `UserId`, `FamilyId`, `AffectedFamilySize` |
| `FamilyRevocation` | `Information` | Logout (revocación familiar) | `UserId`, `RevokedTokensCount`, `FamilyId` |

**Privacidad por construcción:** ningún log incluye el token plain
ni su hash. Verificado por `RefreshTokenServicioLoggingTests`.
`Logs_NeverContainPlainTokenOrHash` corre la traza completa
(success + replay + invalid + expired + logout) y assertea que
plain/hash no aparecen en ningún Message.

### D-RT-9 — `AllowRefresh = false` se mantiene

El ticket de cookie auth actual (`AuthSessionFactory.CreateProperties`)
tiene `AllowRefresh = false`. Esto **no se toca**. La rotación
opera vía la cookie separada `sgv.rt`, no dentro del ticket de
`sgv.auth`. Refactor ese flag es un cambio de contrato ortogonal al
refresh token.

### D-RT-10 — Ruta `api/v1/auth/...` sin `v2` planificado

`AuthApiRoutes.Base = "api/v1/auth"` se mantiene (8 callers, 3 tests
de estabilidad). Toda mención de `/api/auth/...` en la spec o en
los PRs se lee como `/api/v1/auth/...`. No hay un `v2` planificado;
el prefijo `v1` queda vigente por coherencia con el resto de la API.

### D-RT-11 — Decisiones heredadas del design (no re-litigadas)

Estos son los trade-offs explícitos del design #1866/#1867 que se
mantienen cerrados durante la implementación:

- **PK Guid heredando `EntityBase`**. Razón: `Audit_*` audita
  `e.Entity is EntityBase`; un PK `long` no podría auditarse.
- **`RefreshRequest(string RefreshToken)` body-based**. Razón: la
  API no emite cookies; un `Set-Cookie` nunca llegaría al browser.
- **`ReplacedById` (no `ReplacedByTokenId`)**. Razón: `EsCampoSensible`
  filtra nombres con substring `Token`.
- **`AuditoriaSaveChangesInterceptor` intacto**. Razón: ya cubre
  `TokenHash`, `ReplacedById`, etc. Refactor a `[NotAudited]`
  declarativo queda diferido.
- **Concurrencia por UPDATE condicional atómico**, no por
  `SELECT FOR UPDATE`. Razón: más simple, suficiente para el volumen
  de SGV, unit-testeable sin `SgvDbContext`.
- **`RefreshTokenLifetimeDays = 14` absolute**, sin sliding.
  Decisión #1864.
- **Una familia por login** (no por dispositivo). Decisión #1864.
- **No auto-refresh en cada API call**. Decisión #1864.

### Capas y archivos clave

| Capa | Tipo | Archivo | Rol |
|---|---|---|---|
| Wire | `record` | `src/SGV.Contracts/Seguridad/Usuarios/RefreshContracts.cs` | `RefreshRequest`, `RefreshResponse`, `LogoutRequest`, `LogoutResponse`. |
| Wire | `record` | `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | `LoginResponse` extendido con `RefreshToken`/`RefreshTokenExpiresAt` nullable con default. |
| Wire | `class` | `src/SGV.Contracts/Seguridad/RefreshTokenOptions.cs` | `SectionName="RefreshToken"`, defaults de lifetime y rate limit. |
| Wire | `static class` | `src/SGV.Contracts/Auth/AuthApiRoutes.cs` | `Refresh`, `Logout`, `RefreshPolicyName`. |
| Puerto | `interface` | `src/SGV.Aplicacion/Seguridad/Contratos/IRefreshTokenServicio.cs` | `IssueAsync`, `RefreshAsync`, `RevokeAsync` + `RefreshOutcome` + `RefreshResult`. |
| Puerto | `interface` | `src/SGV.Aplicacion/Seguridad/Contratos/IRefreshTokenRepository.cs` | `AddAsync`, `GetByHashAsync`, `TryConsumeAsync`, `RevokeFamilyAsync`, `RevokeAllForUserAsync`. |
| Puerto | `interface` | `src/SGV.Aplicacion/Seguridad/Contratos/IAccessTokenIssuer.cs` | Claim set extraído de `AuthServicio`. |
| Hashing | `static class` | `src/SGV.Aplicacion/Seguridad/Servicios/RefreshTokenHashing.cs` | `ComputeSha256Hex(plain)` 64 hex lowercase. |
| Persistence | `class` | `src/SGV.Infraestructura/Persistencia/Entidades/RefreshTokenEntity.cs` | POCO heredando `EntityBase`. |
| Persistence | `class` | `src/SGV.Infraestructura/Persistencia/Configuraciones/RefreshTokenConfiguracion.cs` | Fluent config: charset, indices, FK. |
| Persistence | `class` | `src/SGV.Infraestructura/Seguridad/Repositorios/RefreshTokenRepository.cs` | `ExecuteUpdateAsync` atómico. |
| Service | `sealed class` | `src/SGV.Infraestructura/Seguridad/RefreshTokenServicio.cs` | Orquesta rotación, replay, auditoría explícita, logging. |
| Service | `sealed class` | `src/SGV.Infraestructura/Seguridad/JwtAccessTokenIssuer.cs` | Reuso del claim set del access token. |
| API | `controller` | `src/SGV.Api/Controllers/AuthController.cs` | `Refresh` (`[AllowAnonymous] [EnableRateLimiting("Refresh")]`), `Logout` (`[Authorize]`). |
| API | `Program.cs` | `AddOptions<RefreshTokenOptions>().BindConfiguration(...).ValidateOnStart()`, `AddRateLimiter` con policy `Refresh`, DI de `IRefreshTokenServicio`. |
| Web | `class` | `src/SGV.Web/Integration/Auth/RefreshTokenCookieAccessor.cs` | Cookie `sgv.rt` con `Secure` derivado de `IWebHostEnvironment`. |
| Web | `class` | `src/SGV.Web/Integration/Auth/AuthApiClient.cs` | `RefreshAsync` (anonymous) + `LogoutAsync` (bearer). |
| Web | `PageModel` | `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` | Persiste `sgv.rt` tras `SignInAsync`. |
| Web | `PageModel` | `src/SGV.Web/Pages/Auth/Logout.cshtml.cs` | POST fail-open: API logout → `SignOutAsync` → `Delete(sgv.rt)`. |
| Tests | `[Fact]` | `tests/SGV.Tests/Seguridad/RefreshTokenServicioTests.cs` | 12 unit (rotación, replay, expiración, familia, concurrencia). |
| Tests | `[Fact]` | `tests/SGV.Tests/Seguridad/RefreshTokenServicioLoggingTests.cs` | 6 unit (PR4): success/failure/replay/revoke + privacidad del log. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Persistencia/RefreshTokensMigrationTests.cs` | Schema `RefreshTokens`: columnas, índices, FK. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Persistencia/RefreshTokenRepositoryTests.cs` | Insert → get → rotate → family revoke. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Persistencia/RefreshTokenRepositoryConcurrentTests.cs` | `Task.WhenAll` con dos `SgvDbContext` independientes. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Persistencia/RefreshTokenAuditoriaTests.cs` | `EsCampoSensible` excluye `TokenHash`. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Api/AuthRefreshEndpointTests.cs` | 7 endpoint tests (200, 401, replay intact). |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Api/AuthRefreshReplayTests.cs` | Replay → 401 + family revoked + audit. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Api/AuthRefreshChainTests.cs` | Login → refresh → refresh → logout → 401. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Api/AuthLogoutEndpointTests.cs` | Logout con/sin refresh en body. |
| Tests | `[MySqlFact]` | `tests/SGV.Tests/Api/AuthRefreshRateLimitTests.cs` | `PermitLimit + 1` → 429; login no limitado. |
| Tests | `[Fact]` | `tests/SGV.Tests/Web/Auth/RefreshTokenCookieAccessorTests.cs` | 11 unit: Development/Production Secure, Delete. |
| Tests | `[Fact]` | `tests/SGV.Tests/Web/Auth/AuthApiClientRefreshTests.cs` | 9 client tests con `DelegatingHandler` captor. |
| Tests | smoke | `tests/SGV.Tests/Web/Auth/SignInCookieIssuanceTests.cs` | `sgv.rt` emitida/omitida según `RefreshToken`. |
| Tests | smoke | `tests/SGV.Tests/Web/Auth/LogoutCookieClearingTests.cs` | Logout API failure → fail-open local. |
| Docs | `infra` | `docs/migracion-inicial-sgv.sql` | Script idempotente release-ready (cubre las 21 migraciones EF Core, incluida `AddRefreshTokens`); revisión manual del DDL. |

### Estado de release del change

Al cierre de PR4, el change `implementa-refresh-tokens` queda
**release-ready** en lo que respecta al scope original:

- ✅ Build verde, 0 errores.
- ✅ Suite de refresh tokens focal: 100% estable con MySQL local
  disponible.
- ✅ 4 PRs merged a `develop` (PR1a #306, PR1b #307, PR2a #308,
  PR3 #309, más PR4 #310).
- ✅ Suite global: 3764 passed / 2 failed (pre-existing
  `VacantesCubrirConcurrencyTests` issue #260, sin relación con
  este change).
- ✅ Auditoría persiste `RotacionExitosa`, `RevocarFamilia`, `Logout`
  con `FamilyId` + `UserId` (sin `TokenHash` por D-RT-2).
- ✅ Observabilidad: 4 eventos de log estructurados (PR4) cubriendo
  success, failure, replay, family revocation.
- ✅ Rate limiting independiente en `/api/v1/auth/refresh` (PR4).
- ✅ Documentación consolidada en este apartado + `docs/migracion-inicial-sgv.sql` (incluye la migración `AddRefreshTokens` en su set de 21).

### Riesgos residuales

1. **Sin CI activo** `#1`: `.github/workflows/ci.yml` está activado
   según `AGENTS.md` pero la cobertura de `[MySqlFact]` se skipea
   silenciosamente sin MySQL local. Mitigación: cada PR reporta
   `dotnet test SGV.slnx` con conteo de skipped/failed.
2. **Crecimiento sin límite de `RefreshTokens` (R9)**: no hay barrido
   de expirados en v1. ~2 filas por sesión de usuario por día al
   volumen actual. Job de purga (`DELETE WHERE ExpiresAt < NOW() -
   INTERVAL 30 DAY`) diferido a un change propio.
3. **Doble-pestaña detectada como replay**: dos pestañas refrescando
   en simultáneo pueden gatillar la revocación familiar. Mitigación
   operativa: el ganador de la carrera recibe `200`; sólo el perdedor
   dispara la revocación. Documentar en onboarding de usuario.
4. **`RefreshTokenHashing` y `RefreshTokenServicio` separados
   (PR1a vs PR2a)**: la separación refleja la decisión de PR1a de
   no exponer `Generate()` al exterior. El servicio emite su propio
   token con `GenerarToken()` privado. Si en el futuro se quiere
   unificar, refactor explícito a un `IRefreshTokenGenerator`.
5. **No-op de `RevokeAsync` sin audit trail**: cuando el usuario no
   tiene refresh tokens activos, no se registra entry en
   `Auditorias`. Razón: el logout sigue siendo exitoso para
   sesiones legacy (cookie `sgv.auth`); no hay nada que auditar.
   Mitigación aceptable: el cookie auth ya registra su propia
   expiración al `SignOutAsync`.

### Fuera de alcance del v1 (deferido a v2+)

- **Sliding window** del refresh token (D-RT-6): el lifetime absoluto
  es 14 días sin extensión por uso.
- **Per-device families**: actualmente una familia por login. Si en
  el futuro se quiere mantener sesiones múltiples simultáneas por
  usuario, el diseño de la familia cambia (no es un simple parámetro).
- **Auto-refresh en cada API call**: cuando el access token está
  cerca de expirar, no se refresca automáticamente. El refresh
  ocurre sólo en endpoints explícitos (`/api/v1/auth/refresh`,
  login). Decisión #1864.
- **Refactor a `[NotAudited]` declarativo**: la sensibilidad de
  campos en la auditoría sigue viviendo como convención de nombre
  en `EsCampoSensible`. Refactor a atributo declarativo deferred
  por riesgo de regresión.
- **Job de barrido de expirados**: ver R9.
- **Métrica con `System.Diagnostics.Metrics`**: el proyecto no
  cuenta con infraestructura de métricas (no hay `IMeterFactory`
  registrado). Diferido a un change que introduzca la medición
  end-to-end. Los logs estructurados de PR4 son la única señal
  operativa vigente.

## Módulo Habilidades — asimetrías residuales del tech debt cleanup (issue #311)

> Issue: `[Habilidades] Asimetrías residuales del tech debt cleanup
> (issue #298 out-of-scope)`. Cierra las dos asimetrías que PR #299
> (`refactor(habilidad): tech debt cleanup`) dejó explícitamente fuera
> de scope. Cero cambios de persistencia, cero cambios de contrato
> HTTP público, cero migraciones: es housekeeping de código puro.

### Contexto y problema

El PR #299 cerró 4 housekeeping items dentro del scope del
release-readiness del módulo Habilidades. El análisis original del
issue #298 había mapeado dos asimetrías residuales que quedaron
documentadas como fuera de scope:

- **(a)** `HabilidadRepository` exponía la navegación `Categoria`
  de forma asimétrica entre paths. La hipótesis del issue era que
  las inconsistencias producían N+1 en alguna vista, pero la
  verificación contra el código actual al cierre de #311 muestra un
  cuadro más matizado (ver § D-311-1 abajo): los paths que ya
  cubrían el Index y los listados paginados (`QueryAsync`,
  `ListAllAsync`, `GetByIdAsync`) sí cargaban la navegación, por
  lo que el N+1 hipotético **no se reproduce en el código actual**.
  Lo que sí existía era un contrato de exposición desigual: tres
  paths materiales no incluían `Categoria`, dejando a cualquier
  consumidor aguas arriba sin garantías uniformes sobre la
  navegación.
- **(b)** `PersonaHabilidad` vivía en `src/SGV.Dominio/Personas/`
  y `CargoHabilidad` en `src/SGV.Dominio/Habilidades/`. Ambos son
  join entities entre una raíz y `Habilidad`; la asimetría rompía
  el patrón "bounded context por carpeta" que el resto del repo
  respeta y complicaba búsquedas globales (`grep "Habilidad"` en
  `SGV.Dominio` daba dos raíces).

Ninguno de los dos hallazgos era bloqueante, pero la deuda se notaba
a medida que casos de uso cruzados entre Persona y Cargo se sumaban.

### D-311-1 — Contrato único de exposición de la navegación `Categoria` (issue #311, asimetría a)

#### Hipótesis de la issue vs. evidencia al cierre

El issue #311 planteaba que la UI hacía N+1 para mostrar el nombre
de la categoría al editar o ver detalle. **Esa hipótesis no se
reproduce contra el código actual**: la verificación al cierre
muestra que

- `HabilidadRepository.QueryAsync` (camino del Index paginado)
  ya cargaba `Include(h => h.Categoria)` antes del Skip/Take
  desde el change `migrar-campo-categoria-habilidades-a-tabla`.
- `HabilidadRepository.ListAllAsync` y `GetByIdAsync` también lo
  hacían.
- `HabilidadServicioConsulta.MapToDto`
  (`src/SGV.Aplicacion/Habilidades/Consultas/HabilidadServicioConsulta.cs:52`)
  proyecta `entity.Categoria?.Nombre` sobre el wire.
- `HabilidadServicioComandos.MapToDto`
  (`src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs:228`)
  proyecta `habilidad.Categoria?.Nombre` sobre el wire en el camino
  de éxito (`CrearAsync` / `ActualizarAsync` / `ReactivarAsync`
  retornan `HabilidadCommandResult.Success(MapToDto(habilidad))`).
  La falla por categoría inexistente usa un código y mensaje propios
  (`HabilidadErrorType.CategoriaInexistente` /
  `HabilidadErrorCodes.CategoriaHabilidadNoExiste` /
  `CategoriaInexistenteMessage = "La categoría indicada no existe."`,
  vía `FailureCategoriaInexistente()`) y nunca llega a proyectar la
  navegación.

Por lo tanto, **el Index y los listados vigentes ya hacían eager
loading** y no había N+1 observable al momento de cerrar #311. La
asimetría real era de **contrato**: tres paths materiales
(`GetByIdForUpdateAsync`, `GetByIdIncludingDeletedAsync`,
`UpdateAsync`) no incluían la navegación, lo que significaba que
un consumidor que proyectara `CategoriaNombre` no podía asumir un
contrato uniforme entre paths — debía conocer qué método estaba
invocando para saber si la navegación llegaba poblada.

#### Decisión: contrato único de exposición de la navegación

`HabilidadRepository` ahora carga `HabilidadEntity.Categoria` con
`Include(h => h.Categoria)` en todos los paths públicos que
materializan un `Habilidad` de dominio: `GetByIdAsync`,
`GetByIdForUpdateAsync`, `GetByIdIncludingDeletedAsync`,
`ListAllAsync`, `QueryAsync` y `UpdateAsync`. La excepción explícita
es `ExistsCategoriaAsync`, cuyo contrato es verificar la existencia
de un id de catálogo y no devuelve un agregado.

| Path | Pre-#311 | Post-#311 |
|---|---|---|
| `GetByIdAsync` | `Include` | `Include` (sin cambio) |
| `ListAllAsync` | `Include` | `Include` (sin cambio) |
| `QueryAsync` | `Include` después del sort | `Include` después del sort (sin cambio) |
| `GetByIdForUpdateAsync` | sin `Include` | `Include` (uniformiza el contrato) |
| `GetByIdIncludingDeletedAsync` | sin `Include` | `Include` (uniformiza el contrato) |
| `UpdateAsync` | sin `Include` | `Include` (uniformiza el contrato) |

El delta observable es la unificación del contrato: cualquier
consumidor que proyecte `CategoriaNombre` puede tratar a todos los
paths por igual. No se elimina N+1 existente (no había); se
garantiza que ninguno de los seis paths materiales introduzca
uno accidentalmente.

El contrato queda documentado a nivel de clase en
`src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs`
con un `<summary>` que explica:

- Qué paths materiales exponen la navegación y por qué.
- Por qué la navegación puede ser `null` cuando `CategoriaId` es
  `null` (la FK es opcional).
- Cuál es la excepción explícita (`ExistsCategoriaAsync`).

Cada uno de los 3 paths modificados gana su propio XMLDoc que
referencia el contrato de clase y deja explícito el filtro
aplicado. Se preserva la semántica existente de `UpdateAsync`
(sigue siendo patch de scalar fields vía
`DomainToPersistenceMapper.UpdateEntity`); el `Include` es aditivo.

**Por qué incluir y no quitar**: el dominio modela `Categoria` como
navegación opcional accesible (`Habilidad.Categoria` está
`public CategoriaHabilidad? Categoria { get; private set; }` y se
hidrata en `Reconstitute`). El servicio de consulta y el de
comandos proyectan `CategoriaNombre`. La alternativa de ocultar
la navegación en todos los paths rompería REQ-CAT-07 y obligaría
a un redesign del contrato `HabilidadDto`.

**Impacto de performance**: el cambio introduce un `LEFT JOIN`
extra contra `CategoriasHabilidad` (4 filas seed) en los 3 paths
modificados. Sin diferencias materiales — el catálogo es inmutable
y la PK cubre el JOIN. En `QueryAsync` el `Include` ya existía;
en `UpdateAsync` la carga tracked es comparable al `GetByIdAsync`
que ya lo hacía.

**Verificación negativa**: el test
`HabilidadRepositoryTests.UpdateAsync_CargaCategoriaEnContexto_YActualizaScalarFields`
fue diseñado con `ChangeTracker.Clear()` + `CategoriaId` invariante
para que sea sensible a la presencia del `Include` en
`UpdateAsync`; quitar el `Include` lo hace fallar en
`Assert.NotNull(tracked.Categoria)`. Eso confirma que el delta
está protegido.

### D-311-2 — `PersonaHabilidad` se muda a `SGV.Dominio.Habilidades` (issue #311, asimetría b)

Se eligió la **Opción A** recomendada por el issue: mover
`PersonaHabilidad` desde `src/SGV.Dominio/Personas/PersonaHabilidad.cs`
a `src/SGV.Dominio/Habilidades/PersonaHabilidad.cs` para que ambos
join entities (`PersonaHabilidad` y `CargoHabilidad`) vivan con
su agregado (`Habilidad`).

**Por qué Habilidades y no Organizacion**: el agregado conceptual
del join es la habilidad. `Habilidad` vive en `SGV.Dominio.Habilidades`,
y tanto `Persona` (que tiene `List<PersonaHabilidad>` como colección
de skills poseídas) como `Cargo` (que tiene una relación de skills
requeridas vía `CargoHabilidad`) consumen `Habilidad` desde el
módulo que la define. Mover `CargoHabilidad` a `Organizacion`
habría dejado `PersonaHabilidad` huérfana del módulo que define la
entidad raíz — exactamente el patrón que estamos cerrando.

**Cambios aplicados** (mínimos y verificables):

- `src/SGV.Dominio/Personas/PersonaHabilidad.cs` → movido a
  `src/SGV.Dominio/Habilidades/PersonaHabilidad.cs` con
  `namespace SGV.Dominio.Habilidades`. La `using SGV.Dominio.Personas;`
  se agrega al archivo movido para resolver la navegación `Persona`.
- El archivo movido gana un `<summary>` que documenta el motivo
  del cambio (issue #311, asimetría residual de #298) y un
  `<remarks>` que apunta a este apartado de
  `docs/decisiones-implementacion.md`.
- Callers actualizados para agregar `using SGV.Dominio.Habilidades;`:
  - `src/SGV.Dominio/Personas/Persona.cs` (colección `_habilidades`
    y `AgregarHabilidad`).
  - `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs`
    (construcción de `new PersonaHabilidad(...)`).
  - `src/SGV.Aplicacion/Personas/Consultas/IPersonaSkillRepository.cs`
    (interface).
  - `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaSkillRepository.cs`
    (implementación).
- Los mappers `PersistenceToDomainMapper` y
  `DomainToPersistenceMapper` ya importaban
  `using SGV.Dominio.Habilidades;` por `CargoHabilidad` /
  `Habilidad`, así que no requieren cambios.
- Los archivos de migración `.Designer.cs` contienen el string
  `"SGV.Dominio.Personas.PersonaHabilidad"` como identificador
  opaco del snapshot del modelo EF Core — no es una referencia
  CLR, sólo se usa como key en el diff entre migraciones. **No se
  tocan** porque son artefactos históricos congelados que se
  regenerarían únicamente al agregar una migración nueva.

**Compatibilidad preservada**:

- Cero cambios en `PersonaHabilidadEntity`
  (`src/SGV.Infraestructura/Persistencia/Entidades/`), que sigue
  en su namespace.
- Cero cambios en la tabla `PersonaHabilidades`, en su FK, ni en
  el índice único `IX_PersonaHabilidades_PersonaId_HabilidadId`.
- Cero cambios en `PersonaSkillRepository` ni en
  `IPersonaSkillRepository` a nivel de comportamiento.
- Los tests existentes `PersonaSkillServicioTests` (8 tests) y
  `PersonaSkillRepositoryTests` (9 tests) ejercen
  `new PersonaHabilidad(...)`, la `List<PersonaHabilidad>` del
  fake y el ciclo Upsert/Delete/Query completo, y siguen pasando
  sin cambios.

### Criterio de mantenimiento

- **Si se agrega un nuevo path de materialización en
  `HabilidadRepository`** (ej. un futuro `GetByCodigoAsync`,
  `ReactivateAsync` con re-fetch, etc.): debe cargar la navegación
  `Categoria` salvo que su contrato sea explícitamente scalar-only
  (análogo a `ExistsCategoriaAsync`). El `<summary>` de la clase
  es la referencia canónica.
- **Si se agrega un nuevo join entity hacia `Habilidad`** (ej.
  `ProyectoHabilidad`, `EquipoHabilidad`): vive en
  `SGV.Dominio.Habilidades`, junto a `CargoHabilidad` y
  `PersonaHabilidad`, salvo que el agregado raíz tenga un bounded
  context distinto y bien definido que justifique lo contrario
  (en cuyo caso se documenta la excepción en este archivo).
- **Si una migración futura re-numera snapshots**: los
  `.Designer.cs` viejos siguen conteniendo
  `"SGV.Dominio.Personas.PersonaHabilidad"` como string opaco.
  EF Core lo trata como key estable y no rompe el pipeline; no
  requieren reescritura retroactiva.
