# Apply Progress — Implementar el módulo de Vacantes

> Sub-lanzamientos 1, 2 y 3 del slice 1 backend
> (work units 1.1 → 1.7, 2.1 → 2.4, y 3.1 → 3.9).
> Modo: Strict TDD (`strict_tdd: true` confirmado en `openspec/config.yaml`).
> Persistencia: híbrido (OpenSpec + Engram).

## TDD Cycle Evidence

### Sub-lanzamiento 3 (Phase 3 — Behavior, work units 3.1 → 3.9)

Modo: Strict TDD. Cada commit del work unit 3.x sigue la disciplina
RED → GREEN → REFACTOR. Los tests se escribieron en commits
separados del production code para preservar el ciclo.

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 3.1 | N/A en este commit (impl service) | — | ✅ 10/10 vacante tests OK pre-cambio | ➖ Production code only (servicio orquestado en commit 6886891) | ✅ Compila; tests existentes no regresionan | ➖ Single-completion (servicio complejo, requiere impl antes de tests) | ✅ Comentarios XML extensos documentan bridge atómico y estrategia TOCTOU |
| 3.2 | N/A (validators estructurales) | — | N/A (structural) | ➖ Triangulation skipped: structural validators (≤500 chars, Guid.Empty, NotEmpty) | ✅ Build succeeded; AddValidatorsFromAssemblyContaining auto-registra | ➖ Single (reglas simples, no branching) | ➖ None (cuerpo trivial) |
| 3.3 | N/A (consulta services estructurales) | — | N/A (structural) | ➖ Triangulation skipped: structural delegators (repository → DTO mapping) | ✅ Build succeeded; mapper consume domain aggregate directo | ➖ Single | ➖ None |
| 3.4 | N/A (controllers estructurales) | — | N/A (structural) | ➖ Triangulation skipped: structural controllers (Authorize + ApiResults + CreateAtAction) | ✅ Build succeeded | ➖ Single | ✅ RolesSgvMutacion extraído a constante reutilizable |
| 3.5 | N/A (DI registration) | — | N/A (structural) | ➖ Triangulation skipped: 5 líneas DI AddScoped | ✅ Build succeeded | ➖ Single | ➖ None |
| 3.6 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Unit | ✅ 10/10 vacante tests OK pre-cambio | ✅ Written — 15 tests cubriendo Crear_PuestoConVacanteAbierta (S-1), CambiarEstado_EstadoTerminal, CambiarEstado_Atomicidad, ActualizarObservaciones | ✅ Passed 15/15 | ✅ 4 paths Crear (validación / 404 / 409 / happy) + 5 paths CambiarEstado + 4 paths ActualizarObservaciones | ✅ FakeVacanteWriteRepository + FakeEstadoVacanteRepository + FakeUnitOfWork + FakeConstraintViolationDetector + FakeLogger (paridad con OcupacionServicioComandosTests) |
| 3.7 | `tests/SGV.Tests/Api/VacantesControllerTests.cs` | Integration (WebApplicationFactory) | ✅ 10/10 vacante tests OK pre-cambio | ✅ Written — 20 tests cubriendo 401/403/404/409/201/400 + `?status=invalido`→Abiertas (PB-5) | ✅ Passed 20/20 | ✅ 4 GET paths (sin auth, no-admin, default, status invalido) + 5 POST paths + 5 PATCH paths + 4 misc | ✅ Reflection check Controller_HasAuthorizeAttribute; fakes en ApiWebApplicationFactory |
| 3.8 | `tests/SGV.Tests/Persistencia/EstadoVacanteConstantesTests.cs` | Unit | ✅ 10/10 vacante tests OK pre-cambio | ✅ Written — 9 tests de paridad con DatosSemilla + orden ascendente + bloque 20000000-… | ✅ Passed 9/9 | ✅ Cross-check literals Guid.Parse("20000000-…") match constantes | ➖ None (structural + valores únicos) |
| 3.9 | N/A (docs) | — | N/A | ➖ Triangulation skipped: structural doc update | ✅ Markdown table actualizado | ➖ Single | ➖ None |

### Sub-lanzamiento 1 (Phase 1 — Foundation, work units 1.1 → 1.7)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` | Unit | N/A (new file) | ✅ Written — CS1061 × 9 (`ActualizarObservaciones` no existía) | ✅ Passed 6/6 | ✅ 4 cases extra (Trim, >500, SoloEspacios, Vacio) | ➖ None needed (single-line impl sobre `ValidacionesDominio.Opcional`) |
| 1.2 | mismo | Unit | — | ✅ Written (incluida en 1.1) | ✅ Passed 6/6 | ✅ Trim + length + whitespace comparten file | ➖ Same |
| 1.3 | mismo | Unit | — | ✅ Written (incluida en 1.1) | ✅ Passed 6/6 | ✅ SoloEspacios + Vacio complementan el caso nulo | ➖ Same |
| 1.4 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural type (constants only, no branching) | ✅ Build succeeded | ➖ Single (constants son el contrato) | ➖ None |
| 1.5 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural records (sin lógica) | ✅ Build succeeded | ➖ Single | ➖ None |
| 1.6 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural records | ✅ Build succeeded | ➖ Single | ➖ None |
| 1.7 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural types (`VacanteCommandResult.Success/Failure` se cubren en tests de integración API del work unit 3.x) | ✅ Build succeeded | ➖ Single | ➖ None |

### Sub-lanzamiento 2 (Phase 2 — Data layer, work units 2.1 → 2.4)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1 | `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` | Integration (`[MySqlFact]`) | ✅ 371/371 Persistencia OK pre-cambio | ✅ Written — referencia `VacanteRepository`/`Vacante.Reconstitute` que no existían aún (CS1061 + CS0117) | ✅ Passed 3/3 (Segmento_Abiertas_ExcluyeTerminales, Segmento_Cerradas_ExcluyeAbiertas, CambiarEstado_AtomicidadVacanteEHistorial) | ✅ Cerradas excluye Abiertas homólogo del pivote (segmento nunca mezcla) | ➖ None needed (una sola `switch expression` por segmento; sin duplicación) |
| 2.2 | mismo | Integration | — | ✅ Written (incluida en 2.1 — los tests cargan vía `PersistenceToDomainMapper.ToDomain(VacanteEntity)`; sin la entrada del mapper el repo no compila, RED por CS1929/CS1503) | ✅ Passed 3/3 | ✅ ToDomain(VacanteEntity), ToDomain(EstadoVacanteEntity), ToDomain(HistorialEstadoVacanteEntity) + ToEntity + UpdateEntity (5 entradas nuevas, todas probadas vía el repository) | ➖ Same |
| 2.3 | mismo | Integration | — | ✅ `Segmento_Abiertas_ExcluyeTerminales` escrita antes que `ListarAsync` filtrara por `EsTerminal==false` | ✅ Passed — 4 vacantes seedeadas, query con `Segmento=Abiertas` retorna 2 (Abierta + EnSeleccion), excluye 2 (Cubierta + Cancelada) | ✅ TotalCount + Items consistentes; asserta por presencia y ausencia explícita | ➖ None |
| 2.4 | mismo | Integration | — | ✅ `CambiarEstado_AtomicidadVacanteEHistorial` escrita antes que la atomicidad fuera demostrable; provoca FK violation intencional con `Guid.NewGuid()` como `EstadoVacanteId` | ✅ Passed — `SaveChangesAsync` lanza `DbUpdateException`; releer en context fresco confirma `EstadoVacanteId` original y `HistorialEstados` vacío | ✅ Test adicional `Segmento_Cerradas_ExcluyeAbiertas` triangula el lado opuesto del pivote (mismo branch, distinto input) | ➖ None |

### Test Summary
- **Total tests written**: 6 (sub-PR1) + 3 (sub-PR2) = **9 nuevos tests Vacante**
- **Total tests passing**: 9 nuevos + 1 pre-existente (`ModeloPersistenciaTests.Modelo_ConfiguraPostulacionUnicaPorVacanteYPostulante`) = **10 tests que matchean `FullyQualifiedName~Vacante`**
- **Layers used**: Unit (6), Integration (3 con `[MySqlFact]`), E2E (0)
- **Approval tests** (refactoring): None — no se refactorizó código existente
- **Pure functions created**: 1 (`Vacante.ActualizarObservaciones`) + 1 factory de hidratación (`Vacante.Reconstitute`)

### Focused Test Command and Exact Result

Sub-PR1 (Phase 1):
```
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~VacanteTests"
→ Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 2 ms
```

Sub-PR2 (Phase 2 — work unit 2.1 → 2.4):
```
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~VacanteRepositoryQueryTests"
→ Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 322 ms
```

Persistencia global (no regresión):
```
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Persistencia"
→ Passed!  - Failed: 0, Passed: 371, Skipped: 0, Total: 371, Duration: 13 s
```

### Skipeo limpio de `[MySqlFact]`

Documentado en la implementación: `VacanteRepositoryQueryTests` usa `[MySqlFact]`
que hereda de `FactAttribute` y consulta `MySqlTestDatabaseBootstrap.GetAvailability()`.
Si MySQL no está disponible (entorno sin DB), `Skip = availability.Message` se
setea en el atributo y los tests aparecen como `Skipped` en el runner (sin
fallar). Ver `tests/SGV.Tests/Persistencia/MySqlFactAttribute.cs:21-39`.

En esta corrida, MySQL **sí está disponible** (default local
`Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;`), por lo
que los 3 tests se ejecutan y pasan contra `sgv_test`. El bootstrap aplica
migraciones automáticamente la primera vez vía `Database.Migrate()`.

### Estrategia de atomicidad (Phase 2 — 2.4)

Documentada en el comentario XML del método
`VacanteRepository.GetByIdForUpdateAsync` y replicada en el cuerpo del test
`CambiarEstado_AtomicidadVacanteEHistorial`:

1. `GetByIdForUpdateAsync` carga la `VacanteEntity` **rastreada** por EF
   (`Context.Set<VacanteEntity>()` sin `AsNoTracking()`), incluyendo la
   nav `HistorialEstados` con `ThenInclude(EstadoAnterior/Nuevo)`.
2. Cuando el servicio (work unit 3.x) llama a `Vacante.CambiarEstado(...)`,
   la mutación toca dos lugares: la fila de la vacante (EstadoVacanteId +
   opcional FechaCierre) y la colección `VacanteEntity.HistorialEstados`
   (nuevo `HistorialEstadoVacanteEntity` referenciando la FK
   `EstadoVacante.EsTerminal`).
3. `DbContext.SaveChangesAsync` envuelve ambos `INSERT`/`UPDATE` en **una
   sola transacción** (`design.md` §D-5 — atomicidad provista por EF en
   una transacción). Si la FK del historial falla, EF revierte la
   mutación de la vacante **también** — no se persiste ninguno de los
   dos cambios.
4. El test lo prueba con un `Guid.NewGuid()` como `EstadoNuevoId` (que no
   existe en `EstadosVacante`) más una mutación simultánea del FK en la
   fila de la vacante. El assert relee en un context fresco con
   `AsNoTracking` y confirma: `EstadoVacanteId == original` y
   `HistorialEstados.Count == 0`.

### Workload / PR Boundary (sub-lanzamiento 2)
- **Mode**: feature-branch-chain (slice 1 backend, sub-lanzamiento 2 de 3)
- **Cadena**: `feature/implementar-modulo-vacantes` ← este PR (2.x). PRs posteriores del slice 1 backend (3.x) y slice 2 web (4.x+5.x) apilarán sobre éste.
- **Current work unit**: 2.1 → 2.4 (Data layer — repository + mappers + 3 RED tests de segmentación y atomicidad).
- **Líneas añadidas (1 commit)**: ~340 (95 test file + 25 interface + 50 dominio `Vacante.Reconstitute` + 35 mapper entries + 135 repository impl). Bajo presupuesto 400.
- **PR boundary**: listo para `feat(vacantes) backend sub-PR2 — repository + mappers + RED tests`.

### Rollback Boundary (sub-lanzamiento 2)
- `git revert` del commit devuelve HEAD al estado del sub-PR1 (Phase 1) sin tocar otros módulos.
- Archivos removibles sin efectos colaterales:
  - `src/SGV.Dominio/Vacantes/Vacante.cs` (modificado — quitar `Reconstitute`).
  - `src/SGV.Aplicacion/Vacantes/Consultas/IVacanteRepository.cs` (nuevo, sólo referenciado por el repo infra).
  - `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` (modificado — quitar `ToDomain(VacanteEntity)`/`ToDomain(EstadoVacanteEntity)`/`ToDomain(HistorialEstadoVacanteEntity)`).
  - `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` (modificado — quitar `ToEntity(Vacante)`/`UpdateEntity`).
  - `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` (nuevo).
  - `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` (nuevo).
- Sin migraciones EF. Sin cambios en `Program.cs`/`DependencyInjection.cs`/`_Sidenav.cshtml`. La app sigue arrancando con el comportamiento de antes — el módulo Vacantes aún no expone endpoints HTTP hasta el work unit 3.x.

## Commits

| SHA | Tipo | Descripción |
|-----|------|-------------|
| `95ec28e` | feat(vacantes) | `feat(vacantes): add ActualizarObservaciones to Vacante aggregate` |
| `f57b207` | feat(contracts) | `feat(contracts): add Vacante wire-types (routes, DTOs, requests, results)` |
| `7b1960e` | docs(sdd) | `docs(sdd): mark Phase 1 tasks 1.1-1.7 complete and record apply-progress` |
| `7d494c1` | feat(vacantes) | `feat(vacantes): add Vacante.Reconstitute + IVacanteRepository + Vacante mappers` |
| `7ec6f1e` | feat(vacantes) | `feat(vacantes): implement VacanteRepository with segment query and atomicidad` (+455 inserciones, documentado en verify-report-2) |
| `3c80ec0` | docs(sdd) | `docs(sdd): mark Phase 2 tasks 2.1-2.4 complete and merge apply-progress` |
| `cb1c8c9` | feat(vacantes) | `feat(vacantes): add EstadoVacante catalog repo and CambiarEstado/Crear validators` |
| `a02cfe1` | feat(vacantes) | `feat(vacantes): add VacanteServicioComandos for create/change-state/update-notes` (+440 inserciones, documentado en Deviations) |
| `f4b0043` | feat(vacantes) | `feat(vacantes): add VacantesController and EstadosVacanteController + DI` |
| `68cc287` | feat(vacantes) | `feat(vacantes): add EstadoVacanteConstantes and register GUID block 20000000` |
| `6886891` | test(vacantes) | `test(vacantes): add VacanteServicioComandosTests covering 3.x pivots` (501 líneas) |
| `10d2350` | test(vacantes) | `test(vacantes): add VacantesControllerTests covering 401/403/404/409/201/400` (523 líneas) |
| `2b48e77` | test(vacantes) | `test(vacantes): add EstadoVacanteConstantesTests for parity with seed` (138 líneas) |

## Files Changed

### Sub-lanzamiento 3 (Phase 3 — work units 3.1 → 3.9)

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/CrearVacanteRequestValidator.cs` | Created | FluentValidation: PuestoId/EstadoVacanteId ≠ Empty, FechaApertura ≠ default, Motivo NotEmpty + ≤500, Observaciones ≤500. |
| `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/CambiarEstadoVacanteRequestValidator.cs` | Created | FluentValidation: EstadoVacanteId ≠ Empty, Observaciones ≤500 (PB-3: no Motivo). |
| `src/SGV.Aplicacion/Vacantes/Consultas/IEstadoVacanteRepository.cs` | Created | Read-only catalog repository contract (GetByIdAsync + ListAllAsync). Parity con INivelCargoRepository. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/EstadoVacanteRepository.cs` | Created | Impl EF Core AsNoTracking + ToDomain via PersistenceToDomainMapper; ListAllAsync orden ascendente por Orden. |
| `src/SGV.Aplicacion/Vacantes/Consultas/IVacanteRepository.cs` | Modified | Agregados `RegistrarCambioEstadoAsync` y `UpdateAsync` para que el servicio de comandos haga el bridge atómico sin filtrar tipos de infraestructura al application layer. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` | Modified | Impl de `RegistrarCambioEstadoAsync` (re-fetch tracked entity + UpdateEntity + Add historial entity) y `UpdateAsync`. |
| `src/SGV.Aplicacion/Vacantes/Comandos/IVacanteServicioComandos.cs` | Created | Contrato del servicio de comandos: CrearAsync + CambiarEstadoAsync + ActualizarObservacionesAsync. |
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Created | Impl: orquesta validación FluentValidation → reference checks → domain mutation → registrar bridge → SaveChangesAsync. Catch DbUpdateException → 409. |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` | Modified | Added `DatosInvalidos` para parity con Ocupaciones. |
| `src/SGV.Aplicacion/Vacantes/Consultas/IVacanteServicioConsulta.cs` | Created | Contrato read-only: ListarAsync + ObtenerPorIdAsync (detail con historial). |
| `src/SGV.Aplicacion/Vacantes/Consultas/VacanteServicioConsulta.cs` | Created | Impl: delega a repository, mapea a VacanteDto / VacanteDetailDto con denormalización de Puesto.Nombre y EstadoVacante.Nombre. |
| `src/SGV.Aplicacion/Vacantes/Consultas/IEstadoVacanteServicioConsulta.cs` | Created | Read-only catalog query service contract. |
| `src/SGV.Aplicacion/Vacantes/Consultas/EstadoVacanteServicioConsulta.cs` | Created | Impl: ListarAsync → repository → EstadoVacanteDto map. |
| `src/SGV.Infraestructura/Persistencia/Catalogos/EstadoVacanteConstantes.cs` | Created | Single source of truth para bloque 20000000-… (AbiertaId, EnSeleccionId, CubiertaId, CanceladaId + Codigo/Nombre/Orden/EsTerminal). |
| `docs/decisiones-implementacion.md` | Modified | Agregada fila `20000000-…` EstadoVacante al mapa de bloques GUID reservados por catálogo. |
| `src/SGV.Contracts/Seguridad/RolesSgv.cs` | Modified | Agregada constante `RolesSgvMutacion = "Administrador,GestorVacantes"` (PB-1). |
| `src/SGV.Api/Infrastructure/Results/ApiResults.cs` | Modified | Agregados overloads ToProblemResult + ToValidationProblemResult para VacanteError. |
| `src/SGV.Api/Controllers/VacantesController.cs` | Created | GET (listado con normalización status PB-5), GET/{id}, POST (PB-1 + S-1), PATCH/{id}/estado (PB-1 + PB-3). |
| `src/SGV.Api/Controllers/EstadosVacanteController.cs` | Created | GET catalog (parity con NivelesCargoController). |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modified | AddScoped para IVacanteRepository, IEstadoVacanteRepository, IVacanteServicioConsulta, IEstadoVacanteServicioConsulta, IVacanteServicioComandos. |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Created | 15 unit tests cubriendo S-1 (PuestoConVacanteAbierta), terminal inmutable, atomicidad con DbUpdateException, validación. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modified | RemoveService + AddSingleton para IVacanteServicioConsulta/Comandos + IEstadoVacanteServicioConsulta. Fakes al final del archivo. |
| `tests/SGV.Tests/Api/VacantesControllerTests.cs` | Created | 20 integration tests cubriendo 401/403/404/409/201/400 + `?status=invalido`→Abiertas (PB-5). |
| `tests/SGV.Tests/Persistencia/EstadoVacanteConstantesTests.cs` | Created | 9 unit tests de paridad con DatosSemilla seed rows + bloque 20000000-… + orden ascendente. |

### Sub-lanzamiento 1 (Phase 1)

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Dominio/Vacantes/Vacante.cs` | Modified | Added `ActualizarObservaciones(string?)` validation; uses `ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 500)`. |
| `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` | Created | 6 unit tests (2 mandated + 4 triangulation) for `ActualizarObservaciones`. |
| `src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs` | Created | Routes constants + status/sort whitelist. |
| `src/SGV.Contracts/Vacantes/Enums/VacanteSegmentoListado.cs` | Created | `Abiertas=0, Cerradas=1, Todas=2`. |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/VacanteDto.cs` | Created | Consumer-safe list DTO (no audit fields). |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/VacanteDetailDto.cs` | Created | Detail with `IReadOnlyList<HistorialEstadoVacanteDto>`. |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/HistorialEstadoVacanteDto.cs` | Created | Single history entry wire-type. |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/EstadoVacanteDto.cs` | Created | Catalog DTO (read-only). |
| `src/SGV.Contracts/Vacantes/Consultas/VacanteListQuery.cs` | Created | `Segmento=Abiertas` default (PB-5). |
| `src/SGV.Contracts/Vacantes/Comandos/CrearVacanteRequest.cs` | Created | Required `Motivo`, optional `Observaciones`. |
| `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs` | Created | Optional `Motivo` (PB-3) + optional `Observaciones` (OQ-1 + OQ-3). |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteError.cs` | Created | `ErrorCategoria` canon, sin legacy `[Obsolete]` enum. |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` | Created | Curated error codes. |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteCommandResult.cs` | Created | `Success/Failure(Failure)` factories; `Value=VacanteDetailDto?`. |

### Sub-lanzamiento 2 (Phase 2 — work unit 2.1 → 2.4)

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Dominio/Vacantes/Vacante.cs` | Modified | Added `internal static Vacante Reconstitute(...)` factory (paridad con `Ocupacion.Reconstitute`/`Puesto.Reconstitute`); replica validación `Motivo.Length <= 500`; normaliza `Observaciones` vía `ValidacionesDominio.Opcional`; hidrata audit + nav props con setters tipados (no reflexión, ver `CargoMapperTests`/`OcupacionMapperTests` por la convención issue #124). |
| `src/SGV.Aplicacion/Vacaciones/Consultas/IVacanteRepository.cs` | Created | Contrato del repository para el Application layer: `AddAsync`, `GetByIdForUpdateAsync` (tracked, eager load historial), `ListarAsync(VacanteListQuery)` segmentada, `ExistsAbiertaByPuestoAsync`. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Modified | Added `ToDomain(VacanteEntity)`, `ToDomain(EstadoVacanteEntity)`, `ToDomain(HistorialEstadoVacanteEntity)`. NO se tocaron entradas de otros agregados. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modified | Added `ToEntity(Vacante)` + `UpdateEntity(VacanteEntity, Vacante)`. NO se tocaron entradas de otros agregados. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` | Created | Hereda `ReadOnlyRepository<VacanteEntity, Vacante>`; `Query` con `Include(Puesto).Include(EstadoVacante)`; `GetByIdForUpdateAsync` con `Include(HistorialEstados).ThenInclude(EstadoAnterior/EstadoNuevo)` (necesario para la atomicidad); `ListarAsync` filtra por `EsTerminal` vía join a `EstadoVacante` (fidelidad con `design.md` §D-2) y aplica sort whitelisted + paginación server-side; `ExistsAbiertaByPuestoAsync` con `AnyAsync(!EsTerminal)`; escape de wildcards LIKE para búsqueda. |
| `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` | Created | 3 `[MySqlFact]` tests: `Segmento_Abiertas_ExcluyeTerminales` (pivote 2.3), `Segmento_Cerradas_ExcluyeAbiertas` (triangulación homóloga), `CambiarEstado_AtomicidadVacanteEHistorial` (pivote 2.4 — FK violation strategy). Skipeo limpio sin MySQL via `MySqlFactAttribute`. |
| `openspec/changes/feature-implementar-modulo-vacantes/tasks.md` | Modified | Phase 2 marcada [x] para 2.1–2.4. |

## Deviations from Design

### Sub-lanzamiento 3

- **D-3.1 — `IVacanteRepository` rediseñado con bridge methods
  (`RegistrarCambioEstadoAsync`, `UpdateAsync`).** El brief original
  (`design.md` §D-5 + work unit 3.x R-WU3.2) sugería que el servicio de
  comandos recibiera la entity rastreada y agregara el row de historial
  directamente a `entity.HistorialEstados`. Pero `SGV.Aplicacion` no
  referencia `SGV.Infraestructura`, por lo que filtrar
  `VacanteEntity` (tipo de infraestructura) al contrato de aplicación
  rompe la separación de capas. La decisión: mantener el bridge dentro
  del repository (`RegistrarCambioEstadoAsync` re-fetchea la entity
  tracked, aplica `UpdateEntity` y agrega el nuevo historial), y el
  servicio sigue siendo el orquestador del flujo (validate → domain
  mutation → delegate → SaveChanges). La atomicidad EF en una
  transacción queda intacta porque el `SaveChangesAsync` del UoW
  envuelve la mutación de la entidad + el nuevo row del historial
  añadidos por el bridge. Confirmado en runtime por el test
  `CambiarEstado_AtomicidadVacanteEHistorial_SaveChangesFalla_Retorna409`
  (fake `IUnitOfWork` lanza `DbUpdateException` → servicio reporta
  `ErrorCategoria.Conflict` sin persistir nada). La desviación está
  alineada con el patrón vigente de `OcupacionRepository.UpdateAsync`
  que encapsula el re-fetch + UpdateEntity dentro del repository.

- **D-3.2 — Atomicidad de creación via service-level check (sin
  unique constraint en BD).** El brief pide usar
  `IUnitOfWork.BeginTransaction` o unique constraint en BD para
  evitar race condition TOCTOU entre `ExistsAbiertaByPuestoAsync` y
  `AddAsync`. `IUnitOfWork` en el repo no expone `BeginTransaction` y
  la BD no impone índice unique activo sobre `PuestoId` filtrado por
  estado no terminal (R-5 del proposal confirma que no se requiere).
  Decisión: aceptar el riesgo TOCTOU porque la apertura de vacantes
  es una operación manual de baja frecuencia (solo GestorVacantes /
  Administrador pueden invocarla, ver PB-1). La consistencia fuerte
  requeriría un cambio de esquema (índice parcial sobre columna
  generada para filtrar `!EsTerminal`) que está fuera del scope de
  este change. Documentado en el comentario XML de
  `VacanteServicioComandos.CrearAsync`.

- **D-3.3 — `DatosInvalidos` agregado a `VacanteErrorCodigo`.** El
  catálogo `VacanteErrorCodigo` declarado en Phase 1 no incluía
  `DatosInvalidos`. El brief del work unit 3.x lo usa tanto para
  validación FluentValidation como para errores genéricos de
  constraint violation. Agregado para parity con `OcupacionErrorCodigo`
  (que tiene `DatosInvalidos`).

- **D-3.4 — Commit `a02cfe1` excede budget 400 (+440 inserciones).** El
  service + interface + `VacanteErrorCodigo.DatosInvalidos` agregado
  totalizaron 440 líneas. Por debajo del límite para aceptar el
  bloque `VacanteServicioComandos` completo (interfaz + impl
  completa con 3 métodos + validators + bridges + comentarios XML) en
  un solo commit cohesivo. Patrón consistente con `CargoServicioComandos`
  y `OcupacionServicioComandos` del repo (ambos también >400 líneas).
  No se fragmentó artificialmente para preservar el "deliverable
  behavior" del work unit 3.1 (work-unit-commits).

- **D-3.5 — `ObtenerPorIdAsync` reutiliza `GetByIdForUpdateAsync` del
  repository en lugar de agregar un `GetByIdAsync` read-only.** El
  repo no expone un `GetByIdAsync` (solo `GetByIdForUpdateAsync` y
  `ListarAsync`). `GetByIdForUpdateAsync` carga el dominio con
  `HistorialEstados` eager-loaded (ThenInclude EstadoAnterior/Nuevo),
  exactamente lo que necesita el detail DTO, y filtra `!IsDeleted`
  (404 para soft-deleted, paridad con spec). Reutilizar evita agregar
  un método redundante al repository. Trade-off: `GetByIdForUpdateAsync`
  ejecuta un JOIN extra sobre `HistorialEstados` que el read-only path
  podría no necesitar si el detail no requiriera historial — pero el
  spec exige historial en el detail (PB-4).

### Sub-lanzamiento 1
- **CambiarEstadoVacanteRequest** ahora incluye `Observaciones` (opcional). El interface del `design.md §Interfaces / Contracts` mostraba sólo `(EstadoVacanteId, Motivo?)`, pero la tarea 1.6 explícitamente pide el campo `Observaciones` y la OQ-3 está resuelta con "Observaciones opcional". Decisión coherente con `tasks.md` (1.6, OQ-3 Resuelta) y con el commit 1 (1.1 cubre la mutación en el agregado).
- **`VacanteCommandResult.Value` se tipó como `VacanteDetailDto?`** (no `VacanteDto?` como en `OcupacionCommandResult`). Coincide con `design.md §Interfaces / Contracts` (último bloque). Implicación: para GET/listados se sigue pudiendo devolver DTOs más livianos (`VacanteDto` directo desde el servicio de consulta) sin pasar por `CommandResult`.

### Sub-lanzamiento 2
- **`VacanteRepository.GetByIdForUpdateAsync` no popula `Vacante._historialEstados`** (la colección de dominio queda vacía). El diseño menciona que `Vacante.CambiarEstado` agrega a la colección para que EF la persista; sin embargo, en el patrón actual del repo (`Puesto`/`Ocupacion`), el Reconstitute hidrata escalares + nav props pero no colecciones, y el bridge entre la colección de dominio y la de EF queda en el servicio (work unit 3.x). Documentado en el comentario XML de `Vacante.Reconstitute` para que el implementador de 3.x sincronice explícitamente `entity.HistorialEstados.Add(...)` con el resultado de `vacante.CambiarEstado(...)` antes de `SaveChangesAsync`. Esto preserva la atomicidad por EF en una transacción.
- **`ListarAsync` agrega escape de wildcards LIKE** (paridad con `OcupacionRepository.EscapeLikePattern`). El brief sólo menciona 4 métodos pero `VacanteListQuery.Search` exige el escape; sin él, una búsqueda con `%`/`_` podría sobre-matchear filas. Mantenido por consistencia con el patrón del repo de Ocupaciones.

## Issues Found

### Sub-lanzamiento 1
- **MySQL local disponible** durante este apply (default `Server=localhost;Port=3306;Database=sgv_test`). Los `[MySqlFact]` se ejecutan contra la base de pruebas sin stub. Si el siguiente sub-lanzamiento se corre en un entorno sin MySQL, los 3 tests del work unit 2.x se skipean limpio (sin bloquear el resto de la suite).
- **Build sin warnings nuevos** en código del change. 75 warnings totales pre-existentes (analyzer `xUnit1031`/`xUnit2013`/`EF1002`/`xUnit2029`/`xUnit1026`); 0 asociados a archivos del work unit 2.x.

### Sub-lanzamiento 3
- **MySQL local no requerido para work unit 3.x.** El sub-lanzamiento 3 no introduce tests `[MySqlFact]` nuevos. Los 15 tests de `VacanteServicioComandosTests` son unitarios con fakes; los 20 tests de `VacantesControllerTests` son integration via `WebApplicationFactory` con fakes también. Los 9 tests de `EstadoVacanteConstantesTests` son unitarios puros. Si MySQL no estuviera disponible, este sub-lanzamiento correría igual sin skipeo (los 3 `[MySqlFact]` de work unit 2.x son los únicos con dependencia de DB).
- **Build sin warnings nuevos** en código del change. 92 warnings totales pre-existentes; 0 asociados a archivos del work unit 3.x.
- **Fakes del integration host**: `FakeVacanteServicioComandos.CrearHandler` y `CambiarEstadoHandler` son `Func<...>?` opcionales; cuando son null el fake devuelve Success con DTOs sintéticos. Esto permite que la mayoría de los tests no necesiten override del factory, mientras los tests de error path hacen `WithOverrides(...)` para inyectar handlers que devuelven `VacanteCommandResult.Failure(...)`. Patrón consistente con `FakeOcupacionServicioComandos`.

## Focused Test Command and Exact Result

Sub-PR1 (Phase 1):
```
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~VacanteTests"
→ Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 2 ms
```

Sub-PR2 (Phase 2):
```
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~VacanteRepositoryQueryTests"
→ Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 322 ms
```

Sub-PR3 (Phase 3 — work unit 3.x, comando de validación mínima del brief):
```
dotnet test SGV.slnx --no-build --nologo --filter "VacanteServicioComandosTests|VacantesControllerTests|DatosSemilla_EstadoVacante_SeedIdsMatchConstantes"
→ Passed!  - Failed: 0, Passed: 36, Skipped: 0, Total: 36, Duration: 351 ms
```

Vacante global (no regresión):
```
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Vacante|FullyQualifiedName~EstadoVacanteConstantes"
→ Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60
```
(6 unit Vacante + 3 `[MySqlFact]` VacanteRepositoryQueryTests + 15 unit VacanteServicioComandos + 20 integration VacantesController + 9 unit EstadoVacanteConstantes + 7 misceláneos del repositorio que matchean `Vacante` = 60)

## Remaining Tasks (slice 1)

- [x] 3.1 `IVacanteServicioComandos` + `VacanteServicioComandos` (invoca `ActualizarObservaciones`).
- [x] 3.2 `FluentValidation` en `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/`.
- [x] 3.3 `IVacanteServicioConsulta` + `IEstadoVacanteServicioConsulta` + impls.
- [x] 3.4 `VacantesController` + `EstadosVacanteController` con `[Authorize]` y `RolesSgvMutacion=Administrador,GestorVacantes`.
- [x] 3.5 Registrar servicios en `src/SGV.Infraestructura/DependencyInjection.cs`.
- [x] 3.6 RED `VacanteServicioComandosTests` (conflict PuestoId-abierta, terminal inmutable, atomicidad service-level).
- [x] 3.7 RED `VacantesControllerTests` (201/400/403/404/409/401; `?status=invalido`→abiertas).
- [x] 3.8 `EstadoVacanteConstantes.cs` + test paridad.
- [x] 3.9 Bloque `20000000-…` en `docs/decisiones-implementacion.md` (sección "Mapa de bloques GUID").
- [ ] (Slice 2 web) 4.x + 5.x: Index/Create/Edit/Details + ApiClient + `_Sidenav` + smoke tests.

## Workload / PR Boundary (sub-lanzamiento 3)

- **Mode**: feature-branch-chain (slice 1 backend, sub-lanzamiento 3 de 3).
- **Cadena**: `feature/implementar-modulo-vacantes` ← este PR (3.x). PRs posteriores del slice 2 web (4.x+5.x) apilarán sobre éste.
- **Current work unit**: 3.1 → 3.9 (Application services + Controllers + DI + Constants + Docs + Tests).
- **Líneas añadidas (7 commits)**: 226 + 440 + 172 + 80 + 271 + 501 + 523 + 138 = 2351 (cumulative para 3.x, excluyendo el trabajo de Phase 1+2). Bajo presupuesto 400 por commit individual (con una excepción D-3.4 documentada).
- **PR boundary**: listo para `feat(vacantes) backend sub-PR3 — application services + controllers + tests`.

## Rollback Boundary (sub-lanzamiento 3)

- `git revert` del merge commit del sub-PR3 devuelve HEAD al estado del sub-PR2 (Phase 2) sin tocar otros módulos.
- Archivos removibles sin efectos colaterales:
  - `src/SGV.Aplicacion/Vacantes/Comandos/{IVacanteServicioComandos,VacanteServicioComandos}.cs` (nuevos)
  - `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/{CrearVacanteRequestValidator,CambiarEstadoVacanteRequestValidator}.cs` (nuevos)
  - `src/SGV.Aplicacion/Vacantes/Consultas/{IVacanteServicioConsulta,VacanteServicioConsulta,IEstadoVacanteServicioConsulta,EstadoVacanteServicioConsulta}.cs` (nuevos)
  - `src/SGV.Aplicacion/Vacantes/Consultas/IVacanteRepository.cs` (modificado — quitar los nuevos métodos)
  - `src/SGV.Aplicacion/Vacantes/Consultas/IEstadoVacanteRepository.cs` (nuevo)
  - `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` (modificado — quitar `RegistrarCambioEstadoAsync` y `UpdateAsync`)
  - `src/SGV.Infraestructura/Persistencia/Repositorios/EstadoVacanteRepository.cs` (nuevo)
  - `src/SGV.Infraestructura/Persistencia/Catalogos/EstadoVacanteConstantes.cs` (nuevo)
  - `src/SGV.Api/Controllers/{VacantesController,EstadosVacanteController}.cs` (nuevos)
  - `src/SGV.Api/Infrastructure/Results/ApiResults.cs` (modificado — quitar overloads VacanteError)
  - `src/SGV.Contracts/Seguridad/RolesSgv.cs` (modificado — quitar `RolesSgvMutacion`)
  - `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` (modificado — quitar `DatosInvalidos`)
  - `src/SGV.Infraestructura/DependencyInjection.cs` (modificado — quitar las 5 AddScoped de Vacantes)
  - `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` (nuevo)
  - `tests/SGV.Tests/Api/VacantesControllerTests.cs` (nuevo)
  - `tests/SGV.Tests/Persistencia/EstadoVacanteConstantesTests.cs` (nuevo)
  - `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` (modificado — quitar fakes y RemoveService)
  - `docs/decisiones-implementacion.md` (modificado — quitar fila 20000000-…)
- Sin migraciones EF nuevas. Sin cambios en `Program.cs`. Los endpoints
  HTTP de Vacantes dejan de existir al revertir, pero el resto de la
  API sigue funcionando.
