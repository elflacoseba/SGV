# Especificación de listado, detalle y baja web de cargos

## Purpose

Definir el primer slice autenticado de `Cargos` en `SGV.Web` para consultar cargos activos, ver su detalle readonly y ejecutar baja lógica sin expandirse a create, edit, skills, eliminados o reactivación.

## Requirements

### Requirement: Acceso autenticado al módulo de cargos

El sistema MUST exponer páginas Razor protegidas para listado y detalle de `Cargos` dentro del shell autenticado.

#### Scenario: Usuario autenticado abre el módulo

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega al módulo `Cargos`
- THEN la aplicación MUST responder con el listado dentro del shell autenticado
- AND la vista MUST mostrar el título del módulo.

#### Scenario: Usuario anónimo intenta acceder

- GIVEN un usuario no autenticado
- WHEN solicita la URL del listado o del detalle de cargos
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`.

### Requirement: Listado visible de cargos activos

El sistema MUST renderizar una tabla de cargos activos usando el patrón visual del shell, MUST consultar exclusivamente el contrato existente de lectura activa y MUST exponer por fila solo acciones de detalle y baja lógica. La interfaz MUST NOT mostrar create, edit, skills, eliminados ni reactivación en este slice.

#### Scenario: Carga inicial del listado

- GIVEN un usuario autenticado abre `Cargos`
- WHEN la página termina de cargar
- THEN la tabla MUST mostrar cargos activos devueltos por el backend
- AND cada fila MUST ofrecer acciones visibles de detalle y baja.

#### Scenario: Listado sin resultados activos

- GIVEN que la consulta de cargos activos no devuelve filas
- WHEN el usuario abre el listado
- THEN la interfaz MUST mostrar un estado vacío entendible
- AND MUST seguir sin exponer acciones fuera del alcance definido.

### Requirement: Detalle readonly con retorno seguro

El sistema MUST mostrar en detalle los datos legibles del cargo en modo solo lectura, MUST ofrecer una acción visible para volver al listado y MUST mostrar un estado recuperable cuando el cargo solicitado no pueda consultarse.

#### Scenario: Apertura de detalle existente

- GIVEN un cargo activo existente
- WHEN el usuario abre su detalle desde el listado
- THEN la página MUST mostrar sus datos en modo solo lectura
- AND MUST ofrecer una acción visible para volver al listado.

#### Scenario: Cargo no disponible en detalle

- GIVEN un identificador de cargo que ya no puede consultarse como activo
- WHEN el usuario abre la pantalla de detalle
- THEN la interfaz MUST mostrar un mensaje visible de no disponible o error recuperable
- AND MUST ofrecer un camino claro para volver al listado.

### Requirement: Baja lógica confirmada con feedback de conflicto

El sistema MUST solicitar confirmación antes de ejecutar la baja lógica, MUST remover el cargo del listado activo cuando la operación sea exitosa y MUST traducir rechazos por conflicto a feedback claro y accionable.

#### Scenario: Usuario cancela la confirmación

- GIVEN una fila con acción de baja visible
- WHEN el usuario inicia la baja y cancela la confirmación
- THEN la aplicación MUST NOT ejecutar la eliminación
- AND la fila MUST permanecer visible en el listado.

#### Scenario: Baja lógica exitosa

- GIVEN un cargo activo eliminable visible en la tabla
- WHEN el usuario confirma la baja y el backend responde éxito
- THEN la interfaz MUST volver al listado activo con confirmación visible
- AND el cargo eliminado MUST dejar de verse.

#### Scenario: Baja rechazada por conflicto

- GIVEN un cargo activo cuya baja es rechazada por dependencias
- WHEN el usuario confirma la baja
- THEN la interfaz MUST mostrar un mensaje claro que indique el conflicto
- AND el cargo MUST permanecer visible para el usuario.

### Requirement: REQ-CW-01 Toggle binario Activas/Eliminadas con reset de página

La página `Index` de cargos MUST permitir alternar entre Activas y Eliminadas usando `status` como selector binario. Al cambiar de segmento, la UI MUST resetear `p=1`, MUST preservar `search` y `sort`, y MUST mostrar activas por defecto.

#### Scenario: Cambiar de activas a eliminadas preserva búsqueda y orden

- GIVEN un usuario autenticado está en Activas con `search` y `sort` aplicados
- WHEN usa el toggle para ir a Eliminadas
- THEN la navegación MUST enviar `status=eliminadas`
- AND MUST preservar `search` y `sort`
- AND MUST reiniciar `p` a `1`.

#### Source

- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/specs/cargo-web-listado-detalle-baja/spec.md:9-18,29-42`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/proposal.md:18-21,34-39,55-56`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/exploration.md:25-30,128-133,175-183`

#### Verification

- Web: `Get_Index_Default_MuestraVistaActivas`
- Web: `Get_Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`
- Web: `Get_Index_SinStatus_CaeA_Activas`

### Requirement: REQ-CW-02 Vista eliminadas con acciones contextuales de reactivación

La vista `status=eliminadas` MUST ocultar detalle, edición, creación y eliminación, y MUST mostrar solo acciones contextuales de reactivación por fila.

#### Scenario: Vista eliminadas muestra solo reactivación por fila

- GIVEN un usuario autenticado abre `Index?status=eliminadas`
- WHEN la tabla termina de renderizarse
- THEN la grilla MUST ocultar Detalle, Editar, Crear y Eliminar
- AND MUST mostrar `Reactivar` por cada cargo eliminado.

#### Source

- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/specs/cargo-web-listado-detalle-baja/spec.md:12-14,43-56`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/proposal.md:18-21,36-38`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/exploration.md:27-30,135-153,175-183`

#### Verification

- Web: `Get_Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`
- Web: cobertura funcional del render contextual en `CargoIndexPageTests`

### Requirement: REQ-CW-03 Redirección y feedback de reactivación

El flujo `?handler=Reactivate` MUST redirigir a Activas cuando la reactivación es exitosa y MUST conservar Eliminadas con feedback visible cuando la operación falla.

#### Scenario: Reactivación exitosa vuelve a activas

- GIVEN un cargo eliminado visible en la vista Eliminadas
- WHEN el usuario confirma `?handler=Reactivate` y el backend responde éxito
- THEN la página MUST redirigir a Activas
- AND MUST mostrar una confirmación visible de reactivación.

#### Scenario: Reactivación fallida conserva eliminadas y muestra error

- GIVEN un cargo eliminado visible en la vista Eliminadas y un conflicto por código activo
- WHEN el usuario confirma `?handler=Reactivate` y el backend rechaza la operación
- THEN la página MUST permanecer en Eliminadas
- AND MUST mostrar un banner claro y accionable con el error.

#### Source

- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/specs/cargo-web-listado-detalle-baja/spec.md:15-17,57-83`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/proposal.md:36-39,56-57,61-63`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/exploration.md:152-153,177-183,202-203,247-252`

#### Verification

- Web: `Post_Reactivate_Exito_RedirigeAActivas`
- Web: `Post_Reactivate_Falla_ConservaSegmentoEliminadas`

### Requirement: REQ-CW-04 Preservación de `status` y contexto post-redirect

La página MUST preservar `status`, `search`, `sort` y `p` en links, formularios, redirects y `TempData` asociados al listado de cargos.

#### Scenario: Links, formularios y TempData preservan status

- GIVEN un usuario navega, ordena, busca o ejecuta Delete/Reactivate dentro del listado
- WHEN la página construye links, hidden inputs y mensajes post-redirect
- THEN `status` MUST preservarse en orden, paginación, búsqueda, POSTs y alertas
- AND MUST mantenerse junto con `StatusMessage`, `StatusKind` y `LastDeletedId`.

#### Source

- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/specs/cargo-web-listado-detalle-baja/spec.md:18-20,84-96`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/proposal.md:18-21,36-39`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/exploration.md:26-30,61,128-148,175-183,247-252`

#### Verification

- Web: `Post_Delete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`
- Web: `Post_Reactivate_Exito_RedirigeAActivas`
- Web: `Post_Reactivate_Falla_ConservaSegmentoEliminadas`

### Requirement: REQ-CW-05 Confirmación JavaScript de reactivación

`cargos-index.js` MUST confirmar la reactivación con SweetAlert2 usando atributos `data-cargo-reactivate-*`, `data-cargo-item-name` y `data-cargo-item-code` antes de enviar el formulario.

#### Scenario: Confirmación JS usa atributos de reactivación

- GIVEN una fila eliminada con botón `Reactivar`
- WHEN el usuario hace click antes del submit
- THEN `cargos-index.js` MUST mostrar SweetAlert2 usando `data-cargo-item-name` y `data-cargo-item-code`
- AND MUST enlazar el flujo al selector `data-cargo-reactivate-*`.

#### Source

- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/specs/cargo-web-listado-detalle-baja/spec.md:21-23,97-108`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/proposal.md:21,38-39`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/exploration.md:155-160,182-183`

#### Verification

- Web/JS: `ReactivateConfirmationScript_WhenCancelled_DoesNotSubmitForm`
- Web/JS: `ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce`

### Requirement: REQ-CW-06 CTA rápido para última baja solo en vista activas

`LastDeletedId` MUST persistirse en `TempData` tras una baja lógica exitosa para ofrecer un CTA rápido de reactivación en Activas, y ese CTA MUST NOT mostrarse cuando la vista actual sea Eliminadas.

#### Scenario: CTA rápido de reactivación aparece solo en activas

- GIVEN un cargo acaba de ser eliminado correctamente desde la vista Activas
- WHEN el usuario vuelve al listado mediante PRG
- THEN el banner MUST conservar `LastDeletedId` para ofrecer reactivación rápida solo en Activas
- AND MUST NOT exponer ese CTA en Eliminadas.

#### Source

- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/specs/cargo-web-listado-detalle-baja/spec.md:24-26,109-120`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/proposal.md:36-37`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/exploration.md:61,140-148,247-252`

#### Verification

- Web: `Post_Delete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`
- Web: `Post_Delete_CuandoSegmentoEsEliminadas_NoMuestraCtaReactivar`
- Web: `Post_Reactivate_Exito_LimpiaLastDeletedId_BannerDesaparece`
