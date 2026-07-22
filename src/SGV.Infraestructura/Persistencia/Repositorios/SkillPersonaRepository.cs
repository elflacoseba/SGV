using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>EF Core repository for the readonly Skill to Persona subresource.</summary>
public sealed class SkillPersonaRepository(SgvDbContext context) : ISkillPersonaRepository
{
    public async Task<PersonaHabilidadesPageResult> ListDetailedBySkillIdAsync(
        Guid skillId,
        HabilidadPersonasListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PersonaHabilidadEntity> baseQuery = context.PersonaHabilidades
            .AsNoTracking()
            .Where(link => link.HabilidadId == skillId);

        baseQuery = query.Segmento == PersonaSegmentoListado.Eliminadas
            ? baseQuery.Where(link => link.Persona.IsDeleted && !link.Persona.IsActive)
            : baseQuery.Where(link => !link.Persona.IsDeleted && link.Persona.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(link =>
                (link.Persona.Legajo != null && link.Persona.Legajo.ToLower().Contains(search)) ||
                link.Persona.Nombres.ToLower().Contains(search) ||
                link.Persona.Apellidos.ToLower().Contains(search));
        }

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var ordered = ApplySort(baseQuery, query.Sort);
        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(link => new SkillPersonaDetailDto(
                new PersonaDto(
                    link.Persona.Id,
                    link.Persona.Legajo,
                    link.Persona.Nombres,
                    link.Persona.Apellidos,
                    link.Persona.Email,
                    link.Persona.TipoDocumentoId,
                    link.Persona.TipoDocumento != null ? link.Persona.TipoDocumento.Codigo : null,
                    link.Persona.TipoDocumento != null ? link.Persona.TipoDocumento.Nombre : null,
                    link.Persona.NumeroDocumento,
                    link.Persona.Telefono,
                    link.Persona.IsActive),
                new NivelHabilidadDto(
                    link.NivelHabilidad.Id,
                    link.NivelHabilidad.Codigo,
                    link.NivelHabilidad.Nombre,
                    link.NivelHabilidad.ValorNumerico,
                    link.NivelHabilidad.Orden))
            {
                PersonaId = link.PersonaId,
                HabilidadId = link.HabilidadId,
                NivelHabilidadId = link.NivelHabilidadId
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PersonaHabilidadesPageResult(items, query.Page, query.PageSize, total, NormalizeSort(query.Sort), query.Segmento);
    }

    private static IOrderedQueryable<PersonaHabilidadEntity> ApplySort(
        IQueryable<PersonaHabilidadEntity> query,
        string? sort) => NormalizeSort(sort) switch
        {
            "legajo_desc" => query.OrderByDescending(link => link.Persona.Legajo).ThenBy(link => link.Persona.Id),
            "legajo_asc" => query.OrderBy(link => link.Persona.Legajo).ThenBy(link => link.Persona.Id),
            "apellidos_desc" => query.OrderByDescending(link => link.Persona.Apellidos).ThenByDescending(link => link.Persona.Nombres).ThenBy(link => link.Persona.Id),
            "nombres_asc" => query.OrderBy(link => link.Persona.Nombres).ThenBy(link => link.Persona.Apellidos).ThenBy(link => link.Persona.Id),
            "nombres_desc" => query.OrderByDescending(link => link.Persona.Nombres).ThenByDescending(link => link.Persona.Apellidos).ThenBy(link => link.Persona.Id),
            _ => query.OrderBy(link => link.Persona.Apellidos).ThenBy(link => link.Persona.Nombres).ThenBy(link => link.Persona.Id)
        };

    private static string NormalizeSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "legajo_asc" or "legajo_desc" or "apellidos_asc" or "apellidos_desc" or "nombres_asc" or "nombres_desc" => sort.ToLowerInvariant(),
        _ => "apellidos_asc"
    };
}
