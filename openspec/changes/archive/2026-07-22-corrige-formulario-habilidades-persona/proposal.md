# Propuesta: Corrige el formulario de habilidades de una persona

## Why

`/personas/{id:guid}/habilidades` renderiza los `<select>` de "Habilidad" y "Nivel" del form "Asignar" sólo con placeholder: ningún usuario puede elegir skill ni nivel. El GET handler carga persona + asociaciones pero nunca consulta el catálogo de habilidades activas ni el de niveles, así que los `<option>` jamás aparecen.

El patrón correcto ya vive en `SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs::LoadSkillsAndCatalogsAsync` (L133-155): llama en paralelo a `GetSkillsAsync` + `IHabilidadApiClient.GetAllAsync` + `GetNivelesHabilidadAsync` y expone `HabilidadesDisponibles` + `NivelOptions` a la vista.

Este change es un delta chico sobre `implementa-persona-habilidades` (no archivado, en `openspec/changes/implementa-persona-habilidades/`), que acercó el PageModel a ese patrón pero omitió la carga de catálogos.

## What Changes

- Inyectar `IHabilidadApiClient` en `PersonaHabilidadesModel` y exponer un helper análogo a `LoadSkillsAndCatalogsAsync` que cargue asociaciones + skills + niveles en paralelo, tratándolos como recuperables si fallan.
- Sumar a `PersonaHabilidadesViewModel` dos colecciones: `IReadOnlyList<HabilidadListItemViewModel>` (reusando el tipo de CargoHabilidades) y `IReadOnlyList<NivelHabilidadDto>`.
- Iterar las nuevas colecciones en los `<select>` de `PersonaHabilidades.cshtml`; conservar placeholder y nombres de campo (`SkillId`, `NivelHabilidadId`).
- Extender `FakePersonaApiClient` + `WebIntegrationFixture` con un `FakeHabilidadApiClient` con seed (paridad con `CreateCargoLeaseAsync`); sumar dos casos a `PersonaHabilidadesPageTests`: GET renderiza `<option>` y catálogo caído deja la grilla con mensaje recuperable.

## Impact

- `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` (constructor, `OnGetAsync`, `ReloadAfterFailedAsignarAsync`, `PersonaHabilidadesViewModel`).
- `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml` (los dos `<select>` del form "Asignar").
- `tests/SGV.Tests/Web/Persona/{FakePersonaApiClient,WebIntegrationFixture,PersonaHabilidadesPageTests}.cs`.
- Sin cambios en API, `SGV.Contracts`, persistencia ni DI global — `IHabilidadApiClient` ya está registrado.

## Non-Goals

- Endpoints, DTOs de `SGV.Contracts.Personas` o wire JSON.
- Edición de `VerificadoAt`/`Fuente` (sigue diferido).
- Refactor de `PersonaHabilidadesViewModel` más allá de las dos propiedades nuevas.
- Grilla inline por fila: sus `<select>` ofrecen sólo el nivel actual y escapan del bug — no se tocan.
- Performance, accesibilidad, i18n o estilos.

## Acceptance Criteria

- GET `/personas/{id:guid}/habilidades` con `Administrador` MUST renderizar `<option>` no vacíos en `<select id="SkillId">` y `<select id="NivelHabilidadId">`, derivados de catálogos poblados por la API.
- GET con persona activa pero catálogo caído por transporte MUST mostrar la grilla y un mensaje legible, dejando los selects sólo con placeholder (sin romper la página).
- POST "Asignar" con form inválido (skill o nivel vacío) MUST recargar la página con ambos selects poblados y mostrar el error de `ModelState`.
- `dotnet build SGV.slnx` y `dotnet test SGV.slnx` MUST pasar, sin nuevos tests de controlador que sólo delegan.

## Next Step

`sdd-spec` → ajustar `openspec/specs/persona-skill-web-management/spec.md` (Requirement "Listado, asignación y baja" → scenario de catálogos poblados) y, si hace falta, delta sobre el spec del módulo cargo para mantener paridad explícita.