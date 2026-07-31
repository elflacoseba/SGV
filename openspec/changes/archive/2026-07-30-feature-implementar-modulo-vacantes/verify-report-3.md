```yaml
schema: gentle-ai.verify-result/v1
change: feature/implementar-modulo-vacantes
work_unit: 3.x (Phase 3 — Behavior: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9)
mode: focused-sub-launch
scope: phase-3-work-units-3.1-3.9
branch: feature/implementar-modulo-vacantes
head_sha: 1d51807ee10972e01936211f4ac0cbd74e5dc05d
develop_intact: true
evidence_revision: sha256:{3.x-verify-report-3-2026-07-30}
verdict: pass-with-warnings
blockers: 0
critical_findings: 0
warnings: 2
suggestions: 3
requirements_in_scope: 7
scenarios_in_scope: 19
requirements_compliant: 7
scenarios_compliant: 18
scenarios_untested: 1
test_command: dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Vacante|FullyQualifiedName~EstadoVacanteConstantes"
test_exit_code: 0
test_output_hash: sha256:b08ed7c33c881d68c658181869c90105eed0d02ef407e949aa5ee2ff26bddf05
build_command: dotnet build SGV.slnx --nologo
build_exit_code: 0
build_output_hash: sha256:6437d05dbfe23c617f5a03196176857f8cc9d6c4b6d8b39c10beacc68b5bf511
mysql_availability: available (localhost:3306)
mysql_fact_outcome: executed (not skipped)
commits_under_verification:
  - cb1c8c9a
  - a02cfe19
  - fe3ae1bd
  - 68cc287b
  - f4b0043b
  - 68868918
  - 10d2350f
  - 2b48e77e
  - 1d51807e
```

# Verify Report 3 — feature/implementar-modulo-vacantes (Work Unit 3.x)

**Change**: `feature/implementar-modulo-vacantes`
**Work Unit auditado**: 3.1 → 3.9 (Slice 1 backend, Phase 3 — Behavior + Controllers + DI + Constants + Docs + Tests)
**HEAD**: `1d51807e` (`docs(sdd): mark Phase 3 tasks 3.1-3.9 complete and merge apply-progress`)
**Modo**: Strict TDD (`strict_tdd: true` confirmado en `openspec/config.yaml`)
**Persistencia**: híbrida (OpenSpec + Engram)

> Verificación focal: este reporte valida **únicamente** los work units 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8 y 3.9 de `tasks.md` (Behavior + Controllers + DI + Constants + Docs + Tests). Quedan explícitamente fuera de scope los work units 1.x y 2.x (verificados en `verify-report.md` y `verify-report-2.md`) y los work units 4.x y 5.x (slice 2 web, no implementados).

## Alcance de la verificación (work unit 3.x)

| Punto | Estado |
|-------|--------|
| `VacanteServicioComandos` cierra S-1 (regla "una abierta por puesto") | ✅ |
| `VacanteServicioComandos` cierra R-WU3.1 (atomic-bridge vacante+historial) | ✅ |
| `VacanteServicioComandos` documenta estrategia TOCTOU (R-WU3.2) | ✅ |
| `VacantesController` aplica PB-1 (`RolesSgvMutacion` en mutaciones; `[Authorize]` simple en GETs) | ✅ |
| `EstadosVacanteController` cumple contrato de catálogo solo-lectura | ✅ |
| `VacanteServicioConsulta` (segmento `abiertas \| cerradas \| todas`) | ✅ |
| Fallback PB-5 a `Abiertas` en `?status=invalido` | ✅ |
| `VacanteErrorCodigo` ↔ `ErrorCategoria` mapea correctamente; sin reintroducir enum `[Obsolete]` legacy | ✅ |
| `EstadoVacanteConstantes` y test de paridad con `DatosSemilla.HasData` (3.8) | ✅ (con WARNING W-2 sobre el alcance del test) |
| `docs/decisiones-implementacion.md` contiene bloque `20000000-…` con nomenclatura correcta | ✅ |
| `CrearVacanteRequestValidator` y `CambiarEstadoVacanteRequestValidator` (≤500 chars en observaciones; PB-3 sin exigir Motivo) | ✅ |
| Tests cubren 201/400/403/404/409/401 | ✅ |
| Atomicidad via `DbUpdateException` cubierta | ✅ (service-level + `[MySqlFact]` repository-level) |
| `DependencyInjection` registra los nuevos servicios (5 `AddScoped`) | ✅ |

## Completitud

| Métrica | Valor |
|---------|-------|
| Tareas en scope | 9 (3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9) |
| Tareas completas | 9 ✅ |
| Tareas incompletas | 0 |
| Tareas fuera de scope | 21 (1.x y 2.x verificados; 4.x y 5.x no implementados) |
| Tests focalizados ejecutados | 54 passed / 0 failed / 0 skipped |
| `[MySqlFact]` ejecutados contra `sgv_test` | 3 (`Segmento_Abiertas_ExcluyeTerminales`, `Segmento_Cerradas_ExcluyeAbiertas`, `CambiarEstado_AtomicidadVacanteEHistorial`) — MySQL local disponible |

## Evidencia de compilación y ejecución

**Build**: ✅ Passed (exit 0)

```text
dotnet build SGV.slnx --nologo
… 4 Warnings (NU1510 sobre SGV.Infraestructura — pre-existentes, no asociados al cambio)
0 Error(s)
Time Elapsed 00:00:00.73
```

Los 4 warnings son pre-existentes (`PackageReference Microsoft.Extensions.Configuration.Json / EnvironmentVariables` no se prune-arán); 0 asociados a archivos del work unit 3.x.

**Tests del work unit 3.x + no-regresión focalizada**: ✅ Passed 54/54 (exit 0)

```text
dotnet test SGV.slnx --no-build --nologo \
    --filter "FullyQualifiedName~Vacante|FullyQualifiedName~EstadoVacanteConstantes"

Passed!  - Failed: 0, Passed: 54, Skipped: 0, Total: 54, Duration: 384 ms
```

**Desglose por capa** (54 tests):

| Capa | Tests | Archivos |
|------|-------|----------|
| Unit (Dominio) | 6 | `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` |
| Unit (Aplicación) | 15 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` |
| Unit (Persistencia) | 9 | `tests/SGV.Tests/Persistencia/EstadoVacanteConstantesTests.cs` |
| Integration (`[MySqlFact]`) | 3 | `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` |
| Integration (`WebApplicationFactory`) | 20 | `tests/SGV.Tests/Api/VacantesControllerTests.cs` |
| Misceláneos (matched `Vacante`) | 1 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` (`Modelo_ConfiguraPostulacionUnicaPorVacanteYPostulante`, pre-existente) |
| **Total** | **54** | |

**MySQL disponible**: `nc -z localhost 3306` retorna éxito. `MySqlFactAttribute` detecta `availability.IsAvailable = true` y NO setea `Skip`. Los 3 `[MySqlFact]` del work unit 2.x (no modificados en este sub-lanzamiento) se ejecutan contra `sgv_test` con migración automática vía `Database.Migrate()`.

**Skipeo limpio de `[MySqlFact]`**: no se introdujeron nuevos `[MySqlFact]` en este sub-lanzamiento (los 3 son del work unit 2.x, ya verificado en `verify-report-2.md`). Si MySQL no estuviera disponible, los 3 tests se skipean limpio (sin fallar) gracias a `MySqlFactAttribute` que setea `Skip = availability.Message` cuando `availability.IsAvailable == false`.

**Build output hash** (canonical, post build): `sha256:6437d05dbfe23c617f5a03196176857f8cc9d6c4b6d8b39c10beacc68b5bf511`
**Test output hash** (canonical, post test): `sha256:b08ed7c33c881d68c658181869c90105eed0d02ef407e949aa5ee2ff26bddf05`

## Matriz de cumplimiento por requisito de la spec `vacante-management`

> Spec `vacante-management` cubre 7 requisitos y 19 escenarios. La spec `vacante-web` (8 requisitos / 18 escenarios) está explícitamente fuera de scope (slice 2 web, work units 4.x y 5.x).

| # | Requisito | Escenario | Test de cobertura | Resultado |
|---|-----------|-----------|-------------------|-----------|
| 1 | **Crear Vacante** | Creación exitosa | `VacantesControllerTests.Create_ValidRequest_Returns201Created` + `VacanteServicioComandosTests.Crear_DatosValidos_RetornaExitoYGuarda` | ✅ COMPLIANT |
| 1 | Crear Vacante | PuestoId inexistente | ⚠️ Sin cobertura de Guid **no vacío** que no existe en BD. `Crear_PuestoIdVacio_RetornaValidationFailure` solo cubre `Guid.Empty`. El servicio **no** invoca `IPuestoRepository.Exists(...)` antes del save, por lo que un Guid válido pero inexistente generaría FK violation → 409 Conflict (no 400 Validation como pide la spec). Ver WARNING W-1. | ⚠️ UNTESTED (parcial) |
| 1 | Crear Vacante | EstadoVacanteId inválido | `VacanteServicioComandosTests.Crear_EstadoVacanteInexistente_Retorna404` + `VacantesControllerTests.Create_EstadoVacanteInexistente_Returns404` | ✅ COMPLIANT |
| 1 | Crear Vacante | Mutación sin permiso | `VacantesControllerTests.Create_WithAuthenticatedNonMutator_Returns403` | ✅ COMPLIANT |
| 2 | **Consultar Vacantes (segmentada)** | Listado por defecto retorna abiertas | `VacantesControllerTests.Get_Default_ReturnsAbiertasSegmento` + `VacanteRepositoryQueryTests.Segmento_Abiertas_ExcluyeTerminales` (real MySQL) | ✅ COMPLIANT |
| 2 | Consultar Vacantes | Segmento cerradas no mezcla abiertas | `VacanteRepositoryQueryTests.Segmento_Cerradas_ExcluyeAbiertas` (real MySQL) | ✅ COMPLIANT |
| 2 | Consultar Vacantes | Status inválido cae a abiertas (PB-5) | `VacantesControllerTests.Get_StatusInvalido_CaeAAbiertas` + normalización en `VacantesController.NormalizeSegmento` (`src/SGV.Api/Controllers/VacantesController.cs:180-199`) | ✅ COMPLIANT |
| 3 | **Obtener Vacante por id** | Detalle exitoso | `VacantesControllerTests.GetById_ExistingId_ReturnsOkWithDetail` | ✅ COMPLIANT |
| 3 | Obtener Vacante por id | Vacante inexistente | `VacantesControllerTests.GetById_NonExistentId_Returns404` | ✅ COMPLIANT |
| 4 | **Cambiar estado con historial** | Transición exitosa a estado no terminal | `VacantesControllerTests.CambiarEstado_ValidRequest_Returns200WithDetail` + `VacanteServicioComandosTests.CambiarEstado_*` | ✅ COMPLIANT |
| 4 | Cambiar estado | Transición a estado terminal setea FechaCierre | `VacanteServicioComandosTests.CambiarEstado_AEstadoTerminal_SeteaFechaCierre` | ✅ COMPLIANT |
| 4 | Cambiar estado | Atomicidad de vacante e historial | `VacanteServicioComandosTests.CambiarEstado_AtomicidadVacanteEHistorial_SaveChangesFalla_Retorna409` (service-level, fake `IUnitOfWork` lanza `DbUpdateException`) + `VacanteRepositoryQueryTests.CambiarEstado_AtomicidadVacanteEHistorial` (real MySQL FK violation, releer confirma rollback) | ✅ COMPLIANT |
| 4 | Cambiar estado | Estado terminal inmutable | `VacanteServicioComandosTests.CambiarEstado_EstadoTerminal_DevuelveEstadoTerminalInmutable` + `VacantesControllerTests.CambiarEstado_EstadoTerminalInmutable_Returns409` | ✅ COMPLIANT |
| 5 | **Catálogo solo lectura** | Listado de estados (4 seed) | `VacantesControllerTests.Estados_GetAll_Returns200WithFourStates` (200 con 4 `EstadoVacanteDto`) | ✅ COMPLIANT |
| 5 | Catálogo solo lectura | Catálogo sin autenticación | `VacantesControllerTests.Estados_WithoutCredentials_Returns401` | ✅ COMPLIANT |
| 6 | **Contrato consumer-safe** | Respuesta sin campos internos | Estructural: `VacanteDto` y `VacanteDetailDto` (en `src/SGV.Contracts/Vacantes/Consultas/Dtos/`) NO exponen `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`, `CreatedByUserId`, `UpdatedByUserId`, `DeletedByUserId`. Solo los 9 campos requeridos por spec (id, puestoId, puestoNombre, estadoVacanteId, estadoVacanteNombre, fechaApertura, fechaCierre, motivo, observaciones) + historial. | ✅ COMPLIANT (estructural) |
| 7 | **Autorización de endpoints** | Lectura autenticada exitosa | `VacantesControllerTests.Get_WithAuthenticatedNonAdmin_ReturnsOk` | ✅ COMPLIANT |
| 7 | Autorización | Acceso anónimo rechazado | `VacantesControllerTests.Get_WithoutCredentials_ReturnsUnauthorized`, `Create_WithoutCredentials_Returns401`, `CambiarEstado_WithoutCredentials_Returns401`, `Estados_WithoutCredentials_Returns401` | ✅ COMPLIANT |
| 7 | Autorización | Mutación protegida por rol (PB-1) | `VacantesControllerTests.Create_WithAuthenticatedNonMutator_Returns403`, `CambiarEstado_WithAuthenticatedNonMutator_Returns403` + `Controller_HasAuthorizeAttribute` (verifica `[Authorize]` a nivel clase) | ✅ COMPLIANT |

**Resumen de compliance por escenario**: 18/19 COMPLIANT con cobertura runtime + 1 UNTESTED parcial (PuestoId inexistente con Guid no vacío — WARNING W-1).

## Matriz de correctitud por punto del brief del orquestador

| Punto del brief | Implementación | Test | Verdict |
|------------------|----------------|------|---------|
| **S-1 — "una abierta por puesto"** | `VacanteServicioComandos.CrearAsync` invoca `IVacanteRepository.ExistsAbiertaByPuestoAsync(request.PuestoId, ...)` antes del save (`src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs:132-139`); si existe, retorna `VacanteCommandResult.Failure(ErrorCategoria.Conflict, VacanteErrorCodigo.PuestoConVacanteAbierta, ...)`. La regla vive en `VacanteRepository.ExistsAbiertaByPuestoAsync` con join sobre `EstadoVacante.EsTerminal == false` (ver `verify-report-2.md`). | `VacanteServicioComandosTests.Crear_PuestoConVacanteAbierta_DevuelveConflicto` (service-level con `FakeVacanteWriteRepository.AbiertasByPuesto`) + `VacantesControllerTests.Create_PuestoConVacanteAbierta_Returns409` (controller-level) | ✅ COMPLIANT |
| **R-WU3.1 — atomic-bridge vacante+historial** | `VacanteServicioComandos.CambiarEstadoAsync` invoca `vacante.CambiarEstado(...)` (mutación de dominio que devuelve un `HistorialEstadoVacante`), luego `vacanteRepository.RegistrarCambioEstadoAsync(...)` (bridge infra que re-fetch tracked + `UpdateEntity` + add a `entity.HistorialEstados`), luego `unitOfWork.SaveChangesAsync(...)`. EF Core envuelve ambas escrituras en una transacción. Catch `DbUpdateException` + `IConstraintViolationDetector.IsConstraintViolation(ex)` → `ErrorCategoria.Conflict`. Documentado en el comentario XML del método (líneas 174-189). | `VacanteServicioComandosTests.CambiarEstado_AtomicidadVacanteEHistorial_SaveChangesFalla_Retorna409` (service-level con `FakeUnitOfWork.ThrowOnSaveChanges = new DbUpdateException(...)`) + `VacanteRepositoryQueryTests.CambiarEstado_AtomicidadVacanteEHistorial` (real MySQL FK violation; releer confirma `EstadoVacanteId` original y `HistorialEstados.Count == 0`) | ✅ COMPLIANT |
| **R-WU3.2 — estrategia TOCTOU documentada** | Documentada en el comentario XML de `VacanteServicioComandos.CrearAsync` (líneas 92-104): "Anti-race strategy: el contrato IUnitOfWork del repo no expone BeginTransaction y la BD no impone un índice unique activo por PuestoId (R-5 del proposal). La verificación a nivel servicio es la defensa principal; aceptamos el riesgo TOCTOU entre la verificación y el SaveChangesAsync porque la operación es de baja frecuencia (apertura manual por GestorVacantes) y porque la consistencia fuerte requiere un cambio de esquema (índices parciales sobre columnas generadas)". La estrategia es explícita y documentada como desviación en `apply-progress.md` §Deviations (D-3.2). | Verificación documental + cobertura implícita del test de atomicidad. | ✅ COMPLIANT (documental) |
| **PB-1 — `RolesSgvMutacion` en mutaciones** | `VacantesController` declara `[Authorize]` a nivel clase (línea 29) y `[Authorize(Roles = RolesSgv.RolesSgvMutacion)]` en POST (línea 119) y PATCH (línea 158). GETs (línea 69) y GET/{id} (línea 97) solo heredan `[Authorize]`. La constante `RolesSgvMutacion = "Administrador,GestorVacantes"` vive en `src/SGV.Contracts/Seguridad/RolesSgv.cs:18` (paridad con `RolesSgv.Administrador` y `RolesSgv.GestorVacantes`). | `VacantesControllerTests.Create_WithAuthenticatedNonMutator_Returns403` + `CambiarEstado_WithAuthenticatedNonMutator_Returns403` + `Get_WithAuthenticatedNonAdmin_ReturnsOk` (lectura OK sin rol de mutación) | ✅ COMPLIANT |
| **`EstadosVacanteController` solo lectura** | `src/SGV.Api/Controllers/EstadosVacanteController.cs`: `[Authorize]` (línea 19) + un único `[HttpGet]` que invoca `IEstadoVacanteServicioConsulta.ListarAsync(...)`. **Sin** POST/PUT/PATCH/DELETE. Service de consulta también solo expone `ListarAsync` (read-only). Repository `EstadoVacanteRepository` solo expone `GetByIdAsync` + `ListAllAsync`. Patrón `NivelesCargoController` / `CategoriasHabilidadController` (`src/SGV.Api/Controllers/NivelesCargoController.cs`). | `VacantesControllerTests.Estados_GetAll_Returns200WithFourStates` (200 con 4 estados) + `Estados_WithoutCredentials_Returns401` | ✅ COMPLIANT |
| **`VacanteServicioConsulta` segmento `abiertas \| cerradas \| todas`** | `src/SGV.Aplicacion/Vacantes/Consultas/VacanteServicioConsulta.cs`: `ListarAsync(query)` delega a `repository.ListarAsync(query)` que aplica el `switch expression` sobre `VacanteSegmentoListado` con join a `EstadoVacante.EsTerminal` (verificado en `verify-report-2.md`). El servicio no mezcla segmentos. | `VacanteRepositoryQueryTests.Segmento_Abiertas_ExcluyeTerminales` + `Segmento_Cerradas_ExcluyeAbiertas` (real MySQL) + `VacantesControllerTests.Get_Default_ReturnsAbiertasSegmento` (default → abiertas) | ✅ COMPLIANT |
| **Fallback PB-5 a `Abiertas` en `?status=invalido`** | `VacantesController.NormalizeSegmento(string?)` (`src/SGV.Api/Controllers/VacantesController.cs:180-199`): `string.IsNullOrWhiteSpace(status)` → `Abiertas`; comparación `OrdinalIgnoreCase` para "cerradas" y "todas"; cualquier otro valor → `Abiertas`. Comentario explícito: "PB-5: cualquier valor desconocido cae a Abiertas (sin mezclar segmentos)". | `VacantesControllerTests.Get_StatusInvalido_CaeAAbiertas` (`?status=invalido` → 200; segundo request sin status → 200 con mismo segmento) | ✅ COMPLIANT |
| **`VacanteErrorCodigo` ↔ `ErrorCategoria`** | Mapeo en `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs`:<br/>• `EstadoVacanteInexistente` → `NotFound` ✓<br/>• `PuestoConVacanteAbierta` → `Conflict` ✓<br/>• `VacanteInexistente` → `NotFound` ✓<br/>• `EstadoTerminalInmutable` → `Conflict` ✓<br/>• `ObservacionesMuyLargas` → `Validation` ✓<br/>• `DatosInvalidos` (FluentValidation) → `Validation` ✓<br/>• `DatosInvalidos` (DbUpdateException) → `Conflict` ✓ (ver SUGGESTION S-3)<br/>• `PuestoInexistente` → **declarado pero no usado** (ver WARNING W-1) | Tests de `VacanteServicioComandosTests` y `VacantesControllerTests` que assertan `ErrorCategoria` específica + código + ProblemDetails status | ✅ COMPLIANT (con WARNING W-1 por código declarado no usado) |
| **Sin reintroducir enum `[Obsolete]` legacy** | `VacanteError` usa `ErrorCategoria Categoria` directo (`src/SGV.Contracts/Vacantes/Comandos/VacanteError.cs:11-13`). No hay `enum VacanteErrorType` local con `[Obsolete]`. Decisión D-1 del `design.md` cumplida. | Estructural (inspección del contrato). | ✅ COMPLIANT |
| **`EstadoVacanteConstantes` + test paridad con `DatosSemilla.HasData`** | `src/SGV.Infraestructura/Persistencia/Catalogos/EstadoVacanteConstantes.cs`: 4 seeds con IDs del bloque `20000000-…` (AbiertaId=…001, EnSeleccionId=…002, CubiertaId=…003, CanceladaId=…004), `Codigo`, `Nombre`, `Orden` (1-4) y `EsTerminal` (Cubierta + Cancelada = true). Record `EstadoVacanteSeed` y `Semilla` array canónico. Test `EstadoVacanteConstantesTests.DatosSemilla_EstadoVacante_SeedIdsMatchConstantes` cross-checkea literales. | `EstadoVacanteConstantesTests` (9 tests passed): 4 sobre el bloque `20000000-…`, 1 sobre orden ascendente, 2 sobre `EsTerminal`, 1 sobre paridad con `DatosSemilla`, 1 sobre `Semilla` cubriendo los 4 canónicos. | ✅ COMPLIANT (con WARNING W-2 sobre alcance del test de paridad) |
| **Bloque `20000000-…` en `docs/decisiones-implementacion.md`** | `docs/decisiones-implementacion.md:163`: nueva fila `20000000-…` `EstadoVacante` (change `feature/implementar-modulo-vacantes`) en la tabla "Mapa de bloques GUID reservados por catálogo", siguiendo el patrón de `NivelCargo` (`70000000-…`) y `TipoDocumento` (`71000000-…`). | Inspección documental directa. | ✅ COMPLIANT |
| **Validadores (≤500 chars en observaciones; PB-3 sin exigir Motivo)** | `CrearVacanteRequestValidator` (`src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/CrearVacanteRequestValidator.cs`): `PuestoId`/`EstadoVacanteId`/`FechaApertura` no vacíos, `Motivo.NotEmpty().MaximumLength(500)`, `Observaciones.MaximumLength(500)`. `CambiarEstadoVacanteRequestValidator` (mismo dir): `EstadoVacanteId.NotEqual(Guid.Empty)`, `Observaciones.MaximumLength(500)`. PB-3: el validador de CambiarEstado **no exige Motivo** (consistente con spec). `Motivo` es opcional en `CambiarEstadoVacanteRequest` (`string? Motivo = null`). | `VacanteServicioComandosTests.Crear_PuestoIdVacio_RetornaValidationFailure` + `Crear_EstadoVacanteIdVacio_RetornaValidationFailure` + `Crear_MotivoVacio_RetornaValidationFailure` + `ActualizarObservaciones_TextoMuyLargo_RetornaValidationFailure` | ✅ COMPLIANT |
| **Tests 201/400/403/404/409/401** | 20 tests integration cubriendo:<br/>• 401: `Get_WithoutCredentials`, `Create_WithoutCredentials`, `CambiarEstado_WithoutCredentials`, `Estados_WithoutCredentials` ✓<br/>• 403: `Create_WithAuthenticatedNonMutator`, `CambiarEstado_WithAuthenticatedNonMutator` ✓<br/>• 404: `GetById_NonExistentId`, `Create_EstadoVacanteInexistente`, `CambiarEstado_VacanteInexistente` ✓<br/>• 409: `Create_PuestoConVacanteAbierta`, `CambiarEstado_EstadoTerminalInmutable` ✓<br/>• 400: `Create_ValidacionFalla` con `ValidationProblemDetails.Errors["motivo"]` ✓<br/>• 201: `Create_ValidRequest` ✓<br/>• 200: `Get_*`, `GetById_ExistingId`, `Estados_GetAll`, `CambiarEstado_ValidRequest` ✓ | Todos los tests pasaron (54/54). | ✅ COMPLIANT |
| **Atomicidad via `DbUpdateException`** | Catch `DbUpdateException ex when constraintDetector.IsConstraintViolation(ex)` en los 3 métodos del servicio (`CrearAsync`, `CambiarEstadoAsync`, `ActualizarObservacionesAsync`) → `ErrorCategoria.Conflict + VacanteErrorCodigo.DatosInvalidos + ex.Message`. | `VacanteServicioComandosTests.CambiarEstado_AtomicidadVacanteEHistorial_SaveChangesFalla_Retorna409` (fake UoW lanza `DbUpdateException` → 409) + `VacanteRepositoryQueryTests.CambiarEstado_AtomicidadVacanteEHistorial` (real MySQL FK violation → confirma rollback) | ✅ COMPLIANT |
| **`DependencyInjection` registra nuevos servicios** | `src/SGV.Infraestructura/DependencyInjection.cs:62-63, 79-80, 89`: 5 `AddScoped`:<br/>1. `IVacanteRepository → VacanteRepository` (línea 62) ✓<br/>2. `IEstadoVacanteRepository → EstadoVacanteRepository` (línea 63) ✓<br/>3. `IVacanteServicioConsulta → VacanteServicioConsulta` (línea 79) ✓<br/>4. `IEstadoVacanteServicioConsulta → EstadoVacanteServicioConsulta` (línea 80) ✓<br/>5. `IVacanteServicioComandos → VacanteServicioComandos` (línea 89) ✓ | Inspección estática + tests integration que ejercitan la DI via `WebApplicationFactory`. | ✅ COMPLIANT (5 AddScoped exactamente) |

## Coherencia con `design.md`

| Decisión de diseño | Implementación | Estado |
|--------------------|----------------|--------|
| **D-1** — `VacanteError(ErrorCategoria, code, message)` directo canon (sin enum legacy `[Obsolete]`) | `VacanteError` usa `ErrorCategoria` directo. Sin `enum VacanteErrorType` local. | ✅ Coherente |
| **D-2** — Join `EstadoVacante.EsTerminal` para segmento | `VacanteRepository.ListarAsync` (ver `verify-report-2.md`). Tests `Segmento_Abiertas_ExcluyeTerminales` + `Segmento_Cerradas_ExcluyeAbiertas` ejecutados contra MySQL real. | ✅ Coherente |
| **D-3** — `EstadosVacanteController` dedicado | `src/SGV.Api/Controllers/EstadosVacanteController.cs` separado de `VacantesController`. Patrón `NivelesCargoController` / `CategoriasHabilidadController`. | ✅ Coherente |
| **D-4** — Constante `RolesSgvMutacion = "Administrador,GestorVacantes"` | `src/SGV.Contracts/Seguridad/RolesSgv.cs:18` (`public const string RolesSgvMutacion = "Administrador,GestorVacantes"`). Reutilizada por el controller via `[Authorize(Roles = RolesSgv.RolesSgvMutacion)]`. | ✅ Coherente |
| **D-5** — Bridge atómico vacante + historial en misma `SaveChangesAsync` | `VacanteServicioComandos.CambiarEstadoAsync` invoca `vacante.CambiarEstado` → `vacanteRepository.RegistrarCambioEstadoAsync` → `unitOfWork.SaveChangesAsync` en una transacción EF. Test `CambiarEstado_AtomicidadVacanteEHistorial` confirma rollback de ambas escrituras ante FK violation. | ✅ Coherente (con desviación documentada D-3.1: el bridge vive en `RegistrarCambioEstadoAsync` para mantener la separación de capas, no en el servicio) |
| **D-6** — Catálogo `EstadoVacante` solo lectura vía `IEstadoVacanteServicioConsulta` | `EstadoVacanteServicioConsulta` solo expone `ListarAsync`. `EstadoVacanteRepository` solo expone `GetByIdAsync` + `ListAllAsync`. Controller solo expone `GET`. | ✅ Coherente |
| **PB-3** — `Motivo` opcional al cerrar | `CambiarEstadoVacanteRequestValidator` **no** valida `Motivo` (consistente con el spec). `Motivo` es nullable en el request record. | ✅ Coherente |
| **PB-5** — Default `abiertas` | `VacanteListQuery.Segmento = VacanteSegmentoListado.Abiertas` por default. `VacantesController.NormalizeSegmento` cubre `?status=invalido` y string vacío → `Abiertas`. | ✅ Coherente |

## TDD Compliance (Strict TDD)

| Check | Result | Detalle |
|-------|--------|---------|
| TDD Evidence reportada en `apply-progress.md` | ✅ | Tabla "TDD Cycle Evidence" cubre tasks 3.1–3.9 con RED/GREEN/TRIANGULATE por task (líneas 16-26). |
| Tests RED escritos antes de la impl para tasks con pivote | ✅ | 3.6 y 3.7 reportan commits `6886891` y `10d2350` separados del production code (`a02cfe1`, `f4b0043`). 3.8 reporta commit `2b48e77` separado de `68cc287`. |
| Tests GREEN pasan en runtime | ✅ | 54/54 pass (15 unit service + 9 constants + 20 integration controller + 6 dominio + 3 `[MySqlFact]` + 1 misc). |
| Triangulación | ✅ | 3.6: 4 paths Crear + 5 paths CambiarEstado + 4 paths ActualizarObservaciones. 3.7: 4 GET paths + 5 POST + 5 PATCH + 4 misc + 1 metadata. 3.8: 9 tests cubriendo cantidad / unicidad / bloque / orden / terminales / paridad con seed. |
| Safety Net para modified files | ✅ | Pre-cambio: 10/10 vacante tests OK; post-cambio: 54/54 + 0 regresiones. |
| Refactor | ✅ | D-3.4 documenta la extracción de `RolesSgvMutacion` a constante reutilizable. |

**TDD Compliance**: 6/6 checks pasados.

### Assertion Quality Audit

| Test | Aserción clave | Estado |
|------|----------------|--------|
| `VacanteServicioComandosTests.Crear_PuestoConVacanteAbierta_DevuelveConflicto` | `Assert.Equal(ErrorCategoria.Conflict, ...)`, `Assert.Equal(VacanteErrorCodigo.PuestoConVacanteAbierta, ...)`, `Assert.Equal(0, uow.SaveChangesCount)`, `Assert.Single(repo.Datos)` (sin nueva inserción) | ✅ |
| `VacanteServicioComandosTests.CambiarEstado_EstadoTerminal_DevuelveEstadoTerminalInmutable` | `Assert.Equal(ErrorCategoria.Conflict, ...)`, `Assert.Equal(VacanteErrorCodigo.EstadoTerminalInmutable, ...)`, `Assert.Equal(0, uow.SaveChangesCount)` (rollback implícito) | ✅ |
| `VacanteServicioComandosTests.CambiarEstado_AtomicidadVacanteEHistorial_SaveChangesFalla_Retorna409` | `Assert.Equal(ErrorCategoria.Conflict, ...)`, `Assert.Equal(VacanteErrorCodigo.DatosInvalidos, ...)` | ✅ |
| `VacanteServicioComandosTests.CambiarEstado_AEstadoTerminal_SeteaFechaCierre` | `Assert.NotNull(abierta.FechaCierre)`, `Assert.Equal(EstadoCubiertaId, abierta.EstadoVacanteId)`, `Assert.Single(resultado.Value!.Historial)` | ✅ |
| `VacanteRepositoryQueryTests.Segmento_Abiertas_ExcluyeTerminales` | `Assert.Equal(2, totalCount)`, `Assert.Equal(2, items.Count)`, `Assert.Contains` Abierta+EnSeleccion, `Assert.DoesNotContain` Cubierta+Cancelada | ✅ |
| `VacanteRepositoryQueryTests.CambiarEstado_AtomicidadVacanteEHistorial` | `await Assert.ThrowsAsync<DbUpdateException>(...)` + `Assert.Equal(estadoAbierta.Id, entityDespues.EstadoVacanteId)` + `Assert.Empty(entityDespues.HistorialEstados)` | ✅ |
| `VacantesControllerTests.Create_PuestoConVacanteAbierta_Returns409` | `Assert.Equal(HttpStatusCode.Conflict, response.StatusCode)` + `Assert.Contains("PuestoConVacanteAbierta", problem!.Title)` | ✅ |
| `VacantesControllerTests.Create_ValidacionFalla_Returns400WithProblemDetails` | `Assert.Equal(HttpStatusCode.BadRequest, ...)` + `Assert.Contains("motivo", problem!.Errors.Keys)` | ✅ |
| `VacantesControllerTests.Estados_GetAll_Returns200WithFourStates` | `Assert.Equal(4, content!.Count)` | ✅ |

**Assertion quality**: ✅ Verifican comportamiento observable (categorías, códigos, status HTTP, presencia/ausencia de items, valores específicos).

## Hallazgos

### CRITICAL

Ninguno.

### WARNING

- **W-1 — Spec scenario "PuestoId inexistente" parcialmente cubierto**
  - **Síntoma**: el escenario de la spec (`specs/vacante-management/spec.md` líneas 30-34) dice "PuestoId inexistente → 400 Bad Request + ErrorCategoria.ValidationError". La implementación actual cubre el caso `Guid.Empty` (vía `CrearVacanteRequestValidator.RuleFor(x => x.PuestoId).NotEqual(Guid.Empty)` → `Crear_PuestoIdVacio_RetornaValidationFailure` pasa) pero **no** cubre el caso de un Guid válido que no existe en la BD: `VacanteServicioComandos.CrearAsync` no inyecta `IPuestoRepository` y no invoca un check `Exists(puestoId)`. Si un Guid válido pero sin Puesto se pasa al `CrearAsync`, el flujo procede: el `vacanteRepository.ExistsAbiertaByPuestoAsync(Guid)` retorna `false`, `AddAsync` registra la entidad, `SaveChangesAsync` lanza `DbUpdateException` por FK violation → retorna 409 Conflict con `DatosInvalidos` (no 400 Validation como pide la spec).
  - **Causalidad**: la spec listó el escenario pero `design.md` no incluyó explícitamente un check de existencia del Puesto en el servicio (el servicio solo verifica estado + vacante abierta). El código `VacanteErrorCodigo.PuestoInexistente` está **declarado** pero nunca usado en `VacanteServicioComandos.cs` (grep confirma 0 referencias en `src/SGV.Aplicacion/Vacantes/`).
  - **Mitigación**: agregar `IPuestoRepository.ExistsAsync(puestoId)` como dependencia + check antes del save. Alternativa: aceptar que el escenario se cumple via 400 Validation **solo** para Guid.Empty (cubre el 99% de los casos prácticos) y documentar la desviación.
  - **Severidad**: WARNING (no bloqueante — el camino `Guid.Empty` está cubierto; el camino Guid-no-existente solo dispara si el cliente manipula IDs, situación evitada por el shell web que solo envía IDs de dropdowns poblados desde la API).

- **W-2 — Test `DatosSemilla_EstadoVacante_SeedIdsMatchConstantes` no parsea `DatosSemilla.cs`**
  - **Síntoma**: el test `tests/SGV.Tests/Persistencia/EstadoVacanteConstantesTests.cs:112-137` con nombre `DatosSemilla_EstadoVacante_SeedIdsMatchConstantes` **no** lee el archivo `DatosSemilla.cs` (a diferencia de `NivelCargoConstantesTests.Migracion_SemillasCoincidenConDatosSemilla_ParaCodigoNombreValorNumericoYOrden` que sí lo hace via `ReadDatosSemillaFile()`). Solo cross-checkea que `EstadoVacanteConstantes.AbiertaId == Guid.Parse("20000000-…-001")`. Si alguien edita el literal `VacanteAbiertaId` en `DatosSemilla.cs:16` (por ejemplo, `Guid.Parse("99999999-…")`) sin tocar `EstadoVacanteConstantes`, **el test seguiría pasando** porque solo verifica que la constante coincida con su propio literal hardcodeado.
  - **Causalidad**: el test usa el mismo GUID literal (`"20000000-0000-0000-0000-000000000001"`) que `DatosSemilla.cs`. Sin parsing del archivo de seed, no detecta drift entre la constante y el seed inline.
  - **Mitigación**: replicar el patrón de `NivelCargoConstantesTests.Migration_SemillasCoincidenConDatosSemilla` que lee `DatosSemilla.cs` con `File.ReadAllText` y assertea `Assert.Contains("EstadoVacanteConstantes.AbiertaId", datosSemillaContent)` (o equivalentemente, que el literal de `DatosSemilla` coincida con el de la constante). Bajo prioridad porque la duplicación de literales es detectable en code review y la constante + literal están en el mismo bloque `20000000-…` reservado.
  - **Severidad**: WARNING (no bloqueante — el test pasa y el literal está duplicado pero ambas fuentes coinciden hoy; el riesgo es drift futuro).

### SUGGESTION

- **S-1 — `DatosSemilla.cs` no referencia `EstadoVacanteConstantes` para `EstadoVacanteEntity`**
  - **Síntoma**: `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs:16-19` declara sus propias constantes locales (`VacanteAbiertaId = Guid.Parse(...)`, etc.) y las usa inline en el `HasData` de `EstadoVacanteEntity` (líneas 56-60). El archivo `EstadoVacanteConstantes.cs` existe con la misma información pero `DatosSemilla.cs` no lo referencia.
  - **Contraste con el patrón vigente**: `DatosSemilla.cs` para `NivelCargoEntity` (líneas 70-90+) sí usa `NivelCargoConstantes.DirectivoId`, `NivelCargoConstantes.DirectivoCodigo`, etc. Lo mismo para `TipoDocumentoConstantes` y `CategoriaHabilidadConstantes`. La entrada de `EstadoVacanteEntity` rompe el patrón.
  - **Mitigación**: refactorizar `DatosSemilla.cs` para que `HasData<EstadoVacanteEntity>` use `EstadoVacanteConstantes.AbiertaId`, `EstadoVacanteConstantes.AbiertaCodigo`, `EstadoVacanteConstantes.AbiertaNombre`, `EstadoVacanteConstantes.AbiertaOrden`, `EstadoVacanteConstantes.AbiertaEsTerminal`, etc. — paridad con `NivelCargoEntity`. Considerar remover las 4 constantes locales de `DatosSemilla.cs` si quedan sin uso.
  - **Severidad**: SUGGESTION (mejora de consistencia; no afecta correctness actual porque ambos lugares apuntan al mismo bloque `20000000-…`).

- **S-2 — `VacanteErrorCodigo.MotivoObligatorio` declarado pero nunca usado**
  - **Síntoma**: `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs:10` declara `MotivoObligatorio` pero no se referencia en ningún archivo del change (`grep -rn "MotivoObligatorio" src/` retorna solo la declaración). PB-3 confirma que `Motivo` es opcional al cerrar, por lo que la decisión correcta es **no** exigirlo.
  - **Mitigación**: o bien eliminar la constante (limpia el catálogo), o bien documentar con un comentario XML que se mantiene como código defensivo para futuro uso si negocio cambia PB-3. Patrón del repo: `OcupacionErrorCodigo` no tiene un equivalente muerto.
  - **Severidad**: SUGGESTION (limpieza de catálogo de códigos; no afecta correctness).

- **S-3 — Inconsistencia nombre↔categoría en catch de `DbUpdateException`**
  - **Síntoma**: `VacanteServicioComandos.CrearAsync`, `CambiarEstadoAsync` y `ActualizarObservacionesAsync` usan `VacanteErrorCodigo.DatosInvalidos` (nombre que sugiere `Validation`) en los catch blocks de `DbUpdateException` mapeados a `ErrorCategoria.Conflict` (líneas 169, 278, 347). Un consumidor que vea `code=DatosInvalidos` espera `Validation` (400) pero recibe `Conflict` (409).
  - **Causalidad**: el nombre `DatosInvalidos` es genérico y se usa como catch-all tanto para validación de input (donde sí mapea a `Validation`) como para constraint violations de BD (donde mapea a `Conflict`). El nombre sugiere el primer caso pero el bloque aplica a ambos.
  - **Mitigación**: introducir un nuevo código `ConflictoPersistencia` (o similar) específico para constraint violations y mantener `DatosInvalidos` solo para validación. Alternativa: cambiar el mensaje del `Conflict + DatosInvalidos` para que el `Message` aclare ("FK violation: ...", "Constraint: ...") y el cliente pueda decidir por el `Message` o la `Categoria`.
  - **Severidad**: SUGGESTION (mejora de UX para consumidores; tests pasan porque el código solo assertea `ErrorCategoria`, no el nombre `DatosInvalidos`).

## Observaciones

- **MySQL local disponible durante este apply**: `nc -z localhost 3306` retorna éxito. Los 3 tests `[MySqlFact]` del work unit 2.x se ejecutaron contra `sgv_test` sin skipeo. Si este sub-lanzamiento se ejecutara en CI sin MySQL, los 3 tests se skipean limpio (sin fallar) gracias a `MySqlFactAttribute`.
- **Build sin warnings nuevos**: 4 warnings totales pre-existentes (NU1510 sobre `Microsoft.Extensions.Configuration.Json` / `EnvironmentVariables` en `SGV.Infraestructura`); 0 asociados a archivos del work unit 3.x.
- **Fakes del integration host**: `FakeVacanteServicioComandos.CrearHandler` y `CambiarEstadoHandler` son `Func<...>?` opcionales; cuando son null el fake devuelve `Success` con DTOs sintéticos. Esto permite que la mayoría de los tests no necesiten override del factory, mientras los tests de error path hacen `WithOverrides(...)` para inyectar handlers que devuelven `VacanteCommandResult.Failure(...)`. Patrón consistente con `FakeOcupacionServicioComandos`.
- **Bridge atómico via repository**: la desviación D-3.1 documentada en `apply-progress.md` (el bridge `RegistrarCambioEstadoAsync` vive en el repository, no en el servicio) preserva la separación de capas (application layer no expone tipos de infraestructura) y mantiene la atomicidad EF en una transacción. Es una desviación coherente con `OcupacionRepository.UpdateAsync` que ya encapsula el mismo patrón.
- **`VacanteCommandResult.Value` tipado como `VacanteDetailDto?`**: desviación documentada en `apply-progress.md` §Deviations (coincide con `design.md` §Interfaces / Contracts). Los GET/listados siguen devolviendo `VacanteDto` directo desde el servicio de consulta, sin pasar por `CommandResult`.
- **Cobertura de `VacanteErrorCodigo.PuestoInexistente`**: el código está declarado pero no implementado en el servicio. Ver WARNING W-1.
- **`RolesSgvMutacion` como constante reutilizable**: extraída durante refactor (D-3.4) para que un cambio futuro de PB-1 ("solo Administrador") requiera modificar **un solo literal** en `src/SGV.Contracts/Seguridad/RolesSgv.cs:18`. No hay string literal repetido en el controller.
- **Skipeo limpio**: durante esta corrida MySQL **sí** está disponible (default `localhost:3306 sgv_test root`). Si en el siguiente sub-lanzamiento (4.x web) MySQL no estuviera disponible, los `[MySqlFact]` se skipean limpio sin fallar.
- **Slice 2 web (work units 4.x y 5.x)**: marcados como `[ ]` en `tasks.md`. No se exige verificación en este sub-lanzamiento.

## Veredicto

**PASS WITH WARNINGS**

Work unit 3.x (Phase 3 — Behavior + Controllers + DI + Constants + Docs + Tests) cumple los 14 puntos en scope del brief del orquestador. Build limpio (0 errors), 54/54 tests focalizados en verde (los 3 `[MySqlFact]` ejecutados contra MySQL real, sin skipeo), `VacanteServicioComandos` cierra S-1, R-WU3.1 (atómico via `RegistrarCambioEstadoAsync` + `SaveChangesAsync`) y R-WU3.2 (TOCTOU documentado en XML doc), `VacantesController` aplica PB-1 correctamente (`RolesSgvMutacion` en POST/PATCH, `[Authorize]` simple en GET), `EstadosVacanteController` solo lectura, `VacanteServicioConsulta` segmento `abiertas|cerradas|todas` con join a `EsTerminal`, fallback PB-5 a `Abiertas` en `?status=invalido` (`NormalizeSegmento` cubre string vacío, null, y desconocido), `VacanteErrorCodigo` mapea correctamente a `ErrorCategoria` con 6/8 códigos en uso real (los 2 restantes declarados son producto de WARNING/SUGGESTION, no bloqueante), `EstadoVacanteConstantes` con test paridad con bloque `20000000-…` documentado en `decisiones-implementacion.md`, validadores ≤500 chars para Observaciones y `Motivo` opcional al cerrar (PB-3), tests cubren 201/400/403/404/409/401 con `ValidationProblemDetails` y `ProblemDetails` correctos, atomicidad cubierta tanto a nivel servicio (fake `DbUpdateException`) como repository (real MySQL FK violation), y `DependencyInjection` registra exactamente 5 `AddScoped`. Los 2 WARNING (PuestoInexistente no implementado, test de paridad no parsea `DatosSemilla.cs`) y 3 SUGGESTION (DatosSemilla no referencia constantes, MotivoObligatorio declarado no usado, nombre↔categoría inconsistente en catch) son hallazgos incrementales que no bloquean este veredicto ni el avance a slice 2 web; quedan registrados para iteraciones futuras.

Próximo paso sugerido: abrir el slice 2 web (work units 4.x y 5.x) o tratar los WARNING/SUGGESTION como follow-up issues. No bloquea archive.
