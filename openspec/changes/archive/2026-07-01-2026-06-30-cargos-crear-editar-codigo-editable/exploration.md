## Exploration: Implementa en el front crear/editar Cargos

### Contexto
- El cambio continúa el trabajo archivado en `openspec/changes/archive/2026-06-30-implementar-modulo-de-cargos-en-el-frontend/`: hoy `SGV.Web` ya expone `Cargos` con listado autenticado, detalle readonly y baja lógica, pero todavía no tiene create/edit.
- El patrón de referencia vigente para create/edit en el shell está en `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/**`: páginas Razor separadas, `PageModel` delgados, cliente HTTP tipado, PRG (`RedirectToPage` tras POST exitoso), `TempData` para feedback y `Partial` compartido para el formulario.
- El shell no usa modales para formularios administrativos. Los únicos modales/diálogos encontrados en estos módulos son confirmaciones SweetAlert2 para baja/reactivación (`wwwroot/js/pages/*.js`).
- No encontré wiring de validación client-side con jQuery Validate o `_ValidationScriptsPartial`; las pantallas actuales dependen de DataAnnotations + `ModelState`/`asp-validation-for`, o sea validación server-side con mensajes inline.

### Alcance preliminar
- Primer corte razonable: agregar create/edit de `Cargo` en `SGV.Web` reutilizando el backend ya existente (`POST /api/v1/cargos`, `PUT /api/v1/cargos/{id}` y `GET /api/v1/niveles-cargo`).
- Queda fuera de este cambio, salvo que el usuario lo pida explícitamente en proposal, sumar reactivación visible, gestión de skills requeridas, paginación server-side o vista de eliminados. Nada de eso es necesario para create/edit y mezclarlo rompe el scope.
- La edición debe respetar que `Codigo` es inmutable en backend: create lo envía, edit NO lo puede persistir. Hay que decidir en proposal si el campo se muestra deshabilitado/readonly en edit o si se reemplaza por texto readonly.

### Lo que ya existe (backend)
- `src/SGV.Api/Controllers/CargosController.cs` YA expone:
  - `POST /api/v1/cargos` → `CrearCargoRequest` → `201 Created` con `CargoDto`.
  - `PUT /api/v1/cargos/{id}` → `ActualizarCargoRequest` → `200 OK` con `CargoDto`.
  - `GET /api/v1/cargos/{id}` → detalle activo para precargar edit.
  - `GET /api/v1/niveles-cargo` y `GET /api/v1/niveles-cargo/{id}` → catálogo readonly para el selector de nivel.
- Contratos reales:
  - `CrearCargoRequest` (`src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs`): `codigo`, `nombre`, `nivelId`, `descripcion?`.
  - `ActualizarCargoRequest`: `nombre`, `nivelId`, `descripcion?` (sin `codigo`).
  - `CargoDto`: `id`, `codigo`, `nombre`, `descripcion`, `nivelId`, `nivelNombre`.
- Validaciones confirmadas en aplicación:
  - `codigo`: requerido, máx. 50 (`CrearCargoRequestValidator`).
  - `nombre`: requerido, máx. 200 (`CrearCargoRequestValidator`, `ActualizarCargoRequestValidator`).
  - `descripcion`: opcional, máx. 1000.
  - `nivelId`: obligatorio (`Guid.Empty` inválido).
- Reglas de negocio confirmadas en `CargoServicioComandos.cs`:
  - create falla con `CodigoDuplicado` si ya existe un activo con ese código.
  - create/update fallan con `NivelCargoNoExiste` si el catálogo no contiene el `nivelId`.
  - update falla con `CargoNoEncontrado` si el id no existe como activo.
  - los errores de validación salen con claves camelCase (`codigo`, `nombre`, `nivelId`) vía `ValidationProblemDetails`.
- Evidencia en tests:
  - `tests/SGV.Tests/Api/CargosControllerTests.cs` verifica `400` con `ValidationProblemDetails`, `409` por `CodigoDuplicado`, `404` por `CargoNoEncontrado`, y `200/201` exitosos.
  - `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs` verifica las mismas reglas y el uso de claves camelCase.

### Lo que ya existe (frontend)
- Páginas actuales de Cargos:
  - `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml(.cs)` → listado activo con búsqueda/sort/paginación en memoria, link a detalle y baja lógica confirmada.
  - `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml(.cs)` → detalle readonly con retorno al listado.
- Integración web actual:
  - `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` solo soporta `GetAllAsync`, `GetByIdAsync` y `DeleteAsync`.
  - `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` todavía NO implementa `CreateAsync`, `UpdateAsync` ni lectura de `ValidationProblemDetails` para formularios.
  - `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs` solo define `CargoListItemViewModel`, `CargoListQuery` y `CargoDeleteResult`; no existen `InputModel`, `CommandResult`, helpers de formulario ni opciones de nivel.
- Navegación shell:
  - `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` muestra `Cargos` con un único subitem `Listado`; no existe enlace a `Nuevo`.

### Patrón de referencia
- `UnidadesOrganizativas` usa páginas separadas, no modal:
  - `Create.cshtml(.cs)` carga catálogos en `OnGet`, hace POST, y ante éxito redirige al detalle con mensaje en `TempData`.
  - `Edit.cshtml(.cs)` reutiliza `_Form.cshtml`, recarga catálogos ante error y preserva contexto de retorno (`returnPage`, `returnSearch`, `returnSort`, etc.).
  - `Details.cshtml` ofrece CTA de edición y vuelta al listado.
- El formulario compartido vive en `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/_Form.cshtml` y se apoya en una interfaz (`IUnidadOrganizativaForm`) + `InputModel` con DataAnnotations.
- La traducción de errores backend se centraliza en helpers:
  - `UnidadOrganizativaApiClient` convierte `ValidationProblemDetails`/`ProblemDetails` a resultados web tipados.
  - `UnidadOrganizativaFormHelpers.ApplyFieldErrorsToModelState(...)` mapea claves camelCase del backend a `Input.<campo>`.
- Tests web de referencia:
  - `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` cubre auth, carga de catálogos, create success, validation/conflict, edit success, warning parcial y preservación de contexto.
  - `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` permite reemplazar el cliente tipado por un fake en pruebas.

### Áreas afectadas
- `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` — ampliar contrato web para create/update y probablemente catálogo de niveles.
- `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` — agregar `POST`, `PUT` y traducción de `ValidationProblemDetails`/`ProblemDetails`.
- `src/SGV.Web/Integration/Organizacion/**` — probablemente nuevos tipos: `CargoInputModel`, `CargoCommandResult`, `CargoError`, `INivelCargoApiClient` o extender el cliente de cargos para consultar niveles.
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` — agregar CTA visible de crear y acción por fila para editar.
- `src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml(.cs)` — nueva pantalla create.
- `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml(.cs)` — nueva pantalla edit con precarga desde `GET /api/v1/cargos/{id}`.
- `src/SGV.Web/Pages/Organizacion/Cargos/_Form.cshtml` — parcial compartido si se sigue el patrón del equipo.
- `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` — agregar CTA de editar manteniendo retorno al listado.
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` — agregar subitem `Nuevo` si se decide seguir el patrón de Unidades Organizativas.
- `tests/SGV.Tests/Web/Cargo/*` y/o `tests/SGV.Tests/Web/CargoWebTests.cs` — extender seams, fake client y pruebas de create/edit.

### Enfoques
1. **Replicar el patrón de Unidades Organizativas con páginas Create/Edit dedicadas**
   - Pros: consistente con el shell actual, simple de testear con `WebApplicationFactory`, aprovecha PRG, antiforgery y partial compartido.
   - Cons: requiere ampliar bastante el seam de Cargos (cliente, fake client, modelos y tests).
   - Esfuerzo: Medio.

2. **Resolver create/edit dentro del listado o detalle con UI embebida**
   - Pros: menos rutas visibles.
   - Cons: rompe el patrón vigente del repo, complica feedback/antiforgery/tests y no encontré evidencia de que el equipo use esa UX para administración.
   - Esfuerzo: Medio/Alto.

### Recomendación
- Recomiendo el enfoque 1. El repositorio ya consolidó un patrón claro para formularios administrativos en Razor Pages: pantallas separadas + partial compartido + cliente tipado + server-side validation. FORZAR otra cosa ahora sería deuda de consistencia, no innovación.
- Para edit, la propuesta debería asumir explícitamente que `codigo` es readonly/no editable porque el backend no lo acepta en `PUT`. Eso NO es un detalle menor de UI; es una restricción contractual real.

### Brechas detectadas
- Falta todo el seam web de escritura para Cargos: `ICargoApiClient`, `CargoApiClient`, `FakeCargoApiClient` y pruebas solo cubren listado/detalle/baja.
- No existe hoy un `InputModel` ni helper compartido para mapear field errors de Cargos al `ModelState`.
- No existe CTA de crear ni CTA de editar en el módulo Cargos actual.
- No hay pruebas web para create/edit de Cargos; habrá que extender el patrón de `CargoIndexPageTests`, `CargoDetailsPageTests` y seam tests.

### Riesgos / bloqueadores
- No hay bloqueador de backend para create/edit: los endpoints y contratos necesarios YA existen.
- Riesgo funcional principal: si la propuesta intenta hacer editable `codigo` en edit, chocará contra el contrato real de `ActualizarCargoRequest`.
- Riesgo UX: como no hay validación client-side cableada, la experiencia depende de roundtrip al servidor para ver errores; si se promete validación inmediata en proposal, habría que justificar un alcance mayor.
- Riesgo de alcance: agregar también reactivación/listado de eliminados/skills empuja el cambio bastante más allá de create/edit.

### Preguntas abiertas
- ¿En edit conviene mostrar `Código` como input readonly para conservar el aspecto del formulario, o como dato readonly fuera del form para dejar explícito que no se guarda?
- ¿El submenú de `Cargos` debe replicar el patrón de `Unidades Organizativas` agregando `Nuevo`, o alcanza con el botón `Crear` dentro del listado en este slice?

### Nombre OpenSpec propuesto
- `2026-06-30-agrega-crear-editar-cargos-frontend`
- Motivo: mantiene la convención observada en archivos archivados (`YYYY-MM-DD-<kebab-case descriptivo>`), expresa que es una continuación frontend del módulo Cargos ya archivado y evita mezclarlo con backend o skills.

### Ready for Proposal
Sí. El change puede pasar a `sdd-propose` sin bloquearse, con foco en create/edit web de Cargos sobre endpoints backend ya existentes y sin prometer paridad extra fuera de alcance.
