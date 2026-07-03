# Proposal: Implementa el módulo de Habilidades en SGV.Web con paridad completa con el módulo Cargos

## Nombre propuesto

- Alternativa más corta: `habilidades-paridad-cargos`.

## Resumen ejecutivo

- `SGV.Web` no tiene módulo de Habilidades.
- El backend de `skills` no expone los contratos que ya usa la UX de `Cargos`.
- Este cambio agrega esa base y deja fuera las asignaciones.

## Contexto y motivación

- La exploración confirmó que faltan páginas web, cliente HTTP, `GET /api/v1/skills/consulta` y `GET /api/v1/niveles-habilidad`.
- `SkillsController` y `HabilidadRepository` hoy quedan limitados a activas por defecto.
- Producto eligió **paridad completa con Cargos**; por eso el cambio requiere backend y web.

## Scope

### In Scope
- Backend: `GET /api/v1/skills/consulta`, lectura de inactivas y `GET /api/v1/niveles-habilidad`.
- Web: `Index/Create/Edit/Details`, `_Form`, PRG, SweetAlert, sidebar e `IHabilidadApiClient`.
- El frontend del catálogo maestro de Habilidades NO muestra ni persiste `NivelHabilidad` porque la entidad `Habilidad` no tiene `NivelId` propio; `GET /api/v1/niveles-habilidad` queda publicado en backend para futuros subrecursos de cargo/persona, pero NO es consumido por este frontend.
- Tests backend primero; MySQL para consulta segmentada.

### Out of Scope / Non-goals
- Asignaciones `habilidad↔cargo` y `habilidad↔persona`.
- Cambios de autorización, nuevos proveedores o migraciones destructivas.

## Capabilities

### New Capabilities
- `habilidad-web-listado-detalle-baja`: listado segmentado, detalle, baja y reactivación.
- `habilidad-web-crear-editar`: create/edit del catálogo maestro sin nivel propio.

### Modified Capabilities
- `habilidad-management`: consulta segmentada, inactivas y catálogo HTTP de niveles.
- `sgv-web-shell`: nueva entrada/sidebar para `Habilidades`.
- `sgv-readonly-api`: discoverability de `skills/consulta` y `niveles-habilidad`.

## Approach

- Capas: API, Aplicación, Infraestructura y Web, reutilizando el patrón de `Cargos`.
- Entrega: **Slice 1 backend + tests**, **Slice 2 cliente/shell**, **Slice 3 Razor/JS + tests**.
- Forecast: **Decision needed before apply: Yes**; **Chained PRs recommended: Yes**; **400-line budget risk: High**.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Api/Controllers/SkillsController.cs` | Modified | Consulta segmentada |
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Consulted | Evidencia de que `Habilidad` no modela `NivelId` propio |
| `src/SGV.Dominio/Habilidades/NivelHabilidad.cs` | Consulted | Catálogo backend preservado para futuros subrecursos |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modified | Navegación del nuevo módulo |
| `src/SGV.Web/Program.cs` | Modified | Registro del cliente HTTP tipado |
| `tests/SGV.Tests/Api/SkillsControllerTests.cs` | Modified | Cobertura de nuevos endpoints |

## Alternativas consideradas

- **A. Paridad con Cargos (elegida)**: más esfuerzo, menos deuda UX.
- **B. Catálogo mínimo actual**: menos riesgo, pero no cumple la decisión de producto.
- **C. Frontend mockeado**: rápido, pero con drift contractual alto.
- Decisión: el catálogo maestro de Habilidades no modela nivel propio; el nivel es atributo de la asociación con cargo o persona (ver `CargoHabilidad.NivelRequeridoId` y `PersonaHabilidadEntity.NivelHabilidadId`). No copiar el patrón Cargos sin adaptación.

## Plan de entrega

- Slice 1: contratos, repositorio y tests.
- Slice 2: cliente tipado, shell y navegación.
- Slice 3: páginas Razor, JS y pruebas web.
- Rollback por slice: revertir web primero; backend después.

## Rollback Plan

- Revertir web primero y backend después; cualquier índice nuevo debe ir en slice reversible aparte.

## Dependencies

- `openspec/changes/implementar-modulo-habilidades-frontend/exploration.md`
- `openspec/specs/habilidad-management/spec.md`
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`

## Success Criteria

- [ ] `GET /api/v1/skills/consulta` devuelve activas/eliminadas con búsqueda, orden y paginación server-side.
- [ ] `GET /api/v1/niveles-habilidad` publica el catálogo consumible por web.
- [ ] `SGV.Web` expone `Habilidades` con `Index/Create/Edit/Details`, baja y reactivación siguiendo el patrón Cargos.
- [ ] `dotnet test SGV.slnx` protege backend y web sin drift spec/implementación.

## Riesgos

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Drift entre spec e implementación | High | Modificar specs antes de apply |
| Paridad 1:1 sobredimensionada | Medium | Mantener fuera de alcance asignaciones |
| Query `/consulta` lenta en MySQL 8 | Medium | Diseñar filtros/orden compatibles y medir repositorio |
| Regresión en sidebar o navegación | Medium | Reusar patrón probado de `Cargos` |
| Falsa confianza sin MySQL real (`issue #59`) | Medium | Mantener cobertura MySQL para consultas nuevas |

## Preguntas abiertas

- ¿`skills/consulta` mantiene la lectura pública actual de `skills` o luego se alinea con la política de `Cargos`?
- ¿El submenú debe usar `Nuevo` o `Nueva` y copiar exactamente la jerarquía visual de `Cargos`?
