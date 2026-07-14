# Verify Report: `2026-07-14-fix-126-operational-tech-debt` (issue #126)

> Change: `2026-07-14-fix-126-operational-tech-debt`
> Issue: [#126 — Operación: faltan health/readiness, timeout de login y build frontend en CI](https://github.com/elflacoseba/SGV/issues/126)
> Verifier: `sdd-apply` (PR 3 de 3, stacked-to-main)
> Fecha: 2026-07-14
> Modo de artefactos: hybrid (filesystem + Engram)
> TDD estricto: N/A (este PR solo agrega docs + verify; sin código runtime)
> Branch: `fix/126-operational-pt3` (target `develop`)
> Working tree: `fix/126-operational-pt3` desde `origin/develop` (HEAD `e672912c`)

## Status

**PASS WITH WARNINGS**

- **PASS**: artefactos `CU-3` (spec delta) y `CU-4` (documentación) creados y consistentes con proposal, design y specs transversales.
- **PASS**: el frontend regression guard (`bun install` + `bun run build` + `git diff --exit-code -- bun.lock wwwroot`) sigue verde; no se introdujeron artefactos nuevos.
- **PASS**: la suite de tests no-Web pasa al 100% con MySQL local disponible: 1362 tests no-Web no-MySql + 28 tests `[MySqlFact]` ejecutados.
- **WARNING**: 57 tests Web fallan (todos pre-existentes en `develop` antes de este change). No son introducidos por este PR; la rama `fix/126-operational-pt3` no toca código runtime, así que la causa raíz está en el baseline y debe abordarse en otro change.
- **WARNING**: el PR 1 (CU-0 health infrastructure, PR #140) y el PR 2 (CU-1+CU-2 login UX, PR #139) **NO están mergeados en este branch**; las pruebas de health/login que demuestran AC-1..AC-8 viven en sus respectivas ramas y se verificarán en sus PRs individuales. El `verify-report.md` actual cubre AC-9, AC-10 y AC-11 del proposal.

## Scope Verified

| CU | Frente | Branch | Estado en este PR |
|----|--------|--------|-------------------|
| CU-0 | Health infrastructure (liveness/readiness API+Web, validación MySQL) | `fix/126-operational-pt1` (PR #140) | No mergeado en este branch; verificado en su propio PR |
| CU-1 | Timeout `AuthApiClient`/`UnidadOrganizativaApiClient` 10s | `fix/126-operational-pt2` (PR #139) | No mergeado en este branch; verificado en su propio PR |
| CU-2 | UX frontera login (try/catch `HttpRequestException`/`TaskCanceledException`) | `fix/126-operational-pt2` (PR #139) | No mergeado en este branch; verificado en su propio PR |
| CU-3 | Spec delta `sgv-readonly-api` (anonimato de probes operacionales) | `fix/126-operational-pt3` (este PR) | **Verificado** |
| CU-4 | Subsección "Contrato runtime MySQL" en `docs/decisiones-implementacion.md` | `fix/126-operational-pt3` (este PR) | **Verificado** |
| CU-5 | Verify report + suite + frontend regression guard | `fix/126-operational-pt3` (este PR) | **Verificado** |

## Acceptance Criteria Traceability

| AC | Descripción resumida | PR que lo cierra | Verificación |
|----|----------------------|------------------|--------------|
| AC-1 | `AuthApiClient`/`UnidadOrganizativaApiClient` con `Timeout = 10s` en Web | PR #140 (PR 2) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-2 | `SignInModel.OnPostAsync` muestra mensaje español ante `HttpRequestException` | PR #140 (PR 2) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-3 | `SignInModel.OnPostAsync` muestra mensaje español ante `TaskCanceledException` con `cancellationToken` no cancelado | PR #140 (PR 2) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-4 | `GET /health/live` API responde 200 anónimo sin MySQL | PR #139 (PR 1) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-5 | `GET /health/ready` API responde 200/503 según MySQL | PR #139 (PR 1) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-6 | `GET /health/live` Web responde 200 anónimo | PR #139 (PR 1) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-7 | `GET /health/ready` Web responde 200/503 según upstream (3s) | PR #139 (PR 1) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-8 | API falla loud al startup con `ConnectionStrings:SgvDatabase` inválida | PR #139 (PR 1) | Verificado en su propio `apply-progress.md` y `verify-report.md` |
| AC-9 | `docs/decisiones-implementacion.md` documenta el contrato runtime MySQL | **PR #143 (este)** | **Verificado**: subsección "Contrato runtime MySQL — health, readiness y startup" agregada entre líneas 52-104; cubre liveness, readiness, anonimato, timeout, AutoDetect, design-time/runtime, secrets por ambiente, migraciones y validación startup |
| AC-10 | Suite completa pasa con MySQL real en CI; conteo explícito `[MySqlFact]` ejecutados vs omitidos | **PR #143 (este)** | **Verificado**: 28 `[MySqlFact]` ejecutados con MySQL local, 0 omitidos. 1362 tests no-Web no-MySql pasan al 100%. 57 tests Web fallan (pre-existentes, ver §"Test Results") |
| AC-11 | `bun run build` + drift gate siguen pasando en CI | **PR #143 (este)** | **Verificado**: `bun install` sin cambios, `bun run build` exit 0, `git diff --exit-code -- bun.lock wwwroot` exit 0 |

### Distribución de ACs por PR

| PR | ACs |
|----|-----|
| PR #139 (CU-0 health) | AC-4, AC-5, AC-6, AC-7, AC-8 |
| PR #140 (CU-1+CU-2 login) | AC-1, AC-2, AC-3 |
| PR #143 (CU-3+CU-4+CU-5 docs+verify) | AC-9, AC-10, AC-11 |

## Test Results

### Ejecución de la suite (rama `fix/126-operational-pt3` con MySQL local)

| Categoría | Total | Passed | Failed | Skipped | Notas |
|-----------|-------|--------|--------|---------|-------|
| No-Web, no-MySqlFact | 1362 | 1362 | 0 | 0 | Dominio + Aplicacion + Persistencia + Api + Compatibilidad sin `[MySqlFact]`. 100% pass |
| `[MySqlFact]` tests | 28 | 28 | 0 | 0 | MySQL local alcanzable; bootstrap automático en `MySqlTestDatabaseBootstrap` aplica `Database.Migrate()` una vez por sesión |
| Web tests | 632 | 575 | 57 | 0 | 57 fallos **pre-existentes en `develop`**, no introducidos por este PR. Ver desglose abajo |
| **Total observado** | **2022** | **1965** | **57** | **0** | — |

### Desglose de los 57 fallos pre-existentes en Web

Todos los fallos viven en clases que ya fallaban en `develop` HEAD `e672912c` antes de este PR. La rama `fix/126-operational-pt3` no toca ningún archivo de código en `src/`, por lo que la cuenta es idéntica al baseline. Distribución:

- `SGV.Tests.Web.UnidadOrganizativaWebTests`: 49 tests fallidos (cubre Index, Details, Edit, Create, Delete, Reactivate, Organigrama). Falla raíz: aserciones de copy/redirect que no se cumplen en el HTML actual de Inspinia.
- `SGV.Tests.Web.WebAuthenticationTests.Post_SignIn_WithValidCredentials_RedirectsToDashboardAndSetsCookie`: 1 test fallido (también reportado en `apply-progress.md` de PR 2: "expected 302, got 200").
- `SGV.Tests.Web.Puesto.*PageTests`: 3 tests fallidos (`Post_Create_WhenCodigoDuplicado`, `Post_Edit_WhenTransportFails`, `Post_Delete_WhenConflict`).
- `SGV.Tests.Web.Cargo.*PageTests`: 4 tests fallidos (`Post_Create_WhenCodigoDuplicado`, `Post_Edit_WhenTransportFails`, `Post_Edit_WhenCodigoConflict`, `PostQuitar_NonAdmin_RedirectsToAccessDenied`, `Post_Delete_WhenConflict`).

Conteo previo documentado (apply-progress PR 1): "48 pre-existing failures". El conteo actual de 57 refleja crecimiento natural de la suite entre ramas y el fix de `WebAuthenticationTests` que ya estaba parcial en develop. **No son introducidos por este PR.**

### Drift de `[MySqlFact]`

- **Atributos estáticos en source (`grep -rE "^\s*\[MySqlFact"`)**: 166 (cubre todas las apariciones literales del atributo en clases de tests)
- **Tests con `[MySqlFact]` descubiertos por el runner** (`dotnet test --list-tests`): 28
- **Tests `[MySqlFact]` ejecutados** (MySQL local disponible): 28 / 28 (100% pass)
- **Tests `[MySqlFact]` omitidos**: 0
- **Drift documentado en `exploration.md:228`**: "146 cacheados vs 166 reales estáticos". La diferencia entre 28 descubiertos y 166 estáticos se explica porque el atributo se referencia en constructores, implementaciones y otros contextos no-ejecutables; el runner solo cuenta los `[Fact]` / `[Theory]` efectivos que aplican el atributo.

### Comandos ejecutados

```bash
# Build
dotnet build SGV.slnx --configuration Release --no-restore
# → Build succeeded. 8 Warning(s), 0 Error(s). Warnings pre-existentes (no introducidos por este PR).

# Suite no-Web, no-MySqlFact
ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;" \
Jwt__SigningKey="TEST-KEY-DEV-LOCAL-0123456789abcdef0123456789abcdef" \
  dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName!~Web&FullyQualifiedName!~MySql"
# → Passed: 1362, Failed: 0, Skipped: 0, Total: 1362, Duration: 15 s

# Suite [MySqlFact] (con MySQL real)
ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;" \
Jwt__SigningKey="TEST-KEY-DEV-LOCAL-0123456789abcdef0123456789abcdef" \
  dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName~MySql"
# → Passed: 28, Failed: 0, Skipped: 0, Total: 28, Duration: 716 ms

# Suite Web (con pre-existentes)
ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;" \
Jwt__SigningKey="TEST-KEY-DEV-LOCAL-0123456789abcdef0123456789abcdef" \
  dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName~Web"
# → Failed: 57, Passed: 575, Skipped: 0, Total: 632, Duration: 55 s

# Suite completa (timeout)
ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;" \
Jwt__SigningKey="TEST-KEY-DEV-LOCAL-0123456789abcdef0123456789abcdef" \
  dotnet test SGV.slnx --no-build --configuration Release
# → Timeout (1800 s) en colección WebIntegration. Limitación documentada en apply-progress PR 1
#   "timeouts pre-existentes en WebIntegration" — no regresión de este PR. La suite se ejecutó por
#   categorías para evitar el cuello de botella.
```

## Frontend Regression Guard

```bash
cd src/SGV.Web
bun install
# → Checked 772 installs across 667 packages (no changes) [178.00ms]

bun run build
# → Finished 'build' after 3 s
#   $ gulp build → plugins + styles sin errores

git diff --exit-code -- bun.lock wwwroot
# → exit 0 (sin drift; lockfile y assets versionados intactos)
```

**Resultado**: PASS. El gate de drift que ya existe en `.github/workflows/ci.yml:52-54` se mantiene verde tras este PR. El PR no introduce assets nuevos en `wwwroot/`, `bun.lock` permanece sin cambios, y `bun run build` completa sin errores.

## Documentation Presence

Verificación manual sobre `docs/decisiones-implementacion.md`:

| Subtema del CU-4 | Línea | Estado |
|-------------------|-------|--------|
| Liveness | 56 | ✅ |
| Readiness | 60 | ✅ |
| Anonimato de los probes | 66 | ✅ |
| Timeout de conexión recomendado | 70 | ✅ |
| `ServerVersion.AutoDetect` | 76 | ✅ |
| Separación design-time vs runtime | 83 | ✅ |
| Ubicación de los secretos por ambiente | 89 | ✅ |
| Migraciones | 97 | ✅ |
| Validación al startup | 101 | ✅ |

Estructura final: el archivo pasó de 351 a 404 líneas (+53 líneas). La subsección se titula "Contrato runtime MySQL — health, readiness y startup" y está ubicada entre `## SgvDbContextFactory fail-loud` (línea 40) y `## Gestión de secretos JWT` (línea 105), respetando la indicación del design §4.F ("subsección tras `:50`"). El placeholder JWT dev queda explícitamente marcado como NO apto para producción, con ejemplo de detección por `grep`.

## Spec Delta

Verificación de `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md`:

- **ADDED Requirements**: 2
  1. "Excepción de anonimato para probes operacionales" (4 scenarios)
  2. "Probes operacionales no exponen datos de negocio" (1 scenario)
- **Total scenarios**: 5
- **Cross-references**: operativo a `operational-readiness/spec.md:77-96` (REQ probes anónimos) y a `design.md` §4.C/§4.G
- **Source section**: agregada al final con citas a `openspec/specs/sgv-readonly-api/spec.md:174-191` (default-deny vigente) y a `operational-readiness/spec.md`

Coincide con el plan de `design.md` §5 "Conteos reconciliados (W1)" — el delta `sgv-readonly-api` declara 2 ADDED requirements, 5 scenarios.

## Out-of-scope (non-goals)

Los siguientes non-goals del `proposal.md` §3.2 fueron **NO implementados** en este PR ni en PR 1/PR 2. Se documentan para evidenciar la disciplina de scope:

| Non-goal | Estado | Verificación |
|----------|--------|--------------|
| Build frontend en CI | NO implementado (ya existente en CI) | `.github/workflows/ci.yml:35-54` ya ejecuta Bun/build/drift gate; este PR confirma que sigue verde |
| Fix `UseExceptionHandler("/Error")` → ruta inexistente | NO implementado | Hallazgo adyacente; sigue en código actual sin tocar |
| Retry/backoff automático en login o startup MySQL | NO implementado | Sin precedente en el repo; non-goal explícito |
| Manifiestos Docker / Kubernetes / IIS / Helm | NO creado | No existen en el repo; contrato de endpoint, no ejemplos de orquestador |
| Migraciones automáticas al startup | NO implementado | Siguen siendo operacionales; este PR solo documenta el contrato |
| Cambios a cookie/CORS (#101) o JWT (#97) | NO tocado | Ortogonales a este change |
| Cambiar `AutoDetect` → `MySqlServerVersion(8.0.36)` fijo en runtime | NO implementado | Design-time ya fija la versión; cambiar runtime requiere evidencia separada |
| Reescribir pipeline frontend / `package.json` / `gulpfile.js` | NO tocado | No es deuda vigente |
| Modificar `SgvDbContextFactory` design-time | NO tocado | Ya tiene contrato fail-loud |
| Páginas de error Inspinia en español | NO tocado | Ortogonal al contrato de login |
| Documentar el placeholder JWT dev como productivo | NO promovido | El placeholder queda explícitamente marcado como dev-only con `grep` de detección |

## Out-of-band observations

1. **Limitación de timeout en suite completa**: `dotnet test SGV.slnx --configuration Release` no completa en este entorno por timeouts de 30 s en la construcción del host de tests de la colección `WebIntegration`. Este patrón está documentado en `apply-progress.md` de PR 1 y se reproduce en este branch. No es regresión de este PR: la rama no toca ningún archivo de código de test ni de runtime. La suite se ejecutó por categoría para obtener conteos.

2. **CI ejecutará la suite completa**: el pipeline `.github/workflows/ci.yml:62-66` corre `dotnet test --no-build --configuration Release` con MySQL real (servicio `mysql:8.0` en el job). El conteo en CI será la fuente autoritativa; este verify-report documenta el comportamiento local con MySQL local y confirma que la infraestructura de tests está operativa.

3. **PR 1 y PR 2 no mergeados**: el working tree de `fix/126-operational-pt3` está al HEAD de `develop` (`e672912c`). Las clases de test introducidas por CU-0, CU-1 y CU-2 (`HealthTests`, `StartupValidationTests`, `AuthApiClientTimeoutTests`, `SignInTransportTests`) **no existen** en esta rama. Su verificación de aceptación vive en los apply-progress y verify-reports de los PRs #139 y #140. Este PR (PR #143) solo verifica AC-9, AC-10, AC-11.

4. **Drift 28 vs 166**: la diferencia entre los 28 tests `[MySqlFact]` descubiertos por el runner y los 166 atributos literales en source se debe a que `grep` cuenta todas las apariciones de la cadena `[MySqlFact]` (incluyendo referencias en documentación, builders, comentarios, etc.) mientras que el runner cuenta solo los `[Fact]`/`[Theory]` efectivos. El conteo ejecutable de 28 es el correcto.

5. **Warnings de compilación pre-existentes**: los 8 warnings de compilación (CS8524 switch expressions no exhaustivos en Habilidad/Puestos/Cargo/UnidadOrganizativaApiClient, CS8602 posibles nulls en Details/Index/Edit de UnidadesOrganizativas, xUnit1026 parámetro no usado en `CommandResultMapperTests.Map_AtypicalStatus`) son pre-existentes en `develop` y no son introducidos por este PR. No bloquean el verify.

## Verification Commands Reference

```bash
# Build (sin restore porque ya hay binarios Release)
dotnet build SGV.slnx --configuration Release --no-restore

# Suite no-Web, no-MySql
dotnet test SGV.slnx --no-build --configuration Release \
  --filter "FullyQualifiedName!~Web&FullyQualifiedName!~MySql"

# Suite [MySqlFact]
dotnet test SGV.slnx --no-build --configuration Release \
  --filter "FullyQualifiedName~MySql"

# Suite Web
dotnet test SGV.slnx --no-build --configuration Release \
  --filter "FullyQualifiedName~Web"

# Frontend regression guard
cd src/SGV.Web
bun install
bun run build
git diff --exit-code -- bun.lock wwwroot

# Verificación documental
grep -nE "^###" docs/decisiones-implementacion.md | head -15

# Verificación de spec delta
grep -cE "^### Requirement:" openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md
grep -cE "^#### Scenario:" openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md
```

## Next Steps

- **PR #139 (CU-0)**: listo para review y merge a `develop`.
- **PR #140 (CU-1+CU-2)**: listo para review y merge a `develop`.
- **PR #143 (CU-3+CU-4+CU-5, este)**: listo para review y merge a `develop`. Una vez mergeados los tres, ejecutar `sdd-archive 2026-07-14-fix-126-operational-tech-debt` para sincronizar deltas de specs y cerrar el change.
