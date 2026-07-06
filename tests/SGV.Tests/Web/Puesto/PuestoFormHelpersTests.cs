using System.Net.Http;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Unit tests para los helpers p&uacute;blicos extra&iacute;dos de los PageModels de
/// Create/Edit de Puestos: <see cref="PuestoFormHelpers.MapToSuperiorViewModel"/>
/// y <see cref="PuestoFormHelpers.LaunchSafeAsync{T}"/>. Garantiza que la
/// extracci&oacute;n a helpers compartidos (PR review #93, correcci&oacute;n #1)
/// preserva el comportamiento original: mapeo 1-a-1 de los 7 campos del view
/// model y captura de excepciones sincr&oacute;nicas en faulted tasks.
/// </summary>
public sealed class PuestoFormHelpersTests
{
    // ──────────────────────────────────────────────
    // Corrección #1 · MapToSuperiorViewModel — proyección DTO → ViewModel
    // ──────────────────────────────────────────────

    [Fact]
    public void MapToSuperiorViewModel_Proyecta_Dto_A_ViewModel_Con_Todos_Los_Campos()
    {
        var id = Guid.NewGuid();
        var unidadId = Guid.NewGuid();
        var cargoId = Guid.NewGuid();
        var superiorId = Guid.NewGuid();
        var dto = new PuestoDto(
            id,
            "P-001",
            "Director Comercial",
            "Descripción con valor",
            unidadId,
            "Comercial",
            cargoId,
            "Director",
            superiorId);

        var vm = PuestoFormHelpers.MapToSuperiorViewModel(dto);

        // Las 7 propiedades del PuestoListItemViewModel quedan cubiertas.
        Assert.Equal(id, vm.Id);
        Assert.Equal("P-001", vm.Codigo);
        Assert.Equal("Director Comercial", vm.Nombre);
        Assert.Equal("Descripción con valor", vm.Descripcion);
        Assert.Equal("Comercial", vm.UnidadOrganizativaNombre);
        Assert.Equal("Director", vm.CargoNombre);
        Assert.Equal(superiorId, vm.PuestoSuperiorId);

        // El CodigoYNombre compuesto debe estar disponible para el dropdown.
        Assert.Equal("P-001 — Director Comercial", vm.CodigoYNombre);
    }

    [Fact]
    public void MapToSuperiorViewModel_CuandoDescripcionYPuestoSuperionSonNull_PreservaLosNulls()
    {
        var id = Guid.NewGuid();
        var unidadId = Guid.NewGuid();
        var cargoId = Guid.NewGuid();
        var dto = new PuestoDto(
            id,
            "P-002",
            "Analista Junior",
            null,
            unidadId,
            "Operaciones",
            cargoId,
            "Analista",
            null);

        var vm = PuestoFormHelpers.MapToSuperiorViewModel(dto);

        // Los campos opcionales mantienen null (default de los records).
        Assert.Null(vm.Descripcion);
        Assert.Null(vm.PuestoSuperiorId);

        // El resto de las propiedades siguen pobladas.
        Assert.Equal(id, vm.Id);
        Assert.Equal("P-002", vm.Codigo);
        Assert.Equal("Analista Junior", vm.Nombre);
        Assert.Equal("Operaciones", vm.UnidadOrganizativaNombre);
        Assert.Equal("Analista", vm.CargoNombre);
        Assert.Equal("P-002 — Analista Junior", vm.CodigoYNombre);
    }

    // ──────────────────────────────────────────────
    // Corrección #1 · LaunchSafeAsync<T> — captura de excepciones sincrónicas
    // ──────────────────────────────────────────────

    [Fact]
    public async Task LaunchSafeAsync_CuandoFactoryLanzaExcepcionSincrona_DevuelveTaskFaulted()
    {
        var thrown = new HttpRequestException("api caída");

        // La firma pública debe aceptar Func<Task<T>>. La factory lanza
        // SINCRÓNICAMENTE (antes de devolver un Task) y el helper debe
        // envolverla en un Task faulted con la misma excepción.
        var task = PuestoFormHelpers.LaunchSafeAsync<int>(() => throw thrown);

        Assert.NotNull(task);
        Assert.Equal(TaskStatus.Faulted, task.Status);

        var observed = await Assert.ThrowsAsync<HttpRequestException>(async () => await task);
        Assert.Same(thrown, observed);
    }

    [Fact]
    public async Task LaunchSafeAsync_CuandoFactoryDevuelveTaskCompletado_DevuelveEseMismoTask()
    {
        // Sanity check: si la factory devuelve un Task RanToCompletion, el
        // helper no debe tocarlo ni envolverlo. Verifica que el task devuelto
        // por la factory se propaga tal cual.
        var completed = Task.FromResult(42);

        var returned = PuestoFormHelpers.LaunchSafeAsync(() => completed);

        Assert.Same(completed, returned);
        Assert.Equal(TaskStatus.RanToCompletion, returned.Status);
        Assert.Equal(42, await returned);
    }

    [Fact]
    public async Task LaunchSafeAsync_CuandoFactoryDevuelveTaskFaulted_PreservaLaExcepcionOriginal()
    {
        // Caso límite: si la factory ya devuelve un Task faulted (no lanza
        // sincrónicamente), el helper NO debe reenvolver la excepción. Esto
        // importa porque re-envolver cambiaría el tipo base / stack de
        // ciertas excepciones y rompería el contrato de las pruebas que
        // inspeccionan la excepción vía Task.Exception.
        var original = new InvalidOperationException("task faulted original");
        var faulted = Task.FromException<int>(original);

        var returned = PuestoFormHelpers.LaunchSafeAsync(() => faulted);

        Assert.Same(faulted, returned);
        Assert.Equal(TaskStatus.Faulted, returned.Status);
        Assert.Same(original, returned.Exception!.InnerException);

        // Await sobre el task debe propagar la MISMA excepción, no una envuelta.
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(async () => await returned);
        Assert.Same(original, observed);
    }
}
