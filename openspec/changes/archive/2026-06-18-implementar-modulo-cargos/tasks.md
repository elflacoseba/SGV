# Tasks: Implementar módulo de Cargos

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1400–1600 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 → PR 3 |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-develop |

Decision needed before apply: Yes (resolved → stacked-to-develop)
Chained PRs recommended: Yes
Chain strategy: stacked-to-develop
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Dominio + Infraestructura: entidades, configuraciones EF, constantes, seed, migración, repositorio NivelCargo | PR 1 | Base: feature/implementar-modulo-cargos. Incluye migración fail-loud y tests de persistencia. |
| 2 | Aplicación: comando service, query service, validadores, DTOs, expansión de repositorio Cargo | PR 2 | Base: PR 1. Incluye tests unitarios de aplicación. |
| 3 | API: controllers, DI, tests de integración | PR 3 | Base: PR 2. Incluye tests de integración API. |

---

## Phase 1: Dominio — Entidades e invariantes

- [x] 1.1 Crear `src/SGV.Dominio/Organizacion/NivelCargo.cs`: clase `EntidadBase` con `Codigo` (string, max 50), `Nombre` (string, max 100), `ValorNumerico` (byte), `Orden` (int). Constructor privado + constructor público con validaciones. Sin `IsActive`/`IsDeleted`.
- [x] 1.2 Modificar `src/SGV.Dominio/Organizacion/Cargo.cs`: reemplazar `string? Nivel` por `Guid NivelId` + `NivelCargo? NivelCargo`. Hacer `Codigo` inmutable (solo settable en constructor). Agregar métodos `Desactivar()` y `Activar()`. Separar `CambiarDatos` en dos: uno para creación (con Codigo) y otro para actualización (sin Codigo). Agregar verificación defensiva: `Desactivar()` debe lanzar `InvalidOperationException` si hay `Puestos` activos (campo `_puestos` con `IsActive == true`).
- [x] 1.3 Crear tests unitarios en `tests/SGV.Tests/Dominio/Organizacion/NivelCargoTests.cs`: invariantes de Codigo, Nombre, ValorNumerico rango.
- [x] 1.4 Crear/actualizar tests unitarios en `tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs`: inmutabilidad de Codigo tras creación, Desactivar/Activar, NivelId requerido, rechazo de desactivación con Puestos activos.

## Phase 2: Infraestructura — Persistencia

- [x] 2.1 Crear `src/SGV.Infraestructura/Persistencia/Entidades/NivelCargoEntity.cs`: clase `EntityBase` con `Codigo`, `Nombre`, `ValorNumerico` (byte), `Orden` (int). Sin `IsActive`/`IsDeleted`.
- [x] 2.2 Modificar `src/SGV.Infraestructura/Persistencia/Entidades/CargoEntity.cs`: reemplazar `string? Nivel` por `Guid NivelId` + `NivelCargoEntity? NivelCargo`.
- [x] 2.3 Crear `src/SGV.Infraestructura/Persistencia/Configuraciones/NivelCargoConfiguracion.cs`: tabla `NivelesCargo`, PK `char(36)`, `Codigo` UNIQUE `varchar(50)`, `Nombre` `varchar(100)`, `ValorNumerico` `tinyint` con check constraint, `Orden` `int`. Sin `IsActive`/`IsDeleted`. Seguir patrón de `TipoUnidadOrganizativaConfiguracion`.
- [x] 2.4 Modificar `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`: agregar FK `NivelId → NivelesCargo.Id` con `OnDelete(Restrict)`, índice `IX_Cargos_NivelId`. Eliminar config de `Nivel` string.
- [x] 2.5 Crear `src/SGV.Infraestructura/Persistencia/Catalogos/NivelCargoConstantes.cs`: 4 Guids estáticos (Directivo, ConduccionMedia, Operativo, Academico). Seguir patrón de `TipoUnidadOrganizativaConstantes`.
- [x] 2.6 Modificar `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs`: agregar `NivelesCargo` HasData usando `NivelCargoConstantes`. Actualizar seed de `CargoEntity` con `NivelId`.
- [x] 2.7 Modificar `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs`: agregar `DbSet<NivelCargoEntity> NivelesCargo`.
- [x] 2.8 Modificar `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs`: actualizar mapper de Cargo (`Nivel→NivelId`, incluir `NivelCargo`). Agregar mapper de `NivelCargo`.
- [x] 2.9 Modificar `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs`: agregar mapper `Cargo` domain→entity (`NivelId`, `NivelCargoEntity`). Agregar mapper `NivelCargo`.
- [x] 2.10 Crear `src/SGV.Infraestructura/Persistencia/Repositorios/NivelCargoRepository.cs`: implementar `INivelCargoRepository` (read-only, como `TipoUnidadOrganizativaRepository`).
- [x] 2.11 Generar migración EF Core `CambiarNivelStringANivelId`: pre-flight SELECT para detectar valores sucios en `Cargos.Nivel`, fail-loud con `InvalidOperationException` si hay ofensivos. Crear tabla `NivelesCargo`, InsertData seed, AddColumn `NivelId` nullable, backfill, NOT NULL constraint, FK + índice, DROP COLUMN `Nivel`.
- [x] 2.12 Crear test `tests/SGV.Tests/Persistencia/NivelCargoConstantesTests.cs`: verificar coherencia entre `InsertData` de migración y `DatosSemilla.HasData`.
- [x] 2.13 Crear/actualizar test `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`: verificar CRUD con `NivelId`, `ExistsActiveCodeAsync`, `ReactivateAsync`.
- [x] 2.14 Crear test `tests/SGV.Tests/Persistencia/MigracionFailLoudTests.cs` (o extender existente): verificar que la migración aborta con valores sucios en `Cargos.Nivel`.

## Phase 3: Aplicación — Servicios y validaciones

- [x] 3.1 Crear `src/SGV.Aplicacion/Organizacion/Comandos/CargoCommandResult.cs`: `CargoErrorType` (NotFound, Conflict, Validation), `CargoError`, `CargoCommandResult` con factory methods. Seguir patrón de `UnidadOrganizativaCommandResult`.
- [x] 3.2 Crear `src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs`: `CrearCargoRequest(Codigo, Nombre, NivelId, Descripcion?)`, `ActualizarCargoRequest(Nombre, NivelId, Descripcion?)` — sin `Codigo`.
- [x] 3.3 Crear `src/SGV.Aplicacion/Organizacion/Comandos/ICargoServicioComandos.cs`: interfaz con `CrearAsync`, `ActualizarAsync`, `DesactivarAsync`, `ReactivarAsync`.
- [x] 3.4 Crear `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/CrearCargoRequestValidator.cs`: FluentValidation — `Codigo` required(50), `Nombre` required(200), `NivelId` not empty, `Descripcion` 1000.
- [x] 3.5 Crear `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs`: FluentValidation — `Nombre` required(200), `NivelId` not empty, `Descripcion` 1000.
- [x] 3.6 Modificar `src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs`: expandir con métodos write — `AddAsync`, `GetByIdForUpdateAsync`, `GetByIdIncludingDeletedAsync`, `UpdateAsync`, `DeleteAsync`, `ReactivateAsync`, `ExistsActiveCodeAsync`, `HasActivePuestosAsync`. Seguir patrón de `IUnidadOrganizativaRepository`.
- [x] 3.7 Crear `src/SGV.Aplicacion/Organizacion/Consultas/INivelCargoRepository.cs`: `IReadOnlyRepository<NivelCargo>` (como `ITipoUnidadOrganizativaRepository`).
- [x] 3.8 Crear `src/SGV.Aplicacion/Organizacion/Consultas/INivelCargoServicioConsulta.cs`: `ListAsync`, `GetByIdAsync`.
- [x] 3.9 Crear `src/SGV.Aplicacion/Organizacion/Consultas/NivelCargoServicioConsulta.cs`: implementación del query service.
- [x] 3.10 Crear `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/NivelCargoDto.cs`: `Id`, `Codigo`, `Nombre`, `ValorNumerico`, `Orden`.
- [x] 3.11 Modificar `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoDto.cs`: reemplazar `string? Nivel` por `Guid NivelId` + `string NivelNombre`.
- [x] 3.12 Crear `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`: implementación con validación, verificación de `NivelId` FK, `Codigo` duplicate check, desactivación con guard de Puestos activos. Seguir patrón de `UnidadOrganizativaServicioComandos`.
- [x] 3.13 Crear tests unitarios en `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs`: crear, actualizar, desactivar (con/sin Puestos activos), reactivar, errores (Codigo dup, NivelId inexistente, NotFound).
- [x] 3.14 Crear tests unitarios en `tests/SGV.Tests/Aplicacion/Organizacion/CrearCargoRequestValidatorTests.cs` y `ActualizarCargoRequestValidatorTests.cs`.
- [x] 3.15 Crear/actualizar tests unitarios en `tests/SGV.Tests/Aplicacion/Organizacion/NivelCargoServicioConsultaTests.cs`.

## Phase 4: API — Endpoints y DI

- [x] 4.1 Modificar `src/SGV.Api/Controllers/CargosController.cs`: agregar `POST` (201/400/409), `PUT` (200/400/404/409), `DELETE` soft (204/404/409), `PATCH reactivar` (200/404/409). Inyectar `ICargoServicioComandos`. Mapear `CargoCommandResult` a `ActionResult` con `ProblemDetails` para errores.
- [x] 4.2 Crear `src/SGV.Api/Controllers/NivelesCargoController.cs`: `GET /api/v1/niveles-cargo` (list), `GET /api/v1/niveles-cargo/{id:guid}` (detail). Read-only, sin endpoints de escritura.
- [x] 4.3 Modificar `src/SGV.Infraestructura/DependencyInjection.cs`: registrar `ICargoServicioComandos`, `INivelCargoServicioConsulta`, `INivelCargoRepository`.
- [x] 4.4 Crear/actualizar tests de integración en `tests/SGV.Tests/Api/CargosControllerTests.cs`: POST/PUT/DELETE/PATCH con casos de éxito y error, ProblemDetails.
- [x] 4.5 Crear tests de integración en `tests/SGV.Tests/Api/NivelesCargoControllerTests.cs`: GET list/detail, escritura devuelve 405 o no existe endpoint.

## Phase 5: Verificación

- [x] 5.1 Ejecutar `dotnet build` — sin errores ni warnings.
- [x] 5.2 Ejecutar `dotnet test` — todos los tests pasan (excepto pre-existing failure no relacionado).
- [x] 5.3 Verificar que los escenarios de las specs están cubiertos por al menos un test cada uno.
