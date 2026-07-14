using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Dominio.Ocupaciones;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;

namespace SGV.Tests.Api;

internal static class FakeAuthenticationDefaults
{
    public const string Scheme = "Test";
    public const string AdminToken = "admin";
    public const string UserToken = "user";

    public static AuthenticationHeaderValue AdminHeader => new(Scheme, AdminToken);
    public static AuthenticationHeaderValue UserHeader => new(Scheme, UserToken);
}

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
    public static readonly Guid UnidadConPadreId = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    private readonly IReadOnlyList<UnidadOrganizativaDto> _activas;
    private readonly IReadOnlyList<UnidadOrganizativaDto> _eliminadas;

    public FakeUnidadOrganizativaServicio(bool isEmpty = false, bool withPadreData = false, bool withEliminadas = false)
    {
        if (isEmpty)
        {
            _activas = [];
            _eliminadas = [];
            return;
        }

        if (withPadreData)
        {
            _activas =
            [
                new(UnidadId1, "GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección",
                    "Máxima autoridad ejecutiva", null, null, null, null, null),
                new(UnidadConPadreId, "AREA-01", "Área Operativa", TipoUnidadOrganizativaConstantes.AreaId, "Área",
                    null, null, null, UnidadId1, "GER", "Gerencia General")
            ];
        }
        else
        {
            _activas =
            [
                new(UnidadId1, "GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección",
                    "Máxima autoridad ejecutiva", null, null, null, null, null)
            ];
        }

        _eliminadas = withEliminadas
            ? [new(Guid.Parse("e0000000-0000-0000-0000-000000000001"), "ELIM-01", "Unidad Eliminada",
                TipoUnidadOrganizativaConstantes.AreaId, "Área",
                null, null, null, null, null, null)]
            : [];
    }

    public Task<IReadOnlyList<UnidadOrganizativaDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_activas);

    public Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_activas.Concat(_eliminadas).FirstOrDefault(d => d.Id == id));

    public Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(
        UnidadOrganizativaQuery query, CancellationToken ct = default)
    {
        var source = query.Segmento == UnidadOrganizativaSegmentoListado.Eliminadas ? _eliminadas : _activas;
        var filtered = source.AsEnumerable();
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
        var roots = _activas.Where(d => d.UnidadPadreId is null).ToList();
        var tree = roots.Select(r => BuildTreeNode(r, _activas)).ToList();
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

internal sealed class FakeNivelCargoServicioConsulta : INivelCargoServicioConsulta
{
    public static readonly Guid Nivel1Id = NivelCargoConstantes.DirectivoId;
    public static readonly Guid Nivel2Id = NivelCargoConstantes.OperativoId;

    private static readonly IReadOnlyList<NivelCargoDto> SeedData =
    [
        new(Nivel1Id, "Directivo", "Directivo", 1, 1),
        new(Nivel2Id, "Operativo", "Operativo", 3, 3),
    ];

    private readonly IReadOnlyList<NivelCargoDto> _data;

    public FakeNivelCargoServicioConsulta(bool isEmpty = false)
    {
        _data = isEmpty ? [] : SeedData;
    }

    public Task<IReadOnlyList<NivelCargoDto>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_data);

    public Task<NivelCargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakeCargoServicioComandos : ICargoServicioComandos
{
    public static readonly Guid DefaultCargoId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultNivelId = NivelCargoConstantes.DirectivoId;

    public Func<CrearCargoRequest, CancellationToken, Task<CargoCommandResult>>? CrearHandler { get; set; }
    public Func<Guid, ActualizarCargoRequest, CancellationToken, Task<CargoCommandResult>>? ActualizarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<CargoCommandResult>>? DesactivarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<CargoCommandResult>>? ReactivarHandler { get; set; }

    public Task<CargoCommandResult> CrearAsync(
        CrearCargoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CrearHandler is not null) return CrearHandler(request, cancellationToken);
        return Task.FromResult(CargoCommandResult.Success(
            new CargoDto(DefaultCargoId, request.Codigo, request.Nombre, request.Descripcion, request.NivelId)));
    }

    public Task<CargoCommandResult> ActualizarAsync(
        Guid id,
        ActualizarCargoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ActualizarHandler is not null) return ActualizarHandler(id, request, cancellationToken);
        return Task.FromResult(CargoCommandResult.Success(
            new CargoDto(id, "DIRECTOR", request.Nombre, request.Descripcion, request.NivelId, "Directivo")));
    }

    public Task<CargoCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (DesactivarHandler is not null) return DesactivarHandler(id, cancellationToken);
        return Task.FromResult(CargoCommandResult.Success(
            new CargoDto(id, "DIRECTOR", "Director", null, DefaultNivelId, "Directivo")));
    }

    public Task<CargoCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (ReactivarHandler is not null) return ReactivarHandler(id, cancellationToken);
        return Task.FromResult(CargoCommandResult.Success(
            new CargoDto(id, "DIRECTOR", "Director", null, DefaultNivelId, "Directivo")));
    }
}

internal sealed class FakeCargoServicio : ICargoServicioConsulta
{
    public static readonly Guid CargoId1 = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid CargoEliminadoId1 = Guid.Parse("be000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<CargoDto> _data;
    private readonly IReadOnlyList<CargoDto> _eliminadas;

    public FakeCargoServicio(bool isEmpty = false, bool withEliminadas = false)
    {
        _data = isEmpty
            ? []
            : [new(CargoId1, "DIRECTOR", "Director", null, Guid.Parse("70000000-0000-0000-0000-000000000001"))];

        _eliminadas = (isEmpty || !withEliminadas)
            ? []
            : [new(CargoEliminadoId1, "ELIM-001", "Cargo Eliminado", null, Guid.Parse("70000000-0000-0000-0000-000000000001"))];
    }

    public Task<IReadOnlyList<CargoDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.Concat(_eliminadas).FirstOrDefault(d => d.Id == id));

    public Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken ct = default)
    {
        var source = query.Segmento == CargoSegmentoListado.Eliminadas ? _eliminadas : _data;
        var filtered = source.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var lowered = query.Search.ToLowerInvariant();
            filtered = filtered.Where(d =>
                d.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || d.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        var total = list.Count;
        var items = list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult(new PagedResult<CargoDto>(items, total, query.Page, query.PageSize));
    }
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
    public static readonly Guid HabilidadEliminadaId1 = Guid.Parse("de000000-0000-0000-0000-000000000001");
    public static readonly Guid HabilidadInactivaId1 = Guid.Parse("d1000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<HabilidadDto> _data;
    private readonly IReadOnlyList<HabilidadDto> _eliminadas;
    private readonly HashSet<Guid> _inactivas;

    public FakeHabilidadServicio(
        bool isEmpty = false,
        bool withEliminadas = false,
        bool withInactive = false,
        IReadOnlyCollection<Guid>? inactiveIds = null)
    {
        _data = isEmpty
            ? []
            : [new(HabilidadId1, "PROG", "Programación", "Lenguajes de programación", "Técnica")];

        _eliminadas = (isEmpty || !withEliminadas)
            ? []
            : [new(HabilidadEliminadaId1, "DEL-001", "Eliminada", "Descripción eliminada", "General")];

        // Set de ids soft-disabled (IsActive=false, IsDeleted=false). El
        // repository filtra por IsActive=true, por lo que estos ids existen
        // conceptualmente pero GetByIdAsync devuelve null. Tests que necesiten
        // ids arbitrarios pueden pasarlos vía inactiveIds; el flag withInactive
        // agrega el id semilla para los escenarios MUST del sdd-verify.
        _inactivas = new HashSet<Guid>();
        if (withInactive)
        {
            _inactivas.Add(HabilidadInactivaId1);
        }
        if (inactiveIds is not null)
        {
            foreach (var id in inactiveIds)
            {
                _inactivas.Add(id);
            }
        }
    }

    public Task<IReadOnlyList<HabilidadDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // La spec MUST escenario 1: una habilidad soft-disable responde 404.
        if (_inactivas.Contains(id))
        {
            return Task.FromResult<HabilidadDto?>(null);
        }

        return Task.FromResult(_data.Concat(_eliminadas).FirstOrDefault(d => d.Id == id));
    }

    public Task<PagedResult<HabilidadDto>> QueryAsync(HabilidadListQuery query, CancellationToken ct = default)
    {
        var source = query.Segmento == HabilidadSegmentoListado.Eliminadas ? _eliminadas : _data;
        var filtered = source.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var lowered = query.Search.ToLowerInvariant();
            filtered = filtered.Where(d =>
                d.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || d.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || (d.Categoria?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.Descripcion?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = filtered.ToList();
        var total = list.Count;
        var items = list.Skip(Math.Max(0, (query.Page - 1) * query.PageSize)).Take(query.PageSize).ToList();
        return Task.FromResult(new PagedResult<HabilidadDto>(items, total, query.Page, query.PageSize));
    }
}

internal sealed class FakeNivelHabilidadServicio : INivelHabilidadServicioConsulta
{
    public static readonly Guid BasicoId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    public static readonly Guid AvanzadoId = Guid.Parse("91000000-0000-0000-0000-000000000002");

    private static readonly IReadOnlyList<NivelHabilidadDto> SeedData =
    [
        new(BasicoId, "BASICO", "Básico", 1, 1),
        new(AvanzadoId, "AVANZADO", "Avanzado", 3, 3),
    ];

    private readonly IReadOnlyList<NivelHabilidadDto> _data;

    public FakeNivelHabilidadServicio(bool isEmpty = false)
    {
        _data = isEmpty ? [] : SeedData;
    }

    public Task<IReadOnlyList<NivelHabilidadDto>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_data);

    public Task<NivelHabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakePuestoServicioComandos : IPuestoServicioComandos
{
    public static readonly Guid DefaultPuestoId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultUnidadId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultCargoId = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    public Func<CrearPuestoRequest, CancellationToken, Task<PuestoCommandResult>>? CrearHandler { get; set; }
    public Func<Guid, ActualizarPuestoRequest, CancellationToken, Task<PuestoCommandResult>>? ActualizarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<PuestoCommandResult>>? DesactivarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<PuestoCommandResult>>? ReactivarHandler { get; set; }

    public Task<PuestoCommandResult> CrearAsync(
        CrearPuestoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CrearHandler is not null) return CrearHandler(request, cancellationToken);
        return Task.FromResult(PuestoCommandResult.Success(
            new PuestoDto(DefaultPuestoId, request.Codigo, request.Nombre, request.Descripcion,
                request.UnidadOrganizativaId, "Gerencia General",
                request.CargoId, "Director", request.PuestoSuperiorId)));
    }

    public Task<PuestoCommandResult> ActualizarAsync(
        Guid id,
        ActualizarPuestoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ActualizarHandler is not null) return ActualizarHandler(id, request, cancellationToken);
        return Task.FromResult(PuestoCommandResult.Success(
            new PuestoDto(id, "GER-001", request.Nombre, request.Descripcion,
                DefaultUnidadId, "Gerencia General", DefaultCargoId, "Director", request.PuestoSuperiorId)));
    }

    public Task<PuestoCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (DesactivarHandler is not null) return DesactivarHandler(id, cancellationToken);
        return Task.FromResult(PuestoCommandResult.Success(
            new PuestoDto(id, "GER-001", "Gerente General", null,
                DefaultUnidadId, "Gerencia General", DefaultCargoId, "Director", null)));
    }

    public Task<PuestoCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (ReactivarHandler is not null) return ReactivarHandler(id, cancellationToken);
        return Task.FromResult(PuestoCommandResult.Success(
            new PuestoDto(id, "GER-001", "Gerente General", null,
                DefaultUnidadId, "Gerencia General", DefaultCargoId, "Director", null)));
    }
}

internal sealed class FakeUnidadOrganizativaServicioComandos : IUnidadOrganizativaServicioComandos
{
    public static readonly Guid DefaultUnidadId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    public Func<CrearUnidadOrganizativaRequest, CancellationToken, Task<UnidadOrganizativaCommandResult>>? CrearHandler { get; set; }
    public Func<Guid, ActualizarUnidadOrganizativaRequest, CancellationToken, Task<UnidadOrganizativaCommandResult>>? ActualizarHandler { get; set; }
    public Func<Guid, CambiarUnidadPadreRequest, CancellationToken, Task<UnidadOrganizativaCommandResult>>? CambiarUnidadPadreHandler { get; set; }
    public Func<Guid, CancellationToken, Task<UnidadOrganizativaCommandResult>>? EliminarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<UnidadOrganizativaCommandResult>>? ReactivarHandler { get; set; }

    public Task<UnidadOrganizativaCommandResult> CrearAsync(
        CrearUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CrearHandler is not null) return CrearHandler(request, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(DefaultUnidadId, request.Codigo, request.Nombre,
                request.TipoUnidadOrganizativaId, string.Empty, request.Descripcion, request.VigenteDesde,
                request.VigenteHasta, request.UnidadPadreId, null, null)));
    }

    public Task<UnidadOrganizativaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ActualizarHandler is not null) return ActualizarHandler(id, request, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, string.Empty, request.Nombre,
                request.TipoUnidadOrganizativaId, string.Empty, request.Descripcion, request.VigenteDesde,
                request.VigenteHasta, null, null, null)));
    }

    public Task<UnidadOrganizativaCommandResult> CambiarUnidadPadreAsync(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CambiarUnidadPadreHandler is not null) return CambiarUnidadPadreHandler(id, request, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección", null, null, null, request.UnidadPadreId, null, null)));
    }

    public Task<UnidadOrganizativaCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (EliminarHandler is not null) return EliminarHandler(id, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección", null, null, null, null, null, null)));
    }

    public Task<UnidadOrganizativaCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (ReactivarHandler is not null) return ReactivarHandler(id, cancellationToken);
        return Task.FromResult(UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(id, "GER", "Gerencia", TipoUnidadOrganizativaConstantes.DireccionId, "Dirección", null, null, null, null, null, null)));
    }
}

internal sealed class FakeHabilidadServicioComandos : IHabilidadServicioComandos
{
    public static readonly Guid DefaultHabilidadId = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    public Func<CrearHabilidadRequest, CancellationToken, Task<HabilidadCommandResult>>? CrearHandler { get; set; }
    public Func<Guid, ActualizarHabilidadRequest, CancellationToken, Task<HabilidadCommandResult>>? ActualizarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<HabilidadCommandResult>>? DesactivarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<HabilidadCommandResult>>? ReactivarHandler { get; set; }

    public Task<HabilidadCommandResult> CrearAsync(
        CrearHabilidadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CrearHandler is not null) return CrearHandler(request, cancellationToken);
        return Task.FromResult(HabilidadCommandResult.Success(
            new HabilidadDto(DefaultHabilidadId, request.Codigo, request.Nombre, request.Descripcion, request.Categoria)));
    }

    public Task<HabilidadCommandResult> ActualizarAsync(
        Guid id,
        ActualizarHabilidadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ActualizarHandler is not null) return ActualizarHandler(id, request, cancellationToken);
        return Task.FromResult(HabilidadCommandResult.Success(
            new HabilidadDto(id, request.Codigo, request.Nombre, request.Descripcion, request.Categoria)));
    }

    public Task<HabilidadCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (DesactivarHandler is not null) return DesactivarHandler(id, cancellationToken);
        return Task.FromResult(HabilidadCommandResult.Success(
            new HabilidadDto(id, "PROG", "Programación", "Lenguajes de programación", "Técnica")));
    }

    public Task<HabilidadCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (ReactivarHandler is not null) return ReactivarHandler(id, cancellationToken);
        return Task.FromResult(HabilidadCommandResult.Success(
            new HabilidadDto(id, "PROG", "Programación", "Lenguajes de programación", "Técnica")));
    }
}

internal sealed class FakePersonaServicioConsulta : IPersonaServicioConsulta
{
    public static readonly Guid PersonaId1 = Guid.Parse("e0000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<PersonaDto> _data;

    public FakePersonaServicioConsulta(bool isEmpty = false)
    {
        _data = isEmpty
            ? []
            : [new(PersonaId1, "LEG-001", "Juan", "Perez", "juan@test.com", "DNI", "12345678", "555-0001", true)];
    }

    public Task<IReadOnlyList<PersonaDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakePersonaServicioComandos : IPersonaServicioComandos
{
    public static readonly Guid DefaultPersonaId = Guid.Parse("e0000000-0000-0000-0000-000000000001");

    public Func<CrearPersonaRequest, CancellationToken, Task<PersonaCommandResult>>? CrearHandler { get; set; }
    public Func<Guid, ActualizarPersonaRequest, CancellationToken, Task<PersonaCommandResult>>? ActualizarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<PersonaCommandResult>>? DesactivarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<PersonaCommandResult>>? ReactivarHandler { get; set; }

    public Task<PersonaCommandResult> CrearAsync(
        CrearPersonaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CrearHandler is not null) return CrearHandler(request, cancellationToken);
        return Task.FromResult(PersonaCommandResult.Success(
            new PersonaDto(DefaultPersonaId, request.Legajo, request.Nombres, request.Apellidos,
                request.Email, request.TipoDocumento, request.NumeroDocumento, request.Telefono, true)));
    }

    public Task<PersonaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarPersonaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ActualizarHandler is not null) return ActualizarHandler(id, request, cancellationToken);
        return Task.FromResult(PersonaCommandResult.Success(
            new PersonaDto(id, request.Legajo, request.Nombres, request.Apellidos,
                request.Email, request.TipoDocumento, request.NumeroDocumento, request.Telefono, true)));
    }

    public Task<PersonaCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (DesactivarHandler is not null) return DesactivarHandler(id, cancellationToken);
        return Task.FromResult(PersonaCommandResult.Success(
            new PersonaDto(id, "LEG-001", "Juan", "Perez", "juan@test.com", "DNI", "12345678", "555-0001", false)));
    }

    public Task<PersonaCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (ReactivarHandler is not null) return ReactivarHandler(id, cancellationToken);
        return Task.FromResult(PersonaCommandResult.Success(
            new PersonaDto(id, "LEG-001", "Juan", "Perez", "juan@test.com", "DNI", "12345678", "555-0001", true)));
    }
}

internal sealed class FakeOcupacionServicioConsulta : IOcupacionServicioConsulta
{
    public static readonly Guid OcupacionId1 = Guid.Parse("f0000000-0000-0000-0000-000000000001");
    public static readonly Guid PersonaId1 = FakePersonaServicioConsulta.PersonaId1;
    public static readonly Guid PuestoId1 = FakePuestoServicio.PuestoId1;

    private readonly IReadOnlyList<OcupacionDto> _data;

    public FakeOcupacionServicioConsulta(bool isEmpty = false)
    {
        _data = isEmpty
            ? []
            : [new(OcupacionId1, PersonaId1, "Juan Perez", PuestoId1, "Gerente General",
                  new DateOnly(2024, 1, 15), null, TipoAsignacion.Permanente, null, "Activo")];
    }

    public Task<PagedResult<OcupacionDto>> ListAsync(bool includeHistory = false, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<OcupacionDto>(_data, _data.Count, page, pageSize));

    public Task<OcupacionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_data.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakeOcupacionServicioComandos : IOcupacionServicioComandos
{
    public static readonly Guid DefaultOcupacionId = Guid.Parse("f0000000-0000-0000-0000-000000000001");

    public Func<CrearOcupacionRequest, CancellationToken, Task<OcupacionCommandResult>>? CrearHandler { get; set; }
    public Func<Guid, ActualizarOcupacionRequest, CancellationToken, Task<OcupacionCommandResult>>? ActualizarHandler { get; set; }
    public Func<Guid, FinalizarOcupacionRequest, CancellationToken, Task<OcupacionCommandResult>>? FinalizarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<OcupacionCommandResult>>? EliminarHandler { get; set; }
    public Func<Guid, CancellationToken, Task<OcupacionCommandResult>>? ReactivarHandler { get; set; }

    public Task<OcupacionCommandResult> CrearAsync(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CrearHandler is not null) return CrearHandler(request, cancellationToken);
        return Task.FromResult(OcupacionCommandResult.Success(
            new OcupacionDto(DefaultOcupacionId, request.PersonaId, "Juan Perez",
                request.PuestoId, "Gerente General", request.FechaInicio, null,
                request.TipoAsignacion, request.Observaciones, "Activo")));
    }

    public Task<OcupacionCommandResult> ActualizarAsync(
        Guid id,
        ActualizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ActualizarHandler is not null) return ActualizarHandler(id, request, cancellationToken);
        return Task.FromResult(OcupacionCommandResult.Success(
            new OcupacionDto(id, request.PersonaId, "Juan Perez",
                request.PuestoId, "Gerente General", request.FechaInicio, null,
                request.TipoAsignacion, request.Observaciones, "Activo")));
    }

    public Task<OcupacionCommandResult> FinalizarAsync(
        Guid id,
        FinalizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (FinalizarHandler is not null) return FinalizarHandler(id, request, cancellationToken);
        return Task.FromResult(OcupacionCommandResult.Success(
            new OcupacionDto(id, FakeOcupacionServicioConsulta.PersonaId1, "Juan Perez",
                FakeOcupacionServicioConsulta.PuestoId1, "Gerente General",
                new DateOnly(2024, 1, 15), request.FechaFin, TipoAsignacion.Permanente,
                request.Observaciones, "Finalizado")));
    }

    public Task<OcupacionCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (EliminarHandler is not null) return EliminarHandler(id, cancellationToken);
        return Task.FromResult(OcupacionCommandResult.Success(
            new OcupacionDto(id, FakeOcupacionServicioConsulta.PersonaId1, "Juan Perez",
                FakeOcupacionServicioConsulta.PuestoId1, "Gerente General",
                new DateOnly(2024, 1, 15), null, TipoAsignacion.Permanente, null, "Eliminado")));
    }

    public Task<OcupacionCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (ReactivarHandler is not null) return ReactivarHandler(id, cancellationToken);
        return Task.FromResult(OcupacionCommandResult.Success(
            new OcupacionDto(id, FakeOcupacionServicioConsulta.PersonaId1, "Juan Perez",
                FakeOcupacionServicioConsulta.PuestoId1, "Gerente General",
                new DateOnly(2024, 1, 15), null, TipoAsignacion.Permanente, null, "Activo")));
    }
}

internal sealed class FakeUsuarioServicioConsulta : IUsuarioServicioConsulta
{
    private static readonly IReadOnlyList<UsuarioDto> Users =
    [
        new("user-1", FakePersonaServicioConsulta.PersonaId1, "admin", "admin@test.com", [RolesSgv.Administrador])
    ];

    public Task<IReadOnlyList<UsuarioDto>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Users);
}

internal sealed class FakeUsuarioServicioComandos : IUsuarioServicioComandos
{
    public Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(UsuarioCommandResult.Success(new UsuarioDto("user-1", request.PersonaId, request.UserName, request.Email, request.Roles)));

    public Task<UsuarioCommandResult> AsignarRolesAsync(string userId, AsignarRolesRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(UsuarioCommandResult.Success(new UsuarioDto(userId, FakePersonaServicioConsulta.PersonaId1, "admin", "admin@test.com", request.Roles)));
}

internal sealed class FakeRolServicioConsulta : IRolServicioConsulta
{
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(RolesSgv.Todos);
}

internal sealed class FakeAuthServicio(LoginResponse? response = null, bool returnUnauthorized = false) : IAuthServicio
{
    public static FakeAuthServicio Unauthorized() => new(returnUnauthorized: true);

    public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(returnUnauthorized ? null : response ?? new LoginResponse("fake-token", DateTimeOffset.UtcNow.AddMinutes(60)));
}

internal sealed class FakeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) || header.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AuthenticationHeaderValue.TryParse(header[0], out var value) || value.Scheme != FakeAuthenticationDefaults.Scheme)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = value.Parameter ?? string.Empty;
        var principal = new ClaimsPrincipal(BuildIdentity(token));
        var ticket = new AuthenticationTicket(principal, FakeAuthenticationDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static ClaimsIdentity BuildIdentity(string token)
    {
        if (token == FakeAuthenticationDefaults.AdminToken)
        {
            return new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    new Claim(ClaimTypes.Name, "admin"),
                    new Claim(ClaimTypes.Role, RolesSgv.Administrador)
                },
                FakeAuthenticationDefaults.Scheme);
        }

        return new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Name, "user")
            },
            FakeAuthenticationDefaults.Scheme);
    }
}

public class ApiWebApplicationFactory : WebApplicationFactory<SGV.Api.Program>
{
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly Action<IConfigurationBuilder>? _configureConfig;

    public ApiWebApplicationFactory(
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? configureConfig = null)
    {
        _configureServices = configureServices;
        _configureConfig = configureConfig;
    }

    /// <summary>
    /// Deriva una nueva factory que aplica los overrides provistos encima
    /// de la configuración de esta factory. La factory devuelta es
    /// <see cref="IAsyncDisposable"/> y pertenece al caller — idealmente
    /// usada con <c>await using</c>. No comparte el host con la raíz.
    /// </summary>
    public ApiWebApplicationFactory WithOverrides(
        Action<IServiceCollection>? configureServices = null,
        Action<IConfigurationBuilder>? configureConfig = null)
    {
        return new ApiWebApplicationFactory(
            Merge(_configureServices, configureServices),
            Merge(_configureConfig, configureConfig));
    }

    private static Action<T>? Merge<T>(Action<T>? current, Action<T>? next) where T : class
    {
        return (current, next) switch
        {
            (null, null) => null,
            (null, not null) => next,
            (not null, null) => current,
            _ => arg => { current(arg); next!(arg); },
        };
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.AdminHeader;
        return client;
    }

    public HttpClient CreateNonAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Allow tests to override configuration (e.g. for connection string validation tests).
        // The default connection string is added first; override runs after so tests can replace it.
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Ensure a valid default for tests that don't override connection strings,
            // but allow _configureConfig to replace it for validation tests.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SgvDatabase"] = "Server=localhost;Database=sgv_test;Uid=root;Connection Timeout=5;"
            });

            _configureConfig?.Invoke(config);
        });

        builder.ConfigureServices(services =>
        {
            // Remove real service registrations to replace with fakes
            services.RemoveService<IUnidadOrganizativaServicioConsulta>();
            services.RemoveService<ICargoServicioConsulta>();
            services.RemoveService<IPuestoServicioConsulta>();
            services.RemoveService<IHabilidadServicioConsulta>();
            services.RemoveService<IUnidadOrganizativaServicioComandos>();
            services.RemoveService<ITipoUnidadOrganizativaServicioConsulta>();
            services.RemoveService<ICargoServicioComandos>();
            services.RemoveService<IPuestoServicioComandos>();
            services.RemoveService<INivelCargoServicioConsulta>();
            services.RemoveService<INivelHabilidadServicioConsulta>();
            services.RemoveService<IHabilidadServicioComandos>();
            services.RemoveService<IPersonaServicioConsulta>();
            services.RemoveService<IPersonaServicioComandos>();
            services.RemoveService<IUsuarioServicioConsulta>();
            services.RemoveService<IUsuarioServicioComandos>();
            services.RemoveService<IRolServicioConsulta>();
            services.RemoveService<IAuthServicio>();
            services.RemoveService<IOcupacionServicioConsulta>();
            services.RemoveService<IOcupacionServicioComandos>();

            // Add default fake services with test data
            services.AddSingleton<IUnidadOrganizativaServicioConsulta>(new FakeUnidadOrganizativaServicio());
            services.AddSingleton<ICargoServicioConsulta>(new FakeCargoServicio());
            services.AddSingleton<IPuestoServicioConsulta>(new FakePuestoServicio());
            services.AddSingleton<IHabilidadServicioConsulta>(new FakeHabilidadServicio());
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(new FakeUnidadOrganizativaServicioComandos());
            services.AddSingleton<ITipoUnidadOrganizativaServicioConsulta>(new FakeTipoUnidadOrganizativaServicio());
            services.AddSingleton<ICargoServicioComandos>(new FakeCargoServicioComandos());
            services.AddSingleton<IPuestoServicioComandos>(new FakePuestoServicioComandos());
            services.AddSingleton<INivelCargoServicioConsulta>(new FakeNivelCargoServicioConsulta());
            services.AddSingleton<INivelHabilidadServicioConsulta>(new FakeNivelHabilidadServicio());
            services.AddSingleton<IHabilidadServicioComandos>(new FakeHabilidadServicioComandos());
            services.AddSingleton<IPersonaServicioConsulta>(new FakePersonaServicioConsulta());
            services.AddSingleton<IPersonaServicioComandos>(new FakePersonaServicioComandos());
            services.AddSingleton<IUsuarioServicioConsulta>(new FakeUsuarioServicioConsulta());
            services.AddSingleton<IUsuarioServicioComandos>(new FakeUsuarioServicioComandos());
            services.AddSingleton<IRolServicioConsulta>(new FakeRolServicioConsulta());
            services.AddSingleton<IAuthServicio>(new FakeAuthServicio());
            services.AddSingleton<IOcupacionServicioConsulta>(new FakeOcupacionServicioConsulta());
            services.AddSingleton<IOcupacionServicioComandos>(new FakeOcupacionServicioComandos());

            services.AddAuthentication(FakeAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, FakeAuthenticationHandler>(FakeAuthenticationDefaults.Scheme, _ => { });
            services.AddAuthorization();

            // Apply additional overrides (e.g. empty collections)
            _configureServices?.Invoke(services);
        });
    }
}
