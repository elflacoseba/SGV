# T-01-04 — Hacer tu primer cambio siguiendo Clean Architecture

**Qué vas a lograr:** agregar una propiedad opcional `Observaciones`
(`string?`, ≤ 500 caracteres) a la entidad `Persona` y propagarla por las
siete capas del grafo: Dominio → Infraestructura (Entity, Configuración,
Migración) → Contracts (Request, DTO) → Aplicación → Api (Controller)
→ Web (ApiClient, Razor Page). Al final, build verde + suite de tests OK.

---

## Prerrequisitos

1. Haber completado **T-01-01** (build limpio, MySQL local levantado).
2. SDK .NET 10 instalado (ver `global.json`).
3. Herramienta `dotnet-ef` instalada globalmente:
   `dotnet tool install -g dotnet-ef`.

---

## Paso 1 — Tocar el Dominio

Editá `src/SGV.Dominio/Personas/Persona.cs`:

1. Agregá la propiedad `public string? Observaciones { get; private set; }`.
2. Extendé `CambiarDatos` para aceptar el nuevo parámetro
   `string? observaciones = null` y guardarlo vía
   `ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 500)`.
3. Extendé `Reconstitute` con `string? observaciones` y asignalo a
   `self.Observaciones = ValidacionesDominio.Opcional(...)`.
4. (Opcional) Agregá un método de negocio si la lógica lo amerita; para un
   campo opcional libre alcanza con extender `CambiarDatos`.

**Verificación:** `dotnet build src/SGV.Dominio/SGV.Dominio.csproj` sigue
compilando.

> ⚠️ A verificar: nunca expongas setters públicos. El dominio usa
> `private set` + Reconstitute; cualquier setter público rompe el invariante
> de "el agregado se muta vía métodos de negocio".

---

## Paso 2 — Tocar la Entity y su Configuración

Editá `src/SGV.Infraestructura/Persistencia/Entidades/PersonaEntity.cs` y
agregá:

```csharp
public string? Observaciones { get; set; }
```

Editá `src/SGV.Infraestructura/Persistencia/Configuraciones/PersonaConfiguracion.cs`
y agregá la columna después de `Telefono`:

```csharp
builder.Property(e => e.Observaciones).HasMaxLength(500);
```

**Verificación:** `dotnet build src/SGV.Infraestructura/SGV.Infraestructura.csproj`
sigue compilando. Las migraciones aún no cambiaron — eso es el paso siguiente.

---

## Paso 3 — Crear la migración EF Core

Desde la raíz del repo:

```bash
dotnet ef migrations add AddObservacionesToPersona \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --output-dir Persistencia/Migraciones
```

**Verificación:** aparece un archivo nuevo
`src/SGV.Infraestructura/Persistencia/Migraciones/<timestamp>_AddObservacionesToPersona.cs`
con `Up(...)` que ejecuta
`migrationBuilder.AddColumn<string?name:="Observaciones", table:="Personas", ...)`
y un `Down(...)` simétrico. La convención de timestamp es `yyyyMMddHHmmss`
(ver otros archivos del directorio como `20260819223914_AddRefreshTokens.cs`).

Aplicá la migración contra tu MySQL local:

```bash
dotnet ef database update \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj
```

> ⚠️ A verificar: si tu connection string es distinta a la del paso 3 de
> T-01-01, exportá `ConnectionStrings__SgvDatabase` antes del comando. Sin
> una connection string válida, `ef` falla con
> `OptionsValidationException("Debe configurar ConnectionStrings:SgvDatabase")`.

---

## Paso 4 — Declarar el contrato

Editá `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs` y agregá
`Observaciones` a ambos records:

```csharp
public sealed record CrearPersonaRequest(
    string? Legajo,
    string Nombres,
    string Apellidos,
    string? Email = null,
    Guid? TipoDocumentoId = null,
    string? NumeroDocumento = null,
    string? Telefono = null,
    string? Observaciones = null);   // <-- nuevo
```

Hacé lo mismo con `ActualizarPersonaRequest`.

Editá `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaDto.cs` y agregá el
campo al final del record:

```csharp
public sealed record PersonaDto(
    Guid Id, string? Legajo, string Nombres, string Apellidos,
    string? Email, Guid? TipoDocumentoId,
    string? TipoDocumentoCodigo, string? TipoDocumentoNombre,
    string? NumeroDocumento, string? Telefono,
    string? Observaciones,   // <-- nuevo
    bool IsActive);
```

**Verificación:** `dotnet build src/SGV.Contracts/SGV.Contracts.csproj`
compila sin errores.

> ⚠️ A verificar: `Contracts` es una **leaf** del grafo. No debe tomar
> dependencias de Dominio ni de Aplicación. Si rompe el build por imports,
> revisá que no hayas agregado un using indebido.

---

## Paso 5 — Pasar el campo por Aplicación

En `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs`:

1. `CrearAsync` y `ActualizarAsync`: pasá `request.Observaciones` a
   `persona.CambiarDatos(...)`.
2. `MapToDto`: incluí `persona.Observaciones` al construir el `PersonaDto`.
3. (Recomendado) En `Validaciones/`, agregá
   `RuleFor(x => x.Observaciones).MaximumLength(500)` a los dos validators.

**Verificación:** `dotnet build src/SGV.Aplicacion/SGV.Aplicacion.csproj`
compila. El campo se hidrata vía `Reconstitute`, así que `PersonaServicioConsulta` no necesita cambios.

---

## Paso 6 — Api y Web

**Api:** el controller recibe los records directamente, así que **no lo
tocás**: el binder de ASP.NET Core mapea la propiedad JSON al record.

**Web:** tres cambios mínimos.

1. `src/SGV.Web/Integration/Personas/PersonaInputModel.cs` — agregá la
   propiedad con `[StringLength(500, ...)]`.
2. `src/SGV.Web/Pages/Personas/_Form.cshtml` — agregá un `<textarea
   asp-for="Input.Observaciones" class="form-control" rows="3">` con su
   `<label>` y `<span asp-validation-for>` dentro de un `col-md-12`.
3. En `Create.cshtml.cs` y `Edit.cshtml.cs`, normalizá al construir el
   request: `string.IsNullOrWhiteSpace(Input.Observaciones) ? null :
   Input.Observaciones.Trim()`.

`PersonaApiClient` no necesita cambios: `PostAsJsonAsync` serializa el record
entero.

---

## Paso 7 — Validar todo el grafo

```bash
dotnet build SGV.slnx
dotnet test SGV.slnx --filter "FullyQualifiedName!~MySqlFact&FullyQualifiedName!~MySqlTheory"
```

**Verificación:** `Build succeeded. 0 Error(s)` y `Failed: 0`. Si tu IDE marca
errores en otros `*ApiClient`, no tocaste esos archivos: `Contracts` propaga
el cambio por composición. Con MySQL local, corré la suite completa
(Tutorial 3).

---

## Próximos pasos

- **T-01-02** — Recorré la nueva propiedad con un alta end-to-end y verificá
  la fila de auditoría.
- [E-04-01](../explanation/01-clean-architecture-dos-composition-roots.md) —
  Explanation de la regla "Contracts es leaf" y por qué `SGV.Web` no
  referencia `SGV.Api`.
- [E-04-11](../explanation/11-patron-reconstitute-internalsvisibleto.md) —
  Explanation del patrón `Reconstitute` y `InternalsVisibleTo` en Dominio.
