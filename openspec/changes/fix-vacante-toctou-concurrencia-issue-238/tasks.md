# Tasks: Fix TOCTOU de vacantes con unique constraint parcial (issue #238)

## Resumen

Cierra ventana TOCTOU en `VacanteServicioComandos.CrearAsync` con unique constraint parcial en BD (columna calculada `ActivePuestoIdUnique` + índice único, patrón de `OcupacionConfiguracion.cs:42-47`). Catch `CrearAsync:177` se actualiza a `PuestoConVacanteAbierta`. Tests unit, modelo y `[MySqlFact]`. `strict_tdd: true`.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~200-280 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

**Work unit**: PR 1 (single PR). Focused test: `dotnet test --filter "FullyQualifiedName~Vacante"`. Rollback: revertir `VacanteConfiguracion.cs` + catch + migración.

## Phase 1: RED — Test unit del catch

- [ ] T1.1 Test en `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs`: con `DbUpdateException` por constraint violation, `CrearAsync` retorna `Result.Failure` con `ErrorCategoria.Conflict` y `VacanteErrorCodigo.PuestoConVacanteAbierta` (fake repo + UoW). Debe fallar.

## Phase 2: GREEN — Catch block update

- [ ] T2.1 En `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs:177`, cambiar `VacanteErrorCodigo.DatosInvalidos` por `VacanteErrorCodigo.PuestoConVacanteAbierta`; alinear mensaje con pre-check (línea 152). `CambiarEstadoAsync:286` y `ActualizarObservacionesAsync:358` sin cambios (D-6).
- [ ] T2.2 Verificar que T1.1 pasa en verde.

## Phase 3: RED — Test de modelo

- [ ] T3.1 Crear `tests/SGV.Tests/Persistencia/VacanteConfiguracionTests.cs`: test vía `ModelBuilder` que verifique shadow `ActivePuestoIdUnique` con `HasComputedColumnSql("CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END", stored: true)`, `HasMaxLength(36)`, `UseCollation("ascii_general_ci")`, índice único. Debe fallar.

## Phase 4: GREEN — Configuración EF

- [ ] T4.1 Editar `src/SGV.Infraestructura/Persistencia/Configuraciones/VacanteConfiguracion.cs`: agregar `Property<string?>("ActivePuestoIdUnique")` con fórmula, collation e índice único (D-2/D-3/D-4). Quitar comentario obsoleto "application logic enforces FechaCierre".
- [ ] T4.2 Verificar que T3.1 pasa en verde.

## Phase 5: Migración EF Core

- [ ] T5.1 `dotnet ef migrations add AddActivePuestoIdUniqueToVacantes --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --output-dir Persistencia/Migraciones`.
- [ ] T6.1 Editar `.cs` de migración: `Down` forward-only con `NotSupportedException` ("Migración forward-only. Para revertir, escribir una migración correctiva explícita."). Índice `IX_Vacantes_ActivePuestoIdUnique`.
- [ ] T6.2 Regenerar `docs/migracion-inicial-sgv.sql` con `dotnet ef migrations script --idempotent`.

## Phase 6: RED — Test de concurrencia [MySqlFact]

- [ ] T7.1 Crear `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` con dos `[MySqlFact]`:
  - T7.1.a Carrera: dos `POST /api/v1/vacantes` simultáneos mismo `PuestoId` (`Task.WhenAll`) → 1×201 + 1×409 `PuestoConVacanteAbierta`.
  - T7.1.b Liberar al cerrar: crear, transicionar a estado terminal con `FechaCierre`, luego crear nueva mismo puesto → 201 en ambos.

## Phase 7: Verify

- [ ] T8.1 `dotnet build SGV.slnx` — sin errores ni warnings nuevos.
- [ ] T8.2 `dotnet test SGV.slnx` — suite verde; `[MySqlFact]` se skipea limpio sin MySQL.
- [ ] T8.3 Suites focales verdes: `Aplicacion/Vacantes/`, `Persistencia/`, `Api/Vacantes/`. Cero regresiones.

## Orden de ejecución

T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8. `strict_tdd`: RED primero, GREEN después; REFACTOR implícito en T2.1, T4.1 y T6.1.

## Criterios de éxito

- [ ] T8.1 + T8.2 + T8.3 verdes.
- [ ] `ActivePuestoIdUnique` en `VacanteConfiguracion.cs` con fórmula correcta.
- [ ] Catch `CrearAsync:177` mapea constraint a `PuestoConVacanteAbierta`.
- [ ] `[MySqlFact]` cubre escenario "Carrera concurrente" del spec.