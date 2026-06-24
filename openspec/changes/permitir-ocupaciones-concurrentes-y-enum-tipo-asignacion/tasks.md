## 1. Dominio y contratos del enum

- [x] 1.1 RED agregar/actualizar pruebas de dominio para `Ocupacion` cubriendo `TipoAsignacion` tipado, fechas inválidas y conservación de vigencia al finalizar.
- [x] 1.2 GREEN crear el enum `TipoAsignacion` con valores numéricos explícitos (`Permanente`, `Interina`, `Temporal`) y actualizar `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` para usarlo en constructor y propiedad.
- [x] 1.3 REFACTOR ajustar nombres, mensajes y helpers de dominio afectados por el cambio a enum sin alterar comportamiento adicional.

## 2. Persistencia EF y unicidad activa

- [x] 2.1 RED ampliar `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` para verificar persistencia numérica de `TipoAsignacion`, mantenimiento de unicidad activa por `Puesto`, eliminación de unicidad activa global por `Persona` y nueva unicidad activa por `Persona + Puesto`.
- [x] 2.2 GREEN actualizar `src/SGV.Infraestructura/Persistencia/Entidades/OcupacionEntity.cs` y `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` para persistir el enum como entero, conservar `ActivePuestoIdUnique` y reemplazar `ActivePersonaIdUnique` por una clave activa única para `Persona + Puesto`.
- [x] 2.3 REFACTOR revisar el tipo de columna/propiedades sombra generado para la nueva unicidad activa y simplificar la configuración EF sin perder compatibilidad con Pomelo/MySQL.

## 3. Migración y snapshot

- [x] 3.1 RED agregar o actualizar pruebas de modelo/migración que fallen si `TipoAsignacion` sigue siendo textual o si la estrategia de unicidad activa no coincide con el nuevo contrato.
- [x] 3.2 GREEN generar la migración EF Core correspondiente y actualizar snapshot para convertir `TipoAsignacion` a tipo numérico, mapear los valores legacy conocidos y recrear los índices/columnas generadas de `Ocupaciones` según el nuevo diseño.
- [x] 3.3 REFACTOR inspeccionar el código de migración para asegurar fallo explícito ante valores legacy desconocidos y eliminar ruido innecesario del diff.

## 4. Verificación integral

- [x] 4.1 Ejecutar la suite relevante de pruebas de dominio y persistencia para demostrar concurrencia válida por Persona en Puestos distintos, rechazo de duplicado activo por `Persona + Puesto`, rechazo de doble ocupación activa por `Puesto` y persistencia numérica del enum.
- [x] 4.2 Validar que los artefactos SDD queden alineados con la implementación final y documentar cualquier decisión técnica menor descubierta durante la ejecución.
