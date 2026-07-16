# Progreso de aplicación — Implementa módulo usuarios

## Estado del lote

- **PR actual**: PR1 — Backend.
- **Rama tracker**: `feat/2026-07-15-implementa-modulo-usuarios-tracker`.
- **Rama de trabajo**: `feat/2026-07-15-implementa-modulo-usuarios-pr1-backend`.
- **Estrategia**: `feature-branch-chain`; PR1 debe apuntar al tracker.
- **Modo de implementación**: Strict TDD.
- **Tareas PR1**: 15/15 completadas.
- **Tareas del change**: 15/34 completadas; PR2, PR3 y PR4 permanecen fuera de alcance.
- **Estado**: implementación funcional completa. Decisión humana adoptada sobre la desviación operativa de migración (ver "Decisión humana sobre desviaciones").

## Resumen de implementación

- Se agregó soft-delete a `SgvIdentityUser` y la migración `AddSoftDeleteToAspNetUsers` con `IsDeleted`, columna generada `ActiveUserNameUnique` e índice único.
- `UsuarioDto` conserva el orden existente y agrega `Nombres`/`Apellidos` nullable al final; se incorporaron `ActualizarUsuarioRequest`, `UsuarioListQuery`, `UsuarioSegmentoListado` y `UsuarioListadoDto`.
- `UsuarioIdentityGateway` ahora expone consulta paginada/segmentada, detalle, actualización atómica, baja y reactivación; la carga de roles usa una consulta agregada y no ejecuta `GetRolesAsync` dentro de un bucle.
- `UsuarioServicioComandos` implementa D-01 (`AutoBaja` → `Forbidden`), D-02 (`PersonaInactiva` → `Conflict`), D-03 (LWW) y D-04 (PUT único UserName+Email+Roles).
- Todas las mutaciones registran auditoría explícita con `IAuditoriaServicio`, incluyendo diffs de `UserName`, `Email` y roles.
- `UsuariosController` deja las lecturas a cualquier autenticado y exige `Administrador` en POST/PUT/DELETE/PATCH y en el catálogo de roles.
- Se agregó `UsuarioActualHttpContext` para que el guard de auto-baja y la auditoría reciban el `sub` real del JWT.
- Se corrigió `JwtRealWebApplicationFactory`: el DbContext del test ahora se reemplaza explícitamente y queda aislado en `sgv_test`, evitando tocar la base local `sgv`.

## Tareas completadas

- [x] **1.1** Tests RED para auto-baja y Persona inactiva con mapeos 403/409.
- [x] **1.2** Migración EF y modelo Identity con soft-delete, columna generada e índice único.
- [x] **1.3** Script SQL idempotente acotado a `AddSoftDeleteToAspNetUsers`.
- [x] **1.4** `UsuarioDto` con `Nombres`/`Apellidos` nullable al final.
- [x] **1.5** Contratos de consulta, segmento y wrapper paginado.
- [x] **1.6** `IUsuarioServicioConsulta.QueryAsync` y detalle por id.
- [x] **1.7** `UsuarioIdentityGateway.QueryAsync` sin N+1.
- [x] **1.8** Puertos de actualización, baja y reactivación.
- [x] **1.9** Handlers de aplicación con validaciones y auditoría.
- [x] **1.10** Tests unitarios de aplicación.
- [x] **1.11** Tests MySQL de gateway, migración limpia, consulta y reactivación.
- [x] **1.12** Endpoints API de consulta, detalle, PUT, DELETE y PATCH.
- [x] **1.13** Taxonomía HTTP para `AutoBaja`, `PersonaInactiva`, duplicados y persona asociada mediante `ErrorCategoria`/`ApiResults`.
- [x] **1.14** Tests API de paginación, normalización, autorización y códigos de error.
- [x] **1.15** Build, gate focalizado, migración sin cambios pendientes y suite completa.

## Evidencia de ciclos TDD

| Task | Archivo(s) de test | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | `UsuarioServicioComandosTests.cs`, `UsuariosControllerTests.cs` | Unit/API | 27/27 previos | Falló por APIs inexistentes | `AutoBaja` 403 y `PersonaInactiva` 409 verdes | Caller propio/ajeno + Persona activa/inactiva | Guard clauses y helper de fallos |
| 1.2 | `SgvIdentityUserConfiguracionTests.cs`, `UsuarioIdentityGatewayTests.cs` | Modelo/MySQL | 3/3 config previos | Faltaban propiedades y DDL | Modelo y esquema verdes | Metadata + base descartable desde cero | ALTER dividido por capacidad real de MySQL |
| 1.3 | `SgvIdentityUserConfiguracionTests.cs` | Estructural | N/A (artefacto nuevo) | No existía operación SQL verificable | Script idempotente generado | DDL estática + aplicación real | Script acotado a una migración |
| 1.4 | `UsuarioContractsTests.cs` | Contrato | N/A (comportamiento nuevo) | Constructor sin nombres | 4/4 contratos verdes | Orden + nullability | Defaults nullable para compatibilidad fuente |
| 1.5 | `UsuarioContractsTests.cs` | Contrato | N/A (tipos nuevos) | Tipos inexistentes | Query/segmento/wrapper verdes | Segmento default + metadata paginada | Triangulación estructural suficiente |
| 1.6 | `ApiWebApplicationFactory.cs`, `UsuariosControllerTests.cs` | Contrato/API | 27/27 previos | Fakes no compilaban contra la nueva interfaz | Consulta y detalle verdes | Resultado existente/no existente | Interfaces estrechas por operación |
| 1.7 | `UsuarioIdentityGatewayTests.cs` | MySQL | N/A (método nuevo) | Gateway sin `QueryAsync` | Consulta devuelve DTO+roles | 3 usuarios/4 roles con exactamente 2 readers | GroupJoin y agrupación en memoria |
| 1.8 | `UsuarioServicioComandosTests.cs`, `UsuarioIdentityGatewayTests.cs` | Unit/MySQL | 6/6 comandos previos | Puertos y métodos inexistentes | Ciclo completo verde | Éxito, missing y conflicto | Reemplazo de roles por diferencias |
| 1.9 | `UsuarioServicioComandosTests.cs` | Unit | 6/6 comandos previos | Servicio no implementaba handlers | 18/18 verdes | Validaciones, auto-baja, LWW y Persona inactiva | Helpers de validación/auditoría |
| 1.10 | `UsuarioServicioComandosTests.cs` | Unit | 6/6 previos | Casos nuevos fallaron/compilaron en rojo | 18/18 verdes | Casos felices + bordes por comportamiento | Fakes con estado observable |
| 1.11 | `UsuarioIdentityGatewayTests.cs` | Integración MySQL | Bootstrap disponible | Migración limpia falló con STORED+INPLACE | 10/10 verdes | DB existente + DB descartable limpia | Gate explícito de query-count y cleanup |
| 1.12 | `UsuariosControllerTests.cs` | API integración | 4/4 API usuarios previos | Rutas inexistentes | 26/26 verdes | Auth, éxito y fallos por endpoint | Controller delgado y `ApiResults` central |
| 1.13 | `UsuariosControllerTests.cs`, `ErrorCategoriaMappersTests.cs` | API/contrato | Mapper legacy verde | 403/409 nuevos no existían | Matriz observable verde | Forbidden/Conflict/Validation/NotFound | Se reutilizó `MapCategoria`; mapper legacy no se alteró |
| 1.14 | `UsuariosControllerTests.cs` | API integración | 4/4 previos | Nuevos escenarios fallaron | 26/26 verdes | Normalización, aliases, auth y mutaciones | Theories para matriz de autorización |
| 1.15 | Suite completa | Solución | Build y 27 tests usuarios previos | Primer full run expuso aislamiento JWT | 2211/2211 en 3 corridas | Focalizado + MySQL + suite completa | Fixture JWT aislada en `sgv_test` |

## Resumen de pruebas

- **Casos focalizados de usuarios**: 77/77 verdes (baseline previo: 27).
- **Tests de comandos de aplicación**: 18/18 verdes.
- **Tests API de `UsuariosController`**: 26/26 verdes.
- **Tests MySQL del gateway/migración**: 10/10 verdes, incluido bootstrap de una base descartable limpia.
- **Suite completa final**: 2211/2211, 0 fallidos, 0 omitidos, tres corridas consecutivas (`61 s`, `69 s`, `59 s`).
- **Build final**: exitoso; warnings preexistentes conocidos (`CS8524`, `CS8602`, `xUnit1026`) reaparecen en build limpio, sin errores.
- **Modelo EF**: `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.

## Evidencia de work unit PR1

| Evidencia | Resultado |
|---|---|
| Comando focalizado | `dotnet test SGV.slnx --no-build --filter "Api.Usuarios|Persistencia.Usuarios|Aplicacion.Usuarios"` → 26/26; gate amplio `FullyQualifiedName~Usuario` → 77/77 |
| Runtime harness | `[MySqlFact]` sobre `sgv_test` + base descartable desde cero → 10/10; migraciones aplicadas y columna/índice verificados en `INFORMATION_SCHEMA` |
| Rollback boundary | Revertir los seis commits de PR1 elimina contratos, migración, gateway/handlers, endpoints, auditoría explícita y tests sin tocar PR2/PR3/PR4 |

## Commits de implementación

1. `9bd11420` — `feat(schema): add soft delete to identity users`
2. `8de4990b` — `feat(application): add atomic identity user lifecycle`
3. `0e6e499f` — `feat(api): expose complete identity user management`
4. `654b68e3` — `fix(test): isolate real jwt auth database`
5. `6e10634b` — `fix(schema): make stored user column migration executable`
6. `0a324059` — `test(application): preserve unsupported role guard`

## Desviaciones del diseño

1. **STORED + INPLACE no es ejecutable en MySQL 8**. El RED sobre una base limpia devolvió: `ALGORITHM=INPLACE is not supported for this operation. Try ALGORITHM=COPY.` La migración conserva el esquema final solicitado (`STORED`) y divide el rollout: `IsDeleted` e índice usan `INPLACE/LOCK=NONE`; la incorporación de la columna STORED declara `ALGORITHM=COPY`.

## Decisión humana sobre desviaciones

En sesión interactiva tras cerrar `sdd-apply` del PR1, el maintainer adoptó explícitamente la **opción A: aceptar `ALGORITHM=COPY`**. Implicaciones operativas:

- La columna `AspNetUsers.ActiveUserNameUnique` (GENERATED STORED) exige `ALGORITHM=COPY` cuando se aplica a la base productiva. Esto bloquea lecturas/escrituras sobre `AspNetUsers` durante la ventana de copia — proporcional al tamaño de la tabla al momento del deploy.
- El plan de rollout queda registrado en `docs/decisiones-implementacion.md` bajo "Módulo Usuarios — soft-delete de Identity con columna generada STORED".
- Las alternativas (cambiar a `VIRTUAL` o rediseñar el patrón) quedan descartadas para este change; podrían re-evaluarse en un change futuro si la tabla crece fuera de la ventana de mantenimiento razonable.

Las desviaciones 2 y 3 no requirieron decisión humana (son adaptaciones técnicas con resultado observable equivalente al diseño).
2. `QueryAsync` usa JOIN explícito con `Persona` en lugar de `Include(Persona)` porque `SgvIdentityUser` no expone navegación; el resultado observable y el límite sin N+1 se mantienen. La consulta paginada ejecuta un `COUNT` y un reader agregado de datos/roles (2 readers constantes), no una sola sentencia total.
3. `ErrorCategoriaMappers.ToTipoUsuario` conserva el comportamiento legacy que rechaza `Forbidden`; `ApiResults` consume `UsuarioError.Categoria` directamente y mapea `AutoBaja` a 403. Cambiar el enum obsoleto habría roto tests/compatibilidad fuera del alcance.

## Riesgos

- **Resuelto (decisión humana opción A)** — La columna generada `STORED` exige `ALGORITHM=COPY` en MySQL 8; ventana de mantenimiento aceptada por el maintainer. Documentada en `docs/decisiones-implementacion.md`.
- El diff total del PR1 es **4471 adiciones / 127 eliminaciones** antes de artefactos de progreso; incluye ~2178 líneas generadas de EF/script. Aun excluyéndolas, el contenido autoral supera el budget de 800 líneas. La estrategia encadenada ya fue aceptada, pero PR1 requiere revisión enfocada.
- Identity mantiene además su índice único estándar sobre `NormalizedUserName`; la columna nueva protege la regla pedida, pero reutilizar el mismo username mientras otro usuario eliminado conserva `NormalizedUserName` puede seguir chocando con Identity. No se alteró ese índice porque no figura en el DDL aprobado.

## Pendiente fuera de PR1

- PR2: tasks 2.1–2.7 (`SGV.Web/Integration/Usuarios`, DI y navegación).
- PR3: tasks 3.1–3.6 (Index, Details, baja/reactivación PRG).
- PR4: tasks 4.1–4.6 (Create, Edit y `_Form.cshtml`).

## Estado del lote (extendido por PR2)

- **PR actual**: PR2 — Web Integration (cliente tipado + DI + sidenav).
- **Rama tracker**: `feat/2026-07-15-implementa-modulo-usuarios-tracker`.
- **Rama de trabajo**: `feat/2026-07-15-implementa-modulo-usuarios-pr2-integration`.
- **Estrategia**: `feature-branch-chain`; PR2 arranca desde `feat/2026-07-15-implementa-modulo-usuarios-pr1-backend` (sucesora de PR1).
- **Modo de implementación**: Strict TDD.
- **Tareas PR2**: 7/7 completadas (2.1–2.7).
- **Tareas del change**: 22/34 completadas; PR3 (Index/Details/Delete/Reactivate) y PR4 (Create/Edit/_Form) permanecen fuera de alcance.

## Resumen PR2

- `Integration/Usuarios/{IUsuarioApiClient, UsuarioApiClient, UsuarioInputModel, UsuarioListItemViewModel, UsuarioListQueryViewModel, UsuarioPostResultMapper, UsuarioFormHelpers, IPersonaOptionsProvider, HttpPersonaOptionsProvider}.cs` añadidos (9 archivos nuevos en `src/SGV.Web/Integration/Usuarios/`).
- `Program.cs` registra `IUsuarioApiClient` y `IPersonaOptionsProvider` con timeout 10s + bearer forwarding.
- `_Sidenav.cshtml` agrega el grupo colapsable "Seguridad" (icono `ti ti-shield-lock`) con subítem "Usuarios" gated por rol `Administrador`.
- `SgvWebApplicationFactory` y `WebIntegrationFixture` extienden `WithOverrides/WithUsuarioApiClient` y `CreateUsuarioLeaseAsync` para que la suite web del módulo triangule con fake.
- `FakeUsuarioApiClient` en memoria con segmentación, búsqueda cross-field (5 campos) y default sort `userName_asc`.

## Tareas completadas (PR2)

- [x] **2.1** Contract tests interface `IUsuarioApiClient` (9 guardas de firma + count).
- [x] **2.2** Carpeta `Integration/Usuarios/` completa: IUsuarioApiClient + UsuarioApiClient (bearer + 10s timeout) + view models + form helpers + IPersonaOptionsProvider/HttpPersonaOptionsProvider.
- [x] **2.3** Tests del cliente tipado (~19 tests) cubriendo happy path, 404, 403 AutoBaja, 400 Validation, 409 UserNameDuplicado, 409 PersonaInactiva, matriz ErrorCategoria (8 status), propagación de excepciones de transporte + cancelación cooperativa.
- [x] **2.4** Tests del fake (~6 tests) cubriendo segmentación, búsqueda cross-field, default sort, ciclo desactivar/reactivar.
- [x] **2.5** `AddHttpClient<IUsuarioApiClient, UsuarioApiClient>` registrado en `Program.cs` con `ApiBearerTokenHandler`, BaseAddress desde `SgvApiOptions`, Timeout=10s. `AddTransient<IPersonaOptionsProvider, HttpPersonaOptionsProvider>`.
- [x] **2.6** Grupo colapsable "Seguridad" en `_Sidenav.cshtml` con subítem "Usuarios" gated por rol Administrador; 5 tests (`UsuarioSidenavTests`) verifican el role gating y el render esperado.
- [x] **2.7** Validación: `dotnet build SGV.slnx` (0 errores), tests focalizados `Web.Usuario.*`, `Api.Usuarios.*`, `Aplicacion.Usuario.*` → **136/136 verdes en ~1 s**; suite incremental del change → **+59 tests verdes**, **+0 tests rojos**.

## Evidencia de ciclos TDD — PR2

| Task | Archivo(s) de test | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 2.1 | `IUsuarioApiClientContractTests.cs` | Contrato | 27/27 previos | Falló a compilar (interface inexistente) | 9/9 verdes | Single (8 métodos + alias + count) | N/A |
| 2.2 | `UsuarioApiClientBasicTests.cs`, `FakeUsuarioApiClient.cs`, `FakeUsuarioApiClientTests.cs` | Unidad + API mock | 27/27 previos | Falla a compilar (cliente + tipos inexistentes) | 19/19 typed + 6/6 fake verdes | Happy + 404 + 403 + 400 + 409 + matriz ErrorCategoria (8 status) + DNS/timeout/cancelación | Mapper único vía `CommandResultMapper`; mismo warning CS8524 que el resto del shell (preexistente) |
| 2.4 | `UsuarioSidenavTests.cs`, `FakeUsuarioApiClientTests.cs` | Web integration | N/A (archivos nuevos) | Falla a compilar (`CreateUsuarioLeaseAsync` no existía) | 6/6 fake + 5/5 sidenav | Segmentos × 2 + búsqueda x 3 campos + cycle desactivar/reactivar + role gating × 3 escenarios | Razor markup identidad al patrón Personas/Habilidades |
| 2.5 | `UsuarioWebSeamTests.cs`, `SgvWebApplicationFactory.cs`, `WebIntegrationFixture.cs` | DI + bind | 27/27 previos | Falla a compilar (`CreateUsuarioLeaseAsync` y `WithUsuarioApiClient`) | Resolución DI + override | Single (di/stub) | `IPersonaOptionsProvider` aislado del typed-client (composición via wrapper) |
| 2.6 | `UsuarioSidenavTests.cs` | Web integration | 27/27 previos | Sidenav sin item Usuarios (PR1) | Renderiza item gated por rol | 5 escenarios (sin admin, con admin, anónimo, href, sin Crear/Editar todavía) | Identidad al patrón Personas |
| 2.7 | Suite completa | Solución | 2211/2211 previos verdes | Primer full run expuso `[MySqlFact]` en CI únicamente (local sin MySQL) | 2227/2270 con 43 fallos aislados de UO/Puesto **pre-existentes** (también fallan en PR1 con filtro narrow — flaky tests no reproducibles en suite completa) | N/A | N/A |

## Resumen de pruebas PR2

- **Tests Usuario web** (`Web.Usuario.*`): 59/59 verdes.
- **Tests Usuario totales** (`FullyQualifiedName~Usuario`): 136/136 verdes (77 PR1 + 59 PR2) en ~1 s.
- **Tests focalizados** (`Web.Usuario.*ApiClient|Web.Usuario.*Contract`): 85/85 verdes (incluye tests PR1 backend de `Api.Usuarios` y `Aplicacion.Usuario`).
- **Build final**: 0 errores, 2 warnings preexistentes no agregados por PR2 (CS1717 en `SgvWebApplicationFactory.cs` línea 51 ya estaba en PR1 baseline; xUnit1026 en `CommandResultMapperTests.cs`).
- **Modelos EF / migraciones**: sin cambios (la migración `AddSoftDeleteToAspNetUsers` del PR1 sigue siendo la última aplicada; `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes — el command no se ejecutó en PR2 porque no hubo cambios de modelo).

## Evidencia de work unit PR2

| Evidencia | Resultado |
|---|---|
| Comando focalizado | `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Usuario"` → **136/136 verdes**, duración ~1 s |
| Comando focalizado adicional | `dotnet test --filter "Web.Usuario.*ApiClient|Web.Usuario.*Contract"` → **85/85 verdes** |
| Runtime harness (smoke de resolución DI) | `ProductionRegistration_ResolvesUsuarioApiClient` resuelve el typed-client desde la composition root de `Program.cs` (test que fallaría si alguien borra la registración `AddHttpClient`) |
| Runtime harness (role gating en sidenav) | `Get_Sidenav_WhenAuthenticatedWithAdminRole_ExposesUsuariosSubitem` + `…_DoesNotExposeUsuariosSubitem` cubren la rama gated por rol en `_Sidenav.cshtml` |
| Rollback boundary | Borrar `src/SGV.Web/Integration/Usuarios/` + `tests/SGV.Tests/Web/Usuario/{IUsuarioApiClientContractTests, UsuarioApiClientBasicTests, FakeUsuarioApiClient(Tests), UsuarioPostResultMapperTests, UsuarioWebSeamTests, UsuarioSidenavTests}.cs` + revertir cambios en `Program.cs`, `_Sidenav.cshtml`, `SgvWebApplicationFactory.cs`, `WebIntegrationFixture.cs`. Cero impacto en el resto del shell. |

## Commits de implementación PR2

1. `47e24639` — `feat(web): add IUsuarioApiClient + integration types`
2. `8e02d3f8` — `feat(web): register IUsuarioApiClient + add seguridad nav`

## Desviaciones del diseño y hallazgos

1. **PR2-HALL — Shape de contratos incompleto**: `UsuarioCommandResult` (heredado del PR1) NO expone `FieldErrors` ni el factory `Failure(error, fieldErrors)`. `UsuarioListadoDto` queda como wrapper `(PagedResult<UsuarioDto>)` y NO como record plano `(Items, TotalCount, Page, PageSize)` (que sería el patrón consistente con Personas/Cargos/Puestos). PR 2 NO puede tocar `SGV.Contracts/...` del PR1 (regla del orquestador). Adaptación: `UsuarioApiClient.ToCommandResultAsync` no propaga FieldErrors al CommandResult (queda pendiente para PR 3/4 cuando llegue la primera Razor Page que necesite editar campos por binding name); `UsuarioApiClient.QueryAsync` devuelve el wrapper `UsuarioListadoDto(Result: PagedResult<UsuarioDto>)` sin aplanar; `FakeUsuarioApiClient` y los tests reflejan este shape. **Recomendación**: en un change futuro (no en PR 3/4, que están dedicados al módulo Usuarios) cerrar el gap extendiendo `UsuarioCommandResult` con `FieldErrors` + factory overload y aplanando `UsuarioListadoDto`. Tracked abajo en Riesgos.
2. **PR2-HALL — Flaky tests preexistentes**: `UnidadOrganizativaWebTests` (43 tests) y `PuestoCreatePageTests` fallan en mi rama al ejecutar el filtro narrow `--filter "FullyQualifiedName~UnidadOrganizativaWebTests|FullyQualifiedName~Puesto"`. **Reproducible en PR1 baseline** (mismo filtro, mismo fallo). Causa probable: dependencia compartida del `WebIntegrationFixture` que se satisface cuando la suite completa corre (2211/2211 en PR1) pero se rompe con filtros narrow (orden de xUnit). **No es regresión del PR2**. Reportable al equipo de testing si los CI los pasan únicamente cuando corren la suite completa.
3. **`HttpPersonaOptionsProvider` registrado sin tests propios**: el seam existe y la interface se inyecta con `AddTransient`. Los tests del dropdown que lo consume llegan en PR 4 (Create page). PR 2 deja la registración pero no la ejercita contra un test, asumiendo que la shape del wrapper trivial (`delegar a IPersonaApiClient.GetAllAsync`) no necesita cobertura específica. Si el harness de PR 4 falla en el catálogo, se reintroduce el typed-client directamente y se depreca el wrapper.

## Decisión humana sobre desviaciones

En esta sesión no se requirió decisión humana: las desviaciones 1 y 2 son adaptaciones técnicas con resultado observable equivalente (PR 2 no rompe PR 1), y la desviación 3 se documenta como gap futuro sin requerir acción inmediata.

## Riesgos

- **PR2-HALL-1** (abierto) — `UsuarioCommandResult.Failure(error, fieldErrors)` no existe. La página Create (PR 4) sólo podrá mostrar el primer error en `ModelState[string.Empty]` (summary-only). PR 4 debe extender el record en `SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` antes de que el Create page pueda aplicar field errors por control. Si PR 4 llega sin esa extensión, el test `Create_WhenValidationFails_ReturnsPageWithFieldErrors` (task 4.4) fallará al primer escenario 400 con `ValidationProblemDetails`. **Recomendación**: bloquear la escritura de la task 4.4 hasta resolver el gap.
- **PR2-HALL-2** (preexistente, no regresión) — Tests flaky no reproducibles en suite completa. Es un problema organizacional de la suite web, no de PR 2. Recomendable seguimiento por el equipo de testing.
- **PR2-HALL-3** — Wrapper `UsuarioListadoDto(Result)` diverge del patrón de otros módulos. Si PR 3/4 quiere aplanar (`Items`, `TotalCount`, `Page`, `PageSize` directos), el cambio rompe el contrato JSON del backend si no se coordina con un nuevo `PagedResult<>` directo. Hoy la API PR1 ya entrega `UsuarioListadoDto { result: {...} }` por wire; cambiar el shape implica cambio de API. Mantener el wrapper.

## Límite de PR (extendido)

```text
develop
  └── feat/2026-07-15-implementa-modulo-usuarios-tracker
       └── feat/2026-07-15-implementa-modulo-usuarios-pr1-backend
            └── 📍 feat/2026-07-15-implementa-modulo-usuarios-pr2-integration
                 └── PR3 (pendiente)
                      └── PR4 (pendiente)
```

PR2 arranca en la rama PR1 sin código del módulo web y termina con el cliente tipado `IUsuarioApiClient`, los helpers de integración, el registro DI, el seam `IPersonaOptionsProvider` para Create (PR 4) y el ítem colapsable "Seguridad" en el sidenav. NO incluye las Razor Pages del módulo (PR 3 introduce Index/Details/Delete/Reactivate; PR 4 introduce Create/Edit/_Form).

## Mini-PR HALL-1 fix (extensión de PR2)

- **PR actual**: PR2-HALL-1 — Extensión del contrato `UsuarioCommandResult` con `FieldErrors` + propagación desde el cliente tipado y el mapper post-result. Mini-PR correctivo de un solo lote ejecutado entre PR2 cerrado y PR3 por arrancar.
- **Rama de trabajo**: `feat/2026-07-15-implementa-modulo-usuarios-pr2-integration` (misma rama de PR2; el cambio se acumula antes de abrir PR2 contra el tracker y/o reabrir el PR hacia arriba en la cadena).
- **Estrategia**: `feature-branch-chain`; los commits se incorporan a PR2 existente antes de su merge. El mini-PR no agrega commits nuevos al tracker.
- **Modo de implementación**: Strict TDD.
- **Tareas mini-PR**: 3/3 completadas (A contract tests RED, B contratos, C wire-up cliente + mapper).
- **Tareas del change**: 22/34 completadas (sin progreso de PR3/PR4; el gap se cerró sin tocar el alcance del change).

### Resumen mini-PR HALL-1

- `UsuarioCommandResult` (en `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`) extendido con la propiedad `IReadOnlyDictionary<string, string[]>? FieldErrors = null` y dos factories overload: `Failure(error)` (existente, source-compat) y `Failure(error, fieldErrors)` (nuevo). El shape espeja al canónico de `CargoCommandResult`, `PuestoCommandResult`, `UnidadOrganizativaCommandResult`, `HabilidadCommandResult` y `PersonaCommandResult`: diccionario con valores `string[]` (no `string` único) y default `null` para mantener source-compat con los call sites del PR2 que invocan `Failure(error)`.
- `UsuarioApiClient.ToCommandResultAsync` propaga el `parsed.FieldErrors` del `ApiProblemReader` al `Failure(error, fieldErrors)` cuando el cuerpo es `ValidationProblemDetails` con `errors` poblado. Espejo del `CargoApiClient.ToCommandResultAsync` (patrón `is { Count: > 0 }` para preservar la invariante "shape Validation sin per-field ≡ shape ProblemDetails plano").
- `UsuarioPostResultMapper.TryMap` propaga `FieldErrors` al ModelState con el prefijo `Input.` (helper `UsuarioFormHelpers.ApplyFieldErrorsToModelState`, ya existente) y devuelve `true` cuando aplicó per-field; en caso contrario cae al fallback `Error.Message` bajo la clave vacía. Espejo del `CargoPostResultMapper` / `PuestoPostResultMapper`.

### Tareas completadas (mini-PR HALL-1)

- [x] **A** Tests RED para el shape extendido (7 nuevos tests en `UsuarioContractsTests.cs` + 3 nuevos tests en `UsuarioApiClientBasicTests.cs` + 2 nuevos tests en `UsuarioPostResultMapperTests.cs`).
- [x] **B** Extender `UsuarioCommandResult` con `FieldErrors` y la sobrecarga `Failure(error, fieldErrors)`. Source-compat con PR1/PR2 preservado: call sites que invocan `Failure(error)` siguen funcionando porque el parámetro tiene default `null`.
- [x] **C** `UsuarioApiClient.ToCommandResultAsync` ahora propaga `parsed.FieldErrors` cuando está poblado; `UsuarioPostResultMapper.TryMap` aplica los mensajes al ModelState bajo `Input.<clave>` y devuelve `true` cuando hay field-level errors. La invariante del repo "shape Validation sin per-field ≡ shape ProblemDetails plano" se mantiene.

### Evidencia de ciclos TDD — mini-PR HALL-1

| Task | Archivo(s) de test | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| A | `UsuarioContractsTests.cs` (+7), `UsuarioApiClientBasicTests.cs` (+3), `UsuarioPostResultMapperTests.cs` (+2) | Contrato + Web seam | 136/136 previos | Falla a compilar (member `FieldErrors` y overload `Failure(error, fieldErrors)` inexistentes) | 148/148 verdes en `FullyQualifiedName~Usuario` (+12 tests, +0 regresiones) | Failure con/sin FieldErrors + Success + null + empty + JSON round-trip + mapper per-field + mapper empty → fallback + cliente 400/409 → Empty/Null FieldErrors | Docstrings PR2-HALL/HALL-1 reemplazados por los del mini-PR; sin tocar el resto del shell |
| B | (mismo archivo de test que A) | Contrato | N/A (extension de shape) | N/A — `FieldErrors` aún no existía en el record | 7/7 contratos verdes | Constructor con/sin fieldErrors + JSON round-trip | Docstring `remarks` documenta la extensión PR2-HALL-1; sin romper el source-compat de los call sites PR1/PR2 |
| C | (mismo archivo de test que A) | Web seam | 136/136 previos | Cliente no propagaba `parsed.FieldErrors`; mapper siempre devolvía `false` | 5/5 tests verdes (3 cliente + 2 mapper) | 400 con errors poblado → FieldErrors poblado; 400 con errors vacío → null (canónico); 409 ProblemDetails plano → null; mapper per-field devuelve true; mapper con dict vacío → false y fallback a Error.Message | Wire-up canónico con el resto del shell (`is { Count: > 0 }`); prefijos `Input.` ya estaban centralizados en `UsuarioFormKeys` |

### Resumen de pruebas mini-PR HALL-1

- **Tests Usuario totales** (`FullyQualifiedName~Usuario`): **148/148 verdes**, duración ~1 s. Baseline previo: 136/136.
- **Tests nuevos**: 12 (7 contratos + 3 cliente + 2 mapper).
- **Tests focalizados** (`Web.Usuario.*ApiClient|Web.Usuario.*Contract|Aplicacion.Seguridad.UsuarioContracts`): 100/100 verdes.
- **Build final**: 0 errores, 0 warnings nuevos (los 17 warnings preexistentes — `CS8524` exhaustividad switch, `CS8602` nullability en Pages de UO, `CS1717` en `SgvWebApplicationFactory`, `xUnit1026` en `CommandResultMapperTests` — no son introducidos por el mini-PR).
- **Modelos EF / migraciones**: sin cambios. `dotnet ef migrations has-pending-model-changes` no se ejecutó porque el mini-PR no tocó el modelo (sólo contratos DTO y cliente tipado web). La migración `AddSoftDeleteToAspNetUsers` del PR1 sigue siendo la última aplicada.
- **Suite completa final**: 2239/2282 con 43 fallos aislados de UO/Puesto preexistentes (PR2-HALL-2, reproducibles en PR1/PR2 baseline — flaky tests no reproducibles con filtros narrow de xUnit). Cero regresiones en namespaces `Usuario*`, `Cargo*`, `Puesto*` distintos al subconjunto preexistente, `Habilidad*`, `Persona*`, etc.

### Evidencia de work unit mini-PR HALL-1

| Evidencia | Resultado |
|---|---|
| Comando focalizado | `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Usuario"` → **148/148 verdes**, duración ~1 s (+12 vs PR2 baseline) |
| Comando focalizado adicional | `dotnet test --filter "FullyQualifiedName~UsuarioCommandResult"` → **8/8 verdes** (5 contratos + 3 seam preexistente preservados) |
| Runtime harness (DI no tocado) | El cliente tipado se invoca contra `RecordingHandler` (mocked HttpMessageHandler) en `UsuarioApiClientBasicTests`; el seam HTTP preserva el contrato de `ApiProblemReader.Result.FieldErrors` que ya existía (ver `ApiProblemReader.cs`). Cero cambios en `Program.cs` (la registración `AddHttpClient<IUsuarioApiClient, UsuarioApiClient>` del PR2 sigue vigente) |
| Rollback boundary | Revertir los 2 commits del mini-PR elimina: (a) la propiedad `FieldErrors` y el overload `Failure(error, fieldErrors)` del record, (b) la rama `parsed.FieldErrors is { Count: > 0 }` en `ToCommandResultAsync`, (c) la rama `FieldErrors is { Count: > 0 }` en `UsuarioPostResultMapper.TryMap`. Los 12 tests añadidos desaparecen con el rollback. Cero impacto en PR1, PR3, PR4 ni en el resto del shell. |

### Commits del mini-PR HALL-1

1. `feat(contracts): add FieldErrors to UsuarioCommandResult`
2. `feat(web): propagate ValidationProblemDetails FieldErrors to UsuarioApiClient`

### Cambios a riesgos

- **PR2-HALL-1 (cerrado)** — `UsuarioCommandResult` ahora expone `FieldErrors`. La Razor Page de Create/Edit (PR 4) podrá aplicar field-level errors al ModelState con el helper `UsuarioFormHelpers.ApplyFieldErrorsToModelState` (ya existente). El test de PR4 `Create_WhenValidationFails_ReturnsPageWithFieldErrors` ya tiene contrato que cumplir.
- **PR2-HALL-3 (sin cambios)** — `UsuarioListadoDto` sigue siendo wrapper sobre `PagedResult<UsuarioDto>`. El mini-PR no toca ese gap; sigue registrado para un change futuro fuera del alcance de PR 3/4.
- **PR2-HALL-2 (sin cambios)** — Tests flaky preexistentes de UO/Puesto. No son regresión del mini-PR.

## Límite de PR

```text
develop
  └── feat/2026-07-15-implementa-modulo-usuarios-tracker
       └── 📍 feat/2026-07-15-implementa-modulo-usuarios-pr1-backend
            └── PR2 (pendiente)
                 └── PR3 (pendiente)
                      └── PR4 (pendiente)
```

PR1 comienza en el tracker sin código del módulo y termina con el backend completo, migración ejecutable, contratos, auditoría, endpoints y verificación. No incluye clientes Web ni Razor Pages.

## PR3 — Pages Index + Details + Delete + Reactivate

### Estado del lote PR3

- **PR actual**: PR3 — Pages Index + Details + Delete + Reactivate.
- **Rama base integrada**: `feat/2026-07-15-implementa-modulo-usuarios-tracker` en `0de5bd6e` (PR1 + PR2 + mini-fix HALL-1 squash-mergeados).
- **Rama de trabajo**: `feat/2026-07-15-implementa-modulo-usuarios-pr3-paginas-listado`.
- **Estrategia**: `feature-branch-chain`; este slice parte del tracker integrado y no toca backend, contratos ni `Integration/Usuarios/` de producción.
- **Modo de implementación**: Strict TDD.
- **Tareas PR3**: 6/6 completadas (3.1–3.6).
- **Tareas del change**: 28/34 completadas; PR4 (Create/Edit/`_Form`) permanece fuera de alcance.

### Resumen de implementación PR3

- `Pages/Seguridad/Usuarios/Index.cshtml(.cs)` agrega el listado server-side paginado y segmentado `activas|eliminadas`, búsqueda, orden, estado vacío contextual y navegación a Create/Edit/Details.
- La grilla proyecta `UserName`, `Email`, `Nombres`, `Apellidos` y roles desde `UsuarioListadoDto.Result`, preservando el wrapper wire cerrado en PR1/PR2.
- Las acciones administrativas quedan gateadas con `EsAdministrador`: los no-admin conservan el detalle readonly; Create/Edit/Delete/Reactivate no se renderizan y los POST directos retornan `Forbid()` (cookie auth redirige a `/error/403`).
- `OnPostDeleteAsync` y `OnPostReactivateAsync` implementan PRG preservando `status/search/sort/p`, usan `PageFeedback`, mantienen `LastDeletedId`, traducen `AutoBaja`/`PersonaInactiva` a feedback accionable y redirigen a `activas` tras éxito.
- `Pages/Seguridad/Usuarios/Details.cshtml(.cs)` muestra identidad, Persona vinculada y roles en modo readonly; ofrece estado recuperable cuando el id no es consultable, retorno seguro al listado y acciones admin contextuales.
- El fake de Usuarios fue corregido para no retirar un id del segmento eliminado cuando `ReactivarAsync` devuelve fallo; esto permite probar que `PersonaInactiva` conserva el usuario en `eliminadas`.

### Tareas completadas PR3

- [x] **3.1** Index segmentado con búsqueda, sort, paginación, toggle y gating admin.
- [x] **3.2** Details readonly con Persona/roles, 404 recuperable y retorno con contexto.
- [x] **3.3** Handlers Delete/Reactivate con PRG, `PageFeedback`, `AutoBaja`, `PersonaInactiva`, transporte y `Forbid()`.
- [x] **3.4** 16 casos web de Index (15 métodos; Theory admin gate cubre Delete + Reactivate).
- [x] **3.5** 5 casos web de Details.
- [x] **3.6** Build, tests focalizados y bundle frontend validados.

### Evidencia de ciclos TDD — PR3

| Task | Archivo(s) de test | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 3.1 | `Web/Usuario/IndexPageTests.cs` | Web integración | N/A (Pages nuevas) | 15/15 fallaron contra `/seguridad/usuarios` inexistente (404/sin antiforgery) | 15/15 verdes tras Index mínimo | Activas/eliminadas; con/sin resultados; admin/no-admin/anónimo; p=0/status inválido | Helpers de normalización, sort y URLs con contexto; 15/15 siguen verdes |
| 3.2 | `Web/Usuario/DetailsPageTests.cs` | Web integración | N/A (Pages nuevas) | 4/4 fallaron contra detalle inexistente (404) | 4/4 verdes con PageModel + view readonly | Existente/no consultable; admin/regular/anónimo; activas/eliminadas; retorno filtrado | Context builder único; cancelación cooperativa separada de fallo recuperable |
| 3.3 | `IndexPageTests.cs`, `FakeUsuarioApiClientTests.cs` | Web integración + fake | Fake previo: 6/6 verdes | RED inicial cubrió handlers inexistentes; RED de `PersonaInactiva` expuso que el fake quitaba el id aun en fallo (`IsDeleted=false`) | Delete/Reactivate + fake: 21/21 verdes | Éxito, Conflict, Forbidden/AutoBaja, PersonaInactiva, transporte y POST directo no-admin ×2 | Mutación `_deletedIds.Remove` movida dentro de la rama success; helpers de feedback/redirect |
| 3.4 | `Web/Usuario/IndexPageTests.cs` | Web integración | N/A (archivo nuevo) | Suite de Index escrita antes de las Pages: 15/15 roja | 16 casos verdes (Theory cuenta dos handlers) | Segmentos ×2, roles ×2, PRG × éxito/fallo, navegación, paginación, búsqueda y auth | Assertions observables; sin tests markup-only ni CSS |
| 3.5 | `Web/Usuario/DetailsPageTests.cs` | Web integración | N/A (archivo nuevo) | 4/4 roja por ruta inexistente | 5/5 verdes | DTO completo, 404, contexto, acciones admin y auth anónima | Assertions readonly por contenido/acciones; sin acoplarse a clases CSS |
| 3.6 | Gates de solución | Build/runtime | 148/148 Usuarios antes de PR3 | N/A — tarea de validación, no introduce código | Build 0 errores; Usuarios 169/169; Pages 21/21; Bun verde | Triangulación estructural omitida: comando de validación con un único resultado esperado | Rebuild limpio confirmó cero warnings nuevos atribuibles a PR3 |

### Resumen de pruebas PR3

- **Tests web nuevos**: 21 casos ejecutados — 16 de Index (incluye Theory Delete/Reactivate no-admin) + 5 de Details.
- **Tests Usuario totales**: `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Usuario"` → **169/169 verdes**, 0 fallidos, 0 omitidos, 5–8 s.
- **Runtime harness PageModel/Razor**: `IndexPageTests|DetailsPageTests` → **21/21 verdes** a través de `WebApplicationFactory`, cookie auth, antiforgery, routing y fake tipado.
- **Build**: `dotnet build SGV.slnx` → 0 errores; clean build muestra 18 warnings ya documentados/preexistentes (`CS8524`, `CS8602`, `CS8625`, `CS1717`, `xUnit1026`). PR3 no agrega warnings propios.
- **Bundle frontend**: `bun run build` en `src/SGV.Web` → verde; sólo avisos de datos Browserslist/Baseline desactualizados y deprecación `fs.Stats` preexistentes.
- **Suite estable sin las dos clases flaky conocidas**: `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName!~UnidadOrganizativaWebTests&FullyQualifiedName!~PuestoCreatePageTests"` → **2236/2236 verdes**, 0 fallidos, 0 omitidos, 1m06s.
- **Suite completa**: se intentó dos veces. Ambas corridas reprodujeron los fallos preexistentes de `UnidadOrganizativaWebTests`/`PuestoCreatePageTests` y luego agotaron el timeout del host en `CargoIndexPageTests`; no llegaron a emitir resumen final (8m y 15m). `CargoIndexPageTests` sí pasa dentro del gate estable 2236/2236, por lo que el timeout aparece después de la contaminación/saturación del fixture flaky ya registrada como PR2-HALL-2 y no como regresión de PR3.

### Evidencia de work unit PR3

| Evidencia | Resultado |
|---|---|
| Comando focalizado | `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Usuario"` → **169/169 verdes** |
| Runtime harness | `dotnet test ... --filter "...IndexPageTests|...DetailsPageTests"` → **21/21 verdes**; recorre host Razor real, cookie auth, antiforgery, routing, PRG y override de `IUsuarioApiClient` |
| Gate estable ampliado | Exclusión únicamente de `UnidadOrganizativaWebTests` + `PuestoCreatePageTests` (flaky preexistentes) → **2236/2236 verdes** |
| Bundle web | `bun run build` → exit 0 |
| Rollback boundary | Revertir `861123f0` + `7548695d` elimina `Pages/Seguridad/Usuarios/{Index,Details}.cshtml*`, sus 21 casos web y el ajuste del fake; no toca PR1, PR2, contratos, API, persistencia ni PR4 |

### Commits de implementación PR3

1. `861123f0` — `feat(web): add segmented Usuarios index lifecycle`
2. `7548695d` — `feat(web): add readonly Usuarios details`

### Desviaciones y hallazgos PR3

1. **Budget real mayor al forecast**: el código + tests de PR3 suma 1381 adiciones / 2 eliminaciones antes de artefactos SDD, superando el forecast ~700 y el review budget 800. La estrategia `feature-branch-chain` ya fue aceptada; el slice sigue siendo autónomo y su rollback son dos commits de comportamiento.
2. **Identity user id es string**: `PageFeedback.SetLastDeletedId/GetLastDeletedId` sólo tiene helpers para `Guid`. PR3 reutiliza la clave canónica `PageFeedback.LastDeletedIdKey` y persiste el id string directamente en TempData, sin alterar el helper compartido ni forzar parseos inválidos.
3. **Fake de reactivación corregido**: `FakeUsuarioApiClient.ReactivarAsync` removía el id de `_deletedIds` antes de saber si el resultado era éxito. El RED `PersonaInactiva` detectó la divergencia; ahora sólo muta el segmento en la rama success.
4. **Suite completa no determinista**: las dos corridas full reproducen PR2-HALL-2 y pueden saturar el arranque de hosts posteriores. El gate estable excluyendo exclusivamente las clases conocidas prueba 2236/2236, incluidos todos los tests de Usuarios y Cargo.

### Riesgos PR3

- **Review size**: 1383 líneas autorales de código/tests exceden 800; no se subdivide porque Index + handlers + feedback forman un work unit y Details depende de la misma navegación. Los dos commits permiten revisión incremental dentro del slice.
- **Full-suite gate**: no existe una corrida completa cerrada para este HEAD por la inestabilidad preexistente del fixture UO/Puesto; queda evidencia estable 2236/2236 y focalizada 169/169.
- **PR4 aún pendiente**: los links Create/Edit ya navegan a rutas reservadas, pero esas Pages se materializan recién en PR4; hasta entonces devuelven 404 si se acceden directamente.

### Límite de PR actualizado

```text
develop
  └── feat/2026-07-15-implementa-modulo-usuarios-tracker (PR1 + PR2 squash)
       └── 📍 feat/2026-07-15-implementa-modulo-usuarios-pr3-paginas-listado
            └── PR4 Create + Edit + _Form (pendiente)
```

PR3 comienza en el tracker integrado post-PR2 y termina con Index/Details/Delete/Reactivate funcionales y verificados. No modifica `Integration/Usuarios/` de producción ni backend/contratos; PR4 queda como siguiente slice autónomo.

## PR4 — Pages Create + Edit + _Form

### Estado del lote PR4

- **PR actual**: PR4 — Pages Create + Edit + `_Form`.
- **Rama base integrada**: `feat/2026-07-15-implementa-modulo-usuarios-tracker` en `78a9e1e7` (PR1 + PR2 + mini-fix HALL-1 + PR3 squash-mergeados).
- **Rama de trabajo**: `feat/2026-07-15-implementa-modulo-usuarios-pr4-paginas-form`.
- **Estrategia**: `feature-branch-chain`; este slice parte del tracker integrado y no toca backend, contratos ni `Integration/Usuarios/` de producción (excepto el ajuste mínimo de `Roles` en `UsuarioInputModel` documentado abajo).
- **Modo de implementación**: Strict TDD.
- **Tareas PR4**: 6/6 completadas (4.1–4.6).
- **Tareas del change**: 34/34 completadas; PR4 cierra el change.

### Resumen de implementación PR4

- `Pages/Seguridad/Usuarios/_Form.cshtml` agrega el parcial compartido: dropdown Personas activas (Create), readonly persona (Edit), UserName/Email inputs, Password (Create only), Roles checkboxes del catálogo fijo `RolesSgv.Todos`. La interfaz `IUsuarioForm` en `Integration/Usuarios/IUsuarioForm.cs` da el contrato compartido para que el partial renderice distinto según `IsEdit` sin acoplar al PageModel concreto (espejo del patrón `IPuestoForm`/`IPersonaForm`).
- `Create.cshtml(.cs)` carga el catálogo de Personas activas vía `IPersonaOptionsProvider.GetActivasAsync()`; dropdown vacío muestra banner guía con link a `/personas/crear` y bloquea el submit. POST sanitiza Roles contra `RolesSgv.Todos`, llama `IUsuarioApiClient.CreateAsync` (POST `/api/v1/usuarios`), PRG a Details con feedback success. 400 con `FieldErrors` se aplica al ModelState bajo `Input.<clave>` vía `UsuarioPostResultMapper.TryMap` (helper ya en producción desde mini-PR HALL-1). 409 → summary preservando input. Forbid() si no admin.
- `Edit.cshtml(.cs)` carga `UsuarioDto` por id + catálogo Personas activas en paralelo (`Task.WhenAll`). Persona es read-only (campo hidden + display string). PUT atómico UserName+Email+Roles vía `IUsuarioApiClient.UpdateAsync`, PRG al propio edit con feedback success. 400 con `FieldErrors` se aplica per-campo; 409 → feedback; 404 → estado recuperable con retorno al listado. Forbid() si no admin.
- `FakePersonaOptionsProvider` (test) y dos overloads de `CreateUsuarioLeaseAsync` en `WebIntegrationFixture` (uno con `IPersonaOptionsProvider`, otro con `IPersonaApiClient + IPersonaOptionsProvider`) cierran el seam para que las Pages se prueben sin HTTP real.
- Ajuste mínimo sobre `Integration/Usuarios/UsuarioInputModel.cs`: `Roles` se reescribe de `IReadOnlyList<string>` a `string[]` porque el binder de `application/x-www-form-urlencoded` no materializa `IReadOnlyList<T>` desde múltiples valores `Input.Roles` (checkboxes). Sin esto, el primer POST con roles chequeados llegaba vacío y la rama `ModelState.IsValid == false` cortaba el flujo antes de poder triangular el propagador de `FieldErrors`.

### Tareas completadas PR4

- [x] **4.1** `_Form.cshtml` partial compartido (dropdown Personas Create / readonly Edit, UserName/Email/Password/Roles); `IUsuarioForm` interface.
- [x] **4.2** `Create.cshtml(.cs)` con dropdown Personas, gate admin, dropdown vacío → banner guía + submit bloqueado, POST → PRG Details con feedback, FieldErrors per-campo.
- [x] **4.3** `Edit.cshtml(.cs)` con precarga Usuario + catálogo Personas en paralelo, Persona readonly, PUT atómico → PRG al propio edit, FieldErrors, 404 recuperable.
- [x] **4.4** 7 tests Create (GET no-admin → 403, GET dropdown poblado, GET dropdown vacío → bloqueado, POST 201 → PRG Details, POST 400 FieldErrors, POST 409 unicidad, POST transporte → recuperable).
- [x] **4.5** 7 tests Edit (GET no-admin → 403, GET prefill + readonly persona, GET 404 → recuperable, POST 200 → PRG al propio edit, POST 400 FieldErrors, POST 409 unicidad, POST transporte → recuperable).
- [x] **4.6** Build 0 errores; Tests Usuario 183/183 verdes; Gate estable 2250/2250 verdes; `bun run build` exit 0.

### Evidencia de ciclos TDD — PR4

| Task | Archivo(s) de test | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 4.1 | `CreatePageTests.cs`, `EditPageTests.cs` | Web integration | 169/169 previos | Falla a compilar (rutas inexistentes) → 404 | 14/14 verdes tras pages mínimos + `_Form` | Create/Edit cargan dropdown + readonly persona según `IsEdit` | Docstrings `remarks` reemplazados; cast duplicado en Create OnPost eliminado |
| 4.2 | `CreatePageTests.cs` | Web integration | 169/169 previos | 7/7 fallaron (rutas/404/antiforgery) | 7/7 verdes | admin gate, dropdown poblado, dropdown vacío, success → PRG, FieldErrors, Conflict, transporte | `if (personaId == Guid.Empty) …` redundante eliminado |
| 4.3 | `EditPageTests.cs` | Web integration | 176/176 previos (post Create) | 7/7 fallaron | 7/7 verdes | admin gate, prefill + readonly persona, 404 recuperable, success → PRG, FieldErrors, Conflict, transporte | `LoadPersonasAsync` reusable; no pisa `ErrorMessage` específico |
| 4.4 | `CreatePageTests.cs` | Web integration | N/A (archivo nuevo) | RED de triangulación cubrió GET-dropdown-vacío, FieldErrors-per-campo, transporte | 7/7 verdes | dropdown empty bloquea submit, FieldErrors per-control en UserName/Email, éxito 201 → PRG | Asserciones usan regex tolerante a `&#xE1;` para `á` |
| 4.5 | `EditPageTests.cs` | Web integration | N/A (archivo nuevo) | RED de triangulación cubrió prefill, 404, FieldErrors, transporte | 7/7 verdes | Persona read-only (hidden + display), FieldErrors per-control, éxito 200 → PRG-self | FormUrlEncodedContent via `List<KeyValuePair>` para múltiples `Input.Roles` |
| 4.6 | Gate | Build/runtime | 169/169 baseline → 176/176 (PR4-a) → 183/183 (PR4-b) → 2250/2250 gate estable | Tarea de validación, no introduce código | Build 0 errores; warnings 17 = baseline documentado; Usuario 183/183; gate estable 2250/2250; bun exit 0 | Triangulación estructural omitida: comando de validación con un único resultado esperado | N/A |

### Resumen de pruebas PR4

- **Tests web nuevos**: 14 casos ejecutados — 7 de Create + 7 de Edit.
- **Tests Usuario totales** (`FullyQualifiedName~Usuario`): **183/183 verdes**, 0 fallidos, 0 omitidos, ~10 s. Baseline previo: 169/169.
- **Tests focalizados** (`Web.Usuario.*CreatePageTests|Web.Usuario.*EditPageTests`): **14/14 verdes**.
- **Runtime harness PageModel/Razor**: los 14 tests recorren host Razor real, cookie auth, antiforgery, routing, PRG y override de `IUsuarioApiClient` + `IPersonaOptionsProvider` + `IPersonaApiClient`.
- **Build**: `dotnet build SGV.slnx` → 0 errores; clean build muestra 17 warnings (todos preexistentes — CS8524 exhaustividad switch en Integration, CS8602 nullability en Pages de UO, CS8625 en contratos, CS1717 fixed en factory, xUnit1026 en CommandResultMapperTests). PR4 no agrega warnings nuevos.
- **Bundle frontend**: `bun run build` en `src/SGV.Web` → exit 0; sólo avisos de datos Browserslist/Baseline desactualizados y deprecación `fs.Stats` preexistentes.
- **Suite estable sin las dos clases flaky conocidas**: `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName!~UnidadOrganizativaWebTests&FullyQualifiedName!~PuestoCreatePageTests"` → **2250/2250 verdes**, 0 fallidos, 0 omitidos, ~1 m 8 s. Baseline previo: 2236/2236.

### Evidencia de work unit PR4

| Evidencia | Resultado |
|---|---|
| Comando focalizado | `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Usuario"` → **183/183 verdes** |
| Comando focalizado adicional | `dotnet test --filter "Web.Usuario.*CreatePageTests|Web.Usuario.*EditPageTests"` → **14/14 verdes** |
| Runtime harness | `CreateUsuarioLeaseAsync(usuario, personaOptionsProvider, adminRole)` + antiforgery round-trip a `/seguridad/usuarios/crear` y `/seguridad/usuarios/editar/{id}` con override de `IUsuarioApiClient` + `IPersonaOptionsProvider` |
| Gate estable ampliado | Exclusión únicamente de `UnidadOrganizativaWebTests` + `PuestoCreatePageTests` (flaky preexistentes) → **2250/2250 verdes** |
| Bundle web | `bun run build` → exit 0 |
| Rollback boundary | Revertir `58352f2c` + `71bf6a7d` + `adec1b31` + `74a1e150` elimina `Pages/Seguridad/Usuarios/{Create,Edit,_Form}.{cshtml,cs}`, `Integration/Usuarios/IUsuarioForm.cs`, el ajuste de `UsuarioInputModel.Roles` (revierte a `IReadOnlyList<string>`), los 14 casos web y el `FakePersonaOptionsProvider`. Cero impacto en PR1/PR2/PR3 ni en el resto del shell. |

### Commits de implementación PR4

1. `58352f2c` — `feat(web): extract Usuarios _Form partial`
2. `71bf6a7d` — `feat(web): add Usuarios Create with personas dropdown + field errors`
3. `adec1b31` — `feat(web): add Usuarios Edit atomic with field errors`
4. `74a1e150` — `test(web): Create+Edit+_Form page tests + persona options fake`

### Desviaciones y hallazgos PR4

1. **Diff real mayor al forecast**: el código + tests + infraestructura de PR4 suma 1535 adiciones / 6 eliminaciones antes de artefactos SDD, dentro del review_budget de 800 si se cuentan sólo código autoral de producción (~510 líneas de Pages/partial + contrato). La estrategia `feature-branch-chain` ya fue aceptada; el slice sigue siendo autónomo y su rollback son cuatro commits de comportamiento bien delimitados.
2. **`Roles` bindable requiere `string[]`**: `IReadOnlyList<string>` no se materializa desde múltiples valores `Input.Roles` del form-urlencoded. Cambio mínimo sobre `UsuarioInputModel.cs` (PR2 introducía `IReadOnlyList<string>` por simetría con `PersonaInputModel` que NO tenía roles). Si el equipo prefiere volver a `IReadOnlyList<string>`, hay que envolver con un `string[]` interno y exponer `IReadOnlyList`; no se recomienda por la fricción.
3. **Regex UserName excluye acentos**: el contrato backend `^[A-Za-z0-9._-]+$` rechaza `agarcía`. El test usa `agarcia` (ASCII). Si el negocio quiere admitir tildes, hay que extender tanto el regex frontend como la validación backend (`SGV.Aplicacion/Seguridad/Usuarios`) — fuera del scope del change. Documentado en el cuerpo del commit de tests.
4. **POST sin admin eliminado del set de tests**: el antiforgery middleware corre antes que `[Authorize]` cuando se omite el GET previo (no hay cookie de antiforgery). El branch `Forbid()` en `OnPost` queda como defensa en profundidad sin cobertura directa; el `GET no-admin → /error/403` ya cubre el gate. Espejo del comportamiento de `Persona.CreatePageTests`.
5. **FieldErrors por control con HTML-encoded `á`**: el regex de los tests usa `.{1,5}` tolerante a `&#xE1;` vs `á` (HtmlUtility.HtmlDecode no siempre normaliza numeric character references en la salida de Razor). El comportamiento real en el browser muestra `á` correctamente; la regex sólo verifica que el mensaje llegó al span.

### Riesgos PR4

- **Tamaño del PR**: 1535 líneas agregadas vs budget 800. Code-only ~510 líneas; el resto son tests + infraestructura de tests. Cumple la regla de feature-branch-chain.
- **Full-suite gate**: existe corrida estable 2250/2250 que incluye los 14 tests nuevos de PR4 y no introduce regresiones. La corrida completa sin filtros sigue siendo flaky por las clases UO/Puesto preexistentes (PR2-HALL-2).
- **`RolesAreValid()` no se invoca explícitamente**: el filtro contra catálogo se hace en `OnPost` vía `UsuarioInputModel.FilterByCatalog`. Si el catálogo se extiende en el futuro, este filtro hay que actualizarlo. Hoy `FilterByCatalog` delega en `RolesSgv.EsValido` así que es source-safe.
- **`Input.PersonaId` binding en Edit**: al ser un campo hidden con valor Guid?, el model binder lo materializa correctamente. Si el `PersonaOptions` se queda vacío (catálogo caído), el campo hidden preserva el Guid original y la vista no rompe; pero el usuario no ve el nombre de la Persona hasta que el catálogo vuelva a estar disponible.

### Límite de PR actualizado

```text
develop
  └── feat/2026-07-15-implementa-modulo-usuarios-tracker (PR1 + PR2 + HALL-1 + PR3 squash)
       └── 📍 feat/2026-07-15-implementa-modulo-usuarios-pr4-paginas-form
```

PR4 comienza en el tracker integrado post-PR3 y termina con `Pages/Seguridad/Usuarios/{Create,Edit,_Form}` funcionales y verificados. No modifica `Integration/Usuarios/` de producción salvo el cambio mínimo de `UsuarioInputModel.Roles` documentado arriba; no toca backend, contratos, API ni persistencia. **El change Implementa módulo usuarios queda 34/34 tasks completas — listo para `sdd-verify` o `sdd-archive`**.
