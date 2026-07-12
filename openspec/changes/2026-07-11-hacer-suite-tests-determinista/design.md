# Design: Suite de tests determinista (issue #121) — it-5

> Cierra el último blocker de Engram #970: conteo material de call sites y
> firmas del composite enumeradas. Conteos por `rg`. Slice 2b se parte en 5
> sub-slices porque 209 call sites > 200.

## Technical Approach

Extraer `IAuthSessionFactory` Singleton en `Program.cs:54` para borrar
`_cachedValidationParameters` (`AuthSessionFactory.cs:14,70-81`). El composite
`WebIntegrationFixture` posee una raíz `SgvWebApplicationFactory` con caché
por overrides y expone **7 helpers `Task<WebClientLease>`**. Tests migran
de `IClassFixture<TModuleFixture>` a `[Collection("WebIntegration")]`. Cada
lease es `IAsyncDisposable` con orden `client → sentinel → factory`,
probado con `TestSentinel` observable. `xunit.runner.json` habilita
paralelismo inter-colección sin `DisableParallelization`.

## Architecture Decisions

| Decisión | Tradeoff | Elegido |
|---|---|---|
| Tipo lease | Derivado evita cast en `WithOverrides`/`WithHabilidadApiClient` | **`SgvWebApplicationFactory`** |
| Evidencia dispose | Spy self-reporting no afirma liberación real | **Sentinel + orden observable** |
| `IAuthSessionFactory` lifetime | Cada `WebAF` construye `IHost` con `IServiceProvider` aislado; `IOptions<>.Value` lazy ⇒ snapshot por host | **Singleton + IOptions** |
| `WithOverrides` | Cast `WAF<Program>→derivado` es unsafe (release/10.0 L128) | **Mantener derivado** |
| `root.Services` | Accederlo ejecuta `StartServer()` | **No acceder** |
| API tests | Solo `SwaggerConfigurationTests` se beneficiaría | **Respetar `using var factory`** |
| Sub-slice 2b | 209 sites > 200 ⇒ fundación + 4 módulos ≤400 LOC/PR | **5 PRs encadenados** |

## Inventario source-backed (rg)

| # | Helper | Sites |
|---|---|---|
| 1 | `CargoWebTestFixture.cs:75` `CreateAuthenticatedClientAsync(FakeCargoApiClient)` | 16 |
| 2 | `CargoWebTestFixture.cs:78` `CreateAdminClientAsync(FakeCargoApiClient)` | 30 |
| 3 | `CargoWebTestFixture.cs:90` 3-arg `(FakeCargo, FakeHabilidad, bool)` | 20¹ |
| 4 | `PuestoWebTestFixture.cs:89` `CreateAuthenticatedClientAsync(FakePuestosApiClient)` | 22 |
| 5 | `PuestoWebTestFixture.cs:96` `CreateAdminClientAsync(FakePuestosApiClient)` | 24 |
| 6 | `PuestoWebTestFixture.cs:103` 3-arg | 1 |
| 7 | `PuestoWebTestFixture.cs:120` 3-arg+bool | 0 (interno) |
| 8 | `HabilidadWebTestFixture.cs:40` `CreateAuthenticatedClientAsync(FakeHabilidadApiClient)` | 32 |
| 9 | `HabilidadesCargosModelTests.cs:428` `(SgvWebAF, FakeHabilidad)` | 12 |
| 10 | `CargoWebTests.cs:169` `CreateAuthenticatedClientAsync()` | 4 |
| 11 | `WebAuthenticationTests.cs:204` `CreateAuthenticatedClientAsync()` | 1 |
| 12 | `WebShellSmokeTests.cs:55` `CreateAuthenticatedClientAsync()` | 1 |
| 13 | `UnidadOrganizativaWebTestHelpers.cs:26` `(FakeUnidad…ApiClient)` | 48 |
|   | **TOTAL** | **211** |

¹ 19 directos + 2 cross-módulo (`HabilidadesCargosModelTests.cs:290` y `PuestoWebTestFixture.cs:103`).

Auxiliares (rg): `[CollectionDefinition]/ICollectionFixture` hoy = 0;
`IClassFixture<TModuleFixture>` = 16 (Cargo 5 + Puesto 5 + Habilidad 5 +
WebShellSmokeTests); `new SgvWebApplicationFactory()` = 47 (33 con `using var
factory`, 14 sin `using` ⇒ leak); `RecordingHttpMessageHandler` copias = 7;
`ExtractAntiforgeryTokenAsync` copias = 8; `Build{Cargo,Puesto,Habilidad}Dto`
sites = 54; GUID sites = 56; cross-módulo
`HabilidadWebTestFixture.{RecordingHttpMessageHandler,ExtractAntiforgeryTokenAsync}` = 1+4.

## Firmas explícitas del composite

```csharp
[CollectionDefinition("WebIntegration")]
public sealed class WebIntegrationCollection : ICollectionFixture<WebIntegrationFixture> {}

internal sealed class WebIntegrationFixture : IAsyncDisposable
{
    public SgvWebApplicationFactory RootFactory { get; }
    public Task<WebClientLease> CreateCargoLeaseAsync(FakeCargoApiClient cargo,
        FakeHabilidadApiClient? habilidad = null, bool adminRole = false);
    public Task<WebClientLease> CreatePuestoLeaseAsync(FakePuestosApiClient puestos,
        IUnidadOrganizativaApiClient? unidad = null, ICargoApiClient? cargo = null, bool adminRole = false);
    public Task<WebClientLease> CreateHabilidadLeaseAsync(FakeHabilidadApiClient habilidad, bool adminRole = false);
    public Task<WebClientLease> CreateUnidadOrganizativaLeaseAsync(FakeUnidadOrganizativaApiClient unidad, bool adminRole = false);
    public Task<WebClientLease> CreateAnonymousLeaseAsync();
    public Task<WebClientLease> CreateAuthOnlyLeaseAsync(bool adminRole = false);
    public ValueTask DisposeAsync();
}

internal sealed class WebClientLease(SgvWebApplicationFactory factory, HttpClient client, TestSentinel sentinel) : IAsyncDisposable
{
    public HttpClient Client => client;
    public async ValueTask DisposeAsync() { client.Dispose(); sentinel.Dispose(); await factory.DisposeAsync(); } // client → sentinel → factory
}

internal sealed class TestSentinel : IDisposable
{
    private static int _alive;
    public static int AliveCount => Volatile.Read(ref _alive);
    public TestSentinel() => Interlocked.Increment(ref _alive);
    public void Dispose() => Interlocked.Decrement(ref _alive);
}
```

## Migración de estado de módulo

| Estado actual | Destino |
|---|---|
| `_baseFactory`/`BaseFactory` (3 fixtures) | `WebIntegrationFixture._root` |
| `With{Cargo,Puestos,Habilidad,Unidad}ApiClient`/`WithCatalogFakes` | Encapsulado en `CreateXxxLeaseAsync` |
| `Build{Cargo,Puesto,Habilidad}Dto` estáticos | `WebTestBuilders.{Cargo,Puesto,Habilidad}.BuildDto` |
| GUIDs `Junior/SeniorNivelId`, `Sample*` | `WebTestBuilders.{Cargo,Puesto}Guids` |
| `RecordingHttpMessageHandler` (7 copias) | `WebTestBuilders.RecordingHandler` |
| `ExtractAntiforgeryTokenAsync` (8 copias) | `WebTestBuilders.ExtractAntiforgeryTokenAsync` |
| `HasInputNamed`/`InputHasAttribute` (5 sites) | `WebTestBuilders.HabilidadMarkup` |
| 16 `IClassFixture<TModuleFixture>` | `[Collection("WebIntegration")]` |

## File Changes (LOC Δ)

| Archivo / grupo | Acción | Δ |
|---|---|---|
| `IAuthSessionFactory.cs` | Crear | +12 |
| `AuthSessionFactory.cs` | static→sealed, borrar L14+L70-81 | +18/-16 |
| `SignIn.cshtml.cs`, `Program.cs` | Inyectar / DI | +6/-2 |
| `WebTestBuilders.cs` | Crear (builders+GUIDs+handler+extract) | +95 |
| `Collections/{TestSentinel,WebClientLease,CollectionDefinitions,WebIntegrationFixture}.cs` | Crear 4 archivos | +135 |
| `Collections/{WebClientLeaseTests,WebIntegrationFixtureTests}.cs` | Crear | +140 |
| `Auth/AuthSessionFactory{NoStaticState,Isolation}Tests.cs` | Crear | +110 |
| `Auth/AuthSessionFactoryTests.cs` | Reemplazar static call | +10/-3 |
| `xunit.runner.json` + csproj `<Content>` | Crear/Modificar | +9 |
| 13 helpers → `Task<WebClientLease>` | Modificar | +130/-65 |
| 209 call sites (`using var client`→`await using var lease; lease.Client`) | 24 archivos | +627/-209 |
| 33 `using var factory = new SgvWebApplicationFactory()…` | Modificar | +99/-33 |
| 16 `[IClassFixture<TModuleFixture>]`→`[Collection]` | Modificar | +16/-16 |
| `docs/decisiones-implementacion.md` | Sección nueva | +25 |
| **TOTAL NETO** | | **+1106** |

## TDD + Rollout (7 PRs encadenados, todos ≤400 LOC)

| Slice | LOC | Scope | Rollback |
|---|---|---|---|
| **1** — DI AuthSessionFactory | ~170 | RED `NoStaticState`+`Isolation`; GREEN `IAuthSessionFactory`+impl+DI+`SignIn` | revert static |
| **2b-0** — Composite infra | ~350 | `TestSentinel`+`WebClientLease`+`WebIntegrationFixture`+`CollectionDefinitions`+`WebTestBuilders`+tests | borrar archivos |
| **2b-1** — Cargo+Shell+Auth | ~361 | 13 firmas + 65 sites Cargo + 6 Shell/Auth + 6 `[Collection]` | revert firmas+sites |
| **2b-2** — Puesto | ~153 | 46 sites + 5 `[Collection]` | revert módulo |
| **2b-3** — Habilidad+HabilidadesCargos | ~144 | 43 sites + 5 `[Collection]` | revert módulo |
| **2b-4** — UO + factory anónimos | ~258 | 48 sites UO + 33 `using var factory` + 5 `[Collection]` | revert módulo |
| **3** — xunit.runner.json + gates | ~150 | json+csproj+`verify-report.md` (3 corridas <15min sin MSB4166) | borrar json+`Content` |

**Forecast total**: 170+350+361+153+144+258+150 = **1586 LOC**.

## Testing Strategy

| Capa | Qué | Cómo |
|---|---|---|
| Unit | `AuthSessionFactory` sin static mutable | `NoStaticStateTests`: `GetFields(NonPublic\|Static).Length == 0` |
| Unit | Lease libera sentinel en orden | `WebClientLeaseTests`: 2 leases paralelos, `AliveCount == 0` post-dispose + orden |
| Unit | Aislamiento JWT entre hosts | `IsolationTests`: 2 hosts con `SigningKey` distintos |
| Integration | Web suite verde | `dotnet test SGV.slnx --no-build` (1477 tests) por slice |
| Gate | Estabilidad | `verify-report.md`: 3 corridas <15min, pass/fail idéntico, sin `MSB4166` |

## Riesgos con impacto medido

| Riesgo | Impacto medido | Mitigación |
|---|---|---|
| Fuga de hosts por 14 `using var factory = new SgvWebApplicationFactory()…` sin `using` (HabilidadesCargosModelTests:12, HabilidadWebSeamTests:2, CargoWebSeamTests:2, CargoWebTests:2, ApiBearerTokenIntegrationTests:1, UnidadOrganizativaCreateDetailsTests:3, PuestoWebSeamTests:0, HabilidadEdit/Create/Index/Details PageTests:4 anónimos + 4 detalle) | Host huérfano por test; MSB4166 reaparece | Slice 2b-4 cubre los 33 sitios `using var factory` y los tests anónimos en módulos |
| Desorden de dispose en lease | Host detenido antes de cerrar HttpClient ⇒ socket colgado | `WebClientLeaseTests` asserta orden client→sentinel→factory |
| Migración masiva de 209 call sites en un solo PR rebota review | PR >400 LOC, riesgo de merge conflict | Slice 2b partido en 4 módulos (Cargo 65 / Puesto 46 / Habilidad 43 / UO 48) |
| `GetFields` puede romperse al agregar nuevas propiedades | Falso positivo en `NoStaticStateTests` | Test reflexiona solo `Static\|NonPublic`, no toca instancia |

## Open Questions

Ninguna. 9 decisiones cerradas: (1) 13 helpers con conteo exacto `rg`; (2)
209 call sites directos medidos; (3) `WebIntegrationFixture` con 7 firmas
enumeradas; (4) `TestSentinel` observable + orden `client→sentinel→factory`;
(5) `WithOverrides` retorna `SgvWebApplicationFactory`; (6) sin acceso a
`root.Services`; (7) API tests no migran; (8) Singleton justificado por
`IServiceProvider` aislado por host + `IOptions<>.Value` lazy; (9) Slice 2b
partido en 5 sub-slices por >200 call sites.