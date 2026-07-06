# cargo-skill-ui-tabla-editable — delta para cargos-navegacion-habilidades

## Propósito
Consolidar la spec completa de la capacidad `cargo-skill-ui-tabla-editable`, incorporando el cierre de W-UX mediante entry points desde `Cargos` y endureciendo el feedback de validación por fila para la edición inline sin cambiar backend, contrato HTTP ni el patrón Razor/PRG vigente.

## Cambios respecto a la versión canónica
- **Requirement 3 (MODIFICADO)** — explicita feedback de validación por fila para `Actualizar`, con errores anclados al campo correcto y duplicados en el `validation-summary` general.
- **Requirement 6 (NUEVO)** — agrega descubribilidad desde el listado activo de `Cargos`.
- **Requirement 7 (NUEVO)** — agrega descubribilidad desde el detalle de `Cargos`.

## Requirements

### Requirement 1 (sin cambios) — Acceso restringido a administradores

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

### Requirement 2 (sin cambios) — Carga inicial e hidratación de la tabla

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

### Requirement 3 (MODIFICADO) — Renderizado editable del vínculo y feedback de validación por fila

La interfaz MUST renderizar una grilla editable con columnas `Habilidad`, `NivelRequerido`, `Ponderacion`, `Obligatoria` y acciones de `Quitar`. Cuando `OnPostActualizarAsync` recibe `FieldErrors` del backend para una fila específica, el PageModel MUST traducir cada key a la convención `Actualizar[{skillId}].Campo`, donde `Campo ∈ {NivelRequeridoId, Ponderacion, EsObligatoria}`. El markup de la fila MUST renderizar un contenedor de error anclado al input, select o checkbox que falló usando esa misma key. El `validation-summary` general arriba de la página MUST seguir presente y MUST contener los mismos errores. El comportamiento de éxito mediante PRG con `TempData` y el uso de `return Page()` para fallos recuperables MUST preservarse. (Previously: la spec solo exigía grilla editable, persistencia de cambios y recarga con los nuevos valores, sin fijar la convención de errores por fila.)

#### Scenario: Render de columnas

- GIVEN una carga inicial exitosa
- WHEN se renderiza la tabla
- THEN la UI MUST mostrar esas cinco columnas visibles
- AND MUST permitir editar `NivelRequerido`, `Ponderacion` y `Obligatoria` sobre una asociación existente.

#### Scenario: Asignar una nueva habilidad

- GIVEN un `Cargo` existente y catálogos disponibles
- WHEN el `Administrador` completa el formulario inline y confirma guardar
- THEN la página MUST ejecutar el `PUT` del subrecurso
- AND MUST reflejar la nueva fila mediante PRG con mensaje visible de éxito.

#### Scenario: Error de validación anclado a la fila correcta

- GIVEN que `OnPostActualizarAsync` recibe `FieldErrors = { "Ponderacion": ["Fuera de rango"] }` para la fila con `SkillId = X`
- WHEN el POST devuelve `400` y la página se re-renderiza con `return Page()`
- THEN el mensaje MUST aparecer junto al input `Ponderacion` de la fila `X`
- AND MUST aparecer también en el `validation-summary` general.

#### Scenario: Éxito de edición preserva el flujo editable

- GIVEN una fila existente en la tabla
- WHEN el `Administrador` cambia `NivelRequerido`, `Ponderacion` o `Obligatoria` y el backend responde éxito
- THEN la página MUST persistir los cambios contra el backend mediante PRG con `TempData`
- AND MUST volver a cargar la grilla manteniéndola editable y mostrando los nuevos valores.

#### Scenario: Error defensivo fuera de la fila activa

- GIVEN que `OnPostActualizarAsync` recibe `FieldErrors` que no aplican a la fila activa
- WHEN la página se re-renderiza tras el fallo recuperable
- THEN el error MUST seguir visible en el `validation-summary` general
- AND MUST NOT perderse aunque no pueda anclarse a un input de esa fila.

### Requirement 4 (sin cambios) — Baja con confirmación y feedback

La interfaz MUST confirmar la baja antes de quitar una asociación y MUST usar `TempData` para comunicar el resultado después del redirect.

#### Scenario: Quitar una habilidad

- GIVEN una asociación visible en la tabla
- WHEN el `Administrador` confirma la acción `Quitar`
- THEN la página MUST ejecutar `DELETE` sobre el subrecurso
- AND MUST volver por PRG con mensaje de éxito o error recuperable.

### Requirement 5 (sin cambios) — Manejo de errores recuperables

La página MUST traducir errores `4xx/5xx` o fallas de transporte a mensajes legibles y MUST NOT mostrar stack traces al usuario final.

#### Scenario: Error del backend al cargar o guardar

- GIVEN que la API responde `4xx`, `5xx` o falla el transporte
- WHEN la página intenta cargar o persistir cambios
- THEN la UI MUST mostrar un mensaje accionable y comprensible
- AND MUST mantener ocultos detalles internos de excepción o stack trace.

### Requirement 6 (NUEVO) — Descubribilidad desde el listado de Cargos

La columna **Acciones** del listado de `Cargos` en `Index.cshtml`, vista activa, MUST exponer un enlace hacia `/organizacion/cargos/{id:guid}/habilidades` con `aria-label="Gestionar habilidades de {Nombre}"`, `data-bs-toggle="tooltip"`, `data-bs-title="Habilidades"` e ícono `ti ti-stars`. El enlace MUST ser visible solo cuando `!Model.IsDeletedView`.

#### Scenario: Fila activa expone enlace a habilidades

- GIVEN un `Administrador` en la vista activa del listado de `Cargos`
- WHEN se renderiza cada fila activa
- THEN la columna **Acciones** MUST contener el enlace a `Habilidades`
- AND el `href` MUST apuntar al `id` correcto del cargo.

#### Scenario: Vista eliminadas no expone enlace a habilidades

- GIVEN un `Administrador` en la vista `eliminadas` del listado de `Cargos`
- WHEN se renderizan las filas del segmento eliminado
- THEN ninguna fila MUST contener el enlace a `Habilidades`.

### Requirement 7 (NUEVO) — Descubribilidad desde el detalle de Cargos

La barra inferior de `Details.cshtml` MUST exponer un botón textual con texto `Habilidades`, ícono `ti ti-stars me-1` y `href` hacia `/organizacion/cargos/{id:guid}/habilidades`, ubicado entre `Editar` y `Volver al listado`. Cuando `IsNotFound == true`, el botón MUST NOT renderizarse.

#### Scenario: Detalle existente muestra botón de habilidades

- GIVEN un `Administrador` en el detalle de un cargo existente
- WHEN la página se renderiza
- THEN la barra inferior MUST contener el botón `Habilidades`
- AND el `href` MUST apuntar al `id` del cargo mostrado.

#### Scenario: Detalle inexistente no muestra botón

- GIVEN que `IsNotFound == true`
- WHEN la página de detalle se renderiza
- THEN el botón `Habilidades` MUST NOT renderizarse.

## Result Contract
- **status**: success
- **executive_summary**: La delta spec actualiza `cargo-skill-ui-tabla-editable` con dos entry points explícitos desde `Cargos` y endurece el feedback por fila en `Actualizar`, preservando la tabla editable, el contrato HTTP vigente y el patrón PRG/`return Page()`.
- **artifacts**: ["openspec/changes/cargos-navegacion-habilidades/specs/cargo-skill-ui-tabla-editable/spec.md"]
- **next_recommended**: design
- **risks**: ["Drift entre keys `Actualizar[{skillId}].Campo` y los nombres reales de inputs o contenedores de validación.", "Scope creep hacia `Edit.cshtml` o navegación cruzada desde catálogo de `Habilidades`.", "Intentar redirigir también en error rompería la preservación de `ModelState` para feedback por fila."]
- **skill_resolution**: paths-injected
