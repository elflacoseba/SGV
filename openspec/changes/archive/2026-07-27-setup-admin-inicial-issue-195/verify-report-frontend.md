# Verify Report (Frontend) — setup-admin-inicial-issue-195 (PR #2)

> Issue: #195
> Change: `setup-admin-inicial-issue-195`
> Spec: REQ-SETUP-005 y REQ-SETUP-006 (los demás ya cubiertos por PR #1)
> Branch: `feat/setup-admin-inicial-issue-195-pr2-frontend`
> PR target: `feat/setup-admin-inicial-issue-195-pr1-backend`
> PR size: 1524 líneas (size:exception aprobado)
> Modo: Strict TDD
> Verdict: **PASS WITH WARNINGS**

## Resumen ejecutivo

El PR #2 implementa la Razor Page anónima `/auth/setup`, el typed client sin bearer, la redirección desde SignIn, el cache de status y el manejo PRG/errores exigidos por REQ-SETUP-005/006. Build, bundle frontend y ambas suites solicitadas pasan; los siete escenarios frontend tienen tests runtime aprobados. No hay hallazgos CRITICAL; quedan tres WARNINGs no bloqueantes sobre evidencia de TempData, fidelity del test de cache y falta de una prueba explícita del `_AuthLayout`.

## Completitud

| Métrica | Resultado |
|---|---:|
| Requirements totales del cambio | 6 |
| Requirements backend ya verificados en PR #1 | 4 (REQ-SETUP-001..004) |
| Requirements frontend aplicables | 2/2 completos |
| Escenarios frontend aplicables | 7/7 compliant |
| WU aplicables | WU-4 y WU-5 completas |
| Tasks de implementación incompletas en WU-4/WU-5 | 0 |

## Desviaciones evaluadas

| Desviación | Impacto | Severidad | Recomendación |
|---|---|---|---|
| `SetupApiClient` parsea manualmente `value` con `System.Text.Json` | Preserva que `SGV.Web` dependa sólo de `SGV.Contracts`; el cliente valida body inválido y lo traduce a fallo recuperable. | **SUGGESTION — estrictamente mejor arquitectónicamente** | Mantener. A futuro podría moverse el envelope wire a Contracts si se reutiliza, evitando parsing manual sin romper capas. |
| `FakeSetupApiClient` modela TTL 30s | Permite probar el flujo Razor, pero el fake replica la lógica productiva y los tests de integración de cache no prueban `IMemoryCache` ni el handler real. El cache real sí está cubierto por `SetupApiClientTests`. | **WARNING — aceptable con mitigación** | Mantener los unit tests del cliente real como fuente de verdad; renombrar/documentar los integration tests como verificación del efecto del cache, no de su implementación real. |
| `StatusCacheKey` público | Aumenta mínimamente la superficie pública sólo para limpieza de tests. No expone secretos ni rompe contratos. | **WARNING — aceptable** | Preferir `internal` + `InternalsVisibleTo` o abstraer el reloj/cache si luego se prueba expiración real; no bloquea. |
| `NormaliseFieldKey` convierte camelCase a `Input.Password` | Es necesario para que `ValidationProblemDetails` llegue a `asp-validation-for`; sigue el patrón existente. | **SUGGESTION — estrictamente mejor funcionalmente** | Mantener. Agregar un caso camelCase explícito (`password`) si se endurece la suite. |
| `TiposDocumentoOptions` usa `Codigo` como `Text` | Coherente con `Personas/_Form.cshtml`; mantiene etiquetas canónicas DNI/PAS/LE/LC. | **SUGGESTION — estrictamente mejor por consistencia** | Mantener. |

## Validación por requirement

### REQ-SETUP-001 — Estado de setup
- **Estado**: N/A en PR #2; ✅ cubierto por PR #1 (`verify-report.md`).

### REQ-SETUP-002 — Creación del primer Administrador
- **Estado**: N/A en PR #2; ⚠️ cubierto por PR #1 con warnings ya registrados.

### REQ-SETUP-003 — Concurrencia e idempotencia
- **Estado**: N/A en PR #2; ⚠️ cubierto por PR #1 con warnings ya registrados.

### REQ-SETUP-004 — Auditoría y seguridad operacional
- **Estado**: N/A en PR #2; ✅ cubierto por PR #1.

### REQ-SETUP-005 — Formulario web de setup
- **Estado**: ✅ OK
- **Escenarios cubiertos**:
  - ✅ **COMPLIANT — Redirección desde SignIn**: `SignInModel.OnGetAsync` (`SignIn.cshtml.cs:24-38`) consulta `ISetupApiClient` y usa `RedirectToPage("/Auth/Setup")`; pasa `Get_SignIn_DBVacia_RedirigeASetup`.
  - ✅ **COMPLIANT — Render del formulario**: `Setup.cshtml:24-108` contiene anti-forgery, nueve controles con tag helpers y dropdown; pasa `Get_Setup_Renderiza9CamposYDropdownConAntiforgery`.
  - ✅ **COMPLIANT — Setup no disponible**: `SetupModel.OnGetAsync` (`Setup.cshtml.cs:39-55`) redirige a SignIn cuando `RequiresSetup=false`; pasa `Get_Setup_ConDbNoVacia_RedirigeASignIn`.
  - ✅ **COMPLIANT — Catálogo de documentos**: `Setup.cshtml.cs:50-53` carga catálogo y proyecta GUID a `Value`; datos de test usan bloque `71000000-…` y el HTML contiene DNI/PAS.
- **Evidencia adicional**: `Setup.cshtml` hereda `Pages/Auth/_ViewStart.cshtml`, el mismo mecanismo de layout que SignIn. La suite verifica el render, aunque no identifica explícitamente `_AuthLayout` por nombre (W-003).

### REQ-SETUP-006 — Resultado y errores del formulario
- **Estado**: ✅ OK con WARNING de evidencia
- **Escenarios cubiertos**:
  - ✅ **COMPLIANT — Submit exitoso**: `Setup.cshtml.cs:99-103` asigna `TempData["SetupSuccess"]` y redirige con PRG; pasa `Post_Setup_DatosValidos_RedirigeASignInConTempData`. El test prueba redirect y request, pero no consume el GET posterior para probar visualmente el TempData (W-001).
  - ✅ **COMPLIANT — Errores de validación**: `ApplyFailureToModelState` (`Setup.cshtml.cs:110-152`) mapea errores por campo a `Input.*`; pasa `Post_Setup_ApiDevuelve400ConFieldErrors_MuestraErroresPorCampo` con mensajes en español.
  - ✅ **COMPLIANT — Error de transporte**: `Setup.cshtml.cs:79-97` captura `HttpRequestException` y timeout no originado por cancelación del request, vuelve a `Page()` y muestra mensaje recuperable; pasan los tests HTTP failure y timeout.

## Matriz de cumplimiento conductual

| Requirement | Escenario | Test runtime | Estado |
|---|---|---|---|
| REQ-SETUP-005 | SignIn redirige con DB vacía | `SignInSetupRedirectTests.Get_SignIn_DBVacia_RedirigeASetup` | ✅ COMPLIANT |
| REQ-SETUP-005 | Render de 9 campos + anti-forgery + layout auth | `SetupPageRenderTests.Get_Setup_Renderiza9CamposYDropdownConAntiforgery` | ✅ COMPLIANT (layout inferido por render) |
| REQ-SETUP-005 | Setup no disponible | `SetupPageRenderTests.Get_Setup_ConDbNoVacia_RedirigeASignIn` | ✅ COMPLIANT |
| REQ-SETUP-005 | Catálogo TipoDocumento | `SetupPageRenderTests.Get_Setup_Renderiza9CamposYDropdownConAntiforgery` | ✅ COMPLIANT |
| REQ-SETUP-006 | Submit exitoso + PRG + éxito | `SetupPageRenderTests.Post_Setup_DatosValidos_RedirigeASignInConTempData` | ✅ COMPLIANT con W-001 |
| REQ-SETUP-006 | Errores por campo en español | `SetupPageRenderTests.Post_Setup_ApiDevuelve400ConFieldErrors_MuestraErroresPorCampo` | ✅ COMPLIANT |
| REQ-SETUP-006 | Error de transporte recuperable | `SetupPageRenderTests.Post_Setup_ApiCae_MuestraMensajeRecuperable`; `...ApiTimeOut...` | ✅ COMPLIANT |

**Compliance summary**: 7/7 escenarios frontend compliant.

## Validación de las decisiones del design (frontend)

| Decisión | Implementación | Coherencia | Severidad |
|---|---|---|---|
| `[AllowAnonymous]` en catálogo | Cubierto por PR #1. | N/A | N/A |
| Fail-open + `IMemoryCache` TTL 30s | `SetupApiClient.cs:54,59-85`; sólo cachea respuesta real exitosa. Unit tests cubren cache, HTTP, timeout y 5xx. | ✅ Conforme | OK |
| Cliente setup anónimo | `Program.cs:243-255` registra typed client sin `ApiBearerTokenHandler`. | ✅ Conforme | OK |
| Razor Page con `_AuthLayout` | Page en `Pages/Auth`; usa el `_ViewStart` común de Auth y renderiza estructura Inspinia. | ✅ Conforme | WARNING de test explícito |
| 9 campos + dropdown | `Setup.cshtml:35-103`; `InputModel` en `Setup.cshtml.cs:175-209`. | ✅ Conforme | OK |
| Anti-forgery | `Setup.cshtml:25`; test detecta `__RequestVerificationToken`. Razor Pages valida automáticamente el POST. | ✅ Conforme | OK |
| PRG + `TempData["SetupSuccess"]` | `Setup.cshtml.cs:99-103`. | ✅ Conforme | WARNING de evidencia end-to-end |
| Manejo de transporte | `Setup.cshtml.cs:79-97`; `SetupApiClient.cs:66-85`. | ✅ Conforme | OK |
| Redirect desde SignIn | `SignIn.cshtml.cs:24-38`, usando `RedirectToPage`, como exige WU-5. | ✅ Conforme | OK |

## Validación de WU-4 y WU-5

| WU | Archivos | Criterios de aceptación | Commit |
|---|---|---|---|
| WU-4 | Existen cliente, result wire local, Razor Page, DI, fake y tests web. La implementación consolidó los tests submit/render en archivos distintos a los nombres sugeridos, sin perder escenarios. | ✅ Cumplidos: layout/auth form, 9 campos, dropdown, cache/fail-open, PRG, field errors, HTTP y timeout. | ✅ `c05c69d0 feat(setup): añadir pantalla y cliente de setup inicial del admin` |
| WU-5 | Existe modificación de `SignIn.cshtml.cs` y suite `SignInSetupRedirectTests.cs`; el fake/cache se ajustó en el mismo WU. | ✅ Cumplidos: true redirige, false renderiza, fail-open renderiza, cache evita llamadas repetidas. | ✅ `149d4677 feat(setup): redirigir a setup cuando AspNetUsers está vacía` |

## TDD Compliance

| Check | Resultado | Detalle |
|---|---|---|
| TDD evidence reportado | ⚠️ Parcial | Apply-progress registra tests, commits y comandos, pero no contiene tabla RED/GREEN/REFACTOR formal. Dado que los tests existen, están co-ubicados con cada WU y pasan, se registra como deuda procesal, no como fallo funcional. |
| Todos los WU tienen tests | ✅ | WU-4 y WU-5 incluyen tests en sus commits. |
| RED confirmado por existencia | ✅ | 4 archivos de tests nuevos + fake; 27 casos descubiertos. |
| GREEN confirmado | ✅ | 27/27 focused y 1137/1137 suite amplia. |
| Triangulación | ✅ | Success, validation, transport, timeout, setup available/unavailable y cache hit/miss. |
| Safety net | ✅ | Build y suite amplia actuales pasan; archivos productivos del setup son nuevos salvo Program/SignIn. |

**TDD Compliance**: evidencia runtime completa; protocolo RED histórico no puede reconstruirse sólo desde el estado final.

## Distribución de capas de tests

| Capa | Tests | Archivos | Herramienta |
|---|---:|---:|---|
| Unit | 13 | 1 | xUnit + `HttpMessageHandler` |
| Integration | 14 | 3 | xUnit + `WebApplicationFactory` |
| E2E | 0 | 0 | No disponible |
| **Total** | **27** | **4** | |

## Calidad de aserciones

✅ No se encontraron tautologías, loops fantasma, tests sin llamada a producción ni smoke tests vacíos. Las aserciones verifican status HTTP, rutas, campos HTML, mensajes, requests, cache y resultados tipados.

## Cobertura de archivos modificados

Cobertura ejecutada con `XPlat Code Coverage` sobre los 27 tests focused:

| Archivo | Cobertura observada | Evaluación |
|---|---:|---|
| `Integration/Setup/SetupApiClient.cs` | 88.88% líneas; 75% branches (clase principal) | ⚠️ Aceptable |
| `Integration/Setup/SetupHttpResult.cs` | 100% líneas/branches | ✅ Excelente |
| `Pages/Auth/Setup.cshtml.cs` | 90% líneas; 55.55% branches (clase principal) | ⚠️ Aceptable |
| `Pages/Auth/SignIn.cshtml.cs` | `OnGetAsync` 100%; `OnPostAsync` fuera del scope focused | ✅ Para WU-5 |
| `Program.cs` | 63.54% global (host amplio) | ➖ No atribuible sólo a setup |

No se persiste el artefacto temporal de cobertura en el repo. Hash Cobertura: `a13f43aeefb3756b1fde674f72d0176e32257061336840e63d5aa3428cfdea5c`.

## Validación de Clean Architecture

- [x] `SGV.Web.csproj` sólo referencia `SGV.Contracts`; no referencia `SGV.Aplicacion` ni `SGV.Infraestructura`.
- [x] El código productivo de `SGV.Web` no importa `SetupCommandResult`; el parsing manual evita romper la dependencia. La referencia a Aplicación aparece sólo en tests para construir el envelope backend.
- [x] `SetupApiClient` se registra sin `ApiBearerTokenHandler`.
- [x] `Setup.cshtml` contiene presentación/tag helpers, sin reglas de dominio.
- [x] `Setup.cshtml.cs` orquesta status, catálogo, request, PRG y errores; no crea Persona/Usuario ni asigna roles localmente.
- [x] Mensajes de validación, transporte, timeout y éxito en español.
- [x] No se registran password, token ni email en logs; los logs contienen sólo evento/excepción técnica.
- [x] Convenciones Razor Pages: `[BindProperty]`, `ModelState.IsValid`, `Page()`, `RedirectToPage`, TempData, `asp-for`, `asp-validation-for` y anti-forgery.

## Validación de la chain strategy

- [x] Branch actual: `feat/setup-admin-inicial-issue-195-pr2-frontend`.
- [x] `feat/setup-admin-inicial-issue-195-pr1-backend` es ancestro de HEAD (`merge-base --is-ancestor` exit 0).
- [x] Diff contra PR #1 contiene 14 archivos y 1524 inserciones/1 eliminación, sin reintroducir los archivos backend del PR #1.
- [x] PR target esperado: `feat/setup-admin-inicial-issue-195-pr1-backend`.
- [x] Queda listo para rebase/retarget contra tracker si PR #1 mergea antes.
- [x] Excepción al budget de 400 líneas aprobada; los dos commits siguen work units cohesivos con tests incluidos.

## Hallazgos CRITICAL (bloqueantes para abrir PR)

Ninguno — el PR #2 está listo para abrir.

## Hallazgos WARNING (no bloqueantes, documentar)

- **W-001 — El test de PRG no prueba el mensaje TempData en el GET posterior**: `Post_Setup_DatosValidos_RedirigeASignInConTempData` verifica 302 y request, pero no sigue la redirección ni comprueba que SignIn renderice `SetupSuccess`. La implementación estática sí asigna la clave correcta. Recomendación: agregar una prueba del GET posterior cuando se endurezca la suite.
- **W-002 — Los tests integration del cache validan un fake que replica el TTL**: `SetupStatusCacheTests` prueba el efecto del fake, no `IMemoryCache` productivo. La mitigación vigente es `SetupApiClientTests.ObtenerEstadoAsync_DosLlamadasEnVentanaDe30s_SoloUnaPeticionAlServidor`, que sí ejecuta el cliente real. No bloquear.
- **W-003 — `_AuthLayout` no se aserta explícitamente**: el render exitoso desde `Pages/Auth` y la estructura Inspinia prueban integración, pero el test no identifica el layout por un marcador estable. La inspección estática confirma la herencia por `_ViewStart`. Recomendación: usar un marcador estable del layout si existe.

## Hallazgos SUGGESTION (mejoras futuras)

- **S-001 — Encapsular `StatusCacheKey`**: cambiar a `internal` con acceso de tests o abstraer reloj/cache reduciría superficie pública y permitiría probar expiración sin esperas reales.
- **S-002 — Agregar caso camelCase explícito**: el test de field errors usa `Password`/`Email`, mientras la API del PR #1 devuelve claves camelCase. La función estática normaliza correctamente por inspección, pero un test con `password` protegería esa frontera exacta.
- **S-003 — Extraer rutas literales**: `SetupApiClient` usa literales para POST setup y catálogo aunque existe `SetupApiRoutes.Base`; centralizar reduciría drift futuro. No afecta el comportamiento actual.

## Tests ejecutados

- `dotnet build SGV.slnx` → ✅ exit 0; 0 errores, 4 warnings NU1510 preexistentes.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Web.Auth.Setup|FullyQualifiedName~Web.Auth.SignIn" --logger "console;verbosity=normal"` → ✅ 27/27 passed, 0 failed, 0 skipped.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Web|Tests.Setup" --logger "console;verbosity=normal"` → ✅ 1137/1137 passed, 0 failed.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Web.Auth.Setup|FullyQualifiedName~Web.Auth.SignIn" --collect:"XPlat Code Coverage"` → ✅ 27/27 passed; cobertura generada.
- `bun run build` en `src/SGV.Web` → ✅ exit 0; bundle construido. Avisos no bloqueantes por metadata Browserslist desactualizada y deprecación de `fs.Stats`.

## Recomendación al orchestrator

**READY_WITH_WARNINGS — OK para abrir PR** contra `feat/setup-admin-inicial-issue-195-pr1-backend`. Los WARNINGs son deuda de precisión de pruebas, no incumplimientos funcionales; los siete escenarios frontend tienen evidencia runtime aprobada.
