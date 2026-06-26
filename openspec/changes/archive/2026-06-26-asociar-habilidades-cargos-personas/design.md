# Design: Asociar Habilidades a Cargos y Personas

## Technical Approach

Exponer dos subrecursos administrables sobre la arquitectura existente: `/api/v1/cargos/{cargoId}/skills` y `/api/v1/personas/{personaId}/skills`. La capa API delegará en servicios de aplicación específicos; la aplicación validará existencia de Cargo/Persona, Habilidad activa y NivelHabilidad; infraestructura persistirá con EF Core/Pomelo sobre las tablas ya modeladas `CargoHabilidades`, `PersonaHabilidades` y `NivelesHabilidad`.

El contrato HTTP usará `skillId` y `nivelId`; internamente `nivelId` mapeará a `NivelRequeridoId` para Cargos y a `NivelHabilidadId` para Personas. La eliminación del subrecurso será borrado físico de la fila de asociación, no soft-delete.

## Architecture Decisions

| Decisión | Elección | Alternativas consideradas | Rationale |
|---|---|---|---|
| Subrecursos anidados | Agregar acciones a `CargosController` y `PersonasController` | Crear controller global de asignaciones | Mantiene la semántica del recurso dueño y evita mezclar asignaciones dentro de `/api/v1/skills`. |
| Servicios dedicados | Crear servicios/contratos separados para habilidades de Cargo y Persona | Ampliar `CargoServicioComandos` y `PersonaServicioComandos` | Evita inflar servicios CRUD existentes y conserva SRP. |
| DTO consumer-safe | DTOs `CargoSkillDto`/`PersonaSkillDto` con `skillId`, `nivelId` y datos mínimos de lectura | Devolver entidades de dominio/persistencia o metadata futura | Mantiene contratos estables, oculta nombres internos (`NivelRequeridoId`, `NivelHabilidadId`) y evita agregar campos sin caso de uso confirmado. |
| Upsert por par | `PUT` crea o actualiza la asociación `{ownerId, skillId}` | `POST` duplicable o endpoint separado de update | La base ya exige unicidad por par; el contrato pedido dice agregar/actualizar. |
| Borrado físico | `DELETE` elimina la entidad `CargoHabilidadEntity`/`PersonaHabilidadEntity` | Soft-delete | Las asociaciones no son auditables en el modelo actual y las specs exigen eliminación física. |

## Data Flow

```text
Cliente
  -> CargosController/PersonasController
  -> Servicio de aplicación de asignaciones
  -> Repositorio de asignaciones + repositorios catálogo
  -> SgvDbContext
  -> CargoHabilidades / PersonaHabilidades / NivelesHabilidad
```

Para guardar: validar owner activo, habilidad activa y nivel existente; buscar asociación por par; crear o actualizar nivel; `SaveChangesAsync`. Para eliminar: validar owner/asociación; remover la fila y guardar.

## File Changes

| File | Acción | Descripción |
|---|---|---|
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillRequests.cs` | Crear | Requests para upsert de habilidad requerida de Cargo. |
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDto.cs` | Crear | DTO de asociación Cargo-Habilidad. |
| `src/SGV.Aplicacion/Organizacion/Comandos/ICargoSkillServicio.cs` | Crear | Contrato de listar, upsert y eliminar. |
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` | Crear | Caso de uso y validaciones de Cargo, Habilidad y NivelHabilidad. |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillRequests.cs` | Crear | Requests para upsert de habilidad de Persona. |
| `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaSkillDto.cs` | Crear | DTO de asociación Persona-Habilidad. |
| `src/SGV.Aplicacion/Personas/Comandos/IPersonaSkillServicio.cs` | Crear | Contrato de listar, upsert y eliminar. |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs` | Crear | Caso de uso equivalente para Personas. |
| `src/SGV.Aplicacion/Habilidades/Consultas/INivelHabilidadRepository.cs` | Crear | Consulta de niveles válidos sin CRUD. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/*SkillRepository.cs` | Crear | Operaciones EF para asociaciones y borrado físico. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/NivelHabilidadRepository.cs` | Crear | Lectura de catálogo `NivelesHabilidad`. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificar | Registrar nuevos servicios y repositorios scoped. |
| `src/SGV.Api/Controllers/CargosController.cs` | Modificar | Endpoints `GET/PUT/DELETE {id}/skills`. |
| `src/SGV.Api/Controllers/PersonasController.cs` | Modificar | Endpoints `GET/PUT/DELETE {id}/skills`. |

## Interfaces / Contracts

```http
GET    /api/v1/cargos/{cargoId}/skills
PUT    /api/v1/cargos/{cargoId}/skills/{skillId}   { "nivelId": "..." }
DELETE /api/v1/cargos/{cargoId}/skills/{skillId}

GET    /api/v1/personas/{personaId}/skills
PUT    /api/v1/personas/{personaId}/skills/{skillId} { "nivelId": "..." }
DELETE /api/v1/personas/{personaId}/skills/{skillId}
```

Errores: `404` para owner/asociación inexistente, `400` para `nivelId` inválido, `409` solo si aparece una colisión de unicidad no resuelta por el upsert.

## Testing Strategy

| Layer | Qué probar | Enfoque |
|---|---|---|
| Dominio | Duplicados y validación básica de asociaciones | xUnit sobre `Cargo.AgregarHabilidad` y `Persona.AgregarHabilidad`; agregar método de actualización si se implementa. |
| Aplicación | Validaciones, upsert, eliminación sin guardar ante errores | Fakes siguiendo `CargoServicioComandosTests` y `PersonaServicioComandosTests`. |
| API | Rutas, status codes, ProblemDetails y Swagger | `ApiWebApplicationFactory` con servicios fake. |
| Persistencia | Unicidad por par, FK de nivel y borrado físico | Tests `[MySqlFact]` sobre repositorios nuevos. |

## Migration / Rollout

No se requiere migración de esquema: `SgvDbContextModelSnapshot` ya contiene `CargoHabilidades` y `PersonaHabilidades` con FK obligatoria a `NivelesHabilidad`, índices por nivel y unicidad por par owner-habilidad. Rollout por PRs encadenados contra `develop`: primero contratos/aplicación, luego infraestructura, luego API/tests.

## Open Questions

- [x] Mantener el contrato simple con `skillId` y `nivelId`; `ponderacion`, `esObligatoria`, `fuente` y `verificadoAt` quedan fuera del primer corte y podrán evaluarse en cambios futuros.
