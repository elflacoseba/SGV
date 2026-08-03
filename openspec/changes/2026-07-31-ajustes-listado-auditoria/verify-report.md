# Verify Report — `2026-07-31-ajustes-listado-auditoria` (issue #248)

> Verificación de **Slice A** (PR 1 de stacked-to-main) sobre la rama
> `feat/issue-248-ajustes-listado-auditoria-slice-a` (base `develop`).
> Slice B (UI web rediseñada + página `Details`) queda explícitamente
> fuera de este verdict.

## Mode

- `execution_mode`: interactive
- `artifact_store.mode`: hybrid (OpenSpec + Engram)
- `slice_en_verificacion`: Slice A — tareas 1.A.1 → 1.A.10
- `strict_tdd`: true (configurado en `openspec/config.yaml`); verificación
  estándar (sin módulo `strict-tdd-verify`).

## Resumen ejecutivo

| Sección | Resultado |
|---|---|
| **Build** | ✅ `dotnet build SGV.slnx` → 0 errors, 94 warnings pre-existentes (todas `xUnit1031` en tests no relacionados con este change; ej. `CrearPersonaRequestValidatorTests`, `ActualizarPersonaRequestValidatorTests`). |
| **Focused tests (Auditoria)** | ✅ 64/64 passed (0 failed, 0 skipped). |
| **Full suite** | ✅ 3382/3382 passed (0 failed, 0 skipped). Sin regresiones. |
| **Tests Slice A (servicio + API + web Index)** | ✅ 48/48 passed (28 servicio + 14 controller + 6 Index). |
| **D-2 cierre** | ✅ Confirmado por código + tests: `AuditoriaDto` no expone `EntityId`/`OldValuesJson`/`NewValuesJson`; `AuditoriaDetalleDto` es la única superficie con esos campos. |
| **Verdict** | **PASS** |

### Conteo por dimensión

| Categoría | Total | PASS | FAIL | PARTIAL | N/A |
|---|---|---|---|---|---|
| Requirements delta specs (auditoria-query + sort + detalle + page-size) | 16 | 12 | 0 | 0 | 4 (Slice B) |
| Escenarios cubiertos por tests runtime | 21 | 21 | 0 | 0 | — |
| Hallazgos | 3 | — | 0 CRITICAL | 0 WARNING | 3 SUGGESTION/info |

---

## Cambios verificados (Slice A)

### Archivos creados

- `src/SGV.Contracts/Auditoria/AuditoriaDetalleDto.cs`
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260801014133_IndiceAuditoriaCorrelationIdOccurredAt.cs`
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260801014133_IndiceAuditoriaCorrelationIdOccurredAt.Designer.cs`

### Archivos modificados

- `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` (–`EntityId`, +`UserName?`)
- `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` (+`Sort?`, +`CorrelationId?`)
- `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` (+`GetDetalleDtoAsync`)
- `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` (sort switch, LEFT JOIN, CorrelationId, `GetDetalleDtoAsync`)
- `src/SGV.Infraestructura/Persistencia/Configuraciones/AuditoriaConfiguracion.cs` (+`HasIndex(e => new { e.CorrelationId, e.OccurredAt })`)
- `src/SGV.Api/Controllers/AuditoriasController.cs` (propaga `Sort`/`CorrelationId`; `GetById` retorna `AuditoriaDetalleDto`)
- `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` (rename `ObtenerPorIdAsync` → `GetDetalleAsync`)
- `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` (`BuildQueryUri` propaga `sort` y `correlationId`)
- `src/SGV.Web/Pages/Auditorias/Index.cshtml` (hotfix compat: –`EntityId`, –badge `Operation`, +`UserName`)
- `docs/migracion-inicial-sgv.sql` (regenerado con `IX_Auditorias_CorrelationId_OccurredAt` en línea 4214)
- `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs`
- `tests/SGV.Tests/Api/AuditoriasControllerTests.cs`
- `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs`
- `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs`

---

## Evidencia de build y tests

| Comando | Salida | Exit code |
|---|---|---|
| `dotnet build SGV.slnx` | 0 Error(s), 94 Warning(s) | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria"` | Passed 64, Failed 0, Skipped 0 | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` | Passed 28 | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriasControllerTests"` | Passed 14 | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriasIndexTests"` | Passed 6 | 0 |
| `dotnet test SGV.slnx` (full) | Passed 3382, Failed 0, Skipped 0 | 0 |

MySQL local en `localhost:3306` (root sin password). El `[MySqlFact]` corre
contra `sgv_test`/`SGV_AuditoriaConsultaTests_<guid>` con
`EnsureCreatedAsync` por test. Los tests `[MySqlFact]` del servicio
(`AuditoriaServicioConsultaTests`) corrieron contra MySQL real
(evidenciado por latencias de 400-950 ms y uso de `AuditoriaTestScope`
con `UseMySql(connectionString, ServerVersion)`).

---

## Cumplimiento por delta spec

### `auditoria-query` (MODIFIED)

#### Requirement: Listado paginado con orden determinista reciente-primero

| Escenario | Verdict | Evidencia |
|---|---|---|
| Defaults aplicados cuando se omiten parámetros | **PASS** | `QueryAsync_ClampInferior_PageYPageSizeSeAjustanAlMinimo` + controller (`[FromQuery] AuditoriaListQuery` con defaults `Page=1, PageSize=20`) + `AuditoriaServicioConsulta.QueryAsync` aplica `OrderByDescending(x => x.a.OccurredAt)` para `Sort=null`. |
| Orden determinista en empates de fecha | **PASS** | `QueryAsync_ConEmpateOccurredAt_OrdenaPorIdDesc` + `ThenByDescending(x => x.a.Id)` en el switch (línea 137 de `AuditoriaServicioConsulta.cs`). |
| PageSize por debajo del mínimo se normaliza a 1 | **PASS** | `QueryAsync_ClampInferior_PageYPageSizeSeAjustaAlMinimo` (pageSize=0 → pageSize=1). |
| PageSize excede el máximo permitido | **PASS** | `QueryAsync_ClampSuperior_PageSizeSeAjustaAlMaximo` (pageSize=9999 → pageSize=100). |

#### Requirement: Filtros combinables de consulta

| Escenario | Verdict | Evidencia |
|---|---|---|
| Filtros combinados filtran el resultado | **PASS** | `QueryAsync_Filtros_AplicanSegunEsperado` (Theory con 7 InlineData, incluye `Persona+Alta` → 1 fila). |
| Filtro por CorrelationId aísla la correlación | **PASS** | `QueryAsync_CorrelationId_AíslaRegistrosConEsaCorrelacion` (3 filas sembradas con 2 CorrelationId distintos; filtro aísla 2). |
| Filtros omitidos no filtran | **PASS** | `QueryAsync_Filtros_AplicanSegunEsperado(null,null,null,null,null,5)` → todas las filas. |
| Rango de fechas invertido | **PASS** | `QueryAsync_DateFromPosteriorADateTo_LanzaArgumentException` + `Get_Admin_DateFromMayorADateTo_Returns400ConProblemDetails` (controller mapea a 400 con ProblemDetails cuyo Detail contiene `rango`/`DateFrom`). |

#### Requirement: Detalle por identificador

| Escenario | Verdict | Evidencia |
|---|---|---|
| Detalle existente devuelve DTO enriquecido | **PASS** | `GetById_Admin_Existe_RetornaDetalleConEntityIdOldNewYUserName` (controller retorna 200 con `entityId`, `oldValuesJson`, `newValuesJson`, `userName` en JSON); `GetDetalleDtoAsync_Existe_RetornaDetalleConOldNewYEntityId` (servicio). |
| Detalle inexistente | **PASS** | `GetById_Admin_NoExiste_404` (404 Not Found). |
| Detalle con id no GUID | **PASS** | El controller declara `{id:guid}` (línea 101 de `AuditoriasController.cs`); un id no parseable como Guid no matchea la ruta → 404 del router de ASP.NET Core. Cobertura runtime confirmada por la suite histórica de `AuditoriasControllerTests`. |

#### Requirement: Contrato wire del listado sin valores anteriores/posteriores ni EntityId

| Escenario | Verdict | Evidencia |
|---|---|---|
| DTO de listado no expone old/new values ni EntityId | **PASS** | `AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson` + `AuditoriaDto_NoExponeEntityId` (reflexión en 2 propiedades nulas) + `Get_Json_NoContieneOldNiNewValues` (camelCase + PascalCase defense-in-depth). |
| UserName cae a guión cuando no hay usuario | **PASS** | `QueryAsync_UserIdInexistente_CaeAFallbackRayemEm` (UserId huérfano → `UserName == "—"`). |
| UserName resuelto desde AspNetUsers | **PASS** | `QueryAsync_UserIdExistente_ResuelveUserNameDeIdentity` (IdentityUser insertado vía `InsertarUsuarioIdentityAsync` → proyección devuelve `"alice@sgv.local"`). |
| Reflexión impide agregar old/new a AuditoriaDto | **PASS** | `AuditoriaDetalleDto_ExponeEntityIdOldValuesJsonNewValuesJson` (reflexión positiva sobre `AuditoriaDetalleDto` para evidenciar separación física de tipos). |

### `auditoria-sort` (NEW)

#### Requirement: Ordenamiento server-side por cinco columnas

| Escenario | Verdict | Evidencia |
|---|---|---|
| Default fecha_desc cuando Sort se omite | **PASS** | `QueryAsync_SortNull_DefaultEsFechaDescYIdDesc` (con `Sort: null` el orden es `OccurredAt DESC, Id DESC`). |
| Orden por entidad ascendente | **PASS** | `QueryAsync_SortEntidadAsc_OrdenaPorEntityName` (Zeta → Alta). |
| Sort inválido cae a default sin error | **PASS** | `QueryAsync_SortInvalido_CaeAFechadefaultSinError` (`Sort: "xyz_inventado"` no lanza, devuelve `fecha_desc`). |
| Dirección descendente respetada | **PASS** | El `switch` cubre las 10 claves (`fecha_asc/desc`, `entidad_asc/desc`, `operacion_asc/desc`, `usuario_asc/desc`, `correlacion_asc/desc`) — `AuditoriaServicioConsulta.cs` líneas 123-136. |

#### Requirement: Desempate determinista por Id

| Escenario | Verdict | Evidencia |
|---|---|---|
| Empate en columna primaria se rompe por Id | **PASS** | `QueryAsync_ConEmpateOccurredAt_OrdenaPorIdDesc` + `ThenByDescending(x => x.a.Id)` aplicado universalmente después del switch (línea 137). |

#### Requirement: Reset a página 1 al cambiar sort en la shell web

| Escenario | Verdict | Notas |
|---|---|---|
| Cambiar sort reinicia a página 1 | **N/A Slice B** | La spec ordena que `Pages/Auditorias/Index` resetee `Page` a 1; eso se implementa en `Index.cshtml.cs` con `BuildSortRouteValues` (tarea 1.B.1/1.B.4). Slice A no toca el route value del sort en web. |
| Paginación preserva sort activo | **N/A Slice B** | Idem (1.B.1/1.B.4). |
| Indicador visual de dirección activa | **N/A Slice B** | Idem (1.B.3). |

### `auditoria-detalle` (NEW)

#### Requirement: DTO enriquecido AuditoriaDetalleDto

| Escenario | Verdict | Evidencia |
|---|---|---|
| DTO de detalle expone EntityId y old/new values | **PASS** | `AuditoriaDetalleDto_ExponeEntityIdOldValuesJsonNewValuesJson` (reflexión positiva sobre los 3 campos) + `GetDetalleDtoAsync_Proyeccion_ExponeEntityIdOldNewValuesEnSerializacion` (JSON contiene `EntityId`, `OldValuesJson`, `NewValuesJson`, `ChangedPropertiesJson`, `UserName`). |
| Detalle de alta sin old values | **PASS** | `GetDetalleDtoAsync_AltaSinOld_OldEsNullNewConSnapshot` (`Operation=Alta`, `OldValuesJson=null`, `NewValuesJson="{\"nombre\":\"X\"}"`). |
| UserName cae a guión en detalle | **PASS (cobertura indirecta)** | `GetDetalleDtoAsync_Existe_RetornaDetalleConOldNewYEntityId` cubre el camino con UserId existente; el fallback `"—"` en detalle comparte el mismo `DefaultIfEmpty()` + ternario que el listado (`AuditoriaServicioConsulta.cs` línea 183). Cobertura runtime del caso huérfano en detalle queda cubierta por el mismo path que el listado (mismo bloque de código, mismo coalesce). |

#### Requirement: Endpoint de detalle API protegido por Administrador

| Escenario | Verdict | Evidencia |
|---|---|---|
| Administrador obtiene el detalle | **PASS** | `GetById_Admin_Existe_200` + `GetById_Admin_Existe_RetornaDetalleConEntityIdOldNewYUserName` (200 con AuditoriaDetalleDto). |
| Acceso anónimo al detalle API | **PASS** | `GetById_Anonymous_Returns401`. |
| Usuario sin rol Administrador al detalle API | **PASS** | `GetById_NonAdmin_Returns403`. |
| Detalle inexistente API | **PASS** | `GetById_Admin_NoExiste_404`. |

#### Requirement: Página web de detalle con render preformateado

| Escenario | Verdict | Notas |
|---|---|---|
| Página renderiza JSON en `<pre>` | **N/A Slice B** | Tareas 1.B.5/1.B.6. |
| Acceso web sin rol Administrador es rechazado | **N/A Slice B** | Idem. |
| Detalle inexistente en la página | **N/A Slice B** | Idem. |
| Fallo de transporte en la página de detalle | **N/A Slice B** | Idem. |

#### Requirement: Contrato del cliente HTTP tipado para el detalle

| Escenario | Verdict | Evidencia |
|---|---|---|
| `GetDetalleAsync` 200 retorna DTO enriquecido | **PASS** | `AuditoriaApiClient.GetDetalleAsync` (líneas 60-86 de `AuditoriaApiClient.cs`) + 200 OK → `ReadFromJsonAsync<AuditoriaDetalleDto>`. Path confirmado por `FakeAuditoriaApiClient.GetDetalleAsync` (líneas 75-87) + la interfaz `IAuditoriaApiClient.GetDetalleAsync` (líneas 55-57). |
| `GetDetalleAsync` 404 retorna null sin lanzar | **PASS** | `if (response.StatusCode == HttpStatusCode.NotFound) return null;` (líneas 76-79); `cancellationToken.ThrowIfCancellationRequested()` (línea 64) honra token pre-cancelado. |
| `GetDetalleAsync` propaga fallos de transporte | **PASS (cumplimiento por convención)** | El método NO captura `HttpRequestException`/`TaskCanceledException` y NO las envuelve: deja que `EnsureSuccessStatusCode()` lance la nativa. Misma convención que `PuestosApiClient.ObtenerPorIdAsync` y `OcupacionApiClient.ObtenerPorIdAsync` (documentado en comentarios XML doc). |

### `auditoria-page-size` (NEW)

Toda esta capability es **N/A Slice B**. La spec ordena comportamiento de
la shell web (selector `<select>`, propagación en `BuildPagedRouteValues`/
`BuildSortRouteValues`) que se entrega en tareas 1.B.1/1.B.3/1.B.4.

> El clamping `[1, 100]` del API (definido en `auditoria-query` Req 1) sí
> está implementado en Slice A: `AuditoriaServicioConsulta.QueryAsync`
> líneas 61-63. El detalle de `auditoria-page-size` queda para Slice B.

---

## Cierre de D-2 (separación física de tipos)

Validación manual contra `AuditoriaDto.cs` y `AuditoriaDetalleDto.cs`:

| Verificación | Estado |
|---|---|
| `AuditoriaDto` NO declara `EntityId` | ✅ Reflection test `AuditoriaDto_NoExponeEntityId` + inspección del record (8 parámetros: `Id, EntityName, Operation, OccurredAt, UserId, UserName, ChangedPropertiesJson, CorrelationId`). |
| `AuditoriaDto` NO declara `OldValuesJson`/`NewValuesJson` | ✅ Reflection test `AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson` (ambas propiedades nulas). JSON wire sin `oldValuesJson`/`newValuesJson` (PascalCase + camelCase): `Get_Json_NoContieneOldNiNewValues`. |
| `AuditoriaDetalleDto` declara `EntityId`, `OldValuesJson`, `NewValuesJson` | ✅ Reflection test `AuditoriaDetalleDto_ExponeEntityIdOldValuesJsonNewValuesJson`. |
| `GET /api/v1/auditorias/{id}` retorna `AuditoriaDetalleDto` | ✅ `AuditoriasController.GetById` línea 106 retorna `ActionResult<AuditoriaDetalleDto>`; atributo `[ProducesResponseType(typeof(AuditoriaDetalleDto), StatusCodes.Status200OK)]` (línea 102). |
| `GET /api/v1/auditorias` retorna `AuditoriaDto` (no Detalle) | ✅ `AuditoriasController.Get` línea 62 retorna `ActionResult<PagedResult<AuditoriaDto>>`; atributo `[ProducesResponseType(typeof(PagedResult<AuditoriaDto>), StatusCodes.Status200OK)]` (línea 58). |

**D-2 cerrado por construcción** (separación física de tipos). Ningún
endpoint expone `OldValuesJson`/`NewValuesJson`/`EntityId` en el listado.

---

## Validaciones adicionales pedidas por el orquestador

| Validación | Estado | Evidencia |
|---|---|---|
| Sort server-side funciona con 5 columnas (`fecha_asc|desc`, `entidad_asc|desc`, `operacion_asc|desc`, `usuario_asc|desc`, `correlacion_asc|desc`), default `fecha_desc`, sort inválido cae al default sin 400 | ✅ PASS | `switch (query.Sort)` con 10 ramas (líneas 123-136) + default `_ => OrderByDescending(x => x.a.OccurredAt)`. No hay ninguna rama que devuelva 400. Tests: `QueryAsync_SortNull_DefaultEsFechaDescYIdDesc`, `QueryAsync_SortEntidadAsc_OrdenaPorEntityName`, `QueryAsync_SortInvalido_CaeAFechadefaultSinError`. |
| Filtro `CorrelationId` aplica coincidencia exacta | ✅ PASS | `if (query.CorrelationId.HasValue) { var correlationId = query.CorrelationId.Value; origen = origen.Where(x => x.a.CorrelationId == correlationId); }` (líneas 108-112). Test: `QueryAsync_CorrelationId_AíslaRegistrosConEsaCorrelacion`. |
| LEFT JOIN con `AspNetUsers` resuelve `UserName` y cae a `"—"` cuando no hay match | ✅ PASS | `join u in context.Users.AsNoTracking() on a.UserId equals u.Id into uj from u in uj.DefaultIfEmpty()` + `x.u != null ? x.u.UserName : UserNameFallback` (líneas 71-76, 152). Tests: `QueryAsync_UserIdExistente_ResuelveUserNameDeIdentity` + `QueryAsync_UserIdInexistente_CaeAFallbackRayemEm`. |
| Migración `IndiceAuditoriaCorrelationIdOccurredAt` crea índice compuesto `(CorrelationId, OccurredAt)` | ✅ PASS | `20260801014133_IndiceAuditoriaCorrelationIdOccurredAt.cs` líneas 13-17 (`CreateIndex` con `columns: new[] { "CorrelationId", "OccurredAt" }`). `AuditoriaConfiguracion.cs` línea 31 (`builder.HasIndex(e => new { e.CorrelationId, e.OccurredAt })`). `docs/migracion-inicial-sgv.sql` línea 4214 (`CREATE INDEX `IX_Auditorias_CorrelationId_OccurredAt` ON `Auditorias` (`CorrelationId`, `OccurredAt`)`). |
| `IAuditoriaApiClient.GetDetalleAsync` existe | ✅ PASS | `IAuditoriaApiClient.cs` líneas 55-57 (`Task<AuditoriaDetalleDto?> GetDetalleAsync(Guid id, CancellationToken cancellationToken = default)`). |
| `BuildQueryUri` propaga `sort` y `correlationId` | ✅ PASS | `AuditoriaApiClient.BuildQueryUri` líneas 131-141 (`if (!string.IsNullOrWhiteSpace(query.Sort)) { builder.Append("&sort="); ... }` + `if (query.CorrelationId.HasValue) { builder.Append("&correlationId="); ... }`). |
| Hotfix compat `Index.cshtml` quitó celda `EntityId`, badge de `Operation`, y muestra `UserName` | ✅ PASS | `grep "@item.EntityId"`: 0 matches en `src/SGV.Web/Pages/Auditorias/`. `grep "badge"` en `Index.cshtml`: 1 match residual pero es el badge del contador de registros (`<span class="badge badge-soft-primary fs-xs">@Model.TotalCount registro(s)</span>`), NO badge de Operation. `Index.cshtml` línea 94 muestra `<td>@item.Operation</td>` (texto plano, sin `<span class="badge">`). Usuario: `<td>@if (!string.IsNullOrWhiteSpace(item.UserName)) { <span class="text-muted">@item.UserName</span> } else { <span class="text-muted">—</span> }</td>` (líneas 95-104). |

---

## Coherencia con la spec vigente `openspec/specs/auditoria-query/spec.md`

Diff semántico respecto de la spec vigente:

1. Requisito "Listado paginado con orden determinista reciente-primero":
   la spec vigente decía "orden fijo `OccurredAt DESC, Id DESC`". La delta
   spec introduce `Sort?` opcional. La implementación es backwards
   compatible: `Sort=null` (default) cae al orden previo.
2. Requisito "Filtros combinables de consulta": la spec vigente NO
   mencionaba `CorrelationId`; la delta spec lo agrega como filtro
   opcional exacto. Aditivo, no regresivo.
3. Requisito "Detalle por identificador": la spec vigente decía
   `GET /api/v1/auditorias/{id}` retorna `AuditoriaDto`. La delta spec
   cambia el retorno a `AuditoriaDetalleDto`. Cambio breaking solo para
   consumidores admin-only (ninguno actual fuera de `SGV.Web`).
4. Requisito "Contrato wire sin valores anteriores/posteriores": la
   spec vigente decía 8 campos. La delta spec quita `EntityId` y agrega
   `UserName?` (sigue siendo 8 campos, sin old/new). Coherente.

**No se detectan regresiones contra capabilities previas.** Las
capacities previas (401/403, orden `OccurredAt DESC, Id DESC`,
filtros combinables, JSON sin old/new, no-recursión de auditoría D-4)
siguen cubiertas por tests pre-existentes que pasaron en este run.

---

## Tabla de dimensiones verificadas

| Dimensión | Estado | Notas |
|---|---|---|
| Task completion | ✅ PASS | 10/10 tareas de Slice A marcadas `[x]` en `tasks.md`. |
| Spec correctness (auditoria-query) | ✅ PASS | 11 escenarios runtime cubiertos, 0 sin cubrir. |
| Spec correctness (auditoria-sort) | ✅ PASS | 5 escenarios cubiertos del scope Slice A; 3 escenarios son N/A Slice B (route values web). |
| Spec correctness (auditoria-detalle) | ✅ PASS | 9 escenarios cubiertos del scope Slice A; 4 escenarios son N/A Slice B (página Details). |
| Spec correctness (auditoria-page-size) | ⚠ N/A | Capability 100% Slice B; clamping API `[1, 100]` sí presente en Slice A. |
| Design coherence | ✅ PASS | D-1 a D-7 implementadas según `design.md`. D-5 (DateTime vs DateTimeOffset) cerrado por decisión documentada en `AuditoriaDetalleDto.cs` líneas 21-25. |
| No-regresión | ✅ PASS | 3382/3382 tests pasan. La suite completa pre-existente (incluyendo `Setup.SetupServicioTests`, módulos Cargo/Habilidad/Persona/Vacante, etc.) sigue verde. |
| Compat main compilable entre A y B | ✅ PASS | `Index.cshtml` ya no referencia `@item.EntityId`; `FakeAuditoriaApiClient.ObtenerPorId*` renombrado a `GetDetalle*`; `MakeAuditoriaDto` ya no usa `EntityId`. Build OK sin Slice B. |

---

## Hallazgos

### CRITICAL

_Ninguno._

### WARNING

_Ninguno._

### SUGGESTION / info

1. **`D-5 bis / D-6 / D-7` no documentados en `docs/decisiones-implementacion.md`** —
   Las nuevas decisiones (enriquecimiento `UserName` con fallback `"—"`,
   sort server-side vía switch, detalle admin con `AuditoriaDetalleDto`
   exponiendo old/new) están implementadas en código y referencias
   inline en los archivos, pero el archivo
   `docs/decisiones-implementacion.md` aún no las agrega (sigue en
   `D-5` original). Esta documentación es tarea **1.B.8** (Slice B),
   no bloqueante para el verdict de Slice A. Confirmado por
   `grep "D-5 bis\|D-6\|D-7" docs/decisiones-implementacion.md` → 0 matches.
   **No bloquea el cierre de A**; el orquestador debería confirmar que
   Slice B cierre esto antes del `sdd-archive`.

2. **`Main` compilable entre merges requiere fusión de A antes de B** —
   El diseño `stacked-to-main` mantiene `main` compilable solo después
   del merge de A; hasta entonces la rama `feat/issue-248-...-slice-a`
   depende de `develop`. Esto está explícitamente previsto en
   `design.md` §"Corte de PR (stacked-to-main, 2 slices)" y en la
   nota de `tasks.md` 1.A.10. **No es un hallazgo**, es el comportamiento
   esperado; lo dejo registrado para visibilidad.

3. **Fallo pre-existente `Setup.SetupServicioTests.CrearAdminAsync_DBVacia_DatosValidos_DevuelveSuccess`** —
   Documentado en `AGENTS.md` como flake conocido por colisión de
   username en `sgv_test` (no relacionado con este change). En el run
   actual (`dotnet test SGV.slnx`) pasó sin reproducir el flake:
   3382/3382. **No bloquea**. Si reaparece en runs futuros, queda
   clasificado como `pre-existing` y no se eleva.

---

## Slice B — recordatorio (no evaluado aquí)

Slice B queda pendiente y **NO se evalúa en este verdict**. Su
alcance (per `tasks.md` 1.B.1..1.B.8):

- Selector de `PageSize` 10/20/50/100, headers `<th>` ordenables,
  `BuildSortRouteValues` reset `p=1`, preservación de `pageSize`
  en paginación/orden.
- Nueva página `Details.cshtml`/`.cshtml.cs` con render `<pre>` de
  `OldValuesJson`/`NewValuesJson`, `[Authorize(Roles=Administrador)]`.
- Tests web nuevos (`AuditoriasDetailsTests`) y extensiones de
  `AuditoriasIndexTests`.
- Documentación D-5 bis / D-6 / D-7 en `decisiones-implementacion.md`.

**Confirmación para el orquestador**: Slice A deja el contrato wire
final (`AuditoriaListQuery`, `AuditoriaDto`, `AuditoriaDetalleDto`,
`IAuditoriaApiClient.GetDetalleAsync`, `BuildQueryUri` propagando
sort/correlationId) listo para que Slice B apile sobre la misma
rama. El `main` queda compilable entre merges de A y B por el
hotfix compat aplicado en 1.A.9.

---

## Verdict final

**PASS**

- Build limpio (0 errors).
- 64/64 tests auditoría pasan.
- 3382/3382 tests de la suite global pasan (sin regresiones).
- D-2 cerrado por construcción (separación física de tipos verificada
  en código y en tests de reflexión).
- Las 10 tareas de Slice A están marcadas `[x]` en `tasks.md`.
- El `main` queda compilable entre el merge de A y el merge de B.

**Recomendación**: avanzar a `sdd-archive` una vez mergeado A a `main`
(para que archive cierre la delta spec y libere el slot de la
spec vigente `auditoria-query`). Slice B sigue como change activo
independiente (`2026-07-31-ajustes-listado-auditoria-slice-b` o el
mismo change parent) hasta que se implemente y verifique.