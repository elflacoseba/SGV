# Delta para `usuario-web-selector-persona-buscador`

> Change: `2026-07-29-web-ocupaciones-buscador-personas-issue-216` (issue #216). Capacidad MODIFICADA. Idioma: español neutro. Código/identificadores: inglés. Spec canónica vigente: `openspec/specs/usuario-web-selector-persona-buscador/spec.md`.

## ADDED Requirements

### Requirement: REQ-USB-12 Configuración del modal via `data-solo-sin-usuario`

El modal reutilizable `_PersonaBuscadorModal.cshtml` y el script `usuario-persona-buscador.js` MUST soportar el atributo `data-solo-sin-usuario` en el contenedor raíz del modal. Cuando el atributo está presente, el JS MUST leerlo (parseo case-insensitive: `"true"|"false"` → booleano) y conditionalizar `url.searchParams.set("soloSinUsuario", valor)` en cada invocación a `GET /api/v1/personas/consulta`. Cuando el atributo está AUSENTE o no es parseable, el JS MUST defaultear a `true` (preserva el comportamiento vigente de Usuarios). El script MUST NO hardcodear `soloSinUsuario=true` sin lectura del atributo.

#### Scenario: Modal Usuarios sin atributo mantiene `soloSinUsuario=true`

- **DADO** el modal de `/seguridad/usuarios/crear` sin `data-solo-sin-usuario` (o valor inválido)
- **CUANDO** el Administrador dispara una búsqueda con texto `garcia`
- **ENTONCES** MUST construirse `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=true&p=1&pageSize=25`
- **Y** el listado MUST excluir personas con usuario activo asociado.

#### Scenario: Modal Ocupaciones con `data-solo-sin-usuario="false"` omite el filtro

- **DADO** el modal de Ocupaciones con `data-solo-sin-usuario="false"` en el raíz
- **CUANDO** se dispara una búsqueda con texto `garcia`
- **ENTONCES** MUST construirse `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=false&p=1&pageSize=25`
- **Y** el listado MAY incluir personas con usuario activo asociado.

#### Scenario: Atributo `data-solo-sin-usuario` con casing/value variants normaliza

- **DADO** un modal con `data-solo-sin-usuario="False"` (mayúsculas)
- **CUANDO** el JS parsea el atributo vía comparación case-insensitive contra `"true"`
- **ENTONCES** MUST interpretarse como `false`
- **Y** MUST construirse la URL con `soloSinUsuario=false`.

#### Scenario: Script backwards-compatible sin cambios de comportamiento en Usuarios

- **DADO** la suite de tests existente de `usuario-web-selector-persona-buscador` (REQ-USB-03, REQ-USB-10)
- **CUANDO** se ejecutan sin modificar markup de Usuarios
- **ENTONCES** los resultados observados MUST ser idénticos a los previos al change
- **Y** MUST NO requierirse cambios al markup de `_PersonaBuscadorModal.cshtml` para preservar el comportamiento de Usuarios.

## MODIFIED Requirements

### Requirement: REQ-USB-03 Modal Bootstrap 5 con búsqueda lazy

Al pulsar el disparador, el sistema MUST abrir el modal `#usuario-persona-buscador-modal` con foco en el input de búsqueda, placeholder `Buscar por legajo, nombre, apellido, email o documento`, y la búsqueda se dispara al pulsar `Enter` o el botón `Buscar` (sin recarga) sobre los campos `Legajo|Apellidos|Nombres|Email|NumeroDocumento` en forma case-insensitive por subcadena. El JS MUST conditionalizar el parámetro `soloSinUsuario` del `GET /api/v1/personas/consulta` según el atributo `data-solo-sin-usuario` del modal raíz (REQ-USB-12); cuando el atributo está ausente o inválido, defaultea a `true`.

(Previously: el requisito hardcodeaba `soloSinUsuario=true` sin lectura de atributo, asumiendo uso exclusivo desde Crear/Editar Usuario.)

#### Scenario: Apertura enfoca el input y renderiza placeholder

- **DADO** el selector en estado vacío y foco en el disparador
- **CUANDO** se hace click en `Buscar Persona`
- **ENTONCES** MUST abrirse el modal con `aria-hidden="false"`
- **Y** el foco inicial MUST estar en el input de búsqueda
- **Y** el placeholder visible MUST ser exactamente `Buscar por legajo, nombre, apellido, email o documento`.

#### Scenario: Búsqueda al pulsar Enter desde Usuarios envía `soloSinUsuario=true`

- **DADO** el modal de Usuarios abierto con texto `garcia`
- **CUANDO** el `Administrador` pulsa `Enter`
- **ENTONCES** MUST dispararse un único `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=true&p=1&pageSize=25`
- **Y** el input MUST mantener el texto `garcia` durante el request.

#### Scenario: Búsqueda desde modal con `data-solo-sin-usuario="false"` omite el parámetro

- **DADO** un modal reutilizado con `data-solo-sin-usuario="false"` (e.g., Ocupaciones) abierto con texto `garcia`
- **CUANDO** el Administrador pulsa `Enter`
- **ENTONCES** MUST dispararse `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=false&p=1&pageSize=25`
- **Y** el input MUST mantener el texto `garcia` durante el request.

### Requirement: REQ-USB-10 Listado exclusivo de activas sin usuario

El modal, cuando se invoca desde el formulario Crear/Editar Usuario (default `soloSinUsuario=true` o atributo ausente), MUST listar exclusivamente personas activas (`IsActive=true` y `IsDeleted=false`) que NO tengan un usuario activo asociado (`AspNetUsers.PersonaId IS NULL`), independientemente de la versión client-side del catálogo `IPersonaOptionsProvider.GetActivasAsync()`. Cuando el modal se reutiliza desde otros contextos con `data-solo-sin-usuario="false"` (e.g., Ocupaciones), el filtro `soloSinUsuario` MUST NO aplicarse y el listado MAY incluir personas con usuario activo asociado, quedando la decisión de exclusión fuera del scope de este requisito (ver `ocupacion-web-selector-persona-buscador`).

(Previously: el requisito exigía `soloSinUsuario=true` unconditionalmente para todo uso del modal, sin contemplar reutilización desde contextos donde una persona puede tener ocupaciones múltiples.)

#### Scenario: Solo activas sin usuario en `/consulta` desde Usuarios

- **DADO** una persona activa sin usuario
- **Y** una persona activa con usuario activo
- **Y** una persona eliminada sin usuario
- **CUANDO** el modal de Usuarios invoca `/consulta?soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener solo la persona activa sin usuario
- **Y** MUST NOT contener personas con usuario ni personas eliminadas.

#### Scenario: Modal reutilizado con `soloSinUsuario=false` no filtra por usuario

- **DADO** una persona activa con usuario activo asociado
- **Y** una persona activa sin usuario
- **CUANDO** un modal reutilizado con `data-solo-sin-usuario="false"` invoca `/consulta?soloSinUsuario=false&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener AMBAS personas activas
- **AND** MUST seguir excluyendo personas eliminadas (filtro `Segmento=Activas` aplica ortogonalmente).

## Consideraciones fuera de alcance

- Reutilización del modal desde otros módulos distintos a Ocupaciones y Usuarios (sigue fuera del scope, solo se habilita el mecanismo de configuración).
- Cambios al markup del modal `_PersonaBuscadorModal.cshtml`: el atributo `data-solo-sin-usuario` se agrega al invocar desde Ocupaciones, no en el partial compartido.
- Validación de `data-solo-sin-usuario` con valores no booleanos: el JS ignorará y defaulteará a `true`.