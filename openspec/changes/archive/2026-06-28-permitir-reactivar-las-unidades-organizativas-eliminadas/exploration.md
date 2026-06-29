## Exploration: Permitir reactivar las unidades organizativas eliminadas

### Current State
- `UnidadOrganizativa` ya modela el ciclo `Activar()` / `Desactivar()` con `IsActive` y hereda `IsDeleted`/`DeletedAt` desde `EntidadAuditable`.
- La capa de aplicación ya expone `ReactivarAsync` en `UnidadOrganizativaServicioComandos`; allí se valida el código activo duplicado y que la unidad padre siga activa.
- La persistencia ya tiene `DeleteAsync` y `ReactivateAsync` en `UnidadOrganizativaRepository`, que solo mutan los flags de baja lógica.
- Las consultas activas filtran `IsActive && !IsDeleted` en `ListAllAsync`, `QueryAsync`, `ListTreeAsync`, `GetByIdForUpdateAsync` y `HasActiveChildrenAsync`/`HasActivePuestosAsync`.
- La API ya publica `PATCH /api/v1/unidades-organizativas/{id}/reactivar`, pero `SGV.Web` no expone ninguna acción para reactivar desde la UI.
- El listado web muestra solo activos; detalle/edición tratan una unidad eliminada como “no disponible” y redirigen o muestran estado recuperable, no un flujo de restauración.
- `openspec/specs/unidad-organizativa-crud/spec.md` documenta baja lógica, pero no reactivación; `openspec/specs/sgv-database/spec.md` ya define unicidad activa y baja lógica, pero tampoco aterriza la reactivación de unidades.

### Affected Areas
- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` — estado activo/inactivo del agregado.
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` — caso de uso de reactivación y validaciones de conflicto.
- `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` — lectura activa y árbol.
- `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` — filtros activos, baja lógica y reactivación.
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` — endpoint HTTP de reactivación.
- `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs` y `UnidadOrganizativaApiClient.cs` — cliente HTTP sin método de reactivar.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/*.cshtml(.cs)` — listado, detalle y edición sin acción de restauración.
- `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs`, `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs`, `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs`, `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` — cobertura actual y huecos para reactivación visible.
- `openspec/specs/unidad-organizativa-crud/spec.md`, `openspec/specs/sgv-database/spec.md`, `openspec/specs/unidad-organizativa-web-listado/spec.md`, `openspec/specs/unidad-organizativa-web-detalle-edicion/spec.md` — specs a alinear.

### Approaches
1. **Exponer la reactivación como acción administrativa de primer nivel** — mantener el contrato backend existente y hacerlo visible desde API/Web.
   - Pros: reutiliza la lógica actual, cierra la brecha de UX, alinea API + UI + tests.
   - Cons: requiere ampliar la superficie de la shell y documentar el flujo.
   - Effort: Medium.

2. **Mantener la reactivación solo en backend/API** — dejar el endpoint como capacidad técnica para clientes externos.
   - Pros: menor cambio inmediato.
   - Cons: no resuelve la necesidad operativa de restaurar unidades desde `SGV.Web`.
   - Effort: Low.

### Recommendation
La opción más razonable es la primera: la base técnica ya existe, así que el cambio debería cerrar la experiencia completa sin tocar el modelo de datos. El foco de la siguiente fase debería ser documentar el flujo, exponerlo de forma coherente en la UI/API y cubrirlo con pruebas de integración.

### Risks
- Reactivar una unidad cuyo padre siga inactivo ya está bloqueado por el servicio; si la UI no explica ese conflicto, la operación parecerá inconsistente.
- La unicidad activa por `Codigo` puede convertir una reactivación en conflicto si existe otra unidad activa con el mismo valor.
- Las consultas activas siguen ocultando las unidades eliminadas, así que el flujo necesita una forma clara de encontrar la unidad antes de restaurarla.
- `UnidadesOrganizativasController` sigue sin `[Authorize]`; si la intención de negocio es restringir la restauración, eso debe revisarse aparte.

### Ready for Proposal
Yes — si el objetivo es habilitar restauración operativa desde la shell web. Si la intención fuera solo exponer la capacidad al consumidor API, hace falta confirmarlo antes de redactar el proposal.
