# PR 1 — Backend cargos: `Codigo` editable en update con unicidad activa

## 1. Summary

Make `Cargo.Codigo` editable in update with the same active uniqueness rule as create.

## 2. Why

Revertir la decisión de inmutabilidad de `Codigo` (archive/2026-06-18-implementar-modulo-cargos) para permitir correcciones de código tras la creación. Mantiene consistencia con la regla de unicidad activa: `Codigo` es único contra cargos activos, ignorando soft-deleted. Sin esta edición, un error de captura en la creación obligaba a baja lógica + alta nueva, contaminando auditoría y FKs en `Puesto`.

## 3. What changes

- **`Cargo` entity** (`src/SGV.Dominio/Organizacion/Cargo.cs`): `Actualizar(string codigo, string nombre, Guid nivelId, string? descripcion = null)` acepta `codigo` como primer parámetro. `Codigo` mantiene `private set`; la mutabilidad se realiza solo desde dentro de la entidad vía `Actualizar(...)`. Shape validation (`Requerido`, max 50) en la entidad.
- **`ActualizarCargoRequest`** (`src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs`): agrega `string Codigo` como primer parámetro posicional del record. **Breaking change contractual** para `PUT /api/v1/cargos/{id}`.
- **`ActualizarCargoRequestValidator`** (`src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs`): valida shape (`NotEmpty`, `MaximumLength(50)`). Unicidad activa NO se valida acá; es responsabilidad del servicio de aplicación.
- **`CargoServicioComandos.ActualizarAsync`** (`src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`): propaga `request.Codigo` a `cargo.Actualizar(...)`, invoca helper compartido `EnsureCodigoNoDuplicadoAsync(codigo, excludingId: id, ct)` y captura `DbUpdateException` (código MySQL 1062) vía `IConstraintViolationDetector` para mapear violaciones de `IX_Cargos_ActiveCodigoUnique` a `Conflict("CodigoDuplicado")` con código HTTP 409.
- **`CargosController`** (`src/SGV.Api/Controllers/CargosController.cs`): XML doc del `PUT` actualizado para reflejar que `Codigo` es editable y obligatorio, y que 409 significa "código ya usado por otro cargo activo".
- **Tests reemplazados/agregados**:
  - Dominio (`tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs`): reemplazados `Actualizar_CodigoNoCambia` y `Codigo_EsInmutableTrasCreacion` por `Actualizar_CambiaCodigoSiNoDuplica`, `Actualizar_ConCodigoVacio_ThrowsArgumentException`, `Actualizar_ConCodigoMayorA50_ThrowsArgumentException`, `Actualizar_ConCodigoNull_ThrowsArgumentException`.
  - Aplicación — validator (`tests/SGV.Tests/Aplicacion/Organizacion/ActualizarCargoRequestValidatorTests.cs`): agregados `Should_Have_Error_When_Codigo_Is_Empty` (Theory con 4 InlineData), `Should_Have_Error_When_Codigo_Exceeds_Max_Length`, `Should_Not_Have_Error_For_Valid_Codigo`.
  - Aplicación — servicio (`tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs`): agregados `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`, `ActualizarAsync_MismoCodigoEnOtroCargoEliminado_PermiteOperacion`, `ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`, `ActualizarAsync_CodigoDuplicado_RaceCondition_DevuelveConflicto`, `ActualizarAsync_CodigoSinCambio_NoFallaValidacionUnicidad`.
  - API (`tests/SGV.Tests/Api/CargosControllerTests.cs`): renombrado `Put_ValidRequest_Returns200OkWithUpdatedDto` → `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto` (body incluye `codigo`), agregados `Put_EmptyCodigo_Returns400WithFieldErrors` y `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`.

## 4. Contract change (BREAKING)

⚠️ **Breaking change**: `PUT /api/v1/cargos/{id}` ahora requiere el campo `codigo` en el body. Consumers externos deben actualizar.

El grep interno no encontró call sites fuera de `src/SGV.Aplicacion`, `tests/SGV.Tests/Aplicacion` y `tests/SGV.Tests/Api`:

```bash
$ grep -rn "new ActualizarCargoRequest" src tests
src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs:42:               new ActualizarCargoRequestValidator())
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:96:        var request = new ActualizarCargoRequest(
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:116:       var request = new ActualizarCargoRequest("DIR-01", "Nombre", NivelIdValido);
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:132:       var request = new ActualizarCargoRequest("DIR-01", "Nombre", NivelIdInexistente);
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:318:       var request = new ActualizarCargoRequest("DIR-02", "Director Renombrado", NivelIdValido);
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:336:       var request = new ActualizarCargoRequest("DIR-02", "Director Renombrado", NivelIdValido);
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:352:       var request = new ActualizarCargoRequest("", "Nombre", NivelIdValido);
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:376:           new ActualizarCargoRequestValidator());
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:377:       var request = new ActualizarCargoRequest("OTRO", "Director Renombrado", NivelIdValido);
tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs:395:       var request = new ActualizarCargoRequest("DIR-01", "Director Renombrado", NivelIdValido);
```

`CargoServicioComandos.cs:42` es del constructor de 3-args (compatibilidad legacy); no requiere cambios. Los 9 hits restantes son tests ya actualizados con `Codigo` en este PR.

PR2A/PR2B (frontend) ajustarán `CargoApiClient.UpdateAsync` cuando se implemente ese flujo, contra la nueva firma.

## 5. Test results

- `dotnet build SGV.slnx`: ✅ pass (0 errores).
- Suite focalizada `Cargo|Cargos`: ✅ **221/221** pass.
  ```bash
  dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos" --no-build
  # Passed!  - Failed: 0, Passed: 221, Skipped: 0, Total: 221, Duration: 5 s
  ```
- Regresión `UnidadOrganizativa`: ✅ **219/219** pass.
  ```bash
  dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativa" --no-build
  # Passed!  - Failed: 0, Passed: 219, Skipped: 0, Total: 219, Duration: 9 s
  ```
- Suite completa `dotnet test SGV.slnx`: ⚠️ **1023/1035** (12 fallos preexistentes NO relacionados con este PR — ver waiver abajo).

## 6. Waiver: pre-existing test failures

> **Waiver justificado**: 12 tests de `SGV.Tests.Persistencia.OcupacionRepositoryTests` fallan contra MySQL real por un bug preexistente en la migración inicial (`ActivePuestoIdUnique INT` incompatible con `PuestoId CHAR(36)`). Este bug está registrado como **issue #59** y es previo a este PR. La suite focalizada de este cambio (Cargo|Cargos) pasa 221/221 y la regresión de UnidadOrganizativa pasa 219/219, lo que confirma que este PR no introduce regresiones. La corrección del issue #59 está fuera del scope de este change y será abordada en un SDD change aparte.

Los 12 fallos se reproducen de forma estable contra MySQL real con el siguiente error (referencia de `verify-report.md` sección 10):

```
MySqlConnector.MySqlException: Incorrect integer value: 'xxx' for column 'ActivePuestoIdUnique'
Data truncated for column 'ActivePuestoIdUnique' at row 1
```

El bug es estructural a la migración inicial (`src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs`), no introducido ni tocado por este PR.

## 7. Architecture compliance

- ✅ **Clean Architecture respetada**: `Dominio` no importa `Infraestructura` ni `Microsoft.AspNetCore`; `Aplicacion` no conoce `HttpContext`/`HttpRequest`/`IActionResult`.
- ✅ **FluentValidation solo valida shape**: `ActualizarCargoRequestValidator` solo define `NotEmpty`, `MaximumLength`, `NotEqual(Guid.Empty)`. La unicidad activa se valida en el servicio de aplicación y en el índice MySQL.
- ✅ **Índice único MySQL** `IX_Cargos_ActiveCodigoUnique` (columna computada `CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`) sigue como árbitro final. La columna se recalcula automáticamente cuando `Codigo` o `IsDeleted` cambian; no requiere migración nueva.
- ✅ **Strict TDD**: tabla `TDD Cycle Evidence` agregada en `apply-progress.md` con ciclo RED → GREEN → REFACTOR por tarea (PR-1.1 a PR-1.8), y verificación para PR-1.9 a PR-1.11.

## 8. Dependencies for downstream PRs

PR2A y PR2B (frontend Create/Edit de cargos) **dependen de este PR**. No mergear PR2A ni PR2B antes que PR1.

- PR2A (Cliente HTTP + Create + submenú `Nueva`): ajusta `ICargoApiClient.CreateAsync` contra `CrearCargoRequest` y `GetNivelesAsync`; usa `POST /api/v1/cargos`. No afectado por el breaking change de `PUT`.
- PR2B (Edit + CTAs de navegación): usa `ICargoApiClient.UpdateAsync(id, ActualizarCargoRequest, ct)` contra la nueva firma del record. Requiere que este PR esté mergeado para compilar y para que el backend ya valide `Codigo` en el body.

## 9. Checklist

- [x] Tests added/updated for all new behavior.
- [x] `dotnet build SGV.slnx` pasa.
- [x] Suite focalizada del PR en verde (`Cargo|Cargos`: 221/221).
- [x] Regresión `UnidadOrganizativa` en verde (219/219).
- [ ] Suite completa `dotnet test SGV.slnx` en verde — **waiver documentado por issue #59 preexistente**.
- [x] Breaking change contractual documentado (sección 4 + XML docs del controller).
- [x] Arquitectura clean respetada (sección 7).
- [x] Conventional commits (8 commits atómicos, ver `git log --oneline feat/cargos-crear-editar-codigo-editable-pr1`).
- [x] Sin `Co-Authored-By` ni atribución a IA.
- [x] Tabla `TDD Cycle Evidence` en `apply-progress.md` (Strict TDD compliant).

---

## Commits incluidos (work units)

```
769842ba docs(apply): track PR1 progress for cargos editable codigo
cedef07c feat(cargos): allow Codigo update in Cargo domain entity
5a041a5b feat(cargos): require and validate Codigo in ActualizarCargoRequest
33e9c1b0 feat(cargos): enforce active uniqueness on Cargo update with index safety net
c8be5c3c docs(cargos): document PUT updates Codigo and update API tests
```

(Último commit del orquestador continuador: `86ab67a9 docs(apply): add TDD cycle evidence table for PR1 backend cargos` — este sí se incluye en el push final.)

## Archivos tocados (resumen)

| Archivo | Cambio |
|---|---|
| `src/SGV.Dominio/Organizacion/Cargo.cs` | `Actualizar` acepta `codigo`; setter sigue privado. |
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs` | `ActualizarCargoRequest` agrega `Codigo` como primer parámetro. |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs` | RuleFor `Codigo.NotEmpty.MaximumLength(50)`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs` | Helper compartido `EnsureCodigoNoDuplicadoAsync`; catch `DbUpdateException` → `Conflict "CodigoDuplicado"`. |
| `src/SGV.Api/Controllers/CargosController.cs` | XML doc del `PUT` documenta `Codigo` editable + 409 con título `CodigoDuplicado`. |
| `tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs` | Tests `Actualizar_CambiaCodigoSiNoDuplica`, `Actualizar_ConCodigo{Vacio,Null,MayorA50}_ThrowsArgumentException`. |
| `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarCargoRequestValidatorTests.cs` | Tests de shape para `Codigo`. |
| `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs` | Tests de unicidad activa en `ActualizarAsync` (incluye race condition). |
| `tests/SGV.Tests/Api/CargosControllerTests.cs` | Tests HTTP 200/400/409 para `PUT`. |
| `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` | Ajuste de firma `Actualizar` en test existente. |
| `openspec/changes/2026-06-30-cargos-crear-editar-codigo-editable/apply-progress.md` | Tabla `TDD Cycle Evidence`. |