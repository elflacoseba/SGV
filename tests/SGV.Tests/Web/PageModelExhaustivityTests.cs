using SGV.Contracts.Comun;
using SGV.Web.Pages.Common;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Smoke test: <see cref="ErrorCategoryMapper.Map"/> must produce a non-empty
/// message for every <see cref="ErrorCategoria"/> variant. The centralized
/// switch replaced the 14 copy-pasted <c>MapCategoriaToMessage</c> methods.
/// </summary>
public sealed class PageModelExhaustivityTests
{
    [Fact]
    public void ErrorCategoryMapper_CoversAllCategorias()
    {
        foreach (var categoria in Enum.GetValues<ErrorCategoria>())
        {
            var message = ErrorCategoryMapper.Map(categoria);
            Assert.False(
                string.IsNullOrWhiteSpace(message),
                $"Categoria.{categoria} no produce mensaje (el switch no cubre la variante).");
        }
    }

    [Fact]
    public void ErrorCategoryMapper_CustomMessages_CoversAllCategorias()
    {
        foreach (var categoria in Enum.GetValues<ErrorCategoria>())
        {
            var message = ErrorCategoryMapper.Map(
                categoria,
                notFoundMessage: "Test NotFound.",
                conflictMessage: "Test Conflict.",
                validationMessage: "Test Validation.");
            Assert.False(
                string.IsNullOrWhiteSpace(message),
                $"Categoria.{categoria} con mensajes custom no produce mensaje.");
        }
    }
}
