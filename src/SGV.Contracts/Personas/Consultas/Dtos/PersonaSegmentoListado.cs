namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// Defines the listing segment for persona queries: active (non-deleted) personas
/// or soft-deleted personas. The value <c>Activas</c> is the default used by the
/// query contract and by the HTTP/Web boundary when no explicit <c>status</c>
/// is provided.
/// </summary>
public enum PersonaSegmentoListado
{
    /// <summary>Return only active (non-deleted) personas. This is the default.</summary>
    Activas = 0,

    /// <summary>Return only soft-deleted personas.</summary>
    Eliminadas = 1
}