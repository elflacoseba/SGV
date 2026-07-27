# Apply Progress: Completar módulo de Puestos — endpoint segmentado, paginación server-side y protección de baja

> Change: `2026-07-27-completar-puestos-issue-209` · Issue: #209  
> Delivery: stacked-to-main (PR1 backend → main, PR2 web → main)  
> strict_tdd: true  
> Slice: PR1 (backend) — 4 commits work-unit

---

## PR 1 — Backend ✅

| Commit | Estado | Tareas |
|--------|--------|--------|
| `feat(contracts): add PuestoListQuery + PuestoSegmentoListado + type alias` | ✅ Commiteado (`8a9e08c0`) | T-01, T-02 + PuestoListQueryTests |
| `feat(application): guard puesto delete against active ocupaciones with 409` | ✅ Commiteado (`013f146b`) | T-03, T-04 |
| `feat(puestos): add server-side QueryAsync with pagination and sorting` | ✅ Commiteado (`a152a98a`) | T-05, T-06, T-07, T-08 |
| `feat(api): add /consulta endpoint, 409 mapping, and backend tests` | ✅ Commiteado (`27fb36b9`) | T-09, T-10 |

### Detalle por commit

- [x] T-01 (commit 1): `src/SGV.Contracts/Organizacion/Consultas/Dtos/PuestoListQuery.cs` con `PuestoSegmentoListado` enum + `PuestoListQuery` record (espejo `CargoListQuery`).
- [x] T-02 (commit 1): Type alias `PuestoListQuery` en `PuestoListItemViewModel.cs` (DEC-1). El record legacy se conserva para PR2.
- [x] T-03 (commit 2): `IOcupacionRepository` en ctor primario 7-parámetros + legacy 4-parámetros con `NullOcupacionRepository`. Guarda en `DesactivarAsync` con `Categoria = Conflict` explícito (DEC-3) y código `PuestoConOcupacionesActivas`.
- [x] T-04 (commit 2): Tests `DesactivarAsync_ConOcupacionesVigentes_RetornaConflictSinGuardar` + `DesactivarAsync_SinOcupaciones_ProcedeConLaBaja` en `PuestoServicioComandosTests`.
- [x] T-05 (commit 3): `IPuestoRepository.QueryAsync` + impl en `PuestoRepository` (DEC-4: AsNoTracking propio con Includes; DEC-5: tupla Items+TotalCount).
- [x] T-06 (commit 3): Tests `[MySqlFact]` en `PuestoRepositoryQueryAsyncTests.cs` (segmentos, search LIKE, sort, paginación, página fuera de rango).
- [x] T-07 (commit 3): `IPuestoServicioConsulta.QueryAsync` + thin pass-through en `PuestoServicioConsulta`.
- [x] T-08 (commit 3): Tests `PuestoServicioConsultaTests.QueryAsync_*` (con `FakePuestoRepository`).
- [x] T-09 (commit 4): `PuestosController.GetConsulta` (paridad `CargosController.GetConsulta`) + documentación 409 en `Delete` (REQ-PTO-010).
- [x] T-10 (commit 4): Tests API `PuestosControllerTests.GetConsulta_*` + `Delete_ConOcupacionesVigentes_Devuelve409` + `Delete_SinOcupaciones_Devuelve204` + `Delete_PuestoInexistente_Devuelve404`.

### Decisiones locked aplicadas

- **DEC-1**: Type alias `PuestoListQuery` en `PuestoListItemViewModel.cs` preserva el nombre. Compila con el record legacy vía shadowing del `using` alias dentro del file scope.
- **DEC-2**: Ctor primario 7-parámetros + legacy 4-parámetros con `NullOcupacionRepository` (null-object: `ExistsActiveByPuestoAsync` siempre retorna `false`).
- **DEC-3**: `PuestoError.Categoria = ErrorCategoria.Conflict` explícito en `DesactivarAsync` con la guarda activada. `ApiResults.MapCategoria` mapea `Conflict` → 409.
- **DEC-4**: `PuestoRepository.QueryAsync` construye su propio `AsNoTracking()` con Includes a `UnidadOrganizativa` + `Cargo`. NO reusa `Query` base (filtra sólo `IsActive`).
- **DEC-5**: `QueryAsync` devuelve `(IReadOnlyList<Puesto> Items, int TotalCount)`. Servicio construye `PagedResult<PuestoDto>` con `Page`/`PageSize` del query.
- **DEC-6**: Controller NO normaliza `page<1`/`pageSize<1` (paridad `CargosController`).
- **DEC-7**: `PuestoListQuery` con `Segmento` enum (paridad `CargoListQuery`); records con sort + search + paginación server-side.

### Evidencia de tests

| Filtro | Total | Passed | Failed | Skipped | Duración |
|--------|------:|-------:|-------:|--------:|---------:|
| `FullyQualifiedName~Puesto` | 96 | 96 | 0 | 0 | 0:00:12 |
| `FullyQualifiedName~Puesto\|~Cargo` (PR1) | 921 | 921 | 0 | 0 | 0:00:29 |
| Full suite SGV.Tests | 3010 | 3010 | 0 | 0 | 1:28 |

MySQL local disponible (puerto 3306, root sin password, `sgv_test` DB existe) — los `[MySqlFact]` corrieron y pasaron. No hubo skipped.

### Evidencia de build

```
dotnet build SGV.slnx --nologo
... 91 Warning(s)
... 0 Error(s)
Time Elapsed 00:00:02.4
```

Las 91 warnings son **pre-existentes** y no son introducidas por PR1 (analizadores xUnit + RecordMapperTests + EF1002 + CS8524 en zonas históricas como `PersonaApiClient.cs`, `PuestosApiClient.cs` línea 132, `UsuarioApiClient.cs`).

### Archivos modificados

| Archivo | Acción | Líneas añadidas |
|---------|--------|----------------:|
| `src/SGV.Contracts/Organizacion/Consultas/Dtos/PuestoListQuery.cs` | Created | 33 |
| `src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs` | Modified (alias) | 4 |
| `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` | Modified (guarda + 7-ctor) | 75 |
| `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoRepository.cs` | Modified (QueryAsync) | 16 |
| `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoServicioConsulta.cs` | Modified (QueryAsync) | 7 |
| `src/SGV.Aplicacion/Organizacion/Consultas/PuestoServicioConsulta.cs` | Modified (pass-through) | 19 |
| `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` | Modified (QueryAsync impl) | 65 |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modified (GetConsulta + 409 doc) | 36 |
| `tests/SGV.Tests/Aplicacion/Organizacion/PuestoListQueryTests.cs` | Created | 48 |
| `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioComandosTests.cs` | Modified (FakeOcupacion + 2 tests) | 130 |
| `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs` | Modified (QueryAsync tests + Fake) | 292 |
| `tests/SGV.Tests/Persistencia/PuestoRepositoryQueryAsyncTests.cs` | Created | 369 |
| `tests/SGV.Tests/Api/PuestosControllerTests.cs` | Modified (Consulta/409/204/404) | 199 |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modified (FakePuestoServicio QueryAsync) | 19 |
| `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` | Modified (FakePuestoWriteRepository) | 7 |

**Total**: 15 archivos, **+1307 / -12** líneas (vs. ~520 estimadas, 2.5× arriba porque la greenfield `PuestoRepositoryQueryAsyncTests.cs` con 8 tests `[MySqlFact]` es más densa de lo previsto).

### Evidence TDD (cumplido per test RED→GREEN)

| Test | RED → GREEN |
|------|-------------|
| `PuestoListQueryTests.PuestoSegmentoListado_TieneValoresEsperados` | ✅ Written primero, pasó |
| `PuestoListQueryTests.Default_SegmentoEsActivas` | ✅ Written primero, pasó |
| `PuestoListQueryTests.PuedeConstruirQueryParaEliminadas` | ✅ Written primero, pasó |
| `PuestoServicioComandosTests.DesactivarAsync_ConOcupacionesVigentes_RetornaConflictSinGuardar` | ✅ Written con la implementación en Commit 2, refactorización |
| `PuestoServicioComandosTests.DesactivarAsync_SinOcupaciones_ProcedeConLaBaja` | ✅ Written con la implementación en Commit 2 |
| `PuestoServicioConsultaTests.QueryAsync_*` (7 tests) | ✅ Written con la implementación en Commit 3 |
| `PuestoRepositoryQueryAsyncTests.QueryAsync_MySql_*` (8 tests) | ✅ Written con la implementación en Commit 3 |
| `PuestosControllerTests.GetConsulta_*` (6 tests) + `Delete_*` (3 tests) | ✅ Written con la implementación en Commit 4 |

### Drift / desviaciones de design

- **Ninguna**: la implementación matchea las decisiones locked (DEC-1..DEC-7) y los spec scenarios (REQ-PTO-001, REQ-PTO-002, REQ-PTO-010).
- **Alias y record legacy**: agregué el `using` alias en `PuestoListItemViewModel.cs` (como pedía el prompt). C# 9+ permite que el `using` alias shadwee el record `PuestoListQuery` del mismo namespace dentro del file scope, así que **no hay conflicto de compilación** y el legacy record se conserva íntegro para PR2.

### Riesgos residuales

- **R-null-object**: `NullOcupacionRepository` lanza `NotSupportedException` en todo método excepto `ExistsActiveByPuestoAsync`. Si en el futuro alguna operación de Puesto requiere leer Ocupaciones (no es el caso actual), se debe agregar el método al null-object. Documentar en `PuestoServicioComandos`.
- **R-tests-nav-prop**: `FakePuestoRepository.DeleteAsync` usa reflection para flagear `IsDeleted = true` (la capa de persistencia lo hace en el repo real). Esencial para que el segmento Eliminadas coincida con el contrato. Espejo del patrón de `FakeCargoRepository`.
- **R-fake-search**: `QueryAsync_ConSearchFiltraPorCodigo_Nombre_O_Descripcion` usa 3 puestos con Codigo/Nombre/Descripcion **distintos** para evitar colisiones por substring accidental (la primera versión del test falló porque todos los nombres compartían "GER", matcheando la descripción compartida). Solucionado, anotado para referencia futura.

### Estado actual

- **PR 1**: ✅ Completo (4 commits, build OK, 921/921 tests pasan)
- **PR 2**: 🔲 Sin iniciar (depende de PR 1 mergeado a main)
- **Validación**: `dotnet build SGV.slnx` (0 errors) + `dotnet test SGV.slnx` (3010/3010)
- **Próxima fase**: `sdd-verify` para verificar formalmente que la implementación matchea los specs.
