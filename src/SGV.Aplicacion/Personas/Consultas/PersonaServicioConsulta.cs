using SGV.Aplicacion.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Consultas;

public sealed class PersonaServicioConsulta(IPersonaRepository repository)
    : IPersonaServicioConsulta
{
    public async Task<IReadOnlyList<PersonaDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static PersonaDto MapToDto(Persona entity)
    {
        return new PersonaDto(
            entity.Id,
            entity.Legajo,
            entity.Nombres,
            entity.Apellidos,
            entity.Email,
            entity.TipoDocumento,
            entity.NumeroDocumento,
            entity.Telefono,
            entity.IsActive
        );
    }
}
