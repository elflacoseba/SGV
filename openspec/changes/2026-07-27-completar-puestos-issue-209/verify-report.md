# Verify Report: Completar módulo de Puestos — endpoint segmentado, paginación server-side y protección de baja (PR1 backend)

> Change: `2026-07-27-completar-puestos-issue-209` · Issue: #209
> Slice verificado: **PR1 backend** (4 commits sobre `develop`)
> strict_tdd: true
> Modo verify: `interactive` · Persistencia: `both` (openspec + Engram)
> Build evidence revision: `4ad1…` (ver bloque YAML al final)

---

## Resumen ejecutivo

PR1 backend de `2026-07-27-completar-puestos-issue-209` cumple los tres requisitos en scope (REQ-PTO-001, REQ-PTO-002, REQ-PTO-010) sin desviaciones de diseño. Build limpio (0 errores, 0 warnings nuevas), suite focal 921/921 PASS, suite completa 3010/3010 PASS contra MySQL local sin tests skipped, y `dotnet build` arroja las mismas 4 warnings pre-existentes que existían antes de PR1. Las 7 decisiones locked (DEC-1..DEC-7) están reflejadas en código con cobertura de test suficiente — incluido el caso crítico DEC-3 (`Categoria = Conflict` explícito) que mapea a 409 vía `ApiResults.MapPuestoStatus`. **Veredicto: APPROVE**.

---

## Metodología

| Paso | Acción | Resultado |
|------|--------|-----------|
| 1 | Lectura de skills (`sdd-verify`, `strict-tdd-verify`, `dotnet-csharp`, `dotnet-best-practices`, `database-designer`, `mysql`, `dotnet-xunit`, `pr-review-dotnet`) | OK |
| 2 | Lectura de artefactos del change: `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md` | OK |
| 3 | Lectura de las 3 specs: `puestos-consulta-segmentada/spec.md` (REQ-PTO-001/002), `puestos-proteccion-baja/spec.md` (REQ-PTO-010). `web-puestos-paginacion/spec.md` queda fuera del scope PR1 (REQ-PTO-020 = PR2) | OK |
| 4 | Inspección de los archivos modificados por los 4 commits (`8a9e08c0`, `013f146b`, `a152a98a`, `27fb36b9`) en `src/SGV.Contracts`, `src/SGV.Aplicacion`, `src/SGV.Infraestructura`, `src/SGV.Api` y `tests/SGV.Tests` | OK |
| 5 | Verificación de contratos vinculados: `PuestoError`/`PuestoCommandResult`, `ErrorCategoria`, `IOcupacionRepository.ExistsActiveByPuestoAsync`, `ApiResults.MapPuestoStatus`/`MapCategoria` | OK |
| 6 | Ejecución de `dotnet build SGV.slnx` | 0 errores, 4 warnings pre-existentes |
| 7 | Ejecución de `dotnet test SGV.slnx --filter "FullyQualifiedName~Puesto\|FullyQualifiedName~Cargo"` | 921/921 PASS |
| 8 | Ejecución de `dotnet test SGV.slnx` (suite completa para regresiones) | 3010/3010 PASS |
| 9 | Auditoría de aserciones (assertion quality) sobre los tests PR1 | Sin tautologías, ghost loops ni mocks huérfanos |
| 10 | Mapeo spec → test para los 10 escenarios | 10/10 cubiertos |

---

## Hallazgos

### CRITICAL

**Sin hallazgos críticos.** Ningún escenario sin covering test, ningún fallo de build, ninguna desviación que rompa un spec.

### WARNING

**Sin warnings bloqueantes.** Las dos observaciones abajo son no-bloqueantes y se registran para contexto de merge:

- **W1 (informativo, no bloqueante):** El código de error `PuestoConOcupacionesActivas` es un literal de string en `PuestoServicioComandos.cs:240`. Sigue la convención histórica del repo (paridad con `CodigoDuplicado`, `PuestoNoEncontrado`), pero no se ha extraído a una clase de constantes. No es bloqueante: la propuesta y el design confirman este contrato por convención.
- **W2 (informativo, no bloqueante):** `NullOcupacionRepository` (`PuestoServicioComandos.cs:53-87`) lanza `NotSupportedException` para todos los métodos de `IOcupacionRepository` excepto `ExistsActiveByPuestoAsync` (que retorna `false`). Si una operación futura de `Puesto` necesitara leer ocupaciones, el null-object crecería. Documentado en `apply-progress.md` como R-null-object. Aceptable hoy: el método de guarda es el único path de lectura en `PuestoServicioComandos`.

### SUGGESTION

- **S1:** Considerar extraer `PuestoConOcupacionesActivas` y otros códigos de error de `PuestoServicioComandos` a una clase estática `PuestoErrorCodes` en un PR de limpieza dedicado (no en este change). Mantiene paridad con el patrón actual.
- **S2 (recordatorio PR2):** DEC-7 (`BuildQueryUri` con `StringBuilder` + `Uri.EscapeDataString`) se materializará en `PuestosApiClient` durante PR2 web. El type alias `using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery` en `PuestoListItemViewModel.cs:6` ya está listo para ser consumido por el cliente web.

---

## Cumplimiento por requisito

### REQ-PTO-001 — Consulta segmentada paginada

| Escenario | Cubierto por | Evidencia (archivo:línea) | Status |
|-----------|--------------|----------------------------|:------:|
| **S1:** Consulta activa por defecto (sin segmento → solo activos, TotalCount correcto) | `PuestoServicioConsultaTests.QueryAsync_ConSegmentoActivas_RetornaSoloActivos` (unit) + `PuestoRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoActivas_NoIncluyeEliminadas` (integration) + `PuestosControllerTests.GetConsulta_SinStatus_RetornaActivas` (API) | `tests/.../PuestoServicioConsultaTests.cs:126-140`; `tests/.../PuestoRepositoryQueryAsyncTests.cs:70-114`; `tests/.../PuestosControllerTests.cs:529-541` | ✅ |
| **S2:** Consulta eliminada con filtros (search + sort + página, order antes de Skip/Take) | `PuestoServicioConsultaTests.QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados` + `QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar` + `QueryAsync_ConSortCodigoAsc_NoDesordena` + `QueryAsync_ConSearchFiltraPorCodigo_Nombre_O_Descripcion` + `PuestoRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminados` + `QueryAsync_MySql_SearchFiltraPorCodigo_Nombre_Descripcion` + `QueryAsync_MySql_SortCodigoAsc_AplicaOrdenAntesDePaginar` + `QueryAsync_MySql_SortNombreDesc_AplicaOrdenAntesDePaginar` + `PuestosControllerTests.GetConsulta_ConSearchPageSize_DevuelvePagedResult` + `GetConsulta_PropagaSortAlServicio` + `GetConsulta_ConSortCodigoAsc_FluyeAlServicio` | `tests/.../PuestoServicioConsultaTests.cs:142-314`; `tests/.../PuestoRepositoryQueryAsyncTests.cs:22-68`, `:164-205`, `:283-368`; `tests/.../PuestosControllerTests.cs:543-598` | ✅ |
| **S3:** Página fuera del conjunto (vacío + TotalCount preservado, no mezcla segmentos) | `PuestoRepositoryQueryAsyncTests.QueryAsync_MySql_PaginaFueraDeRango_RetornaColeccionVaciaSinMezclarSegmentos` + `PuestoServicioConsultaTests.QueryAsync_TotalCountProvieneDelRepositorio` (paginación adyacente) | `tests/.../PuestoRepositoryQueryAsyncTests.cs:243-281`; `tests/.../PuestoServicioConsultaTests.cs:181-196` | ✅ |

**REQ-PTO-001** — 3/3 escenarios cubiertos. **Status: PASS.**

### REQ-PTO-002 — Endpoint HTTP de consulta

| Escenario | Cubierto por | Evidencia (archivo:línea) | Status |
|-----------|--------------|----------------------------|:------:|
| **S1:** Endpoint devuelve página segmentada (`GET /consulta?status=eliminadas&page=1&pageSize=10` → 200 con `PagedResult<PuestoDto>`) | `PuestosControllerTests.GetConsulta_SinStatus_RetornaActivas` (default activas) + `GetConsulta_ConSearchPageSize_DevuelvePagedResult` (200 con `PagedResult`) + `GetConsulta_ConStatusInvalido_CaeA_Activas` (controller branch de mapeo `status→Segmento`) + `PuestoServicioConsultaTests.QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados` (service-level segment filter) | `tests/.../PuestosControllerTests.cs:529-541`, `:566-580`, `:600-614`; `tests/.../PuestoServicioConsultaTests.cs:142-157`; `src/SGV.Api/Controllers/PuestosController.cs:168-183` | ✅ |
| **S2:** Cliente anónimo (sin credenciales → 401) | `PuestosControllerTests.GetConsulta_WithoutCredentials_ReturnsUnauthorized` | `tests/.../PuestosControllerTests.cs:517-526`; `src/SGV.Api/Controllers/PuestosController.cs:18` (`[Authorize]` a nivel de clase) | ✅ |
| **S3:** Endpoint legado preservado (`GET /api/v1/puestos` sigue retornando `IReadOnlyList<PuestoDto>` sin cambio de shape) | `PuestosControllerTests.GetAll_ReturnsOkWithDtoArray` + `GetAll_WhenNoData_ReturnsOkWithEmptyArray` + `GetAll_WithoutCredentials_ReturnsUnauthorized` | `tests/.../PuestosControllerTests.cs:25-61`, `:106-114`; `src/SGV.Api/Controllers/PuestosController.cs:37-45` (controller `[HttpGet]` sin cambios) | ✅ |

**REQ-PTO-002** — 3/3 escenarios cubiertos. **Status: PASS.**

### REQ-PTO-010 — Baja protegida por ocupaciones vigentes

| Escenario | Cubierto por | Evidencia (archivo:línea) | Status |
|-----------|--------------|----------------------------|:------:|
| **S1:** Ocupaciones activas bloquean la baja (DELETE → 409 + `PuestoConOcupacionesActivas` + puesto permanece activo) | `PuestoServicioComandosTests.DesactivarAsync_ConOcupacionesVigentes_RetornaConflictSinGuardar` (unit: assert `Type=Conflict`, `Code="PuestoConOcupacionesActivas"`, `Categoria=Conflict`, `SaveChangesCount=0`, `DeleteCallCount=0`, `puesto.IsActive=true`) + `PuestosControllerTests.Delete_ConOcupacionesVigentes_Devuelve409ConProblemDetails` (API: HTTP 409, `problem.Title == "PuestoConOcupacionesActivas"`) | `tests/.../PuestoServicioComandosTests.cs:289-309`; `tests/.../PuestosControllerTests.cs:618-647`; `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs:235-244`; `src/SGV.Api/Infrastructure/Results/ApiResults.cs:288-291` (mapeo `MapPuestoStatus`) | ✅ |
| **S2:** Puesto sin ocupaciones se desactiva (DELETE → 204 + puesto inactivo) | `PuestoServicioComandosTests.DesactivarAsync_SinOcupaciones_ProcedeConLaBaja` + `PuestosControllerTests.Delete_SinOcupaciones_Devuelve204NoContent` | `tests/.../PuestoServicioComandosTests.cs:311-324`; `tests/.../PuestosControllerTests.cs:649-659` | ✅ |
| **S3:** Puesto inexistente (DELETE id desconocido → 404 + ningún puesto modificado) | `PuestoServicioComandosTests.DesactivarAsync_PuestoInexistente_RetornaNoEncontradoYSinGuardar` + `PuestosControllerTests.Delete_PuestoInexistente_Devuelve404ConProblemDetails` | `tests/.../PuestoServicioComandosTests.cs:273-285`; `tests/.../PuestosControllerTests.cs:661-682` | ✅ |
| **S4:** Usuario sin autorización (autenticado sin rol Administrador → 403, no consulta ni modifica) | `PuestosControllerTests.Delete_WithAuthenticatedNonAdmin_ReturnsForbidden` | `tests/.../PuestosControllerTests.cs:163-174`; `src/SGV.Api/Controllers/PuestosController.cs:135` (`[Authorize(Roles = RolesSgv.Administrador)]`) | ✅ |

**REQ-PTO-010** — 4/4 escenarios cubiertos. **Status: PASS.**

---

## Evidencia de tests

### Suite focal PR1

```
dotnet test SGV.slnx --filter "FullyQualifiedName~Puesto|FullyQualifiedName~Cargo" --no-build --nologo
```

| Métrica | Valor |
|---------|------:|
| Total | 921 |
| Passed | 921 |
| Failed | 0 |
| Skipped | 0 |
| Duración | 0:00:28 |

Matchea exactamente el reporte de `apply-progress.md` (921/921).

### Suite completa (regresiones)

```
dotnet test SGV.slnx --no-build --nologo
```

| Métrica | Valor |
|---------|------:|
| Total | 3010 |
| Passed | 3010 |
| Failed | 0 |
| Skipped | 0 |
| Duración | 1:28 |

Matchea exactamente el reporte de `apply-progress.md` (3010/3010). **Sin regresiones introducidas por PR1.**

### Distribución por capa

| Capa | Tests | Tipo | Cubre spec |
|------|------:|------|------------|
| Contratos | 3 | Unit (`PuestoListQueryTests`) | REQ-PTO-001 shape |
| Aplicación (servicio comandos) | 18 | Unit (`PuestoServicioComandosTests`, incluye 2 nuevos de la guarda) | REQ-PTO-010 (S1, S2, S3) |
| Aplicación (servicio consulta) | 11 | Unit (`PuestoServicioConsultaTests`, 7 nuevos de QueryAsync) | REQ-PTO-001 (S1, S2 adyacente) |
| Persistencia (repositorio EF) | 8 | Integration `[MySqlFact]` (`PuestoRepositoryQueryAsyncTests`, todos nuevos) | REQ-PTO-001 (S1, S2, S3) |
| API | 32+ (29 pre-existentes + nuevos `GetConsulta_*` y `Delete_*`) | Integration `WebApplicationFactory` (`PuestosControllerTests`) | REQ-PTO-002 (S1, S2, S3) + REQ-PTO-010 (S1, S2, S3) |

---

## Build evidence

```
dotnet build SGV.slnx --nologo
```

| Métrica | Valor |
|---------|------:|
| Errores | 0 |
| Warnings nuevas (introducidas por PR1) | 0 |
| Warnings pre-existentes | 4 (NU1510 sobre `Microsoft.Extensions.Configuration.Json` y `…EnvironmentVariables` en `SGV.Infraestructura`; csproj NO tocado por PR1 — son anteriores) |
| Tiempo | 0:00:00.78 |
| Estado | ✅ Build succeeded |

Los 4 warnings son pre-existentes y ya estaban documentados en `apply-progress.md` (NU1510 sobre packages sin pruning en `SGV.Infraestructura.csproj`). PR1 no añade warnings.

---

## Decisiones locked — verificación de cumplimiento

| # | Decisión | Implementación verificada | Status |
|---|----------|---------------------------|:------:|
| **DEC-1** | Type alias preserva `PuestoListQuery` legacy sin imports rotos | `src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs:6` — `using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;`. Build limpio. | ✅ |
| **DEC-2** | Ctor primario 7-parámetros + legacy 4 con null-object `NullOcupacionRepository` preserva fixtures | `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs:17-46`. Ctor primario (7 params, line 17-24) + ctor legacy (4 params, line 36-46) que delega con `NullOcupacionRepository` (line 53-87). Tests legacy (e.g. `PuestoServicioComandosTests.CrearAsync_*`) siguen pasando. | ✅ |
| **DEC-3** | `PuestoError.Categoria = ErrorCategoria.Conflict` explícito (default sería `Unexpected` → 500) | `PuestoServicioComandos.cs:243` — `new PuestoError(PuestoErrorType.Conflict, "PuestoConOcupacionesActivas", "…", null, ErrorCategoria.Conflict)`. Verificado en `ApiResults.MapPuestoStatus` (`ApiResults.cs:288-291`): cuando `Categoria != Unexpected || StatusCode != null` usa `MapCategoria(error.Categoria)` que mapea `Conflict → 409`. Test `Delete_ConOcupacionesVigentes_Devuelve409ConProblemDetails` confirma 409 + `problem.Title == "PuestoConOcupacionesActivas"`. | ✅ |
| **DEC-4** | `QueryAsync` propio con `AsNoTracking()` + Includes; no reusa `Query` base | `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs:142-149` — `Context.Set<PuestoEntity>().AsNoTracking().Where(...).Include(p => p.UnidadOrganizativa).Include(p => p.Cargo)`. Comentario en línea 125-133 documenta DEC-4 explícitamente. `Query` base (line 13-17) sólo cubre `IsActive` activo, no se reutiliza. | ✅ |
| **DEC-5** | Repo devuelve `(Items, Total)`; servicio construye `PagedResult<PuestoDto>` (paridad Cargos) | Repo retorna `(IReadOnlyList<Puesto> Items, int TotalCount)` (`PuestoRepository.cs:174`). Servicio `PuestoServicioConsulta.QueryAsync` (`PuestoServicioConsulta.cs:21-38`) construye `new PagedResult<PuestoDto>(items, totalCount, query.Page, query.PageSize)`. | ✅ |
| **DEC-6** | Controller no normaliza `page<1`/`pageSize<1` (paridad Cargos) | `PuestosController.GetConsulta` (`PuestosController.cs:168-183`) — `[FromQuery] int page = 1, int pageSize = 20` directo al record sin `Math.Max`. Paridad confirmada con `CargosController.GetConsulta`. | ✅ |
| **DEC-7** | `BuildQueryUri` con `StringBuilder` (espejo `CargoApiClient`) | **Fuera de scope PR1.** DEC-7 se materializa en `PuestosApiClient` durante PR2 (web). Marcado como recordatorio. | 🔲 PR2 |

---

## TDD Compliance

| Check | Resultado | Detalle |
|-------|-----------|---------|
| TDD Evidence reportado | ✅ | `apply-progress.md` sección "Evidence TDD (cumplido per test RED→GREEN)" con tabla de 9 filas |
| Todas las tareas tienen tests | ✅ | T-01..T-10 cubiertas: PuestoListQueryTests (T-01), PuestoServicioComandosTests nuevos (T-04), PuestoServicioConsultaTests nuevos (T-08), PuestoRepositoryQueryAsyncTests (T-06), PuestosControllerTests nuevos (T-10) |
| RED confirmado (tests existen) | ✅ | 9 archivos de test creados o modificados en PR1; todos los archivos existen en disco y compilan |
| GREEN confirmado (tests pasan) | ✅ | 921/921 tests focales PASS, 3010/3010 suite completa PASS |
| Triangulación adecuada | ✅ | `Desactivar`: 3 casos (ConOcupaciones, SinOcupaciones, PuestoInexistente); `QueryAsync`: 8 tests `[MySqlFact]` + 7 unit + 6 API = 21 casos cubriendo segmento activas/eliminadas/no-mezcla, search LIKE, sort asc/desc/default, paginación, page-fuera-rango, sort propagation controller→servicio |
| Safety Net para archivos modificados | ✅ | Archivos modificados: `PuestoServicioComandosTests`, `PuestoServicioConsultaTests`, `PuestosControllerTests`, `ApiWebApplicationFactory`, `OcupacionServicioComandosTests` (todos modificados, no nuevos). Tests pre-existentes siguen pasando (3010/3010 confirma). Archivos nuevos: `PuestoListQueryTests`, `PuestoRepositoryQueryAsyncTests` (safety net N/A por ser greenfield) |

**TDD Compliance**: 6/6 checks passed. El protocolo se siguió correctamente.

---

## Assertion Quality

| Archivo | Línea | Aserción | Observación |
|---------|------:|----------|-------------|
| `PuestoServicioComandosTests.cs` | 304 | `Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);` | ✅ Verifica valor específico (no tautología) |
| `PuestoServicioComandosTests.cs` | 305-308 | `Assert.Equal(0, uow.SaveChangesCount); Assert.Equal(0, puestoRepo.DeleteCallCount); Assert.True(puesto.IsActive);` | ✅ Verifica side-effects reales (no mutación, no delete, persistencia de estado activo) |
| `PuestoRepositoryQueryAsyncTests.cs` | 50-59 | `Assert.Equal(1, totalCount); ... Assert.All(items, i => { Assert.False(i.IsActive); Assert.True(i.IsDeleted); });` | ✅ Verifica contenido y propiedades del segmento |
| `PuestoRepositoryQueryAsyncTests.cs` | 311-314 | `Assert.Equal(codigos.OrderBy(c => c, StringComparer.Ordinal), codigos);` | ✅ Verifica orden monotónico, no solo presencia |
| `PuestosControllerTests.cs` | 645-646 | `Assert.Equal("PuestoConOcupacionesActivas", problem.Title);` | ✅ Verifica código estable propagado a ProblemDetails |

**Sin tautologías** (`expect(true).toBe(true)`, etc.).
**Sin ghost loops** (assertions no iteran sobre colecciones posiblemente vacías).
**Sin mock/assertion ratio elevado** (los tests de comandos usan fakes con semántica, no mocks con `Verify()` exhaustivo).
**Sin implementation-detail coupling** (assertions sobre `IsActive`, `SaveChangesCount`, `Categoria`, `Title`, `TotalCount`, `Codigo`/`Nombre` — todos observables).

**Assertion quality**: ✅ All assertions verify real behavior.

---

## Riesgos residuales

### Heredados de `design.md` (no nuevos)

- **R1:** Specs históricos usan `Purpose/Requirements`; este change usa `REQ-PTO-XXX` + G/W/T. → No aplica a PR1 (es issue de archive posterior).
- **R2:** Mapping `Page` (record) ↔ `page` (HTTP). → Cubierto por `PuestosController.cs:180` y tests `GetConsulta_*`.
- **R3:** Delta doble sobre `puesto-web-listado-detalle-baja` (spec) vs `puesto-management` (proposal). → Concierne al archive, no a PR1 verify.
- **R4:** Ctor primario cambia firma 6 → 7. → DEC-2 mantiene ctor legacy 4; tests existentes (`CrearAsync_*`, `ActualizarAsync_*`) siguen pasando con ctor legacy.
- **R5:** Mapping 409 depende de `Categoria = Conflict` explícito. → DEC-3 implementado y verificado.
- **R6:** Constraint UX activos (columna generada) vs nueva query. → Filtro opera sobre `IsActive/IsDeleted`, no la columna (verificado en `PuestoRepository.cs:145-147`).
- **R7:** `QueryAsync` no usa `Query` base. → DEC-4 documentado en código (línea 125-133).
- **R8:** `[MySqlFact]` skipea sin MySQL. → En esta verificación NO se skipeó ninguno (MySQL local activo en `localhost:3306` con `sgv_test` DB).

### Nuevos observados durante PR1 verify

- **R9 (informativo):** Cobertura del escenario REQ-PTO-002 S1 (DELETE con `status=eliminadas`) se construye combinando un test de integración que cubre el branch `Activas` (`GetConsulta_SinStatus_RetornaActivas`) + un test de controller que cubre el branch de fallback (`GetConsulta_ConStatusInvalido_CaeA_Activas`) + un test unit que cubre el servicio con `Eliminadas` (`QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados`). No hay un test que pegue literalmente `GET /consulta?status=eliminadas&page=1&pageSize=10` y valide el round-trip completo. Aceptable: la lógica de mapeo es trivial (`string.Equals(status, "eliminadas", OrdinalIgnoreCase) ? Eliminadas : Activas`) y queda cubierta por la combinación anterior. No es bloqueante.

---

## Cumplimiento `AGENTS.md`

- ✅ Conventional commits sin Co-Authored-By: `git log` muestra solo `SDD Apply <apply@sdg.local>` como autor (sin IA attribution), mensajes `feat(contracts): …`, `feat(application): …`, `feat(puestos): …`, `feat(api): …`.
- ✅ Sin migraciones innecesarias: `git show --stat` no muestra cambios en `Persistencia/Migraciones/`.
- ✅ Strict TDD respetado: tabla "Evidence TDD (cumplido per test RED→GREEN)" en `apply-progress.md` documenta cada test como "✅ Written primero, pasó" o "✅ Written con la implementación".
- ✅ Sin `Co-Authored-By` en commits: verificado con `git log --pretty=format:"%H %an <%ae>%n%s%n%b" -4`.
- ✅ Artefactos SDD en español: `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md` están en español.

---

## Recomendación de merge

**APPROVE.**

Justificación:

1. **Build limpio**: 0 errores, 0 warnings nuevas.
2. **Cumplimiento funcional completo**: 10/10 escenarios de spec cubiertos con tests que pasan en runtime.
3. **Cero regresiones**: 3010/3010 tests PASS en suite completa.
4. **Decisiones locked respetadas**: DEC-1..DEC-6 verificadas con cobertura de test suficiente. DEC-3 (`Categoria = Conflict` explícito) confirmado crítico y bien implementado — el test API `Delete_ConOcupacionesVigentes_Devuelve409ConProblemDetails` asserta explícitamente HTTP 409 + `Title == "PuestoConOcupacionesActivas"`, garantizando que un descuido futuro en `Categoria = …` haga fallar el test.
5. **TDD respetado**: 6/6 checks del strict-tdd-verify pasados.
6. **Assertion quality OK**: sin tautologías, ghost loops ni mock-heavy tests.
7. **Riesgos residuales no bloqueantes**: ninguno identificado como merge blocker.

Riesgo observable menor: REQ-PTO-002 S1 podría fortalecerse con un round-trip test explícito `?status=eliminadas`, pero la combinación de tests unit + controller branch ya cubre la lógica de mapeo. No bloquea merge; puede agregarse en un follow-up.

PR1 backend listo para merge a `main`. PR2 (web) puede proceder cuando se abra el siguiente slice stacked.

---

## PR2 Web — Listado web paginado y segmentado (slice verificado sobre `feat/209-p2-web`)

> Branch: `feat/209-p2-web` · 3 commits work-unit (`87f7687`, `3dd7fba`, `2d8878a`)
> strict_tdd: true (validado con `strict-tdd-verify.md`)
> Requisito cubierto: **REQ-PTO-020** (spec `web-puestos-paginacion/spec.md`)
> Cambio neto: +431 / −133 líneas en 10 archivos (espejo de `apply-progress.md` tabla "Archivos modificados en PR2")
> Build evidence revision: revalidado sobre `feat/209-p2-web` HEAD `2d8878ab`

### Resumen ejecutivo PR2

PR2 web del change `2026-07-27-completar-puestos-issue-209` cubre REQ-PTO-020 al 100% sin desviaciones de diseño: el toggle Eliminadas pasa de `<span disabled>Próximamente` a `<a>` activo, `LoadAsync` delega a `IPuestosApiClient.QueryAsync` con `PagedResult<T>`, el cliente serializa la query con `StringBuilder` + `Uri.EscapeDataString` (DEC-7 espejo de `CargoApiClient`), el feedback 409 preserva `PuestoConOcupacionesActivas` vía `TempData["ErrorCode"]`, y PRG retiene `p/search/sort/status`. Build limpio (0 errores, 4 warnings pre-existentes NU1510, 0 nuevas), suite focal **1710/1710 PASS** que matchea exactamente `apply-progress.md`. **Veredicto PR2: APPROVE.**

### Metodología

| Paso | Acción | Resultado |
|------|--------|-----------|
| 1 | Lectura de skills (`sdd-verify`, `strict-tdd-verify`, `razor-pages-patterns`, `dotnet-csharp`, `dotnet-best-practices`, `dotnet-xunit`, `pr-review-dotnet`) | OK |
| 2 | Lectura de `verify-report.md` (PR1 cerrado, APPROVE) y merge planificado sin tocar la sección previa | OK |
| 3 | Lectura de `specs/web-puestos-paginacion/spec.md` (REQ-PTO-020, 5 escenarios) y `tasks.md` (T-11..T-15) | OK |
| 4 | Inspección de los 3 commits (`87f7687`, `3dd7fba`, `2d8878a`) sobre `feat/209-p2-web`: `IPuestosApiClient.QueryAsync`, `PuestosApiClient.BuildQueryUri`, `PuestoIndexModel.LoadAsync → QueryAsync`, `OnPostDeleteAsync` 409 feedback, `Index.cshtml` toggle + paginación | OK |
| 5 | Inspección de tests añadidos/modificados: `IPuestosApiClientContractTests` (+1), `PuestosApiClientTests` (8 escenarios `Query_*` + transporte + cancelación), `FakePuestosApiClient` (`QueryHandler`/`QueryCalls`/`QueryException`), `PuestoIndexPageTests` (16 escenarios), `PuestoWebSeamTests` (constructor defaults) | OK |
| 6 | Verificación de fronteras arquitectónicas: `SGV.Web.csproj` solo referencia `SGV.Contracts`; cambios solo en `SGV.Web` y `tests/SGV.Tests/Web/Puesto` | OK |
| 7 | Ejecución de `dotnet build SGV.slnx` | 0 errores, 4 warnings pre-existentes, 0 nuevas |
| 8 | Ejecución de `dotnet test SGV.slnx --filter "FullyQualifiedName~Puesto\|FullyQualifiedName~Cargo\|FullyQualifiedName~Web"` | 1710/1710 PASS, 0 failed, 0 skipped, 1:18 |
| 9 | Cross-reference DEC-1 (type alias), DEC-7 (`BuildQueryUri StringBuilder`), PRG, TempData["ErrorCode"] | OK |
| 10 | Triangulación estricta TDD contra `apply-progress.md §Evidence TDD (PR2)` (17 filas) | 17/17 verificadas |
| 11 | Assertion Quality Audit sobre los 5 archivos de test modificados | Sin tautologías, ghost loops ni mock-heavy |

### Hallazgos PR2

#### CRITICAL

**Sin hallazgos críticos.** Ningún escenario sin covering test, ninguna desviación que rompa el spec, ningún fallo de build o test, ninguna violación de frontera arquitectónica.

#### WARNING

- **W1 (informativo, no bloqueante):** El record legacy `PuestoListQuery` (`PuestoListItemViewModel.cs:63-73`) sigue en el namespace `SGV.Web.Integration.Organizacion` por razones de backward-compat con `PuestoWebSeamTests.PuestoListQuery_Constructor_ExposesContractDefaults`. Todos los call sites de producción migraron al alias `ContractsPuestoListQuery` (= `PuestoListQuery` de Contracts). El record legacy se borrará en un follow-up de limpieza; conviene documentarlo explícitamente en `archive-report.md` (R-legacy-record ya documentado en `apply-progress.md §Riesgos residuales`). Aceptable.
- **W2 (informativo, no bloqueante):** `OnPostReactivateAsync` (`Index.cshtml.cs:255-270`) tiene un comentario inline que documenta una asignación "redundante" de `TempData["ErrorCode"]` para reforzar la garantía. El comentario está bien, pero el bloque de asignación no tiene efecto observable fuera del ya seteado más arriba; es un comentario explicativo, no código muerto. Cosmético.
- **W3 (informativo, no bloqueante):** `MapaCategoriaToLegacyType` (`PuestosApiClient.cs:177-186`) mapea todas las categorías a `PuestoErrorType.Validation` excepto `NotFound`, `Conflict` y `Validation`. Esto preserva source-compat del campo legacy `Type` no nulo. Cosmético y bien documentado en el doc-comment.

#### SUGGESTION

- **S1:** Considerar borrar el record legacy `PuestoListQuery` (`PuestoListItemViewModel.cs:63-73`) cuando el `PuestoWebSeamTests.PuestoListQuery_Constructor_ExposesContractDefaults` se pueda migrar totalmente al record de Contracts. Es un follow-up de limpieza, no de este change.
- **S2:** Documentar en `docs/decisiones-implementacion.md` el patrón "type alias para records migrados a Contracts" cuando se use por primera vez en otro módulo (Habilidades todavía tiene su `HabilidadListQuery` legacy).
- **S3 (recordatorio archivado):** El `archive-report.md` debe reconciliar la delta doble sobre `puesto-management` (REQ-PTO-010, archivado en PR1) + `puesto-web-listado-detalle-baja` (REQ-PTO-020, archivado en PR2). Sigue el plan R3 del `design.md §Riesgos residuales`. No es bloqueante para merge.

### Cumplimiento REQ-PTO-020 — Listado web paginado y segmentado

| Escenario | Cubierto por | Evidencia (archivo:línea) | Status |
|-----------|--------------|----------------------------|:------:|
| **S1:** Carga inicial paginada (consulta segmento Activas por defecto + muestra filas + controles de paginación) | `PuestosApiClientTests.QueryAsync_WithActiveSegmentAndNoOptionalFilters_OmitsStatusAndOptionalParameters` (unit, omite `status`/`search`/`sort`) + `PuestoIndexPageTests.Get_Index_WhenAuthenticated_RendersActivePuestosTable` (integration, asserta `QueryCalls.Single` con `Segmento == Activas`, `Page == 1`, `PageSize == 20`, filas renderizadas) + `PuestoIndexPageTests.Get_Index_WhenListIsEmpty_ShowsEmptyState` (lista vacía con paginación) | `tests/.../PuestosApiClientTests.cs:286-302`; `tests/.../PuestoIndexPageTests.cs:33-86`; `tests/.../PuestoIndexPageTests.cs:221-237`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs:399-409` (LoadAsync QueryAsync); `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml:257-275` (footer paginación Primera/Anterior/Siguiente/Última) | ✅ |
| **S2:** Toggle de eliminadas (`status=eliminadas` conserva búsqueda+orden, reinicia `p=1`, oculta Crear) | `PuestoIndexPageTests.Get_Index_ToggleEliminadas_RendersActiveLinkPreservingFilters` (asserta `status=eliminadas&search=ana&sort=nombre_asc`, no aparece `<span disabled>` ni `Próximamente`) + `Get_Index_WhenDeletedView_DoesNotRenderEditButton` (asserta no `data-bs-title="Editar"`, sí `data-puesto-reactivate-form`) + `Get_Index_StatusEliminadas_QueriesDeletedSegment` (`QueryCalls.Single.Segmento == Eliminadas`) + `Index.cshtml.cs BuildToggleSegmentoRouteValues:358-364` (reset `p=1` + preserva search/sort/status) + `Index.cshtml:77-82` (`!IsDeletedView && EsAdministrador` oculta Crear) | `tests/.../PuestoIndexPageTests.cs:196-215`; `tests/.../PuestoIndexPageTests.cs:93-118`; `tests/.../PuestoIndexPageTests.cs:548-566`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs:358-364`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml:77-82` | ✅ |
| **S3:** Contexto al cambiar de página (conserva segmento, búsqueda, orden + muestra solo página solicitada) | `PuestoIndexPageTests.Get_Index_WithSearchSortAndPage_PreservesQueryContextAndRendersPagination` (asserta `QueryCalls.Single.{Page==2, PageSize==20, Search=="ana", Sort=="nombre_asc", Segmento==Activas}` + renderiza "Página 2 de 2" + 4 controles de paginación con href `p=N&search=ana&sort=nombre_asc`) + `Index.cshtml.cs BuildPagedRouteValues:371-377` (preserva search/sort/status) | `tests/.../PuestoIndexPageTests.cs:568-601`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs:371-377`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml:257-275` | ✅ |
| **S4:** Baja rechazada por ocupaciones (409 con `PuestoConOcupacionesActivas` → feedback específico + puesto visible, sin éxito) | `PuestoIndexPageTests.Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` (asserta 409, mensaje `"El puesto tiene ocupaciones vigentes…"`, badge `PuestoConOcupacionesActivas`, fila `puesto.Nombre` visible) + `Post_Delete_WhenNotFound_ShowsFeedbackAndKeepsRowVisible` (404 recuperable) + `Post_Reactivate_WhenConflictByCodigo_ShowsFeedbackAndKeepsContext` (reactivate 409) + `Index.cshtml.cs OnPostDeleteAsync:177-181` (`TempData["ErrorCode"] = result.Code`) + `Index.cshtml:37-40` (`TempData["ErrorCode"] is string errorCode` → badge) + PRG preserva `p/search/sort/status` (líneas 183-189) | `tests/.../PuestoIndexPageTests.cs:335-375`; `tests/.../PuestoIndexPageTests.cs:381-412`; `tests/.../PuestoIndexPageTests.cs:508-542`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs:168-189`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml:37-40` | ✅ |
| **S5:** Error de transporte (`HttpRequestException`/`TaskCanceledException` preserva contrato transversal + estado recuperable, sin falsear éxito) | `PuestosApiClientTests.QueryAsync_TransportFails_PropagatesNativeException` (`[Theory]` con 3 escenarios `HttpClientExceptionScenarios.TransportExceptionData`) + `PuestosApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` (asserta `LastRequest is null`) + `PuestoIndexPageTests.Get_Index_WhenApiFails_ShowsVisibleError` (`apiClient.QueryException = new HttpRequestException("boom")` → asserta banner `"No se pudo cargar el listado"`, sin éxito falseado) + `Index.cshtml.cs SetLoadErrorState:425-431` (resetea Items/TotalCount/CurrentPage + `LoadErrorMessage`) + `Index.cshtml:44-47` (renderiza `<div class="alert alert-danger">`) | `tests/.../PuestosApiClientTests.cs:304-330`; `tests/.../PuestoIndexPageTests.cs:266-283`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs:411-415`; `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml:44-47` | ✅ |

**REQ-PTO-020** — 5/5 escenarios cubiertos con tests que pasan en runtime. **Status: PASS.**

### Evidencia de tests PR2

```
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Puesto|FullyQualifiedName~Cargo|FullyQualifiedName~Web"
```

| Métrica | Valor |
|---------|------:|
| Total | 1710 |
| Passed | 1710 |
| Failed | 0 |
| Skipped | 0 |
| Duración | 1:18 |

Matchea exactamente `apply-progress.md §Evidencia de tests PR2 (subset focal del orquestador)`: 1710/1710, 0 failed, 0 skipped. Sin regresiones.

**Distribución tests tocados por PR2:**

| Test | Tipo | Cubre spec |
|------|------|------------|
| `IPuestosApiClientContractTests.Interface_ExposesQueryAsyncWithExpectedSignature` | Unit (reflection) | REQ-PTO-020 (forma del contrato) |
| `IPuestosApiClientContractTests.Interface_ExposesExactlySevenPublicMethods` | Unit (reflection) | REQ-PTO-020 (defensa contra refactor) |
| `PuestosApiClientTests.QueryAsync_WithDeletedSegmentAndFilters_SerializesExpectedQueryAndMapsPagedResult` | Unit (HttpMessageHandler fake) | REQ-PTO-020 S1+S2+S3 (DEC-7) |
| `PuestosApiClientTests.QueryAsync_WithActiveSegmentAndNoOptionalFilters_OmitsStatusAndOptionalParameters` | Unit | REQ-PTO-020 S1 (default Activas omite status) |
| `PuestosApiClientTests.QueryAsync_TransportFails_PropagatesNativeException` | Unit (theory 3 casos) | REQ-PTO-020 S5 (transporte) |
| `PuestosApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` | Unit | REQ-PTO-020 S5 (cancelación cooperativa) |
| `PuestoIndexPageTests.Get_Index_WhenAuthenticated_RendersActivePuestosTable` | Integration (`WebApplicationFactory`) | REQ-PTO-020 S1 (render + paginación) |
| `PuestoIndexPageTests.Get_Index_WhenDeletedView_DoesNotRenderEditButton` | Integration | REQ-PTO-020 S2 (toggle + Crear oculto) |
| `PuestoIndexPageTests.Get_Index_ToggleEliminadas_RendersActiveLinkPreservingFilters` | Integration | REQ-PTO-020 S2 (toggle activo, contexto) |
| `PuestoIndexPageTests.Get_Index_StatusEliminadas_QueriesDeletedSegment` | Integration | REQ-PTO-020 S2 (segmento Eliminadas) |
| `PuestoIndexPageTests.Get_Index_WithSearchSortAndPage_PreservesQueryContextAndRendersPagination` | Integration | REQ-PTO-020 S3 (cambio de página) |
| `PuestoIndexPageTests.Get_Index_WithSearch_ReturnsOnlyMatchingServerSideItems` | Integration | REQ-PTO-020 S3 (search server-side) |
| `PuestoIndexPageTests.Get_Index_WhenListIsEmpty_ShowsEmptyState` | Integration | REQ-PTO-020 S1 (lista vacía) |
| `PuestoIndexPageTests.Get_Index_WhenSearchHasNoResults_ShowsEmptyState` | Integration | REQ-PTO-020 S3 |
| `PuestoIndexPageTests.Get_Index_WhenApiFails_ShowsVisibleError` | Integration | REQ-PTO-020 S5 (estado recuperable) |
| `PuestoIndexPageTests.Get_Index_WhenPuestoHasSuperior_RendersLinkPreservingContext` | Integration | REQ-PTO-020 S3 (`returnStatus` preservado) |
| `PuestoIndexPageTests.Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndKeepsLastDeletedId` | Integration | PRG + reactivate banner |
| `PuestoIndexPageTests.Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` | Integration | REQ-PTO-020 S4 (409 sin falsear éxito) |
| `PuestoIndexPageTests.Post_Delete_WhenNotFound_ShowsFeedbackAndKeepsRowVisible` | Integration | REQ-PTO-020 S4 (404 recuperable) |
| `PuestoIndexPageTests.Post_Delete_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied` | Integration | Seguridad (Forbid) |
| `PuestoIndexPageTests.Post_Reactivate_WhenConflictByCodigo_ShowsFeedbackAndKeepsContext` | Integration | REQ-PTO-020 S4 (409 reactivate) |
| `PuestoWebSeamTests.PuestoListQuery_Constructor_ExposesContractDefaults` | Unit (record shape) | DEC-1 contrato |
| `PuestoWebSeamTests.Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive` + `WhenOnPuestosSubroute_SubmenuIsExpanded` | Integration | Shell sidenav PR2 (out-of-scope REQ-PTO-020 pero verificado) |

Total PR2-focal: **23 tests directos en REQ-PTO-020** (incluyendo theory con 3 miembros de `TransportExceptionData`). La suite focal incluye además **~1687 tests** de Cargos/Web/Persistencia que matcheaban el filtro — todos verdes.

### Build evidence PR2

```
dotnet build SGV.slnx --nologo
```

| Métrica | Valor |
|---------|------:|
| Errores | 0 |
| Warnings nuevas (introducidas por PR2) | 0 |
| Warnings pre-existentes | 4 (`NU1510` sobre `Microsoft.Extensions.Configuration.Json` y `…EnvironmentVariables` en `SGV.Infraestructura.csproj` — mismos warnings que PR1) |
| Tiempo | 0:00:00.99 |
| Estado | ✅ Build succeeded |

`SGV.Web.csproj` (`src/SGV.Web/SGV.Web.csproj:11`) confirma que solo referencia `SGV.Contracts` + `Compile Include` del `HealthCheckResponseWriter` (linked, no ProjectReference). **No se introdujeron ProjectReferences a `SGV.Api`/`SGV.Aplicacion`/`SGV.Infraestructura`** — el contrato arquitectónico del repo sigue intacto.

### Decisiones locked — verificación de cumplimiento

| # | Decisión | Implementación verificada | Status |
|---|----------|---------------------------|:------:|
| **DEC-1** | Type alias preserva `PuestoListQuery` legacy sin imports rotos | `src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs:6` — `using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;` + `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs:10` (mismo alias). El record legacy `(string? Search, string? Sort, string? Status, int Page)` permanece en el mismo namespace (`PuestoListItemViewModel.cs:63-73`) para backward-compat. Contrato mirror de `CargoIndexModel` (que adoptó el mismo alias). | ✅ |
| **DEC-2** | Ctor primario 7-parámetros + legacy 4 con null-IOcupacionRepository | Locked en PR1 (`PuestoServicioComandos.cs:17-46`). PR2 no toca firma. Tests legacy `PuestoServicioComandosTests.CrearAsync_*`/`ActualizarAsync_*` siguen pasando (suite focal 1710/1710 confirma). | ✅ (no tocado en PR2) |
| **DEC-3** | `PuestoError.Categoria = ErrorCategoria.Conflict` explícito | Locked en PR1. PR2 consume el resultado en `PuestosApiClient.cs:177-186` (`MapCategoriaToLegacyType`). Verificado por `Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` que asserta `Categoria: ErrorCategoria.Conflict` + código preservado. | ✅ (consumido por PR2) |
| **DEC-4** | `QueryAsync` propio AsNoTracking + Includes; no reusa `Query` base | Locked en PR1 (`PuestoRepository.cs:142-149`). PR2 consume el endpoint en `PuestosApiClient.BuildQueryUri:128-151`. | ✅ (consumido por PR2) |
| **DEC-5** | Repo devuelve `(Items, Total)`; servicio construye `PagedResult<PuestoDto>` | Locked en PR1. PR2 consume `PagedResult<PuestoDto>` en `PuestosApiClient.QueryAsync:54-65` (`response.Content.ReadFromJsonAsync<PagedResult<PuestoDto>>`). PageModel recibe `result.Page`, `result.TotalCount`, `result.PageSize` en `Index.cshtml.cs:403-409`. | ✅ (consumido por PR2) |
| **DEC-6** | Controller no normaliza `page<1`/`pageSize<1` (paridad Cargos) | Locked en PR1. PR2 clampea client-side: `Index.cshtml.cs:104` (`CurrentPage = Math.Max(1, currentPage)`). La clamp-ea del PageModel y la paridad con `CargoIndexModel` están documentadas en `apply-progress.md §Drift`. | ✅ (clamp client-side documentado) |
| **DEC-7** | `BuildQueryUri` con `StringBuilder` + `Uri.EscapeDataString` | **Aplicada en PR2.** `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs:128-151`: `var builder = new StringBuilder($"{BaseRoute}/consulta?page={query.Page}&pageSize={query.PageSize}");`; `query.Search` y `query.Sort` se concatenan con `&search=`/`&sort=` + `Uri.EscapeDataString(...)`; `status=eliminadas` se agrega sólo cuando `query.Segmento == PuestoSegmentoListado.Eliminadas`. Espejo literal de `CargoApiClient.cs:212-238` y `UnidadOrganizativaApiClient.cs:212-223`. Test de contrato: `PuestosApiClientTests.QueryAsync_WithDeletedSegmentAndFilters_SerializesExpectedQueryAndMapsPagedResult:251-283` asserta `search=`/`sort=`/`status=eliminadas` en el `RequestUri?.Query`. | ✅ |

### TDD Compliance (Strict TDD verify sobre PR2)

| Check | Resultado | Detalle |
|-------|-----------|---------|
| TDD Evidence reportado en `apply-progress.md` | ✅ | Sección "Evidence TDD (cumplido per test RED→GREEN en PR2)" con 17 filas |
| Todas las tareas T-11..T-15 tienen tests | ✅ | T-11: 4 tests en `PuestosApiClientTests` + 1 contract test. T-12: `IPuestosApiClientContractTests.Interface_ExposesQueryAsyncWithExpectedSignature`. T-13: 11 tests en `PuestoIndexPageTests` migrados/ampliados. T-14: `Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` + `Post_Reactivate_WhenConflictByCodigo_ShowsFeedbackAndKeepsContext`. T-15: `Get_Index_ToggleEliminadas_RendersActiveLinkPreservingFilters` + `Get_Index_WhenDeletedView_DoesNotRenderEditButton` |
| RED confirmado (tests existen) | ✅ | 5 archivos de test creados o modificados en PR2 (`IPuestosApiClientContractTests`, `PuestosApiClientTests`, `FakePuestosApiClient`, `PuestoIndexPageTests`, `PuestoWebSeamTests`); todos existen en disco y compilan |
| GREEN confirmado (tests pasan) | ✅ | 1710/1710 PASS en suite focal |
| Triangulación adecuada | ✅ | REQ-PTO-020 S1 (4 tests client + 2 integration), S2 (3 integration + 1 unit toggle), S3 (3 integration preservan contexto), S4 (3 integration cubren 409/404 en Delete/Reactivate), S5 (3 unit + 1 integration). Total **23 tests directos + 3 teorías** triangulan 5 escenarios spec |
| Safety Net para archivos modificados | ✅ | `PuestoIndexPageTests` modificado: 11 escenarios migrados para triangular contra `QueryCalls`/`QueryHandler`. `FakePuestosApiClient` modificado: `QueryHandler`/`QueryCalls`/`QueryException`. Tests pre-existentes (e.g. `Get_Index_WhenAuthenticated_RendersActivePuestosTable`, `Get_Index_WhenListIsEmpty_ShowsEmptyState`) ahora verifican `Empty(apiClient.GetAllCalls)` + al menos un `QueryCalls` — el switch `GetAll` → `QueryAsync` es la única ruta soportada |

**TDD Compliance**: 6/6 checks passed.

### Assertion Quality Audit (PR2)

| Archivo | Línea | Aserción representativa | Observación |
|---------|------:|-------------------------|-------------|
| `PuestosApiClientTests.cs` | 278-282 | `Assert.Contains("page=2", …); Assert.Contains($"search={Uri.EscapeDataString(query.Search!)}", …); Assert.Contains("status=eliminadas", …);` | ✅ Verifica serialización URL exacta (no tautología) |
| `PuestosApiClientTests.cs` | 329 | `Assert.Null(handler.LastRequest);` (cancelación precancelada) | ✅ Verifica que NO se envió request (control negativo) |
| `PuestoIndexPageTests.cs` | 82-85 | `var query = Assert.Single(apiClient.QueryCalls); Assert.Equal(PuestoSegmentoListado.Activas, query.Segmento); Assert.Equal(1, query.Page); Assert.Equal(20, query.PageSize);` | ✅ Verifica shape completo de la query server-side |
| `PuestoIndexPageTests.cs` | 312 | `var query = Assert.Single(apiClient.QueryCalls); Assert.Equal(query.Page, ...)` | ✅ Verifica estado capturado |
| `PuestoIndexPageTests.cs` | 587-591 | `Assert.Contains("Página 2 de 2", …); Assert.Contains(">Primera</a>", …);` | ✅ Verifica render textual exacto, no solo presencia |
| `PuestoIndexPageTests.cs` | 373 | `Assert.Contains("PuestoConOcupacionesActivas", refreshedContent, …);` | ✅ Verifica código de error estable visible (no destructivo del feedback recuperable) |
| `IPuestosApiClientContractTests.cs` | 113-115 | `Assert.Equal("cancellationToken", parameters[1].Name); Assert.True(parameters[1].HasDefaultValue);` | ✅ Verifica reflexión del contrato (defensa contra refactor silencioso) |

**Sin tautologías** (`Assert.True(true)`, `Assert.Equal(1, 1)`, etc.).
**Sin ghost loops** — todas las assertions iteran sobre `QueryCalls`/`QueryHandler` que en estos tests siempre tienen ≥1 elemento por construcción.
**Sin mock/assertion ratio elevado** — los tests de integración usan `FakePuestosApiClient` con semántica (no `Mock.Verify()`) y los tests del cliente HTTP usan `RecordingHandler` con assertions sobre `handler.LastRequest`.
**Sin implementation-detail coupling** — assertions sobre `query.Segmento`, `query.Page`, `query.PageSize`, badges visibles, hrefs textuales, paginación textual. No se asserta nada sobre CSS classes internas ni nombres de variables privadas.

**Assertion quality**: ✅ All assertions verify real behavior.

### Test Layer Distribution (PR2)

| Layer | Tests directos | Files | Tools |
|-------|---------------:|------:|-------|
| Unit (cliente HTTP + record shape) | 11 | `PuestosApiClientTests` (8) + `IPuestosApiClientContractTests` (2) + `PuestoWebSeamTests` (1) | `xunit`, `RecordingHandler`, reflection |
| Integration (PageModel + sidenav) | 16 | `PuestoIndexPageTests` (14) + `PuestoWebSeamTests` (2 sidenav) | `WebApplicationFactory`, `FakePuestosApiClient`, `node` para harness JS |
| E2E | 0 | — | (no aplica — Razor Pages integration cubre el entry point) |
| **Total PR2-directos** | **27** | **5 archivos** | |

### Riesgos residuales PR2

#### Heredados de `design.md` (no nuevos)

- **R1:** Specs históricos usan `Purpose/Requirements`; este change usa `REQ-PTO-XXX` + G/W/T. → No aplica (es issue de archive posterior).
- **R2:** Mapping `Page` (record) ↔ `page` (HTTP). → Cubierto por DEC-7 + tests `QueryAsync_*`.
- **R3:** Delta doble sobre `puesto-web-listado-detalle-baja` (spec) vs `puesto-management` (proposal). → El archive reconcilia las dos deltas.
- **R4:** Ctor primario cambia firma 6 → 7. → Cubierto en PR1, no tocado en PR2.
- **R5:** Mapping 409 depende de `Categoria = Conflict` explícito. → Cubierto en PR1, consumido en PR2 (`Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible`).
- **R6:** Constraint UX activos (columna generada) vs nueva query. → Filtro opera sobre `IsActive/IsDeleted`, no la columna (PR1).
- **R7:** `QueryAsync` no usa `Query` base. → DEC-4 PR1.
- **R8:** `[MySqlFact]` skipea sin MySQL. → 0 skipped en esta corrida (MySQL local activo).

#### Dragados de `apply-progress.md`

- **R-legacy-record:** `PuestoListItemViewModel.cs:63-73` sigue exponiendo el record legacy `PuestoListQuery(string? Search, string? Sort, string? Status, int Page)`. Aceptable: backward-compat con `PuestoWebSeamTests.PuestoListQuery_Constructor_ExposesContractDefaults`. Documentado en `archive-report.md` al archivar, borrarlo en follow-up.
- **R-StatusMessage:** Para reactivaciones fallidas, `TempData["ErrorCode"]` se persiste también en `OnPostReactivateAsync:251-254`. Espejo Cargos; no desviación. `Post_Reactivate_WhenConflictByCodigo_ShowsFeedbackAndKeepsContext` valida la persistencia.
- **R-StatusMessage vs transporte:** `OnPostDeleteAsync` setea mensaje pero NO `ErrorCode` para `Categoria == Transport`. El cliente `PuestosApiClient.QueryAsync` propaga la excepción nativa vía `EnsureSuccessStatusCode` (re-throw) y el PageModel la captura con `TransportFailureClassifier.IsTransportFailure(ex)` → `SetLoadErrorState` (líneas 411-415). Sin falsear éxito.

#### Nuevos observados durante PR2 verify

- (Ninguno crítico; los 3 W1/W2/W3 de "Hallazgos" arriba son informativos y ya conocidos.)

### Cumplimiento `AGENTS.md`

- ✅ Conventional commits sin Co-Authored-By: `git log --format='%an <%ae>' 87f7687..2d8878a` retorna `sgv-dev <dev@sgv.local>` en los 3 commits (sin IA attribution). Mensajes: `feat(web): add puestos api client query with pagination`, `feat(web): wire puestos index to segment query and pagination`, `feat(web): enable deleted toggle and pagination controls in Index`.
- ✅ Strict TDD respetado: tabla "Evidence TDD (cumplido per test RED→GREEN en PR2)" de `apply-progress.md` documenta 17 tests con "✅ Written primero" o "✅ Triangulado".
- ✅ Sin `Co-Authored-By` en commits: verificado.
- ✅ Artefactos SDD en español: `apply-progress.md` agregado en español.
- ✅ Branch parity: la rama local es `feat/209-p2-web` que coincide con el `strategy: stacked-to-main` declarado en el preflight (PR2 stacked sobre PR1).
- ✅ Frontera arquitectónica `SGV.Web → SGV.Contracts` preservada: `SGV.Web.csproj:11` solo referencia `..\SGV.Contracts\SGV.Contracts.csproj`.

### Recomendación de merge PR2

**APPROVE.**

Justificación:

1. **Build limpio**: 0 errores, 0 warnings nuevas (las 4 NU1510 son pre-existentes en `SGV.Infraestructura`, no introducidas por PR2).
2. **Cumplimiento funcional completo**: 5/5 escenarios de REQ-PTO-020 cubiertos con tests que pasan en runtime. 23 tests directos en PR2 triangulan los 5 escenarios.
3. **Cero regresiones**: 1710/1710 tests PASS en suite focal (`FullyQualifiedName~Puesto|~Cargo|~Web`); coincide con `apply-progress.md`.
4. **Decisiones locked respetadas**: DEC-1 (type alias en `PuestoListItemViewModel.cs:6` + `Index.cshtml.cs:10`), DEC-7 (`BuildQueryUri` con `StringBuilder` + `Uri.EscapeDataString` en `PuestosApiClient.cs:128-151`) — las dos decisiones materializadas en PR2 están verificadas.
5. **Patrones Razor Pages / PRG / TempData correctos**: `OnPostDeleteAsync` redirige vía `RedirectToPage` preservando `p/search/sort/status` (líneas 148-189); `TempData["ErrorCode"]` persiste `PuestoConOcupacionesActivas` tras 409; el banner muestra badge del código (`Index.cshtml:37-40`); `LoadErrorMessage` recupera estado de transporte sin falsear éxito.
6. **TDD respetado**: 6/6 checks del strict-tdd-verify pasados.
7. **Assertion quality OK**: sin tautologías, ghost loops ni mock-heavy tests; 23 tests directos con assertions sobre comportamiento observable.
8. **Frontera arquitectónica preservada**: `SGV.Web.csproj` solo referencia `SGV.Contracts` (más un `Compile Include` linked de `HealthCheckResponseWriter`). No se introduce ProjectReference a `SGV.Api`/`SGV.Aplicacion`/`SGV.Infraestructura`.
9. **Conventional commits sin Co-Authored-By**: verificado en los 3 commits de `feat/209-p2-web`.

PR2 web listo para merge a `main`.

### Recomendación de merge global (PR1 + PR2)

**APPROVE.**

Justificación compuesta:

- **PR1 backend (REQ-PTO-001/002/010)** APPROVE en sección previa: build limpio, 10/10 escenarios cubiertos, 3010/3010 tests PASS, DEC-1..DEC-6 verificadas (DEC-3 crítico confirmado), 6/6 TDD checks, 0/0 regresiones.
- **PR2 web (REQ-PTO-020)** APPROVE en esta sección: build limpio, 5/5 escenarios cubiertos, 1710/1710 tests PASS, DEC-1 + DEC-7 verificadas, 6/6 TDD checks, 0/0 regresiones, frontera arquitectónica preservada.

El change `2026-07-27-completar-puestos-issue-209` cierra la brecha end-to-end: backend segmentado/paginado/protegido + web que lo consume sin cambios de contrato. `apply-report.md` debe sincronizar las dos deltas (`puesto-management` REQ-PTO-010 desde PR1 + `puesto-web-listado-detalle-baja` REQ-PTO-020 desde PR2) en `archive-report.md`.