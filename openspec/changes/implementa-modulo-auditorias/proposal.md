# Proposal: Implementa el módulo de Auditorias

## Intent

El sistema ya persiste registros de auditoría mediante `AuditoriaSaveChangesInterceptor` e `IAuditoriaServicio`, pero carecer por completo de capacidad de consulta. Este change construye la capa de lectura: una API REST paginada con filtros y una interfaz web de solo lectura, ambas restringidas al rol `Administrador`.

**¿Por qué ahora?** Sin consulta de auditoría, las operaciones de negocio carecen de trazabilidad oficial. Los administradores no tienen forma de auditar quién creó/modificó/eliminó entidades del sistema.

## Scope

### In Scope
- API: `GET /api/v1/auditorias` (listado paginado con filtros) + `GET /api/v1/auditorias/{id}` (detalle)
- Web: `Pages/Auditorias/Index` con tabla paginada y filtros sidebar
- DTO `AuditoriaDto` con metadatos + `ChangedPropertiesJson` (sin `OldValuesJson` ni `NewValuesJson`)
- Query contract `AuditoriaListQuery` con filtros: `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId`
- Autorización: `[Authorize(Roles = RolesSgv.Administrador)]` en ambos accessos
- Contratos en `SGV.Contracts/Auditoria/` (carpeta propia, transversal)
- Servicio de consulta `IAuditoriaServicioConsulta` + `AuditoriaServicioConsulta`
- `AuditoriaApiClient` en `SGV.Web/Integration/`
- Tests unitarios del servicio y de integración del controller

### Out of Scope
- Modificación de la escritura existente (interceptor, servicio, tabla)
- Retención, purga o archival de registros de auditoría
- Exportación CSV/Excel
- Auditoría de la auditoría (consultas no se registran)
- Endpoint de detalle web individual (v1: solo listado con drill-down parcial via modal/expand)

## Capabilities

### New Capabilities
- `auditoria-query`: Consulta paginada y detalle de registros de auditoría del sistema. Expone metadatos (entidad, operación, fecha, usuario, propiedades modificadas) sin valores old/new. Accesible únicamente por rol `Administrador`.

### Modified Capabilities
- Ninguna. La escritura de auditoría existente no se modifica.

## Approach

**API:** `AuditoriasController` con `GET /api/v1/auditorias` y `GET /api/v1/auditorias/{id}`, siguiendo el patrón de controllers existentes. Filtros via query params (`EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId`, `Page`, `PageSize`). Respuesta envuelta en `PagedResult<AuditoriaDto>`.

**Web:** Razor Pages `Index` con tabla paginada (paginación server-side), sidebar de filtros, y un panel de detalle expandido. Ejecuta query via `AuditoriaApiClient`.

**Contracts:** Carpeta `SGV.Contracts/Auditoria/` con `AuditoriaDto` (campos: `Id`, `EntityName`, `EntityId`, `Operation`, `OccurredAt`, `UserId`, `ChangedPropertiesJson`, `CorrelationId`) y `AuditoriaListQuery`.

**Persistence:** Reutiliza `SgvDbContext.Auditorias` existente. `AuditoriaServicioConsulta` consulta directamente via EF Core sin repo intermedio (patrón del proyecto para queries simples).

**No se toca la escritura existente.** El interceptor `AuditoriaSaveChangesInterceptor`, el servicio `IAuditoriaServicio`/`AuditoriaServicio`, la entidad `AuditoriaEntity` y la tabla `Auditorias` quedan completamente fuera de alcance: ningún consumidor de escritura (`SetupServicio`, `PersonaServicioComandos`, `UsuarioServicioComandos`) se modifica. El nuevo módulo es puramente aditivo y de solo lectura.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Contracts/Auditoria/` | New | Carpeta con `AuditoriaDto.cs`, `AuditoriaListQuery.cs` |
| `src/SGV.Aplicacion/Auditoria/` | Modified | Se agrega `IAuditoriaServicioConsulta.cs`, `AuditoriaServicioConsulta.cs` |
| `src/SGV.Api/Controllers/AuditoriasController.cs` | New | Controller REST con endpoints de listado y detalle |
| `src/SGV.Web/Pages/Auditorias/` | New | `Index.cshtml`, `Index.cshtml.cs` (PageModel) |
| `src/SGV.Web/Integration/AuditoriaApiClient.cs` | New | Cliente tipado para consumo desde web |
| `tests/SGV.Tests/` | Modified | Tests del nuevo servicio y controller |
| `docs/decisiones-implementacion.md` | Modified | Documenta la decisión del módulo de auditoría |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| La tabla `Auditorias` crece indefinidamente; OFFSET degrada en tablas grandes | High | Diseñar con cursor pagination como mejora futura; v1 acepta OFFSET por ser MVP |
| Exposición inadvertida de PII en `OldValuesJson`/`NewValuesJson` | High | NO incluir estos campos en `AuditoriaDto`; solo `ChangedPropertiesJson` |
| Performance del deserializado JSON en listados | Medium | Enlistar solo metadatos en queries de listado; deserializar `ChangedPropertiesJson` solo en detalle |
| Ningún test existente para query de auditoría | Medium | Construir suite siguiendo patrones de tests existentes del proyecto |

## Rollback Plan

1. Revertir los archivos nuevos: eliminar `AuditoriasController`, `AuditoriaApiClient`, `Pages/Auditorias/`, `Contracts/Auditoria/`, y el servicio de consulta.
2. Eliminar los tests agregados.
3. Dejar intacta la escritura existente (`AuditoriaSaveChangesInterceptor`, `AuditoriaServicio`, `AuditoriaEntity`, tabla `Auditorias`).
4. La migración de EF Core no es necesaria (la tabla ya existe).

## Dependencies

- Tabla `Auditorias` ya existe con índices adecuados (`EntityName+EntityId+OccurredAt`, `UserId+OccurredAt`, `CorrelationId`).
- `IUsuarioActual` ya disponible para resolver el usuario actual en el servicio.

## Success Criteria

- [ ] `GET /api/v1/auditorias` devuelve `200` con paginación y filtros funcionales paraAdministradores autenticados
- [ ] `GET /api/v1/auditorias/{id}` devuelve `200` con detalle paraAdministradores autenticados
- [ ] `GET /api/v1/auditorias` devuelve `403` para usuarios autenticados sin rol `Administrador`
- [ ] `AuditoriaDto` contiene todos los campos declarados sin `OldValuesJson` ni `NewValuesJson`
- [ ] Página `Pages/Auditorias/Index` renderiza la tabla con filtros y paginación
- [ ] Suite de tests cubre el servicio de consulta y el controller
- [ ] `dotnet build SGV.slnx` y `dotnet test SGV.slnx` pasan sin errores
