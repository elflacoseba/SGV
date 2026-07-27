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