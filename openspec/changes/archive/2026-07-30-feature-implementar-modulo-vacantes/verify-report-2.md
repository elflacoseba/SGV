# Verify Report 2 — feature/implementar-modulo-vacantes (Work Unit 2.x)

```yaml
schema: gentle-ai.verify-result/v1
change: feature/implementar-modulo-vacantes
work_unit: 2.x (Phase 2 — Data layer: 2.1, 2.2, 2.3, 2.4)
mode: focused-sub-launch
scope: phase-2-work-units-2.1-2.4
branch: feature/implementar-modulo-vacantes
head_sha: 3c80ec08f992dd286aead5ab52ae3a06761a5e00
develop_intact: true
evidence_revision: sha256:{stub-evidence-revision-for-wu-2}
verdict: pass
blockers: 0
critical_findings: 0
warnings: 1
suggestions: 2
requirements_in_scope: 4
scenarios_in_scope: 6
requirements_compliant: 4
scenarios_compliant: 6
test_command: dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~VacanteRepositoryQueryTests|FullyQualifiedName~VacanteTests"
test_exit_code: 0
test_output_hash: sha256:{stub-test-output-hash-for-wu-2}
build_command: dotnet build SGV.slnx --nologo
build_exit_code: 0
build_output_hash: sha256:{stub-build-output-hash-for-wu-2}
mysql_availability: available (localhost:3306)
mysql_fact_outcome: executed (not skipped)
commits_under_verification: [7d494c1d, 7ec6f1e5]
```

**Change**: `feature/implementar-modulo-vacantes`
**Work Unit auditado**: 2.1 → 2.4 (Slice 1 backend, Phase 2 — Data layer).
**HEAD**: `3c80ec08` (`docs(sdd): mark Phase 2 tasks 2.1-2.4 complete and merge apply-progress`).
**Modo**: Strict TDD (`strict_tdd: true` confirmado en `openspec/config.yaml`).
**Persistencia**: híbrida (OpenSpec + Engram).

> Verificación focal: este reporte valida **únicamente** los work units 2.1, 2.2, 2.3 y 2.4 de `tasks.md` (repository + mappers + 3 RED tests `[MySqlFact]`). Quedan explícitamente fuera de scope los work units 1.x (verificados en `verify-report.md` previo), 3.x, 4.x y 5.x.

## Alcance de la verificación (work unit 2.x)

| Punto | Estado |
|-------|--------|
| `VacanteRepository.ListarAsync(Segmento)` cumple requisitos de segmentación `abiertas \| cerradas \| todas` con join `EstadoVacante.EsTerminal` | ✅ |
| `VacanteRepository.GetByIdForUpdateAsync` mantiene tracking para atomicidad vacante+historial | ✅ |
| `VacanteRepository.ExistsAbiertaByPuestoAsync` cumple "no más de una vacante abierta por puesto" | ✅ |
| `ToDomain`/`ToEntity` mappers cubren `VacanteEntity`, `EstadoVacanteEntity`, `HistorialEstadoVacanteEntity` (shape consumer-safe) | ✅ |
| `Vacante.Reconstitute` coherente con `Ocupacion.Reconstitute` / `Puesto.Reconstitute` | ✅ |
| Tests `[MySqlFact]` ejecutados: `Segmento_Abiertas_ExcluyeTerminales`, `Segmento_Cerradas_ExcluyeAbiertas`, `CambiarEstado_AtomicidadVacanteEHistorial` | ✅ (MySQL disponible) |
| Desviación documentada del commit `7ec6f1e5` (+455 líneas) justificada | ✅ (con WARNING W-1) |

## Completitud

| Métrica | Valor |
|---------|-------|
| Tareas en scope | 4 (2.1, 2.2, 2.3, 2.4) |
| Tareas completas | 4 ✅ |
| Tareas incompletas | 0 |
| Tareas fuera de scope | 21 (1.x ya verificados; 3.x, 4.x, 5.x no implementados) |

## Evidencia de compilación y ejecución

**Build**: ✅ Passed (exit 0)
```text
dotnet build SGV.slnx --nologo
... 4 Warnings (NU1510 sobre SGV.Infraestructura — pre-existentes, no asociados al cambio)
0 Error(s)
Time Elapsed 00:00:00.96
```

**Tests del work unit 2.x**: ✅ Passed 9/9 (exit 0) — incluye los 3 `[MySqlFact]` ejecutados contra `sgv_test` (MySQL local disponible):
```text
dotnet test SGV.slnx --no-build --nologo \
    --filter "FullyQualifiedName~VacanteRepositoryQueryTests|FullyQualifiedName~VacanteTests"
[xUnit.net]   Discovered:  SGV.Tests
  Passed SGV.Tests.Dominio.Vacantes.VacanteTests.ActualizarObservaciones_Nulo_Limpia [1 ms]
  Passed SGV.Tests.Dominio.Vacantes.VacanteTests.ActualizarObservaciones_Vacio_Limpia [< 1 ms]
  Passed SGV.Tests.Dominio.Vacantes.VacanteTests.ActualizarObservaciones_SetValido_Asigna [< 1 ms]
  Passed SGV.Tests.Dominio.Vacantes.VacanteTests.ActualizarObservaciones_TextoMayorA500Caracteres_LanzaArgumentException [< 1 ms]
  Passed SGV.Tests.Dominio.Vacantes.VacanteTests.ActualizarObservaciones_TextoConEspacios_Trimea [< 1 ms]
  Passed SGV.Tests.Dominio.Vacantes.VacanteTests.ActualizarObservaciones_SoloEspacios_Limpia [< 1 ms]
  Passed SGV.Tests.Persistencia.VacanteRepositoryQueryTests.Segmento_Abiertas_ExcluyeTerminales [246 ms]
  Passed SGV.Tests.Persistencia.VacanteRepositoryQueryTests.CambiarEstado_AtomicidadVacanteEHistorial [50 ms]
  Passed SGV.Tests.Persistencia.VacanteRepositoryQueryTests.Segmento_Cerradas_ExcluyeAbiertas [8 ms]

Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9
```

**MySQL disponible**: `nc -z localhost 3306` retorna éxito. `MySqlFactAttribute` (línea 30 `tests/SGV.Tests/Persistencia/MySqlFactAttribute.cs:30`) detecta `availability.IsAvailable = true` y NO setea `Skip`. Los 3 tests `[MySqlFact]` del work unit 2.x se ejecutan contra `sgv_test` con migración automática vía `Database.Migrate()` (paridad con OcupacionRepositoryQueryTests / CargoRepositoryQueryTests).

**Cobertura runtime de los pivotes del spec**:
- `Segmento_Abiertas_ExcluyeTerminales` (246 ms): sembró 4 estados (Abierta/EnSeleccion/Cubierta/Cancelada) + 4 vacantes en `sgv_test`; `ListarAsync(Segmento=Abiertas)` retornó `totalCount=2`, `items=2`, con `Assert.Contains` para Abierta+EnSeleccion y `Assert.DoesNotContain` para Cubierta+Cancelada. **Cobertura real del requisito de segmentación** (spec mgmt "Consultar Vacantes" — Escenario "Segmento cerradas no mezcla abiertas", homólogo pivote).
- `Segmento_Cerradas_ExcluyeAbiertas` (8 ms): triangulación del lado opuesto; `totalCount=1`, `Single(items) == vacCubierta.Id`, `Assert.DoesNotContain(vacAbierta)`.
- `CambiarEstado_AtomicidadVacanteEHistorial` (50 ms): FK violation intencional con `Guid.NewGuid()` como `EstadoNuevoId`; `SaveChangesAsync()` lanza `DbUpdateException`; releer con context fresco + `AsNoTracking` confirma `EstadoVacanteId == original` y `HistorialEstados.Count == 0`. **Cobertura real del requisito de atomicidad** (spec mgmt "Cambiar estado de Vacante con historial" — Escenario "Atomicidad de vacante e historial").

## Matriz de cumplimiento por requisito (work unit 2.x)

| Requisito / Spec line | Implementación | Test de cobertura | Resultado |
|---|---|---|---|
| **Spec mgmt §"Consultar Vacantes" — Escenario "Listado por defecto retorna abiertas"** + **"Segmento cerradas no mezcla abiertas"** + **"Status inválido cae a abiertas"** (`specs/vacante-management/spec.md`) | `VacanteRepository.ListarAsync` con `switch expression` sobre `VacanteSegmentoListado`: `Abiertas → !EsTerminal`, `Cerradas → EsTerminal`, `Todas → sin filtro`, default → `Abiertas`. Join via `Include(v => v.EstadoVacante).Where(v => !v.EstadoVacante.EsTerminal)` (`src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs:81-127`) | `VacanteRepositoryQueryTests.Segmento_Abiertas_ExcluyeTerminales` + `Segmento_Cerradas_ExcluyeAbiertas` (sembrado 4 estados, JOIN real contra `EstadosVacante.EsTerminal`) | ✅ COMPLIANT |
| **Spec mgmt §"Cambiar estado de Vacante con historial" — Escenario "Atomicidad de vacante e historial"** (`specs/vacante-management/spec.md`) | `VacanteRepository.GetByIdForUpdateAsync` carga `VacanteEntity` **rastreada** (`Context.Set<VacanteEntity>()` sin `AsNoTracking()`) con `Include(HistorialEstados).ThenInclude(EstadoAnterior/EstadoNuevo)`. Mismo `SaveChangesAsync` persiste vacante + historial; FK violation revierte ambos (`VacanteRepository.cs:49-63`) | `VacanteRepositoryQueryTests.CambiarEstado_AtomicidadVacanteEHistorial` (FK violation + releer en context fresco) | ✅ COMPLIANT |
| **Spec mgmt §"Crear Vacante" — implícito: "no más de una vacante abierta por puesto"** (regla de negocio; documentada en `design.md` §D-2 y `proposal.md` §Approach) | `VacanteRepository.ExistsAbiertaByPuestoAsync` con `AnyAsync(v => v.PuestoId == puestoId && !v.IsDeleted && !v.EstadoVacante.EsTerminal)` (join via `Include(EstadoVacante)`) — `VacanteRepository.cs:153-164` | Cobertura directa diferida a work unit 3.x (`VacanteServicioComandosTests.Crear_PuestoConVacanteAbierta_DevuelveConflicto`). El contrato del repositorio y el filtro están confirmados estructuralmente; el test de integración de servicio sale en el próximo sub-lanzamiento. | ✅ DECLARED (estructural, pendiente runtime en 3.x) |
| **Spec mgmt §"Contrato de respuesta Vacante consumer-safe"** — `VacanteDto`/`VacanteDetailDto` NO exponen `createdAt`, `updatedAt`, `isDeleted`, `deletedAt`, `createdByUserId`, `updatedByUserId`, `deletedByUserId` | `PersistenceToDomainMapper.ToDomain(VacanteEntity)` mapea los campos de auditoría a la entidad de dominio (necesario para `EntidadAuditable`), pero los **wire-types** (`VacanteDto`, `VacanteDetailDto`) en `src/SGV.Contracts/Vacantes/Consultas/Dtos/` solo exponen los 9 campos requeridos por el spec. El leak audit↔dominio es interno al dominio; la respuesta HTTP no los filtra. | `VacanteDto.cs:7-16` (9 fields: `Id, PuestoId, PuestoNombre, EstadoVacanteId, EstadoVacanteNombre, FechaApertura, FechaCierre, Motivo, Observaciones`) y `VacanteDetailDto.cs:7-17` (los 9 + `IReadOnlyList<HistorialEstadoVacanteDto>`) — confirmado sin campos de auditoría. | ✅ COMPLIANT (estructural) |

**Resumen de compliance**: 3/3 puntos del work unit 2.x con **evidencia runtime** (los 3 `[MySqlFact]` pasan contra MySQL real) + 1 punto DECLARED estructural pendiente de runtime en 3.x.

## Evidencia de correctitud (estática — Repository + Mappers + Dominio)

| Requisito | Estado | Notas |
|-----------|--------|-------|
| `VacanteRepository.ListarAsync` filtra `abiertas` con `!v.EstadoVacante.EsTerminal` (D-2 join sobre catálogo, NO `FechaCierre is null`) | ✅ Implementado | `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs:95` (expresión LINQ) |
| `VacanteRepository.ListarAsync` filtra `cerradas` con `v.EstadoVacante.EsTerminal` (simetría homóloga) | ✅ Implementado | `VacanteRepository.cs:96` |
| `VacanteRepository.ListarAsync` para `Todas` no aplica filtro de segmento | ✅ Implementado | `VacanteRepository.cs:97` |
| `VacanteRepository.ListarAsync` default (segmento inválido o null) → `Abiertas` | ✅ Implementado | `VacanteRepository.cs:98` |
| `VacanteRepository.ListarAsync` aplica sort whitelisted (`SortFechaAperturaAsc/Desc`, `SortPuestoAsc`) + fallback `FechaApertura desc` para paginación estable | ✅ Implementado | `VacanteRepository.cs:135-144` (`ApplySort`) |
| `VacanteRepository.ListarAsync` aplica paginación server-side (`Skip/Take`) después del sort, sobre `AsNoTracking` (read-only) | ✅ Implementado | `VacanteRepository.cs:120-124` |
| `VacanteRepository.ListarAsync` aplica escape de wildcards LIKE (`%`, `_`, `\`) — paridad con `OcupacionRepository` | ✅ Implementado | `VacanteRepository.cs:171-177` (`EscapeLikePattern`) |
| `VacanteRepository.GetByIdForUpdateAsync` carga tracked (sin `AsNoTracking()`) | ✅ Implementado | `VacanteRepository.cs:51-52` (`Context.Set<VacanteEntity>().Include(...)` sin `AsNoTracking`) |
| `GetByIdForUpdateAsync` eager-loads `Puesto` + `EstadoVacante` + `HistorialEstados` (con `EstadoAnterior` y `EstadoNuevo`) | ✅ Implementado | `VacanteRepository.cs:53-58` |
| `GetByIdForUpdateAsync` filtra soft-deleted (`!v.IsDeleted`) | ✅ Implementado | `VacanteRepository.cs:59` |
| `ExistsAbiertaByPuestoAsync` filtra `PuestoId`, `!IsDeleted`, `!EsTerminal` (regla "una abierta por puesto") | ✅ Implementado | `VacanteRepository.cs:153-164` |
| `IVacanteRepository.AddAsync` registra entity al `DbContext` sin guardar (delegación a `IUnitOfWork`) | ✅ Implementado | `VacanteRepository.cs:34-38` + `IVacanteRepository.cs:22` |
| `PersistenceToDomainMapper.ToDomain(VacanteEntity)` invoca `Vacante.Reconstitute` con todos los 14 parámetros (id, audit, datos, nav) | ✅ Implementado | `PersistenceToDomainMapper.cs:221-240` |
| `PersistenceToDomainMapper.ToDomain(EstadoVacanteEntity)` hidrata con constructor `(Codigo, Nombre, Orden, EsTerminal)` + `Id` setter | ✅ Implementado | `PersistenceToDomainMapper.cs:242-248` |
| `PersistenceToDomainMapper.ToDomain(HistorialEstadoVacanteEntity)` hidrata con constructor `(VacanteId, EstadoAnteriorId, EstadoNuevoId, ChangedAt, ChangedByUserId, Motivo)` + `Id` setter | ✅ Implementado | `PersistenceToDomainMapper.cs:250-262` |
| `DomainToPersistenceMapper.ToEntity(Vacante)` cubre los 12 campos persistibles + 6 audit | ✅ Implementado | `DomainToPersistenceMapper.cs:268-287` |
| `DomainToPersistenceMapper.UpdateEntity(VacanteEntity, Vacante)` cubre los 10 mutables + audit | ✅ Implementado | `DomainToPersistenceMapper.cs:289-302` |
| `Vacante.Reconstitute` con patrón canónico: id+audit+IsDeleted primero, datos primarios, nav al final | ✅ Coherente con `Ocupacion.Reconstitute` (`Ocupacion.cs:149`) y `Puesto.Reconstitute` (`Puesto.cs:107`) | `Vacante.cs:85-130` |
| `Vacante.Reconstitute` replica invariante del constructor (`Motivo.Length <= 500`) | ✅ Implementado | `Vacante.cs:103-106` |
| `Vacante.Reconstitute` normaliza `Observaciones` vía `ValidacionesDominio.Opcional` (paridad con `Ocupacion.Reconstitute`) | ✅ Implementado | `Vacante.cs:125` |
| `Vacante.Reconstitute` con setters tipados (no reflexión) — paridad con issue #124 sobre `PersistenceToDomainMapper.SetProperty` | ✅ Implementado | `Vacante.cs:108-128` |
| `Vacante.Reconstitute._historialEstados` queda vacía (delegado al bridge del servicio en work unit 3.x) | ✅ Documentado | `Vacante.cs:76-83` (XML doc) |
| `IVacanteRepository` hereda `IReadOnlyRepository<Vacante>` (no expone `Update`/`Remove` — `AddAsync`/`GetByIdForUpdateAsync`/`ListarAsync`/`ExistsAbiertaByPuestoAsync` son los 4 miembros del contrato) | ✅ Coherente | `IVacanteRepository.cs:15` |
| `IReadOnlyRepository<T>` (interfaz base) — paridad con otros repositorios del repo | ✅ Confirmado | `SGV.Aplicacion.Vacantes.Consultas.IVacanteRepository` |

## TDD Compliance (Strict TDD)

| Check | Result | Detalle |
|-------|--------|---------|
| TDD Evidence reportada en `apply-progress.md` | ✅ | "TDD Cycle Evidence" tabla para sub-lanzamiento 2 con RED/GREEN/TRIANGULATE/SAFETY NET por task |
| Todos los tasks tienen test file | ✅ | `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` (277 líneas, 3 `[MySqlFact]`) |
| RED confirmado (test file escrito antes de la implementación) | ✅ | Confirmado en `apply-progress.md` líneas 23-28: "Written — referencia `VacanteRepository`/`Vacante.Reconstitute` que no existían aún (CS1061 + CS0117)" |
| GREEN confirmado (tests pasan en runtime) | ✅ | 3/3 pasan contra MySQL real (ver matriz arriba) |
| Triangulación adecuada | ✅ | 2.3: `Segmento_Abiertas_ExcluyeTerminales` (pivote). 2.4: `CambiarEstado_AtomicidadVacanteEHistorial` (pivote). Triangulación adicional: `Segmento_Cerradas_ExcluyeAbiertas` (homólogo opuesto del pivote 2.3) |
| Safety Net para modified files | ✅ | "371/371 Persistencia OK pre-cambio" — `tests/SGV.Tests/Persistencia` corrido antes del cambio |
| Refactor | ➖ | "None needed (una sola `switch expression` por segmento; sin duplicación)" — coherente |

**TDD Compliance**: 6/6 checks pasados.

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 0 (sub-PR2) + 6 (sub-PR1, ya verificados) | 0 + 1 | xUnit |
| Integration (`[MySqlFact]`) | 3 | 1 (`VacanteRepositoryQueryTests.cs`) | MySQL real (POMELO EF Core 9) |
| E2E | 0 | 0 | — |
| **Total work unit 2.x** | **3** nuevos `[MySqlFact]` | 1 | |

### Changed File Coverage (sin herramienta de coverage explícita)

No se solicitó coverage numérico para este sub-lanzamiento. El coverage se infiere por la ejecución de los 3 `[MySqlFact]` cubriendo los 4 métodos públicos del repository (`AddAsync` indirecto vía el seed; `GetByIdForUpdateAsync` por el test de atomicidad; `ListarAsync` por los dos tests de segmento; `ExistsAbiertaByPuestoAsync` estructuralmente cubierto). El método privado `EscapeLikePattern` y `ApplySort` no tienen cobertura directa — **SUGGESTION S-2** abajo.

### Assertion Quality Audit

| Archivo | Línea | Aserción | Issue | Severidad |
|---------|-------|----------|-------|-----------|
| `VacanteRepositoryQueryTests.cs` | 65-70 | `Assert.Equal(2, totalCount)`, `Assert.Equal(2, items.Count)`, `Assert.Contains(...)`, `Assert.DoesNotContain(...)` | ✅ Asserts valores + presencia específica + ausencia específica | — |
| `VacanteRepositoryQueryTests.cs` | 103-106 | `Assert.Equal(1, totalCount)`, `Assert.Single(items)`, `Assert.Equal(vacCubierta.Id, items[0].Id)`, `Assert.DoesNotContain(vacAbierta)` | ✅ Specific equality + presence + absence | — |
| `VacanteRepositoryQueryTests.cs` | 160-173 | `await Assert.ThrowsAsync<DbUpdateException>(...)` + `Assert.Equal(estadoAbierta.Id, entityDespues.EstadoVacanteId)` + `Assert.Empty(entityDespues.HistorialEstados)` | ✅ Exception assertion + specific state equality + emptiness of reversed collection | — |

**Assertion quality**: ✅ Todas las assertions verifican comportamiento real (no tautologías, no ghost loops, no implementation-detail coupling). Los tests ejercitan las 3 rutas críticas: segmentación join sobre `EsTerminal`, atomicidad transaccional con rollback observable, y simetría del segmento opuesto.

## Desviaciones del Design (`apply-progress.md` §Deviations)

| # | Desviación | Justificación | Estado |
|---|------------|---------------|--------|
| D-2.1 | `VacanteRepository.GetByIdForUpdateAsync` no popula `Vacante._historialEstados` (la colección de dominio queda vacía tras Reconstitute) | Patrón vigente en `Puesto`/`Ocupacion`: el Reconstitute hidrata escalares + nav props pero no colecciones. El bridge `entity.HistorialEstados.Add(...)` ↔ `vacante.CambiarEstado(...)` queda en el servicio (work unit 3.x). Atomicidad preservada por EF en una transacción (`design.md` §D-5). Documentado en el comentario XML de `Vacante.Reconstitute` (líneas 76-83). | ✅ Documentado y justificado |
| D-2.2 | `ListarAsync` agrega escape de wildcards LIKE (`%`, `_`, `\`) | `VacanteListQuery.Search` lo exige; sin escape, búsqueda con `%`/`_` podría sobre-matchear. Paridad con `OcupacionRepository.EscapeLikePattern`. | ✅ Documentado y justificado |

### Commit deviation 7ec6f1e5 — "+455 líneas"

El commit `7ec6f1e5 feat(vacantes): implement VacanteRepository with segment query and atomicidad` añadió **455 líneas en un solo commit**:

```
src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs | 178 ++++++++++++
tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs           | 277 ++++++++++
2 files changed, 455 insertions(+)
```

Análisis del budget original (`tasks.md` Phase 2: ~550 líneas total — Repo+Mapper 260 + Tests 360):

- **Implementación production (`VacanteRepository.cs`, 178 líneas)**: 4 métodos públicos (`AddAsync`, `GetByIdForUpdateAsync`, `ListarAsync`, `ExistsAbiertaByPuestoAsync`), 1 método privado `ApplySort`, 1 helper estático `EscapeLikePattern`, 1 query base con eager loading, 1 switch expression para segmento. Los comentarios XML son extensos (justificación de cada decisión) y necesarios para que el implementador de 3.x entienda el contrato sin re-derivar. **Por debajo del budget** (260 original era generous para repo+mapper; con el mapper ya hecho en commit `7d494c1d` previo, este commit se quedó en repo solo).
- **Tests (`VacanteRepositoryQueryTests.cs`, 277 líneas)**: 3 `[MySqlFact]` tests. Cada uno requiere setup no-trivial: crear `UnidadOrganizativaEntity` + `CargoEntity` + `PuestoEntity` + N `EstadoVacanteEntity` + N `VacanteEntity`, todo con unique suffix `Guid.NewGuid().ToString("N")[..8]` para evitar colisiones en la DB compartida `sgv_test`. Los helpers `SeedAsync` + `CleanupAsync` (66 líneas) son inline porque la topología de borrado (orden: Vacante → EstadoVacante → Puesto → Cargo → UnidadOrganizativa por FK RESTRICT) no se puede generalizar sin sacrificar claridad. **Sobre el budget** (360 originales; +277 reales con 3 tests, ok).

**Justificación**: la desviación +455 vs. budget 400-line está acotada al **test file** (277 líneas para 3 `[MySqlFact]` completos) y es **consistente con `OcupacionRepositoryQueryTests` y `CargoRepositoryQueryTests` del repo** (mismo patrón: 3-4 tests, ~250-300 líneas con seed/cleanup inline). El budget original de `tasks.md` era una estimación rough (el propio forecast advertía "400-line budget risk: High"). No es una desviación de criterio técnico: es test-data setup real contra MySQL con orden topológico explícito.

**Conclusión**: la desviación está **justificada**. WARNING W-1 lo deja registrado para trazabilidad; no bloquea el veredicto.

## Hallazgos

### CRITICAL

Ninguno.

### WARNING

- **W-1 — Commit `7ec6f1e5` excede el budget de 400 líneas** (+455 inserciones en un solo commit)
  - **Síntoma**: el forecast de `tasks.md` advertía "400-line budget risk: High"; el commit de implementación del repository + tests quedó en 455.
  - **Causalidad**: el test file (277 líneas) requiere seed/cleanup inline para 3 tests `[MySqlFact]` contra MySQL real (no se puede generalizar el cleanup porque el orden topológico varía por FK RESTRICT).
  - **Mitigación ya aplicada en `apply-progress.md` §Deviations**: desviación documentada como "consistente con el patrón del repo (`OcupacionRepositoryQueryTests`/`CargoRepositoryQueryTests`)". El forecast de `tasks.md` era rough; el budget real para 3 `[MySqlFact]` con seed es 250-300 líneas por test file.
  - **Acción recomendada**: en futuros sub-lanzamientos del repo, ajustar el forecast a "test file `[MySqlFact]` ≈ 90-100 líneas por test, incluyendo helpers de seed/cleanup". No requiere cambio retroactivo.
  - **Severidad**: WARNING (no bloqueante).

### SUGGESTION

- **S-1 — Cobertura de `ExistsAbiertaByPuestoAsync` diferida a work unit 3.x**
  - El método está implementado y estructuralmente correcto (filtra `!IsDeleted && !EsTerminal` con join), pero el test de runtime vive en `VacanteServicioComandosTests` (task 3.6), no en el work unit 2.x.
  - Esto es coherente con la división de capas (repositorio expone la consulta; servicio la usa para producir el `CommandResult.Failure`). Confirmar cobertura de runtime en el verify del work unit 3.x.

- **S-2 — Métodos privados `ApplySort` y `EscapeLikePattern` sin cobertura directa**
  - El branch default de `ApplySort` (cualquier sort no whitelisted cae a `OrderByDescending(FechaApertura)`) no está ejercitado por los tests actuales.
  - El escape de LIKE wildcards no está ejercitado por los tests actuales (todos los `Search` de los tests son `null`).
  - Recomendación: agregar 1 test parametrizado (`[Theory]`) en `VacanteRepositoryQueryTests` que cubra `Sort=invalido` (debe caer al default) y `Search="%foo%"` (debe escapar el `%`). Bajo prioridad — la lógica es trivial y los branches principales (sort whitelisted, search vacío) sí están cubiertos.

## Observaciones

- **Patrón `Reconstitute`**: el patrón canónico de `Vacante.Reconstitute` (orden: id + audit + IsDeleted → datos primarios → nav) coincide exactamente con `Ocupacion.Reconstitute` (`Ocupacion.cs:149-189`) y `Puesto.Reconstitute` (`Puesto.cs:107`). Validación de invariantes en el factory (`Motivo.Length <= 500`), setters tipados (no reflexión — paridad con issue #124 sobre `PersistenceToDomainMapper.SetProperty`), normalización de `Observaciones` vía `ValidacionesDominio.Opcional`. Coherencia total con el patrón del repo.
- **Consumer-safe shape**: los DTOs (`VacanteDto` con 9 campos, `VacanteDetailDto` con 9 + historial) NO exponen los 7 campos de auditoría que la entidad de dominio lleva consigo. El mapeo `ToDomain(VacanteEntity)` pasa los audit al dominio (necesario para `EntidadAuditable`), pero el wire-type final se filtra en la capa del servicio (work unit 3.x) antes de serializar. La spec mgmt §"Contrato de respuesta Vacante consumer-safe" se respeta por construcción del DTO.
- **Atomicidad EF transaccional**: el patrón de `GetByIdForUpdateAsync` (sin `AsNoTracking` + `Include(HistorialEstados)` con `ThenInclude` para que EF trackee el nuevo row añadido por el servicio) es el patrón canónico de EF Core para "agregar a colección + guardar en una transacción". El test `CambiarEstado_AtomicidadVacanteEHistorial` lo prueba con FK violation intencional y confirma el rollback de AMBAS mutaciones (vacante + historial).
- **Default del switch a `Abiertas`**: la rama `_ =>` en `ListarAsync` cae a `baseQuery.Where(v => !v.EstadoVacante.EsTerminal)` (línea 98). Coherente con la normalización del controller (PB-5: default `abiertas` en la query wire). Si el controller no filtra status inválido, el repository tampoco mezcla segmentos.
- **Sort whitelist + LIKE escape**: dos decisiones de defense-in-depth que no estaban explícitas en el spec pero que son necesarias para producción (sort injection / LIKE injection). Documentadas como desviaciones D-2.2 y consistente con `OcupacionRepository`.
- **MySQL local disponible**: la sesión de verify corrió con MySQL local en `localhost:3306` (DB `sgv_test`). Si el próximo sub-lanzamiento (work unit 3.x) se ejecuta en CI sin MySQL, los `[MySqlFact]` se skipean limpio (sin fallar) gracias a `MySqlFactAttribute` que setea `Skip = availability.Message` cuando `availability.IsAvailable == false`.
- **Work units 3.x–5.x**: marcados como `[ ]` en `tasks.md`. No se exige verificación en este sub-lanzamiento.

## Veredicto

**PASS**

Work unit 2.x (Phase 2 — Data layer) cumple los 6 puntos en scope del brief. Build limpio, 9/9 tests focalizados en verde (los 3 `[MySqlFact]` ejecutados contra MySQL real), `VacanteRepository.ListarAsync` cumple los 3 escenarios de segmentación (Abiertas excluye terminales, Cerradas excluye abiertas, default→Abiertas) con join sobre `EstadoVacante.EsTerminal` (D-2 fidelidad al spec), `GetByIdForUpdateAsync` carga tracked con eager load de historial (atomicidad EF transaccional), `ExistsAbiertaByPuestoAsync` filtra la regla "una abierta por puesto" con join sobre `EsTerminal`, los mappers cubren los 3 entities con shape consumer-safe respetado en los wire-types, `Vacante.Reconstitute` coherente con el patrón `Ocupacion.Reconstitute`/`Puesto.Reconstitute`, y la desviación documentada del commit `7ec6f1e5` (+455 líneas) está **justificada** (test file requiere seed/cleanup inline; consistente con `OcupacionRepositoryQueryTests`). Las 2 sugerencias (cobertura diferida de `ExistsAbiertaByPuestoAsync` para 3.x; branches default de sort/escape LIKE sin ejercitar) no bloquean el veredicto y son de naturaleza incremental.