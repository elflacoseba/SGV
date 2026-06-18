using System.Reflection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class UnidadOrganizativaServicioConsultaTests
{
    private static readonly Guid UnidadId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtraUnidadId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    private static UnidadOrganizativa CrearUnidadActiva()
    {
        var unidad = new UnidadOrganizativa("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId)
        {
            Id = UnidadId
        };
        unidad.CambiarDatos("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId, "Máxima autoridad ejecutiva");

        // Simulate eager-loaded nav property (EF Core sets this via Include)
        var tipo = new TipoUnidadOrganizativa("Direccion", "Dirección")
        {
            Id = TipoUnidadOrganizativaConstantes.DireccionId
        };
        SetNavigation(unidad, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), tipo);

        return unidad;
    }

    private static UnidadOrganizativa CrearUnidadActivaHija(Guid id, Guid padreId, string codigo, string nombre)
    {
        var unidad = new UnidadOrganizativa(codigo, nombre, TipoUnidadOrganizativaConstantes.AreaId, padreId)
        {
            Id = id
        };
        unidad.CambiarDatos(codigo, nombre, TipoUnidadOrganizativaConstantes.AreaId, null);

        var tipo = new TipoUnidadOrganizativa("Area", "Área")
        {
            Id = TipoUnidadOrganizativaConstantes.AreaId
        };
        SetNavigation(unidad, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), tipo);

        return unidad;
    }

    [Fact]
    public async Task ListAsync_CuandoExistenUnidades_RetornaListaDeDto()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(unidad.Id, dto.Id);
        Assert.Equal(unidad.Codigo, dto.Codigo);
        Assert.Equal(unidad.Nombre, dto.Nombre);
        Assert.Equal(unidad.TipoUnidadOrganizativaId, dto.TipoUnidadOrganizativaId);
        Assert.Equal("Dirección", dto.TipoUnidadNombre);
        Assert.Equal(unidad.Descripcion, dto.Descripcion);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenUnidades_RetornaListaVacia()
    {
        var repo = new FakeUnidadOrganizativaRepository { Datos = [] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoUnidadExiste_RetornaDto()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(UnidadId, default);

        Assert.NotNull(resultado);
        Assert.Equal(unidad.Id, resultado!.Id);
        Assert.Equal(unidad.Codigo, resultado.Codigo);
        Assert.Equal("Dirección", resultado.TipoUnidadNombre);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoUnidadNoExiste_RetornaNull()
    {
        var repo = new FakeUnidadOrganizativaRepository { Datos = [] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    // ---- QueryAsync tests (Task 3.1 / 3.3) ----

    [Fact]
    public async Task QueryAsync_SinFiltros_RetornaPaginadoConTodos()
    {
        var unidad = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(new UnidadOrganizativaQuery(1, 10), default);

        Assert.Equal(2, resultado.TotalCount);
        Assert.Equal(2, resultado.Items.Count);
        Assert.Equal(1, resultado.Page);
        Assert.Equal(10, resultado.PageSize);
    }

    [Fact]
    public async Task QueryAsync_ConSearch_FiltraPorCodigoONombre()
    {
        var unidad = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(1, 10, "AREA"), default);

        Assert.Single(resultado.Items);
        Assert.Equal("AREA-01", resultado.Items[0].Codigo);
    }

    [Fact]
    public async Task QueryAsync_ConTipoUnidadOrganizativaId_FiltraPorTipo()
    {
        var unidad = CrearUnidadActiva(); // Direccion
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa"); // Area
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(1, 10, null, TipoUnidadOrganizativaConstantes.AreaId), default);

        Assert.Single(resultado.Items);
        Assert.Equal("AREA-01", resultado.Items[0].Codigo);
    }

    [Fact]
    public async Task QueryAsync_ConPaginacion_DevuelvePaginaCorrecta()
    {
        var unidad = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var pagina1 = await servicio.QueryAsync(new UnidadOrganizativaQuery(1, 1), default);
        var pagina2 = await servicio.QueryAsync(new UnidadOrganizativaQuery(2, 1), default);

        Assert.Single(pagina1.Items);
        Assert.Equal(2, pagina1.TotalCount);
        Assert.Single(pagina2.Items);
        Assert.Equal(2, pagina2.TotalCount);
    }

    [Fact]
    public async Task QueryAsync_CuandoNoHayCoincidencias_RetornaVacio()
    {
        var repo = new FakeUnidadOrganizativaRepository { Datos = [] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(1, 10, "NOEXISTE"), default);

        Assert.Empty(resultado.Items);
        Assert.Equal(0, resultado.TotalCount);
    }

    // ---- GetTreeAsync tests (Task 3.1 / 3.3) ----

    [Fact]
    public async Task GetTreeAsync_ConJerarquia_RetornaArbolConHijas()
    {
        var padre = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");
        var repo = new FakeUnidadOrganizativaRepository { Datos = [padre, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var arbol = await servicio.GetTreeAsync(default);

        // The root should be the padre (no UnidadPadreId)
        var raiz = Assert.Single(arbol);
        Assert.Equal(padre.Nombre, raiz.Nombre);
        Assert.Single(raiz.Hijas);
        Assert.Equal("AREA-01", raiz.Hijas[0].Codigo);
        Assert.Equal("Área", raiz.Hijas[0].TipoUnidadNombre);
        Assert.Empty(raiz.Hijas[0].Hijas);
    }

    [Fact]
    public async Task GetTreeAsync_CuandoNoHayUnidades_RetornaListaVacia()
    {
        var repo = new FakeUnidadOrganizativaRepository { Datos = [] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var arbol = await servicio.GetTreeAsync(default);

        Assert.Empty(arbol);
    }

    [Fact]
    public async Task GetTreeAsync_DtoIncluyeTipoUnidadOrganizativaId()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var arbol = await servicio.GetTreeAsync(default);

        var raiz = Assert.Single(arbol);
        Assert.Equal(TipoUnidadOrganizativaConstantes.DireccionId, raiz.TipoUnidadOrganizativaId);
        Assert.Equal("Dirección", raiz.TipoUnidadNombre);
    }

    private static void SetNavigation<TEntity, TNav>(TEntity entity, string propertyName, TNav value)
        where TEntity : class
    {
        var field = typeof(TEntity).GetField($"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(entity, value);
    }
}

internal sealed class FakeUnidadOrganizativaRepository : IUnidadOrganizativaRepository
{
    public List<UnidadOrganizativa> Datos { get; set; } = [];

    public Task AddAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<UnidadOrganizativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(e => e.Id == id && e.IsActive && !e.IsDeleted));

    public Task<UnidadOrganizativa?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));

    public Task<bool> IsDescendantAsync(Guid candidateDescendantId, Guid ancestorId, CancellationToken cancellationToken = default)
    {
        var current = Datos.FirstOrDefault(d => d.Id == candidateDescendantId);
        while (current is not null && current.UnidadPadreId.HasValue)
        {
            if (current.UnidadPadreId == ancestorId)
            {
                return Task.FromResult(true);
            }

            current = Datos.FirstOrDefault(d => d.Id == current.UnidadPadreId.Value);
        }

        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<UnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<UnidadOrganizativa>>(Datos.ToList());
    }

    public Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> HasActiveChildrenAsync(Guid unidadId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> HasActivePuestosAsync(Guid unidadId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unidad = Datos.FirstOrDefault(d => d.Id == id);
        if (unidad is not null)
        {
            unidad.Activar();
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<UnidadOrganizativa> Items, int TotalCount)> QueryAsync(
        string? search,
        Guid? tipoUnidadOrganizativaId,
        Guid? unidadPadreId,
        DateOnly? vigenteEn,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filtered = Datos.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(u =>
                u.Codigo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (tipoUnidadOrganizativaId.HasValue)
            filtered = filtered.Where(u => u.TipoUnidadOrganizativaId == tipoUnidadOrganizativaId.Value);

        if (unidadPadreId.HasValue)
            filtered = filtered.Where(u => u.UnidadPadreId == unidadPadreId.Value);

        if (vigenteEn.HasValue)
            filtered = filtered.Where(u => u.IsActive &&
                (!u.VigenteDesde.HasValue || u.VigenteDesde.Value <= vigenteEn.Value) &&
                (!u.VigenteHasta.HasValue || u.VigenteHasta.Value >= vigenteEn.Value));

        var list = filtered.ToList();
        var total = list.Count;
        var pagedItems = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = ((IReadOnlyList<UnidadOrganizativa>)pagedItems, total);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<UnidadOrganizativa>> ListTreeAsync(CancellationToken cancellationToken = default)
    {
        var items = Datos
            .Where(u => u.IsActive)
            .OrderBy(u => u.Codigo)
            .ToList();
        return Task.FromResult<IReadOnlyList<UnidadOrganizativa>>(items);
    }
}
