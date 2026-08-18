# Archive Report: Vacantes Hardening

**Change**: `vacantes-hardening`
**Fecha de archive**: 2026-08-18
**Artifact store**: hybrid (OpenSpec filesystem + Engram)
**Modo**: Strict TDD activo (`openspec/config.yaml`)

---

## Resumen

El change `vacantes-hardening` endurezió el módulo de Vacantes eliminando ocho hallazgos identificados durante revisión: tres críticos (trazabilidad de usuario nula en transiciones de estado, superficie huérfana de `ActualizarObservacionesAsync`, y código muerto de `MotivoObligatorio`) y cinco recomendaciones (split de input models, tests de concurrencia, guard en Edit, pre-poblamiento de `FechaApertura`, y literales de rol en `Index`). La implementación respetó Strict TDD — ciclo RED→GREEN verificable en git history — y entregó 18 commits convencionales sin `Co-Authored-By`, sin amend, sin merge intrusivos.

El build quedó limpio (0 errores, 4 warnings NU1510 pre-existentes), la suite pasó 3291 tests con 319 skips por MySQL no disponible y 7 fallos pre-existentes ajenos al change. Los 2 `[MySqlFact]` de D-4 no se ejecutaron contra MySQL real en este ambiente (skip limpio), pero están marcados y deben correr en CI.

Lo que funcionó bien: la inyección de `IUsuarioActual` ya existente resolvió D-1 sin crear abstracción nueva; el patrón de `IConstraintViolationDetector` ya tenía un caso similar (`ActivePuestoIdUnique`) que guió la extensión para D-4.D.1; la convención de split de input models en `SGV.Web/Integration/` se aplicó consistentemente.

Lo que aprendimos: MySQL 8 y MariaDB emiten formatos distintos para el nombre de constraint en mensajes de error (backticks vs. comillas), lo que requirió un regex con dos grupos nombrados; el skip de `[MySqlFact]` es limpio cuando MySQL no está disponible, sin contaminar métricas de regresión; el `NullUsuarioActual.Instance` permitió mantener backward-compat de tests pre-existentes sin modificar su wiring.

---

## Cambios aplicados (clusters)

### Cluster A — D-1: Identidad de usuario en transiciones

Inyección de `IUsuarioActual` en `VacanteServicioComandos` y `OcupacionServicioComandos`. Los constructores primaires ahora reciben `IUsuarioActual` como último parámetro y resuelven `usuarioActual.UserId` en los call sites de `CambiarEstado`. Convenience constructor con `NullUsuarioActual.Instance` preserva back-compat de tests pre-existentes. Guard contra principal no autenticado retorna `ErrorCategoria.Unauthorized`, mapeado a `401` por el controller.

### Cluster B — D-2: Eliminación de `ActualizarObservacionesAsync`

Método, signatura de interfaz, y override de fake borrados. Cuatro tests orphan eliminados. Grep audit confirmó 0 referencias restantes en src y tests. El side-effect de actualizar observaciones vive ahora exclusivamente como subproducto de `CambiarEstadoAsync`.

### Cluster C — D-3: Split de `VacanteInputModel`

`VacanteCreateInputModel` (sin `EstadoVacanteId`) y `VacanteEditInputModel` (`EstadoVacanteId Guid?` con `[Required]`) reemplazaron al tipo único. El workaround `ModelState.Remove("Input.EstadoVacanteId")` en `Create.cshtml.cs` fue eliminado. Tres tests de reflexión defienden la estructura de cada tipo contra drift futuro.

### Cluster D — D-4: Concurrencia Cubrir

Extensión de `IConstraintViolationDetector` con `GetUniqueConstraintName(DbUpdateException)` para extraer el nombre del índice del mensaje MySQL/MariaDB. Catch en `CrearOcupacionCubriendoVacanteAsync` discrimina `IX_Ocupaciones_VacanteIdUnique` → `OcupacionErrorCodigo.VacanteYaCubierta` (409), resto → `DatosInvalidos`. Dos `[MySqlFact]` exercising TOCTOU y atomicidad fueron creados pero skippearon localmente.

**Desviación documentada (D-4.D.1)**: El spec pedía que la segunda cobertura concurrente fallara con `VacanteErrorCodigo.EstadoTerminalInmutable` (409). La implementación mapea desde la constraint única a `OcupacionErrorCodigo.VacanteYaCubierta` (también 409, código distinto). Comportamiento funcional equivalente — uno gana, el otro pierde con 409.

### Cluster E — D-5: Dead code `MotivoObligatorio`

Constante eliminada de `VacanteErrorCodigo.cs`. Grep audit confirmó 0 referencias en src y tests.

### Cluster F — Triviales (F.1, F.2, F.3)

F.1: `Index.CanMutate` reemplaza literales `"Administrador"` por `RolesSgv.Administrador` y `RolesSgv.GestorVacantes`. F.2: `Edit.OnGetAsync` redirige a Details cuando `EsCerrada = true`. F.3: `Create.OnGetAsync` pre-popula `FechaApertura = DateTime.Today`.

---

## Métricas finales

| Métrica | Valor |
|---|---|
| Tareas ejecutadas | 23 |
| Commits convencionales | 18 |
| Tests agregados | 8 |
| Tests modificados | 2 |
| Tests eliminados | 4 |
| Build | ✅ clean (0 errores) |
| Tests pass | 3291 |
| Tests skip (MySQL) | 319 |
| Tests fail (pre-existentes) | 7 |
| Grep audit — `MotivoObligatorio` en src | 0 |
| Grep audit — `ActualizarObservacionesAsync` en src | 0 |
| Grep audit — `IsInRole("Administrador")` en Web | 0 |
| Grep audit — `ModelState.Remove` en Create (invocaciones reales) | 0 (1 en comentario) |
| Grep audit — `usuarioId: null` en Aplicacion | 0 |
| Net LoC delta | −60 (per design) |

---

## Veredicto de verify

**Status**: `PASS WITH WARNINGS`

| Severidad | Conteo |
|---|---|
| CRITICAL | 0 |
| WARNING | 3 |
| SUGGESTION | 2 |

---

## Warnings y SUGGESTIONS transferidas

### WARNING 1 — `[MySqlFact]` D-4 no ejercitados contra MySQL real

Los tests `CubrirVacante_Concurrencia_TOCTOU_SoloUnaCoberturaExitosa` y `CubrirVacante_Concurrencia_DobleCobertura_ConstraintUnica` se skipean limpio en este ambiente (`localhost:3306` no disponible). Unit test `CrearAsync_Cubrir_ViolacionConstraintUnica_MapeaVacanteYaCubierta` cubre el path lógico.

**Resolución**: los tests DEBEN ejecutarse contra MySQL real en CI (GitHub Actions levanta `mysql:8.0`). Si fallan, la defensa atómica de BD debe verificarse. Seguimiento: `tests/SGV.Tests/Api/Vacantes/VacantesCubrirConcurrencyTests.cs`.

### WARNING 2 — Desviación D-4 escenario 2: error code diferente al spec

Spec pedía `VacanteErrorCodigo.EstadoTerminalInmutable (409)`. Implementación mapea `IX_Ocupaciones_VacanteIdUnique` → `OcupacionErrorCodigo.VacanteYaCubierta (409)` — código distinto, comportamiento funcional equivalente.

**Resolución**: documentada en `apply-progress.md §Desviaciones del diseño → D-4.D.1` y `design.md §D-4`. Seguimiento: decidir si se acepta el error code implementado o se ajusta el spec en un change menor.

### WARNING 3 — `apply-progress.md` sin tabla explícita "TDD Cycle Evidence"

Formato RED/GREEN/TRIANGULATE/SAFETY NET no presente. Formato alternativo por cluster con columna "Estado" ✅ y commit SHAs verificables en git history.

**Resolución**: documentada, deferred a follow-up change si se requiere alineación con template estricto.

### SUGGESTION 1 — Comentario residual en `Create.cshtml.cs:118`

Un comentario menciona el workaround `ModelState.Remove` que fue eliminado. Afecta solo legibilidad; funcionalidad intacta.

**Resolución**: cleanup opcional — eliminar el comentario `// Workaround: ModelState.Remove...` de `Create.cshtml.cs:118`. Deferred a follow-up.

### SUGGESTION 2 — 7 tests pre-existentes requieren MySQL local

`PuestoRepositoryListarDisponiblesTests` (4) y `SetupServicioTests` (3) fallan sin MySQL. No fueron introducidos por este change (último commit previo: `916c7772`).

**Resolución**: en CI estos tests deberían usar `mysql:8.0` service, o migrar a `[MySqlFact]` para skip limpio. Deferred a follow-up de infraestructura de tests.

---

## Decisiones arquitectónicas incorporadas

- **D-1**: `IUsuarioActual` ya existía en el codebase (issue #202). La decisión fue inyectarlo en los constructores primaires de los servicios de comandos, usando `NullUsuarioActual.Instance` para back-compat de tests pre-existentes. Composition root registrado como `AddScoped<IUsuarioActual, UsuarioActualHttpContext>()` en `Program.cs:219`.
- **D-2**: Eliminación completa de superficie huérfana sin endpoint HTTP ni cliente tipado. Sin presencia en API ni en Web, la remoción es limpia.
- **D-3**: Input models de Razor Pages viven en `src/SGV.Web/Integration/<Módulo>/`, NO en `SGV.Contracts`. `VacanteInputModel` spliteado en `VacanteCreateInputModel` (sin `EstadoVacanteId`) y `VacanteEditInputModel` (`EstadoVacanteId Guid?` con `[Required]`). Convención vigente confirmada.
- **D-4**: En `CrearOcupacionCubriendoVacanteAsync`, la defensa atómica de BD (`IX_Ocupaciones_VacanteIdUnique`) tiene precedencia sobre la defensa lógica de `EsTerminal`. El código de error es `OcupacionErrorCodigo.VacanteYaCubierta`, no `EstadoTerminalInmutable`. Patrón alineado con `ActivePuestoIdUnique` ya existente en `VacanteServicioComandos`.
- **D-5**: Constante `MotivoObligatorio` eliminada. El campo `Motivo` en `Cerrar` es y fue siempre opcional; la constante nunca fue referenciada.

---

## Lecciones aprendidas

1. **MySQL constraint name extraction**: MySQL 8 emite backticks (`` `tabla.IX_Name` ``) y MariaDB comillas simples (`'tabla.IX_Name'`) en el mensaje `Duplicate entry`. Un regex con dos grupos nombrados (`n1` para comillas simples, `n2` para backticks) cubre ambos motores sin asumir cuál está activo.

2. **Longest constructor match en DI**: cuando un servicio tiene un constructor primaire con todas las dependencias y un convenience constructor con menos parámetros, ASP.NET Core DI resuelve automáticamente el de más parámetros. Agregar `IUsuarioActual` como último parámetro del constructeur primaire no necesitó cambios en la registración de DI.

3. **`[MySqlFact]` skip behavior**: estos tests emetten un skip limpio (no failure) cuando MySQL no está disponible. La orquestación debe distinguir skip de failure para no contaminar métricas de regresión.

4. **Reflection-based defense tests**: para invariants estructurales (e.g. "CreateInputModel NO expone `EstadoVacanteId`"), los tests de reflexión son la única forma de proteger contra drift futuro sin una aserción trivial en código de producción.

5. **`NullUsuarioActual` como no-op**: el patrón `NullUsuarioActual.Instance` permite inyectar un principal "vacío" que devuelve `null`/`false` en `UserId`/`IsAuthenticated`, manteniendo backward-compat de tests pre-existentes sin modificar su wiring.

---

## Cambios pendientes para CI

⚠️ **Los 2 `[MySqlFact]` de D-4 DEBEN pasar en CI antes de merge.** GitHub Actions levanta `mysql:8.0` como service container. La connection string se resuelve desde `ConnectionStrings__SgvDatabase` en secrets de CI. Verificar que los tests `CubrirVacante_Concurrencia_TOCTOU_SoloUnaCoberturaExitosa` y `CubrirVacante_Concurrencia_DobleCobertura_ConstraintUnica` pasen contra MySQL 8 real antes de cerrar el PR.

---

## Cambios no incluidos / follow-ups

1. **SUGGESTION — Eliminar comentario residual en `Create.cshtml.cs:118`**: el comentario que documenta el workaround `ModelState.Remove` eliminado debería borrarse para cumplir literalmente el contrato grep "0 menciones del string". Cleanup menor, sin riesgo.

2. **SUGGESTION — Migrar `SetupServicioTests` (3 `[Fact]`) a `[MySqlFact]`**: estos tests requieren `JwtRealWebApplicationFactory.InitializeAsync` que necesita MySQL. Migrarlos evitaría los 3 fallos pre-existentes cuando MySQL no está disponible, o harían skip limpio como los demás `[MySqlFact]`.

3. **WARNING 2 (follow-up opcional)**: decidir si el spec de D-4 escenario 2 debe actualizarse para reflejar `OcupacionErrorCodigo.VacanteYaCubierta` en lugar del genérico `EstadoTerminalInmutable`, o si se ajusta el código para producir el código pedido.

---

## Referencias

- `openspec/changes/archive/2026-08-18-vacantes-hardening/exploration.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/proposal.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/design.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/tasks.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/apply-progress.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/verify-report.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/specs/vacante-identity-propagation/spec.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/specs/vacante-remove-actualizar-observaciones/spec.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/specs/vacante-input-model-split/spec.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/specs/vacante-cubrir-concurrency-test/spec.md`
- `openspec/changes/archive/2026-08-18-vacantes-hardening/specs/vacante-error-codigo-cleanup/spec.md`
- `openspec/specs/vacante-management/spec.md` (requisito "Trazabilidad de usuario" agregado)
- `openspec/specs/vacante-web/spec.md` (3 requisitos nuevos: pre-populate, Edit guard, Bind split)
- `docs/decisiones-implementacion.md` (D-1, D-3, D-4 documentadas)
