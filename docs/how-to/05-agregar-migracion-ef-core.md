# H-02-05 — Agregar una nueva migración EF Core

El modelo de Dominio cambió (nueva propiedad, índice, FK) y necesitás que `dotnet ef migrations add` produzca una migración coherente con el grafo del proyecto, sin romper el seed (`DatosSemilla`) ni el script SQL idempotente que se commitea al repo.

---

## Prerrequisitos

- SDK .NET 10 (mismo de `global.json`).
- `dotnet-ef` instalado globalmente (`dotnet tool install -g dotnet-ef`).
- Connection string válida para `database update` en tu MySQL local: `ConnectionStrings__SgvDatabase="server=localhost;database=sgv;user=root;password=;"` o `dotnet user-secrets` en `src/SGV.Api`.
- Haber leído los archivos de `src/SGV.Infraestructura/Persistencia/Catalogos/*.cs` si vas a tocar un catálogo inmutable (los IDs semilla vienen de constantes tipadas, no de GUIDs literales).

---

## Paso 1 — Tocar el modelo y propagar a Entity + Configuración

Edité el Dominio, después la Entity (`src/SGV.Infraestructura/Persistencia/Entidades/<X>Entity.cs`) y la Configuración (`Configuraciones/<X>Configuracion.cs`) siguiendo el patrón del Tutorial 4 (T-01-04): propiedad con tipo + `builder.Property(...).HasMaxLength(...)` o `HasDefaultValue(...)` cuando corresponda.

**Verificación:** `dotnet build src/SGV.Infraestructura/SGV.Infraestructura.csproj` sigue compilando sin tocar migraciones todavía.

---

## Paso 2 — Crear la migración

```bash
dotnet ef migrations add <NombreDescriptivo> \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --output-dir Persistencia/Migraciones
```

Convención de timestamp `yyyyMMddHHmmss` que adopta el repo (ver `20260819223914_AddRefreshTokens.cs` como referencia).

**Verificación:** aparece `src/SGV.Infraestructura/Persistencia/Migraciones/<timestamp>_<NombreDescriptivo>.cs` con `Up(...)` y `Down(...)` simétricos. Si el cambio toca tablas con índices únicos o columnas generadas, abrí el archivo y confirmá que las instrucciones `CreateIndex` / `AddColumn` reflejan la intención (EF no siempre acierta con default values sobre `GENERATED ALWAYS AS`).

---

## Paso 3 — Validar que `DatosSemilla` siga consistente

Si tu cambio toca una tabla sembrada (`NivelHabilidad`, `EstadoVacante`, `EstadoPostulacion`, `NivelCargo`, `Cargo`, `Habilidad`, `TipoUnidadOrganizativa`, `TipoDocumento`, `CategoriaHabilidad`), verificá que los `HasData(...)` de `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` no queden desfasados. Los tests de paridad son:

- `DatosSemilla_NivelCargo_SeedIdsMatchConstantes`
- `DatosSemilla_TipoDocumento_SeedIdsMatchConstantes`
- `DatosSemilla_CategoriaHabilidad_SeedIdsMatchConstantes`

**Verificación:** `dotnet test SGV.slnx --filter "FullyQualifiedName~DatosSemillaTests"` queda verde (skipped `[MySqlFact]` si no hay MySQL local; corrélos con MySQL levantado para asegurarte).

---

## Paso 4 — Aplicar contra MySQL local

```bash
dotnet ef database update \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj
```

**Verificación:** el comando emite `Applying migration <timestamp>_<NombreDescriptivo>` y termina sin error. Si tu cambio agrega un índice sobre una tabla grande, esperá el `BUILD COMPLETE` (puede tardar varios minutos).

---

## Paso 5 — Regenerar el script SQL idempotente

El repo commitea scripts lineales para DBs vacías en `docs/migracion-inicial-sgv.sql` (MySQL 8) y `docs/migracion-inicial-sgv-mariadb.sql` (MariaDB). Regenerá ambos cuando agregás migraciones:

```bash
dotnet ef migrations script --idempotent \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --output docs/migracion-inicial-sgv.sql
```

> ⚠️ A verificar: la variante para MariaDB se genera ajustando manualmente collation `utf8mb4_unicode_ci` y columnas generadas `STORED`. Si la migración nueva toca columnas con `GENERATED ALWAYS AS`, revisá el script resultante contra una MariaDB de prueba antes de commitear.

**Verificación:** el script abre con el header comentado de issue / propósito, lista todas las migraciones en orden cronológico, y termina con la inserción en `__EFMigrationsHistory`. Diff contra `HEAD` con `git diff docs/migracion-inicial-sgv.sql` debería mostrar sólo la nueva migración agregada al final.

---

## Paso 6 — Validar CI antes de pushear

El job de GitHub Actions (`mysql:8.0` service, `JWT_SIGNING_KEY` secret) corre `dotnet test --no-build` contra `sgv_test`. La cobertura de tu migración se ejecuta por la suite `[MySqlFact]`.

```bash
# Equivalente local antes de pushear
docker run --name sgv-mysql-test -d \
  -e MYSQL_ROOT_PASSWORD=sgv_test_pwd \
  -e MYSQL_DATABASE=sgv_test \
  -p 3306:3306 mysql:8.0

export ConnectionStrings__SgvDatabase="Server=127.0.0.1;Port=3306;Database=sgv_test;User=root;Password=sgv_test_pwd;Allow User Variables=True;Default Command Timeout=60"
export Jwt__SigningKey="$(openssl rand -base64 48)"
dotnet test --no-build --configuration Release --verbosity normal

docker stop sgv-mysql-test && docker rm sgv-mysql-test
```

**Verificación:** `Failed: 0` y los `[MySqlFact]` corren (no aparecen como `Skipped` salvo los marcados explícitamente con `[Fact(Skip=...)]`).

---

## Troubleshooting

- **`dotnet ef` falla con `OptionsValidationException("Debe configurar ConnectionStrings:SgvDatabase")`**: falta la env var o el user-secret. Exportá `ConnectionStrings__SgvDatabase` antes de invocar `dotnet ef`.
- **La migración produce `ALTER TABLE ... DROP COLUMN` inesperado**: EF cree que la columna ya no existe en el snapshot. Verificá que `Entity` y `Configuracion` están sincronizados con la entidad de Dominio.
- **`InsertData` con GUIDs mágicos**: el test `Migration_NoContieneGuidsLiterales_ParaNivelesCargo` falla. Usá las constantes de `Catalogos/<Nombre>Constantes.cs`.
- **El script SQL idempotente falla en MariaDB con `Function or expression 'case when ... else null end' cannot be used in the GENERATED ALWAYS AS clause`**: el repositorio documenta esta incompatibilidad en `docs/decisiones-implementacion.md`. Ajustá manualmente a `STORED` para la variante MariaDB.

---

## Referencias

- `src/SGV.Infraestructura/Persistencia/Migraciones/20260819223914_AddRefreshTokens.cs` — ejemplo de migración reciente.
- `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` — seed model snapshot path.
- `src/SGV.Infraestructura/Persistencia/Catalogos/` — constantes de catálogos inmutables.
- `docs/decisiones-implementacion.md` § "Mapa de bloques GUID reservados por catálogo".
- `../tutorials/04-primer-cambio-clean-architecture.md` — Tutorial 4, propagación de cambios end-to-end.
- [R-03-11](../reference/11-tabla-migraciones-ef-core.md) — Tabla
  cronológica de las 22 migraciones con su timestamp y propósito.
