# Proposal: Implementar el módulo de Personas

## Intent

Habilitar la gestión administrativa básica de Personas. Dominio y persistencia ya existen; falta el módulo de Aplicación/API para operar datos propios de `Persona` sin mezclar postulaciones, ocupaciones ni habilidades.

## Scope

### In Scope
- CRUD administrativo de Personas: datos básicos, identificación, contacto y estado.
- Endpoints `api/v1/personas` con DTOs consumer-safe y conflictos claros.
- Repositorio, mapeos y casos de uso según patrones existentes.
- Pruebas por capas según `strict_tdd: true`.

### Out of Scope
- Postulantes, postulaciones y selección.
- Ocupaciones, asignaciones laborales e historial.
- Habilidades, `PersonaHabilidad` y compatibilidad.
- Cambios de esquema salvo brecha verificable.

## Non-goals

- No crear flujos de negocio de vacantes.
- No exponer navegaciones ni entidades internas.

## Capabilities

### New Capabilities
- `persona-management`: gestión administrativa de Personas: crear, consultar, actualizar, desactivar y reactivar, con unicidad activa de `Legajo`, `Email` y documento.

### Modified Capabilities
- `sgv-readonly-api`: agregar Personas como recurso HTTP con operaciones administrativas.
- `sgv-database`: especificar contrato persistido de Personas y traducción de conflictos únicos activos ya modelados para MySQL.

## Approach

Implementar un corte mínimo equivalente a Cargos/Habilidades: DTOs, requests, validadores, resultados tipados, casos de uso, repositorio, mapeos `Persona`/`PersonaEntity` y controlador. Las consultas no deben incluir `Postulantes`, `Ocupaciones` ni `PersonaHabilidad`. Los índices únicos MySQL deben traducirse a errores comprensibles.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Aplicacion/Personas/` | New | Casos de uso, DTOs, requests y validadores. |
| `src/SGV.Infraestructura/Persistencia/` | Modified | Repositorio y mapeos; sin migración prevista. |
| `src/SGV.Api/Controllers/` | New | Controlador `PersonasController`. |
| `tests/SGV.Tests/` | Modified | Cobertura TDD por capas. |
| `openspec/specs/` | Modified/New | Delta specs de capacidades indicadas. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Incluir relaciones fuera de alcance | Medium | DTOs planos y repositorio sin `Include` de relaciones excluidas. |
| Conflictos crudos de MySQL | Medium | Traducir excepciones únicas a conflictos de API. |
| Superar 400 líneas | Medium | Planificar slices y PRs encadenados si aplica. |

## Rollback Plan

Revertir archivos del módulo Personas. Sin migraciones no hay rollback de base de datos; si diseño exige una migración, deberá incluir reversión explícita.

## Dependencies

- Tabla `Personas`, índices únicos activos y `PersonaEntity` existentes.
- Patrones actuales de Cargos/Habilidades para consistencia de arquitectura.

## Success Criteria

- [ ] Personas puede crearse, consultarse, actualizarse, desactivarse y reactivarse sin exponer relaciones excluidas.
- [ ] Duplicados activos de `Legajo`, `Email` o documento se rechazan con errores claros.
- [ ] Specs y documentación SDD quedan en español profesional.
- [ ] `dotnet build` y `dotnet test` pasan.
