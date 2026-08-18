# Especificación: Eliminación de VacanteErrorCodigo.MotivoObligatorio

## Propósito

Eliminar la constante `VacanteErrorCodigo.MotivoObligatorio` del layer público `SGV.Contracts`. Fue declarada pero nunca referenciada en `src/` ni `tests/`. El dominio trata `Motivo` como opcional al cerrar (PB-3 vigente). Decisión D-5.

## Requisitos

### Requisito: Constante eliminada del archivo

`src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` NO DEBE contener la línea `public const string MotivoObligatorio = ...` ni su documentación XML asociada.

#### Escenario: Ausencia del símbolo en el archivo de contrato

- **DADO** el archivo `VacanteErrorCodigo.cs`
- **CUANDO** se inspecciona el tipo por reflexión
- **ENTONCES** el símbolo `VacanteErrorCodigo.MotivoObligatorio` NO DEBE existir.

### Requisito: Ausencia global en src/

`grep -rn "MotivoObligatorio" src/` NO DEBE retornar coincidencias.

#### Escenario: Grep src/ retorna cero

- **DADO** todo el código de `src/`
- **CUANDO** se ejecuta `grep -rn "MotivoObligatorio" src/`
- **ENTONCES** el resultado DEBE ser 0.

### Requisito: Ausencia en tests/

`grep -rn "MotivoObligatorio" tests/` NO DEBE retornar coincidencias.

#### Escenario: Grep tests/ retorna cero

- **DADO** todos los tests de `tests/SGV.Tests`
- **CUANDO** se ejecuta `grep -rn "MotivoObligatorio" tests/`
- **ENTONCES** el resultado DEBE ser 0.

### Requisito: El dominio sigue tratando Motivo como opcional

`Vacante.Cerrar(...)` (en `src/SGV.Dominio/Vacantes/Vacante.cs`) NO DEBE imponer `Motivo` requerido en construcción. La condición vigente de `Motivo` opcional al cerrar (PB-3) DEBE preservarse intacta.

(Previously: comportamiento vigente — no se introduce ni se quita.)

#### Escenario: Cerrar con Motivo null es válido

- **DADO** una vacante `Abierta`
- **CUANDO** se invoca `Vacante.Cerrar(motivo: null, ...)`
- **ENTONCES** la operación NO DEBE lanzar excepción
- **Y** la vacante DEBE persistirse con `Motivo = null`.
