# Especificación de alta y edición web de usuarios

## Purpose

Definir el slice de `Usuarios` en `SGV.Web` para que un `Administrador` cree nuevas cuentas asociadas a `Persona` activa, edite `UserName`/`Email` y roles de cuentas existentes, y reciba feedback claro ante conflictos — sin expandirse a cambio de contraseña, lockout/unlock, baja, reactivación (cubierto por `usuario-web-listado-detalle-baja`) ni CRUD de roles.

## Requirements

### Requirement: REQ-UCE-01 Acceso restringido a Administrador

`Crear` y `Editar` MUST exigir rol `Administrador`. GET MUST redirigir a `/error/403` cuando el usuario autenticado no tiene el rol; POST MUST responder `Forbid()` sin invocar la API.

#### Scenario: GET sin rol admin redirige a 403

- **DADO** un autenticado sin rol `Administrador`
- **CUANDO** solicita `/seguridad/usuarios/crear` o `/seguridad/usuarios/editar/{id}`
- **ENTONCES** MUST redirigir a `/error/403`.

#### Scenario: POST sin rol admin responde Forbid

- **DADO** un autenticado sin rol `Administrador`
- **CUANDO** envía un POST de alta o edición
- **ENTONCES** MUST responder `Forbid()`
- **Y** MUST NOT invocar la mutación contra la API.

### Requirement: REQ-UCE-02 Formulario Crear prellenado con Personas activas

`OnGetAsync` de crear MUST cargar el catálogo de personas activas y MUST construir un dropdown cuyo valor sea el `PersonaId`. Cuando el catálogo esté vacío, el formulario MUST bloquear el render del campo o guiar al usuario con un mensaje accionable y un link al alta de Persona.

#### Scenario: Dropdown poblado por defecto

- **DADO** personas activas disponibles
- **CUANDO** un `Administrador` abre `/seguridad/usuarios/crear`
- **ENTONCES** MUST mostrar un dropdown poblado con `PersonaId`+`Nombres`+`Apellidos`
- **Y** el formulario MUST permitir seleccionar una `Persona`.

#### Scenario: Dropdown vacío bloquea o guía

- **DADO** sin personas activas disponibles
- **CUANDO** un `Administrador` abre `/seguridad/usuarios/crear`
- **ENTONCES** MUST mostrar mensaje visible de alta de Persona primero (o MUST bloquear el submit)
- **Y** MUST ofrecer un camino claro (link o instrucción) hacia `/personas/crear`.

### Requirement: REQ-UCE-03 Validación del formulario Crear

El formulario MUST exigir `UserName` único no vacío, `Email` con formato válido, `Password` que cumpla la política de Identity, al menos un rol del catálogo fijo y un `PersonaId` válido existente. Errores MUST mapearse a campos visibles (`Input.*`) preservando el resto del formulario.

#### Scenario: Validación de unicidad y formato

- **DADO** un `Administrador` completando el alta
- **CUANDO** envía datos con `UserName` duplicado o `Email` inválido
- **ENTONCES** MUST mostrar el error en el campo correspondiente sin perder `PersonaId`, `Password` ni selección de roles.

#### Scenario: Rechazo por Persona inexistente

- **DADO** un `PersonaId` que no existe o ya está inactivo
- **CUANDO** un `Administrador` envía el alta
- **ENTONCES** MUST rechazar la operación con feedback claro
- **Y** NO MUST crear el usuario.

### Requirement: REQ-UCE-04 PRG al detalle tras creación exitosa

Tras `201` en `POST /api/v1/usuarios`, MUST redirigir al detalle del nuevo usuario con feedback success, preservando los filtros del listado.

#### Scenario: Alta exitosa con PRG

- **DADO** datos válidos
- **CUANDO** un `Administrador` envía el alta y el backend responde `201`
- **ENTONCES** MUST redirigir al detalle del nuevo usuario con mensaje visible de éxito.

### Requirement: REQ-UCE-05 Formulario Edit prellenado con datos existentes

`OnGetAsync` de editar MUST recuperar `UsuarioDto` por `id` y prellenar `UserName`, `Email`, `Persona` (read-only) y roles. Persona no consultable MUST mostrar estado recuperable con retorno claro al listado.

#### Scenario: Edit prellena datos

- **DADO** un usuario activo existente
- **CUANDO** un `Administrador` abre `/seguridad/usuarios/editar/{id}`
- **ENTONCES** MUST mostrar los valores actuales y permitir modificar `UserName`, `Email` y roles.

#### Scenario: Edit para usuario no consultable

- **DADO** un identificador de usuario no consultable
- **CUANDO** un `Administrador` abre el editor
- **ENTONCES** MUST mostrar estado recuperable y MUST ofrecer retorno al listado.

### Requirement: REQ-UCE-06 Edición de UserName/Email/roles con PRG

`PUT /api/v1/usuarios/{id}` MUST aplicar cambios de `UserName`, `Email` y roles en una sola operación. Tras `200`, MUST re-redirigir al propio editor con feedback success preservando filtros. `400`/`409`/`403` MUST mapearse a feedback de campo sin perder el resto del formulario.

#### Scenario: Edit exitoso con PRG

- **DADO** un usuario activo existente
- **CUANDO** un `Administrador` guarda cambios y la API responde `200`
- **ENTONCES** MUST re-redirigir al propio editor con feedback success y filtros preservados.

#### Scenario: Conflicto por UserName duplicado

- **DADO** otro usuario activo con el mismo `UserName`
- **CUANDO** un `Administrador` guarda el cambio
- **ENTONCES** MUST mostrar el error en `Input.UserName` sin perder `Email` ni roles.

#### Scenario: Concurrencia con otro Administrador

- **DADO** dos `Administradores` editando el mismo usuario en paralelo
- **CUANDO** ambos guardan cambios casi simultáneamente
- **ENTONCES** la última escritura persistida MUST ser coherente con la API
- **Y** MUST mostrarse feedback claro cuando la edición queda invalidada por otro cambio.

### Requirement: REQ-UCE-07 Catálogo fijo de roles seleccionable

El editor MUST exponer el catálogo de roles SGV (`Administrador`, `GestorVacantes`, `Consultor`) como opciones seleccionables. Roles no presentes en el catálogo MUST NOT aparecer ni ser persistibles.

#### Scenario: Roles fijos seleccionables

- **DADO** un `Administrador` abriendo el editor
- **CUANDO** observa las opciones de roles
- **ENTONCES** MUST ver exclusivamente `Administrador`, `GestorVacantes` y `Consultor` como seleccionables.

#### Scenario: Cambio de roles preserva el resto del formulario

- **DADO** un `Administrador` editando un usuario
- **CUANDO** guarda cambios solo sobre roles
- **ENTONCES** MUST persistir los nuevos roles
- **Y** MUST preservar `UserName` y `Email` sin alterarlos.

## Out of scope

- No incluye baja lógica ni reactivación — cubierto por `usuario-web-listado-detalle-baja`.
- No incluye cambio de contraseña desde la UI administrativa.
- No incluye CRUD de roles ni bloqueos de cuenta.
