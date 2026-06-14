# Delta for SGV Database

## MODIFIED Requirements

### Requirement: MySQL/Pomelo and EF Core Compatibility

The database design MUST target MySQL through Pomelo Entity Framework Core while the application targets .NET 10. EF Core-related packages, including `Microsoft.EntityFrameworkCore*`, Identity EF Core, and Pomelo, MUST remain on compatible 9.x versions. SQL Server MUST NOT remain an active supported provider for current configuration, migrations, or documentation.
(Previously: The database design targeted SQL Server and Entity Framework Core over .NET 9.)

#### Scenario: Configure the supported database provider

- GIVEN the SGV infrastructure project is configured for persistence
- WHEN the application configures the database provider
- THEN it MUST use the MySQL/Pomelo provider
- AND it MUST NOT configure SQL Server as an active provider.

#### Scenario: Preserve EF Core 9.x package compatibility on .NET 10

- GIVEN all SGV projects target .NET 10
- WHEN dependencies are restored
- THEN EF Core-related packages MUST resolve to compatible 9.x versions
- AND Pomelo MUST remain compatible with the resolved EF Core relational package range.

#### Scenario: Preserve existing entity identifiers

- GIVEN the current SGV domain model uses GUID identifiers for application entities
- WHEN application entities are created
- THEN their primary keys MUST continue to use GUID identifiers unless a separate schema redesign changes that behavior.

#### Scenario: Remove SQL Server migration assumptions

- GIVEN persistence migrations or model snapshots are used for verification
- WHEN provider-specific artifacts are reviewed
- THEN SQL Server-specific assumptions MUST be replaced by MySQL-compatible artifacts.

## RENAMED Requirements

### Requirement: Compatibilidad con SQL Server y EF Core → MySQL/Pomelo and EF Core Compatibility

(Reason: The supported provider changes from SQL Server on .NET 9 to MySQL/Pomelo on .NET 10.)
(Migration: Update references, tests, documentation, configuration, and active migration artifacts to the new requirement name and provider target.)
