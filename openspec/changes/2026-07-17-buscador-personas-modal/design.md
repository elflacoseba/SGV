# Design: Buscador modal reutilizable de Personas en Crear/Editar Usuario

## Resumen

Reemplaza el combo plano de `_Form.cshtml:11-28` (hoy vía `IPersonaOptionsProvider.GetActivasAsync()`) por un selector modal Bootstrap 5 con búsqueda server-side sobre `GET /api/v1/personas/consulta?soloSinUsuario=true` (anti-join contra `AspNetUsers.PersonaId`), paginación 25. Replica el patrón de `archive/2026-07-17-modal-confirmacion-bloqueo-desbloqueo`. `409` por carrera → feedback de campo análogo a Cargos. Sin migraciones ni dependencias nuevas.

## Decisiones de diseño

| ID | Decisión | Rationale |
|----|----------|-----------|
| D-01 | Query `soloSinUsuario=true\|false` | Fija el nombre usado por proposal y spec REQ-PM-01. |
| D-02 | `PersonaListQuery` + `bool? SoloSinUsuario = null` | Nullable → cliente omite el parámetro cuando `null`/`false` (back-compat Index Personas). |
| D-03 | `ViewData` partial: `ModalId`/`HiddenInputName`/`HiddenInputId`/`DisplayContainerId` (req) + `CurrentPersonaId` (Guid?)/`CurrentPersonaDisplay` (string?) (opcional) | Mismo patrón plano que `_ConfirmarAccionUsuarioModal.cshtml`. |
| D-04 | Paginación `Anterior` + numérica (1..N con elipsis si >7) + `Siguiente` | REQ-USB-04 lo permite; patrón vigente en el repo. |
| D-05 | Borrar `IPersonaOptionsProvider` / `HttpPersonaOptionsProvider` / `FakePersonaOptionsProvider` / `IUsuarioForm.PersonaOptions` | Grep: únicos consumidores son `_Form.cshtml:17,35`, `Create.cshtml:11,45`, `Edit.cshtml.cs:38,45,126,130,273,279`, `Create.cshtml.cs:34,41,219,225` y sus tests. |
| D-06 | REQ-UCE-09: `Create.OnGetAsync` invoca `IPersonaApiClient.QueryAsync(page=1, pageSize=1, soloSinUsuario=true)` y deriva de `TotalCount` | `pageSize=1` validado por rango 1..100; 1 round-trip extra en GET Crear; sin endpoint nuevo. |
| D-07 | `aria-live="polite"` SHOULD; MUST si auditoría AA formal lo exige | Spec fija atributos MUST; regiones como SHOULD. Confirmar con issue #157. |
| D-08 | JS en `wwwroot/js/pages/usuario-persona-buscador.js` vía `@section Scripts` | Espejo de `personas-index.js`. |
| D-09 | Extender `IPersonaApiClient.QueryAsync(PersonaListQuery)` sin agregar `BuscarAsync` | Mantener una única superficie wire. |
| D-10 | `409` → `ModelState.AddModelError(string.Empty, "Esa persona ya tiene un usuario activo.")` preservando form | Espejo del patrón `CodigoDuplicado` de Cargos. |

## Arquitectura

```
Create/Edit OnGet → @PartialAsync("_PersonaBuscadorModal", viewData)
                          ↓
   modal + @section Scripts { usuario-persona-buscador.js }
                          ↓
   GET /api/v1/personas/consulta?search=&soloSinUsuario=true&p=&pageSize=25
                          ↓
   PersonasController → Servicio → Repository (LEFT JOIN AspNetUsers cuando aplica)
                          ↓
   Render tabla paginada; "Seleccionar" setea hidden + dispara `change`
```

## Cambios por capa

| Capa | Acción |
|------|--------|
| **Contratos** | `PersonaListQuery.cs`: + `bool? SoloSinUsuario = null`. |
| **Aplicación** | `IPersonaRepository` / `IPersonaServicioConsulta` / `PersonaServicioConsulta`: propagar `soloSinUsuario`. |
| **Infraestructura** | `PersonaRepository.QueryAsync`: anti-join contra `Set<SgvIdentityUser>()` cuando `soloSinUsuario==true && Activas`; cortocircuito a `items=[]` si `Eliminadas`. |
| **API** | `PersonasController.GetConsulta`: + `[FromQuery] bool? soloSinUsuario = null`. |
| **Web cliente** | `PersonaApiClient.BuildQueryUri`: agrega `&soloSinUsuario=true` sólo si `SoloSinUsuario == true`. `FakePersonaApiClient`: + helper `WithSoloSinUsuarioSet(IEnumerable<Guid>)`. |
| **Web UI** | Crear `_PersonaBuscadorModal.cshtml` y `wwwroot/js/pages/usuario-persona-buscador.js`. Modificar `_Form.cshtml`/`Create.cshtml(.cs)`/`Edit.cshtml(.cs)`: reemplazar combo por `@PartialAsync`; inyectar `IPersonaApiClient` para REQ-UCE-09; quitar `LoadPersonasAsync` y `PersonaOptions`. `IUsuarioForm`: quitar `PersonaOptions`. |
| **Web (cleanup D-05)** | `Program.cs:195`: quitar registro. Borrar `IPersonaOptionsProvider.cs`, `HttpPersonaOptionsProvider.cs`, `FakePersonaOptionsProvider.cs`. |
| **Tests** | `CreatePageTests.cs`/`EditPageTests.cs`: sustituir `FakePersonaOptionsProvider` por `FakePersonaApiClient.QueryHandler`. `SgvWebApplicationFactory.cs`/`WebIntegrationFixture.cs`: quitar `WithPersonaOptionsProvider` y overloads `CreateUsuarioLeaseAsync(..., personaOptionsProvider)`. |

## Estrategia TDD y Plan (1 PR, ~600 líneas, 3 commits `strict_tdd`)

Tests antes del código. RED → GREEN → REFACTOR por work unit; commits: (1) `test(repo)`+`feat(repo+service+api)` = WU-1..3; (2) `test(client)`+`feat(client)` = WU-4; (3) `test(web)`+`feat(web)` = WU-5..8 + borrado D-05. Sin chained PRs.

| WU | Capa | Tests RED | GREEN |
|----|------|-----------|-------|
| WU-1 | Repo `[MySqlFact]` | 4: `_true_excluye_con_usuario`, `_true_con_eliminadas_vacio`, `_false_preserva_vigente`, `_combina_con_search_sort_paginacion` | Extender `QueryAsync` + LEFT JOIN |
| WU-2 | Servicio `[Fact]` | 4 paralelos a WU-1 con `FakePersonaRepository` extendido | Propagar `SoloSinUsuario` en `ListarAsync` |
| WU-3 | Controller `[ApiIntegration]` | 4: `?soloSinUsuario=true` con Activas/Eliminadas/combinado | Extender `GetConsulta` |
| WU-4 | Cliente `RecordingHandler` + `FakePersonaApiClient` | `_WithSoloSinUsuarioTrue_SerializesInUri`, `_WithNullOrFalse_OmitsParameter`, `_TransportFails_PropagatesNativeException` | Extender `BuildQueryUri` |
| WU-5 | Page Create `[WebIntegration]` | Sin `<select>`, banner REQ-UCE-09 cuando `TotalCount==0`, 409 preserva form | Modificar `Create.cshtml(.cs)` |
| WU-6 | Page Edit | Persona precargada como card; `Quitar` → vacío; `Cambiar` → popup excluye persona actual | Modificar `Edit.cshtml(.cs)` |
| WU-7 | Modal `[WebIntegration]` | `#usuario-persona-buscador-modal` con `role="dialog"`/`aria-modal`/`aria-labelledby`; estados Inicial/Empty/Loading/Error; paginación Prev/Next + numérica | Crear `_PersonaBuscadorModal.cshtml` |
| WU-8 | JS | `Esc`/backdrop/X cierran sin tocar hidden; `Seleccionar` setea hidden + `change`; URL lleva `pageSize=25` | Crear `wwwroot/js/pages/usuario-persona-buscador.js` |

## Riesgos técnicos

- **`LEFT JOIN AspNetUsers` agrega costo si olvidamos cortocircuitar** → plan explícito: rama `soloSinUsuario==true && Activas`; resto bit-identical. WU-1 cubre back-compat.
- **`pageSize=1` en GET Crear agrega 1 round-trip** → aceptado por REQ-UCE-09. Alternativa `?count=true` en Open Questions.
- **`FakePersonaApiClient` no modela `AspNetUsers.PersonaId`** → helper `WithSoloSinUsuarioSet(IEnumerable<Guid>)` en WU-4.
- **Eliminar `IPersonaOptionsProvider` rompe tests / paginación ruidosa** → WU-5/WU-6 modifican tests en el mismo commit; `dotnet test` completo sin Skip. Elipsis `±2` cuando `TotalPages > 7` con `d-none d-md-inline-block` (sin CSS ad-hoc).

## Compatibilidad y Validación

**Specs referenciadas (intactas):** `web-apiclient-transport-contract` (WU-4 cubre excepciones nativas), `sgv-web-authentication`, `identity-user-role-management` (modal sólo desde Create/Edit Usuario con `[Authorize(Roles=Administrador)]`), `usuario-web-listado-detalle-baja`. **Specs modificadas (delta):** `persona-management` (REQ-PM-01 + MODIFIED Requirement), `usuario-web-crear-editar` (REQ-UCE-08/09/10 + MODIFIED REQ-UCE-02). **Validación:** `dotnet build SGV.slnx` 0 errors / 0 warnings nuevos (23 `CS8524` preexistentes); `dotnet test SGV.slnx` suite verde (incluyendo nuevos `[MySqlFact]`); `bun install && bun run build` OK. Smoke manual: Crear → buscador → seleccionar → PRG Details; Editar → Cambiar → submit; 409 carrera → form preservado; DB sin candidatas → banner + CTA a `/personas/crear`.

## Open Questions

- **AA-01**: confirmar con issue #157 si `aria-live="polite"` debe ser MUST (auditoría AA formal) o permanece SHOULD. Si MUST, elevar en REQ-USB-09 antes de `sdd-apply`.
- **D-06 alternativa**: si se prefiere evitar el round-trip extra en GET Crear, alternativa = endpoint `GET /api/v1/personas/sin-usuario?count=true`. Reversibilidad = 1 PR adicional.