# Apply-Progress: Vacantes Hardening

**Fecha**: 2026-08-18
**Cambio**: `vacantes-hardening`
**Modo**: Strict TDD (red-green-refactor por tarea)
**Artifact store**: hybrid (Engram + filesystem)

---

## Resumen ejecutivo

Cambio de **hardening** ejecutado en 5 clusters (D-1, D-2, D-3, D-4, D-5) más
las triviales F.1/F.2/F.3. **23 tareas completadas**, 17 commits
convencionales (sin `Co-Authored-By`), 0 regresiones introducidas, build
limpio. Tests: 3291 pass / 312 skip (MySQL) / 7 fallos pre-existentes
confirmados ajenos al cambio vía `git stash`.

---

## Estado por tarea

### Cluster A — D-1 Identidad de usuario en transiciones

| # | Tarea | Estado | Commit |
|---|---|---|---|
| 01 | A.3 — Crear `FakeUsuarioActual` stub (UserId default, Anonymous singleton) | ✅ | `410a1e9a` |
| 02 | A.4 — RED tests actor/anónimo/history (3 nuevos en Vacante, 2 en Ocupacion) | ✅ | `7996347c` |
| 03 | A.1 — Inyectar `IUsuarioActual` en `VacanteServicioComandos` + guard `ErrorCategoria.Unauthorized` | ✅ | `8b61d4d7` |
| 04 | A.2 — Inyectar `IUsuarioActual` en `OcupacionServicioComandos` (Cubrir path) + guard | ✅ | `1c331b62` |
| 05 | A.5 — Verificar DI factory (auto-resolve por longest ctor match) | ✅ | (sin commit — DI automática) |

**Resultado**: `HistorialEstadoVacante.ChangedByUserId` se persiste con el `UserId` del principal. Convenient constructor con `NullUsuarioActual.Instance` mantiene back-compat para los tests pre-existentes.

### Cluster B — D-2 Eliminación de `ActualizarObservacionesAsync`

| # | Tarea | Estado | Commit |
|---|---|---|---|
| 06 | B.1 — Borrar 4 tests orphan en `VacanteServicioComandosTests.cs` | ✅ | `ffa651ce` |
| 07 | B.2 — Borrar método `ActualizarObservacionesAsync` en impl | ✅ | `e641684f` |
| 08 | B.3 — Borrar signatura en `IVacanteServicioComandos` | ✅ | `02108ca9` |
| 09 | B.4 — Borrar override en `FakeVacanteServicioComandos` | ✅ | `c7fcfcf4` |
| 10 | B.5 — Grep audit (0 refs confirmado) | ✅ | (sin commit — verificado) |

**Resultado**: superficie huérfana completamente removida; comportamiento equivalente preservado vía side-effect de `CambiarEstadoAsync.Observaciones`.

### Cluster E — D-5 Dead code `MotivoObligatorio`

| # | Tarea | Estado | Commit |
|---|---|---|---|
| 11 | E.1 — Borrar constante en `VacanteErrorCodigo.cs` | ✅ | `2af17276` |
| 12 | E.2 — Grep audit (0 refs confirmado) | ✅ | (sin commit — verificado) |

### Cluster C — D-3 Split de `VacanteInputModel`

| # | Tarea | Estado | Commit |
|---|---|---|---|
| 13 | C.1 — Crear `VacanteCreateInputModel` + `VacanteEditInputModel`; borrar `VacanteInputModel` | ✅ | `9deb49fc` |
| 14 | C.2 — Bind `Create.cshtml.cs` al nuevo tipo + quitar `ModelState.Remove` + pre-popular `FechaApertura` | ✅ | `7f322de9` |
| 15 | C.3 — Bind `Edit.cshtml.cs` + redirect a Details si `EsCerrada` | ✅ | `ccbf3c5a` |
| 16 | C.4 — Reemplazar literales por `RolesSgv.*` en `Index.cshtml.cs` | ✅ | `3a94c647` |
| 17 | C.5 — Tests de defensa por reflexión (3 tests nuevos) | ✅ | `f7adb1c9` |
| 18 | C.6 — Smoke build verde | ✅ | (sin commit — verificado, 47 tests Web.Vacantes pass) |

**Resultado**: workaround `ModelState.Remove("Input.EstadoVacanteId")` eliminado. Cada formulario valida exactamente sus campos.

### Cluster D — D-4 Concurrencia Cubrir

| # | Tarea | Estado | Commit |
|---|---|---|---|
| 19 | D.1 — Refactor catch en `CrearOcupacionCubriendoVacanteAsync` para mapear `IX_Ocupaciones_VacanteIdUnique` → `VacanteYaCubierta`. Extender `IConstraintViolationDetector` con `GetUniqueConstraintName`. | ✅ | `2aeb9b71` + `562bbfe7` |
| 20 | D.2 — Crear `VacantesCubrirConcurrencyTests.cs` con 2 `[MySqlFact]` (TOCTOU + atomicidad) | ✅ | `47e3b3bc` |

**Resultado**: cuando la BD rechaza la segunda cobertura concurrente con 1062 (ER_DUP_ENTRY), el servicio ahora mapea correctamente a `OcupacionErrorCodigo.VacanteYaCubierta` en lugar del genérico `DatosInvalidos`.

### Cluster F — Triviales (mergeadas en commits previos)

| # | Tarea | Estado | Mergeado en |
|---|---|---|---|
| 21 | F.1 — Literales → `RolesSgv.*` en `Index.CanMutate` | ✅ | `3a94c647` |
| 22 | F.2 — Guard `EsCerrada` en `Edit.OnGetAsync` | ✅ | `ccbf3c5a` |
| 23 | F.3 — Pre-popular `FechaApertura` con `DateTime.Today` en `Create.OnGetAsync` | ✅ | `7f322de9` |

---

## Desviaciones del diseño

### D-4.D.1 — Refactor del catch (hallazgo espontáneo)

El spec escenario 2 pedía que la segunda cobertura concurrente fallara con `OcupacionErrorCodigo.VacanteYaCubierta`, pero el catch original mapeaba TODO `DbUpdateException` a `DatosInvalidos`. **Esto NO estaba en el proposal original**.

Resolución implementada (alineada con el patrón ya existente en `VacanteServicioComandos` que distingue `ActivePuestoIdUnique`):
1. Extender `IConstraintViolationDetector` con `string? GetUniqueConstraintName(DbUpdateException)`.
2. Implementar `MySqlConstraintViolationDetector.GetUniqueConstraintName` con regex que extrae el nombre del índice del mensaje `Duplicate entry 'X' for key 'tabla.IX_Name'` (cubre MySQL 8 backticks y MariaDB comillas).
3. Refactor del catch en `CrearOcupacionCubriendoVacanteAsync` para discriminar `IX_Ocupaciones_VacanteIdUnique` → `VacanteYaCubierta`; resto → `DatosInvalidos` (default vigente).

Costo: +30 LoC en detector + impl, +1 test unitario + 2 tests de integración `[MySqlFact]`. Backward-compatible: servicios que no usan el nuevo método siguen funcionando.

---

## Métricas finales (greps del proposal)

| Métrica | Esperado | Actual |
|---|---|---|
| `MotivoObligatorio` en src + tests | 0 | 0 ✅ |
| `ActualizarObservacionesAsync` en src + tests | 0 | 0 ✅ |
| `IsInRole("Administrador")` en SGV.Web | 0 | 0 ✅ |
| `ModelState.Remove` en Vacantes (Create) | 0 | 0 (1 mención en comentario) ⚠️ |
| `usuarioId: null` en SGV.Aplicacion (commands) | 0 | 0 ✅ |

Nota: la única ocurrencia residual de "ModelState.Remove" es un comentario en `Create.cshtml.cs:118` que documenta el workaround que **fue eliminado**. Si se requiere 0 menciones del string, eliminar ese comentario.

---

## Build & Test

- **`dotnet build SGV.slnx`**: 0 errors, 95 warnings preexistentes (sin nuevos introducidos).
- **`dotnet test SGV.slnx`**: 3291 passed / 312 skipped (MySQL no disponible) / **7 fallos preexistentes confirmados ajenos al cambio** (verificado vía `git stash` en baseline).
  - `PuestoRepositoryListarDisponiblesTests` (4) — `[MySqlFact]` requieren MySQL local.
  - `SetupServicioTests` (3) — `JwtRealWebApplicationFactory.InitializeAsync` requiere MySQL local.
- **Web tests** (`Web.Vacantes`): 47 passed / 0 failed.

---

## Próximos pasos

- ✅ Listo para `sdd-verify`.
- PR único confirmado por forecast (no chained, no size exception). 17 commits, todos convencionales, sin `Co-Authored-By`.
- Los `[MySqlFact]` de Cubrir **NO** se ejecutaron contra MySQL real en este ambiente (skip limpio por `MySqlFactAttribute`). Verify debe correrlos contra CI con MySQL.

---

## Lecciones / descubrimientos para futuras sesiones

1. **MySQL constraint name extraction**: el mensaje de MySQL 8 viene con backticks (`for key 'tabla.IX_Name'`); MariaDB usa comillas. Un regex con dos grupos nombrados (`n1` para `'...'`, `n2` para `` `...` ``) cubre ambos sin asumir motor.
2. **Longest constructor match**: cuando un servicio tiene un constructor "primario" con todas las deps y un "convenience" sin algunas (back-compat), ASP.NET Core resuelve automáticamente el primario (más parámetros). El cambio D-1 añadió `IUsuarioActual` como último parámetro del primario sin necesidad de tocar la DI registration.
3. **`[MySqlFact]` skip behavior**: los tests con este atributo NO fallan cuando MySQL no está — emiten un skip limpio. La orquestación debe distinguirlos de fallos reales para no contaminar métricas de regresión.
4. **Reflection-based defense tests**: para invariants estructurales (e.g. "CreateInputModel NO expone EstadoVacanteId"), los tests de reflexión son la única forma de proteger contra drift futuro sin atar el código a una aserción trivial.
5. **Convenience constructor + NullUsuarioActual**: el patrón `NullUsuarioActual.Instance` permite inyectar un "no-op" sin romper back-compat de tests pre-existentes que no cablean principal.
