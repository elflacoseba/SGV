# Spec Delta: cargo-web-listado-detalle-baja — cargos-filtro-activos-eliminados

## Propósito

Ampliar el listado web de cargos para alternar entre activas y eliminadas, preservar contexto de navegación y permitir reactivación desde la vista eliminada sin mezclar responsabilidades fuera del slice web.

## Requisitos

### REQ-CW-01: Toggle binario Activas/Eliminadas con reset de página
**DADO** la página `Index` de cargos y una búsqueda/orden vigentes; **CUANDO** el usuario cambia entre Activas y Eliminadas; **ENTONCES** la web MUST resetear `p=1`, MUST preservar `search` y `sort`, MUST usar `status` como selector binario y MUST mostrar activas por defecto.

### REQ-CW-02: Vista eliminadas con acciones contextuales de reactivación
**DADO** la vista `status=eliminadas`; **CUANDO** la grilla se renderiza; **ENTONCES** la web MUST ocultar Detalle, Editar, Crear y Eliminar, MUST mostrar `Reactivar` por fila y MUST invocar `PATCH /api/v1/cargos/{id}/reactivar` mediante `?handler=Reactivate`.

### REQ-CW-03: Redirección y feedback de reactivación
**DADO** un POST `?handler=Reactivate`; **CUANDO** el backend responde éxito o falla; **ENTONCES** la web MUST redirigir a la vista Activas en éxito, MUST conservar la vista Eliminadas en falla y MUST reflejar el resultado en el banner con `StatusMessage` y `StatusKind`.

### REQ-CW-04: Preservación de `status` y contexto post-redirect
**DADO** navegación por orden, paginación, búsqueda o POST de Delete/Reactivate; **CUANDO** la página genera links, hidden inputs o `TempData`; **ENTONCES** la web MUST preservar `status`, `search`, `sort` y `p` en esos flujos y MUST mantener `LastDeletedId` para el alert post-redirect.

### REQ-CW-05: Confirmación JavaScript de reactivación
**DADO** una fila visible en la vista Eliminadas; **CUANDO** el usuario inicia reactivación; **ENTONCES** `cargos-index.js` MUST pedir confirmación con SweetAlert2 usando `data-cargo-reactivate-*`, `data-cargo-item-name` y `data-cargo-item-code`.

### REQ-CW-06: CTA rápido para última baja solo en vista activas
**DADO** una baja lógica exitosa desde el listado; **CUANDO** la página vuelve a la vista Activas; **ENTONCES** `LastDeletedId` MUST persistirse en `TempData` para ofrecer un CTA rápido de reactivación en el banner y MUST NOT mostrarse ese CTA cuando la vista actual sea Eliminadas.

## Escenarios

### ESC-CW-01: Cambiar de activas a eliminadas preserva búsqueda y orden
Given un usuario autenticado está en Activas con `search` y `sort` aplicados
When usa el toggle para ir a Eliminadas
Then la navegación envía `status=eliminadas`, preserva `search` y `sort`, y reinicia `p` a `1`

## Source
- `openspec/specs/cargo-web-listado-detalle-baja/spec.md:26-43`
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:18-21,34-39,55-56`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:25-30,128-133,175-183`

## Verification
- Web: `Index_Default_MuestraVistaActivas`
- Web: `Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`

### ESC-CW-02: Vista eliminadas muestra solo reactivación por fila
Given un usuario autenticado abre `Index?status=eliminadas`
When la tabla termina de renderizarse
Then la grilla oculta Detalle, Editar, Crear y Eliminar, y muestra `Reactivar` por cada cargo eliminado

## Source
- `openspec/specs/cargo-web-listado-detalle-baja/spec.md:62-85`
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:18-21,36-38`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:27-30,135-153,175-183`

## Verification
- Web: `Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`
- Web: prueba de render contextual de acciones por fila

### ESC-CW-03: Reactivación exitosa vuelve a activas
Given un cargo eliminado visible en la vista Eliminadas
When el usuario confirma `?handler=Reactivate` y el backend responde éxito
Then la página redirige a Activas y muestra confirmación visible de reactivación

## Source
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:36-39,56-57`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:152-153,177-183,247-252`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md:12-14,26-30`

## Verification
- Web: `Index_PostReactivate_Exito_RedirigeAActivas`

### ESC-CW-04: Reactivación fallida conserva eliminadas y muestra error
Given un cargo eliminado visible en la vista Eliminadas y un conflicto por código activo
When el usuario confirma `?handler=Reactivate` y el backend rechaza la operación
Then la página permanece en Eliminadas y muestra un banner claro y accionable con el error

## Source
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:36-39,61-63`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:153,202-203,247-252`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md:13-14,28-30`

## Verification
- Web: `Index_PostReactivate_Falla_ConservaSegmentoEliminadas`
- Web/API: prueba de banner por conflicto de unicidad activa

### ESC-CW-05: Links, formularios y TempData preservan status
Given un usuario navega, ordena, busca o ejecuta Delete/Reactivate dentro del listado
When la página construye links, hidden inputs y mensajes post-redirect
Then `status` se preserva en orden, paginación, búsqueda, POSTs y alertas junto con `StatusMessage`, `StatusKind` y `LastDeletedId`

## Source
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:18-21,36-39`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:26-30,61,128-148,175-183,247-252`

## Verification
- Web: prueba de preservación de `status` en links y formularios
- Web: `Index_PostDelete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`

### ESC-CW-06: Confirmación JS usa atributos de reactivación
Given una fila eliminada con botón `Reactivar`
When el usuario hace click antes del submit
Then `cargos-index.js` muestra SweetAlert2 usando `data-cargo-item-name` y `data-cargo-item-code` enlazados al selector `data-cargo-reactivate-*`

## Source
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:21,38-39`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:155-160,182-183`

## Verification
- Web/JS: prueba de confirmación de reactivación en `cargos-index.js`

### ESC-CW-07: CTA rápido de reactivación aparece solo en activas
Given un cargo acaba de ser eliminado correctamente desde la vista Activas
When el usuario vuelve al listado mediante PRG
Then el banner conserva `LastDeletedId` para ofrecer reactivación rápida solo en Activas y no expone ese CTA en Eliminadas

## Source
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:36-37`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:61,140-148,247-252`

## Verification
- Web: `Index_PostDelete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`

## No-objetivos

- No habilitar detalle o edición de cargos eliminados.
- No introducir una vista mixta con badges de estado.
- No cambiar el flujo autenticado general ni las páginas fuera de `Index` para este cambio.
