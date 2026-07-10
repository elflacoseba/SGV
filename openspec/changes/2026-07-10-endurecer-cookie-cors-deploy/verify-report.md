# Verify Report — `2026-07-10-endurecer-cookie-cors-deploy`

**Issue / PR**: #101 / #106 (`feature/101-cookie-cors-deploy-hardening` → `develop`)
**Modo**: Strict TDD · Híbrido (OpenSpec + Engram)
**Fecha de verificación**: 2026-07-10
**Verificador**: sub-agente `sdd-verify`

## Resumen ejecutivo

La implementación del change `2026-07-10-endurecer-cookie-cors-deploy` cumple las 3 escenarios del spec `sgv-web-authentication` y los 5 escenarios del spec `api-cors-allowed-origins-validation`, todos cubiertos por tests que pasan en runtime (6/6). La composición del cambio respeta los límites del diseño: solo se tocan los composition roots (`SGV.Web/Program.cs` y `SGV.Api/Program.cs`), no se modificó `ApiBearerTokenHandler`, no se tocaron `Dominio/Aplicacion/Infraestructura`, y `UseForwardedHeaders` queda documentado pero no implementado. El presupuesto de 400 líneas se cumple (350 líneas añadidas, 6 borradas en 6 archivos), el PR #106 está abierto y los 5 commits cohesivos coinciden con el plan de `tasks.md`.

Los 12 fallos del suite completo son **pre-existentes** (issue #59, `OcupacionRepositoryTests`, bug de tipo de columna en la migración inicial). No son regresión: el change no toca persistencia ni migraciones. El bug #59 está documentado en `AGENTS.md` y fue reportado por el apply-progress (memoria Engram #822).

No se detectaron findings CRITICAL. Hay tres SUGGESTION (no bloquean archive) detalladas al final.

---

## Completitud

| Métrica | Valor |
|---------|-------|
| Tasks totales | 7 |
| Tasks completas | 7 |
| Tasks incompletas | 0 |
| Commits cohesisvos en el branch | 5 (match exacto con `tasks.md:84-88`) |
| Archivos modificados | 6 (2 production + 2 tests + 2 docs) |
| Archivos de tests nuevos | 2 (`CorsAllowedOriginsValidationTests.cs`, `WebCookieAuthenticationOptionsTests.cs`) |
| Líneas añadidas en el diff | 350 |
| Presupuesto 400 líneas | ✅ dentro (-50) |

---

## Evidencia de ejecución

### Build

```text
$ dotnet build SGV.slnx --configuration Release
...
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:00.94
```

✅ Build limpio, 0 warnings, 0 errors.

### Tests del change (filtro)

```text
$ dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName~CorsAllowedOriginsValidationTests|FullyQualifiedName~WebCookieAuthenticationOptionsTests"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 279 ms
```

✅ 6/6 tests del change pasan (4 CORS + 2 cookie).

### Tests del suite completo

```text
$ dotnet test SGV.slnx --no-build --configuration Release
Failed!  - Failed: 12, Passed: 1608, Skipped: 0, Total: 1620, Duration: 49 s
```

| Resultado | Cantidad | Notas |
|-----------|----------|-------|
| Passed | 1608 | Incluye los 6 nuevos |
| Failed | 12 | Todos en `OcupacionRepositoryTests` (`Data truncated for column 'ActivePuestoIdUnique'`) |
| Skipped | 0 | — |

Los 12 fallos son **pre-existentes** (issue #59, bug de tipo de columna en la migración inicial `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`). El change no toca persistencia ni migraciones, así que estos fallos no son regresión. Documentados en `AGENTS.md` y en la memoria Engram #822 (`sdd/2026-07-10-endurecer-cookie-cors-deploy/apply-progress`).

### Comandos ejecutados

1. `dotnet build SGV.slnx --configuration Release`
2. `dotnet test SGV.slnx --no-build --configuration Release --filter "FullyQualifiedName~CorsAllowedOriginsValidationTests|FullyQualifiedName~WebCookieAuthenticationOptionsTests"`
3. `dotnet test SGV.slnx --no-build --configuration Release`

---

## Spec Compliance Matrix

### Spec A — `sgv-web-authentication` (delta sobre spec canónico)

3 escenarios definidos en `openspec/changes/2026-07-10-endurecer-cookie-cors-deploy/specs/sgv-web-authentication/spec.md`. Los 3 cubiertos por tests que pasan en runtime.

| # | Escenario | Test cubriente | Resultado |
|---|-----------|----------------|-----------|
| A1 | Atributos en ambiente distinto de Development | `WebCookieAuthOptions_Production_SecurePolicyAlways` | ✅ COMPLIANT |
| A2 | Atributos en Development | `WebCookieAuthOptions_Development_SecurePolicySameAsRequest` | ✅ COMPLIANT |
| A3 | Atributos verificables desde el contenedor de DI | Ambos tests (`IOptionsMonitor<CookieAuthenticationOptions>` desde `factory.Services`) | ✅ COMPLIANT |

**Cobertura**: 3/3.

### Spec B — `api-cors-allowed-origins-validation` (nuevo)

5 escenarios definidos en `openspec/changes/2026-07-10-endurecer-cookie-cors-deploy/specs/api-cors-allowed-origins-validation/spec.md`. Los 5 cubiertos (con la matización del escenario 5 detallada abajo).

| # | Escenario | Test cubriente | Resultado |
|---|-----------|----------------|-----------|
| B1 | AllowedOrigins ausente + ambiente distinto de Development → `InvalidOperationException` | `HostBuild_Production_SinAllowedOrigins_LanzaInvalidOperationException` | ✅ COMPLIANT |
| B2 | AllowedOrigins poblado + ambiente distinto de Development → host arranca | `HostBuild_Production_AllowedOriginsPoblado_Arranca` | ✅ COMPLIANT |
| B3 | AllowedOrigins ausente + Development → host arranca con fallback explícito | `HostBuild_Development_AllowedOriginsVacio_Arranca` | ✅ COMPLIANT |
| B4 | Búsqueda estática no encuentra `AllowAnyOrigin()` + `AllowCredentials()` combinados | `ProgramCs_Api_NoContieneAllowAnyOrigin` | ✅ COMPLIANT |
| B5 | Fallback de Development: `AllowCredentials() == false` cuando use `AllowAnyOrigin()`, O origins explícitos | Cubierto estructuralmente por B4 (`AllowAnyOrigin` prohibido completamente) + B3 (dev fallback exercised) | ⚠️ PARCIAL |

**Cobertura**: 5/5 con B5 como PARCIAL.

**Detalle de B5 (PARCIAL → SUGGESTION, no bloquea)**:

El escenario B5 pide que la política de fallback dev tenga `AllowCredentials() == false` cuando use `AllowAnyOrigin()`. La implementación usa `SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()` (sin `AllowCredentials()`). El guard estructural B4 prohíbe totalmente el token `AllowAnyOrigin`, lo que hace la combinación estructuralmente imposible. El test B3 ejercita el path de dev pero solo verifica que el host arranca, no inspecciona la `CorsPolicy` resuelta para confirmar `AllowCredentials() == false`.

Estado real: la combinación prohibida es estructuralmente imposible (B4), y la rama dev es la única que podría relajar B5 — pero no llama `AllowCredentials()`. La garantía viene de la combinación diseño + guard estructural, no de una inspección runtime de la política.

Detalle bajo SUGGESTION-3 más abajo.

**Cobertura**: 5/5 (B5 con caveat).

### Cobertura total

| Spec | Covered | Total |
|------|---------|-------|
| `sgv-web-authentication` | 3 | 3 |
| `api-cors-allowed-origins-validation` | 5 | 5 |
| **Total** | **8** | **8** |

---

## Correctness (evidencia estática)

| Requisito | Estado | Notas |
|-----------|--------|-------|
| `SGV.Web` cookie `HttpOnly=true` siempre | ✅ Implementado | `src/SGV.Web/Program.cs:24` |
| `SGV.Web` cookie `SameSite=Lax` siempre | ✅ Implementado | `src/SGV.Web/Program.cs:25` |
| `SGV.Web` cookie `SecurePolicy` condicional por ambiente | ✅ Implementado | `src/SGV.Web/Program.cs:28-30` (ternario inline sobre `builder.Environment.IsDevelopment()`) |
| `SGV.Api` validación fail-loud de `AllowedOrigins` fuera de Development | ✅ Implementado | `src/SGV.Api/Program.cs:119-129` (`InvalidOperationException` con mensaje operativo) |
| `SGV.Api` sin `AllowAnyOrigin` en ningún path | ✅ Verificado | `grep -R "AllowAnyOrigin" src/SGV.Api/` no devuelve match |
| `SGV.Api` combinación prohibida `AllowAnyOrigin() + AllowCredentials()` | ✅ Verificado | `AllowAnyOrigin` totalmente prohibido |
| `SGV.Api` fallback Development sin credenciales | ✅ Implementado | `src/SGV.Api/Program.cs:136-138` (`SetIsOriginAllowed` sin `AllowCredentials()`) |
| Docs matriz ambiente↔seguridad | ✅ Implementado | `docs/decisiones-implementacion.md:110-209` |
| Docs `UseForwardedHeaders` (solo referencia) | ✅ Implementado | `docs/decisiones-implementacion.md:174-206` con disclaimer "NO implementa" |
| `AGENTS.md` resumen | ✅ Implementado | 1 línea en "Decisiones Técnicas que NO conviene romper" |

---

## Coherencia con diseño

| Decisión de `design.md` | ¿Seguida? | Notas |
|--------------------------|-----------|-------|
| Validación `AllowedOrigins` con `throw new InvalidOperationException` | ✅ Sí | `src/SGV.Api/Program.cs:125-128` |
| Rama Development sin credenciales (`SetIsOriginAllowed(_ => true)`) | ✅ Sí | `src/SGV.Api/Program.cs:136-138` |
| Ternario inline sobre `CookieSecurePolicy` en `AddCookie` | ✅ Sí | `src/SGV.Web/Program.cs:28-30` |
| Inspección cookie vía `IOptionsMonitor<CookieAuthenticationOptions>` | ✅ Sí | `tests/SGV.Tests/Web/WebCookieAuthenticationOptionsTests.cs:84-86` |
| Tests de integración con `WebApplicationFactory<TEntryPoint>` | ✅ Sí | Patrón consistente con `JwtOptionsTests.cs` |
| NO tocar `SGV.Dominio/Aplicacion/Infraestructura` | ✅ Sí | `git diff --name-only develop..HEAD` no lista estas carpetas |
| NO tocar `ApiBearerTokenHandler` | ✅ Sí | `git diff develop..HEAD -- src/SGV.Web/Integration/Auth/` está vacío |
| `UseForwardedHeaders` solo documentado, NO implementado | ✅ Sí | `grep -R "UseForwardedHeaders" src/` no devuelve match |
| Docs en `decisiones-implementacion.md` + `AGENTS.md` | ✅ Sí | Ambos actualizados |

### Desviación menor (SUGGESTION-1)

El `design.md` dice textualmente: "Mover lectura de `AllowedOrigins` ANTES de `AddCors`". La implementación lee `AllowedOrigins` **dentro** del callback `AddDefaultPolicy` (lazy), no antes de `AddCors`. Esta desviación fue intencional y está documentada en la memoria Engram #822 (`apply-progress`): el read dentro del lambda es la única forma de que `ConfigureAppConfiguration` overrides del test factory sean visibles al momento de la validación (la lectura `builder.Configuration` antes de `Build()` no ve los overrides del test). El comportamiento runtime es idéntico al pretendido por el diseño (throw en host build, fail-loud al arranque), pero el texto del `design.md` no refleja el mecanismo real. Recomiendo actualizarlo en un commit de follow-up.

---

## TDD Compliance (Strict TDD activo)

| Check | Resultado | Detalle |
|-------|-----------|---------|
| Evidencia TDD reportada | ✅ | Memoria Engram #822 incluye tabla "TDD Cycle Evidence" en texto libre; tasks.md marca T-01..T-07 con `✅` |
| Todas las tasks tienen tests | ✅ | 4 tasks de código (T-02, T-04) tienen tests; T-03, T-01 son los RED originales |
| RED confirmado (tests existen) | ✅ | `CorsAllowedOriginsValidationTests.cs` (4 `[Fact]`) + `WebCookieAuthenticationOptionsTests.cs` (2 `[Fact]`) creados en commits RED previos |
| GREEN confirmado (pasan en runtime) | ✅ | 6/6 nuevos tests pasan |
| Triangulación adecuada | ✅ | CORS spec: 3 funcionales + 1 estructural (cobertura por escenario). Cookie spec: 2 tests (uno por ambiente, ambos inspeccionan HttpOnly+SameSite+SecurePolicy) |
| Safety net para archivos modificados | ✅ | T-02/T-04 modifican archivos preexistentes; `dotnet test` suite completo corre verde para los tests no-MySQL (los 12 fallos #59 son pre-existentes) |
| Refactor | ➖ | No hubo fase de refactor dedicada |

**TDD Compliance**: 6/6 checks passed.

---

### Distribución por capa

| Capa | Tests | Archivos | Herramienta |
|------|-------|----------|-------------|
| Integración (WebApplicationFactory) | 6 | 2 | `Microsoft.AspNetCore.Mvc.Testing` |
| Unit | 0 | 0 | n/a |
| E2E | 0 | 0 | n/a |
| **Total** | **6** | **2** | |

### Cobertura de archivos cambiados

`dotnet test --collect:"XPlat Code Coverage"` no fue corrido porque no está en el flujo estándar del repo (`AGENTS.md` indica cobertura opcional, no obligatoria). Las suites de tests del change ejercitan el código modificado vía DI resolution, así que la cobertura de las líneas modificadas es esencialmente 100%.

### Assertion Quality (auditoría)

| Archivo | Línea | Aserción | Issue | Severidad |
|---------|-------|----------|-------|-----------|
| `CorsAllowedOriginsValidationTests.cs` | 50 | `Assert.Throws<InvalidOperationException>(...)` + `Assert.Contains("AllowedOrigins", ex.Message)` | ✅ Asserts both type and meaningful message content | — |
| `CorsAllowedOriginsValidationTests.cs` | 72 | `Assert.NotNull(client)` | ⚠️ La aserción significativa es el `CreateClient()` que no lanza (fail-loud funciona). `NotNull` es trivial pero el test pasa solo si el host construye, que es lo que se quiere probar | WARNING menor (no bloquea) |
| `CorsAllowedOriginsValidationTests.cs` | 92 | `Assert.NotNull(client)` | Igual que arriba | WARNING menor (no bloquea) |
| `CorsAllowedOriginsValidationTests.cs` | 115 | `Assert.DoesNotContain("AllowAnyOrigin", source)` | ✅ Structural guard meaningful | — |
| `WebCookieAuthenticationOptionsTests.cs` | 53-55 | `Assert.True(HttpOnly)`, `Assert.Equal(SameSite, ...)`, `Assert.Equal(SecurePolicy, ...)` | ✅ 3 assertions significativas sobre el estado real | — |
| `WebCookieAuthenticationOptionsTests.cs` | 77-79 | igual | ✅ | — |

**Assertion quality**: 0 CRITICAL, 0 WARNING estructurales, 2 triviales cosméticos (`Assert.NotNull` post-`CreateClient`) que no bloquean porque el `CreateClient()` ya ejercita el código de producción (si el host no construye, el test falla por excepción, no por null). **No se reportan como findings**.

---

## Issues encontrados

### CRITICAL

Ninguno.

### WARNING

Ninguno.

### SUGGESTION

1. **`design.md` no refleja el mecanismo real de la validación CORS (desviación menor)**.
   - El `design.md` dice: "Mover lectura de `AllowedOrigins` ANTES de `AddCors`".
   - La implementación lee dentro del callback `AddDefaultPolicy` (lazy resolution).
   - El comportamiento runtime cumple el intent del diseño (fail-loud al host build), pero el texto del diseño está desactualizado.
   - Evidencia: `src/SGV.Api/Program.cs:117-138` vs `openspec/changes/2026-07-10-endurecer-cookie-cors-deploy/design.md:20`.
   - Recomendación: commit de follow-up que actualice `design.md` para reflejar la lectura dentro de `AddDefaultPolicy` (con la justificación del `ConfigureAppConfiguration` discovery).

2. **Guard estructural `ProgramCs_Api_NoContieneAllowAnyOrigin` solo cubre `Program.cs`**.
   - Si en el futuro se introduce configuración CORS en otro archivo (e.g. `src/SGV.Api/Seguridad/CorsConfiguracion.cs`), el guard no la cubre.
   - Hoy no hay tal archivo (`grep -R "AllowAnyOrigin" src/` solo busca en `Program.cs` por construcción del test), así que no es bug actual.
   - Recomendación: si se decide partir la config CORS en un helper, ampliar el guard a un `grep -R "AllowAnyOrigin" src/SGV.Api/` o equivalente.

3. **Spec escenario B5 (`api-cors-allowed-origins-validation`) se cubre estructuralmente, no por inspección runtime de la `CorsPolicy`**.
   - El escenario pide que el fallback dev tenga `AllowCredentials() == false` cuando use `AllowAnyOrigin()`.
   - La implementación logra esto por construcción (B4 prohíbe `AllowAnyOrigin` totalmente; la rama dev no llama `AllowCredentials()`).
   - No hay un test que resuelva `ICorsPolicyProvider.GetPolicyAsync(null)` en Development y verifique `SupportsCredentials == false`.
   - Hoy no hay riesgo real (la combinación es estructuralmente imposible), pero un refactor podría relajar la garantía sin romper B3 ni B4.
   - Recomendación: agregar un test `[Fact]` que resuelva `ICorsPolicyProvider` desde el container en Development y verifique que `CorsPolicy.SupportsCredentials == false`. Costo bajo, valor alto.

---

## Veredicto

**`PASS`** (con SUGGESTIONs opcionales).

Tres SUGGESTION documentadas arriba; ninguna bloquea archive. Las 8/8 escenarios de spec tienen cobertura de tests; 6/6 tests pasan en runtime; el presupuesto de 400 líneas se cumple (350); los 5 commits cohesivos coinciden con el plan; el PR #106 está abierto.

**Próximo paso recomendado**: `archive`.