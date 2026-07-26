# Tasks: Permitir Legajo nulo en Personas (issue #202)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~340 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units

Single PR — cambio contenido (~340 líneas, <400), bajo riesgo de salto de presupuesto.

## Phase 0: Pre-apply — bloqueantes

- [x] 0.1 Ejecutar `gentle-ai sdd-status` para confirmar dependencias listas y que `setup-admin-inicial-issue-195` no fue disturbado
  - Verificado: `setup-admin-inicial-issue-195/*` mantiene mtime `Jul 26 14:03:23 2026` (sin modificaciones desde la creación del change).
  - `dotnet build SGV.slnx` → 0 errores.
  - Safety net `PersonaServicioComandosTests` → 16/16 pasando.

## Phase 1: Cambios de tipo (Foundation)

- [x] 1.1 `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs`: `string Legajo` → `string? Legajo` en `CrearPersonaRequest` y `ActualizarPersonaRequest` (Diseño §3; spec persona-management §Alta, spec transport-contract §Req)
- [x] 1.2 `src/SGV.Web/Integration/Personas/PersonaInputModel.cs`: quitar `[Required]`, `[StringLength(20)]`→`(50)`, `string`→`string?`, sin default (Diseño §2; spec persona-management §Alta)
- [x] 1.3 `IPersonaForm.cs` + `CreateModel`/`EditModel`: agregar `bool ShowLegajoContextWarning { get; }` default `false` (Diseño §6)
- [x] 1.4 `src/SGV.Infraestructura/Setup/SetupServicio.cs:104`: `?? string.Empty` → `request.Legajo` (Diseño §7)
- [x] 1.5 `dotnet build SGV.slnx` — verificar compilación de ~45 call-sites con `string?`

## Phase 2: Auditoría al limpiar Legajo (TDD)

- [x] 2.1 RED: crear `FakeAuditoriaServicio` helper en tests/Aplicacion/Personas/; escribir tests `CrearAsync_LegajoNull_PermitidoYGuarda`, `ActualizarAsync_LimpiarLegajo_RegistraAuditoria`, `ActualizarAsync_LegajoSinTransicion_NoEmiteAuditoriaLegajo`, `ActualizarAsync_LegajoDuplicado_SigueRechazando` en `PersonaServicioComandosTests` — FAIL (sin inyección de auditoría) (Diseño §8, spec persona-management §Auditoría explícita)
- [x] 2.2 GREEN: agregar `IAuditoriaServicio` + `IUsuarioActual` al ctor de `PersonaServicioComandos`; implementar bloque de auditoría en `ActualizarAsync` tras `SaveChangesAsync` (Diseño §5)
  - RED confirmado por error de compilación en `CrearServicio(repo, uow, auditoria)` al invocar el nuevo ctor de 6 argumentos.
  - GREEN: 20/20 `PersonaServicioComandosTests` pasando (16 previos + 4 nuevos).
  - Helpers nuevos: `NoopAuditoriaServicio` + `NullUsuarioActual` (internals en `SGV.Aplicacion`) para mantener el ctor de back-compat con 11 tests previos.

## Phase 3: Normalización web (TDD)

- [x] 3.1 RED: `PersonaWebSeamTests`: `EditPageTests.OnPostAsync_LegajoWhitespace_NormalizaANull` — FAIL (PageModels sin normalizar) (Diseño §8)
- [x] 3.2 GREEN: `Create.cshtml.cs:111`: normalizar `Input.Legajo` — whitespace→null, else `.Trim()` (Diseño §4; spec persona-management §Crear whitespace-only)
- [x] 3.3 GREEN: `Edit.cshtml.cs:118`: simplificar pre-carga a `Input.Legajo = persona.Legajo`; línea 174: normalizar POST (Diseño §4; spec persona-management §Editar whitespace-only)
  - RED confirmado: el test nuevo recibía 500 por NRE al invocar `Input.Legajo.Trim()` con whitespace.
  - GREEN: 106/106 tests web Persona/Create/Edit pasando (54 previos + 1 nuevo + ajustes 1 existente).
  - Ajusté `Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm` para enviar `Input.Apellidos` no vacío (sigue siendo [Required]) y un mensaje de legajo alcanzable vía backend (no vía client-side).

## Phase 4: Tests de integración

- [ ] 4.1 RED: `PersonaApiClientBasicTests`: 3 tests seam — `CreateAsync_LegajoNull_SerializaLegajoNull`, `CreateAsync_LegajoVacio_SerializaLegajoVacio`, `UpdateAsync_LegajoConEspaciosNoTrimeaCliente` (Diseño §8, spec transport-contract §5 escenarios)
- [ ] 4.2 RED: `PersonasControllerTests`: 3 tests API — `Post_LegajoNullEnBody_Retorna201`, `Put_LimpiarLegajo_Retorna200YRegistraUpdateLegajo`, `Put_LegajoSinClave_Retorna200` (Diseño §8)
- [ ] 4.3 RED: `PersonaRepositoryTests`: `PersistirPersona_LegajoNull_LecturaPosterior` (`[MySqlFact]`) (Diseño §8)
- [ ] 4.4 `dotnet test SGV.slnx` — todos los tests en verde

## Phase 5: UI warning + Verificación final

- [ ] 5.1 `_Form.cshtml`: agregar `<span class="text-warning small" data-legajo-context-warning hidden>` bajo campo Legajo (Diseño §6)
- [ ] 5.2 `dotnet build SGV.slnx` + `dotnet test SGV.slnx` en verde
- [ ] 5.3 `bun run build` en `src/SGV.Web` (sin impacto esperado)
- [ ] 5.4 Smoke manual: `/personas/crear` con Legajo vacío → redirect ok; `/personas/editar/{id}` limpiando Legajo → 200 + fila `UpdateLegajo` en Auditorias
