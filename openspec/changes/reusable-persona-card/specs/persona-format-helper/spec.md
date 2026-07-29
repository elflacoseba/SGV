# Especificación: persona-format-helper

## Propósito

Definir el comportamiento del helper estático `PersonaFormatHelper.FormatDocumento(PersonaDto?)` que centraliza el formateo de documento de persona (`"{TipoDocumento} {NumeroDocumento}"`, separador **espacio**) hoy duplicado en `Usuarios/Details.cshtml`, `Usuarios/_Form.cshtml` (como `FormatDocumento`) y `Ocupaciones/_Form.cshtml` (como `FormatearDocumento`). El helper vive en `src/SGV.Web/Helpers/PersonaFormatHelper.cs`, es consumido por la partial `_PersonaCard` y elimina las tres copias inline. **El separador espacio preserva el markup vigente** (PER-CARD-09); el colon que usa el JS para `personaDisplay` vive en otra display distinta (parenthetical) y no se toca. No se introduce en `SGV.Api` ni en otros proyectos.

## Requisitos

### Requirement: PERFMT-01 — Formateo de documento

`PersonaFormatHelper.FormatDocumento(PersonaDto?)` MUST retornar `"{TipoDocumento} {NumeroDocumento}"` (un único **espacio** como separador) cuando ambos campos están presentes. Si falta `TipoDocumento`, SHOULD mostrar sólo `NumeroDocumento` sin espacio líder; si falta `NumeroDocumento`, SHOULD mostrar sólo `TipoDocumento` sin espacio de cola. El método MUST ser `static` y determinista (sin IO, sin reloj). El separador espacio preserva el markup vigente del `<dd>Documento</dd>` server-side y evita regresión visual (PER-CARD-09).

#### Scenario: Documento completo
- GIVEN un `PersonaDto` con `TipoDocumento="DNI"` y `NumeroDocumento="12345678"`
- WHEN se invoca `FormatDocumento`
- THEN SHOULD retornar `"DNI 12345678"`.

#### Scenario: Tipo ausente
- GIVEN `PersonaDto` con `TipoDocumento=null` y `NumeroDocumento="12345678"`
- WHEN se invoca
- THEN SHOULD retornar `"12345678"` (sin espacio líder).

#### Scenario: Número ausente
- GIVEN `PersonaDto` con `TipoDocumento="DNI"` y `NumeroDocumento=null`
- WHEN se invoca
- THEN SHOULD retornar `"DNI"` (sin espacio de cola).

#### Scenario: `PersonaDto` nulo
- GIVEN `PersonaDto? = null`
- WHEN se invoca
- THEN MUST retornar `string.Empty` y MUST NOT arrojar.

### Requirement: PERFMT-02 — Caso `Legajo` (sin documento)

Si `PersonaDto` no tiene documento pero posee `Legajo`, `FormatDocumento` SHOULD retornar `Legajo` para preservar el contrato de `PersonaDisplay` en `Ocupaciones`. Si tampoco existe `Legajo`, MUST retornar `string.Empty`.

#### Scenario: Sólo Legajo
- GIVEN `PersonaDto` sin documento pero con `Legajo="0042"`
- WHEN se invoca
- THEN SHOULD retornar `"0042"`.

#### Scenario: Sin documento ni Legajo
- GIVEN `PersonaDto` sin ambos campos
- WHEN se invoca
- THEN MUST retornar `string.Empty`.

### Requirement: PERFMT-03 — Eliminación de duplicados

Tras aplicar el cambio, MUST NOT existir ningún `@functions` declarando `FormatDocumento` ni `FormatearDocumento` en vistas Razor de `SGV.Web`. El único proveedor del formateo MUST ser `PersonaFormatHelper.FormatDocumento`.

#### Scenario: Sin copias inline en vistas
- GIVEN el árbol de `src/SGV.Web/Pages`
- WHEN se inspecciona con `grep` por `FormatDocumento|FormatearDocumento` en archivos `.cshtml`
- THEN MUST retornar cero coincidencias en definiciones `@functions`.

#### Scenario: Helper invocado desde la partial
- GIVEN `_PersonaCard.cshtml` renderizando documento
- WHEN se inspecciona la partial
- THEN MUST llamar a `PersonaFormatHelper.FormatDocumento(Model)`
- AND MUST NOT redefinir la función localmente.

### Requirement: PERFMT-04 — Namespace y ubicación

`PersonaFormatHelper` MUST vivir en `namespace SGV.Web.Helpers` dentro del archivo `src/SGV.Web/Helpers/PersonaFormatHelper.cs`. El proyecto `SGV.Web` MUST exponerlo como miembro público estático accesible desde sus vistas Razor vía `@using SGV.Web.Helpers`.

#### Scenario: Ubicación y visibilidad
- GIVEN el archivo `src/SGV.Web/Helpers/PersonaFormatHelper.cs`
- WHEN se compila `SGV.Web`
- THEN `PersonaFormatHelper.FormatDocumento` MUST ser `public static`
- AND el namespace MUST ser `SGV.Web.Helpers`.