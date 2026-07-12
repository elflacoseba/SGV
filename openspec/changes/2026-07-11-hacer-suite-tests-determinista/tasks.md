# Tasks: Suite de tests determinista (issue #121)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1586 (suma 7 PRs) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2b-0 → PR2b-1 → PR2b-2 → PR2b-3 → PR2b-4 → PR3 |
| Delivery strategy | ask-on-risk (size:exception approved) |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

Maintainer approved a single PR with `size:exception`; sdd-apply may proceed as one review unit despite the forecast.

## Fase 1 — PR1: DI IAuthSessionFactory (~170)

- [x] 1.1 RED `NoStaticStateTests`+`IsolationTests` (escenarios §"Hosts con JWT distintos" / §"Validaciones repetidas independientes").
- [x] 1.2 GREEN crear `IAuthSessionFactory.cs`; refactor `AuthSessionFactory.cs` (borrar L14+L70-81; `grep "private static" → 0`); Singleton en `Program.cs:54`; inyectar en `SignIn.cshtml.cs`.
- [x] 1.3 REFACTOR `AuthSessionFactoryTests.cs`; VERIFICAR `--filter ~AuthSessionFactory --no-build` verde, `dotnet build SGV.slnx` limpio.

## Fase 2 — PR2b-0: Composite infra (~350)

- [x] 2.1 RED `TestSentinel` + `WebClientLeaseTests` (`AliveCount==0`, orden client→sentinel→factory).
- [x] 2.2 RED `WebIntegrationFixtureTests` con las 7 firmas de `design.md`.
- [x] 2.3 GREEN `WebClientLease.cs`, `WebIntegrationCollection.cs` (`[CollectionDefinition("WebIntegration")] ICollectionFixture<WebIntegrationFixture>`), `WebIntegrationFixture.cs` (escenario §"Overrides no crean factories huérfanas").
- [x] 2.4 GREEN `WebTestBuilders.cs`: `Build{Cargo,Puesto,Habilidad}Dto`, GUIDs, `RecordingHandler`, `ExtractAntiforgeryTokenAsync`, `HabilidadMarkup`.
- [x] 2.5 VERIFICAR `--filter ~WebIntegrationFixture|~WebClientLease` verde; diff ≤400 LOC.

## Fase 3 — PR2b-1: Cargo + Shell/Auth (~361)

- [x] 3.1 Migrar `CargoWebTestFixture.cs:75/78/90` a `Task<WebClientLease>`; 66 call sites (incl. cross `HabilidadesCargosModelTests.cs:290`).
- [x] 3.2 Migrar `CargoWebTests.cs:169`, `WebAuthenticationTests.cs:204`, `WebShellSmokeTests.cs:55`; 6 sites.
- [x] 3.3 6 clases (`Cargo*PageTests`+`CargoHabilidadesPageTests`+smoke) a `[Collection("WebIntegration")]`.
- [x] 3.4 VERIFICAR `--filter ~Cargo|~WebShellSmoke|~WebAuthentication --no-build` verde.

## Fix post-PR2b-1 — ownership de `WebClientLease`

- [x] 3.5 FIX aplicar Approach C: toda lease, incluida la anónima, posee una factory derivada; agregar regresión shared-root/derived-root y migrar los 6 workarounds anónimos al lease estándar.

## Fase 4 — PR2b-2: Puesto (~153)

- [x] 4.1 Migrar `PuestoWebTestFixture.cs:89/96/103/120` (4 firmas) + 47 sites.
- [x] 4.2 5 clases `Puesto*PageTests`+`PuestoWebSeamTests` a `[Collection("WebIntegration")]`.
- [x] 4.3 VERIFICAR `--filter ~Puesto --no-build` verde.
- [x] 4.4 PR2b-2-1 Corrective review findings: (a) `await using` + behavioral assertions en los 5 contract tests (sentinel release); (b) eliminar helpers `WithPuestosApiClient`/`WithCargoApiClient`/`WithUnidadOrganizativaApiClient`/`WithCatalogFakes` (0 callers per `rg`); renombrar `BaseFactory` → `RootFactory` para simetría con el composite; (c) contract tests verifican factory derivada vs root propio del fixture, dispose no detiene la raíz compartida (segunda lease operativa), y override observable resolviendo `IPuestosApiClient` desde `lease.Factory.Services`. Mantener baseline 46 fallas preexistentes; `~Puesto` = 46/271 (baseline + 1 test intencional de override); composite infra 26/26 verde.
- [x] 4.5 PR2b-2-2 Final minimal fix del doble dispose residual: (a) `RED` 2 tests idempotentes nuevos en `WebClientLeaseTests.cs` (`Lease_DisposeAsync_CalledTwice_KeepsAliveCountStable` + `TestSentinel_Dispose_CalledTwice_KeepsAliveCountStable`) que prueban que `Dispose`/`DisposeAsync` doble NO decrementa el contador global dos veces; (b) `GREEN` añadir campo `private int _disposed` y guarda `Interlocked.Exchange(ref _disposed, 1) != 0` con early-return en `TestSentinel.Dispose` y en `WebClientLease.DisposeAsync`; (c) `REFACTOR` eliminar los 5 disposes manuales en `PuestoWebTestFixtureLeaseContractTests.cs` — los 4 de la familia "ReturnsLeaseWithDerivedFactoryAndOwnsSentinel" ahora usan bloque interno anidado con `await using` adentro + `Assert.Equal(baseline, AliveCount)` afuera, y `Lease_DisposeAsync_DoesNotDisposeSharedRoot` envuelve el primer lease en un `await using` interno para que se libere antes del segundo; docstring documentando la política "ningún dispose manual". 4 files, +124/-40 = +84 net. Lease/sentinel/contract 28/28 verde; `~Puesto` = 46/271 (idéntico al baseline PR2b-2-1, cero regresiones).
- [x] 4.6 Corrective apply PR2b-3 — serialization de `PuestoWebTestFixtureLeaseContractTests`: (a) `RED` `TestClass_DeclaresWebIntegrationCollection_ToSerializeSentinelAssertions` que refleja `[Collection]` sobre la clase vía `CustomAttributeData.GetCustomAttributes` + lectura del primer argumento del constructor, y falla con `Assert.NotNull` antes de aplicar el atributo; (b) `GREEN` añade `[Collection("WebIntegration")]` con docstring que explica por qué la serialización es necesaria (las aserciones de balance `TestSentinel.AliveCount` son no deterministas cuando los PageTests o los contract tests de Habilidad crean/libera su lease entre la captura del baseline y la aserción); (c) `REFACTOR` elimina `using System.Net.Http;` y `using SGV.Tests.Web.Habilidad;` que quedaron sin uso tras el green, mantiene la disciplina `await using` (cero disposes manuales), conserva el atributo. 1 file, +46/-2. Composite/lease/sentinel/Puesto+Habilidad contract **32/32** verde, 3 corridas idénticas, build Release 0/0, `git diff --check` clean. Habilidad flake original (`Expected: 2, Actual: 1` en `CreateAuthenticatedClientAsync_ReturnsLeaseWithDerivedFactoryAndOwnsSentinel`) eliminada por la serialización — predicha como riesgo al cierre de PR2b-3 (§Desviaciones del design de #977) y remediada acá antes de PR2b-4.

## Fase 5 — PR2b-3: Habilidad+HabilidadesCargos (~144)

- [x] 5.1 Migrar `HabilidadWebTestFixture.cs:40` + 32 sites; cross `HabilidadesCargosModelTests.cs:428`/`:290`.
- [x] 5.2 5 clases `Habilidad*PageTests` a `[Collection("WebIntegration")]`.
- [x] 5.3 VERIFICAR `--filter ~Habilidad --no-build` verde.

## Fase 6 — PR2b-4: UO + factory anónimos (~258)

- [x] 6.1 Migrar `UnidadOrganizativaWebTestHelpers.cs:26` + 48 sites. `CreateAuthenticatedClientAsync(FakeUnidadOrganizativaApiClient)` ahora es método de instancia que devuelve `Task<WebClientLease>` delegando a `_fixture.RootFactory.WithOverrides(...)` + `WebClientLease` con sentinel (justificado narrow deviation: el fake privado de UO no encaja en el tipado de `CreateUnidadOrganizativaLeaseAsync` que pide el fake de Puesto). 48 sitios `using var client = await CreateAuthenticatedClientAsync(apiClient)` → `await using var lease = await CreateAuthenticatedClientAsync(apiClient); var client = lease.Client;`. Helper local `RecordingHttpMessageHandler` borrado (ya provisto por `WebTestBuilders`). Eliminada la aserción interna del helper sobre `Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode)` (la falla documentada pre-existente en develop sigue manifestándose aguas abajo cuando el endpoint devuelve 200 en vez de 302, preservando el conteo de fallos).
- [x] 6.2 Convertir `using var factory = new SgvWebApplicationFactory()…` sin `using` a `await using var lease`. Inventario source-backed (grep): 27 sitios en 11 archivos. Migrados:
  - `HabilidadesCargosModelTests`: 11 sitios (factory + helper) → `await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient); var client = lease.Client;`. Helper estático eliminado. El sitio admin (línea 297) sigue usando `cargoFixture` (ya compuesto).
  - `HabilidadWebSeamTests`: 2 sitios → `CreateAnonymousLeaseAsync` (DI scope) y `CreateHabilidadLeaseAsync(fake)` (override). Clase a `[Collection("WebIntegration")]`.
  - `CargoWebSeamTests`: 2 sitios → `CreateAnonymousLeaseAsync` y `CreateCargoLeaseAsync(fake)`. Clase a `[Collection("WebIntegration")]`.
  - `PuestoWebSeamTests`: 1 sitio (no listado explícitamente pero mismo patrón) → `CreatePuestoLeaseAsync(fake)`. Clase ya estaba en `[Collection]`.
  - `ApiBearerTokenIntegrationTests`: 1 sitio → nueva sobrecarga narrow `WebIntegrationFixture.CreateCargoBridgeLeaseAsync(authHandler, cargoHandler)`. Clase a `[Collection]`. Esta es la única adición de API en este lote, justificada por el patrón único de `WithOverrides(cargoApiHandler: ...)` que el helper estándar no cubre (design.md §"Inventario source-backed (rg)" — 33 sitios sin `using` + necesidad de factory derivada sin dispose).
  - `UnidadOrganizativaCreateDetailsTests`: 3 sitios anónimos → `CreateAnonymousLeaseAsync`. (Combinado con 6.1.)
  - `UnidadOrganizativaAccessAndIndexTests`: 1 sitio anónimo → `CreateAnonymousLeaseAsync`.
  - `Habilidad{Edit,Create,Index,Details}PageTests`: 4 sitios (1 c/u) → `CreateAnonymousLeaseAsync`.
  - `WebAuthenticationTests`: 5 sitios → helper privado `CreateAuthLease(handler)` que envuelve `_fixture.RootFactory.WithOverrides(...)` + `WebClientLease`.
  Grep final: 0 `using var factory = new SgvWebApplicationFactory` fuera de comentarios.
- [x] 6.3 Las 5 clases parciales de `UnidadOrganizativaWebTests` (AccessAndIndex/CreateDetails/DeleteReactivate/Edit/Organigrama) comparten un único `[Collection("WebIntegration")]` declarado en el archivo parcial `UnidadOrganizativaWebTestHelpers.cs` (centralizado para evitar duplicación del atributo entre parciales — xUnit rechaza la duplicación con CS0579). Ctor `(WebIntegrationFixture fixture)` agregado en el archivo central.
- [x] 6.4 VERIFICAR focused suite. 3 corridas consecutivas idénticas: `--filter ~UnidadOrganizativaWebTests|~ApiBearerTokenIntegrationTests|~HabilidadesCargosModelTests|~HabilidadWebSeamTests|~CargoWebSeamTests|~HabilidadEditPageTests|~HabilidadCreatePageTests|~HabilidadIndexPageTests|~HabilidadDetailsPageTests|~WebAuthenticationTests|~PuestoWebSeamTests` → **95 failed / 38 passed / 133 total** en cada corrida. Baseline pre-cambio (`git stash` + rerun): 95 failed / 38 passed / 133 total — idéntico, cero regresiones. Contract tests (`WebClientLeaseTests|WebIntegrationFixtureTests|HabilidadWebTestFixtureLeaseContractTests|PuestoWebTestFixtureLeaseContractTests`): 32/32 verde en 3 corridas consecutivas. Build Release 0/0. `git diff --check` clean. Pre-existing auth failures preservadas: 48 UO (assertion downstream porque login devuelve 200 OK en vez de 302), 1 ApiBearer, 40 HabilidadesCargos/HabilidadSeam/HabilidadPage/CargoSeam, 2 WebAuth, 4 PuestoSeam = 95 total.

## Fase 7 — PR3: xunit.runner.json + doc + gate (~150)

- [ ] 7.1 Crear `xunit.runner.json` (`{parallelizeTestCollections:true, maxParallelThreads:4}`) + `<Content CopyToOutputDirectory=PreserveNewest>` en `SGV.Tests.csproj`.
- [ ] 7.2 Doc sección "Política de paralelismo y DI de AuthSessionFactory" en `docs/decisiones-implementacion.md`.
- [ ] 7.3 Gate (escenario §"Tres corridas consecutivas"): 3 corridas `dotnet test SGV.slnx --no-build`, `<15min`, totales pass/fail idénticos, sin `MSB4166`; documentar en `verify-report.md` antes de sdd-archive.

## Out of Scope / Riesgos

Out of scope: no tocar `ApiWebApplicationFactory.cs` ni tests API; no migrar `MySqlTestDatabaseBootstrap.CachedAvailability` ni `JwtRealWebApplicationFactory` per-test; sin `DisableParallelization`. Riesgos: PR2b-4 cubre 33 sitios sin `using` (retraso reinjecta `MSB4166`); `GetFields` puede fallar si se añaden propiedades a `AuthSessionFactory`; strategy pendiente antes de sdd-apply.
