using System.Net;
using System.Net.Http.Json;
using System.Text;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Contracts.Vacantes.Enums;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// HTTP implementation of <see cref="IVacanteApiClient"/>.
/// </summary>
public sealed class VacanteApiClient(HttpClient httpClient) : IVacanteApiClient
{
    private const string BaseRoute = "/" + VacanteApiRoutes.Base;
    private const string EstadosRoute = "/" + VacanteApiRoutes.EstadosVacanteBase;
    private const string PuestosRoute = "/" + VacanteApiRoutes.PuestosBase;
    private const string PuestosDisponiblesRoute = "/" + VacanteApiRoutes.PuestosDisponiblesBase;

    /// <inheritdoc />
    public async Task<PagedResult<VacanteDto>> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .GetAsync(BuildQueryUri(query), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PagedResult<VacanteDto>>(cancellationToken)
            .ConfigureAwait(false)
            ?? new PagedResult<VacanteDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<VacanteDetailDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .GetAsync($"{BaseRoute}/{id:D}", cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<VacanteDetailDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EstadoVacanteDto>> ListarEstadosAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .GetAsync(EstadosRoute, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<EstadoVacanteDto>>(cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PuestoDto>> ListarPuestosAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .GetAsync(PuestosRoute, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<PuestoDto>>(cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PuestoDto>> ListarPuestosDisponiblesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .GetAsync(PuestosDisponiblesRoute, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<PuestoDto>>(cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<VacanteCommandResult> CrearAsync(
        CrearVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .PostAsJsonAsync(BaseRoute, request, cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var detail = await response.Content
                .ReadFromJsonAsync<VacanteDetailDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return VacanteCommandResult.Success(detail!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<VacanteCommandResult> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .PatchAsync(
                $"{BaseRoute}/{id:D}/estado",
                JsonContent.Create(request),
                cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var detail = await response.Content
                .ReadFromJsonAsync<VacanteDetailDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return VacanteCommandResult.Success(detail!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildQueryUri(VacanteListQuery query)
    {
        var builder = new StringBuilder(
            $"{BaseRoute}?page={Math.Max(1, query.Page)}&pageSize={Math.Max(1, query.PageSize)}");
        builder.Append("&status=");
        builder.Append(Uri.EscapeDataString(ToStatus(query.Segmento)));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.Sort))
        {
            builder.Append("&sort=");
            builder.Append(Uri.EscapeDataString(query.Sort));
        }

        if (query.PuestoId.HasValue)
        {
            builder.Append("&puestoId=");
            builder.Append(query.PuestoId.Value.ToString("D"));
        }

        return builder.ToString();
    }

    private static string ToStatus(VacanteSegmentoListado segmento) => segmento switch
    {
        VacanteSegmentoListado.Cerradas => VacanteApiRoutes.StatusCerradas,
        VacanteSegmentoListado.Todas => VacanteApiRoutes.StatusTodas,
        _ => VacanteApiRoutes.StatusAbiertas
    };

    private static async Task<VacanteCommandResult> ToCommandResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader
            .ReadAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var (categoria, code, message, _) = CommandResultMapper.Map(response, parsed);
        var error = new VacanteError(categoria, code, message);

        return parsed.FieldErrors is { Count: > 0 }
            ? VacanteCommandResult.Failure(error, parsed.FieldErrors)
            : VacanteCommandResult.Failure(error);
    }

    /// <inheritdoc />
    public async Task<bool> ExisteVacanteAbiertaParaPuestoAsync(
        Guid puestoId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Usa el segmento "abiertas" (default) que el backend
            // resuelve contra `EstadoVacante.EsTerminal` (no contra
            // `FechaCierre`), fidelidad con el dominio. TotalCount > 0
            // basta como señal sin necesidad de paginar las filas.
            var query = new VacanteListQuery(
                Page: 1,
                PageSize: 1,
                Search: null,
                Sort: null,
                Segmento: VacanteSegmentoListado.Abiertas,
                PuestoId: puestoId);

            var response = await ListarAsync(query, cancellationToken).ConfigureAwait(false);
            return response.TotalCount > 0;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Política de degradación unificada (T-7.1 / T-7.2 — PR #259
            // review H-5): si la API no responde, degradamos a "no hay
            // vacante abierta" para mostrar el botón NAV-007 en lugar
            // de ocultarlo silenciosamente. Es preferible que el usuario
            // descubra que el camino no aplica a que el botón desaparezca
            // y deje al usuario sin clara acción de salida.
            _ = ex;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<VacanteDto?> ObtenerAbiertaPorPuestoAsync(
        Guid puestoId,
        CancellationToken cancellationToken = default)
    {
        // Q-T2 cerrado: reutiliza el listado segmentado "abiertas" filtrado
        // por PuestoId. Toma la primera fila (el backend garantiza a lo sumo
        // una Vacante abierta por Puesto vía el índice unique vigente;
        // ActivePuestoIdUnique de Vacantes). Devuelve null si la lista
        // está vacía o si el transporte falla (defensivo, alineado con
        // la política de ExisteVacanteAbiertaParaPuestoAsync).
        try
        {
            var query = new VacanteListQuery(
                Page: 1,
                PageSize: 1,
                Search: null,
                Sort: null,
                Segmento: VacanteSegmentoListado.Abiertas,
                PuestoId: puestoId);

            var response = await ListarAsync(query, cancellationToken).ConfigureAwait(false);
            return response.Items.FirstOrDefault();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            _ = ex;
            return null;
        }
    }
}
