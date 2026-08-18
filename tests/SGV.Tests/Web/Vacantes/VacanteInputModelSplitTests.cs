using System.ComponentModel.DataAnnotations;
using System.Reflection;
using SGV.Web.Integration.Vacantes;
using Xunit;

namespace SGV.Tests.Web.Vacantes;

/// <summary>
/// Tests de defensa por reflexión para los input models de Vacantes.
/// Cambio <c>vacantes-hardening</c> D-3: el split del viejo
/// <c>VacanteInputModel</c> en <c>VacanteCreateInputModel</c> y
/// <c>VacanteEditInputModel</c> debe resistir drift futuro
/// (re-fusión accidental del modelo).
/// </summary>
public sealed class VacanteInputModelSplitTests
{
    /// <summary>
    /// D-3: <c>VacanteCreateInputModel</c> NO expone <c>EstadoVacanteId</c>
    /// — el campo fue retirado del formulario Create (issue #273 Slice A).
    /// Si alguien lo reintroduce por error, este test falla.
    /// </summary>
    [Fact]
    public void VacanteCreateInputModel_NoExponeEstadoVacanteId()
    {
        var prop = typeof(VacanteCreateInputModel).GetProperty("EstadoVacanteId");

        Assert.Null(prop);
    }

    /// <summary>
    /// D-3: <c>VacanteEditInputModel.EstadoVacanteId</c> es
    /// <see cref="Guid"/> nullable con <see cref="RequiredAttribute"/>
    /// — Edit permite transiciones de estado explícitas.
    /// </summary>
    [Fact]
    public void VacanteEditInputModel_EstadoVacanteId_EsRequerido()
    {
        var prop = typeof(VacanteEditInputModel).GetProperty("EstadoVacanteId");

        Assert.NotNull(prop);
        Assert.Equal(typeof(Guid?), prop!.PropertyType);
        Assert.NotNull(prop.GetCustomAttribute<RequiredAttribute>());
    }

    /// <summary>
    /// D-3: el viejo <c>VacanteInputModel</c> compartido ya no existe —
    /// su reemplazo son los dos modelos específicos del flujo. Este test
    /// protege contra una re-fusión accidental.
    /// </summary>
    [Fact]
    public void VacanteInputModel_NoExisteDespuesDeSplit()
    {
        var type = typeof(VacanteCreateInputModel).Assembly
            .GetType("SGV.Web.Integration.Vacantes.VacanteInputModel");

        Assert.Null(type);
    }
}
