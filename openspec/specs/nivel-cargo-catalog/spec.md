# Especificación de Catálogo de Nivel de Cargo

## Propósito

Catálogo de niveles jerárquicos usados por Cargos, referenciados mediante `NivelId` como clave foránea. Los niveles definen el orden y la jerarquía de los cargos dentro de la organización.

## Requisitos

### Requisito: Entidad NivelCargo

El sistema DEBE mantener una tabla `NivelesCargo` como catálogo inmutable con PK `Id` (Guid), `Codigo` varchar(50) UNIQUE NOT NULL, `Nombre` varchar(100) NOT NULL, `ValorNumerico` tinyint NOT NULL y `Orden` int NOT NULL.

#### Escenario: Estructura de la tabla

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `DESCRIBE NivelesCargo`
- **ENTONCES** DEBEN existir las columnas `Id` (char(36) PK), `Codigo` (varchar(50) UNIQUE NOT NULL), `Nombre` (varchar(100) NOT NULL), `ValorNumerico` (tinyint NOT NULL) y `Orden` (int NOT NULL)
- **Y** NO DEBEN existir columnas `IsActive` ni `IsDeleted`.

#### Escenario: Constraint de valor numérico

- **DADO** que la tabla `NivelesCargo` existe
- **CUANDO** se inserta un registro con `ValorNumerico` fuera del rango permitido
- **ENTONCES** MySQL DEBE rechazar la inserción por la restricción check.

### Requisito: Referencia desde Cargos via NivelId

La tabla `Cargos` DEBE introducir una columna `NivelId` (char(36) NOT NULL) como FK hacia `NivelesCargo.Id` con `OnDelete(Restrict)` e índice. La columna string `Nivel` DEBE eliminarse tras la migración.

#### Escenario: FK OnDelete(Restrict)

- **DADO** que existe un Cargo que referencia un NivelCargo con `Id` X
- **CUANDO** se ejecuta `DELETE FROM NivelesCargo WHERE Id = X`
- **ENTONCES** MySQL DEBE rechazar la operación con error de foreign key constraint
- **Y** la fila X DEBE permanecer en la tabla.

#### Escenario: Índice sobre NivelId

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `SHOW INDEX FROM Cargos`
- **ENTONCES** DEBE existir un índice sobre la columna `NivelId`
- **Y** ese índice DEBE ser el que usa la FK en `REFERENCES`.

### Requisito: Evolución de Catálogo NivelCargo

La migración que introduce la FK `NivelId` DEBE seguir el patrón de evolución de catálogo (REQ-SPA-EVOLUTION-001): pre-flight de strings sucios, fail-loud si valores ofensivos existen, y eliminación de la columna string `Nivel` solo tras backfill exitoso.

#### Escenario: Backfill limpio

- **DADO** que todas las filas existentes en `Cargos.Nivel` tienen un valor que coincide con un `Codigo` del seed de NivelesCargo
- **CUANDO** la migración corre
- **ENTONCES** el backfill de `NivelId` desde el `Codigo` DEBE completarse
- **Y** la columna `NivelId` DEBE quedar `NOT NULL`
- **Y** la columna string `Nivel` DEBE eliminarse con `DROP COLUMN`.

#### Escenario: Fail-loud aborta antes del ALTER

- **DADO** que al menos una fila tiene `Nivel = "FooBar"` (valor no presente en el seed)
- **CUANDO** la migración corre
- **ENTONCES** DEBE lanzar `InvalidOperationException`
- **Y** el mensaje DEBE listar los valores ofensivos
- **Y** la migración DEBE detenerse antes de cualquier `ALTER TABLE` que cambie `NivelId` a `NOT NULL`
- **Y** la columna `Nivel` (string) DEBE permanecer intacta.

### Requisito: Seed de NivelesCargo con Guids estáticos

El seed de `NivelesCargo` DEBE usar constantes Guid estáticas compartidas entre la migración `InsertData` y `DatosSemilla.HasData`. Un test unitario DEBE verificar que no hay drift entre ambos orígenes.

#### Escenario: Coherencia de seed entre migración y HasData

- **DADO** que la clase de constantes de Guid para NivelesCargo está definida
- **CUANDO** se ejecuta el test de coherencia de seed
- **ENTONCES** todos los `Id` del `InsertData` de la migración DEBEN estar presentes en `DatosSemilla.HasData`
- **Y** la cantidad de `Id` distintos en ambas fuentes DEBE ser idéntica.

### Requisito: Acceso de Solo Lectura a NivelesCargo

El sistema DEBE exponer NivelesCargo como recurso de solo lectura a través de la API. Los endpoints de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre NivelesCargo NO DEBEN estar disponibles.

#### Escenario: Listar niveles de cargo

- **DADO** que existen niveles de cargo persistidos
- **CUANDO** un cliente solicita `GET /api/v1/niveles-cargo`
- **ENTONCES** el sistema DEBE devolver una colección JSON con `id`, `codigo`, `nombre`, `valorNumerico` y `orden` de cada nivel.

#### Escenario: Obtener nivel por identificador

- **DADO** que existe un NivelCargo con el identificador dado
- **CUANDO** un cliente solicita `GET /api/v1/niveles-cargo/{id:guid}`
- **ENTONCES** el sistema DEBE devolver los datos del nivel solicitado.

#### Escenario: Escritura rechazada

- **DADO** un cliente que intenta crear, actualizar o eliminar un NivelCargo
- **CUANDO** envía `POST`, `PUT`, `PATCH` o `DELETE` a `/api/v1/niveles-cargo`
- **ENTONCES** la API DEBE responder con 405 Method Not Allowed o no exponer esos endpoints.