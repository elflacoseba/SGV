# Tasks: Issue #253 — Auditoría drill-down preserva `userName`

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas cambiadas estimadas | 35–70 |
| Presupuesto de revisión | 800 líneas; uso cómodamente inferior |
| Riesgo sobre 400 líneas | Bajo |
| PRs encadenadas recomendadas | No |
| División sugerida | Un único PR y un work-unit commit |
| Estrategia de entrega | `single-pr` |
| Estrategia de cadena | `pending` (no aplica) |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Unidad de trabajo sugerida

| Unidad | Objetivo | PR probable | Comando focalizado | Harness runtime | Límite de rollback |
|---|---|---|---|---|---|
| 1 | Preservar `userName` en Details y su back-link, con regresión | PR único | `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasDetailsTests"` | N/A: `SgvWebApplicationFactory` ya ejercita el GET HTTP y el repo no dispone de E2E | Revertir únicamente `Details.cshtml.cs` y el test agregado |

## Fase 1: RED — regresión enfocada

- [x] 1.1 En `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs`, agregar una `[Theory]` de round-trip con `userName=jperez` y sin filtro, reutilizando `CreateAuditoriaLeaseAsync` y `MakeAuditoriaDetalleDto`; comprobar HTTP 200 y presencia/ausencia correcta de `userName` en el back-link.
- [x] 1.2 Ejecutar el comando focalizado y registrar RED: el caso `jperez` debe fallar porque Details todavía ignora `userName`; el caso sin filtro debe permanecer válido.

## Fase 2: GREEN — corrección mínima de PageModel

- [x] 2.1 En `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs`, renombrar propiedad y comentario `UserId`→`UserName`; cambiar el parámetro/binding de `OnGetAsync` a `[FromQuery(Name = "userName")] string? userName` y normalizarlo en `UserName`.
- [x] 2.2 En el mismo archivo, cambiar `BuildBackUrl()` para emitir `userName = UserName`, sin modificar `Index.cshtml.cs`, API, contratos ni persistencia.
- [x] 2.3 Reejecutar el comando focalizado y registrar GREEN para ambos casos de la teoría; no introducir refactorizaciones ajenas al fix.

## Fase 3: Verificación y entrega

- [x] 3.1 Ejecutar `dotnet build SGV.slnx` y confirmar compilación sin errores.
- [x] 3.2 Ejecutar `dotnet test SGV.slnx` y confirmar la suite completa sin regresiones.
- [x] 3.3 Revisar el diff: solo los dos archivos previstos, menos de 800 líneas cambiadas y test+código juntos en un único work-unit commit/PR.
