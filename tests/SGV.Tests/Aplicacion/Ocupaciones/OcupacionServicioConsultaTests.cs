using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;
using SGV.Dominio.Ocupaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Ocupaciones;

public sealed class OcupacionServicioConsultaTests
{
    private static readonly Guid OcupacionIdActiva = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid OcupacionIdFinalizada = Guid.Parse("80000000-0000-0000-0000-000000000002");
    private static readonly Guid OcupacionIdEliminada = Guid.Parse("80000000-0000-0000-0000-000000000003");

    private static Ocupacion CrearOcupacionActiva()
    {
        return new Ocupacion(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1), TipoAsignacion.Permanente)
        {
            Id = OcupacionIdActiva
        };
    }

    private static Ocupacion CrearOcupacionFinalizada()
    {
        var o = CrearOcupacionActiva();
        o = new Ocupacion(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1), TipoAsignacion.Permanente)
        {
            Id = OcupacionIdFinalizada
        };
        o.Finalizar(new DateOnly(2025, 6, 30));
        return o;
    }

    private static Ocupacion CrearOcupacionEliminada()
    {
        var o = new Ocupacion(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1), TipoAsignacion.Permanente)
        {
            Id = OcupacionIdEliminada
        };
        o.EliminarLogicamente();
        return o;
    }

    // ── ListAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_PorDefecto_RetornaSoloActivas()
    {
        var repo = new FakeOcupacionReadRepository
        {
            Datos = [CrearOcupacionActiva(), CrearOcupacionFinalizada(), CrearOcupacionEliminada()]
        };
        var servicio = new OcupacionServicioConsulta(repo);

        var resultado = await servicio.ListAsync(includeHistory: false, default);

        Assert.Single(resultado);
        Assert.Equal(OcupacionIdActiva, resultado[0].Id);
        Assert.Equal("Activo", resultado[0].Estado);
    }

    [Fact]
    public async Task ListAsync_ConHistorial_RetornaTodasIncluyendoFinalizadasYEliminadas()
    {
        var repo = new FakeOcupacionReadRepository
        {
            Datos = [CrearOcupacionActiva(), CrearOcupacionFinalizada(), CrearOcupacionEliminada()]
        };
        var servicio = new OcupacionServicioConsulta(repo);

        var resultado = await servicio.ListAsync(includeHistory: true, default);

        Assert.Equal(3, resultado.Count);
        Assert.Contains(resultado, d => d.Id == OcupacionIdActiva && d.Estado == "Activo");
        Assert.Contains(resultado, d => d.Id == OcupacionIdFinalizada && d.Estado == "Finalizado");
        Assert.Contains(resultado, d => d.Id == OcupacionIdEliminada && d.Estado == "Eliminado");
    }

    [Fact]
    public async Task ListAsync_CuandoNoHayDatos_RetornaListaVacia()
    {
        var repo = new FakeOcupacionReadRepository { Datos = [] };
        var servicio = new OcupacionServicioConsulta(repo);

        var resultado = await servicio.ListAsync(includeHistory: false, default);

        Assert.Empty(resultado);
    }

    // ── GetByIdAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_Activa_RetornaDto()
    {
        var repo = new FakeOcupacionReadRepository { Datos = [CrearOcupacionActiva()] };
        var servicio = new OcupacionServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(OcupacionIdActiva, default);

        Assert.NotNull(resultado);
        Assert.Equal(OcupacionIdActiva, resultado!.Id);
        Assert.Equal("Activo", resultado.Estado);
    }

    [Fact]
    public async Task GetByIdAsync_Finalizada_RetornaDto()
    {
        var repo = new FakeOcupacionReadRepository { Datos = [CrearOcupacionFinalizada()] };
        var servicio = new OcupacionServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(OcupacionIdFinalizada, default);

        Assert.NotNull(resultado);
        Assert.Equal(OcupacionIdFinalizada, resultado!.Id);
        Assert.Equal("Finalizado", resultado.Estado);
    }

    [Fact]
    public async Task GetByIdAsync_Eliminada_RetornaDto()
    {
        var repo = new FakeOcupacionReadRepository { Datos = [CrearOcupacionEliminada()] };
        var servicio = new OcupacionServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(OcupacionIdEliminada, default);

        Assert.NotNull(resultado);
        Assert.Equal(OcupacionIdEliminada, resultado!.Id);
        Assert.Equal("Eliminado", resultado.Estado);
    }

    [Fact]
    public async Task GetByIdAsync_Inexistente_RetornaNull()
    {
        var repo = new FakeOcupacionReadRepository { Datos = [] };
        var servicio = new OcupacionServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }
}

// ── Fake ──────────────────────────────────────────────────────────

internal sealed class FakeOcupacionReadRepository : IOcupacionRepository
{
    public List<Ocupacion> Datos { get; set; } = [];

    public Task<Ocupacion?> GetByIdIncludingHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(o => o.Id == id));
    }

    public Task<IReadOnlyList<Ocupacion>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Ocupacion>>(Datos.Where(o => o.EsVigente).ToList());
    }

    public Task<IReadOnlyList<Ocupacion>> ListAllIncludingHistoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Ocupacion>>(Datos.ToList());
    }

    public Task<Ocupacion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake: use GetByIdIncludingHistoryAsync for detail reads.");

    public Task AddAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Ocupacion?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task UpdateAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveByPuestoAsync(Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveByPersonaYPuestoAsync(Guid personaId, Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");
}
