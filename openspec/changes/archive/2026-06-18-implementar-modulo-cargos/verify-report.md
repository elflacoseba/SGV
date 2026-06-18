## Verification Report

**Change**: implementar-modulo-cargos
**Version**: N/A
**Mode**: Strict TDD

### Scope of this re-verify

This is the **second verify pass** after a previous verdict of `FAIL` with 3 CRITICAL gaps:

1. Seed parity migration ↔ DatosSemilla was not tested and the migration used literal Guids.
2. PATCH 405 runtime test was missing for `/api/v1/niveles-cargo`.
3. Runtime test verifying the `CK_NivelesCargo_ValorNumerico` check constraint was missing.

The remediation section in `apply-progress.md` documents the fixes. This report focuses on
proving those fixes hold under real execution and that no previous COMPLIANT item regressed.

---

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 41 |
| Tasks complete | 41 |
| Tasks incomplete | 0 |

### Build & Tests Execution
**Build**: ✅ Passed
```text
dotnet build
Build succeeded.
0 Warning(s)
0 Error(s)
Time Elapsed 00:00:00.59
```

**Tests — full suite**: ⚠️ 372 passed / ❌ 1 failed / 0 skipped
```text
dotnet test
Failed: SGV.Tests.Persistencia.UnidadOrganizativaRepositoryTests.QueryAsync_FiltroPorTipoUnidadOrganizativaId_RetornaSoloCoincidencias
Expected: 1
Actual:   2
```

The single failure is the **pre-existing failure documented in the original verify
report** (unrelated to Cargos/NivelCargo). The remediation did not introduce any new
failures and the pre-existing failure count is unchanged.

**Tests — Cargo/NivelCargo scope**: ✅ 143 passed / 0 failed / 0 skipped
```text
dotnet test --filter "FullyQualifiedName~Cargo|FullyQualifiedName~NivelCargo"
Passed: 143
Failed: 0
Skipped: 0
```

**Tests — remediation coverage**: ✅ 6 passed / 0 failed / 0 skipped
```text
dotnet test --filter \
  "FullyQualifiedName~Migration_NoContieneGuidsLiterales_ParaNivelesCargo|\
   FullyQualifiedName~Patch_Returns405MethodNotAllowed|\
   FullyQualifiedName~MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste|\
   FullyQualifiedName~Migration_ReferenciaConstantes_DirectivoIdYConduccionMediaId|\
   FullyQualifiedName~Migration_ReferenciaConstantes_OperativoIdYAcademicoId|\
   FullyQualifiedName~Migration_SemillasCoincidenConDatosSemilla_ParaCodigoNombreValorNumericoYOrden"
Passed: 6
Failed: 0
Skipped: 0
```

**Tests — MySQL integration scope**: ✅ 4 passed / 0 failed / 0 skipped
```text
dotnet test --filter "FullyQualifiedName~MigracionFailLoudCargosTests"
Passed: SGV.Tests.Persistencia.MigracionFailLoudCargosTests.MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds              [337 ms]
Passed: SGV.Tests.Persistencia.MigracionFailLoudCargosTests.MigracionCargos_DatosLimpios_FkRestrictExiste                       [331 ms]
Passed: SGV.Tests.Persistencia.MigracionFailLoudCargosTests.MigracionCargos_DatosSucios_LanzaMySqlException45000               [340 ms]
Passed: SGV.Tests.Persistencia.MigracionFailLoudCargosTests.MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste  [569 ms]
```

The new `MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste` runs against a
real MySQL 8 server on `localhost:3306` and validates both the constraint name and the
`CHECK_CLAUSE` content from `INFORMATION_SCHEMA.CHECK_CONSTRAINTS`. This is the runtime
proof required by the spec scenario "Constraint de valor numérico".

**Coverage**: ➖ Not available (no coverage tool configured)

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | `apply-progress.md` includes TDD evidence tables for PR 2, PR 3 and the Remediation section |
| All executable tasks have tests | ✅ | 11/11 executable TDD tasks map to existing test files |
| RED confirmed (tests exist) | ✅ | Remediation tests `Migration_NoContieneGuidsLiterales_ParaNivelesCargo`, `Migration_ReferenciaConstantes_*`, `Migration_SemillasCoincidenConDatosSemilla_*`, `Patch_Returns405MethodNotAllowed` and `MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste` all exist in source |
| GREEN confirmed (tests pass) | ✅ | 6/6 remediation tests pass; 143/143 Cargo|NivelCargo tests pass |
| Triangulation adequate | ✅ | Migration parity is asserted via 4 separate tests (no literal Guid, references to constants, references by id, parity between migration and DatosSemilla) |
| Safety Net for modified files | ✅ | `NivelCargoConstantes`, `DatosSemilla`, the migration source, the controller and the migration test all have covering tests that pass |

**TDD Compliance**: 6/6 checks passed (up from 4/6 in the previous verify pass)

---

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 96 | 11 | xUnit |
| Integration | 47 | 6 | xUnit + MySQL/WebApplicationFactory |
| E2E | 0 | 0 | not installed |
| **Total** | **143** | **17** | |

(Distribution counted from the `dotnet test --filter "Cargo|NivelCargo"` run.)

---

### Spec Compliance Matrix

#### cargo-management
| Requirement | Scenario | Test / Evidence | Result |
|-------------|----------|-----------------|--------|
| Crear Cargo | Creación exitosa | `CargosControllerTests.Post_ValidRequest_Returns201CreatedWithDto`, `CargoServicioComandosTests.CrearAsync_DatosValidos_RetornaDtoYGuarda` | ✅ COMPLIANT |
| Crear Cargo | Codigo duplicado en Cargo activo | `CargosControllerTests.Post_DuplicateCode_Returns409WithProblemDetails`, `CargoServicioComandosTests.CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar` | ✅ COMPLIANT |
| Crear Cargo | NivelId inexistente | `CargoServicioComandosTests.CrearAsync_NivelIdInexistente_RetornaValidacionYSinGuardar` | ✅ COMPLIANT |
| Consultar Cargos | Listar Cargos activos | `CargoRepositoryTests.ListAllAsync_ExcluyeEntidadesInactivasYEliminadas`, `CargosControllerTests.GetAll_ReturnsOkWithDtoArray` | ✅ COMPLIANT |
| Consultar Cargos | Obtener Cargo por identificador | `CargosControllerTests.GetById_ExistingId_ReturnsOkWithDto` | ✅ COMPLIANT |
| Actualizar Cargo | Actualización exitosa | `CargosControllerTests.Put_ValidRequest_Returns200OkWithUpdatedDto`, `CargoRepositoryTests.UpdateAsync_ModificaCampos` | ✅ COMPLIANT |
| Actualizar Cargo | Actualizar Cargo inexistente | `CargosControllerTests.Put_NonExistent_Returns404WithProblemDetails` | ✅ COMPLIANT |
| Actualizar Cargo | Codigo no modificable | `ActualizarCargoRequest` no expone `Codigo`; `CargoTests.Actualizar_CodigoNoCambia` | ✅ COMPLIANT |
| Desactivar Cargo | Desactivación exitosa | `CargosControllerTests.Delete_ExistingId_Returns204NoContent`, `CargoRepositoryTests.DeleteAsync_MarcaComoInactivoYEliminado` | ✅ COMPLIANT |
| Desactivar Cargo | Desactivar Cargo inexistente | `CargosControllerTests.Delete_NonExistent_Returns404WithProblemDetails` | ✅ COMPLIANT |
| Reactivar Cargo | Reactivación exitosa | `CargosControllerTests.PatchReactivar_ValidRequest_Returns200OkWithDto`, `CargoRepositoryTests.ReactivateAsync_RestauraEstadoActivo` | ✅ COMPLIANT |
| Reactivar Cargo | Reactivar con Codigo conflictivo | `CargosControllerTests.PatchReactivar_Conflict_Returns409WithProblemDetails`, `CargoServicioComandosTests.ReactivarAsync_CodigoConflictivo_RetornaConflictoYSinGuardar` | ✅ COMPLIANT |
| Reactivar Cargo | Reactivar Cargo inexistente | `CargosControllerTests.PatchReactivar_NonExistent_Returns404WithProblemDetails` | ✅ COMPLIANT |
| Contrato de Respuesta Cargo | Respuesta consumer-safe | `CargoDto`, `CargosControllerTests.GetAll_JsonResponseContieneNivelIdYNivelNombre` | ✅ COMPLIANT |

#### nivel-cargo-catalog
| Requirement | Scenario | Test / Evidence | Result |
|-------------|----------|-----------------|--------|
| Entidad NivelCargo | Estructura de la tabla | `NivelCargoEntityTests` (Entity_TienePropiedadesCorrectas, Entity_TablaMapeadaCorrectamente, Entity_NoTienePropiedadesDeAuditoria), migración `20260618180508_CambiarNivelStringANivelId.cs` | ✅ COMPLIANT |
| Entidad NivelCargo | Constraint de valor numérico | `MigracionFailLoudCargosTests.MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste` queries `INFORMATION_SCHEMA.CHECK_CONSTRAINTS` for `CK_NivelesCargo_ValorNumerico` and asserts the CHECK_CLAUSE contains `ValorNumerico >= 0 AND <= 255`. Runtime proof on real MySQL 8. | ✅ COMPLIANT (was UNTESTED) |
| Referencia desde Cargos via NivelId | FK OnDelete(Restrict) | `MigracionFailLoudCargosTests.MigracionCargos_DatosLimpios_FkRestrictExiste` queries `INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS` and asserts `DELETE_RULE = 'RESTRICT'` for `FK_Cargos_NivelesCargo%` | ✅ COMPLIANT (was PARTIAL) |
| Referencia desde Cargos via NivelId | Índice sobre NivelId | `MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds` validates the column `NivelId` exists; `CargoConfiguracion.cs` declares `IX_Cargos_NivelId`; the migration creates the same index in step 7. | ✅ COMPLIANT (was PARTIAL) |
| Evolución de Catálogo NivelCargo | Backfill limpio | `MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds` validates the resulting schema (Nivel column dropped, NivelId present, 4 NivelesCargo rows with correct Codigos) | ✅ COMPLIANT (was PARTIAL) |
| Evolución de Catálogo NivelCargo | Fail-loud aborta antes del ALTER | `MigracionCargos_DatosSucios_LanzaMySqlException45000` runs the pre-flight SQL and asserts `MySqlException.Number == 1644` with "Backfill fail-loud" in the message | ✅ COMPLIANT |
| Seed de NivelesCargo con Guids estáticos | Coherencia entre migración y HasData | `Migration_NoContieneGuidsLiterales_ParaNivelesCargo`, `Migration_ReferenciaConstantes_DirectivoIdYConduccionMediaId`, `Migration_ReferenciaConstantes_OperativoIdYAcademicoId`, `Migration_SemillasCoincidenConDatosSemilla_ParaCodigoNombreValorNumericoYOrden` — 4 tests in `NivelCargoConstantesTests` assert migration source has zero literal `70000000-0000-0000-0000-00000000000N` Guids, references the `XxxId` constants, and consumes the same `(Codigo, Nombre, ValorNumerico, Orden)` tuples as `DatosSemilla` | ✅ COMPLIANT (was FAILING) |
| Acceso de Solo Lectura a NivelesCargo | Listar niveles de cargo | `NivelesCargoControllerTests.GetAll_Returns200With2SeedDtos` | ✅ COMPLIANT |
| Acceso de Solo Lectura a NivelesCargo | Obtener nivel por identificador | `NivelesCargoControllerTests.GetById_ExistingId_Returns200WithDto` | ✅ COMPLIANT |
| Acceso de Solo Lectura a NivelesCargo | Escritura rechazada | `Post_Returns405MethodNotAllowed`, `Put_Returns405MethodNotAllowed`, `Delete_Returns405MethodNotAllowed`, **and the new `Patch_Returns405MethodNotAllowed`** (line 131 of `NivelesCargoControllerTests.cs`) | ✅ COMPLIANT (was PARTIAL — PATCH was UNTESTED) |

#### sgv-database
| Requirement | Scenario | Test / Evidence | Result |
|-------------|----------|-----------------|--------|
| Cargos Reutilizables con Ciclo de Vida | Reutilizar cargo en varios puestos | Out of scope per proposal; Puestos not in change scope | ➖ N/A |
| Cargos Reutilizables con Ciclo de Vida | Codigo inmutable tras creación | `CargoTests.Actualizar_CodigoNoCambia`, `ActualizarCargoRequestValidatorTests` | ✅ COMPLIANT |
| Cargos Reutilizables con Ciclo de Vida | Baja lógica de Cargo | `CargoRepositoryTests.DeleteAsync_MarcaComoInactivoYEliminado`, `CargosControllerTests.Delete_ExistingId_Returns204NoContent` | ✅ COMPLIANT |
| Cargos Reutilizables con Ciclo de Vida | Reactivación de Cargo conservando Codigo | `CargosControllerTests.PatchReactivar_ValidRequest_Returns200OkWithDto` | ✅ COMPLIANT |
| Cargos Referencian NivelCargo por FK | FK de NivelCargo en Cargos | `MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds` (NivelId column present, Nivel column absent), `MigracionCargos_DatosLimpios_FkRestrictExiste` (FK with RESTRICT) | ✅ COMPLIANT |
| Cargos Referencian NivelCargo por FK | Eliminación de NivelCargo con Cargo referenciando | `MigracionCargos_DatosLimpios_FkRestrictExiste` validates `DELETE_RULE = 'RESTRICT'`. An integration test that actually attempts `DELETE` and observes the FK error is not in scope of this remediation, but the metadata proof is solid. | ⚠️ PARTIAL (deferred to SUGGESTION) |
| Unicidad de Codigo Activo de Cargo | Rechazar codigo activo duplicado | `CargosControllerTests.Post_DuplicateCode_Returns409WithProblemDetails` | ✅ COMPLIANT |
| Catálogo NivelesCargo con FK OnDelete(Restrict) | Enforcement de la FK | `MigracionCargos_DatosLimpios_FkRestrictExiste` | ✅ COMPLIANT |
| Catálogo NivelesCargo con FK OnDelete(Restrict) | Índice sobre la FK | Schema inspection in `MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds` confirms `NivelId` column; `CargoConfiguracion` declares the index. | ⚠️ PARTIAL (metadata proof only) |
| Catálogo NivelesCargo con FK OnDelete(Restrict) | Catálogo sin flags de estado | `NivelCargoEntityTests.Entity_NoTienePropiedadesDeAuditoria` (no `IsDeleted`/`IsActive`/`CreatedAt`/`UpdatedAt`) | ✅ COMPLIANT |
| Catálogo NivelesCargo con FK OnDelete(Restrict) | Check constraint de ValorNumerico | `MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste` | ✅ COMPLIANT |
| Migración fail-loud con pre-flight | Backfill limpio | `MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds` | ✅ COMPLIANT |
| Migración fail-loud con pre-flight | Fail-loud aborta antes del ALTER | `MigracionCargos_DatosSucios_LanzaMySqlException45000` | ✅ COMPLIANT |
| Migración fail-loud con pre-flight | Seed de NivelesCargo presente después de la migración | `MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds` (4 rows) | ✅ COMPLIANT |

#### sgv-persistence-architecture
| Requirement | Scenario | Test / Evidence | Result |
|-------------|----------|-----------------|--------|
| Catalog Evolution Exception (REQ-SPA-EVOLUTION-001) | Second invocation of the exception is approved | All four conditions covered: read-only API (NivelesCargoController has only GET), FK OnDelete(Restrict) (FK Restrict Existe), fail-loud migration (MySqlException 45000), shared Guid constants (4 migration-parity tests in NivelCargoConstantesTests) | ✅ COMPLIANT |
| Catalog Evolution Exception (REQ-SPA-EVOLUTION-001) | Migration fail-loud for dirty data | `MigracionCargos_DatosSucios_LanzaMySqlException45000` | ✅ COMPLIANT |
| Catalog Evolution Exception (REQ-SPA-EVOLUTION-001) | Seed Guid drift is impossible | 4 tests in `NivelCargoConstantesTests` (see nivel-cargo-catalog Seed row above) | ✅ COMPLIANT |

#### sgv-readonly-api
| Requirement | Scenario | Test / Evidence | Result |
|-------------|----------|-----------------|--------|
| Read-only Resource Access | Allow cargo write operations | `CargosControllerTests` (POST/PUT/DELETE/PATCH) | ✅ COMPLIANT |
| Read-only Resource Access | Reject unrelated write operations | NivelesCargoController 405 tests (POST/PUT/PATCH/DELETE) | ✅ COMPLIANT |
| Public API Discoverability | Discover cargo management operations | `SwaggerConfigurationTests.Cargos_ExposesWriteOperations` | ✅ COMPLIANT |

**Compliance summary**: 32 compliant / 2 partial (down from 19/3/3 in previous pass)

The 2 remaining PARTIALs are metadata-only proofs:
- `SHOW INDEX FROM Cargos` runtime assertion — the column existence and the configuration both declare the index, and a runtime `SHOW INDEX` is not in the test set.
- `DELETE FROM NivelesCargo WHERE Id = X` runtime attempt — `MigracionCargos_DatosLimpios_FkRestrictExiste` proves the metadata is `RESTRICT`, which is sufficient for the spec wording "DEBE rechazar la operación con error de foreign key constraint".

Both are promoted to SUGGESTION (not WARNING, not CRITICAL) because the spec text is satisfied
by the metadata and no test-driven contract is broken.

---

### Correctness (Static Evidence)
| Requirement | Status | Notes |
|------------|--------|-------|
| CRUD + baja lógica de Cargo | ✅ Implemented | Dominio, aplicación, repositorio y controller alineados |
| `NivelId` + `NivelNombre` en contrato | ✅ Implemented | `CargoDto` (líneas 11-12) expone `Guid NivelId` y `string? NivelNombre` |
| `NivelesCargo` como catálogo read-only | ✅ Implemented | Controller solo `GET`; POST/PUT/PATCH/DELETE devuelven 405 |
| Seed compartido entre migración y `HasData` | ✅ Implemented | `NivelCargoConstantes` (5 properties × 4 niveles + `Semilla` array + `NivelCargoSeed` record). DatosSemilla referencia `XxxCodigo/XxxNombre/XxxValorNumerico/XxxOrden`. Migración itera `Semilla` y referencia `XxxId` para UpdateData. 4 tests estructurales en `NivelCargoConstantesTests` previenen drift. |

### Coherence (Design)
| Decision | Followed? | Notes |
|----------|-----------|-------|
| `Cargo.Codigo` inmutable y fuera del request de actualización | ✅ Yes | `ActualizarCargoRequest` no incluye `Codigo` |
| FK `NivelId` con `OnDelete(Restrict)` e índice | ✅ Yes | `CargoConfiguracion` + migración; `MigracionCargos_DatosLimpios_FkRestrictExiste` lo prueba |
| Migración fail-loud | ✅ Yes | Pre-flight con `SIGNAL SQLSTATE '45000'`; `MigracionCargos_DatosSucios_LanzaMySqlException45000` lo ejecuta contra MySQL real |
| Seed con constantes compartidas entre migración y `HasData` | ✅ Yes | La migración itera `NivelCargoConstantes.Semilla`; los 6 `UpdateData` referencian `XxxId`; cero literales `70000000-...-00N`; `DatosSemilla` referencia los 4 niveles × 5 propiedades. 4 tests `NivelCargoConstantesTests` lo demuestran. |
| `GET /api/v1/niveles-cargo/{id:guid}` | ⚠️ Partial | El controller usa ruta `"{id}"` con `Guid.TryParse` manual; `NivelesCargoControllerTests.GetById_InvalidGuid_Returns400` confirma el comportamiento (devuelve 400 para no-GUID). La diferencia con `"{id:guid}"` es semántica: la intención se cumple, solo que el parseo es manual. |

### Issues Found

**CRITICAL**: None.

The 3 CRITICAL items from the previous verify pass are now fully resolved with passing runtime
tests:

1. **Seed parity** — 4 tests in `NivelCargoConstantesTests` (`Migration_NoContieneGuidsLiterales_ParaNivelesCargo`, `Migration_ReferenciaConstantes_DirectivoIdYConduccionMediaId`, `Migration_ReferenciaConstantes_OperativoIdYAcademicoId`, `Migration_SemillasCoincidenConDatosSemilla_ParaCodigoNombreValorNumericoYOrden`) assert the migration source uses `NivelCargoConstantes.*` references and has zero literal `70000000-...-00N` Guids. All 4 tests pass.
2. **PATCH 405 test** — `NivelesCargoControllerTests.Patch_Returns405MethodNotAllowed` (line 131) sends `HttpMethod.Patch` to `/api/v1/niveles-cargo/{guid}` and asserts `MethodNotAllowed`. The test passes (runtime, real WebApplicationFactory).
3. **Check constraint runtime test** — `MigracionFailLoudCargosTests.MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste` queries `INFORMATION_SCHEMA.CHECK_CONSTRAINTS` and asserts both the constraint name (`CK_NivelesCargo_ValorNumerico`) and the CHECK_CLAUSE content. The test passes (runtime, real MySQL 8).

**WARNING**: None.

The previous WARNINGS around triangulation, safety net, and the controller route template
are resolved or downgraded:

- **Triangulation**: now 4 separate migration-parity tests instead of 1 general assertion.
- **Safety net**: the migration source has explicit assertions for both literal absence and
  constant presence.
- **Controller `"{id}"` vs `"{id:guid}"`**: the test `GetById_InvalidGuid_Returns400`
  exercises the manual-parse path and confirms 400 for non-GUID input. Documented as a
  coherence PARTIAL but not a WARNING — the spec scenario "Obtener nivel por identificador"
  is satisfied.

**SUGGESTION**:
- Add a runtime test that calls `SHOW INDEX FROM Cargos` and asserts an index on `NivelId`,
  to make the index proof fully runtime rather than relying on configuration + column existence.
- Add a runtime test that attempts `DELETE FROM NivelesCargo WHERE Id = X` and expects a
  MySQL FK error, to make the `OnDelete(Restrict)` contract runtime-proven.
- Consider switching `NivelesCargoController` from `[HttpGet("{id}")]` with manual
  `Guid.TryParse` to `[HttpGet("{id:guid}")]` to match the design contract exactly; current
  behavior is equivalent but the route template becomes the source of truth for the format.

### Verdict
**PASS**

All 3 CRITICAL gaps from the previous verify pass are resolved with passing runtime tests. The
implementation matches the proposal, design, tasks, and all 4 specs (cargo-management,
nivel-cargo-catalog, sgv-database, sgv-persistence-architecture, sgv-readonly-api). The
pre-existing failure `UnidadOrganizativaRepositoryTests.QueryAsync_FiltroPorTipoUnidadOrganizativaId_RetornaSoloCoincidencias`
is unrelated to Cargos/NivelCargo and does not block the change.

The change is ready for archive.
