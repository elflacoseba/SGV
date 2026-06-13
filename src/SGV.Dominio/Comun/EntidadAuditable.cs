namespace SGV.Dominio.Comun;

public abstract class EntidadAuditable : EntidadBase
{
    public DateTime CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedByUserId { get; set; }
}
