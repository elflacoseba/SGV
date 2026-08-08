```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:2893c966f185fa956ec51394262aacf5c466bb44223595135feea75c340d2e2c
verdict: fail
blockers: 6
critical_findings: 6
requirements: 1/10
scenarios: 13/29
test_command: dotnet test SGV.slnx --no-build
test_exit_code: 1
test_output_hash: sha256:6cf03dc9c3947a570b40c883b762a8ea40ed632f93a0c7d0aab8864dd13ca2e4
build_command: dotnet build SGV.slnx
build_exit_code: 0
build_output_hash: sha256:6f6765531de20f3a98c4813f515edf644e9cb7d7d5498841c70ba8a1f641cc89
```

# Verify Report: vacante-ocupacion-flow-alignment

## Resumen ejecutivo

La solución compila correctamente y los 167 tests focalizados de dominio, mappers, servicios y controllers afectados pasan. También pasó `bun run build`. La migración EF fue validada contra MySQL 9.6 en una base temporal limpia y en una base con una `Ocupacion` preexistente: preservó `VacanteId = NULL`, los dos índices únicos activos, creó el índice no único y la FK `ON DELETE RESTRICT`; una segunda ejecución no aplicó migraciones.

El change NO está listo para merge ni archive. La suite completa finalizó con 10 fallos (`3433 passed / 10 failed / 0 skipped`), lo cual es bloqueante por configuración OpenSpec. Además, la implementación web contradice escenarios obligatorios: `PuestoOcupaciones` ofrece siempre “Nueva ocupación”, incluso sin Vacante abierta o con Ocupación activa; el conflicto `PuestoSinVacanteAbierta` se muestra como error general y no junto al selector; y no hay tests Razor nuevos que cubran los deltas. La evidencia de atomicidad tampoco prueba rollback real y Q1 carece de test de comportamiento.

## Completeness

| Métrica | Valor |
|---|---:|
| Tasks declaradas por el documento | 21 |
| Marcadores de task encontrados | 25 |
| Marcadores `[x]` | 25 |
| Marcadores `[ ]` | 0 |
| Tasks marcadas completas pero sin evidencia requerida | T-1.6, T-2.4 parcial, T-4.4 parcial, T-5.3 parcial, T-6.1, T-6.2, T-6.3 |

La ausencia de checks pendientes no acredita completitud: T-1.6 exige tres `[MySqlFact]` que no existen (`tasks.md:156-169`) y WU-6 exige tests Razor que tampoco existen (`tasks.md:585-587,622-626,647-649`).

## Build & Test

| Métrica | Valor |
|---|---|
| Build status | succeeded |
| Build exit code | 0 |
| Build warnings | 4 × NU1510 preexistentes |
| Tests passed | 3433 |
| Tests failed | 10 |
| Tests skipped | 0 |
| MySqlFact skipped | 0 — MySQL local estuvo disponible |
| Tests focalizados | 167 passed / 0 failed / 0 skipped |
| Frontend bundle | `bun run build` succeeded |
| Diff whitespace | `git diff --check` succeeded |

### Evidencia de comandos

- `dotnet build SGV.slnx` → exit `0`; hash `sha256:6f6765531de20f3a98c4813f515edf644e9cb7d7d5498841c70ba8a1f641cc89`.
- `dotnet test SGV.slnx --no-build` → exit `1`; hash `sha256:6cf03dc9c3947a570b40c883b762a8ea40ed632f93a0c7d0aab8864dd13ca2e4`.
- Test focalizado de los seis archivos solicitados → `167 passed`; hash `sha256:ae6974405709eac71fa05a8fe513a96f9fdad2967e35da3f304c0667cbd90011`.
- Cobertura focalizada → `167 passed`; hash `sha256:df555653932eae30282536748b6e06578ef2b5cf0ee4f0a11d6a9b38aac337c9`.

### Fallos de la suite completa

| Test | Resultado | Relación con el diff |
|---|---|---|
| `JwtRealAuthTests.TokenEmitido_ConClaveConfigurada_AccedeEndpointProtegido_200` | 401 en lugar de 200 | Archivo no modificado |
| `SoftDeletedUserLoginTests.Login_WithLockedOutUserByEmail_Returns401AndDoesNotIssueToken` | FK `AspNetUsers.PersonaId` | Archivo no modificado |
| `BloquearDesbloquearEliminarGatewayTests.BloquearAsync_AlreadyBlockedUser_IsIdempotent` | FK `AspNetUsers.PersonaId` | Archivo no modificado |
| `SoftDeletedUserLoginTests.Login_WithLockedOutUser_Returns401AndDoesNotIssueToken` | FK `AspNetUsers.PersonaId` | Archivo no modificado |
| `SetupServicioTests.CrearAdminAsync_PasswordCorta_DevuelvePasswordDebil` | `SetupYaCompletado` | Archivo no modificado |
| `BloquearDesbloquearEliminarGatewayTests.QueryAsync_ByActivas_ExcludesBlockedUsers` | colección vacía | Archivo no modificado |
| `SoftDeletedUserLoginTests.Login_AfterFiveFailedAttempts_EvenCorrectPasswordReturns401` | valor nulo | Archivo no modificado |
| `SetupServicioTests.CrearAdminAsync_DBVacia_RegistraAuditoriaConUsuarioOperadorSystem` | usuario `admin` duplicado | Archivo no modificado |
| `SoftDeletedUserLoginTests.Login_WithUnlockedUser_AfterPreviousLockout_Returns200AndIssuesToken` | 401 en lugar de 200 | Archivo no modificado |
| `AuditoriasDetailsTests.Get_Details_WhenRecordExists_RendersPreformattedJsonAndHeader` | no contiene “Después” | Archivo no modificado |

El fallo de `AuditoriasDetailsTests` es ortogonal y su archivo está intacto, consistente con el apply-progress. Sin ejecutar el commit base en un worktree separado no puede probarse históricamente que ya fallaba antes; sí puede afirmarse que el diff actual no toca ese test ni el módulo Auditorías. Los otros nueve fallos aparecieron con MySQL local activo y también ocurren en archivos no modificados. Aun así, el gate exige suite verde y no permite ignorarlos.

## Acceptance Criteria

| # | Criterio | Status | Evidencia |
|---|---|---|---|
| N1 | Rechazo por Ocupación activa | ✅ PASS | Check antes de Vacante abierta en `VacanteServicioComandos.cs:154-177`; unit test `VacanteServicioComandosTests.cs:258-277`; mapping HTTP `VacantesControllerTests.cs:300-326`; tests focalizados verdes. |
| N3 | Rechazo sin Vacante abierta | ✅ PASS | Orden correcto en `OcupacionServicioComandos.cs:153-178`; unit tests `OcupacionServicioComandosTests.cs:73-105`; mapping HTTP `OcupacionesControllerTests.cs:515-541`; tests focalizados verdes. |
| N2 | Cubrir crea Ocupación | ⚠️ PARTIAL | Implementación crea la derivada con `VacanteId`, Puesto, Persona, `Permanente` y una sola llamada a SaveChanges (`VacanteServicioComandos.cs:288-354`); unit test verifica Add y claves (`VacanteServicioComandosTests.cs:316-340`). El test API usa un servicio fake (`VacantesControllerTests.cs:508-524`) y no comprueba persistencia real ni `EsVigente`; el test de atomicidad no prueba rollback. |
| Q2 | Reactivación rechaza Vacante Cancelada | ✅ PASS | Check exclusivo de Reactivar (`OcupacionServicioComandos.cs:394-416`); test Cancelada real mediante helper (`OcupacionServicioComandosTests.cs:633-672`); mapping HTTP (`OcupacionesControllerTests.cs:485-511`). |
| Q1 | Finalizar no reabre | ❌ FAIL / UNTESTED | `FinalizarAsync` no llama a Vacante (`OcupacionServicioComandos.cs:280-329`), pero no existe test que parta de Vacante Cubierta + Ocupación derivada y compruebe que la Vacante permanece Cubierta. |
| Migración idempotente | ✅ PASS | MySQL temporal: migración desde esquema previo con Ocupación preexistente, `VacanteId IS NULL`; segunda ejecución informó “No migrations were applied”. `AddVacanteIdToOcupaciones.cs:12-49`. |
| Constraint único preservado | ✅ PASS | MySQL confirmó `IX_Ocupaciones_ActivePuestoIdUnique` y `IX_Ocupaciones_ActivePersonaPuestoUnique` como únicos, y `IX_Ocupaciones_VacanteId` como no único. Configuración: `OcupacionConfiguracion.cs`; migración: `AddVacanteIdToOcupaciones.cs:21-32`. |
| Tests adaptados pasan | ❌ FAIL | El test adaptado pasa (`OcupacionServicioComandosTests.cs:50-69`) y el subset da 167/167, pero la suite completa termina con 10 fallos. |

**Resumen de acceptance criteria**: 5 PASS, 1 PARTIAL, 2 FAIL.

## Specs Delta

Se contaron 10 requisitos y 29 escenarios formales en las tres specs delta. Solo se consideran `COMPLIANT` los escenarios con un test de cobertura que pasó en runtime.

### Matriz de compliance

| Spec / requisito | Escenario | Evidencia de test | Resultado |
|---|---|---|---|
| vacante-management / Crear Vacante | Puesto con Ocupación activa | `Crear_PuestoConOcupacionActiva_DevuelveConflictoPuestoOcupado`; API mapping | ✅ COMPLIANT |
| vacante-management / Crear Vacante | Creación exitosa | tests de servicio/API existentes | ✅ COMPLIANT |
| vacante-management / Cambiar estado | Cubierta crea Ocupación | unit prueba Add; API usa fake y no prueba persistencia | ⚠️ PARTIAL |
| vacante-management / Cambiar estado | Cubrir sin PersonaId | unit + API mapping | ✅ COMPLIANT |
| vacante-management / Cambiar estado | Atomicidad extendida | `CambiarEstado_Atomicidad_DbUpdateException_Rollback` no verifica rollback; la entidad fake queda mutada | ❌ UNTESTED |
| vacante-management / Cambiar estado | Estado no terminal | `CambiarEstado_A_NoTerminal_FlujoInalterado` | ✅ COMPLIANT |
| vacante-management / Unicidad | Cubrir no libera posición | No existe test secuencial Cubrir → nueva Vacante | ❌ UNTESTED |
| vacante-management / Unicidad | Finalizar derivada libera posición | No existe test secuencial Finalizar → nueva Vacante | ❌ UNTESTED |
| vacante-management / Códigos | Discriminación de 409 | tests N1 comparan código específico | ✅ COMPLIANT |
| web-ocupaciones-crear-editar / FORM-001 | Alta válida con Vacante abierta | servicio cubierto; sin test Razor del formulario | ⚠️ PARTIAL |
| web-ocupaciones-crear-editar / FORM-001 | Puesto sin Vacante abierta | Se agrega error general, no junto a `PuestoId` (`OcupacionFormPageModel.cs:197-212`) | ❌ FAILING |
| web-ocupaciones-crear-editar / FORM-001 | Catálogo no disponible | cobertura web preexistente, sin fallo en suite | ✅ COMPLIANT |
| web-ocupaciones-crear-editar / FORM-001 | Usuario no-admin | autorización y cobertura web preexistente | ✅ COMPLIANT |
| web-ocupaciones-crear-editar / FORM-008 | Reactivación válida | tests de servicio/web preexistentes | ✅ COMPLIANT |
| web-ocupaciones-crear-editar / FORM-008 | Colisión del par | tests preexistentes | ✅ COMPLIANT |
| web-ocupaciones-crear-editar / FORM-008 | Colisión del Puesto | tests preexistentes | ✅ COMPLIANT |
| web-ocupaciones-crear-editar / FORM-008 | Vacante Cancelada | servicio/API cubiertos; no hay test Razor de Details ni feedback histórico | ⚠️ PARTIAL |
| web-ocupaciones-crear-editar / FORM-005 | Puesto sin Vacante abierta visible | Código no mapea `PuestoSinVacanteAbierta` al selector (`OcupacionFormPageModel.cs:199-211`) | ❌ FAILING |
| web-ocupaciones-crear-editar / FORM-005 | Sin falso éxito | Sin test del tercer código; re-render no recalcula `PuestoSinVacanteAbierta` | ⚠️ PARTIAL |
| web-ocupaciones-crear-editar / FORM-009 | Hints en Create | Hint solo aparece si ya hay Puesto seleccionado (`_Form.cshtml:73-100`), no al abrir Create sin selección | ❌ FAILING |
| web-ocupaciones-crear-editar / FORM-009 | Create deriva al flujo automatizado | Tras 409 solo re-renderiza; no deriva al flujo Vacante → Cubierta (`Create.cshtml.cs:167-174`) | ❌ FAILING |
| web-ocupaciones-navegacion-contextual / NAV-006 | Puesto con Vacante abierta | Implementación estática compatible, pero no existe test con estado configurado | ❌ UNTESTED |
| web-ocupaciones-navegacion-contextual / NAV-006 | Puesto sin Vacante abierta | `NewOcupacionRouteValues` siempre es no nulo (`PuestoOcupaciones.cshtml.cs:142-145`), por lo que muestra también “Nueva ocupación” | ❌ FAILING |
| web-ocupaciones-navegacion-contextual / NAV-006 | Puesto con Ocupación activa | No existe “Ver Ocupación vigente”; sigue mostrando “Nueva ocupación” y puede mostrar “Abrir Vacante” | ❌ FAILING |
| web-ocupaciones-navegacion-contextual / NAV-006 | Alta desde Persona | tests preexistentes, defaults seguros | ✅ COMPLIANT |
| web-ocupaciones-navegacion-contextual / NAV-006 | Usuario no-admin | test web preexistente | ✅ COMPLIANT |
| web-ocupaciones-navegacion-contextual / NAV-007 | Abrir Vacante sin vacante | URL/precarga implementadas, pero coexiste CTA incorrecto y no hay test Razor | ⚠️ PARTIAL |
| web-ocupaciones-navegacion-contextual / NAV-007 | Oculto si ya existe | Condición estática correcta; no existe test con nueva dependencia configurada | ❌ UNTESTED |
| web-ocupaciones-navegacion-contextual / NAV-007 | No-admin | gating por rol + test web preexistente | ✅ COMPLIANT |

**Compliance summary**: 13/29 escenarios compliant; 5 partial; 6 failing; 5 untested.

### vacante-management

⚠️ **PARTIAL** — N1, N3-related availability checks, validación de PersonaId y persistencia estructural están implementados. Faltan pruebas válidas de atomicidad, Q1/N4 y los flujos secuenciales que demuestran que la posición se bloquea/libera en el momento correcto.

### web-ocupaciones-crear-editar

❌ **FAIL** — `PuestoSinVacanteAbierta` no aparece junto al selector y el re-render no mantiene el estado visual correcto. El hint tampoco se muestra al abrir el formulario sin Puesto preseleccionado. No hay tests Razor nuevos.

### web-ocupaciones-navegacion-contextual

❌ **FAIL** — la bifurcación exigida no está implementada: “Nueva ocupación” siempre está disponible para admin porque `NewOcupacionRouteValues` nunca es nulo. Tampoco existe la derivación “Ver Ocupación vigente”.

## Verificación de migración

| Check | Resultado | Evidencia |
|---|---|---|
| Compila | ✅ | Build completo succeeded |
| `Up()` nullable | ✅ | `AddVacanteIdToOcupaciones.cs:14-19` |
| Índice no único | ✅ | `AddVacanteIdToOcupaciones.cs:21-24`; MySQL `NON_UNIQUE=1` |
| FK `ON DELETE RESTRICT` | ✅ | `AddVacanteIdToOcupaciones.cs:26-32`; MySQL `DELETE_RULE=RESTRICT` |
| `Down()` correcto | ✅ | drop FK → índice → columna, `AddVacanteIdToOcupaciones.cs:36-48` |
| DB limpia | ✅ | Base temporal creada desde cero y migrada hasta latest |
| DB con Ocupaciones preexistentes | ✅ | Fila insertada antes de la nueva migración conservó `VacanteId = NULL` |
| Segunda ejecución | ✅ | “No migrations were applied. The database is already up to date.” |
| Índices únicos previos | ✅ | Ambos índices activos continuaron únicos |
| SQL standalone regenerado | ⚠️ | Incluye la nueva migración (`docs/migracion-inicial-sgv.sql:4255-4300`), pero ejecutar el script completo falla en SQL preexistente sin `;` (`docs/migracion-inicial-sgv.sql:2572-2574`). Esa línea no fue introducida por este diff. |

## TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD Evidence reported | ⚠️ | Hay resumen por WU, no tabla por task con columnas RED/GREEN/TRIANGULATE/SAFETY NET requerida por Strict TDD. |
| All tasks have tests | ❌ | Tasks de migración y Razor marcadas `[x]` sin los tests exigidos. |
| RED confirmed (tests exist) | ⚠️ | Existen tests de dominio/aplicación/API; faltan `[MySqlFact]` nuevos y tests Razor. |
| GREEN confirmed | ❌ | 167 tests focalizados pasan, pero la suite completa falla. |
| Triangulation adequate | ❌ | Q1/N4, atomicidad real, Cubierta real en Q2 y navegación contextual no están triangulados. |
| Safety Net for modified files | ⚠️ | Apply-progress afirma safety net por WU, pero no aporta la tabla verificable por task. |

**TDD Compliance**: 0/6 checks plenamente satisfechos.

## Test Layer Distribution

| Layer | Evidencia del change | Herramienta | Resultado |
|---|---:|---|---|
| Unit | Dominio, mappers y servicios | xUnit | 167 focalizados verdes junto con API tests |
| Integration HTTP | Controllers con `WebApplicationFactory`, mayormente servicios fake | xUnit + WebApplicationFactory | verdes en subset |
| Integration MySQL nueva | 0 tests nuevos | `MySqlFact` | validación manual exitosa, pero task automatizada ausente |
| Razor/Web nueva | 0 tests nuevos | `SgvWebApplicationFactory` | escenarios delta sin prueba |
| E2E | 0 | no disponible | N/A |

## Changed File Coverage

La cobertura focalizada fue recolectada correctamente. Los paths críticos de aplicación tienen cobertura útil (`VacanteServicioComandos.CrearAsync` 95.58%; `CambiarEstadoAsync` 83.83%; `OcupacionServicioComandos.CrearAsync` 84.48%; `ReactivarAsync` 84.31%). `Ocupacion` y `OcupacionConfiguracion` reportan 100% en el subset.

Los PageModels web modificados reportan 0% en el subset: `Ocupaciones/Create.cshtml.cs`, `Vacantes/Create.cshtml.cs` y `PuestoOcupaciones.cshtml.cs`. Esto confirma la ausencia de cobertura para WU-6. Los mappers completos muestran porcentajes agregados bajos porque el subset solo ejercita `Ocupacion`, no todos los overloads del archivo.

## Assertion Quality

| Archivo | Línea | Assertion / setup | Issue | Severidad |
|---|---:|---|---|---|
| `VacanteServicioComandosTests.cs` | 415-434 | `CambiarEstado_Atomicidad_DbUpdateException_Rollback` | El fake muta la misma instancia antes de lanzar y el test no comprueba rollback de Vacante ni historial; el nombre promete más de lo demostrado. | CRITICAL |
| `OcupacionServicioComandosTests.cs` | 675-718 | `ReactivarAsync_VacanteCubierta_Exito` | El setup devuelve `null` (FK rota), no una Vacante Cubierta; no cubre el escenario nombrado. | CRITICAL |
| `VacanteServicioComandosTests.cs` | 296-310 | `Crear_PuestoConOcupacionEliminada_NoBloquea` | No crea Ocupación eliminada; usa repo vacío. Prueba ausencia, no el edge case descrito. | WARNING |
| `VacantesControllerTests.cs` / `OcupacionesControllerTests.cs` | 300-524 / 485-541 | tests API nuevos | Sustituyen el servicio por fake; prueban mapping HTTP, no integración del comportamiento de aplicación. | WARNING |

**Assertion quality**: 2 CRITICAL, 2 WARNING.

## Quality Metrics

- **Linter**: no disponible.
- **Type checker separado**: no disponible; compilación C# exitosa.
- **Formatter**: no disponible.
- **`git diff --check`**: sin errores.
- **Frontend**: `bun run build` exitoso con warnings de metadata Browserslist desactualizada.

## Desviaciones del Design

1. **`TipoAsignacion.Permanente` en lugar de `Titular`** — ✅ aceptable. `Titular` no existe y `Permanente` es el valor histórico equivalente (`VacanteServicioComandos.cs:334-341`). El propio `design.md:118-122` ya muestra `Permanente`; conviene corregir la palabra residual `Titular` en `tasks.md:410,424` durante la próxima fase documental, sin cambiar producción.
2. **`WithEstadoVacante` por reflection** — ⚠️ aceptable solo como helper de tests, pero no soluciona la mala cobertura del caso Cubierta: ese test usa FK rota. Alternativa más limpia: builder de test que use `Reconstitute`/mapper interno o un fake de repositorio que devuelva una Vacante reconstituida con navegación real.
3. **Sin tests Razor nuevos** — ❌ no aceptable bajo Strict TDD y las specs delta. La lógica de servicio no cubre render condicional, ModelState, preservación de inputs, CTA, `returnUrl` ni roles en Razor. La pérdida de cobertura ya dejó pasar fallos funcionales concretos.

## Regresiones

- No se atribuyó de forma concluyente ninguno de los 10 fallos de suite al diff: sus archivos y módulos directos no fueron modificados.
- El fallo de Auditorías está confirmado en el estado actual y es ortogonal, pero “preexistente” solo queda sustentado por archivo intacto + apply-progress; no por ejecución del commit base.
- Sí hay regresiones funcionales respecto de las specs delta web: CTAs simultáneos/incorrectos, ausencia de “Ver Ocupación vigente”, error N3 fuera del selector y hint incompleto.
- El script SQL standalone sigue siendo inválido en una sección preexistente; la regeneración no corrigió esa deuda.

## Hallazgos

### CRITICAL (bloqueantes)

1. **Suite completa roja**: 10 fallos, exit code 1. OpenSpec exige todos los tests verdes antes de archive (`openspec/config.yaml:49-51`).
2. **NAV-006/NAV-007 incumplidos**: `NewOcupacionRouteValues` siempre habilita “Nueva ocupación” (`PuestoOcupaciones.cshtml.cs:142-145`) y no existe “Ver Ocupación vigente”.
3. **FORM-001/FORM-005 incumplidos**: `PuestoSinVacanteAbierta` cae al error general (`OcupacionFormPageModel.cs:197-212`) en lugar de `Input.PuestoId`.
4. **FORM-009 incompleto**: el hint solo aparece con un Puesto seleccionado (`_Form.cshtml:73-100`) y no hay derivación efectiva tras 409.
5. **Strict TDD incompleto**: faltan los tests `[MySqlFact]` y Razor exigidos por tasks; varias tasks están marcadas completas sin sus criterios de prueba.
6. **Escenarios críticos sin prueba válida**: atomicidad N2, Q1, N4, Vacante Cubierta real en Q2 y navegación contextual. Dos tests tienen setup que no ejercita el escenario anunciado.

### WARNING (deben resolverse antes de merge)

1. `tasks.md` declara 21 tasks, pero contiene 25 marcadores `T-*` marcados `[x]`; corregir el conteo y la trazabilidad.
2. `docs/migracion-inicial-sgv.sql` fue regenerado e incluye la migración, pero el script completo falla en una migración anterior por `UPDATE` sin `;` (`:2572-2574`).
3. La comparación por nombres `"Cubierta"`/`"Cancelada"` es una decisión aprobada, pero sigue siendo frágil ante cambios de seed; mantener tests de invariantes de catálogo.

### SUGGESTION (nice to have)

1. Reemplazar reflection en `WithEstadoVacante` por un builder/reconstitución tipada para evitar tests que accidentalmente prueben FK rota.
2. Corregir en `tasks.md` la referencia residual a `TipoAsignacion.Titular`; el diseño e implementación usan correctamente `Permanente`.

## Recomendación

**Status**: ❌ BLOCKED

### Fixes requeridos antes de merge

1. Implementar la bifurcación completa de `PuestoOcupaciones`: Vacante abierta → “Nueva ocupación”; Ocupación activa → “Ver Ocupación vigente”; ninguna → solo “Abrir Vacante” + mensaje contextual.
2. Mapear `PuestoSinVacanteAbierta` a `Input.PuestoId`, conservar inputs y recalcular el estado/hint al re-render.
3. Agregar tests Razor para FORM-009, NAV-006, NAV-007, precarga/lock de Vacantes Create, roles y conflicto N3.
4. Agregar tests `[MySqlFact]` automatizados para persistencia/FK/migración y un test real de atomicidad N2.
5. Agregar tests Q1/N4 secuenciales y corregir el test “VacanteCubierta” para que use una Vacante Cubierta real.
6. Dejar `dotnet test SGV.slnx --no-build` completamente verde; investigar/aislar los 10 fallos actuales sin excluirlos del gate.
7. Corregir el conteo de tasks y no marcar tasks de testing como completas sin sus tests.

### Verdict

**FAIL** — la base de dominio/aplicación y la migración son prometedoras, pero hay incumplimientos funcionales web, evidencia TDD insuficiente y una suite completa roja.
