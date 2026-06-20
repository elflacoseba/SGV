# Tasks: Implementar el módulo de Personas

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 700-1000 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 dominio+consultas -> PR 2 comandos+persistencia -> PR 3 API+documentación |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Dominio y consultas consumer-safe | PR 1 | Base según estrategia a elegir; sin relaciones excluidas |
| 2 | Comandos, repositorio y conflictos MySQL | PR 2 | Depende de PR 1; incluye validadores y UoW |
| 3 | Controller, Swagger y pruebas API | PR 3 | Depende de PR 2; cierre funcional del slice |

## Phase 1: Base de dominio y contrato de lectura

- [x] 1.1 RED `tests/SGV.Tests/Dominio/Personas/PersonaTests.cs`: cubrir `CambiarDatos`, `CambiarDocumento`, `Desactivar()` y `Activar()` con baja lógica.
- [x] 1.2 GREEN `src/SGV.Dominio/Personas/Persona.cs`: agregar `Desactivar()`/`Activar()` sin tocar `Habilidades` ni `Ocupaciones`.
- [x] 1.3 RED `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioConsultaTests.cs`: detalle/listado devuelven `PersonaDto` plano y excluyen inactivas.
- [x] 1.4 GREEN `src/SGV.Aplicacion/Personas/Consultas/{Dtos/PersonaDto.cs,IPersonaRepository.cs,IPersonaServicioConsulta.cs,PersonaServicioConsulta.cs}`.
- [x] 1.5 REFACTOR `src/SGV.Aplicacion/Personas/Consultas/` y helpers de test: unificar mapeo DTO y nombres consistentes.

## Phase 2: Comandos y validación TDD

- [ ] 2.1 RED `tests/SGV.Tests/Aplicacion/Personas/{CrearPersonaRequestValidatorTests.cs,ActualizarPersonaRequestValidatorTests.cs}`: requeridos, longitudes, email, documento opcional válido.
- [ ] 2.2 GREEN `src/SGV.Aplicacion/Personas/Comandos/{PersonaRequests.cs,Validaciones/CrearPersonaRequestValidator.cs,Validaciones/ActualizarPersonaRequestValidator.cs}`.
- [ ] 2.3 RED `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioComandosTests.cs`: crear, actualizar, desactivar, reactivar, `409`, `404`, `400` y sin guardar ante error.
- [ ] 2.4 GREEN `src/SGV.Aplicacion/Personas/Comandos/{PersonaCommandResult.cs,IPersonaServicioComandos.cs,PersonaServicioComandos.cs}` usando unicidad activa de `Legajo`, `Email` y documento.
- [ ] 2.5 REFACTOR `src/SGV.Aplicacion/Personas/Comandos/` para centralizar field-errors camelCase y mensajes tipados.

## Phase 3: Persistencia e integración

- [ ] 3.1 RED `tests/SGV.Tests/Persistencia/PersonaRepositoryTests.cs`: filtros activos, orden apellido/nombre, reactivación y ausencia de relaciones fuera de alcance.
- [ ] 3.2 GREEN `src/SGV.Infraestructura/Persistencia/{Repositorios/PersonaRepository.cs,Mapeos/DomainToPersistenceMapper.cs,Mapeos/PersistenceToDomainMapper.cs}` sin `Include` de `PersonaHabilidad` u `Ocupaciones`.
- [ ] 3.3 RED `tests/SGV.Tests/Persistencia/PersonaRepositoryUniqueConstraintsTests.cs`: duplicados activos y reutilización tras baja con `PersonaConfiguracion` existente; sin migración salvo brecha verificable.
- [ ] 3.4 GREEN `src/SGV.Infraestructura/DependencyInjection.cs` y traducción de conflictos persistidos en el servicio de comandos.

## Phase 4: API y documentación verificable

- [ ] 4.1 RED `tests/SGV.Tests/Api/PersonasControllerTests.cs`: `GET/POST/PUT/DELETE/PATCH`, `200/201/204/400/404/409` y contrato sin relaciones excluidas.
- [ ] 4.2 GREEN `src/SGV.Api/Controllers/PersonasController.cs` con ruta `api/v1/personas` y ProblemDetails alineado al patrón de `SkillsController`.
- [ ] 4.3 REFACTOR `src/SGV.Api/Program.cs` y `src/SGV.Infraestructura/DependencyInjection.cs`: registrar servicios y actualizar descripción Swagger para incluir Personas.

## Phase 5: Verificación final del slice

- [ ] 5.1 Ejecutar `dotnet test` sobre pruebas nuevas y regresión relevante; confirmar que el slice excluye Postulantes, Ocupaciones, Habilidades y `PersonaHabilidad`.
