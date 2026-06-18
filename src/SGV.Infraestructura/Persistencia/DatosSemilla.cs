using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Infraestructura.Persistencia.Entidades;

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

        builder.Entity<NivelHabilidadEntity>().HasData(
            new NivelHabilidadEntity { Id = NivelBasicoId, Codigo = "Basico", Nombre = "Básico", ValorNumerico = 1, Orden = 1 },
            new NivelHabilidadEntity { Id = NivelIntermedioId, Codigo = "Intermedio", Nombre = "Intermedio", ValorNumerico = 2, Orden = 2 },
            new NivelHabilidadEntity { Id = NivelAvanzadoId, Codigo = "Avanzado", Nombre = "Avanzado", ValorNumerico = 3, Orden = 3 },
            new NivelHabilidadEntity { Id = NivelExpertoId, Codigo = "Experto", Nombre = "Experto", ValorNumerico = 4, Orden = 4 });

        builder.Entity<EstadoVacanteEntity>().HasData(
            new EstadoVacanteEntity { Id = VacanteAbiertaId, Codigo = "Abierta", Nombre = "Abierta", Orden = 1, EsTerminal = false },
            new EstadoVacanteEntity { Id = VacanteEnSeleccionId, Codigo = "EnSeleccion", Nombre = "En Selección", Orden = 2, EsTerminal = false },
            new EstadoVacanteEntity { Id = VacanteCubiertaId, Codigo = "Cubierta", Nombre = "Cubierta", Orden = 3, EsTerminal = true },
            new EstadoVacanteEntity { Id = VacanteCanceladaId, Codigo = "Cancelada", Nombre = "Cancelada", Orden = 4, EsTerminal = true });

        builder.Entity<EstadoPostulacionEntity>().HasData(
            new EstadoPostulacionEntity { Id = PostulacionPostuladoId, Codigo = "Postulado", Nombre = "Postulado", Orden = 1, EsTerminal = false, EsTerminalPositivo = false },
            new EstadoPostulacionEntity { Id = PostulacionPreseleccionadoId, Codigo = "Preseleccionado", Nombre = "Preseleccionado", Orden = 2, EsTerminal = false, EsTerminalPositivo = false },
            new EstadoPostulacionEntity { Id = PostulacionEntrevistadoId, Codigo = "Entrevistado", Nombre = "Entrevistado", Orden = 3, EsTerminal = false, EsTerminalPositivo = false },
            new EstadoPostulacionEntity { Id = PostulacionAprobadoId, Codigo = "Aprobado", Nombre = "Aprobado", Orden = 4, EsTerminal = false, EsTerminalPositivo = false },
            new EstadoPostulacionEntity { Id = PostulacionRechazadoId, Codigo = "Rechazado", Nombre = "Rechazado", Orden = 5, EsTerminal = true, EsTerminalPositivo = false },
            new EstadoPostulacionEntity { Id = PostulacionContratadoId, Codigo = "Contratado", Nombre = "Contratado", Orden = 6, EsTerminal = true, EsTerminalPositivo = true });

        builder.Entity<NivelCargoEntity>().HasData(
            new NivelCargoEntity
            {
                Id = NivelCargoConstantes.DirectivoId,
                Codigo = NivelCargoConstantes.DirectivoCodigo,
                Nombre = NivelCargoConstantes.DirectivoNombre,
                ValorNumerico = NivelCargoConstantes.DirectivoValorNumerico,
                Orden = NivelCargoConstantes.DirectivoOrden
            },
            new NivelCargoEntity
            {
                Id = NivelCargoConstantes.ConduccionMediaId,
                Codigo = NivelCargoConstantes.ConduccionMediaCodigo,
                Nombre = NivelCargoConstantes.ConduccionMediaNombre,
                ValorNumerico = NivelCargoConstantes.ConduccionMediaValorNumerico,
                Orden = NivelCargoConstantes.ConduccionMediaOrden
            },
            new NivelCargoEntity
            {
                Id = NivelCargoConstantes.OperativoId,
                Codigo = NivelCargoConstantes.OperativoCodigo,
                Nombre = NivelCargoConstantes.OperativoNombre,
                ValorNumerico = NivelCargoConstantes.OperativoValorNumerico,
                Orden = NivelCargoConstantes.OperativoOrden
            },
            new NivelCargoEntity
            {
                Id = NivelCargoConstantes.AcademicoId,
                Codigo = NivelCargoConstantes.AcademicoCodigo,
                Nombre = NivelCargoConstantes.AcademicoNombre,
                ValorNumerico = NivelCargoConstantes.AcademicoValorNumerico,
                Orden = NivelCargoConstantes.AcademicoOrden
            });

        builder.Entity<CargoEntity>().HasData(
            new CargoEntity { Id = CargoDecanoId, Codigo = "DECANO", Nombre = "Decano", NivelId = NivelCargoConstantes.DirectivoId, IsActive = true },
            new CargoEntity { Id = CargoSecretarioId, Codigo = "SECRETARIO", Nombre = "Secretario", NivelId = NivelCargoConstantes.DirectivoId, IsActive = true },
            new CargoEntity { Id = CargoDirectorId, Codigo = "DIRECTOR", Nombre = "Director", NivelId = NivelCargoConstantes.ConduccionMediaId, IsActive = true },
            new CargoEntity { Id = CargoJefeDepartamentoId, Codigo = "JEFE_DEPARTAMENTO", Nombre = "Jefe de Departamento", NivelId = NivelCargoConstantes.ConduccionMediaId, IsActive = true },
            new CargoEntity { Id = CargoAdministrativoId, Codigo = "ADMINISTRATIVO", Nombre = "Administrativo", NivelId = NivelCargoConstantes.OperativoId, IsActive = true },
            new CargoEntity { Id = CargoProfesorId, Codigo = "PROFESOR", Nombre = "Profesor", NivelId = NivelCargoConstantes.AcademicoId, IsActive = true });

        builder.Entity<HabilidadEntity>().HasData(
            new HabilidadEntity { Id = HabilidadLiderazgoId, Codigo = "LIDERAZGO", Nombre = "Liderazgo", Categoria = "Conducción", IsActive = true },
            new HabilidadEntity { Id = HabilidadGestionPersonalId, Codigo = "GESTION_PERSONAL", Nombre = "Gestión de Personal", Categoria = "Conducción", IsActive = true },
            new HabilidadEntity { Id = HabilidadSqlServerId, Codigo = "SQL_SERVER", Nombre = "SQL Server", Categoria = "Técnica", IsActive = true },
            new HabilidadEntity { Id = HabilidadEfCoreId, Codigo = "EF_CORE", Nombre = "Entity Framework Core", Categoria = "Técnica", IsActive = true },
            new HabilidadEntity { Id = HabilidadDotNetId, Codigo = "DOTNET", Nombre = "Programación .NET", Categoria = "Técnica", IsActive = true },
            new HabilidadEntity { Id = HabilidadAdministracionPublicaId, Codigo = "ADMINISTRACION_PUBLICA", Nombre = "Administración Pública", Categoria = "Dominio", IsActive = true },
            new HabilidadEntity { Id = HabilidadDocenciaUniversitariaId, Codigo = "DOCENCIA_UNIVERSITARIA", Nombre = "Docencia Universitaria", Categoria = "Académica", IsActive = true });

        builder.Entity<TipoUnidadOrganizativaEntity>().HasData(
            new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.InstitucionId, Codigo = "Institucion", Nombre = "Institución" },
            new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.FacultadId, Codigo = "Facultad", Nombre = "Facultad" },
            new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.SecretariaId, Codigo = "Secretaria", Nombre = "Secretaría" },
            new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.DireccionId, Codigo = "Direccion", Nombre = "Dirección" },
            new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.DepartamentoId, Codigo = "Departamento", Nombre = "Departamento" },
            new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.DivisionId, Codigo = "Division", Nombre = "División" },
            new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.AreaId, Codigo = "Area", Nombre = "Área" });
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
