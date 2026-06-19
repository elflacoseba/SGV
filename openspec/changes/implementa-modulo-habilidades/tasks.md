# Tasks: Módulo administrable de Habilidades

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas cambiadas estimadas | 900-1400 |
| Riesgo presupuesto 400 líneas | High |
| PRs encadenados recomendados | Yes |
| División sugerida | PR 1 dominio/aplicación → PR 2 persistencia → PR 3 API/documentación |
| Delivery strategy | ask-on-risk / ask-always |
| Chain strategy | feature-branch-chain (develop tracker) |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain (develop tracker)
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unidad | Objetivo | PR probable | Notas |
|------|----------|-------------|-------|
| 1 | Reglas de dominio y comandos de aplicación | PR 1 | Crear rama nueva antes de implementar; base `develop`. **Estado: COMPLETADO**. |
| 2 | Persistencia MySQL/Pomelo | PR 2 | Base sobre PR 1; incluir migración solo si falta `ActiveCodigoUnique`. |
| 3 | API `/api/v1/skills` y contrato público | PR 3 | Base sobre PR 2; verificar Swagger y ausencia de asignaciones. |

## Fase 0: Preparación de workflow

- [x] 0.1 Antes de aplicar, crear una rama nueva para la porción aprobada y confirmar estrategia de PRs encadenados.
- [x] 0.2 Revisar `src/SGV.Api/Controllers/SkillsController.cs` y patrones de Cargos/Puestos existentes antes de tocar código.

## Fase 1: Dominio y aplicación (TDD)

- [x] 1.1 RED: agregar pruebas en `tests/SGV.Tests/Dominio/HabilidadTests.cs` para `Codigo` inmutable, actualizar campos editables, desactivar y reactivar.
- [x] 1.2 GREEN: modificar `src/SGV.Dominio/Habilidades/Habilidad.cs` con `Actualizar`, `Desactivar`, `Activar` sin permitir cambio de `Codigo`.
- [x] 1.3 RED: agregar pruebas en `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` para crear, validar, duplicado activo, no encontrado y conflicto al reactivar.
- [x] 1.4 GREEN: crear `src/SGV.Aplicacion/Habilidades/Comandos/*` con requests, interfaz, servicio, resultado y validadores.
- [x] 1.5 REFACTOR: extender `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadRepository.cs` solo con métodos necesarios de escritura y búsqueda.

## Fase 2: Persistencia MySQL/Pomelo (TDD)

> Fuera del alcance del slice 1. Pasa al slice 2 (`feature/habilidades-02-persistencia`).

- [x] 2.1 RED: ampliar `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` para alta, baja lógica, reactivación y unicidad activa.
- [x] 2.2 GREEN: implementar escritura en `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs`.
- [x] 2.3 GREEN: completar mapeos en `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` para `Habilidad`.
- [x] 2.4 Verificar `src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs` y migraciones; generar migración MySQL solo si falta la columna generada.
- [x] 2.5 Registrar `IHabilidadServicioComandos` en `src/SGV.Infraestructura/DependencyInjection.cs`.

## Fase 3: API `/api/v1/skills` (TDD)

> Fuera del alcance del slice 1. Pasa al slice 3.

- [x] 3.1 RED: ampliar `tests/SGV.Tests/Api/SkillsControllerTests.cs` para POST, PUT sin `Codigo`, DELETE, PATCH reactivar, 400/404/409 y ruta canónica.
- [x] 3.2 GREEN: modificar `src/SGV.Api/Controllers/SkillsController.cs` con endpoints administrativos y `ProblemDetails` esperados.
- [x] 3.3 RED/GREEN: actualizar `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` para descubrir escrituras de skills y no documentar `CargoHabilidad`/`PersonaHabilidad`.

## Fase 4: Verificación

- [ ] 4.1 Ejecutar `dotnet build` y `dotnet test`; corregir solo fallas relacionadas con esta porción.
- [ ] 4.2 Confirmar manualmente que no se agregaron endpoints ni comandos de asignación a cargos/personas.
