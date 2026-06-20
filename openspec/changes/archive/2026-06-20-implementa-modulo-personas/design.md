# Design: Implementar el módulo de Personas

## Technical Approach

El primer corte agregará un módulo administrativo de Personas siguiendo el patrón existente de Habilidades/Cargos: DTOs consumer-safe en Aplicación, servicios separados de consulta y comandos, repositorio EF Core en Infraestructura, controlador MVC en `SGV.Api` y pruebas por capa. Se reutilizará la tabla `Personas` ya modelada; no se diseñan migraciones nuevas salvo que la implementación detecte una brecha verificable.

El alcance queda limitado a datos propios de Persona: `Legajo`, `Nombres`, `Apellidos`, `Email`, `TipoDocumento`, `NumeroDocumento`, `Telefono` y estado activo/inactivo. No se implementan ni exponen Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`.

## Architecture Decisions

| Decisión | Opción elegida | Alternativas consideradas | Rationale |
|---|---|---|---|
| Patrón de módulo | Replicar `Habilidades`: `Consultas`, `Comandos`, validators, result tipado y controlador MVC. | Minimal API o servicio único CRUD. | Mantiene Clean Architecture y consistencia con DI, tests y contratos existentes. |
| Persistencia | Reutilizar `PersonaEntity`, `PersonaConfiguracion` y columnas generadas únicas activas. | Crear tablas nuevas o migración preventiva. | La exploración y el código muestran tabla e índices ya existentes; migrar sin brecha sería ruido y riesgo. |
| Relaciones excluidas | Consultas sin `Include` y DTO plano sin colecciones. | Cargar entidad completa y filtrar al mapear. | Evita acoplamiento accidental con selección, ocupaciones y habilidades. |
| Conflictos | Validar duplicados activos antes de guardar y traducir violaciones únicas de MySQL a `409 Conflict`. | Confiar solo en excepciones de base de datos. | La prevalidación mejora mensajes; el índice sigue siendo defensa ante carreras. |
| Baja/reactivación | Usar baja lógica: `IsActive=false`, `IsDeleted=true`, `DeletedAt`; reactivación limpia esos campos. | Eliminación física. | Coincide con `HabilidadRepository` y con índices activos basados en `IsDeleted`. |

## Data Flow

```text
HTTP api/v1/personas
  -> PersonasController
  -> IPersonaServicioConsulta / IPersonaServicioComandos
  -> IPersonaRepository + IUnitOfWork
  -> PersonaRepository
  -> SgvDbContext.Personas
```

Para crear/actualizar/reactivar: validar request, verificar conflictos activos por `Legajo`, `Email` y documento, mutar `Persona`, guardar con Unit of Work y devolver `PersonaDto`. Si MySQL rechaza por índice único activo, convertir a error tipado de conflicto.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Dominio/Personas/Persona.cs` | Modify | Agregar métodos `Desactivar()` y `Activar()`; preservar validaciones existentes y no tocar colecciones. |
| `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaDto.cs` | Create | DTO plano sin auditoría ni navegaciones. |
| `src/SGV.Aplicacion/Personas/Consultas/IPersonaRepository.cs` | Create | Contrato de lectura/escritura y verificaciones de unicidad activa. |
| `src/SGV.Aplicacion/Personas/Consultas/IPersonaServicioConsulta.cs` | Create | Listado activo y detalle activo. |
| `src/SGV.Aplicacion/Personas/Consultas/PersonaServicioConsulta.cs` | Create | Mapea dominio a `PersonaDto`. |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaRequests.cs` | Create | Requests de creación y actualización. |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaCommandResult.cs` | Create | Resultado tipado con `NotFound`, `Conflict`, `Validation`. |
| `src/SGV.Aplicacion/Personas/Comandos/IPersonaServicioComandos.cs` | Create | Crear, actualizar, desactivar y reactivar. |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs` | Create | Casos de uso, validación, conflictos y Unit of Work. |
| `src/SGV.Aplicacion/Personas/Comandos/Validaciones/*Validator.cs` | Create | FluentValidation con longitudes del modelo EF/dominio. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modify | Mapeo `Persona` -> `PersonaEntity` y actualización de campos propios. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Modify | Mapeo `PersonaEntity` -> `Persona` sin mapear colecciones. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaRepository.cs` | Create | Repositorio con filtros activos, orden por apellido/nombre y métodos de conflicto. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modify | Registrar repositorio y servicios de Personas. |
| `src/SGV.Api/Controllers/PersonasController.cs` | Create | Endpoints `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`, `PATCH {id}/reactivar`. |
| `src/SGV.Api/Program.cs` | Modify | Actualizar descripción Swagger para incluir Personas. |
| `tests/SGV.Tests/**/Personas*Tests.cs` | Create | Cobertura dominio, aplicación, persistencia y API. |

## Interfaces / Contracts

`PersonaDto` debe contener solo: `Id`, `Legajo`, `Nombres`, `Apellidos`, `Email`, `TipoDocumento`, `NumeroDocumento`, `Telefono`, `IsActive`.

`CrearPersonaRequest` y `ActualizarPersonaRequest` aceptarán los mismos campos administrables, con `Legajo`, `Nombres` y `Apellidos` requeridos. `Email` y documento serán opcionales; si se informan, se validan formato/longitud y unicidad activa. Documento solo participa en unicidad cuando `TipoDocumento` y `NumeroDocumento` están presentes.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Dominio | Validaciones, activación y desactivación. | xUnit `[Fact]` unitario sobre `Persona`. |
| Aplicación | Validadores, duplicados, not found, sin guardar ante error. | Fakes de `IPersonaRepository` e `IUnitOfWork`, patrón de Habilidades. |
| Persistencia | Filtros activos, mapeos, unicidad activa y ausencia de `Include`. | Tests EF/MySQL con `MySqlFact`; metadata para columnas generadas ya existentes. |
| API | Códigos 200/201/204/400/404/409 y contrato sin relaciones. | `ApiWebApplicationFactory` con servicios fake. |

## Migration / Rollout

No se requiere migración inicial. Rollout por un PR o PRs encadenados si `sdd-tasks` estima riesgo alto contra el presupuesto de 400 líneas.

## Open Questions

- [ ] Confirmar si `Email` debe validarse con formato estricto desde este corte o solo longitud/no vacío cuando se informa.
- [ ] Confirmar si el listado debe incluir solo activas siempre o admitir parámetro administrativo futuro para incluir inactivas.
