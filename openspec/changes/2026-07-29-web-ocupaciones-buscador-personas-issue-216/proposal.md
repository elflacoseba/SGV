# Proposal: Buscador de Personas en formulario de Ocupacion (#216)

> Issue: [#216](https://github.com/elflacoseba/SGV/issues/216) — Agregar un buscador de Personas al crear una Ocupacion
> Cambio: `2026-07-29-web-ocupaciones-buscador-personas-issue-216`
> Modo artefactos: **Both** (OpenSpec + Engram) · Review budget: **400 lineas**

## Contexto

El formulario de Crear/Editar Ocupacion (`_Form.cshtml`) usa un `<select>` plano para `PersonaId`, cargado via `IPersonaApiClient.GetAllAsync()` sin paginar. Cuando el catalogo de personas crece, el selector es inutilizable. La issue #216 pide reemplazar ese dropdown por el modal de buscador ya implementado en Usuarios (`_PersonaBuscadorModal.cshtml` + `usuario-persona-buscador.js`).

El modal existe y funciona para Usuarios. El cambio consiste en reutilizarlo en Ocupaciones, con una diferencia funcional critica: en Ocupaciones una Persona PUEDE tener multiples ocupaciones, por lo que NO debe filtrarse `soloSinUsuario=true`. El JS compartido hardcodea ese filtro en la linea 154, lo cual constituye el principal riesgo de implementacion.

## Alcance

### Incluye

- Reemplazo del `<select PersonaId>` en `_Form.cshtml` de Ocupaciones por la card de Persona + modal reutilizable.
- Fix backwards-compatible en `usuario-persona-buscador.js`: lectura de `data-solo-sin-usuario` desde el modal para conditionalizar `soloSinUsuario=true`; default behaviour para Usuarios sigue funcionando sin cambios.
- Extensiones en `IOcupacionForm` (`PersonaDisplay`, `PersonaVinculada`) y `OcupacionFormPageModel` (carga de persona enriquecida para la card en modo Edit).
- Seccion Scripts en `Create.cshtml` y `Edit.cshtml` de Ocupaciones con inclusion de `usuario-persona-buscador.js` y el markup del modal.
- ViewData populating en `Create.cshtml.cs` y `Edit.cshtml.cs` (CurrentPersonaId, CurrentPersonaDisplay) para pre-seleccion y preserved-state en Edit.
- Tests existentes que validan el `<select>` de Persona (`OcupacionCreatePageTests.Get_Create_WhenAdmin_RendersAllFiveFieldsWithCatalogs`, lineas 102-103) — actualizados o removidos segun corresponda y reemplazados por cobertura equivalente del modal.

### No incluye

- Backend (API, Application, Domain). No se toca `OcupacionesController`, `OcupacionServicio`, ni entidades.
- `<select>` de PuestoId — permanece intacto.
- Mover `_PersonaBuscadorModal.cshtml` de su ubicacion actual en `Pages/Seguridad/Usuarios/`.
- Otros modulos distintos de Ocupaciones que eventualmente requieran el mismo modal.
- Migraciones de base de datos.

## Criterios de aceptacion

1. El formulario de Crear y Editar Ocupacion muestra la card de Persona con boton "Buscar" que abre el modal, en lugar del `<select>` actual.
2. El modal de busqueda de Personas muestra resultados paginados sin aplicar `soloSinUsuario=true`.
3. En modo Edit, la persona ya vinculada aparece pre-seleccionada en la card y se excluye del modal hasta pulsar "Cambiar".
4. El fix en `usuario-persona-buscador.js` es backwards-compatible: Usuarios sigue filtrando `soloSinUsuario=true` sin cambios en su codigo.
5. Los tests que validaban el `<select>` original (`Assert.Contains("Garcia, Ana"...)`, `Assert.Contains("Analista"...)`) se actualizan o retiran y se reemplazan por cobertura equivalente del modal.
6. `dotnet build SGV.slnx` y `dotnet test SGV.slnx` pasan sin errores.
7. `bun run build` pasa si se modifican assets frontend (ninguno esperado).

## Capacidades

**New.** `ocupacion-web-selector-persona-buscador` — reutilizacion del modal existente para el campo PersonaId en el formulario de Ocupaciones, sin filtro `soloSinUsuario`.

**Modified.**

- `usuario-web-selector-persona-buscador` (capacidad existente): recibe `data-solo-sin-usuario` como parametro de configuracion del modal; el comportamiento default (`true`) no cambia para Usuarios.

**Intactas.** `persona-management`, `ocupacion-management`, `puesto-web-crear-editar`, `web-apiclient-transport-contract`.

## Primer corte de escenarios

1. **Crear Ocupacion — busqueda de persona existente**
   - Given: Un administrador autenticado accede a `/organizacion/ocupaciones/crear`
   - When: Pulsa "Buscar" en la card de Persona
   - Then: Se abre el modal con input de busqueda, paginacion y lista de personas activas; al seleccionar una persona la card se actualiza con Nombre, Apellido y la persona queda asignada al `Input.PersonaId`.

2. **Crear Ocupacion — persona pre-cargada via query string**
   - Given: Se accede a `/organizacion/ocupaciones/crear?personaId={id}`
   - When: La pagina carga
   - Then: La card de Persona muestra la persona correspondiente y `Input.PersonaId` esta poblada; el modal al abrirse excluye esa persona.

3. **Editar Ocupacion existente — persona vinculada preservada**
   - Given: Existe una ocupacion con persona asignada; se accede a `/organizacion/ocupaciones/editar/{id}`
   - When: La pagina carga
   - Then: La card de Persona muestra la persona vinculada; al abrir el modal, esa persona no aparece en la lista; pulsar "Cambiar" abre el modal con todas las demas personas.

4. **Busqueda de persona — sin resultados**
   - Given: Se abre el modal de busqueda de personas
   - When: Se escribe un termino que no coincide con ninguna persona
   - Then: Se muestra estado vacio con mensaje "No se encontraron personas".

5. **Error de transporte en busqueda de persona**
   - Given: El API de personas no esta disponible
   - When: Se pulsa "Buscar" en la card de Persona
   - Then: Se muestra estado de error dentro del modal; el formulario permanece intacto.

## Riesgos y mitigaciones

| # | Riesgo | Severidad | Mitigacion |
|---|--------|-----------|------------|
| 1 | `usuario-persona-buscador.js:154` hardcodea `soloSinUsuario=true` en todas las invocaciones. La issue #216 exige que en Ocupaciones NO se filtre por ese flag. | CRITICAL | Agregar lectura de `data-solo-sin-usuario` desde el atributo `data-*` del modal; conditionalizar el `url.searchParams.set`; el default `true` preserva el comportamiento actual de Usuarios. Cambio localizada en una sola linea + una lectura de atributo. |
| 2 | `OcupacionCreatePageTests:102-103` assertion `Assert.Contains("Garcia, Ana", ...)` y `Assert.Contains("Analista", ...)` validan texto del `<option>` del `<select>`. Esas opciones desaparecen con el reemplazo por modal y los tests quebraran. | CRITICAL | Actualizar o remover esas assertions. Los tests de POST que envian `Input.PersonaId` via hidden input no se rompen. Reemplazar la cobertura por assertions equivalentes del modal (card renderizada, input hidden poblados). |
| 3 | `IOcupacionForm` no expone `PersonaDisplay` ni `PersonaVinculada`, necesarios para popular la card enriquecida del modal. | HIGH | Agregar ambas propiedades a la interfaz `IOcupacionForm` e implementarlas en `OcupacionFormPageModel`. |
| 4 | `LoadCatalogsAsync` en `OcupacionFormPageModel` usa `GetAllAsync()` para `PersonaOptions`. La card enriquecida necesita el DTO completo de la persona individual. En Edit, se requiere `GetByIdAsync` adicional para enriquecer la card. | HIGH | En Create la card viene vacia (sin pre-seleccion). En Edit, usar `PersonaApiClient.GetByIdAsync(Input.PersonaId)` para cargar el DTO enriquecido despues de resolver `Input.PersonaId` desde la `OcupacionDto`. |
| 5 | `Create.cshtml` y `Edit.cshtml` de Ocupaciones no tienen seccion `@section Scripts` ni incluyen `usuario-persona-buscador.js`. | MEDIUM | Agregar `@section Scripts { <script src="/js/pages/usuario-persona-buscador.js"></script> }` en ambas pages, debajo del form. |
| 6 | El modal `_PersonaBuscadorModal` espera ViewData `CurrentPersonaId`/`CurrentPersonaDisplay` para pre-seleccionar. En Create vendra vacio; en Edit se poblara desde la `OcupacionDto`. | LOW | La logica de seteo es la misma que en Usuarios y se移植a directamente. |

## Rollback Plan

- Revertir el cambio en `usuario-persona-buscador.js` elimina la lectura de `data-solo-sin-usuario` y restaura el hardcodeo de `soloSinUsuario=true`; Usuarios vuelve a su estado previo inmediatamente.
- Revertir `_Form.cshtml` de Ocupaciones restaura el `<select>` original; los tests de dropdown recuperan su cobertura original.
- Ningun cambio toca base de datos ni migraciones.

## Plan de fases

explore ✅ · **propose ✅** · spec · design · tasks · apply · verify · archive.

## Notas para el implementador

- **Precedente directo**: `archive/2026-07-17-buscador-personas-modal/` — mismo modal, mismo JS, misma logica de ViewData. La unica diferencia es el flag `soloSinUsuario`.
- **Orden de implementacion sugerido**: (1) fix JS en `usuario-persona-buscador.js`, (2) extender `IOcupacionForm` + `OcupacionFormPageModel`, (3) actualizar `_Form.cshtml`, (4) agregar Scripts y modal en `Create.cshtml`/`Edit.cshtml`, (5) actualizar tests.
- **Tests**: los `MySqlFact` de OcupacionRepository NO se rompen; los tests de PageModel que asertan el dropdown original deben actualizarse antes de cualquier otra tarea.
- **Sin nuevas dependencias**: no se agregan paquetes NuGet ni cambios en el pipeline Bun/Gulp.
