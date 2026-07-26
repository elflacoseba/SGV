# Exploración — permitir-legajo-nulo-personas-issue-202

> Issue origen: #202 — Permitir crear y editar Personas con legajo nulo
> Fecha: 2026-07-26
> Estado: Completada

## 1. Estado actual verificado

### 1.1 Capa de Dominio (`SGV.Dominio`)

- `Persona.Legajo` es `string?` (nullable) — **ya correcto, sin cambios necesarios**
- Constructor `Persona(string nombres, string apellidos, string? legajo = null, ...)` — acepta null
- `ValidacionesDominio.Opcional(legajo, nameof(Legajo), 50)` — maneja null/vacío correctamente
- `Reconstitute(...)` también usa `Opcional` para Legajo

**Sin cambios necesarios en dominio.**

### 1.2 Capa de Aplicación (`SGV.Aplicacion`)

- `CrearPersonaRequestValidator`: `RuleFor(x => x.Legajo).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Legajo))` — ya permite null y empty string
- `ActualizarPersonaRequestValidator`: idéntico al crear
- `PersonaServicioComandos.CheckUniquenessAsync`: ya acepta `string? legajo` y solo consulta `ExistsActiveLegajoAsync` cuando `!string.IsNullOrEmpty(legajo)`
- `IPersonaRepository.ExistsActiveLegajoAsync(string legajo, ...)`: parámetro `string` no-nullable, pero solo se invoca con guarda `!string.IsNullOrEmpty(legajo)`

**Sin cambios necesarios en aplicación.** Los validators ya tratan legajo como opcional.

### 1.3 Contratos Wire (`SGV.Contracts`)

- `CrearPersonaRequest(Legajo: string, ...)` — **`string` no-nullable → requiere cambio a `string?`**
- `ActualizarPersonaRequest(Legajo: string, ...)` — **idem**
- `SetupRequest(Legajo: string?, ...)` — **ya es `string?`** ✅

**Archivo afectado: `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs`**

### 1.4 Setup (`SGV.Infraestructura`)

- `SetupServicio.cs` línea 104: `Legajo: request.Legajo ?? string.Empty` — **workaround** que se puede simplificar a `request.Legajo` una vez que `CrearPersonaRequest.Legajo` sea nullable
- `SetupRequestValidator.cs` línea 30-33: `RuleFor(x => x.Legajo).MaximumLength(50).When(x => x.Legajo is not null)` — **ya correcto**

**Archivo afectado: `src/SGV.Infraestructura/Setup/SetupServicio.cs`**

### 1.5 Capa Web (`SGV.Web`)

**InputModel** (`PersonaInputModel.cs`):
- `[Required(ErrorMessage = "El legajo es obligatorio.")]` — **debe eliminarse**
- `[StringLength(20, ...)]` — **debe cambiarse a 50 para coincidir con dominio/DB**
- `string Legajo { get; set; } = string.Empty` — **debe ser `string?`**

**Create handler** (`Create.cshtml.cs` línea 111):
- `Input.Legajo.Trim()` — normaliza incondicionalmente; debe cambiarse a: whitespace → null, si no trim

**Edit handler** (`Edit.cshtml.cs`):
- Línea 118 (pre-carga): `Input.Legajo = persona.Legajo ?? string.Empty` — debe mantenerse si `Input.Legajo` sigue siendo `string`, o simplificarse si pasa a `string?`
- Línea 174 (POST): `Input.Legajo.Trim()` — misma normalización que Create

**Auth/Setup.cshtml.cs** — **ya correcto**:
- `InputModel.Legajo` es `string?` con `[StringLength(50)]`
- Normaliza correctamente: `string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo`

**Archivos afectados:**
- `src/SGV.Web/Integration/Personas/PersonaInputModel.cs`
- `src/SGV.Web/Pages/Personas/Create.cshtml.cs`
- `src/SGV.Web/Pages/Personas/Edit.cshtml.cs`

### 1.6 API (`SGV.Api`)

- `PersonasController.Create(CrearPersonaRequest request, ...)` — solo pasa el request al servicio; System.Text.Json deserializa `"legajo": null` como `null` para `string?`. **Sin cambios.**
- `PersonasController.Update(...)` — idem.

### 1.7 Cliente HTTP Web (`PersonaApiClient`)

- `CreateAsync(CrearPersonaRequest request, ...)` — serializa con `PostAsJsonAsync`. Con `string? Legajo` emite `"legajo": null` cuando es null. **Sin cambios.**

### 1.8 Base de datos / EF Core

- Columna `Personas.Legajo` en MySQL: `varchar(50)` nullable — **ya correcto**
- Sin migraciones necesarias
- Índices únicos con soft delete no afectados

### 1.9 Cambio activo existente

- `openspec/changes/setup-admin-inicial-issue-195/` — cambio activo **no relacionado**. No debe modificarse ni conflarse.

---

## 2. Componentes afectados y flujo de llamadas

```
Browser → Create.cshtml → CreateModel.OnPostAsync()
  → PersonaInputModel.Legajo (binding DataAnnotations)
  → Normalización: Input.Legajo.Trim()
  → new CrearPersonaRequest(Legajo: Input.Legajo.Trim(), ...)  [string Legajo actual]
  → PersonaApiClient.CreateAsync(request)
    → POST /api/v1/personas (JSON: "legajo": "valor")
    → PersonasController.Create()
      → PersonaServicioComandos.CrearAsync()
        → CrearPersonaRequestValidator (MaximumLength .When not null/empty)
        → CheckUniquenessAsync (skips if null/empty)
        → new Persona(nombres, apellidos, request.Legajo, ...) [acepta string?]
        → repository.AddAsync(persona) → SaveChangesAsync()
```

El flujo de Edit (`ActualizarPersonaRequest`) es análogo.

**Punto de cambio**: la transformación `Input.Legajo.Trim()` → `CrearPersonaRequest` es donde se introduce la normalización whitespace-a-null.

---

## 3. Restricciones y no-objetivos

### No objetivos (explicitamente fuera de alcance)
- No cambiar dominio (`Persona.cs`)
- No cambiar validators ni comportamiento de aplicación para legajo opcional
- No cambiar repositorio, DB schema ni migraciones
- No tocar el cambio activo `setup-admin-inicial-issue-195`
- No implementar proposal/spec/design/tasks — solo exploración

### Restricciones
- Unicidad de legajo NO nulo: `ExistsActiveLegajoAsync` sigue protegiendo contra duplicados cuando legajo tiene valor
- El contrato wire cambia (de `string` a `string?`) — source-breaking para call-sites que pasaban `null!` sin querer, pero compatible a nivel JSON (el serializador ya manejaba null en runtime)
- `PersonaInputModel` debe sincronizar sus validaciones DataAnnotations con las del validator backend

---

## 4. Estrategia de pruebas

### Tests existentes que siguen siendo válidos
- `CrearAsync_DatosValidos_RetornaDtoYGuarda` (usa legajo explícito)
- `CrearAsync_LegajoDuplicadoActivo_RetornaConflictoYSinGuardar`
- `CrearAsync_EmailDuplicadoActivo_RetornaConflictoYSinGuardar`
- `CrearAsync_DocumentoDuplicadoActivo_RetornaConflictoYSinGuardar`
- `CrearAsync_LegajoVacio_PermitidoYGuarda` (usa `""` — sigue siendo válido con `string?`)
- `CrearAsync_LegajoConUnSoloEspacio_TambienEsValido` (usa `"   "`)
- `ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda`
- `ActualizarAsync_LegajoConflictivo_RetornaConflictoYSinGuardar`
- Tests de `PersonaApiClientBasicTests` que usan legajo explícito

### Tests a agregar
1. **Crear persona con Legajo null**: el equivalente a `CrearAsync_LegajoVacio_PermitidoYGuarda` pero con `Legajo: null`
2. **Editar persona limpiando Legajo** (null)
3. **Crear/Editar vía API con Legajo omitido en JSON** (seam test)
4. **Regresión: legajo duplicado no nulo sigue siendo rechazado**

### Tests a actualizar
- `PersonaServicioComandosTests.cs`: `CrearAsync_LegajoConUnSoloEspacio_TambienEsValido` — con `string? Legajo`, el constructor positional recibe `"   "` que es válido; test sigue pasando sin cambios
- `PersonaApiClientBasicTests.cs`: `CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors` usa `new CrearPersonaRequest(string.Empty, "Ana", "García")` — sigue compilando con `string?`
- Los tests que construyen `CrearPersonaRequest` sin legajo explícito en parámetros posicionales deben revisarse si algún call-site omite el argumento

---

## 5. Riesgos y preguntas abiertas

| Riesgo | Impacto | Mitigación |
|--------|---------|------------|
| Source-breaking: call-sites que construyen `CrearPersonaRequest` sin Legajo explícito en posición 1 | Compilación falla si hay positional arguments sin legajo | Revisar todos los call-sites (~26 para CrearPersonaRequest, ~19 para ActualizarPersonaRequest); la mayoría usa named arguments |
| Serialización: JSON sin `legajo` vs `"legajo": null` vs `"legajo": ""` | Backend trata `""` como string vacío, no como null | La normalización web debe convertir whitespace a null; la API/model binding de System.Text.Json deserializa `null` y `""` igual que antes |
| `ExistsActiveLegajoAsync(legajo)` con `legajo=""` | Podría matchear falsos positivos si hay registros con legajo vacío | La guarda `!string.IsNullOrEmpty(legajo)` en `CheckUniquenessAsync` lo previene; confirmar que la implementación del repositorio también lo tolera |
| El `[StringLength(20)]` actual en `PersonaInputModel` es más restrictivo que el `[StringLength(50)]` del backend | Backend acepta legajos de 21-50 chars pero web los rechaza | Cambiar a `[StringLength(50)]` alinea ambos |

### Preguntas abiertas
1. **Edit GET pre-carga**: `Input.Legajo = persona.Legajo ?? string.Empty` — si `PersonaInputModel.Legajo` pasa a `string?`, esto se simplifica a `Input.Legajo = persona.Legajo` (ya que ambos son `string?`). ¿Es deseable?
2. **¿Debe la normalización de whitespace-a-null ocurrir en el PageModel o en el cliente HTTP?** Consistencia con otros campos (Email, NumeroDocumento, Telefono) que ya normalizan `string.IsNullOrWhiteSpace → null` en el PageModel.
3. **¿Se requiere un test de integración `[MySqlFact]`** que verifique que una persona con legajo null persiste correctamente contra MySQL real?

---

## 6. Alternativas y compensaciones

| Enfoque | Pros | Contras | Esfuerzo |
|---------|------|---------|----------|
| **A — Mínimo**: solo cambiar tipos y normalización web según issue | Changeset pequeño; solo 5 archivos fuente + tests; no toca dominio/API/validators | Deja `PersonaInputModel.Legajo` como `string?` — el view de Razor podría requerir ajustes para `asp-for` con nullable | Bajo |
| **B — Conservador**: mantener `PersonaInputModel.Legajo` como `string` con `[Required]` eliminado | Menos cambios en la UI; `asp-for` binding no se ve afectado por nullable | Inconsistencia con el dominio/contracts; `?? string.Empty` se replica en vez de eliminarse | Bajo |
| **C — Full cleanup**: además de A, revisar y simplificar todos los call-sites de `CrearPersonaRequest` | Elimina todos los workarounds | Mayor superficie de cambio; riesgo de tocar código no relacionado | Medio |

---

## 7. Recomendación

**Enfoque A (mínimo)** con los siguientes cambios exactos:

1. **`PersonaRequests.cs`**: `string Legajo` → `string? Legajo` en ambos records
2. **`PersonaInputModel.cs`**: eliminar `[Required]`, `StringLength(20)` → `StringLength(50)`, `string` → `string?`, default `= string.Empty` → sin default (o `= string.Empty` igualmente válido)
3. **`Create.cshtml.cs`** POST: reemplazar `Input.Legajo.Trim()` por `string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo.Trim()`
4. **`Edit.cshtml.cs`** POST: misma normalización; GET pre-carga: simplificar a `Input.Legajo = persona.Legajo` si el modelo es nullable
5. **`SetupServicio.cs`**: `request.Legajo ?? string.Empty` → `request.Legajo`
6. **Tests**: agregar casos para null legajo en create/update; revisar compilación de todos los call-sites

No tocar: dominio, validators, API controller, repositorio, DB, migraciones, cambio activo `setup-admin-inicial-issue-195`.

---

## 8. Lista de archivos concretos a modificar

| Archivo | Cambio |
|---------|--------|
| `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs` | `string Legajo` → `string? Legajo` en `CrearPersonaRequest` y `ActualizarPersonaRequest` |
| `src/SGV.Web/Integration/Personas/PersonaInputModel.cs` | Quitar `[Required]`, `StringLength(20)` → `StringLength(50)`, `string` → `string?`, `= string.Empty` discutible |
| `src/SGV.Web/Pages/Personas/Create.cshtml.cs` | Normalizar `Input.Legajo` en POST (whitespace → null, else trim) |
| `src/SGV.Web/Pages/Personas/Edit.cshtml.cs` | Normalizar `Input.Legajo` en POST; simplificar pre-carga GET |
| `src/SGV.Infraestructura/Setup/SetupServicio.cs` | `request.Legajo ?? string.Empty` → `request.Legajo` |
| `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioComandosTests.cs` | Agregar test con `Legajo: null` |
| `tests/SGV.Tests/Web/Persona/PersonaApiClientBasicTests.cs` | Revisar call-sites, agregar test seam con legajo null/omitido |

---

## 9. Verificaciones hechas contra el código real

- [x] `Persona.cs` Legajo es `string?` con `Opcional` ✅
- [x] `CrearPersonaRequestValidator` ya permite null/empty ✅
- [x] `ActualizarPersonaRequestValidator` ya permite null/empty ✅
- [x] `SetupRequest.Legajo` es `string?` ✅
- [x] `SetupRequestValidator` usa `.When(x => x.Legajo is not null)` ✅
- [x] `Auth/Setup.cshtml.cs` ya normaliza Legajo correctamente ✅
- [x] `PersonaServicioComandos.CheckUniquenessAsync` acepta `string?` y protege con `!string.IsNullOrEmpty` ✅
- [x] Columna DB `Personas.Legajo` es nullable varchar(50) ✅
- [x] No hay migraciones pendientes que afecten Legajo ✅
- [x] El cambio activo `setup-admin-inicial-issue-195` no se superpone ✅
- [x] Todos los call-sites de `CrearPersonaRequest` (~26) y `ActualizarPersonaRequest` (~19) compilarán con `string?` porque usan named arguments o strings literales ✅
