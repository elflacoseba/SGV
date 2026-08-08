# Spec: web-ocupaciones-crear-editar

## Purpose

Definir los flujos administrativos Razor para crear, editar, consultar y ejecutar transiciones de ciclo de vida de una Ocupación.

## Scope

Incluye `Create`, `Edit`, `Details`, validación, conflictos y PRG. Excluye edición de históricas, borrado físico y cambios a unicidad.

## Cambios

- Nuevos: `SGV.Web/Pages/Organizacion/Ocupaciones/{Create,Edit,Details}.{cshtml,cshtml.cs}` y `_Form.cshtml`.
- Modificados: cliente/fake de Ocupaciones y pruebas Web.
- Endpoints: `POST`, `PUT/{id}`, `GET/{id}`, `PATCH/{id}/finalizar`, `DELETE/{id}`, `PATCH/{id}/reactivar`.

## ADDED Requirements

### Requirement: REQ-OCC-FORM-001 — Crear Ocupación

CUANDO un Administrador abre `Create`, SHALL disponer de `PersonaId`, `PuestoId`, `FechaInicio`, `TipoAsignacion` y `Observaciones`, con selectores alimentados por catálogos existentes.

**Modificación por N3**: el `Create` DEBE verificar que el `PuestoId` seleccionado tenga una Vacante abierta. Si no la tiene, la API responde `409 Conflict` con código `PuestoSinVacanteAbierta` y el formulario DEBE mostrar el conflicto junto al selector `PuestoId`.

#### Escenarios

#### Scenario: Alta válida
- GIVEN catálogos cargados y datos válidos y una Vacante abierta para el `PuestoId`
- WHEN se envía el formulario
- THEN SHALL invocar Create y persistir la Ocupación.

#### Scenario: Puesto sin Vacante abierta (N3)
- GIVEN que el `PuestoId` seleccionado no tiene ninguna Vacante abierta
- WHEN se envía el formulario `Create`
- THEN la API SHALL responder `409 Conflict` con código `PuestoSinVacanteAbierta`
- Y el formulario SHALL mostrar el conflicto junto al selector `PuestoId`
- Y NO SHALL mostrar éxito ni perder los demás inputs.

#### Scenario: Catálogo no disponible
- GIVEN falla un catálogo
- WHEN carga Create
- THEN SHALL mostrar estado recuperable e impedir una selección inválida.

#### Scenario: Usuario no-admin
- GIVEN un autenticado sin rol Administrador
- WHEN accede o publica
- THEN SHALL redirigir a 403 o responder `Forbid` sin mutación.

### Requirement: REQ-OCC-FORM-002 — Editar solo vigentes

CUANDO se solicita `Edit`, el sistema SHALL permitir cambios únicamente si `Estado=Vigente` y SHALL bloquear finalizadas o eliminadas antes de mutar.

#### Escenarios

#### Scenario: Edición válida
- GIVEN una Ocupación vigente
- WHEN se guardan datos válidos
- THEN SHALL invocar Update y conservarla vigente.

#### Scenario: Finalizada
- GIVEN una Ocupación finalizada
- WHEN se abre o publica Edit
- THEN SHALL bloquear la edición y no invocar Update.

#### Scenario: Eliminada
- GIVEN una Ocupación eliminada
- WHEN se abre o publica Edit
- THEN SHALL bloquear la edición y ofrecer retorno seguro.

### Requirement: REQ-OCC-FORM-003 — Detalle y acciones de ciclo de vida

CUANDO se abre `Details`, SHALL mostrar todos los datos y ofrecer Finalizar, Eliminar o Reactivar solo a Administrador y según el estado permitido.

#### Escenarios

#### Scenario: Finalizar vigente
- GIVEN una Ocupación vigente
- WHEN se confirma `FechaFin`
- THEN SHALL invocar Finalize y mostrar estado `Finalizada`.

#### Scenario: Eliminar vigente
- GIVEN una Ocupación vigente
- WHEN se confirma Eliminar
- THEN SHALL ejecutar baja lógica, nunca borrado físico.

#### Scenario: Reactivar histórica
- GIVEN una Ocupación finalizada o eliminada
- WHEN se confirma Reactivar
- THEN SHALL invocar Reactivate y ocultar acciones incompatibles.

### Requirement: REQ-OCC-FORM-004 — Validación cliente y servidor

CUANDO un formulario es inválido, SHALL validar en cliente y servidor, mapear `ValidationProblemDetails` a `ModelState` por propiedad y conservar inputs/catálogos.

#### Escenarios

#### Scenario: Validación cliente
- GIVEN un campo requerido ausente
- WHEN se intenta enviar
- THEN SHALL mostrar el error junto al input.

#### Scenario: Validación API
- GIVEN la API responde 400 con `FieldErrors`
- WHEN se procesa la respuesta
- THEN SHALL asociar cada error a su propiedad.

#### Scenario: Re-render seguro
- GIVEN un POST inválido
- WHEN se re-renderiza
- THEN SHALL conservar valores y repoblar selectores.

### Requirement: REQ-OCC-FORM-005 — Conflictos de unicidad visibles

CUANDO Create o Edit recibe 409, SHALL distinguir `PersonaYPuestoOcupados`, `PuestoOcupado` y `PuestoSinVacanteAbierta`, conservar el formulario y mostrar feedback funcional.

#### Escenarios

#### Scenario: Persona y Puesto duplicados
- GIVEN ya existe el mismo par vigente
- WHEN se intenta guardar
- THEN SHALL mostrar el conflicto `PersonaYPuestoOcupados`.

#### Scenario: Puesto ocupado
- GIVEN el Puesto tiene otra Ocupación vigente
- WHEN se intenta guardar
- THEN SHALL mostrar `PuestoOcupado`.

#### Scenario: Puesto sin vacante abierta (N3)
- GIVEN el `PuestoId` no tiene Vacante abierta
- WHEN se intenta guardar
- THEN SHALL mostrar el conflicto `PuestoSinVacanteAbierta` junto al selector `PuestoId`.

#### Scenario: Sin falso éxito
- GIVEN cualquiera de esos 409 (`PersonaYPuestoOcupados`, `PuestoOcupado`, `PuestoSinVacanteAbierta`)
- WHEN se re-renderiza
- THEN SHALL conservar datos y no mostrar éxito.

### Requirement: REQ-OCC-FORM-006 — PRG con feedback

CUANDO una mutación termina exitosamente, SHALL aplicar Post-Redirect-Get y transportar por `TempData` un único feedback; ante error SHALL no redirigir como éxito.

#### Escenarios

#### Scenario: Crear o editar
- GIVEN una operación exitosa
- WHEN retorna la API
- THEN SHALL redirigir a listado o detalle con éxito.

#### Scenario: Transición exitosa
- GIVEN finalizar, eliminar o reactivar exitoso
- WHEN termina el POST
- THEN SHALL redirigir al detalle/listado actualizado con feedback.

#### Scenario: Operación fallida
- GIVEN respuesta funcional o transporte fallido
- WHEN termina el POST
- THEN SHALL preservar contexto y no emitir éxito.

### Requirement: REQ-OCC-FORM-007 — FechaFin válida

CUANDO se finaliza, `FechaFin` SHALL ser igual o posterior a `FechaInicio`; cliente y servidor SHALL aplicar la misma regla.

#### Escenarios

#### Scenario: Fecha válida
- GIVEN `FechaFin >= FechaInicio`
- WHEN se finaliza
- THEN SHALL aceptar la transición.

#### Scenario: Bloqueo cliente
- GIVEN `FechaFin < FechaInicio`
- WHEN se intenta enviar desde Web
- THEN SHALL mostrar validación sin llamar a la API.

#### Scenario: Defensa servidor
- GIVEN un consumidor omite la validación cliente
- WHEN envía la fecha inválida
- THEN la API SHALL responder 400 sin persistir.

### Requirement: REQ-OCC-FORM-008 — Reactivación con colisión explícita

CUANDO Reactivate responde 409 por unicidad, Details SHALL mostrar el código específico, mantener el estado histórico y permitir una recuperación informada.

**Modificación por Q2**: la reactivación DEBE rechazarse también cuando la `Vacante` vinculada a la `Ocupacion` está `Cancelada`, además de las colisiones de unicidad existentes.

#### Escenarios

#### Scenario: Reactivación válida
- GIVEN no existen colisiones vigentes y la `Vacante` vinculada (si existe) NO está `Cancelada`
- WHEN se reactiva
- THEN SHALL quedar `Vigente` tras PRG.

#### Scenario: Colisión del par
- GIVEN existe el mismo par activo
- WHEN se reactiva
- THEN SHALL mostrar `PersonaYPuestoOcupados` y mantener historial.

#### Scenario: Colisión del Puesto
- GIVEN otro vínculo vigente ocupa el Puesto
- WHEN se reactiva
- THEN SHALL mostrar `PuestoOcupado` y no mutar.

#### Scenario: Vacante Cancelada bloquea reactivación (Q2)
- GIVEN una `Ocupacion` cuya `Vacante` vinculada (mismo `VacanteId`) está en estado `Cancelada`
- WHEN se confirma Reactivar
- THEN la API SHALL responder `409 Conflict`
- Y Details SHALL mostrar el conflicto manteniendo el estado histórico
- Y NO SHALL mutar la `Ocupacion`.

### Requirement: REQ-OCC-FORM-009 — Flujo normal documentado

El formulario `Create` SHALL documentar al usuario Administrador que el flujo normal de alta de `Ocupacion` es el automatizado: crear Vacante → transicionar a `Cubierta` (que materializa la `Ocupacion`). El alta manual vía `Create` queda restringida al caso en que el `Puesto` ya tiene Vacante abierta (N3) y representa una excepción operativa, no el camino principal.

#### Escenarios

#### Scenario: Hints de flujo en `Create`
- GIVEN un Administrador abriendo `Create`
- WHEN se renderiza el formulario
- THEN SHALL mostrar un hint indicando que el alta directa requiere Vacante abierta para el Puesto
- Y SHALL enlazar al módulo de Vacantes para el flujo principal.

#### Scenario: `Create` no sustituye al flujo automatizado
- GIVEN un Puesto sin Vacante abierta
- WHEN el Administrador intenta el alta directa
- THEN SHALL recibir `PuestoSinVacanteAbierta` y ser derivado al flujo Vacante → Cubierta.

## Modelo de Datos

| Formulario | Shape |
|---|---|
| Create/Edit | `PersonaId`, `PuestoId`, `FechaInicio`, `TipoAsignacion`, `Observaciones?` |
| Finalizar | `FechaFin`, `Observaciones?` |
| Resultado | `OcupacionCommandResult` y `OcupacionDto` de `web-ocupaciones-contrato-api` |

## Errores y Taxonomía

| Caso | `ErrorCategoria` / comportamiento |
|---|---|
| 400 | `Validation`; errores por campo |
| 401 | `Unauthorized`; sesión requerida |
| 403 | `Forbidden`; sin invocar mutación |
| 404 | `NotFound`; detalle recuperable |
| 409 | `Conflict`; código funcional visible |
| Excepción/408/5xx | `Transport`; reintento sin falso éxito |

## Dependencias

- Depende de API-001/004/005 y LST-001/004.
- FORM-002/003 dependen de `OcupacionEstado`; FORM-005/008/009 de códigos 409.
- FORM-001/005/009 y NAV-006/007 dependen del flujo `Puesto → Vacante → Ocupacion` (N1/N2/N3/N4/Q2).
- `web-ocupaciones-navegacion-contextual` precarga estos formularios, sin cambiar sus reglas.
