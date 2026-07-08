# Verify Report: Expone el botón Editar por fila en el listado de Puestos

> SDD verify para el change `2026-07-08-implementa-edicion-puesto-frontend`. Modo orquestador: `interactive`. Artifact store: `both` (OpenSpec filesystem + Engram). Strict TDD: ACTIVO. Idioma: español.

## Resumen

| Campo | Valor |
|-------|-------|
| Change | `2026-07-08-implementa-edicion-puesto-frontend` |
| Verdict | **pass-with-notes** |
| LoC modified | +221 / −21 en 5 archivos (presupuesto 400 LoC: OK) |
| Build | 0 errors, 0 warnings |
| Suites targeted | PuestosControllerTests + PuestoIndexPageTests: **44/44 PASS** |
| Suites Web | 407/407 PASS |
| Suites API | 431/431 PASS |
| Suite completa | 1527 PASS, 12 FAIL pre-existentes (issue #59, no relacionado), **0 regresiones** |
| Working tree isolation | OK — `DatosSemilla.cs` y migración `20260706221558_*` permanecen sin tocar |

## Spec compliance matrix

### Delta `puesto-management/spec.md`

| Escenario | Cobertura | Evidencia | Resultado |
|-----------|-----------|-----------|-----------|
| **Lectura autenticada exitosa** (GETs `2xx`) | `~13 tests CreateAdminClient()` cubriendo `GetAll`/`GetById` con 200 | `PuestosControllerTests.GetAll_ReturnsOkWithDtoArray`, `GetById_ExistingId_ReturnsOkWithDto`, `GetAll_WhenServicioEmpty_Returns200WithEmptyArray`, etc. | ✅ PASS |
| **Acceso anónimo rechazado** (`401`) | 6 tests: 2 GETs + 4 mutations | `GetAll_WithoutCredentials_ReturnsUnauthorized`, `GetById_WithoutCredentials_ReturnsUnauthorized`, `[Theory] Mutation_WithoutCredentials_ReturnsUnauthorized` (4 InlineData para POST/PUT/DELETE/PATCH) | ✅ PASS |
| **Mutación protegida por rol `Administrador`** (`403` no-admin / `2xx` admin) | 4 tests `*_WithAuthenticatedNonAdmin_ReturnsForbidden` + 13 tests admin `2xx` | `Create/Update/Delete/Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden` con `FakeAuthenticationDefaults.UserHeader` | ✅ PASS |

### Canonical `puesto-web-listado-detalle-baja/spec.md` (Requirement: Listado plano)

| Escenario | Cobertura | Evidencia | Resultado |
|-----------|-----------|-----------|-----------|
| **Carga inicial**: cada fila MUST ofrecer `Detalle`, `Editar` y `Eliminar` | `PuestoIndexPageTests.Get_Index_WhenAuthenticated_RendersActivePuestosTable` extendido | `Assert.Contains($"href="/organizacion/puestos/detalles/{first.Id}", …)`, `Assert.Contains($"href="/organizacion/puestos/editar/{first.Id}", …)`, `Assert.Contains("data-bs-title=\"Editar\"", …)`, `Assert.Contains("data-puesto-delete-form", …)` | ✅ PASS |

### Verificación visual de UI (`Index.cshtml`)

| Item | Esperado | Encontrado | Resultado |
|------|----------|------------|-----------|
| Botón `Detalle` (info) en rama activas | sí | `Index.cshtml:185-190` (dentro de `if (!Model.IsDeletedView)`) | ✅ |
| Botón `Editar` (warning + `ti ti-edit`) en rama activas | sí | `Index.cshtml:191-196` (dentro de `if (!Model.IsDeletedView)`) | ✅ |
| `<form data-puesto-delete-form>` en rama activas | sí | `Index.cshtml:197-…` (dentro de `if (!Model.IsDeletedView)`) | ✅ |
| Botón `Editar` en rama eliminadas | NO | Ausente (sólo `<form data-puesto-reactivate-form>` se renderiza; verificado por test 3.2) | ✅ |
| Comment obsoleto "PR 2 — solo Detalle y Eliminar" borrado | sí | Eliminadas las 4 líneas `Index.cshtml:183-186` del baseline | ✅ |

## Delta auth behavior (producción)

| Verificación | Evidencia en código | Resultado |
|-------------|---------------------|-----------|
| `[Authorize]` a nivel clase en `PuestosController` | `PuestosController.cs:16 [Authorize]` | ✅ |
| `[Authorize(Roles = RolesSgv.Administrador)]` en `Create` | línea 73 | ✅ |
| `[Authorize(Roles = …)]` en `Update` | línea 102 | ✅ |
| `[Authorize(Roles = …)]` en `Delete` | línea 131 | ✅ |
| `[Authorize(Roles = …)]` en `Reactivate` | línea 155 | ✅ |
| Read endpoints (`GetAll`, `GetById`) autenticados pero NO admin-gated | sin `[Authorize(Roles=…)]` en líneas 35, 51 | ✅ |
| `[ProducesResponseType(401)]` en GETs | líneas 37, 53 | ✅ |
| `[ProducesResponseType(401)]` + `[ProducesResponseType(403)]` en writes | líneas 76-77, 105-106, 133-134, 157-158 | ✅ |

## Working tree isolation

```bash
git status --short
 M src/SGV.Api/Controllers/PuestosController.cs
 M src/SGV.Infraestructura/Persistencia/DatosSemilla.cs              (no relacionado — dirty pre-existente)
 M src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml
 M src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs
 M tests/SGV.Tests/Api/PuestosControllerTests.cs
 M tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs
?? openspec/changes/2026-07-08-implementa-edicion-puesto-frontend/   (artefactos SDD)
?? src/SGV.Infraestructura/Persistencia/Migraciones/20260706221558_AgregarDatosSemillaPuestos.cs    (no relacionado)
?? src/SGV.Infraestructura/Persistencia/Migraciones/20260706221558_AgregarDatosSemillaPuestos.Designer.cs  (no relacionado)
```

```bash
git diff --stat -- src/SGV.Web/Pages/Organizacion/Puestos \
                    src/SGV.Api/Controllers/PuestosController.cs \
                    tests/SGV.Tests/Web/Puesto \
                    tests/SGV.Tests/Api/PuestosControllerTests.cs
 src/SGV.Api/Controllers/PuestosController.cs       |  27 ++++
 .../Pages/Organizacion/Puestos/Index.cshtml        |  10 +-
 .../Pages/Organizacion/Puestos/Index.cshtml.cs     |  15 +++
 tests/SGV.Tests/Api/PuestosControllerTests.cs      | 150 ++++++++++++++++++---
 tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs |  40 ++++++
 5 files changed, 221 insertions(+), 21 deletions(-)
```

`DatosSemilla.cs` y los dos archivos de migración `20260706221558_*` permanecen **sin tocar** por este change — verificado por `git status` (aparecen dirty pero su contenido no incluye ninguno de los archivos en scope de este change).

## Non-goals verification

| Non-goal | Esperado | Encontrado | Resultado |
|----------|----------|------------|-----------|
| Domain changes | NO | `git diff` no toca `src/SGV.Dominio/` | ✅ |
| Application command/handler changes | NO | `git diff` no toca `src/SGV.Aplicacion/` | ✅ |
| Infrastructure/persistence/migration changes (en scope de este change) | NO | `git diff` no toca `src/SGV.Infraestructura/Persistencia/` ni carpetas de migraciones | ✅ |
| `?status=activas\|eliminadas` para backend | NO (follow-up) | Backend Puestos sigue sin endpoint segmentado | ✅ |
| Relajación de guards de Cargos/Habilidades/Unidades Organizativas | NO | No tocado | ✅ |

## Test evidence

```bash
dotnet build SGV.slnx --nologo
  Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PuestosControllerTests|FullyQualifiedName~PuestoIndexPageTests"
  Passed! - Failed: 0, Passed: 44, Skipped: 0, Total: 44, Duration: 3s
    # PuestosControllerTests: 27
    # PuestoIndexPageTests: 17

dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Web"
  Passed! - Failed: 0, Passed: 407, Skipped: 0, Total: 407, Duration: 32s

dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Api"
  Passed! - Failed: 0, Passed: 431, Skipped: 0, Total: 431, Duration: 14s

dotnet test SGV.slnx --no-build
  Failed! - Failed: 12, Passed: 1527, Skipped: 0, Total: 1539, Duration: 48s
    # 12 FAIL son OcupacionRepositoryTests con "Data truncated for column 'ActivePuestoIdUnique' at row 1"
    #   (issue #59 — bug de tipo en migración inicial, pre-existente y NO relacionado con este change)
```

## Findings

### CRITICAL

- (ninguno)

### WARNING

- (ninguno)

### SUGGESTION

- **[S1] Status context round-trip mismatch entre `BuildEditRouteValues` y `Puestos/Edit`.**
  
  El helper `BuildEditRouteValues(Guid id)` (`Index.cshtml.cs:262-269`) serializa el segmento vigente como `returnStatus = Segmento`. Sin embargo, `Puestos/Edit.cshtml.cs:84` y `:142` esperan `[FromQuery(Name = "status")]` (no `returnStatus`):
  
  ```csharp
  [FromQuery(Name = "status")] string? status = null
  ```
  
  Resultado observable: un usuario que hace clic en **Editar** desde `status=eliminadas` aterriza en `/organizacion/puestos/editar/{id}?p=1&...&returnStatus=eliminadas`. Como `status` queda `null` (binding silencioso falla), `ReturnStatus` se setea a `string.Empty`. Tras guardar, el redirect a Details conserva `returnStatus=null` → el usuario aterriza en Activas, no en Eliminadas. Pérdida de UX, no de correctitud.
  
  El design.md:64-66 afirma: "`returnStatus` (no `status`) es el nombre que `Puestos/Edit` acepta (`Edit.cshtml.cs:85-95`)". Esa frase es **factualmente incorrecta** — el binding real es `status` (línea 84, 142). La asimetría viene del precedent espejado: `Cargos/Edit` no bindea ni `status` ni `returnStatus`, por lo que el bug queda dormido allí.
  
  **Mitigaciones posibles** (no se aplican en este change; queda registrada para que el orquestador/user decida):
  1. Cambiar `BuildEditRouteValues` para emitir ambos nombres: `new { id, p = …, …, returnStatus = Segmento, status = Segmento }` (defensa en profundidad).
  2. Añadir `[FromQuery(Name = "returnStatus")] string? returnStatus = null` como alias en `Puestos/Edit.cshtml.cs:OnGetAsync` y `:OnPostAsync`.
  3. Cambiar `BuildEditRouteValues` para emitir `status` (no `returnStatus`) — coincide con el binding de Puestos/Edit pero rompe el paralelo con Cargos.
  
  Severidad: SUGGESTION. No rompe la funcionalidad core de Edit+Save; sólo la preservación del segmento en el round-trip desde eliminadas.

## Tasks status

| Fase | Tareas | Estado |
|------|--------|--------|
| 1 — Frontend (helper + botón Editar + cleanup comment) | 1.1, 1.2, 1.3 | ✅ GREEN |
| 2 — Backend (`[Authorize]` transversal) | 2.1, 2.2, 2.3, 2.4 | ✅ GREEN |
| 3 — Tests web (presencia/ausencia botón Editar) | 3.1, 3.2 | ✅ GREEN |
| 4 — Tests API (matriz 401/403/2xx) | 4.1, 4.2, 4.3, 4.4, 4.5 | ✅ GREEN |
| 5 — Validación y aislamiento | 5.1, 5.2, 5.3 | ✅ GREEN (sólo build + targeted tests re-corridos; apply-progress ya documentó la suite completa pre-archive) |

## Strict TDD evidence

Re-corrida la verificación de tests:

| Capa | Test | RED previo | GREEN actual | TRIANGULACIÓN |
|------|------|------------|---------------|---------------|
| Web (Index) | `Get_Index_WhenAuthenticated_RendersActivePuestosTable` (extendido con asserts Editar) | ✅ confirmado por `apply-progress.md` (1 test falló antes de Fase 1) | ✅ actual pasa | href + data-bs-title + delete form coexisten |
| Web (Index) | `Get_Index_WhenDeletedView_DoesNotRenderEditButton` | ✅ confirmado por `apply-progress.md` (pasa por ausencia pre, assertivo post Fase 1) | ✅ actual pasa | DoesNotContain Edit + Contains Reactivate form + Contains formaction |
| API (Authorization) | `Controller_HasAuthorizeAttribute` | ✅ confirmado (atributo no existía) | ✅ actual pasa | single intrinsic |
| API (anonymous) | `GetAll_WithoutCredentials_ReturnsUnauthorized` | ✅ | ✅ | par con GetById |
| API (anonymous) | `GetById_WithoutCredentials_ReturnsUnauthorized` | ✅ | ✅ | par con GetAll |
| API (anonymous theory) | `Mutation_WithoutCredentials_ReturnsUnauthorized` (POST/PUT/DELETE/PATCH) | ✅ (4/4 fallaban) | ✅ | 4 InlineData en una Theory |
| API (non-admin) | `Create/Update/Delete/Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ (4/4 con 201/200/204/200 esperaban 403) | ✅ | 4 tests aislados |
| Web (smoke) | 16 tests pre-existentes en `PuestoIndexPageTests` | n/a (no tocados) | ✅ | coverage previa sin regresión |
| API (smoke) | 13 tests pre-existentes migrados a `CreateAdminClient` | n/a (pre-condición para mantener 2xx) | ✅ | suite verde post `[Authorize]` |

## Verdict

**pass-with-notes**

Cambio correctamente implementado contra el delta `puesto-management/spec.md` y el requisito canónico de UI en `puesto-web-listado-detalle-baja/spec.md`. Build limpio, 44/44 tests targeted pasan, 0 regresiones en las capas Web y API. Las 12 fallas de `OcupacionRepositoryTests` son pre-existentes (issue #59) y NO están relacionadas con este change.

El único hallazgo (S1, SUGGESTION) es de naturaleza UX — el round-trip de preservación del segmento `status=eliminadas` desde Edit vuelve a Activas en lugar de Eliminadas, porque `BuildEditRouteValues` emite `returnStatus` mientras `Puestos/Edit` bindea `status`. No bloquea el archive porque: (a) es un espejo verbatim del precedent Cargos (donde queda dormido por no haber binding equivalente); (b) la funcionalidad core de Edit+Save no se ve afectada; (c) el design.md declara erróneamente que `Puestos/Edit` acepta `returnStatus`, así que la implementación es fiel a su especificación aunque la especificación describa mal la realidad del binding.

## Risks and follow-ups (registrados, no aplican al verdict)

1. **S1 (SUGGESTION)** — Status context round-trip desde `status=eliminadas` se pierde tras Edit; ver mitigaciones propuestas en la sección Findings.
2. **Issue #59** — Pre-existente, independiente; bloquea 12 tests de `OcupacionRepositoryTests` por tipo `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`. No bloquea este change.
3. **Dirty files no relacionados** — `DatosSemilla.cs` y `Migraciones/20260706221558_*` requieren su propio PR; aplicar el `git diff` por paths permitidos antes de cualquier commit de este change.

## Persistencia

- OpenSpec filesystem: `openspec/changes/2026-07-08-implementa-edicion-puesto-frontend/verify-report.md` (este archivo).
- Engram: `topic_key: sdd/2026-07-08-implementa-edicion-puesto-frontend/verify-report`, `type: architecture`, `scope: project`, `capture_prompt: false` (verificado, persistido en el mismo flujo).
