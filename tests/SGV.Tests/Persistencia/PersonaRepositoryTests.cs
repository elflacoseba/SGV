using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Repository tests for Persona read and write operations.
/// </summary>
public sealed class PersonaRepositoryTests
{
    // ===================== Read tests =====================

    [MySqlFact]
    public async Task ListAllAsync_RetornaSoloPersonasActivas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var activa = CreatePersonaEntity("LEG-ACT");
        var inactiva = CreatePersonaEntity("LEG-INACT-LIST", isActive: false);
        context.Set<PersonaEntity>().AddRange(activa, inactiva);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var entidades = await repo.ListAllAsync(default);

            Assert.Contains(entidades, e => e.Id == activa.Id);
            Assert.DoesNotContain(entidades, e => e.Id == inactiva.Id);
            Assert.All(entidades, e => Assert.True(e.IsActive));
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(activa, inactiva);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListAllAsync_RetornaPersonasOrdenadasPorApellidoNombre()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var primera = CreatePersonaEntity("LEG-ORD1");
        primera.Apellidos = "AAAAA";
        primera.Nombres = "Primero";
        var segunda = CreatePersonaEntity("LEG-ORD2");
        segunda.Apellidos = "BBBBB";
        segunda.Nombres = "Segundo";

        context.Set<PersonaEntity>().AddRange(primera, segunda);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var entidades = await repo.ListAllAsync(default);

            var entidadesFiltradas = entidades
                .Where(e => e.Id == primera.Id || e.Id == segunda.Id)
                .ToList();

            Assert.Equal(2, entidadesFiltradas.Count);
            Assert.Equal(primera.Id, entidadesFiltradas[0].Id);
            Assert.Equal(segunda.Id, entidadesFiltradas[1].Id);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(primera, segunda);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaNull_CuandoNoExiste()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);

        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }

    [MySqlFact]
    public async Task GetByIdAsync_ExcluyePersonasInactivas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("INACT", isActive: false);
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdAsync(entity.Id, default);

            Assert.Null(obtenido);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    // ===================== Write tests =====================

    [MySqlFact]
    public async Task AddAsync_AgregaPersona_YLuegoSePuedeConsultar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var emailUnico = "addtest-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var persona = new Persona("Juan", "Pérez", "LEG-TEST-" + Guid.NewGuid().ToString("N")[..8], emailUnico)
        {
            Id = Guid.NewGuid()
        };
        persona.CambiarDocumento(new Guid("71000000-0000-0000-0000-000000000001"), "12345678-" + Guid.NewGuid().ToString("N")[..8]);

        await repo.AddAsync(persona, default);
        await context.SaveChangesAsync();

        try
        {
            var obtenido = await repo.GetByIdAsync(persona.Id, default);
            Assert.NotNull(obtenido);
            Assert.Equal(persona.Legajo, obtenido!.Legajo);
            Assert.Equal(persona.Nombres, obtenido.Nombres);
            Assert.Equal(persona.Apellidos, obtenido.Apellidos);
            Assert.Equal(persona.Email, obtenido.Email);
            Assert.Equal(persona.TipoDocumentoId, obtenido.TipoDocumentoId);
            Assert.Equal(persona.NumeroDocumento, obtenido.NumeroDocumento);
            Assert.True(obtenido.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Id == persona.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task PersistirPersona_LegajoNull_LecturaPosterior()
    {
        // AC persona-management § "Crear persona omitiendo Legajo":
        // una Persona persistida con Legajo=null en MySQL debe
        // recuperarse como Legajo=null. El round-trip cubre la columna
        // Personas.Legajo (varchar(50) NULL) sin que el cliente ni el
        // repo apliquen defaults espurios.
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var emailUnico = "legajonull-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var persona = new Persona("Sin", "Legajo", legajo: null, email: emailUnico)
        {
            Id = Guid.NewGuid()
        };

        await repo.AddAsync(persona, default);
        await context.SaveChangesAsync();

        try
        {
            var obtained = await repo.GetByIdAsync(persona.Id, default);
            Assert.NotNull(obtained);
            Assert.Null(obtained!.Legajo);

            // Verifica también el round-trip contra la entidad cruda de EF
            // para descartar cualquier transformación del mapeo.
            var entity = await context.Set<PersonaEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == persona.Id);
            Assert.NotNull(entity);
            Assert.Null(entity!.Legajo);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Id == persona.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task AddAsync_NoIncluyeRelacionesFueraDeAlcance()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new PersonaRepository(context);
        var emailUnico = "norel-" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        var persona = new Persona("Juan", "Pérez", "LEG-NO-REL-" + Guid.NewGuid().ToString("N")[..8], emailUnico)
        {
            Id = Guid.NewGuid()
        };

        await repo.AddAsync(persona, default);
        await context.SaveChangesAsync();

        try
        {
            // Verify the PersonaEntity is loaded without Habilidades or Ocupaciones
            var entity = await context.Set<PersonaEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == persona.Id);

            Assert.NotNull(entity);
            Assert.NotNull(entity!.Habilidades);
            Assert.Empty(entity.Habilidades);
            Assert.NotNull(entity.Ocupaciones);
            Assert.Empty(entity.Ocupaciones);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>().Where(p => p.Id == persona.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_RetornaPersonaActiva()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-UPD");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
            Assert.True(obtenido.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_PersonaInactiva_RetornaNull()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-INACT", isActive: false);
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.Null(obtenido);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdIncludingDeletedAsync_RetornaPersonaInactiva()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-DEL", isActive: false, isDeleted: true);
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var obtenido = await repo.GetByIdIncludingDeletedAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ModificaCampos()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-MOD");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var persona = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(persona);

            persona!.CambiarDatos("Modificado", "ApellidoMod", "LEG-MOD", "mod@test.com", "555-9999");
            persona.CambiarDocumento(new Guid("71000000-0000-0000-0000-000000000004"), "AB123456");
            await repo.UpdateAsync(persona, default);
            await context.SaveChangesAsync();

            var modificado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal("Modificado", modificado!.Nombres);
            Assert.Equal("ApellidoMod", modificado.Apellidos);
            Assert.Equal("LEG-MOD", modificado.Legajo);
            Assert.Equal("mod@test.com", modificado.Email);
            Assert.Equal("555-9999", modificado.Telefono);
            Assert.Equal(new Guid("71000000-0000-0000-0000-000000000004"), modificado.TipoDocumentoId);
            Assert.Equal("AB123456", modificado.NumeroDocumento);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(
                await context.Set<PersonaEntity>().FirstAsync(p => p.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_MarcaComoInactivo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-DEL2");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            await repo.DeleteAsync(entity.Id, default);
            await context.SaveChangesAsync();

            var activo = await repo.GetByIdAsync(entity.Id, default);
            Assert.Null(activo);

            var incluyendoEliminado = await repo.GetByIdIncludingDeletedAsync(entity.Id, default);
            Assert.NotNull(incluyendoEliminado);
            Assert.False(incluyendoEliminado!.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(
                await context.Set<PersonaEntity>().FirstAsync(p => p.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ReactivateAsync_RestauraEstadoActivo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-REACT");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            await repo.DeleteAsync(entity.Id, default);
            await context.SaveChangesAsync();

            await repo.ReactivateAsync(entity.Id, default);
            await context.SaveChangesAsync();

            var reactivado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(reactivado);
            Assert.True(reactivado!.IsActive);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(
                await context.Set<PersonaEntity>().FirstAsync(p => p.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    // ===================== ExistsActive checks =====================

    [MySqlFact]
    public async Task ExistsActiveLegajoAsync_LegajoExistente_RetornaTrue()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-EXIST");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            var exists = await repo.ExistsActiveLegajoAsync(entity.Legajo!, default);

            Assert.True(exists);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveLegajoAsync_ExcluyendoId_RetornaFalse()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-EXCL");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            var exists = await repo.ExistsActiveLegajoAsync(entity.Legajo!, entity.Id, default);

            Assert.False(exists);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveEmailAsync_EmailExistente_RetornaTrue()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = CreatePersonaEntity("LEG-EML", email: "existente@test.com");
        await context.Set<PersonaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            var exists = await repo.ExistsActiveEmailAsync("existente@test.com", default);

            Assert.True(exists);
        }
        finally
        {
            context.Set<PersonaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    // ===================== QueryAsync (segmented) tests =====================

    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminados()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SD{Guid.NewGuid():N}"[..10];
        var activa = CreatePersonaEntity($"ACT-{searchToken}");
        var eliminada = CreatePersonaEntity($"DEL-{searchToken}", isActive: false, isDeleted: true);
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<PersonaEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: PersonaSegmentoListado.Eliminadas,
                default);

            var eliminadaEncontrada = Assert.Single(items, i => i.Id == eliminada.Id);
            Assert.Equal(1, totalCount);
            Assert.DoesNotContain(items, i => i.Id == activa.Id);
            Assert.All(items, i =>
            {
                Assert.False(i.IsActive);
                Assert.True(i.IsDeleted);
            });
            Assert.Equal(eliminada.Id, eliminadaEncontrada.Id);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentoActivas_NoIncluyeEliminadas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SA{Guid.NewGuid():N}"[..10];
        var activa = CreatePersonaEntity($"ACT-{searchToken}");
        var eliminada = CreatePersonaEntity($"DEL-{searchToken}", isActive: false, isDeleted: true);
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<PersonaEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: PersonaSegmentoListado.Activas,
                default);

            Assert.Equal(1, totalCount);
            var activaEncontrada = Assert.Single(items, i => i.Id == activa.Id);
            Assert.DoesNotContain(items, i => i.Id == eliminada.Id);
            Assert.All(items, i =>
            {
                Assert.True(i.IsActive);
                Assert.False(i.IsDeleted);
            });
            Assert.Equal(activa.Id, activaEncontrada.Id);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SearchCoincideEnCualquieraDeLos5Campos()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var token = $"X5{Guid.NewGuid():N}"[..8];

        // Cada uno matchea exactamente UNO de los 5 campos cubiertos por search:
        // Legajo, Nombres, Apellidos, Email, NumeroDocumento. Comparten el token
        // en su campo distintivo y son visibles simultáneamente.
        var porLegajo = CreatePersonaEntity($"LEG-{token}", email: $"a-{Guid.NewGuid():N}@x.com");
        porLegajo.Legajo = $"LEG-{token}";
        var porNombres = CreatePersonaEntity($"LEG-N-{Guid.NewGuid():N}"[..8], email: $"b-{Guid.NewGuid():N}@x.com");
        porNombres.Nombres = $"Nombre{token}";
        var porApellidos = CreatePersonaEntity($"LEG-A-{Guid.NewGuid():N}"[..8], email: $"c-{Guid.NewGuid():N}@x.com");
        porApellidos.Apellidos = $"Apellido{token}";
        var porEmail = CreatePersonaEntity($"LEG-E-{Guid.NewGuid():N}"[..8], email: $"persona{token}@x.com");
        var porDocumento = CreatePersonaEntity($"LEG-D-{Guid.NewGuid():N}"[..8], email: $"d-{Guid.NewGuid():N}@x.com");
        porDocumento.NumeroDocumento = $"DOC{token}";

        await context.Set<PersonaEntity>().AddRangeAsync(
            [porLegajo, porNombres, porApellidos, porEmail, porDocumento]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                token, page: 1, pageSize: 20,
                sort: null,
                segmento: PersonaSegmentoListado.Activas, default);

            Assert.Equal(5, totalCount);
            var ids = items.Select(i => i.Id).ToHashSet();
            Assert.Contains(porLegajo.Id, ids);
            Assert.Contains(porNombres.Id, ids);
            Assert.Contains(porApellidos.Id, ids);
            Assert.Contains(porEmail.Id, ids);
            Assert.Contains(porDocumento.Id, ids);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                porLegajo, porNombres, porApellidos, porEmail, porDocumento);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_Paginacion_TotalCountProvieneDelRepositorio()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var personas = Enumerable.Range(0, 5)
            .Select(i => CreatePersonaEntity($"PER-PG-{sufijo}-{i}"))
            .ToArray();

        await context.Set<PersonaEntity>().AddRangeAsync(personas);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var (page1, totalCount) = await repo.QueryAsync(
                $"PER-PG-{sufijo}", page: 1, pageSize: 2,
                sort: null,
                segmento: PersonaSegmentoListado.Activas, default);

            Assert.Equal(5, totalCount);
            Assert.Equal(2, page1.Count);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>()
                    .Where(p => p.Legajo!.StartsWith($"PER-PG-{sufijo}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Cross-page coherence: con sort=apellidos_desc y pageSize que fuerce
    /// múltiples páginas, el orden entre páginas debe ser consistente. Si el
    /// sort se aplicara solo en la página recibida, página 3 y página 1 podrían
    /// contener ítems arbitrarios y la concatenación sería incoherente.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_MySql_SortApellidosDesc_SeAplicaAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        // 12 personas con apellidos en orden A..L (no correlativos con Legajo)
        // para garantizar que orden por Apellidos desc ≠ orden por Legajo asc.
        var apellidos = new[]
        {
            "Delta",  "Bravo",  "Charlie", "Echo",
            "Alpha",  "Zulu",   "Mike",    "Hotel",
            "Tango",  "Kilo",   "Juliet",  "Foxtrot"
        };
        var entidades = apellidos
            .Select((apellido, i) =>
            {
                var e = CreatePersonaEntity($"PER-SRT-{sufijo}-{i:D2}");
                e.Apellidos = apellido;
                return e;
            })
            .ToArray();

        await context.Set<PersonaEntity>().AddRangeAsync(entidades);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            var (page1, total1) = await repo.QueryAsync(
                $"PER-SRT-{sufijo}", page: 1, pageSize: 5,
                sort: "apellidos_desc",
                segmento: PersonaSegmentoListado.Activas, default);
            var (page3, total3) = await repo.QueryAsync(
                $"PER-SRT-{sufijo}", page: 3, pageSize: 5,
                sort: "apellidos_desc",
                segmento: PersonaSegmentoListado.Activas, default);

            Assert.Equal(12, total1);
            Assert.Equal(12, total3);

            // Página 1 apellidos desc: Zulu, Tango, Mike, Kilo, Juliet
            Assert.Equal(new[] { "Zulu", "Tango", "Mike", "Kilo", "Juliet" },
                page1.Select(p => p.Apellidos).ToArray());
            // Página 3 (los últimos 2): Bravo, Alpha
            Assert.Equal(new[] { "Bravo", "Alpha" },
                page3.Select(p => p.Apellidos).ToArray());

            // Cross-page: el último de page1 (Juliet) debe ser estrictamente
            // mayor alfabéticamente que el primero de page3 (Charlie).
            Assert.True(string.Compare(page1[^1].Apellidos, page3[0].Apellidos,
                StringComparison.OrdinalIgnoreCase) > 0,
                $"El último apellido de página 1 ('{page1[^1].Apellidos}') debe ser " +
                $"mayor alfabéticamente que el primero de página 3 ('{page3[0].Apellidos}').");
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>()
                    .Where(p => p.Legajo!.StartsWith($"PER-SRT-{sufijo}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SortInvalidoOCaeASortPorDefecto()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        // 3 personas con apellidos deliberadamente no correlativos al Legajo
        // (insertamos en orden Zulu, Alpha, Mike).
        var entidades = new[]
        {
            CreatePersonaEntity($"PER-NA-{sufijo}-Z"),
            CreatePersonaEntity($"PER-NA-{sufijo}-A"),
            CreatePersonaEntity($"PER-NA-{sufijo}-M"),
        };
        entidades[0].Apellidos = "Zulu";
        entidades[1].Apellidos = "Alpha";
        entidades[2].Apellidos = "Mike";

        await context.Set<PersonaEntity>().AddRangeAsync(entidades);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            // sort inválido: el repo debe caer al default (apellidos_asc).
            var (items, total) = await repo.QueryAsync(
                $"PER-NA-{sufijo}", page: 1, pageSize: 10,
                sort: "invalido_xyz",
                segmento: PersonaSegmentoListado.Activas, default);

            Assert.Equal(3, total);
            Assert.Equal(new[] { "Alpha", "Mike", "Zulu" },
                items.Select(p => p.Apellidos).ToArray());
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>()
                    .Where(p => p.Legajo!.StartsWith($"PER-NA-{sufijo}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    // ===================== QueryAsync soloSinUsuario tests =====================

    /// <summary>
    /// REQ-PM-01: <c>soloSinUsuario=true</c> + Activas → la consulta devuelve
    /// sólo las personas activas sin usuario activo asociado (anti-join sobre
    /// <c>AspNetUsers.PersonaId</c>). Una persona con usuario debe quedar
    /// excluida; las demás activas (sin usuario) deben permanecer.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_SoloSinUsuarioTrue_ExcluyePersonasConUsuario()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var token = $"SSU{Guid.NewGuid():N}"[..10];

        // 3 activas + 1 eliminada. Sólo la primera tendrá usuario.
        var pConUsuario = CreatePersonaEntity($"PER-SSU-{token}-CU");
        var pSinUsuarioA = CreatePersonaEntity($"PER-SSU-{token}-SA");
        var pSinUsuarioB = CreatePersonaEntity($"PER-SSU-{token}-SB");
        var pEliminada = CreatePersonaEntity(
            $"PER-SSU-{token}-DEL", isActive: false, isDeleted: true);
        pEliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<PersonaEntity>().AddRangeAsync(
            pConUsuario, pSinUsuarioA, pSinUsuarioB, pEliminada);
        await context.SaveChangesAsync();

        var user = CreateIdentityUserParaPersona(pConUsuario.Id, token);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                search: $"PER-SSU-{token}",
                page: 1, pageSize: 25,
                sort: null,
                segmento: PersonaSegmentoListado.Activas,
                soloSinUsuario: true,
                cancellationToken: default);

            Assert.Equal(2, totalCount);
            var ids = items.Select(i => i.Id).ToHashSet();
            Assert.DoesNotContain(pConUsuario.Id, ids);
            Assert.Contains(pSinUsuarioA.Id, ids);
            Assert.Contains(pSinUsuarioB.Id, ids);
        }
        finally
        {
            // Orden importante: limpiar AspNetUsers antes que Personas por la FK.
            await RemoveIdentityUsersAsync(context, token);
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>()
                    .Where(p => p.Legajo!.StartsWith($"PER-SSU-{token}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// REQ-PM-01: <c>soloSinUsuario=true</c> + <c>Segmento=Eliminadas</c> →
    /// cortocircuito: <c>items=[]</c> y <c>totalCount=0</c> sin invocar el
    /// anti-join (no tiene sentido buscar personas eliminadas sin usuario).
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_SoloSinUsuarioTrueConEliminadas_RetornaVacio()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var token = $"SSUE{Guid.NewGuid():N}"[..10];

        var pActiva = CreatePersonaEntity($"PER-SSUE-{token}-A");
        var pEliminadaSinUsuario = CreatePersonaEntity(
            $"PER-SSUE-{token}-DEL", isActive: false, isDeleted: true);
        pEliminadaSinUsuario.DeletedAt = DateTime.UtcNow;

        await context.Set<PersonaEntity>().AddRangeAsync(pActiva, pEliminadaSinUsuario);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                search: $"PER-SSUE-{token}",
                page: 1, pageSize: 25,
                sort: null,
                segmento: PersonaSegmentoListado.Eliminadas,
                soloSinUsuario: true,
                cancellationToken: default);

            Assert.Empty(items);
            Assert.Equal(0, totalCount);
        }
        finally
        {
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>()
                    .Where(p => p.Legajo!.StartsWith($"PER-SSUE-{token}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// REQ-PM-01: <c>soloSinUsuario=false</c> (o ausente) preserva el
    /// comportamiento previo: la consulta devuelve todas las activas,
    /// INCLUDING las que ya tienen usuario. Back-compat estricto con Index
    /// Personas, typeahead, y consumidores existentes.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_SoloSinUsuarioFalseONull_PreservaBackCompat()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var token = $"SSUB{Guid.NewGuid():N}"[..10];

        var pConUsuario = CreatePersonaEntity($"PER-SSUB-{token}-CU");
        var pSinUsuarioA = CreatePersonaEntity($"PER-SSUB-{token}-SA");
        var pSinUsuarioB = CreatePersonaEntity($"PER-SSUB-{token}-SB");

        await context.Set<PersonaEntity>().AddRangeAsync(
            pConUsuario, pSinUsuarioA, pSinUsuarioB);
        await context.SaveChangesAsync();

        var user = CreateIdentityUserParaPersona(pConUsuario.Id, token);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);

            // false
            var (itemsFalse, totalFalse) = await repo.QueryAsync(
                search: $"PER-SSUB-{token}",
                page: 1, pageSize: 25,
                sort: null,
                segmento: PersonaSegmentoListado.Activas,
                soloSinUsuario: false,
                cancellationToken: default);

            // null
            var (itemsNull, totalNull) = await repo.QueryAsync(
                search: $"PER-SSUB-{token}",
                page: 1, pageSize: 25,
                sort: null,
                segmento: PersonaSegmentoListado.Activas,
                soloSinUsuario: null,
                cancellationToken: default);

            Assert.Equal(3, totalFalse);
            Assert.Equal(3, totalNull);
            Assert.Equal(
                new[] { pConUsuario.Id, pSinUsuarioA.Id, pSinUsuarioB.Id }.OrderBy(g => g),
                itemsFalse.Select(i => i.Id).OrderBy(g => g));
            Assert.Equal(
                new[] { pConUsuario.Id, pSinUsuarioA.Id, pSinUsuarioB.Id }.OrderBy(g => g),
                itemsNull.Select(i => i.Id).OrderBy(g => g));
        }
        finally
        {
            await RemoveIdentityUsersAsync(context, token);
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>()
                    .Where(p => p.Legajo!.StartsWith($"PER-SSUB-{token}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// REQ-PM-01: ortogonalidad del filtro. Combinado con <c>search</c>,
    /// <c>sort</c> y <c>page</c>, el filtro <c>soloSinUsuario</c> se compone
    /// antes del <c>Skip/Take</c> y el <c>totalCount</c> refleja el conteo
    /// post-filtro (no el previo).
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_SoloSinUsuarioCombinaConSearchSortPaginacion()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var token = $"SSUO{Guid.NewGuid():N}"[..8];

        // 5 personas activas con apellido "Garcia<N>" que comparten el token
        // en su Legajo y apellido. Sólo 2 quedan sin usuario activo.
        var pG1 = CreatePersonaEntity($"PER-SSUO-{token}-G1");
        pG1.Apellidos = "Garcia Uno";
        var pG2 = CreatePersonaEntity($"PER-SSUO-{token}-G2");
        pG2.Apellidos = "Garcia Dos";
        var pG3 = CreatePersonaEntity($"PER-SSUO-{token}-G3");
        pG3.Apellidos = "Garcia Tres";

        var user1 = CreateIdentityUserParaPersona(pG1.Id, token);
        var user3 = CreateIdentityUserParaPersona(pG3.Id, token);

        await context.Set<PersonaEntity>().AddRangeAsync(pG1, pG2, pG3);
        await context.SaveChangesAsync();

        await context.Users.AddRangeAsync(user1, user3);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PersonaRepository(context);
            var (page1, totalCount) = await repo.QueryAsync(
                search: $"PER-SSUO-{token}",
                page: 1, pageSize: 1,
                sort: "apellidos_asc",
                segmento: PersonaSegmentoListado.Activas,
                soloSinUsuario: true,
                cancellationToken: default);

            // pG1 (con usuario) + pG3 (con usuario) son filtradas.
            // Sólo pG2 queda visible. pageSize=1 → 1 ítem en página, totalCount=1.
            Assert.Equal(1, totalCount);
            var item = Assert.Single(page1);
            Assert.Equal(pG2.Id, item.Id);
            Assert.Equal("Garcia Dos", item.Apellidos);
        }
        finally
        {
            await RemoveIdentityUsersAsync(context, token);
            context.Set<PersonaEntity>().RemoveRange(
                await context.Set<PersonaEntity>()
                    .Where(p => p.Legajo!.StartsWith($"PER-SSUO-{token}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Crea un <see cref="SgvIdentityUser"/> mínimo válido apuntando a una
    /// persona activa. El <c>UserName</c> y el <c>Email</c> se generan con el
    /// <paramref name="token"/> para que sea fácil de limpiar al final del
    /// test sin afectar a otras filas.
    /// </summary>
    private static SgvIdentityUser CreateIdentityUserParaPersona(
        Guid personaId, string token)
    {
        var id = Guid.NewGuid().ToString("N");
        return new SgvIdentityUser
        {
            Id = id,
            UserName = $"u-{token}-{id[..8]}@ssu.test",
            NormalizedUserName = $"U-{token}-{id[..8]}@SSU.TEST",
            Email = $"u-{token}-{id[..8]}@ssu.test",
            NormalizedEmail = $"U-{token}-{id[..8]}@SSU.TEST",
            EmailConfirmed = false,
            PersonaId = personaId,
            SecurityStamp = id
        };
    }

    /// <summary>
    /// Limpia los <see cref="SgvIdentityUser"/> creados en un test por su
    /// prefijo de token (UserName/Email). Necesario ANTES de eliminar las
    /// Personas por la FK con <c>Restrict</c>.
    /// </summary>
    private static async Task RemoveIdentityUsersAsync(SgvDbContext context, string token)
    {
        var prefix = $"u-{token}-";
        var users = await context.Users
            .Where(u => u.UserName!.StartsWith(prefix))
            .ToListAsync();
        if (users.Count > 0)
        {
            context.Users.RemoveRange(users);
            await context.SaveChangesAsync();
        }
    }

    private static PersonaEntity CreatePersonaEntity(
        string prefix,
        bool isActive = true,
        bool isDeleted = false,
        string? email = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Legajo = $"{prefix}-{suffix}",
            Nombres = $"Nombre {prefix}",
            Apellidos = $"Apellido {prefix}",
            Email = email ?? $"{prefix.ToLowerInvariant()}@test.com",
            IsActive = isActive,
            IsDeleted = isDeleted
        };
    }
}
