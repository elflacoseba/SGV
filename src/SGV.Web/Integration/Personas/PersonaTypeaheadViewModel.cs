namespace SGV.Web.Integration.Personas;

/// <summary>
/// Bindable view model for the reusable <c>_PersonaTypeahead.cshtml</c>
/// partial.
///
/// <para>
/// <b>D-PE-03:</b> el partial ya no requiere <c>AllPersonas</c> precargado
/// en el host. El JS (<c>wwwroot/js/pages/personas-typeahead.js</c>) hace
/// fetch al endpoint <c>GET /api/v1/personas/buscar?q={term}&amp;take={n}</c>
/// con debounce. Esto evita el payload inicial de ~100 KB que pesaba la
/// carga completa del dataset activo.
/// </para>
///
/// <para>
/// <c>AllPersonas</c> se conserva como propiedad opcional (default
/// <c>[]</c>) por back-compat con hosts que aún la populan; cuando se
/// popula, el JS cae al modo legacy de filtrado client-side para no
/// cambiar la experiencia hasta que el host migre. La nueva ruta por
/// defecto es server-side search.
/// </para>
/// </summary>
public sealed record PersonaTypeaheadViewModel(
    IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.PersonaDto> AllPersonas = null!,
    Guid? SelectedId = null,
    string InputName = "PersonaId",
    int MinChars = 2,
    string Label = "Buscar persona",
    string Placeholder = "Escribí al menos 2 caracteres...",
    int Take = 50);