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

    /// <summary>
    /// Returns <see langword="true"/> when the occupation is currently active:
    /// no end date set and not logically deleted.
    /// </summary>
    public bool EsVigente => FechaFin is null && !IsDeleted;

    /// <summary>
    /// Guard method that throws <see cref="InvalidOperationException"/>
    /// when the occupation is no longer editable (finalized or deleted).
    /// </summary>
    private void RequerirEditable()
    {
        if (FechaFin is not null)
        {
            throw new InvalidOperationException(
                "La ocupación ya está finalizada y no se puede modificar.");
        }

        if (IsDeleted)
        {
            throw new InvalidOperationException(
                "La ocupación ya está eliminada y no se puede modificar.");
        }
    }

    /// <summary>
    /// Updates all mutable fields of the occupation.
    /// Only allowed when the occupation is active (not finalized, not deleted).
    /// </summary>
    public void Actualizar(Guid personaId, Guid puestoId, DateOnly fechaInicio, TipoAsignacion tipoAsignacion, string? observaciones = null)
    {
        RequerirEditable();

        if (FechaInicio != fechaInicio && FechaFin.HasValue && fechaInicio > FechaFin.Value)
        {
            throw new InvalidOperationException(
                "La nueva fecha de inicio no puede ser posterior a la fecha de fin actual.");
        }

        PersonaId = personaId;
        PuestoId = puestoId;
        FechaInicio = fechaInicio;
        TipoAsignacion = tipoAsignacion;
        Observaciones = ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 1000);
    }

    /// <summary>
    /// Finalizes an occupation by setting its end date.
    /// Only allowed when the occupation is active.
    /// </summary>
    public void Finalizar(DateOnly fechaFin, string? observaciones = null)
    {
        RequerirEditable();

        if (fechaFin < FechaInicio)
        {
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        FechaFin = fechaFin;
        Observaciones = ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 1000);
    }

    /// <summary>
    /// Logically deletes the occupation. Sets <see cref="IsDeleted"/> and
    /// <see cref="DeletedAt"/>. Only allowed when the occupation is active.
    /// </summary>
    public void EliminarLogicamente()
    {
        RequerirEditable();

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reactivates a finalized or logically deleted occupation by clearing
    /// <see cref="FechaFin"/>, <see cref="IsDeleted"/>, and their audit
    /// timestamps. Not allowed when the occupation is already active.
    /// </summary>
    public void Reactivar()
    {
        if (EsVigente)
        {
            throw new InvalidOperationException(
                "La ocupación ya está activa y no se puede reactivar.");
        }

        FechaFin = null;
        IsDeleted = false;
        DeletedAt = null;
    }
}
