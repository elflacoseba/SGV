# Columnas generadas para unicidad activa: convivir con soft-delete

## El dilema del UNIQUE que no es único

En una base con soft-delete, "no puede haber dos cargos con el mismo
código" deja de ser un `UNIQUE INDEX` trivial. Si `IsDeleted = 1`
cuenta para la restricción, los usuarios descubren que nunca pueden
reutilizar el código de un cargo que eliminaron ayer. Si
`IsDeleted = 1` se ignora, el `UNIQUE` no se puede expresar en SQL
porque las columnas se evalúan como iguales.

En SQL Server, la solución estándar sería un índice filtrado:
`CREATE UNIQUE INDEX IX_Cargos_ActiveCodigo ON Cargos(Codigo) WHERE
IsDeleted = 0`. Esa sintaxis no existe en MySQL ni en MariaDB. SGV
necesita otra forma de decir "el código debe ser único entre los
cargos activos, pero dos cargos eliminados con el mismo código pueden
coexistir sin protesta".

## La solución adoptada: columnas generadas

La migración inicial (`20260614183103_InicialSgvo.cs`) crea una
columna computada al lado de cada índice de unicidad. El patrón, sobre
`Cargos`, es:

```sql
ActiveCodigoUnique VARCHAR(255) GENERATED ALWAYS AS
    (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END)
```

y el índice se monta sobre esa columna:

```sql
CREATE UNIQUE INDEX IX_Cargos_ActiveCodigoUnique ON Cargos(ActiveCodigoUnique);
```

La clave del truco es lo que devuelve la columna generada cuando la
fila está eliminada: `NULL`. MySQL trata los `NULL` como distintos
entre sí en índices únicos, así que dos filas eliminadas con el mismo
`Codigo` producen dos `NULL`s que no colisionan. Las filas activas
producen su `Codigo` real, y la restricción `UNIQUE` aplica como se
espera.

Este patrón se replica en todas las entidades con unicidad activa:
`Cargos.ActiveCodigoUnique`, `Puestos.ActiveCodigoUnique`,
`Habilidades.ActiveCodigoUnique`, `UnidadesOrganizativas.ActiveCodigoUnique`,
`Ocupaciones.ActivePuestoIdUnique` y
`Ocupaciones.ActivePersonaPuestoUnique`. Cada uno tiene su columna
generada y su índice único dedicado.

## Restricciones que el equipo debe conocer

La elección del modificador `STORED` vs `VIRTUAL` no es libre. MySQL
8.0 acepta un `UNIQUE INDEX` sobre una columna `VIRTUAL`; MariaDB lo
rechaza. Por eso `SGV.Infraestructura/Persistencia/Migraciones/20260729145632_MariaDbStoredColumnsAndCollation.cs`
fuerza la conversión explícita a `STORED` para que el mismo esquema
sirva en ambos motores. La consecuencia operativa: las columnas
generadas ocupan espacio en disco proporcional al valor que devuelven,
pero pagan eso a cambio de ser evaluables en índices únicos sin
sorpresas entre MySQL y MariaDB.

Las restricciones de portabilidad también se manifiestan en el wrapper
`MigrationsScript` de Pomelo. Cualquier `mb.Sql()` con varios
statements separados por `;` interno rompe el script idempotente.
Patrones prohibidos en migraciones que entran al script
`--idempotent`:

- `BEGIN ... END` con statements internos separados por `;`.
- `CREATE PROCEDURE ... BEGIN ... END` anidado en otro procedure.
- `SET @sql := ...; PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;`

Para lógica condicional idempotente, hay que preferir múltiples
`mb.Sql()` separados, cada uno con un único statement que sea
internamente atómico (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...` o
`DROP PROCEDURE IF EXISTS ...`).

## El caso específico de Vacantes

`ActivePuestoIdUnique` en `Vacantes` agrega una segunda dimensión a la
columna generada. No alcanza con filtrar por `IsDeleted`: una vacante
"cerrada" debe poder convivir con una nueva vacante "abierta" para el
mismo Puesto. La condición incluye el cierre:

```sql
CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0
     THEN `PuestoId` ELSE NULL END
```

De este modo, dos vacantes históricas cerradas para el mismo Puesto
producen `NULL` y no colisionan; reabrir una vacante cerrada o crear
una nueva vacante abierta intenta escribir el `PuestoId` real y la
constraint `UNIQUE` rechaza la segunda activa.

Este patrón cierra la ventana TOCTOU (Time-Of-Check to Time-Of-Use)
que existía cuando el módulo vacantes sólo validaba con
`ExistsAbiertaByPuestoAsync`. El cambio `20260731173842_AddActivePuestoIdUniqueToVacantes`
(documentado en `docs/decisiones-implementacion.md §D-1`) deja la BD
como fuente de verdad: si dos requests presentan `PuestoId` igual en
paralelo, uno pasa y el otro recibe un error de constraint violation
que el servicio traduce a `VacanteErrorCodigo.PuestoConVacanteAbierta`.

## Trade-offs y alternativas descartadas

La alternativa "índices parciales vía SQL Server" no es viable: SGV
está casado con MySQL/MariaDB por una decisión de proyecto documentada
en `docs/decisiones-implementacion.md §"Proveedor de Base de Datos"`.
Migrar el motor sería un proyecto aparte.

La alternativa "eliminar físicamente en lugar de soft-delete" se
descartó mucho antes: el sistema requiere conservar el historial de
qué existió (auditoría, organigramas históricos, Ocupaciones derivadas
de Vacantes eliminadas). El soft-delete es la base del modelo.

La alternativa "validación sólo en la capa de aplicación" se descartó
porque abre la ventana de race condition ya descrita. Mantener la
constraint en la BD garantiza la invariante incluso ante scripts de
migración manual, inserciones raw SQL legadas o bugs en la lógica de
servicio.

## Consecuencias operativas

La columna generada es invisible en casi todo el código de aplicación:
los repositorios, los DTOs, los servicios de comandos — ninguno la
conoce. Sólo el modelo EF la configura y la migración la crea. La
disciplina operativa del equipo es entonces:

**Nunca confiar en que `UNIQUE` evita toda colisión.** Las inserciones
raw SQL o las migraciones de datos que pasan por alto el wrapper de EF
pueden saltarse el índice si lo dropean. La defensa se sostiene sólo
mientras la columna generada exista y el índice esté activo.

**No asumir que cambiar el nombre de la columna rompe el UNIQUE.**
Renombrar `ActiveCodigoUnique` requiere una migración explícita que
recree el índice. Cualquier intento de borrarla y volver a crearla
"manualmente" desde el SQL debe incluir `IsDeleted = 0` en el filtro
o reintroducir la vulnerabilidad.

**`ExecuteUpdateAsync` no puede saltar la constraint**, pero
tampoco puede modificar una columna `GENERATED ALWAYS AS ...` — eso
es por diseño del motor. El servicio que necesita forzar una
"reapertura" masiva tiene que pasar por la entidad de dominio y
aceptar que el código de EF materializa la fila.

## Referencias

- `../how-to/05-agregar-migracion-ef-core.md` — cómo agregar una nueva columna generada con su índice único siguiendo el patrón vigente.
- `../reference/02-esquema-base-de-datos.md` — esquema completo de las columnas generadas en cada tabla.
- `../reference/11-tabla-migraciones-ef-core.md` — historial de migraciones con `STORED`, `VIRTUAL` y conversión a MariaDB.
- `docs/decisiones-implementacion.md` — secciones "Índices Únicos con Soft Delete", "Compatibilidad validada con MySQL 8.4 LTS" y "Ocupaciones Activas".