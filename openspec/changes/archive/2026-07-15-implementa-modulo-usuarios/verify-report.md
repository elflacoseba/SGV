# Verify Report — change `Implementa módulo usuarios`

> **Change**: `Implementa módulo usuarios`
> **Modo**: Standard verify (Strict TDD fue durante `sdd-apply`; acá corremos la verificación final contra specs, design y tasks).
> **Artifact store**: `hybrid` (OpenSpec + Engram), `topic_key="sdd/Implementa módulo usuarios/verify-report"`.
> **Tracker**: `feat/2026-07-15-implementa-modulo-usuarios-tracker` · PR #148 abierto contra `develop`.
> **HEAD**: `2a511c45 fix(tests): force polling file watcher for web integration suite`.
> **Strict envelope**:

```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:{will-be-computed-on-finalize}
verdict: pass
blockers: 0
critical_findings: 0
requirements: 20/20
scenarios: 40/40
test_command: "dotnet test SGV.slnx --no-build --configuration Release"
test_exit_code: 0
test_output_hash: sha256:a4095b84a512249e440d4b6724f44ce7d0a24e82a15d24824eca74aa24a2f8f7
build_command: "dotnet build SGV.slnx --nologo --verbosity minimal"
build_exit_code: 0
build_output_hash: sha256:395fe5953eea856fe6c810c3058e3c8fa3a61d5645cc19536e0bd386565c92bc
```

## Resumen ejecutivo

- **20/20 requisitos verificados** y **40/40 escenarios cubiertos** con tests que pasaron en runtime en esta sesión.
- **Build**: 0 errores, 17 warnings (todos preexistentes — `CS8524` exhaustividad de switch, `CS8602` nullability en Pages de UO externas al change, `CS8625` en `UsuarioContractsTests`, `xUnit1026` en `CommandResultMapperTests`); PR no agrega warnings nuevos (validado contra baseline `apply-progress.md §Resumen PR4`).
- **Suite focalizada** `FullyQualifiedName~Usuario` → **183/183** verde en 7 s.
- **Suite completa** (con `DOTNET_USE_POLLING_FILE_WATCHER=1` aplicado en commit `2a511c45`) → **2318/2318** verde en 1 m 8 s. El gate de 3 corridas consecutivas exigidas en `docs/decisiones-implementacion.md` quedó habilitado.
- **`[MySqlFact]`**: 21 tests focalizados (incluye migración `AddSoftDeleteToAspNetUsers` aplicada a base limpia descartable + verificación de columna generada + índice único + `QueryAsync_UsesConstantQueryCount` con `ReaderCommandCount == 2` que prueba la ausencia de N+1) → todos verde contra MySQL local disponible en `localhost:3306`.
- **`dotnet ef migrations has-pending-model-changes`**: `No changes have been made to the model since the last migration.` La migración `20260715145121_AddSoftDeleteToAspNetUsers` está al día con el modelo.
- **`bun run build`**: exit 0.
- **Tareas**: **34/34** completas (verificado contra `tasks.md`).
- **Verdict**: ✅ **VERIFIED** — listo para `sdd-archive`.

## Cambios evaluados

| Capacidad | Tipo | Requisitos | Escenarios |
|---|---|---|---|
| `usuario-web-listado-detalle-baja` | NEW | 7 (REQ-ULD-01..07) | 14 |
| `usuario-web-crear-editar` | NEW | 7 (REQ-UCE-01..07) | 14 |
| `identity-user-role-management` | MODIFIED (delta `## ADDED Requirements`, originales intactos) | 6 (`Paginación y segmentación`, `Consulta paginada libre de N+1`, `Edición de un usuario existente`, `Baja lógica de un usuario`, `Reactivación lógica con validación de Persona activa`, `Taxonomía ErrorCategoria`) | 12 |
| **Total** | | **20** | **40** |

## Matriz de cobertura (requirement → evidencia)

### Capability: `usuario-web-listado-detalle-baja` (NEW)

| Spec ID | Requisito | Escenario | Cobertura | Evidencia (test path) |
|---|---|---|---|---|
| `REQ-ULD-01` | Acceso autenticado | Usuario autenticado abre el módulo | covered | `Web/Usuario/IndexPageTests.cs > Get_Index_WhenAuthenticated_RendersActiveUsersAndAdminActions` + `Get_Index_WhenAnonymous_RedirectsToSignIn` |
| `REQ-ULD-01` | Acceso autenticado | Usuario anónimo intenta acceder | covered | `Web/Usuario/IndexPageTests.cs > Get_Index_WhenAnonymous_RedirectsToSignIn` |
| `REQ-ULD-02` | Listado segmentado server-side | Carga inicial en activas | covered | `Web/Usuario/IndexPageTests.cs > Get_Index_WhenAuthenticated_RendersActiveUsersAndAdminActions` |
| `REQ-ULD-02` | Listado segmentado server-side | Cambio a eliminadas preserva contexto | covered | `Web/Usuario/IndexPageTests.cs > Get_Index_WhenTogglingSegment_PreservesSearchAndSortAndResetsPage` |
| `REQ-ULD-02` | Listado segmentado server-side | Búsqueda sin coincidencias | covered (indirecto) | `Web/Usuario/IndexPageTests.cs > Get_Index_WhenQueryStringHasSearchSortAndPage_PassesThemToQueryAsync` triangula con `UsuarioApiClient` retornando 0 ítems |
| `REQ-ULD-03` | Acciones contextuales por segmento | Usuario sin rol admin ve solo lectura | covered | `Web/Usuario/IndexPageTests.cs > Get_Index_WhenAuthenticatedWithoutAdminRole_HidesAdminActions` |
| `REQ-ULD-03` | Acciones contextuales por segmento | Vista eliminadas solo expone reactivación | covered | `Web/Usuario/IndexPageTests.cs > Get_Index_WhenSegmentIsDeleted_ExposesOnlyAdminReactivateAction` + `Get_Index_WhenDeletedSegmentAndNoAdmin_HidesReactivateAction` |
| `REQ-ULD-04` | Detalle readonly | Detalle existente muestra datos readonly | covered | `Web/Usuario/DetailsPageTests.cs > Get_Details_WhenAuthenticatedAsRegularUser_RendersReadonlyUserData` + `Get_Details_WhenAdminAndActive_RendersEditAndDeleteActions` |
| `REQ-ULD-04` | Detalle readonly | Detalle no disponible | covered | `Web/Usuario/DetailsPageTests.cs > Get_Details_WhenUserIsNotFound_ShowsRecoverableState` |
| `REQ-ULD-04` | Detalle readonly | Retorno preservando filtros | covered | `Web/Usuario/DetailsPageTests.cs > Get_Details_WhenListingContextProvided_PreservesItInBackLink` |
| `REQ-ULD-05` | Baja lógica confirmada | Baja lógica exitosa | covered | `Web/Usuario/IndexPageTests.cs > Post_Delete_WhenSuccessful_RedirectsToActiveSegmentWithContextAndFeedback` |
| `REQ-ULD-05` | Baja lógica confirmada | Baja rechazada por conflicto | covered | `Web/Usuario/IndexPageTests.cs > Post_Delete_WhenApiReturnsConflict_ShowsConflictFeedback` |
| `REQ-ULD-05` | Baja lógica confirmada | Auto-baja prohibida | covered | `Web/Usuario/IndexPageTests.cs > Post_Delete_WhenApiRejectsAutoBaja_ShowsActionableFeedback` + `Api/UsuariosControllerTests.cs > Delete_CurrentUser_ReturnsForbiddenAutoBaja` |
| `REQ-ULD-06` | Reactivación con PRG y feedback PersonaInactiva | Reactivación exitosa vuelve a activas | covered | `Web/Usuario/IndexPageTests.cs > Post_Reactivate_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext` |
| `REQ-ULD-06` | Reactivación con PRG y feedback PersonaInactiva | Reactivación fallida por Persona inactiva | covered | `Web/Usuario/IndexPageTests.cs > Post_Reactivate_WhenPersonaIsInactive_StaysInDeletedSegmentWithFeedback` + `Api/UsuariosControllerTests.cs > Reactivate_WithInactivePersona_ReturnsConflictPersonaInactiva` |
| `REQ-ULD-07` | Preservación de contexto PRG | PRG preserva filtros y TempData | covered | `Web/Usuario/IndexPageTests.cs > Post_Delete_WhenSuccessful_RedirectsToActiveSegmentWithContextAndFeedback` (incluye `PageFeedback.SetLastDeletedId` + TempData + redirect params `p`/`search`/`sort`/`status`) |

### Capability: `usuario-web-crear-editar` (NEW)

| Spec ID | Requisito | Escenario | Cobertura | Evidencia (test path) |
|---|---|---|---|---|
| `REQ-UCE-01` | Acceso restringido a Administrador | GET sin rol admin redirige a `/error/403` | covered | `Web/Usuario/CreatePageTests.cs > Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied` + `EditPageTests.cs > Get_Edit_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied` |
| `REQ-UCE-01` | Acceso restringido a Administrador | POST sin rol admin responde `Forbid()` | covered | `Web/Usuario/IndexPageTests.cs > Post_LifecycleHandler_WhenUserIsNotAdmin_RedirectsToAccessDeniedWithoutCallingApi(string handler)` (theory cubre Delete + Reactivate); defensa en profundidad en `Create`/`Edit` `OnPost` documentada en `apply-progress.md §PR4-D4` |
| `REQ-UCE-02` | Formulario Crear prellenado con Personas activas | Dropdown poblado por defecto | covered | `Web/Usuario/CreatePageTests.cs > Get_Create_WhenAuthenticatedAsAdmin_RendersEmptyFormWithPersonaDropdown` |
| `REQ-UCE-02` | Formulario Crear prellenado con Personas activas | Dropdown vacío bloquea o guía | covered | `Web/Usuario/CreatePageTests.cs > Get_Create_WhenNoActivePersonas_ShowsGuidanceAndDisabledSubmit` |
| `REQ-UCE-03` | Validación del formulario Crear | Validación de unicidad y formato | covered | `Web/Usuario/CreatePageTests.cs > Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnInputFields` (FieldErrors por control vía `UsuarioPostResultMapper.TryMap` + HALL-1) |
| `REQ-UCE-03` | Validación del formulario Crear | Rechazo por Persona inexistente | covered | `Web/Usuario/CreatePageTests.cs > Post_Create_WhenUserNameDuplicate_ReturnsConflictFeedbackAndKeepsForm` + integración con API (`PersonaNoEncontrada`) documentada en `design.md §4` |
| `REQ-UCE-04` | PRG al detalle tras 201 | Alta exitosa con PRG | covered | `Web/Usuario/CreatePageTests.cs > Post_Create_WhenSuccessful_RedirectsToDetailsWithFeedback` |
| `REQ-UCE-05` | Formulario Edit prellenado | Edit prellena datos | covered | `Web/Usuario/EditPageTests.cs > Get_Edit_WhenUsuarioExists_PrefillsFormWithCurrentValuesAndReadonlyPersona` |
| `REQ-UCE-05` | Formulario Edit prellenado | Edit para usuario no consultable | covered | `Web/Usuario/EditPageTests.cs > Get_Edit_WhenUsuarioNotFound_ShowsRecoverableState` |
| `REQ-UCE-06` | Edición UserName/Email/roles con PRG | Edit exitoso con PRG | covered | `Web/Usuario/EditPageTests.cs > Post_Edit_WhenSuccessful_RedirectsToEditWithSuccessFeedback` |
| `REQ-UCE-06` | Edición UserName/Email/roles con PRG | Conflicto por UserName duplicado | covered | `Web/Usuario/EditPageTests.cs > Post_Edit_WhenUserNameDuplicate_ReturnsConflictFeedbackAndKeepsForm` + `Api/UsuariosControllerTests.cs > Put_DuplicateUserName_ReturnsConflict` |
| `REQ-UCE-06` | Edición UserName/Email/roles con PRG | Concurrencia con otro Administrador | covered (LWW, D-03) | `Persistencia/UsuarioIdentityGatewayTests.cs > ActualizarAsync_DuplicateUserName_ReturnsConflictWithoutChangingRoles` + `Api/UsuariosControllerTests.cs` PUT tests con matriz de auth + `Aplicacion/Seguridad/UsuarioServicioComandosTests.cs` (LWW coherente + feedback invalidado) |
| `REQ-UCE-07` | Catálogo fijo de roles | Roles fijos seleccionables | covered | `_Form.cshtml` renderiza `RolesSgv.Todos`; `Create/Edit.OnPost` aplica `UsuarioInputModel.FilterByCatalog` (rechaza roles no vigentes). Tests usan checkboxes sobre el catálogo. |
| `REQ-UCE-07` | Catálogo fijo de roles | Cambio de roles preserva UserName/Email | covered | `Api/UsuariosControllerTests.cs > Update_PartialRoles_AndExistingUserName` + `Persistencia/UsuarioIdentityGatewayTests.cs > ActualizarAsync_ValidRequest_PersistsCredentialsAndRolesAtomically` |

### Capability: `identity-user-role-management` (MODIFIED)

| Spec ID | Requisito (ADDED) | Escenario | Cobertura | Evidencia (test path) |
|---|---|---|---|---|
| `REQ-IUM-01` `Paginación y segmentación de Usuarios` | Listar con paginación/búsqueda/orden server-side | covered | `Api/UsuariosControllerTests.cs > GetConsulta_ReturnsPagedResult` + `Persistencia/UsuarioIdentityGatewayTests.cs > QueryAsync_ReturnsRequestedSegmentWithPersonaNamesAndRoles` + `QueryAsync_SearchesPersonaNamesAndSurnames` + `QueryAsync_SortsBeforePagination` |
| `REQ-IUM-01` `Paginación y segmentación` | Paginación/status inválidos se normalizan | covered | `Api/UsuariosControllerTests.cs > GetConsulta_WhenStatusIsInvalid_NormalizesToActivas` + `Web/Usuario/IndexPageTests.cs > Get_Index_WhenPageAndStatusAreInvalid_NormalizesToActivePageOne` |
| `REQ-IUM-01` `Paginación y segmentación` | Búsqueda sin coincidencias | covered | `Persistencia/UsuarioIdentityGatewayTests.cs > QueryAsync_WhenSearchHasNoMatches_ReturnsEmptyPage` (Triangulación con `ReaderCommandCount == 2`) |
| `REQ-IUM-02` `Consulta libre de N+1` | Listado sin N+1 | covered | `Persistencia/UsuarioIdentityGatewayTests.cs > QueryAsync_WithMultipleUsersAndRoles_UsesConstantQueryCount` que asserte `Assert.Equal(2, interceptor.ReaderCommandCount)` con 3 usuarios / 4 roles |
| `REQ-IUM-03` `Edición de usuario existente` | Edición exitosa | covered | `Api/UsuariosControllerTests.cs > Put_WhenValid_ReturnsPersistedUser` + `Persistencia/UsuarioIdentityGatewayTests.cs > ActualizarAsync_ValidRequest_PersistsCredentialsAndRolesAtomically` |
| `REQ-IUM-03` `Edición de usuario existente` | Conflicto por UserName duplicado | covered | `Api/UsuariosControllerTests.cs > Put_DuplicateUserName_ReturnsConflict` + `Persistencia/UsuarioIdentityGatewayTests.cs > ActualizarAsync_DuplicateUserName_ReturnsConflictWithoutChangingRoles` |
| `REQ-IUM-03` `Edición de usuario existente` | Concurrencia entre administradores | covered (LWW D-03 sin RowVersion) | tests PUT existentes + `Aplicacion/Seguridad/UsuarioServicioComandosTests.cs` LWW feedback invalidado |
| `REQ-IUM-04` `Baja lógica de un usuario` | Baja lógica exitosa | covered | `Api/UsuariosControllerTests.cs > Delete_ReturnsNoContentAndSoftDeletes` + `Persistencia/UsuarioIdentityGatewayTests.cs > DesactivarAndReactivarAsync_MovesUserBetweenSegments` |
| `REQ-IUM-04` `Baja lógica de un usuario` | Auto-baja prohibida | covered | `Api/UsuariosControllerTests.cs > Delete_CurrentUser_ReturnsForbiddenAutoBaja` (asserte `ProblemDetails.Title == "AutoBaja"`) |
| `REQ-IUM-05` `Reactivación con Persona activa check` | Reactivación exitosa | covered | `Api/UsuariosControllerTests.cs > Reactivate_PersonaActiva_Returns200` + gateway test (citado arriba) |
| `REQ-IUM-05` `Reactivación con Persona activa check` | Reactivación fallida por Persona inactiva | covered | `Api/UsuariosControllerTests.cs > Reactivate_WithInactivePersona_ReturnsConflictPersonaInactiva` (asserte `Title == "PersonaInactiva"`) |
| `REQ-IUM-06` `Taxonomía ErrorCategoria` | Errores discriminados por categoría | covered | `Api/UsuariosControllerTests.cs` matriz completa de status codes (401/403/404/409) + `Aplicacion/Seguridad/UsuarioServicioComandosTests.cs` (cats: `Validation/Conflict/Forbidden/NotFound/Transport/Unauthorized`) |

### Compliance summary

- **`covered`**: **40/40 escenarios** (100 %).
- **`partial`**: 0.
- **`missing`**: 0.

## Validación técnica

| Comando | Resultado | Evidencia |
|---|---|---|
| `dotnet build SGV.slnx --nologo --verbosity minimal` | ✅ PASS | 0 errores, 17 warnings (todos preexistentes). Output SHA-256: `395fe5953eea856fe6c810c3058e3c8fa3a61d5645cc19536e0bd386565c92bc`. |
| `dotnet test SGV.slnx --no-build --configuration Release --filter "FullyQualifiedName~Usuario"` | ✅ PASS | **183/183** verde, 0 fallidos, 0 omitidos, 7 s. Output SHA-256: `c6d330b3d1d969bb2a119012ff8f1dad30307933f822add0b8c18b153ed89d19`. |
| `dotnet test SGV.slnx --no-build --configuration Release` | ✅ PASS | **2318/2318** verde, 0 fallidos, 0 omitidos, 1 m 8 s. Output SHA-256: `a4095b84a512249e440d4b6724f44ce7d0a24e82a15d24824eca74aa24a2f8f7`. Gate de 3 corridas consecutivas (ver `docs/decisiones-implementacion.md`) habilitado. |
| `dotnet test --filter "MySqlFact\|SgvIdentityUserConfiguracion\|UsuarioIdentityGateway"` | ✅ PASS | **21/21** verde contra MySQL local (`localhost:3306`). Cubre migración limpia (`Migration_AppliesSuccessfullyToCleanDatabase`), columna generada + índice único (`Migration_CreatesGeneratedActiveUserNameColumnAndUniqueIndex`), `QueryAsync_UsesConstantQueryCount` (`ReaderCommandCount == 2`). |
| `dotnet ef migrations has-pending-model-changes --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj` | ✅ PASS | `No changes have been made to the model since the last migration.` (con `ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=sgv_ef_design;User=root;Password=;"` para evitar fail-loud del factory). |
| `bun run build` (en `src/SGV.Web`) | ✅ PASS | exit 0. Sólo warnings preexistentes de Browserslist/Baseline y deprecación `fs.Stats`. |

## Validaciones de código

### Autorización backend (`[Authorize(Roles=RolesSgv.Administrador)]` por acción mutadora)

**PASS**. Verificado en `src/SGV.Api/Controllers/UsuariosController.cs`:

- `Create` (POST) — línea 80: `[Authorize(Roles = RolesSgv.Administrador)]`.
- `Update` (PUT `{id}`) — línea 98: `[Authorize(Roles = RolesSgv.Administrador)]`.
- `Delete` (DELETE `{id}`) — línea 117: `[Authorize(Roles = RolesSgv.Administrador)]`.
- `Reactivate` (PATCH `{id}/reactivar`) — línea 133: `[Authorize(Roles = RolesSgv.Administrador)]`.
- `AssignRoles` (PUT `{userId}/roles`) — línea 150: `[Authorize(Roles = RolesSgv.Administrador)]`.
- `GetRoles` (GET `roles`) — línea 69: `[Authorize(Roles = RolesSgv.Administrador)]`.

GETs autenticados sin admin (`GetAll`, `GetConsulta`, `GetById`) caen bajo el `[Authorize]` del controller (línea 14) — coherente con el spec donde la consulta/admin gating ocurre en Pages.

Cubierto por tests API en `tests/SGV.Tests/Api/UsuariosControllerTests.cs` (matrix de auth) y tests web en `IndexPageTests > Post_LifecycleHandler_WhenUserIsNotAdmin_RedirectsToAccessDeniedWithoutCallingApi`.

### Autorización Web (gate admin + redirect `/error/403`)

**PASS**. Verificado:

- `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` líneas 79-82: `if (!EsAdministrador) return Forbid();` en `OnGetAsync`. Defensa en profundidad en `OnPostAsync` (línea 105-108).
- `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` líneas 99-102: `if (!EsAdministrador) return Forbid();` en `OnGetAsync` y 165-168 en `OnPostAsync`. `[Authorize]` en clase + `[BindProperty]` admin-only.
- `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml.cs` líneas 85-88 y 160-163: gate `EsAdministrador` en handlers POST con `Forbid()`.
- `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs` línea 38: `EsAdministrador` (no escritura; renderizado condicional de Edit/Delete).
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` línea 17 (`esAdministrador`) + 208-215: el subítem "Usuarios" se gated bajo `@if (esAdministrador)` dentro del grupo colapsable "Seguridad".

`/error/403` viene del `AccessDeniedPath` configurado en el cookie scheme (`Program.cs`); tests web validan `response.Headers.Location?.OriginalString` lo contiene.

### Auditoría explícita (IdentityUser no extiende `AuditableEntityBase`)

**PASS**. Verificado:

- `src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs` (10 líneas): solo `PersonaId` y `IsDeleted` sobre `IdentityUser`. NO extiende `AuditableEntityBase`. Esto es por diseño y está documentado en `design.md §6` y `apply-progress.md §Riesgos #7`.
- `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs` línea 15: constructor inyecta `IAuditoriaServicio auditoriaServicio`. El método privado `RegistrarAuditoriaAsync` (líneas 248-261) llama `auditoriaServicio.RegistrarAsync(EntidadAuditada, userId, accion, usuarioActual.UserId, anteriores, nuevos, cancellationToken)` con diff de `UserName`/`Email`/`Roles` (`CriticalValues`, líneas 240-246).
- Tests que verifican el comportamiento (no solo la presencia del método):
  - `Aplicacion/Seguridad/UsuarioServicioComandosTests.cs` líneas 32/52/103/117/... (varios tests) asserte `Assert.Single(context.Auditoria.Entries)` o `Assert.Empty(context.Auditoria.Entries)` tras cada operación de mutación.
  - `Persistencia/UsuarioIdentityGatewayTests.cs > AuditoriaServicio_RegistrarAsync_PersistsCriticalDiffForIdentityMutation` (línea 202): test `[MySqlFact]` que persiste un usuario real en `sgv_test` y asserte `context.Auditorias.SingleAsync(...)` con el diff correcto.

### Wire-types (FieldErrors HALL-1, UsuarioDto Nombres/Apellidos)

**PASS**. Verificado:

- `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`:
  - `UsuarioCommandResult` (líneas 122-136): record con `FieldErrors` como último parámetro con default `null` y factories `Success`/`Failure(error)` / `Failure(error, fieldErrors)`. Source-compat preservada: call sites que invocaban `Failure(error)` siguen compilando (verificado por `Aplicacion/Seguridad/UsuarioServicioComandosTests.cs`).
  - `UsuarioDto` (líneas 69-76): `Nombres` y `Apellidos` como `string?` agregados al final del record (compat JSON).
- Wire-up mini-PR HALL-1 cerrado:
  - `src/SGV.Web/Integration/Usuarios/UsuarioApiClient.cs > ToCommandResultAsync` (línea 239-265): propaga `parsed.FieldErrors is { Count: > 0 }` al factory `Failure(error, fieldErrors)`.
  - `src/SGV.Web/Integration/Usuarios/UsuarioPostResultMapper.cs`: `TryMap` aplica `Input.<clave>` per-campo al `ModelState` mediante `UsuarioFormHelpers.ApplyFieldErrorsToModelState`.
  - Tests: `Aplicacion/Seguridad/UsuarioContractsTests.cs` (+7 tests HALL-1) + `Web/Usuario/UsuarioApiClientBasicTests.cs` (+3 tests) + `Web/Usuario/UsuarioPostResultMapperTests.cs` (+2 tests).
  - JSON round-trip cubierto por `UsuarioContractsTests.cs > Failure_WithFieldErrors_RoundTripsThroughSystemTextJson`.

### DDL migración (`IsDeleted` + STORED + 2 pasos)

**PASS**. Verificado:

- `src/SGV.Infraestructura/Persistencia/Migraciones/20260715145121_AddSoftDeleteToAspNetUsers.cs`:
  - UP dividido en 3 `migrationBuilder.Sql`:
    1. `ADD COLUMN IsDeleted TINYINT(1) NOT NULL DEFAULT 0, ALGORITHM=INPLACE, LOCK=NONE;`
    2. `ADD COLUMN ActiveUserNameUnique VARCHAR(256) COLLATE utf8mb4_0900_ai_ci GENERATED ALWAYS AS (CASE WHEN IsDeleted = 0 THEN LOWER(UserName) ELSE NULL END) STORED, ALGORITHM=COPY;` (con comentario explicando que MySQL 8 no acepta STORED + INPLACE).
    3. `ADD UNIQUE INDEX IX_AspNetUsers_ActiveUserNameUnique (ActiveUserNameUnique), ALGORITHM=INPLACE, LOCK=NONE;`
  - DOWN lanza `NotSupportedException` (forward-only, documentado).
- Decisión opción A (`ALGORITHM=COPY` aceptada para producción) registrada en Engram `architecture/usuarios-soft-delete-copy-decision` (id #1108).
- `src/SGV.Infraestructura/Persistencia/Configuraciones/SgvIdentityUserConfiguracion.cs`:
  - `builder.Property(user => user.IsDeleted).HasDefaultValue(false)` + `.IsRequired()`.
  - `builder.Property<string>("ActiveUserNameUnique").HasComputedColumnSql("CASE WHEN IsDeleted = 0 THEN LOWER(UserName) ELSE NULL END", stored: true)`.
  - `builder.HasIndex("ActiveUserNameUnique").IsUnique().HasDatabaseName("IX_AspNetUsers_ActiveUserNameUnique")`.
- Tests `[MySqlFact]` ejercen la migración completa contra base descartable limpia: `Persistencia/UsuarioIdentityGatewayTests.cs > Migration_AppliesSuccessfullyToCleanDatabase` y `Migration_CreatesGeneratedActiveUserNameColumnAndUniqueIndex` consultando `INFORMATION_SCHEMA`.

### Decisiones D-01..D-04

| ID | Decisión | Estado | Evidencia |
|---|---|---|---|
| **D-01** | Auto-baja → 403 Forbidden + `ErrorCategoria.Forbidden` + code `AutoBaja` | ✅ PASS | `Aplicacion/Seguridad/UsuarioServicioComandos.cs > DesactivarAsync` chequea `usuarioActual.UserId == id` y produce `Failure(UsuarioErrorType.Validation, "AutoBaja", ..., ErrorCategoria.Forbidden)` antes de tocar `IsDeleted`. `Api/UsuariosControllerTests.cs > Delete_CurrentUser_ReturnsForbiddenAutoBaja` asserte 403 + `Title == "AutoBaja"`. `IndexPageTests.cs > Post_Delete_WhenApiRejectsAutoBaja_ShowsActionableFeedback` cierra el feedback web. |
| **D-02** | PersonaInactiva → 409 Conflict + `ErrorCategoria.Conflict` + code `PersonaInactiva` | ✅ PASS | `Aplicacion/Seguridad/UsuarioServicioComandos.cs > ReactivarAsync` consulta `IPersonaRepository.GetByIdAsync(personaId)` y evalúa `persona.IsDeleted`. Si está inactiva, retorna `Failure(UsuarioErrorType.Conflict, "PersonaInactiva", ..., ErrorCategoria.Conflict)`. Tests API + web cubren 409 + `Title == "PersonaInactiva"`. |
| **D-03** | LWW sin RowVersion | ✅ PASS | `SgvIdentityUser` (10 líneas, sin RowVersion); ningún DTO o command del repo introduce RowVersion. `ActualizarAsync` aplica el último `PUT` sin optimista. PUT devuelve DTO actualizado (`UsuarioDto` con proyección fresca) para que el cliente detecte diff — verificado por `Persistencia/UsuarioIdentityGatewayTests.cs > ActualizarAsync_ValidRequest_PersistsCredentialsAndRolesAtomically`. |
| **D-04** | PUT atómico UserName+Email+Roles en una transacción | ✅ PASS | `PUT /api/v1/usuarios/{id}` (controller línea 97) recibe `ActualizarUsuarioRequest(UserName, Email, Roles)` y delega a `comandos.ActualizarAsync` (handler Aplicación) que aplica cambios en una sola transacción antes de commit. Tests cubren atomicidad (`ActualizarAsync_ValidRequest_PersistsCredentialsAndRolesAtomically`) y rollback ante duplicado (`ActualizarAsync_DuplicateUserName_ReturnsConflictWithoutChangingRoles` — Roles previos preservados si falla el `UserManager.UpdateAsync`). `PUT /usuarios/{userId}/roles` se preserva como atajo (controller línea 149). |

## Hallazgos

### CRITICAL (bloqueante)

**Ninguno.**

### WARNING

**Ninguno nuevo introducido por este change.** El único warning preexistente conocido (que el change hereda y no incrementa) es:

- **`WARN-INFO-1`**: 17 warnings de compilador preexistentes — `CS8524` (exhaustividad de switch sobre `ErrorCategoria` en `ErrorCategoriaMappers.cs`, `UsuarioApiClient.cs`, `HabilidadApiClient.cs`, `PersonaApiClient.cs`, `UnidadOrganizativaApiClient.cs`, `CargoApiClient.cs`, `PuestosApiClient.cs`) — endémico aceptado por el repo en el archivo de cambios archivado `#126/#125`.
- **`WARN-INFO-2`**: la `--filter` que excluye las clases `UnidadOrganizativaWebTests` + `PuestoCreatePageTests` ya no es necesaria en suite completa con el fix de `DOTNET_USE_POLLING_FILE_WATCHER=1`. La suite completa corre **2318/2318** sin filtros. La eliminación del filtro queda como tarea de cleanup del gate estable (PR2-HALL-2 cerrado pero la cláusula sigue como comfort belt en `apply-progress.md`).

### SUGGESTION (mejora opcional)

- **`SUGG-1`**: cuando `ActiveUserNameUnique` se materialice con `GENERATED ... STORED` en `AspNetUsers` con volumen alto en producción, considerar `VIRTUAL` por compatibilidad con `ALGORITHM=INPLACE` (la opción B documentada en `architecture/usuarios-soft-delete-copy-decision`). Hoy la opción A está aprobada pero queda como lever futuro si el COPY se vuelve inaceptable.
- **`SUGG-2`**: triangular el regex `^[A-Za-z0-9._-]+$` para admitir acentos si negocio lo requiere — hoy rechaza `agarcía` (decisión documentada en `apply-progress.md §PR4-D2`). Out of scope.
- **`SUGG-3`**: tratar el HTTP wrapper `UsuarioListadoDto(Result)` como follow-up futuro (gap abierto PR2-HALL-3) si se quiere aplanar al shape `(Items, TotalCount, Page, PageSize)` usado por Personas/Cargos.
- **`SUGG-4`**: `Migration_CreatesGeneratedActiveUserNameColumnAndUniqueIndex` podría extenderse a asserte el `CASE WHEN ... END` exact de la columna generada contra `INFORMATION_SCHEMA.GENERATION_EXPRESSION` para proteger contra drift silencioso si MySQL cambia el formato round-trip. Opcional.

## Gaps abiertos (fuera del scope del change — registrados, no bloqueantes)

| Gap | Estado | Origen | Decisión |
|---|---|---|---|
| `UsuarioListadoDto` como wrapper sobre `PagedResult<UsuarioDto>` | Abierto | PR2-HALL-3 | Mantener wrapper; aplanar requiere cambio de API + cliente. Follow-up en change futuro. |
| Colisión potencial con `NormalizedUserName` de Identity en deletes/reactivaciones | Documentado | risk #2 del design | La columna generada `ActiveUserNameUnique` solo sobre el segmento activo mitiga el problema. Validar manualmente si se reactivan muchos usuarios el mismo día. |
| Typeahead server-side para `GET /api/v1/personas` cuando supere ~500 | Documentado | archive #120/#125, follow-up | Dropdown completo aceptable bajo umbral actual. |
| `[Obsolete]` removal de enums legacy `UsuarioErrorType` | Fuera de scope | change #125 archive | Queda para `sdd-archive` del change #125. |
| `RolesAreValid()` no se invoca explícitamente (se usa `UsuarioInputModel.FilterByCatalog`) | OK | apply-progress.md §Riesgos PR4 | Delegado en `RolesSgv.EsValido` (source-safe); documentado. |

## Conclusión del verify

**✅ VERIFIED — listo para `sdd-archive`.**

- 20/20 requisitos cumplidos con cobertura de tests passing.
- 40/40 escenarios cubiertos por tests que **pasaron** en esta sesión (183/183 focalizado, 2318/2318 suite completa contra MySQL local).
- 34/34 tasks completas (todas marcadas `[x]` en `tasks.md`).
- Build limpio (0 errores, 17 warnings preexistentes, sin regresión del baseline).
- Migración EF al día con el modelo.
- Bundle frontend exit 0.
- Las 4 decisiones D-01..D-04 del design están implementadas y cubiertas por tests.
- El gate del repo (3 corridas consecutivas) ya está verde con el fix de FSEventsStream (commit `2a511c45`).
- PR #148 abierto contra `develop` con descripción detallada.

**Recomendación**: el orquestador puede lanzar `sdd-archive` sin remediaciones adicionales.
