# Archive report: Permitir crear y editar Personas con legajo nulo (issue #202)

## Resumen ejecutivo
El change #202 alinea wire-types, UI, normalización y setup al hecho de que `Persona.Legajo` ya era `string?` en el Dominio. Se modificaron 6 archivos de código fuente (`PersonaRequests.cs`, `PersonaInputModel.cs`, `IPersonaForm.cs`, `Create.cshtml.cs`, `Edit.cshtml.cs`, `SetupServicio.cs`, `PersonaServicioComandos.cs`) y se agregaron 2 nuevos (`NoopAuditoriaServicio.cs`, `NullUsuarioActual.cs`) para mantener back-compat del ctor de `PersonaServicioComandos`. Se agregaron 12 tests (4 unit, 1 web page, 3 HTTP seam, 3 API integration, 1 MySQL repo) y se ajustó 1 test existente. La suite completa (2948 tests) pasa en verde con MySQL disponible. El interceptor central de auditoría sigue activo. `setup-admin-inicial-issue-195` preservado intacto.

## Issue y referencia
- **Issue GitHub**: #202 — "Permitir crear y editar Personas con legajo nulo"
- **Change**: `permitir-legajo-nulo-personas-issue-202`
- **Branch**: `feat/issue-202-legajo-nulo-personas`
- **PR**: #203 (abierto contra `develop`)
- **Fecha de archivo**: 2026-07-26

## Commits en la rama (12)

```
d55ed53d docs(specs): add SDD planning artifacts for issue #202
0b8365a2 fix(personas): wrap UpdateLegajo audit to log+continue on Registrar failure
20aeab47 docs(apply): record apply-progress for issue-202 Legajo nullable change
95305bb1 feat(web): add hidden Legajo context warning slot in Personas _Form
f264acc3 docs(apply): mark Phase 4 tasks complete
c893ca92 test(personas): add integration coverage for nullable Legajo
fc7eda57 docs(apply): mark Phase 3 tasks complete
dad6d7f6 feat(web): normalize Legajo whitespace to null on Personas pages
c557bd10 docs(apply): mark Phase 2 tasks complete
5de40c78 feat(personas): emit explicit UpdateLegajo audit on null transition
7ccf5852 docs(apply): mark Phase 1 tasks complete
e1a9f2d5 feat(personas): allow nullable Legajo in wire, InputModel and Setup
```

10 commits de implementación SDD (`e1a9f2d5` … `20aeab47`), 1 bounded fix (`0b8365a2` — wrap `RegistrarAsync` para log+continue en fallo), 1 docs/planning (`d55ed53d`). Total: 12 commits ahead de `develop`.

## Resultados de validación

| Métrica | Resultado |
|---------|-----------|
| `dotnet build SGV.slnx` | 0 errores |
| `dotnet test SGV.slnx` | **2949/2949 passing**, 0 failed, 0 skipped |
| `bun run build` (src/SGV.Web) | OK, sin impacto en assets |
| MySQL `[MySqlFact]` | Ejecutados — MySQL alcanzable en `localhost:3306` |
| Tests añadidos | 12 (4 application + 1 web page + 3 HTTP seam + 3 API integration + 1 MySQL repo) |
| Tests ajustados | 1 (`EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm`) |
| Migraciones nuevas | 0 — columna `Personas.Legajo` ya era `varchar(50)` nullable |
| Cambio paralelo | `setup-admin-inicial-issue-195/` **NO modificado** (mtime `Jul 26 14:03:23 2026` preservado) |

**Nota sobre el conteo de tests**: el verify-report registró 2948 tests. Tras el bounded fix (`0b8365a2`) se agregó 1 test adicional para cubrir el nuevo comportamiento log+continue, elevando el total a 2949. Este test está incluido en la suite que corre verde.

## Sync de deltas a specs canónicas

### `openspec/specs/persona-management/spec.md`

| Acción | Detalle |
|--------|---------|
| **MODIFIED** — Alta de Persona | `Legajo` cambió de MUST ser requerido a MAY omitirse; se agregaron escenarios de omisión, whitespace-only, rechazo de >50 caracteres y duplicados activos. Se eliminó el escenario "Rechazar datos obligatorios faltantes" (ya no aplica). |
| **MODIFIED** — Actualización de Persona | Se agregó la transición `Legajo` no-nulo → null con emisión de fila `UpdateLegajo` en `Auditorias`; se agregaron 3 escenarios: limpieza con auditoría, normalización whitespace, sin transición sin auditoría. |
| **ADDED** — Auditoría explícita al limpiar Legajo de Persona | Nuevo requisito con 3 escenarios: limpieza vía formulario web, vía consumidor HTTP autenticado no-web, y sin transición sin fila. |
| Preservados | Los 24 requisitos restantes del spec canónico se preservan sin modificación. |

### `openspec/specs/web-apiclient-transport-contract/spec.md`

| Acción | Detalle |
|--------|---------|
| **ADDED** — `CrearPersonaRequest.Legajo` y `ActualizarPersonaRequest.Legajo` son `string?` | Nuevo requisito con 8 escenarios que cubren: payload null, clave ausente, string vacío, normalización whitespace en Create/Edit Edit, respuesta GET con Legajo NULL, rechazo >50 caracteres. |
| **ADDED** — `PersonaApiClient` no pre-procesa `Legajo` | Nuevo requisito con 3 escenarios: entrega crudo null → `legajo: null`, entrega `""` → `legajo: ""`, preservación de espacios. |
| Preservados | Todos los requisitos previos se preservan sin modificación. |

## Archivos nuevos notables

Dos archivos `internal` en `SGV.Aplicacion` fueron introducidos exclusivamente para mantener back-compat del ctor de `PersonaServicioComandos`:

- `src/SGV.Aplicacion/Auditoria/NoopAuditoriaServicio.cs` (23 líneas) — implementación no-op de `IAuditoriaServicio` para el ctor de back-compat usado por 11 tests previos.
- `src/SGV.Aplicacion/Seguridad/NullUsuarioActual.cs` (19 líneas) — implementación no-op de `IUsuarioActual` para el mismo propósito.

**Nota para mantenimiento futuro**: ambos archivos son test-only helpers que viven en el proyecto de aplicación para evitar referencias circulares. Podrían moverse a `tests/SGV.Tests/` en un change futuro como parte de una limpieza de la superficie pública de `SGV.Aplicacion`.

## Desviaciones del diseño

### D1. Ajuste en test existente (`EditPageTests.Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm`)
**Severidad**: SUGGESTION. El mensaje "El legajo es obligatorio" dejó de generarse client-side al remover `[Required]`. El test ahora usa un mensaje alcanzable vía backend ("El legajo ya está en uso"). Contrato observable intacto.

### D2. `_Form.cshtml` slot condicional con atributo `hidden`
**Severidad**: SUGGESTION. El span incluye `hidden` cuando `ShowLegajoContextWarning == false` para no ocupar espacio vertical. Funcionalmente idéntico al diseño.

## Riesgos residuales y SUGGESTIONs no abordados

1. **Log completo de excepción de auditoría** — El bounded fix `0b8365a2` envuelve `RegistrarAsync` en try/catch con `ILogger.LogWarning`, pero no registra el stack trace completo de la excepción. Mejorable si la evolución del módulo downstream lo demanda.
2. **`OperationCanceledException` filter** — El catch de `TaskCanceledException` en `PersonaServicioComandos` no distingue entre timeout real y cancelación cooperativa del token. Podría agregarse un filtro `OperationCanceledException` si la telemetría muestra falsos positivos.
3. **Test-only helpers en proyecto de aplicación** — `NoopAuditoriaServicio` y `NullUsuarioActual` (descritos arriba) podrían moverse a `SGV.Tests` en un change futuro.

## Estado de los findings del verify
- CRITICAL: 0
- WARNING: 0 (el único WARNING preexistente sobre paridad con `UsuarioServicioComandos` sigue vigente pero no fue introducido por este change)
- SUGGESTION: 4 (riesgos residuales #1, #2, #4 del verify-report + D1/D2 documentados en apply-progress)
- archive_ready: yes

## Artefactos del change archivado
- `exploration.md` (218 líneas)
- `proposal.md` (97 líneas)
- `specs/persona-management/spec.md` (delta, 125 líneas)
- `specs/web-apiclient-transport-contract/spec.md` (delta, 87 líneas)
- `design.md` (107 líneas)
- `tasks.md` (71 líneas, 17/17 tareas completas)
- `apply-progress.md` (184 líneas)
- `verify-report.md` (316 líneas)
- `archive-report.md` (éste)

## Lecciones / Notas para el equipo
- `Trim()` con `string?` rompe en runtime — la transición a nullable requiere revisar todos los call-sites que invocan `.Trim()` sin guarda nula.
- `PersonaPostResultMapper` sólo se invoca si ModelState pasa — al relajar `[Required]`, los tests que dependían de validación client-side deben ajustarse para ejercitar el path backend.
- El patrón ctor de back-compat con `NoopAuditoriaServicio` + `NullUsuarioActual` es reusable para futuros cambios que agreguen dependencias opcionales a servicios existentes sin inflar tests previos.
