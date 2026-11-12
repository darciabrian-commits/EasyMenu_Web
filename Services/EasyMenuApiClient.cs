using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace EasyMenu.Web.Services;

public sealed class EasyMenuApiClient(HttpClient httpClient)
{
    public async Task<LoginResponse?> IniciarSesionAsync(string correo, string clave, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "auth/login",
            new { correo, clave },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
    }

    public async Task<HttpResponseMessage> ObtenerPedidosPorEstadoAsync(
        string estado,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"pedidos/estado/{Uri.EscapeDataString(estado)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> ObtenerPedidoPorCodigoAsync(
        string codigoCorto,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"pedidos/codigo/{Uri.EscapeDataString(codigoCorto)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> CobrarPedidoAsync(
        string pedidoId,
        string metodoPago,
        decimal? montoRecibido,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "pagos/cobrar")
        {
            Content = JsonContent.Create(new { pedidoId, metodoPago, montoRecibido })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> ObtenerPagoPorPedidoAsync(
        string pedidoId,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"pagos/pedido/{Uri.EscapeDataString(pedidoId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> ReembolsarPagoAsync(
        string pagoId,
        string motivo,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"pagos/{Uri.EscapeDataString(pagoId)}/reembolsar?motivo={Uri.EscapeDataString(motivo)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> CambiarEstadoPedidoAsync(
        string id,
        string estado,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"pedidos/{Uri.EscapeDataString(id)}/estado?estado={Uri.EscapeDataString(estado)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> ObtenerProductosAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "productos");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> ObtenerProductoAsync(
        string id,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"productos/{Uri.EscapeDataString(id)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> CrearProductoAsync(
        string nombre,
        string descripcion,
        decimal precio,
        string categoria,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "productos")
        {
            Content = JsonContent.Create(new { nombre, descripcion, precio, categoria })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> ActualizarProductoAsync(
        string id,
        string nombre,
        string descripcion,
        decimal precio,
        string categoria,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"productos/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(new { nombre, descripcion, precio, categoria })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> CambiarDisponibilidadProductoAsync(
        string id,
        bool disponible,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"productos/{Uri.EscapeDataString(id)}/disponibilidad?disponible={disponible.ToString().ToLowerInvariant()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> EliminarProductoAsync(
        string id,
        string token,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"productos/{Uri.EscapeDataString(id)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }
}

public sealed class LoginResponse
{
    public string? Token { get; set; }
    public string? Tipo { get; set; }
    public string? Rol { get; set; }
}
