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

- [ ] 4.1 Migrar `PuestoWebTestFixture.cs:89/96/103/120` (4 firmas) + 47 sites.
- [ ] 4.2 5 clases `Puesto*PageTests`+`PuestoWebSeamTests` a `[Collection("WebIntegration")]`.
- [ ] 4.3 VERIFICAR `--filter ~Puesto --no-build` verde.

## Fase 5 — PR2b-3: Habilidad+HabilidadesCargos (~144)

- [ ] 5.1 Migrar `HabilidadWebTestFixture.cs:40` + 32 sites; cross `HabilidadesCargosModelTests.cs:428`/`:290`.
- [ ] 5.2 5 clases `Habilidad*PageTests` a `[Collection("WebIntegration")]`.
- [ ] 5.3 VERIFICAR `--filter ~Habilidad --no-build` verde.

## Fase 6 — PR2b-4: UO + factory anónimos (~258)

- [ ] 6.1 Migrar `UnidadOrganizativaWebTestHelpers.cs:26` + 48 sites.
- [ ] 6.2 Convertir 33 `using var factory = new SgvWebApplicationFactory()…` sin `using` (`HabilidadesCargosModelTests:12`, `HabilidadWebSeamTests:2`, `CargoWebSeamTests:2`, `CargoWebTests:2`, `ApiBearerTokenIntegrationTests:1`, `UnidadOrganizativaCreateDetailsTests:3`, `Habilidad{Edit,Create,Index,Details}PageTests`) a `await using var lease`.
- [ ] 6.3 5 clases `UnidadOrganizativa*Tests` a `[Collection("WebIntegration")]`.
- [ ] 6.4 VERIFICAR `--filter ~UnidadOrganizativa|~ApiBearerToken --no-build` verde.

## Fase 7 — PR3: xunit.runner.json + doc + gate (~150)

- [ ] 7.1 Crear `xunit.runner.json` (`{parallelizeTestCollections:true, maxParallelThreads:4}`) + `<Content CopyToOutputDirectory=PreserveNewest>` en `SGV.Tests.csproj`.
- [ ] 7.2 Doc sección "Política de paralelismo y DI de AuthSessionFactory" en `docs/decisiones-implementacion.md`.
- [ ] 7.3 Gate (escenario §"Tres corridas consecutivas"): 3 corridas `dotnet test SGV.slnx --no-build`, `<15min`, totales pass/fail idénticos, sin `MSB4166`; documentar en `verify-report.md` antes de sdd-archive.

## Out of Scope / Riesgos

Out of scope: no tocar `ApiWebApplicationFactory.cs` ni tests API; no migrar `MySqlTestDatabaseBootstrap.CachedAvailability` ni `JwtRealWebApplicationFactory` per-test; sin `DisableParallelization`. Riesgos: PR2b-4 cubre 33 sitios sin `using` (retraso reinjecta `MSB4166`); `GetFields` puede fallar si se añaden propiedades a `AuthSessionFactory`; strategy pendiente antes de sdd-apply.
