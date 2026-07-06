# Proposal: Implementar el módulo de Puestos en el Frontend

## Resumen ejecutivo

Slice frontend-only sobre `SGV.Web` que cierra la paridad operativa del
módulo **Puestos** con Cargos: sidenav, listado segmentado, baja lógica,
reactivación, Create, Edit y Details. Backend ya entregado y archivado
(`archive/2026-06-19-implementa-modulo-puestos/`, PR #24, `e965475b`); este
change **no** toca Dominio / Aplicación / Infraestructura / Api. Tres PRs
chained (seams+shell / listado+baja+reactivate / create+edit+details),
~890 líneas, excede el budget de 400 → chained obligatorio. Cinco decisiones
de producto locked del round previo se reflejan tal cual.

## Motivación / problema

Backend cerrado pero **no operable** desde `SGV.Web`: sin navegación, sin
listado, sin alta/edición/baja UI. Administradores no pueden crear, consultar,
editar, eliminar ni reactivar Puestos desde la shell web → backoffice paralelo
o salto del flujo. Cargos resolvió la misma brecha con
`2026-06-30-implementar-modulo-de-cargos-en-el-frontend`. Reproducir el patrón
para Puestos entrega el módulo de punta a punta con costo conocido.

## Estado actual

| Capa | Estado | Fuente |
|------|--------|--------|
| Dominio / Aplicación / Infraestructura / Api | Listo, archivado | `archive/2026-06-19-implementa-modulo-puestos/` |
| Tests backend (`Puesto*Tests`, `*Validator*Tests`, `*Servicio*Tests`, `*Repository*Tests`, `PuestosControllerTests`) | Listos | Idem. `PuestosController` no tiene `[Authorize]`. |
| Frontend `SGV.Web/Pages/Organizacion/Puestos/` | **No existe** (0 hits "puesto" en `src/SGV.Web`) | — |
| `SGV.Web/Integration/Organizacion/Puestos*` | **No existe** | — |
| `_Sidenav.cshtml` entry Puestos | **No existe** | — |
| `tests/SGV.Tests/Web/Puesto/` | **No existe** | — |

**Brecha backend (follow-up, no en este change):** `[Authorize(Roles =
Administrador)]` en `PuestosController` y `GET /api/v1/puestos/consulta?
status=activas|eliminadas` — análogos a `2026-07-01-cargos-crear-autorizacion-admin`
y `2026-07-02-cargos-filtro-activos-eliminados`.

## Resultado deseado

Un usuario autenticado con rol `Administrador` accede a `Organización →
Puestos → Listado` y ve tabla plana de Puestos activos, abre detalle readonly,
edita Nombre/Descripción/Puesto Superior, crea un Puesto eligiendo UO/Cargo/
Puesto Superior desde selects poblados vía API, ejecuta baja lógica
confirmada por SweetAlert2 y reactiva cuando el toggle esté habilitado. En
este slice el toggle se renderiza **deshabilitado** por falta del endpoint
segmentado. Paridad operativa 1:1 con Cargos.

## Alcance del cambio

### In-scope

**Páginas y PageModels** (crear) — `src/SGV.Web/Pages/Organizacion/Puestos/`:
- `Index.cshtml(.cs)` — `[Authorize]`, tabla con columnas `Codigo`, `Nombre`,
  `Unidad Organizativa`, `Cargo`, `Puesto superior` (link o celda vacía),
  acciones `Detalle / Editar / Eliminar`, toggle `Activas|Eliminadas`
  deshabilitado con tooltip, `OnGetAsync`/`OnPostDeleteAsync`/
  `OnPostReactivateAsync`, `TempData` `StatusMessage`/`StatusKind`,
  `LastDeletedId`, `BuildToggleSegmentoRouteValues`.
- `Details.cshtml(.cs)` — readonly, link a Puesto superior si existe, retorno
  preservando `p`/`search`/`sort`/`status`.
- `Create.cshtml(.cs)` — form completo, `UnidadOrganizativaId` y `CargoId`
  por `SelectList` desde `IUnidadOrganizativaApiClient`/`ICargoApiClient`,
  `PuestoSuperiorId?` desde `IPuestosApiClient.GetAllAsync()`.
- `Edit.cshtml(.cs)` — **solo** `Nombre`/`Descripcion?`/`PuestoSuperiorId?`;
  sin `Codigo`, sin UO, sin Cargo.
- `_Form.cshtml` — partial compartido.

**Integración HTTP** (crear) — `src/SGV.Web/Integration/Organizacion/`:
- `IPuestosApiClient.cs` — `GetAllAsync`/`GetByIdAsync`/`CreateAsync`/
  `UpdateAsync`/`DeleteAsync`/`ReactivateAsync`.
- `PuestosApiClient.cs` — mapea `ProblemDetails`/`ValidationProblemDetails`
  a `PuestoCommandResult` (espejo `CargoApiClient.ToCommandResultAsync`).
- `PuestoListItemViewModel.cs` — record de grilla
  `(Id, Codigo, Nombre, Descripcion?, UnidadOrganizativaNombre,
  CargoNombre, PuestoSuperiorId)`.

**Composición / shell** (modificar):
- `src/SGV.Web/Program.cs` — registrar `IPuestosApiClient` con
  `ApiBearerTokenHandler` y `Timeout=10s`.
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` — entry colapsable
  "Puestos" con sub-items `Listado` y `Nuevo`, highlight en
  `/organizacion/puestos(/...)`.

**Asset JS** (crear) — `src/SGV.Web/wwwroot/js/pages/puestos-index.js`:
`wirePuestoDeleteConfirmation` + `wirePuestoReactivateConfirmation`
(SweetAlert2, `reverseButtons`, español).

**Tests** (crear) — `tests/SGV.Tests/Web/Puesto/`:
`PuestoWebTestFixture`, `FakePuestosApiClient`, `PuestosApiClientTests`,
`IPuestosApiClientContractTests`, `PuestoWebSeamTests`,
`PuestoIndexPageTests`, `PuestoDetailsPageTests`, `PuestoCreatePageTests`,
`PuestoEditPageTests` (con test obligatorio de **ausencia** de `Codigo`/
`UnidadOrganizativaId`/`CargoId` en HTML renderizado de `Edit`).
**Modificar** `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` para
permitir override de `IPuestosApiClient`.

### Out-of-scope / Non-goals (explícito)

- Backend `[Authorize(Roles=Administrador)]` en `PuestosController` →
  follow-up `puestos-crear-autorizacion-admin`.
- Endpoint `GET /api/v1/puestos/consulta?status=...` → follow-up
  `puestos-filtro-activos-eliminados`. Toggle "Eliminadas" se renderiza
  deshabilitado.
- Cambios de Dominio / Aplicación / Infraestructura / Api.
- Edición de `UnidadOrganizativaId` y `CargoId` en Edit (dominio los congela
  post-creación).
- Vista de árbol / organigrama de Puestos (OrgChart reservado para UO).
- i18n más allá del español vigente.
- Export a Excel/PDF.
- Búsqueda server-side full-text (la búsqueda queda en memoria).
- Auditoría visual de cambios en UI (auditoría centralizada ya existe).
- Resolución de `PuestoSuperiorNombre` en el listado (el DTO solo expone
  `PuestoSuperiorId`).

## Decisiones de producto confirmadas (locked)

| # | Tema | Decisión | Razón |
|---|------|----------|-------|
| 1 | Render del listado | Tabla plana (no OrgChart) | Paridad con Cargos. |
| 2 | Toggle "Eliminadas" | Deshabilitado con tooltip "Requiere endpoint backend: pendiente de follow-up" | Cierra el gap visual sin prometer lo que el backend no soporta. |
| 3 | `PuestoSuperiorId` en Create | `SelectList` poblado server-side por `IPuestosApiClient.GetAllAsync()` | Mismo patrón que `Cargos/Create` con UO/Cargo. |
| 4 | Alcance de Edit | Solo `Nombre`/`Descripcion?`/`PuestoSuperiorId?` (espejo exacto de `ActualizarPuestoRequest`) | Restricción de dominio y de contrato; test de ausencia obligatorio. |
| 5 | Sidenav | Entry colapsable "Puestos" con sub-items `Listado` y `Nuevo`, highlight en `/organizacion/puestos(/...)` | Consistencia visual con Cargos/Habilidades. |

## Plan de entrega (chained PRs)

> **Forecast 400-líneas: High.** Cambios estimados ~890 líneas
> (PR 1 ~230, PR 2 ~480, PR 3 ~180). **Chained PRs recommended: Yes.** Cada
> PR cierra con `dotnet build SGV.slnx`, `dotnet test --filter "<slice>"` y
> `bun run build` en verde. `apply-progress.md` mantiene la tabla TDD Cycle
> Evidence (RED→GREEN→REFACTOR) por escenario.

### PR 1 — Seams + shell + navegación (~230 líneas)

- Crear `IPuestosApiClient` + `PuestosApiClient` + `PuestoListItemViewModel`
  + `PuestoDeleteResult`.
- Registrar en `Program.cs` (`Timeout=10s`, `ApiBearerTokenHandler`).
- Override en `SgvWebApplicationFactory`.
- Entry colapsable "Puestos" en `_Sidenav.cshtml`.
- Tests: `PuestosApiClientTests`, `IPuestosApiClientContractTests`,
  `PuestoWebSeamTests`, `PuestoWebTestFixture`, `FakePuestosApiClient`,
  test de sidenav (visibilidad + sin placeholders ajenos).

### PR 2 — Listado + baja lógica + reactivación (~480 líneas)

- `Index.cshtml(.cs)` con tabla plana, columnas locked, toggle deshabilitado
  con tooltip, búsqueda/orden/paginación en memoria, `OnPostDeleteAsync`,
  `OnPostReactivateAsync`, `TempData`, `LastDeletedId`,
  `BuildToggleSegmentoRouteValues`.
- `puestos-index.js` con confirmaciones SweetAlert2 + harness JS.
- Tests: `PuestoIndexPageTests` (render activo, toggle deshabilitado visible,
  búsqueda con/sin resultados, error visible, POST Delete éxito/409/404,
  POST Reactivate éxito/409, preservación de contexto).

### PR 3 — Create + Edit + Details (~180 líneas)

- `_Form.cshtml` (partial compartido), `Create.cshtml(.cs)`, `Edit.cshtml(.cs)`
  (sin `Codigo`, sin UO, sin Cargo), `Details.cshtml(.cs)`.
- Tests: `PuestoCreatePageTests`, `PuestoEditPageTests` (con test obligatorio
  de **ausencia** de `Codigo`/`UnidadOrganizativaId`/`CargoId` en HTML),
  `PuestoDetailsPageTests`.

### Cierre del slice

- `dotnet test SGV.slnx` verde; `bun run build` verde.
- `apply-progress.md` con TDD Cycle Evidence completa.
- `verify-report.md` PASS sin CRITICAL.
- Sync delta specs a `openspec/specs/**` y archive del change.

## Estrategia de branching

`delivery_strategy = ask-always`; el orquestador confirmará al lanzar
`sdd-apply`. Dos opciones:
- **Stacked-to-main**: rama larga con tres commits apilados mergeados con
  `--no-ff`. Pros: una sola PR final; Contras: revisión atómica > 400 líneas.
- **Feature-branch-chain** (recomendado): PR 1 → PR 2 contra PR 1 → PR 3
  contra PR 2 → merge final a `main`. Pros: cada PR respeta el budget de
  400; Contras: más overhead de rebase.

## Riesgos y mitigaciones

| Riesgo | Likelihood | Mitigación |
|--------|------------|------------|
| Backend sin `[Authorize]`: mutaciones anónimas posibles | Med | `ApiBearerTokenHandler` propaga JWT cuando existe; documentado como follow-up `puestos-crear-autorizacion-admin`. UI asume cookie auth como Cargos. |
| Toggle "Eliminadas" sin endpoint segmentado: renderizar activo mostraría listado vacío engañoso | Alta | Toggle con `disabled` + tooltip. Test RED en `PuestoIndexPageTests` afirma visibilidad del estado deshabilitado. `IndexModel` acepta `?status=activas|eliminadas` para forward-compat. |
| `Edit` pretende editar `Codigo`/`UnidadOrganizativaId`/`CargoId` y deriva en tests rojos | Med | Alcance explícito; test obligatorio en `PuestoEditPageTests` que afirma ausencia de `name="codigo"`, `name="unidadOrganizativaId"`, `name="cargoId"` en HTML. `_Form.cshtml` recibe flag `IsEdit`. |
| Drift entre keys de `ModelState` (camelCase) y nombres de input | Med | `_Form.cshtml` usa `asp-for` con nombres del DTO; tests RED mockean `ValidationProblemDetails` y verifican que el error cae junto al input. |
| `PATCH /reactivar` responde 409 si `Codigo` ya está ocupado | Med | `PuestosApiClient.ReactivateAsync` mapea 409 a `PuestoCommandResult.Failure(Code="CodigoDuplicado")`; `OnPostReactivateAsync` lo traduce a `TempData` danger. Test RED cubre 409. |
| `bun run build` introduce regresión en bundle Inspinia | Baja | Sidebar reusa `side-nav-item`/`side-nav-link`; sin SCSS propio. Validar en PR 1. |
| Bug pre-existente #59 `OcupacionRepositoryTests` | Baja (no relacionado) | `MySqlFact` desconectados; tests nuevos usan `WebApplicationFactory` + fake client. |

## Dependencias externas

Ninguna. Reuso de `SweetAlert2` (ya en `package.json`) y
`FluentValidation` (ya propagado vía `ValidationProblemDetails`).

## Criterios de aceptación / Definition of Done

- [ ] Sidenav expone `Organización → Puestos → Listado / Nuevo` con highlight
  correcto.
- [ ] Listado muestra columnas `Codigo`, `Nombre`, `Unidad Organizativa`,
  `Cargo`, `Puesto superior` y acciones `Detalle / Editar / Eliminar`.
- [ ] Toggle "Eliminadas" deshabilitado visible con tooltip.
- [ ] Detalle readonly, retorno al listado preservando contexto.
- [ ] Baja lógica: confirmación SweetAlert2, feedback éxito/404/409.
- [ ] Reactivación: feedback éxito/409.
- [ ] `Create` con selects UO/Cargo/Puesto Superior poblados vía API.
- [ ] `Edit` con `Nombre`/`Descripcion?`/`PuestoSuperiorId?`; test afirma
  ausencia de `Codigo`/`UnidadOrganizativaId`/`CargoId` en HTML.
- [ ] 3 PRs cierran con `dotnet build`, `dotnet test` filtrado al slice y
  `bun run build` en verde.
- [ ] `apply-progress.md` documenta TDD Cycle Evidence completa.
- [ ] `verify-report.md` PASS sin CRITICAL; `dotnet test SGV.slnx` verde.

## Próximos pasos (post-archive)

- Follow-up `puestos-crear-autorizacion-admin` →
  `[Authorize(Roles=Administrador)]` en `PuestosController`.
- Follow-up `puestos-filtro-activos-eliminados` →
  `GET /api/v1/puestos/consulta?status=activas|eliminadas` y toggle activo.
- Opcional: `PuestoSuperiorNombre` en `PuestoDto` o endpoint de catálogo.
- Opcional: organigrama de Puestos (paridad con UO) si el producto lo pide.
