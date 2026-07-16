using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Usuarios;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Fake en memoria de <see cref="IPersonaOptionsProvider"/> usado por la
/// suite web del módulo Usuarios (PR 4 introduce el catálogo de Personas
/// activas que alimenta el dropdown de <c>Pages/Seguridad/Usuarios/Create.cshtml</c>).
/// Espejo del <c>FakeUsuarioApiClient</c>: modela el catálogo plano sin
/// paginar para que los tests puedan triangular la carga inicial, el caso
/// "sin Personas activas" y los fallos de transporte sin requerir un
/// backend real.
/// </summary>
public sealed class FakePersonaOptionsProvider : IPersonaOptionsProvider
{
    private readonly IReadOnlyList<PersonaDto> _activas;
    private readonly Exception? _exception;

    public FakePersonaOptionsProvider()
        : this(Array.Empty<PersonaDto>(), null)
    {
    }

    public FakePersonaOptionsProvider(IReadOnlyList<PersonaDto> activas)
        : this(activas, null)
    {
    }

    private FakePersonaOptionsProvider(IReadOnlyList<PersonaDto> activas, Exception? exception)
    {
        _activas = activas;
        _exception = exception;
    }

    /// <summary>Cantidad de invocaciones a <see cref="GetActivasAsync"/>.</summary>
    public int GetActivasCalls { get; private set; }

    /// <summary>
    /// Construye un fake que devuelve la lista especificada en
    /// <see cref="GetActivasAsync"/>.
    /// </summary>
    public static FakePersonaOptionsProvider WithActivas(params PersonaDto[] activas)
        => new(activas);

    /// <summary>
    /// Construye un fake que devuelve el catálogo vacío (dropdown vacío
    /// en Create → bloquea el submit con mensaje guía).
    /// </summary>
    public static FakePersonaOptionsProvider Empty()
        => new(Array.Empty<PersonaDto>());

    /// <summary>
    /// Construye un fake que arroja la excepción indicada en
    /// <see cref="GetActivasAsync"/>.
    /// </summary>
    public static FakePersonaOptionsProvider WithFailure(Exception exception)
        => new(Array.Empty<PersonaDto>(), exception);

    public Task<IReadOnlyList<PersonaDto>> GetActivasAsync(CancellationToken cancellationToken = default)
    {
        GetActivasCalls++;

        if (_exception is not null)
        {
            throw _exception;
        }

        return Task.FromResult(_activas);
    }
}