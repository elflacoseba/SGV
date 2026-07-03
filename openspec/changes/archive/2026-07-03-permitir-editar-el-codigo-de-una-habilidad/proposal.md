# Propuesta: Permitir editar el código de una Habilidad

## Por qué

- Hoy `Codigo` de `Habilidad` queda inmutable después del alta: el dominio no lo actualiza, el request de update no lo expone, el `PUT /api/v1/skills/{id}` no lo acepta y la UI de edit lo muestra `readonly`.
- El `exploration.md` verificó que el bloqueo existe en dominio, aplicación, persistencia, API, Razor Pages y tests; no es un problema sólo visual.
- Esto genera fricción operativa para administradores que necesitan corregir códigos cargados con error sin recrear la habilidad ni depender de bajas lógicas.
- Si no se hace, SGV mantiene una asimetría create/edit injustificada respecto del catálogo maestro y sigue contradiciendo la expectativa ya resuelta en el precedente de `Cargo`, pero sin copiar su manejo de `Nivel`.

## Qué cambia

- Un administrador podrá editar `Codigo`, `Nombre`, `Categoria` y `Descripcion` desde la pantalla de edición de Habilidades.
- El contrato HTTP de `PUT /api/v1/skills/{id}` pasa a aceptar `Codigo`; esto es un **breaking change** para consumidores que dependan del body actual.
- El dominio deja de tratar `Codigo` como inmutable post-creación y lo permite bajo los mismos invariantes de shape actuales (requerido + longitud máxima).
- La unicidad seguirá siendo validada para registros activos, idealmente con pre-check de aplicación y con el índice único como árbitro final ante carreras.

## Qué NO cambia (non-goals)

- No se copia el manejo de `Nivel` desde Cargos; `Habilidad` no tiene `NivelId` propio.
- No se introduce migración nueva; `IX_Habilidades_ActiveCodigoUnique` ya cubre la unicidad activa.
- No cambia la política de unicidad de `Codigo`: sigue siendo único entre registros activos y reutilizable tras soft delete.
- No se agregan nuevas pantallas, navegación adicional ni reglas para asignaciones `habilidad↔cargo` o `habilidad↔persona`.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `habilidad-web-crear-editar`: la requirement que hoy exige `Codigo` readonly en edit debe pasar a permitir edición.
- `habilidad-management`: la requirement que hoy preserva `Codigo` en update debe pasar a aceptar y persistir el nuevo valor respetando unicidad activa.

## Specs afectadas (delta + archive posterior)

- `openspec/specs/habilidad-web-crear-editar/spec.md`
- `openspec/specs/habilidad-management/spec.md`

No surge otra capability baseline afectada desde el `exploration.md`.

## Affected code (alto nivel)

- `src/SGV.Dominio/Habilidades/Habilidad.cs` — habilitar reasignación controlada de `Codigo` en update sin romper invariantes.
- `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadRequests.cs` — `ActualizarHabilidadRequest` acepta `Codigo`.
- `src/SGV.Aplicacion/Habilidades/Comandos/Validaciones/ActualizarHabilidadRequestValidator.cs` — validar shape de `Codigo`.
- `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs` — aplicar el nuevo `Codigo`, evaluar pre-check de unicidad y traducir conflicto concurrente a respuesta coherente.
- `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` — propagar `Codigo` en `UpdateEntity(HabilidadEntity, Habilidad)`.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs` — sin cambio funcional esperado; se relee como restricción vigente.
- `src/SGV.Api/Controllers/SkillsController.cs` — `PUT` acepta `Codigo` y conserva contrato 400/409.
- `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml*` y `_Form.cshtml` — remover `readonly` y postear `Input.Codigo`.
- `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` y `HabilidadApiClient.cs` — transportar `Codigo` en `UpdateAsync`.

## Tests a migrar

- Dominio: `tests/SGV.Tests/Dominio/HabilidadTests.cs` — reemplazar `Codigo_EsInmutableTrasCreacion`, `Actualizar_ModificaCamposEditables` y `Actualizar_CodigoNoCambia` por comportamiento de edición válida.
- Aplicación: `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` y `ActualizarHabilidadRequestValidatorTests.cs` — cubrir código requerido, update válido, duplicado activo, exclusión del propio id y conflicto concurrente.
- Persistencia: `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` — dejar de asumir que `Codigo` queda igual y agregar cobertura de update real.
- API: `tests/SGV.Tests/Api/SkillsControllerTests.cs` y `ApiWebApplicationFactory.cs` — `PUT` con `codigo`, `400` por shape inválido y `409` por duplicado activo.
- Web: `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` y seams/fakes asociados — romper la asunción `readonly` y validar roundtrip con `Codigo` editable.

## Enfoque de implementación (resumen)

- **RED**: introducir tests que fallen en dominio, aplicación, persistencia, API y web sólo sobre comportamiento observable relevante.
- **GREEN**: habilitar la edición end-to-end con el mínimo cambio coordinado backend→frontend.
- **REFACTOR**: alinear manejo de conflictos y seams de test sin copiar piezas de `Cargo` que dependan de `Nivel`.

Orden sugerido de commits internos en un único slice:

1. Dominio: tests + update de `Codigo`.
2. Aplicación: request, validator, servicio y conflicto de unicidad.
3. Persistencia: mapper + tests.
4. API: contrato `PUT` + tests.
5. Web: Razor/Edit + ApiClient + tests.
6. Specs delta en la fase `sdd-spec`.
7. Archive del change en `sdd-archive`.

## Riesgos

- **Breaking change** en `PUT /api/v1/skills/{id}`.
- **Carrera de unicidad** si sólo se hace pre-check y no se traduce la violación del índice único.
- **Tests vigentes protegen la regla opuesta** y deberán actualizarse o eliminarse con justificación.
- **Drift por paridad mecánica con Cargo** si alguien intenta introducir `NivelId` o catálogo de niveles donde no corresponde.
- Si el diff supera el budget, la decisión actual sigue siendo 1 slice; habrá que explicitar el desvío en la fase de apply, no inventarlo aquí.

## Preguntas abiertas / Suposiciones

- Por defecto, la propuesta asume **pre-check de unicidad + traducción de índice único a 409** para mantener UX y robustez frente a carreras.
- Se asume que la semántica actual de soft delete sigue siendo correcta: un código de una habilidad eliminada NO bloquea reutilización en una activa.
- Se asume que no existe una política adicional de privilegios para restringir la edición de `Codigo` a un subconjunto distinto de administradores.

## Delivery

- **1 slice** (decisión ya tomada con el usuario).
- **PR único stacked-to-main** contra la rama por defecto remota verificada: `develop`.
- **Budget**: 400 líneas a monitorear durante apply; no fragmentar artificialmente commits, pero sí mantener work units revisables.
- Conventional commits, sin `Co-Authored-By`.

## Rollback plan

Revertir el cambio de contrato de update, restaurar `Codigo` como no editable en dominio/API/web y quitar la propagación en el mapper. No requiere rollback de schema porque no hay migración nueva.

## Success criteria

- [ ] Un administrador puede editar `Codigo` de una Habilidad existente desde `SGV.Web`.
- [ ] `PUT /api/v1/skills/{id}` acepta `Codigo` y persiste el cambio cuando no hay duplicado activo.
- [ ] Un conflicto por `Codigo` duplicado activo se devuelve de forma coherente al usuario/API, sin degradar a `500`.
- [ ] La solución mantiene la ausencia de `NivelId` en `Habilidad` y preserva la política actual de soft delete + unicidad activa.
