# Exploración: Implementar el módulo de Puestos en el Frontend

> Cambio frontend-only para `SGV.Web`. El backend de Puestos ya existe y está archivado
> (`openspec/changes/archive/2026-06-19-implementa-modulo-puestos/`, PR #24). Esta
> exploración mapea qué hay listo, qué hay que crear y dónde está la diferencia
> respecto del patrón de paridad con Cargos.

## Estado actual

### Backend ya entregado (fuera de alcance de este cambio)

- `src/SGV.Dominio/Organizacion/Puesto.cs` — entidad con `Codigo`/`Nombre`/`Descripcion`/`UnidadOrganizativaId`/`CargoId`/`PuestoSuperiorId` (self-reference opcional). Reglas de dominio: `Codigo` es **inmutable** tras la creación, `PuestoSuperiorId != Id`, soft-delete via `Desactivar()` y reactivación via `Activar()`.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/PuestoConfiguracion.cs` — tabla `Puestos`, FKs `Restrict` contra `UnidadesOrganizativas`, `Cargos` y self-ref, columna computada `ActiveCodigoUnique = CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END` con índice único (mismo patrón que Cargos).
- `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` — `Query` filtra `IsActive` y carga eager `UnidadOrganizativa`/`Cargo`; expone `GetByIdForUpdateAsync` (activos no eliminados), `GetByIdIncludingDeletedAsync`, `DeleteAsync` (set `IsActive=false`, `IsDeleted=true`, `DeletedAt=UtcNow`), `ReactivateAsync`, `ExistsActiveCodeAsync` (excluyendo opcionalmente el propio id).
- `src/SGV.Aplicacion/Organizacion/Consultas/PuestoServicioConsulta.cs` — `ListAsync()` devuelve solo activos ordenados por `Codigo`; `GetByIdAsync` mapea a `PuestoDto` con nombres de UO y Cargo proyectados.
- `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` — `CrearAsync`/`ActualizarAsync`/`DesactivarAsync`/`ReactivarAsync`, todos con `PuestoCommandResult` tipado. Conflictos posibles: `CodigoDuplicado`, `UnidadOrganizativaNoExiste`, `CargoNoExiste`, `PuestoSuperiorNoExiste`, `PuestoSuperiorInvalido` (auto-padres).
- `src/SGV.Api/Controllers/PuestosController.cs` — endpoints ya implementados:
  - `GET /api/v1/puestos` → lista plana de activos.
  - `GET /api/v1/puestos/{id:guid}` → 200 / 404.
  - `POST /api/v1/puestos` → 201 / 400 (con `ValidationProblemDetails`) / 409.
  - `PUT /api/v1/puestos/{id:guid}` → 200 / 400 / 404 / 409.
  - `DELETE /api/v1/puestos/{id:guid}` → 204 / 404. (Nota: `Desactivar` hoy solo puede devolver `NotFound`; el éxito devuelve `Success(null!)` y la API mapea a 204. No hay regla de bloqueo por puestos activos como en Cargo.)
  - `PATCH /api/v1/puestos/{id:guid}/reactivar` → 200 / 404 / 409.
- **Brecha del backend respecto del patrón Cargos**: `PuestosController` **NO** tiene `[Authorize]` a nivel de clase ni `[Authorize(Roles = RolesSgv.Administrador)]` en las mutaciones; `tests/SGV.Tests/Api/PuestosControllerTests.cs:87-94` lo fija explícitamente con `Controller_DoesNotHaveAuthorizeAttribute`. Tampoco existe un endpoint `GET /api/v1/puestos/consulta?status=activas|eliminadas` paginado (el listado es plano). Esto es exactamente el gap que cerró `2026-07-01-...-cargos-crear-autorizacion-admin` y `2026-07-02-cargos-filtro-activos-eliminados` para Cargos.
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/PuestoDto.cs` — DTO con FKs y nombres proyectados: `(Id, Codigo, Nombre, Descripcion, UnidadOrganizativaId, UnidadOrganizativaNombre, CargoId, CargoNombre, PuestoSuperiorId)`. Es el contrato real que verá el frontend; `Codigo` no se expone como editable en update.
- Tests backend existentes: `PuestoTests`, `CrearPuestoRequestValidatorTests`, `ActualizarPuestoRequestValidatorTests`, `PuestoServicioConsultaTests`, `PuestoServicioComandosTests`, `PuestoRepositoryTests`, `PuestosControllerTests`. **No** se tocan en este cambio.

### Frontend `SGV.Web` (alcance del cambio)

- `src/SGV.Web/Pages/Organizacion/` ya aloja tres módulos Razor: `Cargos/`, `Habilidades/`, `UnidadesOrganizativas/`. `Puestos/` no existe. No hay menciones a `puesto` en `src/SGV.Web` (verificado con grep).
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml(.cs)` es el patrón vigente de paridad que debemos replicar: `[Authorize]`, consulta server-side `QueryAsync` con `PagedResult<CargoDto>`, segmento `activas`/`eliminadas` con query string `status`, `OnPostDeleteAsync` + `OnPostReactivateAsync`, `TempData` con `StatusMessage`/`StatusKind`, `LastDeletedId` para CTA rápido, `BuildToggleSegmentoRouteValues` para el toggle y preservación de `p`/`search`/`sort`/`status` en todos los links, forms y POST.
- `src/SGV.Web/Integration/Organizacion/` contiene el contrato+cliente+view models de `Cargos` e `UnidadesOrganizativas`. No hay nada para `Puestos`.
- `src/SGV.Web/Program.cs` registra `IAuthApiClient`, `IUnidadOrganizativaApiClient`, `ICargoApiClient` y `IHabilidadApiClient` con `HttpClient` + `ApiBearerTokenHandler`. Los clientes nuevos deben seguir exactamente esa plantilla (BaseAddress desde `SgvApiOptions`, `AddHttpMessageHandler` con `ApiBearerTokenHandler` y `Timeout = 10s` para bounded UX).
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` ya tiene entries para `Unidades Organizativas`, `Cargos` y `Habilidades`. La entry nueva para `Puestos` debe ir en el subgrupo `Organización` siguiendo la forma colapsable de los vecinos.
- `src/SGV.Web/Pages/Error/` ya cubre 400/401/403/404/408/500/Maintenance; nada que crear.
- `tests/SGV.Tests/Web/` ya tiene `Cargo/` (con `CargoWebTestFixture`, `FakeCargoApiClient`, `CargoApiClientTests`, `CargoIndexPageTests`, `CargoDetailsPageTests`, `CargoCreatePageTests`, `CargoEditPageTests`, `CargoWebSeamTests`, `ICargoApiClientContractTests`, `ApiBearerTokenIntegrationTests`), `Habilidad/` (paralelo), `Auth/`, `_Shared/HttpClientExceptionScenarios*` y `SgvWebApplicationFactory.cs`. La carpeta `Puesto/` no existe todavía.

## Áreas afectadas

| Archivo | Acción | Por qué |
|---|---|---|
| `src/SGV.Web/Program.cs` | Modificar | Registrar `IPuestosApiClient` como `HttpClient` tipado con `ApiBearerTokenHandler` y `Timeout=10s`. |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modificar | Agregar entry colapsable "Puestos" con sub-items `Listado` y `Nuevo`, y estado activo en `/organizacion/puestos(/...)`. |
| `src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs` | Crear | Contrato tipado: `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` (`PuestoDeleteResult`), `ReactivateAsync` (`PuestoCommandResult`). |
| `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs` | Crear | Cliente HTTP que serializa `CrearPuestoRequest`/`ActualizarPuestoRequest`, mapea `ProblemDetails`/`ValidationProblemDetails` a `PuestoCommandResult` (espejo de `CargoApiClient.ToCommandResultAsync`) y traduce `DELETE` a `PuestoDeleteResult`. |
| `src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs` | Crear | Record de grilla `(Id, Codigo, Nombre, Descripcion, UnidadOrganizativaNombre, CargoNombre, PuestoSuperiorId)`. |
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` | Crear | Tabla Inspinia con toggle Activas/Eliminadas, búsqueda, orden, paginación, acciones Detalle/Editar/Eliminar (en activas) y Reactivar (en eliminadas). |
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` | Crear | `[Authorize]`, `OnGetAsync`/`OnPostDeleteAsync`/`OnPostReactivateAsync`, segmento `activas`/`eliminadas`, `LastDeletedId` y `BuildToggleSegmentoRouteValues`. |
| `src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml` | Crear | Vista readonly con datos del Puesto + nombres de UO/Cargo + link al Puesto superior si existe. |
| `src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml.cs` | Crear | `[Authorize]`, `OnGetAsync(id, p, search, sort)`, preservar contexto de retorno. |
| `src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml` | Crear | Form con `Codigo`/`Nombre`/`Descripcion`/`UnidadOrganizativaId`/`CargoId`/`PuestoSuperiorId`. |
| `src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml.cs` | Crear | `[Authorize]`, OnPost con `ModelState` desde `FieldErrors` y redirección al listado por PRG. |
| `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml` | Crear | Form **sin** campo `Codigo` (inmutable en Puesto). |
| `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` | Crear | `[Authorize]`, OnPost con `ActualizarPuestoRequest(Nombre, Descripcion?, PuestoSuperiorId?)`. |
| `src/SGV.Web/Pages/Organizacion/Puestos/_Form.cshtml` | Crear | Partial compartido entre Create/Edit (mismo patrón que `Cargos/_Form.cshtml`). |
| `src/SGV.Web/wwwroot/js/pages/puestos-index.js` | Crear | `wirePuestoDeleteConfirmation` + `wirePuestoReactivateConfirmation` (espejo de `cargos-index.js`, SweetAlert2). |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modificar | Permitir override de `IPuestosApiClient` (paridad con el override de `ICargoApiClient`). |
| `tests/SGV.Tests/Web/Puesto/PuestoWebTestFixture.cs` | Crear | Helper de autenticación + cookies + override del cliente fake (espejo de `CargoWebTestFixture`). |
| `tests/SGV.Tests/Web/Puesto/FakePuestosApiClient.cs` | Crear | In-memory fake con captura de llamadas (`GetAllCalls`, `CreateCalls`, etc.) y overrides por escenario. |
| `tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs` | Crear | Tests unitarios del cliente HTTP con `HttpClient` mockeado para 2xx/4xx/5xx y traducción de `ProblemDetails`/`ValidationProblemDetails`. |
| `tests/SGV.Tests/Web/Puesto/IPuestosApiClientContractTests.cs` | Crear | Pruebas de contrato del interface (espejo de `ICargoApiClientContractTests`). |
| `tests/SGV.Tests/Web/Puesto/PuestoWebSeamTests.cs` | Crear | Override en `SgvWebApplicationFactory` y smoke (espejo de `CargoWebSeamTests`). |
| `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs` | Crear | Cobertura del listado: render activo/eliminado, búsqueda, orden, paginación, error visible, POST Delete/Reactivate, preservación de segmento. |
| `tests/SGV.Tests/Web/Puesto/PuestoDetailsPageTests.cs` | Crear | Render del detalle, estado 404 preservando contexto, retorno al listado. |
| `tests/SGV.Tests/Web/Puesto/PuestoCreatePageTests.cs` | Crear | Render del form, POST exitoso (PRG), POST con `FieldErrors` mostrado junto al input, POST 409 por código duplicado. |
| `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` | Crear | Render del form sin `Codigo`, POST exitoso, POST con `FieldErrors`, POST 409. |

## Enfoques comparados

### A. Slice web con paridad 1:1 con Cargos (recomendado)

Replica el patrón actual de `Cargos`: listado segmentado, baja lógica, reactivación, create, edit, details. Todo el corte queda en `SGV.Web` y se apoya en los endpoints que ya expone `PuestosController`. Imita el flujo `2026-06-30-implementar-modulo-de-cargos-en-el-frontend` (PR 1+2+3 chained) más el delta `2026-07-02-cargos-filtro-activos-eliminados` (toggle/reactivación server-side), pero **sin** esperar a un eventual `puestos-filtro-activos-eliminados` o `puestos-crear-autorizacion-admin`.

- Pros: cierra el módulo Puestos en `SGV.Web` de punta a punta con el mismo seam que ya probaron Cargos y Habilidades; reusa `SgvWebApplicationFactory`, `CargoWebTestFixture` como plantilla, fake client y JS SweetAlert2.
- Contras: hereda los huecos del backend: (1) la vista de eliminadas usará `GET /api/v1/puestos` (que solo trae activos) hasta que se agregue un endpoint `consulta` segmentado — el slice web puede arrancar con `GetAllAsync` y dejar el toggle "Eliminadas" deshabilitado o no mostrarlo hasta que exista backend. (2) Como no hay `[Authorize]`, los usuarios anónimos pueden hoy listar vía API. La UI asume autenticación por cookie igual que Cargos (la `ApiBearerTokenHandler` adjunta el JWT si existe; si no, la API igual responde 200 hoy).
- Esfuerzo: **Medio**.

### B. Slice web mínimo (primer corte equivalente a `2026-06-30-...-cargos`)

Solo Index + Details + Delete, sin Create/Edit/Reactivate, sin toggle de eliminadas. Es la copia textual de la propuesta original de Cargos antes de las dos rondas de follow-ups.

- Pros: cambio más pequeño, riesgo acotado.
- Contras: el usuario tiene que volver más tarde a por create/edit; peor ROI para un módulo que el producto va a usar administrado (Cargos llegó a create/edit/reactivar en menos de una semana de cambios consecutivos).
- Esfuerzo: **Bajo**.

### C. Slice web con paridad + backend gating (autorización + segmentación) en un mismo change

Hace el frontend Y agrega `[Authorize]` + `[Authorize(Roles = RolesSgv.Administrador)]` en `PuestosController`, y un endpoint `GET /api/v1/puestos/consulta?status=...`. Cubre el gap completo pero rompe el principio de "frontend-only" del nombre del change.

- Pros: entrega el módulo cerrado en una sola pasada.
- Contras: duplica el trabajo de tres changes separados (`...-autorizacion-admin`, `...-filtro-activos-eliminados`, `...-frontend`); cambia contrato HTTP/DTOs/dominio, lo que excede el alcance acordado por el orquestador; requiere tests backend (Dominio, Aplicación, Persistencia, Api) que NO son el foco del change.
- Esfuerzo: **Alto**.

## Recomendación

Adoptar **A** (paridad 1:1 frontend-only) **acotado al subconjunto de endpoints que ya existen**:

1. **Listado de activos** vía `GET /api/v1/puestos`. Render con columnas `Código`, `Nombre`, `Unidad organizativa`, `Cargo`. Acciones Detalle / Editar / Eliminar.
2. **Baja lógica** vía `DELETE /api/v1/puestos/{id}`. `OnPostDeleteAsync` con feedback vía `TempData` y PRG.
3. **Reactivación** vía `PATCH /api/v1/puestos/{id}/reactivar` (el endpoint ya existe, aunque el listado no traiga eliminadas: la página de detalle o un banner con CTA rápido lo cubre cuando se conoce el id, igual que `Cargos` con `LastDeletedId`).
4. **Create / Edit / Details** espejados de `Cargos/`, ajustando a que `Codigo` es inmutable en Puesto (Create sí, Edit no).
5. **Toggle "Eliminadas"** se renderiza pero **queda visualmente deshabilitado o redirige a una vista vacía controlada** porque no hay endpoint segmentado. Documentarlo explícito en la propuesta/spec; este sub-punto es la única excepción al "no agregar backend".

El shape de chained PRs (PR 1: seams+shell+navigation; PR 2: listado+delete+reactivate; PR 3: create+edit+details) replica literalmente `2026-06-30-implementar-modulo-de-cargos-en-el-frontend/tasks.md`, lo que da certeza sobre el forecast 400-líneas. Esto NO cierra el gap de backend; queda como follow-up natural (uno o dos cambios de backend análogos a los `2026-07-01-...-admin` y `2026-07-02-...-filtro` ya archivados para Cargos) y se nombra explícitamente en `proposal.md` como **Non-goals**.

Razón para preferir A sobre B: el producto viene de una semana entera de evolución de Cargos para llegar al patrón de paridad. Entregar el módulo de Puestos sin create/edit ni reactivación implicaría volver a abrirlo en pocos días con otro change, mientras que A entrega la UX completa con el mismo costo. Razón para preferir A sobre C: el orquestador marcó el change como frontend-only y el backend está archivado como propio.

## Alcance del TDD (`strict_tdd: true`)

Capas que **aplican** en este change (frontend-only):

| Capa | Tipo de test | Carpeta | Patrón |
|---|---|---|---|
| Web cliente | `PuestosApiClientTests` (HttpClient mockeado) | `tests/SGV.Tests/Web/Puesto/` | Espejo de `CargoApiClientTests.cs` |
| Web contrato | `IPuestosApiClientContractTests` | `tests/SGV.Tests/Web/Puesto/` | Espejo de `ICargoApiClientContractTests.cs` |
| Web seam | `PuestoWebSeamTests` | `tests/SGV.Tests/Web/Puesto/` | Override del fake en `SgvWebApplicationFactory` |
| Web page | `PuestoIndexPageTests`, `PuestoDetailsPageTests`, `PuestoCreatePageTests`, `PuestoEditPageTests` | `tests/SGV.Tests/Web/Puesto/` | `IClassFixture<PuestoWebTestFixture>` + `WebApplicationFactory<Program>` con `FakePuestosApiClient` |
| Asset/script | Harness Node para `puestos-index.js` (SweetAlert2) | Inline o siguiendo patrón del repo | Espejo de `cargos-index.js` |

Capas que **NO aplican** (backend ya cubierto por `2026-06-19-implementa-modulo-puestos`):

- Dominio (`PuestoTests`): ya cubierto.
- Aplicación (`PuestoServicioConsultaTests`, `PuestoServicioComandosTests`, `CrearPuestoRequestValidatorTests`, `ActualizarPuestoRequestValidatorTests`): ya cubierto.
- Persistencia (`PuestoRepositoryTests`): ya cubierto (notar el bug pre-existente #59 en `OcupacionRepositoryTests` no relacionado).
- API (`PuestosControllerTests`): ya cubierto, e incluye `Controller_DoesNotHaveAuthorizeAttribute` que documenta la decisión vigente del backend.

Estructura `apply-progress.md`: tres PRs chained siguiendo `2026-06-30-...-cargos/tasks.md`:

- **PR 1 — Seams + shell**: `IPuestosApiClient`, `PuestosApiClient`, `PuestoListItemViewModel`, `PuestoListQuery`, `PuestoDeleteResult`, registro en `Program.cs`, override en `SgvWebApplicationFactory`, `_Sidenav.cshtml` con nueva entry. Tests RED→GREEN del contrato + seam + sidebar.
- **PR 2 — Listado + baja lógica + reactivación**: `Index.cshtml(.cs)`, `puestos-index.js` con confirmaciones SweetAlert2, harness JS, tests de render activo/eliminado, búsqueda, orden, paginación, POST Delete éxito/409/404, POST Reactivate, preservación de contexto. Si la decisión de propuesta deja el toggle "Eliminadas" deshabilitado por falta de backend, este PR documenta ese comportamiento en spec/test.
- **PR 3 — Create + Edit + Details**: `_Form.cshtml`, `Create.cshtml(.cs)`, `Edit.cshtml(.cs)` (sin `Codigo`), `Details.cshtml(.cs)`, tests de render y POST (éxito + 400 con `FieldErrors` + 409 por código duplicado).

Forecast: ~900 líneas tipo Cargos (PR 1 ~230, PR 2 ~480, PR 3 ~180). Riesgo 400-líneas **High** → **Chained PRs recommended: Yes**.

## Contratos a crear en `SGV.Web/Integration/`

```csharp
public interface IPuestosApiClient
{
    Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PuestoCommandResult> CreateAsync(CrearPuestoRequest request, CancellationToken cancellationToken = default);
    Task<PuestoCommandResult> UpdateAsync(Guid id, ActualizarPuestoRequest request, CancellationToken cancellationToken = default);
    Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record PuestoListItemViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string UnidadOrganizativaNombre,
    string CargoNombre,
    Guid? PuestoSuperiorId);

public sealed record PuestoDeleteResult(bool Succeeded, HttpStatusCode? StatusCode, string? Code, string? Message);
```

`PuestoDto`, `CrearPuestoRequest`, `ActualizarPuestoRequest`, `PuestoCommandResult`, `PuestoError` y `PuestoErrorType` ya viven en `src/SGV.Aplicacion/Organizacion/{Consultas/Dtos,Comandos}` y se referencian directamente desde `SGV.Web` (no se duplican). Los nombres de campos JSON coinciden 1:1 con el contrato backend; no hace falta `JsonPropertyName`.

> Nota: `SGV.Web` ya referencia `SGV.Aplicacion.Organizacion.*` en otros clientes (ver `CargoApiClient.cs`), por lo que esta convención se mantiene.

## Riesgos

1. **Privilegio de escritura sin guard del backend**: `PuestosController` no exige `[Authorize]` ni rol `Administrador`. La UI debe asumir el peor caso y, si en el futuro se agrega `[Authorize(Roles = RolesSgv.Administrador)]`, el cliente `ApiBearerTokenHandler` ya propaga el JWT de la cookie. Mientras tanto, no hay regresión (es un módulo nuevo). Documentar en `proposal.md` como dependencia de un futuro change backend.
2. **Vista de eliminadas sin endpoint segmentado**: como `GET /api/v1/puestos` solo trae activos, el toggle "Eliminadas" quedaría vacío o roto si se renderiza hoy. Mitigación: en `proposal.md` decidimos explícitamente si (a) deshabilitar el toggle visualmente, (b) mostrar el toggle pero sin rows (empty state "Aún no hay backend segmentado"), o (c) ocultar el toggle y diferirlo a un follow-up backend. La recomendación de esta exploración es **(a)** con un mensaje contextual que invite al follow-up.
3. **Transporte / bounded UX**: el cliente nuevo debe usar `Timeout = 10s` para que `HttpRequestException`/`TaskCanceledException` se traduzcan en feedback visible. Reusar el patrón ya implementado en `Program.cs` para `ICargoApiClient` y `IHabilidadApiClient`.
4. **Drift entre keys de `ModelState` y nombres de input del form**: el backend ya emite `FieldErrors` con claves en camelCase (`codigo`, `nombre`, `unidadOrganizativaId`, etc., vía `PuestoServicioComandos.ToCamelCase`). El `_Form.cshtml` debe usar `asp-for` con los nombres correspondientes para que los errores caigan al lado del input correcto. Cubrirlo con tests RED en `PuestoCreatePageTests` y `PuestoEditPageTests`.
5. **`Codigo` inmutable**: a diferencia de Cargos, en Puesto `ActualizarPuestoRequest` **no** incluye `Codigo` (es inmutable por dominio). El `Edit.cshtml` no debe renderizar ese campo; si lo hace, el POST fallaría con `BadRequest` por FluentValidation. Cubrirlo con un test que afirme `Edit.cshtml` no contiene `name="codigo"` en su HTML renderizado.
6. **Conflicto por código en `PATCH /reactivar`**: el endpoint puede responder 409 si al reactivar el `Codigo` ya está ocupado por otro puesto activo. La UI debe reflejarlo en `TempData` con copy específico, igual que el patrón actual de `Cargos`.
7. **`bun run build` y CSS de Inspinia**: el sidebar nuevo no introduce SCSS/CSS propio (reusa clases `side-nav-item`/`side-nav-link`), por lo que el riesgo de romper el bundle es bajo. Validar en PR 1 con `bun install && bun run build`.
8. **Bug pre-existente #59 en `OcupacionRepositoryTests`** (no relacionado con este change pero listado en el `AGENTS.md`): mantener los `MySqlFact` desconectados para que no bloqueen el slice web. Cubrir los nuevos tests con `WebApplicationFactory` + fake client, no con MySQL.

## Preguntas abiertas para la fase de propuesta

1. **¿El listado debe mostrar el "Puesto superior" como columna o solo como link en el detalle?** Hoy `PuestoDto` expone `PuestoSuperiorId` pero no `PuestoSuperiorNombre`. Pediría un join adicional o un segundo endpoint de catálogo para resolver nombres. Para el primer slice, se sugiere mostrar solo el `PuestoSuperiorId` en el detalle y dejar el join para un follow-up.
2. **¿El listado debe ser plano o un árbol jerárquico (Puesto → subordinados)?** El dominio permite self-reference (`PuestoSuperiorId`) y UnidadesOrganizativas ya tiene vista de organigrama con Google OrgChart. ¿Puestos merece la misma vista? Recomendación: **no** en este slice — el producto lo pidió solo para UO, y el listado plano es suficiente para administrar.
3. **¿Debe haber navegación cruzada desde Cargos → Habilidades del Cargo → Puestos del Cargo?** Hoy no existe ese subrecurso. Out of scope; mencionarlo como non-goal.
4. **¿El toggle "Eliminadas" se renderiza deshabilitado, se oculta, o se difiere a un follow-up backend?** Recomendación: **deshabilitado con texto contextual**; alternativa: **diferido** (no se renderiza la entry).
5. **¿Se mantiene la entry `Puestos` en el sidenav como colapsable (igual que Cargos/Habilidades) o plana?** Consistencia visual recomienda colapsable con `Listado` y `Nuevo`.
6. **¿`Puesto.Create` debe permitir elegir `PuestoSuperiorId` desde un select poblado con `GET /api/v1/puestos`?** El catálogo ya existe en el mismo endpoint. Recomendación: **sí**, con filtro opcional por `UnidadOrganizativaId` y `CargoId` para acotar la lista cuando crezca. Decidir si el filtrado requiere un endpoint dedicado o se hace en el cliente.
7. **¿El `Edit` debe permitir cambiar `UnidadOrganizativaId` y `CargoId`?** El `ActualizarPuestoRequest` actual **no** incluye esos campos (solo `Nombre`, `Descripcion`, `PuestoSuperiorId`). Para mover un puesto de UO o Cargo haría falta backend. Decisión recomendada: **fuera de alcance** — mover Puesto entre UO/Cargo se modela como baja + alta, o se delega a un follow-up.

## Listo para propuesta

**Sí**, con los siguientes avisos para que el orquestador los comunique al usuario antes de pasar a `sdd-propose`:

- El slice se ajusta al patrón A (paridad 1:1 frontend-only). Replicará literalmente el ciclo PR 1 / PR 2 / PR 3 de `2026-06-30-implementar-modulo-de-cargos-en-el-frontend`.
- **No** agrega `[Authorize]` ni `[Authorize(Roles = RolesSgv.Administrador)]` a `PuestosController` (queda como follow-up backend, análogo a `2026-07-01-...-cargos-crear-autorizacion-admin`).
- **No** agrega `GET /api/v1/puestos/consulta?status=activas|eliminadas` (queda como follow-up backend, análogo a `2026-07-02-cargos-filtro-activos-eliminados`). El toggle "Eliminadas" se renderiza **deshabilitado** en esta primera entrega.
- **No** promete vista de árbol / organigrama de puestos (la pide el dominio vía `PuestoSuperiorId` pero el producto solo la usó para UO).
- `Codigo` queda **inmutable** en Create/Edit (es una restricción de dominio, no del backend).
- Forecast de tamaño ~900 líneas; 400-líneas risk **High** → **Chained PRs recommended: Yes**. Tres PRs apilados como en `2026-06-30-...-cargos`.

