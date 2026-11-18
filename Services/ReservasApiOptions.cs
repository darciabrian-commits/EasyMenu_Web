using System.Text;

namespace EasyMenu.Web.Services;

public sealed class ReservasApiOptions
{
    private ReservasApiOptions(Uri? baseAddress, string? serviceApiKey)
    {
        BaseAddress = baseAddress;
        ServiceApiKey = serviceApiKey;
    }

    internal Uri? BaseAddress { get; }
    internal string? ServiceApiKey { get; }
    public bool IsConfigured => BaseAddress is not null && ServiceApiKey is not null;

    public static ReservasApiOptions Load(IConfiguration configuration, bool isDevelopment)
    {
        var baseUrl = configuration["ReservasApi:BaseUrl"];
        var serviceApiKey = configuration["ReservasApi:ServiceApiKey"];
        var baseUrlIsValid = Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAddress)
            && baseAddress.UserInfo.Length == 0
            && string.IsNullOrEmpty(baseAddress.Query)
            && string.IsNullOrEmpty(baseAddress.Fragment)
            && baseAddress.AbsolutePath == "/"
            && (baseAddress.Scheme == Uri.UriSchemeHttps
                || (isDevelopment
                    && baseAddress.Scheme == Uri.UriSchemeHttp
                    && baseAddress.IsLoopback));
        var keyIsValid = !string.IsNullOrWhiteSpace(serviceApiKey)
            && Encoding.UTF8.GetByteCount(serviceApiKey) >= 32
            && !serviceApiKey.Any(char.IsControl);

        if (!baseUrlIsValid || !keyIsValid)
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "La configuración obligatoria de ReservasApi no es válida.");
            }

            return new ReservasApiOptions(null, null);
        }

        return new ReservasApiOptions(baseAddress, serviceApiKey);
    }
}
