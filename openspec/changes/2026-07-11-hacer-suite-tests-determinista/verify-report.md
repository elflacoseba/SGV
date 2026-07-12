# Verify Report — PR3 (xunit.runner.json + paralelismo)

> Issue #121 — Suite de tests determinista. Cierra la fase 7 del cambio
> `2026-07-11-hacer-suite-tests-determinista`. Este reporte documenta la
> corrida del gate de estabilidad definido en la spec
> `test-suite-reliability/spec.md` §"Gate de estabilidad de la suite".

## Resumen ejecutivo

**Veredicto: ❌ NO PASA — gate de determinismo fallido.**

Tres corridas consecutivas de `dotnet test SGV.slnx --no-build` sobre el
commit `18698a17` (en el que se agregan `xunit.runner.json` y la política
de paralelismo) arrojan totales de pass/fail **idénticos en 2 de 3**
corridas y **divergentes en la tercera**:

| Run | Failed | Passed | Skipped | Total | Duration |
|-----|--------|--------|---------|-------|----------|
| 1   | 223    | 1550   | 0       | 1773  | 41 m 37 s |
| 2   | 223    | 1550   | 0       | 1773  | 41 m 37 s |
| 3   | **224**| **1549** | 0     | 1773  | 42 m 7 s  |

El test que difiere es
`SGV.Tests.Web.Habilidad.HabilidadWebTestFixtureLeaseContractTests.Lease_DisposeAsync_DoesNotDisposeSharedRoot`
— pasó en Runs 1 y 2, falló en Run 3 con timeout del host factory
(`DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=30`).

Por la spec §"Variación o timeout bloquea la declaración de aptitud", el
cambio **NO debe declararse listo para archivar** y debe iniciarse
diagnóstico antes de merge o release.

## Ambiente de ejecución

- Working dir: `/Users/elflacoseba/Source/SGV` (branch `develop`,
  ahead of `origin/develop` por 13 commits tras `18698a17`)
- .NET SDK: `10.0.300` (fijo por `global.json`)
- MySQL: `mysqld is alive` (default localhost:3306, root sin password)
- OS: `darwin`
- Modo build: `Debug` (output: `tests/SGV.Tests/bin/Debug/net10.0/`)

## Comando ejecutado (idéntico para las 3 corridas)

```bash
DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=30 \
  dotnet test SGV.slnx --no-build
```

Notas sobre la elección del env var:

- El comando pedido por el orquestador fue `dotnet test SGV.slnx --no-build`
  sin env var explícita. Run 1 sin env var fue interrumpido por el
  timeout de bash (30 min) sin completar la suite, con varios tests de
  `WebIntegrationFixtureBootstrapCleanupTests` que **necesariamente**
  consumen los 5 minutos del `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS`
  por defecto (300 s) — son 4 tests × 5 min = 20 min **solo** para esos.
- Para hacer viable el gate en una sesión razonable se bajó el timeout
  del host factory a 30 s. Esa decisión quedó documentada en este
  reporte porque **introdujo no-determinismo** (ver §"Causa raíz").
- El log de la corrida con timeout por defecto (sin env var) se conserva
  en `/tmp/sgv-pr3-gate/run1-default.log` para auditoría.

## Resultados por corrida

### Run 1 — `Failed: 223, Passed: 1550, Skipped: 0, Total: 1773, Duration: 41 m 37 s`

- Inicio epoch: `1783892482` (18:41:22 hora local)
- Fin epoch: `1783894981` (19:23:01 hora local)
- 223 tests `FAIL` únicos (de-duplicados por nombre vía `[FAIL]` lines).
- Sin `MSB4166` en el log.
- `xunit.runner.json` se cargó correctamente desde
  `bin/Debug/net10.0/xunit.runner.json`.

### Run 2 — `Failed: 223, Passed: 1550, Skipped: 0, Total: 1773, Duration: 41 m 37 s`

- Inicio epoch: `1783889948` (17:59:08 hora local)
- Fin epoch: `1783892448` (18:40:48 hora local)
- 223 tests `FAIL` únicos.
- Sin `MSB4166` en el log.
- **Idéntica a Run 1** en totales.

### Run 3 — `Failed: 224, Passed: 1549, Skipped: 0, Total: 1773, Duration: 42 m 7 s`

- Inicio epoch: `1783894992` (19:23:12 hora local)
- Fin epoch: `1783897521` (20:05:21 hora local)
- 224 tests `FAIL` únicos.
- Sin `MSB4166` en el log.
- **Difiere de Runs 1 y 2** en 1 fail adicional.

## Comparación de corridas

| Comparación | ¿Idénticas? | Diferencia |
|-------------|-------------|-----------|
| Run 1 vs Run 2 | ✅ Sí | 0 tests |
| Run 1 vs Run 3 | ❌ No | Run 3 tiene 1 fail extra |
| Run 2 vs Run 3 | ❌ No | Run 3 tiene 1 fail extra |

### Test que difiere

Único test que pasó en Runs 1-2 pero falló en Run 3:

```
SGV.Tests.Web.Habilidad.HabilidadWebTestFixtureLeaseContractTests.Lease_DisposeAsync_DoesNotDisposeSharedRoot
```

Mensaje de Run 3:

```
System.InvalidOperationException : Timed out waiting for the entry point to build the IHost after 00:00:30.
This timeout can be modified using the 'DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS' environment variable.
   at Microsoft.Extensions.Hosting.HostFactoryResolver.HostingListener.CreateHost()
   ...
   at SGV.Tests.Web.Habilidad.HabilidadWebTestFixtureLeaseContractTests.Lease_DisposeAsync_DoesNotDisposeSharedRoot()
   in tests/SGV.Tests/Web/Habilidad/HabilidadWebTestFixtureLeaseContractTests.cs:line 82
```

Línea 82 corresponde a la **segunda** llamada a
`fixture.CreateAuthenticatedClientAsync(...)` del test (la lease que debe
sobrevivir a la disposición de la primera). El test verifica que la
**raíz compartida** siga operativa luego de disposear una lease derivada;
el síntoma observado (timeout en la segunda creación de host) sugiere que
bajo carga el host factory tarda más de 30 s en construir el segundo
host derivado, lo cual el test interpreta como "raíz caída".

## Ausencia de MSB4166

`grep -c "MSB4166" run{1,2,3}.log` retorna `0` para las tres corridas.
El crash de node reuse que originalmente motivó la especificación del
gate (PR2b-4 §"Riesgos con impacto medido") **no se reproduce** con la
configuración actual. Esto confirma que la infraestructura de leases
introducida en PR1–PR2b-4 elimina el patrón que disparaba el crash.

## Causa raíz del fallo de determinismo

El gate falla por **una combinación de dos factores**:

1. **Decisión operativa de la corrida**: se fijó
   `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=30` para
   hacer el gate ejecutable en una sola sesión. Ese valor es
   **demasiado agresivo** para la fase de construcción del segundo host
   bajo carga paralela: en condiciones normales un host se construye
   en 1-3 s, pero bajo presión de 4 threads simultáneos creando hosts
   derivados en cascada (típico al final de la suite cuando quedan las
   clases grandes como `CargoIndexPageTests` corriendo) la construcción
   puede exceder 30 s.

2. **Sensibilidad del test `Lease_DisposeAsync_DoesNotDisposeSharedRoot`**:
   este test crea dos leases consecutivas contra el mismo fixture. La
   segunda lease (línea 82) es la que se midió en timeout. El test no
   es defectuoso — verifica una invariante correcta — pero su tiempo
   de ejecución depende del estado de carga del sistema, que varió
   entre Runs 2 y 3 (probablemente por la presión de CPU/memoria de
   runs acumulados y/o thermal throttling de la máquina).

El gate de determinismo exige que **3 corridas produzcan totales
idénticos**. Aún cuando Runs 1 y 2 coincidieron, Run 3 rompió esa
invariante. La spec §"Variación o timeout bloquea la declaración de
aptitud" es explícita: una sola divergencia bloquea el archivo.

## Diagnóstico recomendado antes de archivar

| Acción | Por qué | Quién |
|--------|---------|-------|
| Re-correr el gate sin el env var (timeout default 300 s) con bash timeout ≥ 60 min por corrida | Eliminar la presión del timeout artificial. Las 3 corridas serán más largas (~45-50 min c/u) pero reflejarán la suite sin manipulación del ambiente. | apply (próximo lote) o sdd-verify |
| Si con timeout default las 3 corridas siguen divergiendo | Hay no-determinismo real en `Lease_DisposeAsync_DoesNotDisposeSharedRoot` o en otra lease contract que debe investigarse. Sugerencia: revisar si el fixture `HabilidadWebTestFixture` deja hosts zombi en alguna ruta de cleanup. | sdd-verify + posible correctivo |
| Si con timeout default las 3 corridas coinciden | El gate pasa y el cambio puede archivarse. La nota "≤15 min" del spec es **irrealista para la suite completa** y debería ajustarse en una revisión posterior de la spec (no en este PR). | sdd-archive |
| Documentar la política de timeout en `docs/decisiones-implementacion.md` | Para que el siguiente gate no repita la decisión operativa de bajar el timeout. | apply (este lote o próximo) |

## Estado de las tareas del PR3

- ✅ 7.1 — `xunit.runner.json` creado + `<Content CopyToOutputDirectory="PreserveNewest">` en `SGV.Tests.csproj` (commit `18698a17`).
- ✅ 7.2 — Sección "Política de paralelismo en la suite de tests" en `docs/decisiones-implementacion.md` (commit `18698a17`).
- ❌ 7.3 — Gate de 3 corridas consecutivas: **NO PASA** (Runs 1-2 coinciden pero Run 3 difiere en 1 fail).
  - Spec §"Tres corridas consecutivas satisfacen el gate" NO satisfecha.
  - Spec §"Variación o timeout bloquea la declaración de aptitud" activada.

## Archivos de evidencia

- `/tmp/sgv-pr3-gate/run1.log` — log completo de Run 1 (con env var).
- `/tmp/sgv-pr3-gate/run2.log` — log completo de Run 2.
- `/tmp/sgv-pr3-gate/run3.log` — log completo de Run 3.
- `/tmp/sgv-pr3-gate/run1-default.log` — log de la corrida truncada con timeout default (auditoría).
- `/tmp/sgv-pr3-gate/fails{1,2,3}.txt` — listas de-duplicadas de tests `FAIL` por corrida.
- Commit: `18698a17` en `develop` (sin pushear).
