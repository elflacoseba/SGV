# Especificación: Propagación de Identidad en Transiciones de Vacante

## Propósito

Resolver el actor autenticado en el composition root de `SGV.Api` y propagarlo a `VacanteServicioComandos` y `OcupacionServicioComandos`, de modo que `HistorialEstadoVacante.ChangedByUserId` se persista con el `UserId` del JWT en lugar de quedar nulo. La identidad fluye a través de una abstracción `IUsuarioActual` inyectada en los servicios de aplicación; el DTO `CambiarEstadoVacanteRequest` no cambia. Decisión D-1.

## Requisitos

### Requisito: Transición autenticada persiste el actor

Cuando un usuario autenticado invoca `PATCH /api/v1/vacantes/{id}/estado` y el handler del controller llama `VacanteServicioComandos.CambiarEstadoAsync`, el `HistorialEstadoVacante` insertado DEBE tener `ChangedByUserId` igual al `UserId` (claim `NameIdentifier`) del `ClaimsPrincipal` del request.

(Previously: `ChangedByUserId` se persistía como `null` porque el servicio recibía `usuarioId: null` hardcodeado en `VacanteServicioComandos.cs:349-353`.)

#### Escenario: Transición autenticada persiste el actor

- **DADO** una vacante en estado `Abierta`
- **Y** un usuario autenticado con `UserId = "user-123"` en el JWT
- **CUANDO** el handler del controller llama `VacanteServicioComandos.CambiarEstadoAsync` con la transición solicitada
- **ENTONCES** el `HistorialEstadoVacante` insertado DEBE tener `ChangedByUserId = "user-123"`
- **Y** NO DEBE ser `null`.

#### Escenario: Principal no autenticado es rechazado con Unauthorized

- **DADO** un request que arriba al controller sin `User.Identity.IsAuthenticated == true`
- **CUANDO** el handler intenta invocar `CambiarEstadoAsync`
- **ENTONCES** el servicio DEBE lanzar una excepción que el controller mapea a `401 Unauthorized`
- **Y** NO DEBE persistir `ChangedByUserId = null` ni devolver `500`.

### Requisito: Cobertura vía Ocupaciones registra el actor

Cuando `OcupacionServicioComandos.CrearOcupacionCubriendoVacanteAsync` ejecuta la transición a `Cubierta` como side-effect de crear una `Ocupacion`, el `HistorialEstadoVacante` resultante DEBE tener `ChangedByUserId` igual al `UserId` del principal autenticado (no `null`).

(Previously: misma root cause que el requisito anterior — `OcupacionServicioComandos.cs:355-359` pasaba `usuarioId: null` hardcodeado.)

#### Escenario: Crear Ocupación cubriendo vacante persiste el actor

- **DADO** una vacante `Abierta` y un usuario con `UserId = "user-456"`
- **CUANDO** se invoca `OcupacionServicioComandos.CrearAsync` con `VacanteId` igual al id de la vacante
- **ENTONCES** la transición a `Cubierta` resultante DEBE tener `ChangedByUserId = "user-456"`
- **Y** la `Ocupacion` DEBE persistirse.

### Requisito: Tests existentes reflejan la trazabilidad

Los tests pre-existentes que asumían `ChangedByUserId = null` en `HistorialEstadoVacante` después de una transición DEBEN actualizarse para esperar el `UserId` del principal configurado en el test. La suite DEBE pasar sin regresión.

#### Escenario: Tests actualizados pasan con UserId propagado

- **DADO** los tests `VacanteServicioComandosTests` y `OcupacionServicioComandosTests` provistos de un `IUsuarioActual` mock que devuelve `UserId = "test-user"`
- **CUANDO** se ejecutan después del refactor
- **ENTONCES** TODOS los tests DEBEN pasar con el `UserId` propagado en `ChangedByUserId`.

## Escenarios

### Escenario: Abstracción IUsuarioActual inyectada en composition root

- **DADO** el `SGV.Api` con un `IHttpContextAccessor` registrado en DI
- **CUANDO** el composition root construye `VacanteServicioComandos`
- **ENTONCES** el servicio DEBE recibir una abstracción `IUsuarioActual` (no `IHttpContextAccessor` directo) que resuelva el `UserId` desde `HttpContext.User`.
