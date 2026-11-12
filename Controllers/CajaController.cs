using System.Globalization;
using System.Net;
using System.Text.Json;
using EasyMenu.Web.Filters;
using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Web.Controllers;

[RequireRole("CAJERO", "ADMINISTRADOR")]
public sealed class CajaController(EasyMenuApiClient apiClient) : Controller
{
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> PedidosPorCobrar(CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.ObtenerPedidosPorEstadoAsync(
                "PENDIENTE_PAGO",
                token,
                cancellationToken);

            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                return PartialView("_PedidosPorCobrar", new PedidosPorCobrarViewModel
                {
                    Mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!IntentarObtenerPedidos(documento.RootElement, out var pedidos))
            {
                return PartialView("_PedidosPorCobrar", new PedidosPorCobrarViewModel
                {
                    Mensaje = "La respuesta de pedidos no tiene un formato válido."
                });
            }

            return PartialView("_PedidosPorCobrar", new PedidosPorCobrarViewModel
            {
                Pedidos = pedidos.Select(ConvertirPedido).ToList()
            });
        }
        catch (HttpRequestException)
        {
            return PartialView("_PedidosPorCobrar", new PedidosPorCobrarViewModel
            {
                Mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PartialView("_PedidosPorCobrar", new PedidosPorCobrarViewModel
            {
                Mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente."
            });
        }
        catch (JsonException)
        {
            return PartialView("_PedidosPorCobrar", new PedidosPorCobrarViewModel
            {
                Mensaje = "La respuesta de pedidos no tiene un formato válido."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Pedido(string codigoCorto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoCorto))
        {
            return PartialView("_Pedido", new PedidoCajaViewModel
            {
                Mensaje = "Escribe el código corto del pedido."
            });
        }

        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.ObtenerPedidoPorCodigoAsync(
                codigoCorto.Trim(),
                token,
                cancellationToken);

            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                return PartialView("_Pedido", new PedidoCajaViewModel
                {
                    Mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var pedido = ConvertirPedido(documento.RootElement);
            var resultadoPago = await ObtenerPagoAsync(pedido.Id, token, cancellationToken);
            if (resultadoPago.SinAutorizacion)
            {
                return IrAlLogin();
            }

            return PartialView("_Pedido", new PedidoCajaViewModel
            {
                Id = pedido.Id,
                CodigoCorto = pedido.CodigoCorto,
                ClienteOMesa = pedido.ClienteOMesa,
                Estado = pedido.Estado,
                Total = pedido.Total,
                Productos = pedido.Productos,
                Pago = resultadoPago.Pago,
                MensajePago = resultadoPago.Mensaje
            });
        }
        catch (HttpRequestException)
        {
            return PartialView("_Pedido", new PedidoCajaViewModel
            {
                Mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PartialView("_Pedido", new PedidoCajaViewModel
            {
                Mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente."
            });
        }
        catch (JsonException)
        {
            return PartialView("_Pedido", new PedidoCajaViewModel
            {
                Mensaje = "La respuesta del pedido no tiene un formato válido."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cobrar(
        string pedidoId,
        string metodoPago,
        decimal? montoRecibido,
        decimal totalPedido,
        CancellationToken cancellationToken)
    {
        metodoPago = metodoPago?.ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pedidoId)
            || metodoPago is not ("EFECTIVO" or "TARJETA" or "TRANSFERENCIA")
            || (metodoPago == "EFECTIVO" && (!montoRecibido.HasValue || montoRecibido < totalPedido)))
        {
            return BadRequest(new { mensaje = "Revisa el método de pago y el monto recibido." });
        }

        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.CobrarPedidoAsync(
                pedidoId,
                metodoPago,
                metodoPago == "EFECTIVO" ? montoRecibido : null,
                token,
                cancellationToken);

            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new
                {
                    mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var datos = BuscarObjeto(documento.RootElement, "pago") ?? documento.RootElement;
            var totalRespuesta = ObtenerDecimal(datos, "total", "montoTotal", "totalPedido") ?? totalPedido;
            var montoRespuesta = ObtenerDecimal(datos, "montoRecibido") ?? montoRecibido;
            var cambio = ObtenerDecimal(datos, "cambio")
                ?? (metodoPago == "EFECTIVO" && montoRespuesta.HasValue
                    ? montoRespuesta.Value - totalRespuesta
                    : 0m);

            return PartialView("_Comprobante", new ComprobanteCajaViewModel
            {
                Total = totalRespuesta,
                MetodoPago = metodoPago,
                MontoRecibido = metodoPago == "EFECTIVO" ? montoRespuesta : null,
                Cambio = cambio
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente."
            });
        }
        catch (JsonException)
        {
            return StatusCode((int)HttpStatusCode.BadGateway, new
            {
                mensaje = "La respuesta del cobro no tiene un formato válido."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reembolsar(
        string pagoId,
        string motivo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pagoId) || string.IsNullOrWhiteSpace(motivo))
        {
            return BadRequest(new { mensaje = "Escribe el motivo del reembolso." });
        }

        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.ReembolsarPagoAsync(
                pagoId,
                motivo.Trim(),
                token,
                cancellationToken);

            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new
                {
                    mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            return Ok(new { mensaje = "Reembolso realizado correctamente." });
        }
        catch (HttpRequestException)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente."
            });
        }
    }

    private async Task<ResultadoPago> ObtenerPagoAsync(
        string pedidoId,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await apiClient.ObtenerPagoPorPedidoAsync(
                pedidoId,
                token,
                cancellationToken);

            if (EsSinAutorizacion(response))
            {
                return new ResultadoPago(null, null, true);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ResultadoPago(null, null, false);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ResultadoPago(
                    null,
                    await LeerMensajeErrorAsync(response, cancellationToken),
                    false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var datos = BuscarObjeto(documento.RootElement, "pago")
                ?? BuscarObjeto(documento.RootElement, "data")
                ?? documento.RootElement;
            return new ResultadoPago(new PagoCajaViewModel
            {
                Id = ObtenerTexto(datos, "id", "pagoId") ?? string.Empty,
                Estado = ObtenerTexto(datos, "estado", "estadoPago")?.ToUpperInvariant() ?? string.Empty,
                MetodoPago = ObtenerTexto(datos, "metodoPago", "metodo") ?? string.Empty,
                Total = ObtenerDecimal(datos, "total", "montoTotal", "totalPedido") ?? 0m,
                MontoRecibido = ObtenerDecimal(datos, "montoRecibido"),
                Cambio = ObtenerDecimal(datos, "cambio"),
                MotivoReembolso = ObtenerTexto(datos, "motivoReembolso", "motivo")
            }, null, false);
        }
        catch (HttpRequestException)
        {
            return new ResultadoPago(null, "No fue posible consultar el pago. Inténtalo nuevamente.", false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ResultadoPago(null, "El servidor tardó demasiado en responder. Inténtalo nuevamente.", false);
        }
        catch (JsonException)
        {
            return new ResultadoPago(null, "La respuesta del pago no tiene un formato válido.", false);
        }
    }

    private IActionResult IrAlLogin()
    {
        return RedirectToAction(
            "Login",
            "Account",
            new { aviso = "La sesión venció o no tienes permiso. Inicia sesión nuevamente." });
    }

    private static bool EsSinAutorizacion(HttpResponseMessage response)
    {
        return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
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

    private static PedidoCajaViewModel ConvertirPedido(JsonElement pedido)
    {
        pedido = BuscarObjeto(pedido, "pedido") ?? pedido;
        var productos = ObtenerPropiedad(pedido, "productos", out var listaProductos)
            || ObtenerPropiedad(pedido, "detalles", out listaProductos)
            || ObtenerPropiedad(pedido, "items", out listaProductos)
            ? ObtenerElementos(listaProductos).Select(ConvertirProducto).ToList()
            : [];
        var total = ObtenerDecimal(pedido, "total", "montoTotal", "totalPedido", "importeTotal")
            ?? productos.Sum(producto => producto.Subtotal);

        return new PedidoCajaViewModel
        {
            Id = ObtenerTexto(pedido, "id", "pedidoId") ?? string.Empty,
            CodigoCorto = ObtenerTexto(pedido, "codigoCorto", "codigo", "codigoPedido") ?? "Pedido",
            ClienteOMesa = ObtenerClienteOMesa(pedido),
            Estado = ObtenerTexto(pedido, "estado") ?? string.Empty,
            Total = total,
            Productos = productos
        };
    }

    private static ProductoCajaViewModel ConvertirProducto(JsonElement producto)
    {
        var datosProducto = BuscarObjeto(producto, "producto") ?? producto;
        var nombre = ObtenerTexto(producto, "nombreProducto", "productoNombre", "nombre")
            ?? ObtenerTexto(datosProducto, "nombreProducto", "productoNombre", "nombre");
        var cantidadTexto = ObtenerTexto(producto, "cantidad", "quantity") ?? "1";
        var cantidad = decimal.TryParse(
            cantidadTexto,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var cantidadNumerica)
                ? cantidadNumerica
                : 1m;
        var subtotal = ObtenerDecimal(producto, "subtotal", "subTotal", "importe")
            ?? cantidad * (ObtenerDecimal(producto, "precioUnitario", "precio", "precioProducto")
                ?? ObtenerDecimal(datosProducto, "precio") ?? 0m);

        return new ProductoCajaViewModel
        {
            Nombre = nombre ?? "Producto",
            Cantidad = cantidadTexto,
            Subtotal = subtotal
        };
    }

    private static string ObtenerClienteOMesa(JsonElement pedido)
    {
        var clienteOMesa = ObtenerTexto(pedido, "clienteOMesa");
        if (!string.IsNullOrWhiteSpace(clienteOMesa))
        {
            return clienteOMesa;
        }

        var cliente = ObtenerTexto(pedido, "clienteNombre", "nombreCliente", "cliente");
        var mesa = ObtenerTexto(pedido, "mesaNombre", "numeroMesa", "mesa");
        return string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(cliente) ? null : $"Cliente: {cliente}",
            string.IsNullOrWhiteSpace(mesa) ? null : $"Mesa: {mesa}"
        }.Where(valor => valor is not null));
    }

    private static IEnumerable<JsonElement> ObtenerElementos(JsonElement elemento)
    {
        return elemento.ValueKind == JsonValueKind.Array
            ? elemento.EnumerateArray().ToArray()
            : [];
    }

    private static JsonElement? BuscarObjeto(JsonElement elemento, string nombre)
    {
        return ObtenerPropiedad(elemento, nombre, out var valor)
            && valor.ValueKind == JsonValueKind.Object
                ? valor
                : null;
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

    private static decimal? ObtenerDecimal(JsonElement elemento, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            if (!ObtenerPropiedad(elemento, nombre, out var valor))
            {
                continue;
            }

            if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero))
            {
                return numero;
            }

            if (valor.ValueKind == JsonValueKind.String
                && decimal.TryParse(valor.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out numero))
            {
                return numero;
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

    private readonly record struct ResultadoPago(PagoCajaViewModel? Pago, string? Mensaje, bool SinAutorizacion);
}
