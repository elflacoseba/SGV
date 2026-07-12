using System.Reflection;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web.Auth;

/// <summary>
/// Regresión estructural sobre <c>SGV.Web.Integration.Auth.AuthSessionFactory</c>:
/// la causa #1 de MSB4166 en la suite completa (issue #121) fue una caché estática
/// de <c>TokenValidationParameters</c> que hacía que un host configurado con
/// <c>Jwt:SigningKey</c> "A" aceptara tokens firmados con la clave "B" cuando el
/// primer test ya había inicializado la caché. Esta guarda de reflexión bloquea
/// el patrón: cero campos estáticos mutables en el tipo.
///
/// Reglas de la invariante:
/// <list type="bullet">
///   <item><c>typeof(AuthSessionFactory).GetFields(NonPublic | Static)</c> lista
///   todos los campos estáticos (incluye <c>const</c> y <c>static readonly</c>).</item>
///   <item>Los literales <c>const</c> se descartan: son inmutables por definición,
///   no pueden almacenar estado mutable entre invocaciones.</item>
///   <item>Los <c>static readonly</c> NO se descartan: aunque sólo se asignan en
///   el constructor estático, persisten durante toda la vida del proceso y
///   son exactamente lo que la issue #121 identificó como fuente de
///   contaminación entre hosts.</item>
/// </list>
/// </summary>
public sealed class AuthSessionFactoryNoStaticStateTests
{
    [Fact]
    public void AuthSessionFactory_NoStaticNonLiteralFields()
    {
        // Arrange — capturamos todos los campos estáticos no públicos declarados
        // sobre AuthSessionFactory. Excluimos los heredados de object: en este
        // momento el tipo es 'static class' sin herencia relevante, pero la
        // guarda debe sobrevivir si alguien la convierte en 'sealed' con
        // jerarquía posterior.
        var staticNonPublicFields = typeof(AuthSessionFactory)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => !field.IsLiteral)
            .ToArray();

        // Assert — ningún campo mutable a nivel de proceso. Si alguien vuelve
        // a meter un ConcurrentDictionary, un Lazy<T> o cualquier caché estática,
        // este test falla con un mensaje accionable que nombra el campo ofensor.
        Assert.Empty(staticNonPublicFields);
    }

    [Fact]
    public void AuthSessionFactory_NoStaticFieldsAtAll()
    {
        // Hardening: la invariante "cero estado compartido a nivel de proceso"
        // se sostiene incluso si alguien añade un `public const string`. Este
        // test complementa al anterior: si en el futuro el equipo necesita
        // constantes públicas para keys mágicas, se documenta en el commit;
        // hasta entonces, la clase debe estar libre de cualquier campo estático.
        var allStaticFields = typeof(AuthSessionFactory)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .ToArray();

        Assert.Empty(allStaticFields);
    }
}