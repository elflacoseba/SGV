# Spec Delta: web-ocupaciones-navegacion-contextual

## Propósito del delta

Ajustar el alta contextual desde `PuestoOcupaciones` (REQ-OCC-NAV-006) al nuevo flujo `Puesto → Vacante → Ocupacion`: cuando un Administrador inicia "Nueva ocupación" desde un Puesto sin Vacante abierta, la acción ya no puede precargar `Create` de Ocupacion directamente, porque la API la rechazaría con `409 PuestoSinVacanteAbierta` (N3). El alta contextual desde Puesto ahora debe iniciar el flujo de creación de Vacante, no el de Ocupacion. El alta contextual desde Persona se mantiene inalterada (en ese caso el `PuestoId` es editable y el usuario puede elegir un Puesto con Vacante abierta).

## Cambios respecto a la spec vigente

### REQUISITOS MODIFICADOS (modified)

#### Requisito: REQ-OCC-NAV-006 — Alta contextual precargada (MODIFIED)

**Cambio**: el alta contextual desde `PuestoOcupaciones` se bifurca según el estado de la Vacante del Puesto. Si el Puesto tiene Vacante abierta, el flujo mantiene el comportamiento actual (precarga `Create` de Ocupacion con `PuestoId`). Si no tiene Vacante abierta, la acción contextual debe redirigir al módulo de Vacantes para abrir una Vacante, no a `Create` de Ocupacion, dado que N3 rechazaría el alta directa.

**Antes**: "Nueva ocupación" desde `PuestoOcupaciones` siempre abría `Create` con `PuestoId` preseleccionado.

**Ahora**: el destino depende de si el Puesto tiene Vacante abierta.

##### Escenario: Alta desde Puesto con Vacante abierta (inalterado)

- **DADO** un Puesto dueño con Vacante abierta
- **CUANDO** se selecciona "Nueva ocupación" desde `PuestoOcupaciones`
- **ENTONCES** SHALL abrir `Create` con `PuestoId` preseleccionado
- **Y** el submit podrá prosperar (sujeto a N3, que en este caso pasa).

##### Escenario: Alta desde Puesto sin Vacante abierta (N3)

- **DADO** un Puesto dueño sin Vacante abierta (ni abierta ni en curso)
- **CUANDO** se selecciona "Nueva ocupación" desde `PuestoOcupaciones`
- **ENTONCES** SHALL redirigir al flujo de creación de Vacante para ese `PuestoId`
- **Y** NO SHALL abrir `Create` de Ocupacion con `PuestoId` precargado (queda rechazado por N3)
- **Y** SHALL mostrar mensaje contextual indicando que primero debe existir una Vacante abierta.

##### Escenario: Alta desde Puesto con Ocupacion activa (N1)

- **DADO** un Puesto con `Ocupacion` activa
- **CUANDO** se selecciona "Nueva ocupación" desde `PuestoOcupaciones`
- **ENTONCES** SHALL informar que el Puesto ya está ocupado y derivar al detalle de la `Ocupacion` vigente
- **Y** NO SHALL ofrecer el flujo de Vacante ni el `Create` de Ocupacion.

##### Escenario: Alta desde Persona (inalterado)

- **DADO** una Persona dueña
- **CUANDO** se selecciona "Nueva ocupación" desde `PersonaOcupaciones`
- **ENTONCES** SHALL abrir `Create` con `PersonaId` preseleccionado
- **Y** el `PuestoId` queda editable para que el usuario elija un Puesto con Vacante abierta (sujeto a N3 en el submit).

##### Escenario: Usuario no-admin (inalterado)

- **DADO** un autenticado sin rol Administrador
- **CUANDO** se renderiza o solicita el alta
- **ENTONCES** SHALL ocultar la acción y bloquear la escritura.

### REQUISITOS NUEVOS (added)

#### Requisito: REQ-OCC-NAV-007 — Navegación al flujo de Vacante desde Puesto

El sistema SHALL exponer desde `PuestoOcupaciones` una acción "Abrir Vacante" para Administradores que dirija al flujo de creación de Vacante del módulo correspondiente con `PuestoId` precargado, de modo de habilitar luego el alta de `Ocupacion` por la vía normal (Vacante → `Cubierta` → Ocupacion automática).

##### Escenario: Abrir Vacante desde Puesto sin vacante

- **DADO** un Puesto sin Vacante abierta ni `Ocupacion` activa
- **Y** un Administrador navegando `PuestoOcupaciones`
- **CUANDO** se selecciona "Abrir Vacante"
- **ENTONCES** SHALL navegar al `Create` de Vacantes con `PuestoId` precargado
- **Y** SHALL conservar retorno seguro al `Puesto Details`.

##### Escenario: Abrir Vacante oculto si ya existe

- **DADO** un Puesto con Vacante abierta
- **CUANDO** se renderiza `PuestoOcupaciones`
- **ENTONCES** SHALL ocultar "Abrir Vacante" y mostrar "Nueva ocupación" habilitado.

##### Escenario: "Abrir Vacante" no-admin

- **DADO** un usuario sin rol Administrador
- **CUANDO** se renderiza `PuestoOcupaciones`
- **ENTONCES** SHALL ocultar "Abrir Vacante" (igual que "Nueva ocupación").

## Escenarios de aceptación

- N3 contextual: "Nueva ocupación" desde `PuestoOcupaciones` con Puesto sin Vacante abierta NO abre `Create` de Ocupacion; deriva al flujo de Vacante.
- N1 contextual: "Nueva ocupación" desde Puesto con Ocupacion activa deriva al detalle de la Ocupacion vigente (no abre `Create`).
- Alta desde Persona inalterada; el `PuestoId` sigue editable y validado por N3 al submit.
- REQ-OCC-NAV-007: acción "Abrir Vacante" visible solo para Administrador y solo si no existe Vacante abierta ni Ocupacion activa; `PuestoId` precargado y retorno seguro al `Puesto Details`.