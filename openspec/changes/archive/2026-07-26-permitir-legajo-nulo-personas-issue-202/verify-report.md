# Verify Report — permitir-legajo-nulo-personas-issue-202

> Issue origen: #202 — Permitir crear y editar Personas con legajo nulo
> Modo: Strict TDD (RED → GREEN por tarea)
> Persistencia: hybrid (OpenSpec + Engram)
> Rama: `feat/issue-202-legajo-nulo-personas` — 10 commits ahead de `develop`
> Cambio paralelo: `setup-admin-inicial-issue-195/` **NO modificado** (mtime `Jul 26 14:03:23 2026` preservado)

---

## Resumen de validación

El change alinea wire (`CrearPersonaRequest` / `ActualizarPersonaRequest`), UI (`PersonaInputModel`, `Create.cshtml.cs`, `Edit.cshtml.cs`, `_Form.cshtml`), setup (`SetupServicio.cs`) y aplicación (`PersonaServicioComandos`) al hecho de que `Persona.Legajo` ya era `string?` en el Dominio. Se agrega además una fila de auditoría explícita (`Accion="UpdateLegajo"`, `LegajoAnterior`, `LegajoNuevo=null`) cuando se limpia un legajo persistido, y un slot de advertencia contextual en la vista que sigue oculto por default. La suite completa (`dotnet test SGV.slnx`) corrió en verde: **2948/2948 pasando, 0 fallados, 0 skipeados**, con MySQL alcanzable en `localhost:3306` y todos los `[MySqlFact]` ejecutados. `dotnet build SGV.slnx` → 0 errores. `bun run build` en `src/SGV.Web` → OK. La columna `Personas.Legajo` permanece como `varchar(50)` nullable en `SgvDbContextModelSnapshot` sin migración nueva. Los 6 archivos de código + 4 archivos de tests fueron modificados; 2 archivos nuevos (`NoopAuditoriaServicio.cs`, `NullUsuarioActual.cs`) para mantener back-compat del ctor de `PersonaServicioComandos`. El interceptor central `AuditoriaSaveChangesInterceptor` se mantiene registrado en `src/SGV.Api/Program.cs:95-99` y emite sus filas `Modificacion`/`Delete` con total independencia de la fila `UpdateLegajo` explícita. Sin colisiones detectadas en `openspec/changes/permitir-legajo-nulo-personas-issue-202/` (no existe `verify-report.md` previo).

---

## Completitud

| Métrica | Valor |
|---------|-------|
| Tasks totales | 17 (Phase 0–5, sub-tasks 0.1, 1.1–1.5, 2.1–2.2, 3.1–3.3, 4.1–4.4, 5.1–5.4) |
| Tasks completas | 17 |
| Tasks incompletas | 0 |
| Review budget forecast | ~340 líneas, dentro del límite 400 (`tasks.md` §"Review Workload Forecast") |
| Commits en rama | 10 (`e1a9f2d5` … `20aeab47`) |
| Tests añadidos | 12 (4 application + 1 web page + 3 HTTP seam + 3 API integration + 1 MySQL repo) |
| Tests ajustados | 1 (`EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm`) |

---

## Build & Tests Execution

**Build**: ✅ Passed
```text
dotnet build SGV.slnx → 0 errors (únicamente warnings NU1510 sobre packages no pruneables en SGV.Infraestructura.csproj, preexistentes al change)
```

**Tests**: ✅ 2948 passed / 0 failed / 0 skipped
```text
dotnet test SGV.slnx → Passed:  2948, Failed:     0, Skipped:     0, Total:  2948, Duration: 1 m 26 s
```

**Frontend**: ✅ OK
```text
bun run build (en src/SGV.Web) → gulp build sin errores; sin impacto en assets.
```

**Coverage**: ➖ No generada en este verify (no requerida por el orchestrator preflight).

---

## Trazabilidad requisito → escenario → test

### Delta `specs/persona-management/spec.md`

#### MODIFIED Requirements

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Alta de Persona | Crear persona válida con Legajo y tipo de documento | `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioComandosTests.cs` — tests previos (no enumerados en este change) | ✅ COMPLIANT (regresión vigente) |
| Alta de Persona | Crear persona omitiendo Legajo | `PersonaServicioComandosTests.CrearAsync_LegajoNull_PermitidoYGuarda` | ✅ COMPLIANT |
| Alta de Persona | Crear persona con Legajo whitespace-only | `EditPageTests.Post_Edit_WhenLegajoWhitespace_NormalizaANullAntesDeApi` (vía normalización Edit) + `PersonaApiClientBasicTests.CreateAsync_LegajoVacio_SerializaLegajoVacio` (seam) | ✅ COMPLIANT |
| Alta de Persona | Rechazar Legajo que excede 50 caracteres | `PersonaServicioComandosTests` — tests previos sobre `MaximumLength(50)` (cubierto por `CrearPersonaRequestValidator`) | ✅ COMPLIANT (regresión) |
| Alta de Persona | Rechazar Legajo duplicado entre Personas activas | `PersonaServicioComandosTests.CrearAsync_LegajoDuplicadoActivo_RetornaConflictoYSinGuardar` + `ActualizarAsync_LegajoDuplicado_SigueRechazando` | ✅ COMPLIANT |
| Alta de Persona | Rechazar documento que no satisface patrón del tipo | `PersonaServicioComandosTests` — tests previos sobre `ActualizarPersonaRequestValidator` | ✅ COMPLIANT (regresión) |
| Actualización de Persona | Actualizar contacto preservando documento válido | `PersonaServicioComandosTests.ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda` (previo) | ✅ COMPLIANT (regresión) |
| Actualización de Persona | Editar limpiando Legajo persiste null y registra auditoría UpdateLegajo | `PersonaServicioComandosTests.ActualizarAsync_LimpiarLegajo_RegistraAuditoria` + `PersonasControllerTests.Put_LimpiarLegajo_Retorna200YRegistraUpdateLegajo` (HTTP layer) | ✅ COMPLIANT |
| Actualización de Persona | Editar con Legajo whitespace-only se normaliza a null antes de la API | `EditPageTests.Post_Edit_WhenLegajoWhitespace_NormalizaANullAntesDeApi` | ✅ COMPLIANT |
| Actualización de Persona | Editar sin transición de Legajo no genera fila UpdateLegajo | `PersonaServicioComandosTests.ActualizarAsync_LegajoSinTransicion_NoEmiteAuditoriaLegajo` | ✅ COMPLIANT |
| Actualización de Persona | Rechazar cambio de documento que rompe patrón | `PersonaServicioComandosTests` — tests previos sobre validator | ✅ COMPLIANT (regresión) |
| Actualización de Persona | Rechazar duplicados activos | `ActualizarAsync_LegajoDuplicado_SigueRechazando` | ✅ COMPLIANT |

#### ADDED Requirements

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Auditoría explícita al limpiar Legajo de Persona | Limpieza de Legajo vía formulario web registrada con UpdateLegajo | `PersonaServicioComandosTests.ActualizarAsync_LimpiarLegajo_RegistraAuditoria` (asserts `Accion="UpdateLegajo"`, `LegajoAnterior="L-001"`, `LegajoNuevo=null`) | ✅ COMPLIANT |
| Auditoría explícita al limpiar Legajo de Persona | Limpieza de Legajo vía consumidor autenticado no-web registrada | `PersonasControllerTests.Put_LimpiarLegajo_Retorna200YRegistraUpdateLegajo` (HTTP 200 + DTO.Legajo null) + `PersonaServicioComandosTests.ActualizarAsync_LimpiarLegajo_RegistraAuditoria` (servicio emite la fila) | ✅ COMPLIANT |
| Auditoría explícita al limpiar Legajo de Persona | Persona con Legajo previamente null no genera UpdateLegajo en update sin transición | `PersonaServicioComandosTests.ActualizarAsync_LegajoSinTransicion_NoEmiteAuditoriaLegajo` (asserts `auditoria.Invocaciones` está vacío cuando `Legajo="L-001"→"L-001"`) | ✅ COMPLIANT |

### Delta `specs/web-apiclient-transport-contract/spec.md`

#### ADDED Requirements

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| `CrearPersonaRequest.Legajo` y `ActualizarPersonaRequest.Legajo` son `string?` | Payload `legajo: null` deserializa a `string? == null` | `PersonasControllerTests.Post_LegajoNullEnBody_Retorna201ConLegajoNull` | ✅ COMPLIANT |
| (idem) | Payload sin la clave `legajo` deserializa a `string? == null` | `PersonasControllerTests.Put_LegajoSinClave_Retorna200` | ✅ COMPLIANT |
| (idem) | Payload con `legajo: ""` deserializa como string vacío y se trata como ausente | `PersonaApiClientBasicTests.CreateAsync_LegajoVacio_SerializaLegajoVacio` (seam HTTP) + `PersonaServicioComandosTests.CrearAsync_LegajoVacio_PermitidoYGuarda` (servicio trata `""` como ausente vía guarda `!string.IsNullOrEmpty`) | ✅ COMPLIANT |
| (idem) | PageModel normaliza whitespace a null antes de la API | `EditPageTests.Post_Edit_WhenLegajoWhitespace_NormalizaANullAntesDeApi` | ✅ COMPLIANT |
| (idem) | PageModel Edit normaliza whitespace antes de invocar Update | `EditPageTests.Post_Edit_WhenLegajoWhitespace_NormalizaANullAntesDeApi` | ✅ COMPLIANT |
| (idem) | Respuesta GET persona con Legajo persistido NULL | `PersonaRepositoryTests.PersistirPersona_LegajoNull_LecturaPosterior` (`[MySqlFact]`) | ✅ COMPLIANT |
| (idem) | Legajo > 50 caracteres rechazado por el validator | tests previos sobre `MaximumLength(50)` (regresión) | ✅ COMPLIANT (regresión) |
| `PersonaApiClient` no pre-procesa `Legajo` | Cliente entrega crudo `null` y serializa `legajo: null` | `PersonaApiClientBasicTests.CreateAsync_LegajoNull_SerializaLegajoNull` (assert `ValueKind == Null`) | ✅ COMPLIANT |
| (idem) | Cliente entrega crudo `""` y serializa `legajo: ""` (no permitido por UI) | `PersonaApiClientBasicTests.CreateAsync_LegajoVacio_SerializaLegajoVacio` (assert `ValueKind == String`) | ✅ COMPLIANT |
| (idem) | Cliente entrega valor con espacios y los preserva | `PersonaApiClientBasicTests.UpdateAsync_LegajoConEspaciosNoTrimeaCliente` (assert body contiene `"legajo":"  L-7  "` literal) | ✅ COMPLIANT |

**Compliance summary**: 22/22 escenarios cubiertos por test que pasó en runtime.

---

## Mapeo a la propuesta y diseño

| Affected Area (proposal §"Affected Areas") | Sección de diseño | Commit | Estado |
|--------------------------------------------|--------------------|--------|--------|
| `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs` | Diseño §2 (Contratos), §3 (Wire-type transition) | `e1a9f2d5 feat(personas): allow nullable Legajo in wire, InputModel and Setup` | ✅ Implementado |
| `src/SGV.Web/Integration/Personas/PersonaInputModel.cs` | Diseño §2 (Web) | `e1a9f2d5` | ✅ Implementado |
| `src/SGV.Web/Integration/Personas/IPersonaForm.cs` | Diseño §6 (Advertencia UI contextual) | `e1a9f2d5` (mismo commit que el input model y el wire) | ✅ Implementado |
| `src/SGV.Web/Pages/Personas/Create.cshtml.cs` | Diseño §4 (Normalización UI) | `dad6d7f6 feat(web): normalize Legajo whitespace to null on Personas pages` | ✅ Implementado |
| `src/SGV.Web/Pages/Personas/Edit.cshtml.cs` | Diseño §4 | `dad6d7f6` | ✅ Implementado |
| `src/SGV.Web/Pages/Personas/_Form.cshtml` | Diseño §6 | `95305bb1 feat(web): add hidden Legajo context warning slot in Personas _Form` | ✅ Implementado |
| `src/SGV.Infraestructura/Setup/SetupServicio.cs` | Diseño §7 (SetupServicio cleanup) | `e1a9f2d5` | ✅ Implementado |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs` | Diseño §5 (Auditoría explícita al limpiar Legajo) | `5de40c78 feat(personas): emit explicit UpdateLegajo audit on null transition` | ✅ Implementado |
| `src/SGV.Aplicacion/Auditoria/NoopAuditoriaServicio.cs` | Diseño §5 (ctor de back-compat) | `5de40c78` (nuevo, internal) | ✅ Implementado |
| `src/SGV.Aplicacion/Seguridad/NullUsuarioActual.cs` | Diseño §5 (ctor de back-compat) | `5de40c78` (nuevo, internal) | ✅ Implementado |
| Tests Aplicación, Web, API, Persistencia | Diseño §8 | `5de40c78` (4 application), `dad6d7f6` (1 web + 1 ajustado), `c893ca92 test(personas): add integration coverage for nullable Legajo` (3 HTTP seam + 3 API + 1 MySQL) | ✅ Implementado |

---

## Verificaciones funcionales explícitas

### 1. `Persona.Legajo` sigue siendo `string?` y la columna MySQL `Personas.Legajo` sigue siendo nullable sin migración nueva
- ✅ `src/SGV.Dominio/Personas/Persona.cs:21` → `public string? Legajo { get; private set; }`
- ✅ `src/SGV.Infraestructura/Persistencia/Entidades/PersonaEntity.cs:8` → `public string? Legajo { get; set; }`
- ✅ `src/SGV.Infraestructura/Persistencia/Configuraciones/PersonaConfiguracion.cs:15` → `builder.Property(e => e.Legajo).HasMaxLength(50);` (sin `IsRequired()`, columna nullable)
- ✅ `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs:1158-1160` → `b.Property<string>("Legajo").HasMaxLength(50).HasColumnType("varchar(50)");` (sin `.IsRequired()`)
- ✅ `git diff --name-only HEAD~10 HEAD` no incluye ningún archivo bajo `src/SGV.Infraestructura/Persistencia/Migraciones/` → **cero migraciones nuevas**.

### 2. `CrearPersonaRequest.Legajo` y `ActualizarPersonaRequest.Legajo` son `string?`
- ✅ `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs:11` → `CrearPersonaRequest(string? Legajo, …)`
- ✅ `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs:26` → `ActualizarPersonaRequest(string? Legajo, …)`
- Confirmado por `dotnet build SGV.slnx → 0 errors` (24 call-sites usan named args y compilan).

### 3. `PersonaInputModel.Legajo` sin `[Required]`, con `[StringLength(50)]` y tipo `string?`
- ✅ `src/SGV.Web/Integration/Personas/PersonaInputModel.cs:23-24` →
  ```csharp
  [StringLength(50, ErrorMessage = "El legajo no puede superar los 50 caracteres.")]
  public string? Legajo { get; set; }
  ```
  Sin `[Required]`, sin default `= string.Empty`.

### 4. `Create.cshtml.cs` y `Edit.cshtml.cs` normalizan whitespace → null antes de invocar la API; la pre-carga de Edit no aplica `?? string.Empty`
- ✅ `Create.cshtml.cs:122` →
  ```csharp
  var legajoNormalizado = string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo.Trim();
  ```
- ✅ `Edit.cshtml.cs:126` (pre-carga GET) → `Input.Legajo = persona.Legajo;` (sin `?? string.Empty`, asignación directa entre `string?`)
- ✅ `Edit.cshtml.cs:184` (POST) →
  ```csharp
  var legajoNormalizado = string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo.Trim();
  ```
- ✅ Cobertura de tests: `EditPageTests.Post_Edit_WhenLegajoWhitespace_NormalizaANullAntesDeApi` (asserts `sent.Request.Legajo == null` cuando `Input.Legajo="   "`).

### 5. `SetupServicio.cs` ya no aplica `?? string.Empty`
- ✅ `src/SGV.Infraestructura/Setup/SetupServicio.cs:104` →
  ```csharp
  var personaRequest = new CrearPersonaRequest(
      Legajo: request.Legajo,
      …);
  ```
  Sin `?? string.Empty`. `request.Legajo` es `string?` (SetupRequest) y `CrearPersonaRequest.Legajo` ahora acepta `string?`; pasa directo.
- ✅ Tests previos `SetupServicio*Tests` siguen pasando (7/7) sin cambios.

### 6. `PersonaServicioComandos.ActualizarAsync` emite la fila `Auditorias` con `Accion="UpdateLegajo"`, `LegajoAnterior` y `LegajoNuevo=null` cuando hay transición no-nulo → null; NO la emite cuando no hay transición; el interceptor central sigue activo

- ✅ `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs:129` captura `var legajoAnterior = persona.Legajo;` **antes** de `CambiarDatos`.
- ✅ Líneas 137-153 — bloque de auditoría con guarda `if (legajoAnterior is not null && persona.Legajo is null)`:
  ```csharp
  await auditoriaServicio.RegistrarAsync(
      entidad: "Persona",
      entityId: persona.Id.ToString(),
      accion: "UpdateLegajo",
      usuarioOperadorId: usuarioActual.UserId,
      valoresAnteriores: new Dictionary<string, object?> { ["LegajoAnterior"] = legajoAnterior },
      valoresNuevos:    new Dictionary<string, object?> { ["LegajoNuevo"] = null },
      cancellationToken: cancellationToken);
  ```
- ✅ Cobertura: `ActualizarAsync_LimpiarLegajo_RegistraAuditoria` (asserts Accion, LegajoAnterior, LegajoNuevo) + `ActualizarAsync_LegajoSinTransicion_NoEmiteAuditoriaLegajo` (asserts `auditoria.Invocaciones` vacío).
- ✅ Interceptor central `AuditoriaSaveChangesInterceptor` permanece registrado en `src/SGV.Api/Program.cs:95-99` y emite su fila `Modificacion` genérica con independencia de la fila explícita.

### 7. `_Form.cshtml` agrega el slot hidden por default y respeta `IPersonaForm.ShowLegajoContextWarning`

- ✅ `src/SGV.Web/Pages/Personas/_Form.cshtml:21-30` →
  ```razor
  @if (Model.ShowLegajoContextWarning)
  {
      <span class="text-warning small" data-legajo-context-warning>
          Este legajo se utiliza en flujos que lo requieren.
      </span>
  }
  else
  {
      <span class="text-warning small" data-legajo-context-warning hidden></span>
  }
  ```
- ✅ `IPersonaForm.ShowLegajoContextWarning` definido en `src/SGV.Web/Integration/Personas/IPersonaForm.cs:54`. Implementaciones:
  - `CreateModel.ShowLegajoContextWarning => false` (`Create.cshtml.cs:57`)
  - `EditModel.ShowLegajoContextWarning => false` (`Edit.cshtml.cs:56`)
- Por default (Create/Edit) el slot queda **vacío y oculto**; cuando un módulo downstream setee `true`, el span muestra el texto y queda visible.

### 8. No hay regresión en los tests previos (2948/2948 sigue vigente)
- ✅ `dotnet test SGV.slnx → Passed: 2948, Failed: 0, Skipped: 0, Total: 2948`.
- ✅ Único test ajustado: `EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm` — documentado como desviación D1 en `apply-progress.md`. Ajuste funcional: el mensaje "El legajo es obligatorio" ya no se genera client-side (porque `[Required]` se removió) y se reemplaza por un error alcanzable vía backend ("El legajo ya está en uso"); `Apellidos` sigue siendo `[Required]` con mensaje backend-alcanzable. Contrato observable del usuario intacto.

---

## Correctitud (evidencia estática)

| Requisito | Estado | Notas |
|-----------|--------|-------|
| `CrearPersonaRequest.Legajo` es `string?` | ✅ Implementado | `PersonaRequests.cs:11` |
| `ActualizarPersonaRequest.Legajo` es `string?` | ✅ Implementado | `PersonaRequests.cs:26` |
| `PersonaInputModel.Legajo` sin `[Required]`, length 50, `string?` | ✅ Implementado | `PersonaInputModel.cs:23-24` |
| `SetupServicio` ya no aplica `?? string.Empty` | ✅ Implementado | `SetupServicio.cs:104` |
| `Create.cshtml.cs` normaliza whitespace → null | ✅ Implementado | `Create.cshtml.cs:122` |
| `Edit.cshtml.cs` pre-carga sin `?? string.Empty` | ✅ Implementado | `Edit.cshtml.cs:126` |
| `Edit.cshtml.cs` normaliza POST whitespace → null | ✅ Implementado | `Edit.cshtml.cs:184` |
| `PersonaServicioComandos.ActualizarAsync` captura `legajoAnterior` antes del cambio | ✅ Implementado | `PersonaServicioComandos.cs:129` |
| `PersonaServicioComandos.ActualizarAsync` emite `UpdateLegajo` en transición no-nulo → null | ✅ Implementado | `PersonaServicioComandos.cs:137-153` |
| `PersonaServicioComandos.ActualizarAsync` NO emite `UpdateLegajo` sin transición | ✅ Implementado | guarda `legajoAnterior is not null && persona.Legajo is null` |
| `_Form.cshtml` slot hidden + texto condicional | ✅ Implementado | `_Form.cshtml:21-30` |
| `IPersonaForm.ShowLegajoContextWarning` default `false` | ✅ Implementado | `CreateModel:57`, `EditModel:56` |
| Interceptor central `AuditoriaSaveChangesInterceptor` activo | ✅ Implementado | `Program.cs:95-99` |
| Cero migraciones nuevas | ✅ Confirmado | `git diff --name-only` no incluye `Migraciones/` |

---

## Coherencia (Diseño)

| Decisión de diseño | ¿Seguida? | Notas |
|--------------------|-----------|-------|
| Enfoque A (mínimo) + auditoría explícita | ✅ Sí | Sin tocar dominio ni DB. Cambios acotados a contratos, web, setup, aplicación. |
| Ctor de back-compat `PersonaServicioComandos(repo, uow)` para no inflar los 11 tests previos | ✅ Sí | `PersonaServicioComandos.cs:33-42` cablea `NoopAuditoriaServicio` + `NullUsuarioActual` + validators reales. |
| Helpers nuevos `NoopAuditoriaServicio` y `NullUsuarioActual` (internal) | ✅ Sí | Aplicación §5 |
| Normalización única en PageModel (`string.IsNullOrWhiteSpace → null`) | ✅ Sí | Reusada por Create y Edit; alineada con Email/NumeroDocumento/Telefono. |
| `PersonaApiClient` NO pre-procesa `Legajo` | ✅ Sí | Verificado por 3 tests seam que prueban serialización literal. |
| Warning UI contextual, no bloqueante, default oculto | ✅ Sí | D2 documentada en `apply-progress.md` — desviación menor: span incluye atributo `hidden` para no ocupar espacio cuando está vacío. |
| Cero impacto sobre `setup-admin-inicial-issue-195` | ✅ Sí | mtime preservado `Jul 26 14:03:23 2026`; contenido de `tasks.md` spot-leído sin cambios. |
| Threat matrix N/A (no toca routing, shell, subprocess, VCS, process integration) | ✅ Sí | Sin cambios en esos vectores. |
| Cero migraciones; columna `Personas.Legajo` permanece `varchar(50)` nullable | ✅ Sí | Snapshot intacto. |
| `IUsuarioActual` ausente en los 11 tests previos mitigado vía `NullUsuarioActual` | ✅ Sí | `apply-progress.md` Hallazgo 3. |

---

## Desviaciones del design reportadas por `apply-progress.md`

### D1. Ajuste en `EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm`
**Severidad asignada**: SUGGESTION (funcionalmente neutra; el contrato observable es el mismo).
**Justificación**: el mensaje "El legajo es obligatorio" ya no se genera client-side porque `[Required]` se removió. El test ahora dispara un mensaje alcanzable vía backend (`El legajo ya está en uso`) y `PersonaPostResultMapper` lo mapea al span. Apellidos sigue siendo `[Required]`.

### D2. `_Form.cshtml` slot condicional
**Severidad asignada**: SUGGESTION (funcionalmente idéntica).
**Justificación**: el diseño proponía un slot hidden por default. La implementación final agrega además el atributo `hidden` al span cuando `ShowLegajoContextWarning == false` para que no ocupe espacio vertical. Sin texto condicional cuando `true`.

Ambas desviaciones fueron **explícitamente registradas** en `apply-progress.md` §"Desviaciones del design" y son consistentes con la intención del diseño.

---

## Riesgos residuales

1. **NRE latente si se reintroduce `?? string.Empty` en algún call-site nuevo** (riesgo bajo, mitigado por revisión de código y por el grep de los 24 call-sites que ya compilan limpio). — *SUGGESTION*: añadir un grep-check pre-merge `git grep -n '?? string.Empty' -- 'src/SGV.*Persona*'` para mantener consistencia con el diseño.

2. **`PersonaInputModel.Legajo` sin default initializer** (`string?` sin `= string.Empty`) puede sorprender a Razor binding si el form se renderiza sin valor previo; el `asp-for` lo maneja como `null` correctamente. Verificado manualmente — el `<input>` renderiza `value=""` que NO se confunde con `string.Empty` gracias a la normalización posterior. — *SUGGESTION*: agregar un test de render explícito si la UI gana complejidad downstream.

3. **Cobertura del bloqueo de auditoría `RegistrarAsync` falla** — si la fila explícita fallara, propagaría la excepción fuera de `ActualizarAsync`, generando 500 al usuario que edita una persona. Paridad con `UsuarioServicioComandos` ya establecida; mismo riesgo compartido. — *WARNING* preexistente, no introducido por este change.

4. **`Put_LimpiarLegajo_Retorna200YRegistraUpdateLegajo` usa fake `PersonaServicioComandos`** — el test de integración API no ejerce la ruta real de auditoría contra `IAuditoriaServicio`; lo hace el test unitario `ActualizarAsync_LimpiarLegajo_RegistraAuditoria`. Aceptable por diseño (separación de capas), pero un test de integración end-to-end con `WebApplicationFactory` real + DB Assert sobre `Auditorias` brindaría evidencia adicional. — *SUGGESTION* para follow-up (no bloqueante).

---

## Severidad

- **CRITICAL**: ninguno.
- **WARNING**: ninguno introducido por este change (un WARNING preexistente compartido con `UsuarioServicioComandos` por paridad; documentado arriba como riesgo residual #3).
- **SUGGESTION**: 4 elementos menores documentados arriba (riesgos residuales #1, #2, #4; desviaciones D1 y D2).

---

## Veredicto

**`PASS`**

Justificación:
- 22/22 escenarios cubiertos por tests que pasaron en runtime.
- 2948/2948 tests verdes (sin regresión).
- 0 build errors.
- 0 migraciones nuevas (columna `Personas.Legajo` ya era nullable `varchar(50)`).
- Interceptor central de auditoría sigue activo en `Program.cs:95-99`.
- Cero cambios sobre `setup-admin-inicial-issue-195/` (mtime preservado).
- Las dos desviaciones (D1, D2) son funcionalmente neutras y documentadas en `apply-progress.md`.

---

## Next recommended

`sdd-archive` — proceder con el archivado del change sincronizando las dos deltas (`persona-management`, `web-apiclient-transport-contract`) hacia los specs canónicos, registrando el cambio en la base OpenSpec. **No requiere correcciones puntuales**; los 4 SUGGESTION son opcionales y pueden abordarse en un follow-up si la evolución del módulo downstream que active `ShowLegajoContextWarning` lo demanda.

Antes de `sdd-archive`, el orquestador debe:
1. Confirmar que los 10 commits del branch `feat/issue-202-legajo-nulo-personas` ya están pusheados al remoto o preparar el push.
2. Resolver la `delivery_strategy` declarada en `tasks.md` (`ask-on-risk`) — la cadena PR no es necesaria porque el budget forecast (~340 líneas) está debajo de 400.
3. Validar la ausencia de nuevos commits en `setup-admin-inicial-issue-195` justo antes del merge (chequeo defensivo adicional).

---

## Next: sdd-archive

El change cumple los criterios de aceptación del proposal §"Acceptance & Success Criteria" en su totalidad:
1. ✅ `/personas/crear` con Legajo vacío/whitespace → 201 + redirect a Details (`Post_LegajoNullEnBody_Retorna201ConLegajoNull` + suite existente de Create).
2. ✅ `/personas/editar/{id}` limpiando Legajo → 200 + fila en Auditorias con `Accion="UpdateLegajo"` (`ActualizarAsync_LimpiarLegajo_RegistraAuditoria` + `Put_LimpiarLegajo_Retorna200YRegistraUpdateLegajo`).
3. ✅ Crear con legajo explícito sigue funcionando; legajo duplicado no nulo sigue rechazado con 409 (`ActualizarAsync_LegajoDuplicado_SigueRechazando`).
4. ✅ Wire emite `"legajo": null` cuando el PageModel envía null; backend persiste NULL (`CreateAsync_LegajoNull_SerializaLegajoNull` + `PersistirPersona_LegajoNull_LecturaPosterior`).
5. ✅ Warning UI sólo aparece cuando el contexto downstream lo demande; nunca bloquea submit (`_Form.cshtml:21-30` + `IPersonaForm.ShowLegajoContextWarning`).
6. ✅ `Auth/Setup` (no afectado) sigue aceptando `Legajo?` opcional — verificado por `SetupServicioTests` (7/7 verde).
7. ✅ `dotnet build SGV.slnx` y `dotnet test SGV.slnx` en verde (2948/2948).

Proceder con `sdd-archive` sin bloqueos.