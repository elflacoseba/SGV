# Exploración: agregar filtro Activos/Eliminados al listado de Cargos

## 1. Resumen ejecutivo

- El listado de Cargos **sí existe** en `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, pero hoy está acotado explícitamente a **activos**: el título, el estado vacío, el cliente HTTP y el `PageModel` hablan solo de activos y solo soportan `?handler=Delete`. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`, `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`)
- El backend de Cargos **ya tiene soft-delete y reactivación** a nivel dominio/aplicación/API/repositorio, pero **no tiene una consulta/listado segmentado** equivalente al de Unidades Organizativas. Hoy `GET /api/v1/cargos` devuelve solo activos. (`src/SGV.Dominio/Organizacion/Cargo.cs`, `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`, `src/SGV.Api/Controllers/CargosController.cs`, `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`)
- El patrón de referencia en Unidades Organizativas se apoya en un segmento binario `status=eliminadas`, una propiedad `IsDeletedView`, un query explícito paginado y un handler `OnPostReactivateAsync` que vuelve a activas en éxito y conserva eliminadas en falla. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`, `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs`)
- La brecha real para Cargos es **coordinada**: falta backend de consulta por segmento y falta frontend para toggle, vista de eliminadas, botón Reactivar, handler POST y soporte JS/tests. No aparece un bloqueante de dominio, porque el soft-delete ya existe. (`src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`, `src/SGV.Web/wwwroot/js/pages/cargos-index.js`)

## 2. Contexto y referencia

El patrón a replicar es el cambio ya implementado en Unidades Organizativas: una misma página `Index` alterna entre **Activas** y **Eliminadas**, preserva `search`/`sort`/`p` al navegar, muestra acciones distintas por fila según el segmento y reutiliza el endpoint de reactivación existente. Eso vive hoy en `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`, `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` y `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs`.

## 3. Hallazgos del código

### 3.1 Listado actual de Cargos (Razor + PageModel)

#### Ubicación

- La página Razor del listado de Cargos existe en `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`.
- Su `PageModel` existe en `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`.

#### Qué hace hoy la Razor

- El encabezado dice **“Listado de cargos activos”** y la ayuda funcional también habla de “baja lógica”, no de reactivación ni de vista de eliminados. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`)
- El formulario GET solo envía `search` y preserva `sort`; **no existe** hidden/input `status`, ni toggle Activos/Eliminadas. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`)
- Las acciones por fila son **Detalle**, **Editar** y **Eliminar**. No hay acción **Reactivar** ni render condicional por segmento. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`)
- El POST de borrado preserva `id`, `page`, `search` y `sort` con hidden inputs, pero no preserva ningún estado de segmento porque hoy no existe. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`)
- La paginación y los links de orden preservan `p`, `search` y `sort`, pero no `status`. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`)
- La página carga SweetAlert2 y `wwwroot/js/pages/cargos-index.js`. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`)

#### Propiedades públicas y handlers del `IndexModel`

Propiedades públicas detectadas en `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`:

- `Items`
- `CurrentPage`
- `TotalPages`
- `TotalCount`
- `Search`
- `Sort`
- `LoadErrorMessage`
- `StatusMessage`
- `StatusKind`

Handlers/métodos públicos detectados en `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`:

- `OnGetAsync(...)`
- `OnPostDeleteAsync(...)`
- `GetSortRoute(string column)`
- `GetSortIcon(string column)`
- `BuildEditRouteValues(Guid id)`

Conclusión: el `PageModel` de Cargos **no** tiene hoy `Segmento`, `IsDeletedView`, `CurrentView`, `OnPostReactivateAsync`, `ReturnToListRouteValues` ni helpers equivalentes al patrón de Unidades Organizativas. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)

#### Cómo carga y serializa hoy el listado de Cargos

- `OnGetAsync` normaliza `p`, `search` y `sort` y luego llama a `LoadAsync`. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)
- `LoadAsync` usa `cargoApiClient.GetAllAsync()` y después aplica **filtro, orden y paginación en memoria**. No consume una consulta paginada del backend. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`, `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`)
- `TotalCount` se calcula con `ComputeTotalCount`, `TotalPages` con `Math.Ceiling(...)` y `CurrentPage` se corrige si excede el máximo. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)
- El alert post-redirect ya existe vía `TempData` con `StatusMessage` y `StatusKind`, pero hoy solo cubre la baja lógica. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)

### 3.2 Backend actual de Cargos (dominio/aplicación/infraestructura/API)

#### Dominio y soft-delete

- `Cargo` hereda de `EntidadAuditable`, y `EntidadAuditable` ya trae `IsDeleted`, `DeletedAt` y `DeletedByUserId`. (`src/SGV.Dominio/Organizacion/Cargo.cs`, `src/SGV.Dominio/Comun/EntidadAuditable.cs`)
- La entidad de dominio también tiene `IsActive` y métodos `Desactivar()` / `Activar()`. (`src/SGV.Dominio/Organizacion/Cargo.cs`)
- En persistencia, `CargoEntity` también tiene `IsActive` y hereda de `AuditableEntityBase`, que persiste `IsDeleted`/`DeletedAt`. (`src/SGV.Infraestructura/Persistencia/Entidades/CargoEntity.cs`, `src/SGV.Infraestructura/Persistencia/Entidades/AuditableEntityBase.cs`)
- La configuración EF define la columna computada `ActiveCodigoUnique = CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END` con índice único, o sea: la unicidad ya está pensada para convivir con soft-delete. (`src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`, `docs/decisiones-implementacion.md`)

#### Repositorio

- `CargoRepository.Query` filtra por `IsActive` e incluye `NivelCargo`; eso significa que la lectura base de `GetByIdAsync`/`ListAllAsync` queda orientada a activos. (`src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`)
- El repositorio **sí** expone:
  - `GetByIdForUpdateAsync(...)` para activos no eliminados.
  - `GetByIdIncludingDeletedAsync(...)` para incluir eliminados.
  - `DeleteAsync(...)` que marca `IsActive = false`, `IsDeleted = true`, `DeletedAt = UtcNow`.
  - `ReactivateAsync(...)` que restaura `IsActive = true`, `IsDeleted = false`, `DeletedAt = null`.
  - `ExistsActiveCodeAsync(...)` para conflicto por código activo.
  - `HasActivePuestosAsync(...)` para evitar eliminar si hay puestos activos. (`src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`)
- Lo que **no** expone hoy es una `QueryAsync(...)` segmentada ni una forma de pedir “solo eliminados” para listados. (`src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs`)

#### Aplicación

- `ICargoServicioConsulta` solo define `ListAsync()` y `GetByIdAsync()`. No hay contrato paginado ni filtro por estado/segmento. (`src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`)
- `CargoServicioConsulta` implementa exactamente eso: lista activa completa y detalle activo. (`src/SGV.Aplicacion/Organizacion/Consultas/CargoServicioConsulta.cs`)
- `ICargoServicioComandos` y `CargoServicioComandos` ya soportan `CrearAsync`, `ActualizarAsync`, `DesactivarAsync` y `ReactivarAsync`. (`src/SGV.Aplicacion/Organizacion/Comandos/ICargoServicioComandos.cs`, `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`)
- La reactivación ya valida conflicto por código activo antes de guardar. (`src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`)

#### API

Endpoints HTTP detectados en `src/SGV.Api/Controllers/CargosController.cs`:

- `GET /api/v1/cargos` → `IReadOnlyList<CargoDto>` (solo activos).
- `GET /api/v1/cargos/{id}` → `CargoDto` (activo o 404).
- `POST /api/v1/cargos` → `CargoDto`.
- `PUT /api/v1/cargos/{id}` → `CargoDto`.
- `DELETE /api/v1/cargos/{id}` → `204 NoContent` (soft-delete).
- `PATCH /api/v1/cargos/{id}/reactivar` → `CargoDto`.
- `GET /api/v1/cargos/{cargoId}/skills` → `IReadOnlyList<CargoSkillDetailDto>`.
- `PUT /api/v1/cargos/{cargoId}/skills/{skillId}` → `CargoSkillDto`.
- `DELETE /api/v1/cargos/{cargoId}/skills/{skillId}` → `204 NoContent`.

DTOs visibles para el frontend:

- `CargoDto` (`Id`, `Codigo`, `Nombre`, `Descripcion`, `NivelId`, `NivelNombre`). (`src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoDto.cs`)
- `NivelCargoDto` para el catálogo de niveles. (`src/SGV.Aplicacion/Organizacion/Consultas/Dtos/NivelCargoDto.cs`)
- `CargoSkillDetailDto` y `CargoSkillDto` para el subrecurso de skills. (`src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDto.cs`)

Conclusión de backend: **soft-delete y reactivación existen**, pero **no existe todavía un caso de uso/listado de cargos eliminados** ni un endpoint tipo `consulta` con segmento. (`src/SGV.Api/Controllers/CargosController.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`)

### 3.3 Desglose del patrón UnidadesOrganizativas

#### Representación del segmento Activas/Eliminadas

- La UI usa query string `status=eliminadas`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- El `PageModel` tiene `Segmento` y `IsDeletedView`, con constante `DeletedView = "eliminadas"`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
- `NormalizeSegmento` acepta solo `eliminadas`; cualquier otro valor cae a activas (`null`). (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)

#### Cómo se filtra el query de datos por soft-delete

- La web llama `unidadOrganizativaApiClient.QueryAsync(new UnidadOrganizativaListQuery(..., Status: Segmento))`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`, `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs`)
- El cliente HTTP serializa `status` en `/api/v1/unidades-organizativas/consulta?...&status=eliminadas`. (`src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs`)
- El controller traduce `status` al enum `UnidadOrganizativaSegmentoListado` y llama a `UnidadOrganizativaQuery`. (`src/SGV.Api/Controllers/UnidadesOrganizativasController.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs`)
- El repositorio filtra con predicado binario: activas = `u.IsActive && !u.IsDeleted`; eliminadas = `!u.IsActive && u.IsDeleted`. (`src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs`)

#### Cómo preserva contexto al cambiar de segmento / paginar / ordenar

- El toggle Activas/Eliminadas resetea a `p = 1` y preserva `search`/`sort`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- El form GET incluye hidden `sort` y hidden `status`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- Los links de orden y paginación siempre arrastran `status = Model.Segmento`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- Los formularios POST de Delete/Reactivate preservan `page`, `search`, `sort`, `view` y `status` en hidden inputs. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)

#### Cómo alterna acciones por fila

- En vista de activas muestra **Detalle + Editar + Eliminar**. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)
- En vista de eliminadas oculta esas acciones y muestra solo **Reactivar**. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`)

#### TempData / alert post-redirect

- El `PageModel` usa `StatusMessage` y `StatusKind` desde `TempData`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
- Además persiste `LastDeletedId` en `TempData` para mostrar un CTA rápido de reactivación en el banner cuando la eliminación fue exitosa. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)

#### Paginación / totalización

- El backend devuelve `PagedResult<UnidadOrganizativaDto>` y el `PageModel` asigna `CurrentPage`, `TotalCount` y `TotalPages` desde la respuesta. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs`)
- La web preserva `Search`, `Sort`, `Segmento` y usa `CurrentPage`/`TotalPages` para renderizar navegación consistente. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)

#### Handlers y contexto preservado

- Sí existen `?handler=Delete` y `?handler=Reactivate`. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`, `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)
- `OnPostReactivateAsync` redirige a **activas** en éxito (sin `status=eliminadas`) y conserva el segmento eliminado en falla. (`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`)

#### JS de soporte

- `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js` usa SweetAlert2 (`window.Swal`) y wirea dos confirmaciones:
  - delete: `[data-uo-delete-form]` + `[data-uo-delete-button]`
  - reactivate: `[data-uo-reactivate-form]` + `[data-uo-reactivate-button]`
- El JS toma `data-uo-item-name` y `data-uo-item-code` para construir el texto modal y usa `form.requestSubmit(button)` cuando está disponible. (`src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js`)

## 4. Brecha (gap) — qué falta

### Backend faltante

1. **Contrato de consulta de Cargos por segmento**
   - Hoy `ICargoServicioConsulta` solo expone `ListAsync()` / `GetByIdAsync()`. Falta un query explícito que permita pedir activas vs eliminadas, idealmente paginado. (`src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`)
2. **Repositorio con consulta segmentada**
   - `ICargoRepository` y `CargoRepository` no tienen `QueryAsync(...)` ni enum/record de segmento como sí existe en Unidades Organizativas. (`src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs`, `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`)
3. **Endpoint HTTP de consulta/listado filtrable**
   - `CargosController` solo tiene `GET /api/v1/cargos` para activos. No existe un `GET /api/v1/cargos/consulta?...&status=eliminadas` ni equivalente. (`src/SGV.Api/Controllers/CargosController.cs`)

### Frontend faltante

1. **Toggle Activas/Eliminadas en la Razor de Cargos**. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`)
2. **Estado de segmento en el PageModel** (`Segmento`, `IsDeletedView`, normalización, preservación en redirect/links/forms). (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)
3. **Handler `OnPostReactivateAsync`** y feedback de éxito/falla alineado al patrón. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`)
4. **Cliente web para reactivar y consultar por segmento**:
   - `ICargoApiClient` no expone `ReactivateAsync(...)`.
   - `CargoApiClient` no consume `PATCH /reactivar` ni una consulta segmentada.
   - `CargoListQuery` existe pero hoy no se usa en ninguna parte. (`src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`, `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`, `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs`)
5. **JS de confirmación para reactivar**
   - `cargos-index.js` hoy solo wirea delete. Falta un segundo wire para reactivate con `data-cargo-reactivate-*`. (`src/SGV.Web/wwwroot/js/pages/cargos-index.js`)

### Cambios coordinados (backend + frontend)

- Para que la UI pueda mostrar **solo eliminados** sin mezclar conjuntos, hace falta primero un contrato backend que los devuelva explícitamente; con el backend actual, el frontend no puede inventar esa vista porque `GET /api/v1/cargos` ya viene filtrado a activos. (`src/SGV.Api/Controllers/CargosController.cs`, `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`)
- La reactivación desde el listado también requiere coordinación: el endpoint backend ya existe, pero la web todavía no lo consume ni renderiza la acción contextual. (`src/SGV.Api/Controllers/CargosController.cs`, `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`)

### Orden sugerido de trabajo

1. Extender backend de consulta/listado para segmentar activas vs eliminadas.
2. Exponer el nuevo contrato HTTP en API y cliente web de Cargos.
3. Adaptar `Index.cshtml.cs` y `Index.cshtml` para toggle + Reactivar por fila.
4. Extender JS y pruebas.

## 5. Riesgos, supuestos y dependencias

### Riesgos técnicos

- **Medio**: si se copia el patrón de Unidades Organizativas pero se deja `GET /api/v1/cargos` como lista completa en memoria, la vista de eliminadas no va a existir de verdad; hay que introducir un contrato explícito de segmento. (`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`, `src/SGV.Api/Controllers/CargosController.cs`)
- **Medio**: cualquier cambio de consulta/listado en Cargos debe respetar la decisión del repo sobre unicidad activa por columna computada `ActiveCodigoUnique`; no conviene degradar ese comportamiento al reactivar. (`src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`, `docs/decisiones-implementacion.md`)
- **Medio**: el delete de Cargos ya tiene restricción por puestos activos; la vista de eliminadas debe considerar conflictos de reactivación por código duplicado y no prometer éxito lineal. (`src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`)
- **Bajo**: el interceptor de auditoría ya centraliza `IsDeleted`/`DeletedAt`; no parece requerir trabajo extra, pero cualquier cambio en queries debe seguir dejando trazabilidad coherente. (`src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs`)

### Supuestos a verificar en fases siguientes

- **Supuesto razonable**: el cambio puede limitarse al **modo listado** de Cargos, sin extender detalle/edición para mostrar estado recuperable de cargos eliminados. Hoy detalle y edición de Cargos siguen siendo active-only/recoverable sin acción de reactivar. (`src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml.cs`, `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs`)
- **Supuesto razonable**: el patrón correcto para Cargos también debería ser **segmento binario exclusivo** (activas o eliminadas), no lista mixta con badge de estado. Ese fue el diseño elegido y archivado para Unidades Organizativas. (`openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`)

### Dependencias

- Reusar el endpoint ya existente `PATCH /api/v1/cargos/{id}/reactivar`. (`src/SGV.Api/Controllers/CargosController.cs`)
- Introducir un query/DTO/enum análogo al de Unidades Organizativas para no improvisar strings sueltos por toda la solución. (`src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs`)

## 6. Cobertura de pruebas actual

### Cargos

- **Dominio**: `tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs` cubre reglas de la entidad Cargo.
- **Aplicación**:
  - `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs` cubre `ListAsync` y `GetByIdAsync`, pero no existe cobertura de un query por segmento porque ese caso de uso no existe todavía.
  - `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs` cubre crear, actualizar, desactivar y reactivar, incluidos conflictos por código y puestos activos.
- **Persistencia**:
  - `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` ya cubre `DeleteAsync_MarcaComoInactivoYEliminado` y `ReactivateAsync_RestauraEstadoActivo`.
  - También cubre unicidad activa con `IX_Cargos_ActiveCodigoUnique`.
  - No hay prueba de `QueryAsync` por segmento porque ese método no existe hoy.
- **API**:
  - `tests/SGV.Tests/Api/CargosControllerTests.cs` cubre `GET`, `POST`, `PUT`, `DELETE` y `PATCH /reactivar`.
  - No hay prueba de un endpoint de consulta por estado porque no existe.
- **Web**:
  - `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` cubre carga inicial, búsqueda vacía, error visible, confirmación JS de delete y `POST ?handler=Delete`.
  - Esa suite documenta explícitamente que el alcance actual **no** incluye “vista de eliminadas”. (`tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`)

### Unidades Organizativas (plantilla a replicar)

- **Aplicación**: `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs` ya cubre `QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas` y `QueryAsync_SegmentosNoSeMezclan`.
- **Persistencia**: `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` cubre `QueryAsync_SegmentosNoSeMezclan` y el filtrado activo/eliminado en MySQL real.
- **API**: `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` cubre `status=eliminadas` y `PATCH /reactivar`.
- **Web**: `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` cubre toggle, empty state contextual, conservación de `status` en links/forms, botón Reactivar por fila y redirect a activas vs permanencia en eliminadas según resultado.

### Gaps de cobertura esperables después del cambio

- Tests de aplicación para el nuevo query/segmento de Cargos.
- Tests de persistencia MySQL para `QueryAsync` de Cargos con activas vs eliminadas y no mezcla.
- Tests de API para el nuevo endpoint/flag HTTP de Cargos.
- Tests web para:
  - toggle Activas/Eliminadas,
  - preservación de `status` en paginación/orden/búsqueda,
  - render contextual de botones,
  - `POST ?handler=Reactivate` éxito/falla,
  - JS de confirmación de reactivación.

## 7. Próximos pasos sugeridos

1. En propuesta/spec, fijar explícitamente que Cargos seguirá el mismo contrato binario `activas` / `eliminadas` del listado de Unidades Organizativas.
2. Definir si Cargos usará también una consulta paginada backend (`consulta`) o si se aceptará otro nombre de endpoint; técnicamente conviene alinear nombres para bajar costo cognitivo.
3. Mantener fuera de alcance, salvo decisión explícita, cualquier expansión a detalle/edición de cargos eliminados.

## 8. Referencias

- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`
- `src/SGV.Web/wwwroot/js/pages/cargos-index.js`
- `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`
- `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`
- `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs`
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml`
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs`
- `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js`
- `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs`
- `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs`
- `src/SGV.Api/Controllers/CargosController.cs`
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/CargoServicioConsulta.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs`
- `src/SGV.Aplicacion/Organizacion/Comandos/ICargoServicioComandos.cs`
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoDto.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs`
- `src/SGV.Dominio/Organizacion/Cargo.cs`
- `src/SGV.Dominio/Comun/EntidadAuditable.cs`
- `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`
- `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs`
- `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`
- `docs/decisiones-implementacion.md`
- `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`
- `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs`
- `tests/SGV.Tests/Api/CargosControllerTests.cs`
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs`
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs`
- `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs`
- `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`
- `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`
