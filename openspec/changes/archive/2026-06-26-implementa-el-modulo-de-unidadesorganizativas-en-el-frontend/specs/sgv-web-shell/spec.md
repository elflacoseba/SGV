# Delta para sgv-web-shell

## MODIFIED Requirements

### Requirement: Minimal technical navigation

El sistema MUST incluir la navegación mínima del shell y, a partir de este cambio, MUST exponer `Unidades Organizativas` como primer módulo funcional de negocio. La navegación autenticada MUST mantener `Home` y `Unidades Organizativas`, y MUST NOT mostrar placeholders de otros módulos de negocio todavía no especificados.

(Previously: la navegación del shell se limitaba a destinos técnicos y no exponía ningún módulo de negocio.)

#### Scenario: Navegación mínima con primer módulo funcional

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario inspecciona las entradas disponibles
- THEN las entradas MUST incluir `Home` y `Unidades Organizativas`
- AND `Unidades Organizativas` MUST ser alcanzable como destino del shell

#### Scenario: Otros módulos siguen fuera de alcance

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario revisa las opciones visibles
- THEN la navegación MUST NOT mostrar placeholders de reclutamiento, vacantes, catálogos u otros módulos no especificados
