# Proposal: Implementar el módulo de Vacantes

## Intent

Habilitar la gestión de vacantes de puestos a través de API REST y interfaz web, permitiendo crear, consultar, cambiar estado y cerrar vacantes. El dominio y la persistencia ya existen; falta la capa de aplicación, contratos wire, controller, repository y páginas web.

## Scope

### In Scope
- CRU de vacantes vía API REST: creación y consultas; el cambio de estado y cierre se realizan mediante PATCH de estado (`VacantesController`)
- Cambio de estado de vacante con registro en `HistorialEstadoVacante`
- Endpoint de catálogo `GET /api/v1/estados-vacante` (solo lectura, autenticado)
- Query segmentada: `abiertas | cerradas | todas`
- Mapper de dominio↔persistencia (`ToDomain`/`ToEntity`)
- Repository `VacanteRepository` con queries filtradas por segmento
- Servicios de aplicación (`IVacanteServicioComandos`, `IVacanteServicioConsulta`, `IEstadoVacanteServicioConsulta`)
- Contratos wire en `SGV.Contracts/Vacantes/`
- Páginas web: Index, Create, Edit, Details
- Menú lateral: entrada "Vacantes"
- Tests unitarios y de integración API
- Documentar bloque GUID `20000000-…` en `decisiones-implementacion.md`

### Out of Scope
- Gestión de postulaciones (existe `Postulacion` en dominio pero sin implementación)
- Compatibilidad por habilidades vacante↔persona
- Proceso de selección y evaluaciones
- Módulo web de creación desde detalle de puesto (PB-2)
- Editor de historial de estados en la web (PB-4)

## Capabilities

### New Capabilities
- `vacante-management`: creación y consultas de vacantes, cambio de estado con historial, query segmentada y catálogo de estados. Cada capability genera `openspec/specs/vacante-management/spec.md`.
- `vacante-web`: UI web para gestión de vacantes (Index, Create, Edit, Details) +ApiClient.

### Modified Capabilities
- Ninguna. Loscatálogos existentes (`EstadoVacante`) son solo lectura y no modifican specs previas.

## Approach

**Slice C (API-only como primer change SDD)** siguiendo el patrón del repo: `cargo-management` + `cargo-web-*` separados. Este change implementa la capa API completa (Contracts → Aplicacion → Infra → Api) con pages web básicas. El slice 2 (web completa desde puesto) viene en un change posterior.

- **Autorización propuesta**: GET/listados → `[Authorize]` (cualquier rol); mutaciones → `[Authorize(Roles = "Administrador,GestorVacantes")]` (pendiente PB-1)
- **Sin soft-delete**: Vacantes usan estados terminales (`Cubierta`, `Cancelada`). `FechaCierre` seteada automáticamente por `Vacante.CambiarEstado(..., cerrar: true)`.
- **Segmento**: `abiertas | cerradas | todas` (no `activas/eliminadas`)
- **Errores**: Usar `ErrorCategoria` canon (`VacanteCommandResult` con `Categoria: ErrorCategoria`)

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Contracts/Vacantes/` | New | DTOs, requests, CommandResult, VacanteApiRoutes |
| `src/SGV.Aplicacion/Vacantes/` | New | Servicios comandos/consultas + validadores |
| `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` | New | Repository completo |
| `src/SGV.Infraestructura/Persistencia/*Mapper.cs` | Modified | ToDomain/ToEntity para Vacante |
| `src/SGV.Api/Controllers/VacantesController.cs` | New | Controller REST |
| `src/SGV.Api/Program.cs` | Modified | Registro de servicios |
| `src/SGV.Web/Integration/Vacantes/` | New | ApiClient + ViewModels |
| `src/SGV.Web/Pages/Organizacion/Vacantes/` | New | Index, Create, Edit, Details |
| `src/SGV.Web/Pages/Shared/_Sidenav.cshtml` | Modified | Item de menú |
| `docs/decisiones-implementacion.md` | Modified | Registrar bloque GUID `20000000-…` |
| `tests/SGV.Tests/` | New | Tests unitarios + integración |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| R-1: Colección `_vacantes` en `Puesto` no se hidrata automáticamente | Medium | Eager loading con `Include`+`ThenInclude` en repository |
| R-2: `HistorialEstadoVacante` puede quedar inconsistente si falla persistencia | Low | UnitOfWork transaccional; mismo `SaveChangesAsync` para vacante+historial |
| R-3: `GestorVacantes` sin spec de permisos | Medium | Definir en spec con decisión de PB-1 confirmada |
| R-4: Sin tests existentes de Vacantes | Medium | Crear tests desde cero en este change |
| R-5: Índice único activo por puesto no requerido (a diferencia de Cargos/Puestos) | Low | Explícito en spec; sin índice adicional necesario |

## Rollback Plan

1. Revertir commit del change en la rama feature
2. Si hay migración EF, crear migración inversa con `dotnet ef migrations remove`
3. Desregistrar servicios en `Program.cs`
4. Eliminar `src/SGV.Contracts/Vacantes/`, `src/SGV.Aplicacion/Vacantes/`, `VacanteRepository.cs`
5. Eliminar `VacantesController.cs` y páginas web creadas
6. Restaurar `decisiones-implementacion.md` si se tocó el mapa de bloques GUID

## Dependencies

- Dominio: `Vacante`, `EstadoVacante`, `HistorialEstadoVacante` ya existen
- Persistencia: `VacanteEntity`, `EstadoVacanteEntity`, `HistorialEstadoVacanteEntity` ya existen
- Seed: 4 estados de vacante ya sembrados (bloque `20000000-…`)
- Rol `GestorVacantes` ya existe en `RolesSgv`
- `ErrorCategoria` y `ApiResults` ya definidos (commandresult-error-taxonomy)

## Success Criteria

- [ ] `dotnet build SGV.slnx` compila sin errores
- [ ] `dotnet test SGV.slnx` pasa (todos los layers)
- [ ] `GET /api/v1/estados-vacante` retorna los 4 estados seed
- [ ] Creación, consultas y cambio de estado de vacantes vía API funcionan con autenticación
- [ ] Cambio de estado registra en `HistorialEstadoVacante`
- [ ] Query segmentada (`abiertas|cerradas|todas`) excluye mezclas de segmento
- [ ] Páginas web Index/Create/Edit/Details cargan y persistén correctamente
- [ ] Menú "Vacantes" visible en `_Sidenav.cshtml` para usuarios autenticados
- [ ] Bloque GUID `20000000-…` documentado en `decisiones-implementacion.md`

## Proposal Question Round

Las siguientes decisiones de negocio deben resolverse antes de specs/design:

**PB-1 — Permisos de GestorVacantes**: ¿Las mutaciones (crear, editar estado, cerrar) son solo para `Administrador`, o también para `GestorVacantes`?

**PB-2 — Creación desde la web**: ¿Se necesita un botón "Crear Vacante" en el detalle de un puesto, o solo desde el módulo de Vacantes?

**PB-3 — FechaCierre obligatoria**: ¿Debe el sistema validar que el campo `Motivo` sea obligatorio al cerrar una vacante?

**PB-4 — Historial visible en web**: ¿Se necesita mostrar el `HistorialEstadoVacante` en la página Details de la vacante web?

**PB-5 — Segmento por defecto**: ¿El listado de vacantes abiertas (`abiertas`) es la vista por defecto, igual que `activas` en Ocupaciones/Cargos?
