# Design: Módulo administrable de Puestos

## Technical Approach

Evolucionar la porción existente de Puestos desde lectura (`GET /api/v1/puestos`) hacia catálogo administrable siguiendo los patrones ya usados por Cargos y Unidades Organizativas: entidad de dominio con invariantes mínimas, requests/validadores en Aplicación, servicio de comandos con `CommandResult`, repositorio EF Core con mapeos explícitos, `UnitOfWork` y endpoints REST en el controlador actual.

La consulta activa existente se preserva: `PuestoRepository.Query` debe seguir filtrando `IsActive`, incluyendo `UnidadOrganizativa` y `Cargo`, y ordenando por `Codigo`. Las operaciones nuevas deben cubrir crear, actualizar campos editables, desactivar por baja lógica y reactivar. Quedan fuera de alcance Ocupaciones, Vacantes, permisos/roles y borrado físico.

## Architecture Decisions

| Decisión | Elección | Alternativas consideradas | Rationale |
|----------|----------|---------------------------|-----------|
| Contrato de actualización | Mantener `Codigo` inmutable tras creación, como `Cargo` | Permitir editar `Codigo` | Reduce conflictos con la unicidad activa MySQL y replica el patrón de catálogo existente. |
| Jerarquía | Aceptar `PuestoSuperiorId` opcional y validar solo existencia/estado activo y no autorreferencia directa | Validar ciclos/descendientes | La spec excluye reglas jerárquicas complejas en esta versión; el check MySQL ya evita autorreferencia. |
| Baja lógica | `DELETE /api/v1/puestos/{id}` desactiva (`IsActive=false`, `IsDeleted=true`) | Borrado físico | Respeta auditoría, consultas activas y la exclusión explícita de eliminación física. |
| Unicidad de `Codigo` | Reutilizar columna generada `ActiveCodigoUnique` e índice único en `PuestoConfiguracion` | Índice filtrado | MySQL no soporta índices filtrados; el patrón existente es compatible con Pomelo/MySQL 8. |
| Acoplamiento con otros módulos | No consultar ni modificar Ocupaciones/Vacantes | Bloquear baja por ocupaciones/vacantes | Esos módulos están fuera de alcance; las navegaciones permanecen solo como modelo persistido. |

## Data Flow

```text
HTTP request
  -> PuestosController
  -> IPuestoServicioComandos
  -> FluentValidation
  -> IPuestoRepository + IUnidadOrganizativaRepository + ICargoRepository
  -> SgvDbContext + UnitOfWork
  -> PuestoDto / ProblemDetails
```

Para reactivación, el servicio debe cargar incluyendo eliminados, validar que no exista otro Puesto activo con el mismo `Codigo`, restaurar baja lógica y devolver el DTO con relaciones conservadas.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Modify | Agregar métodos `Actualizar`, `Desactivar` y `Activar`; validar `UnidadOrganizativaId`/`CargoId` no vacíos y mantener `PuestoSuperiorId` opcional. |
| `src/SGV.Aplicacion/Organizacion/Comandos/PuestoRequests.cs` | Create | `CrearPuestoRequest` con `codigo`, `nombre`, `unidadOrganizativaId`, `cargoId`, `puestoSuperiorId?`, `descripcion?`; `ActualizarPuestoRequest` sin `codigo`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/PuestoCommandResult.cs` | Create | Resultado tipado equivalente a `CargoCommandResult`, con errores `NotFound`, `Conflict`, `Validation`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/IPuestoServicioComandos.cs` | Create | Contrato para crear, actualizar, desactivar y reactivar. |
| `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` | Create | Orquestar validación, duplicados, existencia de unidad/cargo/puesto superior activo, persistencia y mapeo a `PuestoDto`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/*Puesto*Validator.cs` | Create | Reglas de forma: obligatorios, longitudes y GUID no vacío. |
| `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoRepository.cs` | Modify | Agregar métodos write y checks `ExistsActiveCodeAsync`, `GetByIdForUpdateAsync`, `GetByIdIncludingDeletedAsync`. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` | Modify | Implementar add/update/soft-delete/reactivate y consultas de soporte sin romper includes de lectura. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modify | Agregar `Puesto -> PuestoEntity` y actualización de entidad existente. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modify | Registrar `IPuestoServicioComandos`. |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modify | Agregar `POST`, `PUT`, `DELETE` soft-delete y `PATCH {id}/reactivar`, con `ProblemDetails`/`ValidationProblemDetails`. |
| `tests/SGV.Tests/**/Puesto*Tests.cs` | Modify/Create | Cobertura por dominio, aplicación, API y persistencia. |

## Interfaces / Contracts

- `POST /api/v1/puestos` crea y devuelve `201 Created` con `PuestoDto`.
- `PUT /api/v1/puestos/{id}` actualiza campos editables y devuelve `200 OK`.
- `DELETE /api/v1/puestos/{id}` aplica baja lógica y devuelve `204 NoContent`.
- `PATCH /api/v1/puestos/{id}/reactivar` reactiva y devuelve `200 OK`.
- Errores: `400` para validación, `404` para inexistente y `409` para `Codigo` activo duplicado.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Dominio | Invariantes, actualización sin cambiar código, activación/desactivación y superior opcional | xUnit en `tests/SGV.Tests/Dominio/Organizacion/PuestoTests.cs`. |
| Aplicación | Validación antes de repositorios, duplicados, referencias inexistentes, soft-delete/reactivación | Fakes similares a `CargoServicioComandosTests`. |
| API | Endpoints nuevos, códigos HTTP, JSON camelCase y ausencia de `[Authorize]` | Extender `PuestosControllerTests`. |
| Persistencia | Unicidad activa, baja lógica, reactivación e includes de Unidad/Cargo | `MySqlFact` en `PuestoRepositoryTests` y modelo MySQL. |

## Migration / Rollout

No se requiere migración si se conserva el esquema actual: `Puestos` ya tiene `Codigo`, `Nombre`, relaciones, `IsActive`, auditoría, check de autorreferencia e índice único activo por columna generada. Si la implementación detecta divergencia en snapshot/migraciones, generar una migración Pomelo/MySQL mínima y verificar `dotnet test`.

## Open Questions

- Ninguna bloqueante. La validación profunda de ciclos jerárquicos queda diferida por decisión de alcance.
