# Tasks: Asociar Habilidades a Cargos y Personas

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 700-1100 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 contratos/aplicación → PR 2 infraestructura/persistencia → PR 3 API/tests |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-develop |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-develop
400-line budget risk: High

> **Review budget decision**: The initial 3-slice estimate exceeds the 800-line review budget (Slice 1 ≈ 1260, Slice 2 ≈ 896, Slice 3 ≈ 847). The change is kept as a forced chained delivery against `develop` because the logical aggregate boundaries (contracts/application → infrastructure/persistence → API/wiring) are coherent and each slice remains autonomous with its own tests. No physical re-splitting of already implemented code is performed.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Definir contratos y casos de uso de asignación/listado/eliminación | PR 1 | Base `develop`; incluir RED→GREEN→REFACTOR de tests de aplicación. |
| 2 | Persistir asociaciones y validaciones MySQL/EF | PR 2 | Base PR 1; incluir tests de persistencia y repositorios. |
| 3 | Exponer subrecursos API y cerrar cobertura end-to-end | PR 3 | Base PR 2; incluir tests de API, documentación Swagger y aislamiento de catálogo. |

## Phase 1: Contratos y dominio de aplicación

- [x] 1.1 Crear requests/DTOs en `src/SGV.Aplicacion/*/Comandos` y `*/Consultas/Dtos` para `skillId` + `nivelId` en cargo/persona.
- [x] 1.2 Definir `ICargoSkillServicio`, `IPersonaSkillServicio` e intentos de listado/upsert/eliminación con validación de Cargo/Persona, Habilidad y Nivel.
- [x] 1.3 RED: agregar tests de aplicación para nivel inválido, upsert exitoso y borrado físico sin persistir cambios erróneos.

## Phase 2: Infraestructura y persistencia

- [x] 2.1 Implementar repositorios EF Core/Pomelo para `CargoHabilidades`, `PersonaHabilidades` y `NivelesHabilidad` en `src/SGV.Infraestructura/Persistencia/Repositorios`.
- [x] 2.2 Registrar dependencias nuevas en `src/SGV.Infraestructura/DependencyInjection.cs` y asegurar unicidad por par + FK obligatoria a nivel.
- [x] 2.3 GREEN: crear pruebas de persistencia que verifiquen upsert, unicidad y eliminación física en MySQL.

## Phase 3: API y wiring

- [x] 3.1 Agregar endpoints `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills` en `src/SGV.Api/Controllers/CargosController.cs`.
- [x] 3.2 Agregar endpoints `GET/PUT/DELETE /api/v1/personas/{personaId}/skills` en `src/SGV.Api/Controllers/PersonasController.cs`.
- [x] 3.3 REFACTOR: alinear respuestas, códigos HTTP y ProblemDetails con las specs `cargo-skill-assignment` y `persona-skill-assignment`.

## Phase 4: Verificación y cierre

- [x] 4.1 Añadir/ajustar pruebas API para rutas, status codes y documentación de subrecursos sin mezclar `/api/v1/skills`.
- [x] 4.2 Verificar escenarios de `openspec/changes/asociar-habilidades-cargos-personas/specs/*/spec.md` contra la implementación final.
- [x] 4.3 Revisar que los cambios queden listos para PRs encadenados contra `develop` con tests incluidos en cada unidad.
