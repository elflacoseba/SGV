using Xunit;

namespace SGV.Tests.Integration;

/// <summary>
/// Serializes every test class that talks to the shared <c>sgv_test</c>
/// MySQL database so they cannot race on the seed admin, on AspNetUsers
/// inserts or on Persona FK cleanup.
///
/// Without this collection, xUnit runs each test class in its own thread
/// pool task. Multiple <c>JwtRealWebApplicationFactory.InitializeAsync</c>
/// invocations from different classes would then race against
/// <c>FindByNameAsync("admin")</c> + <c>CreateAsync</c>, occasionally
/// hitting "Duplicate entry 'ADMIN' for key 'aspnetusers.UserNameIndex'"
/// (issue #260).
///
/// <see cref="CollectionDefinitionAttribute.DisableParallelization"/>
/// also stops other unrelated collections from running while these
/// integration tests hold the only MySQL connection, removing the second
/// class of races observed in
/// <c>BloquearDesbloquearEliminarGatewayTests.QueryAsync_ByBloqueadas</c>
/// where a parallel <c>VaciarTablasAsync</c> from another class deleted
/// the persona backing the user the gateway had just blocked.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MySqlIntegrationCollection
{
    public const string Name = "MySqlIntegration";
}