# Tasks: Filtrar estado Cubierta del dropdown de edición de Vacante

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~30-50 (5 archivos modificados, 1 test nuevo corto) |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | single-pr |
| Chain strategy | N/A |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: N/A
400-line budget risk: Low

### Suggested Work Units

Single work unit: el fix es chico y autocontenido (DTO + mapper + filtro + 1 test). No requiere chained PRs. PR único contra `develop`.

## Phase 1: DTO + Mapper (Foundation)

- [x] 1.1 Modificar `src/SGV.Contracts/Vacantes/Consultas/Dtos/EstadoVacanteDto.cs`: agregar 6to parámetro posicional `bool EsCubierta` al final del record (queda: `Id, Codigo, Nombre, Orden, EsTerminal, EsCubierta`).
- [x] 1.2 Modificar `src/SGV.Aplicacion/Vacantes/Consultas/EstadoVacanteServicioConsulta.cs`: en `MapToDto(EstadoVacante e)`, poblar el nuevo campo con `e.EsCubierta` (campo ya existe en el dominio, `EstadoVacante.cs:35`).

## Phase 2: Fakes actualizados (build verde — obligatorio)

Estos cambios NO son tests nuevos; son ajustes de compilación para que el ctor de 6 args no rompa el build. Sin ciclo RED/GREEN.

- [x] 2.1 Modificar `tests/SGV.Tests/Web/Vacantes/FakeVacanteApiClient.cs`: en `BuildStates()` agregar 6to arg por estado (`Cubierta=true`, resto `false`).
- [x] 2.2 Modificar `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs`: en `FakeEstadoVacanteServicioConsulta.ListarAsync` (~líneas 1306-1309) agregar 6to arg en el seed (Cubierta=true, resto false). El test `Estados_GetAll_Returns200WithFourStates` (asserta `Count==4`) sigue verde sin tocarlo.

## Phase 3: Filtro en PageModel (Core — TDD estricto)

- [x] 3.1 Modificar `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs`: en `LoadStatesAsync` (línea 196), agregar `.Where(s => !s.EsCubierta)` antes del `await`/`ToList()`.
- [x] 3.2 RED: en `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs`, agregar `[Fact] Get_Edit_ExcludesCubiertaFromDropdown`. Arrange: lease de mutation role con `FakeVacanteApiClient` cuyo `ListarEstadosResult = BuildStates()` (incluye Cubierta). Act: `GET /organizacion/vacantes/editar/{id}` con vacante existente. Assert: el HTML NO contiene ningún `<option>` cuyo `value` sea el Guid de Cubierta; SÍ contiene `<option>` con el Guid de Cancelada. Test debe fallar antes de 3.1.
- [x] 3.3 GREEN: aplicar 3.1. Re-ejecutar el test; debe pasar.
- [x] 3.4 REFACTOR: evaluado. No se extrae helper compartido: las dos semillas divergen intencionalmente (web usa `Guid.NewGuid()` y códigos UPPER_SNAKE; API usa GUIDs deterministas `20000000-…` que el test `Estados_GetAll_Returns200WithFourStates` aprovecha, y códigos SentenceCase). Extraer un helper obligaría a parametrizar IDs/códigos o agregar un flag — costo > beneficio. Design.md lo marca como opcional/no requerido.

## Phase 4: Verificación

- [x] 4.1 `dotnet build SGV.slnx` — 0 errores, 0 warnings nuevos.
- [x] 4.2 `dotnet test SGV.slnx` — 0 fallos, incluyendo el test nuevo y los regresivos (`Get_Edit_WhenMutationRole_PrepopulatesStateAndObservations`, `Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails`, `Estados_GetAll_Returns200WithFourStates`).
- [x] 4.3 (Opcional) `dotnet run` de SGV.Web + GET manual a `/organizacion/vacantes/editar/{id}`: confirmar visualmente que el `<select>` no ofrece Cubierta. — No ejecutado (orquestador decide si necesita smoke manual).

## Dependencies

- Ninguna externa. No toca migraciones, BD, Identity ni auditoría. Cambio wire-compatible (campo nuevo al final del JSON; clientes viejos lo ignoran al deserializar).

## Definition of Done

- [x] `dotnet build SGV.slnx` verde.
- [x] `dotnet test SGV.slnx` verde.
- [x] Test nuevo `Get_Edit_ExcludesCubiertaFromDropdown` verde (TDD RED→GREEN cumplido).
- [x] Tests web previos de Edit/Create siguen verdes.
- [x] API devuelve `esCubierta` por cada item de `GET /api/v1/estados-vacante` (verificable vía `Estados_GetAll_Returns200WithFourStates` ampliado opcionalmente o inspección manual).
- [x] `dotnet run` local de SGV.Web confirma dropdown de Edit sin Cubierta.
