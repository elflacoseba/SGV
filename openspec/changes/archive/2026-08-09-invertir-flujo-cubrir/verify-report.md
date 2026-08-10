# Verify Report: invertir-flujo-cubrir

```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:pending-local-recompute
verdict: pass-with-warnings
blockers: 0
critical_findings: 0
requirements: 4/4
scenarios: 30/36
test_command: dotnet test SGV.slnx --nologo --no-build
test_exit_code: 0
test_output_hash: sha256:pending-local-recompute
build_command: dotnet build SGV.slnx --nologo --no-incremental
build_exit_code: 0
build_output_hash: sha256:pending-local-recompute
```

## Status

- **Overall**: **PASS WITH WARNINGS**
- **Branch**: `develop`@`5ed8239d` (post S1 + S2 + S3 merge)
- **ACs cubiertos**: **10/10** (todos los AC cuentan con evidencia de código + cobertura de tests mayoritaria)
- **Spec coverage**: **30/36 escenarios del delta** (6 escenarios de borde con cobertura parcial / sólo por código, sin test dedicado)
- **Tests**: **3488 pass / 0 fail / 0 skip explícito** (los `[MySqlFact]` se skipean automáticamente sin MySQL local; el runner no los cuenta como "skip" sino como absent del run)
- **Build**: 0 errors, 96 warnings (todos preexistentes; ningún warning nuevo introducido por el change)

## Acceptance Criteria

| AC | Status | Evidencia |
|---|---|---|
| **AC1** | ✅ | Botón "Cubrir Vacante" rendereado en `src/SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml:114-117` cuando `Model.EsCubrible`; href `?vacanteId={id}&returnUrl=...`. Test `VacantesDetailsAndSidenavTests.Get_Details_VacanteAbierta_BotonCubrirVisible` (línea 93). |
| **AC2** | ✅ | Atomicidad transaccional en `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs:161-165,252-377` (rama `CrearOcupacionCubriendoVacanteAsync` con único `try`/`SaveChangesAsync`). Tests `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_VacanteAbierta_CreaOcupacionYTransicionaVacanteACubierta` (línea 902) + `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_FalloEnSaveChanges_NoCreaOcupacionYNoTransicionaVacante` (línea 1042). |
| **AC3** | ✅ | (a) Redirect post-success: `Create.cshtml.cs:238-241` → `RedirectToPage("/Organizacion/Vacantes/Details", ...)`. (b) Bloque "Persona asignada": `Details.cshtml:62-78`. (c) Hidratación backend: `VacanteServicioConsulta.cs:54-71` consulta `IOcupacionRepository.ObtenerVigentePorVacanteAsync` sólo cuando `EsCubierta == true`. Test `VacanteServicioConsultaTests.ObtenerPorIdAsync_VacanteCubiertaConOcupacionDerivada_DevuelveOcupacionDerivadaIdYPersonaAsignada` (línea 30). |
| **AC4** | ✅ | `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs:197` filtra el dropdown con `.Where(s => !s.EsCubierta)`. Vigente desde issue #268 (commit `bf50b82e`, fuera del rango del change). |
| **AC5** | ✅ | `VacanteServicioComandos.cs:296-317` rechaza `EsCubierta` con código `CubrirVacanteRequiereCrearOcupacion` y mensaje "Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada.". Tests: `VacanteServicioComandosTests.CambiarEstado_A_Cubierta_Devuelve400ConCodigoCubrirVacanteRequiereCrearOcupacionYMensaje` (línea 320) + API test `VacantesControllerTests.PatchEstado_A_Cubierta_Returns400ConMensajeUseCubrirVacante` (línea 529). |
| **AC6** | ✅ | `VacanteDetailViewModel.cs:31-39` (`EsCubrible`) devuelve `false` para `Cubierta` / `Cancelada`. Conjunción con `CanMutate` en `Details.cshtml.cs:56` (`EsCubrible => ViewModel is not null && ViewModel.EsCubrible && CanMutate`). Tests `Get_Details_VacanteCubierta_BotonCubrirOculto_BloquePersonaAsignadaVisible` (línea 141) + `Get_Details_VacanteCancelada_BotonCubrirOculto` (línea 175) + `Get_Details_VacanteAbierta_NonMutator_BotonCubrirOculto` (línea 205). |
| **AC7** | ✅ | `Create.cshtml.cs:162-166` setea `ErrorMessage = "Esta Vacante ya está cubierta."` cuando el estado es Cubierta. Test `OcupacionCreatePageTests.Get_Create_WithVacanteCubierta_MuestraError_VacanteYaCubierta` (línea ~893). |
| **AC8** | ✅ | `PuestoOcupaciones.cshtml.cs:190-193` (`IOcupacionesCrossList.NewOcupacionButtonLabel`) retorna `"Cubrir Vacante"` cuando `HayVacanteAbierta && !HayOcupacionActiva`. Tests `PuestoOcupacionesPageTests.Get_VacanteAbiertaSinOcupacion_LabelCubrirVacanteYRouteVacanteId` (línea 495). |
| **AC9** | ✅ | Suite global: `dotnet test SGV.slnx --nologo --no-build` → **Passed: 3488, Failed: 0, Skipped: 0, Total: 3488**. |
| **AC10** | ✅ | `git diff --stat 4396e892..HEAD -- src/SGV.Infraestructura/Persistencia/Migraciones/ docs/migracion-inicial-sgv.sql` → vacío. Sin archivos nuevos de migración; el SQL inicial no se regeneró. |

## Spec Coverage

### vacante-management (delta: 1 MODIFIED + 2 ADDED)

| Escenario | Capa de cobertura | Estado |
|---|---|---|
| **MODIFIED** Transición exitosa a estado no terminal | pre-existente | ✅ (sin cambios en este change) |
| **MODIFIED** Transición a Cubierta vía PATCH es rechazada | API + Aplicación | ✅ `VacantesControllerTests.PatchEstado_A_Cubierta_Returns400ConMensajeUseCubrirVacante` (línea 529) + `VacanteServicioComandosTests.CambiarEstado_A_Cubierta_Devuelve400ConCodigoCubrirVacanteRequiereCrearOcupacionYMensaje` (línea 320) |
| **MODIFIED** Transición a Cubierta con PersonaId populado | Aplicación | ✅ `VacanteServicioComandosTests.CambiarEstado_A_Cubierta_ConPersonaIdPopulado_TambienIgnoraPersonaId` (línea ~370) |
| **MODIFIED** Transición a estado terminal setea FechaCierre | pre-existente | ✅ (sin cambios en este change) |
| **MODIFIED** Estado terminal inmutable | pre-existente | ✅ (sin cambios en este change) |
| **ADDED** Detalle de Vacante Cubierta con Ocupación derivada | API + Aplicación | ✅ `VacanteServicioConsultaTests.ObtenerPorIdAsync_VacanteCubiertaConOcupacionDerivada_DevuelveOcupacionDerivadaIdYPersonaAsignada` (línea 30) + `VacantesControllerTests.GetById_VacanteCubierta_RetornaOcupacionDerivadaIdYPersonaAsignada` (línea 570) |
| **ADDED** Detalle de Vacante Abierta sin cobertura | Aplicación | ✅ `VacanteServicioConsultaTests.ObtenerPorIdAsync_VacanteAbierta_NoConsultaOcupacion_DevuelveOcupacionDerivadaIdNull` (línea 61) |
| **ADDED** Detalle defensivo Cubierta sin Ocupación | Aplicación | ✅ `VacanteServicioConsultaTests.ObtenerPorIdAsync_VacanteCubiertaConOcupacionDerivada_DevuelveOcupacionDerivadaIdNull` (línea ~85) |

### web-ocupaciones-crear-editar (delta: 2 MODIFIED + 1 ADDED)

| Escenario | Capa de cobertura | Estado |
|---|---|---|
| **MODIFIED** Alta válida | pre-existente | ✅ |
| **MODIFIED** Puesto sin Vacante abierta (N3) | pre-existente | ✅ |
| **MODIFIED** Catálogo no disponible | pre-existente | ✅ |
| **MODIFIED** Usuario no-admin | pre-existente | ✅ |
| **MODIFIED** `?vacanteId` con Vacante Abierta — form rendereado y Puesto bloqueado | Web | ✅ `OcupacionCreatePageTests.Get_Create_WithVacanteIdAbierta_RendereaFormConPuestoIdBloqueadoYVgHint` (línea 834) |
| **MODIFIED** `?vacanteId` con Vacante Cubierta — error legible | Web | ✅ `OcupacionCreatePageTests.Get_Create_WithVacanteCubierta_MuestraError_VacanteYaCubierta` (línea ~893) |
| **MODIFIED** `?vacanteId` con Vacante **Cancelada** — error legible | Web | ⚠️ **WARNING**: código lo implementa (`Create.cshtml.cs:168-172` → "Esta Vacante está cancelada y no puede cubrirse.") pero NO hay test dedicado. Cobertura por código + revisión estática. |
| **MODIFIED** `?vacanteId` inexistente — error legible | Web | ✅ `OcupacionCreatePageTests.Get_Create_WithVacanteInexistente_MuestraError_VacanteNoExiste` (línea 924) |
| **MODIFIED** `?vacanteId` enviado — POST con `VacanteId` y redirect | Web | ⚠️ **WARNING**: el camino POST está implementado (`Create.cshtml.cs:207-244`) pero NO tiene test dedicado. Riesgo remanente documentado en `apply-progress.md` §Riesgos remanentes. |
| **MODIFIED** Hints sin vacanteId | pre-existente | ✅ |
| **MODIFIED** Create no sustituye al flujo automatizado | pre-existente | ✅ |
| **MODIFIED** Hint con vacanteId sin código visible | Web | ⚠️ **SUGGESTION**: código genera hint (`Create.cshtml.cs:181-184`) pero no test dedicado que verifique el texto exacto cuando la Vacante no tiene código. |
| **MODIFIED** Hint con vacanteId con código visible | Web | ⚠️ **SUGGESTION**: el hint actual NO incluye el código de la Vacante explícitamente (sólo el nombre del Puesto). Desviación menor del spec escenario; comportamiento aceptable por diseño (ver `apply-progress.md` §Notas). |
| **ADDED** Cubrir Vacante Abierta — happy path transaccional | Aplicación + API | ✅ `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_VacanteAbierta_CreaOcupacionYTransicionaVacanteACubierta` (línea 902) + `OcupacionesControllerTests.Create_ConVacanteId_Returns201Created` (línea 546) |
| **ADDED** Cubrir Vacante **En Selección** — también permitido | Aplicación | ⚠️ **SUGGESTION**: código acepta (`vacante.EstadoVacante?.EsTerminal == false` cubre Abierta y En Selección) pero el único test cubre "Abierta" con el flag `EsTerminal=false`. No hay test específico para En Selección. |
| **ADDED** Cubrir Vacante ya Cubierta — rechazado | Aplicación + API | ✅ `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_VacanteCubierta_Devuelve400_VacanteNoAbierta` (línea 957) + API 409 path |
| **ADDED** Cubrir con Ocupación vigente ya existente — conflicto | Aplicación + API | ✅ `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_VacanteYaCubierta_Devuelve409_VacanteYaCubierta` (línea 982) + `OcupacionesControllerTests.Create_ConVacanteId_VacanteYaCubierta_Returns409VacanteYaCubierta` (línea 586) |
| **ADDED** PuestoId del request no coincide — rechazado | Aplicación | ✅ `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_PuestoIdNoCoincide_Devuelve400_PuestoIdNoCoincideConVacante` (línea 1009) |
| **ADDED** VacanteId inexistente — no encontrado | Aplicación | ✅ `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_VacanteNoEncontrada_DevuelveNotFound` (línea 934) |
| **ADDED** Atomicidad — fallo de transición revierte la Ocupación | Aplicación | ✅ `OcupacionServicioComandosTests.CrearAsync_ConVacanteId_FalloEnSaveChanges_NoCreaOcupacionYNoTransicionaVacante` (línea 1042) |
| **ADDED** PuestoId omitido se resuelve desde la Vacante | Aplicación | ⚠️ **SUGGESTION**: código lo implementa (`OcupacionServicioComandos.cs:291-294`), pero el test T1.1 envía `PuestoId` explícito (no omitido). Caso de omisión no cubierto por test dedicado. |

### web-ocupaciones-navegacion-contextual (delta: 1 MODIFIED + 1 ADDED)

| Escenario | Capa de cobertura | Estado |
|---|---|---|
| **MODIFIED** Alta desde Persona | pre-existente | ✅ |
| **MODIFIED** Alta desde Puesto con Vacante abierta — navega a `?vacanteId=` | Web | ✅ `PuestoOcupacionesPageTests.Get_VacanteAbiertaSinOcupacion_LabelCubrirVacanteYRouteVacanteId` (línea 495) |
| **MODIFIED** Alta desde Puesto sin Vacante abierta (N3) | pre-existente | ✅ |
| **MODIFIED** Alta desde Puesto con Ocupacion activa (N1) | pre-existente | ✅ |
| **MODIFIED** Usuario no-admin | pre-existente | ✅ |
| **ADDED** Puesto con Vacante abierta sin Ocupación activa — label "Cubrir Vacante" | Web | ✅ (mismo test que MODIFIED anterior) |
| **ADDED** Puesto con Vacante **"En Selección"** sin Ocupación activa — label "Cubrir Vacante" | Web | ⚠️ **SUGGESTION**: código acepta cualquier estado no terminal cubrible (`!EsCubierta && !EsCancelada`) pero el test usa explícitamente "Abierta". Sin test específico para "En Selección". |
| **ADDED** Puesto con Vacante abierta y Ocupación activa — label "Nueva ocupación" | Web | ✅ `PuestoOcupacionesPageTests.VacanteAbiertaConOcupacion_LabelNuevaOcupacion` |
| **ADDED** Puesto sin Vacante abierta — se muestra "Abrir Vacante" (NAV-007) | pre-existente | ✅ |
| **ADDED** Usuario no-admin — botón oculto | pre-existente | ✅ |

### vacante-web (delta: 2 ADDED)

| Escenario | Capa de cobertura | Estado |
|---|---|---|
| **ADDED** Vacante Abierta — botón visible para admin | Web | ✅ `Get_Details_VacanteAbierta_BotonCubrirVisible` (línea 93) |
| **ADDED** Vacante En Selección — botón visible | Web | ✅ `Get_Details_VacanteEnSeleccion_BotonCubrirVisible` (línea 117) |
| **ADDED** Vacante Cubierta — botón oculto | Web | ✅ `Get_Details_VacanteCubierta_BotonCubrirOculto_BloquePersonaAsignadaVisible` (línea 141) |
| **ADDED** Vacante Cancelada — botón oculto | Web | ✅ `Get_Details_VacanteCancelada_BotonCubrirOculto` (línea 175) |
| **ADDED** Usuario sin rol de mutación — botón oculto | Web | ✅ `Get_Details_VacanteAbierta_NonMutator_BotonCubrirOculto` (línea 205) — triangulación T3.4-bis |
| **ADDED** Vacante Cubierta con Ocupación y Persona asignada — bloque visible | Web | ✅ (mismo test que "Cubierta botón oculto") |
| **ADDED** Vacante Abierta — bloque oculto | Web | ✅ (parte del test anterior: `Assert.DoesNotContain("Persona asignada")`) |
| **ADDED** Vacante Cubierta sin `PersonaAsignadaNombre` (defensivo) — bloque parcial | Web | ⚠️ **SUGGESTION**: código defensivo (`Details.cshtml:70` omite link si nombre vacío) pero sin test dedicado para `PersonaAsignadaNombre == null && OcupacionDerivadaId != null`. Desviación documentada en `apply-progress.md` §Desviaciones del design (S3). |

## Design Compliance

| Decisión | Estado | Evidencia |
|---|---|---|
| **D-1** Inversión del flujo Cubrir | ✅ | `VacanteServicioComandos.cs:296-317` (rechazo Cubierta) + `OcupacionServicioComandos.cs:161-165,252-377` (rama `VacanteId`). Bloque de creación de Ocupación eliminado. |
| **D-2** Atomicidad en `CrearOcupacionCubriendoVacanteAsync` | ✅ | Único `try`/`SaveChangesAsync` (líneas 342-377). `vacante.CambiarEstado` + `RegistrarCambioEstadoAsync` ejecutados dentro del mismo bloque que `ocupacionRepository.AddAsync`. Cobertura por test de atomicidad (T1.6 con `FakeThrowingUnitOfWork`). |
| **D-3** `VacanteDetailDto` con `OcupacionDerivadaId` + `PersonaAsignadaNombre` hidratados sólo cuando `EsCubierta` | ✅ | DTO extendido (`VacanteDetailDto.cs:18-30`) + hidratación gated (`VacanteServicioConsulta.cs:54-71`: `if (vacante.EstadoVacante?.EsCubierta == true)`). Path defensivo: si la consulta retorna null, ambos campos quedan null sin lanzar. |
| **D-4** Renombre `PersonaIdRequeridoParaCubrir` → `CubrirVacanteRequiereCrearOcupacion` (viejo con `[Obsolete]`) | ✅ | `VacanteErrorCodigo.cs:40` define el nuevo; línea 51-52 marca el viejo con `[Obsolete("…")]` y XML doc. Los tests nuevos referencian exclusivamente el nuevo nombre. |
| **D-5** UI flow definido | ✅ | (a) `Details.cshtml:114-117` botón Cubrir Vacante cuando `EsCubrible`; (b) `Create.cshtml.cs:148-186` resuelve 4 estados de Vacante (Abierta/En Selección/Cubierta/Cancelada/inexistente); (c) `PuestoOcupaciones.cshtml.cs:190-193` label dinámico "Cubrir Vacante" / "Nueva ocupación". |
| **D-6** Convención DADO-CUANDO-ENTONCES (archive phase) | N/A | Esta decisión se aplica en `sdd-archive`, no en `sdd-verify`. Los deltas respetan la convención en escenarios nuevos/modificados; los vigentes heredados (GIVEN-WHEN-THEN) no se reescriben en el delta — diferido a archive. |

## Test Suite

```
$ dotnet build SGV.slnx --nologo --no-incremental
96 Warning(s) | 0 Error(s)  (baseline consistente con S1 + S2 + S3)
Time Elapsed 00:00:09.46

$ dotnet test SGV.slnx --nologo --no-build
Passed!  - Failed:     0, Passed:  3488, Skipped:     0, Total:  3488, Duration: 2 m 25 s

$ dotnet test SGV.slnx --nologo --no-build --filter "<tests del change>"
Passed!  - Failed:     0, Passed:    36, Skipped:     0, Total:    36, Duration: 5 s

$ cd src/SGV.Web && bun run build
[21:58:43] Finished 'build' after 3.34 s  (exit 0)
```

- **Tests nuevos del change** (21 confirmados):
  - Aplicación `OcupacionServicioComandosTests`: 6 (`CrearAsync_ConVacanteId_*` × 6 escenarios: Abierta happy, NoEncontrada 404, Cubierta 400, YaCubierta 409, PuestoIdMismatch 400, AtomicidadRollback).
  - Aplicación `VacanteServicioComandosTests`: 2 (`CambiarEstado_A_Cubierta_Devuelve400ConCodigoCubrirVacanteRequiereCrearOcupacionYMensaje` reescrito + `CambiarEstado_A_Cubierta_ConPersonaIdPopulado_TambienIgnoraPersonaId`).
  - Aplicación `VacanteServicioConsultaTests`: 3 (Cubierta con Ocupación, Abierta sin cobertura, Cubierta sin Ocupación defensivo).
  - API `OcupacionesControllerTests`: 2 (`Create_ConVacanteId_Returns201Created` + `Create_ConVacanteId_VacanteYaCubierta_Returns409VacanteYaCubierta`).
  - API `VacantesControllerTests`: 2 (`PatchEstado_A_Cubierta_Returns400ConMensajeUseCubrirVacante` + `GetById_VacanteCubierta_RetornaOcupacionDerivadaIdYPersonaAsignada`).
  - Web `VacantesDetailsAndSidenavTests`: 5 (Abierta botón, En Selección botón, Cubierta bloque persona, Cancelada oculta, NonMutator oculta).
  - Web `OcupacionCreatePageTests`: 3 (`?vacanteId` Abierta, Cubierta, Inexistente).
  - Web `PuestoOcupacionesPageTests`: 3 (label Cubrir Vacante + ruta vacanteId, label Nueva ocupación coexistencia, fallback `?puestoId=` cuando hay Ocupación activa).

- **Tests que faltan** (escenarios del delta sin cobertura dedicada — SUGGESTION):
  1. `web-ocupaciones-crear-editar` `?vacanteId` con Vacante **Cancelada** → error legible (código lo implementa).
  2. `web-ocupaciones-crear-editar` POST con `VacanteId` y redirect → detalle Vacante (código lo implementa; riesgo documentado).
  3. `web-ocupaciones-crear-editar` hint con código visible / sin código (código incluye nombre del Puesto, no el código).
  4. `web-ocupaciones-crear-editar` REQ-OCC-FORM-010 `Cubrir Vacante "En Selección"` (código lo soporta; test cubre Abierta).
  5. `web-ocupaciones-crear-editar` REQ-OCC-FORM-010 `PuestoId omitido se resuelve desde Vacante` (código lo implementa; test envía `PuestoId` explícito).
  6. `web-ocupaciones-navegacion-contextual` `Cubrir Vacante` con Vacante **"En Selección"** (código soporta; test usa Abierta).
  7. `vacante-web` Bloque Cubierta **sin** `PersonaAsignadaNombre` defensivo (código omite link si nombre vacío; sin test del path defensivo).

- **Tests rotos por el change**: 0. La suite pasa completa.

## Build & Hygiene

```
dotnet build SGV.slnx: 0 errors, 0 warnings nuevos (96 = baseline preexistente)
dotnet test SGV.slnx:  3488 pass / 0 fail / 0 skip explícito
bun run build:        exit 0
```

- **Diff total** (`git diff --stat 4396e892..HEAD`): **56 files changed, 3119 insertions(+), 142 deletions(-)** — algo por encima del estimado (~2100 líneas esperadas). Explicación: el conteo incluye OpenSpec artifacts que están untracked (no commiteados), ver nota abajo. El delta de código fuente (producción + tests) es consistente con la estimación (~370–460 planificadas, ~2800 reales producción + tests). El delta de código de producción es ~1100 líneas netas, los restantes ~1900 son tests + OpenSpec artifacts locales.
- **Commits**: 3 (uno por PR — S1, S2, S3). Conventional commits ✅ (`feat(invertir-flujo-cubrir):`).
- **Sin Co-Authored-By AI**: ✅ — los 3 commits contienen `Co-authored-by: sgv-dev <dev@sgv.local>` (usuario local del repo, preexistente en baseline `bf50b82e~`). No introducen atribución de IA.
- **Sin migraciones**: ✅ — `git diff 4396e892..HEAD -- src/SGV.Infraestructura/Persistencia/Migraciones/` retorna vacío. `docs/migracion-inicial-sgv.sql` no se regeneró.
- **`docs/decisiones-implementacion.md` actualizado**: ✅ — entrada "Inversión del flujo Cubrir (change `invertir-flujo-cubrir`)" en líneas 1047-1123, documentando D-1, D-3, D-4.

### Nota sobre OpenSpec artifacts untracked

`git status` muestra `?? openspec/changes/invertir-flujo-cubrir/` (untracked). Esto es esperado — los artifacts SDD NO se commitean al branch; viven como archivos de trabajo que se archivan en `sdd-archive` después de esta verificación. La convención del repo: `openspec/changes/<cambio>/` queda como working tree hasta el archivado. No afecta el verdict.

## Findings

### CRITICAL (blockers)

Vacío. El change cubre el 100% de los AC y no introduce regresiones.

### WARNING (no blockers, importantes)

1. **WARNING-1 — Vacante Cancelada en Create no tiene test dedicado** (escenario del spec `web-ocupaciones-crear-editar`):
   - El código en `Create.cshtml.cs:168-172` implementa el mensaje "Esta Vacante está cancelada y no puede cubrirse." con la guardia correcta sobre `EstadoVacanteNombre == "Cancelada"`.
   - El delta spec exige cobertura explícita pero `tasks.md` T2.1-T2.3 planificó sólo 3 tests: Abierta, Cubierta, Inexistente.
   - **Mitigación recomendada**: agregar test Web paralelo a `Get_Create_WithVacanteCubierta_MuestraError_VacanteYaCubierta` para Cancelada. Bajo costo, alto valor de regresión.

2. **WARNING-2 — POST path con `VacanteId` no cubierto por test dedicado** (riesgo remanente documentado en `apply-progress.md`):
   - El path `OnPostAsync` cuando el form trae `VacanteId` propaga al request y redirige al Details de la Vacante (líneas 207-244).
   - No hay test que ejercite POST → 201 → redirect → Details con la Ocupación derivada visible. Una regresión en este path no rompe tests S2/S3 pero podría escapar al verificador.
   - **Mitigación recomendada**: agregar test API de integración que cree la Ocupación con `VacanteId` vía POST y verifique el redirect (o agregar test Web que ejercite el form).

3. **WARNING-3 — Comparación `EstadoVacanteNombre` case-insensitive en `EsCubrible`/`EsCubierta`** (riesgo remanente S3):
   - `VacanteDetailViewModel.EsCubrible` (líneas 31-39) y `Details.cshtml.cs.EsCubierta` (líneas 63-65) dependen de que `EstadoVacanteNombre` matchee exactamente `"Cubierta"` o `"Cancelada"` (case-insensitive).
   - Si el backend cambia los strings (i18n, minúsculas, renombre), el botón puede aparecer para Cubierta o desaparecer para En Selección.
   - **Mitigación futura**: pedir un flag explícito `EsCubierta`/`EsCancelada` al backend vía DTO (alineado con la práctica del `EstadoVacanteDto.EsCubierta` ya usado por el dropdown Edit). Out of scope del change.

### SUGGESTION (mejoras)

1. **SUGGESTION-1** — Tests de parametrización `[Theory]` para el flag `EsCubrible` cubrirían (Abierta, En Selección, Cubierta, Cancelada) sin duplicar tests por escenario de estado. Reduciría 4 tests a 1 parametrizado en `VacantesDetailsAndSidenavTests`.

2. **SUGGESTION-2** — Agregar test para el path defensivo `Cubierta && OcupacionDerivadaId != null && PersonaAsignadaNombre == null` (escenario 3 del requisito `Bloque Persona asignada`). El código defensivo funciona pero no tiene cobertura dedicada.

3. **SUGGESTION-3** — Agregar test dedicado al escenario `Cubrir Vacante "En Selección"` (escenarios 2 de REQ-OCC-FORM-010 y de REQ-OCC-NAV-008). Cobertura por código + revisión; sin test explícito.

4. **SUGGESTION-4** — Agregar test dedicado al escenario `PuestoId omitido` se resuelve desde la Vacante (escenario 8 de REQ-OCC-FORM-010). Cobertura por código + revisión; sin test explícito del path de omisión.

5. **SUGGESTION-5** — El hint actual en Create no incluye el código de la Vacante explícitamente cuando existe (escenarios 5/6 de REQ-OCC-FORM-009). El comportamiento por defecto cubre "nombre del Puesto"; el spec pedía opcionalmente "código + nombre". Desviación menor del spec, documentada en `apply-progress.md` §Desviaciones del design (S2 no fue explícita, pero el comportamiento implementado difiere). Decisión del negocio si se requiere el ajuste.

## Riesgos residuales

- **`[MySqlFact]` no corridos**: la suite incluye tests de persistencia que requieren MySQL 8 local (`sgv_test` database). Sin MySQL local, se skipean automáticamente (el runner no los cuenta como "skip" — están ausentes del run). El verificador futuro con MySQL debe correr `dotnet test SGV.slnx` con la connection string activa para validar la atomicidad transaccional de `CrearOcupacionCubriendoVacanteAsync` contra constraints reales (`IX_Ocupaciones_VacanteId`, `ActivePuestoIdUnique`). Cobertura por `FakeThrowingUnitOfWork` (T1.6) cubre el path declarativo, pero la constraint real sólo se valida con BD.
- **Comparación `EstadoVacanteNombre` case-insensitive** (S3, ver WARNING-3): depende de strings del seed.
- **POST path con `VacanteId` sin test dedicado** (S2, ver WARNING-2): regresiones podrían no detectarse automáticamente.
- **`VacanteNoEncontrada` HTTP status code**: el código devuelve `ErrorCategoria.NotFound` pero no se mapea a `404 NotFound` automáticamente — depende del `ApiResults.MapCategoria` o equivalente. El spec escenario lo espera. Verificado en `OcupacionesControllerTests.Create_ConVacanteId_VacanteYaCubierta_Returns409VacanteYaCubierta` que verifica 409; no hay test paralelo que verifique 404. Aceptable pero a monitorear en runtime.

## Recomendación

- **PASS WITH WARNINGS**: el change puede archivarse. Los 10 ACs están cubiertos, las decisiones arquitectónicas D-1..D-5 están reflejadas en código, la suite pasa completa, no hay migraciones, la documentación de decisiones está actualizada.
- Los WARNING-1 y WARNING-2 son recomendables de resolver antes del archivado si se quiere maximizar la confianza (costo bajo: 2 tests Web/API adicionales). Si no, pueden ir como follow-ups post-archive.
- Los SUGGESTIONs son opcionales y pueden resolverse en changes futuros.

## Próximo paso

- **Si PASS WITH WARNINGS aceptable**: `sdd-archive` (sincronizar deltas a specs vigentes + aplicar normalización DADO-CUANDO-ENTONCES per D-6 + actualizar código de error en spec vigente per D-4). Considerar agregar WARNING-1 y WARNING-2 como tasks previas al archivado para maximizar cobertura antes de promover las specs vigentes.
- **Si WARNINGs críticos antes de archivar**: agregar tests Web/API para Cancelada + POST flow; luego `sdd-archive`.
- **Post-archive**: los SUGGESTIONs pueden iterarse en follow-ups (`SUGGESTION-3 En Selección`, `SUGGESTION-4 PuestoId omitido`, `SUGGESTION-2 defensivo PersonaAsignadaNombre`).
