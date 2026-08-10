# Delta Spec: vacante-web — invertir-flujo-cubrir

## ADDED Requirements

### Requisito: Botón "Cubrir Vacante" en Details de Vacante

La página `Vacantes/Details` DEBE renderizar un botón con label **"Cubrir Vacante"** cuando:

- La Vacante está en estado `Abierta` o `En Selección` (i.e. NO `Cubierta`, NO `Cancelada`, NO terminal no cubrible), **Y**
- El usuario autenticado tiene permiso de mutación (rol `Administrador` **o** `GestorVacantes` — equivalente a `CanMutate` ya usado en el módulo).

La URL destino del botón DEBE ser `/organizacion/ocupaciones/crear?vacanteId={vacanteId}&returnUrl=/organizacion/vacantes/detalles/{vacanteId}`. El botón NO DEBE aparecer en estados `Cubierta` ni `Cancelada`, ni para usuarios sin rol de mutación. El botón se renderiza en el bloque de acciones de la página Details.

(Previously: la spec vigente de `vacante-web` no menciona el flujo de Cubrir; la transición a `Cubierta` era responsabilidad de `PATCH /vacantes/{id}/estado` con `PersonaId`, inalcanzable desde el frontend actual.)

#### Escenario: Vacante Abierta — botón visible para admin

- **DADO** una Vacante en estado `Abierta` y un usuario con rol `Administrador`
- **CUANDO** el admin entra a `/organizacion/vacantes/detalles/{id}`
- **ENTONCES** la interfaz DEBE renderizar el botón con label "Cubrir Vacante"
- **Y** el `href` del botón DEBE ser `/organizacion/ocupaciones/crear?vacanteId={id}&returnUrl=/organizacion/vacantes/detalles/{id}`.

#### Escenario: Vacante En Selección — botón visible

- **DADO** una Vacante en estado `En Selección` y un usuario con rol `Administrador` o `GestorVacantes`
- **CUANDO** entra al Details de la Vacante
- **ENTONCES** la interfaz DEBE renderizar el botón "Cubrir Vacante" (la Vacante `En Selección` es cubrible).

#### Escenario: Vacante Cubierta — botón oculto

- **DADO** una Vacante en estado `Cubierta`
- **CUANDO** un usuario con rol de mutación entra al Details
- **ENTONCES** la interfaz NO DEBE renderizar el botón "Cubrir Vacante"
- **Y** en su lugar DEBE aparecer el bloque "Persona asignada" (ver requisito siguiente).

#### Escenario: Vacante Cancelada — botón oculto

- **DADO** una Vacante en estado `Cancelada`
- **CUANDO** un usuario con rol de mutación entra al Details
- **ENTONCES** la interfaz NO DEBE renderizar el botón "Cubrir Vacante".

#### Escenario: Usuario sin rol de mutación — botón oculto

- **DADO** una Vacante `Abierta` y un usuario autenticado sin rol `Administrador` ni `GestorVacantes`
- **CUANDO** entra al Details de la Vacante
- **ENTONCES** la interfaz NO DEBE renderizar el botón "Cubrir Vacante".

### Requisito: Bloque "Persona asignada" en Details de Vacante Cubierta

Cuando la Vacante está `Cubierta` y `VacanteDetailDto.OcupacionDerivadaId` no es null (provisto por `GET /api/v1/vacantes/{id}`), la página `Details` DEBE renderizar un bloque informativo "*Persona asignada*" que:

- Muestre el label "Persona asignada:" seguido del valor `VacanteDetailDto.PersonaAsignadaNombre`.
- Muestre un link con label "Ver ocupación" que navegue a `/organizacion/ocupaciones/detalles/{ocupacionDerivadaId}`.
- Se renderice después del bloque "Detalle de vacante" y antes del bloque de `HistorialEstadoVacante`.

Si la Vacante NO está `Cubierta` (o `OcupacionDerivadaId` es null), el bloque NO se renderiza. Si la Vacante está `Cubierta` pero `PersonaAsignadaNombre` es null (defensivo, estado inconsistente tolerado), el bloque se renderiza con el valor vacío y el link "Ver ocupación" se omite.

#### Escenario: Vacante Cubierta con Ocupación y Persona asignada — bloque visible

- **DADO** una Vacante `Cubierta` cuyo `VacanteDetailDto.OcupacionDerivadaId` apunta a una Ocupación y `PersonaAsignadaNombre` es "Juan Pérez"
- **CUANDO** el admin entra al Details de la Vacante
- **ENTONCES** la interfaz DEBE renderizar el bloque "Persona asignada: Juan Pérez"
- **Y** DEBE renderizar un link "Ver ocupación" con `href=/organizacion/ocupaciones/detalles/{ocupacionDerivadaId}`
- **Y** el bloque DEBE aparecer entre el detalle de la Vacante y el historial.

#### Escenario: Vacante Abierta — bloque oculto

- **DADO** una Vacante `Abierta` con `OcupacionDerivadaId = null`
- **CUANDO** el admin entra al Details
- **ENTONCES** la interfaz NO DEBE renderizar el bloque "Persona asignada".

#### Escenario: Vacante Cubierta sin `PersonaAsignadaNombre` (defensivo) — bloque parcial

- **DADO** una Vacante `Cubierta` con `OcupacionDerivadaId != null` pero `PersonaAsignadaNombre = null` (inconsistencia tolerada)
- **CUANDO** el admin entra al Details
- **ENTONCES** la interfaz DEBE renderizar el bloque "Persona asignada:" con valor vacío/tratado
- **Y** el link "Ver ocupación" DEBE omitirse o deshabilitarse en ausencia de nombre asignado.