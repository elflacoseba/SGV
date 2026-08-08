# Design: vacante-ocupacion-flow-alignment

## Resumen ejecutivo

El change alinea los flujos `Puesto → Vacante → Ocupacion` materializando la FK latente (`Ocupacion.VacanteId`) y endureciendo dos servicios de aplicación con checks cruzados: `VacanteServicioComandos` (N1 rechaza creación si el Puesto tiene Ocupacion activa; N2 materializa la `Ocupacion` al transicionar a `Cubierta`) y `OcupacionServicioComandos` (N3 rechaza alta directa sin Vacante abierta; Q2 bloquea reactivación si la Vacante vinculada está Cancelada).

La pieza más sensible es **N2**: `CambiarEstadoAsync` deja de ser una transición con histórico y pasa a ser transición + histórico + creación atómica de `Ocupacion`. Todo bajo la misma transacción EF (`IUnitOfWork.SaveChangesAsync`), sin new behavior de `BeginTransaction` porque `IUnitOfWork` no lo expone hoy y el wrapper EF cubre el chequeo.

**Corrección crítica al contexto recibido**: los métodos `ExistsActiveByPuestoAsync` en `IOcupacionRepository` y `ExistsAbiertaByPuestoAsync` en `IVacanteRepository` **ya existen** — los services los reusan sin tocar la interfaz. El design no agrega métodos de repositorio nuevos. El único contrato nuevo es `CambiarEstadoVacanteRequest.PersonaId` (opcional, obligatorio si destino es terminal `Cubierta`).

## Decisiones arquitectónicas

### D-1: N1 — check cruzado en `VacanteServicioComandos.CrearAsync`

**Decisión**: inyectar `IOcupacionRepository` en `VacanteServicioComandos` y, antes del check `ExistsAbiertaByPuestoAsync`, llamar `ocupacionRepository.ExistsActiveByPuestoAsync(request.PuestoId, excludingId: null)`. Si existe, retornar `409 PuestoOcupado` (`ErrorCategoria.Conflict`).

| Alternativa | Tradeoff | Decisión |
|---|---|---|
| Check vía BD (constraint nueva) | Detecta carrera pero acopla dominio a infra | Rechazada — el dominio gobierna; el TOCTOU ya está asumido en `ExistsAbiertaByPuestoAsync` |
| Método nuevo `ExistsActiveOcupacionByPuestoAsync` | Duplica firma existente | Rechazada — usa `ExistsActiveByPuestoAsync(excludingId: null)` que ya existe |

**Consecuencias**: 
- + Reutiliza contrato existente, sin cambios en `IOcupacionRepository`.
- − `VacanteServicioComandos` adquiere nueva dependencia (cruce de frontera de servicio).
- Mitigación: composición en el composition root `SGV.Api`; tests ya tienen `FakeOcupacionWriteRepository` reutilizable.

### D-2: N2 — crear Ocupacion al Cubrir

**Decisión (wire shape resuelta)**: extender `CambiarEstadoVacanteRequest` con `Guid? PersonaId`. La validación corre en el dominio/application: si `estadoNuevo.EsTerminal && estadoNuevo.Nombre == "Cubierta"` (o marcador equivalente) y `PersonaId is null`, retornar `400 ErrorCategoria.Validation` con `FieldErrors["personaId"]` y `VacanteErrorCodigo.PersonaIdRequeridoParaCubrir`. Se **descarta** el sub-recurso `POST /api/v1/vacantes/{id}/cubrir`.

| Alternativa | Tradeoff | Decisión |
|---|---|---|
| `PersonaId` opcional en `CambiarEstadoVacanteRequest` | Consistencia con PATCH existente; atomicidad simple | Elegida |
| `POST /cubrir` sub-recurso | Más autodescriptivo pero duplica endpoint; rompe simetría | Rechazada |

**Asunción operativa**: el `PersonaId` llega en el request. El módulo de Selección (Non-Goal) será el caller futuro.

**Atomicidad**: la creación de `Ocupacion` (`vacante.PuestoId`, `request.PersonaId`, `VacanteId = vacante.Id`, `FechaInicio = DateTime.UtcNow`, `EsVigente = true`), la transición de Vacante y el histórico se persisten en el mismo `SaveChangesAsync`. Si la `Ocupacion` viola `ActivePuestoIdUnique` o `ActivePersonaPuestoUnique` (TOCTOU), el `DbUpdateException` queda atrapado por el `constraintDetector` y se revierte todo.

### D-3: N3 — check cruzado en `OcupacionServicioComandos.CrearAsync`

**Decisión**: inyectar `IVacanteRepository` en `OcupacionServicioComandos`. Después de los checks `Persona`, `Puesto` y antes de `ExistsActiveByPersonaYPuestoAsync`, invocar `vacanteRepository.ExistsAbiertaByPuestoAsync(request.PuestoId)`. Si false, retornar `409 PuestoSinVacanteAbierta`.

**Orden de checks**: personas/puestos activos → **N3 Vacante abierta** → `PersonaYPuestoOcupados` → `PuestoOcupado`. N3 va primero porque la ausencia de Vacante es condición previa de toda Ocupacion nueva; los de unicidad son colisiones sobre la Vacante existente.

### D-4: Q2 — Reactivar chequea Vacante no Cancelada

**Decisión**: en `OcupacionServicioComandos.ReactivarAsync`, tras cargar la `ocupacion`, si `ocupacion.VacanteId is not null`, fetch `vacanteRepository.GetByIdForUpdateAsync(ocupacion.VacanteId.Value)`. Si no existe (FK rota histórica), permitir la reactivación (la Vacante fue purgada o migración pre-N2). Si existe y `estadoVacante.Nombre == "Cancelada"` (o un criterio de estado terminal Cancelada distinguible de Cubierta), retornar `409 VacanteCanceladaParaReactivar`.

**Open question anterior resuelta**: Q4 (Cancelar Vacante deja Ocupaciones independientes) se preserva — el check Q2 **sólo** dispara en `ReactivarAsync`, no en `Finalizar` ni en `Eliminar`.

## Open questions resueltas

### NAV-007 destino

`src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` (verificado: las Pages viven bajo `Organizacion/Vacantes/`, no `Pages/Vacantes/`). El link usa `puestoId` como query param; si llega presente, el `PageModel` precarga el dropdown de Puesto y bloquea su edición. Retorno seguro al `Puesto Details` vía `returnUrl`. El botón se renderiza solo si `!vacanteRepository.ExistsAbiertaByPuestoAsync(puestoId)` y el usuario es `Administrador` (Q5 N3 absoluto: nunca se ofrece si ya hay abierta o hay Ocupacion activa).

### N2 wire shape

`PersonaId?` opcional en `CambiarEstadoVacanteRequest`. Validación en application layer: obligatorio si destino `EsTerminal` y nombre `Cubierta`. Ver D-2.

### FORM-009 hints

Texto plano + link Razor interno al `Create` de Vacantes. Sin link externo a Swagger (el usuario Administrador no consume OpenAPI). Sección info bajo el selector `PuestoId` con frase: "El alta directa requiere Vacante abierta para el Puesto. Use el módulo de Vacantes para el flujo principal." + `asp-page="/Organizacion/Vacantes/Create" asp-route-puestoId="@Model.PuestoId"`.

## Cambios en capas

| Archivo | Acción | Descripción |
|------|--------|-------------|
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Modify | Agregar `VacanteId` (Guid?), nav `Vacante?`, parámetro opcional en constructor, en `Reconstitute` |
| `src/SGV.Infraestructura/Persistencia/Entidades/OcupacionEntity.cs` | Modify | Agregar `VacanteId` + nav `VacanteEntity` |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` | Modify | `HasOne(Vacante).WithMany().HasForeignKey(VacanteId).OnDelete(Restrict)` + índice no único sobre `VacanteId` |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modify | `ToEntity(Ocupacion)` y `UpdateEntity` mapean `VacanteId` |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Modify | `ToDomain(OcupacionEntity)` mapea `VacanteId` a `Reconstitute` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/*_AddVacanteIdToOcupaciones.cs` | Create | `AddColumn<Guid>("VacanteId")` nullable + `CreateIndex` + `AddForeignKey` `Restrict` |
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modify | N1 (`ExistsActiveByPuestoAsync`) + N2 (crear `Ocupacion` al Cubrir); constructor recibe `IOcupacionRepository` |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | Modify | N3 (`ExistsAbiertaByPuestoAsync`) + Q2 (check Vacante al Reactivar); constructor recibe `IVacanteRepository` |
| `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs` | Modify | Agregar `Guid? PersonaId = null` |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` | Modify | `PuestoOcupado`, `PersonaIdRequeridoParaCubrir` |
| `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionErrorCodigo.cs` | Modify | `PuestoSinVacanteAbierta`, `VacanteCanceladaParaReactivar` |
| `src/SGV.Api/Controllers/VacantesController.cs` | Modify | Mapping de `PersonaId` ya viene del request; sin cambio estructural (el 400 Validation lo produce el service) |
| `src/SGV.Api/Controllers/OcupacionesController.cs` | Modify | Sin cambio estructural (Q2 lo produce el service) |
| `src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml(.cs)` | Modify | Acción "Abrir Vacante" condicional (NAV-007) + derivación al detalle de Ocupacion si hay activa (NAV-006) |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml(.cs)` | Modify | Precargar `PuestoId` desde query param; bloquear edición del dropdown si vino precargado |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml` | Modify | Hint de flujo normal (FORM-009) |

**No aplica**: agregar métodos a `IOcupacionRepository` o `IVacanteRepository` — los necesarios ya existen.

## Errores: nuevos códigos

| Código | Enum contenedor | HTTP | Contexto |
|---|---|---|---|
| `PuestoOcupado` | `VacanteErrorCodigo` | 409 | N1 — Crear Vacante sobre Puesto con Ocupacion activa |
| `PersonaIdRequeridoParaCubrir` | `VacanteErrorCodigo` | 400 | N2 — Transicionar a Cubierta sin `PersonaId` |
| `PuestoSinVacanteAbierta` | `OcupacionErrorCodigo` | 409 | N3 — Crear Ocupacion sin Vacante abierta en el Puesto |
| `VacanteCanceladaParaReactivar` | `OcupacionErrorCodigo` | 409 | Q2 — Reactivar Ocupacion cuya Vacante asociada está Cancelada |

> `OcupacionErrorCodigo.PuestoOcupado` (409 unicidad) ya existe y **no se reutiliza** para N1 — el delta spec de `vacante-management` introduce un código paralelo `PuestoOcupado` en `VacanteErrorCodigo` para discriminar el bloqueo de Ocupacion activa.

## Data Flow (N2 — path crítico)

```
PATCH /api/v1/vacantes/{id}/estado { EstadoVacanteId=Cubierta, PersonaId }
        │
        ▼
VacantesController → VacanteServicioComandos.CambiarEstadoAsync
        │
        ├── validar request (FluentValidation) → PersonaId? Cabría validar contra destino Cubierta
        ├── GetByIdForUpdateAsync(id)              [tracked Vacante]
        ├── estadoActual ≠ null, no terminal
        ├── estadoNuevo = GetById(EstadoVacanteId)
        │
        ├── if estadoNuevo.EsTerminal && PersonaId is null
        │       → 400 PersonaIdRequeridoParaCubrir
        │
        ├── historial = vacante.CambiarEstado(...) (dominio)
        │
        ├── if estadoNuevo == Cubierta:
        │     ocupacion = new Ocupacion(personaId, vacante.PuestoId,
        │                               DateTime.UtcNow, Permanente,
        │                               VacanteId: vacante.Id)
        │     await ocupacionRepository.AddAsync(ocupacion)
        │
        ├── vacanteRepository.RegistrarCambioEstadoAsync(vacante, historial)
        └── unitOfWork.SaveChangesAsync()           [1 transacción EF]
                    │
                    ▼  (si falla ActivePuestoIdUnique o ACTIVE_...)
          DbUpdateException ↓ constraintDetector → 409 DatosInvalidos (rollback atómico)
```

## Migración de datos

- **Forward-only**, sin backfill. `Ocupaciones` históricas quedan con `VacanteId = NULL`.
- La columna es nullable; los índices `ActivePuestoIdUnique` / `ActivePersonaPuestoUnique` (sobre columna computed) no se ven afectados — `NULL` múltiple permitido en `UNIQUE` MySQL.
- FK `ON DELETE RESTRICT`: borrar una Vacante con Ocupaciones derivadas falla. Q4 permite baja administrativa de Vacante Cancelada, pero N4 dice que la posición sólo se libera al finalizar/eliminar la Ocupacion derivada — el `RESTRICT` es coherente.
- Regenerar `docs/migracion-inicial-sgv.sql` con `dotnet ef migrations script --idempotent`.

## Testing strategy (strict TDD)

| Layer | Qué | Cómo |
|---|---|---|
| Dominio | `Ocupacion` ctor acepta `VacanteId?`, `Reconstitute` lo hidrata | Unit (xUnit) en `tests/SGV.Tests/Dominio` |
| Aplicación N1 | `CrearAsync` con Puesto con Ocupacion activa → `409 PuestoOcupado` | Unit con `FakeOcupacionWriteRepository.ExistsActiveByPuestoAsync := true` |
| Aplicación N2 | `CambiarEstadoAsync` a Cubierta con `PersonaId` crea Ocupacion; sin `PersonaId` → `400`; falla atomic a `409` | Unit con fake repo + UoW con `DbUpdateException` simulado |
| Aplicación N3 | `CrearAsync` sin Vacante abierta → `409 PuestoSinVacanteAbierta`; con Vacante abierta → 201 | Unit — **adaptar el test `CrearAsync_DatosValidos_RetornaDtoYGuarda` (línea 47-64)** para que inyecte `FakeVacanteRepository.ExistsAbiertaByPuestoAsync := true` |
| Aplicación Q2 | `ReactivarAsync` con Vacante Cancelada → `409 VacanteCanceladaParaReactivar`; FK rota → permite; Activa → permite | Unit |
| Persistencia | Migración en DB limpia y con Ocupaciones pre-N2 | `[MySqlFact]` |
| API | `VacantesControllerTests`, `OcupacionesControllerTests` cubren 4xx nuevos | `ApiWebApplicationFactory` |
| Web | `Pages/Organizacion/Puestos/Details`: botón "Abrir Vacante" visible/oculto por rol y estado; `Create` de Ocupaciones muestra hint | `SgvWebApplicationFactory` + Razor Pages tests |

## Work units (commits revisables)

1. **WU-1**: Migración EF + Dominio (`Ocupacion.VacanteId` + ctors + `Reconstitute`) + `OcupacionEntity` + mappers. Sin tocar services.
2. **WU-2**: N1 (`VacanteServicioComandos` + `IOcupacionRepository` inyectado) + `VacanteErrorCodigo.PuestoOcupado` + tests.
3. **WU-3**: N3 (`OcupacionServicioComandos` + `IVacanteRepository` inyectado) + `OcupacionErrorCodigo.PuestoSinVacanteAbierta` + tests + adaptación del test línea 47-64.
4. **WU-4**: N2 (`CambiarEstadoVacanteRequest.PersonaId` + service crea `Ocupacion` al Cubrir) + `VacanteErrorCodigo.PersonaIdRequeridoParaCubrir` + tests.
5. **WU-5**: Q2 (`ReactivarAsync` check Vacante) + `OcupacionErrorCodigo.VacanteCanceladaParaReactivar` + tests.
6. **WU-6**: Web UI (`FORM-009` hint, `NAV-006` derivación, `NAV-007` botón "Abrir Vacante" + precarga `puestoId`) + Razor tests.

> Estimado: ~300-400 líneas de producción + tests. Single PR dentro del budget de 400 líneas.

## Riesgos técnicos

- **Atomicidad N2**: cubierta por una sola transacción EF; `DbUpdateException` hace rollback implícito. Validado por test de atomicidad.
- **TOCTOU N1+N3**: aceptado y documentado (paridad con `ExistsAbiertaByPuestoAsync` vigente). Mitiga el índice `ActivePuestoIdUnique` como safety net a nivel BD para `Ocupaciones`.
- **Adaptación del test 47-64**: requiere tocar el helper `CrearServicio` y los 2-3 fakes de `IVacanteRepository` existentes en tests. Tiempo incremental bajo.
- **Wire-types leaf**: `CambiarEstadoVacanteRequest` vive en `SGV.Contracts` (leaf) — agregar `PersonaId` no rompe el grafo de dependencias.
- **Distinguir Cubierta de Cancelada en Q2**: la implementación debe comparar por nombre de estado o por un flag específico; hoy el dominio sólo expone `EsTerminal`. Se necesita verificación: si ambos son `EsTerminal=true`, el service consulta el nombre del estado o un nuevo método `EstadoVacante.EsCancelada`. Pequeña adición al dominio puede ser necesaria (forzar clarify durante `apply`).

## Open questions

Ninguna. Las 3 heredadas de spec (NAV-007, N2 wire, FORM-009) resueltas arriba. Una micro-decisión diferida a `apply`: cómo distinguir `Cubierta` de `Cancelada` en `EstadoVacante` si ambos son `EsTerminal` (ver Riesgos).