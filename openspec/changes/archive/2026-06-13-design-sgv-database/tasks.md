# Tareas

## 1. Validación de Planificación

- [ ] Revisar el diseño de base de datos con referentes de RR. HH. y administración organizacional.
- [ ] Confirmar si los IDs de usuarios de Identity se mantendrán como strings o se personalizarán a GUID.
- [ ] Confirmar si una persona puede ocupar múltiples puestos activos en la versión 1.0.
- [ ] Confirmar si los postulantes externos requieren habilidades estructuradas en la versión 1.0.

## 2. Modelo de Dominio

- [x] Crear entidades de dominio para unidades organizativas, cargos, habilidades, puestos, personas, ocupaciones, vacantes, postulantes, postulaciones, evaluaciones y auditoría.
- [x] Agregar restricciones de valor para nombres, códigos, fechas, estados y rangos de puntaje.
- [x] Modelar relaciones muchos a muchos con entidades de unión explícitas cuando existan atributos.

## 3. Modelo de Persistencia

- [x] Configurar mapeos de EF Core para SQL Server con claves primarias GUID.
- [x] Configurar convenciones de auditoría y baja lógica.
- [x] Configurar relaciones autorreferenciadas para unidades organizativas y puestos.
- [x] Configurar índices únicos filtrados para códigos activos, ocupaciones activas, legajos y correos.
- [x] Configurar restricciones check para fechas, ponderaciones, niveles y puntajes.

## 4. Migraciones

- [x] Crear migraciones en orden de dependencia: Identity, catálogos, estructura, cargos y habilidades, personas, puestos, ocupaciones, vacantes, postulantes, selección y auditoría.
- [x] Agregar datos semilla para estados, niveles de habilidad, roles base, tipos de unidad, cargos y habilidades.
- [x] Verificar la generación de migraciones contra SQL Server.

## 5. Lógica de Compatibilidad

- [x] Implementar servicio para calcular compatibilidad entre habilidades requeridas por el cargo y habilidades de la persona.
- [x] Persistir snapshots de compatibilidad en postulaciones o evaluaciones.
- [x] Agregar pruebas para coincidencia total, parcial, insuficiente, habilidad obligatoria faltante y cálculo ponderado.

## 6. Auditoría

- [x] Implementar auditoría reutilizable mediante interceptor de EF Core o pipeline de guardado.
- [x] Capturar usuario, fecha y hora, operación, entidad, valores anteriores, valores nuevos, propiedades modificadas y correlation ID.
- [x] Excluir campos sensibles de Identity del JSON de auditoría.

## 7. Verificación

- [x] Agregar pruebas unitarias para reglas de dominio.
- [x] Agregar pruebas de persistencia para índices, restricciones y relaciones.
- [x] Agregar prueba smoke de migraciones contra SQL Server o contenedor SQL Server.
- [x] Documentar decisiones de implementación que difieran de este diseño.
