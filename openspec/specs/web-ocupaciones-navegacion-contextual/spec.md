# Spec: web-ocupaciones-navegacion-contextual

## Purpose

Definir navegación cruzada entre Personas, Puestos y sus Ocupaciones, con filtros server-side y retorno al contexto de origen.

## Scope

Incluye dos páginas cruzadas, enlaces desde detalles, retorno y precarga de alta. Excluye toggle de eliminadas y subrecursos REST anidados.

## Cambios

- Nuevos: `Pages/Personas/PersonaOcupaciones.*` y `Pages/Organizacion/Puestos/PuestoOcupaciones.*`.
- Modificados: `Personas/Details.cshtml`, `Puestos/Details.cshtml` y pruebas Web.
- Endpoint consumido: `GET /api/v1/ocupaciones` con `personaId` o `puestoId`; alta mediante la ruta Create existente.

## ADDED Requirements

### Requirement: REQ-OCC-NAV-001 — Ocupaciones por Persona

CUANDO se abre `PersonaOcupaciones`, SHALL consultar el listado con `personaId`, segmento fijo `Activas` y mostrar únicamente ocupaciones vigentes de la Persona dueña.

#### Escenarios

#### Scenario: Persona con ocupaciones
- GIVEN una Persona activa con vínculos vigentes
- WHEN se abre su página cruzada
- THEN SHALL mostrar solo filas con ese `PersonaId`.

#### Scenario: Persona sin ocupaciones
- GIVEN una Persona activa sin vínculos vigentes
- WHEN carga la página
- THEN SHALL mostrar un estado vacío contextual.

#### Scenario: Persona inexistente
- GIVEN un id no consultable
- WHEN se solicita la página
- THEN SHALL mostrar `NotFound` recuperable sin datos ajenos.

### Requirement: REQ-OCC-NAV-002 — Ocupaciones por Puesto

CUANDO se abre `PuestoOcupaciones`, SHALL consultar con `puestoId`, segmento fijo `Activas` y mostrar únicamente ocupaciones vigentes del Puesto dueño.

#### Escenarios

#### Scenario: Puesto ocupado
- GIVEN un Puesto con una Ocupación vigente
- WHEN se abre su página cruzada
- THEN SHALL mostrar solo filas con ese `PuestoId`.

#### Scenario: Puesto sin ocupación
- GIVEN un Puesto sin vínculo vigente
- WHEN carga la página
- THEN SHALL mostrar un estado vacío contextual.

#### Scenario: Puesto inexistente
- GIVEN un id no consultable
- WHEN se solicita la página
- THEN SHALL mostrar `NotFound` sin descargar el universo.

### Requirement: REQ-OCC-NAV-003 — Enlaces desde detalles

CUANDO una Persona o Puesto activo se muestra en Details, SHALL exponer “Ver ocupaciones” a cualquier usuario autenticado y navegar con el id correcto.

#### Escenarios

#### Scenario: Persona activa
- GIVEN el detalle de una Persona activa
- WHEN se renderiza
- THEN SHALL enlazar a `PersonaOcupaciones` con su id.

#### Scenario: Puesto activo
- GIVEN el detalle de un Puesto activo
- WHEN se renderiza
- THEN SHALL enlazar a `PuestoOcupaciones` con su id.

#### Scenario: Entidad no activa
- GIVEN Persona o Puesto no consultable como activo
- WHEN se renderiza el detalle
- THEN SHALL no ofrecer el enlace contextual.

### Requirement: REQ-OCC-NAV-004 — Sin toggle Eliminadas

CUANDO se renderiza una página cruzada, SHALL mantener `status=activas`, SHALL no enlazar ni bindear otro segmento y SHALL no mostrar toggle Eliminadas.

#### Escenarios

#### Scenario: HTML sin toggle
- GIVEN cualquiera de las páginas cruzadas
- WHEN se inspecciona el HTML
- THEN SHALL no existir control de Historial/Eliminadas.

#### Scenario: Query fija
- GIVEN una carga contextual
- WHEN el PageModel invoca el cliente
- THEN SHALL enviar `Segmento=Activas` y el id dueño.

#### Scenario: Status inyectado
- GIVEN una URL con `status=eliminadas`
- WHEN se procesa
- THEN SHALL ignorarlo y conservar el segmento activo.

### Requirement: REQ-OCC-NAV-005 — Volver preserva origen

CUANDO el usuario selecciona Volver, SHALL regresar al Details de Persona o Puesto que originó la navegación y preservar sus route/query values de contexto.

#### Escenarios

#### Scenario: Retorno a Persona
- GIVEN navegación desde Persona Details
- WHEN se selecciona Volver
- THEN SHALL regresar a esa Persona con su contexto.

#### Scenario: Retorno a Puesto
- GIVEN navegación desde Puesto Details
- WHEN se selecciona Volver
- THEN SHALL regresar a ese Puesto con su contexto.

#### Scenario: Contexto ausente
- GIVEN acceso directo a la página cruzada
- WHEN se selecciona Volver
- THEN SHALL usar el detalle dueño como destino seguro.

### Requirement: REQ-OCC-NAV-006 — Alta contextual precargada

CUANDO un Administrador crea desde una página cruzada, Create SHALL precargar el id dueño, mantener editable el otro selector y conservar un retorno seguro al origen.

**Modificación por el flujo `Puesto → Vacante → Ocupacion`**: el alta contextual desde `PuestoOcupaciones` se bifurca según el estado del Puesto. Si el Puesto tiene Vacante abierta, el flujo mantiene el comportamiento actual (precarga `Create` de Ocupacion con `PuestoId`). Si no tiene Vacante abierta, la acción contextual DEBE redirigir al módulo de Vacantes para abrir una Vacante (NAV-007), dado que N3 rechazaría el alta directa de Ocupacion. Si el Puesto tiene Ocupacion activa, se DERIVA al detalle de la Ocupacion vigente.

#### Escenarios

#### Scenario: Alta desde Persona
- GIVEN una Persona dueña
- WHEN se selecciona Nueva ocupación
- THEN SHALL abrir Create con `PersonaId` preseleccionado.

#### Scenario: Alta desde Puesto con Vacante abierta
- GIVEN un Puesto dueño con Vacante abierta
- WHEN se selecciona Nueva ocupación desde `PuestoOcupaciones`
- THEN SHALL abrir Create con `PuestoId` preseleccionado
- Y el submit podrá prosperar (sujeto a N3, que en este caso pasa).

#### Scenario: Alta desde Puesto sin Vacante abierta (N3)
- GIVEN un Puesto dueño sin Vacante abierta (ni abierta ni en curso)
- WHEN se selecciona "Nueva ocupación" desde `PuestoOcupaciones`
- THEN SHALL redirigir al flujo de creación de Vacante para ese `PuestoId`
- Y NO SHALL abrir `Create` de Ocupacion con `PuestoId` precargado (queda rechazado por N3)
- Y SHALL mostrar mensaje contextual indicando que primero debe existir una Vacante abierta.

#### Scenario: Alta desde Puesto con Ocupacion activa (N1)
- GIVEN un Puesto con `Ocupacion` activa
- WHEN se selecciona "Nueva ocupación" desde `PuestoOcupaciones`
- THEN SHALL informar que el Puesto ya está ocupado y derivar al detalle de la `Ocupacion` vigente
- Y NO SHALL ofrecer el flujo de Vacante ni el `Create` de Ocupacion.

#### Scenario: Usuario no-admin
- GIVEN un autenticado sin rol Administrador
- WHEN se renderiza o solicita el alta
- THEN SHALL ocultar la acción y bloquear la escritura.

### Requirement: REQ-OCC-NAV-007 — Navegación al flujo de Vacante desde Puesto

El sistema SHALL exponer desde `PuestoOcupaciones` una acción "Abrir Vacante" para Administradores que dirija al flujo de creación de Vacante del módulo correspondiente con `PuestoId` precargado, de modo de habilitar luego el alta de `Ocupacion` por la vía normal (Vacante → `Cubierta` → Ocupacion automática).

#### Escenarios

#### Scenario: Abrir Vacante desde Puesto sin vacante
- GIVEN un Puesto sin Vacante abierta ni `Ocupacion` activa
- Y un Administrador navegando `PuestoOcupaciones`
- WHEN se selecciona "Abrir Vacante"
- THEN SHALL navegar al `Create` de Vacantes con `PuestoId` precargado
- Y SHALL conservar retorno seguro al `Puesto Details`.

#### Scenario: Abrir Vacante oculto si ya existe
- GIVEN un Puesto con Vacante abierta
- WHEN se renderiza `PuestoOcupaciones`
- THEN SHALL ocultar "Abrir Vacante" y mostrar "Nueva ocupación" habilitado.

#### Scenario: "Abrir Vacante" no-admin
- GIVEN un usuario sin rol Administrador
- WHEN se renderiza `PuestoOcupaciones`
- THEN SHALL ocultar "Abrir Vacante" (igual que "Nueva ocupación").

## Modelo de Datos

| Contexto | Query |
|---|---|
| Persona | `personaId`, `status=activas`, paginación |
| Puesto | `puestoId`, `status=activas`, paginación |
| Create | `PersonaId?`, `PuestoId?`, contexto local de retorno |

## Errores y Taxonomía

| Caso | `ErrorCategoria` / UX |
|---|---|
| 400 | `Validation`; contexto inválido |
| 401 | `Unauthorized`; sign-in |
| 403 | `Forbidden`; `/error/403` |
| 404 | `NotFound`; dueño no disponible |
| 409 | `Conflict`; delegado al formulario |
| Excepción/408/5xx | `Transport`; carga recuperable |

## Dependencias

- NAV-001/002/004 dependen de API-003 y LST-001/002.
- NAV-006 depende de FORM-001/004/006 y de autorización API-005.
- Sigue el patrón de `persona-skill-web-management` y las páginas Persona/Puesto-Habilidades.
