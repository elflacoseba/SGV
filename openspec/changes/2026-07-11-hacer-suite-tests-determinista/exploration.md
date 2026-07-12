# Exploración: Suite de tests no determinista (issue #121)

> Cambio: `2026-07-11-hacer-suite-tests-determinista`
> Modo de artefacto: híbrido (OpenSpec + Engram)
> Strict TDD: sí (`openspec/config.yaml:11`)

## Estado actual

La suite `tests/SGV.Tests` (1713 tests al corte `develop @ 7b3325a3`) corre en
forma no determinista: pasa en subconjuntos aislados (Web 481/481 en 54s,
API 360/360 en 47s, Persistencia sin Ocupacion 217/217 en 4s, Auth bridge
29/29 en 1s) pero la corrida completa agota `MSB4166` y el mensaje
`Timed out waiting for entry point to build the IHost` a 300s y 900s. La
auditoría disponible (sesión `sgv-issue-121-2026-07-11`) ya señala cuatro
causas probables; esta exploración verifica cada una contra el código real.

### Causa 1 — Cache estático de `TokenValidationParameters` en `AuthSessionFactory`

`src/SGV.Web/Integration/Auth/AuthSessionFactory.cs`:

- L12: `internal static class AuthSessionFactory`.
- L14: `private static TokenValidationParameters? _cachedValidationParameters;`
- L15: `private static readonly object _cacheLock = new();`
- L70-81: `GetOrCreateValidationParameters` implementa double-checked locking con
  `if (_cachedValidationParameters is null)` que solo se evalúa una vez por
  proceso: el primer host que invoque la cache gana y la referencia queda
  congelada para toda la vida del AppDomain del test runner.
- Único consumidor real en runtime: `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs:54`
  invoca `AuthSessionFactory.CreatePrincipal(...)` durante el POST del cookie
  sign-in. La cache no es vaciada en dispose de host.

Verificación de impacto inmediato: `SGV.Web/appsettings.Development.json:7`,
`SGV.Api/appsettings.Development.json:16` y `tests/SGV.Tests/Web/Common/AdminJwtTestHelper.cs:23`
coinciden en `SigningKey = "DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000"`,
así que la firma del JWT del fixture es aceptada por la cache del primer host.
Pero el criterio de aceptación exige "Auth y WebFactories sin estado estático
**verificable por inspección**": ese cache existe y rompe ese contrato.

Búsqueda exhaustiva de más estado estático mutable en `src/`:
`grep "private static\s+\w+\??\s+_[a-z]"` devuelve un único hit, precisamente
`AuthSessionFactory.cs:14`. `MySqlTestDatabaseBootstrap.CachedAvailability`
(`tests/SGV.Tests/Persistencia/MySqlTestDatabaseBootstrap.cs:40`) es el único
otro `static`, pero es inmutable por diseño (Lazy de solo lectura) y está
deliberadamente cacheado por sesión — no es estado compartido entre hosts, es
un probe de disponibilidad.

### Causa 2 — Sin política de paralelismo entre ensamblados ni colecciones

`tests/SGV.Tests/SGV.Tests.csproj` (25 líneas):

- L13: `xunit 2.9.2`.
- L14: `xunit.runner.visualstudio 2.8.2`.
- Sin `<Content Include="xunit.runner.json">` ni `<None Include="xunit.runner.json">`.
- Sin `<None Update="..." CopyToOutputDirectory="..."/>`.

`grep "[assembly:|CollectionBehavior|TestCaseOrderer|MaxParallelThreads"` en
`tests/` no devuelve ningún hit. No hay `[CollectionDefinition]` ni
`[Collection("...")]` en el repositorio (`grep` con esos términos = 0 hits).

Por defecto xUnit v2 paraleliza colecciones: cada clase sin `[Collection]`
forma una colección implícita que corre en paralelo. La suite tiene ~70 clases
de test (ver `glob "tests/**/*.cs"`), todas colecciones implícitas. Sin tope
de threads, todos arrancan a la vez.

### Causa 3 — `WithOverrides` crea una factory nueva por llamada

`tests/SGV.Tests/Web/SgvWebApplicationFactory.cs`:

- L18: `public sealed class SgvWebApplicationFactory : WebApplicationFactory<SGV.Web.Program>`.
- L32-48: constructor privado que recibe overrides y los guarda en campos.
- L50-67: `WithOverrides(...)` retorna `new SgvWebApplicationFactory(...)` (L59) —
  **una instancia nueva por cada llamada**, no un wrapper de la base.
- L83-145: `ConfigureWebHost` aplica los overrides guardados al construir el
  host. `CreateClient()` fuerza la construcción del host (vía `Server`).

Los comentarios en `CargoWebTestFixture.cs:103-107`,
`PuestoWebTestFixture.cs:134-138` y `HabilidadWebTestFixture.cs:96-100`
proclaman que encadenar `WithOverrides` sobre `_baseFactory` "evita crear hosts
adicionales nunca dispuestos (resource leak)". Esto es **incorrecto**: cada
`WithOverrides` devuelve una instancia distinta (`SgvWebApplicationFactory.cs:59`),
y `CreateClient()` sobre esa instancia fuerza la construcción de un host
nuevo (`Server`). La fixture solo dispone `_baseFactory`
(`CargoWebTestFixture.cs:146`, `PuestoWebTestFixture.cs:175`,
`HabilidadWebTestFixture.cs:107`); las factories derivadas se pierden.

Resultado por test de integración web: 1 host nuevo + ningún dispose. Con 481
tests web, hasta ~481 hosts en vuelo simultáneo bajo paralelismo xUnit.

`ApiWebApplicationFactory` (`tests/SGV.Tests/Api/ApiWebApplicationFactory.cs:821`)
presenta el mismo patrón simétrico: cada test hace
`using var factory = new ApiWebApplicationFactory(...)` (cientos de ocurrencias,
ver `grep "new ApiWebApplicationFactory"`). El `using` dispone al final del
test, pero no evita que ~360 hosts API se construyan en paralelo dentro de la
misma ventana de ejecución.

### Causa 4 — Saturación de hosts durante el build paralelo

Combinación de Causa 2 + Causa 3:

- xUnit lanza N colecciones implícitas en paralelo sin tope
  (`maxParallelThreads` por default = `Environment.ProcessorCount` o sin tope).
- Cada colección crea sus propios hosts via `new ...Factory()` + `CreateClient()`.
- `CreateClient()` dispara `WebApplicationFactory<TEntryPoint>.EnsureServer()`,
  que compila el entry point, registra DI, levanta `TestServer`, aplica
  `ValidateOnStart`, etc.
- Con ~480 tests web + ~360 API ejecutándose en paralelo, la presión
  simultánea sobre el host build agota el timeout de vstest →
  `MSB4166` + `Timed out waiting for entry point to build the IHost`.

Confirmado en CI: `.github/workflows/ci.yml:45` corre
`dotnet test --no-build --configuration Release --verbosity normal`. El env
exporta `ConnectionStrings__SgvDatabase` y `Jwt__SigningKey`, así que MySQL
está vivo — el síntoma es de contención pura, no de dependencias faltantes.

### Causa 5 (parcialmente cierta) — `IClassFixture<T>` no reduce host count

`IClassFixture<T>` solo comparte una instancia dentro de UNA clase de test:

- `CargoWebTestFixture` se inyecta a 7 clases (`Cargo*PageTests`,
  `CargoHabilidadesPageTests`): comparten `CargoWebTestFixture._baseFactory`,
  pero cada test invoca `WithOverrides(...)` que crea un factory nuevo.
- `PuestoWebTestFixture` se inyecta a 6 clases (`Puesto*PageTests`,
  `PuestoWebSeamTests`).
- `HabilidadWebTestFixture` se inyecta a 7 clases.
- `WebShellSmokeTests` usa `IClassFixture<SgvWebApplicationFactory>` directo.
- `SwaggerConfigurationTests:9` usa `IClassFixture<WebApplicationFactory<SGV.Api.Program>>`
  directo.

Cada fixture construye su propio `SgvWebApplicationFactory` base, así que el
número de hosts base es ≥ 3 (uno por módulo web) + 1 swagger. Los hosts
derivados (`WithOverrides`) son el grueso.

## Áreas afectadas

- `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:14-15, 70-81` — único
  estado estático mutable que cachea `TokenValidationParameters` por vida del
  proceso. Incumple "Auth y WebFactories sin estado estático verificable por
  inspección".
- `tests/SGV.Tests/SGV.Tests.csproj` — falta `xunit.runner.json` con política
  explícita de paralelismo entre colecciones y tope de threads.
- `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs:50-67` — `WithOverrides` no
  comparte el host de `_baseFactory`; crea una factory nueva cada vez.
- `tests/SGV.Tests/Web/Cargo/CargoWebTestFixture.cs:108-112, 146`,
  `tests/SGV.Tests/Web/Puesto/PuestoWebTestFixture.cs:139-144, 175`,
  `tests/SGV.Tests/Web/Habilidad/HabilidadWebTestFixture.cs:48-51, 107` — los
  comentarios proclaman reuse pero el código crea factories derivadas sin
  dispose.
- `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs:821` — patrón `using var
  factory = new ApiWebApplicationFactory(...)` se repite en cientos de tests
  API (ver `grep "new ApiWebApplicationFactory"`); cada test paga un host
  build sin caché.
- `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs:9` — `IClassFixture<WebApplicationFactory<SGV.Api.Program>>`
  directo sin tipado propio.
- `tests/SGV.Tests/Seguridad/JwtRealAuthTests.cs:38, 59, 83` — usa
  `JwtRealWebApplicationFactory` por test (`using var factory = ...`); los
  tres tests crean hosts independientes porque las claves de firma cambian
  por test.

## Enfoques comparados

### Opción A — Quirúrgica

Eliminar el cache estático de `AuthSessionFactory` (reemplazar con build por
llamada o con cache keyed por `(Issuer, Audience, SigningKey)` via
`ConcurrentDictionary`) y agregar `xunit.runner.json` con
`parallelizeTestCollections: false`.

- Pros: cambio mínimo (2 archivos); riesgo bajo; baja probabilidad de regresión.
- Contras: pierde paralelismo intra-assembly (la suite iría de paralela a
  serial); el tiempo total probablemente baja respecto al timeout actual pero
  deja de aprovechar CPU; los tests API que no comparten estado seguirían
  pagando host builds sin coordinación.
- Esfuerzo: Bajo (2 archivos, ~30 LOC + tests de regresión del cache).
- Riesgo: tests que dependían del orden implícito de xUnit paralelo podrían
  fallar; el contrato "sin estado estático" se cumple solo a medias (sigue
  habiendo `MySqlTestDatabaseBootstrap.CachedAvailability`, pero es de solo
  lectura).

### Opción B — Compartida por `IClassFixture<>` con colecciones nombradas

Saca el cache estático. Crea tres `IClassFixture<>` compartidas por dominio
(`WebAuthFixture`, `ApiFixture`, `AuthFixture`) y declara
`[CollectionDefinition]` con nombres explícitos. Las tests usan
`[Collection("WebAuth")]` etc. `WithOverrides` deja de derivar factories:
recibe `IClassFixture` y obtiene la base ya construida.

- Pros: mantiene paralelismo entre colecciones (Web, API, Auth corren en
  paralelo, dentro de cada una en serie); alinea con la guía xUnit de
  "group tests that share mutable state into named collections".
- Contras: requiere refactor de fixtures existentes
  (`CargoWebTestFixture`, `PuestoWebTestFixture`, `HabilidadWebTestFixture`) y
  posiblemente de tests que asumían `IClassFixture<>` específico; todavía
  deja `WithOverrides` como lugar potencial de fuga si no se elimina.
- Esfuerzo: Medio (3-5 archivos nuevos/modificados; ~100 LOC; refactor de
  fixtures).
- Riesgo: media; cambios en fixtures pueden romper tests que asumen la
  firma actual.

### Opción C — Aislamiento total con colecciones explícitas

Saca el cache estático. Reescribe `WithOverrides` para NO crear factory nueva
(en su lugar expone `ConfigureTestServices` en la factory base y los tests
reciben una factory ya configurada por colección, vía `IClassFixture` o
`ICollectionFixture`). Declara `[CollectionDefinition("WebIntegration",
DisableParallelization = true)]` para Web y API; deja paralelismo entre Web,
API, Auth, Persistencia como grupos independientes. Agrega `xunit.runner.json`
con `maxParallelThreads: 4` como red de seguridad.

- Pros: cumple literalmente con todos los criterios de aceptación (sin static
  state, sin `MSB4166`, determinismo verificable por inspección). Política
  declarativa testeable. Compatible con `strict_tdd: true` — los nuevos
  tests pueden cubrir ausencia de static state.
- Contras: mayor superficie de cambio (4-6 archivos; refactor de fixtures y
  posiblemente de algunos tests); requiere escribir nuevos tests de regresión
  para "no static state" y "Dispose discipline".
- Esfuerzo: Medio-Alto (~150-200 LOC; cambios en 5-7 archivos; nuevos tests).
- Riesgo: baja si se hace en commits chicos por work-unit
  (cache→fixtures→xunit.runner.json→tests de regresión).

### Opción D — Híbrida pragmática

Saca el cache estático. Mantiene `WithOverrides` (porque su semántica "una
factory por test" es defendible cuando se quiere overrides aislados) pero
agrega `xunit.runner.json` con `parallelizeAssembly: false`,
`parallelizeTestCollections: true`, `maxParallelThreads: 4`. No toca fixtures
ni `[CollectionDefinition]`. Confía en el tope de threads para evitar
saturación.

- Pros: cambio mínimo (2 archivos: `AuthSessionFactory.cs` + nuevo
  `xunit.runner.json`); topa la concurrencia sin reescribir fixtures.
- Contras: los 480 tests web + 360 API siguen creando factories derivadas en
  paralelo hasta el tope de 4 threads; eso es ~4 hosts simultáneos — viable
  pero sigue habiendo contención. El cache se va, pero `WithOverrides`
  sigue siendo un punto frágil.
- Esfuerzo: Bajo (1 archivo nuevo + 1 modificación + test de regresión).
- Riesgo: media; reduce el síntoma sin garantizar el cumplimiento de los
  criterios menos objetivos.

## Recomendación

**Opción C (Aislamiento total)** con PRs encadenados por work-unit
(ver `work-unit-commits` skill):

1. **Slice 1 — Cache estático fuera**: eliminar `_cachedValidationParameters`
   en `AuthSessionFactory.cs`; construir `TokenValidationParameters` por
   llamada o con cache `ConcurrentDictionary` keyed por `(Issuer, Audience,
   SigningKey)`. Tests de regresión que verifiquen cero estado estático en
   `AuthSessionFactory` vía inspección de `typeof(AuthSessionFactory).GetFields(BindingFlags.NonPublic | BindingFlags.Static)`.
2. **Slice 2 — Refactor `WithOverrides`**: que la factory base exponga un
   patrón de configuración mutable per-instance (no per-factory) o que las
   fixtures no necesiten `WithOverrides`. Tests de regresión que verifiquen
   dispose disciplinado.
3. **Slice 3 — Política de paralelismo**: agregar `xunit.runner.json` con
   `parallelizeTestCollections: true`, `maxParallelThreads: 4` y
   `[CollectionDefinition("Web", DisableParallelization = true)]` /
   `[CollectionDefinition("Api", DisableParallelization = true)]` para
   colecciones pesadas; `[CollectionDefinition("Persistencia",
   DisableParallelization = false)]` para mantener paralelismo entre
   suites sin WebApplicationFactory.

Justificación: solo la Opción C cumple el criterio de aceptación textual
"Auth y WebFactories sin estado estático verificable por inspección". La
Opción D es el plan B si la refactorización de fixtures resulta más invasiva
de lo estimado; se puede pivotar sin reescribir Slice 1.

Por qué NO la Opción A: sacrificar todo el paralelismo intra-assembly
revierte un beneficio de rendimiento del setup actual. La guía xUnit
recomienda explícitamente colecciones nombradas antes que desactivar
paralelismo global.

Por qué NO la Opción B: depende de un refactor de fixtures similar a C pero
sin la disciplina de dispose ni el tope de threads.

## Riesgos

- **Slice 1 — Riesgo de regresión de AuthSessionFactoryTests**: si se elimina
  el cache sin ajustar los tests unitarios que dependen de él, los 7 tests
  de `AuthSessionFactoryTests.cs` pueden empezar a crear nuevas instancias
  por ejecución. Mitigación: los tests existentes siguen pasando porque
  `JwtSecurityTokenHandler.ValidateToken` es idempotente; verificar con
  `dotnet test --filter "FullyQualifiedName~AuthSessionFactoryTests"`.
- **Slice 2 — Compatibilidad de `WithOverrides`**: si se cambia la firma,
  los ~25 tests que llaman a `_baseFactory.WithOverrides(...)` rompen.
  Mitigación: mantener `WithOverrides` como facade deprecada o refactorizar
  in-place.
- **Slice 3 — Política demasiado restrictiva**: serializar Web puede
  alargar la suite a >15 min en hardware modesto. Mitigación: empezar con
  `parallelizeTestCollections: true` + `maxParallelThreads: 4` y medir
  antes de agregar `DisableParallelization` por colección.
- **MySQL test bootstrap**: `MySqlTestDatabaseBootstrap.CachedAvailability`
  sigue siendo `static`. Es solo-lectura por diseño y ya está documentado
  como "cached once per test-session". No requiere cambio.
- **Static state adicional no detectado**: el grep cubrió
  `private static ... _lowercase`, pero no exhaustivamente `static readonly`
  con inicializador. Si se descubre otro cache en `src/` durante apply,
  agregarlo al scope. Mitigación: durante apply, correr un Roslyn analyzer
  simple o grep más exhaustivo para confirmar.
- **`JwtRealWebApplicationFactory` per-test**: aunque no comparte el cache
  estático (no va por SGV.Web), crea un host por test (3 hosts en
  `JwtRealAuthTests`). Esto se mantiene — es intencional, cada test usa una
  clave distinta. No requiere cambio.
- **CI matrix**: el síntoma aparece también en `.github/workflows/ci.yml:45`
  sin tope de threads. Slice 3 debe documentar el cambio para que CI herede
  `xunit.runner.json` automáticamente (CopyToOutputDirectory).
- **Disciplina de dispose en tests API**: cientos de tests usan
  `using var factory = new ApiWebApplicationFactory(...)`. Slice 2 puede
  detectar que el `using` ya cubre el dispose, pero solo si se elimina la
  creación de factory duplicada; si no, el leak persiste en tests API.

## Listo para propuesta

Sí. La exploración verifica las cuatro causas de la auditoría contra el
código real con citas a `file:line`, descarta causas adicionales (no hay
otros caches estáticos mutables en `src/`) y presenta cuatro opciones con
tradeoffs explícitos. La Opción C satisface los criterios de aceptación
textuales.

Recomendación para el orquestador: lanzar `sdd-propose` a continuación con
scope = "eliminar cache estático + política de paralelismo + disciplina de
dispose", alineado con la Opción C y dividido en 3 PRs encadenados por
work-unit. El orquestador debe transmitir al usuario que el cambio
probablemente toque 5-7 archivos (1 de `src/`, 1 nuevo `xunit.runner.json`,
3 fixtures web, refactor de `WithOverrides`, nuevos tests de regresión).
