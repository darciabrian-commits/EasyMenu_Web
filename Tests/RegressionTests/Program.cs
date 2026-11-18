using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations;
using EasyMenu.Web.Controllers;
using EasyMenu.Web.Filters;
using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

var fallos = 0;
await Ejecutar("Reservas usa rutas, métodos, DTO y autenticación de servicio", ReservasClienteRespetaContrato);
await Ejecutar("Reservas convierte errores HTTP y JSON inválido en resultados controlados", ReservasClienteManejaErroresHttp);
await Ejecutar("Reservas no envía solicitudes sin configuración válida", ReservasClienteExigeConfiguracion);
await Ejecutar("Reservas controla errores de conexión y timeout", ReservasClienteManejaErroresDeTransporte);
await Ejecutar("mesas lista y consulta por identificador mediante Reservas", MesasListaYConsulta);
await Ejecutar("mesas crea y edita con el contrato completo", MesasCreaYEdita);
await Ejecutar("mesas activa y desactiva mediante PUT con estado vigente", MesasCambiaEstadoMediantePut);
await Ejecutar("mesas rechaza datos inválidos y conserva el formulario", MesasRechazaDatosInvalidos);
await Ejecutar("mesas muestra errores 400, 404 y 409 sin perder los datos", MesasManejaErroresHttp);
await Ejecutar("mesas conserva la sesión ante errores 401 y 403 de Reservas", MesasConservaSesionAnteErroresDeReservas);
await Ejecutar("mesas permite consulta a los tres roles y limita modificaciones", MesasProtegePorRol);
await Ejecutar("reservas lista todas mediante GET", ReservasListaGeneral);
await Ejecutar("reservas busca por fecha mediante GET", ReservasBuscaPorFecha);
await Ejecutar("reservas consulta el detalle individual", ReservasConsultaIndividual);
await Ejecutar("reservas crea con el DTO del contrato", ReservasCreaConDtoCorrecto);
await Ejecutar("reservas edita con el DTO del contrato", ReservasEditaConDtoCorrecto);
await Ejecutar("reservas cambia estado mediante PATCH", ReservasCambiaEstado);
await Ejecutar("reservas cancela mediante PATCH sin DELETE", ReservasCancelaMedianteEstado);
await Ejecutar("reservas rechaza fechas pasadas", ReservasRechazaFechasPasadas);
await Ejecutar("reservas rechaza cantidades inválidas", ReservasRechazaCantidadesInvalidas);
await Ejecutar("reservas conserva el error de conflicto 409", ReservasManejaConflicto);
await Ejecutar("reservas presenta el error 404", ReservasManejaNoEncontrado);
await Ejecutar("reservas aplica la autorización por rol", ReservasProtegePorRol);
await Ejecutar("cocina no recibe datos personales de reservas", CocinaNoRecibeTelefonoReserva);
await Ejecutar("cocina no puede modificar reservas", CocinaNoPuedeModificarReservas);
await Ejecutar("reservas conserva la sesión ante errores 401 y 403", ReservasConservaSesionAnteErrores);
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
await Ejecutar("RequireRole conserva la sesión y responde 403 para rol no permitido", RequireRoleRechazaRolNoPermitido);
await Ejecutar("RequireRole redirige al login cuando falta la sesión", RequireRoleRedirigeSinSesion);

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

static async Task MesasListaYConsulta()
{
    const string mesaJson = """
        {"id":12,"numero":8,"capacidad":4,"activa":true,"fechaCreacion":"2026-01-02T03:04:05Z"}
        """;
    var rutas = new List<string>();
    var cliente = CrearClienteReservas((request, _) =>
    {
        rutas.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
        return Task.FromResult(JsonResponse(HttpStatusCode.OK, request.RequestUri.AbsolutePath switch
        {
            "/api/Mesas" => $"[{mesaJson}]",
            "/api/Mesas/12" => mesaJson,
            _ => throw new InvalidOperationException($"Ruta inesperada: {request.RequestUri}")
        }));
    });
    var controller = CrearMesasController(cliente);

    var listado = await controller.ListaMesas(CancellationToken.None);
    var listadoParcial = listado as PartialViewResult;
    var modeloListado = listadoParcial?.Model as MesasAdminViewModel;
    Verificar(listadoParcial?.ViewName == "_ListaMesas" && modeloListado?.Mesas.Count == 1,
        $"No se devolvió el listado deserializado de mesas ({listadoParcial?.ViewName}, {modeloListado?.Mensaje}, {modeloListado?.Mesas.Count}).");

    var detalle = await controller.Detalle(12, CancellationToken.None);
    var detalleParcial = detalle as PartialViewResult;
    var modeloDetalle = detalleParcial?.Model as MesaDetalleViewModel;
    Verificar(detalleParcial?.ViewName == "_DetalleMesa" && modeloDetalle?.Mesa?.Id == 12,
        $"No se devolvió el detalle deserializado de la mesa ({detalleParcial?.ViewName}, {modeloDetalle?.Mensaje}, {modeloDetalle?.Mesa?.Id}).");
    Verificar(rutas.SequenceEqual(["GET /api/Mesas", "GET /api/Mesas/12"]),
        "El listado o detalle no usó la ruta GET correspondiente.");
}

static async Task MesasCreaYEdita()
{
    const string mesaJson = """
        {"id":12,"numero":8,"capacidad":5,"activa":false,"fechaCreacion":"2026-01-02T03:04:05Z"}
        """;
    var solicitudes = new List<(HttpMethod Metodo, string Ruta, string Cuerpo)>();
    var cliente = CrearClienteReservas(async (request, cancellationToken) =>
    {
        var cuerpo = await request.Content!.ReadAsStringAsync(cancellationToken);
        solicitudes.Add((request.Method, request.RequestUri!.AbsolutePath, cuerpo));
        return JsonResponse(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK, mesaJson);
    });
    var controller = CrearMesasController(cliente);

    var crear = await controller.Crear(new MesaFormularioViewModel { Numero = 8, Capacidad = 4 }, CancellationToken.None);
    var editar = await controller.Editar(12, new MesaFormularioViewModel
    {
        Numero = 8,
        Capacidad = 5,
        Activa = false
    }, CancellationToken.None);

    Verificar(crear is RedirectToActionResult { ActionName: nameof(MesasController.Index) }
        && editar is RedirectToActionResult { ActionName: nameof(MesasController.Index) },
        "La creación o edición correcta no regresó al listado.");
    Verificar(solicitudes.Select(solicitud => (solicitud.Metodo, solicitud.Ruta))
        .SequenceEqual([(HttpMethod.Post, "/api/Mesas"), (HttpMethod.Put, "/api/Mesas/12")]),
        "La creación o edición no usó el método y la ruta del contrato.");
    using var createJson = JsonDocument.Parse(solicitudes[0].Cuerpo);
    Verificar(createJson.RootElement.EnumerateObject().Select(propiedad => propiedad.Name)
            .ToHashSet(StringComparer.Ordinal).SetEquals(["numero", "capacidad"])
        && createJson.RootElement.GetProperty("numero").GetInt32() == 8
        && createJson.RootElement.GetProperty("capacidad").GetInt32() == 4,
        "La creación envió campos extra o datos incorrectos.");
    using var updateJson = JsonDocument.Parse(solicitudes[1].Cuerpo);
    Verificar(updateJson.RootElement.EnumerateObject().Select(propiedad => propiedad.Name)
            .ToHashSet(StringComparer.Ordinal).SetEquals(["numero", "capacidad", "activa"])
        && updateJson.RootElement.GetProperty("numero").GetInt32() == 8
        && updateJson.RootElement.GetProperty("capacidad").GetInt32() == 5
        && !updateJson.RootElement.GetProperty("activa").GetBoolean(),
        "La edición no envió el DTO completo de actualización.");
}

static async Task MesasCambiaEstadoMediantePut()
{
    var activa = true;
    var metodos = new List<HttpMethod>();
    var estadosEnviados = new List<bool>();
    var cliente = CrearClienteReservas(async (request, cancellationToken) =>
    {
        metodos.Add(request.Method);
        if (request.Method == HttpMethod.Put)
        {
            var cuerpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var documento = JsonDocument.Parse(cuerpo);
            activa = documento.RootElement.GetProperty("activa").GetBoolean();
            estadosEnviados.Add(activa);
        }

        return JsonResponse(HttpStatusCode.OK,
            $$"""{"id":12,"numero":8,"capacidad":4,"activa":{{activa.ToString().ToLowerInvariant()}},"fechaCreacion":"2026-01-02T03:04:05Z"}""");
    });
    var controller = CrearMesasController(cliente);

    var desactivar = await controller.CambiarEstado(12, CancellationToken.None);
    var activar = await controller.CambiarEstado(12, CancellationToken.None);

    Verificar(desactivar is RedirectToActionResult && activar is RedirectToActionResult,
        "El cambio de estado no regresó al listado.");
    Verificar(metodos.SequenceEqual([HttpMethod.Get, HttpMethod.Put, HttpMethod.Get, HttpMethod.Put])
        && estadosEnviados.SequenceEqual([false, true]),
        "La activación o desactivación no consultó el estado vigente y usó PUT.");
    Verificar(controller.TempData["MensajeExito"] as string == "La mesa se activó correctamente.",
        "No se mostró la confirmación del último cambio de estado.");
}

static async Task MesasRechazaDatosInvalidos()
{
    var llamadasApi = 0;
    var cliente = CrearClienteReservas((_, _) =>
    {
        llamadasApi++;
        throw new InvalidOperationException("No se debía llamar a Reservas.");
    });
    var modelo = new MesaFormularioViewModel { Numero = 0, Capacidad = 0 };
    var validaciones = new List<ValidationResult>();
    var valido = Validator.TryValidateObject(
        modelo,
        new ValidationContext(modelo),
        validaciones,
        validateAllProperties: true);
    var controller = CrearMesasController(cliente);
    foreach (var validacion in validaciones)
    {
        foreach (var miembro in validacion.MemberNames)
        {
            controller.ModelState.AddModelError(miembro, validacion.ErrorMessage!);
        }
    }

    var resultado = await controller.Crear(modelo, CancellationToken.None);
    Verificar(!valido && validaciones.Count >= 2,
        "El número o capacidad igual a cero pasó la validación del modelo.");
    Verificar(resultado is ViewResult && llamadasApi == 0,
        "Se envió a la API un formulario inválido o no se conservó la vista.");
    Verificar(modelo.Numero == 0 && modelo.Capacidad == 0,
        "No se conservaron los datos ingresados al devolver el formulario inválido.");
}

static async Task MesasManejaErroresHttp()
{
    const string mensaje = "Mesa duplicada o no disponible.";
    var cliente = CrearClienteReservas((request, _) => Task.FromResult(request.Method switch
    {
        var metodo when metodo == HttpMethod.Post => JsonResponse(
            HttpStatusCode.BadRequest,
            """{"estado":400,"mensaje":"Número de mesa inválido.","fecha":"2026-11-18"}"""),
        var metodo when metodo == HttpMethod.Get => JsonResponse(
            HttpStatusCode.NotFound,
            """{"estado":404,"mensaje":"No existe la mesa.","fecha":"2026-11-18"}"""),
        _ => JsonResponse(
            HttpStatusCode.Conflict,
            $$"""{"estado":409,"mensaje":"{{mensaje}}","fecha":"2026-11-18"}""")
    }));
    var controller = CrearMesasController(cliente);

    var error400 = await controller.Crear(
        new MesaFormularioViewModel { Numero = 0, Capacidad = 2 }, CancellationToken.None);
    Verificar(error400 is ViewResult
        && controller.ModelState.Values.SelectMany(valor => valor.Errors)
            .Any(error => error.ErrorMessage == "Número de mesa inválido."),
        "No se mostró el mensaje de la API ante el error 400.");
    controller.ModelState.Clear();

    var error404 = await controller.Detalle(12, CancellationToken.None);
    Verificar(error404 is PartialViewResult
        {
            Model: MesaDetalleViewModel { Mensaje: "No existe la mesa." }
        },
        "No se mostró el mensaje de la API ante el error 404.");
    controller.ModelState.Clear();

    var error409 = await controller.Editar(12, new MesaFormularioViewModel
    {
        Numero = 8,
        Capacidad = 4,
        Activa = true
    }, CancellationToken.None);
    Verificar(error409 is ViewResult
        && controller.ModelState.Values.SelectMany(valor => valor.Errors)
            .Any(error => error.ErrorMessage == mensaje)
        && ((MesaFormularioViewModel)((ViewResult)error409).Model!).Numero == 8,
        "No se mostró el conflicto ni se conservaron los datos ante el error 409.");
}

static async Task MesasConservaSesionAnteErroresDeReservas()
{
    foreach (var estado in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
    {
        var mensaje = $"Reservas rechazó la solicitud ({(int)estado}).";
        var cliente = CrearClienteReservas((_, _) => Task.FromResult(JsonResponse(
            estado,
            $$"""{"estado":{{(int)estado}},"mensaje":"{{mensaje}}","fecha":"2026-11-18"}""")));
        var sesion = new TestSession(("Token", "jwt"), ("Rol", "ADMINISTRADOR"));
        var controller = new MesasController(cliente);
        PrepararController(controller, sesion);

        var resultado = await controller.Crear(
            new MesaFormularioViewModel { Numero = 8, Capacidad = 4 },
            CancellationToken.None);

        Verificar(resultado is ViewResult
            && controller.ModelState.Values.SelectMany(valor => valor.Errors)
                .Any(error => error.ErrorMessage == mensaje),
            $"No se mostró el error {((int)estado)} de Reservas.");
        Verificar(sesion.TryGetValue("Token", out _) && sesion.TryGetValue("Rol", out _),
            $"El error {((int)estado)} de Reservas borró la sesión Web.");
    }
}

static Task MesasProtegePorRol()
{
    var atributoControlador = (RequireRoleAttribute?)Attribute.GetCustomAttribute(
        typeof(MesasController),
        typeof(RequireRoleAttribute));
    Verificar(atributoControlador is not null,
        "El controlador de mesas no requiere un rol autenticado.");

    foreach (var rol in new[] { "ADMINISTRADOR", "CAJERO", "COCINA" })
    {
        var sesionConsulta = new TestSession(("Token", "jwt"), ("Rol", rol));
        var controllerConsulta = CrearMesasController(
            CrearClienteReservas((_, _) => throw new InvalidOperationException()), rol);
        var contextoConsulta = CrearFiltroContexto(controllerConsulta, sesionConsulta);
        atributoControlador!.OnActionExecuting(contextoConsulta);
        Verificar(contextoConsulta.Result is null,
            $"El rol {rol} no pudo consultar las mesas.");
    }

    var accionesDeModificacion = new[]
    {
        nameof(MesasController.Crear),
        nameof(MesasController.Editar),
        nameof(MesasController.CambiarEstado)
    };
    foreach (var accion in accionesDeModificacion)
    {
        var metodos = typeof(MesasController).GetMethods()
            .Where(metodo => metodo.Name == accion
                && Attribute.IsDefined(metodo, typeof(RequireRoleAttribute), inherit: false));
        foreach (var metodo in metodos)
        {
            var atributo = (RequireRoleAttribute?)Attribute.GetCustomAttribute(
                metodo,
                typeof(RequireRoleAttribute),
                inherit: false);
            Verificar(atributo is not null,
                $"La acción {accion} no tiene un filtro de rol propio.");
            if (Attribute.IsDefined(metodo, typeof(HttpPostAttribute), inherit: false))
            {
                Verificar(Attribute.IsDefined(metodo, typeof(ValidateAntiForgeryTokenAttribute), inherit: false),
                    $"La acción {accion} no exige antiforgery.");
            }

            foreach (var rol in new[] { "ADMINISTRADOR", "CAJERO", "COCINA" })
            {
                var sesion = new TestSession(("Token", "jwt"), ("Rol", rol));
                var controller = CrearMesasController(
                    CrearClienteReservas((_, _) => throw new InvalidOperationException()), rol);
                var contexto = CrearFiltroContexto(controller, sesion);
                atributo!.OnActionExecuting(contexto);
                Verificar(
                    rol == "ADMINISTRADOR"
                        ? contexto.Result is null
                        : contexto.Result is StatusCodeResult { StatusCode: StatusCodes.Status403Forbidden },
                    $"El rol {rol} tuvo acceso incorrecto a {accion}.");
                Verificar(sesion.TryGetValue("Token", out _) && sesion.TryGetValue("Rol", out _),
                    $"La acción {accion} alteró la sesión del rol {rol}.");
            }
        }
    }
    return Task.CompletedTask;
}

static async Task ReservasListaGeneral()
{
    var ruta = string.Empty;
    var cliente = CrearClienteReservas((request, _) =>
    {
        ruta = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        return Task.FromResult(JsonResponse(HttpStatusCode.OK, $"[{JsonReserva()}]"));
    });

    var resultado = await CrearReservasController(cliente).Lista(null, CancellationToken.None);

    Verificar(resultado is PartialViewResult { ViewName: "_ListaReservas", Model: ReservasListaViewModel { Reservas.Count: 1 } },
        "No se presentó el listado general de reservas.");
    Verificar(ruta == "GET /api/Reservas", "El listado general no usó GET /api/Reservas.");
}

static async Task ReservasBuscaPorFecha()
{
    var fecha = DateOnly.FromDateTime(DateTime.Today).AddDays(3);
    var ruta = string.Empty;
    var cliente = CrearClienteReservas((request, _) =>
    {
        ruta = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        return Task.FromResult(JsonResponse(HttpStatusCode.OK, $"[{JsonReserva(fecha)}]"));
    });

    var resultado = await CrearReservasController(cliente).Lista(fecha, CancellationToken.None);

    Verificar(resultado is PartialViewResult
        {
            Model: ReservasListaViewModel { FechaFiltro: var fechaFiltro, Reservas.Count: 1 }
        } && fechaFiltro == fecha,
        "No se presentó el listado filtrado por fecha.");
    Verificar(ruta == $"GET /api/Reservas/fecha/{fecha:yyyy-MM-dd}",
        "La búsqueda no usó GET /api/Reservas/fecha/{fecha}.");
}

static async Task ReservasConsultaIndividual()
{
    var ruta = string.Empty;
    var cliente = CrearClienteReservas((request, _) =>
    {
        ruta = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        return Task.FromResult(JsonResponse(HttpStatusCode.OK, JsonReserva()));
    });

    var resultado = await CrearReservasController(cliente).Detalle(42, CancellationToken.None);

    Verificar(resultado is ViewResult
        {
            Model: ReservaDetalleViewModel { Reserva: { Id: 42, NumeroMesa: 8 } }
        },
        "No se presentó la reserva individual.");
    Verificar(ruta == "GET /api/Reservas/42", "El detalle no usó GET /api/Reservas/{id}.");
}

static async Task ReservasCreaConDtoCorrecto()
{
    var solicitudes = new List<(HttpMethod Metodo, string Ruta, string Cuerpo)>();
    var cliente = CrearClienteReservas(async (request, cancellationToken) =>
    {
        var ruta = request.RequestUri!.AbsolutePath;
        if (ruta == "/api/Mesas")
        {
            return JsonResponse(HttpStatusCode.OK, JsonMesas());
        }

        var cuerpo = await request.Content!.ReadAsStringAsync(cancellationToken);
        solicitudes.Add((request.Method, ruta, cuerpo));
        return JsonResponse(HttpStatusCode.Created, JsonReserva());
    });
    var modelo = ReservaFormularioValido();
    var controller = CrearReservasController(cliente);

    var resultado = await controller.Crear(modelo, CancellationToken.None);

    Verificar(resultado is RedirectToActionResult { ActionName: nameof(ReservasController.Index) },
        "La creación correcta no regresó al listado.");
    Verificar(solicitudes.Count == 1
        && solicitudes[0].Metodo == HttpMethod.Post
        && solicitudes[0].Ruta == "/api/Reservas",
        "La creación no usó POST /api/Reservas.");
    using var documento = JsonDocument.Parse(solicitudes[0].Cuerpo);
    Verificar(documento.RootElement.EnumerateObject().Select(propiedad => propiedad.Name)
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals(["nombreCliente", "telefono", "fechaReserva", "horaReserva", "cantidadPersonas", "mesaId", "observaciones"])
        && documento.RootElement.GetProperty("nombreCliente").GetString() == modelo.NombreCliente
        && documento.RootElement.GetProperty("telefono").GetString() == modelo.Telefono
        && documento.RootElement.GetProperty("cantidadPersonas").GetInt32() == modelo.CantidadPersonas
        && documento.RootElement.GetProperty("mesaId").GetInt32() == modelo.MesaId,
        "La creación no serializó el DTO requerido.");
}

static async Task ReservasEditaConDtoCorrecto()
{
    var solicitudes = new List<(HttpMethod Metodo, string Ruta, string Cuerpo)>();
    var cliente = CrearClienteReservas(async (request, cancellationToken) =>
    {
        if (request.RequestUri!.AbsolutePath == "/api/Mesas")
        {
            return JsonResponse(HttpStatusCode.OK, JsonMesas());
        }

        var cuerpo = await request.Content!.ReadAsStringAsync(cancellationToken);
        solicitudes.Add((request.Method, request.RequestUri.AbsolutePath, cuerpo));
        return JsonResponse(HttpStatusCode.OK, JsonReserva());
    });
    var modelo = ReservaFormularioValido();
    var controller = CrearReservasController(cliente);

    var resultado = await controller.Editar(42, modelo, CancellationToken.None);

    Verificar(resultado is RedirectToActionResult { ActionName: nameof(ReservasController.Index) },
        "La edición correcta no regresó al listado.");
    Verificar(solicitudes.Count == 1
        && solicitudes[0].Metodo == HttpMethod.Put
        && solicitudes[0].Ruta == "/api/Reservas/42",
        "La edición no usó PUT /api/Reservas/{id}.");
    using var documento = JsonDocument.Parse(solicitudes[0].Cuerpo);
    Verificar(documento.RootElement.EnumerateObject().Select(propiedad => propiedad.Name)
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals(["nombreCliente", "telefono", "fechaReserva", "horaReserva", "cantidadPersonas", "mesaId", "observaciones"])
        && documento.RootElement.GetProperty("nombreCliente").GetString() == modelo.NombreCliente
        && documento.RootElement.GetProperty("mesaId").GetInt32() == modelo.MesaId,
        "La edición no serializó el DTO requerido.");
}

static async Task ReservasCambiaEstado()
{
    var solicitudes = new List<(HttpMethod Metodo, string Ruta, string? Cuerpo)>();
    var cliente = CrearClienteReservas(async (request, cancellationToken) =>
    {
        var cuerpo = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        solicitudes.Add((request.Method, request.RequestUri!.AbsolutePath, cuerpo));
        return JsonResponse(HttpStatusCode.OK, JsonReserva(estado: "CONFIRMADA"));
    });

    var resultado = await CrearReservasController(cliente).CambiarEstado(
        42,
        "CONFIRMADA",
        CancellationToken.None);

    Verificar(resultado is RedirectToActionResult,
        "El cambio de estado correcto no regresó al listado.");
    Verificar(solicitudes.SequenceEqual([
            (HttpMethod.Get, "/api/Reservas/42", (string?)null),
            (HttpMethod.Patch, "/api/Reservas/42/estado", solicitudes.Last().Cuerpo)
        ])
        && solicitudes[1].Cuerpo is not null,
        "El estado no se envió mediante PATCH.");
    using var documento = JsonDocument.Parse(solicitudes[1].Cuerpo!);
    Verificar(documento.RootElement.EnumerateObject().Select(propiedad => propiedad.Name)
            .SequenceEqual(["estado"])
        && documento.RootElement.GetProperty("estado").GetString() == "CONFIRMADA",
        "El PATCH no envió el JSON {\"estado\":\"CONFIRMADA\"}.");
}

static async Task ReservasCancelaMedianteEstado()
{
    var metodos = new List<HttpMethod>();
    string? estado = null;
    var cliente = CrearClienteReservas(async (request, cancellationToken) =>
    {
        metodos.Add(request.Method);
        if (request.Method == HttpMethod.Patch)
        {
            using var documento = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            estado = documento.RootElement.GetProperty("estado").GetString();
        }

        return JsonResponse(HttpStatusCode.OK, JsonReserva(estado: "CANCELADA"));
    });

    var resultado = await CrearReservasController(cliente).Cancelar(42, CancellationToken.None);

    Verificar(resultado is RedirectToActionResult
        && metodos.SequenceEqual([HttpMethod.Get, HttpMethod.Patch])
        && estado == "CANCELADA",
        "La cancelación no cambió el estado mediante PATCH sin DELETE.");
}

static async Task ReservasRechazaFechasPasadas()
{
    var llamadasCrear = 0;
    var cliente = CrearClienteReservas((request, _) => Task.FromResult(
        request.RequestUri!.AbsolutePath == "/api/Mesas"
            ? JsonResponse(HttpStatusCode.OK, JsonMesas())
            : (++llamadasCrear, JsonResponse(HttpStatusCode.Created, JsonReserva())).Item2));
    var modelo = ReservaFormularioValido();
    modelo.FechaReserva = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
    var controller = CrearReservasController(cliente);

    var resultado = await controller.Crear(modelo, CancellationToken.None);

    Verificar(resultado is ViewResult && llamadasCrear == 0
        && controller.ModelState.ContainsKey(nameof(ReservaFormularioViewModel.FechaReserva)),
        "Una fecha pasada llegó a la API o no se informó en el formulario.");
}

static async Task ReservasRechazaCantidadesInvalidas()
{
    var llamadasCrear = 0;
    var cliente = CrearClienteReservas((request, _) => Task.FromResult(
        request.RequestUri!.AbsolutePath == "/api/Mesas"
            ? JsonResponse(HttpStatusCode.OK, JsonMesas())
            : (++llamadasCrear, JsonResponse(HttpStatusCode.Created, JsonReserva())).Item2));
    var modelo = ReservaFormularioValido();
    modelo.CantidadPersonas = 0;
    var controller = CrearReservasController(cliente);
    AgregarValidaciones(controller, modelo);

    var resultado = await controller.Crear(modelo, CancellationToken.None);

    Verificar(resultado is ViewResult && llamadasCrear == 0
        && controller.ModelState.ContainsKey(nameof(ReservaFormularioViewModel.CantidadPersonas)),
        "Una cantidad inválida llegó a la API o no se informó en el formulario.");
}

static async Task ReservasManejaConflicto()
{
    var cliente = CrearClienteReservas((request, _) => Task.FromResult(
        request.RequestUri!.AbsolutePath == "/api/Mesas"
            ? JsonResponse(HttpStatusCode.OK, JsonMesas())
            : JsonResponse(HttpStatusCode.Conflict,
                """{"estado":409,"mensaje":"La mesa ya tiene una reserva en ese horario.","fecha":"2026-11-18"}""")));
    var modelo = ReservaFormularioValido();
    var controller = CrearReservasController(cliente);

    var resultado = await controller.Crear(modelo, CancellationToken.None);

    Verificar(resultado is ViewResult
        && controller.ModelState.Values.SelectMany(valor => valor.Errors)
            .Any(error => error.ErrorMessage == "La mesa ya tiene una reserva en ese horario.")
        && ReferenceEquals(((ViewResult)resultado).Model, modelo),
        "No se mostró el conflicto 409 o se perdieron los datos del formulario.");
}

static async Task ReservasManejaNoEncontrado()
{
    var cliente = CrearClienteReservas((_, _) => Task.FromResult(JsonResponse(
        HttpStatusCode.NotFound,
        """{"estado":404,"mensaje":"No existe la reserva solicitada.","fecha":"2026-11-18"}""")));

    var resultado = await CrearReservasController(cliente).Detalle(42, CancellationToken.None);

    Verificar(resultado is ViewResult
        {
            Model: ReservaDetalleViewModel { Mensaje: "No existe la reserva solicitada.", Reserva: null }
        },
        "No se presentó claramente el error 404.");
}

static Task ReservasProtegePorRol()
{
    var atributoControlador = (RequireRoleAttribute?)Attribute.GetCustomAttribute(
        typeof(ReservasController),
        typeof(RequireRoleAttribute));
    Verificar(atributoControlador is not null, "Reservas no requiere una sesión con rol autorizado.");

    foreach (var rol in new[] { "ADMINISTRADOR", "CAJERO", "COCINA" })
    {
        var controlador = CrearReservasController(
            CrearClienteReservas((_, _) => throw new InvalidOperationException()),
            rol);
        var contexto = CrearFiltroContexto(
            controlador,
            new TestSession(("Token", "jwt"), ("Rol", rol)));
        atributoControlador!.OnActionExecuting(contexto);
        Verificar(contexto.Result is null, $"El rol {rol} no puede consultar reservas.");
    }

    foreach (var accion in new[]
    {
        nameof(ReservasController.Crear),
        nameof(ReservasController.Editar),
        nameof(ReservasController.CambiarEstado),
        nameof(ReservasController.Cancelar),
        nameof(ReservasController.Atender)
    })
    {
        var metodos = typeof(ReservasController).GetMethods()
            .Where(metodo => metodo.Name == accion
                && Attribute.IsDefined(metodo, typeof(RequireRoleAttribute), inherit: false));
        Verificar(metodos.Any(), $"La acción {accion} no tiene autorización propia.");
        foreach (var metodo in metodos)
        {
            var atributo = (RequireRoleAttribute?)Attribute.GetCustomAttribute(
                metodo,
                typeof(RequireRoleAttribute),
                inherit: false);
            foreach (var rol in new[] { "ADMINISTRADOR", "CAJERO", "COCINA" })
            {
                var controlador = CrearReservasController(
                    CrearClienteReservas((_, _) => throw new InvalidOperationException()),
                    rol);
                var contexto = CrearFiltroContexto(
                    controlador,
                    new TestSession(("Token", "jwt"), ("Rol", rol)));
                atributo!.OnActionExecuting(contexto);
                Verificar(
                    (accion == nameof(ReservasController.Atender)
                        ? rol == "ADMINISTRADOR"
                        : rol is "ADMINISTRADOR" or "CAJERO")
                        ? contexto.Result is null
                        : contexto.Result is StatusCodeResult { StatusCode: StatusCodes.Status403Forbidden },
                    $"El rol {rol} tiene permisos incorrectos en {accion}.");
            }
        }
    }

    foreach (var metodo in typeof(ReservasController).GetMethods()
        .Where(metodo => metodo.GetParameters().Any(parametro => parametro.ParameterType == typeof(ReservaFormularioViewModel))))
    {
        Verificar(Attribute.IsDefined(metodo, typeof(ValidateAntiForgeryTokenAttribute), inherit: false),
            $"La acción {metodo.Name} no exige antiforgery.");
    }
    return Task.CompletedTask;
}

static async Task CocinaNoRecibeTelefonoReserva()
{
    var cliente = CrearClienteReservas((_, _) =>
        Task.FromResult(JsonResponse(HttpStatusCode.OK, $"[{JsonReserva()}]")));

    var resultado = await CrearReservasController(cliente, "COCINA").Lista(null, CancellationToken.None);
    var modelo = ((PartialViewResult)resultado).Model as ReservasListaViewModel;
    var reserva = modelo?.Reservas.Single();

    Verificar(!modelo!.MostrarDatosCliente
        && reserva?.NombreCliente is null
        && reserva?.Telefono is null,
        "El modelo entregado a COCINA contiene datos personales del cliente.");
}

static Task CocinaNoPuedeModificarReservas()
{
    var llamadasApi = 0;
    var cliente = CrearClienteReservas((_, _) =>
    {
        llamadasApi++;
        throw new InvalidOperationException("COCINA no debe llegar a la API al modificar reservas.");
    });
    foreach (var accion in new[]
    {
        nameof(ReservasController.Crear),
        nameof(ReservasController.Editar),
        nameof(ReservasController.CambiarEstado),
        nameof(ReservasController.Cancelar),
        nameof(ReservasController.Atender)
    })
    {
        var metodos = typeof(ReservasController).GetMethods()
            .Where(metodo => metodo.Name == accion
                && Attribute.IsDefined(metodo, typeof(RequireRoleAttribute), inherit: false));
        foreach (var metodo in metodos)
        {
            var atributo = (RequireRoleAttribute?)Attribute.GetCustomAttribute(
                metodo,
                typeof(RequireRoleAttribute),
                inherit: false);
            var controlador = CrearReservasController(cliente, "COCINA");
            var contexto = CrearFiltroContexto(
                controlador,
                new TestSession(("Token", "jwt"), ("Rol", "COCINA")));
            atributo!.OnActionExecuting(contexto);
            Verificar(contexto.Result is StatusCodeResult { StatusCode: StatusCodes.Status403Forbidden },
                $"COCINA pudo continuar a la acción {accion}.");
        }
    }

    Verificar(llamadasApi == 0, "COCINA provocó una llamada de modificación a Reservas.");
    return Task.CompletedTask;
}

static async Task ReservasConservaSesionAnteErrores()
{
    foreach (var estado in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
    {
        const string mensaje = "Reservas rechazó la operación.";
        var cliente = CrearClienteReservas((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/api/Mesas"
                ? JsonResponse(HttpStatusCode.OK, JsonMesas())
                : JsonResponse(estado, $$"""{"estado":{{(int)estado}},"mensaje":"{{mensaje}}","fecha":"2026-11-18"}""")));
        var sesion = new TestSession(("Token", "jwt"), ("Rol", "ADMINISTRADOR"));
        var controlador = CrearReservasControllerConSesion(cliente, sesion);
        var resultado = await controlador.Crear(ReservaFormularioValido(), CancellationToken.None);

        Verificar(resultado is ViewResult
            && controlador.ModelState.Values.SelectMany(valor => valor.Errors)
                .Any(error => error.ErrorMessage == mensaje),
            $"No se mostró el error {((int)estado)} de Reservas.");
        Verificar(sesion.TryGetValue("Token", out _) && sesion.TryGetValue("Rol", out _),
            $"El error {((int)estado)} de Reservas borró la sesión Web.");
    }
}

static async Task ReservasClienteRespetaContrato()
{
    const string clave = "clave-prueba-reservas-0123456789-ABC";
    const string mesaJson = """
        {"id":12,"numero":8,"capacidad":4,"activa":true,"fechaCreacion":"2026-01-02T03:04:05Z"}
        """;
    const string reservaJson = """
        {"id":20,"nombreCliente":"Ana","telefono":"5551234","fechaReserva":"2026-03-04",
         "horaReserva":"19:30:00","cantidadPersonas":3,"mesaId":12,"numeroMesa":8,
         "estado":"PENDIENTE","observaciones":"Ventana","fechaCreacion":"2026-01-02T03:04:05Z"}
        """;
    var solicitudes = new List<(HttpMethod Metodo, string Ruta, string? Cuerpo, string? Clave, string? Autorizacion)>();
    var cliente = CrearClienteReservas(async (request, cancellationToken) =>
    {
        var cuerpo = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var claveRecibida = request.Headers.TryGetValues("X-Reservas-Service-Key", out var valoresClave)
            ? valoresClave.Single()
            : null;
        var autorizacion = request.Headers.Authorization?.ToString();
        solicitudes.Add((request.Method, request.RequestUri!.AbsolutePath, cuerpo, claveRecibida, autorizacion));

        var json = request.RequestUri.AbsolutePath switch
        {
            "/api/Mesas" when request.Method == HttpMethod.Get => "[]",
            "/api/Reservas" when request.Method == HttpMethod.Get => "[]",
            "/api/Reservas/fecha/2026-03-04" => $"[{reservaJson}]",
            "/api/Mesas/12" or "/api/Mesas" => mesaJson,
            "/api/Reservas/20/estado" or "/api/Reservas/20" or "/api/Reservas" => reservaJson,
            _ => throw new InvalidOperationException($"Ruta inesperada: {request.RequestUri}")
        };
        return JsonResponse(
            request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK,
            json);
    });

    var fecha = new DateOnly(2026, 3, 4);
    var mesaCreada = await cliente.CrearMesaAsync(new MesaCreate { Numero = 8, Capacidad = 4 });
    var mesaConsultada = await cliente.ObtenerMesaAsync(12);
    var mesas = await cliente.ObtenerMesasAsync();
    var mesaActualizada = await cliente.ActualizarMesaAsync(
        12,
        new MesaUpdate { Numero = 8, Capacidad = 5, Activa = false });
    var reservaCreada = await cliente.CrearReservaAsync(new ReservaCreate
    {
        NombreCliente = "Ana",
        Telefono = "5551234",
        FechaReserva = fecha,
        HoraReserva = new TimeOnly(19, 30),
        CantidadPersonas = 3,
        MesaId = 12,
        Observaciones = "Ventana"
    });
    var reservaConsultada = await cliente.ObtenerReservaAsync(20);
    var reservas = await cliente.ObtenerReservasAsync();
    var reservasFecha = await cliente.ObtenerReservasPorFechaAsync(fecha);
    var reservaActualizada = await cliente.ActualizarReservaAsync(20, new ReservaUpdate
    {
        NombreCliente = "Ana",
        Telefono = "5551234",
        FechaReserva = fecha,
        HoraReserva = new TimeOnly(19, 30),
        CantidadPersonas = 3,
        MesaId = 12,
        Observaciones = "Ventana"
    });
    var estadoActualizado = await cliente.CambiarEstadoReservaAsync(
        20,
        new ReservaEstado { Estado = "CONFIRMADA" });

    Verificar(
        new[]
        {
            mesaCreada.IsSuccess, mesaConsultada.IsSuccess, mesas.IsSuccess, mesaActualizada.IsSuccess,
            reservaCreada.IsSuccess, reservaConsultada.IsSuccess, reservas.IsSuccess,
            reservasFecha.IsSuccess, reservaActualizada.IsSuccess, estadoActualizado.IsSuccess
        }.All(exito => exito),
        "Una operación tipada de Reservas no se deserializó correctamente.");
    Verificar(mesaConsultada.Value?.FechaCreacion
        == new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        "No se deserializó la fecha de creación de la mesa.");
    Verificar(reservaConsultada.Value?.FechaReserva == fecha
        && reservaConsultada.Value.HoraReserva == new TimeOnly(19, 30),
        "No se deserializaron DateOnly y TimeOnly de la reserva.");
    Verificar(reservasFecha.Value?.Single().FechaReserva == fecha,
        "La búsqueda por fecha no devolvió la reserva esperada.");

    var rutasEsperadas = new[]
    {
        (HttpMethod.Post, "/api/Mesas"),
        (HttpMethod.Get, "/api/Mesas/12"),
        (HttpMethod.Get, "/api/Mesas"),
        (HttpMethod.Put, "/api/Mesas/12"),
        (HttpMethod.Post, "/api/Reservas"),
        (HttpMethod.Get, "/api/Reservas/20"),
        (HttpMethod.Get, "/api/Reservas"),
        (HttpMethod.Get, "/api/Reservas/fecha/2026-03-04"),
        (HttpMethod.Put, "/api/Reservas/20"),
        (HttpMethod.Patch, "/api/Reservas/20/estado")
    };
    Verificar(solicitudes.Count == rutasEsperadas.Length,
        "Se envió una cantidad inesperada de solicitudes a Reservas.");
    for (var indice = 0; indice < rutasEsperadas.Length; indice++)
    {
        Verificar(solicitudes[indice].Metodo == rutasEsperadas[indice].Item1
            && solicitudes[indice].Ruta == rutasEsperadas[indice].Item2,
            $"Método o ruta incorrecta en la solicitud {indice + 1}.");
        Verificar(solicitudes[indice].Clave == clave,
            "Faltó la clave de servicio en una solicitud a Reservas.");
        Verificar(solicitudes[indice].Autorizacion is null,
            "Se envió un encabezado Authorization a Reservas.");
    }

    using var mesaCreateJson = JsonDocument.Parse(solicitudes[0].Cuerpo!);
    Verificar(mesaCreateJson.RootElement.GetProperty("numero").GetInt32() == 8
        && mesaCreateJson.RootElement.GetProperty("capacidad").GetInt32() == 4,
        "MesaCreate no se serializó con los nombres y valores del contrato.");
    using var mesaUpdateJson = JsonDocument.Parse(solicitudes[3].Cuerpo!);
    Verificar(!mesaUpdateJson.RootElement.GetProperty("activa").GetBoolean(),
        "MesaUpdate no serializó el campo activa.");
    using var reservaCreateJson = JsonDocument.Parse(solicitudes[4].Cuerpo!);
    Verificar(reservaCreateJson.RootElement.GetProperty("fechaReserva").GetString() == "2026-03-04"
        && reservaCreateJson.RootElement.GetProperty("mesaId").GetInt32() == 12,
        "ReservaCreate no serializó correctamente la fecha o mesa.");
    Verificar(reservaCreateJson.RootElement.GetProperty("horaReserva").GetString()!
        .StartsWith("19:30:00", StringComparison.Ordinal),
        "ReservaCreate no serializó TimeOnly en formato de hora ISO.");
    using var reservaUpdateJson = JsonDocument.Parse(solicitudes[8].Cuerpo!);
    Verificar(reservaUpdateJson.RootElement.GetProperty("cantidadPersonas").GetInt32() == 3
        && reservaUpdateJson.RootElement.GetProperty("observaciones").GetString() == "Ventana",
        "ReservaUpdate no se serializó con los campos del contrato.");
    using var estadoJson = JsonDocument.Parse(solicitudes[9].Cuerpo!);
    Verificar(estadoJson.RootElement.GetProperty("estado").GetString() == "CONFIRMADA",
        "ReservaEstado no se serializó correctamente.");
    Verificar(!solicitudes.Any(solicitud => solicitud.Metodo == HttpMethod.Delete),
        "Se detectó una ruta DELETE no incluida en el contrato.");
    Verificar(!string.Join(" ", mesaCreada.Mensaje, reservaCreada.Mensaje).Contains(clave, StringComparison.Ordinal),
        "La clave de servicio se expuso en un resultado.");
}

static async Task ReservasClienteManejaErroresHttp()
{
    var estados = new[]
    {
        HttpStatusCode.BadRequest,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden,
        HttpStatusCode.NotFound,
        HttpStatusCode.Conflict,
        HttpStatusCode.InternalServerError
    };

    foreach (var estado in estados)
    {
        const string mensaje = "Error controlado de prueba.";
        var cliente = CrearClienteReservas((_, _) => Task.FromResult(JsonResponse(
            estado,
            $$"""{"estado":{{(int)estado}},"mensaje":"{{mensaje}}","fecha":"2026-03-04"}""")));
        var resultado = await cliente.ObtenerMesaAsync(12);

        Verificar(resultado.StatusCode == estado && resultado.Mensaje == mensaje,
            $"No se preservó el estado o mensaje HTTP {((int)estado)}.");
    }

    var jsonInvalido = CrearClienteReservas((_, _) =>
        Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"id":""")));
    var respuestaInvalida = await jsonInvalido.ObtenerMesaAsync(12);
    Verificar(respuestaInvalida.Failure == ReservasApiFailure.InvalidJson
        && respuestaInvalida.StatusCode == HttpStatusCode.OK,
        "No se manejó el JSON inválido como error controlado.");

    var errorJsonInvalido = CrearClienteReservas((_, _) =>
        Task.FromResult(JsonResponse(HttpStatusCode.Conflict, "no-json")));
    var conflicto = await errorJsonInvalido.ObtenerMesaAsync(12);
    Verificar(conflicto.StatusCode == HttpStatusCode.Conflict
        && conflicto.Failure == ReservasApiFailure.InvalidJson
        && !string.IsNullOrWhiteSpace(conflicto.Mensaje),
        "No se preservó el conflicto cuando el cuerpo de error no era JSON válido.");
}

static async Task ReservasClienteExigeConfiguracion()
{
    var solicitudes = 0;
    var opcionesSinConfigurar = ReservasApiOptions.Load(new ConfigurationBuilder().Build(), true);
    var clienteSinConfigurar = CrearClienteReservas((_, _) =>
    {
        solicitudes++;
        throw new InvalidOperationException("No se debía enviar una solicitud.");
    }, opcionesSinConfigurar);
    var resultado = await clienteSinConfigurar.ObtenerMesasAsync();

    Verificar(!opcionesSinConfigurar.IsConfigured
        && resultado.Failure == ReservasApiFailure.Configuration
        && solicitudes == 0,
        "Se envió una solicitud o no se informó la configuración faltante.");

    const string clave = "clave-prueba-reservas-0123456789-ABC";
    Verificar(CrearOpcionesReservas("http://localhost:5000", clave, true).IsConfigured,
        "Se rechazó HTTP en localhost durante el desarrollo.");
    Verificar(!CrearOpcionesReservas("http://reservas.example.test", clave, true).IsConfigured,
        "Se permitió HTTP hacia un servidor remoto durante el desarrollo.");

    var errorProduccion = false;
    try
    {
        _ = ReservasApiOptions.Load(new ConfigurationBuilder().Build(), false);
    }
    catch (InvalidOperationException)
    {
        errorProduccion = true;
    }

    Verificar(errorProduccion, "La aplicación no rechazó la configuración ausente fuera de desarrollo.");
    var errorHttpProduccion = false;
    try
    {
        _ = CrearOpcionesReservas("http://localhost:5000", clave, false);
    }
    catch (InvalidOperationException)
    {
        errorHttpProduccion = true;
    }

    Verificar(errorHttpProduccion, "Se permitió HTTP en producción.");
    Verificar(!resultado.Mensaje!.Contains(clave, StringComparison.Ordinal),
        "La clave apareció en el error de configuración.");
}

static async Task ReservasClienteManejaErroresDeTransporte()
{
    var errorConexion = CrearClienteReservas((_, _) =>
        Task.FromException<HttpResponseMessage>(new HttpRequestException("detalle interno")));
    var resultadoConexion = await errorConexion.ObtenerMesasAsync();
    Verificar(resultadoConexion.Failure == ReservasApiFailure.Connection
        && !resultadoConexion.Mensaje!.Contains("detalle interno", StringComparison.Ordinal),
        "El error de conexión no se controló o expuso detalles internos.");

    var errorLectura = CrearClienteReservas((_, _) =>
        Task.FromException<HttpResponseMessage>(new IOException("detalle interno")));
    var resultadoLectura = await errorLectura.ObtenerMesasAsync();
    Verificar(resultadoLectura.Failure == ReservasApiFailure.Connection,
        "No se controló el error de lectura de la conexión.");

    var timeout = CrearClienteReservas((_, _) =>
        Task.FromException<HttpResponseMessage>(new TaskCanceledException("detalle interno")));
    var resultadoTimeout = await timeout.ObtenerMesasAsync();
    Verificar(resultadoTimeout.Failure == ReservasApiFailure.Timeout
        && !resultadoTimeout.Mensaje!.Contains("detalle interno", StringComparison.Ordinal),
        "El timeout no se controló o expuso detalles internos.");
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

    Verificar(context.Result is StatusCodeResult { StatusCode: StatusCodes.Status403Forbidden },
        "Un usuario que no es administrador no recibió un 403.");
    Verificar(session.TryGetValue("Token", out _)
        && session.TryGetValue("Rol", out _),
        "Se perdió la sesión del usuario autenticado no autorizado.");
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

    Verificar(context.Result is StatusCodeResult { StatusCode: StatusCodes.Status403Forbidden },
        "No se respondió 403 para el rol no permitido.");
    Verificar(sesion.TryGetValue("Token", out _) && sesion.TryGetValue("Rol", out _),
        "Se limpió la sesión del usuario autenticado.");
    return Task.CompletedTask;
}

static Task RequireRoleRedirigeSinSesion()
{
    var sesion = new TestSession();
    var controller = new CajaController(CrearCliente((_, _) =>
        throw new InvalidOperationException("No se esperaba una llamada a la API.")));
    var context = CrearFiltroContexto(controller, sesion);

    new RequireRoleAttribute("CAJERO", "ADMINISTRADOR").OnActionExecuting(context);

    Verificar(context.Result is RedirectToActionResult
        {
            ActionName: "Login",
            ControllerName: "Account"
        },
        "Una solicitud sin sesión no se redirigió al login.");

    var sesionInvalida = new TestSession(("Token", "jwt"), ("Rol", "DESCONOCIDO"));
    var contextoSesionInvalida = CrearFiltroContexto(controller, sesionInvalida);
    new RequireRoleAttribute("CAJERO", "ADMINISTRADOR").OnActionExecuting(contextoSesionInvalida);
    Verificar(contextoSesionInvalida.Result is RedirectToActionResult
        && !sesionInvalida.TryGetValue("Token", out _),
        "Una sesión con rol inválido no se limpió ni redirigió al login.");
    return Task.CompletedTask;
}

static ReservasApiClient CrearClienteReservas(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
    ReservasApiOptions? options = null)
{
    return new ReservasApiClient(
        new HttpClient(new StubHttpMessageHandler(responder)),
        options ?? CrearOpcionesReservas());
}

static ReservasApiOptions CrearOpcionesReservas(
    string baseUrl = "https://reservas.test",
    string serviceApiKey = "clave-prueba-reservas-0123456789-ABC",
    bool isDevelopment = true)
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservasApi:BaseUrl"] = baseUrl,
            ["ReservasApi:ServiceApiKey"] = serviceApiKey
        })
        .Build();
    return ReservasApiOptions.Load(configuration, isDevelopment);
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

static MesasController CrearMesasController(
    ReservasApiClient cliente,
    string rol = "ADMINISTRADOR")
{
    var controller = new MesasController(cliente);
    PrepararController(controller, new TestSession(("Token", "jwt"), ("Rol", rol)));
    return controller;
}

static ReservasController CrearReservasController(
    ReservasApiClient cliente,
    string rol = "ADMINISTRADOR") =>
    CrearReservasControllerConSesion(
        cliente,
        new TestSession(("Token", "jwt"), ("Rol", rol)));

static ReservasController CrearReservasControllerConSesion(
    ReservasApiClient cliente,
    ISession session)
{
    var controller = new ReservasController(cliente);
    PrepararController(controller, session);
    return controller;
}

static ReservaFormularioViewModel ReservaFormularioValido()
{
    return new ReservaFormularioViewModel
    {
        NombreCliente = "Ana Pérez",
        Telefono = "5551234",
        FechaReserva = DateOnly.FromDateTime(DateTime.Today).AddDays(3),
        HoraReserva = new TimeOnly(19, 30),
        CantidadPersonas = 3,
        MesaId = 12,
        Observaciones = "Silla infantil"
    };
}

static string JsonReserva(DateOnly? fecha = null, string estado = "PENDIENTE") =>
    JsonSerializer.Serialize(new
    {
        id = 42,
        nombreCliente = "Ana Pérez",
        telefono = "5551234",
        fechaReserva = fecha ?? DateOnly.FromDateTime(DateTime.Today).AddDays(3),
        horaReserva = new TimeOnly(19, 30),
        cantidadPersonas = 3,
        mesaId = 12,
        numeroMesa = 8,
        estado,
        observaciones = "Silla infantil",
        fechaCreacion = DateTimeOffset.UtcNow
    });

static string JsonMesas() => """
    [{"id":12,"numero":8,"capacidad":4,"activa":true,"fechaCreacion":"2026-01-02T03:04:05Z"}]
    """;

static void AgregarValidaciones(Controller controller, object modelo)
{
    var validaciones = new List<ValidationResult>();
    Validator.TryValidateObject(
        modelo,
        new ValidationContext(modelo),
        validaciones,
        validateAllProperties: true);
    foreach (var validacion in validaciones)
    {
        foreach (var miembro in validacion.MemberNames)
        {
            controller.ModelState.AddModelError(miembro, validacion.ErrorMessage!);
        }
    }
}

static void PrepararController(Controller controller, ISession session)
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { Session = session }
    };
    controller.ViewData = new ViewDataDictionary(
        new EmptyModelMetadataProvider(),
        controller.ModelState);
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
