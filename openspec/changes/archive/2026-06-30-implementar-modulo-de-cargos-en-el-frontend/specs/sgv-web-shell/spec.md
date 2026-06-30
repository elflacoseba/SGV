# Delta de sgv-web-shell

## MODIFIED Requirements

### Requirement: Minimal technical navigation

El sistema MUST incluir la navegación mínima del shell y, a partir de este cambio, MUST exponer `Unidades Organizativas` y `Cargos` como módulos funcionales de negocio habilitados. La navegación autenticada MUST mantener `Home`, `Unidades Organizativas` y `Cargos`, y MUST NOT mostrar placeholders de otros módulos de negocio todavía no especificados.

(Previously: la navegación autenticada exponía `Home` y `Unidades Organizativas` como único módulo funcional de negocio.)

#### Scenario: Navegación mínima con módulos funcionales habilitados

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario inspecciona las entradas disponibles
- THEN las entradas MUST incluir `Home`, `Unidades Organizativas` y `Cargos`
- AND `Cargos` MUST ser alcanzable como destino del shell.

#### Scenario: Otros módulos siguen fuera de alcance

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario revisa las opciones visibles
- THEN la navegación MUST NOT mostrar placeholders de reclutamiento, vacantes, catálogos u otros módulos no especificados.
