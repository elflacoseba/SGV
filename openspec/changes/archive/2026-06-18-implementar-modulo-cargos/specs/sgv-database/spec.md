# Delta para sgv-database

## MODIFIED Requirements

### Requirement: Cargos Reutilizables con Ciclo de Vida

El sistema DEBE mantener cargos como tipos de puesto reutilizables e independientes de los puestos concretos, con ciclo de vida de alta lógica, actualización de campos editables e inmutabilidad de `Codigo`.
(Anteriormente: el requisito solo cubría reutilización sin gestión de ciclo de vida ni inmutabilidad de Codigo.)

#### Escenario: Reutilizar cargo en varios puestos

- **DADO** un cargo llamado Director
- **CUANDO** varios puestos concretos requieren ese cargo
- **ENTONCES** cada puesto DEBE referenciar el mismo cargo.

#### Escenario: Codigo inmutable tras creación

- **DADO** un cargo existente con `Codigo` "DIR01"
- **CUANDO** se intenta modificar el campo `Codigo`
- **ENTONCES** el sistema NO DEBE permitir la modificación
- **Y** el endpoint de actualización NO DEBE incluir `Codigo` como campo editable.

#### Escenario: Baja lógica de Cargo

- **DADO** un cargo activo
- **CUANDO** se solicita desactivar el cargo
- **ENTONCES** el sistema DEBE marcar el cargo como inactivo sin eliminación física
- **Y** el cargo desactivado NO DEBE aparecer en consultas de cargos activos.

#### Escenario: Reactivación de Cargo conservando Codigo

- **DADO** un cargo desactivado con `Codigo` "DIR01"
- **Y** no existe otro cargo activo con `Codigo` "DIR01"
- **CUANDO** se solicita reactivar el cargo
- **ENTONCES** el sistema DEBE restaurar el estado activo
- **Y** el cargo DEBE conservar su `Codigo` original.

### Requirement: Cargos Referencian NivelCargo por FK

El sistema DEBE reemplazar la columna string `Cargos.Nivel` por una FK `Cargos.NivelId` (char(36) NOT NULL) que referencie `NivelesCargo.Id` con `OnDelete(Restrict)`. La columna `NivelId` DEBE estar indexada. La columna string `Nivel` DEBE eliminarse tras la migración.
(Anteriormente: Cargo usaba un campo string `Nivel` sin normalización.)

#### Escenario: FK de NivelCargo en Cargos

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta la estructura de `Cargos`
- **ENTONCES** la columna `NivelId` DEBE existir como `char(36) NOT NULL`
- **Y** DEBE tener una FK hacia `NivelesCargo.Id` con `OnDelete(Restrict)`
- **Y** la columna string `Nivel` NO DEBE existir.

#### Escenario: Eliminación de NivelCargo con Cargo referenciando

- **DADO** que existe un Cargo que referencia un NivelCargo
- **CUANDO** se intenta eliminar el NivelCargo
- **ENTONCES** MySQL DEBE rechazar la operación por violación de FK.

### Requirement: Unicidad de Codigo Activo de Cargo

El sistema DEBE impedir que dos cargos activos compartan el mismo `Codigo`, permitiendo que un mismo `Codigo` se reuse si el cargo previo fue desactivado.

#### Escenario: Rechazar codigo activo duplicado

- **DADO** que existe un cargo activo con un `Codigo`
- **CUANDO** se guarda otro cargo activo con el mismo `Codigo`
- **ENTONCES** el sistema DEBE rechazar el cambio.

#### Escenario: Permitir reutilización tras baja lógica

- **DADO** que un cargo con un `Codigo` fue dado de baja lógicamente
- **CUANDO** se crea un nuevo cargo activo con ese `Codigo`
- **ENTONCES** el sistema PUEDE permitir el cambio si no existe otro cargo activo con ese `Codigo`.

### Requirement: Catálogo `NivelesCargo` con FK `OnDelete(Restrict)`

El sistema DEBE persistir un catálogo inmutable `NivelesCargo` con PK `Id` Guid (`char(36)`), `Codigo varchar(50)` `UNIQUE NOT NULL`, `Nombre varchar(100) NOT NULL`, `ValorNumerico tinyint NOT NULL` con check constraint, y `Orden int NOT NULL`. El catálogo NO DEBE tener columnas `IsActive` ni `IsDeleted`. La columna `Cargos.NivelId` DEBE ser una FK `char(36) NOT NULL` con `OnDelete(Restrict)` y DEBE estar indexada.

#### Escenario: Enforcement de la FK

- **DADO** que existe un `Cargo` que referencia el nivel con id `X`
- **CUANDO** se ejecuta `DELETE FROM NivelesCargo WHERE id = X`
- **ENTONCES** MySQL DEBE rechazar la operación con un error de foreign key constraint
- **Y** la fila `X` DEBE permanecer en la tabla.

#### Escenario: Índice sobre la FK

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `SHOW INDEX FROM Cargos`
- **ENTONCES** DEBE existir un índice sobre la columna `NivelId`
- **Y** ese índice DEBE ser el que usa la FK en `REFERENCES`.

#### Escenario: Catálogo sin flags de estado

- **DADO** que existe la tabla `NivelesCargo`
- **CUANDO** se consultan sus columnas con `DESCRIBE NivelesCargo`
- **ENTONCES** NO DEBE existir una columna `IsActive` ni una columna `IsDeleted`.

#### Escenario: Check constraint de ValorNumerico

- **DADO** que la tabla `NivelesCargo` existe
- **CUANDO** se inserta un registro con `ValorNumerico` fuera del rango permitido
- **ENTONCES** MySQL DEBE rechazar la inserción por la restricción check.

### Requirement: Migración fail-loud con pre-flight de Nivel a NivelId

La migración que introduce la FK `NivelId` DEBE ejecutar un `SELECT` de pre-flight que liste todo valor distinto de `Cargos.Nivel` (string) que no se corresponda con un `Codigo` del seed de NivelesCargo. Si existe al menos un valor ofensivo, la migración DEBE abortar lanzando `InvalidOperationException` con un mensaje que liste los valores ofensivos, sin hacer backfill ni `DROP COLUMN`. Si no hay valores ofensivos, el backfill completa, la columna FK queda `NOT NULL` y la columna string se elimina.

#### Escenario: Backfill limpio de Nivel a NivelId

- **DADO** que todas las filas existentes en `Cargos.Nivel` tienen un valor que coincide con un `Codigo` del seed de NivelesCargo
- **CUANDO** la migración corre
- **ENTONCES** el backfill de `NivelId` desde el `Codigo` DEBE completarse
- **Y** la columna `NivelId` DEBE quedar `NOT NULL`
- **Y** la columna string `Nivel` DEBE eliminarse con `DROP COLUMN`.

#### Escenario: Fail-loud aborta antes del ALTER

- **DADO** que al menos una fila tiene `Nivel = "FooBar"` (un valor que no aparece en el seed de códigos de NivelesCargo)
- **CUANDO** la migración corre
- **ENTONCES** DEBE lanzar `InvalidOperationException`
- **Y** el mensaje de la excepción DEBE listar los valores ofensivos
- **Y** la migración DEBE detenerse antes de cualquier `ALTER TABLE` que cambie `NivelId` a `NOT NULL`
- **Y** la columna `Nivel` (string) DEBE permanecer intacta en la base de datos.

#### Escenario: Seed de NivelesCargo presente después de la migración

- **DADO** que la migración corrió sobre una base de datos limpia
- **CUANDO** se consulta `SELECT COUNT(*) FROM NivelesCargo`
- **ENTONCES** el resultado DEBE corresponder a la cantidad de niveles definidos en el seed.