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
}

public sealed class LoginResponse
{
    public string? Token { get; set; }
    public string? Tipo { get; set; }
    public string? Rol { get; set; }
}
