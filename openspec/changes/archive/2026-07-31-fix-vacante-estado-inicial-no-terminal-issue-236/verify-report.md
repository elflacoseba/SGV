# Verify Report: fix-vacante-estado-inicial-no-terminal-issue-236

## Resumen

La implementación cumple los criterios de aceptación de la issue #236: `POST /api/v1/vacantes` con un `EstadoVacanteId` terminal devuelve `400 Bad Request` con `ErrorCategoria.Validation`, código `VacanteErrorCodigo.EstadoTerminalInmutable` y `FieldErrors["estadoVacanteId"]`. La validación se aplica en `VacanteServicioComandos.CrearAsync` (capa Aplicación), en el lugar correcto y con el contrato exacto definido en la spec delta. La suite enfocada (38 tests) corre verde; las pruebas previas a este change no se rompieron. **CRITICAL=0, WARNING=0, SUGGESTION=1**.

## Criterios de aceptación validados

| Criterio | Estado | Evidencia |
|---|---|---|
| `POST /api/v1/vacantes` con `EstadoVacanteId` terminal devuelve 400 | ✅ | Tests unitarios `Crear_EstadoInicialTerminalCubierta_RetornaValidationFailure` y `Crear_EstadoInicialTerminalCancelada_RetornaValidationFailure` + test API `Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails` — todos verdes |
| Spec incluye escenario "Estado inicial terminal rechazado" | ✅ | `openspec/changes/fix-vacante-estado-inicial-no-terminal-issue-236/specs/vacante-management/spec.md:37-44` |
| Tests verdes en unit e integración | ✅ | 38/38 en `VacanteServicioComandosTests + VacantesControllerTests` (exit 0; duración 370 ms) |
| Sin regresión en tests previos | ✅ | 35 tests previos del scope verdes (38 totales − 3 nuevos) |

## Hallazgos

### CRITICAL
- (0)

### WARNING
- (0)

### SUGGESTION
- **Revisión opcional del cliente Web**: el diseño (`design.md §Riesgos residuales`) sugería confirmar `SGV.Web/Integration/VacantesApiClient*.cs` para el manejo del nuevo `400` con `EstadoTerminalInmutable`. Auditoría de código: `src/SGV.Web/Integration/Vacantes/VacanteApiClient.cs` delega en `CommandResultMapper.Map` (líneas 80–88) que mapea `400 → ErrorCategoria.Validation` independientemente del `Code`; el `Code` se preserva desde `problem.Title` ("EstadoTerminalInmutable"). El `FieldErrors["estadoVacanteId"]` se propaga correctamente vía `parsed.FieldErrors`. **Conclusión: el cliente Web maneja el nuevo 400 sin cambios**; la sugerencia queda documentada pero sin acción obligatoria.

## Validación contra design

| Decisión de diseño | Cumplida |
|---|---|
| Validación en servicio, NO en validador | ✅ — bloque insertado en `VacanteServicioComandos.CrearAsync` (líneas 132–144); `CrearVacanteRequestValidator` no se modificó |
| Código `EstadoTerminalInmutable` con `ErrorCategoria.Validation` | ✅ — `ErrorCategoria.Validation` + `VacanteErrorCodigo.EstadoTerminalInmutable` |
| `FieldErrors["estadoVacanteId"]` poblado | ✅ — `new Dictionary<string, string[]> { ["estadoVacanteId"] = [mensaje] }` |
| Mensaje único compartido entre `Error.Message` y `FieldErrors` | ✅ — `const string mensaje = "El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada)."` usado en ambos lugares |
| Comentario inline sobre 400 vs 409 | ✅ — líneas 132–134: `"Nota: el código es el mismo que CambiarEstadoAsync (409 Conflict); aquí es 400 porque la solicitud es inválida antes de persistir."` |
| Reutilización de `VacanteErrorCodigo.EstadoTerminalInmutable` (sin código nuevo) | ✅ — enum existente (`src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs:9`), sin nuevos códigos agregados |
| Posición del bloque: post-lookup, pre-`ExistsAbiertaByPuestoAsync` | ✅ — entre líneas 132 y 146, exactamente como prescribe `design.md §Aplicación` |

## Validación contra spec delta

| Escenario de la spec | Cubierto por test | Resultado |
|---|---|---|
| Creación exitosa | `Crear_DatosValidos_RetornaExitoYGuarda` (preexistente) | ✅ Verde |
| PuestoId inexistente | `Crear_PuestoIdVacio_RetornaValidationFailure` (preexistente) | ✅ Verde |
| EstadoVacanteId inválido | `Crear_EstadoVacanteIdVacio_RetornaValidationFailure` (preexistente) | ✅ Verde |
| Mutación sin permiso | `Create_WithoutCredentials_Returns401`, `Create_WithAuthenticatedNonMutator_Returns403` (preexistentes) | ✅ Verde |
| **Estado inicial terminal rechazado** | `Crear_EstadoInicialTerminalCubierta_RetornaValidationFailure` + `Crear_EstadoInicialTerminalCancelada_RetornaValidationFailure` (unit, nuevos) + `Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails` (API, nuevo) | ✅ Verde (3/3) |

## Validación TDD (Strict TDD Mode activo)

| Check | Resultado | Detalle |
|---|---|---|
| TDD Evidence reportado | ✅ | `apply-progress.md §Evidencia TDD` (tabla con 3 tareas) |
| Todas las tareas tienen tests | ✅ | 3/3 tareas de `tasks.md` con test file verificado |
| RED confirmado (tests existen) | ✅ | Apply-progress reporta `Failed: 2, Passed: 1, Total: 3` antes de producción; los 3 archivos de test existen |
| GREEN confirmado (tests pasan) | ✅ | 38/38 tests del scope pasan en `dotnet test` actual (exit 0) |
| Triangulación adecuada | ✅ | 2 casos unitarios independientes (Cubierta + Cancelada) para el escenario "Estado inicial terminal rechazado" |
| Safety Net para archivos modificados | ✅ | Suite enfocada previa de 35 tests preservada; nuevas aserciones no interfieren |

**TDD Compliance**: 6/6 checks aprobados.

## Distribución por capa de test

| Capa | Tests | Archivos | Tools |
|---|---|---|---|
| Unit | 2 | 1 (`VacanteServicioComandosTests.cs`) | xUnit + `FakeEstadoVacanteRepository`/`FakeUnitOfWork`/`FakeVacanteWriteRepository` |
| Integration | 1 | 1 (`VacantesControllerTests.cs`) | xUnit + `WebApplicationFactory` + `FakeVacanteServicioComandos` (controller wiring) |
| E2E | — | — | no aplica |
| **Total nuevo** | **3** | **2** | |

## Cobertura de archivos modificados

| Archivo | Tipo | Cobertura observada | Rating |
|---|---|---|---|
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modificado (líneas 132–144) | 100% del nuevo bloque cubierto por 2 tests unitarios con casos distintos (Cubierta + Cancelada) | ✅ Excellent |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Modificado (2 tests nuevos) | 100% | ✅ Excellent |
| `tests/SGV.Tests/Api/VacantesControllerTests.cs` | Modificado (1 test nuevo) | 100% del wiring | ✅ Excellent |

**Average cobertura estimada de los archivos modificados (nuevo código)**: 100%.

## Calidad de aserciones

| Archivo | Línea | Aserción | ¿Verifica comportamiento real? |
|---|---|---|---|
| `VacanteServicioComandosTests.cs` | 107 | `Assert.False(resultado.IsSuccess)` | ✅ |
| `VacanteServicioComandosTests.cs` | 108 | `Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria)` | ✅ |
| `VacanteServicioComandosTests.cs` | 109 | `Assert.Equal(VacanteErrorCodigo.EstadoTerminalInmutable, resultado.Error.Code)` | ✅ |
| `VacanteServicioComandosTests.cs` | 110 | `Assert.Contains("estadoVacanteId", resultado.FieldErrors!.Keys)` | ✅ |
| `VacanteServicioComandosTests.cs` | 111 | `Assert.Equal(0, uow.SaveChangesCount)` | ✅ (no persistencia) |
| `VacanteServicioComandosTests.cs` | 112 | `Assert.Empty(repo.Datos)` | ✅ (no persistencia) |
| `VacanteServicioComandosTests.cs` | 126-131 | Mismas 6 aserciones con `EstadoCanceladaId` | ✅ |
| `VacantesControllerTests.cs` | 260 | `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` | ✅ |
| `VacantesControllerTests.cs` | 261 | `ReadFromJsonAsync<ValidationProblemDetails>()` + `Assert.NotNull` | ✅ |
| `VacantesControllerTests.cs` | 263 | `Assert.Contains("estadoVacanteId", problem!.Errors.Keys)` | ✅ |

**Calidad de aserciones**: ✅ Todas verifican comportamiento real (sin tautologías, sin humo, sin loops sobre colecciones posiblemente vacías).

## Evidencia de no-regresión

| Test | Estado |
|---|---|
| `Crear_DatosValidos_RetornaExitoYGuarda` | ✅ verde |
| `Crear_PuestoIdVacio_RetornaValidationFailure` | ✅ verde |
| `Crear_EstadoVacanteIdVacio_RetornaValidationFailure` | ✅ verde |
| Resto de tests previos de `VacanteServicioComandosTests` (28 tests) | ✅ verde |
| `Create_ValidRequest_Returns201Created`, `Create_PuestoConVacanteAbierta_Returns409`, `Create_EstadoVacanteInexistente_Returns404`, `CambiarEstado_*`, `Get_*`, `Controller_HasAuthorizeAttribute` (13 tests) | ✅ verde |
| **Total tests previos del scope** (35) | ✅ verde |

## Suite completa

- 6 fallos preexistentes documentados en `apply-progress.md § Fallos preexistentes` (4 son `[MySqlFact]` que requieren MySQL local accesible; 1 es `SwaggerConfigurationTests` con asunción desactualizada; 1 es `PersonaRepositoryTests` con incompatibilidad previa).
- **No introducidos por este change** — confirmado en `apply-progress.md §Evidencia de work units` (fila `WU-3 verify | Suite completa`: baseline con stash muestra los mismos 6 fallos).
- No bloqueante para el archivado de este change (todos los archivos y tests del scope quedan verdes).

## Conclusión

**APROBADO**. El change cumple los criterios de aceptación de la issue #236 y la spec delta. Sin CRITICAL ni WARNING. La única sugerencia (cliente Web) queda documentada pero no requiere acción: `CommandResultMapper` ya maneja correctamente `400 → ErrorCategoria.Validation` independientemente del `Code`, por lo que `SGV.Web` recibe el nuevo error sin modificaciones.

Listo para `sdd-archive`.