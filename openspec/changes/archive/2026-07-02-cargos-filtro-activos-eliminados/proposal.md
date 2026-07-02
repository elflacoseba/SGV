# Proposal: Agregar en el listado de cargos el filtro para ver los Activos o los Eliminados

## 1. Resumen ejecutivo

El listado web de Cargos ya existe, pero hoy solo muestra activos y resuelve búsqueda/orden/paginación en memoria sobre `GET /api/v1/cargos`, sin toggle de segmento ni acción de reactivación. (`openspec/changes/cargos-filtro-activos-eliminados/exploration.md`, `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`, `src/SGV.Api/Controllers/CargosController.cs`)
Este cambio propone replicar en Cargos el patrón ya archivado para Unidades Organizativas: segmento binario `activas`/`eliminadas`, query paginado explícito, reactivación por fila y preservación de contexto en la UI. (`openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
El resultado esperado es una sola página `Index` capaz de alternar entre activos y eliminados sin mezclar conjuntos, reutilizando el `PATCH /api/v1/cargos/{id}/reactivar` ya existente. (`openspec/changes/cargos-filtro-activos-eliminados/exploration.md`, `src/SGV.Api/Controllers/CargosController.cs`)

## 2. Contexto y motivación

Hoy la UI de `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` está hardcodeada a “Listado de cargos activos”, no preserva `status` en links/forms y solo expone `?handler=Delete`. (`openspec/changes/cargos-filtro-activos-eliminados/exploration.md`, `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`)
El backend ya soporta soft-delete y reactivación, pero no tiene una consulta segmentada ni un endpoint `consulta` para eliminados, así que la web no puede construir esa vista sin coordinar contrato + UI. (`src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`, `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`, `src/SGV.Api/Controllers/CargosController.cs`)
Eso duele en operación porque un cargo eliminado deja de ser visible en el listado y la recuperación queda fuera del flujo principal, a diferencia de Unidades Organizativas. (`openspec/changes/cargos-filtro-activos-eliminados/exploration.md`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)

## 3. Alcance (in scope)

- **Backend**: extender `ICargoServicioConsulta`, `CargoServicioConsulta`, `ICargoRepository`, `CargoRepository`, DTOs/query objects y `src/SGV.Api/Controllers/CargosController.cs` para soportar consulta paginada segmentada activas/eliminadas. (`src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`, `src/SGV.Api/Controllers/CargosController.cs`)
- **Web / PageModel**: actualizar `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` con `Segmento`, `IsDeletedView`, `OnPostReactivateAsync`, normalización de `status` y preservación de contexto en rutas/forms. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
- **Web / Razor**: agregar toggle Activas/Eliminadas, render condicional de acciones, alerts con `TempData`, hidden `status` y conservación de `search`/`sort`/`p` en `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- **Cliente web**: extender `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`, `CargoApiClient.cs` y `CargoListItemViewModel.cs` para query segmentado y reactivación. (`src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`, `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`, `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs`)
- **JS**: ampliar `src/SGV.Web/wwwroot/js/pages/cargos-index.js` con confirmación SweetAlert2 para reactivar usando selectores `data-cargo-reactivate-*`. (`src/SGV.Web/wwwroot/js/pages/cargos-index.js`, `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js`)
- **Tests**: cubrir aplicación, persistencia MySQL, API y web para segmento, reactivación y preservación de contexto. (`tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs`, `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`, `tests/SGV.Tests/Api/CargosControllerTests.cs`, `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`)

## 4. Fuera de alcance (non-goals)

- No modificar `Details`, `Edit` ni `Create` de Cargos para operar sobre eliminados. (`src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml.cs`, `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs`, `src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml.cs`)
- No cambiar el modelo de soft-delete ni la unicidad activa basada en `ActiveCodigoUnique`. (`src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`, `docs/decisiones-implementacion.md`)
- No introducir una vista mixta con badge de estado; el segmento sigue siendo binario y exclusivo. (`openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`)
- No tocar permisos/autorización fuera de los contratos ya vigentes en `CargosController`. (`src/SGV.Api/Controllers/CargosController.cs`, `openspec/specs/cargo-management/spec.md`)
- No migrar a paginación cursor-based; se mantiene el modelo paginado server-side del patrón de Unidades Organizativas. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)

## 5. Enfoque propuesto

- **Segmento**: usar query string `status=eliminadas`, `Segmento` e `IsDeletedView`; solo `eliminadas` activa la vista eliminada y cualquier otro valor cae a activas. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
- **Backend**: introducir un enum/record `CargoSegmentoListado` análogo al de Unidades Organizativas; `ICargoServicioConsulta.QueryAsync(...)` y `CargoRepository.QueryAsync(...)` deben filtrar `IsActive && !IsDeleted` vs `!IsActive && IsDeleted`; exponer `GET /api/v1/cargos/consulta?status=eliminadas&p=&pageSize=&search=&sort=`. (`openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`, `src/SGV.Api/Controllers/CargosController.cs`)
- **Web**: reemplazar la paginación en memoria de `LoadAsync` por consumo server-side del query paginado; `OnPostReactivateAsync` redirige a activas en éxito y conserva eliminadas en falla; `TempData` mantiene `StatusMessage`, `StatusKind` y `LastDeletedId`. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
- **Razor**: copiar el toggle Activas/Eliminadas, ocultar “Crear” en eliminadas, renderizar “Eliminar” solo en activas y “Reactivar” solo en eliminadas, preservando `status` en orden, paginación, búsqueda y POST. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- **JS**: agregar un segundo wire `data-cargo-reactivate-*` replicando el patrón de `unidades-organizativas-index.js` con copy específico de Cargos. (`src/SGV.Web/wwwroot/js/pages/cargos-index.js`, `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js`)
- **Tests**: agregar cobertura tipo `QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas`, MySQL para no mezcla, API para `status=eliminadas` y web para toggle, botones y redirect. (`tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs`, `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`, `tests/SGV.Tests/Api/CargosControllerTests.cs`, `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`)

## 6. Decisiones abiertas

- **(a) Endpoint** — Recomendación: `GET /api/v1/cargos/consulta`. Razonamiento: alinea naming y costo cognitivo con `UnidadesOrganizativasController`; impacto: menor fricción en cliente, tests y Swagger. (`src/SGV.Api/Controllers/UnidadesOrganizativasController.cs`, `openspec/changes/cargos-filtro-activos-eliminados/exploration.md`)
- **(b) Paginación** — Recomendación: migrar a server-side. Razonamiento: la página actual pagina en memoria sobre `GetAllAsync`, lo que rompe la simetría con eliminadas y escala peor; impacto: cambia contrato web/API, pero baja complejidad accidental en `IndexModel`. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)
- **(c) Query DTO** — Recomendación: `CargoSegmentoListado` explícito en aplicación/controller y `status` string solo en borde HTTP/Web. Razonamiento: evita strings sueltos y replica el patrón ya validado; impacto: más tipos, menos ambigüedad. (`src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs`, `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`)

## 7. Impacto y dependencias

Sin este cambio, el módulo web de Cargos queda inconsistente con Unidades Organizativas: permite baja lógica, pero no recuperar ni inspeccionar eliminados desde el flujo principal. (`tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
La dependencia principal es reutilizar `PATCH /api/v1/cargos/{id}/reactivar` y sumar un contrato de lectura segmentada sin tocar dominio ni esquema. (`src/SGV.Api/Controllers/CargosController.cs`, `docs/decisiones-implementacion.md`)

## 8. Criterios de éxito

- `dotnet test SGV.slnx` pasa con nuevos casos de aplicación, MySQL, API y web. (`openspec/config.yaml`)
- La UI permite alternar Activas/Eliminadas y no mezcla conjuntos. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`, `openspec/changes/cargos-filtro-activos-eliminados/exploration.md`)
- Reactivar desde eliminadas vuelve a activas en éxito y conserva el segmento en falla. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
- No hay regresión en delete, detalle, edit ni contratos de soft-delete/reactivación existentes. (`openspec/specs/cargo-web-listado-detalle-baja/spec.md`, `openspec/specs/cargo-management/spec.md`)

## 9. Riesgos

- **Medio / técnico**: dejar `GetAllAsync` + paginación en memoria y sumar eliminadas encima duplicaría reglas de filtrado y contexto. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)
- **Medio / UX**: no preservar `status` en links/forms puede mandar al usuario al segmento equivocado tras delete/reactivate. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- **Medio / cobertura**: sin tests MySQL y web equivalentes al patrón de referencia, es fácil mezclar activos/eliminados o romper redirects. (`tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs`, `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs`)
- **Bajo / auditoría**: las nuevas queries deben respetar el comportamiento actual de trazabilidad sin alterar el interceptor. (`src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs`)

## 10. Referencias

- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`
- `openspec/specs/cargo-web-listado-detalle-baja/spec.md`
- `openspec/specs/cargo-management/spec.md`
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`
- `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`
- `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`
- `src/SGV.Web/wwwroot/js/pages/cargos-index.js`
- `src/SGV.Api/Controllers/CargosController.cs`
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs`
- `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`
- `tests/SGV.Tests/Api/CargosControllerTests.cs`
- `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`
