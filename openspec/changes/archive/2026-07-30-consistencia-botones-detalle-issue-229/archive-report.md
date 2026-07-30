# Archive Report: consistencia-botones-detalle-issue-229 (issue #229)

> **Change**: `consistencia-botones-detalle-issue-229`
> **Issue**: [#229](https://github.com/elflacoseba/SGV/issues/229)
> **Archived**: 2026-07-30
> **Archived by**: `sdd-archive` (OpenSpec CLI + manual spec sync)
> **Artifact store**: híbrido (OpenSpec filesystem + Engram)

---

## Resumen

Change de normalización markup: unificar la barra de botones "Editar / Volver al
listado" en 2 vistas `Details.cshtml` desviadas (`Ocupaciones` y
`UnidadesOrganizativas`) copiando el patrón canónico de `Cargos/Details.cshtml` y
`Personas/Details.cshtml`. La implementación lived directly on `develop` (4 commits,
sin rama feature) y dejó 4 archivos canónicos intactos. Suite verde 3241/3241,
0 CRITICAL, 0 WARNING. Archives y specs sincronizados.

---

## Change

| Artefacto | Path |
|-----------|------|
| Proposal | `openspec/changes/archive/2026-07-30-consistencia-botones-detalle-issue-229/proposal.md` |
| Design | `openspec/changes/archive/2026-07-30-consistencia-botones-detalle-issue-229/design.md` |
| Tasks | `openspec/changes/archive/2026-07-30-consistencia-botones-detalle-issue-229/tasks.md` |
| Apply Progress | `openspec/changes/archive/2026-07-30-consistencia-botones-detalle-issue-229/apply-progress.md` |
| Verify Report | `openspec/changes/archive/2026-07-30-consistencia-botones-detalle-issue-229/verify-report.md` |
| Spec — web-detalle-consistencia-botones | `specs/web-detalle-consistencia-botones/spec.md` (synced to baseline) |
| Spec — web-ocupaciones-detalle | `specs/web-ocupaciones-detalle/spec.md` (synced to baseline) |

---

## Capabilities sincronizadas al baseline

| Spec | Requirements | Status |
|------|-------------|--------|
| `web-detalle-consistencia-botones` | REQ-DET-BTN-001 (barra fuera del card), REQ-DET-BTN-002 (btn-warning + ti-pencil), REQ-DET-BTN-003 (btn-outline-secondary + ti-arrow-left), REQ-DET-BTN-004 (URL Editar preserva p/search/sort), REQ-DET-BTN-005 (URL Volver preserva paginación), REQ-DET-BTN-006 (contenedor gap-2) | ✅ Sync'd |
| `web-ocupaciones-detalle` | REQ-OCC-DET-PAGE-001 (binding CurrentPage/Search/Sort en OnGetAsync; handlers POST intactos) | ✅ Sync'd |

Las delta-specs declararon ambas capabilities como **nuevas** (ningún spec previo
existía en `openspec/specs/`). Se copiaron como contenido principal, no como
delta, al no haber target de merge.

---

## Estado de PR

⚠️ **Nota operativa**: los 4 commits fueron aplicados **directo a `develop`**
sin crear una rama feature dedicada. El apply actor decidió no crear rama feature
para este change given que:
- El diff estimado era <400 líneas (single-PR strategy, `ask-on-risk`).
- El change era atómico y auto-contenido (3 archivos producción + 1 test).
- No había riesgo de interferir con trabajo concurrente en develop.

Commits en `develop` (HEAD `1d15a13`):

| Hash | Mensaje |
|------|---------|
| `583905e` | `feat(web): bind CurrentPage/Search/Sort in Ocupaciones Details PageModel` |
| `25bd59f` | `fix(web): align Ocupaciones Details buttons to canonical pattern` |
| `622e945` | `fix(web): align UnidadesOrganizativas Details buttons to canonical pattern` |
| `cce13e` | `test(web): align OcupacionDetails href assertion to Url.Page format` |

Recomendación: hacer PR contra `origin/develop` con esos 4 commits para que
queden documentados en el grafo de GitHub. Alternativamente, hacer squash-merge
si se prefiere un solo commit descriptivo.

---

## Métricas finales

| Métrica | Valor |
|---------|-------|
| Commits | 4 |
| Archivos producción tocados | 3 (`Ocupaciones/Details.cshtml`, `Ocupaciones/Details.cshtml.cs`, `UnidadesOrganizativas/Details.cshtml`) |
| Archivos canónicos intactos | 4 (`Cargos`, `Habilidades`, `Puestos`, `Personas` — 0 líneas de diff) |
| Archivos test ajustados | 1 (`OcupacionDetailsPageTests.cs` — corrección de contrato, no test nuevo) |
| Líneas netas producción | +54 / −27 |
| Tests suite completa | 3241/3241 PASS (segundo run, MySQL disponible) |
| Sub-suite Ocupaciones | 15/15 PASS |
| Sub-suite UnidadesOrganizativas | 262/262 PASS |
| Sub-suite web completa | 1351/1351 PASS |
| Escenarios verify | 11/11 PASS |
| CRITICAL issues | 0 |
| WARNING issues | 0 |
| verify-report `archive_recommendation` | `ready` |

---

## Notas operativas

- **Build**: 0 errors, 92 warnings preexistentes (xUnit1031, EF1002, etc. — none
  introduced by this change).
- **Test ajuste justificado**: `OcupacionDetailsPageTests.cs` L156 fue actualizado
  de `href` con Guid formato `D` (con guiones) a formato `N` (sin guiones) para
  reflejar el contrato `Url.Page`. Sigue el patrón de `CargoDetailsPageTests.cs`.
  No es un test nuevo, es corrección del contrato.
- **4 archivos canónicos verificados intactos** vía `git diff 785e10ee HEAD --`
  sobre `Cargos/Details.cshtml`, `Habilidades/Details.cshtml`,
  `Puestos/Details.cshtml`, `Personas/Details.cshtml` → 0 líneas de diff.
- **Handlers POST intactos**: `OnPostFinalizarAsync`, `OnPostEliminarAsync`,
  `OnPostReactivarAsync` y `TryLoadPersonaVinculadaAsync` de
  `Ocupaciones/Details.cshtml.cs` no fueron tocados.
- **Side-effects cero**: no se tocó API, Contracts, Dominio, Aplicación,
  Infraestructura, ni migraciones. No hay cambios de esquema DB.
- **Spec sync**: ambas delta-specs declararon `MODIFIED Requirements: (ninguno)` y
  `REMOVED Requirements: (ninguno)` — capability enteramente nueva. Se copiaron
  como spec completo a `openspec/specs/web-detalle-consistencia-botones/` y
  `openspec/specs/web-ocupaciones-detalle/`.
- **CLI usado**: `openspec archive consistencia-botones-detalle-issue-229 --yes
  --skip-specs`; specs sincronizadas manualmente post-archive.

---

## Siguiente paso recomendado

**PR a `origin/develop`** para documentar los 4 commits en el grafo de GitHub, o
squash-merge directo si se prefiere un commit atómico. El change está completo
y verificado; la issue #229 puede cerrarse tras el merge.
