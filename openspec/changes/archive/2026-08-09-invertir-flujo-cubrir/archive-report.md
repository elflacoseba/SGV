# Archive Report: invertir-flujo-cubrir

## Status
- **Archived**: 2026-08-09
- **Source branch**: develop@77779f0b
- **PRs merged**: S1 (#269, commit 0e98817b), S2 (#270, commit 4ab286b5), S3 (#271, commit 5ed8239d), W-fix (#272, commit 77779f0b)
- **Verdict**: PASS WITH WARNINGS (sdd-verify)
- **Critical blockers**: 0

## Specs sincronizadas

### vacante-management
- **Cambios**: 1 MODIFIED (requisito "Cambiar estado de Vacante con historial" — inversión del flujo N2: PATCH a Cubierta se rechaza con `400 Validation` y código `PersonaIdRequeridoParaCubrir`; se elimina el bloque automático de creación de Ocupación) + 2 ADDED (requisitos "Detalle de Vacante expone Ocupación derivada" e "Atomicidad de la operación Cubrir via OcupacionServicioComandos.CrearAsync")
- **Archivo**: `openspec/specs/vacante-management/spec.md`
- **Deltas aplicados**: requisito "Cambiar estado de Vacante con historial" reemplaza Regla N2 de creación automática por inversión (rechazo + mensaje de orientación); se agregan 2 requisitos nuevos al final de la sección de requisitos.

### web-ocupaciones-crear-editar
- **Cambios**: 2 MODIFIED (REQ-OCC-FORM-001 y REQ-OCC-FORM-009 — soporte de query param `?vacanteId={guid}`, validación de estado de Vacante antes de renderizar form, hint informativo, botón bloqueado + label dinámico) + 1 ADDED (REQ-OCC-FORM-010 — creación de Ocupación con VacanteId, atomicidad transaccional, validaciones de estado y coherencia de PuestoId)
- **Archivo**: `openspec/specs/web-ocupaciones-crear-editar/spec.md`
- **Deltas aplicados**: REQ-OCC-FORM-001 se extiende con 5 escenarios DADO-CUANDO-ENTONCES para `?vacanteId`; REQ-OCC-FORM-009 se extiende con 2 escenarios de hint con/sin código visible; REQ-OCC-FORM-010 se agrega como requisito nuevo al final de la sección.

### web-ocupaciones-navegacion-contextual
- **Cambios**: 1 MODIFIED (REQ-OCC-NAV-006 — navegación contextual desde Puesto ahora usa `?vacanteId=` en lugar de `?puestoId=` cuando hay Vacante abierta sin Ocupación activa, label "Cubrir Vacante") + 1 ADDED (REQ-OCC-NAV-008 — label dinámico del botón de alta)
- **Archivo**: `openspec/specs/web-ocupaciones-navegacion-contextual/spec.md`
- **Deltas aplicados**: REQ-OCC-NAV-006 se extiende con escenario DADO-CUANDO-ENTONCES de navegación a `?vacanteId=`; REQ-OCC-NAV-008 se agrega como requisito nuevo al final de la sección.

### vacante-web
- **Cambios**: 2 ADDED (requisitos "Botón 'Cubrir Vacante' en Details de Vacante" y "Bloque 'Persona asignada' en Details de Vacante Cubierta")
- **Archivo**: `openspec/specs/vacante-web/spec.md`
- **Deltas aplicados**: ambos requisitos se appendean al final de la sección de requisitos, con escenarios en DADO-CUANDO-ENTONCES.

## Decision Implementation (D-1, D-3, D-4)

- **D-1** (inversión del flujo): documentado en `docs/decisiones-implementacion.md` entrada "Inversión del flujo Cubrir (2026-08-09)". El archive NO modifica ese archivo.
- **D-3** (hidratación defensiva de `VacanteDetailDto`): implementado en `VacanteServicioConsulta.cs` e incrementado en `openspec/specs/vacante-management/spec.md` como requisito ADDED.
- **D-4** (renombre de código de error): `PersonaIdRequeridoParaCubrir` → `CubrirVacanteRequiereCrearOcupacion` con `[Obsolete]`. Implementado en `VacanteErrorCodigo.cs`. La spec vigente `vacante-management` refleja el nuevo comportamiento directamente en el requisito MODIFIED.

## D-6 (Normalización DADO-CUANDO-ENTONCES)

- **Escenarios del delta**: ya en DADO-CUANDO-ENTONCES (español), sincronizados directamente a las specs vigentes sin reescritura.
- **Escenarios heredados** de specs vigentes que usan GIVEN-WHEN-THEN (inglés): NO se normalizan en este archive. La decisión D-6 se aplica a los escenarios de los deltas; los escenarios heredados (preexistentes en las specs vigentes) están fuera del alcance de este change. La normalización completa de escenarios heredados se documenta como cambio futuro de mayor envergadura (out of scope del archive).

## Verificación de specs sincronizadas

- Las 4 specs vigentes (`vacante-management`, `web-ocupaciones-crear-editar`, `web-ocupaciones-navegacion-contextual`, `vacante-web`) reflejan los nuevos requisitos y escenarios del change `invertir-flujo-cubrir`.
- Los escenarios del delta están en DADO-CUANDO-ENTONCES (español), conforme a D-6.
- Los escenarios heredados GIVEN-WHEN-THEN no fueron reescritos (D-6 out-of-scope).
- La coherencia specs+código fue verificada por `sdd-verify` (3488-3490 tests pass, 0 fail, AC1-AC10 cubiertos, D-1/D-3/D-4/D-5/D-6 implementados y verificados).

## Resultados de tests al cierre

| Command | Result |
|---------|--------|
| `dotnet build SGV.slnx` | 0 errors, 96 warnings (baseline preexistente, ningún warning nuevo) |
| `dotnet test SGV.slnx` | 3490 pass, 0 fail, 0 skip (full suite post W-fix) |
| `bun run build` (SGV.Web) | exit 0 |

## Chain Context

- Change cerrado: `invertir-flujo-cubrir`
- 4 PRs encadenadas: S1, S2, S3, W-fix
- 18 commits en total (commits S1 + S2 + S3 + W-fix)
- ~3119+ líneas modificadas (código + tests + docs) — diff contado desde baseline pre-change

## Riesgos remanentes (documentados en verify-report)

1. **`[MySqlFact]` no corridos con MySQL real**: la atomicidad transaccional de `CrearOcupacionCubriendoVacanteAsync` fue cubierta por `FakeThrowingUnitOfWork` (T1.6). Para validación contra constraints reales (`IX_Ocupaciones_VacanteId`, `ActivePuestoIdUnique`), correr la suite con MySQL 8 activo.
2. **Comparación `EstadoVacanteNombre` case-insensitive**: `EsCubrible` depende de strings del seed. Si el backend cambia los labels, el botón podría aparecer para Cubierta o desaparecer para En Selección. Mitigación futura: pedir flags `EsCubierta`/`EsCancelada` en DTO.
3. **WARNING-1 y WARNING-2 fueron resueltos por W-fix** (tests `Get_Create_WithVacanteIdCancelada_MuestraError_VacanteCancelada` y `Post_Create_WithVacanteId_CreaOcupacionYRedirigeAVacanteDetails`). Los SUGGESTIONs (En Selección sin test dedicado, PuestoId omitido sin test, defensivo PersonaAsignadaNombre) quedaron como follow-ups opcionales.

## Próximos pasos

- El change `invertir-flujo-cubrir` queda cerrado.
- Follow-ups potenciales:
  - W-3: extender `VacanteDetailDto` con flags `EsCubierta`/`EsCancelada` explícitos (resuelve WARNING-3 del verify-report).
  - Normalización D-6 de escenarios heredados GIVEN-WHEN-THEN → DADO-CUANDO-ENTONCES (cambio de mayor envergadura, no prioritario).
  - Tests adicionales para SUGGESTIONs (En Selección, PuestoId omitido, defensivo PersonaAsignadaNombre).

## Archivos del change (audit trail)

```
openspec/changes/archive/2026-08-09-invertir-flujo-cubrir/
├── proposal.md
├── design.md
├── tasks.md
├── apply-progress.md
├── verify-report.md
└── specs/
    ├── vacante-management/spec.md
    ├── web-ocupaciones-crear-editar/spec.md
    ├── web-ocupaciones-navegacion-contextual/spec.md
    └── vacante-web/spec.md
```

## Specs vigentes actualizadas (source of truth)

```
openspec/specs/vacante-management/spec.md          — 1 MODIFIED + 2 ADDED
openspec/specs/web-ocupaciones-crear-editar/spec.md — 2 MODIFIED + 1 ADDED
openspec/specs/web-ocupaciones-navegacion-contextual/spec.md — 1 MODIFIED + 1 ADDED
openspec/specs/vacante-web/spec.md                 — 2 ADDED
```

---

*Archive generado por `sdd-archive` — `invertir-flujo-cubrir` — 2026-08-09*
