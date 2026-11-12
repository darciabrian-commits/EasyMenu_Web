using EasyMenu.Web.Filters;
using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EasyMenu.Web.Controllers;

[RequireRole("COCINA", "ADMINISTRADOR")]
public sealed class CocinaController(EasyMenuApiClient apiClient) : Controller
{
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Tablero(CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        var estados = new[] { "RECIBIDO", "EN_PREPARACION", "LISTO" };
        var solicitudes = estados
            .Select(estado => CargarPedidosAsync(estado, token, cancellationToken))
            .ToArray();
        var resultados = await Task.WhenAll(solicitudes);

        if (resultados.Any(resultado => resultado.SinAutorizacion))
        {
            return RedirectToAction(
                "Login",
                "Account",
                new { aviso = "La sesión venció o no tienes permiso. Inicia sesión nuevamente." });
        }

        var modelo = new TableroCocinaViewModel
        {
            Recibidos = resultados[0].Pedidos,
            EnPreparacion = resultados[1].Pedidos,
            Listos = resultados[2].Pedidos,
            Mensaje = string.Join(" ", resultados
                .Select(resultado => resultado.Mensaje)
                .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje)))
        };

        return PartialView("_Tablero", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        string id,
        string estadoActual,
        string estado,
        CancellationToken cancellationToken)
    {
        var siguienteEstado = estadoActual switch
        {
            "RECIBIDO" => "EN_PREPARACION",
            "EN_PREPARACION" => "LISTO",
            "LISTO" => "ENTREGADO",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(id) || siguienteEstado != estado)
        {
            return BadRequest(new { mensaje = "No se pudo validar el cambio de estado del pedido." });
        }

        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            using var response = await apiClient.CambiarEstadoPedidoAsync(
                id,
                estado,
                token,
                cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
            {
                return RedirectToAction(
                    "Login",
                    "Account",
                    new { aviso = "La sesión venció o no tienes permiso. Inicia sesión nuevamente." });
            }

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new
                {
                    mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            return Json(new { exito = true });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(503, new { mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente." });
        }
    }

    private async Task<(List<PedidoCocinaViewModel> Pedidos, string? Mensaje, bool SinAutorizacion)> CargarPedidosAsync(
        string estado,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await apiClient.ObtenerPedidosPorEstadoAsync(
                estado,
                token,
                cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
            {
                return ([], null, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ([], await LeerMensajeErrorAsync(response, cancellationToken), false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!IntentarObtenerPedidos(documento.RootElement, out var pedidos))
            {
                return ([], "La respuesta de pedidos no tiene un formato válido.", false);
            }

            return (pedidos.Select(ConvertirPedido).ToList(), null, false);
        }
        catch (HttpRequestException)
        {
            return ([], "No fue posible conectar con el servidor. Inténtalo nuevamente.", false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ([], "El servidor tardó demasiado en responder. Inténtalo nuevamente.", false);
        }
        catch (JsonException)
        {
            return ([], "La respuesta de pedidos no tiene un formato válido.", false);
        }
    }

    private static async Task<string> LeerMensajeErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var documento = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return ObtenerTexto(documento.RootElement, "mensaje")
                ?? "No fue posible completar la operación.";
        }
        catch (JsonException)
        {
            return "No fue posible completar la operación.";
        }
    }

    private static bool IntentarObtenerPedidos(JsonElement elemento, out List<JsonElement> pedidos)
    {
        if (elemento.ValueKind == JsonValueKind.Array)
        {
            pedidos = elemento.EnumerateArray().ToList();
            return true;
        }

        if (elemento.ValueKind == JsonValueKind.Object)
        {
            foreach (var nombre in new[] { "pedidos", "content", "data", "results", "resultado" })
            {
                if (ObtenerPropiedad(elemento, nombre, out var contenido)
                    && IntentarObtenerPedidos(contenido, out pedidos))
                {
                    return true;
                }
            }

            if (ObtenerPropiedad(elemento, "id", out _))
            {
                pedidos = [elemento];
                return true;
            }
        }

        pedidos = [];
        return false;
    }

    private static PedidoCocinaViewModel ConvertirPedido(JsonElement pedido)
    {
        var cliente = ObtenerTexto(pedido, "clienteNombre", "nombreCliente", "cliente");
        var mesa = ObtenerTexto(pedido, "mesaNombre", "numeroMesa", "mesa");
        var productos = ObtenerPropiedad(pedido, "productos", out var listaProductos)
            || ObtenerPropiedad(pedido, "detalles", out listaProductos)
            || ObtenerPropiedad(pedido, "items", out listaProductos)
            ? ObtenerElementos(listaProductos).Select(ConvertirProducto).ToList()
            : [];

        return new PedidoCocinaViewModel
        {
            Id = ObtenerTexto(pedido, "id") ?? string.Empty,
            CodigoCorto = ObtenerTexto(pedido, "codigoCorto", "codigo", "codigoPedido") ?? "Pedido",
            ClienteOMesa = string.Join(" · ", new[]
            {
                string.IsNullOrWhiteSpace(cliente) ? null : $"Cliente: {cliente}",
                string.IsNullOrWhiteSpace(mesa) ? null : $"Mesa: {mesa}"
            }.Where(valor => valor is not null)),
            Productos = productos
        };
    }

    private static ProductoPedidoViewModel ConvertirProducto(JsonElement producto)
    {
        var elementoProducto = producto;
        var nombre = ObtenerTexto(producto, "nombreProducto", "productoNombre", "nombre");
        if (ObtenerPropiedad(producto, "producto", out var datosProducto))
        {
            nombre ??= datosProducto.ValueKind == JsonValueKind.String
                ? datosProducto.GetString()
                : ObtenerTexto(datosProducto, "nombreProducto", "productoNombre", "nombre");
        }

        return new ProductoPedidoViewModel
        {
            Nombre = nombre ?? "Producto",
            Cantidad = ObtenerTexto(elementoProducto, "cantidad", "quantity") ?? "1",
            Notas = ObtenerTexto(elementoProducto, "notas", "nota", "observaciones", "comentario")
        };
    }

    private static IEnumerable<JsonElement> ObtenerElementos(JsonElement elemento)
    {
        return elemento.ValueKind == JsonValueKind.Array
            ? elemento.EnumerateArray().ToArray()
            : [];
    }

    private static string? ObtenerTexto(JsonElement elemento, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            if (!ObtenerPropiedad(elemento, nombre, out var valor))
            {
                continue;
            }

            if (valor.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                return valor.ToString();
            }

            if (valor.ValueKind == JsonValueKind.Object)
            {
                var textoAnidado = ObtenerTexto(valor, "nombre", "numero", "nombreCompleto");
                if (!string.IsNullOrWhiteSpace(textoAnidado))
                {
                    return textoAnidado;
                }
            }
        }

        return null;
    }

    private static bool ObtenerPropiedad(JsonElement elemento, string nombre, out JsonElement valor)
    {
        if (elemento.ValueKind == JsonValueKind.Object)
        {
            foreach (var propiedad in elemento.EnumerateObject())
            {
                if (string.Equals(propiedad.Name, nombre, StringComparison.OrdinalIgnoreCase))
                {
                    valor = propiedad.Value;
                    return true;
                }
            }
        }

        valor = default;
        return false;
    }
}
