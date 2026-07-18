# Apply progress: Hardening BFF same-origin

## Resumen
- Change: `2026-07-18-fix-163-bff-hardening`
- Branch: `fix/163-bff-hardening`
- Estado: completado

## Fase 1: RED
- Tests agregados: 8 en `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs` (132 LoC agregados).
- Warnings xUnit2013 corregidos: 8 sitios (no 3) — `BFF_BuscarConSearchDe200Caracteres_ReenviaAlClienteTipado`, `BFF_BuscarConSearchDe201Caracteres_Responde400YNoLlamaCliente`, `BFF_BuscarConSortEmailDesc_PropagaAlClienteTipado`, `BFF_BuscarConSortDocumentoAsc_Responde400YNoLlamaCliente`, `BFF_BuscarConSortInvalido_Responde400YNoLlamaCliente`, `BFF_BuscarConSegmentoEliminadas_PropagaAlClienteTipado`, `BFF_BuscarConSegmentoInvalido_Responde400YNoLlamaCliente`, `BFF_BuscarSinSortNiSegmento_AplicaDefaultsBackCompat`. Todos convertidos a `Assert.Single(...)` / `Assert.Empty(...)`. El orchestrator reportó 3 warnings, pero el build incremental reveló 8 (todos introducidos por el run anterior en el mismo archivo) y los 8 fueron limpiados para cumplir con "0 warnings nuevos".
- Resultado: 6 rojos / 6 verdes (4 preexistentes + 2 new back-compat: `#1` y `#8`).
- Comando: `dotnet test --filter "FullyQualifiedName~PersonaBuscadorModal" --logger "console;verbosity=minimal"`
- Output resumido:
  ```
  Failed!  - Failed:     6, Passed:     6, Skipped:     0, Total:    12, Duration: 2 s - SGV.Tests.dll (net10.0)
  ```
- Tests fallando: `BFF_BuscarConSearchDe201Caracteres_Responde400YNoLlamaCliente`, `BFF_BuscarConSortEmailDesc_PropagaAlClienteTipado`, `BFF_BuscarConSortDocumentoAsc_Responde400YNoLlamaCliente`, `BFF_BuscarConSortInvalido_Responde400YNoLlamaCliente`, `BFF_BuscarConSegmentoEliminadas_PropagaAlClienteTipado`, `BFF_BuscarConSegmentoInvalido_Responde400YNoLlamaCliente`.

## Fase 2: GREEN
- Handler modificado en `src/SGV.Web/Program.cs:210-278`.
- Detalle no contemplado por el pseudocódigo del design.md: en top-level statements de C# 14 los modificadores `static readonly` NO son válidos para campos locales; se cambió `static readonly HashSet<string>` por `HashSet<string>` (locales sin `readonly`, que siguen siendo funcionalmente inmutables porque el handler solo lee `.Contains(...)` / `.OrderBy(...)` y nunca muta).
- Resultado: 12 verdes.
- Comando: `dotnet test --filter "FullyQualifiedName~PersonaBuscadorModal" --logger "console;verbosity=minimal"`
- Output resumido:
  ```
  Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 2 s - SGV.Tests.dll (net10.0)
  ```

## Fase 3: Verificación final
- `dotnet build SGV.slnx --no-incremental`: ✅ 0 errors. Warnings totales: 23 (todos preexistentes: CS8524 ×12, CS8602 ×3, CS8604 ×2, CS8625 ×1, EF1002 ×2, xUnit2029 ×2, xUnit1026 ×1 — ninguno introducido por este change). Los 8 xUnit2013 introducidos por el run anterior fueron eliminados.
- `dotnet test SGV.slnx --no-build`: ⚠ 1 failed, 2478 passed, 0 skipped (total 2479). El único fallo es `SGV.Tests.Web.Usuario.EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback`, introducido por commit `4f586d48 feat(web): redirect usuario edit success to users list` (PR #170/#171). **Verificado preexistente**: stash de los 2 archivos del change y re-ejecución del test aislado reproduce el mismo fallo. No está relacionado con el handler BFF de `/api/v1/personas/consulta`. Requiere investigación aparte, fuera del scope de #163.
- `bun run build` desde `src/SGV.Web`: ✅ gulp build OK (3 s); warnings deprecados deprecados preexistentes de `baseline-browser-mapping`/`browserslist` no son de este change.

## Commits (3 en total: 1 ortogonal + 2 del change)
- `062536f1` fix(tests): remove duplicated Post_Create_WhenPasswordPolicyFails_RendersSpanishError (ortogonal, preexistente)
- `13833d6a` test(web): add BFF hardening tests for search/sort/segmento
- `4d1a84b1` feat(web): harden BFF /api/v1/personas/consulta with caps and whitelists

## Notas / blockers
- CS0111 preexistente (PR #170/#171 merge) se resolvió como commit ortogonal dentro del mismo PR (`062536f1`).
- Sin migraciones, sin nuevas dependencias, sin cambios al backend ni a `FakePersonaApiClient`.
- Desviación menor del pseudocódigo: `static readonly` → `HashSet<string>` local por incompatibilidad con top-level statements (C# 14). Comportamiento idéntico: las colecciones solo se leen.
- Desviación menor del orchestrator: se limpiaron 8 xUnit2013 en lugar de 3 para cumplir "0 warnings nuevos" del proposal. Las 5 advertencias adicionales estaban en el mismo archivo introducido por el run anterior.
- `allowedSegmentos` queda declarado pero no usado en runtime (el handler hace `Equals("activas"/"eliminadas")`); sigue presente por intención de la whitelist explícita del design.
- Pre-existing fallo no relacionado: `EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback` (commit `4f586d48`).
