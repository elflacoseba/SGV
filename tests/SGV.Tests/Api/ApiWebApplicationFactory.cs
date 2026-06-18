using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;

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

internal sealed class FakeTipoUnidadOrganizativaServicio : ITipoUnidadOrganizativaServicioConsulta
{
    public static readonly Guid DireccionId = TipoUnidadOrganizativaConstantes.DireccionId;
    public static readonly Guid AreaId = TipoUnidadOrganizativaConstantes.AreaId;

    private static readonly IReadOnlyList<TipoUnidadOrganizativaDto> SeedData =
    [
        new(TipoUnidadOrganizativaConstantes.InstitucionId, "Institucion", "Institución"),
        new(TipoUnidadOrganizativaConstantes.FacultadId,    "Facultad",    "Facultad"),
        new(TipoUnidadOrganizativaConstantes.SecretariaId,  "Secretaria",  "Secretaría"),
        new(TipoUnidadOrganizativaConstantes.DireccionId,   "Direccion",   "Dirección"),
        new(TipoUnidadOrganizativaConstantes.DepartamentoId,"Departamento","Departamento"),
        new(TipoUnidadOrganizativaConstantes.DivisionId,    "Division",    "División"),
        new(TipoUnidadOrganizativaConstantes.AreaId,        "Area",        "Área"),
    ];

    private readonly IReadOnlyList<TipoUnidadOrganizativaDto> _data;

    public FakeTipoUnidadOrganizativaServicio(bool isEmpty = false)
    {
        _data = isEmpty ? [] : SeedData;
    }

    public Task<IReadOnlyList<TipoUnidadOrganizativaDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<TipoUnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakeUnidadOrganizativaServicio : IUnidadOrganizativaServicioConsulta
{
    public static readonly Guid UnidadId1 = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<UnidadOrganizativaDto> _data;

    public FakeUnidadOrganizativaServicio(bool isEmpty = false)
    {
        _data = isEmpty
            ? []
            : [new(UnidadId1, "GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección",
                  "Máxima autoridad ejecutiva", null, null, null)];
    }

    public Task<IReadOnlyList<UnidadOrganizativaDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));

    public Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(
        UnidadOrganizativaQuery query, CancellationToken ct = default)
    {
        var filtered = _data.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(d =>
                d.Codigo.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                d.Nombre.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
        if (query.TipoUnidadOrganizativaId.HasValue)
            filtered = filtered.Where(d => d.TipoUnidadOrganizativaId == query.TipoUnidadOrganizativaId.Value);
        if (query.UnidadPadreId.HasValue)
            filtered = filtered.Where(d => d.UnidadPadreId == query.UnidadPadreId.Value);

        var list = filtered.ToList();
        var total = list.Count;
        var items = list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return Task.FromResult(new PagedResult<UnidadOrganizativaDto>(items, total, query.Page, query.PageSize));
    }

    public Task<IReadOnlyList<UnidadOrganizativaTreeNodeDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var roots = _data.Where(d => d.UnidadPadreId is null).ToList();
        var tree = roots.Select(r => BuildTreeNode(r, _data)).ToList();
        return Task.FromResult<IReadOnlyList<UnidadOrganizativaTreeNodeDto>>(tree);
    }

    private static UnidadOrganizativaTreeNodeDto BuildTreeNode(
        UnidadOrganizativaDto node, IReadOnlyList<UnidadOrganizativaDto> all)
    {
        var children = all
            .Where(d => d.UnidadPadreId == node.Id)
            .Select(c => BuildTreeNode(c, all))
            .ToList();
        return new UnidadOrganizativaTreeNodeDto(
            node.Id, node.Codigo, node.Nombre,
            node.TipoUnidadOrganizativaId, node.TipoUnidadNombre, children);
    }
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
                request.TipoUnidadOrganizativaId, string.Empty, request.Descripcion, request.VigenteDesde,
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
                request.TipoUnidadOrganizativaId, string.Empty, request.Descripcion, request.VigenteDesde,
                request.VigenteHasta, null)));
    }

    public Task<UnidadOrganizativaCommandResult> CambiarUnidadPadreAsync(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CambiarUnidadPadreHandler is not null) return CambiarUnidadPadreHandler(id, request, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección", null, null, null, request.UnidadPadreId)));
    }

    public Task<UnidadOrganizativaCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (EliminarHandler is not null) return EliminarHandler(id, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección", null, null, null, null)));
    }

    public Task<UnidadOrganizativaCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección", null, null, null, null)));
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
            services.RemoveService<ITipoUnidadOrganizativaServicioConsulta>();

            // Add default fake services with test data
            services.AddSingleton<IUnidadOrganizativaServicioConsulta>(new FakeUnidadOrganizativaServicio());
            services.AddSingleton<ICargoServicioConsulta>(new FakeCargoServicio());
            services.AddSingleton<IPuestoServicioConsulta>(new FakePuestoServicio());
            services.AddSingleton<IHabilidadServicioConsulta>(new FakeHabilidadServicio());
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(new FakeUnidadOrganizativaServicioComandos());
            services.AddSingleton<ITipoUnidadOrganizativaServicioConsulta>(new FakeTipoUnidadOrganizativaServicio());

            // Apply additional overrides (e.g. empty collections)
            _configureServices?.Invoke(services);
        });
    }
}
