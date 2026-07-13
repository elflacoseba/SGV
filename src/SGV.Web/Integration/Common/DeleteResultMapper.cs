using System.Net;
using SGV.Contracts.Comun;

namespace SGV.Web.Integration.Common;

/// <summary>
/// Centralizes the translation of HTTP delete responses into the canonical
/// metadata consumed by the domain-specific <c>*DeleteResult</c> records.
/// </summary>
internal static class DeleteResultMapper
{
    /// <summary>
    /// Builds delete-result metadata while preserving the response status and
    /// delegating non-success classification to <see cref="CommandResultMapper"/>.
    /// </summary>
    public static async Task<Result> BuildDeleteResultAsync(
        HttpResponseMessage response,
        HttpStatusCode successStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.StatusCode == successStatus)
        {
            return new Result(
                Succeeded: true,
                Categoria: default,
                StatusCode: response.StatusCode,
                Code: null,
                Message: null);
        }

        var problem = await ApiProblemReader
            .ReadAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var (categoria, code, message, _) = CommandResultMapper.Map(response, problem);

        return new Result(
            Succeeded: false,
            Categoria: categoria,
            StatusCode: response.StatusCode,
            Code: code,
            Message: message);
    }

    /// <summary>
    /// Canonical delete-result metadata shared by typed HTTP clients.
    /// </summary>
    internal sealed record Result(
        bool Succeeded,
        ErrorCategoria Categoria,
        HttpStatusCode? StatusCode,
        string? Code,
        string? Message);
}
