# Exploration: agrega-navegacion-personas-habilidades

> Issue #187 — "Agregar botón para ver las Habilidades de una Persona"
> Fecha: 2026-07-22
> Artifact store: hybrid (openspec + engram)

---

## 1. Resumen ejecutivo

Este cambio agrega dos navegaciones faltantes en la UI web de SGV:

1. **Personas → Habilidades**: botón en cada fila del listado `Personas/Index` que navega a `Personas/PersonaHabilidades` (página existente, actualmente solo accesible desde `Details.cshtml` con gating admin).
2. **Habilidades → Personas**: botón en cada fila del listado `Habilidades/Index` que navega a una página **nueva** (`Personas.cshtml`) que muestra qué personas tienen esa habilidad. Esto requiere backend completo (API, repositorio, contratos, cliente web) + frontend (Razor Page, PageModel).

El estimado total ronda **850-1150 líneas netas**, superando el review budget de 800. Se recomienda split en **3 chained PRs**.

El mapa previo del orquestador se verificó y es correcto salvo por dos desviaciones menores (detalle abajo).

---

## 2. Correcciones al mapa previo

| Afirmación del mapa previo | Verificación | Corrección |
|---|---|---|
| `Details.cshtml` línea 82: botón admin-only | ✅ Confirmado línea 82: `@if (Model.Persona!.IsActive && User.IsInRole(RolesSgv.Administrador))` | Exacto. |
| `Cargos/Index.cshtml` línea 167: `btn-primary ti-stars` | ✅ Confirmado línea 167-169: mismo ícono que `Details` (personas) | Exacto. El patrón usa `btn-icon btn-sm rounded-circle` no textual. |
| `Habilidades/Index` botón `Cargos` es `ti-briefcase` | ⚠️ Casi correcto: es `btn-primary` con `ti-briefcase` (no `ti-stars`) | El botón de Habilidades/Cargos usa `ti-briefcase` (maletín), no `ti-stars`. El botón para Personas debería decidir ícono propio (`ti-users` sugerido). |
| `SkillsController.GetCargos` auth: no `[Authorize(Roles=...)]` heredado del controller | ✅ `[Authorize]` a nivel de clase (línea 19), sin restricción de rol en `GetCargos` | Correcto. Cualquier autenticado puede leer el subrecurso. |
| `PersonaHabilidades` es admin-only | ✅ `[Authorize(Roles = RolesSgv.Administrador)]` línea 23 del PageModel | Incluye writes (asignar/quitar), por eso es admin. |
| Precedente `2026-07-06-cargos-navegacion-habilidades` | ✅ Archivado con artifacts completos | Precedente directo para punto 1: ~123-245 líneas total, single PR. |
| Precedente `2026-07-05-habilidades-navegacion-cargos` | ✅ Archivado con artifacts completos | Precedente directo para punto 2: ~850-1095 líneas, 2 chained PRs. |
| Prs #183-#186 mergeados | ✅ `implementa-persona-habilidades/` folder consolidado, `archive-report.md` presente | Cambio cerrado. Sin conflictos vivos con `develop`. |

---

## 3. Decisiones de producto abiertas

### D1 — Acceso a `Pages/Organizacion/Habilidades/Personas.cshtml`: ¿admin-only o read-only autenticado?

**Contexto**: La página nueva muestra qué personas tienen una habilidad específica. Es una vista de consulta (read-only), no permite writes.

| Opción | Consistencia | Tradeoff |
|---|---|---|
| **A: Admin-only** (`[Authorize(Roles = RolesSgv.Administrador)]`) | Consistente con `PersonaHabilidades` (módulo Personas) | Rompe la simetría con `Habilidades/Cargos` que es `[Authorize]` genérico. Usuario no-admin no puede ver quién tiene cada habilidad desde el catálogo. |
| **B: Read-only autenticado** (`[Authorize]`) | Consistente con `Habilidades/Cargos` (módulo Habilidades) | Inconsistente con `PersonaHabilidades`, pero esa página incluye writes — no es comparable funcionalmente. |

**Recomendación: Opción B (read-only autenticado).** La página es una consulta, no administración. Sigue el precedente directo `2026-07-05-habilidades-navegacion-cargos` donde `Habilidades/Cargos` es `[Authorize]`. Los datos de persona mostrados (Legajo, Nombres, Apellidos) ya son accesibles por cualquier autenticado vía `Personas/Index`. Si más adelante se agregan writes, se sube la guarda.

### D2 — ¿La página `Habilidades/Personas` debe incluir acciones de gestión (alta/baja)?

**Contexto**: `PersonaHabilidades` permite asignar y quitar habilidades. `Habilidades/Cargos` es read-only.

| Opción | Tradeoff |
|---|---|
| **A: Solo lectura** | Consistente con `Habilidades/Cargos`. La issue solo pide "ver". Menor complejidad. |
| **B: Con gestión** | Requeriría expoer writes de persona→skill desde la página de habilidades, rompiendo la separación de concerns. Duplicaría la lógica de `PersonaHabilidades`. |

**Recomendación: Opción A (solo lectura).** La issue #187 dice "ver todas las Personas que tienen esa habilidad". La gestión de asignaciones ya existe en `Personas/PersonaHabilidades`. No duplicar.

### D3 — Botón "Ver habilidades" en `Personas/Index`: ¿admin-only o cualquier autenticado?

**Contexto**: El botón navega a `PersonaHabilidades`, que es admin-only y contiene writes. En `Details.cshtml` el botón ya está gated a admin.

| Opción | Tradeoff |
|---|---|
| **A: Admin-only** | Consistente con `Details`. Evita affordance engañosa (usuario no-admin llegaría a un 403 o a una página sin handlers write). |
| **B: Cualquier autenticado** | Un no-admin podría ver la grilla de habilidades pero no interactuar. El PageModel ya gatea writes con `EsAdministrador`. |

**Recomendación: Opción A (admin-only).** Mismo criterio que `Details.cshtml`: el botón se renderiza solo cuando `!Model.IsDeletedView && Model.EsAdministrador`. El botón se ubicará entre Detalle (público) y Editar (admin), mismo orden que en `Cargos/Index`.

### D4 — Paginación/orden/segmento del nuevo endpoint `GET /api/v1/skills/{id}/personas`

**Contexto**: El endpoint espejo `GET /api/v1/skills/{id}/cargos` acepta `page`, `pageSize`, `search`, `sort`, `status`.

**Recomendación**: Idéntico contrato. Aceptar los mismos query params. `status` se refiere al segmento de la **persona** (`activas`/`eliminadas`), NO de la habilidad. `search` busca sobre legajo, nombres, apellidos de la persona. `sort` sobre campos de persona (`legajo_asc`, `apellidos_asc`, etc.).

### D5 — Forma del wire-type `SkillPersonaDetailDto`

**Contexto**: `SkillCargoDetailDto(CargoDto, NivelHabilidadDto)` con link fields init-only.

**Recomendación**: Espejo exacto:

```csharp
public sealed record SkillPersonaDetailDto(PersonaDto Persona, NivelHabilidadDto Nivel)
{
    public Guid PersonaId { get; init; }
    public Guid NivelHabilidadId { get; init; }
    public Guid HabilidadId { get; init; }
}
```

- `PersonaDto` ya trae: Id, Legajo, Nombres, Apellidos, Email, TipoDocumentoCodigo, NumeroDocumento, Telefono, IsActive.
- `NivelHabilidadDto` ya trae: Id, Codigo, Nombre, ValorNumerico, Orden.
- `PersonaId` denormalizado para acceso directo sin navegar `Persona`.
- No incluir `verificadoAt`/`fuente` porque el dominio `PersonaHabilidad` no los tiene (solo `PersonaId`, `HabilidadId`, `NivelHabilidadId`).

Query record: `HabilidadPersonasListQuery(int Page, int PageSize, string? Search, string? Sort, PersonaSegmentoListado Segmento)`.

---

## 4. Mapeo de tamaño y split sugerido

### Estimación total

| Capa | Archivos | Líneas est. |
|---|---|---|
| **Punto 1 — Botón en Personas/Index** | `Index.cshtml`, `Index.cshtml.cs`, tests | 50-80 |
| **Punto 2 — Contratos nuevos** | `SkillPersonaDetailDto.cs`, `HabilidadPersonasListQuery.cs` | 65-85 |
| **Punto 2 — Servicio aplicación** | `ISkillPersonaServicioConsulta.cs` + impl | 60-80 |
| **Punto 2 — Repositorio** | `ISkillPersonaRepository.cs` + impl | 100-140 |
| **Punto 2 — Endpoint API** | `SkillsController.cs` modificación | 55-75 |
| **Punto 2 — Cliente web** | `IHabilidadApiClient.cs` + `HabilidadApiClient.cs` modificación | 50-70 |
| **Punto 2 — Página Razor** | `Personas.cshtml` + `Personas.cshtml.cs` | 200-280 |
| **Punto 2 — Entry point Habilidades/Index** | `Index.cshtml` + `Index.cshtml.cs` modificación | 20-30 |
| **Punto 2 — Tests controller API** | Nuevo archivo | 170-220 |
| **Punto 2 — Tests PageModel** | Nuevo archivo + IndexTests modificación | 130-170 |
| **Total bruto** | ~12-15 archivos | **890-1190** |
| **Budget disponible** | — | **800** |

### Split sugerido (3 PRs)

```
PR #1 — Punto 1 (pequeño, ~50-80 líneas)
├── src/SGV.Web/Pages/Personas/Index.cshtml.cs (helper BuildHabilidadesRouteValues)
├── src/SGV.Web/Pages/Personas/Index.cshtml (botón ti-stars entre Detalle y Editar)
├── tests/SGV.Tests/Web/Persona/IndexPageTests.cs (2 escenarios: activo+admin lo expone, activo+no-admin lo oculta)
└── Base: main → merge directo

PR #2 — Backend + tests para Punto 2
├── src/SGV.Contracts/Organizacion/Consultas/Dtos/SkillPersonaDetailDto.cs (nuevo)
├── src/SGV.Contracts/Organizacion/Consultas/Dtos/HabilidadPersonasListQuery.cs (nuevo)
├── src/SGV.Aplicacion/Organizacion/Consultas/ISkillPersonaServicioConsulta.cs (nuevo)
├── src/SGV.Aplicacion/Organizacion/Consultas/SkillPersonaServicioConsulta.cs (nuevo)
├── src/SGV.Aplicacion/Organizacion/Consultas/ISkillPersonaRepository.cs (nuevo)
├── src/SGV.Infraestructura/Persistencia/Repositorios/SkillPersonaRepository.cs (nuevo)
├── src/SGV.Api/Controllers/SkillsController.cs (nuevo endpoint GetPersonas + inyección)
├── src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs (GetPersonasAsync firma)
├── src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs (implementación)
├── tests/SGV.Tests/Api/HabilidadesPersonasControllerTests.cs (nuevo, 8 escenarios)
└── Base: main (o develop si PR #1 ya mergeó)

PR #3 — Web UI + tests para Punto 2
├── src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml (nuevo)
├── src/SGV.Web/Pages/Organizacion/Habilidades/Personas.cshtml.cs (nuevo PageModel)
├── src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml (botón Personas)
├── src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs (helper)
├── tests/SGV.Tests/Web/Habilidad/HabilidadesPersonasModelTests.cs (nuevo)
├── tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPageTests.cs (extensión)
└── Base: main (o develop si PR #2 ya mergeó)
```

**Alternativa**: 2 PRs (PR #1 = punto 1 solo, PR #2 = punto 2 completo backend+frontend+tests). PR #2 quedaría ~800-1000 líneas, rozando el budget. Riesgo: la gate `ask-on-risk` se dispara igual.

**Recomendación**: 3 PRs. PR #1 es trivial y despeja rápido; PR #2 y PR #3 tienen boundaries claros (backend vs web), con tests independientes por capa.

---

## 5. Riesgos identificados

### Críticos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **Superar review budget** | Alto — bloquea apply sin decisión del usuario | Split en 3 PRs como se propone. Gate `ask-on-risk` debe resolverse antes de apply. |
| **Issue #59 (OcupacionRepositoryTests caídos)** | Medio — los tests `[MySqlFact]` nuevos arrastrarían falsos rojos | Los tests del controller y PageModel NO usan `[MySqlFact]`; usan `WebApplicationFactory` con seed explícito. Mismo patrón que `habilidades-navegacion-cargos` T10. |
| **Gotcha Pomelo: OrderBy sobre DTO posicional** | Medio — EF Core + Pomelo no traduce `OrderBy` sobre `record posicional` en proyección anidada | Aplicar `OrderBy` sobre `PersonaEntity.Apellidos`/`Legajo` ANTES del `Select` al DTO. Test de sort del controller lo verifica end-to-end. |

### Moderados

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **Confundir segmento persona vs habilidad en endpoint** | Bajo — la query normaliza a `PersonaSegmentoListado`, no al enum de habilidad | Usar su propio query record con `PersonaSegmentoListado`. |
| **Inconsistencia DTO: olvidar link fields** | Medio — el `SkillPersonaDetailDto` sin `PersonaId`/`NivelHabilidadId` rompe la vista | Acceptance criteria: DTO debe exponer `PersonaId`, `NivelHabilidadId`, `HabilidadId` como init-only más allá del constructor. |
| **Tocar `Habilidades/Details.cshtml` por simetría** | Bajo — fuera de alcance explícito | `git diff` lo detecta; acceptance criteria lo prohíbe. |
| **Renderizar botón "Personas" en fila eliminada de Habilidades/Index** | Bajo — affordance engañosa | Gating con `!Model.IsDeletedView` como ya hacen los botones existentes. |
| **No preservar contexto de navegación en el href** | Medio — el usuario pierde paginación/búsqueda al volver | Helper `BuildPersonasRouteValues` centraliza id/p/search/sort/status. Test web verifica el href completo. |

### Bajo

| Riesgo | Impacto |
|---|---|
| PR #1 cambia la columna Acciones de `Personas/Index` (la ensancha) | 3 botones → 4 botones admin. Mantener `btn-icon btn-sm rounded-circle`. |
| El PageModel nuevo requiere manejo de error recuperable si la habilidad no existe | Mismo patrón que `HabilidadesCargosModel`: check `GetByIdAsync` de la habilidad padre → estado `IsRecoverable`. |
| El nuevo DTO `SkillPersonaDetailDto` debe vivir en `SGV.Contracts` | Sí, para que Web y Api lo compartan. Ubicación: `SGV.Contracts.Organizacion.Consultas.Dtos` (espejo de `SkillCargoDetailDto`). |

---

## 6. Próximos pasos

1. **Resolver decisiones D1-D5** con el usuario (ver §3 arriba).
2. **Resolver gate `ask-on-risk`**: el usuario debe elegir estrategia de split (3 PRs recomendado).
3. Ejecutar `sdd-propose` para formalizar el alcance.
4. Ejecutar `sdd-spec` para redactar delta specs (mínimo 2 nuevas: `skill-persona-query-contract`, `persona-skill-web-personas-page`; más modificaciones a `persona-management` y `habilidad-web-listado-detalle-baja`).
5. Ejecutar `sdd-design`, `sdd-tasks`, `sdd-apply` en ciclo por cada PR.

---

## Result Contract

- **status**: success
- **executive_summary**: El cambio `agrega-navegacion-personas-habilidades` resuelve la issue #187 con dos puntos independientes: botón "Ver habilidades" en Personas/Index (navega a página existente, ~50-80 líneas) y botón "Ver personas" en Habilidades/Index con página Razor nueva + backend completo (~800-1110 líneas). El total estimado (850-1190 líneas) supera el budget de 800, requiriendo split en 3 chained PRs. Se identificaron 5 decisiones de producto (D1-D5) que el usuario debe resolver antes de proposar. El mapa previo del orquestador se verificó y es correcto.
- **artifacts**:
  - `openspec/changes/agrega-navegacion-personas-habilidades/exploration.md`
- **next_recommended**: propose (una vez resueltas D1-D5 y gate ask-on-risk)
- **risks**:
  - Supera review budget de 800 líneas → requiere split en 3 chained PRs y decisión del usuario
  - Gotcha Pomelo: OrderBy sobre DTO posicional puede fallar en runtime (mitigado: ordenar antes de Select)
  - Issue #59 latente para tests [MySqlFact] — los tests nuevos usan WebApplicationFactory no MySQL
  - Decisión D1 (auth de nueva página) cambia drásticamente la superficie de permisos
  - PRs chained requieren coordinación de merge base (main vs develop)
- **skill_resolution**: paths-injected — `sdd-explore`, `openspec-explore`, `Razor Pages Patterns`, `dotnet-csharp`, `dotnet-best-practices`
