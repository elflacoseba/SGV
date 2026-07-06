# Exploration — cargos-navegacion-habilidades

## Result Contract

- **status**: success
- **executive_summary**: La exploración confirma dos gaps reales y abiertos: la página `Habilidades.cshtml` no tiene entry points visibles desde el flujo principal de Cargos, y los `FieldErrors` del handler `Actualizar` se siguen mapeando al prefijo `AsignarInput.` en vez de anclarse a la fila editable que falló. El cambio puede resolverse sin tocar contrato HTTP ni mover lógica fuera de la página actual. El alcance esperado queda acotado a navegación UI, mapeo de `ModelState` por fila y cobertura web dirigida.
- **artifacts**:
  - `openspec/specs/cargo-skill-ui-tabla-editable/spec.md`
  - `openspec/specs/cargo-skill-asignar-editar/spec.md`
  - `openspec/specs/cargo-skill-ponderacion-obligatoria/spec.md`
  - `openspec/specs/cargo-skill-query-contract/spec.md`
  - `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/archive-report.md`
  - `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/verify-report.md`
  - `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`
  - `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`
  - `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml`
  - `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml.cs`
  - `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml`
  - `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs`
  - `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`
  - `src/SGV.Web/Integration/Organizacion/CargoHabilidadInputModels.cs`
  - `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`
  - `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml`
  - `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml`
  - `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml.cs`
  - `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs`
  - `tests/SGV.Tests/Web/Cargo/CargoHabilidadesAntiDriftTests.cs`
  - `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`
  - `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs`
  - `openspec/changes/cargos-navegacion-habilidades/exploration.md`
- **next_recommended**: propose
- **risks**:
  - `Details.cshtml` hoy no preserva `returnStatus`, a diferencia del módulo de Habilidades; si el cambio quisiera conservar segmento `eliminadas` en un eventual back-link futuro, eso ampliaría alcance.
  - El PageModel de `Habilidades` no recibe hoy contexto de retorno (`p`, `search`, `sort`, `returnStatus`); agregarlo no es necesario para cerrar W-UX/W1, pero puede aparecer como pedido derivado.
  - El feedback por fila exige definir keys estables de `ModelState` para inputs renderizados manualmente; si se mezcla binding implícito con nombres ad-hoc sin convención, los errores pueden volver a caer en el summary general.
  - La página usa PRG para éxitos y `return Page()` para fallos de asignar/actualizar; cualquier intento de “redirigir también en error” rompería el requisito de conservar inputs y mensajes por fila.
  - Existe riesgo de sobre-implementar navegación agregando cambios en `Edit.cshtml` o moviendo UI hacia otra página, aunque el alcance transferido explícitamente lo excluye.
- **skill_resolution**: paths-injected — `sdd-explore` + `Razor Pages Patterns` + `dotnet-csharp` + `dotnet-xunit` + `dotnet-best-practices`

## Exploration: cargos-navegacion-habilidades

### Current State

El módulo de Cargos ya tiene tres superficies relevantes: listado (`Index`), detalle readonly (`Details`) y grilla editable de habilidades (`Habilidades`).

- `Index.cshtml` muestra en la columna **Acciones** tres CTAs por fila activa: **Ver detalle** (`btn-info`, icono `ti ti-eye`), **Editar** (`btn-warning`, `ti ti-edit`) y **Eliminar** (`btn-danger`, `ti ti-trash`). No existe CTA hacia `/organizacion/cargos/{id}/habilidades`.
- `Details.cshtml` muestra al pie solo dos acciones: **Editar** (`btn-warning`, `ti ti-pencil`) y **Volver al listado** (`btn-outline-secondary`, `ti ti-arrow-left`). Tampoco existe CTA hacia Habilidades.
- `Habilidades.cshtml` ya cumple el flujo base del subrecurso: GET hidrata tabla + catálogos; `OnPostAsignarAsync` y `OnPostActualizarAsync` re-renderizan la página ante fallo; `OnPostQuitarAsync` hace PRG con `TempData` para éxito/warning/danger.
- La baja `Quitar` ya quedó corregida con confirmación inline `confirm(...)`, por lo que el riesgo abierto real de UI es W-UX (descubribilidad) y W1 (anclaje de errores por fila).

### Affected Areas

- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` — necesita exponer un CTA visible a Habilidades dentro de la grilla de acciones.
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` — opcionalmente puede centralizar route values del nuevo enlace si se quiere mantener el patrón `Build*RouteValues(...)`.
- `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` — necesita sumar un botón en la barra inferior para navegar a Habilidades.
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml` — requiere surface para mostrar validación por fila en la grilla editable.
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` — requiere distinguir el origen del fallo (`Asignar` vs `Actualizar`) al traducir `FieldErrors` a `ModelState`.
- `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` — debe blindar el nuevo enlace en la grilla activa y su ausencia en eliminadas.
- `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` — debe blindar el nuevo botón en detalle y su ausencia cuando `IsNotFound`.
- `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` — debe agregar cobertura específica para errores de backend en `Actualizar` anclados a la fila.

### Resumen del estado actual de navegación

#### Index

La convención actual de la grilla de acciones en `Index.cshtml` es:

- botones circulares `btn-icon btn-sm rounded-circle`
- color semántico por intención (`info` detalle, `warning` editar, `danger` eliminar)
- íconos `ti ti-*`
- `data-bs-toggle="tooltip"` + `data-bs-title`
- `aria-label` específico por entidad

Hoy el bloque activo de acciones usa exactamente ese patrón y deja espacio natural para un cuarto CTA entre **Detalle** y **Editar**, o entre **Editar** y **Eliminar**.

#### Details

La barra inferior usa botones rectangulares con texto + icono, no icon-only. La convención actual es:

- acción primaria secundaria de edición en `btn btn-warning`
- retorno en `btn btn-outline-secondary`
- iconos `ti ti-pencil` y `ti ti-arrow-left`

No hay una toolbar superior ni acciones embebidas en la card; el lugar correcto para sumar “Habilidades” es esa misma barra inferior.

### Gap UX detallado

#### Hallazgo

El riesgo W-UX sigue vigente: la página `@page "/organizacion/cargos/{id:guid}/habilidades"` existe y funciona, pero no tiene entry point visible desde el flujo primario del usuario.

#### Propuesta para Index

Agregar un nuevo botón icon-only en la columna **Acciones** de la vista activa:

- **ubicación**: dentro del `div.d-flex.justify-content-center.gap-1`, solo cuando `!Model.IsDeletedView`
- **posición recomendada**: después de **Detalle** y antes de **Editar** para mantener progresión “ver → gestionar relación → editar entidad”
- **estilo recomendado**: `btn btn-primary btn-icon btn-sm rounded-circle`
- **icono recomendado**: `ti ti-stars` o `ti ti-list-details`; `ti ti-stars` comunica mejor “habilidades/competencias” y mantiene diferenciación visual respecto de detalle/editar/eliminar
- **tooltip**: `Habilidades`
- **aria-label**: `Gestionar habilidades de @item.Nombre`
- **navegación**: `@Url.Page("/Organizacion/Cargos/Habilidades", new { id = item.Id })`

No conviene enlazarlo en vista `eliminadas`: el módulo actual evita exponer acciones write en elementos dados de baja y la página de Habilidades exige rol administrador + opera sobre asociaciones activas.

#### Propuesta para Details

Agregar un botón textual en la barra inferior:

- **ubicación**: mismo bloque que hoy contiene `Editar` y `Volver al listado`
- **posición recomendada**: entre `Editar` y `Volver al listado`
- **estilo recomendado**: `btn btn-primary`
- **icono recomendado**: `ti ti-stars me-1`
- **texto**: `Habilidades`
- **navegación**: `@Url.Page("/Organizacion/Cargos/Habilidades", new { id = Model.Cargo!.Id })`

Esto respeta la convención del propio `Details`: botones de texto, no íconos circulares.

### Gap W1 detallado

#### Hallazgo

`HabilidadesModel.ApplySkillFailureToModelState(...)` hoy no sabe si el fallo vino del form de asignación o de la fila editable. El helper hace esto:

- si hay `FieldErrors`, recorre cada key del backend
- si la key no empieza con `AsignarInput.`, la reescribe como `AsignarInput.{Campo}`
- luego inserta esos errores en `ModelState`

Eso funciona para `OnPostAsignarAsync`, porque el markup del formulario inferior sí usa:

- `asp-for="AsignarInput.SkillId"`
- `asp-for="AsignarInput.NivelRequeridoId"`
- `asp-for="AsignarInput.Ponderacion"`
- `asp-validation-for="..."`

Pero la grilla editable de `OnPostActualizarAsync` renderiza inputs manuales por fila con nombres simples:

- `name="NivelRequeridoId"`
- `name="Ponderacion"`
- `name="EsObligatoria"`

y NO tiene hoy `asp-validation-for`, ni summary por fila, ni un prefijo diferenciador por `skillId`.

#### Consecuencia observable

Cuando el backend devuelve `FieldErrors` por actualización de una fila existente:

- el PageModel conserva el error
- pero lo inyecta bajo `AsignarInput.*`
- entonces el mensaje solo puede terminar en el summary/formulario de asignación
- y no junto a la fila que disparó el `PUT`

Eso contradice la expectativa de `cargo-skill-ui-tabla-editable` Req 3 para feedback claro sobre la fila editada.

#### Propuesta recomendada

Mantener la arquitectura actual de la página y separar el mapping por contexto:

1. `OnPostAsignarAsync` sigue usando el mapping actual hacia `AsignarInput.*`.
2. `OnPostActualizarAsync` debe pasar contexto adicional (`skillId` o un prefijo de fila) al helper.
3. La grilla debe nombrar inputs y spans de validación con una key estable por fila, por ejemplo:
   - `Actualizar[{skillId}].NivelRequeridoId`
   - `Actualizar[{skillId}].Ponderacion`
   - `Actualizar[{skillId}].EsObligatoria`
4. El helper debe traducir `FieldErrors["Ponderacion"]` a la key de esa fila específica, no al form `AsignarInput`.
5. El markup de cada fila debe renderizar un contenedor de error justo debajo del select/input/checkbox correspondiente.

Con eso el handler sigue haciendo `return Page()` en error, por lo que NO se rompe PRG: los éxitos permanecen en PRG; los fallos de actualización siguen en re-render server-side, que es exactamente el comportamiento que ya usa la página para conservar inputs y mensajes.

#### Qué NO recomiendo

- No redirigir tras fallo de `Actualizar`: perdería `ModelState` y obligaría a serializar errores en `TempData`.
- No reutilizar el formulario inferior de asignación para mostrar errores de edición: ya se probó que es confuso y fue exactamente el WARNING transferido.
- No mover la fila editable a modal/AJAX en este change: rompe presupuesto y cambia demasiado la UX/base de tests.

### Approaches

1. **Agregar navegación mínima + mapping contextual de `ModelState`** — conservar la estructura actual y corregir solo entry points + anclaje por fila.
   - Pros: menor diff, no cambia contrato HTTP, no requiere rediseño de página, mantiene tests y patrón Razor existente.
   - Cons: obliga a introducir una convención explícita de keys por fila en markup manual.
   - Effort: Low/Medium.

2. **Refactorizar la grilla a inputs fuertemente tipados por colección** — modelar filas editables como colección bindeable y usar `asp-for`/`asp-validation-for` por índice.
   - Pros: validación más idiomática de Razor Pages, menos strings mágicos en keys.
   - Cons: mucho más invasivo, reescribe binding, aumenta riesgo sobre una página recién estabilizada y probablemente rompe budget de 400 líneas.
   - Effort: High.

### Recommendation

Recomiendo la **Approach 1**. Es la que cierra exactamente los dos riesgos transferidos sin abrir otro frente: añadir CTAs en `Index` y `Details`, y hacer que `OnPostActualizarAsync` traduzca `FieldErrors` a keys específicas de la fila editada. Eso preserva el contrato del subrecurso, el patrón PRG/TempData ya vigente y la estructura actual de la Razor Page.

### Patrones a respetar

- **PRG con `TempData`** para éxitos y para `Quitar`; no convertir fallos de `Actualizar` en redirect.
- **`[Authorize]` + chequeo explícito `RolesSgv.Administrador`** como frontera de acceso; ya es patrón deliberado de la página.
- **Anti-forgery token** en todos los forms POST.
- **Conventions de query string** del módulo: `p`, `search`, `sort`, `status`.
- **`Url.Page(...)`** para navegación interna; evitar hardcodear rutas salvo donde el repo ya lo hace de forma heredada.
- **Validaciones Bootstrap/Razor** con `asp-validation-for` cuando el campo es bindeable; si la fila sigue con markup manual, replicar visualmente el patrón de error `text-danger` junto al input.
- **Segmento `activas|eliminadas`**: el nuevo CTA a Habilidades debe vivir solo en activas.

### Riesgos / suposiciones con mitigación

- **Suposición**: el botón de Habilidades solo debe existir para cargos activos.  
  **Mitigación**: alinearlo con el branch `!Model.IsDeletedView` ya usado para detalle/editar/eliminar.

- **Suposición**: no hace falta preservar contexto de retorno al entrar a Habilidades.  
  **Mitigación**: documentarlo como no-objetivo del change; si negocio pide back-link contextual, abrir slice separado.

- **Riesgo**: usar keys de `ModelState` incompatibles con el `name=` del input hará que el error no renderice.  
  **Mitigación**: definir una única convención y blindarla con tests HTML sobre `data-valmsg-for` o contenido por fila.

- **Riesgo**: intentar reutilizar `[BindProperty]` del form de asignación para la edición por fila seguirá mezclando errores.  
  **Mitigación**: separar helper o parámetro `target` (`Asignar` vs `Actualizar(skillId)`).

- **Riesgo**: agrandar alcance tocando `Edit.cshtml` o el contrato de `ICargoApiClient`.  
  **Mitigación**: dejar explícito en proposal/spec que el change no modifica ni entry point en Edit ni shape HTTP.

### Lo que NO entra en el change

- NO se va a tocar `Pages/Organizacion/Cargos/Edit.cshtml` para embutir gestión de habilidades.
- NO se va a mover la lógica de negocio/UI fuera de `Pages/Organizacion/Cargos/Habilidades.cshtml` y su PageModel actual.
- NO se va a cambiar el contrato HTTP de `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills`.
- NO se va a rediseñar la grilla a SPA/modal/AJAX.
- NO se va a mezclar la gestión de habilidades con la vista `eliminadas` de cargos.

### Tamaño esperado

Estimación razonable del diff total:

- navegación `Index` + tests asociados: **25-50 líneas**
- navegación `Details` + tests asociados: **20-35 líneas**
- mapping por fila en `Habilidades.cshtml(.cs)` + tests nuevos/ajustados: **80-160 líneas**

**Forecast total**: **125-245 líneas** aproximadamente. Queda dentro del budget de review de 400 líneas si se mantiene este alcance y no se expande a `Edit` ni a soporte de back-link contextual.

### Ready for Proposal

Sí. El change está lo bastante claro para pasar a `propose`: los dos riesgos abiertos existen, el alcance técnico está acotado y no hace falta investigación adicional para formular propuesta, delta spec y tareas.
