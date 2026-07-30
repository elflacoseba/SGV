# Apply Progress — Implementar el módulo de Vacantes

> Sub-lanzamientos 1 y 2 del slice 1 backend (work units 1.1 → 1.7 y 2.1 → 2.4).
> Modo: Strict TDD (`strict_tdd: true` confirmado en `openspec/config.yaml`).
> Persistencia: híbrido (OpenSpec + Engram).

## TDD Cycle Evidence

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
| (sub-PR2) | feat(repository) | `feat(vacantes): add VacanteRepository with segment query and atomicidad` |

## Files Changed

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

### Sub-lanzamiento 1
- **CambiarEstadoVacanteRequest** ahora incluye `Observaciones` (opcional). El interface del `design.md §Interfaces / Contracts` mostraba sólo `(EstadoVacanteId, Motivo?)`, pero la tarea 1.6 explícitamente pide el campo `Observaciones` y la OQ-3 está resuelta con "Observaciones opcional". Decisión coherente con `tasks.md` (1.6, OQ-3 Resuelta) y con el commit 1 (1.1 cubre la mutación en el agregado).
- **`VacanteCommandResult.Value` se tipó como `VacanteDetailDto?`** (no `VacanteDto?` como en `OcupacionCommandResult`). Coincide con `design.md §Interfaces / Contracts` (último bloque). Implicación: para GET/listados se sigue pudiendo devolver DTOs más livianos (`VacanteDto` directo desde el servicio de consulta) sin pasar por `CommandResult`.

### Sub-lanzamiento 2
- **`VacanteRepository.GetByIdForUpdateAsync` no popula `Vacante._historialEstados`** (la colección de dominio queda vacía). El diseño menciona que `Vacante.CambiarEstado` agrega a la colección para que EF la persista; sin embargo, en el patrón actual del repo (`Puesto`/`Ocupacion`), el Reconstitute hidrata escalares + nav props pero no colecciones, y el bridge entre la colección de dominio y la de EF queda en el servicio (work unit 3.x). Documentado en el comentario XML de `Vacante.Reconstitute` para que el implementador de 3.x sincronice explícitamente `entity.HistorialEstados.Add(...)` con el resultado de `vacante.CambiarEstado(...)` antes de `SaveChangesAsync`. Esto preserva la atomicidad por EF en una transacción.
- **`ListarAsync` agrega escape de wildcards LIKE** (paridad con `OcupacionRepository.EscapeLikePattern`). El brief sólo menciona 4 métodos pero `VacanteListQuery.Search` exige el escape; sin él, una búsqueda con `%`/`_` podría sobre-matchear filas. Mantenido por consistencia con el patrón del repo de Ocupaciones.

## Issues Found

- **MySQL local disponible** durante este apply (default `Server=localhost;Port=3306;Database=sgv_test`). Los `[MySqlFact]` se ejecutan contra la base de pruebas sin stub. Si el siguiente sub-lanzamiento se corre en un entorno sin MySQL, los 3 tests del work unit 2.x se skipean limpio (sin bloquear el resto de la suite).
- **Build sin warnings nuevos** en código del change. 75 warnings totales pre-existentes (analyzer `xUnit1031`/`xUnit2013`/`EF1002`/`xUnit2029`/`xUnit1026`); 0 asociados a archivos del work unit 2.x.

## Remaining Tasks (slice 1)

- [ ] 3.1 `IVacanteServicioComandos` + `VacanteServicioComandos` (invoca `ActualizarObservaciones`).
- [ ] 3.2 `FluentValidation` en `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/`.
- [ ] 3.3 `IVacanteServicioConsulta` + `IEstadoVacanteServicioConsulta` + impls.
- [ ] 3.4 `VacantesController` + `EstadosVacanteController` con `[Authorize]` y `RolesSgvMutacion=Administrador,GestorVacantes`.
- [ ] 3.5 Registrar servicios en `src/SGV.Infraestructura/DependencyInjection.cs`.
- [ ] 3.6 RED `VacanteServicioComandosTests` (conflict PuestoId-abierta, terminal inmutable, atomicidad service-level).
- [ ] 3.7 RED `VacantesControllerTests` (201/400/403/404/409/401; `?status=invalido`→abiertas).
- [ ] 3.8 `EstadoVacanteConstantes.cs` + test paridad.
- [ ] 3.9 Bloque `20000000-…` en `docs/decisiones-implementacion.md` (sección "Mapa de bloques GUID").
- [ ] (Slice 2 web) 4.x + 5.x: Index/Create/Edit/Details + ApiClient + `_Sidenav` + smoke tests.
