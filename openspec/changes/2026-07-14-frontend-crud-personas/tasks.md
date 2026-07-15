# Tasks: Frontend CRUD de Personas

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~2.500 – 3.200 |
| 800-line budget risk | High |
| 400-line budget risk | High |
| Chained PRs recommended | Sí |
| Suggested split | PR 1 (backend + wire-types) → PR 2 (integration + DI) → PR 3 (pages + nav) → PR 4 (tests web + docs) |
| Delivery strategy | ask-always |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Sí
Chained PRs recommended: Sí
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Wire-types en `SGV.Contracts.Personas` + mover records `Aplicacion→Contracts` | PR 1 | Base: `feature/frontend-crud-personas`; incluye backend `/consulta` y tests backend |
| 2 | Integration client + DI + navegación | PR 2 | Base: PR 1; independiente de UI |
| 3 | Razor Pages Index/Create/Edit/Details + typeahead | PR 3 | Base: PR 2 |
| 4 | Tests web + documentación | PR 4 | Base: PR 3 |

## Criterios de entrada

- [ ] Backend de Personas operativo en `SGV.Api` (`POST/PUT/PATCH/DELETE/GET {id}`).  
- [ ] Patrón de Cargos/Unidades Organizativas consultable como referencia.  
- [ ] Decisión de ruta confirmada: `/personas` (no `/organizacion/personas`).  
- [ ] SDK .NET 10.0.300 y Bun disponibles.  
- [ ] Base de datos MySQL accesible para tests `[MySqlFact]`.

## Tasks

### Fase 1: Wire-types y migración de contratos

- [ ] **1.1** [WU1] Crear wire-types de consulta en `SGV.Contracts.Personas` (30 min)  
  - Archivos: `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaDto.cs`, `PersonaListQuery.cs`, `PersonaSegmentoListado.cs`, `PersonaListadoDto.cs`  
  - Dependencias: ninguna  
  - Validación: `dotnet build SGV.Contracts` compila; shape JSON coincide con `SGV.Aplicacion.Personas.PersonaDto`  
  - Notas TDD: test de contrato no aplica; validar serialización STJ con test snapshot si aplica.

- [ ] **1.2** [WU1] Crear wire-types de comandos en `SGV.Contracts.Personas` (30 min)  
  - Archivos: `src/SGV.Contracts/Personas/Comandos/CrearPersonaRequest.cs`, `ActualizarPersonaRequest.cs`, `PersonaErrorType.cs`, `PersonaCommandResult.cs`, `PersonaDeleteResult.cs`  
  - Dependencias: 1.1  
  - Validación: `dotnet build SGV.Contracts`; `PersonaCommandResult` incluye `FieldErrors`  
  - Notas TDD: mismo shape JSON que records actuales de `Aplicacion`.

- [ ] **1.3** [WU2] Mover records de `SGV.Aplicacion.Personas` a `SGV.Contracts.Personas` (60 min)  
  - Archivos: eliminar `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaDto.cs`, `src/SGV.Aplicacion/Personas/Comandos/PersonaCommandResult.cs`, `PersonaRequests.cs`; actualizar `using` en `PersonaServicioComandos.cs`, `PersonaServicioConsulta.cs`, `PersonaSkill*`, `PersonasController.cs`  
  - Dependencias: 1.1, 1.2  
  - Validación: `dotnet build SGV.slnx` sin errores; tests existentes de Personas pasan  
  - Notas TDD: ejecutar suite existente antes y después; no cambiar comportamiento.

### Fase 2: Backend paginado `/consulta`

- [ ] **2.1** [WU3] Agregar `QueryAsync` al repositorio de Personas (60 min)  
  - Archivos: `src/SGV.Aplicacion/Personas/Consultas/IPersonaRepository.cs`, `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaRepository.cs`  
  - Dependencias: 1.3  
  - Validación: `dotnet build`; método soporta `search` 5 campos, `sort` 8 valores, segmento `activas|eliminadas`  
  - Notas TDD: escribir test `[MySqlFact]` primero que falle por método inexistente.

- [ ] **2.2** [WU3] Agregar `ListarAsync` al servicio de consulta (60 min)  
  - Archivos: `src/SGV.Aplicacion/Personas/Consultas/IPersonaServicioConsulta.cs`, `PersonaServicioConsulta.cs`  
  - Dependencias: 2.1  
  - Validación: `dotnet test SGV.Aplicacion.Tests` o suite unitaria; devuelve `PagedResult<PersonaDto>`  
  - Notas TDD: fake repo captura parámetros; test de paginación y sort.

- [ ] **2.3** [WU3] Exponer `GET /api/v1/personas/consulta` y ajustar `ApiResults` (60 min)  
  - Archivos: `src/SGV.Api/Controllers/PersonasController.cs`, `src/SGV.Api/Infrastructure/ApiResults/ApiResults.cs`, `src/SGV.Api/Infrastructure/Mappers/ErrorCategoriaMappers.cs`  
  - Dependencias: 2.2  
  - Validación: `dotnet build SGV.Api`; endpoint responde 200 con `PagedResult<PersonaDto>`; autenticación requerida  
  - Notas TDD: tests de controller con `SortCapturingFake` y casos 401/403.

### Fase 3: Tests backend

- [ ] **3.1** [WU4] Tests unitarios de `PersonaServicioConsulta.ListarAsync` (60 min)  
  - Archivos: `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioConsultaTests.cs`  
  - Dependencias: 2.2  
  - Validación: tests pasan; cubren paginación, sort, search, segmento  
  - Notas TDD: 6 tests parametrizados; evitar duplicados.

- [ ] **3.2** [WU4] Tests `[MySqlFact]` de `PersonaRepository.QueryAsync` (60 min)  
  - Archivos: `tests/SGV.Tests/Persistencia/PersonaRepositoryTests.cs`  
  - Dependencias: 2.1  
  - Validación: tests pasan contra MySQL con sembrado 3 activas + 2 eliminadas  
  - Notas TDD: 6 tests cubren filtros, sort y paginación.

- [ ] **3.3** [WU4] Tests de API para `PersonasController.GetConsulta` (60 min)  
  - Archivos: `tests/SGV.Tests/Api/PersonasControllerTests.cs`  
  - Dependencias: 2.3  
  - Validación: tests pasan; cubren 401, 200, paginación, search, sort y role gating  
  - Notas TDD: 6 tests; usar `ApiWebApplicationFactory`.

### Fase 4: Integration client

- [ ] **4.1** [WU5] Definir contrato e implementar `PersonaApiClient` (60 min)  
  - Archivos: `src/SGV.Web/Integration/Personas/IPersonaApiClient.cs`, `PersonaApiClient.cs`  
  - Dependencias: 1.2, 2.3  
  - Validación: `dotnet build SGV.Web`; `BaseRoute="/api/v1/personas"`; `QueryAsync`, `CreateAsync`, `UpdateAsync`, `GetByIdAsync`, `DeleteAsync`, `ReactivateAsync`  
  - Notas TDD: escribir `IPersonaApiClientContractTests` primero.

- [ ] **4.2** [WU5] Crear modelos y helpers de formulario (60 min)  
  - Archivos: `src/SGV.Web/Integration/Personas/PersonaInputModel.cs`, `PersonaListItemViewModel.cs`, `PersonaFormHelpers.cs`, `IPersonaForm.cs`  
  - Dependencias: 4.1  
  - Validación: `dotnet build`; `PersonaFormHelpers.ApplyFieldErrorsToModelState` mapea `FieldErrors` con prefix `Input.`  
  - Notas TDD: test unitario de `ApplyFieldErrorsToModelState`.

- [ ] **4.3** [WU5] Implementar `PersonaPostResultMapper` (30 min)  
  - Archivos: `src/SGV.Web/Integration/Personas/PersonaPostResultMapper.cs`  
  - Dependencias: 4.2  
  - Validación: `dotnet build`; traduce `PersonaCommandResult` a `PostResult`  
  - Notas TDD: test unitario con casos Success, Conflict, Validation.

### Fase 5: Registro y navegación

- [ ] **5.1** [WU6] Registrar `IPersonaApiClient` en DI y ajustar timeout (30 min)  
  - Archivos: `src/SGV.Web/Program.cs`  
  - Dependencias: 4.1  
  - Validación: `dotnet build`; `AddHttpClient<IPersonaApiClient, PersonaApiClient>` con `ApiBearerTokenHandler`  
  - Notas TDD: test de seam opcional.

- [ ] **5.2** [WU10] Agregar ítem de navegación "Personas" en sidenav (30 min)  
  - Archivos: `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml`  
  - Dependencias: 5.1  
  - Validación: `dotnet build`; ítem visible con icono `ti ti-user` y ruta `/personas`  
  - Notas TDD: test web verifica presencia del link.

### Fase 6: Razor Pages

- [ ] **6.1** [WU7] Implementar `Index.cshtml` + `Index.cshtml.cs` + JS de paginación (90 min)  
  - Archivos: `src/SGV.Web/Pages/Personas/Index.cshtml`, `Index.cshtml.cs`, `wwwroot/js/pages/personas-index.js`  
  - Dependencias: 4.3, 5.1  
  - Validación: `dotnet build`; grilla 8 columnas, toggle Activas/Eliminadas, paginación, búsqueda, orden, PRG Delete/Reactivate  
  - Notas TDD: tests de IndexPage: 10 tests.

- [ ] **6.2** [WU8] Implementar `_Form.cshtml` y `Create.cshtml` + `Create.cshtml.cs` (90 min)  
  - Archivos: `src/SGV.Web/Pages/Personas/_Form.cshtml`, `Create.cshtml`, `Create.cshtml.cs`  
  - Dependencias: 4.2, 5.1  
  - Validación: `dotnet build`; formulario con campos requeridos; POST 201 redirige a Details con TempData; 409 mapea campo afectado  
  - Notas TDD: tests de CreatePage: 6 tests.

- [ ] **6.3** [WU8] Implementar `Edit.cshtml` + `Edit.cshtml.cs` (90 min)  
  - Archivos: `src/SGV.Web/Pages/Personas/Edit.cshtml`, `Edit.cshtml.cs`  
  - Dependencias: 6.2  
  - Validación: `dotnet build`; carga datos existente; POST 200 redirige a Edit con TempData; 404 recuperable  
  - Notas TDD: tests de EditPage: 6 tests.

- [ ] **6.4** [WU8] Implementar `Details.cshtml` + `Details.cshtml.cs` (60 min)  
  - Archivos: `src/SGV.Web/Pages/Personas/Details.cshtml`, `Details.cshtml.cs`  
  - Dependencias: 4.1, 5.1  
  - Validación: `dotnet build`; readonly; retorno al listado preserva `p/search/sort/status`; 404 recuperable  
  - Notas TDD: tests de DetailsPage: 4 tests.

- [ ] **6.5** [WU9] Implementar typeahead reutilizable `_PersonaTypeahead.cshtml` (60 min)  
  - Archivos: `src/SGV.Web/Pages/Personas/Shared/_PersonaTypeahead.cshtml`  
  - Dependencias: 4.1, 5.1  
  - Validación: `dotnet build`; filtro client-side ≥2 caracteres; expone hook de selección; no depende de módulo Usuarios  
  - Notas TDD: tests de Typeahead: 3 tests.

### Fase 7: Tests web

- [ ] **7.1** [WU11] Crear fixtures y fakes para tests web de Personas (60 min)  
  - Archivos: `tests/SGV.Tests/Web/Persona/PersonaWebTestFixture.cs`, `FakePersonaApiClient.cs`, `PersonaApiClientBasicTests.cs`, `IPersonaApiClientContractTests.cs`  
  - Dependencias: 4.1, 4.3, 5.1  
  - Validación: tests pasan; fake cubre escenarios de listado, command y 404/409  
  - Notas TDD: espejo `FakeCargoApiClient`.

- [ ] **7.2** [WU11] Tests de Index, Create, Edit, Details (90 min)  
  - Archivos: `tests/SGV.Tests/Web/Persona/IndexPageTests.cs`, `CreatePageTests.cs`, `EditPageTests.cs`, `DetailsPageTests.cs`  
  - Dependencias: 6.1, 6.2, 6.3, 6.4, 7.1  
  - Validación: tests pasan; cubren PRG, role gating, 409→field error, 404 recuperable, segmentación  
  - Notas TDD: usar `WebApplicationFactory` + fake.

- [ ] **7.3** [WU11] Tests de typeahead y web seam (60 min)  
  - Archivos: `tests/SGV.Tests/Web/Persona/TypeaheadTests.cs`, `PersonaWebSeamTests.cs`  
  - Dependencias: 6.5, 7.1  
  - Validación: tests pasan; cubren filtro ≥2 chars, hook de selección, transporte recuperable  
  - Notas TDD: 3 tests de typeahead + 8 seam tests.

### Fase 8: Documentación y cierre

- [ ] **8.1** [WU12] Actualizar `docs/decisiones-implementacion.md` (30 min)  
  - Archivos: `docs/decisiones-implementacion.md`  
  - Dependencias: 6.5, 7.3  
  - Validación: documenta ruta `/personas`, wire-types en `SGV.Contracts.Personas`, asunción de <500 activos para typeahead  
  - Notas TDD: N/A.

- [ ] **8.2** [WU12] Verificación final de build y test suite (30 min)  
  - Archivos: todo el cambio  
  - Dependencias: 7.2, 7.3, 8.1  
  - Validación: `dotnet build SGV.slnx` y `dotnet test SGV.slnx` pasan; `bun run build` en `src/SGV.Web` si aplica  
  - Notas TDD: suite completa como criterio de salida.

## Criterios de salida

- [ ] `dotnet build SGV.slnx` sin errores.  
- [ ] `dotnet test SGV.slnx` pasa (incluyendo tests `[MySqlFact]` si hay MySQL).  
- [ ] `SGV.Web` no referencia `SGV.Aplicacion.Personas`.  
- [ ] `/personas` lista, crea, edita, muestra detalle, desactiva y reactiva personas.  
- [ ] Typeahead reutilizable filtra ≥2 caracteres y no depende de módulo Usuarios.  
- [ ] PRG + feedback operan en escrituras; `?status=eliminadas` oculta acciones salvo Reactivar.  
- [ ] `docs/decisiones-implementacion.md` actualizado.

## Métricas estimadas

| Métrica | Valor |
|---------|-------|
| Total tasks | 26 |
| Duración total | ~20 – 24 h |
| Archivos nuevos | ~35 |
| Archivos modificados | ~15 |
| Líneas agregadas (est.) | ~2.000 |
| Líneas eliminadas (est.) | ~500 |
| Líneas cambiadas (est.) | ~2.500 |
| Tests nuevos (est.) | ~45 |
| Recomendación chained PRs | Sí: 4 PRs en feature-branch-chain |
| Review budget | 800 líneas (excede; requiere excepción o split) |

## Riesgos

| # | Riesgo | Probabilidad | Mitigación |
|---|--------|--------------|------------|
| 1 | Mover records de `Aplicacion→Contracts` toca ~15 archivos y puede romper serialización JSON | Media | Mantener shape idéntico; ejecutar `dotnet test` tras cada task |
| 2 | Typeahead carga todo el listado de personas activas; si hay >500 activas, primer GET pesa >100KB | Baja-Media | Documentar asunción en `docs/decisiones-implementacion.md`; plan de follow-up `/buscar?q=` |
| 3 | `GET /consulta` expone datos personales a cualquier autenticado; compliance puede requerir restricción futura | Baja | Mantener matriz de autorización actual; documentar decisión |
| 4 | Tamaño del cambio (>800 líneas) dificulta revisión en un solo PR | Alta | Usar feature-branch-chain con 4 PRs autónomos |
| 5 | Tests `[MySqlFact]` requieren MySQL local; sin él se skipean 146 tests, reduciendo confianza | Baja | Configurar `ConnectionStrings__SgvDatabase` o validar en CI |
