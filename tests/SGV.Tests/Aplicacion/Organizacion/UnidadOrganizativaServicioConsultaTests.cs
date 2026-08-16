using System.Reflection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Comun;
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
        var unidad = new UnidadOrganizativa("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId, "Máxima autoridad ejecutiva", null)
        {
            Id = UnidadId
        };

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
        var unidad = new UnidadOrganizativa(codigo, nombre, TipoUnidadOrganizativaConstantes.AreaId, null, padreId)
        {
            Id = id
        };

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

    // ---- UnidadPadreCodigo/nombre en DTO (Phase 1) ----

    [Fact]
    public async Task GetByIdAsync_UnidadConPadre_IncluyeUnidadPadreCodigoNombre()
    {
        var padre = CrearUnidadActiva(); // root — no parent
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");

        // Set navigation to the padre on the child entity
        SetNavigation(hija, nameof(UnidadOrganizativa.UnidadPadre), padre);

        var repo = new FakeUnidadOrganizativaRepository { Datos = [padre, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(OtraUnidadId, default);

        Assert.NotNull(resultado);
        Assert.Equal(padre.Codigo, resultado!.UnidadPadreCodigo);
        Assert.Equal(padre.Nombre, resultado.UnidadPadreNombre);
    }

    [Fact]
    public async Task GetByIdAsync_UnidadRaiz_TieneUnidadPadreNulo()
    {
        var raiz = CrearUnidadActiva();
        raiz.CambiarUnidadPadre(null); // ensure it's a root

        var repo = new FakeUnidadOrganizativaRepository { Datos = [raiz] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(UnidadId, default);

        Assert.NotNull(resultado);
        Assert.Null(resultado!.UnidadPadreCodigo);
        Assert.Null(resultado.UnidadPadreNombre);
    }

    [Fact]
    public async Task ListAsync_UnidadConPadre_IncluyeUnidadPadreCodigoNombre()
    {
        var padre = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");
        SetNavigation(hija, nameof(UnidadOrganizativa.UnidadPadre), padre);

        var repo = new FakeUnidadOrganizativaRepository { Datos = [padre, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        var dtoHija = resultado.Single(r => r.Id == OtraUnidadId);
        Assert.Equal(padre.Codigo, dtoHija.UnidadPadreCodigo);
        Assert.Equal(padre.Nombre, dtoHija.UnidadPadreNombre);

        var dtoRaiz = resultado.Single(r => r.Id == UnidadId);
        Assert.Null(dtoRaiz.UnidadPadreCodigo);
        Assert.Null(dtoRaiz.UnidadPadreNombre);
    }

    [Fact]
    public async Task QueryAsync_UnidadConPadre_IncluyeUnidadPadreCodigoNombre()
    {
        var padre = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");
        SetNavigation(hija, nameof(UnidadOrganizativa.UnidadPadre), padre);

        var repo = new FakeUnidadOrganizativaRepository { Datos = [padre, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(new UnidadOrganizativaQuery(1, 10), default);

        var dtoHija = resultado.Items.Single(r => r.Id == OtraUnidadId);
        Assert.Equal(padre.Codigo, dtoHija.UnidadPadreCodigo);
        Assert.Equal(padre.Nombre, dtoHija.UnidadPadreNombre);

        var dtoRaiz = resultado.Items.Single(r => r.Id == UnidadId);
        Assert.Null(dtoRaiz.UnidadPadreCodigo);
        Assert.Null(dtoRaiz.UnidadPadreNombre);
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

    // ===== Clamp de page/pageSize (issue #278) =====
    // El servicio clampea los valores del query antes de invocar al repo
    // para evitar que `Skip((page - 1) * pageSize)` reciba un count
    // negativo y que `Take(pageSize)` exceda el tope de respuesta.

    [Fact]
    public async Task QueryAsync_PageCero_ClampaAUnoYReportaUno()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(Page: 0, PageSize: 10), default);

        // El servicio reporta el valor EFFECTIVO (clamped).
        Assert.Equal(1, resultado.Page);
        Assert.Equal(10, resultado.PageSize);
        // Y entrega el valor clamped al repo, no el original.
        Assert.Equal(1, repo.LastReceivedPage);
    }

    [Fact]
    public async Task QueryAsync_PageNegativo_ClampaAUno()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(Page: -5, PageSize: 10), default);

        Assert.Equal(1, resultado.Page);
        Assert.Equal(1, repo.LastReceivedPage);
    }

    [Fact]
    public async Task QueryAsync_PageSizeCero_ClampaAMinPageSize()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(Page: 1, PageSize: 0), default);

        Assert.Equal(UnidadOrganizativaQuery.MinPageSize, resultado.PageSize);
        Assert.Equal(UnidadOrganizativaQuery.MinPageSize, repo.LastReceivedPageSize);
    }

    [Fact]
    public async Task QueryAsync_PageSizeNegativo_ClampaAMinPageSize()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(Page: 1, PageSize: -10), default);

        Assert.Equal(UnidadOrganizativaQuery.MinPageSize, resultado.PageSize);
        Assert.Equal(UnidadOrganizativaQuery.MinPageSize, repo.LastReceivedPageSize);
    }

    [Fact]
    public async Task QueryAsync_PageSizeExcesivo_ClampaAMaxPageSize()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(Page: 1, PageSize: 1_000_000), default);

        Assert.Equal(UnidadOrganizativaQuery.MaxPageSize, resultado.PageSize);
        Assert.Equal(UnidadOrganizativaQuery.MaxPageSize, repo.LastReceivedPageSize);
    }

    [Fact]
    public async Task QueryAsync_PageYPageSizeEnRango_NoModificaValores()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(Page: 3, PageSize: 25), default);

        Assert.Equal(3, resultado.Page);
        Assert.Equal(25, resultado.PageSize);
        Assert.Equal(3, repo.LastReceivedPage);
        Assert.Equal(25, repo.LastReceivedPageSize);
    }

    [Fact]
    public async Task QueryAsync_PageSizeEnBorde_NoModificaValores()
    {
        // El límite MaxPageSize debe pasar tal cual (no clampa hacia abajo).
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(Page: 1, PageSize: UnidadOrganizativaQuery.MaxPageSize), default);

        Assert.Equal(UnidadOrganizativaQuery.MaxPageSize, resultado.PageSize);
        Assert.Equal(UnidadOrganizativaQuery.MaxPageSize, repo.LastReceivedPageSize);
    }

    // ---- GetTreeAsync tests (Task 3.1 / 3.3) ----

    [Fact]
    public async Task GetTreeAsync_ConJerarquia_RetornaArbolConHijas()
    {
        var padre = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");
        var repo = new FakeUnidadOrganizativaRepository { Datos = [padre, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var response = await servicio.GetTreeAsync(default);

        // The root should be the padre (no UnidadPadreId)
        var raiz = Assert.Single(response.Arbol);
        Assert.Equal(padre.Nombre, raiz.Nombre);
        Assert.Single(raiz.Hijas);
        Assert.Equal("AREA-01", raiz.Hijas[0].Codigo);
        Assert.Equal("Área", raiz.Hijas[0].TipoUnidadNombre);
        Assert.Empty(raiz.Hijas[0].Hijas);
        Assert.Empty(response.NodosConCiloDetectado);
    }

    [Fact]
    public async Task GetTreeAsync_CuandoNoHayUnidades_RetornaListaVacia()
    {
        var repo = new FakeUnidadOrganizativaRepository { Datos = [] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var response = await servicio.GetTreeAsync(default);

        Assert.Empty(response.Arbol);
    }

    // ---- QueryAsync segmento tests (Phase 1) ----

    private static UnidadOrganizativa CrearUnidadEliminada()
    {
        var unidad = CrearUnidadActiva();
        entityIsDeleted(unidad, true);
        entityIsActive(unidad, false);
        return unidad;
    }

    private static void entityIsDeleted(UnidadOrganizativa entity, bool isDeleted)
    {
        var field = typeof(EntidadAuditable).GetField($"<{nameof(EntidadAuditable.IsDeleted)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(entity, isDeleted);
    }

    private static void entityIsActive(UnidadOrganizativa entity, bool isActive)
    {
        var field = typeof(UnidadOrganizativa).GetField($"<{nameof(UnidadOrganizativa.IsActive)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(entity, isActive);
    }

    [Fact]
    public async Task QueryAsync_PorDefecto_RetornaSoloActivas()
    {
        var activa = CrearUnidadActiva();
        var eliminada = CrearUnidadEliminada();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [activa, eliminada] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(new UnidadOrganizativaQuery(1, 10), default);

        Assert.Single(resultado.Items);
        Assert.Equal(activa.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas()
    {
        var activa = CrearUnidadActiva();
        var eliminada = CrearUnidadEliminada();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [activa, eliminada] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(1, 10, Segmento: UnidadOrganizativaSegmentoListado.Eliminadas), default);

        Assert.Single(resultado.Items);
        Assert.Equal(eliminada.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_SegmentosNoSeMezclan()
    {
        var activa = CrearUnidadActiva();
        var eliminada = CrearUnidadEliminada();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [activa, eliminada] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var activas = await servicio.QueryAsync(new UnidadOrganizativaQuery(1, 10), default);
        var eliminadas = await servicio.QueryAsync(
            new UnidadOrganizativaQuery(1, 10, Segmento: UnidadOrganizativaSegmentoListado.Eliminadas), default);

        Assert.Single(activas.Items);
        Assert.Equal(activa.Id, activas.Items[0].Id);
        Assert.Single(eliminadas.Items);
        Assert.Equal(eliminada.Id, eliminadas.Items[0].Id);
    }

    [Fact]
    public async Task GetTreeAsync_DtoIncluyeTipoUnidadOrganizativaId()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var response = await servicio.GetTreeAsync(default);

        var raiz = Assert.Single(response.Arbol);
        Assert.Equal(TipoUnidadOrganizativaConstantes.DireccionId, raiz.TipoUnidadOrganizativaId);
        Assert.Equal("Dirección", raiz.TipoUnidadNombre);
    }

    // ===== WU-3: BuildTree nunca crashea ante ciclos (issue #277) =====
    // Spec: "Construcción del árbol nunca crashea ante ciclos" — scenario
    // "BuildTree retorna árbol parcial y reporta ciclos sin StackOverflow".
    // El escenario MySQL siembra el ciclo directo en BD; este unit test
    // ejercita la misma defensa visited-set contra un dataset ciclado
    // para evitar regresiones futuras si BuildTree cambia su traversal.

    [Fact]
    public async Task GetTreeAsync_ConCicloEnDatos_NoStackOverflowYRetornaSinExplotar()
    {
        // Cycle A↔B (A.UnidadPadreId == B.Id, B.UnidadPadreId == A.Id)
        // ambos colgando de un root real R. La cadena canónica de R se
        // rompe cuando A se reasigna, pero BuildTree debe tolerarlo
        // sin StackOverflow. La defensa visited-set acota la recursión
        // en cualquier configuración donde un id aparezca dos veces
        // en la cadena.
        var idR = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var idA = Guid.Parse("91000000-0000-0000-0000-000000000001");
        var idB = Guid.Parse("91000000-0000-0000-0000-000000000002");

        var r = CrearUnidadActiva();
        r.Id = idR;
        SetNavigation(r, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), new TipoUnidadOrganizativa("Direccion", "Dirección")
        {
            Id = TipoUnidadOrganizativaConstantes.DireccionId
        });

        var a = CrearUnidadActivaHija(idA, idR, "A", "A");
        var b = CrearUnidadActivaHija(idB, idA, "B", "B");
        a.CambiarUnidadPadre(idB); // ciclo A↔B

        var repo = new FakeUnidadOrganizativaRepository { Datos = [r, a, b] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var response = await servicio.GetTreeAsync(default);

        // No debe lanzar. La estructura concreta depende del orden en
        // que `a` queda apuntando a `b`, lo que importa es que el método
        // retorna sin StackOverflow y devuelve una respuesta con `arbol`
        // poblado o vacío según el padre. Pre-WU-4 este test pasaba con
        // `IReadOnlyList<TreeNodeDto>`; post-WU-4 debe ser el nuevo DTO.
        Assert.NotNull(response);
        Assert.NotNull(response.Arbol);
        Assert.NotNull(response.NodosConCiloDetectado);
    }

    // ===== WU-4: GetTreeAsync retorna NodosConCiloDetectado (issue #277) =====
    // Spec: "Construcción del árbol nunca crashea ante ciclos" — scenario
    // "BuildTree retorna árbol parcial y reporta ciclos sin StackOverflow".

    [Fact]
    public async Task GetTreeAsync_ConCiclo_RetornaNodosConCiloDetectadoYSubArbolParcial()
    {
        // Root real: R.
        // Sub-árbol acíclico: X (hijo de R).
        // Ciclo cerrado (sin raíz): A↔B.
        // Esperado:
        //   Arbol: contiene a R (con su hijo X), NO contiene A ni B.
        //   NodosConCiloDetectado: contiene exactamente {A.Id, B.Id}.
        var idR = Guid.Parse("92000000-0000-0000-0000-000000000001");
        var idX = Guid.Parse("92000000-0000-0000-0000-000000000002");
        var idA = Guid.Parse("92000000-0000-0000-0000-000000000003");
        var idB = Guid.Parse("92000000-0000-0000-0000-000000000004");

        var r = CrearUnidadActiva();
        r.Id = idR;
        SetNavigation(r, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), new TipoUnidadOrganizativa("Direccion", "Dirección")
        {
            Id = TipoUnidadOrganizativaConstantes.DireccionId
        });

        var x = CrearUnidadActivaHija(idX, idR, "X", "X");
        var a = new UnidadOrganizativa("A", "A", TipoUnidadOrganizativaConstantes.AreaId, null, null) { Id = idA };
        SetNavigation(a, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), new TipoUnidadOrganizativa("Area", "Área")
        {
            Id = TipoUnidadOrganizativaConstantes.AreaId
        });
        var b = CrearUnidadActivaHija(idB, idA, "B", "B");
        a.CambiarUnidadPadre(idB); // ciclo A↔B (ambos no-root)

        var repo = new FakeUnidadOrganizativaRepository { Datos = [r, x, a, b] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var response = await servicio.GetTreeAsync(default);

        Assert.NotNull(response);

        // El sub-árbol acíclico (R + X) debe estar presente.
        var raiz = Assert.Single(response.Arbol);
        Assert.Equal(idR, raiz.Id);
        Assert.Single(raiz.Hijas);
        Assert.Equal(idX, raiz.Hijas[0].Id);

        // Los nodos cíclicos deben aparecer en NodosConCiloDetectado.
        Assert.Equal(2, response.NodosConCiloDetectado.Count);
        Assert.Contains(idA, response.NodosConCiloDetectado);
        Assert.Contains(idB, response.NodosConCiloDetectado);
    }

    [Fact]
    public async Task GetTreeAsync_SinCiclos_RetornaArbolCompletoYListaVacia()
    {
        // Spec: "BuildTree retorna árbol completo cuando no hay ciclos" —
        // el campo nodosConCiloDetectado MUST ser colección vacía.
        var padre = CrearUnidadActiva();
        var hija = CrearUnidadActivaHija(OtraUnidadId, UnidadId, "AREA-01", "Área Operativa");

        var repo = new FakeUnidadOrganizativaRepository { Datos = [padre, hija] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var response = await servicio.GetTreeAsync(default);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Arbol);
        Assert.Empty(response.NodosConCiloDetectado);
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

    /// <summary>
    /// Último valor de <c>page</c> recibido por el repo. El servicio
    /// aplica un clamp antes de invocar el repo (issue #278), por lo que
    /// este campo siempre debe estar dentro del rango seguro
    /// <c>[1, ∞)</c> en escenarios normales.
    /// </summary>
    public int? LastReceivedPage { get; private set; }

    /// <summary>
    /// Último valor de <c>pageSize</c> recibido por el repo. El servicio
    /// aplica un clamp antes de invocar el repo (issue #278), por lo que
    /// este campo siempre debe estar dentro del rango
    /// <c>[<see cref="UnidadOrganizativaQuery.MinPageSize"/>,
    /// <see cref="UnidadOrganizativaQuery.MaxPageSize"/>]</c>.
    /// </summary>
    public int? LastReceivedPageSize { get; private set; }

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
        UnidadOrganizativaSegmentoListado segmento = UnidadOrganizativaSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        LastReceivedPage = page;
        LastReceivedPageSize = pageSize;

        var filtered = Datos.AsEnumerable();

        // Apply segmento filter first
        filtered = segmento == UnidadOrganizativaSegmentoListado.Activas
            ? filtered.Where(u => u.IsActive && !u.IsDeleted)
            : filtered.Where(u => !u.IsActive && u.IsDeleted);

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
