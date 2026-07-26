# Apply Progress: permitir-legajo-nulo-personas-issue-202

## Estado general

| Métrica | Valor |
|---------|-------|
| Change | `permitir-legajo-nulo-personas-issue-202` (issue #202) |
| PR scope | Single PR (decisión confirmada por el orquestador; review budget ~340 < 400) |
| Mode | Strict TDD (RED → GREEN → REFACTOR por tarea) |
| Tests | 2948/2948 PASS — 0 failed, 0 skipped |
| Build | 0 errors |
| `bun run build` | OK (sin impacto en assets frontend) |
| MySQL `[MySqlFact]` | Ejecutados — MySQL alcanzable en `localhost:3306`; ningún test skipeado |
| Estado | listo para commit por el orquestador + apertura de PR sobre `develop` |
| Cambio paralelo | `setup-admin-inicial-issue-195/` **NO modificado** (verificado con `stat` mtime `Jul 26 14:03:23 2026`) |

## Tareas completadas (Phase 0–5)

### Phase 0 — Pre-apply (bloqueantes)

- ✅ **0.1** Verificación de bloqueantes:
  - `setup-admin-inicial-issue-195/*` mantiene mtime `Jul 26 14:03:23 2026` (sin tocar desde la creación del change).
  - `dotnet build SGV.slnx` → 0 errores.
  - Safety net `PersonaServicioComandosTests` → 16/16 pasando (línea base antes de tocar el código).

### Phase 1 — Cambios de tipo (Foundation)

- ✅ **1.1** `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs`: `string Legajo` → `string? Legajo` en `CrearPersonaRequest` y `ActualizarPersonaRequest`. XML docs actualizados para reflejar la opcionalidad.
- ✅ **1.2** `src/SGV.Web/Integration/Personas/PersonaInputModel.cs`: removido `[Required]`, `[StringLength(20)]` → `(50)`, `string` → `string?`, default `string.Empty` eliminado.
- ✅ **1.3** `src/SGV.Web/Integration/Personas/IPersonaForm.cs`: agregada propiedad `bool ShowLegajoContextWarning { get; }`. `CreateModel` y `EditModel` la implementan con `=> false` por default.
- ✅ **1.4** `src/SGV.Infraestructura/Setup/SetupServicio.cs:104`: removido `?? string.Empty` workaround; ahora pasa `request.Legajo` directo.
- ✅ **1.5** `dotnet build SGV.slnx` → 0 errores tras el cambio de wire-type.

### Phase 2 — Auditoría al limpiar Legajo (TDD)

- ✅ **2.1 RED**: 4 tests nuevos en `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioComandosTests.cs`:
  - `CrearAsync_LegajoNull_PermitidoYGuarda` (assert IsSuccess, AddCallCount==1, auditoriaCount==0)
  - `ActualizarAsync_LimpiarLegajo_RegistraAuditoria` (assert Accion="UpdateLegajo", LegajoAnterior="L-001", LegajoNuevo=null)
  - `ActualizarAsync_LegajoSinTransicion_NoEmiteAuditoriaLegajo` (assert auditoriaCount==0)
  - `ActualizarAsync_LegajoDuplicado_SigueRechazando` (regresión)
- Helpers nuevos en el mismo archivo: `FakeAuditoriaServicio`, `FakeUsuarioActual`, `AuditoriaInvocacion` record.
- RED confirmado por error de compilación (4-arg `CrearServicio` no podía mapear al nuevo ctor de 6 args).
- ✅ **2.2 GREEN**:
  - `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs`: agregado `IAuditoriaServicio` + `IUsuarioActual` al ctor primario; bloque de auditoría tras `SaveChangesAsync` en `ActualizarAsync` (captura `legajoAnterior` antes del `CambiarDatos`, emite sólo si transición es no-nulo → null).
  - `src/SGV.Aplicacion/Auditoria/NoopAuditoriaServicio.cs` (NEW, internal): default no-op para el ctor de back-compat.
  - `src/SGV.Aplicacion/Seguridad/NullUsuarioActual.cs` (NEW, internal): default null para el ctor de back-compat.
  - Ctor de back-compat `(IPersonaRepository, IUnitOfWork)` ahora cablea `NoopAuditoriaServicio` + `NullUsuarioActual` + los validators reales, manteniendo los 11 tests previos verdes.
  - Resultado: 20/20 `PersonaServicioComandosTests` pasando (16 previos + 4 nuevos).
  - Side-check: 7/7 `SetupServicio*` tests pasando (cambio de wire-type sin regresiones en `SetupServicio`).

### Phase 3 — Normalización web (TDD)

- ✅ **3.1 RED**: `EditPageTests.Post_Edit_WhenLegajoWhitespace_NormalizaANullAntesDeApi` — POST con `Input.Legajo="   "` falla con 500 (NRE en `Input.Legajo.Trim()` cuando la wire-type ahora es `string?`).
- ✅ **3.2 GREEN**: `src/SGV.Web/Pages/Personas/Create.cshtml.cs:111` — `var legajoNormalizado = string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo.Trim()` antes del ctor de `CrearPersonaRequest`.
- ✅ **3.3 GREEN**:
  - `src/SGV.Web/Pages/Personas/Edit.cshtml.cs:118` (pre-carga GET): `Input.Legajo = persona.Legajo` (sin `?? string.Empty`; ahora `string?` acepta null directo).
  - `src/SGV.Web/Pages/Personas/Edit.cshtml.cs:174` (POST): misma normalización `string.IsNullOrWhiteSpace → null` que Create.
- Ajuste inevitable en test existente: `EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm` ajustó su payload y mensaje (Legajo es opcional → no podemos esperar el error "legajo obligatorio" desde el cliente; ajustamos a "El legajo ya está en uso" para que el backend lo emita y `PersonaPostResultMapper` lo mapee correctamente). Apellidos sigue siendo `[Required]`.
- Resultado: 106/106 tests web Persona/Create/Edit pasando (54 previos + 1 nuevo + ajustes 1 existente).

### Phase 4 — Tests de integración

- ✅ **4.1** `PersonaApiClientBasicTests`: 3 tests seam —
  - `CreateAsync_LegajoNull_SerializaLegajoNull` (assert `ValueKind == Null` en JSON)
  - `CreateAsync_LegajoVacio_SerializaLegajoVacio` (assert `ValueKind == String` con `""`)
  - `UpdateAsync_LegajoConEspaciosNoTrimeaCliente` (assert body contiene `"legajo":"  L-7  "` literal)
- ✅ **4.2** `PersonasControllerTests`: 3 tests API —
  - `Post_LegajoNullEnBody_Retorna201ConLegajoNull` (assert 201 + `PersonaDto.Legajo==null`)
  - `Put_LimpiarLegajo_Retorna200YRegistraUpdateLegajo` (fake captura invocación; assert 200)
  - `Put_LegajoSinClave_Retorna200` (body omite `legajo`; assert 200)
- ✅ **4.3** `PersonaRepositoryTests.PersistirPersona_LegajoNull_LecturaPosterior` (`[MySqlFact]`): persiste `Persona(...,legajo:null,...)`, verifica round-trip tanto via dominio (`repo.GetByIdAsync`) como via EF entity crudo (`context.Set<PersonaEntity>().AsNoTracking().FirstOrDefaultAsync`).
- ✅ **4.4** `dotnet test SGV.slnx` → **2948/2948 passing, 0 skipped**. MySQL alcanzable; todos los `[MySqlFact]` corrieron.

### Phase 5 — UI warning slot + Verificación final

- ✅ **5.1** `src/SGV.Web/Pages/Personas/_Form.cshtml`: agregado `<span class="text-warning small" data-legajo-context-warning hidden></span>` debajo del campo Legajo. Cuando `IPersonaForm.ShowLegajoContextWarning == true`, el span muestra "Este legajo se utiliza en flujos que lo requieren." Por defecto (Create/Edit) el span está vacío y oculto.
- ✅ **5.2** `dotnet build SGV.slnx` → 0 errores. `dotnet test SGV.slnx` → 2948/2948.
- ✅ **5.3** `bun run build` en `src/SGV.Web` → OK (`gulp build` terminó sin errores; sin impacto en assets frontend).
- ✅ **5.4** Smoke manual: el binding end-to-end queda verificado por los 7 tests de `PersonaApiClientBasicTests` (seam HTTP) + `PersonasControllerTests` (API integration con `WebApplicationFactory`) + `PersonaRepositoryTests.PersistirPersona_LegajoNull_LecturaPosterior` (MySQL round-trip). No se requiere browser smoke porque el cambio en `_Form.cshtml` es puramente aditivo (slot hidden).

## TDD Cycle Evidence

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1+2.2 | `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioComandosTests.cs` | Unit | ✅ 16/16 | ✅ Compile error | ✅ 20/20 | ✅ 4 casos | ✅ Clean |
| 3.1+3.2+3.3 | `tests/SGV.Tests/Web/Persona/EditPageTests.cs` | Web integration | ✅ 54/54 | ✅ 500 NRE | ✅ 106/106 | ✅ Single + ajuste 1 | ✅ Clean |
| 4.1 | `tests/SGV.Tests/Web/Persona/PersonaApiClientBasicTests.cs` | Web seam | ✅ 30/30 | ✅ n/a (new shape) | ✅ 33/33 | ✅ 3 casos | ✅ Clean |
| 4.2 | `tests/SGV.Tests/Api/PersonasControllerTests.cs` | API integration | ✅ 54/54 | ✅ n/a (new shape) | ✅ 57/57 | ✅ 3 casos | ✅ Clean |
| 4.3 | `tests/SGV.Tests/Persistencia/PersonaRepositoryTests.cs` | MySQL integration | ✅ pre-existing | ✅ n/a (new shape) | ✅ 1/1 | ➖ Single | ✅ Clean |

### Test Summary
- **Total tests**: 2948
- **Total tests passing**: 2948
- **Tests added in this change**: 12 (4 audit + 1 web normalization + 3 HTTP seam + 3 API integration + 1 MySQL repo)
- **Tests adjusted in this change**: 1 (`EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm`)
- **Layers used**: Unit (4), Web integration (2), HTTP seam (3), API integration (3), MySQL integration (1)
- **Approval tests** (refactoring): None — purely additive
- **Pure functions created**: 0 (todo el trabajo toca orquestación / DTO / flujo; no se introdujeron pure helpers nuevos)

## Commits en la rama `feat/issue-202-legajo-nulo-personas`

```
e1a9f2d feat(personas): allow nullable Legajo in wire, InputModel and Setup
7ccf585 docs(apply): mark Phase 1 tasks complete
5de40c7 feat(personas): emit explicit UpdateLegajo audit on null transition
c557bd1 docs(apply): mark Phase 2 tasks complete
dad6d7f feat(web): normalize Legajo whitespace to null on Personas pages
fc7eda5 docs(apply): mark Phase 3 tasks complete
c893ca9 test(personas): add integration coverage for nullable Legajo
f264acc docs(apply): mark Phase 4 tasks complete
95305bb feat(web): add hidden Legajo context warning slot in Personas _Form
```

## Archivos modificados (work-unit commits)

| Archivo | Acción | Cambio |
|---------|--------|--------|
| `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs` | Modified | `string Legajo` → `string? Legajo` |
| `src/SGV.Web/Integration/Personas/PersonaInputModel.cs` | Modified | Sin `[Required]`, length 50, `string?` |
| `src/SGV.Web/Integration/Personas/IPersonaForm.cs` | Modified | `bool ShowLegajoContextWarning { get; }` |
| `src/SGV.Web/Pages/Personas/Create.cshtml.cs` | Modified | Normalización whitespace → null |
| `src/SGV.Web/Pages/Personas/Edit.cshtml.cs` | Modified | Normalización POST + pre-carga simplificada |
| `src/SGV.Web/Pages/Personas/_Form.cshtml` | Modified | Slot warning contextual |
| `src/SGV.Infraestructura/Setup/SetupServicio.cs` | Modified | Sin `?? string.Empty` |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs` | Modified | Audit block + nuevos ctor args |
| `src/SGV.Aplicacion/Auditoria/NoopAuditoriaServicio.cs` | Created | Default no-op para back-compat |
| `src/SGV.Aplicacion/Seguridad/NullUsuarioActual.cs` | Created | Default null para back-compat |
| `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioComandosTests.cs` | Modified | 4 tests + fakes |
| `tests/SGV.Tests/Web/Persona/EditPageTests.cs` | Modified | 1 test + ajuste 1 existente |
| `tests/SGV.Tests/Web/Persona/PersonaApiClientBasicTests.cs` | Modified | 3 tests seam |
| `tests/SGV.Tests/Api/PersonasControllerTests.cs` | Modified | 3 tests API |
| `tests/SGV.Tests/Persistencia/PersonaRepositoryTests.cs` | Modified | 1 `[MySqlFact]` |
| `openspec/changes/permitir-legajo-nulo-personas-issue-202/tasks.md` | Modified | Marcado progresivo |

## Desviaciones del design

### D1. Ajuste en `EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm`

El test original (PRECAMBIOS) enviaba `Input.Legajo=""` y esperaba que el backend respondiera con `"El legajo es obligatorio"` mapeado al span. Esto dependía del cliente `[Required]` que cortocircuita ModelState y renderea ese mensaje client-side ANTES de llamar al backend. Tras relajar el cliente (sin `[Required]`), ese mensaje ya no se genera. El test ahora envía `Input.Legajo="L-001"` (no vacío) y deja que el backend emita un error de campo distinto (`El legajo ya está en uso`) que sí ejercita el path de mapping de `PersonaPostResultMapper`. Apellidos sigue siendo `[Required]` y se valida con un mensaje backend-alcanzable (`Los apellidos no cumplen el formato`).

**Impacto funcional**: ninguno — el contrato observable del usuario es el mismo (FieldErrors del backend se renderean bajo los data-valmsg-for correctos).

### D2. `_Form.cshtml` slot condicional

El diseño proponía un slot hidden por default que el módulo downstream activa vía `IPersonaForm.ShowLegajoContextWarning = true`. Implementé el slot como dos ramas:
- `ShowLegajoContextWarning == true` → `<span class="text-warning small" data-legajo-context-warning>Este legajo se utiliza en flujos que lo requieren.</span>` (visible, con texto).
- `ShowLegajoContextWarning == false` (default) → `<span class="text-warning small" data-legajo-context-warning hidden></span>` (vacío y oculto).

**Impacto funcional**: idéntico al diseño; sólo agrega el atributo `hidden` para que el span no ocupe espacio cuando está vacío.

## Hallazgos no triviales

1. **`Trim()` con `string?` rompe en runtime** — El test RED de Phase 3 falló con `500 InternalServerError` porque `Input.Legajo.Trim()` lanzaba NRE cuando el cliente enviaba `"   "`. La transición a `string?` requiere normalización explícita `string.IsNullOrWhiteSpace → null`. Patrón ya vigente para `Email`/`NumeroDocumento`/`Telefono`; lo extendimos a `Legajo`.
2. **`PersonaPostResultMapper` sólo se invoca si ModelState pasa** — Al quitar `[Required]` de `Legajo`, el test pre-existente `Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm` enviaba `Input.Apellidos=""` que AÚN es `[Required]`. ModelState fallaba antes de llegar al API, por lo que el fake del backend nunca se invocaba y el mapeo de field-errors del backend quedaba inerte. Por eso ajustamos ese test.
3. **`IUsuarioActual` ausente en los 11 tests previos de `PersonaServicioComandosTests`** — El ctor de back-compat `(IPersonaRepository, IUnitOfWork)` cablea `NullUsuarioActual` internamente. Cero cambios en los 11 tests previos; pasan sin tocar. Sin esta decisión, el scope del change se habría inflado innecesariamente a refactorizar todos los tests.
4. **MySQL alcanzable en `localhost:3306`** — el `[MySqlFact] PersistirPersona_LegajoNull_LecturaPosterior` corrió contra `sgv_test` real. Si MySQL NO hubiera estado disponible, el test se habría skipeado limpio (sin afectar el resto de la suite).

## Líneas aproximadas (rough)

- **Creadas**: ~75 líneas (`NoopAuditoriaServicio.cs` 23, `NullUsuarioActual.cs` 19, helpers fakes en `PersonaServicioComandosTests.cs` ~33)
- **Modificadas**: ~295 líneas
  - `PersonaRequests.cs` ~12
  - `PersonaInputModel.cs` ~10
  - `IPersonaForm.cs` ~10
  - `Create.cshtml.cs` ~9
  - `Edit.cshtml.cs` ~10
  - `_Form.cshtml` ~15
  - `SetupServicio.cs` ~1
  - `PersonaServicioComandos.cs` ~43
  - `PersonaServicioComandosTests.cs` ~156 (4 tests + fakes + helpers)
  - `EditPageTests.cs` ~61 (1 test + ajuste 1)
  - `PersonaApiClientBasicTests.cs` ~71 (3 tests)
  - `PersonasControllerTests.cs` ~98 (3 tests)
  - `PersonaRepositoryTests.cs` ~41 (1 `[MySqlFact]`)
  - `tasks.md` ~20 (marcado progresivo)
- **Net diff**: ~+370 líneas — dentro del budget de 400 (alineado con forecast "~340" del tasks.md).

## Estado final

- `dotnet build SGV.slnx`: ✅ 0 errors
- `dotnet test SGV.slnx`: ✅ **2948/2948 passing, 0 skipped** (MySQL disponible confirmado en `localhost:3306`; los `[MySqlFact]` corrieron en vez de skipearse)
- `bun run build` (en `src/SGV.Web`): ✅ OK
- `setup-admin-inicial-issue-195/`: ✅ NO modificado (mtime `Jul 26 14:03:23 2026` preservado)
- Próximo paso: el orquestador hace commit + abre PR único sobre `develop` (sin chained PRs; review budget ~370 < 400).