# Tasks: invertir-flujo-cubrir

> Inversión del flujo Cubrir: la creación de Ocupación con `VacanteId` opcional se vuelve el único camino para cubrir una Vacante; `PATCH .../estado` con destino Cubierta se rechaza con `400 Validation`. Estrategia: **stacked PRs a `develop`** (S1 backend + wire, S2 frontend Create, S3 frontend Details). TDD: cada tarea de implementación va precedida por su test rojo. `strict_tdd: true` activo.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~370–460 (S1: 170–200, S2: 120–150, S3: 80–110) |
| 400-line budget risk | Medium (per-PR) — cada PR se mantiene ≤400; el total se reparte en 3 PRs |
| Chained PRs recommended | Yes (3 PRs stacked-to-develop) |
| Suggested split | S1 backend+wire → S2 frontend Create → S3 frontend Details |
| Delivery strategy | auto-chain (decidido en preflight) |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Low (per-PR, con split en 3)

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Backend + wire contracts + tests Aplicacion/API | S1 | `dotnet test --filter "FullyQualifiedName~OcupacionServicioComandosTests\|VacanteServicioComandosTests\|VacanteServicioConsultaTests"` | API HTTP local (sin MySQL → skip limpio de `[MySqlFact]`) | `git revert S1`: revierte wire contracts, `OcupacionServicioComandos.CrearAsync`, `VacanteServicioComandos.CambiarEstadoAsync` y `VacanteServicioConsulta`. S2/S3 requieren rebase. |
| 2 | Frontend Create con `?vacanteId` + label dinámico | S2 | `dotnet test --filter "FullyQualifiedName~OcupacionCreatePageTests\|PuestoOcupacionesPageTests"` | `bun run build` + navegación manual a `/organizacion/ocupaciones/crear?vacanteId=...` | `git revert S2`: restaura `OnGetAsync` sin `vacanteId`, `_Form.cshtml` sin bloque, `PuestoOcupaciones` con literal "Nueva ocupación". S3 requiere rebase. |
| 3 | Frontend Details con botón Cubrir + bloque Persona | S3 | `dotnet test --filter "FullyQualifiedName~VacantesDetailsAndSidenavTests"` | `bun run build` + navegación manual a `/organizacion/vacantes/detalles/{id}` | `git revert S3`: restaura `Details.cshtml` sin botón/bloque, `VacanteDetailViewModel` sin campos extra. Sin dependientes. |

## Cierre de Q-T1..Q-T5 (decisiones de tasks)

### Q-T1 — Tests `[Fact]` vs `[MySqlFact]` para `OcupacionServicioComandos.CrearAsync_ConVacanteId_*`

- **Decisión**: `[Fact]` puro con `FakeUnitOfWork` y mocks de `IVacanteRepository` / `IOcupacionRepository` para todos los escenarios del path validación (happy path, Vacante no encontrada, Vacante no Abierta, Vacante ya cubierta, `PuestoId` mismatch). Para el escenario de **atomicidad** (rollback), usar un `FakeThrowingUnitOfWork` (variante de `FakeUnitOfWork` que lanza `DbUpdateException` en `SaveChangesAsync`) y `[Fact]` también. NO `[MySqlFact]` salvo que aparezca un path que requiera UNIQUE constraint real (no es el caso: las validaciones de unicidad son lógicas en servicio).
- **Razonamiento**: las validaciones de dominio (existencia, estado, match `PuestoId`) son puramente lógicas y no tocan constraints BD. El escenario de rollback ya está cubierto declarativamente por el catch `DbUpdateException when constraintDetector.IsConstraintViolation` que mapea a error funcional — basta con forzar la excepción en un fake para validar la rama. `[MySqlFact]` agrega costo de setup MySQL sin protección adicional.
- **Implicancia**: T1.1–T1.5 y T1.6 usan `[Fact]` puro. Re-correr T1.6 con `dotnet test --filter "CrearAsync_ConVacanteId_FalloEnSaveChanges"` cubre el path rollback sin MySQL. Si en apply aparece un path con constraint real no previsto, se promueve a `[MySqlFact]`.

### Q-T2 — Resolución de `vacanteId` desde `PuestoOcupaciones`

- **Decisión**: agregar `Task<VacanteDto?> ObtenerAbiertaPorPuestoAsync(Guid puestoId, CancellationToken ct)` a `IVacanteApiClient`. Implementación: consultar `GET /api/v1/vacantes?status=abiertas&puestoId={puestoId}` (listado segmentado vigente), tomar el primer resultado y mapear a `VacanteDto`; retornar `null` si la lista está vacía. Se invoca desde `PuestoOcupaciones.cshtml.cs` para alimentar `NewOcupacionRouteValues` con `vacanteId`. Mantener `ExisteVacanteAbiertaParaPuestoAsync` para el caso boolean.
- **Razonamiento**: el listado segmentado ya existe (`VacanteSegmentoListado.Abiertas`) y respeta el patrón vigente del repo. Un método nuevo dedicado (`ObtenerAbiertaPorPuesto`) sería más limpio pero agrega scope de DTO/API no contemplado en este change. Reutilizar el listado evita un endpoint nuevo y mantiene coherencia con `ExisteVacanteAbiertaParaPuestoAsync`.
- **Implicancia**: T2.13 agrega el método al interface + implementación + tests de `FakeVacanteApiClient`/`Fake` para los PageModelTests.

### Q-T3 — `NewOcupacionButtonLabel` en `IOcupacionesCrossList`

- **Decisión**: agregar `string NewOcupacionButtonLabel { get; }` a `IOcupacionesCrossList` con default implícito `"Nueva ocupación"` (devolver literal en `PersonaOcupacionesModel` no es necesario — usar default del interface). `PuestoOcupacionesModel` overridea a `"Cubrir Vacante"` cuando `HayVacanteAbierta && !HayOcupacionActiva`; cae al default en cualquier otro caso visible.
- **Razonamiento**: el default del interface mantiene `PersonaOcupaciones` sin cambios (REQ-OCC-NAV-006 vigente: alta desde Persona mantiene label). Solo `PuestoOcupaciones` aporta lógica específica. Esto evita tocar `PersonaOcupacionesModel` y reduce diff de S2.
- **Implicancia**: T2.7 agrega la propiedad al interface; T2.12 implementa el override en `PuestoOcupacionesModel`; T2.14 modifica `_CrossList.cshtml` para leer `@Model.NewOcupacionButtonLabel` en lugar del literal.

### Q-T4 — `docs/migracion-inicial-sgv.sql`

- **Decisión**: NO regenerar el script SQL. La columna `Ocupaciones.VacanteId` ya existe nullable desde el change archivado `vacante-ocupacion-flow-alignment`. No hay migración nueva en este change.
- **Razonamiento**: el proposal y design lo confirman (sección "Out of Scope" y D-3). Regenerar el script sin cambios introduciría ruido en el diff.
- **Implicancia**: nota explícita en `sdd-verify` para que el verificador confirme que `docs/migracion-inicial-sgv.sql` no requiere regeneración y que los `[MySqlFact]` aplicables siguen pasando sin cambios de esquema.

### Q-T5 — Entrada nueva en `docs/decisiones-implementacion.md`

- **Decisión**: agregar entrada **"Inversión del flujo Cubrir (2026-08-09)"** en la fase de apply (no en este artefacto). Documentar D-1 (inversión del flujo), D-3 (hidratación defensiva de `VacanteDetailDto.OcupacionDerivadaId/PersonaAsignadaNombre`) y D-4 (renombre de `PersonaIdRequeridoParaCubrir` → `CubrirVacanteRequiereCrearOcupacion` con `[Obsolete]` backup).
- **Razonamiento**: el design lo sugiere explícitamente en H-Q5. La entrada la escribe `sdd-apply` cuando confirma el merge de S1, no en tasks. Documentar acá el "qué" para que apply sepa "qué" sin re-leer el design.
- **Implicancia**: tarea T1.31 marcada como **applies-in-apply-phase** (no en este tasks), con checklist que `sdd-apply` debe completar antes del merge de S1.

---

## PR S1 — Backend + Wire contracts (~170–200 líneas)

### [ ] T1.1 [test red]: `OcupacionServicioComandos.CrearAsync_ConVacanteId_VacanteAbierta_CreaOcupacionYTransicionaVacanteACubierta`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`
- Descripción: test `[Fact]` que mockea `IVacanteRepository` (Devuelve Vacante Abierta con `PuestoId=P1`) y verifica: `AddAsync` de Ocupación llamado con `VacanteId`, `PuestoId=P1`, `EsVigente=true`; `CambiarEstado`+`RegistrarCambioEstadoAsync` invocados para Vacante; `SaveChangesCount=1`.
- Criterio: `Assert.True(resultado.IsSuccess)` + asserts sobre `Mock<IVacanteRepository>.Verify(...)` + `Assert.Equal(1, uow.SaveChangesCount)`. RED antes de la impl.
- Líneas estimadas: 60

### [ ] T1.2 [test red]: `CrearAsync_ConVacanteId_VacanteNoEncontrada_DevuelveNotFound`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`
- Descripción: `[Fact]` con `IVacanteRepository.GetByIdForUpdateAsync` retornando `null` → respuesta `Failure` con `ErrorCategoria.NotFound`, código `OcupacionErrorCodigo.VacanteNoEncontrada`.
- Criterio: `Assert.Equal(ErrorCategoria.NotFound, ...)` + `Assert.Equal(OcupacionErrorCodigo.VacanteNoEncontrada, ...)`. `SaveChangesCount=0`.
- Líneas estimadas: 25

### [ ] T1.3 [test red]: `CrearAsync_ConVacanteId_VacanteCubierta_Devuelve400_VacanteNoAbierta`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`
- Descripción: `[Fact]` con Vacante `Cubierta` (`EstadoVacante.EsTerminal=true && EsCubierta=true`) → `Failure(Validation, VacanteNoAbierta, ...)`.
- Criterio: `Assert.Equal(ErrorCategoria.Validation, ...)` + código `VacanteNoAbierta` + `SaveChangesCount=0`.
- Líneas estimadas: 25

### [ ] T1.4 [test red]: `CrearAsync_ConVacanteId_VacanteYaCubierta_Devuelve409_VacanteYaCubierta`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`
- Descripción: `[Fact]` con `IOcupacionRepository.ExistsActiveByVacanteAsync` retornando `true` para la Vacante Abierta → `Failure(Conflict, VacanteYaCubierta, ...)`.
- Criterio: `Assert.Equal(ErrorCategoria.Conflict, ...)` + código `VacanteYaCubierta` + `SaveChangesCount=0`.
- Líneas estimadas: 25

### [ ] T1.5 [test red]: `CrearAsync_ConVacanteId_PuestoIdNoCoincide_Devuelve400_PuestoIdNoCoincideConVacante`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`
- Descripción: `[Fact]` con Vacante `PuestoId=P1` y request `PuestoId=P2` → `Failure(Validation, PuestoIdNoCoincideConVacante, ...)`.
- Criterio: `Assert.Equal(ErrorCategoria.Validation, ...)` + código + `FieldErrors` con clave `puestoId`. RED.
- Líneas estimadas: 25

### [ ] T1.6 [test red]: `CrearAsync_ConVacanteId_FalloEnSaveChanges_NoCreaOcupacionYNoTransicionaVacante`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs`
- Descripción: `[Fact]` con `FakeThrowingUnitOfWork` (variante que lanza `DbUpdateException` en `SaveChangesAsync`) → respuesta `Failure` con código de error de constraint (`DatosInvalidos` o `VacanteYaCubierta` según mapeo vigente). Verifica que ninguna inserción quedó confirmada.
- Criterio: `Assert.False(resultado.IsSuccess)` + asserts sobre que `CambiarEstado` se invocó pero no se persistió (assert en mock: `Verify(..., Times.Once)` + `Verify(SaveChangesAsync, Times.Once)` que lanzó). RED.
- Líneas estimadas: 30

### [ ] T1.7 [impl]: Agregar `Guid? VacanteId` a `CrearOcupacionRequest`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Contracts/Ocupaciones/Comandos/CrearOcupacionRequest.cs`
- Descripción: agregar parámetro `Guid? VacanteId = null` al final del record (backward-compatible). XML doc explicando que cuando está setado, `PuestoId` puede omitirse (se resuelve desde la Vacante).
- Criterio: `dotnet build SGV.slnx` sin warnings nuevos de nullable. Tests de T1.1–T1.6 aún en RED (la firma no compila los asserts porque el campo no existe → falla de compilación, parte de la RED).
- Líneas estimadas: 8

### [ ] T1.8 [impl]: Agregar códigos a `OcupacionErrorCodigo` y renombrar en `VacanteErrorCodigo`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionErrorCodigo.cs`, `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs`
- Descripción: agregar `VacanteNoEncontrada`, `VacanteNoAbierta`, `VacanteYaCubierta`, `PuestoIdNoCoincideConVacante` a `OcupacionErrorCodigo`. Renombrar `PersonaIdRequeridoParaCubrir` → `CubrirVacanteRequiereCrearOcupacion` en `VacanteErrorCodigo`. Marcar el código viejo como `[Obsolete("…use CubrirVacanteRequiereCrearOcupacion…")]` (constante obsoleta con sufijo `_Obsolete` o atributo en XML doc — verificar patrón vigente del repo).
- Criterio: `dotnet build SGV.slnx` compila. Tests de T1.2–T1.5 + T1.13–T1.14 (en este PR) referencian los nuevos códigos.
- Líneas estimadas: 15

### [ ] T1.9 [impl]: Extender `IOcupacionRepository`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionRepository.cs`
- Descripción: agregar `Task<(Guid Id, string PersonaNombre)?> ObtenerVigentePorVacanteAsync(Guid vacanteId, CancellationToken)` y `Task<bool> ExistsActiveByVacanteAsync(Guid vacanteId, CancellationToken)`. XML doc explicando uso (hidratación de DTO / validación de unicidad).
- Criterio: `dotnet build` compila. Tests existentes que usan `FakeOcupacionWriteRepository` deben agregar `throw new NotImplementedException()` en los métodos nuevos (parte de la RED del consumidor).
- Líneas estimadas: 12

### [ ] T1.10 [impl]: Implementar métodos en `OcupacionRepository`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs`
- Descripción: `ObtenerVigentePorVacanteAsync` → query con `AsNoTracking`, project `Id` + `Persona.Nombres + " " + Persona.Apellidos` con `FirstOrDefaultAsync`. `ExistsActiveByVacanteAsync` → `AnyAsync(o => o.VacanteId == vacanteId && !o.IsDeleted && o.FechaFin == null && o.EsVigente)`.
- Criterio: `dotnet build` compila. Cobertura: agregar un test de Persistencia (`OcupacionRepositoryQueryAsyncTests.cs` existente) con `[Fact]` puro si es posible; si requiere LINQ-to-Entities → `[MySqlFact]`.
- Líneas estimadas: 25

### [ ] T1.11 [impl]: Modificar `OcupacionServicioComandos.CrearAsync`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs`
- Descripción: al inicio de `CrearAsync` (después de validación Fluent), si `request.VacanteId.HasValue`: cargar Vacante vía `vacanteRepository.GetByIdForUpdateAsync`; si null → `VacanteNoEncontrada`; si `EsTerminal` (Cubierta/Cancelada) → `VacanteNoAbierta`; si `ExistsActiveByVacanteAsync` → `VacanteYaCubierta`; si request `PuestoId` no coincide → `PuestoIdNoCoincideConVacante`; resolver `PuestoId` desde Vacante si omitido. Crear Ocupación con `VacanteId`; invocar `vacante.CambiarEstado(estadoCubiertaId, ...)`; `vacanteRepository.RegistrarCambioEstadoAsync(vacante, historial)`; mismo `try/SaveChangesAsync` existente.
- Criterio: tests T1.1–T1.6 pasan en GREEN; suite Aplicacion no regresiona.
- Líneas estimadas: 50

### [ ] T1.12 [test green]: Re-correr T1.1–T1.6 y validar
- PR: S1
- Tipo: test (green) / commit de cierre del bloque T1.1–T1.11
- Archivos: ninguno nuevo (validación)
- Descripción: ejecutar `dotnet test --filter "FullyQualifiedName~OcupacionServicioComandosTests.CrearAsync_ConVacanteId"`. Todos los 6 tests deben estar en GREEN.
- Criterio: `Passed: 6`, `Failed: 0`. Si falla alguno, fix mínimo y re-correr.
- Líneas estimadas: 0

### [ ] T1.13 [test red]: `VacanteServicioComandos.CambiarEstado_A_Cubierta_Devuelve400ConCodigoCubrirVacanteRequiereCrearOcupacionYMensaje`
- PR: S1
- Tipo: test (red) — **reemplaza** `CambiarEstado_A_Cubierta_ConPersonaId_CreaOcupacionYRegistraHistorial` existente
- Archivos: `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs`
- Descripción: Reescribir el test vigente: ahora afirma `Failure(Validation, CubrirVacanteRequiereCrearOcupacion, "Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada.")`. Sin importar `PersonaId` (con o sin → mismo resultado). `SaveChangesCount=0`.
- Criterio: RED → el código vigente crea Ocupación y guarda; el test afirma lo contrario → falla.
- Líneas estimadas: 30

### [ ] T1.14 [test red]: `CambiarEstado_A_Cubierta_ConPersonaIdPopulado_TambienIgnoraPersonaId`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs`
- Descripción: `[Fact]` con `PersonaId=Guid.NewGuid()` populado y destino Cubierta → `Failure(Validation, CubrirVacanteRequiereCrearOcupacion, ...)` idéntico al de T1.13.
- Criterio: `Assert.Equal(OcupacionErrorCodigo.VacanteNoEncontrada, ...)` no aplica — es `VacanteErrorCodigo.CubrirVacanteRequiereCrearOcupacion`. `Assert.Equal(0, uow.SaveChangesCount)`.
- Líneas estimadas: 20

### [ ] T1.15 [impl]: Renombrar y deprecar código de error
- PR: S1
- Tipo: impl (parte de T1.8 — puede commitearse junto o separado)
- Archivos: `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs`
- Descripción: ya cubierto por T1.8 (renombre + `[Obsolete]` backup).
- Criterio: tests T1.13–T1.14 referencian el nuevo nombre → pasan en GREEN tras T1.16.
- Líneas estimadas: 0 (subsumido en T1.8)

### [ ] T1.16 [impl]: Reemplazar bloque de creación de Ocupación en `VacanteServicioComandos.CambiarEstadoAsync`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs`
- Descripción: eliminar líneas 288–344 (validación `PersonaId null` + bloque `if (destinoEsCubierta) { new Ocupacion(...); ocupacionRepository.AddAsync(...) }`). Reemplazar por: si `estadoNuevo.EsCubierta` → `Failure(Validation, VacanteErrorCodigo.CubrirVacanteRequiereCrearOcupacion, "Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada.")` con `FieldErrors["personaId"]` vacío (legacy). El campo `PersonaId` se ignora silenciosamente.
- Criterio: tests T1.13–T1.14 GREEN; suite Aplicacion no regresiona.
- Líneas estimadas: 25 (incluye eliminación del bloque)

### [ ] T1.17 [test green]: Re-correr T1.13–T1.14
- PR: S1
- Tipo: test (green)
- Archivos: ninguno nuevo
- Descripción: `dotnet test --filter "FullyQualifiedName~VacanteServicioComandosTests.CambiarEstado_A_Cubierta"`.
- Criterio: `Passed: 2+`, `Failed: 0`.
- Líneas estimadas: 0

### [ ] T1.18 [test red]: `VacanteServicioConsulta.ObtenerPorIdAsync_VacanteCubierta_ConOcupacionDerivadaDevuelveOcupacionDerivadaIdYPersonaAsignada`
- PR: S1
- Tipo: test (red) — **reemplaza** el test vigente que esperaba `null`
- Archivos: `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioConsultaTests.cs`
- Descripción: Reescribir el test vigente que afirmaba `OcupacionDerivadaId=null`. Ahora: Vacante Cubierta con Ocupación derivada (mock de `IOcupacionRepository.ObtenerVigentePorVacanteAsync` retorna `(Guid, "Juan Pérez")`) → DTO tiene `OcupacionDerivadaId = mockId` y `PersonaAsignadaNombre = "Juan Pérez"`.
- Criterio: RED → el código vigente no consulta Ocupaciones → `OcupacionDerivadaId=null`. El test afirma distinto → falla.
- Líneas estimadas: 25

### [ ] T1.19 [test red]: `ObtenerPorIdAsync_VacanteAbierta_DevuelveOcupacionDerivadaIdNull`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioConsultaTests.cs`
- Descripción: Vacante Abierta + mock `ObtenerVigentePorVacanteAsync` retorna `null` → DTO tiene `OcupacionDerivadaId=null` y `PersonaAsignadaNombre=null`.
- Criterio: RED → el código vigente ya devuelve `null` para estos campos, pero el test verifica que NO se hace query a Ocupaciones cuando la Vacante no es Cubierta (assert en mock: `Verify(ObtenerVigentePorVacanteAsync, Times.Never)`). Falla porque el código nuevo hace query siempre.
- Líneas estimadas: 20

### [ ] T1.20 [impl]: Inyectar `IOcupacionRepository` en `VacanteServicioConsulta`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Aplicacion/Vacantes/Consultas/VacanteServicioConsulta.cs`, `src/SGV.Aplicacion/Vacantes/Consultas/IVacanteServicioConsulta.cs`
- Descripción: agregar `IOcupacionRepository ocupacionRepository` al primary constructor (es un `sealed` con primary constructor, sin DI explícito). Actualizar firma de `IVacanteServicioConsulta` si expone el constructor; sino, la firma del servicio concreta cambia y el DI resuelve.
- Criterio: `dotnet build` compila. El cambio en la firma de DI requiere que `VacanteServicioConsultaTests` ajuste la construcción del SUT.
- Líneas estimadas: 10

### [ ] T1.21 [impl]: Extender `VacanteDetailDto`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Contracts/Vacantes/Consultas/Dtos/VacanteDetailDto.cs`
- Descripción: agregar `Guid? OcupacionDerivadaId, string? PersonaAsignadaNombre` al final del record. XML doc con semántica (null si no hay Ocupación derivada; defensivo si Cubierta sin Ocupación).
- Criterio: `dotnet build` compila. Tests API que serializan el DTO deben verificar los nuevos campos.
- Líneas estimadas: 5

### [ ] T1.22 [impl]: Hidratar DTO en `ObtenerPorIdAsync`
- PR: S1
- Tipo: impl
- Archivos: `src/SGV.Aplicacion/Vacantes/Consultas/VacanteServicioConsulta.cs`
- Descripción: en `ObtenerPorIdAsync`, después de `MapToDetailDto(vacante)`, si `vacante.EstadoVacante?.EsCubierta == true` (o cualquier estado terminal con cobertura potencial) llamar `ocupacionRepository.ObtenerVigentePorVacanteAsync(vacante.Id, ct)`. Si retorna `(Guid, string)` → construir nuevo `VacanteDetailDto` con `with { OcupacionDerivadaId = id, PersonaAsignadaNombre = nombre }`. Si retorna `null` → DTO con campos null.
- Criterio: tests T1.18–T1.19 GREEN. Defensivo: T1.19 cubre Vacante Abierta con `Verify(...Times.Never)` para no llamar al repo cuando no aplica.
- Líneas estimadas: 20

### [ ] T1.23 [test green]: Re-correr T1.18–T1.19
- PR: S1
- Tipo: test (green)
- Archivos: ninguno nuevo
- Descripción: `dotnet test --filter "FullyQualifiedName~VacanteServicioConsultaTests.ObtenerPorIdAsync"`.
- Criterio: `Passed: 2+`, `Failed: 0`.
- Líneas estimadas: 0

### [ ] T1.24 [test red]: API test `OcupacionesController.Create_ConVacanteId_Returns201`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Api/OcupacionesControllerTests.cs`
- Descripción: `[Fact]` integración vía `ApiWebApplicationFactory`. POST `/api/v1/ocupaciones` con body JSON conteniendo `vacanteId` válido + personaId + puestoId + fechaInicio + tipoAsignacion. Esperar `201 Created` con DTO que tiene `vacanteId` en respuesta (si aplica) o null si el DTO vigente no expone el campo. Verificar que la respuesta es Success.
- Criterio: RED → el endpoint actual ignora `vacanteId` o falla validación. Falla esperada.
- Líneas estimadas: 30

### [ ] T1.25 [test red]: API test `OcupacionesController.Create_ConVacanteId_VacanteCubierta_Returns409`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Api/OcupacionesControllerTests.cs`
- Descripción: `[Fact]` con Vacante Cubierta preexistente (seed) → POST con `vacanteId` de esa Vacante → `409 Conflict` con código `VacanteYaCubierta` o `VacanteNoAbierta` (según semántica final; el spec dice `VacanteYaCubierta` para Ocupación existente, `VacanteNoAbierta` para Cubierta sin Ocupación). Elegir `VacanteYaCubierta` para este test.
- Criterio: `Assert.Equal(HttpStatusCode.Conflict, response.StatusCode)` + `Assert.Contains("VacanteYaCubierta", body)`.
- Líneas estimadas: 25

### [ ] T1.26 [test red]: API test `VacantesController.PatchEstado_A_Cubierta_Returns400ConMensajeUseCubrirVacante`
- PR: S1
- Tipo: test (red) — **reemplaza** `PatchEstado_A_Cubierta_ConPersonaId_CreaOcupacion` vigente
- Archivos: `tests/SGV.Tests/Api/VacantesControllerTests.cs`
- Descripción: PATCH `/api/v1/vacantes/{id}/estado` con destino Cubierta → `400 Bad Request` con código `CubrirVacanteRequiereCrearOcupacion` y mensaje conteniendo "Use el botón 'Cubrir Vacante'". Sin creación de Ocupación derivada (assert sobre GET `/ocupaciones?puestoId=...` → no aparece nueva).
- Criterio: `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` + assert en JSON del body.
- Líneas estimadas: 30

### [ ] T1.27 [test red]: API test `VacantesController.GetById_VacanteCubierta_RetornaOcupacionDerivadaIdYPersonaAsignada`
- PR: S1
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Api/VacantesControllerTests.cs` o `tests/SGV.Tests/Api/Vacantes/VacantesControllerDetailTests.cs` (nuevo)
- Descripción: GET `/api/v1/vacantes/{id}` con Vacante Cubierta + Ocupación derivada seed → `200 OK` con DTO que tiene `ocupacionDerivadaId != null` y `personaAsignadaNombre != null`.
- Criterio: `Assert.NotNull(dto.OcupacionDerivadaId)` + `Assert.Equal("Juan Pérez", dto.PersonaAsignadaNombre)`.
- Líneas estimadas: 30

### [ ] T1.28 [test green]: Re-correr T1.24–T1.27
- PR: S1
- Tipo: test (green)
- Archivos: ninguno nuevo
- Descripción: `dotnet test --filter "FullyQualifiedName~OcupacionesControllerTests|VacantesControllerTests"`.
- Criterio: `Passed: 4+`, `Failed: 0`. Si fallan, fix mínimo.
- Líneas estimadas: 0

### [ ] T1.29 [refactor]: Eliminar `using Ocupaciones` y dependencia dead-code
- PR: S1
- Tipo: refactor
- Archivos: `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs`, posiblemente `VacanteServicioComandosTests.cs` (ajuste de firma del SUT)
- Descripción: tras T1.16, `VacanteServicioComandos` ya no usa `IOcupacionRepository` ni el namespace `SGV.Aplicacion.Ocupaciones.Consultas`. Eliminar `using` correspondiente y el parámetro `IOcupacionRepository` del constructor (tanto primary como convenience). Actualizar el DI registration (si está hardcodeado) y los tests que construyen el SUT.
- Criterio: `dotnet build` + `dotnet test --filter "FullyQualifiedName~VacanteServicioComandosTests"` sin regresiones.
- Líneas estimadas: 15

### [ ] T1.30 [docs]: Marcar `CambiarEstadoVacanteRequest.PersonaId` como deprecated
- PR: S1
- Tipo: docs
- Archivos: `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs`
- Descripción: XML doc en el record summary mencionando que `PersonaId` está deprecado; el flujo Cubrir vive en `OcupacionServicioComandos.CrearAsync` con `VacanteId`. Considerar agregar `[Obsolete("…use Ocupaciones Create con VacanteId…")]` al parámetro si el repo lo soporta en records (verificar patrón vigente).
- Criterio: `dotnet build` sin warnings nuevos; suite de tests no se afecta.
- Líneas estimadas: 5

### [ ] T1.31 [docs/apply-phase]: Entrada en `docs/decisiones-implementacion.md`
- PR: S1
- Tipo: docs (applies-in-apply-phase, no en este tasks)
- Archivos: `docs/decisiones-implementacion.md`
- Descripción: `sdd-apply` agrega entrada "Inversión del flujo Cubrir (2026-08-09)" documentando D-1, D-3 y D-4 (design §B). Aplicar ANTES del merge de S1.
- Criterio: archivo actualizado, suite de tests `Docs/CoherenciaDecisionesImplementacionTests` no regresiona.
- Líneas estimadas: 20

---

## PR S2 — Frontend Create (~120–150 líneas)

### T2.1 [test red]: `OcupacionCreatePageTests.?vacanteId_ConVacanteAbierta_RendereaFormConPuestoIdBloqueadoYVgHint`
- PR: S2
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs`
- Descripción: `[Fact]` Web con `?vacanteId={guid}` y `IVacanteApiClient.ObtenerPorIdAsync` mockeado retornando Vacante Abierta + `PuestoId=P1`. Verificar: form rendereado, `Input.PuestoId==P1`, dropdown PuestoId tiene `disabled`, `VacanteHintLabel` contiene "Esta Vacante" + nombre del Puesto.
- Criterio: `Assert.True(form.Renderero)` + `Assert.Equal(P1, pageModel.Input.PuestoId)` + assert sobre HTML del dropdown (search `disabled`).
- Líneas estimadas: 35

### T2.2 [test red]: `OcupacionCreatePageTests.?vacanteId_ConVacanteCubierta_MuestraError_VacanteYaCubierta`
- PR: S2
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs`
- Descripción: `[Fact]` Web con `?vacanteId={guid}` y Vacante Cubierta → página muestra mensaje "Esta Vacante ya está cubierta." (NO se renderea form).
- Criterio: `Assert.Contains("Esta Vacante ya está cubierta.", html)` + `Assert.False(form.Renderero)`.
- Líneas estimadas: 20

### T2.3 [test red]: `OcupacionCreatePageTests.?vacanteId_ConVacanteInexistente_MuestraError_VacanteNoExiste`
- PR: S2
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs`
- Descripción: `[Fact]` Web con `?vacanteId={guid}` y `IVacanteApiClient.ObtenerPorIdAsync` mockeado retornando `null` → página muestra mensaje "La Vacante no existe.".
- Criterio: `Assert.Contains("La Vacante no existe.", html)`.
- Líneas estimadas: 20

### T2.4 [test red]: `PuestoOcupacionesPageTests.VacanteAbiertaSinOcupacion_LabelCubrirVacante`
- PR: S2
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Ocupaciones/PuestoOcupacionesPageTests.cs`
- Descripción: `[Fact]` con `HayVacanteAbierta=true` y `HayOcupacionActiva=false` → `NewOcupacionButtonLabel == "Cubrir Vacante"` y `NewOcupacionRouteValues` contiene `vacanteId`.
- Criterio: `Assert.Equal("Cubrir Vacante", label)` + assert sobre `routeValues.vacanteId`.
- Líneas estimadas: 25

### T2.5 [test red]: `PuestoOcupacionesPageTests.VacanteAbiertaConOcupacion_LabelNuevaOcupacion`
- PR: S2
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Ocupaciones/PuestoOcupacionesPageTests.cs`
- Descripción: `[Fact]` con `HayVacanteAbierta=true` y `HayOcupacionActiva=true` → `NewOcupacionButtonLabel == "Nueva ocupación"` (default).
- Criterio: `Assert.Equal("Nueva ocupación", label)`.
- Líneas estimadas: 15

### T2.6 [impl]: Extender `IOcupacionForm` y `OcupacionFormPageModel`
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs`, `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionFormPageModel.cs`, `src/SGV.Web/Integration/Ocupaciones/OcupacionInputModel.cs`
- Descripción: agregar `Guid? VacanteId` al `OcupacionInputModel` (campo hidden, con `[BindProperty]` si aplica). Agregar `string? VacanteHintLabel { get; }` al interface con default `null`. Crear `protected string? VacanteHintLabel { get; set; }` en el base PageModel.
- Criterio: tests T2.1–T2.3 (RED) aún fallan porque el flujo `?vacanteId` no está conectado.
- Líneas estimadas: 12

### T2.7 [impl]: Agregar `NewOcupacionButtonLabel` a `IOcupacionesCrossList`
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionesCrossList.cs`
- Descripción: agregar `string NewOcupacionButtonLabel { get; }` con default implícito `=> "Nueva ocupación";` (C# 8 default interface member). Documentar que `PuestoOcupacionesModel` lo overridea.
- Criterio: `dotnet build` compila; tests de `PersonaOcupacionesPageTests` (que no overridean) siguen verdes con default.
- Líneas estimadas: 8

### T2.8 [impl]: Modificar `Create.cshtml.cs` OnGet con `?vacanteId`
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml.cs`
- Descripción: agregar parámetro `[FromQuery(Name = "vacanteId")] Guid? vacanteId` a `OnGetAsync`. Si está setado: invocar `vacanteApiClient.ObtenerPorIdAsync(vacanteId)`; si null → `ErrorMessage="La Vacante no existe."` y return Page; si `EsCubierta` → `ErrorMessage="Esta Vacante ya está cubierta."`; si `EsCancelada` → `ErrorMessage="Esta Vacante está cancelada y no puede cubrirse."`; si Abierta/En Selección → set `Input.VacanteId=vacanteId`, `Input.PuestoId=vacante.PuestoId`, `VacanteHintLabel="Esta Vacante del Puesto {NombrePuesto}…"` y `_puestoTieneVacanteCache = true` (para que ReloadFormState no re-consulte).
- Criterio: tests T2.1–T2.3 GREEN.
- Líneas estimadas: 30

### T2.9 [impl]: Modificar `Create.cshtml` para mostrar errores/hints
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml`
- Descripción: agregar bloque al inicio del card: si `ErrorMessage` no es null → `<div class="alert alert-warning">{ErrorMessage}</div>`; si `VacanteHintLabel` no es null → `<div class="alert alert-info">{VacanteHintLabel}</div>` (después del header, antes del form).
- Criterio: tests T2.1–T2.3 verifican presencia de los strings.
- Líneas estimadas: 15

### T2.10 [impl]: Modificar `_Form.cshtml` con `VacanteId` hidden + dropdown bloqueado
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml`
- Descripción: si `Model.Input.VacanteId.HasValue` → agregar `<input type="hidden" asp-for="Input.VacanteId" />`; en el dropdown de PuestoId, agregar atributo `disabled` (y un hidden adicional que preserve el valor para model binding si el dropdown está disabled). Verificar patrón vigente para dropdown bloqueado (probablemente ya existe para `?puestoId`).
- Criterio: tests T2.1 verifican `disabled` en el HTML.
- Líneas estimadas: 12

### T2.11 [impl]: Modificar `PuestoOcupaciones.cshtml.cs` para popular `vacanteId`
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs`
- Descripción: en el `OnGetAsync`, después de cargar `HayVacanteAbierta`, si `true && !HayOcupacionActiva` → invocar `vacanteApiClient.ObtenerAbiertaPorPuestoAsync(PuestoId, ct)` (Q-T2) y guardar `Guid? _vacanteAbiertaId`. Modificar `NewOcupacionRouteValues` para incluir `vacanteId = _vacanteAbiertaId.Value` en lugar de `puestoId`. Si la consulta retorna null (defensivo) → fallback a comportamiento previo con `puestoId`.
- Criterio: tests T2.4–T2.5 GREEN.
- Líneas estimadas: 25

### T2.12 [impl]: Exponer `NewOcupacionButtonLabel` dinámico en `PuestoOcupacionesModel`
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs`
- Descripción: agregar `string IOcupacionesCrossList.NewOcupacionButtonLabel => HayVacanteAbierta && !HayOcupacionActiva ? "Cubrir Vacante" : "Nueva ocupación";`.
- Criterio: tests T2.4–T2.5 verifican el label.
- Líneas estimadas: 5

### T2.13 [impl]: Agregar `ObtenerAbiertaPorPuestoAsync` a `IVacanteApiClient` + `VacanteApiClient` + `FakeVacanteApiClient`
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Integration/Vacantes/IVacanteApiClient.cs`, `src/SGV.Web/Integration/Vacantes/VacanteApiClient.cs`, `tests/SGV.Tests/Web/Ocupaciones/FakeOcupacionApiClient.cs` o equivalente de VacanteApiClient si existe
- Descripción: agregar método `Task<VacanteDto?> ObtenerAbiertaPorPuestoAsync(Guid puestoId, CancellationToken)` al interface. Implementación en `VacanteApiClient`: armar `VacanteListQuery` con `Segmento=VacanteSegmentoListado.Abiertas` + `PuestoId=puestoId` + `PageSize=1`, llamar `ListarAsync`, retornar `Items.FirstOrDefault()`. Actualizar fake correspondiente con método nuevo.
- Criterio: `dotnet build` + tests Web no regresionan.
- Líneas estimadas: 25

### T2.14 [impl]: Modificar `_CrossList.cshtml` para leer `NewOcupacionButtonLabel`
- PR: S2
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Ocupaciones/_CrossList.cshtml`
- Descripción: reemplazar el literal `Nueva ocupación` por `@Model.NewOcupacionButtonLabel` en el botón "Nueva ocupación".
- Criterio: tests T2.4 verifican que el HTML contiene "Cubrir Vacante"; tests de `PersonaOcupacionesPageTests` siguen verdes con label por defecto.
- Líneas estimadas: 3

### T2.15 [test green]: Re-correr T2.1–T2.5
- PR: S2
- Tipo: test (green)
- Archivos: ninguno nuevo
- Descripción: `dotnet test --filter "FullyQualifiedName~OcupacionCreatePageTests|PuestoOcupacionesPageTests"`.
- Criterio: `Passed: 5+`, `Failed: 0`.
- Líneas estimadas: 0

### T2.16 [docs]: XML doc en `ObtenerAbiertaPorPuestoAsync`
- PR: S2
- Tipo: docs
- Archivos: `src/SGV.Web/Integration/Vacantes/IVacanteApiClient.cs`
- Descripción: XML doc del método nuevo explicando uso (resolver `vacanteId` desde `PuestoOcupaciones` para alimentar `?vacanteId=...`). Cross-reference al REQ-OCC-NAV-006 invertido.
- Criterio: `dotnet build` sin warnings.
- Líneas estimadas: 5

### T2.17 [validation]: `bun run build` en SGV.Web
- PR: S2
- Tipo: validation
- Archivos: `src/SGV.Web`
- Descripción: ejecutar `bun install && bun run build` desde `src/SGV.Web`. Verifica que los cambios en `.cshtml` y bundling no rompen.
- Criterio: `bun run build` exit 0.
- Líneas estimadas: 0

---

## PR S3 — Frontend Details (~80–110 líneas)

- [x] T3.1 [test red]: `VacantesDetailsAndSidenavTests.VacanteAbierta_BotonCubrirVisible`
- [x] T3.2 [test red]: `VacantesDetailsAndSidenavTests.VacanteEnSeleccion_BotonCubrirVisible`
- [x] T3.3 [test red]: `VacantesDetailsAndSidenavTests.VacanteCubierta_BotonCubrirOculto_BloquePersonaAsignadaVisible`
- [x] T3.4 [test red]: `VacantesDetailsAndSidenavTests.VacanteCancelada_BotonCubrirOculto`
- [x] T3.4-bis [test red, triangulación]: `VacantesDetailsAndSidenavTests.VacanteAbierta_NonMutator_BotonCubrirOculto` (eje `CanMutate`)
- [x] T3.5 [impl]: Extender `VacanteDetailViewModel`
- [x] T3.6 [impl]: Actualizar `FromDto` para mapear nuevos campos
- [x] T3.7 [impl]: Exponer `EsCubrible` en `Details.cshtml.cs`
- [x] T3.8 [impl]: Modificar `Vacantes/Details.cshtml` con botón + bloque
- [x] T3.9 [test green]: Re-correr T3.1–T3.4
- [x] T3.10 [validation]: `bun run build` en SGV.Web

### T3.1 [test red]: `VacantesDetailsAndSidenavTests.VacanteAbierta_BotonCubrirVisible`
- PR: S3
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs`
- Descripción: `[Fact]` Web con Vacante Abierta + usuario admin (`CanMutate=true`) → HTML contiene botón "Cubrir Vacante" con `href="/organizacion/ocupaciones/crear?vacanteId={id}&returnUrl=..."`.
- Criterio: `Assert.Contains("Cubrir Vacante", html)` + assert sobre `href` con regex o substring matching.
- Líneas estimadas: 25

### T3.2 [test red]: `VacantesDetailsAndSidenavTests.VacanteEnSeleccion_BotonCubrirVisible`
- PR: S3
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs`
- Descripción: `[Fact]` con Vacante En Selección + admin → botón visible (idéntico a T3.1).
- Criterio: idéntico a T3.1 con `estadoNombre="En Selección"`.
- Líneas estimadas: 20

### T3.3 [test red]: `VacantesDetailsAndSidenavTests.VacanteCubierta_BotonCubrirOculto_BloquePersonaAsignadaVisible`
- PR: S3
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs`
- Descripción: `[Fact]` con Vacante Cubierta + OcupacionDerivadaId no null + PersonaAsignadaNombre="Juan Pérez" → botón NO visible; bloque "Persona asignada: Juan Pérez" + link "Ver ocupación" con `href="/organizacion/ocupaciones/detalles/{ocupacionId}"`.
- Criterio: `Assert.DoesNotContain("Cubrir Vacante", html)` + `Assert.Contains("Persona asignada: Juan Pérez", html)` + assert sobre href del link.
- Líneas estimadas: 30

### T3.4 [test red]: `VacantesDetailsAndSidenavTests.VacanteCancelada_BotonCubrirOculto`
- PR: S3
- Tipo: test (red)
- Archivos: `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs`
- Descripción: `[Fact]` con Vacante Cancelada + admin → botón NO visible; bloque "Persona asignada" NO visible.
- Criterio: `Assert.DoesNotContain("Cubrir Vacante", html)` + `Assert.DoesNotContain("Persona asignada:", html)`.
- Líneas estimadas: 15

### T3.5 [impl]: Extender `VacanteDetailViewModel`
- PR: S3
- Tipo: impl
- Archivos: `src/SGV.Web/Integration/Vacantes/VacanteDetailViewModel.cs`
- Descripción: agregar `Guid? OcupacionDerivadaId`, `string? PersonaAsignadaNombre` al record. Agregar `bool EsCubrible` (computed: `!EsCerrada && EstadoVacanteNombre != "Cancelada"`, o un flag más explícito si el DTO lo trae). Actualizar `FromDto` para mapear.
- Criterio: tests T3.1–T3.4 (RED) aún fallan porque la vista no usa estos campos.
- Líneas estimadas: 12

### T3.6 [impl]: Actualizar `FromDto` para mapear nuevos campos
- PR: S3
- Tipo: impl
- Archivos: `src/SGV.Web/Integration/Vacantes/VacanteDetailViewModel.cs`
- Descripción: dentro de `FromDto`, agregar al constructor los nuevos campos: `dto.OcupacionDerivadaId`, `dto.PersonaAsignadaNombre`. Calcular `EsCubrible` basado en `dto.EstadoVacanteNombre` (no Cubierta, no Cancelada) — alternativa: pedir al backend un flag explícito. Decisión: usar el nombre del estado por simplicidad (no requiere nuevo campo DTO).
- Criterio: tests T3.1–T3.4 GREEN tras T3.7–T3.8.
- Líneas estimadas: 8

### T3.7 [impl]: Exponer `EsCubrible` en `Details.cshtml.cs`
- PR: S3
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml.cs`
- Descripción: agregar propiedad pública `bool EsCubrible => ViewModel is not null && ViewModel.EsCubrible && CanMutate;` (combinar el flag del VM con el permiso).
- Criterio: tests T3.1–T3.4 verifican que el botón aparece solo cuando `EsCubrible && CanMutate`.
- Líneas estimadas: 5

### T3.8 [impl]: Modificar `Vacantes/Details.cshtml` con botón + bloque
- PR: S3
- Tipo: impl
- Archivos: `src/SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml`
- Descripción: en el bloque de acciones (donde está Edit), si `Model.EsCubrible` → renderizar `<a class="btn btn-primary" href="/organizacion/ocupaciones/crear?vacanteId={id}&returnUrl=...">Cubrir Vacante</a>`. Después del card "Detalle de vacante" y antes del card "Historial", si `ViewModel.EsCerrada && ViewModel.OcupacionDerivadaId.HasValue` → renderizar bloque "Persona asignada: {PersonaAsignadaNombre}" + (si nombre no null) link "Ver ocupación" a `/organizacion/ocupaciones/detalles/{OcupacionDerivadaId}`.
- Criterio: tests T3.1–T3.4 GREEN.
- Líneas estimadas: 30

### T3.9 [test green]: Re-correr T3.1–T3.4
- PR: S3
- Tipo: test (green)
- Archivos: ninguno nuevo
- Descripción: `dotnet test --filter "FullyQualifiedName~VacantesDetailsAndSidenavTests"`.
- Criterio: `Passed: 4+`, `Failed: 0`.
- Líneas estimadas: 0

### T3.10 [validation]: `bun run build` en SGV.Web
- PR: S3
- Tipo: validation
- Archivos: `src/SGV.Web`
- Descripción: `bun install && bun run build` desde `src/SGV.Web`.
- Criterio: exit 0.
- Líneas estimadas: 0

---

## Order of execution

1. **S1** (`feature/invertir-flujo-cubrir-s1-backend`): branch desde `develop`. Commits por work-unit (T1.7→T1.11 en orden, con T1.1–T1.6 RED primero; T1.13→T1.16 luego; T1.18→T1.22 al final; T1.29 refactor + T1.30/T1.31 docs al cierre). Merge a `develop`.
2. **S2** (`feature/invertir-flujo-cubrir-s2-create-frontend`): branch desde `develop` post-S1. Commits por work-unit (T2.6→T2.14 con T2.1–T2.5 RED primero; T2.15 GREEN; T2.16–T2.17 al cierre). Merge a `develop`.
3. **S3** (`feature/invertir-flujo-cubrir-s3-vacante-details`): branch desde `develop` post-S2. Commits por work-unit (T3.5→T3.8 con T3.1–T3.4 RED primero; T3.9 GREEN; T3.10 validación). Merge a `develop`.

## Definition of Done (delegado a sdd-apply / sdd-verify)

- [ ] S1, S2, S3 mergeadas a `develop` en orden.
- [ ] `dotnet test SGV.slnx` global en verde (suite + `[MySqlFact]` skipeo limpio si MySQL no está disponible).
- [ ] `dotnet build SGV.slnx` sin warnings nuevos (nullable, Obsolete, etc.).
- [ ] `bun run build` en `src/SGV.Web` exitoso.
- [ ] `docs/decisiones-implementacion.md` actualizado con la entrada "Inversión del flujo Cubrir (2026-08-09)" (T1.31 en apply phase).
- [ ] `docs/migracion-inicial-sgv.sql` NO modificado (confirmado por sdd-verify, Q-T4).
- [ ] OpenSpec change archivado con normalización DADO-CUANDO-ENTONCES (D-6) y renombre del código de error en spec vigente (D-4).
- [ ] AC1–AC10 del proposal verificados por tests automatizados.
- [ ] Cada PR incluye Chain Context (depends on, current, verificación, rollback).
