using Xunit;

namespace SGV.Tests.Api.Collections;

/// <summary>
/// Definición de colección xUnit que comparte una única instancia de
/// <see cref="ApiIntegrationFixture"/> entre todas las clases de tests
/// marcadas con <c>[Collection("ApiIntegration")]</c>. Reemplaza los
/// <c>new ApiWebApplicationFactory()</c> por test con una raíz compartida.
/// </summary>
[CollectionDefinition("ApiIntegration")]
public sealed class ApiIntegrationCollection : ICollectionFixture<ApiIntegrationFixture>
{
}