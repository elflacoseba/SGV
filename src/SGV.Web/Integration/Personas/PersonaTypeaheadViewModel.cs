namespace SGV.Web.Integration.Personas;

/// <summary>
/// Bindable view model for the reusable <c>_PersonaTypeahead.cshtml</c>
/// partial. The list of personas is preloaded server-side in the host
/// page (typically via <see cref="IPersonaApiClient.GetAllAsync"/> in the
/// host's <c>OnGetAsync</c>) so the typeahead can filter client-side
/// with debounce without triggering additional HTTP requests on each
/// keystroke.
/// <para>
/// The partial is intentionally persona-agnostic in its presentation
/// markup (renders a search input, a dropdown list, and a hidden field
/// bound to <see cref="SelectedId"/>) and emits a standard
/// <c>change</c> event on the hidden field whenever a row is picked,
/// which lets the host page subscribe via plain DOM event handlers.
/// </para>
/// </summary>
/// <param name="AllPersonas">
/// Pre-fetched active personas to filter. The partial renders this list
/// verbatim as the source set; filtering is performed client-side by
/// <c>wwwroot/js/pages/personas-typeahead.js</c>.
/// </param>
/// <param name="SelectedId">
/// Selected persona id. Bound from a hidden input named
/// <see cref="InputName"/>. The form post sends this value to the host
/// handler; <c>null</c> means no selection yet.
/// </param>
/// <param name="InputName">
/// Name of the hidden input the partial renders. Defaults to
/// <c>PersonaId</c>. Hosts that already use a different field name
/// (e.g. <c>Usuario.PersonaId</c> in a future Usuarios module) can
/// override this to avoid name collisions.
/// </param>
/// <param name="MinChars">
/// Minimum number of characters required before the dropdown shows
/// matches. Defaults to <c>2</c> per design (issue #101 convention).
/// </param>
/// <param name="Label">
/// Visible label for the search input. Defaults to <c>"Buscar persona"</c>.
/// </param>
/// <param name="Placeholder">
/// Placeholder shown inside the search input when empty. Defaults to
/// <c>"Escribí al menos 2 caracteres..."</c>.
/// </param>
public sealed record PersonaTypeaheadViewModel(
    IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.PersonaDto> AllPersonas,
    Guid? SelectedId = null,
    string InputName = "PersonaId",
    int MinChars = 2,
    string Label = "Buscar persona",
    string Placeholder = "Escribí al menos 2 caracteres...");