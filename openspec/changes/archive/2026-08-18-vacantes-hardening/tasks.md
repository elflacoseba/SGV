# Tareas: Vacantes Hardening

## Review Workload Forecast

Estimated: ~360; neto −60.
Delivery strategy: ask-on-risk
Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units: A 01–05 [Aplicacion/N/A/A]; B/E 06–12 [build+grep/N/A/contratos]; C/F 13–18,21–23 [Web/smoke/Web]; D 19–20 [VacantesCubrir/MySQL-skip/D].

## Tareas

| TASK · Cluster · Esfuerzo · LoC | Files | T/W/D | Commit |
|---|---|---|---|
| 01 A.3 · 30m · +25 | `tests/SGV.Tests/Aplicacion/Comun/FakeUsuarioActual.cs` | stub `UserId=test-user`/`Anonymous=null`; verde. | `test: fake user` |
| 02 A.4 · 1h · +20 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs`; `.../Ocupaciones/OcupacionServicioComandosTests.cs` | RED actor/anon/history; helpers; flows. | `test: actor` |
| 03 A.1 · 1h · +18 | `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | TASK-02; UserId+guard; no `usuarioId:null`. | `feat: actor` |
| 04 A.2 · 1h · +18 | `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | Cubrir actor/anon; UserId+guard; historial. | `feat: cover` |
| 05 A.5 · 30m · 0 | `src/SGV.Api/Program.cs:218-219`; `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | DI; verify Scoped/fake; factory verde. | `test: wire` |
| 06 B.1 · 30m · −9 | `src/SGV.Aplicacion/Vacantes/Comandos/IVacanteServicioComandos.cs` | grep; quitar XML+firma; interfaz=0. | `refactor: member` |
| 07 B.3 · 30m · −80 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs:819-893` | Borrar `ActualizarObservaciones_VacanteInexistente_Retorna404`, `ActualizarObservaciones_TextoValido_PersisteYLimpia`, `ActualizarObservaciones_TextoMuyLargo_RetornaValidationFailure`, `ActualizarObservaciones_NuloOLimpio_LimpiaValor`; mantener side-effect; refs=0. | `test: orphan tests` |
| 08 B.2 · 30m · −71 | `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs:20,380-450` | grep app; quitar método/menciones, preservar side-effect; build. | `refactor: implementation` |
| 09 B.4 · 30m · −18 | `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs:1341-1358` | controller tests; quitar override; fake compila. | `test: fake` |
| 10 B.5 · 30m · 0 | `src/`; `tests/` | grep `ActualizarObservacionesAsync`; limpiar; 0. | `chore: orphan audit` |
| 11 E.1 · 30m · −1 | `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs:29` | reflexión+`Cerrar(null)`; quitar constante; Motivo. | `chore: error code` |
| 12 E.2 · 30m · 0 | `src/`; `tests/` | grep `MotivoObligatorio`; auditar; 0. | `chore: audit` |
| 13 C.1 · 1h · +30 | `src/SGV.Web/Integration/Vacantes/VacanteCreateInputModel.cs` | RED/reflexión sin estado; crear; tipo correcto. | `feat: create model` |
| 14 C.2 · 1h · +35 | `src/SGV.Web/Integration/Vacantes/VacanteEditInputModel.cs` | RED/reflexión `Guid?`+`[Required]`; crear; atributo correcto. | `feat: edit model` |
| 15 C.3 · 30m · −35 | `src/SGV.Web/Integration/Vacantes/VacanteInputModel.cs` | borrar; refs=0. | `refactor: model` |
| 16 C.4 · 1h · 0 | `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs:35-118` | POST `estadoVacanteId:null`; bind Create+quitar `ModelState.Remove`; wire estable. | `feat: bind create` |
| 17 C.5 · 1h · 0 | `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs:23-25,180-188` | PATCH GUID; bind Edit+`PopulateInput`; Razor compila. | `feat: bind edit` |
| 18 C.6 · 1h · 0 | `src/SGV.Web/Pages/Organizacion/Vacantes/{Create,Edit}.cshtml`; tests Web | build+smoke; bindings verdes. | `test: web smoke` |
| 19 D.1 · 1h · +10 | `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs:370-376`; `src/SGV.Aplicacion/Comun/Persistencia/IConstraintViolationDetector.cs;src/SGV.Infraestructura/Persistencia/MySqlConstraintViolationDetector.cs;tests/SGV.Tests/Aplicacion/*/` | RED constraint name; `IX_Ocupaciones_VacanteIdUnique`→`OcupacionErrorCodigo.VacanteYaCubierta`, no `DatosInvalidos`; 409. | `fix: map constraint` |
| 20 D.2 · 2h · +55 | `tests/SGV.Tests/Api/Vacantes/VacantesCubrirConcurrencyTests.cs` | `[MySqlFact]` `CubrirVacante_Concurrencia_TOCTOU_SoloUnaCoberturaExitosa` + `CubrirVacante_Concurrencia_DobleCobertura_ConstraintUnica`; race+cleanup; 1×2xx/1×409, skip. | `test: concurrency` |
| 21 F.1 · 30m · 0 | `src/SGV.Web/Pages/Organizacion/Vacantes/Index.cshtml.cs:48` | roles `CanMutate`; usar `RolesSgv.*`; literales=0. | `fix: roles` |
| 22 F.2 · 30m · +5 | `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs:51-67` | terminal→Details/abierta→Page; guard `EsCerrada`; sin form terminal. | `feat: terminal` |
| 23 F.3 · 30m · +1 | `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs:92-95` | GET/POST; `DateTime.Today`; visible. | `feat: date` |

Final: −60 neto/~360 brutas; **well under budget**; cierre: build+test.
