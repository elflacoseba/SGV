# Especificación de la página web editable de habilidades por cargo

## Purpose

Definir el comportamiento observable de la nueva Razor Page `Pages/Organizacion/Cargos/Habilidades.cshtml` para que un administrador gestione habilidades requeridas por cargo desde `SGV.Web` con una tabla editable, feedback claro y patrón PRG.

## Requirements

### Requirement 1: Acceso restringido a administradores

La página y sus handlers de escritura MUST requerir autenticación y rol `Administrador`.

#### Scenario: Usuario sin rol `Administrador`

- GIVEN un usuario autenticado sin rol `Administrador`
- WHEN intenta abrir la página o ejecutar sus handlers de guardado o baja
- THEN la aplicación MUST impedir el acceso
- AND MUST NOT exponer acciones de escritura utilizables.

#### Scenario: Usuario anónimo

- GIVEN un usuario no autenticado
- WHEN solicita la página de habilidades del cargo
- THEN `SGV.Web` MUST redirigirlo al flujo de sign-in.

### Requirement 2: Carga inicial e hidratación de la tabla

La página MUST consultar el backend al abrirse y MUST hidratar la tabla con todas las habilidades actualmente asociadas al cargo.

#### Scenario: Carga inicial exitosa

- GIVEN un `Cargo` existente con habilidades asociadas
- WHEN un `Administrador` abre `Habilidades.cshtml`
- THEN la página MUST mostrar una fila por cada asociación devuelta por `GET /api/v1/cargos/{cargoId}/skills`
- AND MUST incluir los valores actuales de `Habilidad`, `NivelRequerido`, `Ponderacion` y `EsObligatoria`.

#### Scenario: Cargo sin habilidades

- GIVEN un `Cargo` existente sin asociaciones
- WHEN la página carga correctamente
- THEN la tabla MUST mostrar un estado vacío legible
- AND MUST seguir ofreciendo la acción para asignar una nueva habilidad.

### Requirement 3: Renderizado editable del vínculo

La interfaz MUST renderizar una grilla editable con columnas `Habilidad`, `NivelRequerido`, `Ponderacion`, `Obligatoria` y acciones de `Quitar`.

#### Scenario: Render de columnas

- GIVEN una carga inicial exitosa
- WHEN se renderiza la tabla
- THEN la UI MUST mostrar esas cinco columnas visibles
- AND MUST permitir editar `NivelRequerido`, `Ponderacion` y `Obligatoria` sobre una asociación existente.

#### Scenario: Asignar una nueva habilidad

- GIVEN un `Cargo` existente y catálogos disponibles
- WHEN el `Administrador` completa el formulario inline o modal y confirma guardar
- THEN la página MUST ejecutar el `PUT` del subrecurso
- AND MUST reflejar la nueva fila mediante PRG con mensaje visible de éxito.

#### Scenario: Editar una habilidad existente

- GIVEN una fila existente en la tabla
- WHEN el `Administrador` cambia `NivelRequerido`, `Ponderacion` o `Obligatoria` y guarda
- THEN la página MUST persistir los cambios contra el backend
- AND MUST volver a cargar la tabla mostrando los nuevos valores.

### Requirement 4: Baja con confirmación y feedback

La interfaz MUST confirmar la baja antes de quitar una asociación y MUST usar `TempData` para comunicar el resultado después del redirect.

#### Scenario: Quitar una habilidad

- GIVEN una asociación visible en la tabla
- WHEN el `Administrador` confirma la acción `Quitar`
- THEN la página MUST ejecutar `DELETE` sobre el subrecurso
- AND MUST volver por PRG con mensaje de éxito o error recuperable.

### Requirement 5: Manejo de errores recuperables

La página MUST traducir errores `4xx/5xx` o fallas de transporte a mensajes legibles y MUST NOT mostrar stack traces al usuario final.

#### Scenario: Error del backend al cargar o guardar

- GIVEN que la API responde `4xx`, `5xx` o falla el transporte
- WHEN la página intenta cargar o persistir cambios
- THEN la UI MUST mostrar un mensaje accionable y comprensible
- AND MUST mantener ocultos detalles internos de excepción o stack trace.
