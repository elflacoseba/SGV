# Proposal: habilidades-navegacion-cargos

## Resumen ejecutivo

Falta el espejo **Habilidad → Cargos** del flujo **Cargo → Habilidades**. La propuesta agrega solo lectura y navegación: subrecurso API, cliente tipado, página Razor readonly y CTA en `Habilidades/Index`, sin tocar dominio ni persistencia (`openspec/changes/habilidades-navegacion-cargos/exploration.md:3-23,47-59`).

## Motivación

Hoy el usuario puede descubrir habilidades desde un cargo, pero no cargos desde una habilidad; el gap existe en API, integración web y UI (`openspec/changes/habilidades-navegacion-cargos/exploration.md:15-21,47-57`). Conviene cerrarlo ahora porque el patrón espejo y la segmentación `status=activas|eliminadas` ya están validados (`openspec/changes/habilidades-navegacion-cargos/exploration.md:41-45,59-61`).

## Cambio propuesto

- **API**: agregar `GET /api/v1/skills/{skillId}/cargos` con DTO readonly específico; seguir patrón de subrecurso enriquecido sin contaminar contratos padre (`openspec/changes/habilidades-navegacion-cargos/exploration.md:25-31,47-49`; `openspec/specs/cargo-skill-query-contract/spec.md:9-18,37-46`).
- **Web**: agregar `IHabilidadApiClient.GetCargosAsync(...)` y `Pages/Organizacion/Habilidades/Cargos.cshtml(.cs)` readonly con toggle `activas|eliminadas` (`openspec/changes/habilidades-navegacion-cargos/exploration.md:41-43,51-57`).
- **Acciones por fila**: `Cargo/Details` para cualquier autenticado; `Cargos/Habilidades` solo para `Administrador` (`openspec/changes/habilidades-navegacion-cargos/exploration.md:43-45,57-58,63-66`).
- **Entry point**: agregar botón `Cargos` solo en filas activas de `Habilidades/Index`; no tocar `Habilidades/Details` (`openspec/changes/habilidades-navegacion-cargos/exploration.md:19-21,53-55,69-71`).

## Capacidades OpenSpec impactadas

- **MODIFIED** `habilidad-web-listado-detalle-baja`: declarar CTA `Cargos` en activas y mantener fuera de alcance la edición del vínculo (`openspec/specs/habilidad-web-listado-detalle-baja/spec.md:50-66,92-97`).
- **MODIFIED** `habilidad-management`: declarar `GET /api/v1/skills/{skillId}/cargos` y preservar fuera de alcance los writes del vínculo (`openspec/specs/habilidad-management/spec.md:5-6,143-152,171-192`).
- **ADDED** `skill-cargo-query-contract`: contrato readonly skill→cargos análogo a `cargo-skill-query-contract` (`openspec/changes/habilidades-navegacion-cargos/exploration.md:29-31`).

## Decisiones locked

1. Nueva página readonly `Pages/Organizacion/Habilidades/Cargos.cshtml`.
2. CTAs por fila: `Cargo/Details` para todos; `Cargos/Habilidades` solo para `Administrador`.
3. Segmentación `status=activas|eliminadas` consistente con índices actuales.
4. Botón de entrada solo en `Habilidades/Index` activo.
5. La página readonly admite cualquier autenticado.
6. Sin migraciones ni cambios de dominio; solo proyección/read model y subrecurso HTTP.
7. Entrega por PR simple salvo gate posterior `ask-on-risk`.
8. Preflight fijo: `interactive`, artifact store `openspec`, `ask-on-risk`, `review_budget_lines: 400`.

## Asunciones reversibles

- Si la consulta skill→cargos necesita contexto del vínculo, entonces el DTO incluirá datos mínimos de cargo y asociación.
- Si `status` es inválido o falta, entonces caerá a `activas` como el resto del módulo.
- Si la habilidad existe pero no tiene cargos en el segmento, entonces la API responderá colección vacía y la web mostrará estado vacío.
- Si un cargo eliminado sigue asociado a una habilidad, entonces será visible solo en `status=eliminadas`.
- Si el usuario no es `Administrador`, entonces el CTA de gestión no se renderizará.
- Si el naming final cambia, entonces debe conservar simetría con `cargo-skill-query-contract`.

## Riesgos

- **Permisos**: mostrar CTA admin a no-admin produciría navegación a `403` (`openspec/changes/habilidades-navegacion-cargos/exploration.md:63-66`).
- **Drift documental**: si no se actualizan las dos specs existentes, quedará contradicción con sus out-of-scope (`openspec/changes/habilidades-navegacion-cargos/exploration.md:67-68`).
- **Shape contractual**: reciclar `CargoDto` podría dejar sin contexto a la página o contaminar contratos padre (`openspec/changes/habilidades-navegacion-cargos/exploration.md:69-70`).
- **Scope creep**: por simetría podría intentarse tocar `Habilidades/Details` o edición inline, pero está fuera de slice (`openspec/changes/habilidades-navegacion-cargos/exploration.md:71-73`).

## Fuera de alcance

- Migraciones, cambios de tabla o soft-delete en `CargoHabilidad`.
- Writes del vínculo skill↔cargo, edición inline o cambios en `Cargos/Habilidades`.
- Botón en `Habilidades/Details`.
- Restringir toda la página nueva solo a administradores.
- Alterar contratos padre de `SkillsController` o `Cargo`.

## Plan de fases

Explore ✅ → Propose ✅ → **Spec** (3 delta specs) → Design → Tasks → Apply → Verify → Archive. Preflight bloqueado: `ask-on-risk`, `review_budget_lines: 400`.

## Rollback plan

Revertir subrecurso, cliente tipado, página Razor y CTA de `Habilidades/Index`; no hay rollback de datos porque no hay migraciones.

## Success criteria

- [ ] Existe navegación `Habilidad → Cargos` desde `Habilidades/Index` activo.
- [ ] La página nueva respeta `status=activas|eliminadas` y mantiene lectura autenticada.
- [ ] El CTA de gestión solo aparece para `Administrador`.
- [ ] Las specs quedan alineadas: 2 modificadas + 1 nueva.

## Próximo paso recomendado

Ejecutar **`sdd-spec`** para redactar las tres delta specs.
