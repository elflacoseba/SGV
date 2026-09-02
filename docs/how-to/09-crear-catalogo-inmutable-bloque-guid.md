# H-02-09 — Crear un nuevo catálogo inmutable con bloque GUID reservado

El proyecto reserva bloques contiguos de 16 bits del espacio de GUIDs para que los catálogos inmutables seedeados por migración tengan IDs estables y predecibles. Un catálogo NUEVO debe pedir su bloque, declarar constantes tipadas y registrar el bloque en el mapa (decisión §"Mapa de bloques GUID").

---

## Prerrequisitos

- Haber leído `docs/decisiones-implementacion.md` § "Mapa de bloques GUID reservados por catálogo" (línea ~860).
- Haber inspeccionado `src/SGV.Infraestructura/Persistencia/Catalogos/NivelCargoConstantes.cs` como plantilla.
- Diseño del catálogo cerrado: cantidad de filas seed, códigos únicos, orden.

---

## Paso 1 — Asignar un bloque contiguo

Bloques ya reservados: `10000000` (NivelHabilidad), `20000000` (EstadoVacante), `30000000` (EstadoPostulacion), `40000000` (Cargo), `50000000` (Habilidad), `60000000` (TipoUnidadOrganizativa), `70000000` (NivelCargo), `71000000` (TipoDocumento), `72000000` (CategoriaHabilidad). El próximo libre es `73000000-…`.

**Verificación:** `grep -rn "73000000" src/` no devuelve hits antes de tu cambio. Si hay colisión, avanzá al siguiente bloque libre.

---

## Paso 2 — Declarar las constantes tipadas

Creá `src/SGV.Infraestructura/Persistencia/Catalogos/<Nombre>Constantes.cs` siguiendo el patrón de `NivelCargoConstantes.cs`: declarar `Id`, `Codigo`, `Nombre`, etc. por cada fila seed; un record `<Nombre>Seed(...)`; y un array `Semilla` que los materialice. Cada `Id` parsea el GUID del bloque reservado.

**Verificación:** la fila final del archivo es el record `Seed` y `Semilla` lo materializa. Compilá `src/SGV.Infraestructura` para asegurar que no rompiste el patrón.

---

## Paso 3 — Crear la migración con `InsertData`

En la migración nueva usá `migrationBuilder.InsertData(table, columns, values: object[,])` donde `values` consume `<Nombre>Constantes.Semilla1Id` / `Semilla1Codigo` (nunca GUIDs literales inline).

**Verificación:** el test `Migration_NoContieneGuidsLiterales_Para<Nombre>` (espejo del existente para `NivelCargo`) asserta este contrato.

---

## Paso 4 — Sincronizar `DatosSemilla`

En `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` agregá un `builder.Entity<<Nombre>Entity>().HasData(<Nombre>Constantes.Semilla.Select(s => new <Nombre>Entity { Id = s.Id, Codigo = s.Codigo, /* otros campos */ }).ToArray())`.

**Verificación:** el test `DatosSemilla_<Nombre>_SeedIdsMatchConstantes` queda verde. La paridad entre migración y `HasData` evita que un `add migration` accidental mueva los IDs del snapshot sin tocar la fila viva.

---

## Paso 5 — Actualizar el mapa de bloques

Editá `docs/decisiones-implementacion.md` § "Mapa de bloques GUID reservados por catálogo" y `AGENTS.md` § "Decisiones Técnicas" para agregar la fila `73000000-… | <Nombre> (issue #N) | <Nombre>Constantes | Semilla1Id, Semilla2Id`.

---

## Paso 6 — Correr la suite y los tests de paridad

```bash
dotnet test SGV.slnx --filter "FullyQualifiedName~<Nombre>Constantes|FullyQualifiedName~DatosSemilla"
```

**Verificación:** los tests de drift quedan verdes. La migración aplica limpia contra `sgv_test` con MySQL local.

---

## Troubleshooting

- **`Migration_NoContieneGuidsLiterales` falla**: la migración usa `Guid.Parse("73000000-…")` inline. Reemplazá por `<Nombre>Constantes.SemillaXId`.
- **EF genera un `DROP` sobre la tabla seed**: el snapshot está desincronizado con el `HasData`. Confirmá que `Semilla` se mantiene idéntico y el orden de las propiedades del record no cambió.
- **Dos features piden el mismo bloque**: chocaron en code review. Resolvé y avanzá uno al siguiente bloque libre.

---

## Referencias

- `docs/decisiones-implementacion.md` § "Mapa de bloques GUID reservados por catálogo" (línea ~860).
- `AGENTS.md` § "Decisiones Técnicas que NO conviene romper".
- `src/SGV.Infraestructura/Persistencia/Catalogos/NivelCargoConstantes.cs` — plantilla con `Seed` y `Semilla`.
- `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` — sitio de `HasData` por catálogo.
- `../how-to/05-agregar-migracion-ef-core.md` — cómo crear la migración asociada.
- [E-04-12](../explanation/12-catalogos-inmutables-bloques-guid.md) —
  Explanation de por qué se usan bloques GUID reservados en vez de
  `Guid.NewGuid()`.
