# Diseño técnico: Corrige el formulario de habilidades de una persona

## Resumen

Delta chico sobre el change abierto `implementa-persona-habilidades`. Inyecta `IHabilidadApiClient` en `PersonaHabilidadesModel`, carga en paralelo los catálogos de habilidades activas y de niveles junto con las asociaciones de la persona, expone las dos colecciones nuevas en `PersonaHabilidadesViewModel` y las itera en los `<select>` del form "Asignar". Sin cambios de contratos, API, persistencia ni DI global.

## Cambios por capa

### Producción

| Ruta | Acción | Detalle |
|---|---|---|
| `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` | Modificar | Constructor primario: sumar `IHabilidadApiClient habilidadApiClient` como segundo parámetro (después de `IPersonaApiClient`, antes de `ILogger`). Exponer `internal IHabilidadApiClient HabilidadApiClient => habilidadApiClient;` siguiendo el patrón de `Pages/Organizacion/Cargos/Habilidades.cshtml.cs::L33`. |
| Idem | Modificar | `OnGetAsync` (L52-90): después de validar persona activa, invocar un helper nuevo `LoadCatalogsAsync(Guid id, CancellationToken ct)` que lance `Task.WhenAll` con `personaApiClient.GetSkillsAsync(id, ct)` + `habilidadApiClient.GetNivelesHabilidadAsync(ct)` + `habilidadApiClient.GetAllAsync(ct)`. Mapear `GetAllAsync` → `HabilidadListItemViewModel(Id, Codigo, Nombre, Descripcion, Categoria)` con un `Select` análogo a `Habilidades.cshtml.cs::L145-148`. Asignar a `ViewModel.HabilidadesDisponibles` y `ViewModel.NivelOptions`. Capturar sólo `TransportFailureClassifier.IsTransportFailure(ex)` (igual que `Habilidades.cshtml.cs::L150-155`); en ese caso, dejar las dos colecciones vacías y setear `ErrorMessage` si aún no hay uno. **No** redirigir: el spec exige que la grilla + los placeholders sigan renderizándose. |
| Idem | Modificar | `ReloadAfterFailedAsignarAsync` (L251-270): invocar `LoadCatalogsAsync` después de recargar la persona/skills para que el re-render tras un POST inválido también pinte los selects. |
| Idem | Modificar (record) | `PersonaHabilidadesViewModel` (L276-305): sumar dos propiedades `init` con default `[]`: `IReadOnlyList<HabilidadListItemViewModel> HabilidadesDisponibles` y `IReadOnlyList<NivelHabilidadDto> NivelOptions`. Sumar una sobrecarga `From(persona, skills, catalogos)` que reciba las dos colecciones, o ampliar `From` con parámetros opcionales + un método estático separado `From(persona, skills, habilidades, niveles)` para mantener back-compat con los call sites existentes. **Decisión**: añadir sobrecarga nueva; el `From(persona, skills)` vigente sigue existiendo para no propagar cambios al call site del happy path. |
| `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml` | Modificar | En los dos `<select>` del form "Asignar" (L116-118 y L123-125): iterar `Model.ViewModel.HabilidadesDisponibles` y `Model.ViewModel.NivelOptions` con un `foreach` que renderice `<option value="@item.Id">@item.Nombre</option>` (o `Codigo` + `Nombre` si el catálogo de habilidades es denso). Preservar el placeholder como primera `<option>` y el atributo `selected` no debe agregarse porque las colecciones son derivadas, no preseleccionadas. |

### Resolución del tipo compartido `HabilidadListItemViewModel`

**Decisión**: NO se reubica. Ya vive en `src/SGV.Web/Integration/Habilidades/HabilidadListItemViewModel.cs` (namespace `SGV.Web.Integration.Habilidades`), NO en `Pages/Organizacion/Cargos/` como sugiere el proposal.

| Opción | Tradeoff | Decisión |
|---|---|---|
| Usar tal cual desde `SGV.Web.Integration.Habilidades` | Cero acoplamiento cross-árbol; ambos Pages ya referencian el namespace raíz del proyecto. | **Adoptada** |
| Mover a `Pages/Shared/Catalogos/` | Cruza la frontera entre Web (composition) y un sub-árbol de Pages específico; viola la separación actual entre `Integration/` (clientes + DTOs de UI) y `Pages/` (Razor). | Descartada |
| Duplicar el record en `Pages/Personas/` | Genera drift silencioso cuando el record en Cargo sume un campo (descripción/categoría). | Descartada |

### Patrón de carga paralela

**Decisión**: Helper `internal async Task LoadCatalogsAsync(Guid id, CancellationToken ct)` dentro de `PersonaHabilidades.cshtml.cs`, NO helper compartido cross-archivo.

| Opción | Tradeoff | Decisión |
|---|---|---|
| Réplica local (~25 líneas) | Paridad estructural 1-a-1 con `Habilidades.cshtml.cs::LoadSkillsAndCatalogsAsync`; ambos helpers quedan `internal` y privados a su PageModel; sin acoplamiento cross-árbol. | **Adoptada** |
| Extraer a `Pages/Common/CatalogLoader.cs` | Obliga a moverlo a un tercer archivo y diseñar una signature genérica que cubra tres clientes distintos (Persona/Cargo + Skills/Niveles); costo de abstracción > beneficio. | Descartada |
| Mover el helper de Cargo a `Pages/Common/` | Rompe encapsulamiento del módulo Cargo y arrastra renames en su test (`CargoHabilidadesLoadTests`). | Descartada |

`LoadCatalogsAsync` es **análogo** (no compartido) a `LoadSkillsAndCatalogsAsync`: misma forma `Task.WhenAll` + `TransportFailureClassifier` + `Select` → record. Diferencias: este NO llama a `GetSkillsAsync` del cliente de Persona (eso ya viene de `OnGetAsync`); sólo agrega los dos clientes de catálogo.

## Inyección de dependencias

`IHabilidadApiClient` **YA está registrado** en `src/SGV.Web/Program.cs:171` vía `builder.Services.AddHttpClient<IHabilidadApiClient, HabilidadApiClient>` (verificado en `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs::L24` que lo consume). **No se requiere registro adicional** en producción.

En tests:
- `tests/SGV.Tests/Web/Persona/WebIntegrationFixture.cs` ya tiene el override de `IPersonaApiClient`. Sumar un override equivalente para `IHabilidadApiClient` con un `FakeHabilidadApiClient` análogo (paridad con `FakeCargoApiClient`/`CreateCargoBridgeLeaseAsync`).
- `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs:149` construye `new PersonaHabilidadesModel(apiClient, NullLogger<…>)` — al sumarse el segundo parámetro hay que añadir un `FakeHabilidadApiClient().WithSeed(...)` o equivalente.

## Tests / fakes

### Ampliaciones a fakes

| Fake | Acción | Detalle |
|---|---|---|
| `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` | Sin cambios | Ya tiene `GetSkillsResult` / `GetSkillsException` / `GetSkillsCalls` (L386-462) heredados de Slice 2. No se toca. |
| `tests/SGV.Tests/Web/Persona/FakeHabilidadApiClient.cs` (nuevo) | Crear | Mínimo viable: `GetAllResult` / `GetAllException` / `GetAllCalls` + `GetNivelesResult` / `GetNivelesException` / `GetNivelesCalls` + `WithSeed(...)` fluente. Réplica estructural de `FakeCargoApiClient` reducido al subconjunto que consume `PersonaHabilidadesModel`. |

### Ampliaciones a tests existentes

| Test file | Acción | Escenario |
|---|---|---|
| `PersonaHabilidadesPageTests.cs` (existente) | Modificar línea 149 | Constructor: añadir `FakeHabilidadApiClient().WithSeed(habilidades, niveles)` como 2° argumento del PageModel. |
| `PersonaHabilidadesPageTests.cs` (existente) | Sumar 1 test | `OnGet_PopulatesCatalogsFromHabilidadApiClient`: dado persona activa + seeds, GET debe invocar `GetAllAsync` y `GetNivelesHabilidadAsync` y dejar `ViewModel.HabilidadesDisponibles` y `NivelOptions` populados. |
| `PersonaHabilidadesPageTests.cs` (existente) | Sumar 1 test | `OnGet_HabilidadApiClientTransportFailure_LeavesCatalogsEmptyAndKeepsPageAlive`: el helper captura la falla, las dos colecciones quedan `[]`, `IsRecoverable=true`, `ErrorMessage` legible. |
| `PersonaHabilidadesPageTests.cs` (existente) | Sumar 1 test (opcional) | `OnPostAsignar_ModelStateInvalid_AfterFailedValidation_AlsoReloadsCatalogs`: cubre el spec scenario "POST inválido recarga los catálogos". |

> **No** se agregan tests de controlador (no aplica), ni de WebApplicationFactory nuevos (los 11 de integración ya cubren auth + POST; el bug del catálogo sólo es observable a través del GET handler, que ya está cubierto por los tests PageModel).

## Contratos confirmados

| Endpoint | Contrato | Verificación |
|---|---|---|
| `GET /api/v1/skills` | `Task<IReadOnlyList<HabilidadDto>>` — activo, lista plana. | `IHabilidadApiClient.GetAllAsync` (`src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs:17`). Mapea a `HabilidadListItemViewModel(Id, Codigo, Nombre, Descripcion, Categoria)`. |
| `GET /api/v1/niveles-habilidad` | `Task<IReadOnlyList<NivelHabilidadDto>>` — catálogo de niveles. | `IHabilidadApiClient.GetNivelesHabilidadAsync` (`src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs:45`). Wire-types en `src/SGV.Contracts/Habilidades/Consultas/Dtos/`. |

Ambos shapes ya existen y son consumidos por `Pages/Organizacion/Cargos/Habilidades.cshtml.cs` — **confirmados**, sin cambios de wire.

## Riesgos y mitigaciones

| # | Riesgo | Mitigación |
|---|---|---|
| 1 | **Bajo**: tests existentes de `PersonaHabilidadesPageTests` rompen al añadir el 2° parámetro del constructor. | Cambio mecánico en una línea (L149). Build verde tras el fix. Sin nuevos mocks. |
| 2 | **Bajo**: la sobrecarga `From(persona, skills, habilidades, niveles)` puede romper el call site de Slice 3b si la firma vigente se renombra en vez de sobrecargarse. | Mantener `From(persona, skills)` como 1ª signature y agregar la 2da con parámetros nuevos (cero cambio a los 4 call sites existentes en producción + tests). |
| 3 | **Bajo**: si `LoadCatalogsAsync` re-lanza una excepción NO clasificada como transporte, podría romper el helper que captura todo. | Replicar el filtro `TransportFailureClassifier.IsTransportFailure(ex)` exactamente como `Habilidades.cshtml.cs::L150`; cualquier otra excepción burbujea (paridad con Cargo). |

## No-objetivos (heredados del proposal)

- Endpoints, DTOs de `SGV.Contracts.Personas`, wire JSON.
- Edición de `VerificadoAt` / `Fuente` (sigue diferido).
- Refactor de `PersonaHabilidadesViewModel` más allá de las dos propiedades nuevas.
- Grilla inline por fila: sus `<select>` (L80-82) ofrecen sólo el nivel actual — escapan del bug y no se tocan.
- Performance, accesibilidad, i18n o estilos.

## Estimación

| Área | Líneas |
|---|---|
| PageModel (`PersonaHabilidades.cshtml.cs`) | 35-50 |
| Vista (`PersonaHabilidades.cshtml`) | 10-15 |
| ViewModel (sobrecarga `From`) | 5-10 |
| `FakeHabilidadApiClient.cs` | 40-60 |
| Tests `PersonaHabilidadesPageTests.cs` | 50-90 |
| **Total** | **~140-225 líneas** |

Forecast dentro del budget de 400 líneas del review.

## Próximo paso

`sdd-tasks` debe confirmar el particionado (un solo slice pequeño es viable) y el orden de PR. No reabrir las decisiones congeladas del change `implementa-persona-habilidades`.
