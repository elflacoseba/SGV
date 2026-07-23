# Delta for SGV Database

## ADDED Requirements

### Requirement: Catálogo `CategoriasHabilidad` con FK `OnDelete(Restrict)` (REQ-CAT-01)

El sistema DEBE persistir un catálogo inmutable `CategoriasHabilidad` con PK `Id` Guid (`char(36)`), `Codigo varchar(50) UNIQUE NOT NULL` y `Nombre varchar(100) NOT NULL`. El catálogo NO DEBE tener columnas `IsActive` ni `IsDeleted`. La siembra MUST ocurrir exclusivamente dentro de una migración de EF Core y MUST contener exactamente 4 filas (`Conduccion`, `Tecnica`, `Dominio`, `Academica`) con GUIDs del bloque reservado `72000000-…`.

#### Scenario: Estructura de la tabla `CategoriasHabilidad`

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `DESCRIBE CategoriasHabilidad`
- **ENTONCES** DEBEN existir `Id` (char(36) PK), `Codigo` (varchar(50) UNIQUE NOT NULL) y `Nombre` (varchar(100) NOT NULL)
- **Y** NO DEBEN existir columnas `IsActive` ni `IsDeleted`.

#### Scenario: Seed crea exactamente 4 filas

- **DADO** que la tabla está vacía
- **CUANDO** la migración corre
- **ENTONCES** el `SELECT COUNT(*) FROM CategoriasHabilidad` MUST devolver `4`
- **Y** los códigos `Conduccion`, `Tecnica`, `Dominio` y `Academica` MUST estar presentes.

### Requirement: `Habilidades.CategoriaId` FK opcional con `OnDelete(Restrict)` (REQ-CAT-02)

El sistema DEBE introducir la columna `Habilidades.CategoriaId` (`char(36)` `NULL`) como FK hacia `CategoriasHabilidad.Id` con `OnDelete(Restrict)` y DEBE estar indexada (`IX_Habilidades_CategoriaId`). La columna string legacy `Habilidades.Categoria` (`varchar(100)` `NULL`) DEBE eliminarse tras la migración de backfill.

#### Scenario: Esquema post-migración no contiene columna `Categoria`

- **DADO** que la migración corrió
- **CUANDO** se consultan las columnas de `Habilidades` con `DESCRIBE Habilidades`
- **ENTONCES** la columna `Categoria` (string legacy) NO DEBE existir
- **Y** la columna `CategoriaId` (`char(36)` `NULL`) DEBE existir como FK hacia `CategoriasHabilidad.Id`.

#### Scenario: Enforcement de la FK con `OnDelete(Restrict)`

- **DADO** que existe una `Habilidad` que referencia `CategoriasHabilidad.Id = <X>`
- **CUANDO** se ejecuta `DELETE FROM CategoriasHabilidad WHERE Id = <X>`
- **ENTONCES** MySQL DEBE rechazar la operación con error de foreign key constraint
- **Y** la fila `<X>` DEBE permanecer en la tabla.

#### Scenario: Índice sobre la FK

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `SHOW INDEX FROM Habilidades`
- **ENTONCES** DEBE existir un índice sobre la columna `CategoriaId`
- **Y** ese índice DEBE ser el que usa la FK en `REFERENCES`.

### Requirement: Migración fail-loud con pre-flight de strings sucios (REQ-CAT-04 — variante opt-in relajada)

La migración que introduce `Habilidades.CategoriaId` DEBE ejecutar un `SELECT` de pre-flight que liste los valores distintos de `Habilidades.Categoria` (string legacy) que no se correspondan con un `Nombre` del seed de `CategoriasHabilidad` (match exacto, case-insensitive). Bajo la variante **opt-in relajada** del REQ-SPA-EVOLUTION-001 (FK nullable, orphan-tolerant), los valores ofensivos deben quedar con `CategoriaId = NULL` en lugar de abortar: el backfill completa, la columna string se elimina con `DROP COLUMN`, y el interceptor de auditoría registra la transición `legacy string → NULL` por cada fila afectada.

#### Scenario: Backfill limpio mapea categorías semilla a GUIDs

- **DADO** que `Habilidades.Categoria` contiene sólo valores que matchean exactamente con `CategoriasHabilidad.Nombre` (por ejemplo `Conducción`, `Técnica`, etc.)
- **CUANDO** la migración corre
- **ENTONCES** el backfill de `CategoriaId` desde `CategoriasHabilidad.Nombre` (case-insensitive) DEBE completarse
- **Y** la columna `CategoriaId` DEBE quedar `NULL` en cero filas
- **Y** la columna string `Categoria` DEBE eliminarse con `DROP COLUMN` después del backfill.

#### Scenario: Valores sucios caen a `NULL` con auditoría

- **DADO** que al menos una fila tiene `Categoria = "FooBar"` (valor no presente en el seed de `CategoriasHabilidad.Nombre`)
- **CUANDO** la migración corre
- **ENTONCES** esa fila queda con `CategoriaId = NULL`
- **Y** la columna string `Categoria` DEBE eliminarse con `DROP COLUMN` después del backfill
- **Y** el interceptor de auditoría DEBE registrar la transición `legacy string → NULL` en `Auditorias` para cada fila afectada
- **Y** la migración NO DEBE abortar (variante relajada del REQ-SPA-EVOLUTION-001).

#### Scenario: Drop de la columna legacy tras backfill

- **DADO** que el backfill completa (limpio o con NULLs)
- **CUANDO** la migración ejecuta el `DROP COLUMN Habilidades.Categoria`
- **ENTONCES** la columna string legacy DEBE eliminarse de la tabla
- **Y** un `SELECT` posterior contra `DESCRIBE Habilidades` MUST NOT incluir `Categoria`.
