# Propuesta — Implementar asignar/quitar Habilidades de un Cargo

## Resumen ejecutivo
El cambio habilita en `SGV.Web` la administración de habilidades requeridas por cargo mediante una página dedicada con tabla editable. El backend ya resuelve la asociativa `CargoHabilidad`; este cambio prioriza consumirlo desde la shell y cerrar el gap funcional para administradores.

También se propone extender el contrato existente del subrecurso para que la UI pueda editar los cuatro campos acordados del vínculo: `Habilidad`, `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`. El valor de negocio es completar el CRUD operativo del cargo sin duplicar modelado ya implementado.

## Motivación y problema
Hoy un administrador puede gestionar el catálogo de `Cargos`, pero no sus habilidades asociadas desde la UI. Eso obliga a dejar una capacidad de negocio existente solo en API, con fricción operativa y riesgo de drift entre shell web y dominio.

## Alcance
### Incluido
- Nueva Razor Page dedicada para administrar `/cargos/{id}/habilidades` con tabla editable y PRG.
- Extensión mínima del backend existente para exponer/aceptar `Ponderacion` y `EsObligatoria`.
- Extensión de `ICargoApiClient`/`CargoApiClient` para `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills`.
- Ajuste de pruebas existentes de aplicación, persistencia, API y nuevas pruebas web del flujo.

### Excluido (Non-Goals)
- Reimplementar dominio, repositorio o endpoints ya existentes del subrecurso.
- Mover `Nivel` al catálogo `Habilidad` o introducir `Habilidad.NivelId`.
- Agregar soft delete a `CargoHabilidad` o rediseñar auditoría.
- Incorporar edición de skills dentro de `Edit.cshtml` o ampliar `Details.cshtml` en este corte.

## Estado actual del repo
### Backend existente a respetar
`CargoHabilidad`, `CargoSkillServicio`, `CargosController` y sus tests ya existen. El subrecurso actual usa `PUT` idempotente y borrado físico; la asociativa ya persiste `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`.

### Frontend SGV.Web — gap
`SGV.Web` no tiene cliente tipado ni página para el subrecurso; el módulo `Cargos` solo administra datos maestros.

## Approach propuesto
### Backend (extensión mínima)
- Extender DTOs/request de `CargoSkill` para incluir `Ponderacion` y `EsObligatoria`.
- Extender el servicio actual de upsert; no reemplazarlo.
- Ajustar `cargo-skill-query-contract` si el DTO de lectura debe alinear ids + objetos anidados + campos del vínculo.

### Frontend SGV.Web
- Nueva página `Pages/Organizacion/Cargos/Habilidades.cshtml` con tabla editable, validaciones y acciones `Guardar/Quitar`.
- Mantener `[Authorize]` y rol `Administrador` para write.

## Specs delta esperadas
- `specs/cargo-skill-asignar-editar/spec.md`
- `specs/cargo-skill-ponderacion-obligatoria/spec.md`
- `specs/cargo-skill-ui-tabla-editable/spec.md`
- `specs/cargo-skill-query-contract/spec.md` *(modificada, si se confirma alineación contractual)*

## Capabilities
### New Capabilities
- `cargo-skill-asignar-editar`: upsert del vínculo con los cuatro campos editables.
- `cargo-skill-ponderacion-obligatoria`: reglas visibles/validables de `Ponderacion` y `EsObligatoria`.
- `cargo-skill-ui-tabla-editable`: administración web dedicada del subrecurso.

### Modified Capabilities
- `cargo-skill-query-contract`: alinear el GET con el shape real requerido por la UI.

## Riesgos y consideraciones
| Riesgo | Prob. | Mitigación |
|---|---|---|
| Drift modelando nivel en `Habilidad` | Media | Mantener `NivelRequeridoId` en `CargoHabilidad` |
| Scope creep por “rehacer backend” | Media | Extender solo contratos/servicio necesarios |
| Ajuste de contrato > 400 líneas | Alta | Considerar chained PRs en `sdd-tasks` |
| Migración innecesaria | Baja | Asumir `decimal(5,2)` y constraints actuales salvo evidencia contraria |

## Suposiciones explícitas
- `decimal(5,2)` y el check de `Ponderacion` siguen siendo válidos.
- La auditoría vía interceptor actual es suficiente para la asociativa.
- La vista readonly en `Details` no entra en este cambio.

## Preguntas abiertas para el usuario
- Ninguna crítica para cerrar proposal; el siguiente paso puede profundizar contratos y reglas de validación.

## Rollback Plan
Revertir la nueva página web y la extensión contractual de `CargoSkill`, preservando el backend existente base del subrecurso.

## Referencias
- `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/exploration.md`
- `docs/decisiones-implementacion.md`
- `openspec/specs/cargo-skill-query-contract/spec.md`
- `openspec/specs/cargo-web-crear-editar/spec.md`

## Success Criteria
- [ ] Un administrador puede listar, asignar, actualizar y quitar habilidades de un cargo desde `SGV.Web`.
- [ ] La UI edita `Habilidad`, `NivelRequeridoId`, `Ponderacion` y `EsObligatoria` sin drift con dominio/DB.
- [ ] El cambio reutiliza el backend existente con extensión mínima y cobertura de pruebas alineada.
