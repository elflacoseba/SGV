using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
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

            // Add default fake services with test data
            services.AddSingleton<IUnidadOrganizativaServicioConsulta>(new FakeUnidadOrganizativaServicio());
            services.AddSingleton<ICargoServicioConsulta>(new FakeCargoServicio());
            services.AddSingleton<IPuestoServicioConsulta>(new FakePuestoServicio());
            services.AddSingleton<IHabilidadServicioConsulta>(new FakeHabilidadServicio());

            // Apply additional overrides (e.g. empty collections)
            _configureServices?.Invoke(services);
        });
    }
}
