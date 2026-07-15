namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Contrato del catálogo de <see cref="SGV.Contracts.Personas.Consultas.Dtos.PersonaDto"/>
/// activas que alimentará el dropdown de <c>Pages/Seguridad/Usuarios/Create.cshtml</c>
/// (PR 4). Se define en PR 2 para que el seam HTTP del módulo exponga
/// la API estable desde ya y los tests de integración puedan triangular
/// la selección sin acoplarse al HTTP handler concreto.
/// </summary>
/// <remarks>
/// <para>
/// Justificación de la interfaz: el catálogo de Personas activas es un
/// subdominio transitorio para crear usuarios; mantener la dependencia
/// sobre <c>IPersonaApiClient.GetAllAsync</c> directo en el PageModel
/// de Create lo ataría al shape HTTP. Esta interfaz permite que PR 4
/// inyecte el proveedor concreto (HTTP-backed en producción, fake en
/// tests) y que el PageModel sólo conozca el contrato.
/// </para>
/// <para>
/// La implementación concreta <c>HttpPersonaOptionsProvider</c> vive
/// en <c>src/SGV.Web/Integration/Usuarios</c> y se registra en
/// <c>Program.cs</c>; un <c>FakePersonaOptionsProvider</c> vive en
/// <c>tests/SGV.Tests/Web/Usuario</c> para que las pages se puedan
/// probar sin tocar el HTTP pipeline.
/// </para>
/// </remarks>
public interface IPersonaOptionsProvider
{
    /// <summary>
    /// Devuelve el catálogo de personas activas (lista plana sin
    /// paginar). Acepto baja concurrencia y dataset &lt; 500 personas
    /// (mismo criterio que el typeahead de Personas archivado en
    /// PR 3/4 del change <c>2026-07-14-frontend-crud-personas</c>).
    /// </summary>
    Task<IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.PersonaDto>> GetActivasAsync(
        CancellationToken cancellationToken = default);
}
