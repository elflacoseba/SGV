# Delta de sgv-web-shell — implementar módulo Puestos en frontend

## MODIFIED Requirements

### Requirement: Minimal technical navigation

El sistema MUST incluir la navegación mínima del shell y, a partir de este cambio, MUST exponer `Unidades Organizativas`, `Cargos`, `Habilidades` y `Puestos` como módulos funcionales de negocio habilitados. La navegación autenticada MUST mantener `Home`, `Unidades Organizativas`, `Cargos`, `Habilidades` y `Puestos`; `Puestos` MUST renderizarse dentro del grupo `Organización` como entry colapsable con icono `ti ti-hierarchy` y submenú `Listado` + `Nuevo`; y el shell MUST NOT mostrar placeholders de otros módulos no especificados.

(Previously: la navegación autenticada exponía `Home`, `Unidades Organizativas`, `Cargos` y `Habilidades` como módulos funcionales habilitados, sin entry colapsable para `Puestos`.)

#### Scenario: Navegación mínima con Puestos habilitado

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario inspecciona las entradas disponibles
- THEN las entradas MUST incluir `Home`, `Unidades Organizativas`, `Cargos`, `Habilidades` y `Puestos`
- AND `Puestos` MUST ser alcanzable como destino del shell dentro del grupo `Organización`.

#### Scenario: Submenú de Puestos visible y activo

- GIVEN un usuario autenticado ubicado en `/organizacion/puestos` o cualquier subruta (`crear`, `editar/{id}`, `detalle/{id}`)
- WHEN se renderiza el grupo `Organización` del sidenav
- THEN la navegación MUST mostrar `Puestos` expandido
- AND MUST incluir los sub-items `Listado` y `Nuevo`
- AND MUST reflejar el estado `active` para el sub-item correspondiente.

#### Scenario: Otros módulos siguen fuera de alcance

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario revisa las opciones visibles
- THEN la navegación MUST NOT mostrar placeholders de reclutamiento, vacantes, catálogos u otros módulos no especificados.
