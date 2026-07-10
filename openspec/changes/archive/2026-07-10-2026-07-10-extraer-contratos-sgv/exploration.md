## Exploration: extraer `SGV.Contracts` (issue #100)

### Current State
- `src/SGV.Api/Contracts/` contiene un único archivo: `AuthApiRoutes.cs` (clase estática con `Base`, `LoginRelative`, `Login`). Es el único material vivo del contrato compartido.
- La verdadera frontera que se rompe NO es `SGV.Api.Contracts` (1 archivo en Web) sino `SGV.Aplicacion.*`: 38 archivos en `SGV.Web` lo importan.
- Distribución de `using SGV.<capa>` en `src/SGV.Web/**.cs`:
  - `SGV.Aplicacion` → 38 archivos (Web completo).
  - `SGV.Api.Contracts` → 1 archivo (`Integration/Auth/AuthApiClient.cs`).
  - `SGV.Api.*` (resto) → 0.
  - `SGV.Infraestructura` → 0.
  - `SGV.Dominio` → 0.
- Grafo de proyectos (`*.csproj`):
  - `SGV.Web.csproj` → `SGV.Api.csproj` (única referencia; arrastra transitivamente `Aplicacion` e `Infraestructura`).
  - `SGV.Api.csproj` → `SGV.Aplicacion` + `SGV.Infraestructura`.
  - `SGV.Aplicacion.csproj` → `SGV.Dominio`.
  - `SGV.Infraestructura.csproj` → `SGV.Dominio` + `SGV.Aplicacion`.
- `SGV.Api/Controllers/*.cs` serializan los DTOs de `SGV.Aplicacion.*Consultas.Dtos` y reciben `SGV.Aplicacion.*Comandos` **sin re-empaquetar** (`Ok(cargoDto)`, `[FromBody] CrearCargoRequest`). Los tipos de Aplicacion son literalmente el contrato de wire.
- `SGV.slnx` lista los 6 proyectos actuales; se debe agregar `SGV.Contracts` antes de `SGV.Api` y `SGV.Web` para preservar el orden lógico.
- `SGV.Web/Integration/` agrupa tres carpetas (`Auth`, `Organizacion`, `Habilidades`) con 30 archivos `.cs`: clientes tipados, interfaces y helpers de mapeo. Todos terminan devolviendo o recibiendo DTOs/requests/results de `SGV.Aplicacion`.
- Tests (`tests/SGV.Tests`):
  - `Api/ApiWebApplicationFactory.cs` instancia DTOs literalmente (`new CargoDto(...)`, `new CrearCargoRequest(...)`) para los fakes de servicios. Al migrar los records, los constructores migran con ellos.
  - `Web/WebAuthenticationTests.cs` usa `LoginRequest`, `LoginResponse` y `AuthApiRoutes` para serializar la respuesta del handler fake.
  - 11 archivos de controllers en `SGV.Api/Controllers/` importan los DTOs/requests migrables.
  - `SGV.Aplicacion.Compatibilidad` (servicio de matching persona↔habilidad) **no** es consumido por Web, así que queda intacto.

### Affected Areas
- `src/SGV.Api/Contracts/AuthApiRoutes.cs` — única carpeta `Contracts` actual a eliminar tras la migración.
- 38 archivos en `src/SGV.Web/**/*.cs` (Pages/Organizacion/*, Integration/*, Pages/Auth/SignIn.cshtml.cs) — actualizar `using SGV.Aplicacion.*` → `using SGV.Contracts.*`.
- `src/SGV.Api/Controllers/{Auth,Cargos,Puestos,UnidadesOrganizativas,Skills,Personas,Ocupaciones,Usuarios,NivelesCargo,NivelesHabilidad,TipoUnidadesOrganizativas}Controller.cs` (11 archivos) — actualizar imports.
- `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` y los 17 archivos de tests en `tests/SGV.Tests/Api/` — actualizar imports.
- `tests/SGV.Tests/Web/WebAuthenticationTests.cs` y `tests/SGV.Tests/Seguridad/JwtRealAuthTests.cs` — actualizar imports.
- `src/SGV.Aplicacion/{Organizacion,Habilidades,Seguridad}/*.cs` — `using` interno apunta a sus propios subnamespaces hoy; algunos archivos se quedan con auto-referencias o necesitan reescritura si Contracts re-namespacea.
- `SGV.slnx` — agregar el nuevo proyecto.
- `src/SGV.Api/SGV.Api.csproj`, `src/SGV.Web/SGV.Web.csproj`, `src/SGV.Aplicacion/SGV.Aplicacion.csproj`, `tests/SGV.Tests/SGV.Tests.csproj` — referencias de proyecto.
- `docs/decisiones-implementacion.md` línea 83 (menciona `ActualizarUnidadOrganizativaRequest` por nombre) — sigue válida porque solo cambia el namespace.
- `AGENTS.md` secciones 22-24 ("Auth", "Frontend", "Estructura") — agrega nota del nuevo proyecto `SGV.Contracts`.

### Approaches
1. **Migración total en un solo PR** — crear `SGV.Contracts`, mover todos los DTOs/requests/results/errors en un cambio atómico; actualizar Api, Web, Aplicacion y tests en el mismo commit.
   - Pros: corte limpio, un solo migration commit, suite verde de una vez o se sabe exactamente qué falló.
   - Cons: blast radius grande (≈60 archivos tocados); un fallo rompe build de toda la solución temporalmente.
   - Effort: High

2. **Migración por capas en PRs encadenados** — PR1 = crear `SGV.Contracts` + mover `AuthApiRoutes` (mínimo); PR2 = `Organizacion`; PR3 = `Habilidades`; PR4 = `Seguridad` y `RolesSgv`. Cada PR deja la solución compilando y los tests pasando.
   - Pros: blast radius controlado por PR; regresiones se aíslan; cada merge es revisable en 30-90 min (alineado con `chained-pr` skill).
   - Cons: PR1 aislada tiene poco valor visible (solo `AuthApiRoutes`); riesgo de "intermediate state confunde".
   - Effort: High (sumado)

3. **Migración total con PR-encadenado por capa (recomendada)** — la estrategia 2 pero garantizando que cada PR cierre un subconjunto completo (namespace + consumidores + tests del namespace).
   - Pros: balance ideal entre seguridad y avance; cumple la regla de "no dejar la solución rota entre PRs".
   - Cons: requiere disciplina para no mezclar capas en un mismo PR.
   - Effort: High

### Recommendation
- Adoptar enfoque 3 (PR encadenado por capa) en este orden: (a) crear `SGV.Contracts` y mover solo `AuthApiRoutes` → namespace `SGV.Contracts.Auth`; (b) Organizacion (DTOs, requests, results, errors, enums de segmento); (c) Habilidades; (d) Seguridad (`RolesSgv`, `LoginRequest`, `LoginResponse`, `UsuarioDto`, `UsuarioCommandResult`, `UsuarioError`, `UsuarioErrorType`); (e) eliminar `src/SGV.Api/Contracts/` y borrar la referencia `SGV.Web → SGV.Api`. Mantener compatibilidad de marcas de tiempo: el name del tipo no cambia (`CargoDto` sigue siendo `CargoDto`), solo el namespace.
- Grafo limpio post-cambio: `Dominio ← Aplicacion ← Contracts ← {Api, Web}`. `Aplicacion` consume `SGV.Contracts` porque sus servicios (`UsuarioServicioComandos`) validan roles usando `RolesSgv.TodosValidos(...)`.
- Tarea final: actualizar `AGENTS.md` y `decisiones-implementacion.md` con la nota "el contrato HTTP vive en `SGV.Contracts`". Mantener la línea 83 (Codigo inmutable) intacta salvo ajuste de namespace en el comentario.

### ⚠️ Alerta sobre decisiones
- Las tres decisiones del usuario **se sostienen con la evidencia**:
  - **Migración total** + **namespace `SGV.Contracts`** — la API no re-packagea DTOs, así que mover y re-namespacear no exige mapper nuevo.
  - **DTOs en lugar de tipos de Aplicacion en Web** — para la sub-capa wire-shared (records/enums de `Comandos`, `Consultas.Dtos`, `Seguridad`) hay docenas de tipos. Para `Compatibilidad` (que Web no consume) NO hace falta mover.
- Evidencia contradictoria: **ninguna**. `CargoDto` y compañía siguen siendo la única verdad que cruza la frontera Api↔Web; moverlas es seguro.

### Risks
- Riesgo **fuerte**: dependencia transitiva de `RolesSgv` desde dentro de Aplicacion. Mover `RolesSgv` a `SGV.Contracts` exige que `SGV.Aplicacion` referencie `SGV.Contracts` (una nueva arista hacia abajo en el grafo de capas). El grafo se mantiene limpio (`Dominio ← Aplicacion ← Contracts`), pero conviene declararlo explícito en `design.md`.
- Riesgo **medio**: los 38 archivos de Web se actualizan en lote; un merge conflict simultáneo con `develop` puede dividir el cambio en dos commits huérfanos. Mitigación: PRs por capa (enfoque 3).
- Riesgo **medio**: `decisiones-implementacion.md` línea 83 menciona `ActualizarUnidadOrganizativaRequest` por nombre simple; el párrafo sigue válido pero conviene un diff rápido para ajustar el namespace en la explicación.
- Riesgo **bajo**: tests `ApiWebApplicationFactory.cs` (902 líneas) instancian muchos DTOs en constructores primarios — un cambio de namespace obliga a tocar el archivo entero. Concentrar el cambio en un solo PR para que un fallo sea fácil de revertir.
- Riesgo **bajo**: `SGV.Api/Controllers/AuthController.cs` usa `[Route(AuthApiRoutes.Base)]`. La constante sigue existiendo pero cambia el namespace del import.

### Ready for Proposal
**Yes.** Las decisiones están ratificadas, no hay evidencia que las invalide, y existe un plan concreto (enfoque 3). El orquestador puede delegar a `sdd-propose` con la tranquilidad de que el grafo posterior será: Dominio ← Aplicacion ← Contracts ← {Api, Web}, y la API dejará de ser dependencia de Web. El next step natural es redactar `proposal.md`, `design.md`, `tasks.md` y `specs/**/spec.md` (delta), todo en español, con PRs encadenados por capa.
