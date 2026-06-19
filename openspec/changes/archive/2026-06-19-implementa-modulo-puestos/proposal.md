# Proposal: Implementar módulo administrable de Puestos

## Intent

Convertir Puestos de recurso de solo lectura a catálogo administrable: crear, editar, consultar, desactivar y reactivar. La persistencia y lectura ya existen; faltan contratos de escritura, validaciones y endpoints de gestión.

## Scope

### In Scope
- Crear Puestos con `codigo` y `nombre` obligatorios.
- Mantener relaciones existentes a Unidad Organizativa, Cargo y `PuestoSuperior` opcional.
- Editar datos permitidos, desactivar por baja lógica y reactivar sin borrado físico.
- Preservar consultas activas y contratos consumer-safe.

### Out of Scope / Non-goals
- Gestión de Ocupaciones o Vacantes.
- Permisos, roles o autorización.
- Reglas complejas de jerarquía/ciclos más allá de aceptar `PuestoSuperiorId` opcional.
- Borrado físico.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `sgv-readonly-api`: Puestos deja de ser read-only y expone acciones administrables; Ocupaciones, Vacantes, Habilidades y Tipos de Unidad Organizativa siguen fuera de escritura.
- `sgv-database`: Puestos Concretos incorpora ciclo de vida, baja lógica/reactivación y unicidad de `Codigo` entre activos con el patrón MySQL existente.

## Approach

Seguir patrones de Cargos y Unidades Organizativas: comandos, validación previa a reglas de repositorio, operaciones write, mapeos explícitos, Unit of Work y endpoints consistentes. Reutilizar `Puestos` y su unicidad activa compatible con MySQL.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `openspec/specs/sgv-readonly-api/spec.md` | Modified | Cambiar contrato para permitir escritura de Puestos. |
| `openspec/specs/sgv-database/spec.md` | Modified | Ampliar reglas de ciclo de vida y unicidad activa de Puestos. |
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Modified | Invariantes mínimas y ciclo de vida. |
| `src/SGV.Aplicacion/Organizacion` | Modified | Comandos y validaciones. |
| `src/SGV.Infraestructura/Persistencia` | Modified | Escritura y mapeos MySQL. |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modified | Endpoints write. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Contradecir la spec read-only vigente | Alta | Modificar explícitamente `sgv-readonly-api`. |
| Acoplar Puestos con Ocupaciones/Vacantes | Media | Declararlos fuera de alcance. |
| Unicidad activa incorrecta en MySQL | Media | Conservar patrón de columna generada ya existente. |

## Rollback Plan

Revertir deltas SDD y comandos/endpoints write. Si hubiera migración, aplicar la inversa o restaurar snapshot; no se requiere borrar datos porque la gestión usa baja lógica.

## Dependencies

- Persistencia actual de Puestos, Unidades Organizativas y Cargos sobre MySQL 8/Pomelo EF Core 9.x.

## Success Criteria

- [ ] Puestos puede crearse y consultarse como catálogo activo.
- [ ] Puestos puede editarse sin gestionar Ocupaciones ni Vacantes.
- [ ] Desactivar Puesto realiza soft-delete y lo excluye de consultas activas.
- [ ] Reactivar Puesto restaura visibilidad si no hay conflicto de `Codigo` activo.
- [ ] La documentación OpenSpec refleja que Puestos ya no es read-only.
