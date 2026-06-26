# Guía del Repositorio

## Resumen rápido

SGV es una solución .NET 10 con Clean Architecture, ASP.NET Core API, Razor Pages para `SGV.Web`, EF Core 9 y MySQL 8 mediante Pomelo. El flujo del repo combina desarrollo tradicional con OpenSpec/SDD y `strict_tdd: true`.

## Ruta rápida para trabajar

1. Restaurá dependencias con `dotnet restore`.
2. Compilá la solución con `dotnet build SGV.slnx`.
3. Ejecutá pruebas con `dotnet test SGV.slnx`.
4. Si tocás `src/SGV.Web`, instalá dependencias frontend con `bun install` dentro de `src/SGV.Web` y validá el bundle con `bun run build`.
5. Si tocás persistencia o integración, validá también contra MySQL.
6. Antes de planificar o implementar, revisá `openspec/` y `docs/decisiones-implementacion.md`.

## Estructura del Proyecto y Organización

- `SGV.slnx`: solución principal del repositorio.
- `global.json`: fija SDK `10.0.300`.
- `src/SGV.Dominio/`: entidades, value objects y reglas de negocio.
- `src/SGV.Aplicacion/`: casos de uso, contratos, validaciones y servicios.
- `src/SGV.Infraestructura/`: EF Core, Identity, repositorios, interceptor de auditoría y migraciones.
- `src/SGV.Api/`: controladores HTTP, autenticación y composición de la aplicación.
- `src/SGV.Web/`: frontend Razor Pages y shell web basado en Inspinia Starterkit.
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
- EF Core 9.x.
- `Pomelo.EntityFrameworkCore.MySql 9.0.0` como proveedor único soportado.
- MySQL 8 requerido para escenarios reales de persistencia e integración.
- ASP.NET Core Identity con clave string.
- FluentValidation en capa de aplicación.
- Bun + Gulp para assets del frontend en `src/SGV.Web`.
- xUnit 2.9.2 + `Microsoft.NET.Test.Sdk` + `coverlet.collector`.

## Convenciones de Código y Diseño

- Usá indentación de cuatro espacios.
- PascalCase para tipos y miembros públicos; camelCase para variables locales y parámetros.
- Métodos asíncronos terminan en `Async`.
- Respetá separaciones de capa: dominio no depende de infraestructura; aplicación no conoce detalles HTTP.
- `SGV.Web` actúa como capa web/composition layer; no mover lógica de dominio o persistencia al frontend.
- Los cambios OpenSpec se nombran en kebab-case.
- Conservá nombres técnicos, código, comentarios e identificadores en inglés salvo que el contexto existente del archivo exija otra cosa.
- Los documentos generados por SDD deben escribirse en español: `proposal.md`, `design.md`, `tasks.md`, `exploration.md`, `apply-progress.md`, `verify-report.md`, `archive-report.md` y `specs/**/spec.md`.

## Guías de Pruebas

- El repo trabaja con `strict_tdd: true` en `openspec/config.yaml`.
- La suite incluye pruebas de dominio, aplicación, persistencia, API, compatibilidad y web.
- Los tests de API usan `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs`.
- Los tests web usan `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs`.
- La CI levanta MySQL 8 y ejecuta `dotnet test --no-build --configuration Release`.
- Si cambiás persistencia, índices únicos, soft delete, Identity o migraciones, no alcanza con pruebas puramente unitarias.
- Si tocás `SGV.Web` o assets frontend, validá al menos `bun run build` además de la suite .NET relevante.

## Decisiones Técnicas que NO conviene romper

- MySQL es el proveedor activo; no introducir supuestos de SQL Server.
- La unicidad sobre registros activos usa columnas generadas para convivir con soft delete.
- Identity mantiene `IdentityUser` con clave string.
- La auditoría centraliza eventos en una tabla `Auditorias` mediante interceptor de EF Core.
- `SGV.Web` es una shell Razor Pages apoyada en Inspinia; preservar esa responsabilidad y no mezclarla con reglas de negocio.
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
