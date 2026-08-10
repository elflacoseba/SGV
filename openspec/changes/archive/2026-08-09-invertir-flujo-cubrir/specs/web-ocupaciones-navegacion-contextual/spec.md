# Delta Spec: web-ocupaciones-navegacion-contextual — invertir-flujo-cubrir

## MODIFIED Requirements

### Requirement: REQ-OCC-NAV-006 — Alta contextual precargada

CUANDO un Administrador crea desde una página cruzada, Create SHALL precargar el id dueño, mantener editable el otro selector y conservar un retorno seguro al origen.

**Modificación por el flujo `Puesto → Vacante → Ocupacion`**: el alta contextual desde `PuestoOcupaciones` se bifurca según el estado del Puesto. Si el Puesto tiene Vacante abierta, el flujo mantiene el comportamiento actual (precarga `Create` de Ocupacion con `PuestoId`). Si no tiene Vacante abierta, la acción contextual DEBE redirigir al módulo de Vacantes para abrir una Vacante (NAV-007), dado que N3 rechazaría el alta directa de Ocupacion. Si el Puesto tiene Ocupacion activa, se DERIVA al detalle de la Ocupacion vigente.

**Modificación por `invertir-flujo-cubrir`**: cuando el Puesto tiene Vacante abierta y NO tiene Ocupacion activa, el botón de alta contextual NO navega a `Create` con `?puestoId=` — navega a `Create` con `?vacanteId={vacanteId}` (el `PuestoId` se resuelve desde la Vacante en el backend del form). El `returnUrl` DEBE conservarse para volver al contexto origen (`Puesto Details` o `PuestoOcupaciones` según procedencia). El label del botón pasa a ser "Cubrir Vacante" (ver REQ-OCC-NAV-008).

(Previously: el alta contextual con Vacante abierta usaba `?puestoId=` y dejaba a `Create` crear la Ocupación sin cubrir la Vacante — el flujo Cubrir dependía de `PATCH /vacantes/{id}/estado` con `PersonaId`, inalcanzable desde el form.)

#### Escenarios

#### Scenario: Alta desde Persona
- GIVEN una Persona dueña
- WHEN se selecciona Nueva ocupación
- THEN SHALL abrir Create con `PersonaId` preseleccionado.

#### Scenario: Alta desde Puesto con Vacante abierta — navega a `?vacanteId=`
- **DADO** un Puesto dueño con Vacante `Abierta` y sin `Ocupacion` activa
- **CUANDO** el admin hace click en el botón de alta desde `PuestoOcupaciones`
- **ENTONCES** SHALL navegar a `/organizacion/ocupaciones/crear?vacanteId={vacanteId}&returnUrl=...`
- **Y** NO SHALL navegar a `?puestoId=` (comportamiento previo deprecado para este caso)
- **Y** el `PuestoId` se resuelve desde la Vacante al render del form (REQ-OCC-FORM-001)
- **Y** el label del botón SHALL ser "Cubrir Vacante" (REQ-OCC-NAV-008).

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

## ADDED Requirements

### Requirement: REQ-OCC-NAV-008 — Label dinámico del botón de alta desde PuestoOcupaciones

El botón de alta de Ocupación renderizado en `PuestoOcupaciones` (vista `_CrossList`) SHALL mostrar el label **"Cubrir Vacante"** cuando el Puesto tiene Vacante abierta (`Abierta` o `En Selección`) y NO tiene `Ocupacion` activa. En cualquier otro caso en que el botón de alta se mantenga visible (p. ej. como fallback para escenarios futuros), el label vigente "Nueva ocupación" se mantiene. Cuando el Puesto no tiene Vacante abierta, el botón de alta NO se muestra y se renderiza "Abrir Vacante" (NAV-007).

El `PuestoOcupaciones.cshtml.cs` DEBE exponer `NewOcupacionButtonLabel` en el `ViewModel` para que `_CrossList.cshtml` lo renderice sin lógica condicional en el marcado.

#### Escenarios

#### Scenario: Puesto con Vacante abierta sin Ocupación activa — label "Cubrir Vacante"
- **DADO** un Puesto con Vacante `Abierta` y sin `Ocupacion` activa
- **CUANDO** el admin entra a `PuestoOcupaciones`
- **ENTONCES** el botón de alta SHALL mostrar el label "Cubrir Vacante"
- **Y** `NewOcupacionButtonLabel` en el `ViewModel` SHALL ser `"Cubrir Vacante"`.

#### Scenario: Puesto con Vacante "En Selección" sin Ocupación activa — label "Cubrir Vacante"
- **DADO** un Puesto con Vacante `En Selección` (no `Abierta` estrictamente) y sin `Ocupacion` activa
- **CUANDO** el admin entra a `PuestoOcupaciones`
- **ENTONCES** el botón SHALL mostrar "Cubrir Vacante" (Vacante cubrible sigue siendo apta).

#### Scenario: Puesto con Vacante abierta y Ocupación activa — label "Nueva ocupación"
- **DADO** un Puesto con Vacante `Abierta` y `Ocupacion` activa coexistente (inconsistencia tolerada por la vista)
- **CUANDO** el admin entra a `PuestoOcupaciones`
- **ENTONCES** el botón SHALL mostrar el label "Nueva ocupación" (no aplica Cubrir)
- **Y** el alta navega al flujo genérico, no a `?vacanteId=`.

#### Scenario: Puesto sin Vacante abierta — se muestra "Abrir Vacante" (NAV-007)
- **DADO** un Puesto sin Vacante `Abierta` ni `En Selección`
- **CUANDO** el admin entra a `PuestoOcupaciones`
- **ENTONCES** NO se renderiza el botón de alta de Ocupación
- **Y** se renderiza "Abrir Vacante" (NAV-007) en su lugar.

#### Scenario: Usuario no-admin — botón oculto
- **DADO** un usuario autenticado sin rol `Administrador`
- **CUANDO** se renderiza `PuestoOcupaciones`
- **ENTONCES** ni el botón "Cubrir Vacante" ni "Nueva ocupación" ni "Abrir Vacante" se muestran (hereda la restricción vigente de NAV-006/NAV-007).