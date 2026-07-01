# PR 2A — Frontend cargos: Create page, cliente HTTP, sidenav `Nueva`

## ⚠️ `size:exception` solicitada

Este PR excede el review budget de **400 líneas** acordado en el change:
**+974 / -5 líneas en 14 archivos** (vs forecast inicial de ~350).

**Justificación**: el task list original (`tasks.md` PR-2A) listó 12 tareas combinando cliente HTTP + form scaffolding + Create page + Index CTA + Sidenav + **6 web tests**. Los 6 tests consumen 304 líneas (`CargoCreatePageTests.cs`) y la cobertura amplia (incluyendo el caso de catálogo caído y validación server-side) sale naturalmente por encima del budget. Reducirlo artificialmente bajaría la calidad del test suite del módulo cargos.

**Alternativas evaluadas y descartadas**:
- **Dividir en sub-PRs** (PR2A.1 cliente+form + PR2A.2 página+nav+tests): alineado con la decisión original, pero agregaría un round-trip de review extra para un cambio cohesivo. La unidad funcional es esta.
- **Reducir tests a los 5 obligatorios**: dejaría afuera el caso de catálogo recuperable, que es importante para UX y se prueba en 11 líneas.

## 1. Summary

Implementa el flujo de **Create** de cargos en el frontend de `SGV.Web`: cliente HTTP extendido, infraestructura de form compartido, página `Create.cshtml` con PRG, CTA en el listado y entry `Nueva` en el sidenav. NO incluye Edit (eso es PR2B).

## 2. Why

PR1 (backend, ya mergeado como #60) habilitó `PUT /api/v1/cargos/{id}` para hacer `Codigo` editable y desbloqueó el contrato para que el front pueda crear cargos sin necesidad de mantener invariantes locales. PR2A es el primer paso visible para administradores: crear un nuevo cargo.

## 3. What changes

### Cliente HTTP (`src/SGV.Web/Integration/Organizacion/`)
- `ICargoApiClient` extendido con `CreateAsync(CrearCargoRequest, ct)` y `GetNivelesAsync(ct)`.
- `CargoApiClient` implementa ambos métodos contra `POST /api/v1/cargos` y `GET /api/v1/niveles-cargo`. Parsea `ValidationProblemDetails` y `ProblemDetails`, mapea 400/404/409 a `CargoCommandResult` con `FieldErrors` o `CargoErrorType.Conflict`.
- `FakeCargoApiClient` extendido con `CreateResult`, `CreateCalls`, `CreateException`, `NivelesResult`, `NivelesException`, `NivelesCalls` para tests web aislados de backend.

### Form scaffolding (`src/SGV.Web/Pages/Organizacion/Cargos/` + helpers)
- `CargoInputModel` (DataAnnotations: `Codigo` required + max 50, `Nombre` required + max 200, `Descripcion`? + max 1000, `NivelId` required).
- `ICargoForm` con `Input` + `NivelOptions` + `ErrorMessage` + `IsEdit` (siempre `false` en PR2A, queda listo para PR2B) + `ReturnToListUrl`.
- `CargoFormHelpers` (`Integration/Organizacion/`): `BuildReturnToListUrl` (preserva `p/search/sort` del listado) + `ApplyFieldErrorsToModelState`.
- `_Form.cshtml` partial Inspinia (`form-floating` para Codigo/Nombre/Descripcion, dropdown `NivelId` con `asp-items="@(new SelectList(...))"`).

### Páginas (`src/SGV.Web/Pages/Organizacion/Cargos/`)
- **Nuevo**: `Create.cshtml` (`/organizacion/cargos/crear`, `@model CreateModel`) + `Create.cshtml.cs` (`[Authorize]`).
  - `OnGetAsync`: carga `GetNivelesAsync` con try/catch; estado recuperable si el catálogo falla.
  - `OnPostAsync`: valida ModelState, construye `CrearCargoRequest`, llama `CreateAsync`, mapea:
    - 201 → **PRG** a `Details` con TempData success.
    - 409 → `ModelState.AddModelError("Input.Codigo", result.Error.Message)`.
    - 400 con `FieldErrors` → `ApplyFieldErrorsToModelState`.
    - Otro error → `ErrorMessage` general.
  - Recarga el catálogo antes de `return Page()` en cualquier fallo.
- **Modificado**: `Index.cshtml` agrega botón "Crear cargo" en el card-header (icono `ti ti-plus`).

### Navegación (`src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml`)
- Entry `Nueva` dentro del submenú del grupo `Cargos`, mismo patrón que `Unidades Organizativas`. Estado `active` propagado vía `StartsWithSegments("/organizacion/cargos")` (ya existente).

## 4. Test results

- `dotnet build SGV.slnx` → ✅ 0 errores, 0 warnings.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos"` → ✅ **240/240 pass** (234 previos + 6 nuevos).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Web"` → ✅ **98/98 pass** (88 previos + 10 nuevos: 4 en `CargoApiClientTests` + 6 en `CargoCreatePageTests`).
- `bun install && bun run build` en `src/SGV.Web` → ✅ pipeline frontend OK.
- Suite completa `dotnet test SGV.slnx` → ⚠️ **1042/1054** (12 fallos preexistentes waiverados por issue #59, ver sección Waiver).

## 5. Waiver: pre-existing test failures

> **Waiver justificado**: 12 tests de `SGV.Tests.Persistencia.OcupacionRepositoryTests` fallan contra MySQL real por un bug preexistente en la migración inicial (`ActivePuestoIdUnique INT` incompatible con `PuestoId CHAR(36)`). Bug registrado como **issue #59** y previo a este PR. La suite focalizada de este cambio pasa 240/240 y la regresión de UnidadOrganizativa pasa 219/219, lo que confirma que este PR no introduce regresiones. La corrección del issue #59 está fuera del scope de este change.

## 6. Architecture compliance

- ✅ **Clean Architecture respetada**: web no mete lógica de dominio ni de persistencia. Cliente HTTP en `Integration/Organizacion/`, contrato del form en `Pages/Organizacion/Cargos/`, helpers reutilizables en `Integration/Organizacion/`.
- ✅ **Inspinia UI**: `form-floating`, `btn-primary`, `card`, `ti ti-plus` icon, dropdown con `asp-items`.
- ✅ **PRG** (Post/Redirect/Get) tras éxito.
- ✅ **Validación server-side con roundtrip**: ModelState se valida antes de llamar al API client; los errores del backend se traducen a `asp-validation-for` en el mismo formulario.
- ✅ **Strict TDD**: 4 tests RED → GREEN en cliente HTTP + 6 tests RED → GREEN en Create page; tabla `TDD Cycle Evidence` en `apply-progress.md`.

## 7. Dependencies for downstream PRs

PR2B (frontend Edit + Details CTA) **depende de este PR**. No mergear PR2B antes que PR2A. PR2B necesitará:
- Agregar `UpdateAsync(id, ActualizarCargoRequest, ct)` a `ICargoApiClient` (nuevo método, no rompe esta firma).
- Crear `Edit.cshtml` + `Edit.cshtml.cs` reutilizando `_Form.cshtml` con `IsEdit = true`.
- Actualizar `CargoDetailsPageTests` (que actualmente asume detalle estrictamente readonly — ver nota en `apply-progress.md`).

## 8. Decisiones técnicas relevantes

- **`ICargoForm` se ubica en `src/SGV.Web/Pages/Organizacion/Cargos/`** (asimétrico con `IUnidadOrganizativaForm` que está en `Integration/Organizacion/`). Decisión deliberada del orchestrator para este PR. Si en archive se quiere unificar la convención, mover el archivo y actualizar los usings.
- **Mapeo 409 → `Input.Codigo` directo**: el 409 del backend viene como `ProblemDetails` plano, no como `ValidationProblemDetails`, por lo que `FieldErrors` está vacío. Se usa `ModelState.AddModelError("Input.Codigo", ...)` directo para que el mensaje aparezca en el `<span data-valmsg-for="Input.Codigo">` esperado.
- **Recarga del catálogo tras POST fallido**: `OnPostAsync` llama `LoadCatalogsAsync` antes de `return Page()` en todos los caminos de fallo, para que el dropdown siga funcional si el usuario corrige y reintenta.

## 9. Checklist

- [x] Tests added/updated for all new behavior (10 tests nuevos: 4 en cliente + 6 en page).
- [x] `dotnet build SGV.slnx` pasa.
- [x] Suite focalizada del PR en verde (`Cargo|Cargos`: 240/240).
- [x] Suite web en verde (98/98).
- [x] Pipeline frontend (`bun run build`) OK.
- [ ] Suite completa `dotnet test SGV.slnx` en verde — **waiver documentado por issue #59 preexistente**.
- [x] Conventional commits (5 work units + 1 docs).
- [x] Sin `Co-Authored-By` ni atribución a IA.
- [x] Tabla `TDD Cycle Evidence` en `apply-progress.md` (Strict TDD compliant).
- [x] Sidenav actualizado con entry `Nueva` que respeta convención existente.
- [x] PRG (Post/Redirect/Get) implementado en OnPostAsync exitoso.

---

## Commits incluidos (work units)

```
f323c6e1 feat(cargos-web): add Create and GetNiveles to CargoApiClient
390dd37e feat(cargos-web): scaffold Create form shared infrastructure
07fd366b feat(cargos-web): implement Create page for Cargos
6329fbdd feat(cargos-web): expose Crear cargo CTA in Index and Sidenav
318ef646 test(cargos-web): cover Create page, Sidenav Nueva, duplicate conflict
bfdf4dea docs(apply): track PR2A progress (Frontend Create)
```

## Archivos tocados (resumen)

| Capa | Archivos |
|---|---|
| Producción | `ICargoApiClient.cs`, `CargoApiClient.cs`, `CargoInputModel.cs`, `CargoFormHelpers.cs`, `ICargoForm.cs`, `_Form.cshtml`, `Create.cshtml`, `Create.cshtml.cs`, `Index.cshtml`, `_Sidenav.cshtml` (10) |
| Tests | `CargoApiClientTests.cs`, `FakeCargoApiClient.cs`, `CargoCreatePageTests.cs` (3) |
| OpenSpec | `apply-progress.md` (1, sección PR2A) |

**Total**: 14 archivos; **+974 / -5 líneas** (excede budget 400 — `size:exception` solicitada).

---

> Parte del change SDD `2026-06-30-cargos-crear-editar-codigo-editable` (PR1 ya mergeado como #60). PR2B (Edit + Details CTA) viene después; chained stacked-to-main, depende de este PR.