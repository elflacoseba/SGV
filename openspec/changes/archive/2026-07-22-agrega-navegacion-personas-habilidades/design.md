# Diseño técnico: agrega-navegacion-personas-habilidades

## Resumen ejecutivo

El cambio agrega navegación Persona→Habilidades sobre la página existente y Habilidad→Personas mediante un subrecurso readonly nuevo. Replica `SkillCargo` y `PersonaSkill`, preserva Clean Architecture, autenticación cookie→JWT y segmentación por estado de Persona. No modifica dominio ni esquema. La entrega se divide en tres PRs stacked-to-main.

## Contexto y motivación

La issue #187 cierra el único vínculo Persona↔Habilidad sin acceso cruzado desde ambos listados. Las specs `REQ-SPQC-01..07`, `REQ-HLD-NEW*`, `REQ-HM-NEW*` y `REQ-PM-NEW*` fijan paginación, permisos, wire shape y navegación sin renumerar requisitos.

## Arquitectura del cambio

```text
Razor Page → IHabilidadApiClient → GET /api/v1/skills/{id}/personas
                                      ↓
SkillsController → SkillPersonaServicioConsulta → SkillPersonaRepository
                                                        ↓
                                 PersonaHabilidad + Persona + NivelHabilidad
```

| Decisión | Alternativa | Rationale |
|---|---|---|
| DTOs bajo `Habilidades/Consultas/Dtos` | `Organizacion` | El owner del subrecurso es Skill; replica `SkillCargoDetailDto`. |
| Repositorio proyecta readonly | Reusar servicio de comandos | Evita mezclar lectura paginada con writes. |
| Ordenar entidad antes de `Select` | Ordenar DTO | Pomelo no traduce confiablemente records posicionales anidados. |

## Componentes nuevos y modificados

- **Contracts**: crear `SkillPersonaDetailDto.cs`, `HabilidadPersonasListQuery.cs` y `PersonaHabilidadesPageResult.cs` en `src/SGV.Contracts/Habilidades/Consultas/Dtos/`.
- **Aplicación**: crear `ISkillPersonaServicioConsulta`, `SkillPersonaServicioConsulta` e `ISkillPersonaRepository` en `src/SGV.Aplicacion/Habilidades/Consultas/`; validar `Guid.Empty` y construir metadatos.
- **Infraestructura**: crear `SkillPersonaRepository` con `AsNoTracking`, filtro por `Persona.IsDeleted/IsActive`, búsqueda, orden, `Skip/Take` y proyección única sin N+1.
- **API**: extender constructor de `SkillsController` y agregar `GetPersonas` inmediatamente después de `GetCargos`; validar padre, normalizar parámetros y devolver 404 o página válida.
- **Web Integration**: extender `IHabilidadApiClient.GetPersonasAsync`, `HabilidadApiClient` y el fake usado por tests con seed determinista.
- **Web Pages**: crear `Habilidades/Personas.cshtml(.cs)` readonly, con habilidad padre, grilla, vacío, búsqueda, sort, toggle y paginación; cada fila enlaza a `Personas/Details`.
- **UI gating**: agregar `BuildPersonasRouteValues` en `Habilidades/Index.cshtml.cs` y `BuildHabilidadesRouteValues` en `Personas/Index.cshtml.cs`; modificar ambos `.cshtml`.

## Wire contracts

JSON camelCase:

```text
SkillPersonaDetailDto: { persona, nivel, personaId, habilidadId, nivelHabilidadId }
HabilidadPersonasListQuery: { page, pageSize, search, sort, segmento }
PersonaHabilidadesPageResult: { items, page, pageSize, total, sort, segmento }
```

`persona` conserva `PersonaDto`; `nivel`, `NivelHabilidadDto`. HTTP usa `status`, mapeado a `PersonaSegmentoListado`; default `activas`. Sort permitido: `legajo|apellidos|nombres` `_asc|_desc`; default `apellidos_asc`.

## Modelo de datos

No se requieren migraciones: se leen tablas y relaciones existentes. Aunque `PersonaHabilidad` sí modela `VerificadoAt` y `Fuente`, D5 y el non-goal excluyen exponerlos; tampoco cambia su unicidad ni persistencia.

## Auth y autorización

API hereda `[Authorize]` de `SkillsController`; el GET no exige rol. `Habilidades/Personas` usa `[Authorize]`. El botón Personas aparece para cualquier autenticado solo si `!Model.IsDeletedView`; Habilidades en `Personas/Index` solo si `Model.EsAdministrador && !Model.IsDeletedView`.

## DI / wiring

La evidencia del repo corrige el punto de wiring: `SGV.Api/Program.cs` ya llama `AddInfraestructuraServicios()` y solo cambia el constructor del controller. Registrar ambos nuevos tipos como `Scoped` en `src/SGV.Infraestructura/DependencyInjection.cs`. `SGV.Web/Program.cs` ya registra `IHabilidadApiClient` como typed client y `AddRazorPages`; no se registra PageModel explícitamente.

## Testing strategy

Strict TDD: cada slice escribe RED antes de GREEN.

| Capa | Cobertura |
|---|---|
| Contratos | Compatibilidad JSON exacta. |
| Repository | Filtro, búsqueda, orden previo, paginación con EF InMemory. |
| Servicio | Guid vacío, metadata y delegación. |
| API | 200 vacío/con datos, 404, 401, límites, search/sort/status con `WebApplicationFactory`. |
| PageModel | padre, resultados, toggle, `IsRecoverable`, Guid vacío. |
| Cliente | URI y deserialización; 404/500/transporte con handler fake. |
| Web | gating/hrefs, smoke `SgvWebApplicationFactory`, `dotnet test SGV.slnx`, `bun run build`. |

## Riesgos técnicos y mitigaciones

Pomelo exige ordenar antes de proyectar; `status` debe filtrar Persona, no Habilidad; 404 padre no equivale a 200 vacío; helpers preservan `p/search/sort/status`; tests verifican que eliminadas no exponga CTAs.

## Threat matrix

La ruta HTTP nueva queda cubierta por auth, constraint `{skillId:guid}`, whitelist y tests 401/404/normalización. Las cinco fronteras de la matriz (`documentation-like paths`, selección Git, commit, push y comandos PR) son **N/A**: no se ejecutan archivos, shell ni automatizaciones VCS.

## Plan de entrega

- **PR A**, base `main`: botón/helper Personas→Habilidades y tests.
- **PR B**, base `main`: contracts, aplicación, repositorio, API, firma cliente y tests backend.
- **PR C**, base `main` después de mergear B: cliente/fake, Razor Page, CTA y tests web.

PR A y B son independientes; C depende de B. Cada PR se valida y revierte por archivos, sin rollback de datos.

## Referencias

Issue #187; `proposal.md`; `exploration.md`; specs del change; designs archivados `2026-07-05-habilidades-navegacion-cargos`, `2026-07-06-cargos-navegacion-habilidades`; `implementa-persona-habilidades/design.md`; `docs/decisiones-implementacion.md:229-256`; `PersonaSkillRepository`, `SkillCargoRepository`, `SkillsController.GetCargos` y `Habilidades/Cargos.cshtml.cs`.
