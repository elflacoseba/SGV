# Exploration: Implementar el módulo de Vacantes

## 1. Estado actual del módulo Vacantes

### Lo que existe ( dominio y persistencia )

| Capa | Artefacto | Estado |
|------|-----------|--------|
| **Dominio** | `SGV.Dominio/Vacantes/Vacante.cs` | Entidad `Vacante : EntidadAuditable` con `PuestoId`, `EstadoVacanteId`, `FechaApertura`, `FechaCierre`, `Motivo`, `Observaciones` y método `CambiarEstado(...)` |
| **Dominio** | `SGV.Dominio/Vacantes/EstadoVacante.cs` | Entidad `EstadoVacante : EntidadBase` con `Codigo`, `Nombre`, `Orden`, `EsTerminal` |
| **Dominio** | `SGV.Dominio/Vacantes/HistorialEstadoVacante.cs` | Entidad `HistorialEstadoVacante : EntidadBase` con la tripleta `EstadoAnteriorId → EstadoNuevoId`, `ChangedAt`, `ChangedByUserId`, `Motivo` |
| **Dominio** | `Puesto.Vacantes` | Colección `_vacantes` ya existe en `Puesto.cs` línea 10 y 55 |
| **Persistencia** | `VacanteEntity`, `EstadoVacanteEntity`, `HistorialEstadoVacanteEntity` | Entidades Entity Framework ya definidas |
| **Persistencia** | `VacanteConfiguracion`, `EstadoVacanteConfiguracion`, `HistorialEstadoVacanteConfiguracion` | Configuraciones EF ya creadas (sin índices únicos de soft-delete aún) |
| **Persistencia** | `SgvDbContext` | `DbSet<EstadoVacanteEntity>`, `DbSet<VacanteEntity>`, `DbSet<HistorialEstadoVacanteEntity>` ya registrados (líneas 34-38) |
| **Persistencia** | `DatosSemilla` | 4 estados de vacante seeded: `VacanteAbiertaId`, `VacanteEnSeleccionId`, `VacanteCubiertaId`, `VacanteCanceladaId` (bloque GUID `20000000-...`) |
| **Seguridad** | `RolesSgv.GestorVacantes` | Rol existente en `SGV.Contracts/Seguridad/RolesSgv.cs` línea 9 |

### Lo que NO existe (por construir)

| Capa | Artefacto faltante |
|------|---------------------|
| **Aplicacion** | `IVacanteRepository` / `VacanteRepository` |
| **Aplicacion** | `IVacanteServicioConsulta` / `VacanteServicioConsulta` |
| **Aplicacion** | `IVacanteServicioComandos` / `VacanteServicioComandos` |
| **Aplicacion** | `IEstadoVacanteServicioConsulta` / `EstadoVacanteServicioConsulta` |
| **Aplicacion** | Validadores FluentValidation (`CrearVacanteRequestValidator`, etc.) |
| **Contracts** | `VacanteCommandResult`, `VacanteError`, `VacanteErrorType` |
| **Contracts** | `CrearVacanteRequest`, `ActualizarVacanteRequest`, `CambiarEstadoVacanteRequest` |
| **Contracts** | `VacanteDto`, `VacanteDetailDto`, `VacanteListQuery`, `VacanteSegmentoListado` |
| **Contracts** | `EstadoVacanteDto` (catálogo) |
| **Contracts** | `VacanteApiRoutes` |
| **Contracts** | `[Obsolete]` `VacanteErrorType` + `ErrorCategoria` migration (pattern de #125) |
| **API** | `VacantesController` |
| **API** | Registro de servicios en `Program.cs` |
| **Infraestructura** | `PersistenceToDomainMapper.ToDomain(VacanteEntity)` |
| **Infraestructura** | `DomainToPersistenceMapper.ToEntity(Vacante)` |
| **Web** | `IVacanteApiClient` / `VacanteApiClient` en `SGV.Web/Integration/` |
| **Web** | `SGV.Web/Pages/Organizacion/Vacantes/Index.cshtml` y `.cs` |
| **Web** | `SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml` y `.cs` |
| **Web** | `SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml` y `.cs` |
| **Web** | `SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml` y `.cs` |
| **Web** | `_Sidenav.cshtml` — entrada de menú "Vacantes" |
| **Tests** | Tests unitarios de servicios, tests de integración API |

---

## 2. Entidades domain relevantes ya existentes

### Vacante (dominio)

```csharp
public sealed record class Vacante : EntidadAuditable
{
    //ctor: Vacante(puestoId, estadoVacanteId, fechaApertura, motivo)
    public Guid PuestoId { get; private set; }
    public Puesto Puesto { get; private set; }
    public Guid EstadoVacanteId { get; private set; }
    public EstadoVacante EstadoVacante { get; private set; }
    public DateTime FechaApertura { get; private set; }
    public DateTime? FechaCierre { get; private set; }
    public string Motivo { get; private set; }
    public string? Observaciones { get; private set; }
    public IReadOnlyCollection<HistorialEstadoVacante> HistorialEstados => _historialEstados;
    public IReadOnlyCollection<Postulacion> Postulaciones => _postulaciones;
    // Método: CambiarEstado(estadoNuevoId, usuarioId?, motivo?, fecha?, cerrar)
}
```

### EstadoVacante seed (datos conocidos)

| GUID | Código | Nombre | ¿Terminal? |
|------|--------|--------|------------|
| `20000000-0000-0000-0000-000000000001` | `Abierta` | Abierta | No |
| `20000000-0000-0000-0000-000000000002` | `EnSeleccion` | En Selección | No |
| `20000000-0000-0000-0000-000000000003` | `Cubierta` | Cubierta | Sí |
| `20000000-0000-0000-0000-000000000004` | `Cancelada` | Cancelada | Sí |

### Modelo de datos EF ya existente (VacanteEntity)

```
VacanteEntity: AuditableEntityBase
  - PuestoId (Guid, FK)
  - EstadoVacanteId (Guid, FK)
  - FechaApertura (DateTime)
  - FechaCierre (DateTime?)
  - Motivo (string 500)
  - Observaciones (string? 1000)
  - HistorialEstados (List<HistorialEstadoVacanteEntity>)
  - Postulaciones (List<PostulacionEntity>)

HistorialEstadoVacanteEntity: EntityBase
  - VacanteId (Guid, FK)
  - EstadoAnteriorId (Guid?)
  - EstadoNuevoId (Guid, FK)
  - ChangedAt (DateTime)
  - ChangedByUserId (string? 450)
  - Motivo (string? 500)

EstadoVacanteEntity: EntityBase
  - Codigo (string 50, unique)
  - Nombre (string 100)
  - Orden (int)
  - EsTerminal (bool)
```

---

## 3. Patrones reutilizables identificados

### 3.1 Arquitectura de módulo completo (patrón Ocupaciones/Cargos/Puestos)

Cada módulo sigue el mismo esquema de capas:

```
Contracts (wire-types)
  ├── Comandos/  → Request records + CommandResult + ErrorType enum
  ├── Consultas/  → DTOs + Query records + PagedResult<T>
  └── Enums/      → Segmentos de listado

Aplicacion
  ├── Comandos/   → IServicioComandos + ServicioComandos + Validaciones/
  └── Consultas/  → IServicioConsulta + IServicioRepository + ServicioConsulta

Infraestructura
  └── Persistencia/Repositorios/  → Repository concrete impl

API
  └── Controllers/  → Controller con [Authorize], [ProducesResponseType] por acción

Web
  ├── Pages/  → Index, Create, Edit, Details
  └── Integration/  → ApiClient + InputModel + ViewModel
```

### 3.2 CommandResult con ErrorCategoria (taxonomía #125)

Patrón ya establecido en Ocupaciones (aunque marcado `[Obsolete]`). El nuevo Vacantes debe usar `ErrorCategoria` directamente como en `CargoCommandResult` con `Categoria: ErrorCategoria = ErrorCategoria.Unexpected` por default.

### 3.3 Soft-delete vs. estado terminal

`Vacante` no usa soft-delete: `FechaCierre` es `null` mientras está abierta y se setea cuando `cerrar = true` en `CambiarEstado`. Los estados `Cubierta` y `Cancelada` son **terminales** (`EsTerminal = true`). No se requiere patrón de índice único con columna generada para Vacante (a diferencia de Cargos/Puestos).

### 3.4 Autorización

- GET (`List`, `GetById`, `Query`) → `[Authorize]` (cualquier rol autenticado)
- POST / PUT / PATCH / DELETE → `[Authorize(Roles = RolesSgv.Administrador + "," + RolesSgv.GestorVacantes)]`

Esto es una **decisión de negocio pendiente**: ¿`GestorVacantes` puede crear/cerrar vacantes o solo consultarlas? El rol existe y fue sembrado, pero no hay spec que documente sus permisos. **Pregunta de negocio clave.**

### 3.5 Catálogo de estados

`EstadoVacante` es un catálogo **no mutable** (solo lectura), similar a `NivelCargo` y `TipoUnidadOrganizativa`. No requiere CRUD completo; solo un endpoint `GET /api/v1/estados-vacante` autenticado.

---

## 4. Áreas afectadas por la implementación

| Archivo / Carpeta | Por qué se toca |
|--------------------|-----------------|
| `src/SGV.Contracts/` | Nueva subcarpeta `Vacantes/` con todos los DTOs de request/response |
| `src/SGV.Aplicacion/Vacantes/` | Nuevo folder; servicios de comandos y consultas completos |
| `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/` | FluentValidators para cada request |
| `src/SGV.Aplicacion/DependencyInjection.cs` | Registrar `IEstadoVacanteServicioConsulta` y `IVacanteServicioComandos` |
| `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` | Repository completo con Query + soft-delete implícito |
| `src/SGV.Infraestructura/Persistencia/PersistenceToDomainMapper.cs` | `ToDomain(VacanteEntity)`, `ToDomain(EstadoVacanteEntity)`, `ToDomain(HistorialEstadoVacanteEntity)`, `ToEntity(Vacante)` |
| `src/SGV.Api/Controllers/VacantesController.cs` | Controller REST con 8-10 endpoints |
| `src/SGV.Api/Program.cs` | Registro de servicios de Vacantes |
| `src/SGV.Web/Integration/Vacantes/` | `IVacanteApiClient`, `VacanteApiClient`, ViewModels, InputModels |
| `src/SGV.Web/Pages/Organizacion/Vacantes/` | Index, Create, Edit, Details + PageModels |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Agregar grupo "Vacantes" al menú lateral |
| `tests/SGV.Tests/` | Tests unitarios de servicios, integración API |

---

## 5. Preguntas de negocio pendientes de clarificar

### PB-1: Permisos de `GestorVacantes` vs `Administrador`

El rol `GestorVacantes` existe sembrado. ¿Las mutaciones (crear, editar estado, cerrar vacante) son:
- Solo `Administrador` (como Ocupaciones)?
- `Administrador` + `GestorVacantes`?
- Solo `GestorVacantes`?

**Impacto**: cambia los `[Authorize(Roles = ...)]` del controller.

### PB-2: ¿Las vacantes se crean desde la web o solo desde la API?

El flujo actual de `PuestoOcupaciones.cshtml` sugiere que las vacantes podrían crearse desde el contexto de un puesto. ¿Se necesita un botón "Crear Vacante" en el detalle de un puesto?

### PB-3: ¿`FechaCierre` es obligatoria o se calcula automáticamente?

Cuando se cambia a estado terminal (`Cubierta` o `Cancelada`), `Vacante.CambiarEstado(..., cerrar: true)` setea `FechaCierre`. Pero no hay validación en el dominio que impida cerrar sin motivo. ¿Se requiere?

### PB-4: ¿Se necesita historial de cambios de estado visible en la web?

`HistorialEstadoVacante` ya existe en el dominio. ¿Se muestra en `Details.cshtml` de la vacante?

### PB-5: ¿El endpoint de listado es segmentado (`activas` / `cerradas` / `todas`)?

Patrón establecido en Ocupaciones/Cargos/Puestos usa `status=activas|eliminadas`. Para Vacantes el concepto de "eliminada" no aplica — en su lugar: `status=abiertas|cerradas| todas`. Esto requiere definir el enum `VacanteSegmentoListado` con los valores correctos.

---

## 6. Enfoques de implementación

### Enfoque A — Módulo Vacantes completo (CRUD + Web) como un solo change SDD

Se implementa todo el módulo en un solo ciclo SDD (proposal → spec → design → tasks → apply → verify).

- **Pros**: Cohesión temporal, un solo PR grande, visibilidad completa del módulo.
- **Cons**: Alto riesgo de scope creep, 400+ líneas de cambio, revisión difícil.
- **Esfuerzo**: Alto.

### Enfoque B — Slice 1: API vacía + Contracts + Dominio Aplicacion (sin Web)

Primero se implementa la API REST completa con contratos, servicios y repository. El módulo web se hace en un change posterior.

- **Pros**: Separación de concerns, PR más pequeño, permite probar la API antes del frontend.
- **Cons**: Módulo incompleto visible en la web (sin páginas).
- **Esfuerzo**: Medio-Alto.

### Enfoque C — Slice 1: VacantesController + Contracts + Aplicacion + Repository (API básica)

Solo CRUD de vacantes sin subrecursos ni web. Similar a cómo se hizo `cargo-management` vs `cargo-web-crear-editar`.

- **Pros**: PR pequeño, alineado con el patrón de otros módulos (cargo-management + cargo-web-*).
- **Cons**: La web no puede gestionar vacantes hasta un segundo change.
- **Esfuerzo**: Medio.

---

## 7. Recomendación

**Enfoque C (Slice 1 API-only)** como punto de partida, por las siguientes razones:

1. El patrón de este repo es separar el change de gestión API (`cargo-management`) del change de UI web (`cargo-web-crear-editar`, `cargo-web-listado-detalle-baja`).
2. Permite definir los contratos wire con `ErrorCategoria` desde el inicio.
3. El slice 2 web puede usar la API ya funcionando como consumidor.
4. Las preguntas de negocio PB-1 a PB-5 se resuelven en la fase de spec sin bloquear la implementación de la capa de datos.

---

## 8. Riesgos identificados

| ID | Riesgo | Mitigación |
|----|--------|------------|
| R-1 | **Vacante no tiene soft-delete**: no hay índice `ActiveCodigoUnique` como en Cargo/Puesto. Si se necesita unicidad activa de código de vacante por puesto, hay que agregarlo. | Definir en spec si se necesita; si no, la unicidad es por `(PuestoId, FechaApertura)` implícita. |
| R-2 | **`Puesto.Vacantes` collection navigation**: la colección `_vacantes` existe en `Puesto` pero no se hidrata automáticamente en queries. El `VacanteRepository` debe incluir `Include(v => v.Puesto)` + `ThenInclude` de `Puesto.UnidadOrganizativa` y `Puesto.Cargo` para los DTOs. | Asegurar que el mapper hace eager loading de relaciones. |
| R-3 | **Bloque GUID `20000000-...`** reservado para Vacantes. No hay conflictos con los bloques existentes (`60000000` UO, `70000000` NivelCargo, `71000000` TipoDocumento, `72000000` CategoriaHabilidad). El bloque de Vacantes no está documentado en `decisiones-implementacion.md`. | Agregar entrada `20000000-…` al mapa de bloques GUID en `decisiones-implementacion.md` durante apply. |
| R-4 | **Endpoint de catálogos**: `EstadoVacante` es un catálogo de solo lectura. No requiere controller CRUD completo — solo `GET /api/v1/estados-vacante`. No hay spec ni precedent directopara catalog-only controllers en este proyecto (los otros catálogos como `NivelesCargoController` usan `[Authorize]` simple). | Crear un endpoint `GET /api/v1/estados-vacante` simple en `VacantesController` o un `EstadosVacanteController` dedicado. |
| R-5 | **Transacciones y `HistorialEstadoVacante`**: `CambiarEstado` agrega al historial en memoria. Si la operación de persistencia del historial falla, la vacante queda en estado inconsistente. El `UnitOfWork` debe persitir ambos en la misma transacción. | Verificar que `VacanteRepository` incluye `_historialEstados` en el tracking de EF Core (collections navigation additive). |
| R-6 | **Sin tests existentes**: no hay suite de tests para Vacantes. Cualquier implementación debe crear tests desde cero. El gate de 3 corridas aplica. | Crear tests de unidad para los servicios y tests de integración API desde el inicio. |

---

## 9. Dependencias y orden de implementación sugerido

```
1. Contracts/Vacantes/
   └── DTOs, Requests, CommandResult, VacanteApiRoutes

2. Infra: PersistenceToDomainMapper (ToDomain para VacanteEntity + HistorialEstadoVacanteEntity)
   + DomainToPersistenceMapper (ToEntity para Vacante)
   + VacanteRepository

3. Aplicacion/Vacantes/
   ├── Consultas/  IEstadoVacanteServicioConsulta + EstadoVacanteServicioConsulta
   ├── Comandos/   IVacanteServicioComandos + VacanteServicioComandos + Validaciones/

4. API: VacantesController ( endpoints CRUD + cambio de estado + GET catálogo estados )
   + Program.cs (registro de servicios)

5. Web: IVacanteApiClient + VacanteApiClient + Pages/Index + Pages/Create + Pages/Details
   + _Sidenav.cshtml (ítem de menú)

6. Tests: unit tests VacanteServicioComandos + integración VacantesController
```

---

## 10. Decisiones de implementación tomadas en esta exploración

- **No soft-delete**: `Vacante` usa estados terminales (`Cubierta`, `Cancelada`) en lugar de soft-delete. No se requiere índice único con columna generada.
- **`GestorVacantes + Administrador` en mutaciones**: Se propone que ambos roles puedan hacer mutaciones, pero queda sujeto a confirmación de negocio (PB-1).
- **Catálogo de estados GET público (authenticado)**: `GET /api/v1/estados-vacante` sin restricciones de rol.
- **Segmento de listado**: `abiertas | cerradas | todas` (no `activas/eliminadas`).
- **FechaCierre automática**: seteada por `Vacante.CambiarEstado(..., cerrar: true)` al transicionar a estado terminal.
- **Exploración guardada en**: `openspec/changes/feature-implementar-modulo-vacantes/exploration.md` + Engram `sdd/feature-implementar-modulo-vacantes/explore`.
