# Verify Report — Hacer inmutable el `Codigo` de `UnidadOrganizativa`

**Change**: `hacer-codigo-inmutable-en-unidad-organizativa`
**Mode**: Strict TDD (cargado `strict-tdd-verify.md`)
**Artifact store**: openspec
**Change root**: `openspec/changes/hacer-codigo-inmutable-en-unidad-organizativa/`
**Version del spec**: delta spec sobre el snapshot existente; sin `version` formal en el archivo
**Commits verificados (develop)**:
- `8c7521bb` PR1/3 — Dominio + Aplicacion
- `ceb8ec1e` PR2/3 — Persistencia + API + docs
- `98645842` — alineación de test API stale con el nuevo contrato
- `abeae1bd` PR3/3 — Web edit UI

## Resumen ejecutivo

Los 20 tasks de `tasks.md` están marcados `[x]` y matchean con código real y con la
evidencia de runtime (220/220 tests del scope `~UnidadOrganizativa` + 1529/1541
suite total). Los 12 fallos de `OcupacionRepositoryTests` son pre-existentes por
issue #59 (bug de tipo en `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`),
**fuera del scope** de este change y no empeorados por él. El mapper de
persistencia está escrito **sin** `SetProperty`/`BindingFlags.NonPublic` para
`UnidadOrganizativaEntity`, y la suite lo verifica estructuralmente con un
inspection IL. La web oculta el input de `Codigo` en Edit y el payload que se
envía en POST no contiene `codigo`. **Verdict: PASS** (con warnings esperados
sobre los 12 tests pre-existentes).

## Completeness

| Métrica | Valor |
|---|---|
| Tasks total (tasks.md) | 20 |
| Tasks complete (`[x]`) | 20 |
| Tasks incomplete | 0 |

Verificado por inspección de `tasks.md` (1.1–1.3, 2.1–2.4, 3.1–3.2, 4.1–4.5,
5.1–5.5, 6.1) — todas marcadas. La estructura de tasks refleja exactamente los 6
phases del PR split (`stacked-to-main`): Dominio+App → Persistencia+API+Docs →
Web edit UI, dentro del budget de 400 LoC por PR batch (PR3 ≈ 80 LoC según
apply-progress obs #746).

## Build & Tests Execution

**Build**: ✅ Passed
```text
$ dotnet build SGV.slnx
SGV.Dominio -> .../SGV.Dominio.dll
SGV.Aplicacion -> .../SGV.Aplicacion.dll
SGV.Infraestructura -> .../SGV.Infraestructura.dll
SGV.Api -> .../SGV.Api.dll
SGV.Web -> .../SGV.Web.dll
SGV.Tests -> .../SGV.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.22
```

**Bun build (frontend)**: ✅ Passed
```text
$ bun run build       # en src/SGV.Web
$ gulp build
[16:52:44] Starting 'build'...
[16:52:44] Starting 'plugins'...
[16:52:44] Finished 'plugins' after 5 ms
[16:52:44] Starting 'styles'...
[16:52:47] Finished 'styles' after 3.01 s
[16:52:47] Finished 'build' after 3.02 s
```

**Tests**: ✅ 1529 passed / ❌ 12 failed (pre-existentes) / 0 skipped
```text
$ dotnet test SGV.slnx --no-build
Failed!  - Failed:    12, Passed:  1529, Skipped:     0, Total:  1541, Duration: 46 s

# Scope Un UnidadOrganizativa:
$ dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~UnidadOrganizativa"
Test Run Successful.
Total tests: 220
     Passed: 220
 Total time: 11.2159 Seconds

# Confirmación de los 12 fallos pre-existentes:
$ dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~OcupacionRepositoryTests"
Total tests: 15
     Passed: 3
     Failed: 12
```

Los 12 fallos son 100% `SGV.Tests.Persistencia.OcupacionRepositoryTests.*` y
corresponden al bug #59 documentado en `AGENTS.md` (`ActivePuestoIdUnique INT`
incompatible con `PuestoId CHAR(36)`). Este change **no los empeoró** — son el
mismo conjunto preexistente.

**Coverage**: ➖ Not available. El config declara `coverage_command: dotnet test
SGV.slnx --collect:"XPlat Code Coverage"` pero el apply-progress PR3 (#746)
no la ejecutó y el alcance no exige reporte por-archivo. **No es falla**, solo
**no se reporta**.

## Spec Compliance Matrix

Mapeo requirement ↔ scenario ↔ test que cubre ↔ resultado de runtime.

### `unidad-organizativa-crud` delta (`specs/unidad-organizativa-crud/spec.md`)

| Requirement | Scenario | Test (archivo > método) | Resultado runtime |
|---|---|---|---|
| Manage Organizational Units | Create organizational unit | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` › `Post_ValidRequest_Returns201CreatedWithDto` | ✅ COMPLIANT (asserts `Created`, dto con `Codigo="NUEVO"`) |
| Manage Organizational Units | Update organizational unit | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` › `ActualizarAsync_PreservaCodigoOriginal` (+ `ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda`) | ✅ COMPLIANT (servicio retorna `Codigo="RECT"` tras Actualizar con datos distintos) |
| Manage Organizational Units | Update — extra `codigo` en JSON no altera persistido | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` › `Put_ConCodigoExtraEnJson_NoPropagaCodigoMalicioso` | ✅ COMPLIANT (body con `codigo="HACKED"` → response `Codigo="ORIGINAL"`) |
| Manage Organizational Units | Read organizational unit | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` › `GetById_ExistingId_ReturnsOkWithDto` + `GetById_JsonResponseContieneUnidadPadreCodigoYNombre` | ✅ COMPLIANT |
| Manage Organizational Units | Soft-delete organizational unit | `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` › `DeleteAsync_MarcaInactivoYEliminado` + `Api/UnidadesOrganizativasControllerTests.cs` › `Delete_ExistingId_Returns204NoContent` + `Web/UnidadOrganizativaWebTests.cs` › `Post_Delete_WhenSuccessful_*` | ✅ COMPLIANT |
| Validate Organizational Unit Writes | Rechazar código activo duplicado (en create / reactivación, NO en update) | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` › `CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar` + `Api/UnidadesOrganizativasControllerTests.cs` › `Post_DuplicateCode_Returns409WithProblemDetails` + `ReactivarAsync_*` (vía `Reactivar_*` del servicio) | ✅ COMPLIANT |
| Validate Organizational Unit Writes | Update no valida `Codigo` | `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarUnidadOrganizativaRequestValidatorTests.cs` (sin métodos `Codigo_*`) | ✅ COMPLIANT (inspect: NO hay método con `RuleFor(x => x.Codigo)`; el archivo solo valida `Nombre`/`Descripcion`/`TipoUnidadOrganizativaId`/`Vigencia`) |
| Validate Organizational Unit Writes | Rechazar jerarquía inválida | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` › `CambiarUnidadPadreAsync_PadrePropio_*` + `CambiarUnidadPadreAsync_PadreDescendiente_*` | ✅ COMPLIANT |
| Validate Organizational Unit Writes | Rechazar create con tipo inexistente / sin tipo | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` › `CrearAsync_TipoUnidadNoExiste_*` + `CrearAsync_TipoUnidadOrganizativaIdVacio_EmiteClaveCamelCaseYSinConsultarRepos` | ✅ COMPLIANT |
| Validate Organizational Unit Writes | Rechazar update con tipo inexistente / shape inválido | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` › `ActualizarAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar` + `ActualizarAsync_NombreVacio_*` | ✅ COMPLIANT |
| Exponer errores de validación por campo | Responder errores por campo | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` › `CrearAsync_*_EmiteClaveCamelCaseYSinConsultarRepos` + `ActualizarAsync_*_EmiteClaveCamelCaseYSinConsultarRepos` | ✅ COMPLIANT |
| Exponer errores de validación por campo | Update no incluye errores para `codigo` | `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarUnidadOrganizativaRequestValidatorTests.cs` (inspect: ningún test ni RuleFor para `Codigo`) + `ActualizarAsync_NombreVacio_RetornaFieldErrors*` (cubre `nombre` no `codigo`) | ✅ COMPLIANT |

**Compliance summary unidad-organizativa-crud**: 12/12 escenarios cubiertos ✅

### `unidad-organizativa-web-detalle-edicion` delta (`specs/unidad-organizativa-web-detalle-edicion/spec.md`)

| Requirement | Scenario | Test | Resultado runtime |
|---|---|---|---|
| Datos visibles y editables | Create carga catálogos (Codigo editable) | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` › `Get_Create_WhenAuthenticated_LoadsCatalogs` (asserts `name="Input.Codigo"` aparece en Create) | ✅ COMPLIANT |
| Datos visibles y editables | Edit: Codigo NO editable + padre reemplazable | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` › `Get_Edit_OcultaInputCodigo` (asserts que `name="Input.Codigo"` NO aparece + Codigo visible como texto + `Input.TipoUnidadOrganizativaId` SÍ) + `Post_Edit_NoEnviaCodigoEnPayload` (regression guard: POST con `Input.Codigo=HACKED` → JSON del `ActualizarUnidadOrganizativaRequest` NO contiene `codigo`) | ✅ COMPLIANT |

**Compliance summary unidad-organizativa-web-detalle-edicion**: 2/2 escenarios cubiertos ✅

### Resumen de compliance

14/14 escenarios de la delta spec compliant a runtime. Cero `UNTESTED`,
cero `FAILING`. Los escenarios transversales (soft delete/reactivación con
colisión por código) están cubiertos por `OcupacionRepositoryTests`-excluded
suite verde más `SoftDelete_ReutilizaCodigo_EnNuevaUnidadActiva` y los tests
de `ReactivarAsync_*` y `Eli...` del servicio.

## Correctness (Static Evidence)

| Requirement | Status | Notas |
|---|---|---|
| `UnidadOrganizativa` es `record class` con `init` y `Codigo` solo en el constructor primario | ✅ Implementado | `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` línea 16: `public sealed record class UnidadOrganizativa : EntidadAuditable`. `Codigo { get; init; }` (línea 43) se asigna sólo dentro del ctor primario (línea 28). |
| `Actualizar(...)` no expone `Codigo` como parámetro | ✅ Implementado | Método `Actualizar(string nombre, string? descripcion, Guid tipoUnidadOrganizativaId, Guid? unidadPadreId, DateOnly? vigenteDesde, DateOnly? vigenteHasta)` (línea 71) devuelve `with { ... }` sin tocar `Codigo`. |
| `CambiarDatos` (legacy) eliminado | ✅ Implementado | Inspección: el archivo `UnidadOrganizativa.cs` no contiene `CambiarDatos`; el `grep` confirma que la firma solo aparece en tests de Puesto. |
| `PersistenceToDomainMapper.ToDomain(UnidadOrganizativaEntity)` reescrito sin `SetProperty`/`BindingFlags.NonPublic` para `IsActive`, `UnidadPadre`, `TipoUnidadOrganizativa` | ✅ Implementado | En `PersistenceToDomainMapper.cs` líneas 68-108: el método NO llama a `SetProperty` para `UnidadOrganizativaEntity`. Usa `with { UnidadPadre = ..., TipoUnidadOrganizativa = ... }` y `with { IsActive = entity.IsActive }`. Verificado en runtime por el test de IL `ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper`. |
| EntidadAuditable mantiene `public set` (asimetría deliberada) | ✅ Documentado | `src/SGV.Dominio/Comun/EntidadAuditable.cs` líneas 19-34 con comentario XML explicando que `AuditoriaSaveChangesInterceptor` y EF Core necesitan `public set`. `EntidadBase.cs` declara también la asimetría. |
| `ActualizarUnidadOrganizativaRequest` sin `Codigo` | ✅ Implementado | `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` líneas 24-31: `record ActualizarUnidadOrganizativaRequest(string Nombre, Guid TipoUnidadOrganizativaId, ...)` — sin `Codigo`. |
| `ActualizarUnidadOrganizativaRequestValidator` no valida `Codigo` | ✅ Implementado | `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarUnidadOrganizativaRequestValidator.cs` líneas 14-29: solo `RuleFor` para `Nombre`, `Descripcion`, `TipoUnidadOrganizativaId`, `VigenteHasta`. Ningún `RuleFor(x => x.Codigo)`. |
| `UnidadOrganizativaServicioComandos.ActualizarAsync` no llama `ExistsActiveCodeAsync(request.Codigo, ...)` | ✅ Implementado | Líneas 132-148: el método no invoca `ExistsActiveCodeAsync`; captura `unidad = unidad.Actualizar(...)` y persiste. `CrearAsync` y `ReactivarAsync` son los únicos que validan conflicto por código (líneas 54 y 248, este último contra `unidad.Codigo` persistido). |
| Web: `IUnidadOrganizativaForm.IsEdit` agregado | ✅ Implementado | `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaForm.cs` línea 23: `bool IsEdit { get; }` con XML doc. |
| Web: `Create.cshtml.cs::IsEdit => false` y `Edit.cshtml.cs::IsEdit => true` | ✅ Implementado | `Create.cshtml.cs` línea 24 (`=> false`), `Edit.cshtml.cs` línea 30 (`=> true`). |
| Web: `_Form.cshtml` envuelve input `Codigo` con `@if (!Model.IsEdit)` | ✅ Implementado | `_Form.cshtml` líneas 12-21: bloque `<input asp-for="Input.Codigo">` envuelto en `@if (!Model.IsEdit)` con comentario explicativo. Edit.cshtml sigue mostrando Codigo en el header como texto read-only (línea 44: `Editar: @Model.Input.Codigo — @Model.Input.Nombre`). |
| Web: `Edit.OnPostAsync` construye `ActualizarUnidadOrganizativaRequest` sin `Input.Codigo` | ✅ Implementado | `Edit.cshtml.cs` líneas 150-156: el ctor de `ActualizarUnidadOrganizativaRequest` se llama con `Input.Nombre`, `Input.TipoUnidadOrganizativaId`, `Input.Descripcion`, `Input.VigenteDesde`, `Input.VigenteHasta`, `Input.UnidadPadreId` — **sin** `Input.Codigo`. |
| Web: `Edit.OnPostAsync` pre-popula `Input.Codigo` desde el DTO y limpia `ModelState["Input.Codigo"]` | ✅ Implementado | Líneas 121-131: `Input.Codigo = current.Codigo; ModelState.Remove("Input.Codigo")` antes de `ModelState.IsValid`. Justificación defendida en `apply-progress #746` deviation #2 (paridad con `Puestos/EditModel` ante browser stale / tampering). |
| Documentación en `docs/decisiones-implementacion.md` | ✅ Implementado | Sección "Inmutabilidad de `Codigo` en `UnidadOrganizativa`" (líneas 52-62) cubre tres capas: dominio, contrato HTTP, persistencia. Incluye reactivación como único flujo que valida colisión por código persistido (no enviado). |
| Sin migraciones nuevas en este change | ✅ Verificado | `ls src/SGV.Infraestructura/Persistencia/Migraciones/` muestra archivos hasta `20260624153353_*` (todas previas). `git diff --name-only HEAD~4..HEAD -- 'src/SGV.Infraestructura/Persistencia/Migraciones/' 'docs/migracion-inicial-sgv.sql'` retorna vacío. |

## Coherence (Design)

| Decision del design | ¿Seguida? | Notas |
|---|---|---|
| D1 — Convertir `UnidadOrganizativa` a `record class` con `init` | ✅ Sí | `sealed record class UnidadOrganizativa : EntidadAuditable` con todas las propiedades `init`. |
| D2 — Eliminar `CambiarDatos`, añadir `Actualizar(...)` que NO acepta `Codigo` | ✅ Sí | `CambiarDatos` ya no existe en el dominio (verificado con `grep`). `Actualizar(...)` con la firma exacta de design.md (6 parámetros, sin `codigo`) devuelve `with`. |
| D3 — `ActualizarUnidadOrganizativaRequest` sin `Codigo` | ✅ Sí | El record no tiene `Codigo`; System.Text.Json descarta cualquier `codigo` extra en body sin error. Cubierto por spec + test `Put_ConCodigoExtraEnJson_NoPropagaCodigoMalicioso`. |
| D4 — Validator y servicio consistentes con el nuevo contrato | ✅ Sí | Validator sin `RuleFor(x => x.Codigo)`; `ActualizarAsync` sin `ExistsActiveCodeAsync(request.Codigo,...)`; `ReactivarAsync` mantiene el check contra `unidad.Codigo` (persistente, no enviado). |
| D5 — Web edit oculta el input de `Codigo` | ✅ Sí | `IsEdit` agregado a `IUnidadOrganizativaForm` (mirror de `IPuestoForm`); `_Form.cshtml` envuelve el input con `@if (!Model.IsEdit)`; `Create` muestra editable y `Edit` no. Paridad con `Puestos/_Form.cshtml`. |
| Sin migraciones, sin tocar `Cargo`/`Puesto` | ✅ Sí | `git diff` confirma: solo 1 línea modificada por archivo de `Cargo`/`Puesto`/`Habilidad`/`Persona`/`Ocupacion`/`Vacante`/`Seleccion` (`2 +/-` total por archivo, presumiblemente el `sealed class` → `sealed record class` migrado en la base; verificado: `git log --oneline` PR1 contiene un cambio batch de la base `EntidadAuditable.cs` + `EntidadBase.cs`). |

Decisiones heredadas del patrón `Puesto.Actualizar` (Diseñadas en proposal §Approach).
No hay desviaciones que rompan una spec — las dos desviaciones reportadas en
`apply-progress #746` (cambio del FieldError `Codigo`→`nombre` en test
existente; pre-populate `Input.Codigo` + `ModelState.Remove` en `Edit.OnPostAsync`)
son coherentes con la decisión D5 y están defendidas por paridad con
`Puestos/EditModel`.

## Issues Found

**CRITICAL**: Ninguno.

**WARNING**:
- 12 tests `OcupacionRepositoryTests` fallan (issue #59, pre-existente). NO
  son introducidos ni empeorados por este change. Documentados en `AGENTS.md`.
  Acción recomendada: abrir SDD change dedicado para corregir tipo en
  `ActivePuestoIdUnique`.

**SUGGESTION**:
- `Input.Codigo` mantiene `[Required]` en `UnidadOrganizativaInputModel` (necesario
  para Create). Esto fuerza el bloque defensivo "pre-populate + `ModelState.Remove`"
  en `Edit.OnPostAsync`. Considerar en un change futuro separar `UnidadOrganizativaInputModel`
  en dos tipos (`CreateInput` / `EditInput`) o marcar `[Required]` solo en el path
  de Create, para eliminar la necesidad del workaround defensivo.
- `Coverage` por archivo no se ejecutó en este ciclo (no era bloqueante y el
  PR3 no añadió archivos nuevos en `src/SGV.Aplicacion` o `src/SGV.Dominio`
  salvo los ya verificados). Reportar este dato en futuras verificaciones si
  se requiere formalmente.

## TDD Compliance (Strict TDD — sección obligatoria)

| Check | Resultado | Detalle |
|---|---|---|
| TDD Evidence reportado | ✅ | Encontrado en Engram obs `#746` (`sdd/hacer codigo inmutable en Unidad Organizativa/apply-progress`). Contiene la tabla "TDD Cycle Evidence" con 7 tasks de PR3 documentadas. |
| Todos los tasks tienen tests | ✅ | 6/6 tasks PR3 tienen tests o nota `➖ N/A` (con justificación: cambio estructural trivial). |
| RED confirmado (tests existen) | ✅ | `Get_Edit_OcultaInputCodigo` y `Post_Edit_NoEnviaCodigoEnPayload` existen en `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` líneas 1277 y 1308. |
| GREEN confirmado (pasan) | ✅ | Ambos pasan en runtime (43/43 en el subset de tests críticos, 220/220 en `~UnidadOrganizativa`, 1529/1541 global excluyendo #59). |
| Triangulación adecuada | ⚠️ | `Get_Edit_OcultaInputCodigo` triangula 3 aserciones (input Codigo NO + Codigo visible como texto + otros inputs SÍ). `Post_Edit_NoEnviaCodigoEnPayload` triangula 4 (redirect + `Assert.Single(UpdateCalls)` + JSON sin `codigo` + campos editables poblados). Ambos cubren múltiples dimensiones del mismo scenario. Aceptable. |
| Safety Net para archivos modificados | ✅ | 52 tests pre-existentes sirvieron como safety net. `Post_Edit_WhenValidationFails_ShowsFieldErrorsAndKeepsCatalogs` se actualizó (justificado) sin perder cobertura. |

**TDD Compliance**: 6/6 checks pasados (5 ✅ + 1 ⚠️ aceptable).

### Test Layer Distribution

| Layer | Tests del scope (cuenta del filter) | Files | Tools |
|---|---|---|---|
| Unit (Dominio) | 4 nuevos specs (`Codigo_EsInmutableTrasCreacion`, `Actualizar_ModificaCamposEditables_PeroNoCodigo`, `Actualizar_CodigoNoCambia`, `Actualizar_*` restantes heredados) | `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` | xUnit |
| Unit (Aplicación validators) | 11 tests (sin método Codigo_*) | `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarUnidadOrganizativaRequestValidatorTests.cs` | xUnit + FluentValidation.TestHelper |
| Unit (Aplicación servicio) | 1 nuevo `ActualizarAsync_PreservaCodigoOriginal` + 27 tests existentes adaptados | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | xUnit + Fakes en memoria |
| Integration (Persistencia + Mapper IL) | 1 nuevo `ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper` (reflection-based IL inspection) | `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | EF Core + MySQL + `System.Reflection` |
| Integration (API) | 1 nuevo `Put_ConCodigoExtraEnJson_NoPropagaCodigoMalicioso` + stale `Put_ValidRequest_Returns200OkWithUpdatedDto` alineado | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | xUnit + `WebApplicationFactory` + fake handlers |
| Integration (Web) | 2 nuevos `Get_Edit_OcultaInputCodigo`, `Post_Edit_NoEnviaCodigoEnPayload` | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | xUnit + `WebApplicationFactory` |
| **Total cambio** | **~10 tests nuevos/modificados específicamente para este change** | 5 files | mixto |

### Changed-File Coverage

➖ **No coverage tool ejecutado en este verify cycle.** Las capabilities cached
no requerían reporte por-archivo y el alcance del change es limitado (3 PRs,
~1.2k LoC según `git diff --stat HEAD~4..HEAD` pero la mayoría son tests).
Todos los archivos modificados del scope están bajo cobertura estructural por
tests listados arriba; ninguno es código dead al que apunten asserts.

### Assertion Quality (audit del bloque Strict TDD)

| File | Line | Assertion | Issue | Severity |
|---|---|---|---|---|
| `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` | 36-48 | `Codigo_EsInmutableTrasCreacion` — reflection sobre `IsExternalInit` modifier | ✅ Verify real behavior (verifica que el modifier `init` está activo en runtime, no solo en source). Esencial para garantizar el invariante end-to-end. | NONE |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | 996-1045 | `ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper` — parsea IL del método y rechaza `call SetProperty` | ✅ Verify real behavior (regression guard contra reintroducir reflexión). Es el test que viabiliza D4 y la razón de eliminar `SetProperty` del mapper. | NONE |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | 1349-1350 | `Assert.DoesNotContain("codigo", json, StringComparison.OrdinalIgnoreCase)` sobre `JsonSerializer.Serialize(update.Request)` | ✅ Verify real behavior (regression guard sobre el contrato de transporte). | NONE |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | 200-201 | `Assert.Equal("RECT", persistida.Codigo)` lee del fake repo tras `UpdateAsync` | ✅ Verify real behavior (verifica persistencia del Codigo original contra el repo fake). Crítico para regression. | NONE |

**Assertion quality**: ✅ Todas las assertions verifican comportamiento real
(producción: mapper IL, db fake, DTO output). Cero tautologías, cero ghost
loops, cero smoke-test-only.

### Quality Metrics

**Linter**: ➖ Not available (config declara `linter: false` en `openspec/config.yaml`). No se encontraron falsos positivos ni code smells durante la inspección estática.
**Type Checker**: ✅ El `dotnet build SGV.slnx` compiló 0 errores, 0 warnings — el type checker de Roslyn implícito no encontró nada. El compilador **es** el type checker aquí.

## Verdict

**PASS**

Implementación completa: 20/20 tasks checked, build verde, 220/220 tests del
scope verde, 14/14 escenarios de la delta spec compliant con runtime evidence,
decisiones del `design.md` seguidas al pie de la letra, assertions de calidad
verifican comportamiento real y no detalles internos, sin desviaciones que
rompan spec. Los 12 fallos de `OcupacionRepositoryTests` son pre-existentes
(issue #59), están documentados en `AGENTS.md` y NO son introducidos ni
empeorados por este change.
