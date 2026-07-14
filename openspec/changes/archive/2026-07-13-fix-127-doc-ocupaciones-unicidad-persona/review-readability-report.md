# Revisión de Legibilidad — `review-readability`

**Lente**: `review-readability`  
**Change**: `2026-07-13-fix-127-doc-ocupaciones-unicidad-persona`  
**Fecha**: 2026-07-13  
**Revisor**: R2 Readability

---

## Verdict

**PASS CON SUGERENCIAS**

El diff es pequeño (~120 LoC), enfocado, y la intención es clara. La estructura del test sigue las convenciones de `ModeloPersistenciaTests.cs`. Hay un **WARNING** por un bug silencioso en el camino de fallo de `ExtraerSeccion`, más dos **SUGGESTION** de estilo que mejoran consistencia con el suite existente.

---

## Naming

- ✅ **Clase**: `CoherenciaDecisionesImplementacionTests` — sigue el patrón español de `ModeloPersistenciaTests`. Claro.
- ✅ **Prefijos de métodos**: `Doc_*` y `Modelo_*` — distinguen claramente qué validan (prosa vs. modelo EF). Consistentes con `Modelo_Ocupacion_*` en el suite existente.
- ✅ **Constantes**: `ShadowPuesto`, `ShadowPersonaPuesto`, `ShadowPersonaSimple` — intención clara, paralelas a los nombres de columna generada.
- ⚠️ **SUGGESTION**: `AssertShadowProperty**Unica**` usa el adjetivo *"Única"* (sin tilde en el identificador). El suite existente usa el sustantivo *"Unicidad"* (ej. `Modelo_Ocupacion_ConservaUnicidadActivaPorPuesto` en L133 de `ModeloPersistenciaTests.cs`). Cambiar a `AssertShadowPropertyUnicidad` mantendría consistencia terminológica dentro del mismo proyecto de tests.

---

## Complexity

- ✅ **`ResolverRutaMarkdown`**: Ascenso por `AppContext.BaseDirectory` caminando hacia el padre. Es el patrón estándar para encontrar archivos del repo desde un test runner compilado en `/bin/Debug/net10.0/`. Lineal, sin sorpresas, tira `FileNotFoundException` con mensaje claro si no encuentra el archivo. **Aceptable**.
- ✅ **`ExtraerSeccion`**: Una sola expresión regular de 3 líneas con grupos nombrados. No hay over-engineering. No se usan Markdig, ReverseMarkdown ni parsers externos — bien.
- ✅ **`AssertShadowPropertyUnica`**: Helper de 13 líneas, extraído correctamente, sin abstracciones innecesarias.

---

## Intention

- ✅ `Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes`: "el doc menciona los dos invariantes que existen".
- ✅ `Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes`: "el doc ya NO tiene la nota sobre cargos concurrentes que estaba en la versión anterior".
- ✅ `Modelo_Ocupaciones_ExponeShadowPropertiesUnicasVigentes`: "el modelo EF tiene las dos shadow properties con índices únicos, y la antigua `ActivePersonaIdUnique` ya no existe".
- ✅ XML doc en la clase explica el propósito general y referencia la spec canónica.
- ✅ Los `[Fact]` están correctamente ubicados (xUnit v3, clase `sealed` para optimización del runner).

---

## Maintainability

### Extracción de sección por regex

```csharp
private static string ExtraerSeccion(string markdown, string encabezado)
{
    var patron = new Regex(
        @"^##\s+" + Regex.Escape(encabezado) + @"\s*$(?<cuerpo>.*?)(?=^##\s+|\z)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    var match = patron.Match(markdown);
    return match.Success ? match.Groups["cuerpo"].Value : string.Empty;
}
```

**Brittleness analysis**:
- El regex asume `## ` (heading level 2) tanto para el heading buscado como para el delimitador de fin. En el documento actual **todos** los headings son nivel 2 (confirmado: 17 secciones, todas `##`). Esto es correcto hoy.
- `Regex.Escape(encabezado)` protege contra caracteres especiales en el nombre del heading. ✅
- `\z` (absolute end of string) + `RegexOptions.Singleline` funciona correctamente para la última sección. ✅
- **Riesgo real**: si alguien cambia `## Ocupaciones Activas` a `### Ocupaciones Activas` (nivel 3), el regex no matchea y `ExtraerSeccion` devuelve `string.Empty` — el `Assert.NotNull` lo pasa (ver **WARNING**) y el test falla con un mensaje confuso.
- **Juicio**: Es aceptable para un test de coherencia que *debe* fallar si el documento cambia de formato. La severidad depende del bug de `string.Empty` vs. `null` (ver WARNING abajo).

---

## Review Size

| Archivo | Líneas | Estado |
|---|---|---|
| `docs/decisiones-implementacion.md` | 1 línea reemplazada (L21) | Modificado |
| `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` | 120 líneas | Nuevo |
| `openspec/changes/archive/.../*` | SDD archive | Contexto no revisado |
| **Total diff** | **~121 LoC** | |

**PR-shaped**: Sí. Un solo tema (validar que doc ↔ modelo están sincronizados sobre unicidad de ocupaciones). No separable en múltiples PRs. ✅

---

## Context Clarity

- ✅ Los nombres de métodos siguen el patrón `{Area}_{Seccion}_{Scenario}`:
  - `Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes`
  - `Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes`
  - `Modelo_Ocupaciones_ExponeShadowPropertiesUnicasVigentes`
- ✅ Cada `[Fact]` tiene un solo assert conceptual (aunque usa múltiples `Assert.Contains` internamente, todos verifican el mismo escenario).
- ✅ El xml-doc de la clase y la referencia a la spec canónica resuelven el "WHY" sin leer el change proposal.

---

## Findings

### CRITICAL: 0
### WARNING: 1

#### W-01: `ExtraerSeccion` retorna `string.Empty` en fallo, no `null`

- **Archivo**: `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs`, línea 118
- **Evidencia**: El método retorna `match.Success ? ... : string.Empty` (L118). Los llamadores hacen `Assert.NotNull(seccion)` en L36 y L55. `string.Empty` **no es null**, por lo que la aserción pasa silenciosamente. Si la sección no se encuentra (heading renombrado, reformateado a nivel 3, etc.), el test falla en el `Assert.Contains` siguiente con un mensaje como:

  ```
  Assert.Contains() Failure: Not found: ActivePuestoIdUnique in (empty string)
  ```

  En lugar de un mensaje claro como `"Section 'Ocupaciones Activas' not found in document"`.
- **Por qué importa**: Un desarrollador que reformatee el documento recibe un falso positivo parcial (el `Assert.NotNull` pasa) y luego un error confuso. El fix es trivial: cambiar `string.Empty` por `null` en L118, y opcionalmente renombrar a `ExtraerSeccionOrNull`.
- **Severidad**: WARNING (bloquea el commit si el equipo exige mensajes de error claros en tests de coherencia).

### SUGGESTION: 2

#### S-01: `AssertShadowPropertyUnica` — inconsistencia terminológica con el suite existente

- **Archivo**: `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs`, línea 75
- **Evidencia**: El método `AssertShadowPropertyUnica` usa el adjetivo "Unica" (por "Única"). El suite existente en `ModeloPersistenciaTests.cs` usa el sustantivo "Unicidad" (L133: `Modelo_Ocupacion_ConservaUnicidadActivaPorPuesto`, L152: `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto`).
- **Sugerencia**: Renombrar a `AssertShadowPropertyUnicidad` para mantener consistencia terminológica dentro de la misma solución de tests.
- **Severidad**: SUGGESTION

#### S-02: Tipo completamente calificado `Microsoft.EntityFrameworkCore.Metadata.IEntityType`

- **Archivo**: `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs`, línea 76
- **Evidencia**: El parámetro `entidad` usa el tipo completamente calificado `Microsoft.EntityFrameworkCore.Metadata.IEntityType` en lugar de importar el namespace. `ModeloPersistenciaTests.cs` hace `using Microsoft.EntityFrameworkCore.Metadata;` (L4) y usa `IEntityType` sin calificación.
- **Sugerencia**: Agregar `using Microsoft.EntityFrameworkCore.Metadata;` y usar `IEntityType` a secas para consistencia con el suite existente.
- **Severidad**: SUGGESTION

---

## Recommended Pre-PR Actions

1. **Corregir W-01**: Cambiar `string.Empty` por `null` en L118 de `ExtraerSeccion`. Esto convierte un error confuso en una aserción clara: `Assert.NotNull` falla con "Expected: non-null, Actual: null" y el stack trace apunta directamente a la línea que no encontró la sección.
   - Cambio: `return match.Success ? match.Groups["cuerpo"].Value : null;`
   - Opcional: renombrar a `ExtraerSeccionOrNull` si el equipo prefiere nombres que revelen nulabilidad.

2. **(Opcional) Aplicar S-01**: Renombrar `AssertShadowPropertyUnica` → `AssertShadowPropertyUnicidad` para alineación terminológica.

3. **(Opcional) Aplicar S-02**: Importar `Microsoft.EntityFrameworkCore.Metadata` y acortar el tipo del parámetro.

Ninguna de estas sugerencias es bloqueante para merge, pero W-01 es muy recomendable antes del commit por calidad de diagnóstico.

---

## Resumen

| Check | Estado |
|---|---|
| Naming | ✅ (S-01 menor) |
| Complexity | ✅ Sin over-engineering |
| Intention | ✅ Clara |
| Maintainability | ⚠️ W-01: camino de fallo silencioso |
| Review Size | ✅ PR-shaped |
| Context Clarity | ✅ |
