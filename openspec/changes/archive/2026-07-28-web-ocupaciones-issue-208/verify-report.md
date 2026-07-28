schema: gentle-ai.verify-result/v1
evidence_revision: sha256:d666bd3e575c059a6b20fabb0a1a7e0b7d9a4dbcc3de456c84359416d11708de
verdict: fail
blockers: 2
critical_findings: 2
requirements: 26/26
scenarios: 76/76
test_command: dotnet test SGV.slnx --no-build --filter "Ocupacion"; dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Tests.Web"
test_exit_code: 1
test_output_hash: sha256:19e1ed6f9bd8e1b2aac4627ec426252064c053b07338ccbfc4349d8654ad4bc5
build_command: dotnet build SGV.slnx --nologo
build_exit_code: 0
build_output_hash: sha256:d666bd3e575c059a6b20fabb0a1a7e0b7d9a4dbcc3de456c84359416d11708de

# Verify Report: Issue #208 — Módulo Web de Ocupaciones

> Change: `2026-07-28-web-ocupaciones-issue-208`
> Specs: 4 delta specs
> Total REQs: 26
> Modo: Strict TDD
> Rama verificada: `develop` (`92937641`, alineada con `origin/develop`)

## Resumen ejecutivo

La implementación cubre los 26 requisitos y las 24 tasks, respeta las decisiones locked y compila sin errores. La suite completa pasó `2899/3153`, con 254 tests MySQL skipeados por el harness pese a que MySQL está disponible. El verdict es **FAIL** porque la ejecución literal del filtro `Ocupacion` tuvo 2 fallos de contaminación de datos y el filtro solicitado `Web.*` no encontró ningún test; el filtro semánticamente correcto de Web pasó `1265/1265`.

## Completitud

| Métrica | Resultado |
|---|---:|
| Tasks totales | 24 |
| Tasks completadas | 24 |
| Tasks incompletas | 0 |
| REQs totales | 26 |
| REQs con implementación | 26 |
| Escenarios verificados | 76/76 |

`apply-progress.md` contiene evidencia TDD para T-001..T-024, y los archivos de tests referenciados existen. No se encontraron tareas pendientes.

## Specs verificados

### REQ-OCC-API-001..006 — Contrato API

| REQ | Estado | Evidencia |
|---|---|---|
| REQ-OCC-API-001 | ✅ PASS | Wire-types en `src/SGV.Contracts/Ocupaciones/`; `OcupacionContractsTests`; `SGV.Web.csproj` referencia únicamente `SGV.Contracts`. |
| REQ-OCC-API-002 | ✅ PASS | `OcupacionesController.Get` usa `status` con default `activas`; no expone `includeHistory`; `OcupacionesControllerTests` cubre activa/historial. |
| REQ-OCC-API-003 | ✅ PASS | `OcupacionRepository.QueryAsync` aplica `PersonaId`/`PuestoId` antes de `Count` y `Skip/Take`, combinados con AND; tests de aplicación, API y MySQL. |
| REQ-OCC-API-004 | ✅ PASS | `OcupacionCommandResult` en Contracts con `ErrorCategoria`, `Code`, `Message` y `FieldErrors`; tests de contratos, comandos y cliente. |
| REQ-OCC-API-005 | ✅ PASS | `[Authorize]` en controller y `[Authorize(Roles = RolesSgv.Administrador)]` en writes; tests API de 401/403. |
| REQ-OCC-API-006 | ✅ PASS | `PagedResult<OcupacionDto>` con total después de filtros y paginación; tests API/servicio y `OcupacionRepositoryQueryAsyncTests`. |

### REQ-OCC-LST-001..006 — Listado

| REQ | Estado | Evidencia |
|---|---|---|
| REQ-OCC-LST-001 | ✅ PASS | `IOcupacionApiClient`/`OcupacionApiClient`, registro typed `HttpClient`, timeout 10 s y bearer handler en `src/SGV.Web/Program.cs`; tests de contrato y transporte. |
| REQ-OCC-LST-002 | ✅ PASS | `IndexModel` delega filtros/paginación a API; `OcupacionIndexPageTests` cubre carga, filtros, vacío y metadatos. |
| REQ-OCC-LST-003 | ✅ PASS | Toggle `status=activas|eliminadas`, preservación de filtros y reinicio de página; tests de toggle. |
| REQ-OCC-LST-004 | ✅ PASS | Manejo con `TransportFailureClassifier`, `CommandResultMapper`/feedback y sin falso éxito; tests de errores HTTP/transporte/PRG. |
| REQ-OCC-LST-005 | ✅ PASS | `_Sidenav.cshtml` muestra Listado a autenticados y Nuevo sólo a Administrador; `OcupacionSidenavTests`. |
| REQ-OCC-LST-006 | ✅ PASS | Acciones por estado/rol en `Index.cshtml`; tests para vigente, histórica y readonly. |

### REQ-OCC-FORM-001..008 — Formularios

| REQ | Estado | Evidencia |
|---|---|---|
| REQ-OCC-FORM-001 | ✅ PASS | `Create.cshtml.cs` + `_Form.cshtml`, catálogos Persona/Puesto, autorización Admin y precarga contextual; `OcupacionCreatePageTests`. |
| REQ-OCC-FORM-002 | ✅ PASS | `EditModel` bloquea estado no vigente antes de mutar, en GET y POST; `OcupacionEditPageTests`. |
| REQ-OCC-FORM-003 | ✅ PASS | `DetailsModel` implementa Finalizar/Eliminar/Reactivar con gates por rol/estado y PRG; `OcupacionDetailsPageTests`. |
| REQ-OCC-FORM-004 | ✅ PASS | DataAnnotations, mapeo de `FieldErrors` a `ModelState` y repoblación de catálogos; tests de validación/Create/Edit. |
| REQ-OCC-FORM-005 | ✅ PASS | `MapConflictToModelState` diferencia `PersonaYPuestoOcupados` y `PuestoOcupado`; tests de conflictos 409. |
| REQ-OCC-FORM-006 | ✅ PASS | PRG y `TempData` en altas, edición y transiciones; tests de éxito/fallo. |
| REQ-OCC-FORM-007 | ✅ PASS | Validación cliente (`min`) y servidor `FechaFin >= FechaInicio`; tests aseguran que fecha inválida no invoca API. |
| REQ-OCC-FORM-008 | ✅ PASS | Details conserva estado histórico y muestra códigos específicos ante 409 de Reactivar; tests de colisión par/puesto. |

### REQ-OCC-NAV-001..006 — Navegación contextual

| REQ | Estado | Evidencia |
|---|---|---|
| REQ-OCC-NAV-001 | ✅ PASS | `PersonaOcupacionesModel` verifica Persona activa y consulta `Segmento=Activas` + `PersonaId`; 13 tests. |
| REQ-OCC-NAV-002 | ✅ PASS | `PuestoOcupacionesModel` verifica Puesto disponible y consulta `Segmento=Activas` + `PuestoId`; 11 tests. |
| REQ-OCC-NAV-003 | ✅ PASS | Links “Ver ocupaciones” en Details de Persona/Puesto sólo para entidades activas; tests de ambos detalles. |
| REQ-OCC-NAV-004 | ✅ PASS | `_CrossList.cshtml` no contiene toggle; PageModels ignoran `status=eliminadas`; tests HTML/query fija. |
| REQ-OCC-NAV-005 | ✅ PASS | Botón Volver apunta al Details dueño; decisión documentada en DEC-15 y tests de retorno. |
| REQ-OCC-NAV-006 | ✅ PASS | CTA Nueva sólo Admin y pasa `personaId`/`puestoId` a Create; tests de gate y precarga. |

## Tasks verificados

| Task | Estado | Evidencia |
|---|---|---|
| T-001 | ✅ COMPLETADO | Wire-types/enums/routes en `src/SGV.Contracts/Ocupaciones/`. |
| T-002 | ✅ COMPLETADO | `OcupacionCommandResult` migrado a Contracts con `ErrorCategoria` y compat legacy. |
| T-003 | ✅ COMPLETADO | `IOcupacionServicioConsulta.QueryAsync(OcupacionListQuery)`. |
| T-004 | ✅ COMPLETADO | `OcupacionRepository.QueryAsync` server-side. |
| T-005 | ✅ COMPLETADO | `OcupacionesController.Get(status, filtros, paginación)`. |
| T-006 | ✅ COMPLETADO | `OcupacionesControllerTests` actualizado y ampliado. |
| T-007 | ✅ COMPLETADO | `OcupacionRepositoryQueryAsyncTests` con `[MySqlFact]`. |
| T-008 | ✅ COMPLETADO | `IOcupacionApiClient`/`OcupacionApiClient`. |
| T-009 | ✅ COMPLETADO | `OcupacionListItemViewModel`. |
| T-010 | ✅ COMPLETADO | `Index.cshtml`/`Index.cshtml.cs`. |
| T-011 | ✅ COMPLETADO | Registro DI en `Program.cs`. |
| T-012 | ✅ COMPLETADO | Entrada Ocupaciones en `_Sidenav.cshtml`. |
| T-013 | ✅ COMPLETADO | Fake y tests de Index/cliente/errores. |
| T-014 | ✅ COMPLETADO | `OcupacionInputModel` y Details VM. |
| T-015 | ✅ COMPLETADO | `_Form.cshtml` + `IOcupacionForm`. |
| T-016 | ✅ COMPLETADO | Create + validación/conflictos/PRG. |
| T-017 | ✅ COMPLETADO | Edit sólo vigente. |
| T-018 | ✅ COMPLETADO | Details + ciclo de vida. |
| T-019 | ✅ COMPLETADO | Tests CRUD y mutaciones cliente. |
| T-020 | ✅ COMPLETADO | `PersonaOcupaciones`. |
| T-021 | ✅ COMPLETADO | `PuestoOcupaciones`. |
| T-022 | ✅ COMPLETADO | Enlaces contextuales desde Details. |
| T-023 | ✅ COMPLETADO | Retorno/precarga contextual. |
| T-024 | ✅ COMPLETADO | 24 tests de navegación cruzada. |

## Decisiones locked verificadas

### Proposal — 9 decisiones

| Decisión | Estado | Evidencia |
|---|---|---|
| `status=activas|eliminadas` | ✅ RESPETADA | Controller, repositorio, Index y tests. |
| Filtros `personaId`/`puestoId` en endpoint único | ✅ RESPETADA | `OcupacionListQuery`, controller y repository; no hay subrecursos anidados. |
| Migrar `OcupacionCommandResult` a `ErrorCategoria` | ✅ RESPETADA | Contracts + mappers + tests. |
| Wire-types exclusivamente en `SGV.Contracts` | ✅ RESPETADA | Carpeta Contracts y dependencia leaf. |
| Cliente Web tipado | ✅ RESPETADA | DI + bearer handler + fake. |
| Cuatro Razor Pages CRUD | ✅ RESPETADA | Index/Create/Edit/Details implementadas. |
| Navegación cruzada Persona/Puesto | ✅ RESPETADA | Dos páginas cross-list + links desde Details. |
| Sidenav colapsable con gates | ✅ RESPETADA | `_Sidenav.cshtml` + tests. |
| Delivery en 4 slices | ✅ RESPETADA | PRs #212, #213, #214 y #215 mergeados a develop. |

### Design — decisiones técnicas

| Decisión | Estado | Evidencia |
|---|---|---|
| Dominio sin cambios | ✅ RESPETADA | No hay cambios de Ocupacion en Dominio en el diff del change. |
| Query server-side con segmento/filtros/total antes de paginar | ✅ RESPETADA | `OcupacionRepository.QueryAsync`. |
| Índices existentes y cero migraciones | ✅ RESPETADA | Diff de Migraciones vacío; no se agregaron migraciones. |
| DTO enum `OcupacionEstado` con wire string estable | ✅ RESPETADA | Contract test de serialización. |
| Cliente con cancelación/transporte nativo | ✅ RESPETADA | `OcupacionApiClient` + tests. |
| Pages y autorización según estado | ✅ RESPETADA | PageModels, Razor gates y tests Web. |
| Cross-pages con filtro fijo Activas | ✅ RESPETADA | PageModels y tests de status inyectado. |
| `ReturnUrl`/contexto seguro | ✅ RESPETADA | Volver al Details dueño y precarga de Create; DEC-15/16 documentadas. |
| Breaking changes `includeHistory`, DTO, tipos y firmas | ✅ RESPETADA | Código actualiza controller, service, repository, contracts y tests. |
| `SGV.Contracts` leaf / sin NuGet nuevo | ✅ RESPETADA | Project references y build; no nuevas dependencias del change. |
| TDD estricto por work unit | ✅ RESPETADA CON OBSERVACIÓN | Tabla TDD completa y archivos presentes; una ejecución focal está contaminada por MySQL persistente. |

## Validaciones técnicas

| Validación | Resultado |
|---|---|
| `dotnet build SGV.slnx --nologo` | ✅ 0 errores, 4 warnings NU1510 preexistentes |
| `dotnet test SGV.slnx --no-build --filter "Ocupacion"` | ❌ 271/273 passed, 2 failed en `sgv_test` por datos persistentes; corrida aislada alternativa: 251 passed/22 skipped |
| `dotnet test SGV.slnx --no-build --filter "Web.*"` | ❌ 0 tests encontrados; el patrón no coincide con FQNs xUnit |
| Filtro Web corregido `FullyQualifiedName~Tests.Web` | ✅ 1265/1265 passed |
| Suite completa con DB aislada configurada | ✅ 2899 passed, 254 skipped, 0 failed de 3153 |
| MySQL local | ✅ `mysqladmin ping` responde; los `[MySqlFact]` del harness quedaron skipeados con la DB alternativa |
| Boundary check solicitado | ✅ 0 hits |
| Referencias de `SGV.Web` | ✅ Sólo `../SGV.Contracts/SGV.Contracts.csproj` |
| Sin migraciones no esperadas | ✅ Diff vacío en `src/SGV.Infraestructura/Persistencia/Migraciones/` |
| Breaking changes design.md líneas 170–178 | ✅ Todos reflejados en controller, contracts, servicio, repository y tests |
| `bun run build` | ➖ No aplica: no se modificaron assets/frontend pipeline |

## TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD Evidence reported | ✅ | `apply-progress.md` contiene tablas para los cuatro slices. |
| All tasks have tests | ✅ | T-001..T-024 tienen archivo/escenario o cobertura explícita. |
| RED confirmed (tests exist) | ✅ | Archivos listados existen. |
| GREEN confirmed (tests pass) | ⚠️ | Los tests web pasan; la ejecución focal `Ocupacion` no es limpia por 2 fallos MySQL de contaminación. |
| Triangulation adequate | ✅ | Las tablas declaran 76 escenarios y los tests cubren variaciones observables. |
| Safety net | ✅ | Evidencia declarada en apply-progress para archivos modificados/refactors. |

**TDD Compliance**: 5/6 checks plenamente pasados; 1 con warning operacional.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit | incluido en suite | múltiples | xUnit |
| Integration WAF | 1265 en filtro Web | múltiples | WebApplicationFactory |
| MySqlFact | 0 ejecutados; 254 skipeados en suite | múltiples | MySqlFact |
| E2E | 0 | 0 | No disponible |

## Changed File Coverage

La corrida de cobertura sí generó `coverage.cobertura.xml`, pero el reporte no permite atribuir cobertura de forma confiable a los archivos actuales movidos/renombrados: los PDB conservan rutas/clases históricas de `SGV.Aplicacion` para varios tipos migrados, y los archivos actuales aparecen con cobertura 0 aunque sus tests pasan. Se reporta como **no determinable**, no como cobertura funcional cero.

## Assertion Quality

✅ No se detectaron tautologías ni assertions sin llamada a producción. Los `Assert.Empty` encontrados están asociados a escenarios explícitos de “sin resultados” o a guardas de “no invocar mutación”, con escenarios complementarios no vacíos/positivos. No se encontraron ghost loops.

## Hallazgos

### CRITICAL

1. La validación literal `dotnet test SGV.slnx --no-build --filter "Ocupacion"` terminó `271/273`, con fallos en `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminadasYFinalizadas` (esperado 2, actual 3) y `OcupacionRepositoryTests.ListAllIncludingHistoryAsync_ReturnsAllRows` (esperado 3, actual 5). La causa coincide con contaminación de datos persistente documentada en `apply-progress.md`; aun así, el gate de verify exige exit code 0.
2. La validación literal `--filter "Web.*"` no encontró tests (`No test matches`). La suite Web sí pasa con el filtro real `FullyQualifiedName~Tests.Web`, pero el comando solicitado no demuestra ejecución.

### WARNING

1. MySQL responde localmente, pero los tests `[MySqlFact]` se skipearon al usar la DB aislada `sgv_test_208`; la suite completa reportó 254 skips. La cobertura de persistencia específica no queda demostrada en esta corrida.
2. Existen 91 warnings preexistentes documentados por apply-progress; el build actual mostró 4 warnings NU1510 durante esta ejecución. No se observaron errores nuevos.
3. La cobertura por archivo modificado no es determinable por paths históricos tras movimientos de wire-types; el artefacto de cobertura fue generado pero no es confiable para el delta actual.
4. El diff acumulado y el PR de Slice 3a excedieron ampliamente el soft-cap LOC del design; fue documentado durante apply y no rompe requisitos funcionales, pero debe conservarse como excepción de delivery.

### SUGGESTION

1. Limpiar/recrear una base de test dedicada y ajustar `MySqlFactAttribute`/`TestSgvDbContextFactory` para que el override de `ConnectionStrings__SgvDatabase` se use realmente; repetir el filtro `Ocupacion` hasta obtener 0 fallos y ejecutar los MySqlFact.
2. Repetir el filtro Web con el nombre FQN documentado (`FullyQualifiedName~Tests.Web`) en el contrato de verify, o corregir el alias operativo `Web.*`.
3. Regenerar cobertura con símbolos/rutas normalizados para files moved/renamed.

## Verdict

**FAIL**

La implementación está completa y es coherente con specs/design/tasks, pero no cumple el gate de runtime exigido: una prueba focal falla por datos persistentes y uno de los comandos obligatorios no ejecuta tests.

## Próximos pasos

Remediar los dos hallazgos críticos operacionales, repetir build + filtros focales + MySQLFact, y volver a ejecutar `sdd-verify`. Sólo con todos los comandos obligatorios en exit code 0 corresponde proceder con `sdd-archive`.

## Evidence hashes

- Build output: `sha256:d666bd3e575c059a6b20fabb0a1a7e0b7d9a4dbcc3de456c84359416d11708de`
- Full test output: `sha256:19e1ed6f9bd8e1b2aac4627ec426252064c053b07338ccbfc4349d8654ad4bc5`
- Coverage output: `sha256:eb223e82ead64cc9a85483f9a7f6de5bfa3107a8fa2743ab8ff9ff76f9f70b5e`
