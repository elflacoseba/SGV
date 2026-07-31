# Tasks: fix-vacante-estado-inicial-no-terminal-issue-236

## Resumen
Rechazar estado inicial terminal en `VacanteServicioComandos.CrearAsync`. Tres tests nuevos (2 unit + 1 API). Orden RED → GREEN → verify por `strict_tdd: true`. Single PR (~80-100 líneas).

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 80-100 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | single PR |
| Delivery strategy | single-pr |
| Chain strategy | size:exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| WU-1 | Tests rojos | single PR (1/3) | `dotnet test SGV.slnx --filter "FullyQualifiedName~EstadoInicialTerminal"` debe fallar | N/A (cambio en test project) | Eliminar 3 tests agregados sin tocar prod |
| WU-2 | Implementación verde | single PR (2/3) | mismo filter debe pasar | N/A (cambio en librería, no requiere runtime) | Revertir bloque `if (estadoVacante.EsTerminal)` en `VacanteServicioComandos.cs` |
| WU-3 | Verificación suite | single PR (3/3) | `dotnet test SGV.slnx` verde | N/A (`[MySqlFact]` se skipean sin MySQL) | N/A (no produce código) |

## Fase 1: Tests rojos (RED) — WU-1

- [x] 1.1 Agregar `Crear_EstadoInicialTerminalCubierta_RetornaValidationFailure` en `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` con `CrearRequestValido(estadoVacanteId: EstadoCubiertaId)`. Assert `IsSuccess==false`, `Error.Categoria==Validation`, `Error.Code==EstadoTerminalInmutable`, `FieldErrors["estadoVacanteId"]`, `uow.SaveChangesCount==0`.
- [x] 1.2 Agregar `Crear_EstadoInicialTerminalCancelada_RetornaValidationFailure` análogo con `EstadoCanceladaId`.
- [x] 1.3 Agregar `Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails` en `tests/SGV.Tests/Api/VacantesControllerTests.cs` copiando patrón de `Create_ValidacionFalla_Returns400WithProblemDetails` (líneas 199-229). Fake `CrearHandler` retorna `Failure(Validation, EstadoTerminalInmutable, ...)` con `FieldErrors["estadoVacanteId"]`. Assert `BadRequest` y `ValidationProblemDetails.Errors["estadoVacanteId"]`.
- [x] 1.4 Validar RED: los 2 tests unitarios que dependen del código nuevo fallan; el test API de wiring pasa por diseño (autorizado por el usuario para continuar).

## Fase 2: Implementación (GREEN) — WU-2

- [x] 2.1 Insertar bloque en `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` entre lookup de `estadoVacante` (~130) y `ExistsAbiertaByPuestoAsync` (~132): `if (estadoVacante.EsTerminal) return VacanteCommandResult.Failure(new VacanteError(Validation, VacanteErrorCodigo.EstadoTerminalInmutable, mensaje), new Dictionary<string,string[]>{["estadoVacanteId"]=[mensaje]});` con `mensaje="El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada)."`.
- [x] 2.2 Validar GREEN: filter `VacanteServicioComandosTests|VacantesControllerTests` pasa en verde.

## Fase 3: Verificación final — WU-3

- [x] 3.1 `dotnet build SGV.slnx` → verde.
- [x] 3.2 `dotnet test SGV.slnx --no-restore --filter "FullyQualifiedName~VacanteServicioComandosTests|FullyQualifiedName~VacantesControllerTests"` → 38/38 verde. Suite completa con 6 fallos preexistentes confirmados fuera del scope de Vacantes (SwaggerConfigurationTests asume solo GETs antes del PR #231; el resto son `[MySqlFact]` que requieren MySQL local accesible). No bloqueante para este change.
- [x] 3.3 `git diff` confirma scope: solo `VacanteServicioComandos.cs`, `VacanteServicioComandosTests.cs`, `VacantesControllerTests.cs`. Commit `2bfa58c` creado sobre `develop`.

## Commits sugeridos

1. `test(vacantes): cover estado inicial terminal rejection (red)` — WU-1.
2. `feat(vacantes): reject terminal estado inicial on create (green)` — WU-2.
3. `chore(vacantes): validate suite after fix #236` — WU-3 (solo si hay ajustes).

## Decisiones

- Orden RED → GREEN → verify obligatorio por `strict_tdd: true`.
- Reutilizar helpers (`CrearRequestValido`, `EstadoCubiertaId`, `EstadoCanceladaId`, `FakeEstadoVacanteRepository`); sin helpers nuevos.
- Patrón API copiado de `Create_ValidacionFalla_Returns400WithProblemDetails` (líneas 199-229).
- Mensaje único compartido entre `Error.Message` y `FieldErrors["estadoVacanteId"]`.
- No tocar validador ni controller. Validación vive solo en servicio.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| `EstadoCubiertaId`/`EstadoCanceladaId` no exportados | Si no visibles en apply, usar `Guid.Parse("20000000-0000-0000-0000-000000000003/004")` inline |
| Cliente Web asume solo `409` para `EstadoTerminalInmutable` | Revisar `SGV.Web/Integration/VacantesApiClient*.cs` durante apply |
| Contrato implícito: antes `201`, ahora `400` con estado terminal | Documentado en spec/proposal; clientes que ya asumen no-terminales funcionan idéntico |
