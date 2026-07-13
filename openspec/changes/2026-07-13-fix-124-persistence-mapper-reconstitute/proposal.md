# Proposal: Refactor de `PersistenceToDomainMapper` para eliminar reflexión (issue #124)

> **Resumen ejecutivo.** Hoy `PersistenceToDomainMapper` muta entidades de Dominio con `PropertyInfo.SetValue` + `BindingFlags.NonPublic`, saltando el chequeo de `init`/`private set`. Vamos a introducir un ctor `internal static Reconstitute(...)` en 6 entidades (Cargo, Habilidad, Puesto, Persona, Ocupacion, UnidadOrganizativa) que acepta todos los campos persistibles y los asigna con setters tipados, eliminando el helper `SetProperty<T>`. NO migramos a `record init` total, NO tocamos `DomainToPersistenceMapper`, `*Entity`, migraciones, auditoría ni contratos HTTP.

## Intent

Issue GitHub #124 (`tech-debt`, `refactor`, `persistence`): el path MySQL → Dominio rompe el contrato de inmutabilidad y haría fallar silenciosamente cualquier futura migración a `init`-only. El refactor elimina la reflexión, extiende el patrón vigente en `UnidadOrganizativa` a las otras 5 entidades mediante un factory simétrico, y suma tests IL que detectan reintroducción del helper. Mantiene el comportamiento observable (schema, contratos, resultados de consulta).

## Scope (in-scope)

| Categoría | Deliverable |
|---|---|
| Dominio (6 entidades) | Agregar `internal static Reconstitute(...)` con todos los campos persistibles en `Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion`. `UnidadOrganizativa` abandona el patrón `with` para paridad total (decisión del usuario) y reescribe `Actualizar` / `DefinirVigencia` / `CambiarUnidadPadre` / `Activar` / `Desactivar` con `private set`. |
| Infraestructura | `PersistenceToDomainMapper.cs`: eliminar helper `SetProperty<T>` (líneas 225-232) y `using System.Reflection;`; reemplazar los 12 call sites por invocación directa del factory. |
| `.csproj` | `<InternalsVisibleTo Include="SGV.Tests" />` en `SGV.Dominio.csproj` (cambio trivial, paralelo a `SGV.Infraestructura.csproj:25-29`). |
| Tests | 5 tests IL estructurales (1 por entidad, replicando el patrón de `UnidadOrganizativaRepositoryTests.cs:984-1045`) + ampliar `OcupacionMapperTests` / crear 4 archivos paralelos (`CargoMapperTests`, `HabilidadMapperTests`, `PuestoMapperTests`, `PersonaMapperTests`) con cobertura de round-trip, `IsActive=false`, nav properties opcionales. |

## Non-goals

- Migrar a `record init` total en todas las entidades (Opción 2 del exploration: blast radius >1000 LoC, descartada).
- Tocar `DomainToPersistenceMapper`, clases `*Entity`, migraciones EF Core, `AuditoriaSaveChangesInterceptor`, contratos HTTP o la shell web.
- Modificar `Cargo.Desactivar()` ni su invariante `_puestos` activos (mantener el silenciado actual; tratar como issue aparte en `archive-report`).
- Actualizar `docs/decisiones-implementacion.md` — queda diferido al `archive-report` según decisión del usuario.

## Capabilities

- **New Capabilities**: None.
- **Modified Capabilities**: None. El refactor cumple textualmente `Observable Persistence Invariants` de `sgv-persistence-architecture`: schema idéntico, contratos idénticos, comportamiento de repositorio idéntico. No requiere delta spec.

## Approach

Opción 1 del exploration, ampliada con la decisión de llevar `UnidadOrganizativa` también a `Reconstitute` (paridad total). Firma típica:

```csharp
internal static Cargo Reconstitute(
    Guid id, string codigo, string nombre, Guid nivelId, string? descripcion,
    bool isActive, NivelCargo? nivelCargo,
    /* + audit fields: createdAt/createdByUserId/updatedAt/updatedByUserId,
         isDeleted/deletedAt/deletedByUserId */)
{
    var self = new Cargo(codigo, nombre, nivelId, descripcion) { Id = id };
    self.IsActive = isActive;     // private set, válido intra-clase
    self.NivelCargo = nivelCargo; // nav opcional
    return self;
}
```

Variantes por entidad (validadas en `sdd-design`):

- `Persona.Reconstitute` acepta `telefono` / `tipoDocumento` / `numeroDocumento` explícitos y los asigna vía `private set` (decisión cerrada por el usuario).
- `Ocupacion.Reconstitute` replica la validación `FechaFin >= FechaInicio` del ctor primario para preservar la invariante actual.
- `UnidadOrganizativa.Reconstitute` reemplaza la cadena `with` actual; los métodos mutadores (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) se reescriben para asignar con `private set` en vez de devolver nueva instancia. Las colecciones internas (`_unidadesHijas`, `_puestos`) sobreviven al ciclo de hidratación porque el mapper nunca las reconstituye — son pobladas por repositorios a través de métodos de negocio.

## Affected Areas

| Path | Rol |
|---|---|
| `src/SGV.Dominio/Organizacion/Cargo.cs` | Prod — agrega `Reconstitute` |
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Prod |
| `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | Prod — pierde `with`, gana `Reconstitute` + reescritura de mutadores |
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Prod |
| `src/SGV.Dominio/Personas/Persona.cs` | Prod |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Prod |
| `src/SGV.Dominio/SGV.Dominio.csproj` | Prod — `InternalsVisibleTo("SGV.Tests")` |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Prod — sin `SetProperty` ni `System.Reflection` |
| `tests/SGV.Tests/Persistencia/CargoMapperTests.cs` (nuevo) | Tests IL estructural + comportamiento |
| `tests/SGV.Tests/Persistencia/HabilidadMapperTests.cs` (nuevo) | Tests IL estructural + comportamiento |
| `tests/SGV.Tests/Persistencia/PuestoMapperTests.cs` (nuevo) | Tests IL estructural + comportamiento |
| `tests/SGV.Tests/Persistencia/PersonaMapperTests.cs` (nuevo) | Tests IL estructural + comportamiento |
| `tests/SGV.Tests/Persistencia/OcupacionMapperTests.cs` (existente) | Ampliar con test IL estructural + cobertura adicional |

## Risks

| Sev | Riesgo | Mitigación |
|---|---|---|
| MED | `Cargo` reconstituido con `IsActive=false` silencia la invariante `_puestos` activos (ya sucede hoy por reflexión). | Documentar en `archive-report`; abrir issue aparte para endurecer `Cargo.Desactivar()` con un test de invariante explícito. |
| MED | `Persona` no expone setters externos para `Telefono` / `TipoDocumento` / `NumeroDocumento`. | `Reconstitute` acepta esos parámetros explícitos y los asigna vía `private set` (decisión cerrada por el usuario, sin modificar el contrato público). |
| MED | Inexistencia de tests IL estructurales para las 5 entidades restantes (solo UO lo tiene). | El change introduce esos tests como deliverable explícito; ciclo RED → GREEN obligatorio por `strict_tdd: true`. |
| LOW | `Ocupacion.Reconstitute` debe validar `FechaFin >= FechaInicio`. | Replicar la validación del ctor primario dentro del factory. |
| LOW | `InternalsVisibleTo("SGV.Tests")` no existe hoy en `SGV.Dominio.csproj`. | Agregar al `.csproj` en el mismo PR (cambio trivial, paralelo a `SGV.Infraestructura.csproj`). |
| LOW | Orden de operaciones en `Reconstitute` afecta a `EsVigente` (Ocupacion: `FechaFin is null && !IsDeleted`). | Documentar orden canónico en XML doc: `audit + IsDeleted` → `FechaFin` → `Persona` / `Puesto` nav. |

Sin riesgos CRITICAL ni HIGH.

## Acceptance Criteria (mapeado a issue #124)

- `PersistenceToDomainMapper.cs` no contiene referencias a `PropertyInfo.SetValue` ni a `BindingFlags.NonPublic`.
- `grep -rn "SetProperty\|PropertyInfo" src/SGV.Infraestructura/` retorna 0 hits.
- Las 6 entidades (Cargo, Habilidad, Puesto, Persona, Ocupacion, UnidadOrganizativa) exponen `internal Reconstitute(...)` consumible desde `SGV.Tests`.
- 5 tests IL estructurales nuevos verdes; el test IL existente de `UnidadOrganizativa` sigue verde sin regresión.
- `dotnet build SGV.slnx` y `dotnet test SGV.slnx` verdes en las suites Dominio, Aplicacion, Persistencia, API, Web y Compatibilidad.
- 0 migraciones EF Core nuevas; 0 cambios de schema, contratos HTTP, ni archivos de auditoría.

## Rollback Plan

`git revert` del PR único. Como no tocamos migraciones ni schema, el revert deja el repositorio equivalente al estado previo. Si el helper `SetProperty` ya fue eliminado, restaurar `PersistenceToDomainMapper.cs` desde git (`git checkout HEAD~1 -- src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs`) y revertir los ctors `Reconstitute` añadidos al Dominio. Los tests verdes originales reaparecen automáticamente porque no se modifican archivos de test más allá de los nuevos (que entran junto con el revert).

## Dependencies

- `InternalsVisibleTo("SGV.Tests")` agregado a `SGV.Dominio.csproj` para que los tests puedan invocar el factory `internal`.
- xUnit 2.9.2 + `Microsoft.NET.Test.Sdk` ya disponibles; no requiere paquetes nuevos.

## Success Criteria

- [ ] 0 usos de `PropertyInfo.SetValue` en `src/`.
- [ ] 6 entidades con `internal Reconstitute(...)`.
- [ ] 5 tests IL estructurales verdes.
- [ ] `dotnet build` + `dotnet test` verdes en todas las suites.
- [ ] 0 migraciones EF Core nuevas.

## Size Forecast

| Categoría | LoC estimado |
|---|---|
| Dominio (6 ctors + `.csproj` + reescritura de UO sin `with`) | ~150 |
| Infraestructura (`PersistenceToDomainMapper.cs`) | ~30 |
| Tests (5 IL estructurales + ~5 archivos de comportamiento) | ~200 |
| **Total** | **~380** |

Dentro del budget de 400 LoC. Si `sdd-tasks` detecta un tamaño >400 al desglosar, el orquestador preguntará antes de `apply` según la `delivery strategy: ask-always`.

## Open Questions

Ninguna. Las decisiones del usuario cierran toda ambigüedad: `UnidadOrganizativa` también adopta `Reconstitute` (paridad total), `Persona` recibe los tres campos opcionales como parámetros explícitos no-nullable en `Reconstitute`, `Cargo.Desactivar` queda fuera de scope y las actualizaciones de `docs/decisiones-implementacion.md` se difieren al `archive-report`.
