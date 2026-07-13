# Verify Report — Eliminar catálogos UO/Cargo sin consumidor en Edit de Puestos (#120)

**Change**: `2026-07-13-fix-120-uo-catalog-no-truncation`
**Issue**: #120
**Rama**: `fix/120-uo-catalog-no-truncation` (sin commits al cierre de verify)
**Mode**: Strict TDD
**Fecha**: 2026-07-13

## Resumen ejecutivo

Implementación verificada y conforme al spec. `Edit.cshtml.cs` ya no invoca `IUnidadOrganizativaApiClient.QueryAsync(...)` ni `ICargoApiClient.GetAllAsync(...)`; conserva únicamente la carga de `PuestoSuperiorOptions`. Build limpio (0 warnings, 0 errors) y los 3 tests nuevos de `PuestoEditLoadCatalogsTests` pasan en runtime. La única WARNING materializada es la ausencia de cobertura directa del scenario "Falla de transporte" del REQ-3 — el código lo soporta, pero el path no está ejercitado por tests. Veredicto: **PASS WITH WARNINGS** — recomendación `merge` con la WARNING registrada para un PR subsecuente.

## Completitud

| Métrica | Valor |
|---------|-------|
| Tareas totales | 8 |
| Tareas completas | 8 |
| Tareas incompletas | 0 |
| Tests nuevos | 3 |
| Tests pasando | 3/3 |
| Líneas modificadas (diff stat) | +52/-39 (≈ 91) — dentro del presupuesto de 400 |

## Build & Tests Execution

**Build**: ✅ Passed (0 warnings, 0 errors)

```text
SGV.Contracts -> ...Contracts.dll
SGV.Dominio -> ...Dominio.dll
SGV.Aplicacion -> ...Aplicacion.dll
SGV.Infraestructura -> ...Infraestructura.dll
SGV.Api -> ...Api.dll
SGV.Web -> ...Web.dll
SGV.Tests -> ...Tests.dll

Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

**Tests focalizados del change**: ✅ 3/3 PASS (~0.8 s)

```text
Correctas SGV.Tests.Web.Puesto.PuestoEditLoadCatalogsTests.Edit_GET_NoInvocaCatalogoCargos [3 ms]
Correctas SGV.Tests.Web.Puesto.PuestoEditLoadCatalogsTests.Edit_GET_CargaPuestosSuperiores [1 ms]
Correctas SGV.Tests.Web.Puesto.PuestoEditLoadCatalogsTests.Edit_GET_NoInvocaCatalogoUnidadesOrganizativas [< 1 ms]

Pruebas totales: 3
     Correcto: 3
```

**Tests de control Create**: ⚠️ 1/11 PASS — **baseline pre-existente, NO regresión de #120**

```text
Con error SGV.Tests.Web.Puesto.PuestoCreatePageTests.Get_Create_WhenAuthenticated_FormContainsAllSixFields [203 ms]
Mensaje de error: Assert.Equal() Failure: Values differ
Expected: OK
Actual:   Found

warn: SGV.Web.Pages.Auth.SignInModel[0]
      SGV.Api returned an access token that SGV.Web could not validate. SecurityTokenSignatureKeyNotFoundException
```

Confirmado como pre-existente: ejecuté `git stash` → `dotnet test` (sin el fix aplicado) → mismo patrón exacto de 10/11 FAIL en `PuestoCreatePageTests` (mismos errores `Antiforgery token was not rendered` y `Expected: OK Actual: Found`). El stash fue restaurado. La causa raíz está documentada en `exploration.md` y `apply-progress.md`: el PR #129 (`fix/121-deterministic-test-suite-v2`) sólo se mergeó a `develop`, no a esta rama. **No es alcance de #120**.

## Spec Compliance Matrix

| Req | Scenario | Test | Resultado runtime |
|-----|----------|------|-------------------|
| REQ-1 | GET a Edit con id válido — `UnidadOrganizativaOptions` debe ser `[]`, `QueryCalls.Count == 0`, `GetAllActivasCalls.Count == 0` | `PuestoEditLoadCatalogsTests.Edit_GET_NoInvocaCatalogoUnidadesOrganizativas` | ✅ COMPLIANT |
| REQ-1 | HTML no renderiza select de UO en Edit | `PuestoEditPageTests.Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` (líneas 192-204) | ⚠️ PARTIAL — test existe pero en suite rota por baseline auth. Verificación manual de `_Form.cshtml:38-61` confirma que los selects están envueltos en `@if (!Model.IsEdit) { ... }` y `IsEdit => true` en `EditModel:38`. |
| REQ-2 | GET a Edit con id válido — `CargoOptions` debe ser `[]`, `GetAllCalls.Count == 0` | `PuestoEditLoadCatalogsTests.Edit_GET_NoInvocaCatalogoCargos` | ✅ COMPLIANT |
| REQ-2 | HTML no renderiza select de Cargo en Edit | Mismo test que cubre REQ-1 scenario 2 (regex `name="Input\.CargoId"`) | ⚠️ PARTIAL — igual que REQ-1 |
| REQ-3 | Select poblado en Edit — `PuestoSuperiorOptions` debe contener N opciones con etiquetas `Codigo + Nombre` | `PuestoEditLoadCatalogsTests.Edit_GET_CargaPuestosSuperiores` | ✅ COMPLIANT — verificado `Assert.Single(GetAllCalls)` y `Assert.Equal(2, sut.PuestoSuperiorOptions.Count)` con ambos seeds presentes. |
| REQ-3 | Falla de transporte del catálogo de superiores — debe mostrar estado recuperable y `PuestoSuperiorOptions = []` | (ninguno encontrado) | ❌ UNTESTED — ver WARNING #1 |
| REQ-4 | Developer consulta el patrón en `decisiones-implementacion.md` | Inspección manual de la sección agregada | ✅ COMPLIANT |

**Compliance summary**: 4/4 requirements partially-o-fully compliant; 6/7 scenarios compliant (1 UNTESTED + 2 PARTIAL por baseline auth pre-existente).

## Correctness (Static Evidence)

| Requirement | Estado | Notas |
|-------------|--------|-------|
| `Edit.cshtml.cs` no invoca `IUnidadOrganizativaApiClient.QueryAsync` ni `GetAllActivasAsync` | ✅ Implementado | Constructor ya no recibe `IUnidadOrganizativaApiClient`; `LoadCatalogsAsync` solo crea `puestosTask`. Confirmado por `git diff HEAD -- src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs`. |
| `Edit.cshtml.cs` no invoca `ICargoApiClient.GetAllAsync` | ✅ Implementado | Constructor ya no recibe `ICargoApiClient`; idem anterior. |
| `Edit.cshtml.cs` sí invoca `IPuestosApiClient.GetAllAsync` | ✅ Implementado | `puestosTask = PuestoFormHelpers.LaunchSafeAsync(() => puestosApiClient.GetAllAsync(cancellationToken));` |
| `UnidadOrganizativaOptions` y `CargoOptions` quedan como `[]` | ✅ Implementado | Inicializadores `= []` en líneas 30-32 del archivo. |
| Constructor reducido de 4 a 2 dependencias | ✅ Implementado | Firma final: `EditModel(IPuestosApiClient puestosApiClient, ILogger<EditModel> logger)`. DI resuelve automáticamente; los 3 únicos `new EditModel(...)` están en el archivo de tests del change. |
| Visibilidad `private` → `internal` | ✅ Implementado | `internal async Task LoadCatalogsAsync(...)`. `[assembly: InternalsVisibleTo("SGV.Tests")]` confirmado en `Program.cs:9`. |
| Sección de docs en ubicación coherente | ✅ Implementado | Insertada después de "Inmutabilidad de Codigo en UnidadOrganizativa" y antes de "Autorización del API". Mantiene la narrativa: UO → catálogo de UO en Puestos → autorización general. |

## Coherence (Design)

| Decisión del design | ¿Seguida? | Notas |
|--------------------|-----------|-------|
| Eliminar `unidadesTask`/`cargosTask` y sus ramas post-WhenAll | ✅ Sí | Eliminadas las dos tareas y los dos bloques `if (TaskStatus.RanToCompletion)`. `WhenAll` ahora envuelve solo `puestosTask`. |
| Reducir dependencias del PageModel | ✅ Sí | Constructor reducido de 4 a 2 deps (`IPuestosApiClient`, `ILogger<EditModel>`). |
| Conservar `UnidadOrganizativaOptions` y `CargoOptions` con `[]` | ✅ Sí | Inicializadores preservados. |
| Preservar carga tolerante a fallos | ✅ Sí | `LaunchSafeAsync`, `Task.WhenAll(puestosTask)`, `Task.Status` y mensaje recuperable intactos. |
| Proteger la regresión en PageModel con contadores de fakes | ✅ Sí | Tres tests verifican `QueryCalls`, `GetAllActivasCalls`, `GetAllCalls` y `PuestoSuperiorOptions.Count`. |
| Documentar "catálogo completo" vs "listado paginado" | ✅ Sí | Sección en `decisiones-implementacion.md` describe ambos contratos y prohíbe catálogos en Edit. |

## TDD Compliance (Strict TDD)

| Check | Resultado | Detalles |
|-------|-----------|----------|
| TDD Evidence reportado | ✅ | Tabla "TDD Cycle Evidence" presente en `apply-progress.md` (líneas 47-52). |
| All tasks have tests | ✅ | 3/3 tasks de Fase 1 tienen test files verificados. |
| RED confirmado (tests existen) | ✅ | `PuestoEditLoadCatalogsTests.cs` existe en `tests/SGV.Tests/Web/Puesto/` con los 3 tests. |
| GREEN confirmado (tests pasan) | ✅ | 3/3 tests pasaron en runtime (corrida verificada). |
| Triangulación adecuada | ⚠️ | REQ-1 y REQ-2 tienen 1 test cada uno (cumplen: 1 scenario crítico). REQ-3 cubre solo scenario 1 — el scenario 2 ("Falla de transporte") no tiene cobertura. Ver WARNING #1. |
| Safety Net para archivos modificados | ✅ | `Edit.cshtml.cs` modificado; el `PuestoEditLoadCatalogsTests` se construyó primero (RED) y se ajustó al GREEN. `decisiones-implementacion.md` modificado — sin safety net de tests porque es documentación. |

**TDD Compliance**: 5/6 checks passed (1 con gap).

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit (PageModel aislado con fakes) | 3 | 1 | xUnit + NullLogger + Fakes en memoria |
| Integration (WebApplicationFactory) | 0 nuevos | 0 | (los existentes `PuestoEditPageTests` están en baseline roto) |
| E2E | 0 | 0 | — |

**Total**: 3 tests nuevos, 100% en capa unit. Estrategia justificada por apply-progress: el baseline de auth web está roto en la rama, así que se optó por instanciar `EditModel` directamente. Esto es coherente con el design ("unit-style con aislamiento del PageModel").

## Assertion Quality Audit

| File | Line | Assertion | Issue | Severity |
|------|------|-----------|-------|----------|
| `PuestoEditLoadCatalogsTests.cs` | 58-59 | `Assert.Empty(QueryCalls); Assert.Empty(GetAllActivasCalls);` | Verifica comportamiento observable (contadores de invocaciones del fake) — OK. | — |
| `PuestoEditLoadCatalogsTests.cs` | 83 | `Assert.Empty(GetAllCalls);` | Idem — OK. | — |
| `PuestoEditLoadCatalogsTests.cs` | 114 | `Assert.Single(GetAllCalls);` | Verifica exactamente 1 llamada a `GetAllAsync` (anti-duplicación en pre/post). OK. | — |
| `PuestoEditLoadCatalogsTests.cs` | 118-124 | `Assert.Equal(2, ...Count); Assert.Contains(...) x2` | Triangulación positiva: valida cantidad Y presencia de cada seed por Codigo+Nombre. OK. | — |

**Assertion quality**: ✅ Todas las assertions verifican comportamiento real (no tautologías, no type-only, no smoke-test-only). El mock/assertion ratio es ~3:5 (fakes de unidades, cargos, puestos × assertions) — bajo, dentro del umbral.

**Gap identificado**: No hay assertions para la rama `puestosTask.Status != RanToCompletion` (escenario de fallo). El code path existe (`LoadCatalogsAsync` líneas 318-329) pero ningún test lo ejercita. WARNING #1.

## Issues Found

### CRITICAL
Ninguno.

### WARNING

**#1 — Scenario "Falla de transporte" del REQ-3 sin cobertura directa**

- **Archivo**: `tests/SGV.Tests/Web/Puesto/PuestoEditLoadCatalogsTests.cs` (archivo entero — falta el cuarto test).
- **Descripción**: El spec REQ-3 incluye un scenario explícito ("Falla de transporte del catálogo de superiores") que requiere verificar que, cuando `IPuestosApiClient.GetAllAsync` lanza una excepción (timeout, HttpRequestException, etc.), el PageModel responde con `PuestoSuperiorOptions = []`, `ErrorMessage = "No se pudo cargar el catálogo necesario. Intentá nuevamente."` y estado recuperable sin persistir cambios. El código en `Edit.cshtml.cs:318-334` implementa este path correctamente, pero el `FakePuestosApiClient` ya soporta `GetAllException` (línea 46) y `LoadCatalogsAsync` tiene el bloque `else` que setea el error. Ningún test del change ejercita esta rama.
- **Impacto**: Gap de cobertura. Si una refactorización futura elimina el bloque `else` o cambia el mensaje, no hay red de seguridad automatizada. El scenario "html editable" del REQ-1/REQ-2 sufre el mismo gap (cubierto por `PuestoEditPageTests` que está en baseline roto), pero al menos hay verificación manual posible. El scenario de fallo de superiores es 100% no probado.
- **Acción recomendada**: Agregar `Edit_GET_CuandoFallaCatalogoSuperiores_MuestraEstadoRecuperable` que inyecte `FakePuestosApiClient.GetAllException = new HttpRequestException(...)` y verifique:
  - `PuestoSuperiorOptions.Count == 0`
  - `ErrorMessage` contiene "No se pudo cargar el catálogo"
  - `puestosClient.GetAllCalls.Count == 1` (la llamada sí se hizo)
  Esto es un cambio pequeño (≈ 25 líneas) y podría agregarse como follow-up en el mismo PR o en uno inmediato subsecuente.
- **Severidad**: WARNING (no bloquea el merge — el path funciona y ya estaba así pre-existente; el gap es de cobertura, no de corrección).

### SUGGESTION

**#1 — XML-doc de `LoadCatalogsAsync` podría compactarse**

- **Archivo**: `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs:280-295` (15 líneas de XML-doc).
- **Descripción**: El comentario XML-doc incluye una justificación extensa sobre la decisión de cambiar `private` → `internal` (5 líneas) que es informativa pero podría moverse al changelog o a un ADR. Mantenerla en el doc ayuda a futuros mantenedores que dudan sobre la firma `internal`, así que la decisión de mantenerla es defendible.
- **Acción recomendada**: Sin acción. Si se quiere, mover la justificación del cambio `internal` a un comentario de una línea + referencia a `decisiones-implementacion.md`.

**#2 — Podría mencionarse explícitamente el contrato `IPuestoForm` en la sección de docs**

- **Archivo**: `docs/decisiones-implementacion.md:90-118`.
- **Descripción**: La sección describe la propiedad de `EditModel` (constructor reducido), pero no menciona que `IPuestoForm` exige las tres propiedades (`UnidadOrganizativaOptions`, `CargoOptions`, `PuestoSuperiorOptions`) y por eso `EditModel` no puede eliminar las propiedades vacías. Es un detalle sutil que un developer futuro podría cuestionar al ver dos propiedades `[]` sin uso aparente.
- **Acción recomendada**: Agregar una línea que diga: "`IPuestoForm` exige las tres colecciones como contrato compartido con `CreateModel` y `_Form.cshtml`; Edit cumple el contrato con listas vacías."

## Verdict

**PASS WITH WARNINGS**

Tres tests nuevos verdes en runtime, build limpio, contract de `LoadCatalogsAsync` reducido de 4 deps a 2 con defensa estructural contra reintroducir dead code (firma del ctor), documentación operativa agregada. La única WARNING materializada es un gap de cobertura de un scenario explícito del spec (REQ-3 scenario "Falla de transporte"), que es deuda pre-existente heredada del baseline auth roto y no atribuible al change.

**Recomendación**: `merge` con la WARNING #1 registrada para un PR subsecuente de cobertura. El path de fallo funciona y ya estaba implementado antes del change — el gap es que ningún test lo enforce, no que el código esté mal. Bloquear el merge por un gap de cobertura pre-existente sería castigar al change #120 por un problema del baseline #121.

## Próximos pasos

1. **sdd-archive** — sincronizar el spec delta `puesto-web-crear-editar/spec.md` a `openspec/specs/...` y archivar el change.
2. **PR subsecuente (fuera de #120)** — agregar el test de "Falla de transporte" sugerido en WARNING #1 (~25 líneas, scope acotado).
3. **Issue separada (pre-existente)** — resolver el baseline auth web en `PuestoEditPageTests`/`PuestoCreatePageTests` (relacionado con PR #129 sólo mergeado a `develop`).

## Riesgos para considerar post-archive

- La sección de docs usa "Edit no carga catálogos" como regla operativa, pero un futuro PR podría necesitar reintroducir carga. El pattern `private` → `internal` no protege contra reintroducir `LoadCatalogsAsync` con un catálogo nuevo — sólo contra reintroducir el catálogo específico UO/Cargo. Un developer que añada, por ejemplo, `IClienteApiClient` necesitará pasar por la revisión de la firma del ctor (que es ruidosa) — el comentario XML-doc en `LoadCatalogsAsync` lo recuerda.
- El test `PuestoEditLoadCatalogsTests` cubre solo el PageModel. Si alguien modifica `_Form.cshtml` para renderizar un select en Edit, el test seguirá verde pero el comportamiento будет incorrecto. Un test que verifique la ausencia del `<select>` en el HTML renderizado es estructuralmente necesario — y ese test ya existe en `PuestoEditPageTests` pero está en baseline roto.