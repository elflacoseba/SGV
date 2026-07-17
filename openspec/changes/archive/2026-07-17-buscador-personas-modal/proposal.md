# Proposal: Buscador modal reutilizable de Personas en Crear/Editar Usuario

## Contexto

`_Form.cshtml` (líneas 11-28) usa un combo plano (`<select asp-for="Input.PersonaId">`) cargado por `IPersonaOptionsProvider.GetActivasAsync() → IPersonaApiClient.GetAllAsync() → GET /api/v1/personas` sin paginar. Inusable con set grande. `SgvIdentityUserConfiguracion.cs:26-34` confirma `IX_AspNetUsers_PersonaId` UNIQUE con FK `Restrict`; el selector debe filtrar personas sin usuario asociado para evitar `409` por condición de carrera.

## Cambio propuesto

Selector con buscador modal Bootstrap 5, paginado server-side vía `GET /api/v1/personas/consulta` con `soloSinUsuario=true`. En Edición la persona vinculada se preserva y se omite del modal. `409` → feedback de campo análogo al patrón `Codigo` duplicado de Cargos.

## Alcance

**Incluido.** Partial `_PersonaBuscadorModal.cshtml` co-localizado en `Pages/Seguridad/Usuarios/`, contrato `ViewData` espejo de `_ConfirmarAccionUsuarioModal.cshtml`; reemplazo del `<select>`/`<input readonly>` en `_Form.cshtml`; JS separado `wwwroot/js/pages/usuario-persona-buscador.js` (fetch async, debounce, `pageSize=25`, estados inicial/empty/loading/error, cierre `Esc`/backdrop, foco inicial en input); `PersonaListQuery` con `bool? SoloSinUsuario` default `false` ortogonal a `Segmento`; `PersonaRepository.QueryAsync` con `LEFT JOIN AspNetUsers` exigiendo `PersonaId IS NULL`; `PersonasController.GetConsulta` con `[FromQuery] bool? soloSinUsuario = null`; `PersonaApiClient.BuildQueryUri` serializa `soloSinUsuario=true`; tests `strict_tdd` (repo `[MySqlFact]`, servicio, controller `[ApiIntegration]`, cliente `RecordingHandler`, page `[WebIntegration]` con `FakePersonaApiClient` extendido).

**No incluido.** Reutilización desde otros módulos; cambios al dominio, migraciones, constraint o FK; reorden del Index de Personas; cambios al typeahead de `Pages/Personas/Shared/`; edición de persona desde el modal; dependencias nuevas; destino de `IPersonaOptionsProvider.GetActivasAsync()` (queda para `design`).

## Capacidades

**New.** `usuario-web-selector-persona-buscador` — selector modal Bootstrap 5 paginado server-side para Crear/Editar Usuario.

**Modified.**

- `persona-management` (requisito *Listado segmentado y paginado de Personas*): acepta `soloSinUsuario` default `false`, ortogonal al segmento.
- `usuario-web-crear-editar` (REQ-UCE-02): dropdown poblado reemplazado por selector con buscador; banner vacío y CTA a `/personas/crear` se conservan.

**Referenciadas (intactas).** `web-apiclient-transport-contract`, `sgv-web-authentication`, `identity-user-role-management`, `usuario-web-listado-detalle-baja`.

## Criterios de aceptación

- `Crear` y `Editar` exponen el selector modal; ningún `<select>` carga el catálogo completo.
- Modal lista solo activas sin usuario, 25/página, búsqueda sobre `Legajo|Apellidos|Nombres|Email|NumeroDocumento`.
- Cierre `Esc`/backdrop/X; foco inicial en input; foco devuelto al disparador.
- En Edición la persona actual se preserva y se omite del modal hasta `Quitar`/`Cambiar`.
- Error de transporte → estado recuperable; `409` → feedback de campo sin perder el resto del formulario.
- Sin nuevas dependencias ni migraciones; `dotnet build`, `dotnet test`, `bun run build` verdes.

## Decisiones diferidas a `design`

1. Nombre final del query (`soloSinUsuario` vs `sinUsuario`).
2. Contrato `ViewData` del partial (`ModalId`, `SelectedHiddenInputName`, `CurrentPersonaId`).
3. Paginación interna: numérica vs `Prev/Next` simple.
4. Política de retención de `IPersonaOptionsProvider.GetActivasAsync()`.
5. AA: `aria-live` para regiones de estado y `tabindex` de filas.

## Riesgos

| Riesgo | Mitigación |
|--------|------------|
| `SoloSinUsuario=true` oculta la persona actual en Edit | Selector pre-pobla con `CurrentPersonaId`; `Cambiar` abre popup; `Quitar` limpia |
| Carrera entre `Buscar` y `Guardar` | `409` por `IX_AspNetUsers_PersonaId` → feedback de campo análogo a Cargos |
| JS inline inmanejable | JS en archivo separado, replica patrón de `personas-index.js` |
| Seam web inexistente para `soloSinUsuario` | Extender `FakePersonaApiClient.QueryHandler`; `FakePersonaOptionsProvider` intacto |

## Plan de fases

explore ✅ · **propose ✅** · spec · design · tasks · apply · verify · archive.

## Notas para el implementador

- **Precedente**: `archive/2026-07-17-modal-confirmacion-bloqueo-desbloqueo/` + `_ConfirmarAccionUsuarioModal.cshtml`; mismo patrón partial + `ViewData`, JS en archivo separado por la complejidad.
- **Endpoint**: extender `GET /api/v1/personas/consulta` (NO `GET /api/v1/personas`); ya paginado y autenticado.
- **Strict TDD**: tests previos al código; extender `PersonaServicioConsultaTests.cs` con `SoloSinUsuario`.
- **No-goals firmes**: sin nuevas migraciones, sin tocar constraint/FK, sin nuevas dependencias, sin tocar el typeahead vigente, sin `default:` en switches exhaustivos.
