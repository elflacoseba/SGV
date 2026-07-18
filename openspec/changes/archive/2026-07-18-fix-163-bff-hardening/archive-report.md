# Archive report: Hardening BFF same-origin — issue #163

## Metadata
- Issue: #163
- Change: `2026-07-18-fix-163-bff-hardening`
- Branch origen: `fix/163-bff-hardening`
- Commits mergeados (3): `062536f1` (ortogonal CS0111), `13833d6a` (RED), `4d1a84b1` (GREEN)
- Fecha archive: 2026-07-18
- Verificado por: sdd-verify

## Resumen

Hardening del BFF same-origin `GET /api/v1/personas/consulta` en `SGV.Web`. Resuelve RIS-001 (cap de `?search` a 200 caracteres) y RIS-002 (whitelists cerradas para `?sort` con 8 tokens sincronizados con `PersonaRepository.ApplySort`, y `?segmento` con `activas|eliminadas`). Defaults back-compat preservados. Sin cambios al backend ni a `FakePersonaApiClient`.

## Capacidades modificadas

- `usuario-web-selector-persona-buscador`: el BFF valida entradas antes de invocar `IPersonaApiClient` y responde 400 con `ProblemDetails` ante entradas inválidas.

## Specs sincronizados

- `openspec/specs/usuario-web-selector-persona-buscador/spec.md` (ACTUALIZADO) — merge de 4 requisitos ADDED del delta spec del change al main spec existente. Propósito, decisiones de especificación (Q4-Q6) y consideraciones fuera de alcance actualizados.

## Cambios por archivo

| Archivo | LoC | Tipo |
|---|---|---|
| `src/SGV.Web/Program.cs` | +53 | feat (handler endurecido) |
| `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs` | +132 | test (8 nuevos RED→GREEN) |
| `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` | −46 | fix (CS0111 ortogonal preexistente) |

## Verificación

- ✅ `dotnet build SGV.slnx`: 0 errors, 0 warnings nuevos.
- ✅ `dotnet test --filter "FullyQualifiedName~PersonaBuscadorModal"`: 12/12 verdes.
- ⚠ `dotnet test SGV.slnx`: 2486 passed, 1 failed (preexistente ortogonal — `EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback`, commit `4f586d48`).
- ✅ `bun run build`: bundle OK.

## Findings

- ⚠ W1: Fallo preexistente en `EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback`. Acción recomendada: issue de seguimiento aparte.
- 💡 S1: Cobertura parcial del scenario 4.2 del spec. Acción recomendada: agregar 2 asserts en tests existentes.

## Decisiones de diseño cerradas

1. **Whitelist sort alineada con `PersonaRepository.ApplySort`**: 8 tokens (`legajo/apellidos/nombres/email` × asc/desc). `documento_asc/desc` queda **explícitamente fuera** del scope por falta de soporte en `ApplySort`. Si el negocio lo requiere, es un change separado al backend.
2. **Validación en BFF (no backend)**: cortar antes de salir del proceso reduce latencia, logs y superficie de ataque contra `SGV.Api`. El backend NO rechaza valores inválidos — cae silenciosamente al default.
3. **Constantes locales al handler**: `allowedSorts` y `allowedSegmentos` viven en `Program.cs`, no en un archivo separado. KISS; extraer cuando haya otro handler que los reuse.
4. **No se introduce `PersonaSort` enum**: el campo `Sort` de `PersonaListQuery` sigue siendo `string?`. Introducir el enum duplicaría la fuente de verdad con `ApplySort` y rompería el contrato del API.
5. **CS0111 preexistente resuelto como commit ortogonal dentro del mismo PR**: el merge de PR #170/#171 dejó `Post_Create_WhenPasswordPolicyFails_RendersSpanishError` duplicado byte a byte en `CreatePageTests.cs:460/506`. Commit `062536f1` lo eliminó para desbloquear `dotnet build`.

## Stale-checkbox reconciliation

El archivo `tasks.md` del change mantenía los 21 checkboxes en `- [ ]` sin marcar, estado heredado de la generación inicial de `sdd-tasks` que nunca fue actualizado por `sdd-apply`. `apply-progress.md` y `verify-report.md` prueban que la totalidad de las tasks fueron completadas (3 commits, 8 tests RED→GREEN, handler implementado, build y suite completa verificados). El orchestrator instruyó explícitamente proceder con el archive usando esta evidencia como respaldo. Sección 1.2 de `sdd-archive` SKILL.md — excepción ejercida.

## Restricciones respetadas

- ✅ Strict TDD (`RED→GREEN` documentado).
- ✅ Conventional commits sin `Co-Authored-By` ni atribución IA.
- ✅ Artefactos SDD en español.
- ✅ Sin migraciones, sin nuevas dependencias NuGet.
- ✅ `SGV.Web` permanece como shell — no se introduce lógica de dominio.

## Próximos pasos sugeridos

1. Push de la rama y abrir PR contra `develop`.
2. Issue de seguimiento para el fallo preexistente `EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback`.
3. (Opcional) Issue de seguimiento para el polish de cobertura del scenario 4.2.
4. (Opcional) Evaluar si `documento_asc/desc` debe sumarse al backend y a la whitelist — depende de decisión de negocio.
