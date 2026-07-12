using Xunit;

namespace SGV.Tests.Web.Collections;

/// <summary>
/// Definición de colección xUnit que comparte una única instancia de
/// <see cref="WebIntegrationFixture"/> entre todas las clases de tests
/// marcadas con <c>[Collection("WebIntegration")]</c>. Reemplaza los 16
/// <c>IClassFixture&lt;TModuleFixture&gt;</c> actuales (Cargo 5 + Puesto 5 +
/// Habilidad 5 + WebShellSmokeTests).
/// </summary>
[CollectionDefinition("WebIntegration")]
public sealed class WebIntegrationCollection : ICollectionFixture<WebIntegrationFixture>
{
}