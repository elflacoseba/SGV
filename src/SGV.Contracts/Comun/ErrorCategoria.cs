namespace SGV.Contracts.Comun;

/// <summary>
/// Categoría semántica de fallo devuelta por los <c>*CommandResult</c> y
/// <c>*DeleteResult</c> de <c>SGV.Contracts</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only.</b> Las variantes y sus ordinales son contrato público
/// estable: agregar nuevas variantes SOLO al final y NO reordenar ni
/// reasignar ordinales existentes. Esto preserva la persistencia, los logs
/// históricos y la serialización por nombre (<c>switch</c> expressions por
/// nombre, no por ordinal).
/// </para>
/// <para>
/// <b>Matriz HTTP → Categoría</b> (canónica, design §2.3):
/// <list type="table">
///   <listheader>
///     <term><c>ErrorCategoria</c></term>
///     <description>Status HTTP observable</description>
///   </listheader>
///   <item><term><c>NotFound</c> (0)</term><description>HTTP 404.</description></item>
///   <item><term><c>Conflict</c> (1)</term><description>HTTP 409.</description></item>
///   <item><term><c>Validation</c> (2)</term><description>HTTP 400/422 (con <c>FieldErrors</c> opcional).</description></item>
///   <item><term><c>Unauthorized</c> (3)</term><description>HTTP 401.</description></item>
///   <item><term><c>Forbidden</c> (4)</term><description>HTTP 403.</description></item>
///   <item><term><c>Transport</c> (5)</term><description>HTTP 408/5xx/502/503/504 desde <see cref="System.Net.Http.HttpResponseMessage"/>.</description></item>
///   <item><term><c>Unexpected</c> (6)</term><description>Cualquier otro status no exitoso (incluye 3xx).</description></item>
/// </list>
/// </para>
/// </remarks>
public enum ErrorCategoria
{
    /// <summary>Recurso inexistente (HTTP 404).</summary>
    NotFound = 0,

    /// <summary>Conflicto de unicidad/estado (HTTP 409).</summary>
    Conflict = 1,

    /// <summary>Datos inválidos (HTTP 400/422), con <c>FieldErrors</c> opcional.</summary>
    Validation = 2,

    /// <summary>Sesión ausente o credencial inválida (HTTP 401).</summary>
    Unauthorized = 3,

    /// <summary>Autenticado sin permiso (HTTP 403).</summary>
    Forbidden = 4,

    /// <summary>Falla de transporte o 5xx del backend (HTTP 408/500/502/503/504).</summary>
    Transport = 5,

    /// <summary>Cualquier otro status no exitoso (incluye 3xx).</summary>
    Unexpected = 6,
}
