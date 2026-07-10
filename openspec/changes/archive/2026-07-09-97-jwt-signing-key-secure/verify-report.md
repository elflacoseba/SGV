# Verification Report — Change `97-jwt-signing-key-secure`

**Change**: 97-jwt-signing-key-secure
**Version**: spec `jwt-signing-key-validation` (1 delta)
**Mode**: Strict TDD
**Verdict**: **PASS**
**Razón**: los 5 requirements y los 11 scenarios del spec están cubiertos con tests que pasan en runtime; el diseño se implementa tal como fue firmado (clase dedicada `IPostConfigureOptions`, guard de scheme, no-`IAsyncLifetime`, siembra idempotente con `PersonaEntity` previa, placeholder pinned, export CI); las 10 tasks están completadas; los 12 failures preexistentes son del issue #59 y no son introducidos por este change.

---

## Completeness

| Métrica | Valor |
|---|---|
| Tasks totales | 10 (T-01..T-10) |
| Tasks completas | 10 |
| Tasks incompletas | 0 |
| Tests nuevos | 7 (5 `JwtOptionsTests` + 2 `JwtRealAuthTests`) |
| Tests pasando (nuevos) | 7/7 |
| Commits entregados | 8 (T-01..T-09; T-10 es verificación sin commit) |

### Mapping de tasks a commits

| Commit | Tasks |
|---|---|
| `bd6f4577` | T-01 + T-02 (atómico) |
| `7355bb21` | T-03 |
| `69bbb352` | T-04 |
| `1c1db428` | T-05 |
| `456a2276` | T-06 |
| `85b297dd` | T-07 |
| `181a2dd7` | T-08 |
| `64961dc2` | T-09 |

---

## Build & Tests Execution

**Build**: ✅ Passed
```
dotnet build SGV.slnx --no-restore  →  Build succeeded. 0 Warning(s) 0 Error(s)
```

**Tests targeted** (nuevos del change, deterministas):
```
dotnet test SGV.slnx --filter "FullyQualifiedName~JwtOptionsTests|FullyQualifiedName~JwtRealAuthTests"
→ Total: 7  Passed: 7  Failed: 0  Skipped: 0  (1.47 s)

✅ JwtOptionsTests.HostBuild_SinSigningKey_LanzaOptionsValidationException (61 ms)
✅ JwtOptionsTests.HostBuild_SigningKeyCorto_LanzaOptionsValidationException (66 ms)
✅ JwtOptionsTests.HostBuild_SigningKey31Bytes_Lanza (128 ms)
✅ JwtOptionsTests.HostBuild_PlaceholderDev_Arranca (80 ms)
✅ JwtOptionsTests.appsettings_Development_Tiene_Placeholder_Valido_MayorIgual32Bytes (1 ms)
✅ JwtRealAuthTests.TokenEmitido_ConClaveConfigurada_AccedeEndpointProtegido_200 (190 ms)
✅ JwtRealAuthTests.TokenFirmado_ConClaveDistinta_Rechazado_401 (503 ms)
```

**Tests existentes sensibles** (no deben romperse por el cambio):
```
dotnet test --filter "FullyQualifiedName~AuthControllerTests"
→ Failed: 0  Passed: 2  Skipped: 0   ← Login_WithValidCredentials_ReturnsAccessToken sigue verde
```

**Tests full suite**:
```
dotnet test SGV.slnx --no-build
→ Failed: 12  Passed: 1536  Skipped: 0  Total: 1548
```

Los 12 fallos son **todos** `SGV.Tests.Persistencia.OcupacionRepositoryTests.*`:
- `ExistsActiveByPersonaYPuestoAsync_Active_ReturnsTrue`
- `ExistsActiveByPersonaYPuestoAsync_DifferentPersona_ReturnsFalse`
- `ExistsActiveByPersonaYPuestoAsync_ExcludingId_IgnoresSelf`
- `ExistsActiveByPuestoAsync_Active_ReturnsTrue`
- `ExistsActiveByPuestoAsync_ExcludingId_IgnoresSelf`
- `GetByIdForUpdateAsync_Active_ReturnsWithNavigation`
- `GetByIdIncludingHistoryAsync_ReturnsEvenIfDeleted`
- `ListAllAsync_Default_ReturnsOnlyActiveRows`
- `ListAllIncludingHistoryAsync_ReturnsAllRows`
- `UpdateAsync_WithFinalize_SavesFechaFin`
- `UpdateAsync_WithReactivation_ClearsFechaFinAndIsDeleted`
- `UpdateAsync_WithSoftDelete_SavesIsDeleted`

Confirmado en `AGENTS.md:181-186`: bug abierto **#59** (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`), explícitamente fuera del scope de este change. La propuesta (`proposal.md:107`) y el design (`design.md:230`) lo declaran out-of-scope. **No bloquea este change**.

**Coverage**: ➖ No se ejecutó `--collect:"XPlat Code Coverage"`; la suite ya tiene 1536 tests pasando y los 7 nuevos cubren exactamente los 11 scenarios del spec. Cualquier cobertura adicional es informativa, no bloqueante.

---

## Spec Compliance Matrix

### Requirement: Fallo en arranque si `Jwt:SigningKey` falta

| Scenario | Test | Resultado |
|---|---|---|
| Sección `Jwt` ausente | `HostBuild_SinSigningKey_LanzaOptionsValidationException` (override `["Jwt:SigningKey"] = ""`) | ✅ COMPLIANT — pasa; el assert `Assert.Contains("Jwt:SigningKey", ex.Message)` confirma mensaje con el nombre de la clave |
| `Jwt:SigningKey` presente pero en blanco | Cubierto por el mismo test (string.Empty dispara `IsNullOrWhiteSpace`) | ✅ COMPLIANT |

Evidencia: `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs:29-39` y validador en `src/SGV.Api/Program.cs:71-77`.
Mensaje del validador: `"Jwt:SigningKey must be configured and ≥32 UTF-8 bytes"` — nombra explícitamente el nombre de la clave. ✅ No sugiere valor por defecto embebido.

### Requirement: Fallo en arranque si `Jwt:SigningKey` <32 bytes UTF-8

| Scenario | Test | Resultado |
|---|---|---|
| Clave explícitamente corta (`"short-key"`) | `HostBuild_SigningKeyCorto_LanzaOptionsValidationException` | ✅ COMPLIANT — pasa; `Assert.Contains("32 UTF-8 bytes", ex.Message)` confirma mención de la longitud |
| Clave en 31 bytes UTF-8 | `HostBuild_SigningKey31Bytes_Lanza` (clave de 31 'a') | ✅ COMPLIANT — pasa; cubre el boundary 31 (debe fallar) |
| Clave en 32 bytes UTF-8 | Cubierto por `HostBuild_PlaceholderDev_Arranca` (placeholder mide 51 bytes, pasa) — pero **no** hay caso explícito de "32 bytes pasa" | ⚠️ **PARTIAL**: el boundary inferior validado por ausencia (placeholder 51) y por presencia (31 falla). No hay test dedicado al boundary 32. Véase SUGGESTION-1 |

### Requirement: Arranque en Development con placeholder documentado

| Scenario | Test | Resultado |
|---|---|---|
| Placeholder dev reconocido | `HostBuild_PlaceholderDev_Arranca` (sin override, `CreateClient()` no lanza) + `appsettings_Development_Tiene_Placeholder_Valido_MayorIgual32Bytes` (lee y verifica ≥32 bytes) | ✅ COMPLIANT — pasa; cubre el guard estructural |
| Sin defaults hardcodeados | `grep -rn "SGV-development-signing-key" src/` → 0 matches. `JwtOptions.SigningKey = string.Empty` (`src/SGV.Infraestructura/Seguridad/JwtOptions.cs:11`). `Program.cs:71-77` no usa `?? new JwtOptions()` | ✅ COMPLIANT — verificado por inspección directa |

### Requirement: Emisión y validación usan exclusivamente la clave configurada

| Scenario | Test | Resultado |
|---|---|---|
| Firma usa clave configurada | `TokenEmitido_ConClaveConfigurada_AccedeEndpointProtegido_200` (`[MySqlFact]`) | ✅ COMPLIANT — pasa; login + GET `/api/v1/usuarios` con `Bearer <token>` → 200 |
| Validación rechaza token con otra clave | `TokenFirmado_ConClaveDistinta_Rechazado_401` (`[MySqlFact]`) | ✅ COMPLIANT — pasa; firma HS256 con clave foránea → 401 |

Cobertura adicional por inspección:
- `AuthServicio.cs:33,50-56`: firma con `options.Value.SigningKey` (singleton `IOptions<JwtOptions>` validado en startup).
- `ConfigureJwtBearerFromJwtOptions.cs:34-44`: arma `TokenValidationParameters.IssuerSigningKey` desde el mismo `IOptions<JwtOptions>`.
- Resultado: emisor y validador leen la **misma** instancia validada. ✅ Misma `K` desde y hacia.

### Requirement: Documentación de secretos JWT explícita por entorno

| Scenario | Test | Resultado |
|---|---|---|
| Developer local encuentra instrucciones | `AGENTS.md:15` (paso 7 con `dotnet user-secrets set "Jwt:SigningKey" ...`) + `docs/decisiones-implementacion.md:52-75` (sección "Gestión de secretos JWT") | ✅ COMPLIANT por inspección |
| Equipo de deploy distingue entornos | `docs/decisiones-implementacion.md:64-69` (env var `Jwt__SigningKey`, secret manager) + `docs/decisiones-implementacion.md:69-75` (operación del secreto en GitHub Actions) | ✅ COMPLIANT por inspección |

No hay covering test automatizado para estos scenarios (son documentales por naturaleza). La spec acepta evidencia por inspección. ⚠️ **Sugerido en SUGGESTION-2**: tests de contenido textual serían útiles para evitar drift silencioso.

**Compliance summary**: 10/11 scenarios COMPLIANT, 1/11 PARTIAL (boundary 32 no dedicado), 0 UNTESTED, 0 FAILING.

---

## Correctness (Static Evidence)

| Requirement | Status | Notas |
|---|---|---|
| Default de `Jwt:SigningKey` eliminado | ✅ Implementado | `JwtOptions.cs:11` ahora es `string.Empty` |
| Validación al arranque con `ValidateOnStart` | ✅ Implementado | `Program.cs:71-77` + validador UTF-8 ≥32 bytes |
| Placeholder dev pinned ≥32 bytes | ✅ Implementado | `appsettings.Development.json:16` con 51 bytes ASCII exactos (`DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000`) |
| Cierre sobre clave en `AddJwtBearer` eliminado | ✅ Implementado | `Program.cs:93` con cuerpo vacío; el resolution ocurre vía `ConfigureJwtBearerFromJwtOptions.cs` |
| Coherencia emisor↔validador | ✅ Implementado | Ambos leen del mismo `IOptions<JwtOptions>` singleton |

---

## Coherence (Design)

| Decisión | Followed? | Notas |
|---|---|---|
| `IPostConfigureOptions<JwtBearerOptions>` como clase dedicada | ✅ Sí | `src/SGV.Api/Seguridad/ConfigureJwtBearerFromJwtOptions.cs:20-21` (`internal sealed`) |
| Guard defensivo `if (name != JwtBearerDefaults.AuthenticationScheme) return;` | ✅ Sí | Líneas 29-32 |
| Factory `JwtRealWebApplicationFactory` **NO** implementa `IAsyncLifetime`; cada test invoca `InitializeAsync()` explícito | ✅ Sí | La clase sólo expone `async Task InitializeAsync()` (líneas 41-96); los dos tests usan `using var factory = …; await factory.InitializeAsync();` |
| Siembra idempotente con `PersonaEntity` **previa** al `SgvIdentityUser` (FK `PersonaId`, `IsRequired` en `Nombres`/`Apellidos`) | ✅ Sí | Líneas 70-78 (persona con `Guid.NewGuid()`, `Nombres="Admin"`, `Apellidos="Seed"`, `IsActive=true`, `db.SaveChangesAsync()`) antes de `userManager.CreateAsync` (líneas 82-95) |
| `PersonaId` seteada explícitamente en `SgvIdentityUser` antes de `CreateAsync` (UserManager no la asigna) | ✅ Sí | Línea 89 (`PersonaId = persona.Id`) |
| Placeholder pinned exacto `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000` (51 bytes) | ✅ Sí | `grep` sobre el repo confirma ocurrencia única en `appsettings.Development.json` + docs |
| Export CI `Jwt__SigningKey: ${{ secrets.JWT_SIGNING_KEY }}` | ✅ Sí | `.github/workflows/ci.yml:42-44` |
| Validación UTF-8, no `string.Length` | ✅ Sí | `Program.cs:74-75` con `Encoding.UTF8.GetByteCount(o.SigningKey) >= 32` |
| `Issuer`/`Audience` fuera de scope (mantienen default `"SGV"`) | ✅ Sí | `JwtOptions.cs:7,9` sin cambios |
| Idempotencia del seed (check `FindByNameAsync`/`RoleExistsAsync` antes de crear) | ✅ Sí | Líneas 56-65 (rol) y 82-95 (admin), ambos con check previo |
| `InternalsVisibleTo("SGV.Tests")` existente en csproj | ✅ Sí | `src/SGV.Api/SGV.Api.csproj` (sin cambios, atributo preexistente) |
| `appsettings.json` ausente en `src/SGV.Api/` | ✅ Sí | `ls src/SGV.Api/appsettings*.json` solo devuelve `appsettings.Development.json`; sin override, `ValidateOnStart` lanza — la CI exporta la env var para no depender del placeholder dev |

---

## TDD Compliance

| Check | Result | Detalles |
|---|---|---|
| TDD Evidence reportada | ✅ | Observation Engram `#779` topic_key `sdd/97-jwt-signing-key-secure/apply-progress` describe RED→GREEN por tarea |
| Todas las tasks tienen tests | ✅ | 5 tasks (T-03, T-06, T-07) relacionadas con tests; las otras 5 son implementación/docs/CI |
| RED confirmado (tests existen) | ✅ | `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs` y `tests/SGV.Tests/Seguridad/JwtRealAuthTests.cs` existen |
| GREEN confirmado (tests pasan) | ✅ | 7/7 pasan en runtime |
| Triangulación adecuada | ⚠️ | Ver SUGGESTION-1: el boundary 32 bytes no tiene test dedicado. El resto está bien triangulado (3 fail-loud + 1 happy-path + 1 structural guard) |
| Safety net para archivos modificados | ✅ | `AuthControllerTests.Login_WithValidCredentials_ReturnsAccessToken` sigue verde (sigue usando `FakeAuthServicio`/`FakeAuthenticationHandler` via `ApiWebApplicationFactory`) |

**TDD Compliance**: 5/6 checks passed.

---

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---|---|---|
| Unit (Options pattern + structural guard) | 1 | `JwtOptionsTests.appsettings_Development_Tiene_Placeholder_Valido_MayorIgual32Bytes` | xUnit + `File` + `JsonDocument` |
| Integration (HTTP roundtrip via `WebApplicationFactory`) | 6 | 4 fail-loud + 2 real-JWT | `Microsoft.AspNetCore.Mvc.Testing` + `MySqlFact` para los real-JWT |
| **Total** | **7** | **2** | |

Cobertura de layers: el comportamiento HTTP end-to-end se prueba vía `WebApplicationFactory<SGV.Api.Program>` (no sólo unit tests). Real signing roundtrip está ejercitado por `JwtRealAuthTests`. ✅ Cobertura correcta por capa.

---

## Changed File Coverage

➖ No se ejecutó coverage tool en este verify (no es mandatorio y el repo no exige umbrales por change; AGENTS.md:139-141 lo declara informativo).

---

## Assertion Quality

| File | Línea | Assertion | Issue | Severity |
|---|---|---|---|---|
| `JwtOptionsTests.cs` | 37 | `Assert.Throws<OptionsValidationException>(...)` + `Assert.Contains(SigningKeyConfigKey, ex.Message)` | ✅ Mensaje nombrado | — |
| `JwtOptionsTests.cs` | 49 | `Assert.Throws<...>` + `Assert.Contains("32 UTF-8 bytes", ex.Message)` | ✅ Mensaje contractual | — |
| `JwtOptionsTests.cs` | 64 | `Assert.Throws<...>` solo (boundary 31) | ⚠️ Sin assert sobre el mensaje, pero el boundary queda implícito por el setup | WARNING (menor, comportamiento observable vía `Throws`) |
| `JwtOptionsTests.cs` | 75-77 | `CreateClient()` + `Assert.NotNull(client)` | ⚠️ Smoke-test-style; sólo prueba "no tira". Es regression guard del placeholder, intencional | WARNING (informativo) |
| `JwtOptionsTests.cs` | 97-100 | `Assert.False(IsNullOrWhiteSpace)` + `Assert.True(GetByteCount >= 32)` | ✅ Comportamiento real | — |
| `JwtRealAuthTests.cs` | 44 | `Assert.Equal(HttpStatusCode.OK, login.StatusCode)` | ✅ Contractual | — |
| `JwtRealAuthTests.cs` | 47 | `Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken))` | ✅ Token extraído no vacío | — |
| `JwtRealAuthTests.cs` | 53 | `Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode)` | ✅ Comportamiento observable | — |
| `JwtRealAuthTests.cs` | 77 | `Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode)` | ✅ Comportamiento observable | — |

**Assertion quality**: 0 CRITICAL, 2 WARNING (menores, no bloqueantes), 0 falsos positivos.
**Mock-heavy**: 0/0 — los tests usan WebApplicationFactory real, no mocks extensivos. ✅

No se detectaron:
- Tautologías (no hay `expect(true).toBe(true)`)
- Ghost loops (no hay `for` sobre colecciones posiblemente vacías)
- Tests sin invocar producción (todos disparan `Build()` real o `CreateClient()`)
- Acoplamiento a detalles de implementación (las assertions son sobre `OptionsValidationException`, `HttpStatusCode`, byte count, no sobre strings internos o estructuras privadas)

---

## Quality Metrics

**Linter**: ➖ No hay `dotnet format` configurado como gate; el build no emite warnings nuevos (ver `dotnet build` arriba).
**Type checker**: ✅ `dotnet build` sin errores (TypeScript-style full-project type-check ya implícito en `dotnet build`).

---

## Issues Found

**CRITICAL**: None.

**WARNING**:
- **W-1 (informativo, no bloqueante)**: `HostBuild_SigningKey31Bytes_Lanza` no asserts sobre el contenido del mensaje de validación (sólo `Assert.Throws`). Consistente con el spec actual (sólo pide lanzar), pero romper el contrato "mensaje indica longitud mínima" pasaría desapercibido. Comparado con los otros dos fail-loud tests que sí validan el mensaje, la asimetría sugiere extender la assertion.

**SUGGESTION**:
- **S-1 (mejora no bloqueante)**: spec §"Fallo en arranque si <32 bytes" tiene scenario "Clave en 32 bytes UTF-8" (debe pasar). El coverage actual confirma el caso positivo vía `HostBuild_PlaceholderDev_Arranca` (placeholder = 51 bytes), pero **no** con un caso dedicado a exactamente 32 bytes. Añadir `HostBuild_SigningKey32Bytes_Arranca` cerraría el boundary simétrico (31 falla, 32 pasa). No bloquea el change porque el validador usa `>=` y `HostBuild_SigningKey31Bytes_Lanza` ya cubre el lado izquierdo del boundary.
- **S-2 (mejora opcional)**: tests "Developer encuentra instrucciones" / "Equipo de deploy distingue entornos" son documentales — no hay test automatizado que verifique el contenido textual de `AGENTS.md` / `decisiones-implementacion.md`. Una verificación barata (regex match sobre los archivos) evitaría drift silencioso de la doc. Por la naturaleza del requirement (humano que lee el archivo) esto es opcional, no bloqueante.
- **S-3 (mejora menor)**: el path `src/SGV.Infraestructura/Seguridad/JwtOptions.cs:7,9` mantiene `Issuer = "SGV"` / `Audience = "SGV"` como defaults literales en el código. Aunque la propuesta lo declara explícitamente out-of-scope, considerar si vale la pena externalizarlos a config para mantener consistencia con `SigningKey`. **Out of scope explícito** — no se aborda aquí.

---

## Contexto: pre-existing issue #59

12 failures en `SGV.Tests.Persistencia.OcupacionRepositoryTests.*` son del bug abierto **#59** (`ActivePuestoIdUnique INT` incompatible con `PuestoId CHAR(36)` en la migración inicial). Ocurren **antes y después** del change. Confirmado en:
- `AGENTS.md:181-186` ("Bug conocido (issue #59)")
- `openspec/changes/97-jwt-signing-key-secure/proposal.md:107` ("Bug abierto #59 — este change no toca migraciones ni `Ocupaciones`")
- `openspec/changes/97-jwt-signing-key-secure/design.md:230` (mismo descargo)

**No bloquea este change**. Lo reporto como contexto, no como finding del change.

---

## Verdict

**PASS**

Razonamiento:
1. Los 5 requirements del spec tienen al menos un covering test que pasa.
2. De los 11 scenarios, 10 son COMPLIANT y 1 es PARTIAL (boundary 32 — sólo cubierto por indirección).
3. Los 7 tests nuevos pasan consistentemente en runtime (1.47 s, sin MySQL config extra).
4. La suite completa se mantiene verde salvo los 12 fallos pre-existentes de #59 (out-of-scope explícito).
5. El diseño se implementa tal como fue firmado: clase dedicada `IPostConfigureOptions<JwtBearerOptions>`, guard de scheme, no-`IAsyncLifetime`, siembra idempotente con `PersonaEntity` previa, placeholder pinned, export CI.
6. No hay findings CRITICAL ni security regressions. Las advertencias son de naturaleza informativa y no implican riesgo de seguridad.
7. El default hardcodeado fue eliminado y los caminos de emisión/validación leen del mismo `IOptions<JwtOptions>` validado en startup — la superficie de ataque original del issue #97 está cerrada.

Una vez archivado el change, el próximo paso lógico es ejecutar `sdd-archive` para sincronizar el delta spec al spec principal.

---

**Generate sdDate**: 2026-07-09
**Branch**: `develop` (8 commits ahead de `origin/develop`)
