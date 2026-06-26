# Apply Progress — implementar-login-frontend

## Status: Phase 2 complete (login/logout)

### RED ✅
- Added behavioral coverage in `tests/SGV.Tests/Web/WebAuthenticationTests.cs` for public login UX, invalid login error, successful login redirect, and logout.

### GREEN ✅
- Added `src/SGV.Web/Pages/Auth/SignIn.cshtml` and `SignIn.cshtml.cs` with server-side validation, PRG on success, and cookie sign-in.
- Added `src/SGV.Web/Pages/Auth/Logout.cshtml` and `Logout.cshtml.cs` with POST-only sign-out.
- Added `src/SGV.Web/Pages/Auth/_ViewStart.cshtml` and `src/SGV.Web/Pages/Shared/_AuthLayout.cshtml` for the auth shell.

### REFACTOR ✅
- Added `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs` to centralize claims/cookie ticket creation for login.
- Kept `SGV.Web` consuming centralized auth routes from `SGV.Api` through the existing shared contract.

### Verification ✅
- `dotnet test SGV.slnx --filter FullyQualifiedName~SGV.Tests.Web.WebAuthenticationTests`
- `dotnet build SGV.slnx`
- `dotnet test SGV.slnx` still reports pre-existing unrelated API/persistence failures outside this slice.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1 | `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Integration-style web | ✅ Existing web suite passed | ✅ Written | ✅ Passed | ✅ 2+ scenarios | ✅ Clean |
| 2.2 | `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Integration-style web | ✅ Existing web suite passed | ✅ Written | ✅ Passed | ✅ 2 scenarios | ✅ Clean |
| 2.3 | `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Integration-style web | ✅ Existing web suite passed | ✅ Written | ✅ Passed | ✅ 1 scenario | ✅ Clean |
| 2.4 | `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Refactor support | ✅ Existing web suite passed | ➖ N/A | ✅ Passed | ➖ Single responsibility | ✅ Clean |

## Next
- Ready for Phase 3: shell protegido.
