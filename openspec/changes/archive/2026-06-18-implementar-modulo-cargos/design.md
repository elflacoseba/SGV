# Design: Implementar módulo de Cargos

## Technical Approach

Seguir Clean Architecture existente: invariantes en Dominio, casos de uso en Aplicación, EF Core/Pomelo en Infraestructura, controllers en API. El patrón `UnidadOrganizativa` (CRUD + baja lógica + reactivación) sirve como plantilla para los comandos de Cargo. Se introduce la entidad `NivelCargo` como catálogo inmutable (como `TipoUnidadOrganizativa`), reemplazando el campo string `Cargo.Nivel` por FK `Cargo.NivelId`. La migración sigue REQ-SPA-EVOLUTION-001: pre-flight de datos sucios, backfill, `DROP COLUMN` solo si el backfill es limpio.

## Architecture Decisions

| Decisión | Elección | Descartado | Razón |
|----------|----------|------------|-------|
| Modelo de NivelCargo | Entidad `EntidadBase` (sin auditoría) | Entidad con IsActive/IsDeleted | Catálogo inmutable según spec: no tiene ciclo de vida |
| FK NivelId | `OnDelete(Restrict)` + índice | `OnDelete(Cascade)` | Evitar borrado accidental de niveles referenciados por cargos |
| Unicidad de Codigo activo | Computed column `ActiveCodigoUnique` (igual que UnidadOrganizativa) | Unique filter index | MySQL no soporta filtered indexes; patrón ya validado |
| Migración Nivel → NivelId | Fail-loud con pre-flight SQL | Migración silenciosa o manual | REQ-SPA-EVOLUTION-001 exige abortar si hay valores sucios |
| Seed de NivelesCargo | Constantes estáticas compartidas entre migración y HasData | Hardcodear Guids sueltos | Evitar drift entre migración y modelo; test lo verifica |
| Inmutabilidad de Codigo | Validación en dominio + validator; Codigo no incluido en ActualizarRequest | Allow re-code via endpoint | Spec dice Codigo no modificable libremente |
| Repositorio Cargo | Expandir ICargoRepository con métodos write (como IUnidadOrganizativaRepository) | Repositorio separado | Seguir patrón existente; reemplaza el read-only actual |

## Data Flow

```
Controller → ICargoServicioComandos → ICargoRepository (write) / INivelCargoRepository (read)
                                       ↓
                            Cargo (Dominio) → DomainToPersistenceMapper → SgvDbContext
                                       ↑
                            CargoCommandResult ← invariante / validación error
```

```
NivelesCargoController → INivelCargoServicioConsulta → INivelCargoRepository → ReadOnlyRepository
```

Migración:
```
Pre-flight SELECT → fail-loud? → InsertData NivelesCargo + AddColumn NivelId(nullable)
                  → OK?      → Backfill NivelId from join → NOT NULL constraint → DROP COLUMN Nivel
```

## File Changes

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Dominio/Organizacion/NivelCargo.cs` | Crear | Entidad inmutable: Id, Codigo, Nombre, ValorNumerico, Orden |
| `src/SGV.Dominio/Organizacion/Cargo.cs` | Modificar | Reemplazar `Nivel` string por `NivelId` Guid; agregar `Desactivar()`, `Activar()`; hacer `Codigo` inmutable tras creación |
| `src/SGV.Aplicacion/Organizacion/Comandos/ICargoServicioComandos.cs` | Crear | Interfaz: CrearAsync, ActualizarAsync, DesactivarAsync, ReactivarAsync |
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs` | Crear | Implementación con validación, NivelId FK check, Codigo duplicate check |
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoCommandResult.cs` | Crear | Result type: CargoErrorType (NotFound, Conflict, Validation), CargoError, CargoCommandResult |
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs` | Crear | CrearCargoRequest, ActualizarCargoRequest (sin Codigo) |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/CrearCargoRequestValidator.cs` | Crear | FluentValidation: Codigo required(50), Nombre required(200), NivelId not empty, Descripcion 1000 |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs` | Crear | FluentValidation: Nombre required(200), NivelId not empty, Descripcion 1000 |
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoDto.cs` | Modificar | Reemplazar `Nivel` string por `NivelId` Guid + `NivelNombre` string |
| `src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs` | Modificar | Agregar métodos write: AddAsync, GetByIdForUpdateAsync, GetByIdIncludingDeletedAsync, UpdateAsync, DeleteAsync, ReactivateAsync, ExistsActiveCodeAsync |
| `src/SGV.Aplicacion/Organizacion/Consultas/INivelCargoServicioConsulta.cs` | Crear | ListAsync, GetByIdAsync |
| `src/SGV.Aplicacion/Organizacion/Consultas/NivelCargoServicioConsulta.cs` | Crear | Implementación del query service |
| `src/SGV.Aplicacion/Organizacion/Consultas/INivelCargoRepository.cs` | Crear | IReadOnlyRepository<NivelCargo> |
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/NivelCargoDto.cs` | Crear | Id, Codigo, Nombre, ValorNumerico, Orden |
| `src/SGV.Infraestructura/Persistencia/Entidades/NivelCargoEntity.cs` | Crear | EntityBase: Id, Codigo, Nombre, ValorNumerico(byte), Orden; sin IsActive/IsDeleted |
| `src/SGV.Infraestructura/Persistencia/Entidades/CargoEntity.cs` | Modificar | Reemplazar `Nivel` string por `NivelId` Guid + NivelCargoEntity navigation |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/NivelCargoConfiguracion.cs` | Crear | Tabla NivelesCargo, PK char(36), Codigo UNIQUE, check constraint ValorNumerico, Sin IsActive/IsDeleted |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs` | Modificar | Agregar FK NivelId → NivelesCargo con OnDelete(Restrict), índice IX_Cargos_NivelId; eliminar config de Nivel string |
| `src/SGV.Infraestructura/Persistencia/Catalogos/NivelCargoConstantes.cs` | Crear | Constantes Guid estáticas para 4 niveles (Directivo, ConducciónMedia, Operativo, Academico) |
| `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` | Modificar | Agregar NivelesCargo HasData usando NivelCargoConstantes; actualizar CargoEntity seed con NivelId |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Modificar | Actualizar mapper de Cargo (Nivel→NivelId, NivelCargo); agregar mapper de NivelCargo |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modificar | Agregar mapper Cargo domain→entity (NivelId, NivelCargoEntity); agregar mapper NivelCargo |
| `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs` | Modificar | Expandir con métodos write como UnidadOrganizativaRepository |
| `src/SGV.Infraestructura/Persistencia/Repositorios/NivelCargoRepository.cs` | Crear | ReadOnlyRepository<NivelCargoEntity, NivelCargo> con Include NivelCargo |
| `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` | Modificar | Agregar DbSet<NivelCargoEntity> NivelesCargo |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificar | Registrar ICargoServicioComandos, INivelCargoServicioConsulta, INivelCargoRepository |
| `src/SGV.Api/Controllers/CargosController.cs` | Modificar | Agregar POST, PUT, DELETE(soft), PATCH reactivar; inyectar ICargoServicioComandos |
| `src/SGV.Api/Controllers/NivelesCargoController.cs` | Crear | GET list, GET by id — read-only |
| `src/SGV.Infraestructura/Persistencia/Migraciones/*` | Crear | Migración EF Core: crear NivelesCargo, agregar NivelId FK, backfill, DROP Nivel |

## Interfaces / Contracts

### Domain: NivelCargo
```csharp
public sealed class NivelCargo : EntidadBase
{
    public string Codigo { get; private set; }  // max 50
    public string Nombre { get; private set; }   // max 100
    public byte ValorNumerico { get; private set; }
    public int Orden { get; private set; }
}
```

### Domain: Cargo (modificado)
```csharp
public sealed class Cargo : EntidadAuditable
{
    public string Codigo { get; private set; }   // inmutable tras creación
    public string Nombre { get; private set; }
    public string? Descripcion { get; private set; }
    public Guid NivelId { get; private set; }    // reemplaza string? Nivel
    public NivelCargo? NivelCargo { get; private set; }
    public bool IsActive { get; private set; }
    void Desactivar() => IsActive = false;
    void Activar() => IsActive = true;
}
```

### Application: CargoCommandResult
```csharp
public enum CargoErrorType { NotFound, Conflict, Validation }
public sealed record CargoError(CargoErrorType Type, string Code, string Message);
public sealed record CargoCommandResult(
    bool IsSuccess, CargoDto? Value, CargoError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);
```

### Application: Requests
```csharp
public sealed record CrearCargoRequest(string Codigo, string Nombre, Guid NivelId, string? Descripcion = null);
public sealed record ActualizarCargoRequest(string Nombre, Guid NivelId, string? Descripcion = null); // sin Codigo
```

### API: NivelesCargo (read-only)
- `GET /api/v1/niveles-cargo` → `IReadOnlyList<NivelCargoDto>`
- `GET /api/v1/niveles-cargo/{id:guid}` → `NivelCargoDto`
- Sin endpoints de escritura (405 si se intenta)

### API: Cargos (ampliado)
- `GET /api/v1/cargos` (existente, sin cambios)
- `GET /api/v1/cargos/{id:guid}` (existente, sin cambios)
- `POST /api/v1/cargos` → 201 / 400 / 409
- `PUT /api/v1/cargos/{id:guid}` → 200 / 400 / 404 / 409
- `DELETE /api/v1/cargos/{id:guid}` → 204 / 404 / 409
- `PATCH /api/v1/cargos/{id:guid}/reactivar` → 200 / 404 / 409

## Testing Strategy

| Capa | Qué probar | Enfoque |
|------|-----------|---------|
| Dominio | Invariantes de Cargo: Codigo obligatorio y longitud, NivelId requerido, Desactivar/Activar | Unit tests xUnit |
| Dominio | Invariantes de NivelCargo: Codigo, Nombre, ValorNumerico rango | Unit tests xUnit |
| Aplicación | CargoServicioComandos: crear, actualizar, desactivar, reactivar; errores (Codigo dup, NivelId inexistente, NotFound) | Unit tests con mocks (ICargoRepository, INivelCargoRepository, IUnitOfWork) |
| Aplicación | Validators: CrearCargoRequestValidator, ActualizarCargoRequestValidator | Unit tests FluentValidation |
| Aplicación | NivelCargoServicioConsulta: listar y obtener | Unit tests con mocks |
| Persistencia | CargoRepository: CRUD +ExistsActiveCode + Reactivate; NivelCargoRepository: read-only | Integration tests contra MySQL |
| Persistencia | NivelCargoConfiguracion: check constraint ValorNumerico, UNIQUE Codigo | Integration tests |
| Persistencia | Migración fail-loud: pre-flight detecta valores sucios | Integration test dedicado |
| Persistencia | DatosSemilla: NivelCargoConstantes coherencia entre migración InsertData y HasData | Unit test de drift |
| API | CargosController: POST/PUT/DELETE/PATCH reactivar + ProblemDetails | Integration tests (WebApplicationFactory) |
| API | NivelesCargoController: GET list/detail; escritura devuelve 405 | Integration tests |

## Migration / Rollout

Migración EF Core con fail-loud (REQ-SPA-EVOLUTION-001):

1. **Pre-flight**: `SELECT DISTINCT Nivel FROM Cargos WHERE Nivel IS NOT NULL AND Nivel NOT IN (...)` contra los 4 códigos seed (Directivo, ConduccionMedia, Operativo, Academico). Si hay valores ofensivos → `InvalidOperationException` listando los valores; no se ejecuta ningún ALTER.
2. **Criar tabla NivelesCargo**: `CreateTable` con PK, UNIQUE Codigo, check constraint ValorNumerico.
3. **InsertData seed**: 4 filas con Guids de `NivelCargoConstantes`.
4. **Agregar NivelId (nullable)**: `AddColumn NivelId char(36) NULL`.
5. **Backfill**: `UPDATE Cargos SET NivelId = (SELECT Id FROM NivelesCargo WHERE NivelesCargo.Codigo = Cargos.Nivel)`.
6. **NOT NULL constraint**: `AlterColumn NivelId NOT NULL`.
7. **FK + índice**: `AddForeignKey IX_Cargos_NivelId → NivelesCargo.Id OnDelete(Restrict)`.
8. **DROP COLUMN Nivel**: eliminar columna string legacy.

**Rollback**: migración inversa restaura columna Nivel string, elimina FK, elimina tabla NivelesCargo. Requiere que todos los Cargos tengan NivelId válido para reconstruir Nivel string.

**Seed NivelesCargo**: 4 niveles con Guids estáticos:
- `NivelCargoConstantes.DirectivoId` → Codigo="Directivo", ValorNumerico=1, Orden=1
- `NivelCargoConstantes.ConduccionMediaId` → Codigo="ConduccionMedia", ValorNumerico=2, Orden=2
- `NivelCargoConstantes.OperativoId` → Codigo="Operativo", ValorNumerico=3, Orden=3
- `NivelCargoConstantes.AcademicoId` → Codigo="Academico", ValorNumerico=4, Orden=4

## Open Questions

- [ ] Confirmar si los 4 niveles de cargo son suficientes o si se requieren más valores en el seed inicial.
- [ ] Confirmar si para desactivar un Cargo se debe verificar que no tenga Puestos activos asociados (defensa defensiva, no gestión de Puestos).