# Archive Report: fix-vacante-toctou-concurrencia-issue-238

## Resumen

El fix de la ventana TOCTOU en `CrearAsync` fue implementado, verificado y archivado exitosamente. La delta spec fue sincronizada al spec principal `openspec/specs/vacante-management/spec.md`: el requirement "Crear Vacante" incorporó enforcement via unique constraint parcial en BD + escenario de carrera concurrente, y se agregó el requirement nuevo "Unicidad de vacante abierta por puesto (defense-in-depth en BD)" con sus 4 escenarios. El change folder fue movido al archive.

---

## Issue

**#238** — Ventana TOCTOU en la regla de negocio "una sola vacante abierta por puesto"

---

## Cambios aplicados

| Archivo | Tipo | Spec/Diseño-ref |
|---------|------|-----------------|
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modificado | Catch `CrearAsync:177` → `PuestoConVacanteAbierta` (design D-6) |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/VacanteConfiguracion.cs` | Modificado | Shadow property `ActivePuestoIdUnique` + unique index (design D-1 a D-4) |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260731173842_AddActivePuestoIdUniqueToVacantes.cs` | Creado | Forward-only migration (design D-5) |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Regenerado | Snapshot con la nueva columna e índice |
| `docs/migracion-inicial-sgv.sql` | Regenerado | Script idempotente con la migración nueva |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Modificado | +1 test unit del catch |
| `tests/SGV.Tests/Persistencia/VacanteConfiguracionTests.cs` | Creado | +3 tests de modelo (shadow, fórmula, índice) |
| `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` | Modificado | Ajuste de fixtures que violaban la nueva constraint |
| `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` | Creado | +3 `[MySqlFact]` (carrera, cerrar-reabrir, soft-delete) |
| `openspec/specs/vacante-management/spec.md` | Sincronizado | Delta mergeada (ver §Delta spec) |

---

## Delta spec sincronizada

El archivo `openspec/specs/vacante-management/spec.md` fue actualizado:

- **MODIFIED — "Crear Vacante"**: se agregó el párrafo enforcement via unique constraint parcial en BD (`FechaCierre IS NULL AND IsDeleted = 0`) y se añadieron los escenarios "Puesto con vacante abierta" y "Carrera concurrente para el mismo PuestoId".
- **ADDED — "Unicidad de vacante abierta por puesto (defense-in-depth en BD)"**: requirement nuevo con 4 escenarios que cubren la constraint desde la perspectiva de la BD (vacante abierta no viola, cerrada libera, soft-deleted libera, reabrir rechazada).

---

## Veredicto

**APROBADO_CON_OBSERVACIONES**

- Build: OK (0 errores, 4 warnings pre-existentes no relacionados al change).
- Suite completa: 3334/3334 passed en DB limpia.
- Suites focales: `Aplicacion.Vacantes` 18/18, `Persistencia` 383/383, `Api.Vacantes` 24/24, `VacantesConcurrenciaTests` 3/3.
- Decisiones D-1 a D-7 del design: 7/7 verificadas en código.
- 11/11 escenarios del spec: compliant.
- CRITICAL issues: ninguno.

**WARNING** (documentado en verify-report, no bloqueante):
- W-1: Tests flaky pre-existentes por estado compartido del MySQL test DB (no son regresiones de este change). Ver lista en verify-report §Findings.

---

## Notas para el PR

### Work-unit commits

```
e1b4625f feat(vacantes): mapear catch de CrearAsync a PuestoConVacanteAbierta y sombra ActivePuestoIdUnique
46a642bb feat(vacantes): agregar migración AddActivePuestoIdUniqueToVacantes y ajustar fixtures pre-existentes
151beaec test(vacantes): agregar [MySqlFact] de carrera y liberación por cierre/soft-delete
2f404b2b docs(openspec): registrar apply-progress del change fix-vacante-toctou-concurrencia-issue-238
```

### Decisión arquitectónica clave

**Estrategia 1 — Unique constraint parcial via columna generada (patrón vigente):** se agregó una shadow property `ActivePuestoIdUnique` en `VacanteConfiguracion.cs` con fórmula `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END` (stored, `ascii_general_ci`). El patrón replica exactamente `OcupacionConfiguracion.cs:42-47` (módulo precedent). MySQL ignora `NULL` en unique indexes, por lo que vacantes cerradas o soft-deleted no violan la constraint. La BD es fuente de verdad ante la carrera TOCTOU; el pre-check `ExistsAbiertaByPuestoAsync` se conserva como rechazo temprano sin round-trip a `SaveChanges`.

### Advertencia: tests flaky pre-existentes (NO son regresiones)

La suite completa `dotnet test SGV.slnx` puede mostrar fallos intermitentes en los siguientes tests ajenos a este change (estado compartido del MySQL test DB):

- `SGV.Tests.Seguridad.JwtCorteInmediatoMySqlFactTests.BloquearUsuario_InvalidaJwtInmediatamente`
- `SGV.Tests.Setup.SetupHappyPathMySqlFactTests.Crear_DatosValidos_CreaPersonaUsuarioRolYAuditoria`
- `SGV.Tests.Persistencia.PersonaRepositoryTests.ActualizarPersona_LimpiarLegajo_PersisteNullYRegistraUpdateLegajoEnAuditorias`
- `SGV.Tests.Persistencia.PersonaRepositoryTests.GetByIdForUpdateAsync_RetornaPersonaActiva`
- `SGV.Tests.Persistencia.UsuarioIdentityGatewayTests.QueryAsync_*`
- `SGV.Tests.OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas_*`

Antes de ejecutar la suite completa en un entorno con MySQL contaminado, limpiar la DB: `DELETE FROM Auditorias; DELETE FROM AspNetUsers; DELETE FROM Personas; ...`. Para verificar el scope propre de este change, usar: `dotnet test --filter "FullyQualifiedName~Vacante"`.

### Nota para deploy en producción

Antes de aplicar la migración en producción, ejecutar la query de detección de duplicados:
```sql
SELECT PuestoId, COUNT(*) FROM Vacantes WHERE FechaCierre IS NULL AND IsDeleted = 0 GROUP BY PuestoId HAVING COUNT(*) > 1
```
Si devuelve filas, resolver manualmente (cerrar todas menos una por puesto) antes de aplicar la migración — la constraint fallaría al crear el índice único sobre duplicados.

---

## Cierre

El change está listo para hacer push del branch y abrir PR contra `develop`. El issue #238 puede cerrarse tras mergear el PR.
