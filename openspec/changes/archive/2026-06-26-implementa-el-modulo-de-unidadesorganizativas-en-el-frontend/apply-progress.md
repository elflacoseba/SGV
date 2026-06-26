# Apply Progress: implementa-el-modulo-de-unidadesorganizativas-en-el-frontend

## Status
**Phase**: Apply — completado
**Progress**: 10/10 tasks complete
**Mode**: Strict TDD
**Delivery**: size:exception (maintainer-approved)

## Resumen

Se completó el módulo frontend de listado para `Unidades Organizativas` dentro de `SGV.Web`, manteniendo el alcance aprobado: navegación autenticada, consulta SSR, búsqueda, paginación, ordenamiento visible de la página, eliminación confirmada con SweetAlert2 y feedback explícito para éxito/conflicto.

## Implementación

- Nuevo cliente tipado `Integration/Organizacion` con contrato de consulta y eliminación, más parsing de `ProblemDetails` para `409/404`.
- Nueva Razor Page `Pages/Organizacion/UnidadesOrganizativas/Index` con shell autenticado, tabla estilo Inspinia, buscador, paginación, estados vacío/error y `OnPostDeleteAsync` preservando filtros.
- Menú lateral actualizado para exponer `Home` + `Unidades Organizativas` sin placeholders fuera de alcance.
- `SgvWebApplicationFactory` extendido para inyectar dobles del nuevo cliente en pruebas web.
- Assets web actualizados para incluir SweetAlert2 y regenerar bundles rastreados en `wwwroot`.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 9/9 (`WebAuthenticationTests` + `WebShellSmokeTests`) | ✅ Archivo nuevo con fallas de compilación por contratos/página inexistentes | ✅ `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web.UnidadOrganizativaWebTests"` | ✅ acceso anónimo, menú, carga, vacío y error | ✅ helpers de login/dobles consolidados en el mismo archivo |
| 1.2 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 9/9 | ✅ tests dependían de override aún no soportado | ✅ targeted tests en verde tras extender `SgvWebApplicationFactory` | ✅ el doble registra queries/delete y permite distintos escenarios | ✅ override aislado sin romper auth |
| 1.3 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | N/A (archivos nuevos) | ✅ tests referenciaron contratos inexistentes | ✅ targeted tests en verde tras crear contrato/list item | ✅ query + delete cubiertos con casos distintos | ✅ contratos alineados a diseño |
| 2.1 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 9/9 | ✅ assertions nuevas para título, búsqueda, paginación y sort | ✅ targeted tests en verde | ✅ cambio de página + `nombre_desc` ejercen caminos distintos | ✅ decode HTML y asserts conductuales |
| 2.2 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 9/9 | ✅ consulta/delete aún sin cliente real/DI | ✅ targeted tests en verde tras `UnidadOrganizativaApiClient` + `Program.cs` | ✅ GET y DELETE cubiertos | ✅ querystring limitado al backend real sin sort server-side |
| 2.3 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 9/9 | ✅ página sin PageModel/handlers | ✅ targeted tests en verde tras `OnGetAsync` y sort visible | ✅ vacío, error, página 2 y sort visible | ✅ ajuste de binding para `page` reservado |
| 2.4 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 9/9 | ✅ vista inexistente | ✅ targeted tests en verde tras tabla Inspinia | ✅ tabla con filas + empty/error states | ✅ acciones y mensajes consistentes |
| 2.5 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 9/9 | ✅ menú sin entrada del módulo | ✅ targeted tests en verde tras actualizar `_Sidenav` | ✅ `Home` + `Unidades Organizativas` validados | ✅ estado activo agregado sin placeholders extra |
| 3.1 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 18/18 (web shell + auth + UO) | ✅ escenarios de cancelación/success/409 escritos primero | ✅ targeted tests en verde | ✅ hook sin POST, éxito y conflicto | ✅ helpers reutilizados |
| 3.2 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 18/18 | ✅ SweetAlert2 aún ausente | ✅ targeted tests en verde tras assets + hook JS | ✅ script + CSS + presencia de hook | ✅ bundles regenerados |
| 3.3 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 18/18 | ✅ delete handler aún sin feedback/redirección | ✅ targeted tests en verde tras `OnPostDeleteAsync` y parsing de `ProblemDetails` | ✅ éxito ajusta página, `409` preserva fila | ✅ mensajes y redirect explícitos |
| 4.1 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ 18/18 | ✅ nombres/helpers mejorables | ✅ targeted tests en verde tras limpieza | ✅ escenarios permanecen cubiertos | ✅ consolidación final de helpers/asserts |
| 4.2 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` + suite | Build + integration | ✅ 18/18 | ✅ validación final pendiente | ✅ `dotnet build SGV.slnx`, `dotnet test SGV.slnx` | ✅ targeted web + suite completa | ✅ build frontend validado con `npm install --no-package-lock && npx gulp build`; `bun run build` quedó bloqueado por ausencia de bun en el entorno |

## Verification

- ✅ `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web.WebAuthenticationTests|FullyQualifiedName~SGV.Tests.Web.WebShellSmokeTests"`
- ✅ `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web.UnidadOrganizativaWebTests"`
- ✅ `dotnet build SGV.slnx`
- ✅ `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web.WebAuthenticationTests|FullyQualifiedName~SGV.Tests.Web.WebShellSmokeTests|FullyQualifiedName~SGV.Tests.Web.UnidadOrganizativaWebTests"`
- ⚠️ `dotnet test SGV.slnx` → `920/921` en verde; persiste la falla preexistente/unrelated `SGV.Tests.Api.SwaggerConfigurationTests.SwaggerDocument_DefinesBearerSchemeWithoutGlobalSecurityRequirement`
- ⚠️ `bun run build` no pudo ejecutarse porque `bun` no está instalado en el entorno (`zsh: command not found: bun`)
- ✅ Fallback operativo para assets: `npm install --no-package-lock && npx gulp build`

## Remediación focalizada post-verify

- Se agregó evidencia de runtime para la cancelación/confirmación de SweetAlert2 extrayendo el hook JS a `wwwroot/js/pages/unidades-organizativas-index.js` y ejecutándolo desde xUnit con un harness real en Node.
- Se reforzó la evidencia runtime de navegación agregando aserciones negativas sobre placeholders fuera de alcance (`Vacantes`, `Catálogos`, `Reclutamiento`) en la respuesta renderizada del shell autenticado.
- Se reintentó la validación oficial `bun run build` y el entorno sigue sin `bun`; la limitación se mantiene como caveat real.

### Evidencia TDD de remediación

| Remediación | Test File | Layer | RED | GREEN | TRIANGULATE | REFACTOR |
|-------------|-----------|-------|-----|-------|-------------|----------|
| Cancelación real de SweetAlert2 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration + JS runtime harness | ✅ el test falló al requerir un script inexistente y sin export verificable | ✅ `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web.UnidadOrganizativaWebTests"` | ✅ cancelación sin submit + confirmación con submit único | ✅ hook extraído a archivo reusable `wwwroot/js/pages/unidades-organizativas-index.js` |
| Navegación sin placeholders ajenos | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web integration | ✅ nueva aserción negativa escrita antes del ajuste fino del selector | ✅ mismo targeted test en verde | ✅ se validan `Home`/`Unidades Organizativas` y se excluyen placeholders concretos | ✅ asserts negativos acotados al markup real del menú |
| Reintento validación frontend oficial | N/A | Build | ✅ se intentó el comando real requerido por el repo | ⚠️ `bun run build` sigue bloqueado por entorno (`bun` ausente) | ➖ no aplica | ➖ no aplica |

### Verificaciones adicionales de remediación

- ✅ `dotnet build SGV.slnx`
- ✅ `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web.UnidadOrganizativaWebTests"` → `11/11` en verde
- ✅ `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web.WebAuthenticationTests|FullyQualifiedName~SGV.Tests.Web.WebShellSmokeTests|FullyQualifiedName~SGV.Tests.Web.UnidadOrganizativaWebTests"` → `20/20` en verde
- ⚠️ `dotnet test SGV.slnx` → `781/923` en verde, `141` skip, persiste la falla no relacionada `SGV.Tests.Api.SwaggerConfigurationTests.SwaggerDocument_DefinesBearerSchemeWithoutGlobalSecurityRequirement`
- ⚠️ `bun run build` → `zsh: command not found: bun`

## Issues / Notes

- La querystring `page` en Razor Pages necesitó binding explícito con `FromQuery(Name = "page")` / `FromForm(Name = "page")` porque el nombre reservado no estaba entrando al handler como esperaba.
- Los tests HTML necesitaron `HtmlDecode` para validar textos con acentos renderizados como entidades.
- La verificación full-suite sigue condicionada por la falla Swagger ya informada como contexto no relacionado.

## Next

- Continuar con `sdd-verify`, aceptando como riesgo residual la falla Swagger no relacionada y la ausencia de `bun` en este entorno.
