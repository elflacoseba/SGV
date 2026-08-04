# auditoria-detalle Specification

## Purpose

Definir la página y el endpoint de detalle de un registro de auditoría que expone los valores anteriores/posteriores (`OldValuesJson`, `NewValuesJson`), `EntityId`, `ChangedPropertiesJson` y `UserName`, accesible únicamente para `Administrador`. Esta capability es la única vía del sistema para exponer old/new values en el wire, cerrando D-2 por separación física de tipos (`AuditoriaDetalleDto` vs `AuditoriaDto`).

## Requirements

### Requirement: DTO enriquecido AuditoriaDetalleDto

`SGV.Contracts.Auditoria.AuditoriaDetalleDto` MUST exponer `Id` (`Guid`), `EntityName` (`string`), `EntityId` (`string`), `Operation` (`string`), `OccurredAt` (`DateTimeOffset`), `UserId` (`string`), `UserName` (`string`), `CorrelationId` (`Guid`), `ChangedPropertiesJson` (`string?`), `OldValuesJson` (`string?`) y `NewValuesJson` (`string?`). `OldValuesJson`/`NewValuesJson` MAY ser `null` cuando el registro corresponda a una operación sin snapshot (p.ej. alta). `UserName` MUST replicar la regla de fallback `"—"` del listado cuando el LEFT JOIN con `AspNetUsers` no encuentra fila.

#### Scenario: DTO de detalle expone EntityId y old/new values

- GIVEN un registro persistido con `OldValuesJson`/`NewValuesJson`/`EntityId` poblados
- WHEN el administrador solicita `GET /api/v1/auditorias/{id}`
- THEN el `AuditoriaDetalleDto` serializado contiene `entityId`, `oldValuesJson`, `newValuesJson`, `changedPropertiesJson`, `userName` y `correlationId`

#### Scenario: Detalle de alta sin old values

- GIVEN un registro de `Operation=Alta` (sin `OldValuesJson`)
- WHEN se solicita el detalle
- THEN `oldValuesJson` es `null` y `newValuesJson` contiene el snapshot del alta

#### Scenario: UserName cae a guión en detalle

- GIVEN un registro cuyo `UserId` no existe en `AspNetUsers`
- WHEN se solicita el detalle
- THEN `userName` se proyecta como `"—"`

### Requirement: Endpoint de detalle API protegido por Administrador

`GET /api/v1/auditorias/{id:guid}` SHALL exigir un usuario autenticado con rol `Administrador`. Peticiones sin autenticación MUST responder `401`; autenticadas sin el rol MUST responder `403`. El endpoint SHALL devolver `AuditoriaDetalleDto` cuando el registro existe (`200 OK`) y `404 Not Found` cuando no existe o el `id` no es un GUID válido. La autorización extiende el requisito "Autorización restringida al rol Administrador" de `auditoria-query`.

#### Scenario: Administrador obtiene el detalle

- GIVEN un usuario autenticado con rol `Administrador`
- WHEN envía `GET /api/v1/auditorias/{id:guid}` para un registro existente
- THEN recibe `200 OK` con `AuditoriaDetalleDto`

#### Scenario: Acceso anónimo al detalle API

- GIVEN un cliente sin credenciales
- WHEN envía `GET /api/v1/auditorias/{id:guid}`
- THEN recibe `401 Unauthorized`

#### Scenario: Usuario sin rol Administrador al detalle API

- GIVEN un usuario autenticado sin rol `Administrador`
- WHEN solicita el detalle
- THEN recibe `403 Forbidden` y el cuerpo NO contiene old/new values

#### Scenario: Detalle inexistente API

- GIVEN un `id` sin registro persistido
- WHEN el administrador solicita el detalle
- THEN recibe `404 Not Found`

### Requirement: Página web de detalle con render preformateado

`GET /auditorias/details?id={guid}` SHALL existir en `SGV.Web` protegida con `[Authorize(Roles = "Administrador")]`. La página MUST consumir `IAuditoriaApiClient.GetDetalleAsync(id)` y renderizar `OldValuesJson` y `NewValuesJson` dentro de etiquetas `<pre>` monoespaciadas, preservando el formato JSON legible. La página MUST reaccionar a un detalle inexistente con un estado legible (NO un crash) y a fallos de transporte recuperables del `AuditoriaApiClient` con un mensaje de transporte, sin perder el `id` consultado.

#### Scenario: Página renderiza JSON en `<pre>`

- GIVEN un registro existente con old/new values formateados
- WHEN el administrador accede a `/auditorias/details?id={guid}`
- THEN `OldValuesJson` y `NewValuesJson` se renderizan dentro de `<pre>` monoespaciado, preservando saltos de línea

#### Scenario: Acceso web sin rol Administrador es rechazado

- GIVEN un usuario autenticado sin rol `Administrador`
- WHEN solicita `/auditorias/details?id={guid}`
- THEN la autorización de la página lo rechaza (`403`/redirect según la shell), sin renderizar old/new values

#### Scenario: Detalle inexistente en la página

- GIVEN un `id` sin registro
- WHEN el administrador accede a `/auditorias/details?id={guid}`
- THEN la página muestra un estado legible de «no encontrado», NO una excepción

#### Scenario: Fallo de transporte en la página de detalle

- GIVEN el `AuditoriaApiClient.GetDetalleAsync` no puede completar la llamada por falla temporal
- WHEN el administrador accede a la página
- THEN se muestra un mensaje de error de transporte recuperable preserving el `id` consultado

### Requirement: Contrato del cliente HTTP tipado para el detalle

`IAuditoriaApiClient` MUST exponer `GetDetalleAsync(Guid id, CancellationToken)` retornando `AuditoriaDetalleDto?` (`null` para `404`, sin lanzar). El cliente MUST propagar `HttpRequestException` y `TaskCanceledException` nativas (en línea con `web-apiclient-transport-contract`) y MUST respetar un `CancellationToken` pre-cancelado sin iniciar el envío HTTP.

#### Scenario: `GetDetalleAsync` 200 retorna DTO enriquecido

- GIVEN el backend responde `200 OK` con `AuditoriaDetalleDto`
- WHEN el PageModel invoca `GetDetalleAsync(id)`
- THEN el resultado es `AuditoriaDetalleDto` no nulo con old/new values

#### Scenario: `GetDetalleAsync` 404 retorna null sin lanzar

- GIVEN el backend responde `404 Not Found`
- WHEN se invoca `GetDetalleAsync(id)`
- THEN el resultado es `null` y NO se lanza excepción

#### Scenario: `GetDetalleAsync` propaga fallos de transporte

- GIVEN el pipeline HTTP finaliza con `HttpRequestException`/`TaskCanceledException`
- WHEN se invoca `GetDetalleAsync`
- THEN la excepción nativa se propaga al consumidor, sin traducirse a resultado funcional

## Notas de implementación (no normativas)

- `AuditoriaServicioConsulta.GetDetalleDtoAsync` proyecta el `AuditoriaDetalleDto` con `AsNoTracking` (D-4 vigente; no audit-audita).
- La shell reutiliza el índice existente por `Id` (PK); no requiere migración de esquema `Auditorias`.

## ADDED Files

- `src/SGV.Contracts/Auditoria/AuditoriaDetalleDto.cs`
- `src/SGV.Web/Pages/Auditorias/Details.cshtml`
- `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs`

## MODIFIED Files

- `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` — `GetDetalleDtoAsync`.
- `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` — proyección de `AuditoriaDetalleDto` con LEFT JOIN.
- `src/SGV.Api/Controllers/AuditoriasController.cs` — `GetById` retorna `AuditoriaDetalleDto` con `[Authorize(Roles = "Administrador")]`.
- `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` — `GetDetalleAsync`.
- `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` — implementar `GetDetalleAsync`.