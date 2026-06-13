using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SGV.Dominio.Habilidades;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Seleccion;
using SGV.Dominio.Vacantes;

namespace SGV.Infraestructura.Persistencia;

internal static class DatosSemilla
{
    public static readonly Guid NivelBasicoId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid NivelIntermedioId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid NivelAvanzadoId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid NivelExpertoId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    public static readonly Guid VacanteAbiertaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid VacanteEnSeleccionId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid VacanteCubiertaId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid VacanteCanceladaId = Guid.Parse("20000000-0000-0000-0000-000000000004");

    public static readonly Guid PostulacionPostuladoId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid PostulacionPreseleccionadoId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid PostulacionEntrevistadoId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid PostulacionAprobadoId = Guid.Parse("30000000-0000-0000-0000-000000000004");
    public static readonly Guid PostulacionRechazadoId = Guid.Parse("30000000-0000-0000-0000-000000000005");
    public static readonly Guid PostulacionContratadoId = Guid.Parse("30000000-0000-0000-0000-000000000006");

    public static readonly Guid CargoDecanoId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid CargoSecretarioId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    public static readonly Guid CargoDirectorId = Guid.Parse("40000000-0000-0000-0000-000000000003");
    public static readonly Guid CargoJefeDepartamentoId = Guid.Parse("40000000-0000-0000-0000-000000000004");
    public static readonly Guid CargoAdministrativoId = Guid.Parse("40000000-0000-0000-0000-000000000005");
    public static readonly Guid CargoProfesorId = Guid.Parse("40000000-0000-0000-0000-000000000006");

    public static readonly Guid HabilidadLiderazgoId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid HabilidadGestionPersonalId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    public static readonly Guid HabilidadSqlServerId = Guid.Parse("50000000-0000-0000-0000-000000000003");
    public static readonly Guid HabilidadEfCoreId = Guid.Parse("50000000-0000-0000-0000-000000000004");
    public static readonly Guid HabilidadDotNetId = Guid.Parse("50000000-0000-0000-0000-000000000005");
    public static readonly Guid HabilidadAdministracionPublicaId = Guid.Parse("50000000-0000-0000-0000-000000000006");
    public static readonly Guid HabilidadDocenciaUniversitariaId = Guid.Parse("50000000-0000-0000-0000-000000000007");

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<IdentityRole>().HasData(
            CrearRol("Administrador"),
            CrearRol("RecursosHumanos"),
            CrearRol("GestorOrganizacional"),
            CrearRol("EvaluadorSeleccion"),
            CrearRol("Lector"));

        builder.Entity<NivelHabilidad>().HasData(
            new NivelHabilidad("Basico", "Básico", 1, 1) { Id = NivelBasicoId },
            new NivelHabilidad("Intermedio", "Intermedio", 2, 2) { Id = NivelIntermedioId },
            new NivelHabilidad("Avanzado", "Avanzado", 3, 3) { Id = NivelAvanzadoId },
            new NivelHabilidad("Experto", "Experto", 4, 4) { Id = NivelExpertoId });

        builder.Entity<EstadoVacante>().HasData(
            new EstadoVacante("Abierta", "Abierta", 1, false) { Id = VacanteAbiertaId },
            new EstadoVacante("EnSeleccion", "En Selección", 2, false) { Id = VacanteEnSeleccionId },
            new EstadoVacante("Cubierta", "Cubierta", 3, true) { Id = VacanteCubiertaId },
            new EstadoVacante("Cancelada", "Cancelada", 4, true) { Id = VacanteCanceladaId });

        builder.Entity<EstadoPostulacion>().HasData(
            new EstadoPostulacion("Postulado", "Postulado", 1, false, false) { Id = PostulacionPostuladoId },
            new EstadoPostulacion("Preseleccionado", "Preseleccionado", 2, false, false) { Id = PostulacionPreseleccionadoId },
            new EstadoPostulacion("Entrevistado", "Entrevistado", 3, false, false) { Id = PostulacionEntrevistadoId },
            new EstadoPostulacion("Aprobado", "Aprobado", 4, false, false) { Id = PostulacionAprobadoId },
            new EstadoPostulacion("Rechazado", "Rechazado", 5, true, false) { Id = PostulacionRechazadoId },
            new EstadoPostulacion("Contratado", "Contratado", 6, true, true) { Id = PostulacionContratadoId });

        builder.Entity<Cargo>().HasData(
            new Cargo("DECANO", "Decano", "Directivo") { Id = CargoDecanoId },
            new Cargo("SECRETARIO", "Secretario", "Directivo") { Id = CargoSecretarioId },
            new Cargo("DIRECTOR", "Director", "Conducción media") { Id = CargoDirectorId },
            new Cargo("JEFE_DEPARTAMENTO", "Jefe de Departamento", "Conducción media") { Id = CargoJefeDepartamentoId },
            new Cargo("ADMINISTRATIVO", "Administrativo", "Operativo") { Id = CargoAdministrativoId },
            new Cargo("PROFESOR", "Profesor", "Académico") { Id = CargoProfesorId });

        builder.Entity<Habilidad>().HasData(
            new Habilidad("LIDERAZGO", "Liderazgo", "Conducción") { Id = HabilidadLiderazgoId },
            new Habilidad("GESTION_PERSONAL", "Gestión de Personal", "Conducción") { Id = HabilidadGestionPersonalId },
            new Habilidad("SQL_SERVER", "SQL Server", "Técnica") { Id = HabilidadSqlServerId },
            new Habilidad("EF_CORE", "Entity Framework Core", "Técnica") { Id = HabilidadEfCoreId },
            new Habilidad("DOTNET", "Programación .NET", "Técnica") { Id = HabilidadDotNetId },
            new Habilidad("ADMINISTRACION_PUBLICA", "Administración Pública", "Dominio") { Id = HabilidadAdministracionPublicaId },
            new Habilidad("DOCENCIA_UNIVERSITARIA", "Docencia Universitaria", "Académica") { Id = HabilidadDocenciaUniversitariaId });
    }

    private static IdentityRole CrearRol(string nombre)
    {
        return new IdentityRole
        {
            Id = nombre,
            Name = nombre,
            NormalizedName = nombre.ToUpperInvariant(),
            ConcurrencyStamp = $"rol-{nombre}"
        };
    }
}
