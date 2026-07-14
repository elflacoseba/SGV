# Apply progress: Alineación doc/modelo de unicidad de Ocupaciones (issue #127)

## Resumen

RED → GREEN → suite verde. Cambios entregados:

- `docs/decisiones-implementacion.md`: 1 línea reemplazada (L21) dentro de la sección "Ocupaciones Activas" (L19-21). 1 inserción, 1 borrado, neto `+0` líneas en el archivo.
- `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` (nuevo): 119 líneas, namespace `SGV.Tests.Docs`, tres `[Fact]` cubriendo prosa↔modelo, ausencia de nota de cargos concurrentes, y shadow properties vigentes.
- Total work tree: ~120 líneas modificadas/creadas, dentro del presupuesto de 400.
- Filtro `dotnet test SGV.slnx --filter "FullyQualifiedName~CoherenciaDecisionesImplementacion"` ⇒ `Passed! 3/3` en <50 ms.

## Archivos modificados

### `docs/decisiones-implementacion.md` (L21 dentro de "Ocupaciones Activas")

Reemplazo (verbatim del texto nuevo):

```
La versión inicial aplica una única ocupación vigente por Puesto (`ActivePuestoIdUnique`) y una única ocupación vigente por la combinación Persona + Puesto (`ActivePersonaPuestoUnique`), mediante columnas generadas con índices únicos. Una Persona puede mantener varias ocupaciones activas simultáneas siempre que correspondan a Puestos distintos. La regla vigente de unicidad per-persona simple no se enforce; una futura restricción de ese tipo requeriría reintroducir la columna `ActivePersonaIdUnique` con su índice único y la verificación correspondiente en la capa de aplicación.
```

`git diff --stat`: `docs/decisiones-implementacion.md | 2 +-`.

### `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` (nuevo, 119 líneas)

- Clase `public sealed class CoherenciaDecisionesImplementacionTests` en `namespace SGV.Tests.Docs`.
- `[Fact] Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes` — asserta presencia de `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique` (case-insensitive) en la sección extraída vía regex.
- `[Fact] Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes` — asserta ausencia de la frase literal "Si el negocio requiere cargos concurrentes" en la sección.
- `[Fact] Modelo_Ocupaciones_ExponeShadowPropertiesUnicasVigentes` — enumera el modelo EF Core vía `SgvDbContext` (construido con `TestSgvDbContextFactory`, patrón idéntico a `ModeloPersistenciaTests.cs:15`); asserta `FindProperty("ActivePuestoIdUnique")` no nulo + índice único; lo mismo para `ActivePersonaPuestoUnique`; `FindProperty("ActivePersonaIdUnique")` debe ser nulo.
- Helpers: `ResolverRutaMarkdown()` (Lazy, asciende desde `AppContext.BaseDirectory` buscando `docs/decisiones-implementacion.md`), `ExtraerSeccion()` (regex multiline sobre `## ` + cuerpo hasta próximo `## ` o fin), `AssertShadowPropertyUnica()` (helper privado).

## Output de RED

Ejecutado antes de tocar el markdown (cambio sólo agregado el archivo de test):

```
Failed SGV.Tests.Docs.CoherenciaDecisionesImplementacionTests.Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes [2 ms]
  Error Message:
   Assert.DoesNotContain() Failure: Sub-string found
                                ↓ (pos 156)
String: ···"con índices únicos. Si el negocio requier"···
Found:  "Si el negocio requiere cargos concurrente"···

Failed SGV.Tests.Docs.CoherenciaDecisionesImplementacionTests.Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes [< 1 ms]
  Error Message:
   Assert.Contains() Failure: Sub-string not found
String:    "\nLa versión inicial aplica una única ocup"···
Not found: "ActivePuestoIdUnique"

Failed!  - Failed:     2, Passed:     1, Skipped:     0, Total:     3, Duration: 26 ms - SGV.Tests.dll (net10.0)
```

RED demostrado: 2 fallos (prosa) + 1 pass (modelo). El test de modelo pasa desde el inicio porque el modelo ya está bien; los dos fallos prueban que la prosa **no** declara los invariantes vigentes — exactamente el drift que el issue #127 reporta.

## Output de GREEN

Después de reescribir L21 del markdown (con el texto literal de `tasks.md` T2):

```
Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 19 ms - SGV.Tests.dll (net10.0)
```

RED → GREEN completo. Tiempo total <50 ms (cumple scenario "pasa verde en menos de 5 segundos" del spec).

## Output de suite completa

Filtro amplio excluyendo los tests de integración web que requieren bootstrap de `WebApplicationFactory` (éstos fallan en baseline pre-cambio por tema de host factory timeout, ajeno a este change):

```
dotnet test SGV.slnx --filter "FullyQualifiedName~Persistencia|FullyQualifiedName~Dominio|FullyQualifiedName~Compatibilidad|FullyQualifiedName~CoherenciaDecisionesImplementacion|FullyQualifiedName~Common"

Passed!  - Failed:     0, Passed:   492, Skipped:     0, Total:   492, Duration: 3 s - SGV.Tests.dll (net10.0)
```

- 492 tests pass, 0 fail, 0 skip.
- MySQL 8 disponible localmente (`mysqld is alive` en `localhost:3306`), por lo que **todos** los `[MySqlFact]` corren contra `sgv_test` (sin skip). Confirmado: el modelo de Ocupaciones no cambió, el snapshot sigue consistente, las migraciones existentes no se tocaron.
- Verificación cruzada con baseline pre-cambio (git stash) confirmó que las fallas en tests de `Web/Cargo/*` y `Web/UnidadOrganizativaWebTests/*` son **pre-existentes** (mismo error `Expected: OK / Actual: Found` antes y después del cambio), relacionadas con bootstrap de `WebApplicationFactory<Program>` y no con este change.

Sub-filtro Ocupaciones completo:

```
dotnet test SGV.slnx --filter "...Ocupacion..."
Passed!  - Failed:     0, Passed:   103, Skipped:     0, Total:   103, Duration: 374 ms - SGV.Tests.dll (net10.0)
```

Cubre: `CoherenciaDecisionesImplementacionTests` (3) + `ModeloPersistenciaTests` (incluye `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` y `Modelo_Ocupacion_ConservaUnicidadActivaPorPuesto`) + `OcupacionRepositoryTests` + `OcupacionServicioComandosTests` + `OcupacionServicioConsultaTests` + `OcupacionTests` (dominio) + `OcupacionGeneratedColumnRegressionTests`. Sin regresión.

## Diferencias con la propuesta

1. **Path resolution del markdown**: el plan sugería `AppContext.BaseDirectory` + búsqueda ascendente. Implementado exactamente así, pero con un detalle: cuando el test corre desde `tests/SGV.Tests/bin/Debug/net10.0/`, ascende hasta `tests/SGV.Tests/`, luego `tests/`, luego repo root, donde encuentra `docs/decisiones-implementacion.md`. Confirmado robusto para `dotnet test`, `dotnet test --no-build`, y CI con el mismo cwd implícito. Mensaje de error explícito si no se encuentra.

2. **Test class fixture**: el plan sugería `[Collection("DocsCoherencia")]` "para una sola `[Fact]` el default sequential basta". Implementé **tres** `[Fact]` independientes (no `[Theory]`/`InlineData`) — más legible y cada fact falla con su propio nombre, en línea con el patrón de `ModeloPersistenciaTests.cs`. Sin collection fixture.

3. **Modelo expuesto vía `SgvDbContext` construido por `TestSgvDbContextFactory.CreateDbContext([])`** (mismo patrón que `ModeloPersistenciaTests.cs:15`). El plan sugería "construir EF model in-memory via `SgvDbContext` + `ModelBuilder`"; usado el contexto real para mantener paridad con el resto del suite y validar que `OcupacionConfiguracion` aplica como en runtime.

## Pendiente (no delegado al apply)

- Commit + PR (el orchestrator lo manejará en un paso posterior). Mensaje sugerido: `feat: alinear doc de Ocupaciones con modelo vigente (issue #127)` hacia `develop`. Sin atribución IA.
- Verificación final del `git diff --stat` antes de commit: confirmado `1 insertion(+), 1 deletion(-)` en markdown + 119 líneas nuevas en test = ~120 líneas totales, dentro del presupuesto de 400.
- Las fallas pre-existentes en `Web/Cargo/*` y `Web/UnidadOrganizativaWebTests/*` (bootstrap de `WebApplicationFactory`) son **fuera del scope** de este change —建议 abrir issue aparte si el equipo quiere atacarlas.