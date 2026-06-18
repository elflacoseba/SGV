# Proposal: Implementar módulo de Cargos

## Intent

Convertir `Cargos` de recurso consultivo a catálogo maestro gestionado, con identificador estable, baja lógica y reactivación, sin acoplar Habilidades ni Puestos.

## Scope

### In Scope
- Crear, consultar, actualizar campos editables, desactivar y reactivar `Cargo`.
- `Codigo` único y estable: se define al crear y no cambia libremente.
- Baja lógica: eliminar inactiva; reactivar conserva el mismo `Codigo`.
- `Nivel` normalizado como entidad/tabla referenciada por `NivelId`.

### Out of Scope / Non-goals
- Gestión de Habilidades, Puestos, personas, asignaciones o compatibilidad.
- Reglas sobre puestos que consuman cargos, salvo límites defensivos.
- Borrado físico o reutilización libre de `Codigo` creando otro cargo.

## Capabilities

### New Capabilities
- `cargo-management`: gestión del catálogo maestro, ciclo de vida, validaciones y API.
- `nivel-cargo-catalog`: niveles usados por cargos mediante `NivelId`.

### Modified Capabilities
- `sgv-readonly-api`: cargos dejan de ser read-only; los demás catálogos siguen sin escrituras.
- `sgv-database`: cargos mantienen identidad reutilizable, baja lógica y referencia normalizada a nivel.
- `sgv-persistence-architecture`: registrar evolución intencional de esquema/contratos.

## Approach

Seguir Clean Architecture: invariantes en Dominio, casos de uso en Aplicación, EF Core/Pomelo en Infraestructura y contratos consumer-safe en API. Mantener consultas existentes y agregar comandos. MySQL conserva unicidad activa de `Codigo`; `NivelId` será FK indexada.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Dominio/Organizacion` | Modified/New | `Cargo`, `Nivel` e invariantes de ciclo de vida. |
| `src/SGV.Aplicacion/Organizacion` | Modified/New | Comandos, DTOs, validaciones y errores. |
| `src/SGV.Infraestructura/Persistencia` | Modified/New | EF, MySQL, repositorios, migración y seed si aplica. |
| `src/SGV.Api/Controllers/CargosController.cs` | Modified | Endpoints de gestión y errores. |
| `tests/SGV.Tests` | Modified/New | Pruebas de dominio, aplicación, API y persistencia. |
| `openspec/specs/sgv-readonly-api/spec.md` | Modified | Resolver conflicto: cargos salen de la restricción read-only. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Conflicto con spec read-only actual | High | Delta explícito para cargos solamente. |
| `Nivel` actual como primitivo requiere migración | Medium | Diseño con backfill/seed validable y FK. |
| Referencias desde Puestos | Medium | Definir defensa; no gestionar puestos ahora. |

## Rollback Plan

Revertir endpoints/servicios nuevos y aplicar migración inversa que restaure `Cargos.Nivel` si la normalización fue desplegada. La baja lógica evita pérdida irreversible.

## Dependencies

- MySQL 8 con Pomelo EF Core 9.x.
- Decidir catálogo inicial y orden de `Nivel`.

## Success Criteria

- [ ] Cargos se pueden crear, leer, actualizar, desactivar y reactivar sin borrar físicamente.
- [ ] `Codigo` no cambia libremente y no se duplica en cargos activos/reactivados.
- [ ] `NivelId` referencia una fila válida de `Nivel`.
- [ ] Habilidades y Puestos quedan fuera de gestión funcional.
