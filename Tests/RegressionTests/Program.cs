using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using EasyMenu.Web.Controllers;
using EasyMenu.Web.Filters;
using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

var fallos = 0;
await Ejecutar("el cobro consulta el pedido y usa el PagoDTO real", CobroConsultaPedidoYUsaPagoDto);
await Ejecutar("el cobro rechaza pagos duplicados", CobroRechazaPagoDuplicado);
await Ejecutar("el cobro muestra el rechazo de duplicado de la API", CobroMuestraDuplicadoDeApi);
await Ejecutar("el cobro valida efectivo contra el total real", CobroValidaMontoContraTotalReal);
await Ejecutar("productos rechaza categorías manipuladas sin llamar a la API", ProductoRechazaCategoriaInvalida);
await Ejecutar("el login diferencia error del servidor de credenciales incorrectas", LoginDistingueErrorServidor);
await Ejecutar("el login exige todos los campos del DTO y limpia sesión", LoginValidaDtoYLimpiaSesion);
await Ejecutar("el logout limpia la sesión", LogoutLimpiaSesion);
await Ejecutar("RequireRole limpia sesiones con rol no autorizado", RequireRoleRechazaRolNoPermitido);

Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : $"{fallos} prueba(s) fallaron.");
Environment.ExitCode = fallos == 0 ? 0 : 1;

async Task Ejecutar(string nombre, Func<Task> prueba)
{
    try
    {
        await prueba();
        Console.WriteLine($"PASS {nombre}");
    }
    catch (Exception excepcion)
    {
        fallos++;
        Console.Error.WriteLine($"FAIL {nombre}: {excepcion.Message}");
    }
}

static async Task CobroConsultaPedidoYUsaPagoDto()
{
    var solicitudes = new List<(HttpMethod Metodo, string Ruta, string? Cuerpo)>();
    var cliente = CrearCliente((request, cancellationToken) =>
    {
        var cuerpo = request.Content is null
            ? null
            : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        solicitudes.Add((request.Method, request.RequestUri!.AbsolutePath, cuerpo));

        return Task.FromResult(request.RequestUri.AbsolutePath switch
        {
            "/api/pedidos/p1" => JsonResponse(HttpStatusCode.OK, """{"id":"p1","estado":"PENDIENTE_PAGO","total":45.00}"""),
            "/api/pagos/pedido/p1" => new HttpResponseMessage(HttpStatusCode.NotFound),
            "/api/pagos/cobrar" => JsonResponse(
                HttpStatusCode.OK,
                """{"id":"pago-7","total":45.00,"metodoPago":"EFECTIVO","montoRecibido":50.00,"cambio":5.00}"""),
            _ => throw new InvalidOperationException($"Ruta inesperada: {request.RequestUri}")
        });
    });
    var controller = CrearCajaController(cliente);

    var resultado = await controller.Cobrar("p1", "EFECTIVO", 50m, CancellationToken.None);

    Verificar(resultado is PartialViewResult { ViewName: "_Comprobante" }, "No se devolvió el comprobante.");
    var modelo = (ComprobanteCajaViewModel)((PartialViewResult)resultado).Model!;
    Verificar(modelo.Total == 45m && modelo.Cambio == 5m && modelo.MontoRecibido == 50m,
        "El comprobante no usa los importes de PagoDTO.");
    Verificar(solicitudes.Select(solicitud => solicitud.Ruta).SequenceEqual(
            ["/api/pedidos/p1", "/api/pagos/pedido/p1", "/api/pagos/cobrar"]),
        "No se consultó pedido/pago antes del cobro.");

    var cuerpoCobro = solicitudes.Last().Cuerpo!;
    using var documento = JsonDocument.Parse(cuerpoCobro);
    var propiedades = documento.RootElement.EnumerateObject().Select(propiedad => propiedad.Name).ToHashSet();
    Verificar(propiedades.SetEquals(["pedidoId", "metodoPago", "montoRecibido"]),
        "El cobro envió campos adicionales o incompletos.");
    Verificar(!cuerpoCobro.Contains("totalPedido", StringComparison.OrdinalIgnoreCase),
        "El cobro envió un total proporcionado por el navegador.");
}

static async Task CobroRechazaPagoDuplicado()
{
    var cobros = 0;
    var cliente = CrearCliente((request, _) =>
    {
        if (request.RequestUri!.AbsolutePath == "/api/pedidos/p1")
        {
            return Task.FromResult(JsonResponse(HttpStatusCode.OK,
                """{"id":"p1","estado":"PENDIENTE_PAGO","total":25}"""));
        }

        if (request.RequestUri.AbsolutePath == "/api/pagos/pedido/p1")
        {
            return Task.FromResult(JsonResponse(HttpStatusCode.OK,
                """{"id":"pago-1","estado":"COMPLETADO","total":25,"metodoPago":"TARJETA","cambio":0}"""));
        }

        if (request.RequestUri.AbsolutePath == "/api/pagos/cobrar")
        {
            cobros++;
        }

        throw new InvalidOperationException("No se esperaba otra solicitud a la API.");
    });

    var resultado = await CrearCajaController(cliente).Cobrar("p1", "TARJETA", null, CancellationToken.None);

    Verificar(resultado is ConflictObjectResult && cobros == 0,
        "Se permitió volver a cobrar un pedido que ya tiene pago.");
}

static async Task CobroMuestraDuplicadoDeApi()
{
    var cliente = CrearCliente((request, _) =>
        Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/pedidos/p1" => JsonResponse(HttpStatusCode.OK,
                """{"id":"p1","estado":"PENDIENTE_PAGO","total":25}"""),
            "/api/pagos/pedido/p1" => new HttpResponseMessage(HttpStatusCode.NotFound),
            "/api/pagos/cobrar" => JsonResponse(HttpStatusCode.Conflict,
                """{"estado":409,"mensaje":"El pedido ya tiene un pago registrado.","fecha":"2026-11-18"}"""),
            _ => throw new InvalidOperationException($"Ruta inesperada: {request.RequestUri}")
        }));

    var resultado = await CrearCajaController(cliente).Cobrar("p1", "TARJETA", null, CancellationToken.None);

    Verificar(resultado is ObjectResult { StatusCode: 409 },
        "No se conservó el estado de conflicto que devolvió la API.");
    var valor = ((ObjectResult)resultado).Value!;
    Verificar(valor.GetType().GetProperty("mensaje")?.GetValue(valor) is string mensaje
        && mensaje.Contains("pago registrado", StringComparison.OrdinalIgnoreCase),
        "No se mostró el mensaje de duplicado de la API.");
}

static async Task CobroValidaMontoContraTotalReal()
{
    var cliente = CrearCliente((request, _) =>
    {
        Verificar(request.RequestUri!.AbsolutePath == "/api/pedidos/p1",
            "La validación de efectivo debe consultar primero el pedido.");
        return Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """{"id":"p1","estado":"PENDIENTE_PAGO","total":80}"""));
    });

    var resultado = await CrearCajaController(cliente).Cobrar("p1", "EFECTIVO", 50m, CancellationToken.None);

    Verificar(resultado is BadRequestObjectResult, "Se aceptó un monto menor al total real.");
}

static async Task ProductoRechazaCategoriaInvalida()
{
    var llamadasApi = 0;
    var cliente = CrearCliente((_, _) =>
    {
        llamadasApi++;
        throw new InvalidOperationException("No se debía llamar a la API.");
    });
    var controller = new AdminController(cliente);
    PrepararController(controller, new TestSession(("Token", "token"), ("Rol", "ADMINISTRADOR")));

    var resultado = await controller.GuardarProducto(new ProductoFormularioViewModel
    {
        Nombre = "Producto",
        Precio = 1m,
        Categoria = "CATEGORIA_MANIPULADA"
    }, CancellationToken.None);

    Verificar(resultado is ViewResult && controller.ModelState.ContainsKey(nameof(ProductoFormularioViewModel.Categoria)),
        "No se rechazó una categoría fuera de la lista permitida.");
    Verificar(llamadasApi == 0, "La categoría inválida llegó a la API.");
}

static async Task LoginDistingueErrorServidor()
{
    var cliente = CrearCliente((_, _) => Task.FromResult(
        JsonResponse(HttpStatusCode.InternalServerError, """{"mensaje":"Error interno"}""")));
    var sesion = new TestSession(("Token", "token-antiguo"), ("Rol", "CAJERO"));
    var controller = new AccountController(cliente);
    PrepararController(controller, sesion);

    await controller.Login(new LoginViewModel { Correo = "caja@example.com", Clave = "clave" }, CancellationToken.None);

    Verificar(controller.ViewBag.ErrorLogin is null, "Un error 500 se mostró como credenciales incorrectas.");
    Verificar(controller.ModelState.Values.SelectMany(valor => valor.Errors)
            .Any(error => error.ErrorMessage.Contains("servidor de autenticación", StringComparison.OrdinalIgnoreCase)),
        "No se informó que ocurrió un error del servidor.");
    Verificar(!sesion.TryGetValue("Token", out _), "No se limpió la sesión al iniciar sesión.");
}

static async Task LoginValidaDtoYLimpiaSesion()
{
    var cliente = CrearCliente((_, _) => Task.FromResult(JsonResponse(
        HttpStatusCode.OK,
        """{"token":"jwt-valido","rol":"CAJERO"}""")));
    var sesion = new TestSession(("Token", "token-antiguo"), ("Rol", "ADMINISTRADOR"));
    var controller = new AccountController(cliente);
    PrepararController(controller, sesion);

    await controller.Login(new LoginViewModel { Correo = "caja@example.com", Clave = "clave" }, CancellationToken.None);

    Verificar(controller.ModelState.Values.SelectMany(valor => valor.Errors)
            .Any(error => error.ErrorMessage.Contains("datos de sesión", StringComparison.OrdinalIgnoreCase)),
        "Se aceptó un DTO sin tipo de token.");
    Verificar(!sesion.TryGetValue("Token", out _), "Se conservó una sesión anterior tras DTO incompleto.");
}

static Task LogoutLimpiaSesion()
{
    var sesion = new TestSession(("Token", "jwt"), ("Rol", "CAJERO"));
    var controller = new AccountController(CrearCliente((_, _) =>
        throw new InvalidOperationException("Logout no debe llamar a la API.")));
    PrepararController(controller, sesion);

    controller.Logout();

    Verificar(!sesion.TryGetValue("Token", out _) && !sesion.TryGetValue("Rol", out _),
        "El logout no limpió todos los datos de sesión.");
    return Task.CompletedTask;
}

static Task RequireRoleRechazaRolNoPermitido()
{
    var sesion = new TestSession(("Token", "jwt"), ("Rol", "CAJERO"));
    var httpContext = new DefaultHttpContext { Session = sesion };
    var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    var context = new ActionExecutingContext(
        actionContext,
        [],
        new Dictionary<string, object?>(),
        new CajaController(CrearCliente((_, _) => throw new InvalidOperationException())));

    new RequireRoleAttribute("ADMINISTRADOR").OnActionExecuting(context);

    Verificar(context.Result is RedirectToActionResult, "No se rechazó el rol no permitido.");
    Verificar(!sesion.TryGetValue("Token", out _), "No se limpió la sesión con rol no permitido.");
    return Task.CompletedTask;
}

static EasyMenuApiClient CrearCliente(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
{
    return new EasyMenuApiClient(new HttpClient(new StubHttpMessageHandler(responder))
    {
        BaseAddress = new Uri("https://api.test/api/")
    });
}

static CajaController CrearCajaController(EasyMenuApiClient cliente)
{
    var controller = new CajaController(cliente);
    PrepararController(controller, new TestSession(("Token", "jwt"), ("Rol", "CAJERO")));
    return controller;
}

static void PrepararController(Controller controller, ISession session)
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { Session = session }
    };
}

static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
{
    return new HttpResponseMessage(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}

static void Verificar(bool condicion, string mensaje)
{
    if (!condicion)
    {
        throw new InvalidOperationException(mensaje);
    }
}

sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => responder(request, cancellationToken);
}

sealed class TestSession(params (string Clave, string Valor)[] valores) : ISession
{
    private readonly Dictionary<string, byte[]> datos = valores.ToDictionary(
        valor => valor.Clave,
        valor => Encoding.UTF8.GetBytes(valor.Valor));

    public bool IsAvailable => true;
    public string Id => "prueba";
    public IEnumerable<string> Keys => datos.Keys;
    public void Clear() => datos.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => datos.Remove(key);
    public void Set(string key, byte[] value) => datos[key] = value;

    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value)
    {
        var encontrado = datos.TryGetValue(key, out var valor);
        value = valor;
        return encontrado;
    }
}
