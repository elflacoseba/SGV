using SGV.Dominio.Comun;

namespace SGV.Dominio.Habilidades;

public sealed record class Habilidad : EntidadAuditable
{
    private Habilidad()
    {
    }

    public Habilidad(string codigo, string nombre, Guid? categoriaId = null, string? descripcion = null)
    {
        CambiarDatos(codigo, nombre, categoriaId, descripcion);
        IsActive = true;
    }

    /// <summary>
    /// Código único de la habilidad. Mutable solo desde dentro de la entidad
    /// vía <see cref="Actualizar"/>; la verificación de unicidad activa contra
    /// otras Habilidades es responsabilidad del servicio de aplicación.
    /// </summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    /// <summary>
    /// FK opcional al catálogo <see cref="CategoriaHabilidad"/>. La
    /// validación contra el catálogo es responsabilidad del servicio de
    /// aplicación (<c>HabilidadServicioComandos.CrearAsync</c> /
    /// <c>ActualizarAsync</c>).
    /// </summary>
    public Guid? CategoriaId { get; private set; }

    /// <summary>
    /// Navegación al catálogo <see cref="CategoriaHabilidad"/> hidratado por
    /// el repositorio cuando se hace <c>Include</c> o proyección LEFT JOIN.
    /// </summary>
    public CategoriaHabilidad? Categoria { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Reemplaza todos los campos editables y el código. Reservado al constructor
    /// y al mapper de persistencia (slice 2).
    /// </summary>
    public void CambiarDatos(string codigo, string nombre, Guid? categoriaId = null, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), HabilidadRules.CodigoMaxLength);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        CategoriaId = categoriaId;
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    /// <summary>
    /// Actualiza los campos editables de la habilidad, incluido <see cref="Codigo"/>.
    /// La unicidad activa del código se valida en el servicio de aplicación
    /// antes de invocar este método; este solo aplica reglas de shape
    /// (requerido, longitud máxima). Delega en <see cref="CambiarDatos"/>
    /// para evitar duplicación de invariantes entre el constructor y la
    /// actualización.
    /// </summary>
    /// <param name="codigo">Nuevo código de la habilidad. Requerido, máximo <see cref="HabilidadRules.CodigoMaxLength"/> caracteres.</param>
    /// <param name="nombre">Nuevo nombre de la habilidad. Requerido, máximo 200 caracteres.</param>
    /// <param name="categoriaId">FK opcional al catálogo <see cref="CategoriaHabilidad"/>.</param>
    /// <param name="descripcion">Descripción opcional, máximo 1000 caracteres.</param>
    public void Actualizar(string codigo, string nombre, Guid? categoriaId = null, string? descripcion = null)
        => CambiarDatos(codigo, nombre, categoriaId, descripcion);

    /// <summary>
    /// Desactiva la habilidad (baja lógica). No elimina el registro y no
    /// altera asignaciones existentes a cargos o personas.
    /// </summary>
    public void Desactivar()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactiva la habilidad. La verificación de unicidad activa de Codigo
    /// es responsabilidad del servicio de aplicación.
    /// </summary>
    public void Activar()
    {
        IsActive = true;
    }

    /// <summary>
    /// Factory de hidratación desde la capa de persistencia. Replica las
    /// invariantes de shape del constructor primario y asigna todos los campos
    /// persistibles (incluyendo audit + <see cref="IsActive"/>) con setters
    /// tipados para evitar el path de reflexión que
    /// <c>PersistenceToDomainMapper.SetProperty</c> implementaba. Sólo accesible
    /// desde <c>SGV.Infraestructura</c> y <c>SGV.Tests</c>.
    /// </summary>
    /// <param name="id">Identificador persistido.</param>
    /// <param name="codigo">Código único, requerido, máx. <see cref="HabilidadRules.CodigoMaxLength"/>.</param>
    /// <param name="nombre">Nombre requerido, máx. 200 caracteres.</param>
    /// <param name="categoriaId">FK opcional al catálogo <see cref="CategoriaHabilidad"/>.</param>
    /// <param name="descripcion">Descripción opcional, máx. 1000 caracteres.</param>
    /// <param name="isActive">Flag activo persistido.</param>
    /// <param name="createdAt">Marca de auditoría de creación.</param>
    /// <param name="createdByUserId">Usuario creador (nullable).</param>
    /// <param name="updatedAt">Marca de auditoría de última edición.</param>
    /// <param name="updatedByUserId">Usuario de última edición (nullable).</param>
    /// <param name="isDeleted">Flag de soft delete.</param>
    /// <param name="deletedAt">Marca de auditoría de borrado lógico.</param>
    /// <param name="deletedByUserId">Usuario de borrado lógico (nullable).</param>
    internal static Habilidad Reconstitute(
        Guid id,
        string codigo,
        string nombre,
        Guid? categoriaId,
        string? descripcion,
        bool isActive,
        DateTime createdAt,
        string? createdByUserId,
        DateTime? updatedAt,
        string? updatedByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId)
    {
        var self = new Habilidad
        {
            Id = id,
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId,
            UpdatedAt = updatedAt,
            UpdatedByUserId = updatedByUserId,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedByUserId = deletedByUserId
        };

        // Aplicamos las mismas reglas de shape que CambiarDatos, evitando
        // reasignar después de la construcción del record.
        self.Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), HabilidadRules.CodigoMaxLength);
        self.Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        self.CategoriaId = categoriaId;
        self.Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        self.IsActive = isActive;

        return self;
    }
}