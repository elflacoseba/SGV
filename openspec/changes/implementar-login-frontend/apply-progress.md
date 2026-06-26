# Apply Progress — implementar-login-frontend

## Status: Phase 1 slice complete (auth foundation)

### RED ✅
- Added `tests/SGV.Tests/Web/WebAuthenticationTests.cs` to cover centralized auth route resolution and `AuthApiClient` behavior.
- The new tests failed before implementation, as expected.

### GREEN ✅
- Added `src/SGV.Api/Contracts/AuthApiRoutes.cs` and updated `src/SGV.Api/Controllers/AuthController.cs` to use the shared route constants.
- Added `src/SGV.Web/Integration/Auth/SgvApiOptions.cs`, `IAuthApiClient.cs`, and `AuthApiClient.cs`.
- Updated `src/SGV.Web/SGV.Web.csproj`, `src/SGV.Web/Program.cs`, and `src/SGV.Web/appsettings*.json` to wire the API base URL, cookie auth, authorization, and the typed auth client.

### REFACTOR ✅
- Extended `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` with a default fixture constructor plus `WithOverrides(...)` so tests can inject fake services/handlers without breaking xUnit fixtures.

### Verification ✅
- `dotnet test SGV.slnx --filter FullyQualifiedName~SGV.Tests.Web`
- `dotnet build SGV.slnx`
- `dotnet test SGV.slnx` still reports unrelated pre-existing API/persistence failures outside this slice.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.2 | `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Unit | N/A (new) | ✅ Written | ✅ Passed | ✅ 2 cases | ✅ Clean |
| 1.3 | `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Unit | N/A (new) | ✅ Written | ✅ Passed | ✅ 2 cases | ✅ Clean |
| 1.4 | `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Integration-style unit | N/A (new) | ✅ Written | ✅ Passed | ✅ 2 cases | ✅ Clean |

## Next
- Finish the remaining login/logout and protected-shell slice in the next PR.
