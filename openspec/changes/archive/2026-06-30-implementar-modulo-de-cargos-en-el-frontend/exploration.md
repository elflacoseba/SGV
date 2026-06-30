## Exploración: Implementar el módulo de Cargos en el frontend

### Estado actual
- `SGV.Web` ya tiene un patrón funcional de módulo Razor Pages autenticado en `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/**`: PageModels delgados, cliente tipado por módulo, formularios parciales, feedback con `TempData` y confirmaciones JS en `wwwroot/js/pages`.
- La navegación del shell hoy solo expone `Home` y `Unidades Organizativas` desde `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml`; no existe ninguna entrada ni carpeta web para Cargos.
- El registro de dependencias de `src/SGV.Web/Program.cs` solo agrega `IAuthApiClient` e `IUnidadOrganizativaApiClient`, así que Cargos necesita su propio cliente HTTP tipado y su wiring.
- El backend de Cargos ya expone CRUD base en `src/SGV.Api/Controllers/CargosController.cs`: `GET /api/v1/cargos`, `GET /api/v1/cargos/{id}`, `POST`, `PUT`, `DELETE` y `PATCH /reactivar`, apoyado en `CargoDto` (`src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoDto.cs`) y `CrearCargoRequest` / `ActualizarCargoRequest` (`src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs`).
- El formulario web de Cargos puede abastecerse del catálogo `GET /api/v1/niveles-cargo` ya disponible en `src/SGV.Api/Controllers/NivelesCargoController.cs` y modelado por `NivelCargoDto`.
- Hay una capacidad adicional de backend para habilidades requeridas por cargo en `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills`, pero `SGV.Web` no tiene ningún cliente, página ni script asociado a ese subrecurso.
- Hay gaps relevantes entre backend y una UX equivalente a Unidades Organizativas: `CargoServicioConsulta` + `CargoRepository` solo listan activos; no existe query paginada, filtro de eliminados ni endpoint para obtener un cargo eliminado por id para detalle/edición/reactivación desde UI.
- Tampoco encontré en `src/SGV.Api/Controllers` un endpoint para listar `NivelesHabilidad`; eso bloquea una UI completa para alta/edición de skills requeridas sin ampliar backend.

### Áreas afectadas
- `src/SGV.Web/Program.cs` — registrar `ICargoApiClient` y, si entra skills, clientes auxiliares para catálogos relacionados.
- `src/SGV.Web/Integration/Organizacion/**` — referencia directa del patrón a replicar: interfaz + cliente HTTP + helpers de errores/ModelState.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/**` — módulo guía para estructura de rutas, PageModels, parciales compartidos y retorno al listado.
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` — agregar navegación de Cargos y estado activo del menú.
- `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js` — referencia para confirmaciones SweetAlert2 de baja/reactivación si Cargos mantiene esa UX.
- `src/SGV.Api/Controllers/CargosController.cs` — fuente real del contrato frontend actual y de sus limitaciones (sin consulta paginada ni listado de eliminados).
- `src/SGV.Api/Controllers/NivelesCargoController.cs` — catálogo ya disponible para poblar selects del formulario.
- `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` y `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` — patrón de pruebas web con cliente fake por módulo; un módulo de Cargos debería seguir el mismo seam.

### Enfoques
1. **Primer corte CRUD web de Cargos sin paridad total con Unidades Organizativas** — implementar listado simple de activos + create/edit/details + delete usando solo contratos backend ya disponibles, y dejar fuera la gestión visual de eliminados y de skills requeridas.
   - Pros: encaja con el backend actual sin pedir cambios previos en API; reutiliza casi 1:1 el patrón de `UnidadesOrganizativas`; mantiene bajo el riesgo de alcance.
   - Cons: el listado no tendría paginación ni segmento de eliminados; la reactivación quedaría sin flujo descubrible; el módulo no cubriría skills requeridas aunque el backend ya exista.
   - Esfuerzo: Medio.

2. **Módulo de Cargos con UX paritaria y administración de skills** — además del CRUD, agregar vista de eliminados/reactivación y sección de habilidades requeridas similar a un subrecurso administrable.
   - Pros: entrega un módulo más completo y alineado con las capacidades de negocio de Cargos.
   - Cons: hoy requiere ampliar backend antes o durante la propuesta: consulta/listado de eliminados, acceso a cargo eliminado y catálogo de `NivelesHabilidad`; también aumenta bastante el volumen de pruebas web.
   - Esfuerzo: Alto.

### Recomendación
Avanzar con el enfoque 1 como primer slice del frontend. La base más sólida es copiar la arquitectura ya estabilizada en `UnidadesOrganizativas` — cliente tipado, Razor Pages protegidas, parcial de formulario y pruebas con fake client — pero recortando el alcance a lo que el backend de Cargos realmente soporta hoy sin inventar flujos. Si el usuario quiere reactivación visible por UI o gestión de skills requeridas, eso debería salir explícitamente en propuesta/spec como extensión dependiente de nuevos contratos backend.

### Riesgos
- Si se intenta clonar la UX de `Unidades Organizativas`, el cambio se descarrila: Cargos no tiene hoy `consulta` paginada ni segmento/listado de eliminados.
- La gestión web de skills requeridas está incompleta desde contrato de catálogos: existe `/api/v1/cargos/{cargoId}/skills`, pero no un endpoint API inspeccionado para listar `NivelesHabilidad`.
- `DELETE /api/v1/cargos/{id}` puede fallar por puestos activos (`CargoConPuestosActivos` en `CargoServicioComandos.cs`); la UI debe contemplar feedback de conflicto, no solo éxito.
- El review budget de 400 líneas puede romperse rápido si se mezcla navegación, páginas Razor, cliente HTTP, scripts y pruebas web en un solo corte.

### Listo para propuesta
Sí — con una advertencia clara: la propuesta no debería prometer paridad con `Unidades Organizativas` ni administración de skills de Cargo salvo que primero se acepte ampliar los contratos backend faltantes.
