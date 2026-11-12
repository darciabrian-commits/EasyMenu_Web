using System.Net.Http.Json;

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
}

public sealed class LoginResponse
{
    public string? Token { get; set; }
    public string? Tipo { get; set; }
    public string? Rol { get; set; }
}
