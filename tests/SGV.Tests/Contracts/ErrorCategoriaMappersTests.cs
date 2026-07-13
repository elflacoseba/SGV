using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Contracts;

/// <summary>
/// Aprobación de contrato para <see cref="ErrorCategoriaMappers"/>.
///
/// Estos tests verifican el mapeo round-trip entre cada enum <c>*ErrorType</c>
/// vigente y la nueva taxonomía <see cref="ErrorCategoria"/>. La conversión
/// es <b>nombre-a-nombre</b> (no por ordinal) y exhaustiva: cada valor del
/// enum origen debe mapear al <see cref="ErrorCategoria"/> con el mismo
/// significado semántico cuando existe equivalente, y lanzar
/// <see cref="NotSupportedException"/> cuando no hay equivalente (p.ej.
/// <c>HabilidadErrorType</c> no tiene variantes <c>Unauthorized</c>,
/// <c>Forbidden</c> ni <c>Unexpected</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Regression explícito.</b> <c>CargoSkillErrorType.Validation</c>
/// tiene ordinal 1 mientras que <c>ErrorCategoria.Validation</c> tiene
/// ordinal 2 (con <c>Conflict</c> en 1). El test
/// <c>CargoSkillErrorType_Validation_MapsToCategoriaValidation_NotConflict</c>
/// blinda que el mapeo por nombre NO degrada a ordinal.
/// </para>
/// </remarks>
public sealed class ErrorCategoriaMappersTests
{
    // ============================================================
    // HabilidadErrorType (4 valores: NotFound, Conflict, Validation, Infrastructure)
    // ============================================================

    [Theory]
    [InlineData(HabilidadErrorType.NotFound, ErrorCategoria.NotFound)]
    [InlineData(HabilidadErrorType.Conflict, ErrorCategoria.Conflict)]
    [InlineData(HabilidadErrorType.Validation, ErrorCategoria.Validation)]
    [InlineData(HabilidadErrorType.Infrastructure, ErrorCategoria.Transport)]
    public void ToCategoria_HabilidadErrorType_MapsToExpectedCategoria(
        HabilidadErrorType source,
        ErrorCategoria expected)
    {
        var actual = ErrorCategoriaMappers.ToCategoria(source);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(ErrorCategoria.NotFound, HabilidadErrorType.NotFound)]
    [InlineData(ErrorCategoria.Conflict, HabilidadErrorType.Conflict)]
    [InlineData(ErrorCategoria.Validation, HabilidadErrorType.Validation)]
    [InlineData(ErrorCategoria.Transport, HabilidadErrorType.Infrastructure)]
    public void ToTipo_HabilidadErrorType_RoundTripPreservesSemanticName(
        ErrorCategoria categoria,
        HabilidadErrorType expected)
    {
        var actual = ErrorCategoriaMappers.ToTipoHabilidad(categoria);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(ErrorCategoria.Unauthorized)]
    [InlineData(ErrorCategoria.Forbidden)]
    [InlineData(ErrorCategoria.Unexpected)]
    public void ToTipo_HabilidadErrorType_NoEquivalente_ThrowsNotSupported(ErrorCategoria unsupported)
    {
        Assert.Throws<NotSupportedException>(() => ErrorCategoriaMappers.ToTipoHabilidad(unsupported));
    }

    [Fact]
    public void ToCategoria_HabilidadErrorType_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorCategoriaMappers.ToCategoria((HabilidadErrorType)9999));
    }

    // ============================================================
    // CargoErrorType (3 valores: NotFound, Conflict, Validation)
    // ============================================================

    [Theory]
    [InlineData(CargoErrorType.NotFound, ErrorCategoria.NotFound)]
    [InlineData(CargoErrorType.Conflict, ErrorCategoria.Conflict)]
    [InlineData(CargoErrorType.Validation, ErrorCategoria.Validation)]
    public void ToCategoria_CargoErrorType_MapsToExpectedCategoria(
        CargoErrorType source,
        ErrorCategoria expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToCategoria(source));
    }

    [Theory]
    [InlineData(ErrorCategoria.NotFound, CargoErrorType.NotFound)]
    [InlineData(ErrorCategoria.Conflict, CargoErrorType.Conflict)]
    [InlineData(ErrorCategoria.Validation, CargoErrorType.Validation)]
    [InlineData(ErrorCategoria.Transport, CargoErrorType.Validation)]
    [InlineData(ErrorCategoria.Unexpected, CargoErrorType.Validation)]
    public void ToTipo_CargoErrorType_RoundTripAndFallbackToValidation(
        ErrorCategoria categoria,
        CargoErrorType expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToTipoCargo(categoria));
    }

    [Theory]
    [InlineData(ErrorCategoria.Unauthorized)]
    [InlineData(ErrorCategoria.Forbidden)]
    public void ToTipo_CargoErrorType_NoEquivalente_ThrowsNotSupported(ErrorCategoria unsupported)
    {
        Assert.Throws<NotSupportedException>(() => ErrorCategoriaMappers.ToTipoCargo(unsupported));
    }

    [Fact]
    public void ToCategoria_CargoErrorType_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorCategoriaMappers.ToCategoria((CargoErrorType)9999));
    }

    // ============================================================
    // PuestoErrorType (3 valores: NotFound, Conflict, Validation)
    // ============================================================

    [Theory]
    [InlineData(PuestoErrorType.NotFound, ErrorCategoria.NotFound)]
    [InlineData(PuestoErrorType.Conflict, ErrorCategoria.Conflict)]
    [InlineData(PuestoErrorType.Validation, ErrorCategoria.Validation)]
    public void ToCategoria_PuestoErrorType_MapsToExpectedCategoria(
        PuestoErrorType source,
        ErrorCategoria expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToCategoria(source));
    }

    [Theory]
    [InlineData(ErrorCategoria.NotFound, PuestoErrorType.NotFound)]
    [InlineData(ErrorCategoria.Conflict, PuestoErrorType.Conflict)]
    [InlineData(ErrorCategoria.Validation, PuestoErrorType.Validation)]
    [InlineData(ErrorCategoria.Transport, PuestoErrorType.Validation)]
    [InlineData(ErrorCategoria.Unexpected, PuestoErrorType.Validation)]
    public void ToTipo_PuestoErrorType_RoundTripAndFallbackToValidation(
        ErrorCategoria categoria,
        PuestoErrorType expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToTipoPuesto(categoria));
    }

    [Theory]
    [InlineData(ErrorCategoria.Unauthorized)]
    [InlineData(ErrorCategoria.Forbidden)]
    public void ToTipo_PuestoErrorType_NoEquivalente_ThrowsNotSupported(ErrorCategoria unsupported)
    {
        Assert.Throws<NotSupportedException>(() => ErrorCategoriaMappers.ToTipoPuesto(unsupported));
    }

    [Fact]
    public void ToCategoria_PuestoErrorType_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorCategoriaMappers.ToCategoria((PuestoErrorType)9999));
    }

    // ============================================================
    // UnidadOrganizativaErrorType (3 valores: NotFound, Conflict, Validation)
    // ============================================================

    [Theory]
    [InlineData(UnidadOrganizativaErrorType.NotFound, ErrorCategoria.NotFound)]
    [InlineData(UnidadOrganizativaErrorType.Conflict, ErrorCategoria.Conflict)]
    [InlineData(UnidadOrganizativaErrorType.Validation, ErrorCategoria.Validation)]
    public void ToCategoria_UnidadOrganizativaErrorType_MapsToExpectedCategoria(
        UnidadOrganizativaErrorType source,
        ErrorCategoria expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToCategoria(source));
    }

    [Theory]
    [InlineData(ErrorCategoria.NotFound, UnidadOrganizativaErrorType.NotFound)]
    [InlineData(ErrorCategoria.Conflict, UnidadOrganizativaErrorType.Conflict)]
    [InlineData(ErrorCategoria.Validation, UnidadOrganizativaErrorType.Validation)]
    [InlineData(ErrorCategoria.Transport, UnidadOrganizativaErrorType.Validation)]
    [InlineData(ErrorCategoria.Unexpected, UnidadOrganizativaErrorType.Validation)]
    public void ToTipo_UnidadOrganizativaErrorType_RoundTripAndFallbackToValidation(
        ErrorCategoria categoria,
        UnidadOrganizativaErrorType expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToTipoUnidadOrganizativa(categoria));
    }

    [Theory]
    [InlineData(ErrorCategoria.Unauthorized)]
    [InlineData(ErrorCategoria.Forbidden)]
    public void ToTipo_UnidadOrganizativaErrorType_NoEquivalente_ThrowsNotSupported(ErrorCategoria unsupported)
    {
        Assert.Throws<NotSupportedException>(() => ErrorCategoriaMappers.ToTipoUnidadOrganizativa(unsupported));
    }

    [Fact]
    public void ToCategoria_UnidadOrganizativaErrorType_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorCategoriaMappers.ToCategoria((UnidadOrganizativaErrorType)9999));
    }

    // ============================================================
    // CargoSkillErrorType (6 valores: NotFound, Validation, Conflict, Unauthorized, Forbidden, Transport)
    // ============================================================

    [Theory]
    [InlineData(CargoSkillErrorType.NotFound, ErrorCategoria.NotFound)]
    [InlineData(CargoSkillErrorType.Validation, ErrorCategoria.Validation)]
    [InlineData(CargoSkillErrorType.Conflict, ErrorCategoria.Conflict)]
    [InlineData(CargoSkillErrorType.Unauthorized, ErrorCategoria.Unauthorized)]
    [InlineData(CargoSkillErrorType.Forbidden, ErrorCategoria.Forbidden)]
    [InlineData(CargoSkillErrorType.Transport, ErrorCategoria.Transport)]
    public void ToCategoria_CargoSkillErrorType_MapsToExpectedCategoria(
        CargoSkillErrorType source,
        ErrorCategoria expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToCategoria(source));
    }

    [Theory]
    [InlineData(ErrorCategoria.NotFound, CargoSkillErrorType.NotFound)]
    [InlineData(ErrorCategoria.Conflict, CargoSkillErrorType.Conflict)]
    [InlineData(ErrorCategoria.Validation, CargoSkillErrorType.Validation)]
    [InlineData(ErrorCategoria.Unauthorized, CargoSkillErrorType.Unauthorized)]
    [InlineData(ErrorCategoria.Forbidden, CargoSkillErrorType.Forbidden)]
    [InlineData(ErrorCategoria.Transport, CargoSkillErrorType.Transport)]
    public void ToTipo_CargoSkillErrorType_RoundTripPreservesSemanticName(
        ErrorCategoria categoria,
        CargoSkillErrorType expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToTipoCargoSkill(categoria));
    }

    /// <summary>
    /// Regression explícito: ordinal <c>CargoSkillErrorType.Validation = 1</c>
    /// pero <c>ErrorCategoria.Validation = 2</c> (con <c>Conflict = 1</c>).
    /// El mapeo debe ir por nombre, no por ordinal.
    /// </summary>
    [Fact]
    public void CargoSkillErrorType_Validation_MapsToCategoriaValidation_NotConflict()
    {
        var categoria = ErrorCategoriaMappers.ToCategoria(CargoSkillErrorType.Validation);

        Assert.Equal(ErrorCategoria.Validation, categoria);
        Assert.NotEqual(ErrorCategoria.Conflict, categoria);
    }

    [Fact]
    public void CargoSkillErrorType_Conflict_MapsToCategoriaConflict_NotValidation()
    {
        var categoria = ErrorCategoriaMappers.ToCategoria(CargoSkillErrorType.Conflict);

        Assert.Equal(ErrorCategoria.Conflict, categoria);
        Assert.NotEqual(ErrorCategoria.Validation, categoria);
    }

    [Fact]
    public void ToTipo_CargoSkillErrorType_Unexpected_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(
            () => ErrorCategoriaMappers.ToTipoCargoSkill(ErrorCategoria.Unexpected));
    }

    [Fact]
    public void ToCategoria_CargoSkillErrorType_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorCategoriaMappers.ToCategoria((CargoSkillErrorType)9999));
    }

    // ============================================================
    // UsuarioErrorType (4 valores: NotFound, Conflict, Validation, Unauthorized)
    // ============================================================

    [Theory]
    [InlineData(UsuarioErrorType.NotFound, ErrorCategoria.NotFound)]
    [InlineData(UsuarioErrorType.Conflict, ErrorCategoria.Conflict)]
    [InlineData(UsuarioErrorType.Validation, ErrorCategoria.Validation)]
    [InlineData(UsuarioErrorType.Unauthorized, ErrorCategoria.Unauthorized)]
    public void ToCategoria_UsuarioErrorType_MapsToExpectedCategoria(
        UsuarioErrorType source,
        ErrorCategoria expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToCategoria(source));
    }

    [Theory]
    [InlineData(ErrorCategoria.NotFound, UsuarioErrorType.NotFound)]
    [InlineData(ErrorCategoria.Conflict, UsuarioErrorType.Conflict)]
    [InlineData(ErrorCategoria.Validation, UsuarioErrorType.Validation)]
    [InlineData(ErrorCategoria.Unauthorized, UsuarioErrorType.Unauthorized)]
    [InlineData(ErrorCategoria.Transport, UsuarioErrorType.Validation)]
    [InlineData(ErrorCategoria.Unexpected, UsuarioErrorType.Validation)]
    public void ToTipo_UsuarioErrorType_RoundTripAndFallbackToValidation(
        ErrorCategoria categoria,
        UsuarioErrorType expected)
    {
        Assert.Equal(expected, ErrorCategoriaMappers.ToTipoUsuario(categoria));
    }

    [Fact]
    public void ToTipo_UsuarioErrorType_Forbidden_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(
            () => ErrorCategoriaMappers.ToTipoUsuario(ErrorCategoria.Forbidden));
    }

    [Fact]
    public void ToCategoria_UsuarioErrorType_UndefinedValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorCategoriaMappers.ToCategoria((UsuarioErrorType)9999));
    }
}
