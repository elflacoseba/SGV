using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Persistencia;

public sealed class SgvDbContext : IdentityDbContext<SgvIdentityUser, IdentityRole, string>
{
    public SgvDbContext(DbContextOptions<SgvDbContext> options)
        : base(options)
    {
    }

    public DbSet<UnidadOrganizativaEntity> UnidadesOrganizativas => Set<UnidadOrganizativaEntity>();

    public DbSet<CargoEntity> Cargos => Set<CargoEntity>();

    public DbSet<HabilidadEntity> Habilidades => Set<HabilidadEntity>();

    public DbSet<NivelHabilidadEntity> NivelesHabilidad => Set<NivelHabilidadEntity>();

    public DbSet<CargoHabilidadEntity> CargoHabilidades => Set<CargoHabilidadEntity>();

    public DbSet<PersonaEntity> Personas => Set<PersonaEntity>();

    public DbSet<PersonaHabilidadEntity> PersonaHabilidades => Set<PersonaHabilidadEntity>();

    public DbSet<PuestoEntity> Puestos => Set<PuestoEntity>();

    public DbSet<OcupacionEntity> Ocupaciones => Set<OcupacionEntity>();

    public DbSet<EstadoVacanteEntity> EstadosVacante => Set<EstadoVacanteEntity>();

    public DbSet<VacanteEntity> Vacantes => Set<VacanteEntity>();

    public DbSet<HistorialEstadoVacanteEntity> HistorialEstadosVacante => Set<HistorialEstadoVacanteEntity>();

    public DbSet<PostulanteEntity> Postulantes => Set<PostulanteEntity>();

    public DbSet<EstadoPostulacionEntity> EstadosPostulacion => Set<EstadoPostulacionEntity>();

    public DbSet<PostulacionEntity> Postulaciones => Set<PostulacionEntity>();

    public DbSet<HistorialEstadoPostulacionEntity> HistorialEstadosPostulacion => Set<HistorialEstadoPostulacionEntity>();

    public DbSet<EvaluacionPostulacionEntity> EvaluacionesPostulacion => Set<EvaluacionPostulacionEntity>();

    public DbSet<AuditoriaEntity> Auditorias => Set<AuditoriaEntity>();

    public DbSet<TipoUnidadOrganizativaEntity> TiposUnidadOrganizativa => Set<TipoUnidadOrganizativaEntity>();

    public DbSet<NivelCargoEntity> NivelesCargo => Set<NivelCargoEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(SgvDbContext).Assembly);
        DatosSemilla.Configurar(builder);
    }
}
