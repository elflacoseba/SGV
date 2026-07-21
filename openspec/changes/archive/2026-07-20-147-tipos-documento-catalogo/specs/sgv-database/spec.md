# Capability: SGV Database (delta)

> **Status:** MODIFIED — capability exists at `openspec/specs/sgv-database/spec.md`. This delta adds the persistence requirements for the `TiposDocumento` catalog, the `Personas.TipoDocumentoId` FK, the reconstructed `ActiveDocumentoUnique` generated column, and the historical backfill gate.
> **Change:** `2026-07-20-147-tipos-documento-catalogo` (issue #147)

## Summary of the change

Una nueva tabla de catálogo `TiposDocumento` (read-only, inmutable, seedeada con `DNI|LE|LC|Pasaporte`) se introduce. `Personas.TipoDocumentoId` se vuelve una foreign key `char(36) NULL` (opcional para preservar `NumeroDocumento` huérfano) hacia `TiposDocumento.Id` con `OnDelete(Restrict)` y un índice de soporte. La columna generada `ActiveDocumentoUnique` se reconstruye con la fórmula `CONCAT(TipoDocumentoId, ':', NumeroDocumento)` para no-eliminados, conservando unicidad activa. La columna string legacy `Personas.TipoDocumento` se elimina tras el backfill. **La política de backfill DELTA la condición #3 de `REQ-SPA-EVOLUTION-001`**: valores legacy sin código conocido se persisten con `TipoDocumentoId = NULL` y `NumeroDocumento` preservado. El delta formal a `REQ-SPA-EVOLUTION-001` vive en `sgv-persistence-architecture/spec.md`.

## ADDED Requirements

### Requirement: Catálogo `TiposDocumento` con FK `OnDelete(Restrict)`

El sistema DEBE persistir un catálogo inmutable `TiposDocumento` con PK `Id` Guid (`char(36)`), `Codigo varchar(50) UNIQUE NOT NULL`, `Nombre varchar(100) NOT NULL`, `PatronValidacion varchar(255) NULL`, `LongitudMinima int NULL` y `LongitudMaxima int NULL`. El catálogo NO DEBE tener columnas `IsActive` ni `IsDeleted`. La columna `Personas.TipoDocumentoId` DEBE ser una FK `char(36) NULL` con `OnDelete(Restrict)` y DEBE estar indexada. (La nulabilidad de la FK es deliberada: ver escenario "Migración histórica: legacy desconocido → TipoDocumentoId NULL".)

#### Escenario: Enforcement de la FK

- **DADO** que existe una Persona que referencia el `TipoDocumento` con id `X`
- **CUANDO** se ejecuta `DELETE FROM TiposDocumento WHERE Id = X`
- **ENTONCES** MySQL DEBE rechazar la operación con un error de foreign key constraint
- **Y** la fila `X` DEBE permanecer en la tabla.

#### Escenario: Índice sobre la FK

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `SHOW INDEX FROM Personas`
- **ENTONCES** DEBE existir un índice sobre la columna `TipoDocumentoId`
- **Y** ese índice DEBE ser el que usa la FK en `REFERENCES`.

#### Escenario: Catálogo sin flags de estado

- **DADO** que existe la tabla `TiposDocumento`
- **CUANDO** se consultan sus columnas con `DESCRIBE TiposDocumento`
- **ENTONCES** NO DEBE existir una columna `IsActive` ni una columna `IsDeleted`.

### Requirement: Navegación `Persona.TipoDocumento` y FK configurada

`PersonaConfiguracion` DEBE declarar la navegación `TipoDocumento` (`PersonaEntity?`) y la FK `TipoDocumentoId` con `OnDelete(Restrict)` apuntando a `TiposDocumento.Id`. La FK DEBE estar activa tanto en el modelo EF como en la base de datos.

#### Escenario: Configuración EF declara navegación y FK

- **DADO** el `ModelBuilder` del `SgvDbContext` con `PersonaConfiguracion` aplicada
- **CUANDO** se inspecciona el modelo EF
- **ENTONCES** la propiedad `TipoDocumento` está mapeada como navegación hacia `TipoDocumentoEntity`
- **Y** la FK `TipoDocumentoId` está configurada con `OnDelete(Restrict)`.

### Requirement: `ActiveDocumentoUnique` reconstruido con la nueva fórmula

La columna generada `ActiveDocumentoUnique` DEBE reconstruirse con la fórmula `CONCAT(TipoDocumentoId, ':', NumeroDocumento)` evaluada solo para filas no eliminadas (`IsDeleted = 0`), y DEBE seguir siendo la base del índice único activo `IX_Personas_ActiveDocumentoUnique`. La unicidad activa de `TipoDocumentoId + NumeroDocumento` MUST preservarse tras el cambio.

#### Escenario: Definición de la columna generada tras la migración

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta el DDL de `Personas` (vía `SHOW CREATE TABLE Personas` o equivalente)
- **ENTONCES** la columna `ActiveDocumentoUnique` está definida como `AS (CASE WHEN TipoDocumentoId IS NOT NULL AND NumeroDocumento IS NOT NULL AND IsDeleted = 0 THEN CONCAT(TipoDocumentoId, ':', NumeroDocumento) ELSE NULL END)`
- **Y** existe un índice único `IX_Personas_ActiveDocumentoUnique` sobre esa columna.

#### Escenario: Unicidad activa preservada con la nueva fórmula

- **DADO** dos Personas activas con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12345678"`
- **CUANDO** se persiste la segunda
- **ENTONCES** MySQL DEBE rechazar la inserción por violación del índice único activo
- **Y** la primera Persona permanece intacta.

### Requirement: Migración histórica backfill de `TipoDocumento` (string) a `TipoDocumentoId` (FK)

La migración DEBE mapear cada valor existente de `Personas.TipoDocumento` (string) al `Id` de `TiposDocumento` cuyo `Codigo` coincida exactamente. Para los valores `DNI|LE|LC|Pasaporte`, `TipoDocumentoId` queda con el `Id` correspondiente. La columna string legacy `Personas.TipoDocumento` DEBE eliminarse con `DROP COLUMN` una vez que el backfill haya finalizado.

#### Escenario: Backfill limpio mapea códigos conocidos a GUIDs

- **DADO** que todas las filas existentes en `Personas.TipoDocumento` tienen un valor que coincide con un `Codigo` del seed de `TiposDocumento`
- **CUANDO** la migración corre
- **ENTONCES** el backfill de `TipoDocumentoId` desde el `Codigo` DEBE completarse
- **Y** la columna string `TipoDocumento` DEBE eliminarse con `DROP COLUMN`.

#### Escenario: Migración histórica — legacy desconocido → `TipoDocumentoId` NULL con `NumeroDocumento` preservado

- **DADO** al menos una fila con `Personas.TipoDocumento = "FooBar"` (valor que no aparece en el seed de códigos de `TiposDocumento`)
- **CUANDO** la migración corre
- **ENTONCES** la fila queda con `TipoDocumentoId = NULL`
- **Y** `NumeroDocumento` permanece intacto (huérfano, listo para remediación manual post-deploy)
- **Y** el interceptor de auditoría registra la transición `string → NULL` en `Auditorias` para esa fila
- **Y** la columna string `TipoDocumento` DEBE eliminarse con `DROP COLUMN` después del backfill
- **Y** la migración DEBE continuar sin abortar (esto DELTA la condición #3 fail-loud de `REQ-SPA-EVOLUTION-001`; ver `sgv-persistence-architecture/spec.md`).

### Requirement: `PersonaConfiguracion` reemplaza `TipoDocumento` string por `TipoDocumentoId` FK

El modelo EF DEBE dejar de mapear `PersonaEntity.TipoDocumento` (string) tras la migración. La nueva propiedad `PersonaEntity.TipoDocumentoId` (Guid?) DEBE ser la representación de la FK hacia `TiposDocumento`. La columna legacy NO DEBE existir en la tabla `Personas` tras `DROP COLUMN`.

#### Escenario: Schema post-migración no contiene columna `TipoDocumento`

- **DADO** que la migración corrió
- **CUANDO** se consultan las columnas de `Personas` con `DESCRIBE Personas`
- **ENTONCES** la columna `TipoDocumento` (string legacy) NO DEBE existir
- **Y** la columna `TipoDocumentoId` (char(36) NULL) DEBE existir como FK hacia `TiposDocumento`.