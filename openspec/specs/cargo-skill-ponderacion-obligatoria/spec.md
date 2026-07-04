# Especificación de ponderación y obligatoriedad en habilidades de cargo

## Purpose

Definir las reglas visibles y validables de `Ponderacion` y `EsObligatoria` dentro del vínculo `CargoHabilidad`, preservando la semántica actual de dominio y evitando drift entre UI, API y persistencia.

## Requirements

### Requirement 1: Defaults explícitos del vínculo

El sistema MUST completar valores por defecto consistentes cuando una nueva asociación se crea sin enviar ambos campos opcionales del vínculo.

#### Scenario: Default de `Ponderacion`

- GIVEN un `PUT` válido que omite `Ponderacion`
- WHEN el sistema crea una nueva asociación `CargoHabilidad`
- THEN MUST asignar `Ponderacion = 1.00`
- AND la lectura posterior MUST devolver `1.00` como valor persistido.

#### Scenario: Default de `EsObligatoria`

- GIVEN un `PUT` válido que omite `EsObligatoria`
- WHEN el sistema crea una nueva asociación `CargoHabilidad`
- THEN MUST asignar `EsObligatoria = false`
- AND la lectura posterior MUST devolver `false` como valor persistido.

### Requirement 2: `Ponderacion` positiva y precisa

El sistema MUST aceptar `Ponderacion` solo cuando sea mayor a cero y con precisión máxima de dos decimales.

#### Scenario: `Ponderacion = 0` no vuelve opcional la habilidad

- GIVEN un payload con `Ponderacion = 0`
- WHEN un `Administrador` intenta guardar la asociación
- THEN el sistema MUST rechazar la solicitud como inválida
- AND MUST NOT reinterpretar ese valor como “habilidad opcional”.

#### Scenario: Valor decimal válido

- GIVEN un payload con `Ponderacion = 2.50`
- WHEN un `Administrador` guarda la asociación
- THEN el sistema MUST aceptar el valor exacto enviado
- AND la lectura posterior MUST conservar `2.50`.

### Requirement 3: Reflexión fiel en respuestas de lectura

El sistema MUST exponer `Ponderacion` y `EsObligatoria` en el contrato de lectura del subrecurso para que la UI no derive ni reconstruya esos datos por fuera del backend.

#### Scenario: Habilidad obligatoria visible en la consulta

- GIVEN una asociación guardada con `EsObligatoria = true`
- WHEN un cliente consulta `GET /api/v1/cargos/{cargoId}/skills`
- THEN cada item correspondiente MUST incluir `EsObligatoria = true`
- AND MUST mantener también sus datos de `Habilidad` y `Nivel` asociados.

#### Scenario: Habilidad no obligatoria visible en la consulta

- GIVEN una asociación guardada con `EsObligatoria = false`
- WHEN un cliente consulta el subrecurso
- THEN el item MUST incluir `EsObligatoria = false`
- AND MUST incluir el valor real de `Ponderacion` persistido.

### Requirement 4: Sin redondeo implícito

El sistema MUST rechazar una `Ponderacion` con más de dos decimales en vez de modificar silenciosamente el dato ingresado.

#### Scenario: Precisión excedida

- GIVEN un payload con `Ponderacion = 1.257`
- WHEN un `Administrador` ejecuta `PUT`
- THEN la solicitud MUST fallar con error de validación
- AND la respuesta MUST permitir corregir el campo sin ambigüedad.

> **Nota**: el backend verificado hoy garantiza `Ponderacion > 0` y precisión `decimal(5,2)`. El tope funcional `100.00` queda definido por este cambio para el flujo editable y se valida **únicamente en la capa de aplicación** (FluentValidation / servicio); NO se introduce un `CHECK` constraint en la base de datos. Tampoco se introduce soft delete ni se modifica el modelado de `CargoHabilidad`.
