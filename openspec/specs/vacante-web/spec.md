# Especificación de UI Web de Gestión de Vacantes

## Propósito

Agregar el flujo autenticado de gestión de vacantes en `SGV.Web` (páginas Index, Create, Edit, Details), ApiClient tipado e ítem de menú, consumiendo la API REST de `vacante-management`. La creación se realiza exclusivamente desde el módulo de Vacantes, NO desde el detalle de puesto (PB-2).

## Decisiones de negocio asumidas (PB-1, PB-2, PB-4)

| PB | Decisión asumida | Sujeta a confirmación |
|----|-------------------|------------------------|
| PB-1 | Las páginas de mutación (Create, Edit, cambio de estado) requieren rol `Administrador` o `GestorVacantes`. Index/Details solo autenticación. | Sí |
| PB-2 | La creación se inicia desde el módulo de Vacantes. Este change NO agrega botón "Crear Vacante" en el detalle de puesto. | Sí — el slice 2 web puede agregarlo |
| PB-4 | La página Details DEBE mostrar el `HistorialEstadoVacante` de la vacante (cronológico, solo lectura). | Sí |

## Requisitos

### Requisito: Acceso a páginas de vacantes

El sistema DEBE exponer páginas Razor protegidas Index, Create, Edit y Details bajo `/organizacion/vacantes/`. Los usuarios autenticados sin rol `Administrador` ni `GestorVacantes` DEBEN ser redirigidos a `/error/403` en Create y Edit.

#### Escenario: Usuario con rol permitido abre Index

- **DADO** un usuario autenticado
- **CUANDO** navega a `/organizacion/vacantes`
- **ENTONCES** la aplicación DEBE mostrar el listado segmentado dentro del shell autenticado.

#### Escenario: Usuario autenticado sin rol accede a Create

- **DADO** un usuario autenticado sin rol `Administrador` ni `GestorVacantes`
- **CUANDO** solicita `/organizacion/vacantes/crear`
- **ENTONCES** la aplicación DEBE redirigirlo a `/error/403`.

#### Escenario: Usuario anónimo intenta acceder

- **DADO** un usuario no autenticado
- **CUANDO** solicita cualquier página de vacantes
- **ENTONCES** la aplicación DEBE redirigirlo a `/auth/sign-in`.

### Requisito: Listado segmentado en Index

La página Index DEBE consumir `GET /api/v1/vacantes?status={segmento}` y DEBE ofrecer los filtros `abiertas | cerradas | todas`, con `abiertas` como vista por defecto (PB-5).

#### Escenario: Vista por defecto muestra abiertas

- **DADO** vacantes abiertas y cerradas en el backend
- **CUANDO** el usuario abre Index sin filtros
- **ENTONCES** la interfaz DEBE mostrar solo vacantes abiertas.

#### Escenario: Cambio de segmento en la UI

- **DADO** el usuario está en Index
- **CUANDO** selecciona el filtro `cerradas`
- **ENTONCES** la interfaz DEBE recargar solo vacantes cerradas
- **Y** NO DEBE mezclarlas con abiertas.

#### Escenario: Backend no disponible

- **DADO** que la API no responde
- **CUANDO** el usuario abre Index
- **ENTONCES** la interfaz DEBE mostrar un estado recuperable con error visible y acción de reintento.

### Requisito: Formulario de Create con catálogo de estados

El sistema DEBE mostrar en Create los campos `PuestoId`, `EstadoVacanteId`, `FechaApertura`, `Motivo`, `Observaciones`. Los dropdowns de Puesto y Estado DEBEN poblarse desde la API antes de habilitar el guardado.

#### Escenario: Catálogos cargados en Create

- **DADO** que `GET /api/v1/estados-vacante` y los puestos responden
- **CUANDO** el usuario abre Create
- **ENTONCES** la interfaz DEBE mostrar opciones seleccionables de Puesto y Estado.

#### Escenario: Falla la carga de catálogos

- **DADO** que un catálogo falla al cargar
- **CUANDO** el usuario abre Create
- **ENTONCES** la interfaz DEBE mostrar un estado recuperable y bloquear el guardado hasta reintentar.

### Requisito: Guardado con feedback accionable (PRG)

El sistema DEBE aplicar Post-Redirect-Get tras operaciones exitosas y DEBE traducir validaciones, conflictos y fallos de transporte a feedback claro por campo, conservando los datos ingresados.

#### Escenario: Create exitoso

- **DADO** datos válidos para una nueva vacante
- **CUANDO** el usuario confirma y el backend persiste
- **ENTONCES** la shell DEBE redirigir a Details de la nueva vacante
- **Y** DEBE mostrar un mensaje visible de éxito.

#### Escenario: Error de validación por campo

- **DADO** un formulario con datos inválidos
- **CUANDO** el backend responde errores de validación
- **ENTONCES** la interfaz DEBE asociar errores a campos
- **Y** DEBE conservar los datos ingresados.

#### Escenario: Conflicto de PuestoId con vacante abierta existente

- **DADO** un intento de create con `PuestoId` que ya tiene vacante abierta
- **CUANDO** el backend responde conflicto
- **ENTONCES** la interfaz DEBE mostrar mensaje claro sin perder el formulario.

#### Escenario: Mutación web rechazada por rol

- **DADO** un usuario autenticado sin rol permitido envía Create o Edit
- **CUANDO** procesa el handler
- **ENTONCES** este DEBE responder `Forbid()`
- **Y** NO DEBE invocar la mutación contra la API.

### Requisito: Edit permite cambiar estado y observaciones

La página Edit DEBE permitir modificar `Observaciones` y cambiar el `EstadoVacanteId` invocando el cambio de estado del backend, reflejando el seteo automático de `FechaCierre` al transicionar a terminal (PB-3: `Motivo` opcional).

#### Escenario: Edit muestra datos actuales

- **DADO** una vacante existente
- **CUANDO** un usuario autorizado abre Edit
- **ENTONCES** la interfaz DEBE mostrar estado actual y observaciones prellenadas.

#### Escenario: Cambio a estado terminal visible

- **DADO** una vacante abierta en Edit
- **CUANDO** el usuario selecciona estado `Cubierta` y guarda
- **ENTONCES** la interfaz DEBE reflejar `FechaCierre` poblada tras la redirección.

### Requisito: Details con historial de estados (PB-4)

La página Details DEBE mostrar los datos de la vacante y DEBE mostrar el `HistorialEstadoVacante` en orden cronológico, solo lectura.

#### Escenario: Historial visible en Details

- **DADO** una vacante con histórico de cambios
- **CUANDO** el usuario abre Details
- **ENTONCES** la interfaz DEBE listar cada transición con estado anterior, nuevo estado, fecha y motivo.

#### Escenario: Details sin historial

- **DADO** una vacante recién creada sin transiciones
- **CUANDO** se abre Details
- **ENTONCES** la interfaz DEBE mostrar mensaje indicando que no hay historial previo.

### Requisito: Vacante inexistente en páginas de detalle/edición

- **DADO** un identificador que no resuelve una vacante
- **CUANDO** un usuario abre Details o Edit
- **ENTONCES** la interfaz DEBE mostrar un estado recuperable de no disponible
- **Y** DEBE ofrecer camino claro de retorno al listado.

### Requisito: Ítem de menú Vacantes

El sistema DEBE mostrar en `_Sidenav` una entrada `Vacantes` apuntando a `/organizacion/vacantes` visible para usuarios autenticados, con estado `active` en Index, Create, Edit y Details.

#### Escenario: Entrada Vacantes visible

- **DADO** un usuario autenticado navega el shell
- **CUANDO** se renderiza el sidenav
- **ENTONCES** la navegación DEBE mostrar la entrada `Vacantes`
- **Y** DEBE enlazar a `/organizacion/vacantes`.

#### Escenario: Estado active en páginas de vacantes

- **DADO** un usuario ubicado en una página del módulo Vacantes
- **CUANDO** se renderiza el sidenav
- **ENTONCES** la entrada `Vacantes` DEBE reflejar estado `active`.