## Exploration: Implementar FluentValidation para validaciones de UnidadesOrganizativas

### Current State

El módulo `UnidadesOrganizativas` ya existe de punta a punta en Clean Architecture:

- **Dominio**: `UnidadOrganizativa` valida invariantes internas con `ValidacionesDominio` y excepciones (`Codigo` requerido <= 50, `Nombre` requerido <= 200, `Descripcion` opcional <= 1000, `TipoUnidadOrganizativaId != Guid.Empty`, vigencia final no anterior a inicio, padre distinto de sí mismo).
- **Aplicación**: los requests son records sin DataAnnotations ni FluentValidation. `UnidadOrganizativaServicioComandos` concentra validaciones de caso de uso: código activo duplicado, existencia de tipo de unidad, existencia de padre, ciclos jerárquicos y captura de excepciones del dominio para devolver `UnidadOrganizativaCommandResult`.
- **API**: `UnidadesOrganizativasController` tiene `[ApiController]` y delega directamente en el servicio de comandos. Los errores de aplicación se traducen a `ProblemDetails` simple con `title`, `detail`, `status` y `type`.
- **Infraestructura/MySQL**: EF Core configura restricciones de longitud, FK `TipoUnidadOrganizativaId`, FK jerárquica restrictiva, índice por tipo, índice por padre, índice por nombre y unicidad de código activo mediante columna computada `ActiveCodigoUnique`.
- **Tests**: hay pruebas de dominio, aplicación, API y persistencia. Las pruebas de aplicación cubren reglas de negocio actuales; las de API hoy mockean el servicio de comandos y no prueban una integración real de validación de request previa al servicio.

No se encontró ningún paquete ni registro actual de FluentValidation. Tampoco hay DataAnnotations (`[Required]`, `[StringLength]`, etc.) en los request records.

### Affected Areas

- `src/SGV.Aplicacion/SGV.Aplicacion.csproj` — ubicación más natural para agregar `FluentValidation.DependencyInjectionExtensions` y definir validadores de requests como lógica de aplicación, sin acoplarlos a MVC.
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` — modelos de entrada a validar (`CrearUnidadOrganizativaRequest`, `ActualizarUnidadOrganizativaRequest`, `CambiarUnidadPadreRequest`).
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` — punto actual de validación pre-commit; si se usa validación manual, debería inyectar `IValidator<TRequest>` o un adaptador para validar antes de consultar repositorios/guardar.
- `src/SGV.Infraestructura/DependencyInjection.cs` — hoy registra servicios de aplicación; podría registrar validadores si se mantiene este como composition root de servicios internos, aunque conceptualmente el registro de validadores de aplicación también podría exponerse desde `SGV.Aplicacion`.
- `src/SGV.Api/Program.cs` — si se decide validar en borde HTTP, aquí se integraría el comportamiento con controllers; ojo con auto-validación MVC porque FluentValidation ya no recomienda ese pipeline para proyectos nuevos.
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` — podría quedar sin cambios si la validación vive en aplicación; si se valida en API, habría que mapear errores a `ValidationProblemDetails` antes de llamar al servicio.
- `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` — debería cubrir validaciones de request y asegurar que no se consulten repositorios/guarde ante errores básicos.
- `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` — debería cubrir el contrato HTTP si se espera que bodies inválidos devuelvan 400 antes/de forma consistente con el servicio.
- `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` — debería seguir protegiendo invariantes del dominio; FluentValidation NO debería reemplazarlas.

### Approaches

1. **Validación manual en capa de Aplicación con FluentValidation** — registrar validadores en DI e invocarlos explícitamente al inicio de los métodos de comando.
   - Pros: respeta Clean Architecture, permite reglas async si en el futuro hacen falta, evita depender del pipeline MVC, mantiene validaciones disponibles fuera de HTTP.
   - Cons: requiere tocar el servicio de comandos y mapear `ValidationResult` al `UnidadOrganizativaCommandResult` actual; hay que decidir formato de códigos/mensajes.
   - Effort: Medium.

2. **Validación automática en ASP.NET Core MVC** — integrar FluentValidation en el pipeline de controllers para poblar `ModelState` antes de ejecutar acciones.
   - Pros: reduce código explícito en controller/servicio para reglas simples de request.
   - Cons: FluentValidation no recomienda el pipeline MVC automático para proyectos nuevos; no soporta validación async en ese pipeline, es MVC-only y desplaza reglas hacia API.
   - Effort: Medium.

3. **Validación híbrida: API para forma básica + Aplicación para reglas de negocio** — usar validadores en aplicación y opcionalmente un filtro/API adapter para formato HTTP.
   - Pros: separa formato HTTP de reglas de caso de uso; permite evolucionar a `ValidationProblemDetails` sin romper dominio.
   - Cons: mayor superficie de cambio; puede superar el presupuesto si se intenta en una sola tajada con refactors de contrato de errores.
   - Effort: Medium/High.

### Recommendation

Recomiendo empezar con **validación manual en la capa de Aplicación** usando FluentValidation para reglas síncronas de los requests (`Codigo`, `Nombre`, `Descripcion`, `TipoUnidadOrganizativaId`, rango de vigencia y autopadre en `CambiarUnidadPadreRequest` si el servicio recibe también el id objetivo o se deja esa regla donde está). Mantener en el dominio las invariantes actuales como red de seguridad. Mantener en el servicio las reglas que dependen de repositorios: duplicado activo, existencia de tipo, existencia de padre y ciclo descendiente.

La primera tajada razonable y revisable debería limitarse a:

- agregar el paquete de FluentValidation en `SGV.Aplicacion`;
- crear validadores para `CrearUnidadOrganizativaRequest` y `ActualizarUnidadOrganizativaRequest`;
- registrar validadores;
- invocarlos al inicio de `CrearAsync` y `ActualizarAsync`;
- agregar tests de aplicación para entradas inválidas y no persistencia.

Dejar para una segunda tajada, si se desea, el refinamiento del contrato HTTP hacia `ValidationProblemDetails` por campo.

### Risks

- Duplicar reglas entre dominio y FluentValidation puede generar divergencia si no se define propiedad explícita de cada capa.
- Cambiar de `ProblemDetails` simple a `ValidationProblemDetails` puede romper tests/consumidores si se hace sin spec explícita.
- `Guid` no nullable en records hace que `tipoUnidadOrganizativaId` ausente llegue como `Guid.Empty`; FluentValidation puede reportarlo como obligatorio, pero el contrato de error por campo debe especificarse.
- Las reglas con repositorio (`tipoUnidadId` existente, duplicados, jerarquía descendiente) no conviene moverlas a validadores si eso introduce consultas async acopladas al pipeline MVC automático.
- El cambio toca paquete/DI/tests y puede crecer rápido; conviene mantenerlo bajo el presupuesto de 400 líneas con una primera tajada acotada.

### Ready for Proposal

Sí. El orquestador debería proponer un cambio enfocado en introducir FluentValidation en Aplicación para validaciones de request de `UnidadesOrganizativas`, preservando invariantes de dominio y reglas con repositorio en el servicio de comandos. Decisión pendiente: si el contrato HTTP debe seguir devolviendo `ProblemDetails` simple o migrar a `ValidationProblemDetails` por campo.
