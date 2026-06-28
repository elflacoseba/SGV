using SGV.Aplicacion.Organizacion.Consultas.Dtos;

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
}
