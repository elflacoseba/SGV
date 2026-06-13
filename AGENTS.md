# Guía del Repositorio

## Estructura del Proyecto y Organización

Este repositorio actualmente contiene artefactos de planificación para SGV, no código de aplicación.

- `openspec/config.yaml`: configuración de OpenSpec para trabajo guiado por especificaciones.
- `openspec/changes/<nombre-del-cambio>/`: cambios propuestos con `proposal.md`, `design.md`, `tasks.md` y especificaciones delta.
- `openspec/changes/<nombre-del-cambio>/specs/<capacidad>/spec.md`: escenarios de requisitos para una capacidad.
- `.codex/skills/`: habilidades locales de Codex usadas por colaboradores y agentes.

Cuando comience la implementación, ubica el código fuente en una estructura clara como `src/`, las pruebas en `tests/` y las migraciones cerca del proyecto de infraestructura.

## Comandos de Construcción, Prueba y Desarrollo

Todavía no existe una aplicación construible. Comandos útiles para el estado actual:

- `rg --files`: lista rápidamente los archivos del workspace.
- `git status --short`: muestra cambios pendientes.
- `openspec status --change <nombre-del-cambio>`: revisa el avance de OpenSpec cuando el CLI esté instalado.
- `openspec validate --change <nombre-del-cambio>`: valida un cambio antes de implementarlo cuando esté disponible.

No agregues comandos de compilación hasta que exista la solución .NET. Cuando se incorpore, documenta comandos exactos como `dotnet build`, `dotnet test` y los comandos de migraciones.

## Estilo de Código y Convenciones de Nombres

Usa Markdown conciso con encabezados descriptivos para documentos de planificación. Nombra los cambios OpenSpec en formato kebab case, por ejemplo `design-sgv-database`. Mantén las carpetas de capacidades en minúsculas y con nombres específicos, como `sgv-database`.

Para futuro código .NET, usa convenciones estándar de C#: indentación de cuatro espacios, PascalCase para tipos y miembros públicos, camelCase para variables locales y parámetros, y métodos asíncronos terminados en `Async`.

## Guías de Pruebas

Todavía no hay marco de pruebas configurado. La implementación futura debe incluir pruebas unitarias para reglas de dominio, pruebas de persistencia para mapeos y restricciones de EF Core, y pruebas de migración contra SQL Server. Nombra las pruebas por comportamiento, por ejemplo `CalcularCompatibilidad_DevuelveCoincidenciaParcial_CuandoFaltaNivelObligatorio`.

## Confirmaciones y Solicitudes de Cambio

El repositorio aún no tiene historial de confirmaciones, por lo que no existe una convención local establecida. Usa mensajes breves en imperativo, opcionalmente con alcance, como `docs: agregar guia del repositorio` o `spec: definir requisitos de vacantes`.

Las solicitudes de cambio deben incluir resumen, artefactos OpenSpec modificados, resultados de validación, issues o decisiones vinculadas, y capturas solo cuando haya cambios de UI.

## Instrucciones para Agentes

Antes de editar, revisa los artefactos OpenSpec existentes y preserva cambios del usuario. No implementes código de base de datos ante una solicitud solo de planificación. Si el CLI de OpenSpec no está disponible, crea manualmente los archivos esperados e informa que no se pudo ejecutar la validación.
