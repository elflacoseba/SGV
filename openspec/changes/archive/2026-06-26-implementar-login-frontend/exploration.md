## Exploration: login en el frontend

### Current State
- `SGV.Web` hoy es un shell Razor Pages con `_ViewStart.cshtml` apuntando a `_VerticalLayout`.
- `_VerticalLayout` incluye topbar, sidenav, footer y customizer; no existe una página `Auth` ni un layout auth dedicado.
- `src/SGV.Web/Program.cs` solo registra Razor Pages y el pipeline estático; no hay autenticación, `HttpClient` ni cookies.
- El modelo Inspinia `InspinaTemplate/Starterkit/Pages/Auth/SignIn.cshtml` usa `@page "/auth/sign-in"`, `ViewBag.title`, una tarjeta centrada, `auth-box`, `auth-brand`, un form simple y `FooterScripts`.
- El backend ya expone login real en `POST /api/v1/auth/login`, que devuelve `LoginResponse` con JWT y expiración.

### Affected Areas
- `src/SGV.Web/Pages/Auth/SignIn.cshtml` y `.cshtml.cs` — nueva pantalla de login.
- `src/SGV.Web/Pages/_ViewStart.cshtml` o `_ViewStart.cshtml` del subfolder `Auth` — la página no debe heredar el layout vertical.
- `src/SGV.Web/Pages/Shared/_BaseLayout.cshtml` — base visual para auth pages.
- `src/SGV.Web/Program.cs` — si el login será funcional, requiere plumbing de auth/state.
- `src/SGV.Web/appsettings*.json` — base URL del API si el frontend consume el login remoto.
- `src/SGV.Api/Controllers/AuthController.cs` y `src/SGV.Infraestructura/Seguridad/AuthServicio.cs` — contrato real que el frontend debería reutilizar.
- `tests/SGV.Tests/Web/*` — cobertura para la nueva ruta `/auth/sign-in`.
- `openspec/specs/sgv-web-shell/spec.md` — hoy prohíbe UI de autenticación; este trabajo necesita una excepción o delta nuevo.

### Approaches
1. **Pantalla estática** — copiar la maqueta Inspinia sin lógica de POST.
   - Pros: mínimo cambio, rápido de entregar.
   - Cons: no autentica de verdad.
   - Effort: Low

2. **Razor Page + login contra API + estado de sesión en SGV.Web** — el `PageModel` valida contra `/api/v1/auth/login` y luego establece la sesión/cookie del frontend.
   - Pros: login usable, reutiliza el backend existente.
   - Cons: exige decidir estrategia de cookie/token y agregar middleware/auth state.
   - Effort: High

3. **Frontend con token en cliente** — la UI llama al API y guarda el JWT en navegador.
   - Pros: menos cambios de servidor.
   - Cons: peor postura de seguridad y logout/refresh más frágil.
   - Effort: Medium

### Recommendation
- Si el objetivo es un login real, elegir el enfoque 2.
- La página debe salir de `_BaseLayout` o de un layout auth dedicado; no debe heredar `_VerticalLayout`.
- Reusar el contrato existente del API en vez de duplicar validación de credenciales.
- Tratar esto como un cambio nuevo de OpenSpec, porque el shell actual explícitamente excluye auth UI.

### Risks
- El spec vigente de `sgv-web-shell` choca con una pantalla de login.
- `SGV.Web` no tiene modelo de sesión/auth hoy; se puede mezclar UI con seguridad si no se define bien.
- Si se guarda JWT en cliente, cambian las expectativas de seguridad.
- Los enlaces a `reset password`/`register` del template pueden quedar rotos si no se crean páginas soporte.

### Ready for Proposal
Yes: definir UX de auth, estrategia de sesión/token y si SGV.Web autentica directo o solo consume el API.
