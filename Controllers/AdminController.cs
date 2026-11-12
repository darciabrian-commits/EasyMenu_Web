using EasyMenu.Web.Filters;
using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace EasyMenu.Web.Controllers;

[RequireRole("ADMINISTRADOR")]
public sealed class AdminController(EasyMenuApiClient apiClient) : Controller
{
    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Productos() => View();

    [HttpGet]
    public IActionResult Pedidos() => View();

    [HttpGet]
    public async Task<IActionResult> ListaPedidos(string? estado, CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (estado is not null && !EstadosPedido.Contains(estado, StringComparer.Ordinal))
        {
            return PartialView("_ListaPedidos", new PedidosAdminViewModel
            {
                Mensaje = "El estado seleccionado no es válido."
            });
        }

        try
        {
            using var response = estado is null
                ? await apiClient.ObtenerPedidosAsync(token, cancellationToken)
                : await apiClient.ObtenerPedidosPorEstadoAsync(estado, token, cancellationToken);

            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                return PartialView("_ListaPedidos", new PedidosAdminViewModel
                {
                    Estado = estado,
                    Mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!IntentarObtenerPedidosAdmin(documento.RootElement, out var pedidos))
            {
                return PartialView("_ListaPedidos", new PedidosAdminViewModel
                {
                    Estado = estado,
                    Mensaje = "La respuesta de pedidos no tiene un formato válido."
                });
            }

            return PartialView("_ListaPedidos", new PedidosAdminViewModel
            {
                Estado = estado,
                Pedidos = pedidos
                    .Select(pedido => ConvertirPedidoAdmin(pedido))
                    .OrderByDescending(pedido => pedido.FechaHora ?? DateTime.MinValue)
                    .ToList()
            });
        }
        catch (HttpRequestException)
        {
            return PartialView("_ListaPedidos", new PedidosAdminViewModel
            {
                Estado = estado,
                Mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PartialView("_ListaPedidos", new PedidosAdminViewModel
            {
                Estado = estado,
                Mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente."
            });
        }
        catch (JsonException)
        {
            return PartialView("_ListaPedidos", new PedidosAdminViewModel
            {
                Estado = estado,
                Mensaje = "La respuesta de pedidos no tiene un formato válido."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DetallePedido(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return PartialView("_DetallePedido", new PedidoAdminViewModel
            {
                Mensaje = "No se indicó el pedido que deseas consultar."
            });
        }

        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.ObtenerPedidoAsync(id, token, cancellationToken);
            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                return PartialView("_DetallePedido", new PedidoAdminViewModel
                {
                    Mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!IntentarObtenerPedidoAdmin(documento.RootElement, id, out var pedido))
            {
                return PartialView("_DetallePedido", new PedidoAdminViewModel
                {
                    Mensaje = "La respuesta del pedido no tiene un formato válido."
                });
            }

            return PartialView("_DetallePedido", pedido);
        }
        catch (HttpRequestException)
        {
            return PartialView("_DetallePedido", new PedidoAdminViewModel
            {
                Mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PartialView("_DetallePedido", new PedidoAdminViewModel
            {
                Mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente."
            });
        }
        catch (JsonException)
        {
            return PartialView("_DetallePedido", new PedidoAdminViewModel
            {
                Mensaje = "La respuesta del pedido no tiene un formato válido."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarPedido(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new { mensaje = "No se indicó el pedido que deseas cancelar." });
        }

        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.CancelarPedidoAsync(id, token, cancellationToken);
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

            return Ok(new { mensaje = "El pedido se canceló correctamente." });
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

    [HttpGet]
    public async Task<IActionResult> ListaProductos(CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.ObtenerProductosAsync(token, cancellationToken);
            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                return PartialView("_ListaProductos", new ProductosAdminViewModel
                {
                    Mensaje = await LeerMensajeErrorAsync(response, cancellationToken)
                });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!IntentarObtenerProductos(documento.RootElement, out var productos))
            {
                return PartialView("_ListaProductos", new ProductosAdminViewModel
                {
                    Mensaje = "La respuesta de productos no tiene un formato válido."
                });
            }

            return PartialView("_ListaProductos", new ProductosAdminViewModel { Productos = productos });
        }
        catch (HttpRequestException)
        {
            return PartialView("_ListaProductos", new ProductosAdminViewModel
            {
                Mensaje = "No fue posible conectar con el servidor. Inténtalo nuevamente."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PartialView("_ListaProductos", new ProductosAdminViewModel
            {
                Mensaje = "El servidor tardó demasiado en responder. Inténtalo nuevamente."
            });
        }
        catch (JsonException)
        {
            return PartialView("_ListaProductos", new ProductosAdminViewModel
            {
                Mensaje = "La respuesta de productos no tiene un formato válido."
            });
        }
    }

    [HttpGet]
    public IActionResult NuevoProducto() => View("FormularioProducto", new ProductoFormularioViewModel());

    [HttpGet]
    public async Task<IActionResult> EditarProducto(string id, CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await apiClient.ObtenerProductoAsync(id, token, cancellationToken);
            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                TempData["MensajeError"] = await LeerMensajeErrorAsync(response, cancellationToken);
                return RedirectToAction(nameof(Productos));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!IntentarLeerProducto(documento.RootElement, out var producto))
            {
                TempData["MensajeError"] = "La respuesta del producto no tiene un formato válido.";
                return RedirectToAction(nameof(Productos));
            }

            return View("FormularioProducto", new ProductoFormularioViewModel
            {
                Id = producto.Id,
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                Precio = producto.Precio,
                Categoria = producto.Categoria,
                CategoriaOriginal = producto.Categoria
            });
        }
        catch (HttpRequestException)
        {
            TempData["MensajeError"] = "No fue posible conectar con el servidor. Inténtalo nuevamente.";
            return RedirectToAction(nameof(Productos));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TempData["MensajeError"] = "El servidor tardó demasiado en responder. Inténtalo nuevamente.";
            return RedirectToAction(nameof(Productos));
        }
        catch (JsonException)
        {
            TempData["MensajeError"] = "La respuesta del producto no tiene un formato válido.";
            return RedirectToAction(nameof(Productos));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarProducto(
        ProductoFormularioViewModel modelo,
        CancellationToken cancellationToken)
    {
        if (!new[] { "PLATILLO", "BEBIDA", "POSTRE" }.Contains(modelo.Categoria, StringComparer.Ordinal)
            && !(modelo.Id is not null && modelo.Categoria == modelo.CategoriaOriginal))
        {
            ModelState.AddModelError(nameof(modelo.Categoria), "Selecciona una categoría válida.");
        }

        if (!ModelState.IsValid)
        {
            return View("FormularioProducto", modelo);
        }

        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = modelo.Id is null
                ? await apiClient.CrearProductoAsync(
                    modelo.Nombre,
                    modelo.Descripcion ?? string.Empty,
                    modelo.Precio,
                    modelo.Categoria,
                    token,
                    cancellationToken)
                : await apiClient.ActualizarProductoAsync(
                    modelo.Id,
                    modelo.Nombre,
                    modelo.Descripcion ?? string.Empty,
                    modelo.Precio,
                    modelo.Categoria,
                    token,
                    cancellationToken);

            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, await LeerMensajeErrorAsync(response, cancellationToken));
                return View("FormularioProducto", modelo);
            }

            TempData["MensajeExito"] = modelo.Id is null
                ? "Producto creado correctamente."
                : "Producto actualizado correctamente.";
            return RedirectToAction(nameof(Productos));
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No fue posible conectar con el servidor. Inténtalo nuevamente.");
            return View("FormularioProducto", modelo);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "El servidor tardó demasiado en responder. Inténtalo nuevamente.");
            return View("FormularioProducto", modelo);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarDisponibilidad(
        string id,
        bool disponible,
        CancellationToken cancellationToken)
    {
        return await EjecutarCambioProductoAsync(
            token => apiClient.CambiarDisponibilidadProductoAsync(id, disponible, token, cancellationToken),
            disponible ? "El producto ahora está disponible." : "El producto ahora no está disponible.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarProducto(string id, CancellationToken cancellationToken)
    {
        return await EjecutarCambioProductoAsync(
            token => apiClient.EliminarProductoAsync(id, token, cancellationToken),
            "Producto eliminado correctamente.",
            cancellationToken);
    }

    private async Task<IActionResult> EjecutarCambioProductoAsync(
        Func<string, Task<HttpResponseMessage>> ejecutar,
        string mensajeExito,
        CancellationToken cancellationToken)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return IrAlLogin();
        }

        try
        {
            using var response = await ejecutar(token);
            if (EsSinAutorizacion(response))
            {
                return IrAlLogin();
            }

            if (response.IsSuccessStatusCode)
            {
                TempData["MensajeExito"] = mensajeExito;
            }
            else
            {
                TempData["MensajeError"] = await LeerMensajeErrorAsync(response, cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            TempData["MensajeError"] = "No fue posible conectar con el servidor. Inténtalo nuevamente.";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TempData["MensajeError"] = "El servidor tardó demasiado en responder. Inténtalo nuevamente.";
        }

        return RedirectToAction(nameof(Productos));
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

    private static bool IntentarObtenerProductos(JsonElement elemento, out List<ProductoAdminViewModel> productos)
    {
        if (elemento.ValueKind == JsonValueKind.Object)
        {
            foreach (var propiedad in new[] { "productos", "content", "data", "results", "resultado" })
            {
                if (ObtenerPropiedad(elemento, propiedad, out var contenido)
                    && IntentarObtenerProductos(contenido, out productos))
                {
                    return true;
                }
            }
        }

        if (elemento.ValueKind == JsonValueKind.Array)
        {
            productos = [];
            foreach (var item in elemento.EnumerateArray())
            {
                if (!IntentarLeerProducto(item, out var producto))
                {
                    productos = [];
                    return false;
                }

                productos.Add(producto);
            }

            return true;
        }

        productos = [];
        return false;
    }

    private static bool IntentarLeerProducto(JsonElement elemento, out ProductoAdminViewModel producto)
    {
        if (elemento.ValueKind == JsonValueKind.Object
            && ObtenerPropiedad(elemento, "producto", out var envoltorio)
            && envoltorio.ValueKind == JsonValueKind.Object)
        {
            elemento = envoltorio;
        }

        if (elemento.ValueKind == JsonValueKind.Object
            && ObtenerTexto(elemento, "id") is { } id
            && ObtenerTexto(elemento, "nombre") is { } nombre
            && ObtenerDecimal(elemento, "precio") is { } precio
            && ObtenerBooleano(elemento, "disponible") is { } disponible)
        {
            producto = new ProductoAdminViewModel
            {
                Id = id,
                Nombre = nombre,
                Descripcion = ObtenerTexto(elemento, "descripcion") ?? string.Empty,
                Categoria = ObtenerTexto(elemento, "categoria") ?? "Sin categoría",
                Precio = precio,
                Disponible = disponible
            };
            return true;
        }

        producto = new ProductoAdminViewModel();
        return false;
    }

    private static string? ObtenerTexto(JsonElement elemento, string nombre)
    {
        if (!ObtenerPropiedad(elemento, nombre, out var valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            _ => null
        };
    }

    private static decimal? ObtenerDecimal(JsonElement elemento, string nombre)
    {
        if (!ObtenerPropiedad(elemento, nombre, out var valor))
        {
            return null;
        }

        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero))
        {
            return numero;
        }

        return valor.ValueKind == JsonValueKind.String
            && decimal.TryParse(valor.GetString(), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out numero)
                ? numero
                : null;
    }

    private static bool? ObtenerBooleano(JsonElement elemento, string nombre)
    {
        if (!ObtenerPropiedad(elemento, nombre, out var valor))
        {
            return null;
        }

        if (valor.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return valor.GetBoolean();
        }

        return valor.ValueKind == JsonValueKind.String
            && bool.TryParse(valor.GetString(), out var booleano)
                ? booleano
                : null;
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

    private static readonly string[] EstadosPedido =
    [
        "PENDIENTE_PAGO",
        "RECIBIDO",
        "EN_PREPARACION",
        "LISTO",
        "ENTREGADO",
        "CANCELADO",
        "EXPIRADO"
    ];

    private static bool IntentarObtenerPedidosAdmin(JsonElement elemento, out List<JsonElement> pedidos)
    {
        if (elemento.ValueKind == JsonValueKind.Array)
        {
            pedidos = elemento.EnumerateArray().ToList();
            return pedidos.All(pedido => pedido.ValueKind == JsonValueKind.Object);
        }

        if (elemento.ValueKind == JsonValueKind.Object)
        {
            foreach (var nombre in new[] { "pedidos", "content", "data", "results", "resultado" })
            {
                if (ObtenerPropiedad(elemento, nombre, out var contenido)
                    && IntentarObtenerPedidosAdmin(contenido, out pedidos))
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

    private static bool IntentarObtenerPedidoAdmin(
        JsonElement elemento,
        string id,
        out PedidoAdminViewModel pedido)
    {
        foreach (var nombre in new[] { "pedido", "data", "resultado" })
        {
            if (ObtenerPropiedad(elemento, nombre, out var contenido)
                && contenido.ValueKind == JsonValueKind.Object)
            {
                elemento = contenido;
                break;
            }
        }

        if (elemento.ValueKind != JsonValueKind.Object)
        {
            pedido = new PedidoAdminViewModel();
            return false;
        }

        pedido = ConvertirPedidoAdmin(elemento, id);
        return true;
    }

    private static PedidoAdminViewModel ConvertirPedidoAdmin(JsonElement elemento, string? idFallback = null)
    {
        var elementosProductos = ObtenerPropiedad(elemento, "productos", out var productos)
            || ObtenerPropiedad(elemento, "detalles", out productos)
            || ObtenerPropiedad(elemento, "items", out productos)
                ? productos.ValueKind == JsonValueKind.Array
                    ? productos.EnumerateArray().ToList()
                    : []
                : [];
        var listaProductos = elementosProductos
            .Where(producto => producto.ValueKind == JsonValueKind.Object)
            .Select(ConvertirProductoPedidoAdmin)
            .ToList();
        var total = ObtenerDecimal(elemento, "total")
            ?? ObtenerDecimal(elemento, "montoTotal")
            ?? ObtenerDecimal(elemento, "totalPedido")
            ?? ObtenerDecimal(elemento, "importeTotal")
            ?? listaProductos.Sum(producto => producto.Subtotal);

        return new PedidoAdminViewModel
        {
            Id = ObtenerTexto(elemento, "id")
                ?? ObtenerTexto(elemento, "pedidoId")
                ?? idFallback
                ?? string.Empty,
            CodigoCorto = ObtenerTexto(elemento, "codigoCorto")
                ?? ObtenerTexto(elemento, "codigo")
                ?? ObtenerTexto(elemento, "codigoPedido")
                ?? "Pedido",
            ClienteOMesa = ObtenerClienteOMesaAdmin(elemento),
            Estado = (ObtenerTexto(elemento, "estado") ?? string.Empty).ToUpperInvariant(),
            Total = total,
            FechaHora = ObtenerFechaHoraElSalvador(elemento),
            Productos = listaProductos
        };
    }

    private static ProductoPedidoAdminViewModel ConvertirProductoPedidoAdmin(JsonElement elemento)
    {
        var detalle = elemento;
        var nombre = ObtenerTexto(detalle, "nombreProducto")
            ?? ObtenerTexto(detalle, "productoNombre")
            ?? ObtenerTexto(detalle, "nombre");
        if (ObtenerPropiedad(detalle, "producto", out var producto))
        {
            nombre ??= producto.ValueKind == JsonValueKind.String
                ? producto.GetString()
                : ObtenerTexto(producto, "nombre");
        }

        var cantidad = ObtenerTexto(detalle, "cantidad")
            ?? ObtenerTexto(detalle, "quantity")
            ?? "1";
        var precioUnitario = ObtenerDecimal(detalle, "precioUnitario")
            ?? ObtenerDecimal(detalle, "precio")
            ?? ObtenerDecimal(detalle, "price")
            ?? 0m;
        var subtotal = ObtenerDecimal(detalle, "subtotal")
            ?? ObtenerDecimal(detalle, "subTotal")
            ?? (decimal.TryParse(
                    cantidad,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var cantidadNumerica)
                ? precioUnitario * cantidadNumerica
                : precioUnitario);

        return new ProductoPedidoAdminViewModel
        {
            Nombre = nombre ?? "Producto",
            Cantidad = cantidad,
            PrecioUnitario = precioUnitario,
            Subtotal = subtotal,
            Notas = ObtenerTexto(detalle, "notas")
                ?? ObtenerTexto(detalle, "nota")
                ?? ObtenerTexto(detalle, "observaciones")
                ?? ObtenerTexto(detalle, "comentario")
        };
    }

    private static string ObtenerClienteOMesaAdmin(JsonElement pedido)
    {
        var clienteOMesa = ObtenerTexto(pedido, "clienteOMesa");
        if (!string.IsNullOrWhiteSpace(clienteOMesa))
        {
            return clienteOMesa;
        }

        var cliente = ObtenerTexto(pedido, "clienteNombre")
            ?? ObtenerTexto(pedido, "nombreCliente")
            ?? ObtenerTextoAnidado(pedido, "cliente");
        var mesa = ObtenerTexto(pedido, "mesaNombre")
            ?? ObtenerTexto(pedido, "numeroMesa")
            ?? ObtenerTextoAnidado(pedido, "mesa");
        var partes = new[]
        {
            string.IsNullOrWhiteSpace(cliente) ? null : $"Cliente: {cliente}",
            string.IsNullOrWhiteSpace(mesa) ? null : $"Mesa: {mesa}"
        }.Where(parte => parte is not null);
        var resultado = string.Join(" · ", partes);
        return string.IsNullOrWhiteSpace(resultado) ? "Sin dato" : resultado;
    }

    private static string? ObtenerTextoAnidado(JsonElement elemento, string nombre)
    {
        if (!ObtenerPropiedad(elemento, nombre, out var valor))
        {
            return null;
        }

        if (valor.ValueKind == JsonValueKind.Object)
        {
            return ObtenerTexto(valor, "nombre")
                ?? ObtenerTexto(valor, "numero")
                ?? ObtenerTexto(valor, "nombreCompleto");
        }

        return ObtenerTexto(elemento, nombre);
    }

    private static DateTime? ObtenerFechaHoraElSalvador(JsonElement pedido)
    {
        var fechaTexto = ObtenerTexto(pedido, "fechaHora")
            ?? ObtenerTexto(pedido, "fechaCreacion")
            ?? ObtenerTexto(pedido, "fecha")
            ?? ObtenerTexto(pedido, "createdAt")
            ?? ObtenerTexto(pedido, "fechaPedido");
        if (!DateTime.TryParse(
                fechaTexto,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal
                    | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var fechaUtc))
        {
            return null;
        }

        var zonaElSalvador = TimeZoneInfo.FindSystemTimeZoneById("America/El_Salvador");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc), zonaElSalvador);
    }
}
