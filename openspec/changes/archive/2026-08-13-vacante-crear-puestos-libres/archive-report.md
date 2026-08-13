# Archive Report: vacante-crear-puestos-libres

**Change**: `vacante-crear-puestos-libres`
**Fecha de archivo**: 2026-08-13
**Modo de artifact store**: hybrid (Engram + OpenSpec)
**Branch de merge**: develop

---

## Resumen

Dropdown de `Vacantes/Create` consume `GET /api/v1/puestos/disponibles` (nuevo endpoint que aplica 2 NOT EXISTS — Ocupación vigente AND Vacante Abierta). El usuario ya no ve puestos que serían rechazados con 409 al POST. La validación backend (N1 + `ActivePuestoIdUnique`) queda intacta como defense-in-depth.

## Acceptance Criteria (8/8 ✅)

| # | AC | Test que lo cubre | PASS |
|---|---|---|---|
| AC-1 | `GET /api/v1/puestos/disponibles` devuelve solo puestos activos sin Ocupación vigente NI Vacante Abierta | `[MySqlFact] ListarDisponibles_MySql_ConOcupacionVigente_Excluye` + `…_ConVacanteAbierta_Excluye` + `…_CasoCombinadoOcupacionYVacante_ExcluidoPorOcupacion` + `GetDisponibles_ReturnsOkWithDtoArray` (`PuestosControllerTests.cs:119`) | ✅ |
| AC-2 | Dropdown de `Vacantes/Create` consume el nuevo endpoint y NO incluye puestos con Ocupación vigente | `VacantesCreateEditForbidTests.Get_Create_WhenMutationRole_RendersFormWithCatalogs` (1× a `ListarPuestosDisponiblesAsync`, 0× a `ListarPuestosAsync`) + nuevo `Get_Create_DropdownSoloIncluyeDisponibles` (`VacantesCreateEditForbidTests.cs:79`) | ✅ |
| AC-3 | Tests `[MySqlFact]` cubren los 4 escenarios (con/sin Ocupación × con/sin Vacante Abierta) | 7 métodos `[MySqlFact]` en `PuestoRepositoryListarDisponiblesTests.cs` (4 cuadrantes explícitos + 3 complementarios: soft-deleted, finalizado, orden) | ✅ |
| AC-4 | Validación backend existente (N1 + constraint unique `ActivePuestoIdUnique`) NO se modifica | Targeted test filter `PuestoOcupado\|PuestoConVacanteAbierta` → **12/12 passed**; `VacanteConfiguracion.cs:40-45` (`ActivePuestoIdUnique` computed + unique index `IX_Vacantes_ActivePuestoIdUnique`) intacto | ✅ |
| AC-5 | `GET /api/v1/puestos` mantiene su comportamiento actual (todos los activos) | `PuestosControllerTests.GetAll_NoModificaShape_GetDisponiblesTambien` (verifica seed en `GetAll`, `[]` en `GetDisponibles` — divergencia intencional protege contra swap accidental) | ✅ |
| AC-6 | `dotnet build SGV.slnx` compila sin errores | Build verde — 0 errors, 4 warnings preexistentes (NU1510 sobre `Microsoft.Extensions.Configuration.Json` y `EnvironmentVariables` en `SGV.Infraestructura.csproj`) | ✅ |
| AC-7 | Suite `dotnet test SGV.slnx` pasa sin regresión | **3520/3520 passed**, 0 failed, 0 skipped; duración 2m 14s | ✅ |
| AC-8 | `ListarPuestosAsync` en `IVacanteApiClient` permanece funcional | `VacanteApiClientListarPuestosTests` preexisting intacto (6/6 verde); 5 tests adaptados en `VacantesCreateEditForbidTests` con `Assert.Empty(apiClient.ListarPuestosCalls)` confirman 0 invocaciones del método legacy | ✅ |

## Spec scenarios (14/14 ✅)

Resumen: 8 escenarios de REQ-PTO-DISP-001 (agregado en `puesto-management/spec.md`) + 6 del requisito Create modificado en `vacante-web/spec.md`, todos cubiertos por tests verdes. Defense-in-depth verificado por filtro de regresión dirigido.

## Métricas

- **Tests agregados**: 18 nuevos tests (3 unit servicio + 7 `[MySqlFact]` repo + 3 API controller + 1 web client + 4 web page = 18; 5 deltas menores).
- **Tests pasando**: 3520/3520.
- **Líneas modificadas**: 707 netas (src + tests) — `size:exception` autorizado por usuario.
- **Work units**: 5 (backend foundation → persistencia → API → web integration → verification).
- **Commits**: 5 (4 features/tests + 1 archive commit).

## Defense-in-depth

- N1 (`PuestoOcupado`) intacto: `VacanteServicioComandos.CrearAsync` sigue rechazando con 409 si Ocupación vigente.
- `ActivePuestoIdUnique` constraint intacto en `VacanteConfiguracion.cs`.
- Targeted regression filter `PuestoOcupado|PuestoConVacanteAbierta`: 12/12 verde.

## Limitaciones conocidas

- `ListarPuestosAsync` en `IVacanteApiClient` queda con 0 callers funcionales (contrato muerto potencial) — flag de follow-up.
- Spec files accumulated (`openspec/specs/puesto-management/spec.md` y `vacante-web/spec.md`) commiteadas en este archive commit.

## Próximos pasos (post-archive)

- [ ] Crear PR hacia `develop` con los 5 commits.
- [ ] PR review con `pr-review-dotnet`.
- [ ] Cleanup: considerar remoción de `ListarPuestosAsync` en change futuro si no se reusa.
