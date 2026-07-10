# Guía del Repositorio

## Resumen rápido

SGV es una solución .NET 10 con Clean Architecture, ASP.NET Core API, Razor Pages para `SGV.Web`, EF Core 9 y MySQL 8 mediante Pomelo. `SGV.Web` hoy funciona como shell autenticada que consume `SGV.Api` mediante clientes tipados `HttpClient`, con cookie auth en web y JWT bearer en API. El grafo de proyectos es `Dominio ← Aplicacion ← Contracts ← {Api, Web}`; `SGV.Web` ya **NO** referencia `SGV.Api` directamente — sus contratos wire viven en `SGV.Contracts`. El flujo del repo combina desarrollo tradicional con OpenSpec/SDD y `strict_tdd: true`.

## Ruta rápida para trabajar

1. Restaurá dependencias con `dotnet restore`.
2. Compilá la solución con `dotnet build SGV.slnx`.
3. Ejecutá pruebas con `dotnet test SGV.slnx`.
4. Si tocás `src/SGV.Web`, instalá dependencias frontend con `bun install` dentro de `src/SGV.Web` y validá el bundle con `bun run build`.
5. Si tocás persistencia o integración, validá también contra MySQL.
6. Antes de planificar o implementar, revisá `openspec/` y `docs/decisiones-implementacion.md`.
7. Antes del primer `dotnet run` de `SGV.Api`, generá una clave JWT propia con `dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes>" --project src/SGV.Api`. Sin esto, el host **no arranca** (`OptionsValidationException`). El placeholder dev en `src/SGV.Api/appsettings.Development.json` también sirve para un primer arranque, pero **NO es apto para producción ni commits**. Ver sección "Gestión de secretos JWT" en `docs/decisiones-implementacion.md`.

## Estructura del Proyecto y Organización

- `SGV.slnx`: solución principal del repositorio.
- `global.json`: fija SDK `10.0.300`.
- `src/SGV.Dominio/`: entidades, value objects y reglas de negocio.
- `src/SGV.Aplicacion/`: casos de uso, contratos (interfaces) de servicios, validaciones y servicios. Solo depende de `SGV.Dominio` y `SGV.Contracts`.
- `src/SGV.Contracts/`: **wire-types compartidos** entre `SGV.Api` y `SGV.Web` (records/DTOs de request/response/result + constantes). Es una **leaf** del grafo (no referencia ningún proyecto). Organizado por subdominio: `Auth/`, `Organizacion/`, `Habilidades/`, `Seguridad/`.
- `src/SGV.Infraestructura/`: EF Core, Identity, repositorios, interceptor de auditoría y migraciones.
- `src/SGV.Api/`: controladores HTTP, autenticación y composición de la aplicación. Depende de `SGV.Aplicacion`, `SGV.Contracts` e `SGV.Infraestructura`.
- `src/SGV.Web/`: frontend Razor Pages y shell web basado en Inspinia Starterkit. Depende **únicamente** de `SGV.Contracts` (no de `SGV.Api`).
- `src/SGV.Web/Integration/`: clientes tipados hacia `SGV.Api`, bridge de JWT (`ApiBearerTokenHandler`) y contratos de integración web.
- `src/SGV.Web/Pages/Organizacion/`: módulos web vigentes de unidades organizativas, cargos y habilidades.
- `src/SGV.Web/Pages/Error/`: páginas de error HTTP de la shell web (`401`, `403`, `404`, `408`, `500`, `Maintenance`).
- `tests/SGV.Tests/`: pruebas unitarias, de persistencia, integración API, compatibilidad y smoke tests web.
- `docs/decisiones-implementacion.md`: decisiones técnicas vigentes del proyecto.
- `docs/migracion-inicial-sgv.sql`: script SQL idempotente generado.
- `openspec/config.yaml`: configuración SDD/OpenSpec del repo.
- `openspec/changes/<cambio>/`: artefactos de cambio (`proposal.md`, `design.md`, `tasks.md`, `exploration.md`, `apply-progress.md`, `verify-report.md`, `archive-report.md` y `specs/**/spec.md` según aplique).
- `InspinaTemplate/`: template de referencia importado para la shell web y ejemplos visuales.
- `.github/workflows/ci.yml`: pipeline de CI con build + tests sobre MySQL 8.

## Comandos de Construcción, Prueba y Desarrollo

- `dotnet restore`: restaura dependencias.
- `dotnet build SGV.slnx`: compila toda la solución.
- `dotnet test SGV.slnx`: ejecuta toda la suite.
- `dotnet test SGV.slnx --collect:"XPlat Code Coverage"`: ejecuta pruebas con cobertura.
- `bun install` (en `src/SGV.Web`): instala dependencias del pipeline frontend.
- `bun run dev` (en `src/SGV.Web`): levanta el pipeline de assets de Inspinia/Gulp para desarrollo.
- `bun run build` (en `src/SGV.Web`): genera el bundle frontend para validación local.
- `dotnet ef migrations add <Nombre> --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --output-dir Persistencia/Migraciones`: crea una migración.
- `dotnet ef migrations script --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --idempotent --output docs/migracion-inicial-sgv.sql`: genera script SQL idempotente.

## Stack Técnico y Restricciones

- .NET 10 (`net10.0`) con SDK `10.0.300`.
- C# 14, nullable enabled e implicit usings enabled.
- Clean Architecture: `Dominio -> Aplicacion -> Infraestructura`, con `Api` como composition root.
- ASP.NET Core API + Swagger (`Swashbuckle.AspNetCore`).
- ASP.NET Core Razor Pages en `SGV.Web` para la shell/frontend.
- `SGV.Api` autentica con JWT bearer; `SGV.Web` autentica con cookies y reenvía el token a la API vía `ApiBearerTokenHandler`.
- EF Core 9.x.
- `Pomelo.EntityFrameworkCore.MySql 9.0.0` como proveedor único soportado.
- MySQL 8 requerido para escenarios reales de persistencia e integración.
- ASP.NET Core Identity con clave string.
- FluentValidation en capa de aplicación.
- Bun + Gulp para assets del frontend en `src/SGV.Web`.
- Google Charts OrgChart para la vista de organigrama de unidades organizativas.
- xUnit 2.9.2 + `Microsoft.NET.Test.Sdk` + `coverlet.collector`.

## Convenciones de Código y Diseño

- Usá indentación de cuatro espacios.
- PascalCase para tipos y miembros públicos; camelCase para variables locales y parámetros.
- Métodos asíncronos terminan en `Async`.
- Respetá separaciones de capa: dominio no depende de infraestructura; aplicación no conoce detalles HTTP.
- `SGV.Web` actúa como capa web/composition layer; no mover lógica de dominio o persistencia al frontend.
- La integración runtime con backend debe pasar por clientes tipados en `src/SGV.Web/Integration/`. Los wire-types consumidos por Web viven en `SGV.Contracts` (no en `SGV.Api`).
- Los cambios OpenSpec se nombran en kebab-case.
- Conservá nombres técnicos, código, comentarios e identificadores en inglés salvo que el contexto existente del archivo exija otra cosa.
- Los documentos generados por SDD deben escribirse en español: `proposal.md`, `design.md`, `tasks.md`, `exploration.md`, `apply-progress.md`, `verify-report.md`, `archive-report.md` y `specs/**/spec.md`.

## Guías de Pruebas

- El repo trabaja con `strict_tdd: true` en `openspec/config.yaml`.
- La suite incluye pruebas de dominio, aplicación, persistencia, API, compatibilidad y web.
- Los tests de API usan `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs`.
- Los tests web usan `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs`.
- La suite web/API ya cubre auth bridge web->API, listados segmentados `activas|eliminadas`, reactivación por PRG y fallos de transporte recuperables en clientes tipados.
- La CI levanta MySQL 8 y ejecuta `dotnet test --no-build --configuration Release`.
- Si cambiás persistencia, índices únicos, soft delete, Identity o migraciones, no alcanza con pruebas puramente unitarias.
- Si tocás `SGV.Web` o assets frontend, validá al menos `bun run build` además de la suite .NET relevante.

## Filosofía de Testing

El objetivo de los tests es proteger el comportamiento funcional de la aplicación, no maximizar el porcentaje de cobertura ni la cantidad de código de pruebas.

Cada test debe aportar valor real. Antes de generar un test, evaluar si protege una regla de negocio, un comportamiento importante o previene una regresión. Si la respuesta es negativa, no generar el test.

### Qué debe testearse

Priorizar siempre:

- Reglas de negocio del Dominio.
- Casos de uso de la capa Application.
- Validaciones mediante FluentValidation.
- Cálculos.
- Transformaciones de datos con lógica.
- Permisos y autorización.
- Casos límite (Edge Cases).
- Correcciones de errores (cada bug corregido debe quedar protegido por al menos un test).
- Flujos de negocio que involucren múltiples operaciones.

### Qué no debe testearse salvo que se solicite explícitamente

Evitar generar tests para:

- Getters y setters.
- Constructores triviales.
- DTOs.
- Records sin lógica.
- Entidades sin comportamiento.
- Configuración de Dependency Injection.
- Configuración de ASP.NET Core.
- Código generado automáticamente.
- Mapeos simples.
- Controladores que únicamente delegan la ejecución al caso de uso correspondiente.
- Repositorios cuya única responsabilidad sea invocar Entity Framework Core sin agregar lógica propia.

### Cantidad de tests

- No generar múltiples tests que validen exactamente el mismo comportamiento.
- Cuando varios casos puedan cubrirse mediante un test parametrizado, utilizar un único test con Theory e InlineData en lugar de múltiples métodos prácticamente iguales.
- Preferir pocos tests de alta calidad antes que muchos tests redundantes.
- Si un método contiene una lógica sencilla y un único test cubre completamente su comportamiento, no generar casos adicionales innecesarios.

### Relación entre código y tests

- Es aceptable que el proyecto de tests tenga más líneas de código que el proyecto principal únicamente cuando exista una justificación funcional.
- No generar automáticamente cinco o más tests para proteger un método trivial.
- Si la implementación es pequeña y de bajo riesgo, mantener la suite de pruebas proporcional a su complejidad.

### Enfoque de calidad

- Los tests deben validar comportamiento observable, nunca detalles internos de implementación.
- No escribir tests que dependan de nombres de variables, implementación privada o estructura interna del código.
- Los tests deben seguir siendo válidos aunque la implementación cambie, siempre que el comportamiento esperado permanezca igual.

### Cobertura

- No perseguir el 100% de cobertura.
- Priorizar cobertura sobre funcionalidades críticas antes que cobertura sobre código trivial.
- Una cobertura razonable sobre reglas de negocio es preferible a una cobertura total basada en pruebas de bajo valor.

### Optimización de tiempo y tokens

- Antes de generar tests, evaluar el costo-beneficio.
- No aumentar innecesariamente el tamaño del proyecto de pruebas.
- Cada test adicional implica mayor tiempo de mantenimiento, mayor consumo de contexto y mayor consumo de tokens para futuras tareas.
- Generar únicamente los tests necesarios para proporcionar confianza en el funcionamiento del sistema.

### Prioridad por capas

| Capa | Cobertura esperada |
|---|---|
| **Dominio** | Alta |
| **Application (Casos de Uso)** | Alta |
| **Infrastructure** | Moderada, únicamente cuando exista lógica propia |
| **API** | Mínima. No testear controladores que solo deleguen |
| **Razor Pages** | Testear únicamente cuando exista lógica relevante en el PageModel. No generar tests para código de presentación o marcado HTML |

### Regla general

Ante la duda, generar **menos** tests, pero que sean significativos, mantenibles y orientados al comportamiento del sistema. La calidad de una suite de pruebas se mide por el valor que aporta, no por la cantidad de archivos, métodos o líneas de código.

## Tests de Integración con MySQL

- Si tenés MySQL local con `root` sin password en puerto 3306 (setup default de Homebrew/Docker), los tests `[MySqlFact]` corren automáticamente contra la DB `sgv_test` **sin configuración adicional**.
- El bootstrap es automático: `MySqlFactAttribute` aplica `Database.Migrate()` una vez por sesión de test. Crea `sgv_test` si no existe y aplica migraciones pendientes. Migrate es idempotente.
- `TestSgvDbContextFactory` resuelve la connection string en este orden:
  1. `ConnectionStrings__SgvDatabase` env var.
  2. `appsettings.json` / `appsettings.Development.json` desde el CWD del test runner.
  3. Default: `Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;` (stock MySQL dev).
  4. Si cae al stub `127.0.0.1:1` (sin configuración y sin MySQL), los `[MySqlFact]` se skipean limpio (146 tests).
- Si tu MySQL local usa otro puerto, usuario o password, seteá `ConnectionStrings__SgvDatabase` en la shell antes de `dotnet test`.
- El factory de producción (`SgvDbContextFactory`) **no usa estos defaults**: tira `InvalidOperationException` si no hay configuración, forzando al developer a usar `dotnet user-secrets` o env var en CI.
- **Bug conocido (issue #59):** 12 tests de `OcupacionRepositoryTests` fallan contra MySQL real por un bug de tipo en la migración inicial (`ActivePuestoIdUnique INT` incompatible con `PuestoId CHAR(36)`). Pendiente de SDD change.

## Decisiones Técnicas que NO conviene romper

- MySQL es el proveedor activo; no introducir supuestos de SQL Server.
- La unicidad sobre registros activos usa columnas generadas para convivir con soft delete.
- Identity mantiene `IdentityUser` con clave string.
- La auditoría centraliza eventos en una tabla `Auditorias` mediante interceptor de EF Core.
- `SGV.Api` valida autenticación solo con bearer token; `SGV.Web` depende del bridge por cookie + `ApiBearerTokenHandler` para hablar con la API autenticada.
- Los listados segmentados de cargos, habilidades y unidades organizativas usan `status=activas|eliminadas`; no volver a mezclar ambos conjuntos en un mismo contrato de consulta.
- El organigrama de unidades organizativas usa Google OrgChart como vista oficial de jerarquía en web.
- Las operaciones write de cargos, habilidades y usuarios están protegidas por rol `Administrador`; no relajar esa frontera sin cambio explícito de negocio.
- `SGV.Web` es una shell Razor Pages apoyada en Inspinia; preservar esa responsabilidad y no mezclarla con reglas de negocio.
- La cookie de autenticación web y la política CORS de la API se endurecieron por ambiente en la issue #101. La cookie lleva `HttpOnly=true`, `SameSite=Lax`, `SecurePolicy={SameAsRequest en Development | Always en otros}`; la API exige `AllowedOrigins` poblado fuera de `Development` con fail-loud. Ver `docs/decisiones-implementacion.md` para la matriz completa ambiente ↔ seguridad.
- Revisá `docs/decisiones-implementacion.md` antes de modificar persistencia, auditoría, ocupaciones o seguridad.

## OpenSpec / SDD

- Antes de editar, revisá artefactos existentes en `openspec/changes/`.
- No sobrescribas artefactos del cambio sin preservar trabajo previo del usuario.
- Si el pedido es solo de planificación, no implementes código ni migraciones.
- Si el CLI de OpenSpec no está disponible, creá/manualizá los archivos esperados e informá que no se pudo validar con la herramienta.
- Todo artefacto SDD nuevo o actualizado debe quedar en español, incluso cuando la herramienta o plantilla base venga en inglés.

## Confirmaciones y Solicitudes de Cambio

- Usá conventional commits breves, por ejemplo `feat: add ocupaciones query service` o `fix: separate db update exception handling`.
- Nunca agregues `Co-Authored-By` ni atribución a IA.

## Instrucciones para Agentes

- Toda respuesta conversacional al usuario debe salir en español.
- Verificá claims técnicos contra código o documentos antes de afirmarlos.
- Preservá cambios del usuario en curso.
- Antes de tocar estructura, dependencias, persistencia o reglas de negocio, revisá primero los artefactos OpenSpec y `docs/decisiones-implementacion.md`.
- Si tocás algo que afecte build o test, corré la validación mínima relevante.
- Si generás documentos en cualquier fase SDD, redactalos en español y alineados con el estado real del repo.
