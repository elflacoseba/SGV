# Delta for sgv-database

## MODIFIED Requirements

### Requirement: Historial de Ocupaciones

The system MUST persist occupations as historical `Persona`-to-`Puesto` assignments with `FechaInicio`, nullable `FechaFin`, controlled `TipoAsignacion`, and logical deletion metadata. A `Persona` MAY hold multiple active occupations only when they belong to different `Puesto` records. The system MUST keep at most one active occupation per `Puesto` and at most one active occupation per `Persona + Puesto` pair. Finalization MUST preserve the row and set `FechaFin`. Logical delete MUST preserve the row and remove it from active uniqueness and active queries. Reactivation MUST reuse the same row and MUST be rejected when an active uniqueness conflict already exists.
(Previously: The requirement only defined historical dates and active uniqueness for `Puesto` and `Persona + Puesto`, without explicit logical delete and reactivation persistence semantics.)

#### Scenario: Current occupation of a puesto

- GIVEN an occupation has `FechaFin = null` and `IsDeleted = false`
- WHEN the current state of its `Puesto` is queried
- THEN that occupation MUST represent the active assignee of the `Puesto`.

#### Scenario: Concurrent occupations in different puestos

- GIVEN one `Persona` has an active occupation in a `Puesto`
- WHEN another active occupation is stored for the same `Persona` in a different `Puesto`
- THEN the system MUST allow both active rows.

#### Scenario: Duplicate active persona and puesto pair

- GIVEN one active occupation already exists for a `Persona + Puesto` pair
- WHEN another active row for the same pair is stored
- THEN the system MUST reject the second active row.

#### Scenario: Duplicate active puesto

- GIVEN one active occupation already exists for a `Puesto`
- WHEN another active row for that same `Puesto` is stored
- THEN the system MUST reject the second active row.

#### Scenario: Historical lookup after finalization

- GIVEN an occupation has `FechaFin` set
- WHEN the history of the `Puesto` is queried for a date inside its range
- THEN the system MUST identify that stored occupation as the historical assignee.

#### Scenario: Logical delete releases active uniqueness without deleting history

- GIVEN an active occupation is logically deleted
- WHEN active occupation queries or active uniqueness checks run
- THEN that row MUST be excluded from active visibility and active uniqueness
- AND the historical row MUST remain persisted.

#### Scenario: Reactivation conflict

- GIVEN a historical occupation is being reactivated
- WHEN another active row already conflicts on `Puesto` or `Persona + Puesto`
- THEN the system MUST reject the reactivation
- AND the historical row MUST remain unchanged.
