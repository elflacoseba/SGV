# Spec Delta: Reconstitute factories en `PersistenceToDomainMapper`

> **Change**: `2026-07-13-fix-124-persistence-mapper-reconstitute`
> **Issue**: #124 — Eliminar reflexión del mapper de persistencia
> **Dominio canónico**: `sgv-persistence-architecture`

## Contexto

Este delta formaliza el contrato de reconstitución de entidades de dominio desde la capa de persistencia tras la eliminación del helper `SetProperty<T>` basado en `System.Reflection`. El cambio introduce factories tipadas `internal static Reconstitute(...)` en 6 entidades como mecanismo oficial de reconstitución.

## ADDED Requirements

### Requirement: REQ-124-1 — Reconstitute factories tipadas

Las entidades de dominio que requieren reconstitución desde la capa de persistencia DEBEN exponer una factory estática interna `Reconstitute(...)` con setters tipados (sin reflexión). Los parámetros DEBEN incluir todos los campos persistibles en el orden canónico: `Id + auditoría + IsDeleted` → datos primarios → `IsActive` → propiedades de navegación.

#### Scenario: Las 6 entidades exponen `internal static Reconstitute(...)`

- **GIVEN** las entidades Cargo, Habilidad, Puesto, Persona, Ocupacion y UnidadOrganizativa son reconstituidas desde persistencia
- **WHEN** se invoca `ToDomain(TEntity)` en `PersistenceToDomainMapper`
- **THEN** cada una DEBE delegar a su factory `internal static Reconstitute(...)` con la signatura exacta definida en el `design.md` §2
- **AND** los setters DEBEN ser tipados (sin `PropertyInfo.SetValue` ni `BindingFlags.NonPublic`)

#### Scenario: `PersistenceToDomainMapper.ToDomain(TEntity)` delega al factory

- **GIVEN** el mapper recibe una entidad EF Core (`CargoEntity`, `HabilidadEntity`, etc.)
- **WHEN** se ejecuta `ToDomain(TEntity)`
- **THEN** el mapper DEBE invocar directamente `Entidad.Reconstitute(...)`
- **AND** NO DEBE pasar por `PropertyInfo.SetValue` ni `SetProperty<T>`

### Requirement: REQ-124-2 — IL Guards estructurales

Cada entidad con `Reconstitute` DEBE tener un test IL estructural que verifique que ningún código del mapper reintroduce `PropertyInfo.SetValue` ni el helper `SetProperty<T>`.

#### Scenario: 6 IL guards verifican la ausencia de reflexión

- **GIVEN** la implementación actual de `PersistenceToDomainMapper` usa factories tipados
- **WHEN** se ejecutan los 6 tests `ToDomain_*_NoLlamaSetPropertyReflectionHelper`
- **THEN** los 6 DEBEN pasar (Cargo, Habilidad, Puesto, Persona, Ocupacion — 5 nuevos — más UnidadOrganizativa existente)
- **AND** cada test DEBE inspeccionar `MethodBody.GetILAsByteArray()`, decodificar tokens `0x28`/`0x6F`, y fallar si resuelve `SetProperty` declarada en `PersistenceToDomainMapper`

#### Scenario: Build limpio sin `using System.Reflection`

- **GIVEN** el archivo `PersistenceToDomainMapper.cs` fue limpiado
- **WHEN** se ejecuta `grep -n "System.Reflection\|PropertyInfo\|SetProperty" src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs`
- **THEN** DEBE devolver 0 hits
- **AND** `grep -rn "PropertyInfo\.SetValue" src/` DEBE devolver 0 hits

### Requirement: REQ-124-3 — Sin nuevas migraciones EF Core

El refactor de reconstitución NO DEBE introducir cambios en el schema de base de datos.

#### Scenario: Sin migraciones nuevas

- **GIVEN** el cambio solo modifica clases C# de dominio y el mapper
- **WHEN** se ejecuta `git status -- src/SGV.Infraestructura/Persistencia/Migraciones/`
- **THEN** DEBE estar limpio (sin archivos nuevos ni modificados)

## Trazabilidad

- PR: #136
- Branch: `fix/124-persistence-mapper-reconstitute`
- Commits: `ebd10db0`...`06458c94` (8 commits atómicos)
- apply-progress: `openspec/changes/2026-07-13-fix-124-persistence-mapper-reconstitute/apply-progress.md`
- verify-report: `openspec/changes/2026-07-13-fix-124-persistence-mapper-reconstitute/verify-report.md`
- Verificación de suites: 1585/1587 PASS (2 pre-existentes WebIntegration confirmados contra `develop`, sin regresión del change)
