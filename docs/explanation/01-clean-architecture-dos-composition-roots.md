# Clean Architecture en SGV: por qué dos composition roots

## La motivación histórica

Cuando SGV nació como un solo ejecutable monolítico, `Dominio`, `Aplicacion`,
`Infraestructura` y un proyecto web vivían detrás de un único composition root.
La separación de capas era disciplinada — los namespaces respetaban las
dependencias — pero no había forma de que un cliente externo del sistema
consumiera la lógica sin pasar por el proceso web. La consecuencia operativa
era simple: cualquier integración que requería hablar con la API tenía que
clonar el código de los DTOs o montar el host completo. La duplicación de
contratos y el riesgo de drift entre "lo que ve la Web" y "lo que acepta la
API" eran permanentes.

El corte se hizo alrededor del cambio `2026-07-10-extraer-contratos-sgv`:
se separó `SGV.Api` como host, se introdujo `SGV.Contracts` como leaf
transversal, y `SGV.Web` quedó como shell que sólo habla con `Contracts` y
con la API por HTTP. Esta no fue una refactorización estética: el objetivo
era eliminar la asimetría entre los DTOs que viajaban en el wire y los que
la Web ya conocía de memoria.

## La solución adoptada

El grafo actual puede dibujarse así:

```
                +-----------+         +-----------+
                |  Dominio  |  <----- | Aplicacion|
                +-----------+         +-----------+
                                            ^
                                            |
                                    +-----------------+
                                    | Infraestructura |
                                    +-----------------+
                                            ^
                +-----------+                 |
                | Contracts |  <-------------+-----+
                +-----------+                       |
                       ^                            |
                       |                  +--------+--------+
                       +------------------+                 |
                                          |                 |
                                   +-------------+     +-----------+
                                   |  SGV.Api    |     |  SGV.Web  |
                                   +-------------+     +-----------+
```

Las observaciones que siguen no son metas sino consecuencias del grafo
declarado en `SGV.slnx` y en los `*.csproj` de cada proyecto.

`Dominio` no conoce a nadie. Es el corazón. `Aplicacion` lo conoce y nada
más — siquiera ignora que existe HTTP. `Infraestructura` implementa los
contratos de `Aplicacion` y los mapea contra EF Core, MySQL, MailKit y
JWT. `Contracts` es leaf: no depende de nadie en el grafo, y cualquier
proyecto puede referenciarlo sin acoplar capas.

`SGV.Api` es el composition root del backend. Conoce `Aplicacion`,
`Infraestructura` y `Contracts`. Es el único host donde se monta el
`DbContext`, el `SaveChangesInterceptor`, el middleware JWT bearer y
los controladores. `SGV.Web` es el composition root del shell web.
Conoce únicamente `Contracts` — más un único archivo de utilidad,
`HealthCheckResponseWriter.cs`, que se linkea por `<Compile Include>` y
no por `ProjectReference`. Esta asimetría entre `Api` y `Web` no es un
accidente: el shell web no debería poder importar EF Core, ni leer
`SgvDbContext`, ni ver las clases internas de Infraestructura.

`Contracts` evita la duplicación de wire-types. Un `PersonaDto`, un
`LoginResponse` o un `AuditoriaDto` se declaran una sola vez en
`SGV.Contracts` y son consumidos tanto por `SGV.Api` (que los serializa
hacia el cliente) como por `SGV.Web` (que los deserializa en sus
clientes tipados). Cambiar un nombre de propiedad en un record de
`Contracts` produce un error de compilación simultáneo en ambos hosts.

`SGV.Web` referencia `Contracts` y nada más del backend. Esto se
verifica materialmente en `SGV.Web.csproj`: el único `<ProjectReference>`
apunta a `..\SGV.Contracts\SGV.Contracts.csproj`. La consecuencia es
que el shell nunca puede llamar directamente al DbContext ni a un
servicio de aplicación — debe hacerlo via HTTP contra `SGV.Api`.

## Trade-offs y alternativas descartadas

La decisión original podría haber sido "Web referencia Api como proyecto
compartido". Esto era común en plantillas de ASP.NET y daba la ilusión de
una sola fuente de verdad sin HTTP. Se descartó porque:

- Si `Web` pudiera importar clases internas de `Api`, dejaría de haber un
  corte limpio: el día que un endpoint cambie su firma interna, `Web`
  podría compilar contra el contrato nuevo sin pasar por la red y
  esconder regresiones que sólo aparecen en runtime.
- El gráfico de despliegue se vuelve confuso: si ambos procesos terminan
  en el mismo binario, no hay forma de escalar la API por separado ni de
  moverla a otro runtime. Mantener dos `<AssemblyName>` separados preserva
  la opción.
- El `ProjectReference` directo arrastra transitive dependencies. Hoy
  `SGV.Web` arrastra `Microsoft.AspNetCore.Authentication.JwtBearer` vía
  `System.IdentityModel.Tokens.Jwt 8.14.0`, pero nada de EF Core ni
  Pomelo. Si la API sumara `StackExchange.Redis`, el shell no pagaría el
  costo de compilación.

La alternativa "Contracts no existe y Web duplica los records" fue
descartada porque el costo de mantener dos copias divergentes se paga
rápido. Cualquier nuevo campo en `PersonaDto` (ver
`openspec/changes/archive/2026-07-14-frontend-crud-personas/`) requiere
sincronizar manualmente tres archivos si Contracts no existe; con
Contracts, el compilador hace ese trabajo.

La opción "Contracts conoce a Aplicacion" también fue descartada: hubiera
permitido que `Contracts` heredara tipos como `Persona` del dominio, pero
hubiera arrastrado reglas de negocio (validaciones, invariantes) al wire
contract, donde no las queremos. `Contracts` se mantiene deliberadamente
"tonto": sólo records de transporte, constantes de catálogo y opciones
de configuración.

## Consecuencias operativas

El grafo impone tres disciplinas que el equipo debe sostener en el día a
día.

**Cambiar un record en `Contracts` rompe ambos hosts simultáneamente.**
Esto es deliberado. Si se renombra `PersonaDto.Nombres` por
`PersonaDto.FirstName`, el cambio aparece como rojo en `SGV.Web` (donde
el cliente tipado intenta leer `Nombres`) y en `SGV.Api` (donde el
controlador la asigna). La unificación a través del compilador es lo que
hace que el wire y los call sites no puedan separarse en silencio.

**Tests de integración pueden montar ambos hosts por separado.** El
proyecto `SGV.Tests` levanta `SgvApiApplicationFactory` y
`SgvWebApplicationFactory` como hosts independientes. Cada uno arranca
con su propia clave JWT, su propio `AuthSessionFactory` (issue #121) y
sus propias opciones de cookies. Esto es lo que hace que un cambio en la
clave de firma del backend no afecte a los tests que sólo montan la Web
— y viceversa.

**El `<Compile Include>` de `HealthCheckResponseWriter.cs` es la única
excepción a la regla de "Web no comparte archivos con Api".** Se eligió
linkear un único archivo chico porque produce un JSON health canónico
para ambos hosts sin duplicar código y sin que `Web` tenga que
referenciar el proyecto `Api`. Si en el futuro esa excepción se
extiende, conviene re-evaluar: cada archivo nuevo que cruza el límite
es una señal de que el corte de capas podría no estar donde debe.

## Referencias

- `../reference/03-wire-types-contracts.md` — qué vive exactamente en `SGV.Contracts` y qué no.
- `../reference/06-pipeline-middleware-api.md` — composición concreta del lado `SGV.Api`.
- `../reference/07-pipeline-arranque-web.md` — composición concreta del lado `SGV.Web`.
- `../tutorials/04-primer-cambio-clean-architecture.md` — primer cambio end-to-end respetando el grafo.
- `docs/decisiones-implementacion.md` — sección "Política de paralelismo en la suite de tests" y "Cultura regional es-AR" para entender cómo `InternalsVisibleTo` se gestiona entre capas.