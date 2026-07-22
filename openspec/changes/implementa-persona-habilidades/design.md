# Diseño técnico: Implementa Persona-Habilidades

## Resumen ejecutivo

Se completará el flujo web sobre el backend PersonaSkill existente, sin tocar dominio, persistencia, endpoints ni migraciones. Los wire-types `PersonaSkill*` se moverán atómicamente desde `SGV.Aplicacion.Personas` a `SGV.Contracts.Personas`, preservando nombres JSON y usando `NivelHabilidadId`. `IPersonaApiClient` incorporará consulta, upsert y baja mediante el mismo bridge cookie→JWT y mappers comunes de errores. La nueva Razor Page será admin-only y seguirá la paridad de `CargoHabilidades`, con handlers POST separados y PRG. Una persona inactiva se bloqueará con redirección a la página de error 404/estado no disponible, evitando cargar o mutar el subrecurso desde Web. El drift archivado del query contract se resolverá alineando la documentación con el wire actual anidado (`skill`/`nivel`), sin enriquecer el backend.

## Mapa de capas y archivos

| Área | Archivos concretos | Cambio |
|---|---|---|
| Contracts | `src/SGV.Contracts/Personas/Comandos/*`, `Consultas/Dtos/*` | Crear/mover requests, DTOs, results y `Categoria`; borrar duplicados de Aplicación. |
| Aplicación/API | `src/SGV.Aplicacion/Personas/Comandos/PersonaSkill*`, servicios/interfaces; `src/SGV.Api/Controllers/PersonasController.cs`; `Infrastructure/Results/ApiResults.cs` | Actualizar usings y mappers sin cambiar lógica ni HTTP. |
| Web integración | `src/SGV.Web/Integration/Personas/ApiClients/*`, contratos/fakes | Añadir `GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`; reutilizar `CommandResultMapper`/`DeleteResultMapper`. |
| Razor | `Pages/Personas/PersonaHabilidades.cshtml(.cs)`, `Pages/Personas/Details.cshtml` | Grilla, alta/edición/baja, autorización, mensajes PRG y enlace condicionado a persona activa. |
| Tests | `tests/SGV.Tests/Web/Persona/*`, compatibilidad/API si aplica | RED primero: contratos, cliente fake, autorización, PRG, errores y anti-drift. |

No se modifican `SGV.Dominio`, `SGV.Infraestructura`, migraciones ni el mapa de bloques GUID: no hay catálogo nuevo ni esquema afectado.

## Contratos, errores y flujo

La migración es atómica: actualizar en una misma unidad los consumidores de API, Aplicación, Web y tests; no mantener período de superposición ni duplicados. El JSON conserva el shape vigente. El query de detalle usa los objetos anidados actuales `skill` y `nivel`; no se agregan `skillId`/`nivelId` planos al backend.

`PersonaSkillErrorType` deja de ser público. `PersonaSkillError.Categoria` usa `ErrorCategoria` y `PersonaSkillDeleteResult` conserva `StatusCode` como metadata:

| Error actual | Categoría | HTTP |
|---|---|---|
| `PersonaNoEncontrada`, `HabilidadNoEncontrada`, `AsociacionNoEncontrada` | `NotFound` | 404 |
| `NivelHabilidadNoExiste`, `DatosInvalidos`, `OperacionInvalida` | `Validation` | 400 |
| no documentado / transporte | `Unexpected` / `Transport` según mapper común | status observable |

API continúa delegando en `ApiResults`; Web no replica switches privados. El cliente usa `ApiBearerTokenHandler` y el patrón existente de retries/fallas recuperables (`HttpRequestException`, timeout, JSON), dejando cancelación explícita sin convertirla en mensaje de negocio.

Flujo: `PersonaHabilidades PageModel → IPersonaApiClient → ApiBearerTokenHandler → PersonasController → IPersonaSkillServicio → repositorio existente`.

## Razor y UX

`PersonaHabilidades.cshtml.cs` tendrá `[Authorize(Roles = RolesSgv.Administrador)]`, GET para cargar persona/lista y handlers POST separados para upsert y delete, todos con antiforgery y `RedirectToPage`/PRG. El formulario de fila enviará `NivelHabilidadId`; la vista mantendrá el look-and-feel Inspinia y mostrará estado vacío y `TempData` (`StatusMessage`/`StatusKind`).

Por decisión cerrada, una persona inactiva se trata como no consultable: el GET redirige a `/error/404` (o estado equivalente ya usado por la shell) y los POST rechazan antes de invocar el cliente. Details solo renderiza `Habilidades` para persona activa y administrador; el backend continúa siendo la autoridad y responde 404 ante inactividad.

## Orden de aplicación y testing

1. RED de contratos/mapeo y anti-drift; mover tipos y actualizar usings en Api/Aplicación/tests.
2. GREEN del cliente tipado y fakes, verificando bridge, resultados y errores.
3. RED/GREEN del PageModel y Razor: autorización, GET, upsert/delete, persona inactiva y PRG.
4. Enlazar Details y ejecutar compatibilidad de wire JSON.

Validar `dotnet build SGV.slnx`, `dotnet test SGV.slnx` y, por cambios Web, `bun install`/`bun run build` desde `src/SGV.Web`. No ejecutar ni crear migraciones EF; la persistencia no cambia. Si se toca `tests/SGV.Tests`, aplicar además las tres corridas consecutivas `dotnet test SGV.slnx --no-build` exigidas por la guía.

## Estimación y rollout

| Área | Estimación |
|---|---:|
| Contracts/API/Aplicación | 70–110 líneas |
| Cliente y fakes | 90–130 |
| Razor/PageModel/Details | 180–260 |
| Tests significativos | 160–230 |
| **Total** | **500–730** |

El forecast supera 400 líneas. Se sugieren slices candidatos: (1) migración Contracts + taxonomía + tests; (2) cliente tipado + fakes + tests; (3) Razor Page + Details + tests. Delivery queda sujeto a `ask-always`; no se fuerza la división aquí. Cada slice debe compilar y tener rollback por reversión de sus archivos, sin datos persistidos que revertir.

## Riesgos y decisiones abiertas

- **Alto**: migración incompleta deja referencias a Aplicación o DTOs duplicados; bloquear con build y test estructural.
- **Medio**: drift JSON archivado; documentar alineación al wire anidado y test de deserialización.
- **Medio**: forecast excede presupuesto; pedir confirmación antes de tasks.

No quedan decisiones de producto abiertas. `sdd-tasks` debe confirmar únicamente el particionado final y el orden de PRs; no reabrir las decisiones congeladas.
