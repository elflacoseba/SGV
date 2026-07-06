# Archive Report: Implementar el módulo de Puestos en el Frontend

**Change slug**: `implementa-modulo-puestos-en-frontend`
**Archived at**: 2026-07-06
**Archived to**: `openspec/changes/archive/2026-07-06-implementa-modulo-puestos-en-frontend/`
**Mode**: openspec

## Verdict

**PASS** — el ciclo SDD se cierra en verde. El cambio implementó el slice frontend-only completo del módulo `Puestos` en `SGV.Web` (listado plano con toggle `Eliminadas` deshabilitado, baja lógica confirmada, reactivación con feedback, detalle readonly, y flujos de create/edit) con paridad operativa respecto de `Cargos`. Los 5 PRs encadenados (`#89` seams+shell · `#91` listado+baja+reactivate · `#92` create · `#93` edit · `#94` details) están mergeados en `develop` con `021a6565` como HEAD. El slice Puestos pasa **100/100 tests** y la suite web completa **406/406** sin regresión. `verify-report.md` no reporta issues CRITICAL ni WARNING; solo 3 SUGGESTIONS no bloqueantes.

## Tarea Complete Gate

- [x] 22/22 tareas de implementación en `tasks.md` marcadas completas (PR 1: 1.1–1.8 · PR 2: 2.1–2.4 · PR 3A: 3A.1–3A.4 · PR 3B: 3B.1–3B.3 · PR 3C: 3C.1–3C.3).
- [x] Build: `dotnet build SGV.slnx` → 0 warnings, 0 errors.
- [x] Frontend pipeline: `bun run build` en `src/SGV.Web` → verde en 3.58 s.
- [x] Tests del slice Puestos: `FullyQualifiedName~SGV.Tests.Web.Puesto` → **100/100 PASS** (0 skipped, 0 failed).
- [x] Suite web completa: `FullyQualifiedName~SGV.Tests.Web` → **406/406 PASS** sin regresión.
- [x] Token check: `>Crear<` ausente en `Edit.cshtml*`; `>Crear<`/`>Reactivar<` ausentes en `Details.cshtml*` (`git grep` → 0 hits). Test RED obligatorio `Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` presente y verde con triangulación negativa + positiva.
- [x] Sin issues CRITICAL en `verify-report.md` (verdict `PASS`; 23/23 SHAs de `apply-progress` validados contra `git log`).

> **Nota sobre §9 (Definition of Done)**: los checkboxes del meta-checklist "Definition of Done" en `tasks.md §9` quedaron sin marcar, pero **no son tareas de implementación**: son ítems de gate de fases (build/test/verify/archive) cuyo último ítem es precisamente esta sincronización + archive. No se realizó reconciliación mecánica de esos checkboxes porque la regla dura del orquestador prohíbe modificar los artefactos del change folder fuera del `git mv`. Todas las tareas de implementación reales (secciones 3, 4 y 5 → 1.1 a 3C.3) están `[x]`, y `verify-report.md` prueba que los ítems de §9 están completos (build 0/0, 100/100 slice, 406/406 suite, 5 PRs mergeados, `apply-progress` completo, verdict PASS).

## Sincronización de delta specs

| Capability | Acción | Detalle |
|------------|--------|---------|
| `puesto-web-listado-detalle-baja` | **CREADA** en canonical | Spec completa nueva: 6 requirements y 11 escenarios (acceso autenticado, listado plano con toggle `Eliminadas` deshabilitado, baja lógica confirmada con feedback de conflicto, reactivación por `LastDeletedId`, detalle readonly con retorno preservando contexto, entry colapsable en sidenav). No existía canonical previo; la delta era una spec completa, copiada 1:1 a `openspec/specs/puesto-web-listado-detalle-baja/spec.md` (85 líneas, byte-idéntica). |
| `puesto-web-crear-editar` | **CREADA** en canonical | Spec completa nueva: 8 requirements y 13 escenarios (acceso autenticado a create/edit, create con los 6 campos, `PuestoSuperiorId` con select N+1, edit estricto de 3 campos con ausencia de `Codigo`/`UnidadOrganizativaId`/`CargoId`, `_Form.cshtml` compartido, guardado con PRG + feedback, submenú `Nuevo`). Copiada 1:1 a `openspec/specs/puesto-web-crear-editar/spec.md` (104 líneas, byte-idéntica). |
| `sgv-web-shell` | **MODIFICADA** en canonical | El requirement `Minimal technical navigation` se reemplazó in-place por la versión del delta: ahora expone `Unidades Organizativas`, `Cargos`, `Habilidades` **y `Puestos`** como módulos funcionales habilitados; `Puestos` se renderiza dentro del grupo `Organización` como entry colapsable con icono `ti ti-hierarchy` y submenú `Listado` + `Nuevo`. Se reemplazaron sus escenarios por "Navegación mínima con Puestos habilitado" y "Submenú de Puestos visible y activo", conservando "Otros módulos siguen fuera de alcance". Nota `(Previously: …)` actualizada. Los otros 5 requirements (`Functional base shell`, `Demo content removal`, `Neutral branding and Inspinia visual system`, `No authentication dependency`, `Frontend validation expectations`) se preservan intactos. |
| `web-apiclient-transport-contract` | **MODIFICADA** (ADDED) en canonical | Se agregaron 3 requirements client-specific para `IPuestosApiClient` al final de `## Requirements` (propagación de fallos nativos, cancelación cooperativa pre-cancelada, traducción de `ProblemDetails` a `PuestoCommandResult`/`PuestoDeleteResult`), con sus 5 escenarios. Los 2 requirements transversales previos (`Propagar fallos nativos de transporte`, `Respetar cancelación cooperativa del consumidor`) se preservan sin cambios. El marcador delta `## ADDED Requirements` NO se copió al canonical: los bloques se integraron como `### Requirement:` planos bajo `## Requirements` (convención canónica). |

### Diff stats (delta → canonical)

| Archivo | Líneas antes | Líneas después | Δ |
|---------|--------------|----------------|---|
| `openspec/specs/puesto-web-listado-detalle-baja/spec.md` | inexistente | 85 | +85 |
| `openspec/specs/puesto-web-crear-editar/spec.md` | inexistente | 104 | +104 |
| `openspec/specs/sgv-web-shell/spec.md` | 124 | 125 | +1 (replace in-place del requirement `Minimal technical navigation`) |
| `openspec/specs/web-apiclient-transport-contract/spec.md` | 35 | 87 | +52 (3 requirements ADDED con 5 escenarios) |

## Contenido del archivo

| Artefacto | Estado |
|-----------|--------|
| `proposal.md` | ✅ Preservado |
| `exploration.md` | ✅ Preservado |
| `design.md` | ✅ Preservado |
| `specs/puesto-web-listado-detalle-baja/spec.md` | ✅ Preservado (delta sincronizado a canonical) |
| `specs/puesto-web-crear-editar/spec.md` | ✅ Preservado (delta sincronizado a canonical) |
| `specs/sgv-web-shell/spec.md` | ✅ Preservado (delta sincronizado a canonical) |
| `specs/web-apiclient-transport-contract/spec.md` | ✅ Preservado (delta sincronizado a canonical) |
| `tasks.md` | ✅ Preservado (22/22 tareas de implementación completas) |
| `apply-progress.md` | ✅ Preservado (5 tablas Cycle Evidence RED→GREEN→REFACTOR con SHAs reales) |
| `verify-report.md` | ✅ Preservado (verdict PASS, 33/33 escenarios COMPLIANT) |
| `archive-report.md` | ✅ Este documento |

## Source of truth actualizado

Los siguientes specs canónicos reflejan el comportamiento implementado:

- `openspec/specs/puesto-web-listado-detalle-baja/spec.md` — **nueva spec** del módulo web de puestos: listado, detalle y baja (6 requirements, 11 escenarios).
- `openspec/specs/puesto-web-crear-editar/spec.md` — **nueva spec** de create/edit web de puestos (8 requirements, 13 escenarios).
- `openspec/specs/sgv-web-shell/spec.md` — requirement `Minimal technical navigation` actualizado para incluir `Puestos` como cuarto módulo funcional con entry colapsable `ti ti-hierarchy`.
- `openspec/specs/web-apiclient-transport-contract/spec.md` — 3 requirements nuevos para `IPuestosApiClient` (transporte nativo, cancelación cooperativa, traducción de `ProblemDetails`).

## Cycle Evidence Total

| Métrica | Valor |
|---------|-------|
| Escenarios spec cubiertos | 33/33 COMPLIANT (100%) · 0 UNTESTED · 0 PARTIAL · 0 FAILING |
| Tests slice Puestos | 100/100 PASS (84 C# + 4 JS harness + 12 Theory rows) |
| Suite web completa | 406/406 PASS (sin regresión) |
| Archivos de test | 9 (`PuestoIndexPageTests`, `PuestoCreatePageTests`, `PuestoEditPageTests`, `PuestoDetailsPageTests`, `PuestoFormHelpersTests`, `PuestoPostResultMapperTests`, `PuestosApiClientTests`, `IPuestosApiClientContractTests`, `PuestoWebSeamTests`) |
| PRs mergeados | 5 encadenados: `#89` (seams+shell) · `#91` (listado+baja+reactivate) · `#92` (create) · `#93` (edit) · `#94` (details) |
| Commits con SHA real | 23/23 validados contra `git log` (ninguno placeholder) |
| HEAD `develop` | `021a6565` |
| Build | `dotnet build SGV.slnx` → 0 warnings / 0 errors |
| Frontend | `bun run build` → verde (3.58 s) |
| Test RED obligatorio | `Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` presente y PASS (triangulación negativa ×3 + positiva ×3) |

## Lecciones aprendidas

- **Paridad de seam con Cargos acelera el slice**: reutilizar el patrón probado de `Cargos` (fake con respuestas programadas + captura, JS duplicado por página, toggle deshabilitado con atributo HTML, inspección HTML con regex) permitió cubrir 33 escenarios sin inventar infraestructura nueva. La delta de `web-apiclient-transport-contract` reafirma el contrato transversal ya establecido.
- **El forecast de líneas del design quedó 5–6× por debajo de la realidad** (design §10 ~890 vs. ~4470 reales). El NIT 1 del `tasks.md` corrigió esto de forma honesta y forzó dividir PR 3 en 3A/3B/3C (`feature-branch-chain` de 5 PRs). Lección: derivar el forecast de `git diff --stat` de módulos análogos ya archivados, no de conteos manuales de líneas del design.
- **`IUnidadOrganizativaApiClient` no expone `GetAllAsync()`**: el catálogo de UO para el select de `PuestoSuperiorId` terminó usando `QueryAsync(... pageSize=200 ...)`. Es un workaround aceptado y documentado, pero deja un límite silencioso si el catálogo supera 200 unidades activas (ver SUGGESTION-1).
- **Pre-populate en `Edit.OnPostAsync`**: el `[Required]` heredado de `Codigo/UO/Cargo` en el `InputModel` compartido obliga a un `GetByIdAsync` extra por cada POST de Edit. Trade-off aceptado vs. quitar `[Required]` (rompería Create) o duplicar modelo; la optimización a `<input type="hidden">` queda como follow-up (SUGGESTION-2).
- **Un solo `_Form.cshtml` compartido con `if (!Model.IsEdit)`** cubre Create (6 campos) y Edit (3 campos) sin duplicar markup, y el test RED negativo (`Assert.DoesNotMatch` sobre `name="Input.Codigo"`, `...UnidadOrganizativaId"`, `...CargoId"`) es la guard efectiva contra copy-paste regresivo.

## Próximos pasos (futuro)

- **`puestos-crear-autorizacion-admin`** — aplicar `[Authorize(Roles=Administrador)]` a las operaciones write de `PuestosController` en `SGV.Api` (fuera de alcance de este slice frontend-only).
- **`puestos-filtro-activos-eliminados`** — habilitar el segmento `Eliminadas` (hoy `disabled` con tooltip "Próximamente") cuando exista el endpoint `/api/v1/puestos/consulta?status=activas|eliminadas` en el backend.
- **SUGGESTION-1** — evaluar un endpoint `GET /api/v1/unidades-organizativas/all` o `pageSize` mayor por default para el catálogo de UO del select de `PuestoSuperiorId`, evitando truncado silencioso >200 unidades.
- **SUGGESTION-2** — optimizar el pre-populate de Edit a `<input type="hidden">` para los campos inmutables, eliminando el `GetByIdAsync` extra por POST si se mide regresión de latencia.
- **SUGGESTION-3** — reforzar la guard de tokens añadiendo un `Assert.DoesNotContain(">Crear<", content)` explícito en `PuestoIndexPageTests`.
- **Opcional** — exponer `PuestoSuperiorNombre` en el DTO para evitar resolver el nombre del superior vía catálogo en el detalle.

## SDD Cycle Complete

El cambio fue completamente planificado (`proposal` + `exploration`), especificado (2 specs nuevas + 2 deltas), diseñado (`design` con D1–D9), desglosado en tareas (`tasks` con forecast honesto y chain de 5 PRs), implementado en 5 PRs encadenados mergeados (`apply` con TDD estricto), verificado con TDD estricto + `bun run build` + suite web sin regresión (`verify` PASS 33/33) y archivado (`archive`). El módulo `Puestos` queda disponible en el shell web como cuarto módulo funcional de negocio autenticado, con paridad operativa respecto de `Cargos`. Listo para el próximo cambio.
