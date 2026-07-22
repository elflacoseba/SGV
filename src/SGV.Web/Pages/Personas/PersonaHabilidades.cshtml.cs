using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Personas;

namespace SGV.Web.Pages.Personas;

/// <summary>
/// PageModel de la gestión administrativa de habilidades asociadas a una
/// persona. Este slice expone únicamente la carga inicial; las mutaciones se
/// incorporarán en el slice siguiente.
/// </summary>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class PersonaHabilidadesModel(
    IPersonaApiClient personaApiClient,
    ILogger<PersonaHabilidadesModel> logger) : PageModel
{
    /// <summary>Datos que consume la vista de habilidades.</summary>
    public PersonaHabilidadesViewModel ViewModel { get; private set; } = new();

    /// <summary>Indica si el usuario actual tiene el rol administrador.</summary>
    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>Handler GET de la página de habilidades de una persona.</summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        try
        {
            var persona = await personaApiClient.GetByIdAsync(id, cancellationToken);
            if (persona is null || !persona.IsActive)
            {
                logger.LogWarning(
                    "Persona with Id {PersonaId} is not available for skill management.",
                    id);
                return Redirect("/error/404");
            }

            var skills = await personaApiClient.GetSkillsAsync(id, cancellationToken);
            ViewModel = PersonaHabilidadesViewModel.From(persona, skills);
            return Page();
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException
            or OperationCanceledException
            or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "Failed to load skills page for persona {PersonaId}.", id);
            ViewModel = ViewModel with
            {
                PersonaId = id,
                IsRecoverable = true,
                ErrorMessage = "No se pudo cargar la página de habilidades. Intentá nuevamente."
            };
            return Page();
        }
    }
}

/// <summary>Estado de presentación de la página de habilidades.</summary>
public sealed record PersonaHabilidadesViewModel
{
    /// <summary>Identificador de la persona.</summary>
    public Guid PersonaId { get; init; }

    /// <summary>Nombre completo mostrado como encabezado.</summary>
    public string PersonaNombre { get; init; } = string.Empty;

    /// <summary>Filas de asociaciones cargadas desde el backend.</summary>
    public IReadOnlyList<PersonaHabilidadRowViewModel> Skills { get; init; } = [];

    /// <summary>Indica que la carga falló de forma recuperable.</summary>
    public bool IsRecoverable { get; init; }

    /// <summary>Mensaje visible para un fallo recuperable.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Mapea los wire-types a los datos que la vista necesita.</summary>
    public static PersonaHabilidadesViewModel From(
        PersonaDto persona,
        IReadOnlyList<PersonaSkillDetailDto> skills)
        => new()
        {
            PersonaId = persona.Id,
            PersonaNombre = $"{persona.Nombres} {persona.Apellidos}",
            Skills = skills
                .Select(skill => PersonaHabilidadRowViewModel.From(skill))
                .ToArray()
        };
}

/// <summary>Fila de una asociación Persona-Habilidad para la grilla.</summary>
public sealed record PersonaHabilidadRowViewModel(
    Guid SkillId,
    string SkillCodigo,
    string SkillNombre,
    Guid NivelHabilidadId,
    string NivelNombre)
{
    /// <summary>Mapea el DTO anidado al modelo de presentación.</summary>
    public static PersonaHabilidadRowViewModel From(PersonaSkillDetailDto skill)
        => new(
            skill.Skill.Id,
            skill.Skill.Codigo,
            skill.Skill.Nombre,
            skill.Nivel.Id,
            skill.Nivel.Nombre);
}
