# Archive Report: web-apiclient-transport-failure-coverage

## Status

ARCHIVED

## Resumen

El change `web-apiclient-transport-failure-coverage` cerró su ciclo SDD y fue movido a `openspec/changes/archive/2026-07-03-web-apiclient-transport-failure-coverage/`.
La capability baseline `web-apiclient-transport-contract` se revisó contra su delta y no requirió cambios: el spec principal ya conserva los dos requisitos transversales y el delta archivado documenta la aplicación concreta a `HabilidadApiClient` y `CargoApiClient`.
La verificación final sigue en estado `READY-FOR-MERGE`, con los mismos 12 fallos baseline conocidos de `OcupacionRepositoryTests` (#59) y sin regresiones nuevas.

## Metadata del change

| Campo | Valor |
|-------|-------|
| Issue | #78 |
| Tipo | Test hardening (sin feature nueva) |
| Modo | openspec |
| Strict TDD | sí |
| Branch base | develop |
| Delivery strategy | single PR con `size:exception` |
| Cambios en código de tests | 4 archivos (`git diff 0e635ddb^..HEAD -- tests/SGV.Tests`): +296 / -70, net +226 |
| Cambios en docs SDD del change | 3 archivos versionados durante apply/verify (`tasks.md`, `apply-progress.md`, `verify-report.md`); este archive agrega `archive-report.md` |

## Spec sync

### Decisión

**No-op semántico sobre el baseline**.

### Evidencia

- Baseline actual: `openspec/specs/web-apiclient-transport-contract/spec.md`
- Delta archivado: `openspec/changes/archive/2026-07-03-web-apiclient-transport-failure-coverage/specs/web-apiclient-transport-contract/spec.md`
- Tipo de delta detectado: `## ADDED Requirements`

### Razonamiento

El baseline ya define el contrato transversal con dos requisitos genéricos:

1. `Propagar fallos nativos de transporte`
2. `Respetar cancelación cooperativa del consumidor`

El delta no modifica ni elimina esos requisitos. Sólo agrega una requirement contextual que vincula explícitamente ese contrato a `HabilidadApiClient` y `CargoApiClient` dentro de la historia del change. Por lo tanto, consolidar ese bloque en el baseline habría duplicado semántica ya cubierta por la capability principal.

Como ajuste mecánico no funcional, el encabezado `## Propósito` del baseline se normalizó a `## Purpose` para que esta capability cumpla el validador actual de OpenSpec. No cambia requisitos ni escenarios.

### Diff baseline antes/después

```diff
-## Propósito
+## Purpose
```

## Test results finales

```text
$ dotnet test SGV.slnx --no-build --configuration Release
Failed!  - Failed:    12, Passed:  1254, Skipped:     0, Total:  1266, Duration: 33 s - SGV.Tests.dll (net10.0)
```

Los 12 fallos siguen siendo el baseline conocido de `OcupacionRepositoryTests` (#59), asociado al bug de migración `ActivePuestoIdUnique`. No aparecieron fallos nuevos del change archivado.

## Commits

| SHA | Mensaje | Files changed | Lines |
|-----|---------|---------------|-------|
| `0e635ddbe82922b5d4efb884089bc9709eb1e3a7` | `test(web): add shared HttpClientExceptionScenarios helper for transport failures` | 2 | +176 / -0 |
| `e4dac3482f52f100e0cda63c5af0389915ee7e25` | `test(web): migrate HabilidadApiClientTests to shared helper and add transport failure coverage` | 2 | +51 / -30 |
| `25a779749fb31ebc3c2cd8e3314b341529ccd22d` | `test(web): add transport failure coverage for CargoApiClient via shared helper` | 1 | +57 / -41 |
| `b548879b1cf456b4659a057a4d0d420706dc223f` | `docs(sdd): record apply progress for web-apiclient-transport-failure-coverage` | 2 | +215 / -0 |
| `d64e37585e55351c10a0c84c06c24e4238f88642` | `test(web): address verify warnings on HttpClientExceptionScenarios` | 2 | +18 / -5 |
| `9eafa5cde53337686baa7449dd5abfc84fdb8d36` | `docs(sdd): update apply-progress and verify-report for remediation commit` | 2 | +190 / -0 |
| `HEAD (archive)` | `docs(sdd): archive web-apiclient-transport-failure-coverage and sync delta specs` | Pendiente al momento de redactar este archivo; este mismo commit cierra el change | N/A |

## Capabilities impactadas

- `web-apiclient-transport-contract` (NUEVA): introduce 2 requisitos baseline transversales a todos los clientes HTTP tipados de `SGV.Web`.

## Acceptance criteria

- ✓ `HabilidadApiClientTests` cubre `TaskCanceledException`, `HttpRequestException` y token pre-cancelado sin invocar handler.
- ✓ `CargoApiClientTests` cubre los mismos escenarios.
- ✓ El helper compartido vive en `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs`.
- ✓ `dotnet test SGV.slnx` conserva únicamente el baseline conocido de `OcupacionRepositoryTests` issue #59.

## Risks / Notas

- El PR esperado superará el presupuesto nominal de 400 líneas, pero ya tiene excepción explícita `size:exception` definida por el usuario.
- `proposal.md`, `design.md` y el baseline `openspec/specs/web-apiclient-transport-contract/spec.md` existían localmente sin commit previo; este archive los incorpora en el historial junto con el resto del change para no perder trazabilidad.
- El filtro específico del verify ejecutó 58 tests porque también matchea suites/fakes relacionadas por nombre; no afecta la validez de los 4 tests nuevos del contrato.

## Next Steps (post-archive)

- Crear PR único desde `develop` con etiqueta/justificación `size:exception`.
- Revisar y mergear el commit de archive junto con los 6 commits previos del change.
- Cerrar la issue #78 después del merge.
