# Archive Report: Catálogo `TipoDocumento` y FK en `Persona` (issue #147)

| Campo | Valor |
|---|---|
| **Change** | `2026-07-20-147-tipos-documento-catalogo` |
| **Fecha de archive** | 2026-07-20 |
| **Realizado por** | `sdd-archive` sub-agent |
| **Rama final** | `147-tipodocumento/web-ui` |
| **Modo de artifact store** | Hybrid (OpenSpec + Engram) |

## Resumen del cambio

Catálogo inmutable `TipoDocumento` (4 filas: DNI, LE, LC, Pasaporte, bloque GUID `71000000-…`) reemplaza el string legacy `Personas.TipoDocumento` por FK nullable `Personas.TipoDocumentoId` (`Guid?`, `char(36) NULL`, `OnDelete(Restrict)`). La columna generada `ActiveDocumentoUnique` se reconstruye con `CONCAT(TipoDocumentoId, ':', NumeroDocumento)`. Validación server-side con `ITipoDocumentoCatalogoConsulta` en validators (FK_INEXISTENTE, PATRON_NO_CUMPLIDO, LONGITUD_FUERA_DE_RANGO). Web shell con `<select>` en Create/Edit de Personas. **Delta a REQ-SPA-EVOLUTION-001**: variante opt-in relajada de condición #3 (backfill no aborta, valores legacy desconocidos → NULL con `NumeroDocumento` preservado).

## Lo entregado vs lo planeado

| Aspecto | Planeado | Entregado | Estado |
|---|---|---|---|
| Dominio | `TipoDocumento` record, `Persona.TipoDocumentoId` | ✅ Creado con validación, regex con timeout 50ms anti-ReDoS | Completo |
| Persistencia | Entity, config, constants, repository, migration, DatosSemilla | ✅ TiposDocumento table, FK, columna generada, backfill parcial con logging | Completo |
| Migración | DDL ordenado, forward-only, pre-flight no fail-loud | ✅ Migración `20260720230343_TipoDocumentoCatalogoYPersonaFk`, Down() = NotSupportedException | Completo |
| Contracts | `TipoDocumentoDto`, `PersonaDto` actualizado | ✅ 6-field DTO, PersonaDto con denormalización JOIN | Completo |
| API | `TiposDocumentoController` read-only | ✅ GET list + GET byId, auth required, 405 para writes | Completo |
| Validators | FK_INEXISTENTE, PATRON_NO_CUMPLIDO, LONGITUD_FUERA_DE_RANGO | ✅ 3 reglas async con catálogo inyectado | Completo |
| Web UI | `<select>` en Create/Edit, fake client, smoke tests | ✅ Create/Edit con pre-selección, helper back-compat eliminado | Completo |
| Docs | `docs/decisiones-implementacion.md`, `AGENTS.md`, `migracion-inicial-sgv.sql` | ✅ Mapa de bloques GUID, regenerado SQL idempotente (113 KB) | Completo |

### Desviaciones documentadas

1. **D1 (PR1)**: Validators y servicios no se actualizaron en PR1 (eran scope de PR2).
2. **D2 (PR1)**: ActiveDocumentoUnique requirió backfill en 2 pasos (reescritura manual de migración).
3. **D3 (PR1)**: Migración forward-only con Down() = NotSupportedException.
4. **D1 (PR2)**: Validator con catálogo nullable (back-compat); registración explícita llegó en PR3.
5. **D2 (PR2)**: PersonaServicioComandos.MapToDto sin JOIN (deferred a follow-up).
6. **D1 (PR3)**: PersonaInputModel no incluye TiposDocumento (sigue patrón ICargoForm.NivelOptions).
7. **D2 (PR3)**: TipoDocumentoKey legacy eliminado, back-compat helper eliminado.
8. **D3 (PR3)**: Paridad JOIN en PersonaServicioComandos deferida.

## PRs del chain

| PR | Rama | Estado | Cambios netos |
|---|---|---|---|
| [#178](https://github.com/elflacoseba/SGV/pull/178) | `147-tipodocumento/foundation` | Abierto | ~+900 líneas (Foundation: dominio + persistencia + migración + tests) |
| [#179](https://github.com/elflacoseba/SGV/pull/179) | `147-tipodocumento/api-validation` | Abierto | ~+650 líneas (API + validators + JOIN denormalizado + docs) |
| [#180](https://github.com/elflacoseba/SGV/pull/180) | `147-tipodocumento/web-ui` | Abierto | ~+635 líneas (Web UI: client + Create/Edit + smoke tests + DI fix) |

## Métricas finales

| Métrica | Valor |
|---|---|
| **Tests totales** | 2609/2609 PASS (0 failed, 0 skipped en PR3 final) |
| **Tests nuevos** | ~72 (dominio 27 + constantes 10 + validators 15 + controller 14 + servicio 4 + DI 4 + fake 4 + web create 4 + web edit 2 + client 2 + contract 1 + api persona 1) |
| **Archivos creados** | ~18 (dominio 2, persistencia 5, contracts 1, api 1, tests 9) |
| **Archivos modificados** | ~25 (Persona dominio + entity + config + mapper + validators + services + DTOs + web pages + docs) |
| **Net diff total** | ~+2185 líneas (PR1 + PR2 + PR3) |
| **Cobertura [MySqlFact]** | ~9 tests (migración, backfill, FK, unicidad, auditoría) |

## Decisiones clave tomadas

1. **Delta opt-in relajado a REQ-SPA-EVOLUTION-001 condición #3**: backfill NO aborta, valores legacy desconocidos → NULL con `NumeroDocumento` preservado. Aprobado por el usuario en issue #147.
2. **Bloque GUID `71000000-…`**: reasignado de `700…` (ocupado por NivelCargo) a nuevo bloque contiguo.
3. **Migración forward-only**: Down() = `NotSupportedException` (precedente `FixActivePuestoIdUniqueType`).
4. **Auditoría automática**: vía `AuditoriaSaveChangesInterceptor` existente (sin código nuevo de auditoría para este cambio).
5. **Validación en 3 códigos de error diferenciados**: FK_INEXISTENTE, PATRON_NO_CUMPLIDO, LONGITUD_FUERA_DE_RANGO.
6. **JOIN denormalizado en path de lectura solamente**: PersonaServicioComandos.MapToDto emite null para TipoDocumentoCodigo/Nombre (deferred a follow-up).

## Follow-ups identificados

1. **Paridad JOIN en PersonaServicioComandos**: inyectar `ITipoDocumentoCatalogoConsulta` en servicio de comandos para que POST/PUT también devuelvan TipoDocumentoCodigo/Nombre. ~5 líneas + tests.
2. **Registración explícita de validators**: ya implementada en PR3 como defensa en profundidad. Mantener monitoreo.
3. **Próximos catálogos**: reservar bloques contiguos (`72000000-…`, `73000000-…`) siguiendo el mapa documentado en `docs/decisiones-implementacion.md`.

## Stale-checkbox reconciliation

El archivo `tasks.md` contiene checkboxes `- [ ]` para todas las tareas T1-T24, pero la implementación está completa según `apply-progress.md` (3 PRs, 2609/2609 tests, todos los bloques A-G de PR3 verificados). El orquestador confirmó que todos los PRs fueron implementados y testeados. Se realiza reconciliación excepcional mecánica: los checkboxes no se actualizan porque el tasks.md es un artifact de planificación, no un tracker operativo; el `apply-progress.md` es la fuente de verdad del estado de ejecución. Este archive-report documenta la reconciliación para la auditoría.

## Specs sincronizados

| Spec | Acción | Detalles |
|---|---|---|
| `openspec/specs/tipo-documento-catalog/spec.md` | **Creado** | Nuevo spec canónico para el catálogo `TipoDocumento` (REQ-TD-001 a REQ-TD-007) |
| `openspec/specs/sgv-persistence-architecture/spec.md` | **Actualizado** | REQ-SPA-EVOLUTION-001: condición #3 extendida con variante opt-in relajada, tercera invocación registrada, nuevo escenario para variante relajada |
| `openspec/specs/sgv-database/spec.md` | **Actualizado** | 5 requisitos ADDED: catálogo TiposDocumento, navegación FK, ActiveDocumentoUnique reconstruido, backfill histórico, reemplazo de string por FK |
| `openspec/specs/sgv-readonly-api/spec.md` | **Actualizado** | 2 requisitos ADDED: catálogo tipos-documento endpoint GET, contrato TipoDocumentoDto en Swagger |
| `openspec/specs/persona-management/spec.md` | **Actualizado** | 2 requisitos MODIFIED (Alta y Actualización con validación por catálogo) + 7 requisitos ADDED (validación NumeroDocumento, auditoría, cliente tipado, Create/Edit web, feedback) |

## Verification summary

- `dotnet build SGV.slnx`: ✅ 0 errors
- `dotnet test SGV.slnx`: ✅ 2609/2609 PASS
- PRs abiertos: #178, #179, #180

## SDD Cycle Complete

El change `2026-07-20-147-tipos-documento-catalogo` ha sido completamente planificado, implementado, verificado y archivado. Los specs delta se sincronizaron con los specs canónicos. El cambio está listo para commit por el orquestador.
