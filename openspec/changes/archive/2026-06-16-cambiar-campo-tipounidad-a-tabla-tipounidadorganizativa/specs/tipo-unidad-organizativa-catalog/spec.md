# Capability: Tipo de Unidad Organizativa (Catálogo)

> **Status:** NEW — capability does not exist in `openspec/specs/` yet. This delta is the full first version of the capability.
> **Change:** `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa`

## Purpose

Document the read-only, immutable catalog of organizational unit types that classifies each `UnidadOrganizativa`. The catalog is the single source of truth for the seven seeded values and is consumed by the `UnidadOrganizativa` write path to validate the `TipoUnidadOrganizativaId` foreign key. The catalog is **not** a CRUD resource: it is read-only at runtime and can only be evolved through a new migration.

## Requirements

### REQ-TUO-001 — Catalog immutability.

The `TipoUnidadOrganizativa` catalog MUST be immutable at runtime. The system MUST NOT expose any write endpoint (`POST`, `PUT`, `PATCH`, `DELETE`) over HTTP for the `/api/v1/tipos-unidad-organizativa` collection or any item underneath. The catalog is seeded exclusively by an EF Core migration that uses static `Guid` constants; any modification of catalog contents requires a new migration.

#### Scenario: Seed creates 7 static types

- **GIVEN** the `TiposUnidadOrganizativa` table is empty
- **WHEN** the migration runs against a fresh database
- **THEN** exactly 7 rows exist
- **AND** each row has the seeded `Id`, `Codigo`, and `Nombre` values declared as constants in the migration and in `DatosSemilla.cs`
- **AND** the 7 codes are `Institucion`, `Facultad`, `Secretaria`, `Direccion`, `Departamento`, `Division`, `Area`.

#### Scenario: No write endpoints exposed

- **GIVEN** the API is running
- **WHEN** any client attempts `POST`, `PUT`, `PATCH`, or `DELETE` on `/api/v1/tipos-unidad-organizativa` or `/api/v1/tipos-unidad-organizativa/{id:guid}`
- **THEN** the response is `405 Method Not Allowed` or `404 Not Found`
- **AND** no row in the `TiposUnidadOrganizativa` table is inserted, updated, or deleted.

### REQ-TUO-002 — List all types.

The system MUST expose `GET /api/v1/tipos-unidad-organizativa` (anonymous, no authentication required) that returns every type in the catalog.

#### Scenario: Returns full list

- **GIVEN** 7 seeded types exist in `TiposUnidadOrganizativa`
- **WHEN** a client calls `GET /api/v1/tipos-unidad-organizativa`
- **THEN** the response status is `200 OK`
- **AND** the response body is a JSON array of 7 elements
- **AND** each element has the fields `id`, `codigo`, `nombre` and no other fields.

#### Scenario: Empty database

- **GIVEN** the `TiposUnidadOrganizativa` table has no rows
- **WHEN** a client calls `GET /api/v1/tipos-unidad-organizativa`
- **THEN** the response status is `200 OK`
- **AND** the response body is an empty JSON array `[]` (not `404 Not Found`).

### REQ-TUO-003 — Get by id.

The system MUST expose `GET /api/v1/tipos-unidad-organizativa/{id:guid}` that returns a single type or a structured error.

#### Scenario: Existing id

- **GIVEN** a seeded type exists with id `X`
- **WHEN** a client calls `GET /api/v1/tipos-unidad-organizativa/X`
- **THEN** the response status is `200 OK`
- **AND** the response body is `{ "id": "<X>", "codigo": "<codigo>", "nombre": "<nombre>" }` with no other fields.

#### Scenario: Missing id

- **GIVEN** a well-formed Guid that does not exist in `TiposUnidadOrganizativa`
- **WHEN** a client calls `GET /api/v1/tipos-unidad-organizativa/<missing>`
- **THEN** the response status is `404 Not Found`
- **AND** the response body is a structured error (e.g. RFC 7807 ProblemDetails) indicating the resource was not found.

#### Scenario: Invalid Guid format

- **GIVEN** a path segment that is not a valid Guid (e.g. `/api/v1/tipos-unidad-organizativa/not-a-guid`)
- **WHEN** a client calls the endpoint
- **THEN** the response status is `400 Bad Request`
- **AND** the response body is a structured error explaining the route constraint violation.

### REQ-TUO-004 — DTO shape.

The response DTO MUST contain exactly three fields: `id: Guid`, `codigo: string`, `nombre: string`. It MUST NOT include timestamps, active flags, deleted flags, audit fields, or navigation properties. The DTO reflects the "pure catalog" model.

#### Scenario: DTO matches schema

- **GIVEN** any seeded type
- **WHEN** it is returned by either `GET /api/v1/tipos-unidad-organizativa` or `GET /api/v1/tipos-unidad-organizativa/{id:guid}`
- **THEN** the JSON payload has exactly the fields `id`, `codigo`, and `nombre`
- **AND** it has no other top-level fields.

### REQ-TUO-005 — Deterministic seed Ids.

The 7 seed Ids MUST be **static shared constants** referenced from a single source of truth (e.g. a `static class Catalogos` or `TipoUnidadOrganizativaCatalogo`), used by both the EF Core migration's `InsertData` and `DatosSemilla.cs`. The migration and the runtime seed MUST agree on the Ids to guarantee idempotency between a fresh migration and a runtime seed. A unit test asserts this equality.

#### Scenario: Seed Ids are stable

- **GIVEN** the migration class and `DatosSemilla.cs` both reference the same 7 static Guid constants
- **WHEN** the test `DatosSemilla_SeedIdsMatchMigracionEstatica` runs
- **THEN** it passes — every Id declared in the migration matches the corresponding Id declared in `DatosSemilla`
- **AND** there are exactly 7 distinct Ids in both lists.
