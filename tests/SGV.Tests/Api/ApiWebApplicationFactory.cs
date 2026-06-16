using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Tests.Api;

internal static class ServiceCollectionExtensions
{
    public static void RemoveService<T>(this IServiceCollection services) where T : class
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null)
            services.Remove(descriptor);
    }
}

internal sealed class FakeUnidadOrganizativaServicio : IUnidadOrganizativaServicioConsulta
{
    public static readonly Guid UnidadId1 = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<UnidadOrganizativaDto> _data;

    public FakeUnidadOrganizativaServicio(bool isEmpty = false)
    {
        _data = isEmpty
            ? []
            : [new(UnidadId1, "GER", "Gerencia General", "Dirección",
                  "Máxima autoridad ejecutiva", null, null, null)];
    }

    public Task<IReadOnlyList<UnidadOrganizativaDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakeCargoServicio : ICargoServicioConsulta
{
    public static readonly Guid CargoId1 = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<CargoDto> _data;

    public FakeCargoServicio(bool isEmpty = false)
    {
        _data = isEmpty
            ? []
            : [new(CargoId1, "DIRECTOR", "Director", "Conducción media", null)];
    }

    public Task<IReadOnlyList<CargoDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakePuestoServicio : IPuestoServicioConsulta
{
    public static readonly Guid PuestoId1 = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    public static readonly Guid UnidadId1 = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid CargoId1 = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<PuestoDto> _data;

    public FakePuestoServicio(bool isEmpty = false)
    {
        _data = isEmpty
            ? []
            : [new(PuestoId1, "GER-001", "Gerente General", "Responsable de la gerencia",
                  UnidadId1, "Gerencia General", CargoId1, "Director", null)];
    }

    public Task<IReadOnlyList<PuestoDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakeHabilidadServicio : IHabilidadServicioConsulta
{
    public static readonly Guid HabilidadId1 = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<HabilidadDto> _data;

    public FakeHabilidadServicio(bool isEmpty = false)
    {
        _data = isEmpty
            ? []
            : [new(HabilidadId1, "PROG", "Programación", "Lenguajes de programación", "Técnica")];
    }

    public Task<IReadOnlyList<HabilidadDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakeUnidadOrganizativaServicioComandos : IUnidadOrganizativaServicioComandos
{
    public static readonly Guid DefaultUnidadId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    public Func<CrearUnidadOrganizativaRequest, CancellationToken, Task<UnidadOrganizativaCommandResult>>? CrearHandler { get; set; }
    public Func<Guid, ActualizarUnidadOrganizativaRequest, CancellationToken, Task<UnidadOrganizativaCommandResult>>? ActualizarHandler { get; set; }
    public Func<Guid, CambiarUnidadPadreRequest, CancellationToken, Task<UnidadOrganizativaCommandResult>>? CambiarUnidadPadreHandler { get; set; }
    public Func<Guid, CancellationToken, Task<UnidadOrganizativaCommandResult>>? EliminarHandler { get; set; }

    public Task<UnidadOrganizativaCommandResult> CrearAsync(
        CrearUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CrearHandler is not null) return CrearHandler(request, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(DefaultUnidadId, request.Codigo, request.Nombre,
                request.TipoUnidad, request.Descripcion, request.VigenteDesde,
                request.VigenteHasta, request.UnidadPadreId)));
    }

    public Task<UnidadOrganizativaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ActualizarHandler is not null) return ActualizarHandler(id, request, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, request.Codigo, request.Nombre,
                request.TipoUnidad, request.Descripcion, request.VigenteDesde,
                request.VigenteHasta, null)));
    }

    public Task<UnidadOrganizativaCommandResult> CambiarUnidadPadreAsync(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CambiarUnidadPadreHandler is not null) return CambiarUnidadPadreHandler(id, request, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", "Dirección", null, null, null, request.UnidadPadreId)));
    }

    public Task<UnidadOrganizativaCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (EliminarHandler is not null) return EliminarHandler(id, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", "Dirección", null, null, null, null)));
    }
}

public class ApiWebApplicationFactory : WebApplicationFactory<SGV.Api.Program>
{
    private readonly Action<IServiceCollection>? _configureServices;

    public ApiWebApplicationFactory(Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real service registrations to replace with fakes
            services.RemoveService<IUnidadOrganizativaServicioConsulta>();
            services.RemoveService<ICargoServicioConsulta>();
            services.RemoveService<IPuestoServicioConsulta>();
            services.RemoveService<IHabilidadServicioConsulta>();
            services.RemoveService<IUnidadOrganizativaServicioComandos>();

            // Add default fake services with test data
            services.AddSingleton<IUnidadOrganizativaServicioConsulta>(new FakeUnidadOrganizativaServicio());
            services.AddSingleton<ICargoServicioConsulta>(new FakeCargoServicio());
            services.AddSingleton<IPuestoServicioConsulta>(new FakePuestoServicio());
            services.AddSingleton<IHabilidadServicioConsulta>(new FakeHabilidadServicio());
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(new FakeUnidadOrganizativaServicioComandos());

            // Apply additional overrides (e.g. empty collections)
            _configureServices?.Invoke(services);
        });
    }
}
