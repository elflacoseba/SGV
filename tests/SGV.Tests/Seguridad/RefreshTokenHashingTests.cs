using System.Text.RegularExpressions;
using SGV.Aplicacion.Seguridad.Servicios;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// Behaviour lock-down for <see cref="RefreshTokenHashing.ComputeSha256Hex"/>.
/// PR1a (change <c>implementa-refresh-tokens</c>) introduces the
/// refresh-token rotation flow; the hashing helper MUST be deterministic
/// (so two reads of the same row match), pure (no I/O, easy to unit
/// test), and produce a 64-char lowercase hex string that fits a
/// <c>varchar(64)</c> column without truncation.
///
/// The five scenarios below mirror REQ-RTM-HASH-1 (spec block B) and
/// protect the wire width from accidental regressions (e.g. someone
/// switching to <c>Convert.ToBase64String</c>).
/// </summary>
public sealed class RefreshTokenHashingTests
{
    private static readonly Regex LowerHex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    [Fact]
    public void ComputeSha256Hex_TokenArbitrario_LongitudEsExactamente64Chars()
    {
        var hash = RefreshTokenHashing.ComputeSha256Hex("un-token-cualquiera");

        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void ComputeSha256Hex_MismoToken_DosLlamadasRetornanHashIdentico()
    {
        const string token = "token-determinista";

        var primerHash = RefreshTokenHashing.ComputeSha256Hex(token);
        var segundoHash = RefreshTokenHashing.ComputeSha256Hex(token);

        Assert.Equal(primerHash, segundoHash);
    }

    [Fact]
    public void ComputeSha256Hex_OutputEsHexLowercaseDe64Chars()
    {
        var hash = RefreshTokenHashing.ComputeSha256Hex("cualquier-token");

        Assert.Matches(LowerHex64, hash);
    }

    [Fact]
    public void ComputeSha256Hex_TokensDistintos_ProducenHashesDistintos()
    {
        var hashA = RefreshTokenHashing.ComputeSha256Hex("token-A");
        var hashB = RefreshTokenHashing.ComputeSha256Hex("token-B");

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ComputeSha256Hex_TokenConCaracteresNoAscii_HashEsDeterministaYValido()
    {
        const string tokenConTildes = "ñándú-ñ-validación-2026-áéíóú";

        var primerHash = RefreshTokenHashing.ComputeSha256Hex(tokenConTildes);
        var segundoHash = RefreshTokenHashing.ComputeSha256Hex(tokenConTildes);

        Assert.Equal(primerHash, segundoHash);
        Assert.Matches(LowerHex64, primerHash);
    }
}
