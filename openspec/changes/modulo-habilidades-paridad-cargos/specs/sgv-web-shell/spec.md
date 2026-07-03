# Delta for sgv-web-shell

## Propósito

Extender la navegación autenticada del shell para hacer descubrible el nuevo módulo `Habilidades` con la misma jerarquía administrativa usada por `Cargos`.

## Modificaciones

- La navegación mínima pasa de `Home`, `Unidades Organizativas` y `Cargos` a incluir también `Habilidades`.
- `Habilidades` se publica como grupo colapsable debajo de `Cargos`, con `Listado` y `Nueva`.

## MODIFIED Requirements

### Requirement: Minimal technical navigation

El sistema MUST incluir la navegación mínima del shell y, a partir de este cambio, MUST exponer `Unidades Organizativas`, `Cargos` y `Habilidades` como módulos funcionales de negocio habilitados. La navegación autenticada MUST mantener `Home`, `Unidades Organizativas`, `Cargos` y `Habilidades`; `Habilidades` MUST renderizarse debajo de `Cargos` como grupo colapsable con icono `ti ti-star` y submenú `Listado` + `Nueva`; y el shell MUST NOT mostrar placeholders de otros módulos no especificados.

(Previously: la navegación autenticada exponía `Home`, `Unidades Organizativas` y `Cargos` como módulos funcionales habilitados.)

#### Scenario: Navegación mínima con Habilidades habilitado

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario inspecciona las entradas disponibles
- THEN las entradas MUST incluir `Home`, `Unidades Organizativas`, `Cargos` y `Habilidades`
- AND `Habilidades` MUST ser alcanzable como destino del shell.

#### Scenario: Submenú de Habilidades visible y activo

- GIVEN un usuario autenticado ubicado en una página de Habilidades
- WHEN se renderiza el grupo `Habilidades`
- THEN la navegación MUST mostrar `Listado` y `Nueva`
- AND MUST reflejar el estado `active` del grupo y de la opción correspondiente.

#### Scenario: Otros módulos siguen fuera de alcance

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario revisa las opciones visibles
- THEN la navegación MUST NOT mostrar placeholders de reclutamiento, vacantes, catálogos u otros módulos no especificados.

## Out of scope

- No redefine branding, layout ni controles de Inspinia.
- No agrega navegación para asignaciones de habilidades con cargos o personas.
