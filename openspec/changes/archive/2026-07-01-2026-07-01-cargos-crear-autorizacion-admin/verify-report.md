# Verify Report: Autorización diferenciada en CargosController

## Verification Metadata
| Field | Value |
|-------|-------|
| Change | 2026-07-01-cargos-crear-autorizacion-admin |
| Mode | Strict TDD |
| Date | 2026-07-01 |
| Verdict | FAIL |

## Completeness
| Metric | Value |
|-------|-------|
| Tasks completed | 15 |
| Tasks total | 15 |
| Completion | 100% |

## Build & Test Evidence
- `dotnet build`: exit code `0` — `0` warnings, `0` errors.
- `dotnet test`: exit code `1` — `1086` total, `1074` passed, `12` failed, `0` skipped.
- Tests del change: `46/46` passing (`CargosControllerTests`, `CargoSkillControllerTests`, `UsuariosControllerTests`).
- Tests públicos read-only reejecutados para no-regresión fuera de Cargos: `2/2` passing (`SkillsControllerTests.GetAll_ReturnsOkWithDtoArray`, `TipoUnidadesOrganizativasControllerTests.GetAll_Returns200With7SeedDtos`).
- Fallos de suite completa observados: `12` fallos en `SGV.Tests.Persistencia.OcupacionRepositoryTests`, todos con el bug preexistente `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` ya documentado en `AGENTS.md` / issue `#59`.

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | `apply-progress.md` incluye tabla `TDD Cycle Evidence`. |
| All tasks have tests | ⚠️ | `13/15` tasks tienen evidencia directa de archivo de test; `4.1` y `4.2` son gates de build/suite, no tareas de archivo de test. |
| RED confirmed (tests exist) | ✅ | `5/5` checkpoints RED referencian archivos reales: `UsuariosControllerTests.cs`, `CargosControllerTests.cs`, `CargoSkillControllerTests.cs`. |
| GREEN confirmed (tests pass) | ⚠️ | `6/7` checkpoints GREEN se reconfirmaron limpio; `4.2` sigue terminando con suite global en rojo por el bug preexistente `#59`. |
| Triangulation adequate | ⚠️ | El artefacto no trae columna explícita de triangulación; por inspección, los escenarios requeridos sí quedaron cubiertos por múltiples casos 401/403/2xx. |
| Safety Net for modified files | ⚠️ | El artefacto no documenta columna explícita de safety net previa sobre archivos modificados. |

**TDD Compliance**: 2 checks totalmente verdes, 4 checks parcialmente evidenciados / con warning.

---

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 0 | 0 | xUnit |
| Integration | 46 | 3 | xUnit + `WebApplicationFactory` + fake auth pipeline |
| E2E | 0 | 0 | — |
| **Total** | **46** | **3** | |

---

### Changed File Coverage
| File | Line % | Branch % | Uncovered Lines | Rating |
|------|--------|----------|-----------------|--------|
| `src/SGV.Api/Controllers/CargosController.cs` | 94.44% | 75.00% | `L268-L269`, `L307` | ⚠️ Acceptable |

**Average changed file coverage**: 94.44%

---

### Assertion Quality
**Assertion quality**: ✅ All assertions verify real behavior

---

### Quality Metrics
**Linter**: ➖ Not available  
**Type Checker**: ✅ `dotnet build SGV.slnx` sin errores

## Spec Compliance Matrix
| Spec | Requirement | Scenario | Covered by test | Status |
|------|-------------|----------|-----------------|--------|
| `cargo-management` | Autorización de endpoints de cargos | Lectura autenticada exitosa | `CargosControllerTests.GetAll_ReturnsOkWithDtoArray`, `CargosControllerTests.GetById_ExistingId_ReturnsOkWithDto` | PASS |
| `cargo-management` | Autorización de endpoints de cargos | Acceso anónimo rechazado | `CargosControllerTests.GetAll_WithoutCredentials_ReturnsUnauthorized`, `CargosControllerTests.GetById_WithoutCredentials_ReturnsUnauthorized` | PASS |
| `cargo-management` | Autorización de endpoints de cargos | Mutación protegida por rol administrador | `Post_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Put_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Delete_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden`, más `Post_ValidRequest_Returns201CreatedWithDto`, `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto`, `Delete_ExistingId_Returns204NoContent`, `PatchReactivar_ValidRequest_Returns200OkWithDto` | PASS |
| `cargo-skill-query-contract` | Autorización del subrecurso de skills de cargos | Consulta autenticada exitosa | `CargoSkillControllerTests.GetSkills_ReturnsOkWithDtoArray` | PASS |
| `cargo-skill-query-contract` | Autorización del subrecurso de skills de cargos | Acceso anónimo rechazado | `CargoSkillControllerTests.GetSkills_WithoutCredentials_ReturnsUnauthorized` | PASS |
| `cargo-skill-query-contract` | Autorización del subrecurso de skills de cargos | Mutación protegida por rol administrador | `PutSkill_WithAuthenticatedNonAdmin_ReturnsForbidden`, `DeleteSkill_WithAuthenticatedNonAdmin_ReturnsForbidden`, más `PutSkill_ValidRequest_Returns200OkWithDto`, `DeleteSkill_ExistingAssignment_Returns204NoContent` | PASS |
| `sgv-readonly-api` | No Authentication Requirement | Lectura pública anónima permitida | `SkillsControllerTests.GetAll_ReturnsOkWithDtoArray`, `TipoUnidadesOrganizativasControllerTests.GetAll_Returns200With7SeedDtos` | PASS |
| `sgv-readonly-api` | No Authentication Requirement | Lectura anónima de Cargos rechazada | `CargosControllerTests.GetAll_WithoutCredentials_ReturnsUnauthorized`, `CargosControllerTests.GetById_WithoutCredentials_ReturnsUnauthorized`, `CargoSkillControllerTests.GetSkills_WithoutCredentials_ReturnsUnauthorized` | PASS |
| `sgv-readonly-api` | No Authentication Requirement | Lectura autenticada de Cargos exitosa | `CargosControllerTests.GetAll_ReturnsOkWithDtoArray`, `CargosControllerTests.GetById_ExistingId_ReturnsOkWithDto`, `CargoSkillControllerTests.GetSkills_ReturnsOkWithDtoArray` | PASS |
| `sgv-readonly-api` | No Authentication Requirement | Mutación de Cargos protegida por rol administrador | Cobertura combinada de mutaciones en `CargosControllerTests` y `CargoSkillControllerTests` con matriz `403` no-admin + `2xx` admin | PASS |

## Correctness
- `src/SGV.Api/Controllers/CargosController.cs` implementa exactamente **1** `[Authorize]` a nivel controller (`line 16`).
- Las **6** mutaciones definidas por el design tienen `[Authorize(Roles = RolesSgv.Administrador)]`: `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill` (`lines 80, 113, 144, 170, 218, 246`).
- Los GET (`GetAll`, `GetById`, `GetSkills`) quedaron autenticados por herencia del atributo de controller, sin overrides adicionales.
- La metadata HTTP pedida por el design está presente: `401` en lecturas y `401/403` en mutaciones, preservando además `404/409/400` previos donde correspondía.
- `ApiWebApplicationFactory` extiende el harness mínimo: `UserHeader`, `AdminHeader`, tokens diferenciados y `BuildPrincipal(string token)` para distinguir admin vs usuario autenticado sin rol.
- `CreateAuthenticatedClient()` centraliza el cliente admin por defecto para los tests de regresión funcional.

## Design Coherence
- Se respetó el enfoque declarativo del `design.md`: atributos ASP.NET Core, sin policies custom y sin tocar `Program.cs`.
- `git diff --name-only` confirma que `src/SGV.Api/Program.cs` **no** forma parte del change verificado.
- No se introdujeron literales de rol `"Admin"` ni `"Administrador"` en los archivos modificados; la autorización usa `RolesSgv.Administrador`.
- Se preservó el aislamiento del resto de la API pública: los GET read-only no-Cargo siguen respondiendo anónimamente, validado con tests reejecutados sobre `/api/v1/skills` y `/api/v1/tipos-unidad-organizativa`.

## Issues

### CRITICAL
- `dotnet test SGV.slnx` sigue saliendo con código `1` por `12` fallos en `SGV.Tests.Persistencia.OcupacionRepositoryTests` (bug preexistente `#59`). Aunque NO son regresión de este change, la regla actual de verify exige suite completa verde antes de archivar; por lo tanto el change **no está listo para archive**.

### WARNING
- El `apply-progress.md` no documenta columnas explícitas de **triangulación** ni **safety net**, así que la trazabilidad Strict TDD quedó incompleta aunque la cobertura funcional real sí exista.
- La desviación declarada en `apply-progress.md` para la task `3.3` confirma que parte de la cobertura `401` se escribió después de tener `[Authorize]` controller-level ya aplicado; eso reduce la pureza del ciclo RED→GREEN para ese punto puntual.

### SUGGESTION
- Cuando se reabra el verify tras corregir el bug `#59`, conviene conservar en `apply-progress.md` una tabla Strict TDD más rica (triangulación y safety net) para que la auditoría sea completamente mecánica y no dependa de inspección manual.

## Final Verdict
FAIL

## Recommendation
Requiere fixes antes de `archive`: el change está correcto contra specs/design/tasks, pero la suite completa aún no pasa y la política actual de verify bloquea archivar mientras persistan los 12 fallos de `OcupacionRepositoryTests`.
