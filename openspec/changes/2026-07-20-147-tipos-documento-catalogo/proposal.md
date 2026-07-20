# Propuesta: Catálogo de tipos de documento y FK en Persona

## Resumen ejecutivo
SGV reemplazará texto libre por catálogo inmutable, FK opcional, validación server-side, migración MySQL con unicidad activa.

## Motivación y problema de negocio
Texto libre y validación por longitud permiten inconsistencias y duplicados.

## Alcance de la solución
- Catálogo `TipoDocumento`: `DNI`, `LE`, `LC`, `Pasaporte`.
- `Persona.TipoDocumentoId`, DTO enriquecido, endpoint y select Web.
- Validación, migración y auditoría.

## No-objetivos
Sin CRUD, JavaScript, nuevos tipos ni cambios de auth/otras unicidades.

## Capacidades
**Nueva:** `tipo-documento-catalog`.
**Modificadas:** `persona-management`, `sgv-database`, `sgv-readonly-api`, `sgv-persistence-architecture`.

## Enfoque
Replicar `TipoUnidadOrganizativa`: dominio EF-agnóstico, `Entity`, constantes/`HasData`, servicio, DTO y controller.

## Criterios de aceptación
- Seeds constantes; GET autenticado devuelve `TipoDocumentoDto`.
- Create/Update rechazan FK inexistente, patrón/longitud inválidos y duplicado.
- Create/Edit reponen el select sin JavaScript de validación.
- Migración mapea conocidos/desconocidos y deja FK/índice válidos.
- `Auditorias` registra anterior/nuevo `TipoDocumentoId`.

## Decisiones de producto confirmadas
Endpoint dedicado; FluentValidation server-side; auditoría mediante el interceptor existente.

## Decisiones técnicas a definir en design
> Todas las ambigüedades detectadas en la ronda inicial fueron **resueltas en pre-propuesta**. Quedan aquí como notas vinculantes; design solo aterriza los detalles mecánicos.

- **Nulabilidad conjunta, patrones, casing y timeout regex** — Resuelto en pre-propuesta: `NumeroDocumento` se valida siempre; `TipoDocumentoId` se modela como `Guid?` opcional en input y se resuelve server-side desde el código del catálogo. Patrón exacto y timeout se confirman en design.
- **Inyección del catálogo, proyecciones y cliente Web** — Resuelto en pre-propuesta: mismo patrón de inyección que `TipoUnidadOrganizativa`; cliente tipado en `SGV.Web/Integration`. Detalle de proyección en design.
- **Tipo/colación de `ActiveDocumentoUnique`; rollback forward-only** — Resuelto en pre-propuesta: índice único sobre columna calculada `CONCAT(TipoDocumentoId, ':', NumeroDocumento)` en `utf8mb4_0900_ai_ci`; rollback = migración correctiva forward. Validación final en design.
- **Delta `unknown→NULL` frente al fail-loud `REQ-SPA-EVOLUTION-001`** — Resuelto en pre-propuesta: política unknown→NULL aprobada; ver "Excepciones a decisiones técnicas vigentes".
- **Rango `700…`, ya usado por `NivelCargo`** — Resuelto en pre-propuesta: reasignar a bloque `71000000-0000-0000-0000-000000000000`; ver "Mapa de rangos GUID del proyecto".

## Mapa de rangos GUID del proyecto
Convención vigente: cada catálogo inmutable ocupa un bloque contiguo reservado en el primer grupo del GUID.

| Catálogo | Bloque reservado | Estado |
|---|---|---|
| `NivelCargo` | `70000000-0000-0000-0000-000000000000` (`…001` Directivo, `…002` Conducción Media, `…003` Operativo, `…004` Académico) | Ocupado |
| `TipoDocumento` (este change) | `71000000-0000-0000-0000-000000000000` (`…001` DNI, `…002` LE, `…003` LC, `…004` Pasaporte) | Asignado |
| Próximos catálogos | Reservar bloques contiguos (`72000000-…`, `73000000-…`, …) y documentar aquí | Pendiente |

Los GUIDs viven en `src/SGV.Infraestructura/Persistencia/Catalogos/TipoDocumentoConstantes.cs` (nuevo) y son referenciados desde la migración EF y `DatosSemilla.HasData`. Toda propuesta futura que sume un catálogo debe leer esta tabla y reservar su bloque antes de tocar código.

## Excepciones a decisiones técnicas vigentes

### Delta a `REQ-SPA-EVOLUTION-001` — política unknown legacy → NULL
La condición #3 de `REQ-SPA-EVOLUTION-001` exige fail-loud ante strings legacy sin código conocido (`SIGNAL SQLSTATE '45000'`). Para este change el usuario aprobó conscientemente relajar esa condición:

- **Qué se relaja:** la migración NO abortará si un valor legacy de `TipoDocumento` (string) no matchea ningún `Codigo` del seed. La fila queda con `TipoDocumentoId = NULL` y `NumeroDocumento` preservado tal cual.
- **Por qué:** voluntad explícita del usuario plasmada en la issue #147; menor fricción operativa (no obliga a remediación manual previa al deploy); el costo de remediación se aborda post-deploy.
- **Mitigación de trazabilidad:** la fila nunca se borra. El `NumeroDocumento` se conserva huérfano, habilitando remediación manual futura. El interceptor de auditoría registra la transición `string → Guid?` en `Auditorias` con usuario y timestamp.
- **Sigue vigente:** el resto de la condición #3 (pre-flight, deterministicidad, no `DROP COLUMN` hasta backfill OK) y las condiciones #1, #2, #4 de `REQ-SPA-EVOLUTION-001`. La FK sigue siendo `OnDelete(Restrict)` y el seed vive en `SGV.Infraestructura.Persistencia.Catalogos.TipoDocumentoConstantes`.

Este delta se formalizará como escenario explícito en `sgv-persistence-architecture/spec.md` durante la fase de spec.

## Ronda de preguntas de propuesta
- ~~Confirmar antes de spec si `NumeroDocumento` se conserva cuando el tipo legacy queda `NULL`.~~ → Resuelto en pre-propuesta: sí, se conserva (ver "Excepciones a decisiones técnicas vigentes").
- No quedan preguntas abiertas para el usuario antes de spec.

## Riesgos y mitigaciones
| Riesgo | Nivel | Mitigación |
|---|---|---|
| Conflicto de rango GUID con `NivelCargo` | Bajo | Reasignado a bloque `71000000-…`; documentado en "Mapa de rangos GUID del proyecto" |
| Pérdida de tipo legacy ante falla de backfill | Bajo | `NumeroDocumento` huérfano + `Auditorias` permite remediación post-deploy |
| Índice Guid compuesto (`ActiveDocumentoUnique`) | Alto | drop/recreate con `CONCAT(TipoDocumentoId, ':', NumeroDocumento)`; canario MySQL |
| Contracts breaking (DTOs API/Web) | Alto | `TipoDocumentoDto` enriquecido con `Codigo`/`Nombre`; tests API + Web |
| Auditoría incompleta | Medio | test del interceptor cubriendo transición `string → Guid null` |

## Impacto por capa
| Capa | Cambio |
|---|---|
| Dominio | entidad; FK; `Reconstitute` |
| Persistencia | tabla; seed (`TipoDocumentoConstantes`); FK; mappers; índice |
| Aplicacion | consulta; validators; unicidad |
| Contracts | DTOs/requests |
| Api | GET autenticado |
| Web | cliente; select; Create/Edit |
| Tests | dominio; aplicación; API; Web; MySQL |

## Migración de base de datos
1. Crear `TiposDocumento`, código único y seeds desde `TipoDocumentoConstantes` (bloque `71000000-…`).
2. Quitar índice/columna `ActiveDocumentoUnique`; agregar `TipoDocumentoId char(36) NULL` e índice.
3. Backfill por código (string → Guid). **Los valores que no matcheen ningún `Codigo` del seed quedan como `TipoDocumentoId = NULL`, con `NumeroDocumento` preservado.** Esta política DELTA la condición #3 (fail-loud) de `REQ-SPA-EVOLUTION-001`; ver "Excepciones a decisiones técnicas vigentes".
4. Agregar FK `OnDelete(Restrict)`. Quitar string legacy solo cuando el backfill haya finalizado (conocidos mapeados + NULLs aceptados por política).
5. Recrear columna con `CONCAT(TipoDocumentoId, ':', NumeroDocumento)` para no-eliminados e índice único.

## Plan de validación / verificación
Strict TDD; unitarios/API/Web; `[MySqlFact]` para seed, backfill (conocidos + NULL huérfano), FK, DDL, unicidad y auditoría.

## Rollback
Backup obligatorio; tras el `DROP`, migración correctiva forward o restauración para recuperar tipos desconocidos. Las filas con `TipoDocumentoId NULL` son trazables vía `Auditorias` + `NumeroDocumento` huérfano, así que la remediación posterior es viable.

## Dependencias
MySQL 8, Pomelo/EF Core 9 y auth vigente; sin paquetes nuevos.

## Notas de compliance / decisión
Respetar `docs/decisiones-implementacion.md` (unicidad, auditoría, migraciones) y registrar la excepción a `REQ-SPA-EVOLUTION-001` en `sgv-persistence-architecture` durante spec. Mantener el "Mapa de rangos GUID del proyecto" en este proposal como referencia operativa para próximos catálogos.
