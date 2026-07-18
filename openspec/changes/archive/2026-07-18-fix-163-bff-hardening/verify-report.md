# Verify report: Hardening BFF same-origin — issue #163

## Resumen
- Change: `2026-07-18-fix-163-bff-hardening`
- Branch: `fix/163-bff-hardening`
- Estado: ✅ verificado con findings (1 WARNING ortogonal preexistente, 1 SUGGESTION de cobertura de spec, 0 CRITICAL)

## 1. Mapping Spec → Tests → Implementación

| Requirement | Scenarios | Tests que cubren | Implementación en `Program.cs` | ¿Cubierto? |
|---|---|---|---|---|
| **R1**: BFF acota `?search` a 200 caracteres | 2 (válido 200, inválido 201) | `BFF_BuscarConSearchDe200Caracteres_ReenviaAlClienteTipado`, `BFF_BuscarConSearchDe201Caracteres_Responde400YNoLlamaCliente` | constante `SearchMaxLength = 200` (l. 212), check `search.Length > SearchMaxLength` (l. 238-244) con `Results.Problem(400)` | ✅ Sí |
| **R2**: BFF acepta `?sort=` con whitelist cerrada | 2 (token válido, token fuera de whitelist) | `BFF_BuscarConSortEmailDesc_PropagaAlClienteTipado` (válido: `email_desc`), `BFF_BuscarConSortDocumentoAsc_Responde400YNoLlamaCliente` (rechaza `documento_asc` y lista los 8 tokens), `BFF_BuscarConSortInvalido_Responde400YNoLlamaCliente` (rechaza `hack`) | `HashSet<string> allowedSorts` con 8 tokens (l. 215-221) + check `!allowedSorts.Contains(resolvedSort)` (l. 247-253) | ✅ Sí (con cobertura ampliada: 3 tests para 2 scenarios, cubre whitelist exhaustivamente) |
| **R3**: BFF acepta `?segmento=` con whitelist cerrada | 2 (válido `eliminadas`, inválido `todas`) | `BFF_BuscarConSegmentoEliminadas_PropagaAlClienteTipado`, `BFF_BuscarConSegmentoInvalido_Responde400YNoLlamaCliente` | `HashSet<string> allowedSegmentos` con 2 tokens (l. 223-226), check `Equals("activas")`/`Equals("eliminadas")` (l. 256-267) | ✅ Sí |
| **R4**: BFF preserva defaults back-compat | 2 (sin params → defaults, un param válido + default del otro) | `BFF_BuscarSinSortNiSegmento_AplicaDefaultsBackCompat` (ambos defaults verificados); cobertura **parcial** del scenario 4.2 vía `BFF_BuscarConSortEmailDesc_*` y `BFF_BuscarConSegmentoEliminadas_*` (no asertan el default del campo opuesto) | `resolvedSort = sort ?? "apellidos_asc"` (l. 246), `resolvedSegmento = PersonaSegmentoListado.Activas` (l. 255) | ⚠ **Parcial** (ver SUGGESTION abajo) |

**Síntesis**: 8/8 scenarios tienen al menos un test pasando. Scenario 4.2 está implícitamente cubierto por la combinación de los tests 3 y 6, pero **no verifica explícitamente** que `Sort="apellidos_asc"` se mantenga cuando sólo se pasa `segmento=eliminadas` (ni viceversa).

## 2. Resultados de la suite del change

`dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PersonaBuscadorModal" --logger "console;verbosity=normal"`:
- Passed: 12/12
- Failed: 0
- Skipped: 0
- Output resumido:

```
Test Run Successful.
Total tests: 12
     Passed: 12
 Total time: 3.6896 Seconds - SGV.Tests.dll (net10.0)
```

Los 12 tests corresponden a:
- 4 previos (modal UI): `PersonaBuscadorModal_TieneRoleDialogYAriaModal`, `PersonaBuscadorModal_EstadoInicial_MuestraMensajeGuia`, `PersonaBuscadorModal_EstadoEmpty_MuestraMensajeSinResultados`, `PersonaBuscadorModal_ConsultaSameOrigin_UsaClienteTipadoDePersonas`.
- 8 nuevos (hardening BFF): los enumerados en la tabla de la sección 1.

## 3. Ortogonalidad

`git diff 062536f1~1..HEAD --stat`:
- `src/SGV.Web/Program.cs` — +53 LoC (handler endurecido).
- `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs` — +132 LoC (8 tests nuevos + helper).
- `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` — −46 LoC (duplicado CS0111 eliminado, ortogonal preexistente).

Archivos NO tocados (verificado archivo por archivo, sin output de `git diff`):
- `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaListQuery.cs`
- `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaRepository.cs`
- `src/SGV.Web/Integration/Personas/PersonaApiClient.cs`
- `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs`

Verificación adicional: `git show 062536f1~1:<archivo>` vs contenido actual confirmó byte-a-byte la igualdad de los 4 archivos de la lista NO-tocados. En particular:
- `PersonaListQuery` sigue siendo `string? Sort` (sin `PersonaSort` enum).
- `PersonaRepository.ApplySort` mantiene exactamente los 8 tokens coincidentes con `allowedSorts`.
- La whitelist del BFF replica literalmente `ApplySort:218-235` (incluye `email_*`, excluye `documento_*`).

## 4. Atribución IA

`git log 062536f1~1..HEAD --pretty=fuller | grep -iE "co-authored-by|ai|gpt|claude|copilot"`: NO_ATRIBUCION_IA_ENCONTRADA.

✅ Limpio. Todos los commits son del autor humano `Sebastián Serrisuela <sebaserri@gmail.com>`, formato conventional (`fix(tests):`, `test(web):`, `feat(web):`), sin trailers `Co-authored-by:` ni otra atribución IA.

## 5. Suite completa

`dotnet test SGV.slnx --no-build --logger "console;verbosity=minimal"`:
- **Passed: 2486**
- **Failed: 1**
- Skipped: 0
- Total: 2487

**Único fallo confirmado**:

```
Failed SGV.Tests.Web.Usuario.EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback
  Error Message:
   Assert.Contains() Failure: Sub-string not found
  String:    "/seguridad/usuarios?p=2&search=anuevo&sor"···
  Not found: "status=activas"
  Stack Trace:
   at SGV.Tests.Web.Usuario.EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback() in /Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/Usuario/EditPageTests.cs:line 205
```

Verificación de ortogonalidad del fallo: introducido por `4f586d48 feat(web): redirect usuario edit success to users list` (PR #170/#171, issue #170 — redirect con `status=activas`). El test assertea que la URL del redirect incluya `status=activas`, pero el handler actual emite `?p=2&search=anuevo&sor` (faltando el `&status=activas`). Queda fuera del scope de #163 y es un fallo preexistente reproducible **sin** los archivos del change (verificado por el orchestrator durante apply, repeatable con `git stash`).

No hay **otros fallos distintos** al preexistente.

### Nota de entorno (transparencia)

La primera ejecución de la suite completa arrojó 1006 fallos con `FileNotFoundException: FluentValidation, Version=12.0.0.0`, NO relacionados con el change: provenían de `tests/SGV.Tests/obj/**/SGV.Tests.csproj.AssemblyReference.cache` stale (caché de referencias de assembly con un binding a una versión previa). Tras `dotnet restore` + `dotnet build --no-incremental` regenerado, los caches stale se purgaron y la suite pasó de 1481 a 2486 verdes. **Esto no es regresión del change**: los archivos del change no tocan versiones de NuGet ni `csproj`. Es un issue ambiental del setup de build local del reviewer, no afecta al merge.

## 6. Build limpio

`dotnet build SGV.slnx --no-incremental`:
- Errors: 0
- Warnings totales: 23 (todos preexistentes, ningún nuevo introducido por el change)
- Distribución de los 23 warnings:
  - CS8524 ×12 (enum switch no exhaustivo — preexistente en código de dominio)
  - CS8602 ×3 (null check en lugar ya validado)
  - CS8604 ×2 (argumento posiblemente null)
  - CS8625 ×1 (literal null a non-nullable)
  - EF1002 ×2 (`ExecuteSqlRawAsync` con interpolación — tests de persistencia, marcados para SQL injection)
  - xUnit2029 ×2 (`Assert.Empty` para verificar inexistencia — usar `Assert.DoesNotContain`)
  - xUnit1026 ×1 (parámetro de `[Theory]` no usado)

Comparado con el baseline (`062536f1~1..062536f1`): mismos 23 warnings; los 8 xUnit2013 introducidos durante el RED del change fueron limpiados en el commit GREEN.

## 7. Frontend

`bun run build` desde `src/SGV.Web`:
- Status: ✅ OK
- Tiempo: 3.03 s
- Pipeline: `gulp build` → `plugins` → `styles` (Bundler CSS + Inspinia assets)
- Output:
  ```
  [17:58:23] Using gulpfile ~/Source/SGV/src/SGV.Web/gulpfile.js
  [17:58:23] Starting 'build'...
  [17:58:23] Starting 'plugins'...
  [17:58:23] Finished 'plugins' after 4.92 ms
  [17:58:23] Starting 'styles'...
  [17:58:26] Finished 'styles' after 3.02 s
  [17:58:26] Finished 'build' after 3.03 s
  ```
- Warnings deprecados preexistentes de `baseline-browser-mapping`/`browserslist`: sí (no atribuibles a #163; datos de >2 meses / >9 meses según `browserslist` — preexistentes al change).
- Un `DEP0180` (DeprecationWarning: `fs.Stats` constructor es deprecated) — preexistente de Node 22+ sobre `gulp.src`.

## Findings

### CRITICAL
*(Vacío)*

Ningún CRITICAL encontrado:
- 3 commits del change sin atribución IA.
- Sin archivos fuera de scope modificados.
- Suite completa solo falla por 1 test preexistente ortogonal.
- Build limpio, frontend OK.

### WARNING

- **W1**: Fallo preexistente `SGV.Tests.Web.Usuario.EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback`. El test assertea que la URL del redirect incluya `status=activas`, pero el handler actual de `Edit.cshtml.cs` no lo incluye (emite `?p=2&search=...&sort=...`). **Preexistente** al change #163 — introducido por `4f586d48 feat(web): redirect usuario edit success to users list` (issue #170). Verificado reproducible sin los archivos del change.
  - Acción recomendada: seguimiento aparte, abrir nueva issue (#163-bis o #170-bis — investigar si el handler de Edit debe propagar `status`, `sort` y `search` del query string al redirect de éxito). **NO bloquea el merge de #163**.

### SUGGESTION

- **S1**: Scenario 4.2 del spec ("BFF respeta un parámetro válido y mantiene el default del otro") no tiene **test dedicado** que aserte explícitamente que el default del campo opuesto se preserva. Los tests existentes (`BFF_BuscarConSortEmailDesc_*` y `BFF_BuscarConSegmentoEliminadas_*`) cubren **parcialmente** el comportamiento, pero no blindan explícitamente la invariante de back-compat ante refactors futuros del handler.
  - Costo: bajo (2 líneas adicionales en cada test existente: `Assert.Equal("apellidos_asc", query.Sort);` después del test del segmento, y viceversa).
  - Beneficio: ancla explícita de la invariante declarada en `spec.md` (líneas 70-74). Trade-off menor — el cambio es responsabilidad del orchestrator en una iteración de polish.
- **S2** (ambiental, no del change): los `SGV.*.csproj.AssemblyReference.cache` quedan stale entre versiones de NuGet. Considerar `dotnet clean` antes de `dotnet test` en scripts CI para evitar el problema de binding 12.0.0.0 en runs subsecuentes.

## Cumplimiento de criterios de aceptación

Bullets de la sección "Enfoque y criterios de aceptación" de `proposal.md`:

- ✅ "`search` de hasta 200 caracteres se reenvía; más de 200 devuelve 400 y no llama al cliente tipado."
  Cubierto por `BFF_BuscarConSearchDe200Caracteres_ReenviaAlClienteTipado` + `BFF_BuscarConSearchDe201Caracteres_Responde400YNoLlamaCliente` (PASA).
- ✅ "`sort` acepta únicamente `apellidos_asc`, `apellidos_desc`, `nombres_asc`, `nombres_desc`, `legajo_asc`, `legajo_desc`, `email_asc` y `email_desc`; otro valor devuelve 400."
  Cubierto por `BFF_BuscarConSortDocumentoAsc_Responde400YNoLlamaCliente` (lista los 8 tokens), `BFF_BuscarConSortEmailDesc_*` (caso positivo) y `BFF_BuscarConSortInvalido_*` (caso `hack`) (PASA).
- ✅ "`segmento` acepta únicamente `activas|eliminadas`; otro valor devuelve 400."
  Cubierto por `BFF_BuscarConSegmentoEliminadas_*` (caso positivo) y `BFF_BuscarConSegmentoInvalido_*` (rechaza `todas`, menciona ambos tokens) (PASA).
- ⚠ "Sin `sort` ni `segmento`, se preservan `apellidos_asc` y `Activas`."
  Cubierto por `BFF_BuscarSinSortNiSegmento_AplicaDefaultsBackCompat` (PASA). La otra mitad del requirement (preservar el default del campo opuesto cuando solo uno está presente) está **implícita** — ver SUGGESTION S1.
- ✅ "Valores válidos llegan correctamente a `PersonaListQuery`."
  Cubierto por todos los tests positivos (3, 6, 8) que assertean `query.Search`, `query.Sort` o `query.Segmento` igual al valor recibido o al default (PASA).

### Restricciones transversales del proposal

- ✅ Strict TDD (`RED→GREEN`): 8 tests añadidos antes que la implementación (verificado: `13833d6a test(web):` precede a `4d1a84b1 feat(web):`).
- ✅ `SGV.Web` permanece como shell (no se introduce lógica de dominio).
- ✅ Artefactos en español (proposal, spec, design, tasks, apply-progress, este verify-report).
- ✅ Commits conventional sin `Co-Authored-By`.

## Conclusión

El change `2026-07-18-fix-163-bff-hardening` **está listo para merge**: cumple los 5 criterios de aceptación, los 8 scenarios del spec están cubiertos (1 con cobertura parcial documentada como SUGGESTION), los 3 commits no introducen nuevos warnings ni regresiones de tests, la ortogonalidad es total (3 archivos tocados, ningún efecto colateral en `PersonaListQuery` / `ApplySort` / `PersonaApiClient` / `FakePersonaApiClient`), y el frontend sigue compilando. **No hay CRITICALs**.

El único fallo de la suite completa (`EditPageTests.Post_Edit_WhenSuccessful_RedirectsToIndexWithSuccessFeedback`) es **preexistente y ortogonal** al change, introducido por commit `4f586d48` (issue #170, PR #170/#171). Debe gestionarse como issue de seguimiento aparte, no bloquea el merge de #163.

Recomendación final: ejecutar `next_recommended = archive` (sin bloqueos).
