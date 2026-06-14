# Guía del Repositorio

## Estructura del Proyecto y Organización

Este repositorio contiene la solución .NET 10 para SGV (Sistema de Gestión de Vacantes) con Clean Architecture.

- `src/SGV.Dominio/`: modelo de dominio y reglas de negocio.
- `src/SGV.Aplicacion/`: lógica de aplicación y servicios.
- `src/SGV.Infraestructura/`: persistencia EF Core con Pomelo/MySQL 8, migraciones y configuraciones.
- `tests/SGV.Tests/`: pruebas xUnit para dominio, aplicación y persistencia.
- `openspec/config.yaml`: configuración de OpenSpec para trabajo guiado por especificaciones.
- `openspec/changes/<nombre-del-cambio>/`: cambios propuestos con `proposal.md`, `design.md`, `tasks.md` y especificaciones delta.
- `docs/`: documentación técnica y scripts de migración SQL.

## Comandos de Construcción, Prueba y Desarrollo

- `dotnet build`: compila toda la solución.
- `dotnet test`: ejecuta las pruebas xUnit.
- `dotnet test --collect:"XPlat Code Coverage"`: ejecuta pruebas con cobertura.
- `dotnet ef migrations add <Nombre> --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --output-dir Persistencia/Migraciones`: genera una nueva migración.
- `dotnet ef migrations script --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --idempotent --output docs/migracion-inicial-sgv.sql`: genera el script SQL idempotente.

## Estilo de Código y Convenciones de Nombres

Se usan convenciones estándar de C#: indentación de cuatro espacios, PascalCase para tipos y miembros públicos, camelCase para variables locales y parámetros, y métodos asíncronos terminados en `Async`. Los cambios OpenSpec se nombran en kebab case.

## Guías de Pruebas

Las pruebas usan xUnit 2.9.2. Se incluyen pruebas unitarias para reglas de dominio, pruebas de persistencia para mapeos y restricciones de EF Core con Pomelo/MySQL, y pruebas de compatibilidad de habilidades. Las migraciones se generan contra MySQL 8.

## Stack Técnico

- .NET 10 (`net10.0`), SDK 10.0.300
- Entity Framework Core 9.x (paquetes relacionales en 9.x)
- Pomelo Entity Framework Core MySql 9.0.0
- MySQL 8 como proveedor de base de datos
- ASP.NET Core Identity con EF Core
- xUnit 2.9.2 para pruebas

## Confirmaciones y Solicitudes de Cambio

Usa mensajes breves en imperativo con conventional commits, por ejemplo `feat: agregar modelo de vacantes` o `fix: corregir indice unico MySQL`.

## Instrucciones para Agentes

Antes de editar, revisa los artefactos OpenSpec existentes y preserva cambios del usuario. No implementes código de base de datos ante una solicitud solo de planificación. Si el CLI de OpenSpec no está disponible, crea manualmente los archivos esperados e informa que no se pudo ejecutar la validación.
Toda la salida de información debe ser generada en español.
