# Propuesta — Cargos: navegación a Habilidades y errores por fila

## Result Contract

- **status**: success
- **executive_summary**: La propuesta acota el change a dos cierres concretos: descubrir la página `Habilidades.cshtml` desde el flujo principal de `Cargos` y corregir el anclaje de errores de edición por fila sin tocar backend ni contrato HTTP. La capacidad afectada es únicamente `cargo-skill-ui-tabla-editable`.
- **artifacts**:
  - `openspec/changes/cargos-navegacion-habilidades/proposal.md`
- **next_recommended**: spec
- **risks**:
  - Drift entre keys de `ModelState` y `name`/contenedores de validación de la grilla.
  - Scope creep hacia `Edit.cshtml` o navegación cruzada desde catálogo de `Habilidades`.
  - Intentar redirigir también en error rompería el patrón actual de `return Page()` para conservar feedback.
- **skill_resolution**: paths-injected — `sdd-propose`, `cognitive-doc-design`, `Razor Pages Patterns`

## Resumen ejecutivo
Este change cierra dos riesgos transferidos del change archivado: W-UX (falta de entry points) y W1 (errores de edición que no quedan anclados a la fila). La página objetivo sigue siendo `Pages/Organizacion/Cargos/Habilidades.cshtml`, accesible desde `Index.cshtml` y `Details.cshtml` del cargo. Se conserva intacto el backend, el contrato HTTP del subrecurso y el patrón Razor/PRG entregado en el change anterior.

## Motivación y problema
Fuente literal del `archive-report.md` del change archivado:

> - [ ] W1 PR3b — errores de validación de `Actualizar` no quedan anclados a la fila editada. Decisión de UX pendiente.
> - [ ] Página `Habilidades.cshtml` no enlazada desde `Index`/`Edit`. Decisión de UX pendiente.
> - [ ] Issue #59 — 12 fallos pre-existentes de `OcupacionRepositoryTests` por bug en migración inicial (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`).

Para el usuario final, el primer problema vuelve invisible una capacidad ya implementada: el administrador no encuentra cómo gestionar habilidades desde el flujo principal de `Cargos`. Para el admin operativo, el segundo problema degrada el feedback de validación: al editar una fila, el error aparece desacoplado del campo que falló y aumenta el riesgo de correcciones erróneas o repetidas.

## Alcance
### Incluido
- Botón/CTA en `Index.cshtml` dentro de la columna **Acciones** de cargos activos hacia `/organizacion/cargos/{id}/habilidades`.
- Botón **Habilidades** en `Details.cshtml` barra inferior hacia el mismo destino.
- Tests web para ambos CTAs en `CargoIndexPageTests` y `CargoDetailsPageTests`.
- Ajuste de `ApplySkillFailureToModelState` con variante específica para `OnPostActualizarAsync` que mapea `FieldErrors` a keys por fila (`Actualizar[{skillId}].Campo`).
- Ajuste del markup de la grilla editable para usar nombres de input por fila y contenedor de error por fila consistente con esa convención.
- Preservación del summary general además del error junto al campo/fila que falló.
- Tests web adicionales en `CargoHabilidadesPageTests` para blindar errores de `Actualizar` anclados a la fila y visibles también en el summary.

### Excluido (Non-Goals)
- NO se modifica `Edit.cshtml` para embeber gestión de habilidades.
- NO se mueve lógica fuera de `Pages/Organizacion/Cargos/Habilidades.cshtml` y su PageModel.
- NO se modifica el contrato HTTP de `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills`.
- NO se rediseña la grilla a SPA/modal/AJAX.
- NO se mezcla esta navegación con la vista `eliminadas` de cargos.
- NO se expone navegación cruzada desde el catálogo de `Habilidades`.

## Estado actual del repo
### Backend existente a respetar
El subrecurso `Cargo↔Habilidad`, sus DTOs enriquecidos, el cliente tipado web y la Razor Page `Pages/Organizacion/Cargos/Habilidades.cshtml` ya existen por el change archivado `implementar-asignar-quitar-habilidades-de-un-cargo`. El backend ya resuelve `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills`, incluyendo validaciones de `Ponderacion` y reglas del vínculo.

### Frontend SGV.Web — gap puntual
Este change NO reescribe esa base: la ENLAZA desde los puntos correctos del módulo `Cargos` y CORRIGE el mapeo de errores de actualización para que el feedback quede donde el usuario lo necesita, sin tocar API, aplicación, persistencia ni contrato.

## Approach propuesto
### Frontend SGV.Web — `Index.cshtml` y `Details.cshtml`
- **Index**: agregar un botón `btn-primary` con icono `ti ti-stars`, ubicado entre **Detalle** y **Editar**, con `aria-label` específico y visible solo cuando `!Model.IsDeletedView`.
- **Details**: agregar un botón `btn btn-primary` con icono `ti ti-stars me-1`, texto **Habilidades**, ubicado entre **Editar** y **Volver**.

### Frontend SGV.Web — `Habilidades.cshtml` / `Habilidades.cshtml.cs`
- Separar el helper de errores para distinguir `Asignar` de `Actualizar`.
- En `OnPostActualizarAsync`, traducir `FieldErrors["Campo"]` a keys estables por fila: `Actualizar[{skillId}].NivelRequeridoId`, `Actualizar[{skillId}].Ponderacion`, `Actualizar[{skillId}].EsObligatoria`.
- Hacer que la grilla editable renderice inputs y contenedores de validación alineados con esa convención, manteniendo además el `validation-summary` general arriba de la página.
- Conservar el comportamiento actual: éxitos por PRG y fallos con `return Page()` para no perder inputs ni mensajes.

### Tests
- `CargoIndexPageTests`: presencia del CTA **Habilidades** por fila activa y ausencia en vista `eliminadas`.
- `CargoDetailsPageTests`: presencia del botón **Habilidades** en la barra inferior del detalle.
- `CargoHabilidadesPageTests`: error de `Actualizar` visible junto al input de la fila correcta y también en el summary general; sin regresión del flujo de éxito/PRG.

## Specs delta esperadas
- `openspec/specs/cargo-skill-ui-tabla-editable/spec.md` — **MODIFIED**: agregar requirement sobre entry points desde `Index` y `Details`, y ajustar/agregar requirement de feedback por fila más summary general para errores de edición.
- `openspec/specs/cargo-skill-asignar-editar/spec.md` — se mantiene vigente sin cambios.
- `openspec/specs/cargo-skill-ponderacion-obligatoria/spec.md` — se mantiene vigente sin cambios.
- `openspec/specs/cargo-skill-query-contract/spec.md` — se mantiene vigente sin cambios.

## Capabilities
### Modified Capabilities
- `cargo-skill-ui-tabla-editable`

## Riesgos y consideraciones
| Riesgo | Impacto | Mitigación |
|---|---|---|
| Drift entre keys de `ModelState` y `name`/contenedor de validación por fila | Alto | Definir una convención única `Actualizar[{skillId}].Campo` y blindarla con tests web. |
| Scope creep hacia `Edit.cshtml` o back-link contextual | Medio | Dejar explícitos los non-goals y limitar la delta spec a `cargo-skill-ui-tabla-editable`. |
| Intentar resolver W1 con redirect en error | Medio | Conservar `return Page()` para fallos y PRG solo para éxito. |
| Cambios innecesarios en backend o migraciones | Bajo | Mantener sin cambios contrato HTTP, DTOs, validaciones y persistencia existentes. |

## Suposiciones explícitas
- `decimal(5,2)` y la regla vigente de `Ponderacion` siguen siendo suficientes para este slice.
- La auditoría actual por interceptor EF Core sigue siendo suficiente; no hace falta ampliar trazabilidad para este ajuste UI.
- Un back-link contextual desde `Habilidades.cshtml` hacia `Cargos` no entra en este change.

## Preguntas abiertas para el usuario
Ninguna crítica para cerrar proposal; pasar a spec/design.

## Rollback Plan
Revertir los cambios en `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` y sus tests web asociados. No hay migración ni cambio contractual, por lo que el rollback es limpio.

## Referencias
- `openspec/changes/cargos-navegacion-habilidades/exploration.md`
- `openspec/specs/cargo-skill-ui-tabla-editable/spec.md`
- `openspec/specs/cargo-skill-asignar-editar/spec.md`
- `openspec/specs/cargo-skill-ponderacion-obligatoria/spec.md`
- `openspec/specs/cargo-skill-query-contract/spec.md`
- `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/proposal.md`
- `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/archive-report.md`
- `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/tasks.md`
- `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/verify-report.md`

## Success Criteria
- [ ] Un administrador ve un botón **Habilidades** por cada cargo activo en el listado y desde su detalle puede navegar a la página de gestión.
- [ ] Cuando el backend devuelve `FieldErrors` por una edición de fila, el mensaje aparece al lado del input que falló y también en el summary general.
- [ ] La página `Habilidades.cshtml` mantiene su flujo actual: PRG para éxito y `return Page()` para fallos recuperables.
- [ ] `dotnet test SGV.slnx` verde; `bun run build` verde.
- [ ] No se modifica `Edit.cshtml` ni el contrato HTTP del subrecurso.

## Result Contract

- **status**: success
- **executive_summary**: La propuesta acota el change a dos cierres concretos: descubrir la página `Habilidades.cshtml` desde el flujo principal de `Cargos` y corregir el anclaje de errores de edición por fila sin tocar backend ni contrato HTTP. La capacidad afectada es únicamente `cargo-skill-ui-tabla-editable`.
- **artifacts**:
  - `openspec/changes/cargos-navegacion-habilidades/proposal.md`
- **next_recommended**: spec
- **risks**:
  - Drift entre keys de `ModelState` y `name`/contenedores de validación de la grilla.
  - Scope creep hacia `Edit.cshtml` o navegación cruzada desde catálogo de `Habilidades`.
  - Intentar redirigir también en error rompería el patrón actual de `return Page()` para conservar feedback.
- **skill_resolution**: paths-injected — `sdd-propose`, `cognitive-doc-design`, `Razor Pages Patterns`
