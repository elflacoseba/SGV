# Propuesta: Migrar campo Categoría de Habilidades a Tabla

## Resumen ejecutivo

`Habilidad.Categoria` es texto libre, admite errores y carece de integridad referencial. Se propone `CategoriasHabilidad`, FK opcional y contratos/UI con IDs estables. El cambio breaking alcanza toda la stack, datos, pruebas y documentación.

## Motivación

El string no gobierna valores ni evita typos. La FK normaliza el dominio y habilita agrupaciones/reportes futuros; el dashboard se difiere.

## Decisión propuesta

1. Catálogo abierto, inmutable, seed-only, sin CRUD ni `IsDeleted`, conforme a `REQ-SPA-EVOLUTION-001`.
2. `Habilidades.CategoriaId` nullable; backfill por nombre exacto; desconocidos a `NULL`.
3. Reservar 16 GUIDs `72000000-…`; sembrar Conducción, Técnica, Dominio y Académica.
4. Wire breaking: `Categoria` → `CategoriaId` + `CategoriaNombre`; endpoint read-only y selector web.

## Cambios concretos

| Área | Cambio |
|---|---|
| Dominio | Nueva `CategoriaHabilidad`; navegación/FK opcional en `Habilidad`; eliminar string. |
| Persistencia | Tabla, entity/configuración, mapper, repositorio, seed y migración: backfill, FK `Restrict`, drop columna/índice legacy, add `IX_Habilidades_CategoriaId`. |
| Aplicación | `ICategoriaHabilidadRepository`, `ICategoriaHabilidadService`; `IHabilidadRepository` y servicios reciben `Guid? categoriaId`. |
| API | `GET /api/v1/categorias-habilidad`; evaluar GET por id; actualizar `SkillsController`. |
| Contracts | Reorganizar `Habilidades/Categorias/`; agregar `CategoriaHabilidadDto`/`CategoriaHabilidadListItem`; actualizar `CrearHabilidadRequest`, `ActualizarHabilidadRequest`, `HabilidadDto`; registrar breaking change. |
| Web | Nuevo `CategoriaHabilidadApiClient`; dropdown opcional con “Limpiar”; actualizar `_Form`, Details, Index y formularios Cargos/Personas. |
| Docs | Registrar `72000000-… = CategoriaHabilidad` en `docs/decisiones-implementacion.md`. |

## Capabilities

### Nuevas
- `categoria-habilidad-catalog`: catálogo seed-only y endpoint.

### Modificadas
- `habilidad-management`, `habilidad-web-crear-editar`, `sgv-database`, `sgv-persistence-architecture`, `web-apiclient-transport-contract`.

## Alternativas consideradas

- A, string: conserva inconsistencias.
- D, enum: exige recompilar un catálogo abierto.
- C, CRUD: agrega mutabilidad, permisos y riesgo referencial innecesarios.

## No-objetivos

Dashboard, CRUD admin, i18n, auditoría del catálogo, full-text y exportación.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Wire breaking | CHANGELOG, PR explícita y despliegue coordinado. |
| Backfill `NULL` | Reportar remanentes; sin rename automático. |
| Rendimiento | Índice FK y `EXPLAIN` de búsquedas/orden legacy. |
| Colisión GUID | Verificar bloque antes de migrar. |

## Plan de pruebas

Strict TDD: invariantes, requeridos/max lengths e inmutabilidad; listado/mapeo; migración idempotente, backfill/drop; endpoint y create/update con id válido, omitido e inexistente (`400/404` según spec); cliente, dropdown y PRG.

## Rollback

Backup; revertir aplicación y aplicar migración forward que reconstruya el string desde el catálogo. No confiar en `Down` tras el drop.

## Criterios de aceptación

- Migración limpia; 7 habilidades resueltas o `NULL`.
- Sin `Habilidades.Categoria`; FK e índice presentes.
- `dotnet test SGV.slnx`, `bun run build` y docs, correctos.

## Dependencias y referencias

Bloque libre, sin concurrencia. Aplican `habilidad-management`, `habilidad-web-crear-editar`, `sgv-database`, `sgv-persistence-architecture`, `commandresult-error-taxonomy`, `web-apiclient-transport-contract`.

## Pregunta abierta

¿Incluir `Descripcion`? Recomendación: no; sólo `Codigo` y `Nombre`.
