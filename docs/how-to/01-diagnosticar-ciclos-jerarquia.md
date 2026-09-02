# H-02-01 — Diagnosticar ciclos en la jerarquía de unidades organizativas

Un WARNING como `SGV: ciclo jerárquico detectado en UnidadesOrganizativas. Nodos participantes: …` aparece al arrancar `SGV.Api` cuando la columna `UnidadPadreId` de `UnidadesOrganizativas` forma un back-edge. El diagnóstico no aborta el proceso (es informativo), pero rompe la garantía anti-ciclos del trigger MySQL `trg_UnidadesOrganizativas_BeforeInsert_Ciclo` (issue #277).

---

## Prerrequisitos

- Acceso al log de arranque de `SGV.Api` (consola o `journalctl`).
- Credenciales MySQL con permiso `SELECT` sobre `UnidadesOrganizativas` y `UPDATE` para la remediación.
- Haber leído `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` (es el script operativo que vas a correr).

---

## Paso 1 — Ubicar el WARNING en el log

El diagnóstico se dispara desde `app.Lifetime.ApplicationStarted` en `src/SGV.Api/Program.cs` (líneas ~420-465) y delega a `IDiagnosticoJerarquiaService.DiagnosticarAsync()`. La implementación vive en `src/SGV.Infraestructura/Organizacion/DiagnosticoJerarquiaService.cs`.

```bash
# Filtrar los ciclos detectados al arranque
grep "ciclo jerárquico detectado" /var/log/sgv/api.log
```

**Verificación:** la línea contiene `Nodos participantes:` seguido de GUIDs separados por coma. Cada GUID es un nodo de un ciclo distinto.

---

## Paso 2 — Listar los ciclos completos con el script operativo

El script `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` recorre la jerarquía activa con una CTE recursiva y devuelve una fila por cada nodo participante en cualquier ciclo (MySQL 8.0+, MariaDB 10.6+).

```bash
mysql -uroot -p sgv < docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql
```

**Verificación:** la salida incluye columnas `NodoOrigenDelCiclo`, `PrimerNodoDelCiclo`, `CaminoConCierre` (formato `A -> B -> A`) y `CantidadNodos`. Si retorna 0 filas, la jerarquía está limpia. Varias filas con el mismo `CaminoConCierre` corresponden al mismo ciclo visto desde cada nodo participante.

---

## Paso 3 — Romper el ciclo reasignando el padre

Dos caminos soportados:

1. **Portal admin:** `PATCH /api/v1/unidades-organizativas/{id}/unidad-padre` con un `padreId` válido que no pertenezca al camino del ciclo. El trigger rechaza cualquier reasignación que forme un ciclo nuevo con error `CicloJerarquico` (HTTP 409).
2. **SQL manual (sólo si el portal no resuelve):** `UPDATE UnidadesOrganizativas SET UnidadPadreId = '<guid-valido>' WHERE Id = '<guid-nodo-a-cortar>';`

**Verificación:** tras el cambio, ejecutá el script del Paso 2 otra vez. La fila correspondiente al ciclo reparado ya no aparece. Los nodos siguen en la tabla, sólo cambió el `UnidadPadreId`.

---

## Paso 4 — Confirmar que el próximo arranque queda limpio

Reiniciá `SGV.Api` y repetí el `grep` del Paso 1. La nueva línea de log debe ser:

```
SGV: diagnóstico de jerarquía OK (sin ciclos detectados en UnidadesOrganizativas).
```

**Verificación:** ningún WARNING nuevo; las mutaciones sobre `UnidadesOrganizativas` vuelven a pasar por el trigger sin chocar contra `SIGNAL SQLSTATE '45000'`.

---

## Troubleshooting

- **El diagnóstico falla y se loggea como warning** (`el diagnóstico de jerarquía falló en el arranque y fue ignorado`): MySQL no responde al `AnyAsync` inicial. El arranque continúa; revisá `ConnectionStrings__SgvDatabase` y la disponibilidad del servidor antes de operar.
- **El script devuelve error de sintaxis en MariaDB < 10.6**: la CTE recursiva no está disponible. Actualizá el servidor o adaptá el script a una consulta con `WHILE` / tabla temporal.

---

## Referencias

- `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` — script de lectura con instrucciones operativas.
- `src/SGV.Infraestructura/Organizacion/DiagnosticoJerarquiaService.cs` — implementación del detector.
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260816203122_AddTriggerAntiCiclosUnidadesOrganizativas.cs` — trigger de defensa.
- `../tutorials/02-primera-mutacion-unidad-organizativa.md` — flujo de mutación end-to-end.
- [E-04-08](../explanation/08-anti-ciclos-jerarquia.md) — Explanation del
  trigger anti-ciclos y la CTE recursiva.
