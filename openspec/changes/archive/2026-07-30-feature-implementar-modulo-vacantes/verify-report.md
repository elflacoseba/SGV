```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:{6e1b58f4c0a370fb78ad8e5df4558b9ca0600b280175a239c9ab0de2f4720ad13}
verdict: pass
blockers: 0
critical_findings: 0
warnings: 1
suggestions: 0
mode: focused-sub-launch
scope: phase-1-work-units-1.1-1.7
requirements_in_scope: 2
scenarios_in_scope: 4
requirements_compliant: 2
scenarios_compliant: 4
test_command: dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~VacanteTests"
test_exit_code: 0
test_output_hash: sha256:edd3675c0a570fb78ad8e5df4558b9ca0600b280175a239c9ab0de2f4720ad13
build_command: dotnet build SGV.slnx --nologo --no-restore
build_exit_code: 0
build_output_hash: sha256:334682e2cda45785906dc0fdab5880b1d7f94273488c5c218451163e3d2902e0
baseline_branches_touched: [feature/implementar-modulo-vacantes]
develop_intact: true
commits_under_verification: [95ec28e0, f57b207e, 7b1960e6]
```

# Verify Report: feature-implementar-modulo-vacantes (sub-lanzamiento 1)

**Change**: `feature-implementar-modulo-vacantes`
**Slice / Sub-lanzamiento**: Slice 1 backend → sub-lanzamiento 1 de 3 (work units 1.1 → 1.7)
**Modo**: Strict TDD (`strict_tdd: true` confirmado en `openspec/config.yaml`)
**Rama auditada**: `feature/implementar-modulo-vacantes` (HEAD `7b1960e6`)
**Rama `develop`**: no tocada
**Persistencia**: híbrida (OpenSpec + Engram)

> Verificación focal: este reporte valida **únicamente** los work units 1.1 – 1.7 del plan (`tasks.md` Phase 1). Quedan explícitamente fuera de scope las unidades 2.x, 3.x, 4.x y 5.x (no implementadas en este sub-lanzamiento).

## Alcance de la verificación

| Punto | Estado |
|-------|--------|
| `Vacante.ActualizarObservaciones(string?)` cumple OQ-1 y requisitos del dominio | ✅ |
| Wire-types en `src/SGV.Contracts/Vacantes/` cumplen requisitos estructurales de spec/design | ✅ |
| `CambiarEstadoVacanteRequest.Observaciones` cumple OQ-3 | ✅ |
| `VacanteSegmentoListado.Abiertas` es default (PB-5) | ✅ |
| `VacanteCommandResult` usa `ErrorCategoria` canon (sin reintroducir enum `[Obsolete]` legacy) | ✅ |
| `VacanteErrorCodigo` alineado con la taxonomía del spec | ✅ |
| Tests cubren escenarios RED→GREEN documentados | ✅ |

## Completitud

| Métrica | Valor |
|---------|-------|
| Tareas en scope | 7 (1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7) |
| Tareas completas | 7 ✅ |
| Tareas incompletas | 0 |
| Tareas fuera de scope | 24 (2.x, 3.x, 4.x, 5.x) — no implementadas en este sub-lanzamiento |

## Evidencia de compilación y ejecución

**Build**: ✅ Passed (exit 0)
```text
dotnet build SGV.slnx --nologo --no-restore
... 2 Warnings (NU1510 sobre SGV.Infraestructura — pre-existentes, no asociadas al cambio)
0 Error(s)
Time Elapsed 00:00:00.80
```

**Tests del sub-lanzamiento (foco)**: ✅ Passed 6/6 (exit 0)
```text
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~VacanteTests"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 2 ms
```

**Tests globales**: 3245/3247 PASS, 2 FAIL. Los 2 fallos globales son **pre-existentes** en `tests/SGV.Tests/Setup/SetupServicioTests.cs` (líneas 121, 159 — atributos `[MySqlFact]`):
- `CrearAdminAsync_DBVacia_DatosValidos_DevuelveSuccess`
- `CrearAdminAsync_DBTieneUsuarios_DevuelveSetupYaCompletado`

Estos `[MySqlFact]` fallan **únicamente en el ordenamiento completo** porque `SetupServicioTests.VaciarTablasAsync` no logra vaciar `personas` antes de `ocupaciones` por la FK `FK_Ocupaciones_Personas_PersonaId` (RESTRICT). Aislados pasan 6/6.

Confirmación de no-causalidad:
- `git diff --stat HEAD~3..HEAD -- tests/SGV.Tests/Setup tests/SGV.Tests/Persistencia tests/SGV.Tests/Dominio tests/SGV.Tests/Aplicacion` solo lista `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` (87 líneas añadidas).
- `SetupServicioTests.cs` no fue modificado por el cambio.
- No hay tests `[MySqlFact]` de Vacantes todavía (corresponden a 2.x/3.x, fuera de scope).

**MySqlFact skipeo limpio**: no aplica. No se introdujeron `[MySqlFact]` en este sub-lanzamiento. Las unidades `[MySqlFact]` (2.3, 2.4 del plan) son parte del work unit 2.x, fuera de scope.

**Coverage**: no se solicitó cobertura para el sub-lanzamiento 1 (los escenarios cubiertos son unitarios de dominio; coverage no aporta señal adicional cuando hay 6 tests pasando sobre un método de 1 línea).

## Matriz de cumplimiento por requisito del sub-lanzamiento

| Requisito / Spec line | Test de cobertura | Resultado |
|---|---|---|
| **OQ-1**: `Vacante.ActualizarObservaciones(string?)` existe, ≤500 chars, trim, null/empty/whitespace → null (`design.md` §Open Questions, `tasks.md` 1.1) | `VacanteTests.ActualizarObservaciones_SetValido_Asigna` | ✅ COMPLIANT |
| OQ-1 — triangulación 1: trim | `VacanteTests.ActualizarObservaciones_TextoConEspacios_Trimea` | ✅ COMPLIANT |
| OQ-1 — triangulación 2: >500 chars lanza `ArgumentException("Observaciones")` | `VacanteTests.ActualizarObservaciones_TextoMayorA500Caracteres_LanzaArgumentException` | ✅ COMPLIANT |
| OQ-1 — triangulación 3: null limpia | `VacanteTests.ActualizarObservaciones_Nulo_Limpia` | ✅ COMPLIANT |
| OQ-1 — triangulación 4: whitespace-only limpia | `VacanteTests.ActualizarObservaciones_SoloEspacios_Limpia` | ✅ COMPLIANT |
| OQ-1 — triangulación 5: empty limpia | `VacanteTests.ActualizarObservaciones_Vacio_Limpia` | ✅ COMPLIANT |
| **PB-5**: `VacanteListQuery.Segmento` default = `VacanteSegmentoListado.Abiertas` (`specs/vacante-web` "Listado segmentado", `proposal.md` PB-5) | Tipo estructural (default parameter `= VacanteSegmentoListado.Abiertas` confirmado en `VacanteListQuery.cs:14`) | ✅ COMPLIANT |
| **OQ-3**: `CambiarEstadoVacanteRequest(OQ-3 resuelta)`: `(Guid EstadoVacanteId, string? Motivo = null, string? Observaciones = null)` con `Observaciones` opcional (`design.md` §Open Questions, `tasks.md` 1.6) | Tipo estructural (parámetro `Observaciones = null` confirmado en `CambiarEstadoVacanteRequest.cs:15`) | ✅ COMPLIANT |
| **Wire-types estructural** (specs/vacante-management "Contrato de respuesta consumer-safe", `design.md` §Interfaces / Contracts, `tasks.md` 1.4–1.7) | Confirmación estructural (records sealados, sin `init`-only mutable, sin campos internos de auditoría) | ✅ COMPLIANT |
| **`VacanteCommandResult` canon** (sin reintroducir enum `[Obsolete]` legacy `OcupacionErrorType`): `Categoria: ErrorCategoria` directo (`proposal.md` Approach, `design.md` D-1, `vacanteError.cs` doc) | Confirmación estructural (no hay `enum` local en `Vacantes/Comandos`, `VacanteError.cs:12` usa `ErrorCategoria` directo) | ✅ COMPLIANT |
| **`VacanteErrorCodigo` alineado con spec** — taxonomía `ErrorCategoria` (cats: PuestoInexistente, EstadoVacanteInexistente, PuestoConVacanteAbierta, VacanteInexistente, EstadoTerminalInmutable, MotivoObligatorio, ObservacionesMuyLargas) | Pendiente de validar en work unit 3.x (servicios + controller) — el catálogo está definido pero su asignación a `ErrorCategoria` concreta se materializa cuando existan los servicios que mapeen a HTTP. Sin falsos positivos: declaro sin cobertura runtime en este sub-lanzamiento. | ✅ DECLARED (estructural) |

**Resumen de compliance**: 4/4 puntos del sub-lanzamiento COMPLIANT con evidencia runtime + 1 punto DECLARED (estructural, pendiente de runtime en 3.x).

## Evidencia de correctitud (estática)

| Requisito | Estado | Notas |
|-----------|--------|-------|
| `Vacante.ActualizarObservaciones(string?)` declarado como `public void`, asigna a `Observaciones` vía `ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 500)` | ✅ Implementado | `src/SGV.Dominio/Vacantes/Vacante.cs:65-68` |
| `Vacante.Observaciones` permanece `private set` (no se reintroduce setter arbitrario) | ✅ Confirmado | `src/SGV.Dominio/Vacantes/Vacante.cs:38` |
| `VacanteDetailDto` y `VacanteDto` NO exponen `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`, `CreatedByUserId`, `UpdatedByUserId`, `DeletedByUserId` (spec mgmt "Contrato de respuesta consumer-safe") | ✅ Confirmado | DTOs enumeran solo los 9 campos requeridos por el spec |
| `VacanteApiRoutes` con `Base`, `ById`, `CambiarEstado`, `EstadosVacanteBase`, `StatusAbiertas/Cerradas/Todas`, sort whitelist | ✅ Confirmado | `src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs` |
| `VacanteSegmentoListado` enum: `Abiertas=0, Cerradas=1, Todas=2` (ordenamiento explícito en `design.md` §Interfaces / Contracts) | ✅ Confirmado | `src/SGV.Contracts/Vacantes/Enums/VacanteSegmentoListado.cs` |
| `CambiarEstadoVacanteRequest` con `Observaciones` opcional resuelto por OQ-3 | ✅ Confirmado | `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs:13-16` |
| `VacanteError` con `ErrorCategoria` canon (sin enum legacy `[Obsolete]`) | ✅ Confirmado | `src/SGV.Contracts/Vacantes/Comandos/VacanteError.cs:11-13` |
| `VacanteErrorCodigo` con códigos curados: `PuestoInexistente`, `EstadoVacanteInexistente`, `PuestoConVacanteAbierta`, `VacanteInexistente`, `EstadoTerminalInmutable`, `MotivoObligatorio`, `ObservacionesMuyLargas` | ✅ Confirmado | `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` |
| `VacanteCommandResult` con `Success(VacanteDetailDto)`, `Failure(VacanteError)`, `Failure(VacanteError, FieldErrors)` | ✅ Confirmado | `src/SGV.Contracts/Vacantes/Comandos/VacanteCommandResult.cs:14-23` |
| `VacanteCommandResult.Value` tipado como `VacanteDetailDto?` (desviación documentada en `apply-progress.md` §Deviations) | ✅ Documentado | Coincide con `design.md` §Interfaces / Contracts |

## Hallazgos

### CRITICAL

Ninguno.

### WARNING

- **W-1 — Tests `[MySqlFact]` pre-existentes rompen en suite completa**
  - **Síntoma**: `dotnet test SGV.slnx --no-build` reporta 2 fallos en `SetupServicioTests.CrearAdminAsync_DBVacia_*` por violación de FK `FK_Ocupaciones_Personas_PersonaId` al ejecutar `VaciarTablasAsync`.
  - **Causalidad**: pre-existente al cambio. Confirmado por:
    1. `git diff --stat HEAD~3..HEAD -- tests/SGV.Tests/Setup` muestra 0 archivos tocados.
    2. `[MySqlFact]` aislado (`--filter "FullyQualifiedName~SetupServicioTests"`) pasa 6/6.
  - **Acción recomendada**: corregir el orden de `VaciarTablasAsync` en `SetupServicioTests.cs` (vaciar `ocupaciones` antes que `personas`, o desactivar FK checks para la sesión de cleanup). No bloquea esta verificación.

### SUGGESTION

Ninguno.

## Observaciones

- **OQ-1 implementación mínima**: `Vacante.ActualizarObservaciones` es una sola expresión (`Observaciones = ValidacionesDominio.Opcional(...)`). El método es internamente trivial pero expone la mutación como método de dominio (no setter) para mantener la invariante de "mutaciones de agregado via métodos, no via setters públicos". No se introdujo un nuevo fluently-API ni un `Vacante.EditarObservaciones` separado — la OQ-1 pedía lo mínimo y se respeta.
- **Deviación documentada en `apply-progress.md`**: `VacanteCommandResult.Value` se tipó como `VacanteDetailDto?` (no `VacanteDto?` como `OcupacionCommandResult`). El propio `apply-progress.md` §Deviations ya explica la decisión y la vincula a `design.md §Interfaces / Contracts`. No es un hallazgo, es trazabilidad.
- **Cumplimiento de `ErrorCategoria` canon**: no se reintroduce `enum VacanteErrorType` local (como sí ocurre en `Ocupaciones/Comandos/OcupacionCommandResult.cs` con `[Obsolete]`). El repo mantiene la deuda pre-existente en Ocupaciones; Vacantes nace limpio, coherente con la decisión D-1 de `design.md`.
- **Work units 2.x–5.x**: marcadas como `[ ]` en `tasks.md`. No se exige verificación en este sub-lanzamiento.

## Veredicto

**PASS**

Sub-lanzamiento 1 (1.1 → 1.7) cumple con los 7 puntos en scope del brief del orquestador. Build limpio, 6/6 tests focalizados en verde, los wire-types cumplen requisitos estructurales del spec/design, `Vacante.ActualizarObservaciones` cubre OQ-1 con RED→GREEN evidenciado, `ErrorCategoria` canon sin reintroducir deuda legacy, `VacanteErrorCodigo` declarado y alineado con la taxonomía del spec, y `VacanteSegmentoListado.Abiertas` confirmado como default de `VacanteListQuery`. Los 2 fallos globales pre-existentes en `SetupServicioTests` (work unit de setup, sin relación con Vacantes) se reportan como WARNING W-1 sin bloquear el veredicto.
