# Catálogos inmutables con bloques GUID reservados

## Por qué GUIDs y no `IDENTITY`

SGV necesita identificadores únicos para las filas de catálogo que
sobreviven a reinicios de base, a restores desde backups y a
re-seeds manuales. Una columna `INT AUTO_INCREMENT` cumpliría la
primera y la tercera, pero no la segunda: un restore en otro
ambiente puede terminar con IDs idénticos a los del ambiente
original y la confusión operacional está garantizada.

Los GUIDs son globalmente únicos por construcción. La pregunta
operativa es cómo elegir los valores para que las filas sembradas
sean **estables** entre despliegues. La respuesta obvia es
`Guid.NewGuid()` en cada seeder, pero esa respuesta destruye la
estabilidad: cualquier `dotnet ef migrations add` accidental que
re-materialice el snapshot del modelo cambiaría los IDs de las
filas semilla, y los tests que asumen IDs específicos quedarían
rojos sin causa real.

La elección adoptada es **GUIDs con bloques reservados por
catálogo**, persistidos como constantes en
`src/SGV.Infraestructura/Persistencia/Catalogos/<Nombre>Constantes.cs`.

## El mapa de bloques

La convención vigente reserva bloques contiguos de 16 bits del
espacio de GUIDs. Cada bloque agrupa `2^16 = 65536` filas. El primer
byte del `Guid` identifica el catálogo al que pertenece la fila.

| Bloque GUID    | Catálogo                        | Constantes                       |
|----------------|---------------------------------|----------------------------------|
| `60000000-…`   | `TipoUnidadOrganizativa`        | `InstitucionId`, `AreaId`, `GerenciaId`, `SedeId`, etc. |
| `70000000-…`   | `NivelCargo` (issue #141)       | `DirectivoId`, `ConduccionMediaId`, `OperativoId`, `AcademicoId` |
| `71000000-…`   | `TipoDocumento` (issue #147)    | `DniId`, `LeId`, `LcId`, `PasaporteId` |
| `72000000-…`   | `CategoriaHabilidad`            | `ConduccionId`, `TecnicaId`, `DominioId`, `AcademicaId` |
| `20000000-…`   | `EstadoVacante`                 | `AbiertaId`, `EnSeleccionId`, `CubiertaId`, `CanceladaId` |
| (libre)        | Próximos catálogos              | —                                |

`NivelCargo` usa 4 filas, `TipoDocumento` usa 4, `EstadoVacante`
usa 4, `CategoriaHabilidad` usa 4. El catálogo más grande
(`TipoUnidadOrganizativa`) usa 24 y todavía queda holgado dentro del
bloque de 65536. La elección de 16 bits por bloque es holgada para
catálogos pequeños y medianos y barata de reservar (un byte literal).

## Doble fuente de verdad y el test de paridad

Cada catálogo tiene dos representaciones en el repo:

- `DatosSemilla.HasData` (EF Core model snapshot path) — el snapshot
  del modelo EF declara las filas semilla como parte del modelo.
- `migrationBuilder.InsertData(...)` en la migración específica —
  la fila SQL real que se ejecuta contra MySQL.

Ambas representations consumen las constantes de
`<Nombre>Constantes.cs`. El test
`DatosSemilla_<Nombre>_SeedIdsMatchConstantes` assertea que las dos
representaciones usen la misma source-of-truth. Si alguien edita
`DatosSemilla.cs` con un GUID literal, el test falla. Si alguien
edita la migración con un GUID literal distinto, el test también
falla. La disciplina se sostiene por la combinación de tests
estructurales y migración.

## Consecuencias operativas

La principal consecuencia operativa es la **regla para nuevos
catálogos**. Cualquier catálogo inmutable nuevo debe:

1. Asignarse un bloque contiguo `XX000000-…` con `XX` aún no usado.
2. Declarar sus IDs en
   `src/SGV.Infraestructura/Persistencia/Catalogos/<Nombre>Constantes.cs`
   siguiendo el patrón de `NivelCargoConstantes` y
   `TipoDocumentoConstantes`.
3. Actualizar el mapa en `docs/decisiones-implementacion.md §"Mapa de
   bloques GUID reservados por catálogo"` y en `AGENTS.md`.

El catálogo mutable (CRUD vía API, IDs autogenerados por
`Guid.NewGuid()`) NO usa este patrón — son entidades de negocio y
sufren las mismas reglas de identidad que cualquier otra fila.

## Lo que se gana

La elección tiene tres beneficios concretos:

**Estabilidad entre reinicios.** Un restore de la base desde un
backup en otro ambiente produce exactamente los mismos GUIDs en los
catálogos inmutables. Los tests que asumen IDs específicos siguen
verdes, los logs históricos siguen siendo comparables y los
integraciones externas que guardaron un `Id` de catálogo siguen
resolviendo.

**Orden predecible en la UI.** Los IDs crecen dentro del bloque en
orden de declaración (`70000000-…0001`, `…0002`, etc.), así que el
orden natural por PK es el orden semántico. Un dropdown que ordena
por `Codigo` igual produce el ordenamiento deseado, pero si en el
futuro hace falta "orden de creación", basta con ordenar por la PK
del catálogo.

**Documentación visible del catálogo dueño.** Cuando un operador ve
un GUID `72000000-0000-0000-0000-000000000003` en una fila, sabe que
esa fila pertenece al bloque `72000000` reservado para
`CategoriaHabilidad`. No hace falta abrir la tabla para confirmar
de qué catálogo viene. La "etiqueta" del bloque es legible.

## Lo que se pierde

El costo más visible es la **necesidad de reservar un bloque antes
de crear un catálogo nuevo**. Si alguien agrega `EstadoX` con IDs
`Guid.NewGuid()` sin pedir bloque, rompe el patrón y los tests de
paridad no van a detectarlo inmediatamente — sólo cuando alguien
intente sembrar desde cero en otro ambiente. La defensa es por
revisión de PR: cualquier catálogo inmutable nuevo debe tener su
bloque reservado explícitamente en este documento y en
`decisiones-implementacion.md`.

El segundo costo es la **complejidad del seed**: hay que mantener
dos representaciones sincronizadas (snapshot del modelo + migración)
y un test que verifica la paridad. Para catálogos con pocas filas
(4-10 entradas), el costo es aceptable; para catálogos de cientos
de filas, el snapshot puede divergir por accidente.

## Trade-offs y alternativas descartadas

La alternativa "GUIDs secuenciales con `COMB`" (COMB GUIDs de SQL
Server) no es portable a MySQL. La alternativa "IDs tipo UUID v7"
sería mejor para orden temporal pero requeriría generarlos desde la
aplicación, no desde el motor — y bloquearía re-seeds manuales.

La alternativa "usar `IDENTITY` con secuencia nombrada" se descartó
porque pierde la portabilidad entre MySQL y MariaDB (los nombres
de secuencia difieren) y porque los restores producen colisiones.

El uso de bloques explícitos gana porque convierte la decisión
"¿qué catálogo es este GUID?" en una operación de inspección de
dos caracteres. Cualquier solución automática pierde ese
diagnóstico inmediato.

## Referencias

- `../how-to/09-crear-catalogo-inmutable-bloque-guid.md` — el procedimiento paso a paso para reservar un bloque nuevo.
- `../reference/08-catalogos-inmutables-bloques-guid.md` — la tabla completa de bloques y constantes vigentes.
- `openspec/specs/cargo-skill-query-contract/` y los demás specs de catálogos — los Given/When/Then que justifican cada bloque.
- `docs/decisiones-implementacion.md` — sección "Mapa de bloques GUID reservados por catálogo" con la tabla canónica.