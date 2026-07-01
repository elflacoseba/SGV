# Design: Implementa en el front crear/editar Cargos

## 1. Resumen arquitectónico

Flujo: `SGV.Dominio/Cargo` → `SGV.Aplicacion` (requests, validator de shape, servicio de comandos) → `SGV.Infraestructura` (repo + índice único ya vigente) → `SGV.Api/Controllers/CargosController.cs` y `NivelesCargoController.cs` → `SGV.Web` (Razor Pages + partial compartido + cliente HTTP tipado) → `tests/SGV.Tests`.

El cambio entra en la arquitectura actual sin romper dependencias: Dominio sigue puro; Aplicación sigue dependiendo de abstracciones (`ICargoRepository`, `INivelCargoRepository`) y NO conoce HTTP; Web consume API vía `ICargoApiClient`.

## 2. Cambios por capa

### 2.1 Dominio

| Acción | Archivo | Cambio |
|---|---|---|
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Dominio/Organizacion/Cargo.cs` | `Actualizar(...)` debe aceptar `codigo` y dejar de documentarlo como inmutable; la validación sigue siendo de longitud/requerido, no de unicidad. |
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs` | Reemplazar `Actualizar_CodigoNoCambia` por casos de cambio válido y rechazo de duplicado a nivel servicio/aplicación; mantener cobertura de invariantes de entidad. |

### 2.2 Aplicación

| Acción | Archivo | Cambio |
|---|---|---|
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs` | `ActualizarCargoRequest` agrega `Codigo`. |
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs` | Validación de shape: `Codigo` requerido + max 50; `Nombre`, `NivelId`, `Descripcion` siguen igual. |
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs` | Compartir chequeo de unicidad activa entre create/update, propagar `Codigo` a `cargo.Actualizar(...)` y mapear conflicto 409 también ante carrera con índice único. |
| leer | `/Users/elflacoseba/Source/SGV/src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/CrearCargoRequestValidator.cs` | Referencia para mantener límites de `Codigo`/`Nombre`. |
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Aplicacion/Organizacion/ActualizarCargoRequestValidatorTests.cs` | Nuevos casos para `Codigo` requerido, vacío, largo y request válido. |
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs` | Casos de update válido con cambio de código, duplicado activo, código vacío y exclusión del propio id. |

Nota: la unicidad NO conviene meterla en FluentValidation porque el patrón real del repo hoy la resuelve en el servicio (`CargoServicioComandos` y `UnidadOrganizativaServicioComandos`); el design mantiene esa convención y extrae helper privado compartido.

### 2.3 Infraestructura

| Acción | Archivo | Cambio |
|---|---|---|
| leer | `/Users/elflacoseba/Source/SGV/src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs` | Confirmado `IX_Cargos_ActiveCodigoUnique`; no requiere migración nueva. |
| leer | `/Users/elflacoseba/Source/SGV/src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs` | `ExistsActiveCodeAsync(codigo, excludingId)` ya cubre update. |

### 2.4 API

| Acción | Archivo | Cambio |
|---|---|---|
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Api/Controllers/CargosController.cs` | Sin lógica nueva; se actualiza el contrato de `PUT` por el nuevo DTO y se conserva 400/409. |
| leer | `/Users/elflacoseba/Source/SGV/src/SGV.Api/Controllers/NivelesCargoController.cs` | Confirmado `GET /api/v1/niveles-cargo` para catálogo. |
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Api/CargosControllerTests.cs` | `PUT` debe enviar `codigo`; agregar duplicado activo (409) y código vacío (400). |

### 2.5 Web

| Acción | Archivo | Cambio |
|---|---|---|
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml` | Pantalla create con PRG a details. |
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml.cs` | Carga catálogo de niveles, POST a API, traducción de errores y `TempData`. |
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml` | Pantalla edit con mensaje de éxito y estado recuperable. |
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs` | GET del cargo + niveles, POST `PUT`, PRG a sí misma. |
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/_Form.cshtml` | Partial compartido para `Codigo`, `Nombre`, `Descripcion`, `Nivel`. |
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Organizacion/ICargoForm.cs` | Se ubica junto a `IUnidadOrganizativaForm`, no en `Pages/`, para respetar el patrón real. |
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Organizacion/CargoInputModel.cs` | DataAnnotations y shape del formulario. |
| crear | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Organizacion/CargoFormHelpers.cs` | `ApplyFieldErrorsToModelState` + utilidades de retorno PRG. |
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` | Agregar `CreateAsync`, `UpdateAsync`, `GetNivelesAsync`. |
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` | Implementar POST/PUT, parse de `ValidationProblemDetails` y catálogo `/api/v1/niveles-cargo`. |
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Agregar entry `Nueva`; el grupo ya queda activo por `StartsWithSegments("/organizacion/cargos")`. |
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` | Botón `Editar`. |
| modificar | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` | CTA `Crear cargo`. |
| leer | `/Users/elflacoseba/Source/SGV/src/SGV.Web/Program.cs` | DI ya registra `ICargoApiClient`; no debería requerir alta extra. |

### 2.6 Tests web

| Acción | Archivo | Cambio |
|---|---|---|
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Sin cambio estructural; ya permite override de `ICargoApiClient`. |
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs` | Extender el fake existente con create/update/niveles; es el equivalente real al patrón pedido. |
| crear | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/Cargo/CargoCreateEditPageTests.cs` | Smoke tests create/edit siguiendo `UnidadOrganizativaWebTests`. |
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/CargoWebTests.cs` | Verificar submenú `Nueva`. |
| modificar | `/Users/elflacoseba/Source/SGV/tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` | El detalle deja de ser estrictamente readonly: mostrar `Editar`. |

## 3. Contratos y DTOs

- `ActualizarCargoRequest(string Codigo, string Nombre, Guid NivelId, string? Descripcion)`.
- `CargoDto`: sin cambios.
- `ICargoForm`: `Input`, `NivelOptions`, `ErrorMessage`.

## 4. Estrategia Web ↔ API

`Create/Edit` usarán `ICargoApiClient` (`HttpClient` tipado). `GET` carga cargo y niveles antes de renderizar; si falla el catálogo, la página muestra error recuperable. `POST` traduce `400` con `ValidationProblemDetails` a `ModelState` por campo (`Input.Codigo`), `409` a error visible del campo `Codigo`, y errores de disponibilidad a mensaje general. PRG: create → details; edit → edit con `TempData`.

## 5. Migración de datos

No hay migración de schema. La consulta adicional de unicidad ya existe vía `ExistsActiveCodeAsync`; sólo se reutiliza en update.

## 6. Estrategia de tests

Dominio: cambio válido de `Codigo`. Aplicación: validator de shape + servicio con unicidad/exclusión del propio id. API: `PUT` válido, `PUT` duplicado 409, `PUT` código vacío 400. Web smoke: carga de niveles, POST create, GET/POST edit, duplicado mostrado en `Codigo`, PRG y navegación.

## 7. Estrategia de entrega (PRs)

| Opción | Alcance | Tradeoff |
|---|---|---|
| A — chained PRs | PR1 backend (`Cargo.cs`, requests, servicio, API, tests). PR2 web (pages, partial, cliente, fake, tests). | Menor carga de review; PR2 depende de PR1. Es la opción más probable por forecast 550–850 líneas. |
| B — single PR con `size:exception` | End-to-end completo en una sola revisión. | Menos rebases, pero excede budget 400 y aumenta fatiga del reviewer. |

`sdd-tasks` debe decidir el split final con `ask-always`.

## 8. Riesgos técnicos

- `ActualizarCargoRequest` agrega campo obligatorio: posible breaking change para consumidores externos.
- Carrera entre pre-check y `SaveChangesAsync`: el diseño usa pre-check para UX y el índice único como árbitro final; hay que traducir la violación a 409.
- PR2 queda bloqueado si PR1 no entra.
- En archive habrá que reconciliar este delta con `cargo-web-listado-detalle-baja`.

## 9. Notas de revisión

Revisar primero backend (`Cargo.cs`, `CargoServicioComandos.cs`, `CargosControllerTests.cs`) porque define el contrato. Luego validar Web (`CargoApiClient`, `_Form`, `Create/Edit`). Los tests críticos son: duplicado activo en update, mapeo `409 → Input.Codigo`, y carga del catálogo `GET /api/v1/niveles-cargo`.
