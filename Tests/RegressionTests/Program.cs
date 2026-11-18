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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

var fallos = 0;
await Ejecutar("el resumen consulta por fecha y deserializa todos sus campos", ResumenVentasConsultaFechaYDeserializaCampos);
await Ejecutar("el resumen muestra los errores HTTP de la API", ResumenVentasMuestraErrorHttp);
await Ejecutar("las acciones de administración son exclusivas del administrador", AdminRechazaRolesNoPermitidos);
await Ejecutar("el personal se lista sin conservar contraseñas", PersonalListaUsuariosSinClave);
await Ejecutar("se pueden crear usuarios cajero y cocina con el contrato real", PersonalCreaRolesPermitidos);
await Ejecutar("personal rechaza roles inválidos sin llamar a la API", PersonalRechazaRolInvalido);
await Ejecutar("personal muestra errores HTTP al crear usuarios", PersonalMuestraErrorHttpAlCrear);
await Ejecutar("personal desactiva usuarios mediante PATCH", PersonalDesactivaUsuario);
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

static async Task ResumenVentasConsultaFechaYDeserializaCampos()
{
    HttpRequestMessage? solicitud = null;
    var cliente = CrearCliente((request, _) =>
    {
        solicitud = request;
        return Task.FromResult(JsonResponse(HttpStatusCode.OK, """
            {
              "fecha":"2026-11-18",
              "cantidadCobrosRegistrados":12,
              "totalCobradoEfectivo":125.50,
              "totalCobradoTarjeta":200.25,
              "totalCobradoTransferencia":75.00,
              "totalBrutoVentas":400.75,
              "cantidadReembolsosRegistrados":2,
              "totalReembolsos":20.25,
              "totalNetoVentas":380.50
            }
            """));
    });
    var controller = CrearAdminController(cliente);

    var resultado = await controller.ConsultaResumenVentas("2026-11-18", CancellationToken.None);

    Verificar(resultado is PartialViewResult { ViewName: "_ResumenVentas" },
        "No se devolvió la vista parcial del resumen.");
    var resumen = ((ResumenVentasViewModel)((PartialViewResult)resultado).Model!).Resumen!;
    Verificar(solicitud?.Method == HttpMethod.Get
        && solicitud.RequestUri!.PathAndQuery == "/api/pagos/resumen?fecha=2026-11-18",
        "No se consultó el endpoint de resumen con la fecha seleccionada.");
    Verificar(solicitud!.Headers.Authorization?.Scheme == "Bearer"
        && solicitud.Headers.Authorization.Parameter == "jwt",
        "La consulta no usó el token de sesión.");
    Verificar(resumen.Fecha == new DateOnly(2026, 11, 18)
        && resumen.CantidadCobrosRegistrados == 12
        && resumen.TotalCobradoEfectivo == 125.50m
        && resumen.TotalCobradoTarjeta == 200.25m
        && resumen.TotalCobradoTransferencia == 75m
        && resumen.TotalBrutoVentas == 400.75m
        && resumen.CantidadReembolsosRegistrados == 2
        && resumen.TotalReembolsos == 20.25m
        && resumen.TotalNetoVentas == 380.50m,
        "No se deserializaron todos los campos reales del resumen.");
}

static async Task ResumenVentasMuestraErrorHttp()
{
    var cliente = CrearCliente((_, _) => Task.FromResult(JsonResponse(
        HttpStatusCode.BadRequest,
        """{"estado":400,"mensaje":"La fecha no es válida.","fecha":"2026-11-18"}""")));

    var resultado = await CrearAdminController(cliente)
        .ConsultaResumenVentas("2026-11-18", CancellationToken.None);

    var modelo = (ResumenVentasViewModel)((PartialViewResult)resultado).Model!;
    Verificar(modelo.Mensaje == "La fecha no es válida.",
        "No se mostró el mensaje de error devuelto por la API.");
}

static Task AdminRechazaRolesNoPermitidos()
{
    var session = new TestSession(("Token", "jwt"), ("Rol", "CAJERO"));
    var context = CrearFiltroContexto(CrearAdminController(CrearCliente((_, _) =>
        throw new InvalidOperationException("No se esperaba una llamada a la API."))), session);
    var atributo = (RequireRoleAttribute?)Attribute.GetCustomAttribute(
        typeof(AdminController),
        typeof(RequireRoleAttribute));

    Verificar(atributo is not null, "AdminController no protege sus acciones con RequireRole.");
    atributo!.OnActionExecuting(context);

    Verificar(context.Result is RedirectToActionResult,
        "Un usuario que no es administrador obtuvo acceso al controlador Admin.");
    Verificar(!session.TryGetValue("Token", out _),
        "No se limpió la sesión del usuario no autorizado.");
    return Task.CompletedTask;
}

static async Task PersonalListaUsuariosSinClave()
{
    HttpRequestMessage? solicitud = null;
    var cliente = CrearCliente((request, _) =>
    {
        solicitud = request;
        return Task.FromResult(JsonResponse(HttpStatusCode.OK, """
            [
              {"id":7,"nombre":"Ana","correo":"ana@example.com","clave":"no-exponer","rol":"CAJERO","activo":true},
              {"id":8,"nombre":"Luis","correo":"luis@example.com","clave":"no-exponer","rol":"COCINA","activo":false}
            ]
            """));
    });

    var resultado = await CrearAdminController(cliente).ListaPersonal(CancellationToken.None);

    Verificar(resultado is PartialViewResult { ViewName: "_ListaPersonal" },
        "No se devolvió la vista parcial del personal.");
    var usuarios = ((PersonalAdminViewModel)((PartialViewResult)resultado).Model!).Usuarios;
    Verificar(solicitud?.Method == HttpMethod.Get
        && solicitud.RequestUri!.AbsolutePath == "/api/usuarios"
        && solicitud.Headers.Authorization?.Parameter == "jwt",
        "No se consultó correctamente el endpoint de usuarios.");
    Verificar(usuarios.Count == 2
        && usuarios[0].Id == "7"
        && usuarios[0].Nombre == "Ana"
        && usuarios[0].Correo == "ana@example.com"
        && usuarios[0].Rol == "CAJERO"
        && usuarios[0].Activo
        && usuarios[1].Rol == "COCINA"
        && !usuarios[1].Activo,
        "No se mapearon los datos del listado de usuarios.");
    Verificar(typeof(UsuarioPersonalViewModel).GetProperty("Clave") is null,
        "El modelo de personal conserva una contraseña.");
}

static async Task PersonalCreaRolesPermitidos()
{
    foreach (var rol in new[] { "CAJERO", "COCINA" })
    {
        HttpRequestMessage? solicitud = null;
        string? cuerpoSolicitud = null;
        var cliente = CrearCliente((request, cancellationToken) =>
        {
            solicitud = request;
            cuerpoSolicitud = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        });
        var controller = CrearAdminController(cliente);

        var resultado = await controller.CrearUsuario(new UsuarioFormularioViewModel
        {
            Nombre = "Personal",
            Correo = $"{rol.ToLowerInvariant()}@example.com",
            Clave = "clave-solo-para-la-solicitud",
            Rol = rol
        }, CancellationToken.None);

        Verificar(resultado is RedirectToActionResult { ActionName: nameof(AdminController.Personal) },
            $"No se creó el rol {rol}.");
        Verificar(solicitud?.Method == HttpMethod.Post
            && solicitud.RequestUri!.AbsolutePath == "/api/usuarios"
            && solicitud.Headers.Authorization?.Parameter == "jwt",
            $"No se envió el POST real para {rol}.");
        using var documento = JsonDocument.Parse(cuerpoSolicitud!);
        var propiedades = documento.RootElement.EnumerateObject()
            .Select(propiedad => propiedad.Name)
            .ToHashSet(StringComparer.Ordinal);
        Verificar(propiedades.SetEquals(["nombre", "correo", "clave", "rol"])
            && documento.RootElement.GetProperty("rol").GetString() == rol,
            "La creación envió campos extra o un rol distinto al permitido.");
    }
}

static async Task PersonalRechazaRolInvalido()
{
    var llamadasApi = 0;
    var controller = CrearAdminController(CrearCliente((_, _) =>
    {
        llamadasApi++;
        throw new InvalidOperationException("No se debía llamar a la API.");
    }));

    var resultado = await controller.CrearUsuario(new UsuarioFormularioViewModel
    {
        Nombre = "Usuario",
        Correo = "usuario@example.com",
        Clave = "clave",
        Rol = "ADMINISTRADOR"
    }, CancellationToken.None);

    Verificar(resultado is ViewResult && llamadasApi == 0,
        "Se intentó crear un rol no permitido.");
    Verificar(controller.ModelState.ContainsKey(nameof(UsuarioFormularioViewModel.Rol)),
        "No se reportó el rol inválido en la validación del servidor.");
}

static async Task PersonalMuestraErrorHttpAlCrear()
{
    var cliente = CrearCliente((_, _) => Task.FromResult(JsonResponse(
        HttpStatusCode.Conflict,
        """{"estado":409,"mensaje":"El correo ya está registrado.","fecha":"2026-11-18"}""")));
    var controller = CrearAdminController(cliente);
    var modelo = new UsuarioFormularioViewModel
    {
        Nombre = "Usuario",
        Correo = "usuario@example.com",
        Clave = "clave-que-no-debe-retenerse",
        Rol = "CAJERO"
    };

    var resultado = await controller.CrearUsuario(modelo, CancellationToken.None);

    Verificar(resultado is ViewResult
        && controller.ModelState.Values.SelectMany(valor => valor.Errors)
            .Any(error => error.ErrorMessage == "El correo ya está registrado."),
        "No se mostró el error HTTP de la API al crear el usuario.");
    Verificar(modelo.Clave.Length == 0,
        "Se conservó la contraseña tras el error de creación.");
}

static async Task PersonalDesactivaUsuario()
{
    HttpRequestMessage? solicitud = null;
    var cliente = CrearCliente((request, _) =>
    {
        solicitud = request;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    });
    var controller = CrearAdminController(cliente);

    var resultado = await controller.DesactivarUsuario("7", CancellationToken.None);

    Verificar(resultado is RedirectToActionResult { ActionName: nameof(AdminController.Personal) },
        "La desactivación no regresó al listado de personal.");
    Verificar(solicitud?.Method == HttpMethod.Patch
        && solicitud.RequestUri!.AbsolutePath == "/api/usuarios/7/desactivar"
        && solicitud.Headers.Authorization?.Parameter == "jwt",
        "No se utilizó el endpoint PATCH para desactivar al usuario.");
    Verificar(controller.TempData["MensajeExito"] as string == "Usuario desactivado correctamente.",
        "No se confirmó la desactivación correctamente.");
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

static AdminController CrearAdminController(EasyMenuApiClient cliente)
{
    var controller = new AdminController(cliente);
    PrepararController(controller, new TestSession(("Token", "jwt"), ("Rol", "ADMINISTRADOR")));
    return controller;
}

static void PrepararController(Controller controller, ISession session)
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { Session = session }
    };
    controller.TempData = new TempDataDictionary(controller.HttpContext, new TestTempDataProvider());
}

static ActionExecutingContext CrearFiltroContexto(Controller controller, ISession session)
{
    var httpContext = new DefaultHttpContext { Session = session };
    var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    return new ActionExecutingContext(
        actionContext,
        [],
        new Dictionary<string, object?>(),
        controller);
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

sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
    }
}
