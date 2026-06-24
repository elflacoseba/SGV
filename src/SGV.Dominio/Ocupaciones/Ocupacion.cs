using SGV.Dominio.Comun;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;

namespace SGV.Dominio.Ocupaciones;

public sealed class Ocupacion : EntidadAuditable
{
    private Ocupacion()
    {
    }

    public Ocupacion(Guid personaId, Guid puestoId, DateOnly fechaInicio, TipoAsignacion tipoAsignacion, DateOnly? fechaFin = null)
    {
        if (fechaFin.HasValue && fechaFin.Value < fechaInicio)
        {
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        PersonaId = personaId;
        PuestoId = puestoId;
        FechaInicio = fechaInicio;
        FechaFin = fechaFin;
        TipoAsignacion = tipoAsignacion;
    }

    public Guid PersonaId { get; private set; }

    public Persona Persona { get; private set; } = null!;

    public Guid PuestoId { get; private set; }

    public Puesto Puesto { get; private set; } = null!;

    public DateOnly FechaInicio { get; private set; }

    public DateOnly? FechaFin { get; private set; }

    public TipoAsignacion TipoAsignacion { get; private set; }

    public string? Observaciones { get; private set; }

    public bool EsVigente => FechaFin is null;

    public void Finalizar(DateOnly fechaFin, string? observaciones = null)
    {
        if (fechaFin < FechaInicio)
        {
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        FechaFin = fechaFin;
        Observaciones = ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 1000);
    }
}
