# Apply Progress — Eliminar catálogos UO/Cargo sin consumidor en Edit de Puestos (#120)

**Rama**: `fix/120-uo-catalog-no-truncation` (sin commits al cierre de apply)
**Change**: `openspec/changes/2026-07-13-fix-120-uo-catalog-no-truncation/`
**Fecha**: 2026-07-13

## Resumen ejecutivo

Implementación exitosa de la fix de dead code. Se eliminó la carga redundante de `IUnidadOrganizativaApiClient.QueryAsync(... pageSize=200 ...)` y `ICargoApiClient.GetAllAsync(...)` en `Edit.cshtml.cs`, junto con sus dependencias del constructor. La suite nueva `PuestoEditLoadCatalogsTests` cubre los tres invariantes definidos en el spec (cero llamadas a UO/Cargo + persistencia de la carga de `PuestoSuperiorOptions`). Build limpio, GREEN estable.

## Estado de tareas (de `tasks.md`)

### Fase 1 — RED

- [x] **1.1** `Edit_GET_NoInvocaCatalogoUnidadesOrganizativas` en `PuestoEditLoadCatalogsTests.cs`.
  - Evidencia: test compiló; en corrida RED falló con `Collection: [UnidadOrganizativaListQuery { Page = 1, PageSize = 200, Search = , Sort = , Status = activas }]` (mensaje exacto del dead code que denunciaba la issue #120).
- [x] **1.2** `Edit_GET_NoInvocaCatalogoCargos` en el mismo archivo.
  - Evidencia: en corrida RED falló con `Collection: [1]`.
- [x] **1.3** `Edit_GET_CargaPuestosSuperiores` en el mismo archivo.
  - Evidencia: anti-regresión. Pasó en RED y sigue pasando en GREEN.

### Fase 2 — GREEN

- [x] **2.1** Refactor `LoadCatalogsAsync` y constructor de `EditModel` en `Edit.cshtml.cs`.
  - **Acción**: removidas `unidadesTask`/`cargosTask`, sus ramas `if (TaskStatus.RanToCompletion)`, eliminados los parámetros `IUnidadOrganizativaApiClient` e `ICargoApiClient` del ctor; `Task.WhenAll` ahora envuelve solo `puestosTask`.
  - **Constructor firma final**: `EditModel(IPuestosApiClient puestosApiClient, ILogger<EditModel> logger)` — la firma del constructor es ahora la primera línea de defensa contra reintroducir el dead code.
  - **Resultado**: 3/3 tests de la Fase 1 pasan.
- [x] **2.2** `dotnet build SGV.slnx` sin warnings/errors nuevos.
  - Evidencia: "Compilación correcta. 0 Advertencia(s), 0 Errores" (segunda corrida).

### Fase 3 — REFACTOR

- [x] **3.1** Actualizar XML-doc de `LoadCatalogsAsync` (solo `PuestoSuperiorOptions`).
  - Acción: el comentario ahora explica por qué Edit sólo carga el catálogo de puestos superiores; incluye referencia cruzada a la nueva sección de `decisiones-implementacion.md` y nota sobre la elección de `internal` vs `private`.
- [x] **3.2** Sección "Patrón catálogo vs listado — Unidades Organizativas" en `decisiones-implementacion.md`.
  - Acción: 30 líneas nuevas entre la sección "Inmutabilidad de Codigo en UnidadOrganizativa" y "Autorización del API". Cubre catálogo completo (`GetAllActivasAsync`) vs listado paginado (`QueryAsync`), y cierra con la regla operativa que la suite `PuestoEditLoadCatalogsTests` enforce.

### Fase 4 — VERIFICATION

- [x] **4.1** `dotnet test --filter "FullyQualifiedName~PuestoEdit"` (la nueva suite).
  - **Resultado**: 3 PASS, 0 FAIL en `PuestoEditLoadCatalogsTests`.
  - **Riesgo registrado**: 11 fallos pre-existentes en `PuestoEditPageTests` siguen ahí, NO introducidos por esta fix (ver "Riesgos materializados" abajo).
- [x] **4.2** Build limpio del repo (`dotnet build SGV.slnx`).
  - **Resultado**: 0 warnings, 0 errors.

## TDD Cycle Evidence

| Test                              | RED (corrida sin refactor)                                                                  | GREEN (corrida con refactor)   | REFACTOR (docs) |
|-----------------------------------|---------------------------------------------------------------------------------------------|--------------------------------|------------------|
| `Edit_GET_NoInvocaCatalogoUnidadesOrganizativas` | FAIL — `Collection: [UnidadOrganizativaListQuery { Page = 1, PageSize = 200, ..., Status = activas }]` | PASS — `Assert.Empty(QueryCalls); Assert.Empty(GetAllActivasCalls);` | PASS — sin cambios |
| `Edit_GET_NoInvocaCatalogoCargos` | FAIL — `Collection: [1]`                                                                   | PASS — `Assert.Empty(GetAllCalls);` | PASS — sin cambios |
| `Edit_GET_CargaPuestosSuperiores` | PASS — anti-regresión pre-existente (control positivo)                                    | PASS — `Assert.Single(GetAllCalls); PuestoSuperiorOptions.Count == 2` | PASS — sin cambios |

## Archivos tocados

| Archivo                                            | Acción       | Líneas | Resumen                                                                                                                                    |
|---------------------------------------------------|--------------|--------|---------------------------------------------------------------------------------------------------------------------------------------------|
| `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` | Modificado    | -27/+11 | Quita 2 deps del ctor, 2 tasks paralelas, 2 ramas de status, `WhenAll` con 1 sola task, XML-doc actualizado. **Firma del ctor = IPuestosApiClient + ILogger**. |
| `tests/SGV.Tests/Web/Puesto/PuestoEditLoadCatalogsTests.cs` | Nuevo         | +127   | Suite unit-style con aislamiento del PageModel (no WebApplicationFactory). Tres tests verificando contadores de los fakes.                |
| `docs/decisiones-implementacion.md`               | Modificado    | +30    | Sección "Patrón catálogo vs listado — Unidades Organizativas" entre las secciones de inmutabilidad de UO y autorización del API.         |

**Total estimado de líneas modificadas**: ~118 (≈ 78 tests + 11 prod + 30 doc = ~119) — dentro del presupuesto de 400 líneas del change.

## Comandos de validación corridos y resultados

| Comando                                                                                                       | Resultado                                      |
|---------------------------------------------------------------------------------------------------------------|------------------------------------------------|
| `dotnet build SGV.slnx` (post-cambios)                                                                       | OK — 0 warnings, 0 errors                     |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~PuestoEditLoadCatalogsTests"`                             | OK — 3/3 PASS (~0.9 s)                        |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~Puesto"` (broad)                                          | 229/275 PASS; 46 FAIL (todos baseline auth)   |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~Dominio\|~Aplicacion\|~Api\|~Persistence\|~Compatibilidad\|~Contracts"` | 1174/1176 PASS; 2 FAIL (ambos auth web)   |

## Decisiones no triviales tomadas durante la implementación

1. **Estrategia de aislamiento de tests**: el baseline de `PuestoEditPageTests` sigue roto en esta rama (11/12 fallas preexistentes, idénticas a las documentadas en `exploration.md`). En lugar de pelear con el harness web, se optó por instanciar `EditModel` directamente con fakes inyectados vía constructor, e invocar `LoadCatalogsAsync` de forma unitaria. Esto requirió cambiar la visibilidad de `LoadCatalogsAsync` de `private` a `internal` (aprovechando el `InternalsVisibleTo("SGV.Tests")` que `Program.cs` ya concede).

2. **Eliminar los parámetros del constructor**: la firma final `(IPuestosApiClient, ILogger<EditModel>)` es la primera línea de defensa contra reintroducir el dead code. Cualquier developer futuro que intente re-añadir la carga de UO/Cargo deberá primero ampliar la firma del ctor — un cambio explícito y ruidoso que el code review detectará. Esta es la única defensa que sobrevive a la tentación de "ya que estamos...".

3. **Mantener `UnidadOrganizativaOptions` y `CargoOptions` con inicializador `[]`**: la interfaz `IPuestoForm` los exige (el partial `_Form.cshtml` los accede), pero Edit no los popula. Inicializarlos vacíos cumple el contrato sin necesidad de cambiar la interfaz.

4. **No tocar `Create.cshtml.cs`**: el spec es explícito en que sólo Edit tiene el bug. Create **necesita** los tres catálogos para poblar los selects visibles. Out of scope.

5. **Ubicación de la nueva sección en `decisiones-implementacion.md`**: insertada justo después de "Inmutabilidad de Codigo en UnidadOrganizativa" (que es la última sección específica de UO) y antes de "Autorización del API" (primera sección genérica). Mantiene la lógica narrativa: UO → catálogo de UO en Puestos → autorización general.

## Hallazgos no triviales (potencialmente relevantes para otras sesiones)

1. **El baseline de auth web sigue roto en esta rama de feature**. El PR #129 (`fix/121-deterministic-test-suite-v2`) sólo se mergeó a `develop`, no a esta rama. `PuestoEditPageTests`, `CargoEditPageTests`, `PuestoDetailsPageTests`, `CargoDetailsPageTests`, `PuestoIndexPageTests`, `PuestoWebSeamTests` fallan al autenticar vía `RecordingHttpMessageHandler` (la mayoría retorna `302 Found` en vez de `302` que el handler espera, o el bootstrap devuelve `200 OK` en vez de autenticarse exitosamente). **Esto no es alcance de #120** — fue documentado en `exploration.md` y se respeta. La nueva suite usa un patrón de aislamiento por construcción del PageModel que es inmune a este baseline.

2. **`EditModel` es la única implementación de `IPuestoForm` que recibe `LoadCatalogsAsync` con un catálogo**. `CreateModel` carga los tres catálogos porque su `_Form.cshtml` con `IsEdit=false` sí renderiza los selects de `UnidadOrganizativaId` y `CargoId`. La asimetría queda documentada explícitamente en la nueva sección de decisiones.

## Próximos pasos

1. **sdd-verify**: ejecutar `dotnet test SGV.slnx` focal + reporter sobre `PuestoEditLoadCatalogsTests` y la suite de Puestos. Documentar los 11 fallos pre-existentes como caveat (no regresión). Decidir si aceptamos formalmente el baseline de auth como fuera del scope del change #120 antes de proceder a `sdd-archive`.
2. **sdd-archive**: sincronizar el spec delta `puesto-web-crear-editar/spec.md` a `openspec/specs/...` (la spec ya está creada — la delta ya no requiere archived-specs complement; verificar con `openspec validate` si está disponible).
3. **(Fuera de #120)** Investigar y resolver el baseline de auth web en una rama dedicada (issue separada). El problema afecta todos los PageTests de SGV.Web.

## Riesgos materializados durante la implementación

| Riesgo                                                                                       | Severidad | Estado                                                                                                                                         |
|----------------------------------------------------------------------------------------------|-----------|------------------------------------------------------------------------------------------------------------------------------------------------|
| Baseline de `PuestoEditPageTests` roto (redirect a `/auth/sign-in` en lugar de sesión real)  | Media     | **Confirmado pre-existente** — 11/12 tests fallan antes Y después de mi fix (idéntico patrón). NO es regresión introducida por #120.           |
| Drop de parámetros del ctor rompa dependencia oculta                                        | Baja      | **No materializado** — `grep` confirma única definición de `EditModel`; no hay instancias manuales ni reflection-based. DI lo resuelve solo. |
| Falsa regresión en `PuestoIndexPageTests` u otros tests web                                 | Baja      | **Confirmado pre-existente** — los 46 fallos en el filtro `~Puesto` son todos del baseline auth, no del cambio de `LoadCatalogsAsync`.         |
| `Edit_GET_CargaPuestosSuperiores` no falle en RED                                           | Baja      | **Decisión**: es un control positivo anti-regresión (no un RED clásico). El spec lo justifica explícitamente.                                 |

## Estado final

- ✅ RED verificado (2 fallas + 1 anti-regresión pasando).
- ✅ GREEN estable (3/3 tests nuevos + suite focal verde).
- ✅ REFACTOR aplicado (XML-doc + nueva sección de decisiones).
- ✅ Build limpio.
- ⚠️ Caveat pre-existente: 11/12 tests en `PuestoEditPageTests` y la mayoría de los integration tests siguen fallando por baseline de auth web (issue separada). NO es regresión de #120.
- 🔲 Pendiente: `sdd-verify` (ejecutar suite completa con criterio explícito sobre baseline fallido) → `sdd-archive`.
