using SGV.Dominio.Comun;

namespace SGV.Dominio.Vacantes;

public sealed class HistorialEstadoVacante : EntidadBase
{
    private HistorialEstadoVacante()
    {
    }

    public HistorialEstadoVacante(Guid vacanteId, Guid? estadoAnteriorId, Guid estadoNuevoId, DateTime changedAt, string? changedByUserId, string? motivo)
    {
        VacanteId = vacanteId;
        EstadoAnteriorId = estadoAnteriorId;
        EstadoNuevoId = estadoNuevoId;
        ChangedAt = changedAt;
        ChangedByUserId = ValidacionesDominio.Opcional(changedByUserId, nameof(ChangedByUserId), 450);
        Motivo = ValidacionesDominio.Opcional(motivo, nameof(Motivo), 500);
    }

    public Guid VacanteId { get; private set; }

    public Vacante Vacante { get; private set; } = null!;

    public Guid? EstadoAnteriorId { get; private set; }

    public EstadoVacante? EstadoAnterior { get; private set; }

    public Guid EstadoNuevoId { get; private set; }

    public EstadoVacante EstadoNuevo { get; private set; } = null!;

    public DateTime ChangedAt { get; private set; }

    public string? ChangedByUserId { get; private set; }

    public string? Motivo { get; private set; }
}
