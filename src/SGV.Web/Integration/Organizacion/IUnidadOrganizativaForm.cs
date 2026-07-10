using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Interface shared by CreateModel and EditModel for the <c>_Form.cshtml</c> partial.
/// </summary>
public interface IUnidadOrganizativaForm
{
    UnidadOrganizativaInputModel Input { get; }

    IReadOnlyList<TipoUnidadOrganizativaDto> TipoOptions { get; }

    IReadOnlyList<ParentOptionViewModel> ParentOptions { get; }

    string? ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> cuando la página es Edit; el partial <c>_Form.cshtml</c>
    /// usa este flag para ocultar el input inmutable de <c>Codigo</c>
    /// (campo locked post-create). Create siempre devuelve <c>false</c>.
    /// </summary>
    bool IsEdit { get; }
}
