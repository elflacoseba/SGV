# Propuesta: Hardening del BFF same-origin de consulta de Personas

## Intención y contexto

El issue #163 corrige `RIS-001/002` del verify adversarial (`review-risk`) del issue padre #157, archivado tras mergear los PRs #158/#159/#160. El BFF reenvía entradas sin límites y fija la consulta para un único consumidor.

- **RIS-001:** un cliente puede enviar `?search` de 1 MB; el valor llega al backend y amplifica el costo de `LIKE` sobre filas y campos, con comportamiento cuadrático y riesgo de DoS suave.
- **RIS-002:** `sort` y `segmento` están hardcodeados, por lo que cada consumidor futuro empujaría más variantes al mismo handler hasta convertirlo en un adapter monolítico.

## Alcance

### Incluido

- Limitar `search` a 200 caracteres.
- Aceptar y validar `sort` y `segmento` antes de invocar `IPersonaApiClient`.
- Extender las pruebas BFF existentes mediante `QueryCalls`.

### Fuera de alcance (Non-goals)

- Extender `PersonaRepository.ApplySort`, modificar el backend, contratos, migraciones o `FakePersonaApiClient`.
- Introducir `PersonaSort`; `PersonaListQuery.Sort` continúa siendo `string?`.
- Mover el BFF fuera de `Program.cs` o resolver otros findings de #157.

## Capacidades

### Nuevas

Ninguna.

### Modificadas

- `usuario-web-selector-persona-buscador`: la consulta same-origin valida longitud, orden y segmento, devolviendo errores HTTP 400 estandarizados.

## Enfoque y criterios de aceptación

En `Program.cs`, incorporar parámetros opcionales, validar antes de `QueryAsync` y responder con `Results.Problem(...)` (`ProblemDetails`, status 400).

- [ ] `search` de hasta 200 caracteres se reenvía; más de 200 devuelve 400 y no llama al cliente tipado.
- [ ] `sort` acepta únicamente `apellidos_asc`, `apellidos_desc`, `nombres_asc`, `nombres_desc`, `legajo_asc`, `legajo_desc`, `email_asc` y `email_desc`; otro valor devuelve 400.
- [ ] `segmento` acepta únicamente `activas|eliminadas`; otro valor devuelve 400.
- [ ] Sin `sort` ni `segmento`, se preservan `apellidos_asc` y `Activas`.
- [ ] Valores válidos llegan correctamente a `PersonaListQuery`.

## Decisión de diseño

La whitelist replica exactamente `ApplySort`: incluye `email_*`. `documento_asc/desc` queda conscientemente excluido porque el repositorio no lo implementa; si negocio lo requiere, será un follow-up backend separado.

## Áreas afectadas y tamaño

| Archivo | Cambio |
|---|---|
| `src/SGV.Web/Program.cs:210-229` | Validación y defaults del BFF |
| `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs` | Casos RED→GREEN |

Estimación: **50-80 LoC**, single PR; 12,5-20% del budget de 400 LoC.

## Riesgos

- La whitelist puede desalinearse si cambia `ApplySort`; mitigar con tests explícitos.
- `documento_*` seguirá indisponible: trade-off aceptado para evitar ampliar #163.
- Consumidores que hoy envíen valores inválidos recibirán 400; los defaults mantienen back-compat del modal.

## Rollback y dependencias

Revertir el commit restaura el handler previo. Sin migraciones, configuración ni dependencias externas.

## Restricciones y verificación

Strict TDD (`RED→GREEN`); `SGV.Web` permanece como shell. Artefactos en español y commits conventional sin `Co-Authored-By`.

Validar `dotnet build SGV.slnx`; `dotnet test SGV.slnx --filter FullyQualifiedName~PersonaBuscadorModal`; `dotnet test SGV.slnx`; y `bun run build` desde `src/SGV.Web`.
