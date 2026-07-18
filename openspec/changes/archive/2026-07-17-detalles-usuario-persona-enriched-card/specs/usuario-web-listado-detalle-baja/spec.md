# Delta: Card enriquecida de Persona en el detalle readonly del usuario

> Reemplaza solo `REQ-ULD-04` del spec canónico `usuario-web-listado-detalle-baja`. Referencias de implementación: `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` (líneas 78-81 a sustituir), `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs` (DI `IPersonaApiClient` + helper `TryLoadPersonaVinculadaAsync`), `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` (líneas 27-106 como espejo del árbol DOM) y `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaDto.cs`.

## MODIFIED Requirements

### Requirement: REQ-ULD-04 Detalle readonly con persona enriquecida y retorno seguro (MODIFIED)

El detalle MUST mostrar `UsuarioDto` en solo lectura —incluyendo `Nombres`/`Apellidos`, roles y una card de persona enriquecida que replica el árbol DOM de la card preseleccionada de Editar Usuario (`card border mb-0` con `data-usuario-persona-card`, `card-body`, `dl.row.mb-0` y `dt.col-sm-3`/`dd.col-sm-9`) cuando `IPersonaApiClient.GetByIdAsync` devuelve un `PersonaDto`. La card MUST renderizar `Apellidos`+`Nombres`, `Legajo` opcional, `Documento` (`TipoDocumento NumeroDocumento` vía `FormatDocumento`), `Email`, `Teléfono` y el badge de Estado (`badge-soft-success` cuando `IsActive=true`, `badge-soft-secondary` cuando `IsActive=false`). El `<a href="/personas/detalle/{PersonaId}">` MUST conservarse como título clickable de la card. Cuando `GetByIdAsync` devuelve `null` (404) o lanza `HttpRequestException`, el detalle MUST caer al fallback plano "Apellidos, Nombres" derivado del `UsuarioDto` **sin** marcar `IsNotFound`, **sin** renderizar los botones `Quitar`/`Cambiar` ni el modal `#usuario-persona-buscador-modal`. La vista MUST ofrecer retorno al listado preservando `p`/`search`/`sort`/`status`. Un identificador del usuario no consultable MUST producir estado recuperable con retorno claro al listado.

(Previously: la sección "Persona vinculada" mostraba únicamente el Guid crudo enlazado a `/personas/detalle/{PersonaId}`; sin enriquecimiento y sin fallback tipificado.)

#### Scenario: Detalle existente muestra campos legibles y retorno preservado

- **DADO** un usuario activo o bloqueado existente
- **CUANDO** un autenticado abre su detalle
- **ENTONCES** MUST mostrarse los campos legibles del `UsuarioDto` en solo lectura
- **Y** MUST ofrecerse retorno al listado preservando `p`/`search`/`sort`/`status`.

#### Scenario: Identificador no consultable produce estado recuperable

- **DADO** un identificador de usuario inexistente o eliminado
- **CUANDO** un autenticado abre su detalle
- **ENTONCES** MUST mostrarse estado recuperable con retorno claro al listado.

#### Scenario: Persona enriquecida visible cuando el API devuelve DTO

- **DADO** un usuario con `PersonaId` válido y `IPersonaApiClient.GetByIdAsync` que devuelve un `PersonaDto` con `Apellidos`, `Nombres`, `Legajo`, `TipoDocumento`, `NumeroDocumento`, `Email`, `Telefono` e `IsActive`
- **CUANDO** el `OnGetAsync` termina y la vista renderiza
- **ENTONCES** la sección "Persona vinculada" MUST renderizar la card enriquecida con los siete campos del DTO
- **Y** MUST aplicarse `data-usuario-persona-card` con `dl.row.mb-0` y los `dt.col-sm-3`/`dd.col-sm-9`
- **Y** MUST renderizarse `badge-soft-success` cuando `IsActive=true` o `badge-soft-secondary` cuando `IsActive=false`
- **Y** el `<a href="/personas/detalle/{PersonaId}">` MUST permanecer como título clickable.

#### Scenario: Fallback plano cuando el API devuelve 404

- **DADO** un usuario con `PersonaId` y `IPersonaApiClient.GetByIdAsync` que devuelve `null` (404)
- **CUANDO** el `OnGetAsync` termina y la vista renderiza
- **ENTONCES** la sección "Persona vinculada" MUST mostrar el texto plano "Apellidos, Nombres" derivado del `UsuarioDto`
- **Y** `IsNotFound` MUST permanecer en `false` (el detalle del usuario se renderiza completo, no el estado recuperable).

#### Scenario: Fallback plano sin IsNotFound ante error de transporte

- **DADO** un usuario con `PersonaId` y `IPersonaApiClient.GetByIdAsync` que lanza `HttpRequestException` u otro error clasificado por `TransportFailureClassifier.IsTransportFailure`
- **CUANDO** el `OnGetAsync` termina y la vista renderiza
- **ENTONCES** `IsNotFound` MUST quedar en `false`
- **Y** la card MUST caer al display plano "Apellidos, Nombres"
- **Y** el detalle MUST renderizarse completo (no el estado recuperable).

#### Scenario: Detalle sin controles de selección de persona

- **DADO** cualquier render del detalle de usuario con o sin persona vinculada
- **CUANDO** la vista termina
- **ENTONCES** la página MUST NOT contener los atributos `data-usuario-persona-quitar` ni `data-usuario-persona-buscar`
- **Y** MUST NOT existir el elemento `#usuario-persona-buscador-modal`.
