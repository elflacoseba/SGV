# Proposal: Suite de tests determinista (issue #121)

## Intent

La suite tests/SGV.Tests (1713 tests) pasa aislada pero agota MSB4166 completa. Causas verificadas: cache estático en AuthSessionFactory; sin xunit.runner.json; WithOverrides sin dispose; paralelismo total. Cumple "Auth y WebFactories sin estado estático verificable por inspección".

## Scope

### In Scope

- AuthSessionFactory (static) → servicio DI: nueva IAuthSessionFactory + impl inyectable; actualizar SignIn.cshtml.cs y AuthSessionFactoryTests.
- xunit.runner.json con parallelizeTestCollections true y maxParallelThreads 4.
- SgvWebApplicationFactory.WithOverrides: overrides per-instance en la base, sin factory nueva.
- Fixtures Web (Cargo/Puesto/Habilidad) a CollectionDefinition + ICollectionFixture.
- Tests de regresión: cero static mutable en AuthSessionFactory (reflexión) y dispose disciplinado.

### Out of Scope

- Comportamiento de auth; specs de producto.
- Refactor de ApiWebApplicationFactory per-test más allá de heredar xunit.runner.json.
- MySqlTestDatabaseBootstrap.CachedAvailability (Lazy solo-lectura).
- JwtRealWebApplicationFactory per-test (claves distintas).

## Capabilities

### New Capabilities
None

### Modified Capabilities
None

## Approach

Tres PRs encadenados por work-unit:

1. **Slice 1 — DI de AuthSessionFactory**: extraer IAuthSessionFactory, registrar en Program.cs, reescribir 3 call sites. Regresión: 0 static mutable vía reflexión.
2. **Slice 2 — Dispose + ICollectionFixture**: WithOverrides expone overrides per-instance; fixtures Web a CollectionDefinition + ICollectionFixture.
3. **Slice 3 — Política de paralelismo**: xunit.runner.json con parallelizeTestCollections true, maxParallelThreads 4, CopyToOutputDirectory; documentar.

Criterio por PR: 3 corridas locales consecutivas de dotnet test SGV.slnx --no-build con idéntico pass/fail count.

## Affected Areas

Producción: src/SGV.Web/Integration/Auth/ (AuthSessionFactory static → sealed + IAuthSessionFactory.cs), Program.cs (registrar DI), SignIn.cshtml.cs (inyectar). Tests: xunit.runner.json (New) + SGV.Tests.csproj (CopyToOutputDirectory), SgvWebApplicationFactory.cs (WithOverrides sin factory nueva), fixtures Web (ICollectionFixture), AuthSessionFactoryTests.cs (instanciar impl), AuthSessionFactoryNoStaticStateTests.cs (New, regresión). Docs: docs/decisiones-implementacion.md.

## Risks

- **AuthSessionFactoryTests rotos al pasar de static a DI (Med)** — Slice 1 aislado; validar --filter ~AuthSessionFactoryTests pre-merge.
- **Refactor WithOverrides rompe ~25 call sites (Med)** — mantener fachada idéntica; tests de Seam cubren contrato.
- **maxParallelThreads 4 lleva suite a >15 min (Low-Med)** — medir 3 corridas y ajustar.
- **Static state adicional no detectado en apply (Low)** — grep exhaustivo durante apply.

## Rollback Plan

Cada slice es revertible con git revert. Slice 1: AuthSessionFactory vuelve a static. Slice 2: WithOverrides retoma firma original, fixtures vuelven a IClassFixture. Slice 3: borrar xunit.runner.json y el Content Include del csproj. Rollback local; sin migraciones.

## Dependencies

xUnit 2.9.2 soporta xunit.runner.json (>= 2.4); sin upgrade. MySQL 8 local + CI. dotnet test SGV.slnx --no-build sigue siendo el comando de aceptación.

## Success Criteria

- grep "private static" en AuthSessionFactory.cs devuelve 0 hits.
- typeof(AuthSessionFactory).GetFields(NonPublic|Static).Length == 0 (test de regresión).
- xunit.runner.json versionado con CopyToOutputDirectory.
- **3 corridas locales consecutivas de dotnet test SGV.slnx --no-build con idéntico pass/fail count** (criterio de aceptación de PR).
- MSB4166 no aparece en corrida completa.
- docs/decisiones-implementacion.md documenta política de paralelismo y decisión DI.