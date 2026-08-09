using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Vacantes;
using Xunit;

namespace SGV.Tests.Aplicacion.Vacantes;

/// <summary>
/// Cobertura RED→GREEN de <see cref="VacanteServicioConsulta"/>.
/// WU-3.x / spec <c>vacante-management</c> AC2-AC3.
/// </summary>
public sealed class VacanteServicioConsultaTests
{
    private static readonly Guid PuestoId1 = Guid.Parse("70000000-0000-0000-0000-000000000001");

    private static readonly Guid EstadoAbiertaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid EstadoCubiertaId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    private static readonly Guid VacanteIdCubierta = Guid.Parse("70000000-0000-0000-0000-000000000801");
    private static readonly Guid VacanteIdAbierta = Guid.Parse("70000000-0000-0000-0000-000000000802");
    private static readonly Guid VacanteIdInexistente = Guid.Parse("70000000-0000-0000-0000-000000000899");

    private static readonly Guid OcupacionDerivadaId = Guid.Parse("70000000-0000-0000-0000-000000000810");

    [Fact]
    public async Task ObtenerPorIdAsync_VacanteCubiertaConOcupacionDerivada_DevuelveOcupacionDerivadaIdYPersonaAsignada()
    {
        // T1.18 (invertir-flujo-cubrir): Vacante Cubierta con Ocupacion
        // derivada → DTO hidrata OcupacionDerivadaId + PersonaAsignadaNombre.
        var vacanteCubierta = CrearVacanteConEstadoCubierta(VacanteIdCubierta);

        var vacanteRepo = new FakeVacanteLookupConEager
        {
            Staged = { [VacanteIdCubierta] = vacanteCubierta }
        };
        var ocupacionRepo = new FakeOcupacionLookupCobertura
        {
            CoberturaPorVacante =
            {
                [VacanteIdCubierta] = (OcupacionDerivadaId, "Juan Pérez")
            }
        };

        var servicio = new VacanteServicioConsulta(
            (IVacanteRepository)vacanteRepo,
            (IOcupacionRepository)ocupacionRepo);

        var resultado = await servicio.ObtenerPorIdAsync(VacanteIdCubierta, default);

        Assert.NotNull(resultado);
        Assert.Equal(OcupacionDerivadaId, resultado!.OcupacionDerivadaId);
        Assert.Equal("Juan Pérez", resultado.PersonaAsignadaNombre);
        Assert.Equal(1, ocupacionRepo.ObtenerVigenteCallCount);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_VacanteAbierta_NoConsultaOcupacion_DevuelveOcupacionDerivadaIdNull()
    {
        // T1.19 (invertir-flujo-cubrir): Vacante Abierta → DTO con campos
        // null. El servicio NO consulta IOcupacionRepository (defensivo:
        // evita una query innecesaria y mantiene simetría con el path
        // vigente donde sólo las Vacantes Cubiertas tienen Ocupacion
        // derivada).
        var vacanteAbierta = CrearVacanteConEstadoAbierta(VacanteIdAbierta);

        var vacanteRepo = new FakeVacanteLookupConEager
        {
            Staged = { [VacanteIdAbierta] = vacanteAbierta }
        };
        var ocupacionRepo = new FakeOcupacionLookupCobertura();

        var servicio = new VacanteServicioConsulta(
            (IVacanteRepository)vacanteRepo,
            (IOcupacionRepository)ocupacionRepo);

        var resultado = await servicio.ObtenerPorIdAsync(VacanteIdAbierta, default);

        Assert.NotNull(resultado);
        Assert.Null(resultado!.OcupacionDerivadaId);
        Assert.Null(resultado.PersonaAsignadaNombre);
        Assert.Equal(0, ocupacionRepo.ObtenerVigenteCallCount);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_VacanteCubiertaSinOcupacionDerivada_DevuelveCamposNullSinExcepcion()
    {
        // T1.19-bis (defensivo): estado inconsistente (Cubierta sin
        // Ocupacion) → ambos campos null sin lanzar excepción. La
        // hidratación NO debe romper el endpoint.
        var vacanteCubierta = CrearVacanteConEstadoCubierta(VacanteIdCubierta);

        var vacanteRepo = new FakeVacanteLookupConEager
        {
            Staged = { [VacanteIdCubierta] = vacanteCubierta }
        };
        var ocupacionRepo = new FakeOcupacionLookupCobertura(); // sin entradas

        var servicio = new VacanteServicioConsulta(
            (IVacanteRepository)vacanteRepo,
            (IOcupacionRepository)ocupacionRepo);

        var resultado = await servicio.ObtenerPorIdAsync(VacanteIdCubierta, default);

        Assert.NotNull(resultado);
        Assert.Null(resultado!.OcupacionDerivadaId);
        Assert.Null(resultado.PersonaAsignadaNombre);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static Vacante CrearVacanteConEstadoCubierta(Guid id)
    {
        var estadoCubierta = new EstadoVacante("Cubierta", "Cubierta", 3, true, esCubierta: true)
        {
            Id = EstadoCubiertaId
        };
        var v = new Vacante(PuestoId1, EstadoCubiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = id
        };
        typeof(Vacante).GetProperty(nameof(Vacante.EstadoVacante))!.SetValue(v, estadoCubierta);
        return v;
    }

    private static Vacante CrearVacanteConEstadoAbierta(Guid id)
    {
        var estadoAbierta = new EstadoVacante("Abierta", "Abierta", 1, false)
        {
            Id = EstadoAbiertaId
        };
        var v = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = id
        };
        typeof(Vacante).GetProperty(nameof(Vacante.EstadoVacante))!.SetValue(v, estadoAbierta);
        return v;
    }
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakeVacanteLookupConEager : IVacanteRepository
{
    public Dictionary<Guid, Vacante> Staged { get; } = [];
    public int GetByIdForUpdateCallCount { get; private set; }

    public Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult(Staged.TryGetValue(id, out var v) ? v : null);
    }

    // Stubs no ejercidos por ObtenerPorIdAsync.
    public Task AddAsync(Vacante vacante, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RegistrarCambioEstadoAsync(Vacante v, HistorialEstadoVacante h, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(Vacante vacante, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(IReadOnlyList<Vacante> Items, int TotalCount)> ListarAsync(VacanteListQuery q, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAbiertaByPuestoAsync(Guid puestoId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Vacante?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Vacante>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeOcupacionLookupCobertura : IOcupacionRepository
{
    public Dictionary<Guid, (Guid Id, string PersonaNombre)> CoberturaPorVacante { get; } = [];
    public int ObtenerVigenteCallCount { get; private set; }

    public Task<(Guid Id, string PersonaNombre)?> ObtenerVigentePorVacanteAsync(
        Guid vacanteId,
        CancellationToken cancellationToken = default)
    {
        ObtenerVigenteCallCount++;
        return Task.FromResult(
            CoberturaPorVacante.TryGetValue(vacanteId, out var tup)
                ? (tup.Id, tup.PersonaNombre)
                : ((Guid Id, string PersonaNombre)?)null);
    }

    // Stubs no ejercidos por ObtenerPorIdAsync.
    public Task<Ocupacion?> GetByIdIncludingHistoryAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Ocupacion?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(Ocupacion o, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(Ocupacion o, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Ocupacion>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Ocupacion>> ListAllIncludingHistoryAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(IReadOnlyList<Ocupacion> Items, int TotalCount)> QueryAsync(OcupacionListQuery q, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsActiveByPuestoAsync(Guid puestoId, Guid? excludingId = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsActiveByPersonaYPuestoAsync(Guid personaId, Guid puestoId, Guid? excludingId = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsActiveByVacanteAsync(Guid vacanteId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Ocupacion?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
}
