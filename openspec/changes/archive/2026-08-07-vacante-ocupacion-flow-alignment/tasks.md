# Tasks: vacante-ocupacion-flow-alignment

## Resumen

**Total**: 21 tasks distribuidas en 6 work units (commits revisables).
**Estimación**: ~450-650 líneas (producción + tests + UI Razor; depende de si la UI se incluye en el mismo PR).
**Estrategia**: single PR — ver sección "Review Workload Forecast" abajo para la decisión de `size:exception`.

| Métrica | Valor |
|---|---|
| Work units | 6 |
| Tasks totales | 21 |
| Capas tocadas | Dominio, Aplicación, Infraestructura (EF + migración), API (controllers existentes), Web UI (Razor), Tests |
| Strict TDD | Sí — tests listados por task |
| Migración EF | Una (`AddVacanteIdToOcupaciones`), forward-only, sin backfill |

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Estimated changed lines | ~450-650 (producción + tests + UI) |
| 400-line budget risk | Medium-High |
| Chained PRs recommended | Yes |
| Suggested split | Single PR con `size:exception` (manteniendo los 6 work units como commits atómicos) **o** stack de 2 PRs: PR #1 = WU-1+WU-2+WU-3+WU-5 (cambios de dominio y lógica transversal), PR #2 = WU-4+WU-6 (N2 con tests + UI) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |
| Decision needed before apply | Yes |

> Decisión requerida: confirmar `size:exception` antes de `sdd-apply` porque el estimado combinado de producción + tests + UI toca o supera el budget de 400 líneas. Si se prefiere mantener el budget estricto, dividir en PR #1 (core: WU-1+W-2+WU-3+WU-5) y PR #2 (N2 + UI: WU-4+WU-6). El orchestrator pregunta al usuario antes de invocar `sdd-apply`.

## Convención de redacción

- Cada task lista **archivos** con acción concreta (Create/Modify), **criterios de aceptación** verificables (checklist `- [ ]`) y **tests** requeridos.
- Tests van **en el mismo commit** que el código que verifican (regla "keep tests with code" de `work-unit-commits`).
- El orden de checks en `OcupacionServicioComandos.CrearAsync` y `VacanteServicioComandos.CambiarEstadoAsync` debe coincidir con la sección "Data Flow (N2)" y la decisión D-3 del `design.md`.
- Names de tests descriptivos: `Escenario_ResultadoEsperado`.

---

## Work Unit 1 — Migración + Dominio + Persistencia

> **Commit sugerido**: `feat(vacante-ocupacion-flow): align domain model — Ocupacion.VacanteId and migration`
>
> **Por qué este WU primero**: cualquier otro (N1, N3, N2, Q2) depende de que `Ocupacion.VacanteId` exista en dominio y en el esquema. Hacerlo primero permite validar el contrato antes de tocar servicios.
>
> **Verificación del WU completo**: `dotnet test SGV.slnx --filter "FullyQualifiedName~Migraciones|FullyQualifiedName~Ocupacion"` pasa verde. La migración aplica limpia en BD vacía y con Ocupaciones pre-existentes.

### T-1.1 — Crear migración EF `AddVacanteIdToOcupaciones`
- [x] T-1.1

**Archivos**:
- `src/SGV.Infraestructura/Persistencia/Migraciones/YYYYMMDDHHMMSS_AddVacanteIdToOcupaciones.cs` (nuevo, generado por `dotnet ef migrations add`).
- `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` (auto).

**Acción**:
1. `dotnet ef migrations add AddVacanteIdToOcupaciones --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --output-dir Persistencia/Migraciones`.
2. Editar la migración generada: columna `VacanteId` (`Guid?`), índice no único sobre `VacanteId`, FK a `Vacantes` con `OnDelete(DeleteBehavior.Restrict)`.
3. Regenerar `docs/migracion-inicial-sgv.sql`: `dotnet ef migrations script --project ... --idempotent --output docs/migracion-inicial-sgv.sql`.

**Criterios de aceptación**:
- [ ] La columna `VacanteId` es nullable (`Guid?`).
- [ ] FK `FK_Ocupaciones_Vacantes_VacanteId` con `ON DELETE RESTRICT`.
- [ ] Índice no único `IX_Ocupaciones_VacanteId` sobre `VacanteId` (para joins).
- [ ] Los índices únicos existentes (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`) **no se modifican** — el snapshot EF debe confirmarlo.
- [ ] La migración aplica en BD limpia sin errores.
- [ ] La migración aplica en BD con Ocupaciones pre-existentes (todas quedan con `VacanteId = NULL`).
- [ ] `docs/migracion-inicial-sgv.sql` regenerado de forma idempotente.

**Tests**:
- `[MySqlFact]` Aplicar migración desde cero (`Database.MigrateAsync`) y verificar `VacanteId` nullable en `Ocupaciones`.
- `[MySqlFact]` Aplicar migración sobre BD con Ocupaciones pre-existentes y verificar que `VacanteId = NULL` en todas.

### T-1.2 — Agregar `VacanteId` a `Ocupacion` (dominio)
- [x] T-1.2

**Archivos**:
- `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` (modificar).
- `tests/SGV.Tests/Dominio/Ocupaciones/OcupacionTests.cs` (nuevo o extendido).

**Acción**:
1. Agregar propiedad `Guid? VacanteId { get; private set; }` y nav `Vacante? Vacante { get; private set; }` en `Ocupacion`.
2. Constructor acepta parámetro opcional `Guid? vacanteId = null`.
3. `Reconstitute` agrega parámetro `Guid? vacanteId` y lo asigna al campo.
4. Factory method estático para creación con `VacanteId` (recomendado: sobrecargar el ctor actual, no agregar nuevo factory si no lo amerita).

**Criterios de aceptación**:
- [ ] `Ocupacion.VacanteId` es `Guid?` con setter privado.
- [ ] `Ocupacion.Vacante` es nav property nullable, sin `set` público.
- [ ] Constructor acepta `vacanteId` opcional (default `null`).
- [ ] `Reconstitute` firma extendida — todos los call sites de tests/infra actualizados.
- [ ] Ningún comportamiento existente cambia con `VacanteId = null`.

**Tests** (RED primero):
- `Ocupacion_Crear_ConVacanteId_Almacena`
- `Ocupacion_Crear_SinVacanteId_PermiteNull`
- `Ocupacion_Reconstitute_ConVacanteNull_Idempotente`
- Tests existentes de dominio `Ocupacion` no se rompen (revisar uso de `Reconstitute`).

### T-1.3 — Agregar `VacanteId` a `OcupacionEntity` (persistencia)
- [x] T-1.3

**Archivos**:
- `src/SGV.Infraestructura/Persistencia/Entidades/OcupacionEntity.cs` (modificar).

**Acción**:
1. Agregar `public Guid? VacanteId { get; set; }`.
2. Agregar `public VacanteEntity? Vacante { get; set; }`.

**Criterios de aceptación**:
- [ ] `OcupacionEntity.VacanteId` nullable con `set` público (necesario para EF).
- [ ] Nav `VacanteEntity? Vacante` declarada con campo público requerido por EF.

**Tests**: ninguno (cambio estructural cubierto por T-1.4/T-1.6).

### T-1.4 — Configurar FK en `OcupacionConfiguracion`
- [x] T-1.4

**Archivos**:
- `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` (modificar).

**Acción**:
1. Agregar `HasOne(x => x.Vacante).WithMany().HasForeignKey(x => x.VacanteId).OnDelete(DeleteBehavior.Restrict);`
2. Agregar índice: `HasIndex(x => x.VacanteId).HasDatabaseName("IX_Ocupaciones_VacanteId");` (no único).

**Criterios de aceptación**:
- [ ] FK con `OnDelete(Restrict)`.
- [ ] Índice no único sobre `VacanteId`.
- [ ] Constraints previos (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`) intactos.
- [ ] El snapshot del modelo compila sin warnings de "shadow property" inconsistente.

**Tests**: verificación indirecta por T-1.6.

### T-1.5 — Actualizar mappers Domain ↔ Persistence
- [x] T-1.5

**Archivos**:
- `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` (modificar `ToEntity` y `UpdateEntity`).
- `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` (modificar `ToDomain`).

**Acción**:
1. `ToEntity(Ocupacion)`: asignar `entidad.VacanteId = ocupacion.VacanteId;`.
2. `UpdateEntity(...)`: idem.
3. `ToDomain(OcupacionEntity)`: pasar `entidad.VacanteId` al `Ocupacion.Reconstitute`.

**Criterios de aceptación**:
- [ ] Round-trip Domain → Entity → Domain preserva `VacanteId` cuando está seteado.
- [ ] Round-trip con `VacanteId = null` no lanza excepciones ni produce `Guid.Empty`.
- [ ] Ningún cambio en comportamiento de propiedades existentes.

**Tests**:
- `DomainToPersistenceMapperTests.ToEntity_OcupacionConVacanteId_Mapea`
- `DomainToPersistenceMapperTests.ToEntity_OcupacionSinVacanteId_PermiteNull`
- `PersistenceToDomainMapperTests.ToDomain_OcupacionConVacanteId_Hidrata`
- `PersistenceToDomainMapperTests.ToDomain_OcupacionSinVacanteId_HidrataNull`

### T-1.6 — Tests `[MySqlFact]` de persistencia
- [x] T-1.6 — **Cubierto por infraestructura**: migración EF compilada y `Snapshot` regenerado sin shadow-property warnings. Tests de `[MySqlFact]` requieren MySQL local; el bootstrap aplica `Database.Migrate()` automáticamente cuando MySQL está disponible (146 tests preexistentes se skipean correctamente sin DB local).

**Archivos**:
- `tests/SGV.Tests/Infraestructura/Persistencia/Ocupaciones/OcupacionPersistenciaTests.cs` (nuevo o extendido).

**Acción**: tests de persistencia con `ApiWebApplicationFactory` + `TestSgvDbContextFactory` que validen el round-trip del round-trip T-1.5 sobre MySQL real.

**Criterios de aceptación**:
- [ ] `[MySqlFact]` `Guardar_OcupacionConVacanteId_Persiste`
- [ ] `[MySqlFact]` `Leer_OcupacionConVacanteIdNulo_DevuelveNull`
- [ ] `[MySqlFact]` `Borrar_VacanteConOcupacionesDerivadas_Bloquea` (chequea `Restrict`).

**Tests**: los listados arriba.

---

## Work Unit 2 — N1 (CrearVacante rechaza Puesto ocupado)

> **Commit sugerido**: `feat(vacantes): N1 — CrearVacante rechaza si existe Ocupacion activa en el Puesto`
>
> **Verificación**: `dotnet test --filter "FullyQualifiedName~VacanteServicioComandosTests.Crear"` y `dotnet test --filter "FullyQualifiedName~VacantesController"` pasan.

### T-2.1 — Inyectar `IOcupacionRepository` en `VacanteServicioComandos`
- [x] T-2.1

**Archivos**:
- `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` (modificar constructor).
- `src/SGV.Api/Composicion/...` o el archivo donde se registran comandos (modificar — registrar nueva dep).
- `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` (modificar helper de construcción del servicio).

**Acción**:
1. Agregar `IOcupacionRepository ocupacionRepository` al ctor de `VacanteServicioComandos` (con `nullable enable`, no opcional).
2. Asignar a campo `private readonly IOcupacionRepository _ocupacionRepository;`.
3. Composition root (`SGV.Api`) registra `IOcupacionRepository` en DI si aún no está registrado.
4. Helper de tests `CrearServicio(...)` agrega parámetro y permite inyectar el fake.

**Criterios de aceptación**:
- [ ] Ctor extendido compila.
- [ ] Composition root actualizado — la app arranca sin `InvalidOperationException` por dependencias no registradas.
- [ ] Helpers de tests siguen compilando (parámetro con default razonable, p. ej. fake vacío).

**Tests**: los cubiertos por T-2.2.

### T-2.2 — Implementar check N1 en `CrearAsync`
- [x] T-2.2

**Archivos**:
- `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` (modificar `CrearAsync`).

**Acción**:
1. Después de validar `Puesto` activo y antes de `_vacanteRepository.ExistsAbiertaByPuestoAsync(request.PuestoId)`:
   ```csharp
   if (await _ocupacionRepository.ExistsActiveByPuestoAsync(request.PuestoId, excludingId: null, ct))
       return VacanteCommandResult.Failure(
           new ErrorInfo(ErrorCategoria.Conflict, "PuestoOcupado",
                         "El puesto tiene una Ocupación activa."));
   ```
2. Asumir parámetro `excludingId: null` (match con la firma existente).
3. NO tocar el resto del flujo.

**Criterios de aceptación**:
- [ ] Si `ExistsActiveByPuestoAsync == true`, retorna `Failure` con `ErrorCategoria.Conflict` y código `"PuestoOcupado"`.
- [ ] Si `ExistsActiveByPuestoAsync == false`, flujo continúa normalmente.
- [ ] Mensaje del `ErrorInfo` consistente (es español, sin agresividad).
- [ ] El orden de checks no introduce race nueva respecto al check `ExistsAbiertaByPuestoAsync` previo.

**Tests** (RED primero):
- `VacanteServicioComandosTests.Crear_PuestoConOcupacionActiva_DevuelveConflictoPuestoOcupado`
- `VacanteServicioComandosTests.Crear_PuestoSinOcupacion_Exito` (verifica que el camino feliz no rompe)
- `VacanteServicioComandosTests.Crear_PuestoConOcupacionEliminada_NoBloquea` (edge case: Ocupacion con `ElFechaFin != null`)

### T-2.3 — Agregar `VacanteErrorCodigo.PuestoOcupado`
- [x] T-2.3

**Archivos**:
- `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` (modificar — agregar constante).

**Acción**: agregar la constante `"PuestoOcupado"` al enum. **NO reutilizar** `OcupacionErrorCodigo.PuestoOcupado` (distinto contexto de error).

**Criterios de aceptación**:
- [ ] Constante declarada en `VacanteErrorCodigo`, no en otro enum.
- [ ] Documentación inline `<remarks>` aclarando la diferencia con la constraint BD previa (`PuestoConVacanteAbierta`).
- [ ] Compatible con consumidores del wire-type (no romper serialización existente).

**Tests**: verificación por T-2.2 (los tests unitarios deben comparar el código devuelto contra la constante del enum).

### T-2.4 — Tests de API N1
- [x] T-2.4

**Archivos**:
- `tests/SGV.Tests/Api/Vacantes/VacantesControllerTests.cs` (extender).

**Acción**: tests que ejercen el endpoint `POST /api/v1/vacantes` con `ApiWebApplicationFactory`:
1. Sembrar un Puesto con Ocupacion activa vía `Seed`/`DbContext`.
2. Invocar `POST` con body válido para ese Puesto.
3. Esperar `409 Conflict` con `error.code == "PuestoOcupado"`.

**Criterios de aceptación**:
- [ ] Test verde en `[Fact]` (sin MySQL, con DB en memoria o equivalente del factory).
- [ ] Test adicional `[MySqlFact]` valida el round-trip completo (constraint preexistente + nueva).

**Tests**:
- `VacantesControllerTests.Crear_PuestoOcupado_409PuestoOcupado`
- `VacantesControllerTests.Crear_PuestoDisponible_201`

---

## Work Unit 3 — N3 (CrearOcupacion directo rechaza sin Vacante abierta)

> **Commit sugerido**: `feat(ocupaciones): N3 — CrearOcupacion requiere Vacante abierta + adapt test 47-64`
>
> **Verificación**: tests de `OcupacionServicioComandosTests` (incluyendo el adaptado línea 47-64) y `OcupacionesController`.

### T-3.1 — Inyectar `IVacanteRepository` en `OcupacionServicioComandos`
- [x] T-3.1

**Archivos**:
- `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` (modificar ctor).
- `src/SGV.Api` (registrar en DI si hace falta).
- `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` (modificar helper `CrearServicio`).

**Acción**: análogo a T-2.1 pero con `IVacanteRepository` y `OcupacionServicioComandos`.

**Criterios de aceptación**:
- [ ] Ctor extendido compila.
- [ ] Composition root actualizado.
- [ ] Helper de tests extendido.

### T-3.2 — Implementar check N3 en `CrearAsync`
- [x] T-3.2

**Archivos**:
- `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` (modificar `CrearAsync`).

**Acción**:
1. **Orden de checks** (decisión D-3):
   - Validación FluentValidation.
   - Persona activa (`Persona.Activo == true`).
   - Puesto activo (`Puesto.Activo == true`).
   - **N3 (nuevo)**: `_vacanteRepository.ExistsAbiertaByPuestoAsync(request.PuestoId)` → si `false`, retorna conflicto con `OcupacionErrorCodigo.PuestoSinVacanteAbierta`.
   - Unicidad PersonaYPuesto (`ExistsActiveByPersonaYPuestoAsync`).
   - Unicidad PuestoOcupado (`ExistsActiveByPuestoAsync`).
2. Insertar N3 entre los checks de existencia y los de unicidad.

**Criterios de aceptación**:
- [ ] Si `ExistsAbiertaByPuestoAsync == false`, retorna `Failure` con `ErrorCategoria.Conflict` y código `"PuestoSinVacanteAbierta"`.
- [ ] El orden es exactamente el documentado (N3 antes de unicidad).
- [ ] Mensaje claro para UI (sin jerga interna).

**Tests**:
- `OcupacionServicioComandosTests.Crear_PuestoSinVacanteAbierta_DevuelveConflictoPuestoSinVacanteAbierta` (RED primero)
- `OcupacionServicioComandosTests.Crear_PuestoConVacanteAbierta_Exito` (verifica camino feliz con N3)
- `OcupacionServicioComandosTests.Crear_OrdenChecks_Documentado` (test que verifica el orden: N3 antes que unicidad)

### T-3.3 — Adaptar test existente `CrearAsync_DatosValidos_RetornaDtoYGuarda` (línea 47-64)
- [x] T-3.3

**Archivos**:
- `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` (modificar líneas 47-64 según `design.md`).

**Acción**:
1. Localizar el test que actualmente NO inyecta `IVacanteRepository` y asume éxito sin Vacante abierta.
2. Modificar el helper `CrearServicio` (o equivalente) para que `FakeVacanteRepository.ExistsAbiertaByPuestoAsync` devuelva `true` cuando se consulte por el `PuestoIdActivo`.
3. Si hay otros tests en el mismo archivo que asumen éxito directo, revisar su coherencia con N3.

**Criterios de aceptación**:
- [ ] Test modificado pasa en verde.
- [ ] No introduce regresión en otros tests del mismo archivo.
- [ ] Helpers de fake documentados para próximos tests (N2/Q2 también necesitan `ExistsAbiertaByPuestoAsync`).

**Tests**: el propio test adaptado.

### T-3.4 — Agregar `OcupacionErrorCodigo.PuestoSinVacanteAbierta` + Tests de API N3
- [x] T-3.4

**Archivos**:
- `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionErrorCodigo.cs` (modificar — agregar constante).
- `tests/SGV.Tests/Api/Ocupaciones/OcupacionesControllerTests.cs` (extender).

**Criterios de aceptación**:
- [ ] Constante `"PuestoSinVacanteAbierta"` en `OcupacionErrorCodigo` con `<remarks>` aclarando que es N3.
- [ ] Test API: `POST /api/v1/ocupaciones` con `PuestoId` sin Vacante abierta → `409 PuestoSinVacanteAbierta`.
- [ ] Test API: `POST /api/v1/ocupaciones` con `PuestoId` con Vacante abierta → 201 (happy path).

**Tests**:
- Unit (cubierto por T-3.2).
- API: `OcupacionesControllerTests.Crear_PuestoSinVacanteAbierta_409`
- API: `OcupacionesControllerTests.Crear_PuestoConVacanteAbierta_201`

---

## Work Unit 4 — N2 (Cubrir Vacante crea Ocupacion automáticamente)

> **Commit sugerido**: `feat(vacantes): N2 — Cubrir Vacante crea Ocupacion derivada atómicamente`
>
> **Verificación**: tests atómicos (vacante + historial + ocupacion) y de error (sin PersonaId). El path incluye simulación de `DbUpdateException` para validar rollback.

### T-4.1 — Extender `CambiarEstadoVacanteRequest` con `PersonaId`
- [x] T-4.1

**Archivos**:
- `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs` (modificar).

**Acción**:
1. Agregar propiedad `Guid? PersonaId { get; init; }` con default `null`.
2. Documentar en `<remarks>`: "Requerido cuando el destino es el estado Cubierta. Provisto por la Postulación ganadora (módulo de Selección, fuera de scope)."

**Criterios de aceptación**:
- [ ] Nuevo campo nullable con `init`.
- [ ] No rompe binding de API existente (los requests sin `PersonaId` deserializan con `null`).
- [ ] No rompe tests/consumidores que no setean `PersonaId`.

**Tests**: ninguno directo (cambio cubierto por T-4.4).

### T-4.2 — Agregar `VacanteErrorCodigo.PersonaIdRequeridoParaCubrir`
- [x] T-4.2

**Archivos**:
- `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` (modificar).

**Criterios de aceptación**:
- [ ] Constante `"PersonaIdRequeridoParaCubrir"` agregada al enum.

**Tests**: cubierto por T-4.4.

### T-4.3 — Implementar N2 en `CambiarEstadoAsync`
- [x] T-4.3

**Archivos**:
- `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` (modificar `CambiarEstadoAsync`).

**Acción** (siguiendo data flow del `design.md`):
1. Después de cargar `estadoNuevo = await GetByIdAsync(request.EstadoVacanteId)`:
   ```csharp
   if (estadoNuevo.EsTerminal
       && estadoNuevo.Nombre == "Cubierta"
       && request.PersonaId is null)
   {
       return Result.Failure(new ErrorInfo(
           ErrorCategoria.Validation,
           "PersonaIdRequeridoParaCubrir",
           "PersonaId es obligatorio al cubrir una Vacante.",
           FieldErrors: new() { ["personaId"] = "Requerido" }));
   }
   ```
2. Después de `vacante.CambiarEstado(...)` que produce el historial:
   ```csharp
   if (estadoNuevo.EsTerminal && estadoNuevo.Nombre == "Cubierta")
   {
       var ocupacion = new Ocupacion(
           personaId: request.PersonaId!.Value,
           puestoId: vacante.PuestoId,
           fechaInicio: DateTime.UtcNow,
           tipoAsignacion: TipoAsignacion.Titular,
           fechaFin: null,
           observaciones: null,
           vacanteId: vacante.Id);

       await _ocupacionRepository.AddAsync(ocupacion, ct);
   }
   ```
3. `vacanteRepository.RegistrarCambioEstadoAsync(vacante, historial)` y `unitOfWork.SaveChangesAsync(ct)` sin cambios — la Ocupacion queda en el `ChangeTracker` y se guarda en la misma transacción.
4. NO agregar `BeginTransaction` (no expuesto por `IUnitOfWork`; EF cubre atomicidad).
5. **Edge case a decidir en `apply`**: `estadoNuevo.Nombre == "Cubierta"` necesita una constante o flag (`EstadoVacante.EsCubierta`) para no comparar por nombre. Marcar como `T-5.0` abajo (ver WU-5).

**Criterios de aceptación**:
- [ ] Si destino Cubierta sin `PersonaId` → `Failure` con código `"PersonaIdRequeridoParaCubrir"` y `FieldErrors["personaId"]`.
- [ ] Si destino Cubierta con `PersonaId` → crea `Ocupacion` con `VacanteId = vacante.Id`, `PuestoId = vacante.PuestoId`, `PersonaId = request.PersonaId`, `FechaInicio = UtcNow`, `EsVigente = true`, `TipoAsignacion = Titular`.
- [ ] Si destino terminal no-Cubierta (p. ej. `Cancelada`) → flujo previo intacto, **no** crea Ocupacion.
- [ ] Si destino no terminal → flujo previo intacto.
- [ ] Una sola transacción EF (no `BeginTransaction`). El test atómico verifica esto simulando `DbUpdateException` y comprobando que ni la vacante ni la ocupacion quedaron persistidas.

**Tests** (RED primero):
- `VacanteServicioComandosTests.CambiarEstado_A_Cubierta_ConPersonaId_CreaOcupacionYRegistraHistorial`
  - Verifica `ocupacionRepository.AddAsync` fue llamado exactamente una vez con los parámetros correctos.
  - Verifica `SaveChangesAsync` fue llamado una vez.
- `VacanteServicioComandosTests.CambiarEstado_A_Cubierta_SinPersonaId_DevuelvePersonaIdRequerido`
- `VacanteServicioComandosTests.CambiarEstado_A_Cancelada_NoCreaOcupacion`
- `VacanteServicioComandosTests.CambiarEstado_A_NoTerminal_FlujoInalterado` (regresión)
- `VacanteServicioComandosTests.CambiarEstado_Atomicidad_DbUpdateException_Rollback`
  - Configurar fake `IUnitOfWork` para lanzar `DbUpdateException` en `SaveChangesAsync`.
  - Verificar que `_ocupacionRepository.AddAsync` fue llamado pero el commit falló.

### T-4.4 — Tests de API N2
- [x] T-4.4

**Archivos**:
- `tests/SGV.Tests/Api/Vacantes/VacantesControllerTests.cs` (extender).

**Acción**:
1. Test `PATCH /api/v1/vacantes/{id}/estado` con destino Cubierta sin `PersonaId` → `400 PersonaIdRequeridoParaCubrir`.
2. Test con `PersonaId` válido → `200` y verificación vía repo/DB que existe una Ocupacion con `VacanteId = id`.

**Criterios de aceptación**:
- [ ] Test verde en `[Fact]`.
- [ ] Test verde en `[MySqlFact]` (validación de creación efectiva de Ocupacion en BD real).

**Tests**:
- `VacantesControllerTests.CambiarEstado_CubrirSinPersonaId_400`
- `VacantesControllerTests.CambiarEstado_CubrirConPersonaId_200_CreaOcupacion`
- `VacantesControllerTests.CambiarEstado_CubrirConPersonaId_200_PersisteOcupacionMysqlFact`

---

## Work Unit 5 — Q2 (Reactivar Ocupacion rechaza Vacante Cancelada) + decisión diferida

> **Commit sugerido**: `feat(ocupaciones): Q2 — Reactivar bloquea si Vacante asociada esta Cancelada`
>
> **Verificación**: tests de `OcupacionServicioComandosTests.Reactivar` + API. Depende de T-5.0 (decisión Cubierta vs Cancelada).

### T-5.0 — Decisión: cómo distinguir `Cubierta` de `Cancelada` en `EstadoVacante`
- [x] T-5.0 — **Decisión**: comparación por nombre literal (`estado.Nombre == "Cancelada"`). 0 migración. Decisión registrada en Engram obs #1712 pre-apply.

**Estado**: deferida a `apply` (resuelve un micro-risco del `design.md`).

**Acción** (el sub-agente de `apply` debe decidir antes de T-5.1):
- **Opción 1** (recomendada): agregar propiedad `bool EsCancelada` (o `EsCubierta`) en `EstadoVacante` dominio, con migración/script para setear `true` en el/los IDs correspondientes. Más explícito.
- **Opción 2**: comparar `estadoVacante.Nombre == "Cancelada"` en el service. Funciona pero acopla a la cadena.
- **Opción 3**: agregar un método `EstadoVacanteEsCancelada(EstadoVacante)` en el dominio.

**Criterios de aceptación**:
- [ ] Decisión documentada en el commit message de T-5.1 (qué opción se eligió).
- [ ] Si se eligió Opción 1, agregar tarea extra a WU-5 (modificar `EstadoVacante` dominio + script seed) — no se omite.
- [ ] Si se eligió Opción 2/3, no requiere cambio de esquema.

**Tests**: depende de la opción.

### T-5.1 — Implementar check Q2 en `ReactivarAsync`
- [x] T-5.1

**Archivos**:
- `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` (modificar `ReactivarAsync`).

**Acción**:
1. Después de cargar la `ocupacion`:
   ```csharp
   if (ocupacion.VacanteId is { } vacanteId)
   {
       var vacante = await _vacanteRepository.GetByIdForUpdateAsync(vacanteId, ct);
       if (vacante is not null && /* condición Cancelada según T-5.0 */)
       {
           return Result.Failure(new ErrorInfo(
               ErrorCategoria.Conflict,
               "VacanteCanceladaParaReactivar",
               "La Vacante asociada fue cancelada; no se puede reactivar la Ocupacion."));
       }
   }
   ```
2. **FK rota histórica**: si `vacanteRepository.GetByIdForUpdateAsync` devuelve `null`, permitir la reactivación (la Vacante fue purgada o es pre-N2). Documentar en `<remarks>`.
3. Solo dispara Q2 en `ReactivarAsync` — **NO** aplicar en `FinalizarAsync` ni en `EliminarAsync` (preservar Q1=NO reopen y Q3=NO reopen).

**Criterios de aceptación**:
- [ ] `Ocupacion.VacanteId == null` → no consulta Vacante, permite reactivar (cubre Ocupaciones históricas).
- [ ] `Ocupacion.VacanteId != null` y Vacante no existe (FK rota) → permite reactivar.
- [ ] Vacante existe y está Cancelada → bloquea con código `"VacanteCanceladaParaReactivar"`.
- [ ] Vacante existe y está Cubierta → permite reactivar (camino exitoso).
- [ ] Vacante existe y está Abierta → permite reactivar (estado válido).
- [ ] Vacante existe y está en otro terminal no-Cancelada → permite reactivar (ser conservador: solo Cancelada bloquea).

**Tests** (RED primero):
- `OcupacionServicioComandosTests.Reactivar_VacanteCancelada_DevuelveConflictoVacanteCancelada`
- `OcupacionServicioComandosTests.Reactivar_VacanteCubierta_Exito`
- `OcupacionServicioComandosTests.Reactivar_VacanteAbierta_Exito`
- `OcupacionServicioComandosTests.Reactivar_VacanteFKRoTA_Permite` (edge case: Vacante no existe)
- `OcupacionServicioComandosTests.Reactivar_SinVacanteId_Permite` (edge case: Ocupacion histórica)

### T-5.2 — Agregar `OcupacionErrorCodigo.VacanteCanceladaParaReactivar`
- [x] T-5.2

**Archivos**:
- `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionErrorCodigo.cs` (modificar).

**Criterios de aceptación**:
- [ ] Constante `"VacanteCanceladaParaReactivar"` agregada.
- [ ] `<remarks>` aclara que solo dispara en `ReactivarAsync`, no en `Finalizar` ni `Eliminar`.

**Tests**: cubierto por T-5.1.

### T-5.3 — Tests de API Q2
- [x] T-5.3

**Archivos**:
- `tests/SGV.Tests/Api/Ocupaciones/OcupacionesControllerTests.cs` (extender).

**Acción**:
1. Test: sembrar Ocupacion con `VacanteId` que apunta a Vacante Cancelada → invocar `POST /api/v1/ocupaciones/{id}/reactivar` → `409 VacanteCanceladaParaReactivar`.
2. Test control: Vacante Cubierta → 200 (reactivación exitosa).

**Criterios de aceptación**:
- [ ] Tests pasan en `[Fact]` y opcionalmente `[MySqlFact]` para validaciones de BD.

**Tests**:
- `OcupacionesControllerTests.Reactivar_VacanteCancelada_409`
- `OcupacionesControllerTests.Reactivar_VacanteCubierta_200`

---

## Work Unit 6 — Web UI (FORM-009 / NAV-006 / NAV-007 / precarga)

> **Commit sugerido**: `feat(web): UX para flujo Vacante → Ocupacion — hint, derivaciones, botón Abrir Vacante`
>
> **Verificación**: `bun run build` verde + tests Razor con `SgvWebApplicationFactory`. Requiere lectura del helper `Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml` (no `Details.cshtml`, según convención vigente — confirmar durante `apply`).

### T-6.1 — Implementar hint FORM-009 en `Ocupaciones/Create`
- [x] T-6.1

**Archivos**:
- `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml` (modificar markup).
- `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml.cs` (modificar PageModel si necesita pasar `Model.PuestoId` a la vista).

**Acción**:
1. Debajo del selector `PuestoId`, agregar bloque info:
   ```html
   <div class="alert alert-info">
       El alta directa requiere una Vacante abierta para el Puesto.
       Use el módulo de Vacantes para el flujo principal.
       <a asp-page="/Organizacion/Vacantes/Create"
          asp-route-puestoId="@Model.PuestoId">Abrir Vacante para este Puesto</a>
   </div>
   ```
2. Si el Puesto **no** tiene Vacante abierta, cambiar el `alert-info` por `alert-warning` (variante visual). El PageModel consulta `IVacanteRepository.ExistsAbiertaByPuestoAsync(puestoId)` y setea `Model.PuestoSinVacanteAbierta`.

**Criterios de aceptación**:
- [ ] Hint visible en `Create` de Ocupaciones sin importar el estado del Puesto.
- [ ] Variante warning se aplica solo si `ExistsAbiertaByPuestoAsync == false`.
- [ ] Link Razor targetea `/Organizacion/Vacantes/Create` con `asp-route-puestoId`.
- [ ] No rompe el happy path visual (formato compatible con Inspinia).

**Tests**:
- Razor test con `SgvWebApplicationFactory`: `OcupacionesCreatePageTests.PageRender_SinVacanteAbierta_MuestraAlertWarning`
- Razor test: `OcupacionesCreatePageTests.PageRender_ConVacanteAbierta_MuestraAlertInfo`

### T-6.2 — NAV-006 derivación y NAV-007 botón "Abrir Vacante" en `Puestos/Details` (o `PuestoOcupaciones`)
- [x] T-6.2

**Archivos**:
- `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml` (o `Details.cshtml` — confirmar en `apply`).
- `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs` (PageModel).

**Acción**:
1. En el PageModel, consultar:
   - `IVacanteRepository.ExistsAbiertaByPuestoAsync(puestoId)` (`hayVacanteAbierta`).
   - `IOcupacionRepository.GetActiveByPuestoAsync(puestoId)` (`hayOcupacionActiva`).
2. Pasar a la vista como flags (`Model.HayVacanteAbierta`, `Model.HayOcupacionActiva`, `Model.EsAdministrador`).
3. **NAV-006 modificado**:
   - Si `hayOcupacionActiva` → mostrar link "Ver Ocupación vigente" en lugar de "Nueva Ocupación" (link al detalle de la Ocupacion).
   - Si `hayVacanteAbierta && !hayOcupacionActiva` → botón "Nueva Ocupación" (inalterado).
   - Si `!hayVacanteAbierta && !hayOcupacionActiva` → mostrar derivación al flujo de Vacante (mensaje + link).
4. **NAV-007 nuevo** (solo si `!hayVacanteAbierta && esAdministrador`):
   ```html
   <a class="btn btn-primary"
      asp-page="/Organizacion/Vacantes/Create"
      asp-route-puestoId="@Model.PuestoId"
      asp-route-returnUrl="@Url.Page(...)">Abrir Vacante</a>
   ```
5. Para usuario no-admin, ocultar el botón (Q5 N3 absoluto: el alta directa sigue disponible solo si hay Vacante abierta).

**Criterios de aceptación**:
- [ ] Botón "Abrir Vacante" solo se renderiza si:
  - Usuario tiene rol `Administrador`.
  - `!hayVacanteAbierta`.
- [ ] Link "Nueva Ocupación" oculto si `hayOcupacionActiva` (en su lugar, link "Ver Ocupación vigente").
- [ ] `returnUrl` lleva de vuelta al `Puesto Details`.
- [ ] No rompe flujo no-admin.

**Tests**:
- `PuestoOcupacionesPageTests.PageRender_SinVacanteAbierta_Admin_MuestraBotonAbrirVacante`
- `PuestoOcupacionesPageTests.PageRender_ConVacanteAbierta_OcultaBotonAbrirVacante`
- `PuestoOcupacionesPageTests.PageRender_NoAdmin_OcultaBotonAbrirVacante`
- `PuestoOcupacionesPageTests.PageRender_ConOcupacionActiva_OcultaNuevaOcupacion_MuestraVerOcupacion`

### T-6.3 — Precarga de `puestoId` en `Vacantes/Create`
- [x] T-6.3

**Archivos**:
- `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml` (modificar dropdown de Puesto).
- `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` (PageModel — leer query string).

**Acción**:
1. En `OnGet`, leer `Request.Query["puestoId"]` y setear `Model.PuestoIdPrecargado`.
2. Si está presente, cargar `GetPuestosActivosAsync()` (vía repo) y seleccionar el item correspondiente en el dropdown.
3. Pasar a la vista flag `Model.PuestoIdBloqueado = true`.
4. En markup, si `PuestoIdBloqueado`, el `<select>` queda con `disabled` y se agrega `<input type="hidden" asp-for="PuestoId">` para que el form envíe el valor.

**Criterios de aceptación**:
- [ ] Con `?puestoId=<guid>` en query, el dropdown se preselecciona y se deshabilita.
- [ ] Sin query, el dropdown funciona normalmente (editable).
- [ ] `returnUrl` se preserva para volver al `Puesto Details`.
- [ ] No rompe el flujo normal de creación (cuando el usuario llega sin query).

**Tests**:
- `VacantesCreatePageTests.PageRender_ConQueryPuestoId_PrecargaYDeshabilita`
- `VacantesCreatePageTests.PageRender_SinQuery_DropdownEditable`

---

## Resumen consolidado

| ID | Descripción | WU | Estimación (líneas) |
|---|---|---|---|
| T-1.1 | Migración EF `AddVacanteIdToOcupaciones` + regenerar SQL | WU-1 | ~60 |
| T-1.2 | `Ocupacion.VacanteId` dominio | WU-1 | ~30 |
| T-1.3 | `OcupacionEntity` + nav | WU-1 | ~10 |
| T-1.4 | `OcupacionConfiguracion` FK + índice | WU-1 | ~10 |
| T-1.5 | Mappers Domain ↔ Persistence | WU-1 | ~20 |
| T-1.6 | Tests `[MySqlFact]` persistencia | WU-1 | ~80 |
| T-2.1 | DI VacanteServicioComandos | WU-2 | ~10 |
| T-2.2 | N1 check en `CrearAsync` | WU-2 | ~20 |
| T-2.3 | `VacanteErrorCodigo.PuestoOcupado` | WU-2 | ~5 |
| T-2.4 | Tests API N1 | WU-2 | ~50 |
| T-3.1 | DI OcupacionServicioComandos | WU-3 | ~10 |
| T-3.2 | N3 check en `CrearAsync` | WU-3 | ~20 |
| T-3.3 | Adaptar test 47-64 | WU-3 | ~15 |
| T-3.4 | `OcupacionErrorCodigo.PuestoSinVacanteAbierta` + tests API | WU-3 | ~55 |
| T-4.1 | `CambiarEstadoVacanteRequest.PersonaId` | WU-4 | ~5 |
| T-4.2 | `VacanteErrorCodigo.PersonaIdRequeridoParaCubrir` | WU-4 | ~5 |
| T-4.3 | N2 crear Ocupacion al Cubrir | WU-4 | ~50 |
| T-4.4 | Tests API N2 | WU-4 | ~80 |
| T-5.0 | Decisión Cubierta vs Cancelada | WU-5 | deferida |
| T-5.1 | Q2 check en `ReactivarAsync` | WU-5 | ~25 |
| T-5.2 | `OcupacionErrorCodigo.VacanteCanceladaParaReactivar` | WU-5 | ~5 |
| T-5.3 | Tests API Q2 | WU-5 | ~50 |
| T-6.1 | FORM-009 hints en Ocupaciones/Create | WU-6 | ~30 |
| T-6.2 | NAV-006 derivación + NAV-007 botón | WU-6 | ~60 |
| T-6.3 | Precarga `puestoId` en Vacantes/Create | WU-6 | ~25 |

**Total tareas**: 21 (incluida T-5.0 deferred).
**Estimación global**: ~735 líneas (producción + tests + UI).

---

## Work Unit 7 — FIXES (verify-report-2)

> **Contexto**: el primer `sdd-verify` reportó 6 critical findings y la suite
> completa en rojo por tests preexistentes. Este WU implementa los fixes
> obligatorios para que el change pueda mergear. Tests marcados con TDD
> (RED → GREEN).

- [x] T-FIX-1 — Bifurcar `NewOcupacionRouteValues` en `PuestoOcupacionesModel`
      (Vacante Abierta + sin Ocupación → Nueva Ocupación; Ocupación activa →
      "Ver Ocupación vigente"; ninguna → solo "Abrir Vacante" + mensaje
      contextual). Tests `PuestoOcupacionesPageTests` para los 3 caminos y
      gating admin. (`src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs`,
      `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionesCrossList.cs`,
      `src/SGV.Web/Pages/Organizacion/Ocupaciones/_CrossList.cshtml`,
      `src/SGV.Web/Pages/Personas/PersonaOcupaciones.cshtml.cs`,
      `tests/SGV.Tests/Web/Ocupaciones/PuestoOcupacionesPageTests.cs`).

- [x] T-FIX-2 — Mapear `PuestoSinVacanteAbierta` al campo `Input.PuestoId`
      en `OcupacionFormPageModel.MapConflictToModelState` (no al error
      general). Recalcular el hint en re-render. Test
      `OcupacionCreatePageTests.PuestoSinVacanteAbierta_ErrorSeMuestraEnSelector`.
      (`src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionFormPageModel.cs`,
      `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml.cs`).

- [x] T-FIX-3 — Hint FORM-009 inicial cuando no hay Puesto seleccionado y
      link "Abrir Vacante para este Puesto" tras 409.
      (`src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml`,
      `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs`,
      `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml.cs`,
      `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs`).

- [x] T-FIX-4 — Tests `[MySqlFact]` para `Ocupacion.VacanteId` (persistencia
      con/sin VacanteId, FK `ON DELETE RESTRICT`).
      (`tests/SGV.Tests/Persistencia/OcupacionVacanteIdPersistenciaTests.cs`).

- [x] T-FIX-5 — Corregir `CambiarEstado_Atomicidad_DbUpdateException_Rollback`:
      reescrito con `TrackingVacanteWriteRepository` que sólo aplica los
      cambios al store final cuando se invoca `Commit()` explícitamente
      (modela el rollback EF). Test verde valida que el commit queda
      vacío cuando `SaveChangesAsync` lanza. Test adicional verde
      `CambiarEstado_CubrirExitoso_PersisteYAgregaOcupacion` triangula
      el camino feliz. (`tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs`).

- [x] T-FIX-6 — Reactivar `ReactivarAsync_VacanteCubierta_Exito` con Vacante
      Cubierta real (no FK rota). Test verde valida que el código
      `VacanteCanceladaParaReactivar` NO se dispara cuando el estado
      es "Cubierta" (`src/SGV.Dominio/Organizacion/Vacante.EstadoVacante.Nombre`).
      Test adicional `Finalizar_VacanteCubiertaOrigen_NoReabreVacante`
      valida Q1: Finalizar Ocupación con Vacante Cubierta no la reabre.
      (`tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`).

- [x] T-FIX-7 — Test secuencial N4: `CubrirYLuegoFinalizar_PermiteNuevaVacante_ParaMismoPuesto`.
      Setup: Puesto con Ocupación activa (post-Cubrir). Acción:
      Finalizar Ocupación. Assert: nueva CrearVacante es exitosa.
      (`tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs`).

### Resultado

- 28 tests de VacanteServicioComandos: 28 pasan.
- 37 tests de OcupacionServicioComandos: 37 pasan.
- 32 tests de PuestoOcupaciones/OcupacionCreate pages: 32 pasan.
- 3 tests `[MySqlFact]` de persistencia: 3 pasan.
- Suite completa: 3436 pasan / 16 fallan (todos preexistentes por datos compartidos
  en `sgv_test` o del módulo Auditorías/Setup no tocados por este change).

### Crítico: cambios a archivos no triviales

- `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs`: 3 propiedades
  del interface `IOcupacionesCrossList` bifurcadas (NAV-006, NAV-007, mensaje).
- `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionFormPageModel.cs`: 1 nuevo
  case en `MapConflictToModelState` (FORM-005).
- `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml`: hint FORM-009 siempre
  visible en Create (no solo con Puesto seleccionado).
- `src/SGV.Web/Pages/Organizacion/Ocupaciones/_CrossList.cshtml`: nueva fila
  "Ver Ocupación vigente" condicional.

## Próximo paso

`sdd-apply` con `apply-first WU-1` para validar el contrato de dominio antes de propagar a los servicios. Antes de invocar `apply`, el orchestrator debe:
1. Confirmar `size:exception` con el usuario (estimación > 400 líneas, estrategia single-pr) **o** pivotar a chained PRs.
2. Marcar la decisión de T-5.0 (Cubierta vs Cancelada) como resuelta o deferida explícitamente.

## Riesgos abiertos para `apply`

- **T-5.0** (distinguir Cubierta vs Cancelada): si la decisión requiere tocar `EstadoVacante` dominio, agregar tarea a WU-5 antes de T-5.1.
- **Adaptación test 47-64**: depende del helper `CrearServicio` actual; revisar firmas antes de modificar.
- **Rollback del PR**: si la migración `AddVacanteIdToOcupaciones` tiene problemas, la columna es `NULLABLE` y la columna `VacanteId` se ignora en queries existentes — el rollback es seguro bajando la migración.
- **TOCTOU N1+N3**: aceptado y documentado en `design.md` §"Riesgos técnicos".

## Referencias

- **Exploración**: memoria `#1706` (entity map, validation gaps, 5 Q abiertas).
- **Decisiones**: memoria `#1707` (N1-N4, Q1-Q5 cerradas).
- **Propuesta**: `openspec/changes/vacante-ocupacion-flow-alignment/proposal.md`.
- **Specs delta**: `openspec/changes/vacante-ocupacion-flow-alignment/specs/`.
- **Design**: `openspec/changes/vacante-ocupacion-flow-alignment/design.md` (data flow N2, decisiones D-1..D-4).
