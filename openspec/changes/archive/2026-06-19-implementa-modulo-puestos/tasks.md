# Tasks: Módulo administrable de Puestos

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 650-900 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 dominio/aplicación → PR 2 persistencia → PR 3 API |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Reglas de dominio y comandos de aplicación | PR 1 | Incluye tests de dominio, validadores y servicio de comandos. |
| 2 | Persistencia MySQL de ciclo de vida y unicidad activa | PR 2 | Depende de PR 1; incluye repositorio, mapper y tests MySQL. |
| 3 | Endpoints HTTP de gestión de puestos | PR 3 | Depende de PR 2; incluye DI, controlador y tests API. |

## Phase 1: Dominio y contratos de aplicación

- [x] 1.1 RED: agregar pruebas en `tests/SGV.Tests/Dominio/Organizacion/PuestoTests.cs` para campos requeridos, superior opcional, actualizar sin cambiar `Codigo`, desactivar y activar.
- [x] 1.2 GREEN: modificar `src/SGV.Dominio/Organizacion/Puesto.cs` con invariantes y métodos `Actualizar`, `Desactivar` y `Activar`.
- [x] 1.3 RED: crear tests de validadores en `tests/SGV.Tests/Aplicacion/Organizacion/*PuestoRequestValidatorTests.cs` para obligatorios, longitudes y GUID vacío.
- [x] 1.4 GREEN: crear `src/SGV.Aplicacion/Organizacion/Comandos/PuestoRequests.cs` y validadores en `Comandos/Validaciones/*Puesto*Validator.cs`.

## Phase 2: Servicio de comandos

- [x] 2.1 RED: crear `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioComandosTests.cs` para crear, actualizar, duplicado activo, referencias inválidas, baja lógica y reactivación con conflicto.
- [x] 2.2 GREEN: crear `PuestoCommandResult.cs`, `IPuestoServicioComandos.cs` y `PuestoServicioComandos.cs` en `src/SGV.Aplicacion/Organizacion/Comandos/`.
- [x] 2.3 Modificar `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoRepository.cs` con métodos write y consultas incluyendo eliminados.

## Phase 3: Persistencia MySQL

- [x] 3.1 RED: extender `tests/SGV.Tests/Persistencia/PuestoRepositoryTests.cs` para unicidad activa, reutilización tras baja, reactivación e includes de lectura activa.
- [x] 3.2 GREEN: modificar `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` para alta, update, soft-delete, reactivación y checks de código activo.
- [x] 3.3 Modificar `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` para mapear y actualizar `PuestoEntity` sin romper relaciones existentes.
- [x] 3.4 Verificar `src/SGV.Infraestructura/Persistencia/Configuraciones/PuestoConfiguracion.cs`; generar migración solo si el snapshot no contiene `ActiveCodigoUnique` e índice único activo.

## Phase 4: API e integración

- [x] 4.1 RED: extender `tests/SGV.Tests/Api/PuestosControllerTests.cs` para `POST`, `PUT`, `DELETE`, `PATCH {id}/reactivar`, errores `400/404/409` y JSON camelCase.
- [x] 4.2 GREEN: modificar `src/SGV.Api/Controllers/PuestosController.cs` con endpoints de gestión y respuestas `ProblemDetails`/`ValidationProblemDetails`.
- [x] 4.3 Registrar `IPuestoServicioComandos` en `src/SGV.Infraestructura/DependencyInjection.cs`.

## Phase 5: Verificación

- [x] 5.1 Ejecutar `dotnet build` y corregir fallos de compilación.
- [x] 5.2 Ejecutar `dotnet test` y confirmar escenarios OpenSpec de creación, edición, consulta activa, baja lógica, reactivación y exclusiones.
