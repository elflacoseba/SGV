# Design: Extraer `SGV.Contracts` (issue #100)

## Technical Approach

Refactor de namespace-only. La API ya serializa DTOs de `SGV.Aplicacion` sin re-empaquetar (`Ok(cargoDto)`, `[FromBody] CrearCargoRequest`); esos tipos ya son wire-contract. La propuesta los mueve a un nuevo classlib `SGV.Contracts` (sin dependencias de negocio) y deja el grafo `Dominio ← Aplicacion ← Contracts ← {Api, Web}`. No cambian payloads, endpoints, validaciones, autorización ni persistencia. El orden de PRs (Auth → Organizacion → Habilidades → Seguridad) queda fijado por `SkillCargoDetailDto` que consume `CargoDto` (Habilidades depende de Organizacion).

## Architecture Decisions

| # | Decisión | Alternativas | Rationale |
|---|----------|--------------|-----------|
| 1 | Nuevo `SGV.Contracts` (classlib `net10.0`, sin PackageReference de negocio) | a) Reutilizar `SGV.Api/Contracts/`. b) Carpeta compartida sin proyecto. | a) Acopla el contrato a la API. b) Sin enforce de grafo. |
| 2 | Nueva arista `Aplicacion → Contracts` al mover tipos | a) Duplicar tipos. b) Mover solo lo que Web importa. | a) Rompe Clean Architecture. b) `UsuarioServicioComandos` valida roles con `RolesSgv.TodosValidos`; sin la arista, Aplicacion no consume su propio `RolesSgv` migrado. |
| 3 | 4 PRs encadenados por capa | a) Migración total atómica. b) PR por archivo. | a) Blast radius ≈60 archivos. b) Cadena 30+ PRs sin valor. |
| 4 | Orden Auth → Organizacion → Habilidades → Seguridad | a) Empezar por la más grande. b) Seguridad primero. | `SkillCargoDetailDto` (Habilidades) importa `CargoDto` (Organizacion); invertir rompe build en PR3. |

## Data Flow

    SGV.Web ──HTTP──> SGV.Api ──> SGV.Aplicacion ──> SGV.Dominio
       │                  │              │
       └──────────── SGV.Contracts ──────┘
              (DTOs / Requests / Results / Errors / RolesSgv)

`SGV.Contracts` queda en arista horizontal: Api, Web y Aplicacion lo referencian.

## File Changes

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Contracts/SGV.Contracts.csproj` | Crear | Classlib `net10.0`, sin deps; `Nullable=enable`, `ImplicitUsings=enable`. |
| `src/SGV.Contracts/Auth/AuthApiRoutes.cs` | Crear (PR1) | Migrado desde `src/SGV.Api/Contracts/`. |
| `src/SGV.Contracts/Organizacion/{Comandos,Consultas/Dtos}/*.cs` | Crear (PR2) | `*Requests`, `*CommandResult`, todos los `*Dto`, `PagedResult`, enums de segmento. |
| `src/SGV.Contracts/Habilidades/{Comandos,Consultas/Dtos}/*.cs` | Crear (PR3) | `HabilidadRequests`, `HabilidadCommandResult`, `HabilidadDto`, `NivelHabilidadDto`, `SkillCargoDetailDto`, queries. |
| `src/SGV.Contracts/Seguridad/{RolesSgv,Usuarios}/*.cs` | Crear (PR4) | `RolesSgv`, `LoginRequest/Response`, `UsuarioDto`, `UsuarioCommandResult`, `UsuarioError`, `UsuarioErrorType`. |
| `src/SGV.Api/Contracts/` | Eliminar (PR1) | Trasladado a Contracts. |
| `src/SGV.Api/SGV.Api.csproj` | Modificar | PR1 sin cambios; PR4 suma `<ProjectReference>..\SGV.Contracts`. |
| `src/SGV.Aplicacion/SGV.Aplicacion.csproj` | Modificar (PR2) | Suma referencia a `SGV.Contracts` (abre la arista nueva). |
| `src/SGV.Web/SGV.Web.csproj` | Modificar (PR4) | Quita `<ProjectReference>SGV.Api</ProjectReference>`; suma `SGV.Contracts`. |
| `src/SGV.Web/**/*.cs` (38) | Modificar | `using SGV.Aplicacion.* → SGV.Contracts.*` por capa. |
| `src/SGV.Api/Controllers/**/*.cs` (11) | Modificar | `using` actualizado por capa. |
| `src/SGV.Aplicacion/{Organizacion,Habilidades,Seguridad,Ocupaciones,Personas}/**` | Modificar | `using` interno reescrito a `SGV.Contracts.*` para servicios que consumen DTOs. |
| `tests/SGV.Tests/{Api,Web,Seguridad}/**` + `ApiWebApplicationFactory.cs` | Modificar | `using` actualizado; constructores primarios preservados. |
| `SGV.slnx` | Modificar (PR1) | Insertar `SGV.Contracts` antes de `SGV.Api` y `SGV.Web`. |
| `AGENTS.md`, `docs/decisiones-implementacion.md` | Modificar (PR4) | Nota del nuevo proyecto; ajustar línea 83 con namespace actualizado. |

## Interfaces / Contracts

```
SGV.Contracts.Auth         → AuthApiRoutes (constantes de ruta).
SGV.Contracts.Organizacion → Comandos.* (Requests + CommandResults)
                              Consultas.Dtos.* (records, PagedResult<T>, enums)
SGV.Contracts.Habilidades  → Comandos.* / Consultas.Dtos.*
SGV.Contracts.Seguridad    → RolesSgv; Usuarios.LoginRequest/Response,
                              UsuarioDto, UsuarioCommandResult, UsuarioError, UsuarioErrorType.
```

Tipos que **NO** migran (quedan en `SGV.Aplicacion`): servicios e interfaces (`I*ServicioComandos`, `I*Repository`), `Compatibilidad`/`Ocupaciones`/`Personas` (sin consumidor Web), validadores FluentValidation, gateway interno `IUsuarioIdentityGateway`. Los nombres de tipo no cambian (`CargoDto` sigue siendo `CargoDto`); solo cambia el namespace → payload JSON bit-idéntico.

## Testing Strategy

| Capa | Qué valida | Cómo |
|------|------------|------|
| Integration API | Payload JSON idéntico por endpoint | Suite `tests/SGV.Tests/Api/**` deserializa DTOs migrados. |
| Integration API | Constructores primarios de DTOs | `ApiWebApplicationFactory.cs` + fakes. |
| Integration Web | Bridge cookie→JWT, listados segmentados, PRG | `tests/SGV.Tests/Web/**` (WebAuthenticationTests, CargoHabilidadesPageTests, etc.). |
| Estático (por PR) | Imports residuales | `grep -r "using SGV.Aplicacion" src/SGV.Web` → 0 al cerrar PR4; `grep -r "using SGV.Api.Contracts" src/` → 0 desde PR1. |
| Estático (PR4) | Grafo de proyectos | `dotnet list SGV.slnx reference`: `Web → Contracts` y `Web ⟂ Api`. |

Strict TDD se preserva: no se agregan tests por el refactor; la suite vigente ya cubre el comportamiento. La verificación por PR es "suite verde + grep limpio".

## Migration / Rollout

Sin migración de datos. Rollout = 4 PRs encadenados, cada uno con build+tests verdes:

- **PR1 (Auth)**: crear `SGV.Contracts`, mover `AuthApiRoutes`, eliminar `src/SGV.Api/Contracts/`, agregar `SGV.Contracts` a `SGV.slnx`, actualizar `AuthController`, `AuthApiClient` y `WebAuthenticationTests`.
- **PR2 (Organizacion)**: mover DTOs/Requests/Results/Errors; actualizar `Aplicacion` (abre arista nueva), Api controllers, Web (≈22 archivos: Pages/Organizacion + Integration/Organizacion), tests API.
- **PR3 (Habilidades)**: mover DTOs/Requests/Results; actualizar `Aplicacion` (re-resolver `Organizacion` desde Contracts), Api controllers, Web (≈6 archivos), tests.
- **PR4 (Seguridad + cleanup)**: mover `RolesSgv` y tipos de `UsuarioContracts`; actualizar `DatosSemilla`, Api controllers, Web (`SignIn.cshtml.cs`, `AuthApiClient`, `Pages/.../Cargos/{Habilidades,Cargos}.cshtml.cs`), tests (`UsuarioServicioComandosTests`, `UsuariosControllerTests`, `JwtReal*`). Quitar `Web → SGV.Api`. Actualizar `AGENTS.md` y `decisiones-implementacion.md`.

**Rollback por PR**: `git revert <sha>` restaura estado previo.
**Rollback global**: revertir PR4 → PR3 → PR2 → PR1 en orden inverso.

## Open Questions

Ninguna. El orden de PRs está fijado por evidencia (`SkillCargoDetailDto → CargoDto`); la lista de qué queda fuera (Compatibilidad, Ocupaciones, Personas, validadores, gateway) está fijada por ausencia de consumidores Web.