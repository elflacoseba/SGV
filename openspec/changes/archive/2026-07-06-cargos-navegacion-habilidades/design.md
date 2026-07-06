# Diseño técnico — cargos-navegacion-habilidades

## 1. Resumen
El change agrega entry points visibles hacia `Habilidades.cshtml` desde `Cargos/Index` y `Cargos/Details`, y corrige el feedback de validación de `Actualizar` para que el error quede anclado a la fila editada sin romper el patrón actual Razor Pages + PRG. Se preservan backend, contrato HTTP, DTOs y flujo server-rendered.

## 2. Vista general de la solución
```text
Index (activos) ──click Habilidades──┐
Details ────────click Habilidades────┼──> GET /organizacion/cargos/{id}/habilidades
                                     │     └─ Hidrata Cargo + Skills + catálogos
                                     │
Fila editable ── POST Actualizar ────┼──> API UpsertSkill
                                     │     ├─ éxito: TempData + RedirectToPage(id)
                                     │     └─ fallo 4xx: ModelState por fila + Page()
                                     └──> validation-summary general conserva errores
```

## 3. Cambios por archivo
| Archivo | Tipo | Cambios concretos | Decisiones técnicas |
|---|---|---|---|
| `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` | markup | Nuevo botón icon-only `btn-primary` con `ti ti-stars` entre Detalle y Editar, solo en `!Model.IsDeletedView`, con `aria-label`, tooltip y `href` a `Habilidades` por `id`. | Se reutiliza el patrón visual existente de la columna Acciones; no preserva filtros ni segmento. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` | markup | Nuevo botón textual `btn btn-primary` con `ti ti-stars me-1` y texto `Habilidades`, entre `Editar` y `Volver al listado`, dentro del bloque `!Model.IsNotFound`. | Sigue la convención local de botones con icono+texto del footer; no toca `Details.cshtml.cs`. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml` | markup | Cada fila pasa a nombrar inputs con `Actualizar[{skillId}].Campo`; agrega contenedor de error junto a select/input/checkbox y mantiene el `validation-summary` general. | Se conserva POST clásico, sin AJAX. Para inputs manuales se usa renderizado explícito del mensaje (`invalid-feedback d-block` o `text-danger`) para no depender de `asp-validation-for`. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` | code | `OnPostActualizarAsync` deja de reutilizar el mapping de `Asignar`; agrega binding por colección/diccionario para leer `Actualizar[{skillId}]` y aplica `FieldErrors` a keys por fila. `OnPostAsignarAsync` queda intacto. | Se elige split del helper: `ApplyAsignarFailureToModelState` y `ApplyActualizarFailureToModelState(skillId)`. Menor blast radius, más legible y evita condicionales contextuales en un único método. |
| `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` | test | Cobertura de presencia del CTA en vista activa y ausencia en `eliminadas`. | Preferir 1 theory o 2 tests chicos, sin duplicar el setup existente. |
| `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` | test | Cobertura de presencia del botón en detalle existente y ausencia cuando `IsNotFound`. | Se aprovechan los tests actuales de render del footer. |
| `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | test | Cobertura de error anclado en fila correcta + summary general, fallback defensivo al summary y no regresión del PRG de éxito. | Máximo 3 casos nuevos; mantener suite enfocada en comportamiento observable. |

## 4. Estrategia de ModelState por fila (W1)
- Convención exacta: `Actualizar[{skillId}].NivelRequeridoId`, `Actualizar[{skillId}].Ponderacion`, `Actualizar[{skillId}].EsObligatoria`.
- Binding: el PageModel incorpora una propiedad bindeable `Actualizar` indexada por `skillId`; `OnPostActualizarAsync` toma la fila activa desde esa colección y sigue usando `skillId` de la ruta/query para identificar la asociación.
- Transformación: `FieldErrors["Ponderacion"]` del backend se reescribe a `ModelState[$"Actualizar[{skillId}].Ponderacion"]`.
- Render: cada control manual de la fila consulta esa misma key y muestra el mensaje debajo del control con clases Bootstrap visibles en server-render (`invalid-feedback d-block`); el checkbox usa el mismo patrón debajo del bloque `form-check`.
- Summary general: no cambia; los mismos `ModelState` errors siguen participando del `asp-validation-summary="ModelOnly"` existente.
- Caso defensivo: si una key devuelta por backend no pertenece a `{NivelRequeridoId,Ponderacion,EsObligatoria}` o no puede asociarse con la fila activa, el mensaje MUST agregarse a `ModelState[string.Empty]` para summary general sin anclaje.
- Decisión: **split** en `ApplyAsignarFailureToModelState` / `ApplyActualizarFailureToModelState`. Es más idiomático en este repo porque preserva intacto el flujo ya estable de `Asignar`, reduce branching accidental y hace explícito qué handler traduce qué convención.

## 5. Convenciones de navegación
- El enlace a `Habilidades` solo pasa `id`; `p`, `search`, `sort` y `status` quedan fuera de alcance.
- `aria-label` y tooltip siguen el patrón existente de `Index`: atributo específico por entidad + `data-bs-toggle="tooltip"` + `data-bs-title`.
- Anti-forgery no aplica a los nuevos botones porque son enlaces `<a>`.

## 6. Estrategia de testing
- Runner: xUnit + `SgvWebApplicationFactory` / fixture web existente.
- Cobertura nueva esperada:
  - Index activo: enlace con `aria-label` correcto y `href` al `id` correcto.
  - Index eliminadas: ausencia del enlace.
  - Details existente: presencia del botón.
  - Details inexistente: ausencia del botón.
  - Habilidades `Actualizar` éxito: sin regresión del PRG.
  - Habilidades `Actualizar` 400 con `Ponderacion`: error en fila correcta y en summary.
  - Habilidades error defensivo: solo summary.
- Presupuesto: 5-7 casos máximo.
- Strict TDD: primero RED sobre markup/nuevo mapping; luego GREEN mínimo.

## 7. Riesgos de implementación y mitigaciones
- Drift entre keys de `ModelState` y `name=` de inputs manuales → blindar con tests HTML sobre la key completa.
- Romper PRG/`Page()` por redirect accidental en error → mantener éxito con redirect y fallos recuperables con `return Page()`.
- Cambiar el parseo de `ValidationProblemDetails.Errors` → no tocar el shape; solo cambiar el target key.
- Ensanchar la columna Acciones de `Index` → mantener botón icon-only `btn-sm` y posición intermedia.
- Duplicación del helper → split pequeño y específico, no lógica copiada dispersa.

## 8. Compatibilidad y migraciones
No hay migración de BD, cambio de contrato HTTP, DTOs, API, Aplicación, Infraestructura ni clientes tipados. El diff queda restringido a Razor Pages (`SGV.Web`) y tests web.

## 9. Estimación de tamaño del diff
| Archivo | Líneas estimadas |
|---|---:|
| `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` | 8-16 |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` | 6-12 |
| `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml` | 28-55 |
| `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` | 24-48 |
| `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` | 12-24 |
| `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` | 10-18 |
| `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | 35-72 |
| **Total** | **123-245** |

## 10. Result Contract
- **status**: success
- **executive_summary**: El diseño mantiene el backend intacto y resuelve el change con un diff acotado a Razor Pages: agrega navegación visible desde `Index` y `Details`, y separa el mapeo de errores de `Actualizar` para anclarlos por fila sin romper PRG ni el summary general.
- **artifacts**: [`openspec/changes/cargos-navegacion-habilidades/design.md`]
- **next_recommended**: tasks
- **risks**: ["Drift entre keys `Actualizar[{skillId}].Campo` y los `name=` reales de la fila.", "Redirect accidental en errores de actualización que rompa `ModelState`.", "Ajustes de layout en la columna Acciones del listado."]
- **skill_resolution**: paths-injected — `sdd-design`, `Razor Pages Patterns`, `dotnet-csharp`, `cognitive-doc-design`
