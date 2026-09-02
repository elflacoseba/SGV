# H-02-11 — Ejecutar solo los tests de un módulo

La suite completa tarda varios minutos. Para iterar sobre un módulo (Personas, Vacantes, Auth, Auditoría, etc.), filtrá por namespace o por atributo. xUnit 2.9.2 acepta filtros por FullyQualifiedName, DisplayName y atributos personalizados.

---

## Prerrequisitos

- SDK .NET 10 (mismo de `global.json`).
- Suite compilada (`dotnet build SGV.slnx` o `dotnet test` previo).
- Para tests `[MySqlFact]`: MySQL local accesible (ver `../how-to/07-levantar-mysql-docker-para-tests.md`).

---

## Paso 1 — Filtrar por namespace

La sintaxis canónica es `--filter "FullyQualifiedName~<texto>"` (operador `~` = contains):

```bash
dotnet test SGV.slnx \
  --filter "FullyQualifiedName~SGV.Tests.Vacantes"
```

**Verificación:** el resumen lista sólo los tests cuyo FullyQualifiedName contiene `SGV.Tests.Vacantes`. El resto de los assemblies ni se enumeran.

---

## Paso 2 — Filtrar por clase

Bajá la granularidad a una clase puntual:

```bash
dotnet test SGV.slnx \
  --filter "FullyQualifiedName~OcupacionRepositoryQueryAsyncTests"
```

**Verificación:** corre sólo los métodos de esa clase.

---

## Paso 3 — Filtrar por DisplayName

xUnit también matchea por `DisplayName~` (lo que ves en el reporte, sin namespace):

```bash
dotnet test SGV.slnx \
  --filter "DisplayName~Deberia_reactivar_cuando_esta_eliminado"
```

Combiná con OR lógico: `"DisplayName~A|FullyQualifiedName~B"`.

---

## Paso 4 — Filtrar por atributo (sólo o excluyendo MySQL)

```bash
# SOLO los tests de persistencia (requieren MySQL)
dotnet test SGV.slnx \
  --filter "FullyQualifiedName~MySqlFact|FullyQualifiedName~MySqlTheory|FullyQualifiedName~Tests.Persistencia"

# EXCLUIR los tests de persistencia (suite rápida en dev)
dotnet test SGV.slnx \
  --filter "FullyQualifiedName!~MySqlFact&FullyQualifiedName!~MySqlTheory"
```

**Verificación:** el primer filtro deja sólo tests que contactan MySQL (sin MySQL, todos van a `Skipped`). El segundo deja la suite pura sin dependencias externas.

---

## Paso 5 — Filtrar por Trait (cuando exista)

El repo no usa `[Trait("Category", "Integration")]` sistemáticamente. Si lo necesitás, filtrá con `Category=Integration` o `Category!=Smoke`.

> ⚠️ A verificar: si ningún test declara `[Trait("Category", "Smoke")]`, el filtro es no-op. Si lo necesitás, agregá `[Trait]` a los tests que quieras marcar.

---

## Paso 6 — Tener en cuenta el paralelismo

`tests/SGV.Tests/xunit.runner.json` declara `parallelizeAssembly=false`, `parallelizeTestCollections=true`, `maxParallelThreads=4`. Las clases dentro de un assembly corren en paralelo (hasta 4 hilos), pero los assemblies no se mezclan entre sí. Tests en la misma `[Collection("MySqlIntegration")]` se serializan para no chocar contra el schema compartido de `sgv_test`.

**Verificación:** en el resumen, los tests serializados aparecen con tiempos que se solapan a nivel de proceso pero no de thread. Si ves errores intermitentes de "table doesn't exist" entre tests de la misma collection, agregalos a la misma `[Collection]` o corré `--blame-hang-timeout 60s` para detectar el que traba.

---

## Paso 7 — Reporte de cobertura puntual

```bash
dotnet test SGV.slnx \
  --filter "FullyQualifiedName~SGV.Tests.Vacantes" \
  --collect:"XPlat Code Coverage"
```

**Verificación:** el `coverage.cobertura.xml` cubre únicamente los assemblies ejecutados por el filtro. Útil para revisar cobertura de un módulo específico después de un cambio.

---

## Troubleshooting

- **`No test matches the given testcase filter`**: el namespace o DisplayName no existe. Verificá con `dotnet test SGV.slnx --list-tests --filter "FullyQualifiedName~SGV.Tests.Vacantes"` que el filtro matchee algo.
- **`[MySqlFact]` aparecen como `Skipped`**: MySQL no está accesible. Exportá `ConnectionStrings__SgvDatabase` o levantá el contenedor como indica el how-to 07.
- **`Category!=X` no excluye nada**: el atributo `Trait` no está declarado. Cambiá a `DisplayName~` o agregá `[Trait]`.
- **`MySqlException: Table 'X' doesn't exist` intermitente**: tests en paralelo escribiendo sobre la misma tabla sin colección. Mirá las definiciones `[Collection(...)]` en `tests/SGV.Tests/Persistencia/`.

---

## Referencias

- `tests/SGV.Tests/xunit.runner.json` — paralelismo.
- `tests/SGV.Tests/Persistencia/MySqlFactAttribute.cs` — skip con caché.
- `openspec/config.yaml` § `testing` — runner, comando, capas.
- `../tutorials/03-correr-suite-tests.md` — flujo end-to-end con y sin MySQL.
- `../how-to/07-levantar-mysql-docker-para-tests.md` — cómo correr la suite completa.
