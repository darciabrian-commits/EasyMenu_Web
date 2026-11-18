using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EasyMenu.Web.Models;

namespace EasyMenu.Web.Services;

public enum ReservasApiFailure
{
    None,
    Configuration,
    Connection,
    Timeout,
    InvalidJson
}

public sealed class ReservasApiResult<T>
{
    internal ReservasApiResult(
        HttpStatusCode? statusCode,
        T? value,
        string? mensaje,
        ReservasApiFailure failure)
    {
        StatusCode = statusCode;
        Value = value;
        Mensaje = mensaje;
        Failure = failure;
    }

    public HttpStatusCode? StatusCode { get; }
    public T? Value { get; }
    public string? Mensaje { get; }
    public ReservasApiFailure Failure { get; }
    public bool IsSuccess => Failure == ReservasApiFailure.None
        && StatusCode is HttpStatusCode.OK or HttpStatusCode.Created
        && Value is not null;
}

public sealed class ReservasApiClient
{
    private const string EncabezadoClaveServicio = "X-Reservas-Service-Key";
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly ReservasApiOptions options;

    public ReservasApiClient(HttpClient httpClient, ReservasApiOptions options)
    {
        this.httpClient = httpClient;
        this.options = options;

        if (options.IsConfigured)
        {
            this.httpClient.BaseAddress = options.BaseAddress;
            this.httpClient.Timeout = TimeSpan.FromSeconds(90);
            this.httpClient.DefaultRequestHeaders.Add(
                EncabezadoClaveServicio,
                options.ServiceApiKey);
        }
    }

    public Task<ReservasApiResult<List<MesaResponse>>> ObtenerMesasAsync(
        CancellationToken cancellationToken = default) =>
        EnviarAsync<List<MesaResponse>>(HttpMethod.Get, "api/Mesas", null, cancellationToken);

    public Task<ReservasApiResult<MesaResponse>> ObtenerMesaAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<MesaResponse>(HttpMethod.Get, $"api/Mesas/{id}", null, cancellationToken);

    public Task<ReservasApiResult<MesaResponse>> CrearMesaAsync(
        MesaCreate mesa,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<MesaResponse>(HttpMethod.Post, "api/Mesas", mesa, cancellationToken);

    public Task<ReservasApiResult<MesaResponse>> ActualizarMesaAsync(
        int id,
        MesaUpdate mesa,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<MesaResponse>(HttpMethod.Put, $"api/Mesas/{id}", mesa, cancellationToken);

    public Task<ReservasApiResult<List<ReservaResponse>>> ObtenerReservasAsync(
        CancellationToken cancellationToken = default) =>
        EnviarAsync<List<ReservaResponse>>(HttpMethod.Get, "api/Reservas", null, cancellationToken);

    public Task<ReservasApiResult<ReservaResponse>> ObtenerReservaAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<ReservaResponse>(HttpMethod.Get, $"api/Reservas/{id}", null, cancellationToken);

    public Task<ReservasApiResult<List<ReservaResponse>>> ObtenerReservasPorFechaAsync(
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        var fechaFormateada = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return EnviarAsync<List<ReservaResponse>>(
            HttpMethod.Get,
            $"api/Reservas/fecha/{Uri.EscapeDataString(fechaFormateada)}",
            null,
            cancellationToken);
    }

    public Task<ReservasApiResult<ReservaResponse>> CrearReservaAsync(
        ReservaCreate reserva,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<ReservaResponse>(HttpMethod.Post, "api/Reservas", reserva, cancellationToken);

    public Task<ReservasApiResult<ReservaResponse>> ActualizarReservaAsync(
        int id,
        ReservaUpdate reserva,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<ReservaResponse>(HttpMethod.Put, $"api/Reservas/{id}", reserva, cancellationToken);

    public Task<ReservasApiResult<ReservaResponse>> CambiarEstadoReservaAsync(
        int id,
        ReservaEstado estado,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<ReservaResponse>(
            HttpMethod.Patch,
            $"api/Reservas/{id}/estado",
            estado,
            cancellationToken);

    private async Task<ReservasApiResult<T>> EnviarAsync<T>(
        HttpMethod metodo,
        string ruta,
        object? cuerpo,
        CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            return new ReservasApiResult<T>(
                null,
                default,
                "La integración con Reservas no está configurada.",
                ReservasApiFailure.Configuration);
        }

        using var request = new HttpRequestMessage(metodo, ruta);
        if (cuerpo is not null)
        {
            request.Content = JsonContent.Create(cuerpo, options: OpcionesJson);
        }

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created))
            {
                var (mensaje, jsonInvalido) = await LeerMensajeErrorAsync(response, cancellationToken);
                return new ReservasApiResult<T>(
                    response.StatusCode,
                    default,
                    mensaje,
                    jsonInvalido ? ReservasApiFailure.InvalidJson : ReservasApiFailure.None);
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(OpcionesJson, cancellationToken);
                return value is null
                    ? new ReservasApiResult<T>(
                        response.StatusCode,
                        default,
                        "Reservas devolvió una respuesta vacía.",
                        ReservasApiFailure.InvalidJson)
                    : new ReservasApiResult<T>(
                        response.StatusCode,
                        value,
                        null,
                        ReservasApiFailure.None);
            }
            catch (JsonException)
            {
                return new ReservasApiResult<T>(
                    response.StatusCode,
                    default,
                    "Reservas devolvió una respuesta con formato inválido.",
                    ReservasApiFailure.InvalidJson);
            }
        }
        catch (HttpRequestException)
        {
            return new ReservasApiResult<T>(
                null,
                default,
                "No fue posible conectar con el servicio de Reservas.",
                ReservasApiFailure.Connection);
        }
        catch (IOException)
        {
            return new ReservasApiResult<T>(
                null,
                default,
                "No fue posible conectar con el servicio de Reservas.",
                ReservasApiFailure.Connection);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ReservasApiResult<T>(
                null,
                default,
                "El servicio de Reservas tardó demasiado en responder.",
                ReservasApiFailure.Timeout);
        }
    }

    private static async Task<(string Mensaje, bool JsonInvalido)> LeerMensajeErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var documento = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            if (documento.RootElement.ValueKind == JsonValueKind.Object
                && documento.RootElement.TryGetProperty("mensaje", out var propiedad)
                && propiedad.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(propiedad.GetString()))
            {
                return (propiedad.GetString()!, false);
            }

            return (MensajePredeterminado(response.StatusCode), false);
        }
        catch (JsonException)
        {
            return (MensajePredeterminado(response.StatusCode), true);
        }
    }

    private static string MensajePredeterminado(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "La solicitud enviada a Reservas no es válida.",
        HttpStatusCode.Unauthorized => "Falló la autenticación del servicio de Reservas.",
        HttpStatusCode.Forbidden => "Reservas rechazó la autorización del servicio.",
        HttpStatusCode.NotFound => "No se encontró el recurso solicitado en Reservas.",
        HttpStatusCode.Conflict => "La operación entra en conflicto con el estado actual.",
        _ when (int)statusCode >= 500 => "El servicio de Reservas no está disponible temporalmente.",
        _ => "No fue posible completar la operación en Reservas."
    };
}
