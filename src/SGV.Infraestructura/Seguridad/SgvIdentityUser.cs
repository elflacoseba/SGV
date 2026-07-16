using Microsoft.AspNetCore.Identity;

namespace SGV.Infraestructura.Seguridad;

public sealed class SgvIdentityUser : IdentityUser
{
    public Guid PersonaId { get; set; }

    public bool IsDeleted { get; set; }
}
