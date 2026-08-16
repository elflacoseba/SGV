-- =============================================================================
-- Script: docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql
--
-- Issue #277 — Defensa operativa complementaria al trigger MySQL.
--
-- Propósito:
--   LISTAR los ciclos transitivos existentes en la jerarquía activa de
--   `UnidadesOrganizativas`. READ-ONLY: no modifica filas. Pensado para
--   uso operativo por un DBA cuando el diagnóstico de arranque reporte
--   WARNING, o como verificación previa al deploy del trigger.
--
-- Compatibilidad:
--   MySQL 8.0+. MariaDB 10.6+ también soporta CTE recursivas; si el
--   servidor destino es MariaDB anterior ajustar collation/separador.
--
-- Idempotencia:
--   Script READ-ONLY. Puede ejecutarse varias veces sin riesgo.
--
-- Salida:
--   Una fila por cada nodo participante en cualquier ciclo. Cada fila
--   incluye el path completo del ciclo en formato "A -> B -> A" para
--   que el operador identifique la cadena. Use SELECT DISTINCT o
--   agregación externa si quiere un resumen por ciclo.
-- =============================================================================

WITH RECURSIVE padre_walk (
    current_id,
    padre_id,
    profundidad,
    camino,
    es_ciclo
) AS (
    -- Anchor: cada unidad activa y no borrada es punto de partida.
    SELECT
        u.Id,
        u.UnidadPadreId,
        0,
        CAST(u.Id AS CHAR(36)),
        0
    FROM UnidadesOrganizativas u
    WHERE u.IsActive = 1 AND u.IsDeleted = 0

    UNION ALL

    -- Step: caminamos al padre, agregando al path. Si el padre ya
    -- aparece en el path, hemos encontrado un ciclo. Limitamos la
    -- profundidad a 32 para garantizar terminación incluso con datos
    -- patológicos.
    SELECT
        w.current_id,
        p.UnidadPadreId,
        w.profundidad + 1,
        CONCAT(w.camino, ' -> ', CAST(p.Id AS CHAR(36))),
        CASE
            WHEN p.Id = w.current_id THEN 1
            WHEN w.camino LIKE CONCAT('% -> ', CAST(p.Id AS CHAR(36))) THEN 1
            ELSE 0
        END
    FROM padre_walk w
    JOIN UnidadesOrganizativas p
        ON p.Id = w.padre_id
    WHERE w.es_ciclo = 0
      AND w.profundidad < 32
      AND p.IsActive = 1 AND p.IsDeleted = 0
)
SELECT
    pw.current_id                                   AS NodoOrigenDelCiclo,
    SUBSTRING_INDEX(pw.camino, ' -> ', 1)            AS PrimerNodoDelCiclo,
    pw.camino                                       AS CaminoConCierre,
    (CHAR_LENGTH(pw.camino) - CHAR_LENGTH(REPLACE(pw.camino, ' -> ', '')) / 4)
                                                    AS CantidadNodos
FROM padre_walk pw
WHERE pw.es_ciclo = 1
ORDER BY pw.current_id, pw.camino;

-- =============================================================================
-- INSTRUCCIONES PARA EL OPERADOR:
--
--   1) Si la consulta retorna 0 filas: la jerarquía está limpia.
--
--   2) Si retorna filas:
--      - Cada fila pertenece a un ciclo; varias filas pueden
--        corresponder al mismo ciclo (uno por cada nodo participante).
--      - La columna CaminoConCierre muestra el camino en formato
--        A -> B -> ... -> A. El último nodo es siempre el cierre.
--      - NO ejecute ningún UPDATE/DELETE desde este script.
--
--   3) Remediación:
--      - Portal admin (PATCH
--        /api/v1/unidades-organizativas/{id}/unidad-padre) para
--        reasignar el padre y romper la cadena.
--      - O aplicar manualmente un UPDATE con un padre válido
--        después de inspeccionar las dependencias.
-- =============================================================================
