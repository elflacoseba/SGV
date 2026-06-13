using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SGV.Dominio.Auditoria;
using SGV.Dominio.Habilidades;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;
using SGV.Dominio.Seleccion;
using SGV.Dominio.Vacantes;

namespace SGV.Infraestructura.Persistencia;

public sealed class SgvDbContext : IdentityDbContext<IdentityUser>
{
    public SgvDbContext(DbContextOptions<SgvDbContext> options)
        : base(options)
    {
    }

    public DbSet<UnidadOrganizativa> UnidadesOrganizativas => Set<UnidadOrganizativa>();

    public DbSet<Cargo> Cargos => Set<Cargo>();

    public DbSet<Habilidad> Habilidades => Set<Habilidad>();

    public DbSet<NivelHabilidad> NivelesHabilidad => Set<NivelHabilidad>();

    public DbSet<CargoHabilidad> CargoHabilidades => Set<CargoHabilidad>();

    public DbSet<Persona> Personas => Set<Persona>();

    public DbSet<PersonaHabilidad> PersonaHabilidades => Set<PersonaHabilidad>();

    public DbSet<Puesto> Puestos => Set<Puesto>();

    public DbSet<Ocupacion> Ocupaciones => Set<Ocupacion>();

    public DbSet<EstadoVacante> EstadosVacante => Set<EstadoVacante>();

    public DbSet<Vacante> Vacantes => Set<Vacante>();

    public DbSet<HistorialEstadoVacante> HistorialEstadosVacante => Set<HistorialEstadoVacante>();

    public DbSet<Postulante> Postulantes => Set<Postulante>();

    public DbSet<EstadoPostulacion> EstadosPostulacion => Set<EstadoPostulacion>();

    public DbSet<Postulacion> Postulaciones => Set<Postulacion>();

    public DbSet<HistorialEstadoPostulacion> HistorialEstadosPostulacion => Set<HistorialEstadoPostulacion>();

    public DbSet<EvaluacionPostulacion> EvaluacionesPostulacion => Set<EvaluacionPostulacion>();

    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(SgvDbContext).Assembly);
        DatosSemilla.Configurar(builder);
    }
}
