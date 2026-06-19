# Proposal: Módulo administrable de Habilidades

## Intent

Convertir Habilidades de catálogo read-only a catálogo administrable, manteniendo `/api/v1/skills`: crear, actualizar campos editables, desactivar y reactivar sin asignar a cargos o personas.

## Scope

### In Scope
- CRUD administrativo de `Habilidades`.
- Mantener `/api/v1/skills` como ruta canónica.
- Validar `Codigo`, `Nombre`, `Categoria` y `Descripcion`.
- Preservar unicidad activa de `Codigo` en MySQL/Pomelo.
- Seguir patrones de Cargos/Puestos.

### Out of Scope
- Asignar habilidades a cargos (`CargoHabilidad`).
- Registrar habilidades de personas (`PersonaHabilidad`).
- Cambios en compatibilidad, vacantes o selección.
- Autenticación, autorización y UI.

## Non-goals

- No rediseñar `NivelesHabilidad` ni su seed.
- No cambiar la ruta a español ni duplicarla en esta primera porción.
- No eliminar físicamente habilidades.

## Capabilities

### New Capabilities
- `habilidad-management`: gestión administrable del catálogo maestro de Habilidades.

### Modified Capabilities
- `sgv-readonly-api`: permitir escritura para `skills` conservando lectura pública.
- `sgv-database`: explicitar unicidad activa, baja lógica y reactivación.

## Approach

Reutilizar dominio, EF Core y consultas existentes. Agregar comandos, escritura en repositorio, mapeos, requests y pruebas. `Codigo` queda estable: se define al crear y no se edita.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Dominio/Habilidades` | Modified | Ciclo de vida. |
| `src/SGV.Aplicacion/Habilidades` | Modified | Comandos y validadores. |
| `src/SGV.Infraestructura/Persistencia` | Modified | Escritura y mapeos. |
| `src/SGV.Api/Controllers/SkillsController.cs` | Modified | Endpoints en `/api/v1/skills`. |
| `tests/SGV.Tests` | Modified | Pruebas por capa. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Contrato read-only actual de `skills` | Med | Delta spec en `sgv-readonly-api`. |
| Conflictos por `Codigo` al reactivar | Med | Validar unicidad activa. |
| Referencias desde cargos/personas | Med | Excluir asignaciones. |
| Presupuesto de revisión | Med | Rama nueva y evaluar PRs encadenados. |

## Open Questions / Assumptions

- `Codigo` será inmutable tras la creación.
- Desactivar una habilidad no elimina ni modifica relaciones existentes.
- Pendiente definir permisos cuando exista módulo de seguridad.

## Rollback Plan

Revertir endpoints de escritura, comandos, repositorio y delta specs. Si hay migración futura, revertirla con script idempotente validado en MySQL.

## Dependencies

- Base existente de `Habilidades`, `NivelesHabilidad`, EF Core/Pomelo y `/api/v1/skills`.
- Rama nueva para fases futuras de documentación e implementación.

## Success Criteria

- [ ] `skills` mantiene lectura pública y suma CRUD administrativo documentado.
- [ ] No se implementan asignaciones a cargos/personas.
- [ ] Las reglas de unicidad activa y baja/reactivación quedan cubiertas por specs y pruebas.
- [ ] El diseño futuro preserva Clean Architecture y compatibilidad MySQL.
