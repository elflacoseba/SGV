# Tasks — Implementar asignar/quitar Habilidades de un Cargo

## Review Workload Forecast

| PR | Tareas | Líneas est. | Riesgo budget 400 | Chained PRs | Decisión previa |
|---|---|---|---|---|---|
| **PR1** — Aplicación | T1.1 - T1.5 | ~250-360 | bajo | no | no |
| **PR2** — Infraestructura + API | T2.1 - T2.4 | ~250-380 | bajo-medio | no | no |
| **PR3a** — Cliente web tipado | T3.1 - T3.3 | ~180-260 | bajo | sí (parte de split) | no |
| **PR3b** — Razor Page + tests web | T3.4 - T3.7 | ~330-470 | **alto** | sí | **sí** |
| **Total** | 15 tareas | ~1010-1470 | — | **recomendado** | **sí** |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending (`stacked-to-main` | `feature-branch-chain` | `size:exception` para PR3b — el orquestador debe preguntar)
400-line budget risk: High (concentrado en PR3b)

> **Forecast**: PR1 y PR2 entran en presupuesto. PR3b dispara el límite porque combina PageModel nuevo, markup editable, anti-drift y suite web completa. Se **recomienda splitear PR3 en PR3a (cliente) + PR3b (page)** para mantener cada PR revisable en ~60 min.

---

## PR1 — Contratos, validator, servicio y tests de aplicación

### T1.1 — Extender DTOs y request
- **Capa**: Aplicación
- **Archivos**: `CargoSkillRequests.cs` (agregar `Ponderacion?`, `EsObligatoria?`, renombrar `NivelId` → `NivelRequeridoId`); `CargoSkillDto.cs` (`SkillId`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria`); `CargoSkillDetailDto.cs` (mismos cuatro + `Skill` + `Nivel`).
- **Criterios**: `cargo-skill-query-contract` Req 1-2; `cargo-skill-ponderacion-obligatoria` Req 3; campo `nivelRequeridoId` SIN alias `nivelId`.
- **Dependencias**: —
- **Líneas est.**: ~50-80
- **Strict TDD**: extender `CargoSkillServicioTests.cs` con assertions de nuevos campos antes de mover el servicio.

### T1.2 — Crear `AsignarCargoSkillRequestValidator`
- **Capa**: Aplicación (FluentValidation)
- **Archivos**: nuevo `Validaciones/AsignarCargoSkillRequestValidator.cs`. Reglas: `NivelRequeridoId != Guid.Empty`, `Ponderacion > 0`, `Ponderacion <= 100.00`, máx 2 decimales.
- **Criterios**: `cargo-skill-ponderacion-obligatoria` Req 2 y 4; `cargo-skill-asignar-editar` Req 4.
- **Dependencias**: T1.1
- **Líneas est.**: ~35-55
- **Strict TDD**: nuevo `AsignarCargoSkillRequestValidatorTests.cs` cubriendo `Ponderacion=0`, `=100.001`, `=-1`, `NivelRequeridoId=Guid.Empty`, defaults sin valor.

### T1.3 — Extender `CargoSkillServicio.UpsertAsync` con defaults y validator
- **Capa**: Aplicación
- **Archivos**: `CargoSkillServicio.cs` (inyectar `IValidator<AsignarCargoSkillRequest>`, validar antes de consultar repos, defaults `Ponderacion=1.00m` y `EsObligatoria=false`); `CargoSkillCommandResult.cs` (agregar `FieldErrors` opcional + overload `Failure(error, fieldErrors)`).
- **Criterios**: `cargo-skill-ponderacion-obligatoria` Req 1; `cargo-skill-asignar-editar` Req 1 escenario "Actualización idempotente".
- **Dependencias**: T1.1, T1.2
- **Líneas est.**: ~50-80
- **Strict TDD**: tests nuevos en `CargoSkillServicioTests`: defaults cuando request omite campos, `Ponderacion` inválida → `FieldErrors` y `SaveChangesCount==0`.

### T1.4 — Validar replace idempotente con campos del vínculo
- **Capa**: Aplicación (test-first)
- **Archivos**: `CargoSkillServicioTests.cs`.
- **Criterios**: `cargo-skill-asignar-editar` Req 1 escenario idempotente.
- **Dependencias**: T1.3
- **Líneas est.**: ~40-60

### T1.5 — Validar `ListAsync` con DTO enriquecido
- **Capa**: Aplicación
- **Archivos**: `CargoSkillServicioTests.cs` + `FakeCargoSkillRepository.ListDetailedByCargoIdAsync` (en `TestFakes` o local).
- **Criterios**: `cargo-skill-query-contract` Req 1 y 2.
- **Dependencias**: T1.3
- **Líneas est.**: ~30-50

### Verificación al cerrar PR1
- `dotnet build SGV.slnx` limpio.
- `dotnet test tests/SGV.Tests --filter "FullyQualifiedName~CargoSkill"` verde.
- CI MySQL no aplica (no toca persistencia).

---

## PR2 — Repositorio, controller, API y Swagger

### T2.1 — Enriquecer proyección de `CargoSkillRepository`
- **Capa**: Infraestructura
- **Archivos**: `CargoSkillRepository.cs` (extender `ListDetailedByCargoIdAsync` con `SkillId`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria` en una sola query sin N+1).
- **Criterios**: `cargo-skill-query-contract` Req 1 y 4.
- **Dependencias**: PR1 mergeado
- **Líneas est.**: ~30-50
- **Strict TDD**: `[MySqlFact]` en `CargoSkillRepositoryTests`: upsert con `Ponderacion=2.50` y `EsObligatoria=true`, lectura posterior devuelve esos valores en el detalle.

### T2.2 — Bifurcar errores de validación en `CargosController`
- **Capa**: API
- **Archivos**: `CargosController.cs` (refactor `ToSkillProblemResult` → `ValidationProblemDetails` cuando `FieldErrors.Count > 0`; mantener `Problem(...)` cuando no).
- **Criterios**: `cargo-skill-asignar-editar` Req 3 escenario "Nivel requerido inexistente"; `cargo-skill-ponderacion-obligatoria` Req 4.
- **Dependencias**: T2.1
- **Líneas est.**: ~30-50
- **Strict TDD**: `CargoSkillControllerTests`: `UpsertSkill_FieldErrors_ReturnsValidationProblemDetails`, `UpsertSkill_PonderacionExcede100_Returns400ConCampoPonderacion`.

### T2.3 — Exponer `nivelRequeridoId` sin alias y documentar schema
- **Capa**: API
- **Archivos**: `CargosController.cs` (actualizar `ProducesResponseType` y comentarios `<response>` del GET; asegurar shape sin `nivelId` legado).
- **Criterios**: `cargo-skill-query-contract` Req 1, 3 y nota final.
- **Dependencias**: T2.1
- **Líneas est.**: ~25-40
- **Strict TDD**: `CargoSkillControllerTests` + `SwaggerConfigurationTests`: aserción de `nivelRequeridoId`, `ponderacion`, `esObligatoria`, `skill`, `nivel` en el schema del subrecurso; ausencia de `nivelId`.

### T2.4 — Anti-regresión de shape en `Cargo` padre
- **Capa**: API (test-only)
- **Archivos**: extender `CargosControllerTests.cs` o `SwaggerConfigurationTests.cs`.
- **Criterios**: `cargo-skill-query-contract` Req 3 escenario "No contaminar el contrato padre".
- **Dependencias**: T2.3
- **Líneas est.**: ~25-40

### Verificación al cerrar PR2
- `dotnet build SGV.slnx` limpio.
- `dotnet test tests/SGV.Tests --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~Swagger"` verde.
- Suite `[MySqlFact]` corre si MySQL local está disponible; se skipea limpio si no.
- `dotnet test tests/SGV.Tests/Api` completo verde.

---

## PR3a — Cliente web tipado para el subrecurso

### T3.1 — Extender `ICargoApiClient`
- **Capa**: Web (integration)
- **Archivos**: `ICargoApiClient.cs` (sumar `GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`).
- **Criterios**: precondiciones de `cargo-skill-ui-tabla-editable`.
- **Dependencias**: PR2 mergeado
- **Líneas est.**: ~30-50

### T3.2 — Implementar `CargoApiClient` para el subrecurso
- **Capa**: Web (integration)
- **Archivos**: `CargoApiClient.cs` (`GET /api/v1/cargos/{cargoId}/skills`, `PUT /.../{skillId}`, `DELETE /.../{skillId}`; traducir `ValidationProblemDetails` a `CargoSkillCommandResult.Failure(error, fieldErrors)` consistente con `UpdateAsync`).
- **Criterios**: `cargo-skill-ponderacion-obligatoria` Req 4; `cargo-skill-ui-tabla-editable` Req 5.
- **Dependencias**: T3.1
- **Líneas est.**: ~80-120

### T3.3 — Tests del cliente y `FakeCargoApiClient` extendido
- **Capa**: Tests
- **Archivos**: `CargoApiClientTests.cs` (200/204/400 con FieldErrors/404/transport); `FakeCargoApiClient.cs` (sumar `SkillUpsertResult/Calls`, `SkillDeleteResult/Calls`, `GetSkillsResult/Calls` para PR3b).
- **Criterios**: equivalencia HTTP ↔ controller.
- **Dependencias**: T3.2
- **Líneas est.**: ~70-100

### Verificación al cerrar PR3a
- `dotnet build SGV.slnx` limpio.
- `dotnet test tests/SGV.Tests --filter "FullyQualifiedName~Web.Cargo.CargoApiClient|FullyQualifiedName~Web.Cargo.FakeCargoApiClient"` verde.

---

## PR3b — Razor Page, anti-drift y tests web

### T3.4 — Crear `Habilidades.cshtml` + PageModel
- **Capa**: Web (Razor Pages)
- **Archivos**: `Pages/Organizacion/Cargos/Habilidades.cshtml` (ruta `/organizacion/cargos/{id:guid}/habilidades`, tabla editable con columnas `Habilidad`, `NivelRequerido`, `Ponderacion`, `Obligatoria`, acción `Quitar`, formulario inline o modal para asignar); `Habilidades.cshtml.cs` (`[Authorize]` + chequeo explícito `RolesSgv.Administrador` en GET/POST, handlers `OnGetAsync`/`OnPostAsignarAsync`/`OnPostActualizarAsync`/`OnPostQuitarAsync`, PRG con `TempData["StatusMessage"]`/`["StatusKind"]`, mapeo de `ValidationProblemDetails` a `ModelState`).
- **Criterios**: `cargo-skill-ui-tabla-editable` Req 1, 2, 3, 4 y 5.
- **Dependencias**: PR3a mergeado
- **Líneas est.**: ~200-280
- **Strict TDD**: empezar con tests de T3.5 fallando contra el PageModel.

### T3.5 — Tests de la Razor Page
- **Capa**: Tests
- **Archivos**: nuevo `Web/Cargo/CargoHabilidadesPageTests.cs` (usando `SgvWebApplicationFactory` o fixture estilo `HabilidadWebTestFixture` + `FakeCargoApiClient` extendido):
  - `Get_Anonymous_RedirectsToSignIn`.
  - `Get_AuthenticatedWithoutAdminRole_Returns403OrForbid`.
  - `Get_Admin_EmptySkills_RendersEmptyState`.
  - `Get_Admin_WithSkills_RendersRowWithNivelRequeridoId`.
  - `PostAsignar_Admin_CallsUpsertSkillAsync_AndPrgRedirectsWithSuccess`.
  - `PostActualizar_Admin_PropagatesPonderacionYEsObligatoria`.
  - `PostQuitar_Admin_CallsDeleteSkillAsync_AndPrgRedirectsWithSuccess`.
  - `Post_TransportFailure_ShowsRecoverableMessage_NoStackTrace`.
  - `Post_BackendReturns400WithPonderacionFieldError_ModelStateShowsError`.
- **Criterios**: escenarios de `cargo-skill-ui-tabla-editable`.
- **Dependencias**: T3.4
- **Líneas est.**: ~250-350

### T3.6 — Anti-drift cross-module
- **Capa**: Tests
- **Archivos**: extender `Web/Habilidad/HabilidadAntiDriftTests.cs` con `HabilidadesPage_NoContaminaHabilidadCatalogoConNivelRequerido` (ausencia de `<select name="Habilidad.NivelId">`, presencia de `NivelRequeridoId` en la tabla).
- **Criterios**: memoria #569; `cargo-skill-ui-tabla-editable` Req 3.
- **Dependencias**: T3.4
- **Líneas est.**: ~30-50

### T3.7 — Verificación de assets y navegación
- **Capa**: Web (build/test)
- **Archivos**: sin código nuevo. `bun run build` en `src/SGV.Web` debe pasar. Decisión de UX ya fuera de scope (si no se enlaza desde `Index/Edit`, queda como URL alcanzable — documentar en `apply-progress.md`).
- **Criterios**: navegación posible; assets consistentes.
- **Dependencias**: T3.4
- **Líneas est.**: 0-10

### Verificación al cerrar PR3b
- `dotnet build SGV.slnx` limpio.
- `dotnet test tests/SGV.Tests/Web` completo verde.
- `bun run build` en `src/SGV.Web` verde.

---

## Riesgos por PR y mitigaciones

| PR | Riesgo | Mitigación |
|---|---|---|
| PR1 | Renombrar `NivelId` → `NivelRequeridoId` rompe consumidores si el controller aún serializa `nivelId` | Cambio atómico DTO+servicio+tests; el controller se actualiza en PR2. |
| PR2 | Contaminar contrato padre de `Cargo` con skills embebidas | T2.4 con test de regresión explícito. |
| PR3a | Mapeo incompleto de `ValidationProblemDetails` deja `FieldErrors` vacíos | Tests de T3.3 cubren 400 con y sin `Errors` poblado. |
| PR3b | Excede budget de 400 líneas y dificulta revisión sana | Split en PR3a+PR3b; chain strategy queda a decisión del usuario. |
| PR3b | Reintroducir `Habilidad.NivelId` por copy-paste | T3.6 anti-drift cross-module. |

## Dependencias externas

- **Migración**: NO requerida (decisión confirmada en `design.md`: tope `100.00` solo en aplicación).
- **Cambios en otros módulos**: ninguno.
- **Bloqueos**: ninguno conocido. `ICargoApiClient` se rompe hasta que PR3a mergee (consumers existentes no usan subrecurso hoy).

## Work units (commits por tarea)

Con `strict_tdd: true`, cada tarea lógica sigue **RED → GREEN** en commits separados (cuando aplica). Cambios mecánicos van en un único `feat:`.

| Tarea | Commits sugeridos |
|---|---|
| T1.1 | `test(aplicacion): expectativas DTO enriquecido` (RED) → `feat(aplicacion): extender DTOs/request CargoSkill` (GREEN) |
| T1.2 | `test(aplicacion): reglas del validator` (RED) → `feat(aplicacion): AsignarCargoSkillRequestValidator` (GREEN) |
| T1.3 | `test(aplicacion): defaults y FieldErrors en UpsertAsync` (RED) → `feat(aplicacion): inyectar validator y propagar FieldErrors` (GREEN) |
| T1.4 | `test(aplicacion): idempotencia preservando Ponderacion/EsObligatoria` (RED) → `feat(aplicacion): refactor upsert` (GREEN) |
| T1.5 | `test(aplicacion): ListAsync expone DTO completo` (RED) → `test(aplicacion): fake repo proyecta nuevos campos` (refactor) |
| T2.1 | `test(persistencia): cargo-skill persiste Ponderacion/EsObligatoria` (RED `[MySqlFact]`) → `feat(persistencia): enriquecer ListDetailedByCargoIdAsync` (GREEN) |
| T2.2 | `test(api): ValidationProblemDetails con FieldErrors` (RED) → `feat(api): bifurcar ToSkillProblemResult` (GREEN) |
| T2.3 | `test(api+swagger): GET expone nivelRequeridoId sin alias` (RED) → `feat(api): documentar schema del subrecurso` (GREEN) |
| T2.4 | `test(api): blindar contrato padre de Cargo` (sin impl necesaria) |
| T3.1 | (entra con T3.2, no testeable aisladamente) |
| T3.2 | `test(web): CargoApiClient cubre 200/204/400/404/transport` (RED) → `feat(web): subrecurso en CargoApiClient` (GREEN) |
| T3.3 | incluido en T3.2 |
| T3.4 | `test(web): Habilidades.cshtml cubre GET/POST/errores` (RED contra placeholder) → `feat(web): PageModel + markup Habilidades.cshtml` (GREEN) |
| T3.5 | parte de T3.4 |
| T3.6 | `test(web): anti-drift Habilidad.NivelId vs CargoHabilidad.NivelRequeridoId` (sin impl si ya pasa) |
| T3.7 | `chore(web): verificar bun build verde y documentar navegación` |

## Próximo paso sugerido

`apply` empezando por PR1, pero antes el orquestador debe:

1. Confirmar con el usuario la **chain strategy** para PR3 (`stacked-to-main`, `feature-branch-chain` o `size:exception` para PR3b).
2. Confirmar si `Habilidades.cshtml` debe enlazarse desde `Index.cshtml` o `Edit.cshtml` (T3.7); por defecto queda como URL alcanzable.

## Referencias

- `proposal.md`, `exploration.md`, `design.md`, `specs/**/spec.md` (mismo directorio).
- `openspec/config.yaml` (`strict_tdd: true`).
- Memoria #569 (`Habilidad` no tiene `NivelId` propio).
- `docs/decisiones-implementacion.md`.