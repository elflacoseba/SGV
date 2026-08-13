# Exploration: `vacante-crear-puestos-libres`

## Contexto

El dropdown de "Nueva Vacante" en `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` (línea 232) consume `vacanteApiClient.ListarPuestosAsync()`, que a su vez invoca `GET /api/v1/puestos` → `PuestosController.GetAll()` → `PuestoServicioConsulta.ListAsync()` → `PuestoRepository.ListAllAsync()`. Este listado devuelve **todos los puestos activos** (`IsActive = true`, `IsDeleted = 0`), sin filtro por disponibilidad de negocio. El conflicto N1 (`PuestoOcupado`) se detecta recién al hacer POST del formulario.

La regla N1 documentada en `openspec/specs/vacante-management/spec.md` §"Regla N1" establece que no se puede abrir una Vacante para un Puesto que ya tenga una Ocupación activa (`EsVigente = true`). La propuesta es filtrar el dropdown para mostrar solo "Puestos libres" (sin Ocupación vigente), llevando la validación de vuelta al momento de carga del formulario.

---

## Estado actual

### Backend: capacidad existente

| Método | Ubicación | ¿Devuelve "puestos libres"? |
|--------|-----------|------------------------------|
| `IOcupacionRepository.ExistsActiveByPuestoAsync(puestoId)` | `OcupacionRepository.cs:171` | **No** — es un exists booleano; invocado por N1 para rechazar, no para listar |
| `IOcupacionRepository.ExistsActiveByVacanteAsync(vacanteId)` | `OcupacionRepository.cs:210` | **No** — similar, exists para cobertua duplicada |
| `IOcupacionRepository.ObtenerVigentePorVacanteAsync(vacanteId)` | `OcupacionRepository.cs:231` | **No** — proyecciones para hidratar DTOs |
| `IPuestoRepository.ListAllAsync()` | `PuestoRepository.cs:21` | **No** — filtra solo `IsActive`, sin join a Ocupacion |
| `IPuestoServicioConsulta.ListAsync()` | `PuestoServicioConsulta.cs:9` | **No** — delegador puro |

**Conclusión:** No existe actualmente ningún método que devuelva puestos filtrados por "sin Ocupación vigente". La operación requerida es implementable agregando un `LEFT JOIN` / `NOT EXISTS` en la query de `ListAllAsync` del `PuestoRepository`, o creando un nuevo método dedicado.

### Dominio: ¿El agregado Puesto conoce a Ocupacion?

**No.** El agregado `Puesto` (`Dominio/Organizacion/Puesto.cs`) tiene una colección `_ocupaciones` y una navegación `IReadOnlyCollection<Ocupacion> Ocupaciones`, pero no tiene lógica de dominio que consulte esta colección para determinar si está "libre". La verificación de vigencia (`EsVigente = FechaFin is null && !IsDeleted`) vive exclusivamente en `Ocupacion`. El join con Ocupacion es puro Query/Repository, no parte del agregado.

### Contrato DTO: `PuestoDto`

```csharp
// Contracts/Organizacion/Consultas/Dtos/PuestoDto.cs
public sealed record PuestoDto(
    Guid Id, string Codigo, string Nombre, string? Descripcion,
    Guid UnidadOrganizativaId, string UnidadOrganizativaNombre,
    Guid CargoId, string CargoNombre,
    Guid? PuestoSuperiorId);  // Sin campo de Ocupacion
```

El `PuestoDto` actual no expone estado de Ocupacion. Para el dropdown, no se requiere cambiar el DTO — solo filtrar la lista.

---

## Hallazgos

### 1. Call sites de `ListarPuestosAsync` (IVacanteApiClient)

| Archivo | Línea | Contexto |
|---------|-------|----------|
| `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` | 232 | Dropdown del formulario Create |
| `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` | 41, 61, 76, 106, 156, 202, 244 | Tests de smoke del Create page (usa `ListarPuestosResult` en fake) |
| `tests/SGV.Tests/Web/Vacantes/VacanteApiClientListarPuestosTests.cs` | 27 | Tests unitarios del cliente HTTP |

El único call site funcional en runtime es `Create.cshtml.cs:232`. Los tests son todos con fakes que setean `ListarPuestosResult` con datos controlables.

### 2. Call sites de `IPuestoServicioConsulta.ListAsync`

| Archivo | Línea | Contexto |
|---------|-------|----------|
| `src/SGV.Api/Controllers/PuestosController.cs` | 43 | `GetAll()` → `GET /api/v1/puestos` |
| `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs` | 71, 91 | Tests unitarios del servicio |

### 3. Tests existentes relevantes

| Test | Archivo | ¿Cubre el cambio? |
|------|---------|-------------------|
| `Crear_PuestoConOcupacionActiva_DevuelveConflictoPuestoOcupado` | `VacanteServicioComandosTests.cs:373` | N1: ya existente — verifica que POST rechace con 409 cuando hay Ocupacion activa |
| `Crear_PuestoSinOcupacion_Exito` | `VacanteServicioComandosTests.cs:394` | N1: ya existente — happy path cuando no hay Ocupacion |
| `Crear_PuestoConOcupacionEliminada_NoBloquea` | `VacanteServicioComandosTests.cs:410` | N1: ya existente — Ocupacion finalizada/eliminada no bloquea |
| `Post_Create_WhenApiReturnsConflict` | `VacantesCreateEditForbidTests.cs:233` | Tests web de conflicto al POST |
| `Get_Create_WhenMutationRole_RendersFormWithCatalogs` | `VacantesCreateEditForbidTests.cs:34` | Verifica que se llame a `ListarPuestosAsync` (1 call) |

**No existe test que verifique el comportamiento del dropdown filtrado**, ni test de integración web que verifique la ausencia de puestos ocupados en el dropdown de Create.

### 4. Arquitectura de filtros de Ocupacion

El método `ExistsActiveByPuestoAsync` en `OcupacionRepository` implementa:

```csharp
// !o.IsDeleted && o.FechaFin == null  → EsVigente
AnyAsync(o => o.PuestoId == puestoId && !o.IsDeleted && o.FechaFin == null)
```

Para filtrar puestos libres, el JOIN/NOT EXISTS equivalente sería:

```sql
-- MySQL: puestos activos sin Ocupacion vigente
SELECT p.* FROM Puestos p
WHERE p.IsActive = 1 AND p.IsDeleted = 0
AND NOT EXISTS (
    SELECT 1 FROM Ocupaciones o
    WHERE o.PuestoId = p.Id AND o.IsDeleted = 0 AND o.FechaFin IS NULL
)
```

---

## Riesgos

1. **Puesto con Vacante abierta pero sin Ocupacion**: un Puesto puede tener una Vacante abierta (`Abierta` o `En Selección`) sin que haya una Ocupacion vigente. Hoy ese Puesto aparece en el dropdown y el POST falla con `409 PuestoConVacanteAbierta` (constraint unique). Si filtramos solo por "sin Ocupacion", este Puesto seguiría apareciendo — la regla de negocio de "libre" necesita decidir si incluir o excluir estos puestos.

2. **Impacto en tests existentes**: `VacantesCreateEditForbidTests.Get_Create_WhenMutationRole_RendersFormWithCatalogs` llama a `ListarPuestosAsync` y espera 1 llamada; si se agrega un parámetro query (ej: `?soloLibres=true`), el fake `FakeVacanteApiClient` necesitará actualizar su firma.

3. **Contract breach en otros consumers**: si `GET /api/v1/puestos` se modifica para filtrar por defecto, cualquier otro consumer que use este endpoint para listar TODOS los puestos activos (ej: dropdowns de Puestos en otros módulos) vería un comportamiento diferente. **Recomendación**: crear un nuevo endpoint o un parámetro explícito.

4. **Sesgo de presentación**: filtrar en el backend pero no cambiar el `PuestoDto` significa que el dropdown mostrará solo puestos libres, pero si en el futuro otro módulo necesita la lista completa, el diseño de API no lo soportaría sin un flag.

5. **N+1 en tests**: el `FakeVacanteApiClient.ListarPuestosResult` en los tests de integración web setea una lista específica; si se cambia el comportamiento del endpoint real, los tests con fakes seguirían pasando si no se actualizan.

---

## Preguntas abiertas para la propuesta

1. **Alcance del filtro**: ¿"Puesto libre" significa exclusivamente "sin Ocupacion vigente" (N1) o también "sin Vacante abierta"? La spec N1 dice que no se puede abrir Vacante con Ocupacion activa; pero no dice nada sobre si se puede abrir otra Vacante cuando ya existe una abierta. El constraint `ActivePuestoIdUnique` protege contra dos Vacantes abiertas para el mismo puesto, pero el dropdown hoy muestra puestos con Vacante abierta. **¿Querés que el filtro incluya también `NOT EXISTS Vacante Abierta` además de `NOT EXISTS Ocupacion vigente`?**

2. **Nuevo endpoint vs. flag en existente**: ¿`GET /api/v1/puestos?disponibles=true` (nuevo parámetro, backward-compatible) o `GET /api/v1/puestos-libres` (nuevo endpoint dedicado)? El segundo es más limpio para el contrato API pero requiere routing adicional.

3. **Scope de la exploración**: ¿el cambio se limita al dropdown de Vacantes/Create o debe impactar también otros dropdowns de puestos (ej: `Puestos/Create`, `Ocupaciones/Create`)? Verificar si el usuario necesita "puestos libres" en más de un lugar.

4. **Requisito de negocio confirmado**: ¿el comportamiento esperado es que el dropdown solo muestre puestos sin Ocupacion vigente, y que el POST nunca pueda fallar con `PuestoOcupado`? O ¿se quiere solo mejorar la UX (mostrar hint visual) pero mantener la validación en backend?

5. **Tests a agregar**: ¿se requieren tests de integración web que verifiquen que el dropdown no contiene puestos ocupados, o alcanza con tests unitarios del repositorio + tests de smoke del page?

---

## Síntesis para propuesta

**Reutilización de capacidad existente:** `IOcupacionRepository.ExistsActiveByPuestoAsync(puestoId)` es el bloque de building que haría falta para el NOT EXISTS. La implementación de `PuestoRepository.ListAllAsync` tendría que agregar el `LEFT JOIN` o `NOT EXISTS` contra `OcupacionEntity` filtrado por `!IsDeleted && FechaFin IS NULL`. No se requiere crear una nueva query desde cero — el patrón ya existe en la constraint de N1 en `VacanteServicioComandos.CrearAsync`.

**Artefacto de salida:** `openspec/changes/vacante-crear-puestos-libres/exploration.md`
**Topic key Engram:** `sdd/vacante-crear-puestos-libres/explore`
