# Verify Report — PR 1 Backend (cargos)

## 1. Resumen de verificación

- **Branch verificada**: `feat/cargos-crear-editar-codigo-editable-pr1`
- **Feature code**: ~375 líneas reales de PR (`develop...HEAD`: 337 inserciones, 38 borrados, 10 archivos). `main..HEAD` no representa este slice porque la rama está stackeada sobre `develop`.
- **Build**: pass (`dotnet build SGV.slnx`)
- **Tests**: fail en suite completa (`dotnet test SGV.slnx` → 1023 passed, 12 failed, 0 skipped, 1035 total). Suite focalizada del slice en verde: `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos"` → 221/221 pass; `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativa"` → 219/219 pass.
- **Veredicto**: ❌ NOT COMPLIANT

**Motivos bloqueantes**:
1. La suite completa falla con 12 tests rojos en `SGV.Tests.Persistencia.OcupacionRepositoryTests` por el bug conocido de MySQL `ActivePuestoIdUnique` (`Incorrect integer value` / `Data truncated`).
2. `apply-progress.md` no incluye la tabla **TDD Cycle Evidence** exigida por Strict TDD; solo registra checkboxes por tarea.

## 2. Cobertura por escenario de spec

### Requirement modificada: Actualizar Cargo

| Requisito | Escenario | Escenarios cubiertos por tests | Estado | Notas |
|---|---|---|---|---|
| Actualizar Cargo | Actualización exitosa con Codigo único | `CargoTests.Actualizar_CambiaCodigoSiNoDuplica`, `CargoServicioComandosTests.ActualizarAsync_MismoCodigoEnOtroCargoEliminado_PermiteOperacion` | ✅ PASS | La mutabilidad del código queda cubierta en dominio y la aceptación con unicidad activa queda cubierta en aplicación. |
| Actualizar Cargo | Actualización exitosa sin cambiar Codigo | `CargoTests.Actualizar_ModificaCamposEditables`, `CargoServicioComandosTests.ActualizarAsync_CodigoSinCambio_NoFallaValidacionUnicidad`, `CargosControllerTests.Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto` | ✅ PASS | Se valida que editar otros campos no dispara falso positivo de unicidad. |
| Actualizar Cargo | Codigo requerido en update | `ActualizarCargoRequestValidatorTests.Should_Have_Error_When_Codigo_Is_Empty`, `CargoServicioComandosTests.ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`, `CargosControllerTests.Put_EmptyCodigo_Returns400WithFieldErrors` | ✅ PASS | Hay cobertura de shape validation, short-circuit del servicio y contrato HTTP 400. |
| Actualizar Cargo | Codigo duplicado contra otro Cargo activo | `CargoServicioComandosTests.ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`, `CargosControllerTests.Put_DuplicateActiveCodigo_Returns409WithProblemDetails` | ✅ PASS | El código de error expuesto es `CodigoDuplicado`. |
| Actualizar Cargo | Codigo repetido de un Cargo eliminado | `CargoServicioComandosTests.ActualizarAsync_MismoCodigoEnOtroCargoEliminado_PermiteOperacion` | ✅ PASS | Verifica que la unicidad se aplique solo contra activos. |
| Actualizar Cargo | Actualizar Cargo inexistente | `CargoServicioComandosTests.ActualizarAsync_CargoInexistente_RetornaNoEncontradoYSinGuardar`, `CargosControllerTests.Put_NonExistent_Returns404WithProblemDetails` | ✅ PASS | Cobertura en servicio y API. |

### Requirement agregada: Unicidad activa de Codigo en update

| Requisito | Escenario | Escenarios cubiertos por tests | Estado | Notas |
|---|---|---|---|---|
| Unicidad activa de Codigo en update | Update comparte la misma regla que create | `CargoServicioComandosTests.CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar`, `CargoServicioComandosTests.ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`, `CargosControllerTests.Put_DuplicateActiveCodigo_Returns409WithProblemDetails` | ✅ PASS | Create y update convergen en el mismo código de conflicto. |
| Unicidad activa de Codigo en update | Update ignora registros inactivos para unicidad activa | `CargoServicioComandosTests.ActualizarAsync_MismoCodigoEnOtroCargoEliminado_PermiteOperacion` | ✅ PASS | Consistente con soft delete + índice computado. |

**Cobertura de escenarios del delta spec**: **8/8** con tests identificados y suite focalizada en verde.

## 3. Cobertura por tarea

| Tarea | Estado | Evidencia |
|---|---|---|
| PR-1.1 RED: tests de dominio para cambio de `Codigo` | ✅ DONE | `tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs`; tests `Actualizar_CambiaCodigoSiNoDuplica`, `Actualizar_ConCodigoVacio_ThrowsArgumentException`, `Actualizar_ConCodigoMayorA50_ThrowsArgumentException`. |
| PR-1.2 GREEN: `Cargo.Actualizar` acepta `codigo` | ✅ DONE | `src/SGV.Dominio/Organizacion/Cargo.cs`; firma `Actualizar(string codigo, ...)`, `Codigo` sigue con `private set`. |
| PR-1.3 RED: tests del validator para `Codigo` en update | ✅ DONE | `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarCargoRequestValidatorTests.cs`; casos empty/null/whitespace/max length/valid. |
| PR-1.4 GREEN: extender `ActualizarCargoRequest` + validator | ✅ DONE | `src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs`, `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs`. |
| PR-1.5 RED: tests de servicio para unicidad activa en update | ✅ DONE | `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs`; cubre duplicado activo, eliminado, código inválido, race condition y mismo código. |
| PR-1.6 GREEN: `CargoServicioComandos.ActualizarAsync` con unicidad activa + helper compartido | ✅ DONE | `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`; helper `EnsureCodigoNoDuplicadoAsync`, catch de `DbUpdateException`, propagación de `request.Codigo`. |
| PR-1.7 RED: tests de API para `PUT` con `codigo` (400/409) | ✅ DONE | `tests/SGV.Tests/Api/CargosControllerTests.cs`; `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto`, `Put_EmptyCodigo_Returns400WithFieldErrors`, `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`. |
| PR-1.8 GREEN: actualizar XML doc y contrato de `PUT` | ✅ DONE | `src/SGV.Api/Controllers/CargosController.cs`; XML docs de `PUT` actualizadas para `Codigo` editable y obligatorio. |
| PR-1.9 Verificar índice único cubre update | ✅ DONE | `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs:1001-1005` (`IX_Cargos_ActiveCodigoUnique`), `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs:104-117` (`ExistsActiveCodeAsync(..., excludingId)`). |
| PR-1.10 VERIFY: build + suite Cargo | ⚠️ PARTIAL | `dotnet build SGV.slnx` verde; `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos"` verde (221/221); `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativa"` verde (219/219). Pero `dotnet test SGV.slnx` completo falla (12 rojos ajenos al slice). |
| PR-1.11 Regenerar `docs/migracion-inicial-sgv.sql` (condicional) | ✅ DONE | No hay migración nueva; PR-1.9 confirma que no aplica regeneración. |

## 4. Verificación de reglas OpenSpec / Clean Architecture

- [x] Dominio no importa Infraestructura. Evidencia: grep sin matches de `using SGV.Infraestructura` o `using Microsoft.AspNetCore` en `src/SGV.Dominio/**`.
- [x] Aplicación no conoce HTTP. Evidencia: grep sin matches de `HttpContext`, `HttpRequest`, `IActionResult` en `src/SGV.Aplicacion/**`.
- [x] FluentValidation solo valida shape, NO consulta DB. Evidencia: `ActualizarCargoRequestValidator` solo define `NotEmpty`, `MaximumLength`, `NotEqual(Guid.Empty)`.
- [x] El handler traduce la violación del índice único a 409 con código claro. Evidencia: `CargoServicioComandos` devuelve `CargoErrorType.Conflict` + `CodigoDuplicado`; `CargosController.ToProblemResult(...)` lo serializa como `409` con `title = CodigoDuplicado`.
- [x] El índice único `IX_Cargos_ActiveCodigoUnique` sigue siendo el árbitro final. Evidencia: migración inicial `20260614183103_InicialSgvo.cs:1001-1005`; la columna computada `ActiveCodigoUnique` sigue vigente.
- [x] Tests críticos presentes: cambio válido de Codigo, duplicado activo rechazado, Codigo vacío rechazado, sin cambio de Codigo sigue funcionando.
- [ ] No se introdujo código que rompa `dotnet build` ni `dotnet test`. `dotnet build` pasa, pero `dotnet test SGV.slnx` completo sigue rojo con 12 fallos.

## 5. TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD Evidence reported | ❌ | `apply-progress.md` NO contiene tabla `TDD Cycle Evidence`; solo hay estado por tarea. |
| All tasks have tests | ⚠️ | 8/11 tareas de implementación tienen evidencia de tests; 3/11 son verificación/soporte. |
| RED confirmed (tests exist) | ✅ | Existen los archivos de tests reportados para Dominio, Aplicación y API. |
| GREEN confirmed (tests pass) | ✅ | La suite focalizada del slice (`Cargo|Cargos`) pasa 221/221. |
| Triangulation adequate | ✅ | Hay variación de escenarios: éxito, vacío, duplicado activo, eliminado, race condition, inexistente. |
| Safety Net for modified files | ⚠️ | `apply-progress.md` no documenta explícitamente safety net por archivo modificado. |

**TDD Compliance**: **3/6 checks** plenamente satisfechos.

## 6. Test Layer Distribution

| Layer | Tests (métodos) | Files | Tools |
|---|---:|---:|---|
| Unit | 53 | 3 | xUnit |
| Integration | 33 | 2 | xUnit + ASP.NET Core TestServer / MySQL (`[MySqlFact]`) |
| E2E | 0 | 0 | not installed |
| **Total** | **86** | **5** | |

**Archivos clasificados**:
- Unit: `CargoTests.cs`, `ActualizarCargoRequestValidatorTests.cs`, `CargoServicioComandosTests.cs`
- Integration: `CargosControllerTests.cs`, `CargoRepositoryTests.cs`

## 7. Changed File Coverage

Cobertura específica por archivo cambiado: **omitida**. No hay herramienta de coverage dedicada ejecutada en esta fase.

## 8. Assertion Quality

**Assertion quality**: ✅ All assertions verify real behavior.

Revisión manual de los 5 archivos de test modificados:
- no hay tautologías,
- no hay loops fantasma,
- no hay asserts sin ejecutar código de producción,
- los `Assert.NotNull(...)` encontrados están acompañados por aserciones de valor o flujo relevantes.

## 9. Quality Metrics

- **Linter**: ➖ No disponible como comando separado en esta fase.
- **Type Checker**: ✅ No errors (`dotnet build SGV.slnx`).

## 10. Hallazgos (CRITICAL / WARNING / SUGGESTION)

### CRITICAL (bloquea el PR)

1. **La suite completa no está verde**. `dotnet test SGV.slnx` falla con 12 tests en `SGV.Tests.Persistencia.OcupacionRepositoryTests` por el bug conocido de MySQL `ActivePuestoIdUnique` (`Incorrect integer value` / `Data truncated`). Aunque el fallo no parece introducido por este slice, la regla de verificación pedida exige ejecutar y aprobar la suite completa.
2. **Falta evidencia formal de Strict TDD en `apply-progress.md`**. No existe la tabla `TDD Cycle Evidence` requerida por el skill `sdd-verify` cuando `strict_tdd` está activo.

### WARNING (no bloquea pero hay que arreglar)

1. **`main..HEAD` no sirve para medir este PR** en este branch stackeado; devuelve un diff enorme del repo. Para el tamaño real del slice hay que usar `develop...HEAD` (375 líneas). Conviene alinear base real y comando documentado.
2. **El test HTTP feliz de update no afirma el `Codigo` devuelto**. `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto` confirma `200 OK` y `Nombre`, pero no comprueba el valor actualizado de `Codigo`.

### SUGGESTION (mejora opcional)

1. Agregar un test de integración/persistencia que actualice `Codigo` en MySQL y verifique explícitamente que el cambio persiste sin romper la recomputación de `ActiveCodigoUnique`.
2. Mantener el patrón de `CargoServicioComandos` alineado con `OcupacionServicioComandos` documentando en el PR body que el detector de constraint violations ahora es parte del contrato de la capa Aplicación.

## 11. Veredicto final y próximos pasos

- **¿PR1 listo para abrir como PR?**: **No**, mientras la verificación siga con CRITICAL abiertos.
- **¿Hay que mergear algo antes?**: **NO**. PR1 es el primero de la cadena.
- **¿Bloqueos para PR2A?**:
  - Resolver o acordar waiver explícito para la suite completa roja (`dotnet test SGV.slnx`).
  - Completar la evidencia Strict TDD en `apply-progress.md` con la tabla requerida.

### Resumen ejecutivo

- El slice backend de cargos está **funcionalmente alineado** con el delta spec: `Codigo` ahora es editable, la unicidad activa se aplica en update y los escenarios del spec quedan cubiertos **8/8**.
- La arquitectura se mantiene limpia: Dominio sin Infraestructura, Aplicación sin HTTP, validator solo de shape e índice MySQL como árbitro final.
- El tamaño real del PR1 está **dentro del budget** de review: ~375 líneas contra `develop...HEAD`.
- La verificación **NO pasa** por dos razones de proceso/gate: suite completa roja (12 fallos de `OcupacionRepositoryTests`) y falta de tabla `TDD Cycle Evidence` en `apply-progress.md`.
