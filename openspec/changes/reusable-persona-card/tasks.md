# Tareas: reusable-persona-card (issue #219)

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas cambiadas estimadas | 650–900 |
| Riesgo presupuesto 400 líneas | High |
| PRs encadenadas | Yes |
| Split | PR 1 → PR 2 → PR 3 → PR 4 |
| Estrategia de entrega | auto-chain |
| Estrategia de cadena | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades sugeridas

| Slice | Objetivo / límite | Verificación enfocada | Runtime | Rollback |
|---|---|---|---|---|
| 1 / PR 1 | Helper + partial + tests; ≤250 líneas | `dotnet test tests/SGV.Tests --filter "PersonaFormatHelper|PersonaCard"` | Render de partial en ambos modos | Revertir helper, partial, `_ViewImports` y tests |
| 2 / PR 2 | Migrar Usuarios; ≤250 líneas | `dotnet test tests/SGV.Tests --filter "FullyQualifiedName~Web.Usuario"` | GET Details/Edit autenticados | Revertir dos vistas y sus tests |
| 3 / PR 3 | Migrar Ocupaciones; ≤300 líneas | `dotnet test tests/SGV.Tests --filter "FullyQualifiedName~Web.Ocupaciones"` | GET Details exitoso y API Persona caída | Revertir vistas, PageModel, ViewModel y tests |
| 4 / PR 4 | Regresión y validación; ≤180 líneas | `dotnet test SGV.slnx` | Smoke de las cuatro vistas | Revertir sólo guards/correcciones finales |

## Slice 1: Fundación (base: `main`)

- [x] 1.1 RED: crear tests parametrizados para PERFMT-01/02 y rendering readonly/editable/null/data-* de PER-CARD-01..05/08/10.
- [x] 1.2 GREEN: crear `src/SGV.Web/Helpers/PersonaFormatHelper.cs`, `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` y registrar el namespace en `src/SGV.Web/Pages/_ViewImports.cshtml`.
- [x] 1.3 REFACTOR: eliminar duplicación interna, verificar ≤250 líneas y commit `feat(web): add reusable persona card`.

## Slice 2: Usuarios (depende de Slice 1; base: `main` tras PR 1)

- [x] 2.1 RED: ampliar `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` y `EditPageTests.cs` para paridad readonly, enlace, acciones editables y contrato JS.
- [x] 2.2 GREEN: migrar `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` y `_Form.cshtml`; retirar ambos helpers Razor inline sin cambiar PageModels ni JS.
- [x] 2.3 Verificar ≤250 líneas, tests Usuario y commit `refactor(web): reuse persona card in usuarios`.

## Slice 3: Ocupaciones (depende de Slice 1; base: `main` tras PR 2)

- [x] 3.1 RED: extender `OcupacionDetailsPageTests.cs` para DTO enriquecido, enlace, 404/transporte con `PersonaNombre`, y badge Ocupación independiente; extender Create/Edit para card editable completa.
- [x] 3.2 GREEN: agregar `PersonaDto? Persona` en `OcupacionDetailsViewModel.cs`; inyectar `IPersonaApiClient` y fallback clasificado en `Details.cshtml.cs`.
- [x] 3.3 GREEN: migrar `Ocupaciones/Details.cshtml` y `_Form.cshtml` a la partial, preservando modal/hidden y eliminando `FormatearDocumento`.
- [x] 3.4 Verificar ≤300 líneas, tests Ocupaciones y commit `refactor(web): reuse persona card in ocupaciones`.

## Slice 4: Integración y cierre (depende de 1–3; base: `main` tras PR 3)

- [ ] 4.1 Agregar guard de fuentes para cero definiciones Razor `FormatDocumento|FormatearDocumento`, contrato `data-*` permitido y exclusión de `Pages/Personas/Details.cshtml`.
- [ ] 4.2 Ejecutar `dotnet build SGV.slnx`, `dotnet test SGV.slnx` y smoke manual de las cuatro vistas; registrar resultados y diff por slice ≤ límite.
- [ ] 4.3 Corregir sólo regresiones del componente, confirmar rollback independiente y commit `test(web): verify reusable persona card integration`.

## Criterio de done

Cuatro PRs aterrizan en orden sobre `main`; cada una respeta su límite, compila, pasa su suite enfocada, conserva su rollback, y la última prueba todos los escenarios finales sin modificar `Personas/Details.cshtml`, JS, API, contratos ni persistencia.
