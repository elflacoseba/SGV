# Tasks — habilidades-navegacion-cargos

> **Estado por work unit** (ver `apply-progress.md` para evidencia detallada):
> - **WU-A — Foundation + API (T1, T2, T3, T4, T8)**: ✅ implementado y verificado — `dotnet build SGV.slnx` exit 0, `dotnet test --filter "FullyQualifiedName!~OcupacionRepositoryTests"` 1386/1386 PASS.
> - **WU-B — Web layer (T5, T6, T7, T9)**: 🔲 pendiente.
> - **WU-C — Verificación (T11)**: 🔲 pendiente del orquestador.

## Resumen

Este artefacto descompone el change `habilidades-navegacion-cargos` en **11 tareas committables** que materializan el espejo readonly **Habilidad → Cargos**, ancladas contra el `design.md` (sección 6) y las tres delta specs (`habilidad-web-listado-detalle-baja`, `habilidad-management`, `skill-cargo-query-contract`). El cambio es de **solo lectura y navegación**: nuevo subrecurso API `GET /api/v1/skills/{skillId}/cargos`, cliente tipado web `HabilidadApiClient.GetCargosAsync`, página Razor readonly `Pages/Organizacion/Habilidades/Cargos` con dos CTAs por fila (`Cargo/Details` siempre, `Cargos/Habilidades` solo `Administrador`), entry point en `Habilidades/Index` activas y tests asociados. NO hay migración de BD, NO hay cambios de dominio y NO se tocan las páginas de detalle (`Habilidades/Details`, `Cargo/Details`) ni el contrato padre de `SkillsController`.

El forecast acumulado supera el budget de 400 líneas porque la superficie toca cuatro capas (Aplicación, Infraestructura/Persistencia, API, Web) y suma dos archivos nuevos de tests sustantivos (controller + PageModel). Eso **dispara la gate `ask-on-risk`** ya configurada en el pre-flight: `sdd-apply` NO debe arrancar hasta que el usuario confirme si prefiere `feature-branch-chain`, `stacked-to-main` o mantiene `single-pr` con `size:exception`. Las unidades de trabajo propuestas están detalladas más abajo como sugerencias; la decisión final la toma el orquestador tras la pregunta al usuario.

La disciplina `strict_tdd: true` del repo se respeta en cada bloque funcional: tests RED se escriben ANTES del markup/código que los cierra en GREEN, salvo en la capa de Aplicación/Infraestructura donde la cobertura se delega a los tests del controller (Task 8) por la ausencia de harness InMemory y por el issue #59 conocido en `[MySqlFact]` (justificación explícita en Task 10).

## Convención de tareas

Cada task del documento usa el siguiente formato:

- **Título**: `### N. <Title>` siguiendo el orden topológico del `design.md` §6.
- **Descripción**: 2-4 oraciones que dicen QUÉ se hace y POR QUÉ, no CÓMO.
- **Acceptance Criteria**: bullets `-able` de chequear (compila, test verde, archivo presente, contrato X, etc.).
- **Archivos**: rutas absolutas desde la raíz del repo, separadas por nueva creación vs. modificación.
- **Spec(s) covered**: referencia al requirement/escenario concreto que la task cierra.
- **Estimación (líneas +/-)**: rango conservador siguiendo la guía del orquestador (≤40 por archivo nuevo simple, 80-150 por PageModel complejo).
- **TDD Cycle Evidence**: placeholder con el ciclo RED→GREEN esperado para `apply` (qué test se escribe antes y qué cambio cierra el ciclo).

## Review Workload Forecast

| Bloque | Tareas | Archivos afectados | Líneas est. | Riesgo budget 400 |
|---|---|---|---:|---|
| A — Contratos de Aplicación | T1, T2 | 4 archivos nuevos | 90-130 | — |
| B — Persistencia + Endpoint API | T3, T4 | 2 archivos nuevos + 1 modificación | 170-220 | — |
| C — Tests del controller | T8 | 1 archivo nuevo | 170-220 | — |
| D — Integración Web | T5, T6, T7 | 1 modificación + 2 archivos nuevos + 1 modificación | 290-355 | — |
| E — Tests Web | T9 | 1 archivo nuevo + 1 modificación | 130-170 | — |
| F — Cobertura repo/servicio | T10 | (omitido por justificación) | 0 | — |
| G — Hardening | T11 | sin archivo productivo | 0 | — |
| **Total** | **11** | **~8 nuevos + ~4 modificados** | **850-1095** | **High** |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

El forecast agregado (~850-1095 líneas, centro ~970) supera el budget de 400 marcado por `review_budget_lines: 400`. **La gate `ask-on-risk` fires** y bloquea `sdd-apply` hasta decisión del usuario. Tres chain strategies posibles quedan abiertas para que el orquestador pregunte:

- **`stacked-to-main`** — PR #1 (Bloques A+B+C, ~430-570 líneas, base `main`) mergea primero; PR #2 (Bloques D+E, ~420-525 líneas, base `main`) mergea después y reusa el subrecurso ya publicado. Mejor para iteración rápida en equipos speed-first.
- **`feature-branch-chain`** — `feature/habilidades-navegacion-cargos` como tracker; PR #1 mergea al tracker, PR #2 mergea al tracker, solo el tracker mergea a `main`. Mejor para control de rollback y release coordinado.
- **`size:exception` / single-pr** — se conserva el PR único con el diff completo y aprobación explícita del maintainer. Mejor cuando se quiere preservar trazabilidad de cambio end-to-end, pero exige justificación formal.

Notas de forecast:
- T6 (Razor Page + PageModel) es el bloque más pesado porque es la primera página readonly de este lado y su PageModel incluye mapping DTO→ViewModel, paginación, toggle y gating admin; se respeta el rango 80-150 del PageModel + 60-100 del markup.
- T8 (controller tests) y T9 (page tests) son los segundos más pesados porque cada uno cubre 8 y 5 escenarios respectivamente siguiendo el patrón del espejo `CargoSkillControllerTests` + `HabilidadesCargosModelTests`.
- T10 se omite por justificación (issue #59 + ausencia de harness InMemory), por eso su línea cuenta 0. Esa omisión NO es scope creep: la cobertura del repositorio y del servicio ya está garantizada transitivamente por T8 (controller de extremo a extremo) y por los asserts de mapping en T6/T9.

## Phase 1 — Contratos de Aplicación (foundation)

### 1. DTO readonly + Query record

- **Descripción**: Crear `SkillCargoDetailDto` (record con constructor primario `(Cargo, Nivel)` y miembros `init` para `CargoId`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria`) y `HabilidadCargosListQuery` (record POJO con `Page`, `PageSize`, `Search`, `Sort`, `HabilidadSegmentoListado Segmento`). El DTO reusa `CargoDto` y `NivelHabilidadDto` ya existentes en `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/` y `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/`. La query reutiliza el enum `HabilidadSegmentoListado` para garantizar consistencia con `SkillsController.GetConsulta`.
- **Acceptance Criteria**:
  - [x] `SkillCargoDetailDto.cs` compila y expone los 9 campos del contrato: `Cargo`, `Nivel`, `CargoId`, `Codigo`, `Nombre`, `NivelId`, `NivelNombre`, `CargoEliminado`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria` (los últimos como `init`).
  - [x] `HabilidadCargosListQuery.cs` compila y es un `sealed record` con los 5 campos mencionados.
  - [x] Tests de compilación (`dotnet build SGV.slnx`) finalizan sin warnings nuevos ni errores.
- **Archivos**:
  - Nuevo: `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/SkillCargoDetailDto.cs`
  - Nuevo: `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadCargosListQuery.cs`
- **Spec(s) covered**: `skill-cargo-query-contract` — "Respuesta paginada y enriquecida del subrecurso" (shape del DTO); `habilidad-management` — "Consultar cargos asociados a una habilidad" (DTO dedicado con `NivelRequeridoId`, `Ponderacion`, `EsObligatoria`).
- **Estimación (líneas +/-)**: 30-40 (dos archivos pequeños).
- **TDD Cycle Evidence**: esta tarea es de contratos, no tiene test unitario propio. Su cobertura viene por la Phase 2 (controller tests T8). El placeholder `apply` debe confirmar `dotnet build` verde ANTES de continuar.

### 2. Servicio de consulta `ISkillCargoServicioConsulta`

- **Descripción**: Crear la interfaz `ISkillCargoServicioConsulta` con un único método `ListarCargosAsync(Guid habilidadId, HabilidadCargosListQuery query, CancellationToken cancellationToken = default)` que devuelve `Task<PagedResult<SkillCargoDetailDto>>`. La implementación `SkillCargoServicioConsulta` recibe por DI el nuevo `ISkillCargoRepository` y **delega sin lógica adicional** (la normalización de `page/pageSize/status` ocurre en el controller, no en el servicio, para mantener el patrón vigente de `SkillsController.GetConsulta`). Namespace: `SGV.Aplicacion.Habilidades.Consultas`.
- **Acceptance Criteria**:
  - [x] Interfaz compila y está en el namespace correcto.
  - [x] Implementación delega 1-a-1 al repositorio sin tocar EF Core.
  - [x] Inyección por constructor con `ArgumentNullException.ThrowIfNull` si las convenciones del repo lo exigen (verificar patrón existente en `HabilidadServicioConsulta`).
- **Archivos**:
  - Nuevo: `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoServicioConsulta.cs`
  - Nuevo: `src/SGV.Aplicacion/Habilidades/Consultas/SkillCargoServicioConsulta.cs`
- **Spec(s) covered**: `habilidad-management` — "Consultar cargos asociados a una habilidad" (servicio que materializa el subrecurso); `skill-cargo-query-contract` — "Alcance acotado" (servicio sin writes).
- **Estimación (líneas +/-)**: 50-70 (interfaz 12-18 + impl 38-52).
- **TDD Cycle Evidence**: contrato, sin test unitario propio. Cobertura transitiva por T8 (controller). `apply` verifica `dotnet build` verde antes de pasar a T3.

## Phase 2 — Persistencia + Endpoint API

### 3. Repositorio EF Core `ISkillCargoRepository` + impl

- **Descripción**: Crear la interfaz `ISkillCargoRepository` con método `ListDetailedBySkillIdAsync(Guid habilidadId, HabilidadCargosListQuery query, CancellationToken cancellationToken = default)` que devuelve `Task<(IReadOnlyList<SkillCargoDetailDto> Items, int TotalCount)>`. La implementación `SkillCargoRepository` en Infraestructura resuelve JOIN sobre `CargoHabilidadEntity` + `CargoEntity` + `HabilidadEntity` + `NivelHabilidadEntity`, aplica `AsNoTracking()`, materializa el segmento vía `Cargo.IsDeleted`, pagina con `Skip/Take` y devuelve el `TotalCount` con `CountAsync` separado. **Gotcha Pomelo crítica**: ordenar sobre `CargoEntity.Codigo` o `Nombre` (campos nativos), NUNCA sobre el DTO proyectado, porque Pomelo no traduce `OrderBy` aplicado a records posicionales. Proyectar al DTO en un `Select` POSTERIOR al `OrderBy`.
- **Acceptance Criteria**:
  - [x] Interfaz hereda de `IReadOnlyRepository<CargoHabilidad>` o sigue la convención existente (verificar firma de `ICargoSkillRepository` antes de aplicar).
  - [x] Implementación usa `AsNoTracking()` en todas las queries.
  - [x] Filtro de segmento aplicado a `Cargo.IsDeleted` (no a `CargoHabilidad`, que no tiene soft-delete).
  - [x] `OrderBy` aplicado a `CargoEntity.Codigo` (default) ANTES de la proyección al DTO.
  - [x] `Skip/Take` aplicados con `pageSize` normalizado a `[1..100]`.
  - [x] `TotalCount` calculado con `CountAsync` independiente de la página solicitada.
- **Archivos**:
  - Nuevo: `src/SGV.Aplicacion/Habilidades/Consultas/ISkillCargoRepository.cs`
  - Nuevo: `src/SGV.Infraestructura/Persistencia/Repositorios/SkillCargoRepository.cs`
- **Spec(s) covered**: `skill-cargo-query-contract` — "Respuesta paginada y enriquecida" (Items + TotalCount), "Query y normalización del segmento" (filtro por `status`); `habilidad-management` — "Consultar cargos asociados a una habilidad" (paginación server-side).
- **Estimación (líneas +/-)**: 100-140 (interfaz 18-26 + impl 82-114 con joins y filtros).
- **TDD Cycle Evidence**: cobertura por T8 (controller end-to-end) y por T10 (omitido con justificación). Si el orquestador decide añadir T10 con InMemory, ese sería el RED; en este forecast queda como T8 → GREEN.

### 4. Endpoint API `SkillsController.GetCargosAsync`

- **Descripción**: Agregar el método `GetCargos` al controller existente `src/SGV.Api/Controllers/SkillsController.cs`, anotado con `[HttpGet("{skillId:guid}/cargos")]`, `[ProducesResponseType(typeof(PagedResult<SkillCargoDetailDto>), 200)]`, `[ProducesResponseType(401)]` y `[ProducesResponseType(404)]`. Inyectar `ISkillCargoServicioConsulta _skillCargoServicio` por constructor junto a `_servicio` y `_comandos`. La implementación: (1) `await _servicio.GetByIdAsync(skillId, ct)` → si `null`, devolver `NotFound()`; (2) normalizar `page` (≥1), `pageSize` (`[1..100]`) y `status` (cae a `activas` salvo que sea literalmente `eliminadas`); (3) construir `HabilidadCargosListQuery`; (4) delegar al servicio y devolver `Ok(result)`. La autorización es la heredada del controller (`[Authorize]` a nivel de clase, sin `[Authorize(Roles=...)]` para esta ruta).
- **Acceptance Criteria**:
  - [x] Ruta `GET /api/v1/skills/{skillId:guid}/cargos` registrada y respondiendo en Swagger.
  - [x] 200 paginado cuando la habilidad existe con cargos.
  - [x] 200 con `Items` vacíos cuando la habilidad existe sin cargos (NO 404).
  - [x] 404 cuando el `skillId` no corresponde a una habilidad existente.
  - [x] 401 cuando falta bearer token (heredado del filtro del controller).
  - [x] `status=archivo` (o cualquier valor inválido) NO devuelve 400; resuelve a `activas` y devuelve 200.
  - [x] Constructor del controller actualizado con la nueva dependencia.
- **Archivos**:
  - Modificación: `src/SGV.Api/Controllers/SkillsController.cs` (método nuevo + inyección de dependencia).
- **Spec(s) covered**: `habilidad-management` — "Consultar cargos asociados a una habilidad" (todos los escenarios), "Autorización de endpoints de habilidades" (401 sin token); `skill-cargo-query-contract` — "Autenticación y distinción entre vacío y recurso inexistente" (401/404/200-vacío), "Query y normalización del segmento" (status inválido).
- **Estimación (líneas +/-)**: 55-75 (método nuevo 40-55 + constructor 5-10 + using/namespace 10).
- **TDD Cycle Evidence**: RED primero en T8 (8 escenarios del controller). `apply` deja el esqueleto del método con `throw new NotImplementedException()` solo si los tests RED lo requieren; en este forecast se recomienda escribir el método completo y los tests en el mismo work unit para evitar una iteración vacía.

### 5. Cliente tipado web `IHabilidadApiClient.GetCargosAsync`

- **Descripción**: Extender `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` con `Task<PagedResult<SkillCargoDetailDto>> GetCargosAsync(Guid skillId, HabilidadCargosListQuery query, CancellationToken cancellationToken = default)` e implementar en `HabilidadApiClient.cs` siguiendo el patrón de `QueryAsync` (líneas 122-133 del espejo): construir `Uri` con `"{BaseRoute}/{skillId}/cargos?page=...&pageSize=...&search=...&sort=...&status=..."` vía `QueryHelpers` o concatenación segura (mantener convención del archivo), invocar `HttpClient.GetAsync`, deserializar `PagedResult<SkillCargoDetailDto>` y mapear códigos de estado (`200` → resultado; `401`/`404` → propagar según convención del call site de la página; `5xx` → excepción que el PageModel trata como fallo de transporte). El `skillId` viaja como **segmento de ruta**, NO como query string.
- **Acceptance Criteria**:
  - Método declarado en la interfaz con la firma exacta.
  - Implementación construye URI con `skillId` en path y `page/pageSize/search/sort/status` en query.
  - Manejo de `401` y `404` consistente con el resto del cliente (verificar política en `CargoApiClient.GetSkillsAsync` líneas 138-158).
  - 5xx lanza excepción que el PageModel reconoce como error de transporte recuperable.
  - Compilación verde.
- **Archivos**:
  - Modificación: `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` (firma nueva).
  - Modificación: `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` (implementación nueva).
- **Spec(s) covered**: `skill-cargo-query-contract` — "Alcance acotado" (consumidor web del subrecurso), "Autenticación y distinción entre vacío y recurso inexistente" (cliente propaga 401/404).
- **Estimación (líneas +/-)**: 40-55 (interfaz 10-14 + impl 30-41).
- **TDD Cycle Evidence**: cobertura indirecta por T9 (tests del PageModel). El cliente se prueba a través del PageModel con `SgvWebApplicationFactory`.

## Phase 3 — Razor Page readonly

### 6. Razor Page `Pages/Organizacion/Habilidades/Cargos.cshtml` + PageModel

- **Descripción**: Crear el archivo `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml` con ruta `@page "/organizacion/habilidades/{id:guid}/cargos"` (espejo de `Cargos/Habilidades.cshtml`). Crear el PageModel `HabilidadesCargosModel` con propiedades bindeables `[BindProperty(SupportsGet = true)]` para `Id`, `Page`, `PageSize`, `Search`, `Sort`, `Status`; propiedades de presentación `Items` (`IReadOnlyList<HabilidadCargoListItemViewModel>`), `TotalCount`, `CurrentPage`, `TotalPages`, `EsAdministrador`, `IsDeletedView`, `HabilidadNombre`. Handler `OnGetAsync(CancellationToken cancellationToken)` con la siguiente secuencia: (1) `await _servicio.GetByIdAsync(Id, ct)` para validar la habilidad padre — si `null`, devolver `NotFound()` o redirect a `/Organizacion/Habilidades/Index` (elegir convención revisando `Habilidades/Details.cshtml.cs:41-61` y `Cargos/Details.cshtml.cs`); (2) mapear `Status` a `HabilidadSegmentoListado` con fallback a `Activas`; (3) invocar `HabilidadApiClient.GetCargosAsync(Id, query, ct)`; (4) mapear DTO → ViewModel preservando `NivelRequeridoId`, `Ponderacion`, `EsObligatoria`; (5) calcular `TotalPages = ceil(TotalCount / PageSize)` y `CurrentPage`. El markup incluye: header con nombre de habilidad + breadcrumb "Habilidades / {nombre}", toggle `Activas|Eliminadas` (helper `BuildToggleSegmentoRouteValues` copiado de `Habilidades/Index.cshtml.cs:96-114`), tabla con columnas `Código`, `Nombre`, `Nivel`, `Acciones`, dos botones por fila (`ti ti-eye` info → `Cargo/Details` SIEMPRE; `ti ti-edit` warning → `Cargos/Habilidades` SOLO si `Model.EsAdministrador`), estado vacío con mensaje "No hay cargos asociados en el segmento X." y paginación preservando `Search`, `Sort`, `Status`.
- **Acceptance Criteria**:
  - Página renderiza tabla con `Items`, `TotalCount` y `TotalPages` cuando hay datos.
  - Paginación funciona: cambiar `page` recarga y conserva `search/sort/status`.
  - Toggle Activas/Eliminadas alterna segmento y refleja resultados.
  - Botón `Cargo/Details` siempre visible para cualquier fila.
  - Botón `Cargos/Habilidades` aparece SOLO si `Model.EsAdministrador`.
  - Si la habilidad no existe (`GetByIdAsync` devuelve null) → `NotFound()` o redirect a Index (convención a confirmar antes de aplicar).
  - Estado vacío renderiza mensaje claro sin botón roto.
  - PageModel declara `EsAdministrador` con `User.IsInRole(RolesSgv.Administrador)` (helper ya existente).
  - `using SGV.Web.Integration.Habilidades;` para el `SkillCargoDetailDto` y para `HabilidadCargosListQuery`.
- **Archivos**:
  - Nuevo: `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml`
  - Nuevo: `src/SGV.Web/Pages/Organizacion/Habilidades/Cargos.cshtml.cs`
- **Spec(s) covered**: `habilidad-web-listado-detalle-baja` — escenarios del cambio (la página NO está nombrada en esa spec, pero la cubre indirectamente porque su entry point vive en Index); `habilidad-management` — "Consultar cargos asociados a una habilidad" (consumidor web); `skill-cargo-query-contract` — "Alcance acotado" (la página no abre writes).
- **Estimación (líneas +/-)**: 200-280 (cshtml 80-110 + cshtml.cs 120-170 con mapping, paginación y gating).
- **TDD Cycle Evidence**: cobertura por T9 (PageModel tests + tests de markup). `apply` puede escribir el PageModel junto con un test RED inicial que verifique `Items.Length == 0` cuando el subrecurso devuelve vacío.

### 7. Entry point en `Habilidades/Index` (botón `Cargos` solo en activas)

- **Descripción**: En `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs`, agregar el helper público `BuildCargosRouteValues(Guid id) => new RouteValueDictionary { ["id"] = id, ["p"] = CurrentPage, ["search"] = Search, ["sort"] = Sort, ["status"] = Segmento }`. En `Index.cshtml`, insertar un nuevo botón `btn btn-primary btn-icon btn-sm rounded-circle` con ícono `ti ti-briefcase` (o el icono que decida producto) entre los existentes `Detalle` y `Editar`, dentro del `div.d-flex.justify-content-center.gap-1`, con `aria-label="Cargos de {Nombre}"`, `data-bs-toggle="tooltip"`, `data-bs-title="Cargos"` y `href` construido con `@Url.Page("/Organizacion/Habilidades/Cargos", Model.BuildCargosRouteValues(item.Id))`. Renderizar SOLO cuando `!Model.IsDeletedView`. NO tocar `Habilidades/Details.cshtml` (fuera de alcance explícito del change).
- **Acceptance Criteria**:
  - Helper `BuildCargosRouteValues` existe en `Index.cshtml.cs` y devuelve `RouteValueDictionary` con los 5 campos preservados.
  - Botón `Cargos` aparece en `Index.cshtml` activo, entre `Detalle` y `Editar`.
  - Botón `Cargos` NO aparece en `Index.cshtml` cuando `Model.IsDeletedView == true` (esos solo muestran `Reactivar`).
  - `href` apunta a `/organizacion/habilidades/{id}/cargos` con `p`, `search`, `sort`, `status` preservados.
  - `aria-label` y `data-bs-title` presentes.
  - `Habilidades/Details.cshtml` NO se modifica (verificar con `git diff` antes de cerrar la tarea).
- **Archivos**:
  - Modificación: `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` (botón nuevo).
  - Modificación: `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` (helper nuevo).
- **Spec(s) covered**: `habilidad-web-listado-detalle-baja` — todos los MODIFIED scenarios (vista activas muestra `Cargos`, navegación preserva contexto, vista eliminadas solo `Reactivar`).
- **Estimación (líneas +/-)**: 20-28 (markup 10-14 + helper 10-14).
- **TDD Cycle Evidence**: RED primero en T9 (extensión de `HabilidadesIndexPageTests` con dos escenarios: activo expone, eliminadas oculta). `apply` cierra GREEN con el markup y el helper.

## Phase 4 — Tests

### 8. Tests del controller `HabilidadesCargosControllerTests`

- **Descripción**: Crear `tests/SGV.Tests/Api/HabilidadesCargosControllerTests.cs` con 8 escenarios cubiertos por 8 `[Fact]` (o combinación de `[Theory]` cuando aplique) usando `WebApplicationFactory<Program>` + bearer token via `AddBearerToken()`:
  1. `GetCargos_HabilidadExistente_ConCargos_DevuelvePagedResultConItems` — `200`, `Items.Length == 3`, `TotalCount == 3`, `Page == 1`, `PageSize == 20`, cada item con `Cargo`, `Nivel`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria`.
  2. `GetCargos_HabilidadExistente_SinCargos_DevuelveColeccionVacia` — `200`, `Items.Length == 0`, `TotalCount == 0` (NO `404`).
  3. `GetCargos_SkillIdInexistente_Devuelve404` — `Guid.NewGuid()` → `404`.
  4. `GetCargos_SinBearerToken_Devuelve401` — sin `AddBearerToken()` → `401`.
  5. `GetCargos_StatusInvalido_CaeAActivas` — `?status=archivo` → `200` con datos de activas (NO `400`).
  6. `GetCargos_Paginacion_DevuelveItemsCorrectos` — 12 cargos totales, `pageSize=5`, `page=2` → 5 items, `TotalCount == 12`, `Page == 2`.
  7. `GetCargos_SortPorCodigoDesc_OrdenaResultados` — `?sort=codigo_desc` → primer item con `Codigo` mayor al segundo.
  8. `GetCargos_FiltroEliminadas_DevuelveSoloCargosEliminados` — cargo soft-deleted + cargo activo asociados → `?status=eliminadas` devuelve solo el soft-deleted.
- **Acceptance Criteria**:
  - [x] 8 escenarios implementados como `[Fact]` (o `[Theory]` con `[InlineData]` cuando los datos lo permitan).
  - [x] Todos los escenarios verdes contra `dotnet test`.
  - [x] Ningún test usa `[MySqlFact]` (ver justificación T10).
  - [x] El test factory (`ApiWebApplicationFactory`) se reutiliza si es posible; si requiere seed adicional, documentar el helper de seed.
- **Archivos**:
  - Nuevo: `tests/SGV.Tests/Api/HabilidadesCargosControllerTests.cs`
- **Spec(s) covered**: `habilidad-management` — los 3 escenarios ADDED del subrecurso + 2 escenarios de autorización; `skill-cargo-query-contract` — los 4 requisitos (paginado, segmento, auth, alcance).
- **Estimación (líneas +/-)**: 170-220 (8 escenarios con setup compartido, factory y asserts específicos).
- **TDD Cycle Evidence**: estos tests SON el RED de T4 (controller). `apply` los escribe PRIMERO, observa rojo por `NotFound()`/excepción del método nuevo, luego implementa T4 para llegar a GREEN.

### 9. Tests Web — PageModel + extensión Index

- **Descripción**: Crear `tests/SGV.Tests/Web/Habilidad/HabilidadesCargosModelTests.cs` con 3-4 escenarios usando `SgvWebApplicationFactory`:
  1. `OnGetAsync_HabilidadExistente_ConCargos_RenderizaTablaConItems` — verifica `Items.Length == 2`, `TotalPages == 1`, `EsAdministrador` consistente con el usuario del test.
  2. `OnGetAsync_HabilidadInexistente_DevuelveNotFoundORedirect` — verificar el comportamiento decidido en T6.
  3. `OnGetAsync_StatusInvalido_ResuelveAActivas` — `?status=archivo` → page model normaliza a `Activas`.

  Adicionalmente, extender `tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPageTests.cs` con 2 escenarios:
  4. `Index_ActiveRow_ExposesCargosButton` — el HTML renderizado contiene el `<a>` hacia `/organizacion/habilidades/{id}/cargos`.
  5. `Index_DeletedRow_HidesCargosButton` — el HTML renderizado NO contiene ese `<a>`.

  Para los escenarios 4 y 5, se prefiere 1 `[Theory]` con `[InlineData(true, false)]`/`[InlineData(false, true)]` si el harness lo permite; sino, 2 `[Fact]` separados.
- **Acceptance Criteria**:
  - 3 escenarios del PageModel verdes.
  - 2 escenarios del Index verdes (uno presencia, uno ausencia).
  - Tests usan `SgvWebApplicationFactory` con bearer token para autenticarse como admin o como usuario estándar según corresponda.
  - `EsAdministrador` mapeado correctamente para los 3 roles: `Administrador=true`, usuario-no-admin=false, anónimo-fuera-de-alcance.
- **Archivos**:
  - Nuevo: `tests/SGV.Tests/Web/Habilidad/HabilidadesCargosModelTests.cs`
  - Modificación: `tests/SGV.Tests/Web/Habilidad/HabilidadesIndexPageTests.cs`
- **Spec(s) covered**: `habilidad-web-listado-detalle-baja` — "Vista activas muestra acciones del catálogo activo", "Vista eliminadas muestra solo reactivación" (cubierto por T9.4 y T9.5); `skill-cargo-query-contract` — "Status inválido cae a activas" (cubierto por T9.3).
- **Estimación (líneas +/-)**: 130-170 (PageModel 100-130 + Index 30-40).
- **TDD Cycle Evidence**: tests RED de T6 (PageModel) y T7 (Index). `apply` los escribe ANTES de la implementación de markup/PageModel.

### 10. Tests de repositorio/servicio — OMITIDO con justificación

- **Descripción**: Esta tarea **NO se implementa** por las siguientes razones técnicas documentadas en `exploration.md` y en `AGENTS.md`:
  1. El repositorio de tests del proyecto **NO usa `UseInMemoryDatabase`** en ningún archivo (búsqueda `UseInMemoryDatabase|InMemory` en `tests/SGV.Tests/**/*.cs` devuelve cero coincidencias); el único harness disponible para persistencia es `[MySqlFact]`.
  2. `[MySqlFact]` está **caído por el issue #59** (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`), documentado en `AGENTS.md` sección "Tests de Integración con MySQL". Usar ese atributo para `SkillCargoRepository` expondría el cambio al mismo bug latente.
  3. La cobertura del repositorio y del servicio ya está garantizada **transitiva y completamente** por los 8 escenarios end-to-end del controller (T8), que ejercitan EF Core real, la normalización de `page/pageSize/status`, el filtro de segmento y la paginación server-side.
  4. La cobertura de la lógica del servicio (que es cero por diseño — solo delega) no aporta valor respecto a lo ya cubierto por T8.
- **Acceptance Criteria**:
  - La tarea queda cerrada con la justificación explícita registrada en este documento.
  - `apply` la marca como **omitida** y NO crea archivos de test de repo/servicio en este slice.
  - Si en el futuro se cierra el issue #59 o se introduce un harness InMemory maduro, esta tarea se reactiva como mejora opcional.
- **Archivos**: ninguno.
- **Spec(s) covered**: ninguno directamente; la cobertura es transitiva.
- **Estimación (líneas +/-)**: 0.
- **TDD Cycle Evidence**: N/A — la omisión es justificada y la cobertura es por T8.

### 11. Hardening y verificación final

- **Descripción**: Ejecutar la cadena de verificación completa para sellar la PR. Comandos en orden estricto:
  1. `dotnet restore SGV.slnx` — restaurar dependencias.
  2. `dotnet build SGV.slnx` — compilación sin warnings nuevos ni errores.
  3. `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` — suite completa exceptuando los tests caídos por issue #59 (no introducidos por este change). Verificar 100% verde en: `HabilidadesCargosControllerTests`, `HabilidadesCargosModelTests`, `HabilidadesIndexPageTests` extendidos y suite previa intacta.
  4. `cd src/SGV.Web && bun install` — instalar dependencias frontend si no están.
  5. `bun run build` dentro de `src/SGV.Web` — assets frontend de Inspinia/Gulp compilables.
  6. `git diff --stat` para confirmar que el diff acumulado está dentro del budget esperado (850-1095 líneas). Si supera 1200, abrir ticket de seguimiento.
- **Acceptance Criteria**:
  - `dotnet build SGV.slnx` finaliza sin warnings ni errores nuevos.
  - `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` queda 100% verde.
  - `bun install` y `bun run build` en `src/SGV.Web` finalizan sin errores.
  - `git diff --stat` muestra archivos esperados (los listados arriba) y solo esos.
  - Ningún archivo de los explícitamente fuera de alcance (`Habilidades/Details.cshtml*`, `Cargo/Details.cshtml*`, `Cargos/Habilidades.cshtml*`, scripts de migración, `CargoHabilidadConfiguracion.cs`) aparece modificado.
- **Archivos**: sin cambios de fuente; solo ejecución de comandos y reporte en `apply-progress.md`.
- **Spec(s) covered**: success criteria global del change (`proposal.md` §success_criteria).
- **Estimación (líneas +/-)**: 0 (comandos y verificación).
- **TDD Cycle Evidence**: N/A — esta tarea es la guardia final del ciclo RED→GREEN y del presupuesto de review.

## Mapa spec → tasks

| Spec / escenario | Task que lo cubre |
|---|---|
| `habilidad-web-listado-detalle-baja` — Vista activas muestra acciones del catálogo activo | T7 |
| `habilidad-web-listado-detalle-baja` — Navegación a cargos preserva contexto | T7 |
| `habilidad-web-listado-detalle-baja` — Vista eliminadas muestra solo reactivación | T7 |
| `habilidad-management` — Habilidad existente devuelve colección paginada | T4, T8 |
| `habilidad-management` — Habilidad existente sin cargos devuelve vacío | T4, T8 |
| `habilidad-management` — Habilidad inexistente devuelve no encontrado | T4, T8 |
| `habilidad-management` — Operaciones write no disponibles | T4 (controller no expone writes) |
| `habilidad-management` — Lecturas autenticadas exitosas | T4, T8 |
| `habilidad-management` — Acceso anónimo rechazado | T4, T8 |
| `habilidad-management` — Mutación protegida por rol administrador | (sin cambios — preservado) |
| `skill-cargo-query-contract` — Respuesta paginada y enriquecida | T1, T4, T8 |
| `skill-cargo-query-contract` — Colección vacía sin cambiar el shape | T4, T8 |
| `skill-cargo-query-contract` — Status inválido cae a activas | T4, T8 |
| `skill-cargo-query-contract` — Acceso sin token es rechazado | T4, T8 |
| `skill-cargo-query-contract` — Habilidad inexistente devuelve 404 | T4, T8 |
| `skill-cargo-query-contract` — No contaminar contratos padre ni abrir writes | T4, T6 |

## Work Units (commits sugeridos, strict TDD RED → GREEN cuando aplica)

> Dependiente de la chain strategy que el usuario elija tras la gate `ask-on-risk`. Estos son los work units base que se mapean 1-a-1 a los PR propuestos en el forecast.

| Work unit | Tasks | Commits sugeridos | PR sugerido |
|---|---|---|---|
| WU-A — Foundation + API | T1, T2, T3, T4, T8 | `feat(api): skill-cargos detail DTO and query record` → `feat(api): skill cargo query service` → `feat(infra): skill cargo repository with ordering-before-projection gotcha` → `test(api): habilidades cargos controller 8 scenarios` (RED) → `feat(api): skills controller get cargos endpoint` (GREEN) → `chore(api): inject skill cargo query service into skills controller` | **PR #1** |
| WU-B — Web layer | T5, T6, T7, T9 | `feat(web): habilidad api client get cargos async` → `test(web): habilidades cargos page model scenarios` (RED) → `feat(web): habilidades cargos readonly page and page model` (GREEN) → `test(web): habilidades index exposes cargos button on active rows` (RED) → `feat(web): habilidades index build cargos route values helper and button` (GREEN) → `test(web): habilidades index hides cargos button on deleted rows` (GREEN) | **PR #2** |
| WU-C — Verificación | T10 (omitido justificado), T11 | `chore(verify): omit repo tests with justification and run full verification chain` | **PR #1** o **PR #2** según chain strategy |

Mapeo a chain strategies posibles:
- `stacked-to-main`: PR #1 (WU-A + WU-C) mergea a `main` primero; PR #2 (WU-B) mergea a `main` después. Reusa el subrecurso ya publicado.
- `feature-branch-chain`: `feature/habilidades-navegacion-cargos` como tracker; PR #1 (WU-A) mergea al tracker; PR #2 (WU-B) mergea al tracker; el tracker mergea a `main` con un PR #3 final de cleanup (T11).
- `size:exception` / single-pr: todos los WU en una sola PR con aprobación explícita.

## Riesgos por tarea y mitigación

| Tarea | Riesgo | Mitigación |
|---|---|---|
| T1 | Olvidar algún campo del DTO que la spec exige (`NivelId`, `NivelNombre`, `CargoEliminado`) | Lista de 9 campos en el acceptance criteria + cross-check con spec `skill-cargo-query-contract` antes de mergear. |
| T2 | Acoplar lógica de negocio en el servicio y romper el patrón vigente | El servicio solo delega; cualquier validación debe vivir en el controller o en el repo. |
| T3 | Aplicar `OrderBy` sobre `SkillCargoDetailDto` (record posicional) y que Pomelo no traduzca | Regla explícita en acceptance criteria: `OrderBy` sobre `CargoEntity.Codigo`/`Nombre`, `Select` posterior. Test de sort del controller (T8.7) verifica end-to-end. |
| T3 | Reusar `ICargoSkillRepository` directamente sin crear la abstracción simétrica | Aunque tentador, rompe simetría y oculta el bug Pomelo detrás de una firma que ya está afectada. Crear `ISkillCargoRepository` propio. |
| T4 | Olvidar la inyección del nuevo servicio en el constructor y romper compilación | Acceptance criteria lo verifica; `dotnet build` lo detecta. |
| T4 | Devolver `404` cuando la lista viene vacía (en lugar de `200` con `Items` vacíos) | Spec lo exige explícitamente; tests T8.2 y T8.3 verifican la distinción. |
| T5 | Confundir `skillId` como query string en lugar de segmento de ruta | Acceptance criteria explícito: el `skillId` va en path, los demás en query. |
| T6 | Reutilizar `CargoDto` a secas y perder `NivelRequeridoId`/`Ponderacion`/`EsObligatoria` | T1 define el DTO dedicado; el PageModel mapea explícitamente esos campos. |
| T6 | Renderizar el botón `Cargos/Habilidades` para usuarios no admin y producir navegación a `403` | Helper `EsAdministrador` con `User.IsInRole(RolesSgv.Administrador)` ya vigente en el repo; gating en la vista con `@if (Model.EsAdministrador)`. |
| T7 | Tocar `Habilidades/Details.cshtml` por simetría con el espejo `Cargos/Details` | La spec lo prohíbe y el change lo declara explícitamente fuera de alcance. Acceptance criteria incluye "NO se modifica Details" + verificación con `git diff`. |
| T7 | Olvidar preservar `p/search/sort/status` en el `href` | Helper `BuildCargosRouteValues` lo centraliza; test T9.4 valida el contenido del href. |
| T8 | Usar `[MySqlFact]` y arrastrar el issue #59 a esta superficie | Acceptance criteria prohíbe `[MySqlFact]` para estos tests; el seed se hace vía `WebApplicationFactory` + `AddBearerToken()` con `TestSgvDbContextFactory` si hace falta. |
| T9 | Confundir PageModel con Page y romper `OnGetAsync` al refactorizar | Tests RED primero; GREEN al consolidar markup. Si una iteración falla, regresión clara al test anterior. |
| T10 | Presionar para añadir tests de repo "por completitud" y arrastrar el issue #59 | Justificación registrada explícitamente; cobertura ya viene por T8. Si el usuario objeta, este es el punto a renegociar. |
| T11 | Olvidar el filtro `--filter "FullyQualifiedName!~OcupacionRepositoryTests"` y reportar rojo falso | Comando exacto documentado; mismo filtro que el orquestador pasó como locked. |

## Próximo paso sugerido

La gate `ask-on-risk` está disparada (`Decision needed before apply: Yes`). El orquestador debe **preguntar al usuario** qué chain strategy prefiere entre las tres opciones listadas en el forecast (`stacked-to-main`, `feature-branch-chain`, `size:exception`/single-pr) antes de lanzar `sdd-apply`. Una vez confirmada la estrategia, `sdd-apply` puede arrancar con el work unit WU-A (PR #1), siguiendo los commits RED→GREEN documentados arriba. WU-B (PR #2) se ejecuta solo después de que PR #1 mergee (en `stacked-to-main`) o se acumule en el tracker (en `feature-branch-chain`).

## Result Contract

- **status**: success
- **executive_summary**: `tasks.md` descompone el change `habilidades-navegacion-cargos` en 11 tareas agrupadas en cuatro fases (contratos de Aplicación, persistencia + API, Razor Page, tests + hardening). Forecast acumulado ~850-1095 líneas (centro ~970), supera el budget de 400 → **gate `ask-on-risk` fires** y bloquea `sdd-apply` hasta que el usuario elija chain strategy. Tres work units propuestos (WU-A Foundation+API, WU-B Web layer, WU-C Verificación) mapeables a chained PRs. Disciplina `strict_tdd: true` respetada: T10 (tests de repo) omitido con justificación por issue #59 + ausencia de InMemory; cobertura end-to-end garantizada por T8 (8 escenarios del controller) + T9 (3+2 escenarios web).
- **artifacts**:
  - `openspec/changes/habilidades-navegacion-cargos/tasks.md`
- **next_recommended**: ask-on-risk → luego `sdd-apply` (solo si el usuario confirma chain strategy)
- **risks**:
  - `ask-on-risk` no resuelto: `sdd-apply` no debe arrancar hasta que el usuario elija `stacked-to-main` / `feature-branch-chain` / `size:exception`.
  - Drift en el DTO: si T1 omite algún campo de los 9 que exige la spec `skill-cargo-query-contract`, el controller y la página quedan inconsistentes.
  - Gotcha Pomelo en T3: si el `OrderBy` se aplica sobre `SkillCargoDetailDto` en lugar de `CargoEntity`, EF Core tira `InvalidOperationException` en runtime.
  - Tocar `Habilidades/Details.cshtml` por simetría con `Cargos/Details`: explícitamente fuera de alcance; `git diff` lo detecta pero requiere disciplina del implementador.
  - `[MySqlFact]` en T8 arrastraría el issue #59 a esta superficie; acceptance criteria lo prohíbe.
- **skill_resolution**: paths-injected — `sdd-tasks`, `Razor Pages Patterns`, `dotnet-best-practices`, `dotnet-xunit`
- **task_summary**:
  - **total**: 11
  - **completed**: 0
  - **pending**: 11
  - **omitted_with_justification**: 1 (T10)
  - **allComplete**: false
- **forecast_summary**:
  - **estimated_lines**: 850-1095 (centro ~970)
  - **budget_lines**: 400
  - **risk**: High
  - **decision_needed_before_apply**: Yes
  - **chained_prs_recommended**: Yes
  - **chain_strategy**: pending