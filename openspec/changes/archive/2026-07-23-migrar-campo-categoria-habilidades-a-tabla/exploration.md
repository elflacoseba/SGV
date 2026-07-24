## Exploración: Migrar campo Categoría de Habilidades a Tabla

### Estado Actual

`Habilidad.Categoria` es un campo `string?` opcional (max 100 chars) en la entidad de dominio, persistido como `varchar(100)` nullable en la columna `Categoria` de la tabla `Habilidades`. No existe ningún catálogo, tabla, enum o constantes de categorías — los únicos valores conocidos son 4 strings hardcodeados en las seeds: `"Conducción"`, `"Técnica"`, `"Dominio"` y `"Académica"`.

El campo se comporta como texto libre en toda la stack:
- **Dominio**: opcional, solo validación de longitud
- **API**: acepta string opcional en create/update
- **Web**: input libre (`<input asp-for="Input.Categoria">`), no dropdown
- **Listados**: se muestra como badge, se puede buscar (LIKE) y ordenar

### Áreas Afectadas (potenciales)

| Capa | Archivos | Impacto |
|------|----------|---------|
| **Dominio** | `src/SGV.Dominio/Habilidades/Habilidad.cs` | `Categoria` cambia de `string?` a `Guid? CategoriaId`; constructor, Actualizar, CambiarDatos, Reconstitute |
| **Contracts** | `HabilidadDto.cs`, `HabilidadRequests.cs` | `string? Categoria` → `Guid? CategoriaId` + `string? CategoriaNombre` — BREAKING CHANGE |
| **Contracts (nuevo)** | `CategoriaHabilidadDto.cs` | Nuevo DTO para el catálogo |
| **Infraestructura** | `HabilidadEntity.cs`, `HabilidadConfiguracion.cs`, `DomainToPersistenceMapper.cs`, `PersistenceToDomainMapper.cs` | Nueva columna FK, nueva entidad `CategoriaHabilidadEntity`, nuevo mapper |
| **Infraestructura (nuevo)** | `CategoriaHabilidadEntity.cs`, `CategoriaHabilidadConfiguracion.cs`, `CategoriaHabilidadConstantes.cs` | Entidad, configuración y constantes del catálogo |
| **Semilla** | `DatosSemilla.cs` | Mover Categoria de string literal a FK referenciando seed IDs |
| **Aplicación** | `HabilidadServicioComandos.cs`, `HabilidadServicioConsulta.cs`, validadores | Cambiar validación de string a FK lookup |
| **Aplicación (nuevo)** | `ICategoriaHabilidadRepository.cs`, `CategoriaHabilidadServicioConsulta.cs` | Servicio de consulta del catálogo (read-only) |
| **API (nuevo)** | `CategoriasHabilidadController.cs` | Endpoint `GET /api/v1/categorias-habilidad` |
| **API** | `SkillsController.cs` | HabilidadDto cambia — afecta serialización |
| **Web** | `HabilidadInputModel.cs`, `_Form.cshtml`, `Create.cshtml.cs`, `Edit.cshtml.cs` | Input libre → `<select>` dropdown poblado desde API |
| **Web** | `HabilidadListItemViewModel.cs`, `Index.cshtml.cs`, `Details.cshtml` | Ajustar visualización: nombre + id |
| **Web (nuevo / existente)** | Cliente `IHabilidadApiClient` | Nuevo método `GetCategoriasHabilidadAsync()` |
| API/Web client | `HabilidadApiClient` | Categoria cambia de posición en DTO — actualizar mapping |
| **Migraciones** | Nueva migración EF | `ALTER TABLE Habilidades ADD CategoriaHabilidadId` + FK + backfill |
| **Tests** | Múltiples archivos | Ajustar fakes, assertions, test data |

### Enfoques

1. **A. Mantener `string?` libre (estado actual)**
   - Pros: Cero esfuerzo, cero riesgo, cero cambios
   - Contras: Sin consistencia, datos sucios potenciales (typos, variantes), sin gobierno del dominio
   - Esfuerzo: Ninguno

2. **B. Tabla `CategoriasHabilidad` inmutable + FK + bloque GUID `72000000-…`**
   - Pros: Datos consistentes, IDs estables, patrón probado (NivelCargo/TipoDocumento), catálogo extensible sin recompile
   - Contras: BREAKING CHANGE en HabilidadDto (Categoria string → CategoriaId + CategoriaNombre), migración de datos, blast radius medio (~15-20 archivos)
   - Esfuerzo: Medio-Alto

3. **C. Tabla `CategoriasHabilidad` editable (CRUD admin) + FK**
   - Pros: Como B + administración vía UI
   - Contras: Como B + mucho más esfuerzo (CRUD endpoints + UI admin), riesgo de orfandad
   - Esfuerzo: Alto

4. **D. Enum server-side + EF ValueConverter**
   - Pros: Sencillo, type-safe, sin tabla nueva, sin migración de BD (columna existente sigue siendo varchar)
   - Contras: Catalogo CERRADO — requiere recompile para agregar categorías. Las 4 categorías actuales parecen estables, pero es decisión de negocio
   - Esfuerzo: Bajo-Medio

### Recomendación

Depende de la respuesta del usuario sobre si las categorías son un dominio cerrado (→ D) o abierto (→ B). Si son cerradas y estables, la alternativa D (enum) es la de menor costo y mantiene el contrato wire casi intacto. Si pueden crecer, la B (tabla inmutable) es la opción correcta pero con un costo y blast radius mayores.

### Riesgos

- **Compatible backward**: HabilidadDto cambia — clientes web que parsean `Categoria` como string (`result.Value.Categoria`) se rompen
- **Concurrencia**: Ningún change in-flight toca Categoria hoy
- **Specs**: Son neutrales — ni obligan ni prohíben normalizar
- **Bloque GUID**: Si se elige B, el bloque `72000000-…` está libre; hay que declararlo en `decisiones-implementacion.md`

### Ready for Proposal

Sí — la exploración está completa. Sin embargo, el orquestador DEBE preguntar al usuario si las categorías son un conjunto cerrado y estable (→ enum) o abierto (→ tabla) antes de pasar a `sdd-propose`. Esa respuesta cambiará radicalmente el approach y el esfuerzo.
