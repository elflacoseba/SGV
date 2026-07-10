# Verify Report — `2026-07-10-extraer-contratos-sgv`

## Verification Report

**Change**: `2026-07-10-extraer-contratos-sgv` (issue #100 — extraer `SGV.Contracts` y romper `Web → Api`)
**Version**: N/A (refactor puro, sin delta funcional)
**Mode**: Strict TDD
**Branch**: `refactor/100-contracts-pr4-seguridad-cleanup`
**Rama base**: `develop` con PR1 (#108), PR2 (#109) y PR3 (#110) ya mergeados
**Verificador**: sub-agente `sdd-verify`
**Fecha**: 2026-07-10

### Completeness

| Métrica | Valor |
|---|---|
| Tasks totales (1.1–4.11) | 33 |
| Tasks completas | **33** |
| Tasks incompletas | 0 |
| PRs encadenados mergeados | 3 (PR1 #108, PR2 #109, PR3 #110) |
| PRs encadenados abiertos / aplicados localmente | 1 (PR4 — `f924d945`) |
| Archivos movidos a `src/SGV.Contracts/` | 36 (excl. `obj/`) |
| Subdominios implementados en Contracts | `Auth/`, `Organizacion/{Comandos,Consultas/Dtos}`, `Habilidades/{Comandos,Consultas/Dtos}`, `Seguridad/{RolesSgv,Usuarios}` |

### Build & Tests Execution

**Build**: ✅ Passed — 0 warnings / 0 errors

```
$ dotnet build SGV.slnx --configuration Release
  SGV.Contracts -> src/SGV.Contracts/bin/Release/net10.0/SGV.Contracts.dll
  SGV.Dominio -> src/SGV.Dominio/bin/Release/net10.0/SGV.Dominio.dll
  SGV.Aplicacion -> src/SGV.Aplicacion/bin/Release/net10.0/SGV.Aplicacion.dll
  SGV.Infraestructura -> src/SGV.Infraestructura/bin/Release/net10.0/SGV.Infraestructura.dll
  SGV.Api -> src/SGV.Api/bin/Release/net10.0/SGV.Api.dll
  SGV.Web -> src/SGV.Web/bin/Release/net10.0/SGV.Web.dll
  SGV.Tests -> tests/SGV.Tests/bin/Release/net10.0/SGV.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Tests**: ✅ 1613 passed / ❌ 12 failed (bit-idéntico al baseline pre-refactor)

```
$ dotnet test SGV.slnx --no-build --configuration Release
Failed!  - Failed:    12, Passed:  1613, Skipped:     0, Total:  1625, Duration: 49 s
```

Los 12 fallos son 100% pre-existentes y corresponden al bug #59 documentado en `AGENTS.md`:

```
SGV.Tests.Persistencia.OcupacionRepositoryTests.GetByIdIncludingHistoryAsync_ReturnsEvenIfDeleted
SGV.Tests.Persistencia.OcupacionRepositoryTests.UpdateAsync_WithFinalize_SavesFechaFin
SGV.Tests.Persistencia.OcupacionRepositoryTests.UpdateAsync_WithSoftDelete_SavesIsDeleted
SGV.Tests.Persistencia.OcupacionRepositoryTests.ListAllIncludingHistoryAsync_ReturnsAllRows
SGV.Tests.Persistencia.OcupacionRepositoryTests.ExistsActiveByPuestoAsync_Active_ReturnsTrue
SGV.Tests.Persistencia.OcupacionRepositoryTests.UpdateAsync_WithReactivation_ClearsFechaFinAndIsDeleted
SGV.Tests.Persistencia.OcupacionRepositoryTests.ExistsActiveByPersonaYPuestoAsync_DifferentPersona_ReturnsFalse
SGV.Tests.Persistencia.OcupacionRepositoryTests.ExistsActiveByPersonaYPuestoAsync_Active_ReturnsTrue
SGV.Tests.Persistencia.OcupacionRepositoryTests.GetByIdForUpdateAsync_Active_ReturnsWithNavigation
SGV.Tests.Persistencia.OcupacionRepositoryTests.ExistsActiveByPuestoAsync_ExcludingId_IgnoresSelf
SGV.Tests.Persistencia.OcupacionRepositoryTests.ListAllAsync_Default_ReturnsOnlyActiveRows
SGV.Tests.Persistencia.OcupacionRepositoryTests.ExistsActiveByPersonaYPuestoAsync_ExcludingId_IgnoresSelf
```

Traza común: `MySqlException : Incorrect integer value: '<uuid>' for column 'ActivePuestoIdUnique'`. Refleja incompatibilidad de tipos entre la migración inicial (`ActivePuestoIdUnique INT`) y `PuestoId CHAR(36)`. **No introducido ni tocado por este change** — pendiente en issue #59 separado.

**Coverage**: ➖ No solicitada en proposal (refactor puro, no se introdujeron tests nuevos). El change no toca lógica de negocio, solo namespaces y referencias.

### Spec Compliance Matrix

> La Engram-observation `sdd/2026-07-10-extraer-contratos-sgv/spec` (#843) declara formalmente: **"No corresponde crear delta spec funcional"**. La propuesta es refactor puro (`New Capabilities: None`, `Modified Capabilities: None`, sin cambios en endpoints, payloads JSON, autorización ni validaciones). Por tanto, la verificación de cumplimiento de spec es **preservación de comportamiento observable**, no "se cumplieron nuevos requirements".

| Spec revisada | Comportamiento | Estado |
|---|---|---|
| `web-apiclient-transport-contract` | Contratos de transporte/cancelación de clientes tipados se preservan (mismo `LoginResponse`/`LoginRequest`/`AuthApiRoutes` desde nuevo namespace) | ✅ COMPLIANT (preservado) |
| `puesto-management` | Endpoints, auth y contratos `2xx/401/403` se mantienen (DTOs/Requests migrados a `SGV.Contracts.Organizacion`) | ✅ COMPLIANT (preservado) |
| `cargo-management` | Reglas de cargos, segmentación y DTOs consumer-safe se mantienen | ✅ COMPLIANT (preservado) |
| `habilidad-management` | Contratos `/api/v1/skills`, queries, auth y DTOs se mantienen | ✅ COMPLIANT (preservado) |
| `sgv-web-authentication` | `AuthApiRoutes` sigue siendo definición centralizada compartida (ahora en `SGV.Contracts.Auth`); bridge cookie→JWT vigente | ✅ COMPLIANT (preservado) |
| `unidad-organizativa-crud` / `unidad-organizativa-web-listado` | Contratos de unidad organizativa, segmentación y reactivación se mantienen; `ActualizarUnidadOrganizativaRequest` no incluye `Codigo` (revisado contra `decisiones-implementacion.md` línea 83 — `SGV.Contracts.Organizacion.Comandos.ActualizarUnidadOrganizativaRequest`) | ✅ COMPLIANT (preservado) |
| `identity-user-role-management` | `RolesSgv` conserva catálogo y semántica; ahora en `SGV.Contracts.Seguridad` | ✅ COMPLIANT (preservado) |
| `skill-cargo-query-contract` / `cargo-skill-query-contract` / `persona-skill-query-contract` | Shapes de subrecursos y requisitos GET-only se mantienen | ✅ COMPLIANT (preservado) |

**Approval testing aplicado**: el baseline de tests vigente (1625 total) cubre los 8 specs vigentes. Como el conteo pasando/fallando es **bit-idéntico** (1613/1625), el comportamiento observable está preservado.

**Compliance summary**: 8/8 specs vigentes preservadas. 0 specs modificadas. 0 specs nuevas (por diseño).

### Correctness (Static Evidence)

| Requisito del success criteria | Estado | Evidencia |
|---|---|---|
| `dotnet build SGV.slnx` verde | ✅ Implementado | 0 warnings / 0 errors sobre los 7 proyectos (incluido el nuevo `SGV.Contracts`) |
| `dotnet test SGV.slnx` verde | ✅ Implementado | 1613/1613 tests no #59 pasan; mismos 12 fallos #59 que el baseline |
| `grep -r "using SGV.Aplicacion" src/SGV.Web` → 0 | ✅ Implementado | `(none)` |
| `grep -r "using SGV.Api.Contracts" src/` → 0 | ✅ Implementado | `(none)` (también verificado sobre `tests/`) |
| `SGV.Web.csproj` no referencia `SGV.Api` | ✅ Implementado | Única referencia: `ProjectReference=..\SGV.Contracts\SGV.Contracts.csproj` |
| Grafo final `Dominio ← Aplicacion ← Contracts ← {Api, Web}` | ✅ Implementado | `dotnet list reference` confirma; ver sección "Coherence" abajo |
| `AGENTS.md` menciona `SGV.Contracts` | ✅ Implementado | Línea 5 (grafo) + línea 23 (descripción de `src/SGV.Contracts/`) + línea 25 (`SGV.Api` deps) + línea 26 (`SGV.Web` depende únicamente de `SGV.Contracts`) + línea 74 (`Los wire-types consumidos por Web viven en SGV.Contracts`) |
| `docs/decisiones-implementacion.md` menciona namespace nuevo (línea 83) | ✅ Implementado | `SGV.Contracts.Organizacion.Comandos.ActualizarUnidadOrganizativaRequest` |
| Payloads JSON idénticos antes y después | ✅ Implementado (preservación) | Suite API vigente deserializa los mismos DTOs; los nombres de tipo no cambiaron (`CargoDto` sigue siendo `CargoDto`, solo cambió el namespace). Tests API/Web verdes |

### Coherence (Design)

| Decisión | Seguimiento | Notas |
|---|---|---|
| D1 — Nuevo `SGV.Contracts` classlib `net10.0` sin PackageReference de negocio | ✅ Sí | `SGV.Contracts.csproj` solo declara `<TargetFramework>net10.0</TargetFramework>` |
| D2 — Nueva arista `Aplicacion → Contracts` al mover tipos | ✅ Sí | `SGV.Aplicacion.csproj` referencia `SGV.Contracts` |
| D3 — 4 PRs encadenados por capa | ✅ Sí | PR1 (#108), PR2 (#109), PR3 (#110) mergeados a develop; PR4 aplicado localmente (`f924d945`) |
| D4 — Orden Auth → Organizacion → Habilidades → Seguridad | ✅ Sí | Grafo commit-level confirma el orden de merges en develop |
| Grafo `Dominio ← Aplicacion ← Contracts ← {Api, Web}` | ✅ Sí | Inspección `dotnet list reference`: |

```
$ dotnet list src/SGV.Web reference        →  ../SGV.Contracts/SGV.Contracts.csproj
$ dotnet list src/SGV.Api reference        →  ../SGV.Aplicacion + ../SGV.Contracts + ../SGV.Infraestructura
$ dotnet list src/SGV.Aplicacion reference →  ../SGV.Dominio + ../SGV.Contracts
$ dotnet list src/SGV.Contracts reference  →  (sin Project to Project references)
```

Web **NO** referencia Api. Contracts es leaf. ✅

### Diseño → código verificado

| Artefacto del diseño | Implementación real |
|---|---|
| `src/SGV.Contracts/Auth/AuthApiRoutes.cs` | ✅ Existe |
| `src/SGV.Contracts/Organizacion/Comandos/*.cs` (8 archivos) | ✅ Existen: `CargoRequests`, `CargoCommandResult`, `CargoSkillRequests`, `CargoSkillCommandResult`, `CargoSkillDeleteResult`, `PuestoRequests`, `PuestoCommandResult`, `UnidadOrganizativaRequests`, `UnidadOrganizativaCommandResult` |
| `src/SGV.Contracts/Organizacion/Consultas/Dtos/*.cs` (10 archivos) | ✅ Existen: `CargoDto`, `CargoListQuery`, `CargoSkillDetailDto`, `CargoSkillDto`, `NivelCargoDto`, `PuestoDto`, `TipoUnidadOrganizativaDto`, `UnidadOrganizativaDto`, `UnidadOrganizativaQuery`, `UnidadOrganizativaTreeNodeDto`, `PagedResult` |
| `src/SGV.Contracts/Habilidades/Consultas/Dtos/*.cs` | ✅ Existen: `HabilidadDto`, `NivelHabilidadDto`, `HabilidadListQuery`, `HabilidadCargosListQuery`, `SkillCargoDetailDto` |
| `src/SGV.Contracts/Habilidades/Comandos/*.cs` | ✅ Existen: `HabilidadRequests`, `HabilidadCommandResult` |
| `src/SGV.Contracts/Seguridad/RolesSgv.cs` | ✅ Existe |
| `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | ✅ Existe |
| `src/SGV.Api/Contracts/` eliminado | ✅ No existe |
| `src/SGV.Infraestructura/Seguridad/` (AuthServicio, UsuarioIdentityGateway) imports mixtos interfaces vs wire-types | ✅ Confirmado: `using SGV.Contracts.Seguridad.Usuarios` + `using SGV.Aplicacion.Seguridad.Usuarios` (interfaces quedan en Aplicación por design rule) |
| `src/SGV.Api/Controllers/AuthController.cs` mix auth routes + LoginRequest/Response + IAuthServicio | ✅ Confirmado: `SGV.Contracts.Auth` + `SGV.Contracts.Seguridad.Usuarios` + `SGV.Aplicacion.Seguridad.Usuarios` |
| `src/SGV.Api/Controllers/UsuariosController.cs` mix wire + interfaces | ✅ Confirmado |
| `src/SGV.Web/Integration/Auth/AuthApiClient.cs` consume solo Contracts | ✅ Confirmado: imports solo `SGV.Contracts.Auth` y `SGV.Contracts.Seguridad.Usuarios` |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` consume Contracts | ✅ Confirmado: imports todo desde `SGV.Contracts.*` |
| `tests/SGV.Tests/Web/WebAuthenticationTests.cs` consume Contracts | ✅ Confirmado: imports solo `SGV.Contracts.Seguridad.Usuarios` y `SGV.Contracts.Auth` |
| `AGENTS.md` y `decisiones-implementacion.md` ajustadas | ✅ Ambas actualizadas |

### TDD Compliance (Strict TDD)

> Esta es la dimensión Strict TDD del verify phase. Reviso el `apply-progress` (#848) contra el estado real.

| Check | Result | Detalles |
|---|---|---|
| `apply-progress` con "TDD Cycle Evidence" | ⚠️ No usa tabla TDD explícita | El apply-progress es narrativo-cumulativo (PR1+PR2+PR3+PR4). Para un refactor puro, no es violación: la regla del repo es "no agregar tests por el refactor, preservar suite vigente". |
| Tests nuevos esperados por el change | ➖ N/A | El change es refactor namespace-only — la proposal y el design declaran explícitamente: "Strict TDD se preserva: no se agregan tests por el refactor; la suite vigente ya cubre el comportamiento". |
| RED/GREEN/Triangulación por tarea | ➖ N/A | No aplica a refactor; no hay código nuevo que necesite ciclo RED→GREEN |
| Safety Net: suite baseline comparada pre/post | ✅ Sí | Pre-refactor baseline: 1625 total · 1613 passed · 12 failed. Post-refactor (verificado): 1625 total · 1613 passed · 12 failed. **Bit-idéntico**. |
| Approval testing aplicado | ✅ Sí | Mismo conteo pasando/fallando → comportamiento observable preservado |

**TDD Compliance**: 1/1 check relevante (safety net) pasó. No aplica Strict TDD al detalle RED/GREEN porque no hay tests nuevos — el change es refactor.

### Test Layer Distribution

> No se modificaron ni crearon tests nuevos (por diseño de refactor puro).

| Layer | Tests | Files touched | Tools |
|---|---|---|---|
| Unit | (sin cambios) | 0 | xUnit 2.9.2 |
| Integration API | `using` migrated en tests existentes | ≥ 6 (`ApiWebApplicationFactory`, `*ControllerTests`) | WebApplicationFactory + HttpClient |
| Integration Web | `using` migrated en tests existentes | ≥ 18 (Web/Habilidad, Cargo, Puesto, UnidadOrganizativa, WebAuth, WebShellSmoke) | `SgvWebApplicationFactory` + Razor Pages testing |
| Persistencia | `using` migrated en tests existentes | 1 (`DatosSemillaTests`) | xUnit + EF Core InMemory / MySQL |
| **Total tests creados o modificados** | **0 nuevos** · **~30+ migrados `using`** | — | |

### Quality Metrics

**Linter**: ➖ No configurado en el repo (no hay `.editorconfig` estricto ni analyzer rule set de Roslyn). El build de .NET 10 sirve como linter: **0 warnings**.

**Type Checker**: ✅ El compilador de .NET 10 cubre todo el type checking. Build verde con 0 errores = type check verde. El tipo de retorno de cada controller cambia con el namespace pero el shape del record no cambia, así que el binding JSON sigue idéntico.

### Issues Found

**CRITICAL**: None.

**WARNING**:

1. **12 fallos pre-existentes en `OcupacionRepositoryTests` (bug #59)** — incompatibilidad de tipos `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en la migración inicial. **No introducido por este change** (bit-idéntico al baseline). Está tracked en `AGENTS.md` y pendiente en una SDD change propia.

2. **Desviación documentada: `HabilidadDto` / `NivelHabilidadDto` adelantados en PR2** — eran necesarios en PR2 porque `CargoSkillDetailDto` (Organización) los consume, así que el PR3 los encontró ya migrados y los reutilizó sin duplicar. Decisión coherente con el orden de dependencias declarado en `design.md` (D4).

3. **Desviación documentada: `PackageReference System.IdentityModel.Tokens.Jwt 8.14.0` directo en `SGV.Web.csproj`** — al romper `Web → Api`, `AuthSessionFactory.cs` perdió la dependencia transitiva. Se hizo explícita en Web con la misma versión que usa `SGV.Infraestructura`. Compensa sin introducir regresión.

4. **Desviación documentada: `UsuarioContracts.cs` (Aplicación) se partió en dos** — el archivo en Aplicación hoy solo contiene interfaces (`IUsuarioIdentityGateway`, `IUsuarioServicioComandos`, `IUsuarioServicioConsulta`, `IRolServicioConsulta`, `IAuthServicio`); los wire-types viven en `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`. Las interfaces quedan en Aplicación por design rule (puertos de aplicación, no wire-types).

5. **Conteo real de tests modificados (18) mayor al estimado en task 4.6 (7)** — el task 4.6 se refería a tests "críticos de Seguridad/Auth", pero la verificación encontró otros tests Web que también consumían `LoginResponse`, `RolesSgv`, etc., y los migró. Sin impacto funcional.

6. **`IUsuarioActual` y otros puertos NO se movieron** — permanecen en `SGV.Aplicacion.Seguridad`. Coherente con la design rule "interfaces son contratos de aplicación, no wire-types". Algunos importadores (`Program.cs`, `AuditoriaSaveChangesInterceptor.cs`, `DependencyInjection.cs`) siguen importando `SGV.Aplicacion.Seguridad` legítimamente.

**SUGGESTION**:

1. Considerar homogeneizar el patrón de archivos en `src/SGV.Contracts/`: `Organizacion/Comandos/` está separado por propósito (`*Requests.cs`, `*CommandResult.cs` por separado), mientras que `Seguridad/Usuarios/` consolida los 8 wire-types en un único `UsuarioContracts.cs`. Es decisión de estilo, no funcional. Si se quiere consistencia absoluta, se podría separar en un commit aparte — pero NO es necesario para este change.

2. Considerar reordenar las carpetas de `src/SGV.Contracts/` alfabéticamente por subdominio en la raíz (`Auth/`, `Habilidades/`, `Organizacion/`, `Seguridad/`) para coincidir con el orden alfabético esperado. Estado actual: `Auth/`, `Habilidades/`, `Organizacion/`, `Seguridad/` → **ya está en orden alfabético**. No aplica.

### Verdict

**PASS WITH WARNINGS**

Refactor de namespace puro ejecutado limpio: el grafo objetivo `Dominio ← Aplicacion ← Contracts ← {Api, Web}` está Materializado en disco y `SGV.Web → SGV.Api` está ROTA. Los 33 tasks están completos, el build verde (0/0), y el conteo de tests es bit-idéntico (1613/1625) — la única fuente de fallos (bug #59) es pre-existente y no relacionada con este change. Las desviaciones documentadas son coherentes con el design y los principles declarados. Listo para `sdd-archive` (sync de delta specs — en este caso, archivado simbólico porque no hay delta funcional).

---

## Persistencia

Este reporte se persiste según el modo `hybrid`:

- **Filesystem**: `openspec/changes/2026-07-10-extraer-contratos-sgv/verify-report.md` (este archivo).
- **Engram**: `topic_key: sdd/2026-07-10-extraer-contratos-sgv/verify-report`, `type: architecture`, `capture_prompt: false`.
