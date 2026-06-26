# Proposal: Asociar Habilidades a Cargos y Personas

## Intent

Habilitar la gestión de habilidades asociadas a Cargos y Personas. La base ya contiene `CargoHabilidades`, `PersonaHabilidades` y `NivelesHabilidad`; falta exponer el comportamiento como subrecursos API con reglas de aplicación.

## Scope

### In Scope
- Gestionar habilidades requeridas de un Cargo en `/api/v1/cargos/{cargoId}/skills`.
- Gestionar habilidades de una Persona en `/api/v1/personas/{personaId}/skills`.
- Guardar nivel por asociación: `NivelRequerido` para Cargos y dominio/proficiencia para Personas.
- Eliminar físicamente la asociación al quitar una habilidad.

### Out of Scope
- CRUD de `Habilidades` o `NivelesHabilidad`.
- Cambios al cálculo de compatibilidad o snapshots.
- Autorización, permisos o auditoría fina.

### Non-goals
- No rediseñar tablas de asociación.
- No usar soft-delete para asociaciones.
- No mezclar asignaciones dentro de `/api/v1/skills`.

## Capabilities

### New Capabilities
- `cargo-skill-assignment`: asociación de Habilidades requeridas a Cargos, con nivel requerido y borrado físico.
- `persona-skill-assignment`: asociación de Habilidades a Personas, con nivel de dominio/proficiencia y borrado físico.

### Modified Capabilities
- `cargo-management`: deja de excluir gestión de Habilidades vía subrecurso del Cargo.
- `persona-management`: deja de excluir `PersonaHabilidad` vía subrecurso de la Persona.
- `habilidad-management`: mantiene `/api/v1/skills` como catálogo; asignaciones viven en subrecursos externos.
- `sgv-database`: precisa nivel, unicidad por par y borrado físico de asociaciones.
- `sgv-readonly-api`: documenta los nuevos subrecursos.

## Approach

Reutilizar Clean Architecture: entidades de asociación, servicios de aplicación, repositorios EF Core/Pomelo sobre tablas actuales y controllers anidados. Validar existencia de Cargo/Persona, Habilidad y Nivel antes de persistir.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Dominio/Habilidades` | Modified | Reglas de asociación y nivel. |
| `src/SGV.Aplicacion` | New/Modified | Casos de uso y DTOs. |
| `src/SGV.Infraestructura/Persistencia` | Modified | Repositorios y mapeos EF. |
| `src/SGV.Api/Controllers` | Modified | Endpoints anidados. |
| `tests/SGV.Tests` | Modified | Cobertura por capa. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Confusión catálogo/asociaciones | Medium | Mantener `/api/v1/skills` solo como catálogo. |
| Exceso de revisión | High | PRs encadenados contra `develop`. |
| Nivel inválido | Medium | Validar FK contra `NivelesHabilidad`. |

## Rollback Plan

Revertir controllers, servicios y repositorios agregados. Si aparece una migración correctiva, revertirla con script idempotente.

## Dependencies

- Tablas `CargoHabilidades`, `PersonaHabilidades`, `NivelesHabilidad`.
- Catálogos activos de Cargos, Personas y Habilidades.

## Success Criteria

- [ ] Se pueden listar, agregar/actualizar y quitar habilidades de un Cargo.
- [ ] Se pueden listar, agregar/actualizar y quitar habilidades de una Persona.
- [ ] Cada asociación persiste el nivel correcto.
- [ ] Quitar una asociación elimina físicamente solo esa relación.
