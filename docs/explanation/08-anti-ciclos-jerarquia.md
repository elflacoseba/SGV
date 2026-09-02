# Anti-ciclos en la jerarquía: trigger MySQL + diagnóstico de arranque

## El problema que el código de aplicación no alcanza a cerrar

Una UnidadOrganizativa apunta a su padre vía `UnidadPadreId`. Esa
relación puede formar un ciclo: si A es padre de B y B es padre de A,
la jerarquía deja de ser un árbol y se vuelve un grafo con
componente fuertemente conexo. Cualquier algoritmo que asume
"acíclico" — recálculo de organigrama, detección de profundidad,
listado de "antecesores de X" — puede entrar en loop infinito o
devolver resultados inconsistentes.

La defensa lógica tiene tres capas, documentada en
`docs/decisiones-implementacion.md §D-UO-1`:

**Capa de dominio.** `UnidadOrganizativa.Actualizar` y
`CambiarUnidadPadre` rechazan self-parent (`InvalidOperationException`)
con el mensaje "Una unidad organizativa no puede ser padre de sí
misma". El check es trivial pero protege el caso A.Id == B.Id que
sería el más común por error de UI.

**Capa de aplicación.** `UnidadOrganizativaServicioComandos.ActualizarAsync`
y `CambiarUnidadPadreAsync` consultan
`IUnidadOrganizativaRepository.IsDescendantAsync` con un visited-set
local antes de persistir. Si la cadena del candidato es descendiente
del padre propuesto, o si la caminata revisa un nodo ya visitado, el
servicio devuelve `Conflict "CicloJerarquico"` (HTTP 409). Esta capa
cubre los ciclos transitivos construidos en operaciones concurrentes
porque ambas validaciones ejecutan `IsDescendantAsync` antes del
`SaveChanges`.

**Capa de persistencia.** La migración
`20260816203122_AddTriggerAntiCiclosUnidadesOrganizativas` crea dos
triggers `BEFORE INSERT` y `BEFORE UPDATE` sobre la tabla. Cada
trigger usa una CTE recursiva que recorre la cadena de padres
partiendo del candidato y emite `SIGNAL SQLSTATE '45000' SET
MESSAGE_TEXT = 'CicloJerarquico'` si la cadena cierra sobre sí misma.
MySQL trata el SIGNAL como error 1644, que
`MySqlConstraintViolationDetector` reconoce y traduce al código
canónico `UnidadOrganizativaErrorCodigos.CicloJerarquico`. El contrato
HTTP siempre es 409, nunca 500.

## Por qué necesitamos el trigger aunque tengamos la aplicación

Confiar sólo en la capa de aplicación abre tres vectores de fallo:

**Migraciones de datos legados.** Si en algún momento se importa una
tabla `UnidadesOrganizativas` desde un sistema anterior con ciclos
pre-existentes, la validación de aplicación no se ejecuta — la
importación suele usar `ExecuteSqlRaw` o un seeder que pasa por alto
el dominio. Los triggers cortan el problema a nivel de INSERT/UPDATE,
independientemente del camino que use el script.

**Triggers deshabilitados.** Un operador podría deshabilitar los
triggers temporalmente para sembrar datos que legítimamente
reorganizan la jerarquía (reparación operativa). Sin diagnóstico de
arranque, esa deshabilitación es invisible hasta el próximo
`SaveChanges` que intente cerrar un ciclo — y ahí ya no hay defensa.
El diagnóstico expuesto en `GET /api/v1/unidades-organizativas/diagnostico-jerarquia`
reporta los ciclos pre-existentes, pero el log de arranque es el que
le avisa al operador que los triggers están deshabilitados (o que
existen ciclos que se colaron antes de la defensa).

**`EnsureCreated()` en lugar de `Database.Migrate()`.** Si un
ambiente se levanta con `EnsureCreated`, los triggers NO se crean —
sólo existen las tablas y los índices del modelo EF. La capa de
aplicación protege el camino crítico, pero los ciclos que vengan por
rutas no instrumentadas no tienen red de seguridad. El log de
arranque explícita este riesgo al comparar lo que el EF espera contra
lo que MySQL tiene.

## El CTE recursivo y su límite de profundidad

El CTE dentro del trigger tiene la forma:

```sql
WITH RECURSIVE padre_chain (id, depth) AS (
  SELECT NEW.UnidadPadreId, 0
  UNION ALL
  SELECT u.UnidadPadreId, p.depth + 1
  FROM UnidadesOrganizativas u
  INNER JOIN padre_chain p ON u.Id = p.id
  WHERE u.IsDeleted = 0 AND p.depth < 32
)
SELECT COUNT(*) INTO @sgv_ciclo_count FROM padre_chain WHERE id = NEW.Id;
```

El `WHERE p.depth < 32` acota la recursión al límite que MySQL
permite para CTEs recursivas. En una jerarquía con más de 32 niveles,
el trigger cortaría antes de cerrar el ciclo y el `INSERT`/`UPDATE`
pasaría. En la práctica una organización con 32 niveles de
sub-unidades es una rareza, pero el número está implícito en el
código de la migración y debe revisarse si se migra a otro motor o
se sube el límite. La consecuencia operativa: la defensa es válida
para organizaciones "normales" pero no es matemáticamente completa
para jerarquías extremas. La capa de aplicación con `IsDescendantAsync`
es la red que cubre ese caso (es O(depth) sobre la base completa, sin
límite artificial).

## El diagnóstico de arranque

`SGV.Api/Program.cs` registra un callback `ApplicationStarted` que
corre `IDiagnosticoJerarquiaService.DiagnosticarAsync` después del
arranque. La implementación vive en
`src/SGV.Infraestructura/Organizacion/DiagnosticoJerarquiaService.cs`
y hace una pasada O(N²) sobre las unidades activas para detectar
back-edges en el grafo dirigido de padres.

Si no hay ciclos, se emite un `LogInformation` y nada más. Si hay
ciclos, cada uno se loguea como `LogWarning` con la lista de IDs
participantes:

```
SGV: ciclo jerárquico detectado en UnidadesOrganizativas. Nodos participantes: <id1>, <id2>, ...
```

El diagnóstico **no aborta el startup** por construcción. La
justificación es operativa: si la corrección requiere intervención
manual, el operador debe poder llegar al endpoint
`/api/v1/unidades-organizativas/diagnostico-jerarquia` para ver
qué nodos están involucrados. Si el host no arrancara, nadie podría
ejecutar el endpoint. La consecuencia es que el sistema sigue
aceptando tráfico con un grafo cíclico; los triggers siguen
rechazando cualquier mutación que cierre un nuevo ciclo, pero las
filas que ya están en ciclo permanecen hasta corrección manual.

## Qué hacer cuando aparece el WARNING

El operador tiene tres caminos disponibles, en orden de preferencia.

**Reorganizar las filas involucradas.** Abrir `GET
/api/v1/unidades-organizativas/diagnostico-jerarquia` (rol
Administrador) y leer la lista de nodos. Para cada par `padre →
hijo` que cierra el ciclo, decidir cuál debe apuntar a `NULL`
(`UnidadPadreId = NULL`) o a un ancestro válido. La corrección se
hace vía `PATCH /api/v1/unidades-organizativas/{id}` con el campo
corregido.

**Correr el script utilitario.** El archivo
`docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` es
un script standalone que aplica un CTE recursivo similar al del
trigger pero como `SELECT` reportable. Útil cuando el operador
quiere ver los ciclos sin tener la API arriba, o cuando el diagnóstico
se ejecuta contra un backup restaurado en otro ambiente.

**Evaluar si los triggers deben ser deshabilitados temporalmente.**
Esto es válido únicamente cuando hay una migración masiva legítima
que necesita pasar por alto la defensa. La operación se documenta
como rotación manual de triggers y debe ir acompañada de un runbook
que recuerde re-habilitarlos antes de cerrar el cambio.

## Trade-offs y consecuencias operativas

El trigger agrega un costo fijo por INSERT/UPDATE sobre
`UnidadesOrganizativas`. La CTE recursiva es lineal en la profundidad
del grafo, pero el WHERE filtra por `IsDeleted = 0` así que las
filas soft-deleted no se incluyen en la caminata. El costo se vuelve
visible sólo con jerarquías muy profundas o tasas de mutación muy
altas, ninguno de los cuales es característico del uso actual.

La dependencia MySQL-only es la consecuencia más seria. Los triggers
anti-ciclos no son portables a SQL Server o PostgreSQL. La capa de
aplicación es portable y protege el camino crítico, pero la defensa
contra ciclos en datos legados importados sólo existe en MySQL. Un
proyecto que migre a otro motor debe documentar una defensa
equivalente (por ejemplo, una función `CHECK` con CTE recursivo o un
job batch que recorra la tabla).

El log de arranque se ejecuta en su propio scope manual porque
`IDiagnosticoJerarquiaService` depende de `SgvDbContext` (scoped) y
la invocación ocurre fuera del request pipeline. Esta indirección
cuesta legibilidad del código pero garantiza que un fallo en la
conexión a MySQL durante el diagnóstico no aborte el arranque del
host.

## Referencias

- `../how-to/01-diagnosticar-ciclos-jerarquia.md` — pasos operativos para leer y resolver un WARNING de diagnóstico.
- `../reference/02-esquema-base-de-datos.md` — definición completa de los triggers y sus índices.
- `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` — script utilitario standalone.
- `docs/decisiones-implementacion.md` — sección "Módulo de Unidades Organizativas + Organigrama — defensa contra ciclos y dependencias MySQL-only" (decisiones D-UO-1 a D-UO-4).
- `openspec/changes/archive/2026-08-16-fix-unidad-organizativa-ciclo-jerarquia-277/` — artefactos SDD completos del change que introdujo los triggers.