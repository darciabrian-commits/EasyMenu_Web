using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EasyMenu.Web.Controllers;

public sealed class AccountController(EasyMenuApiClient apiClient) : Controller
{
    [HttpGet]
    public IActionResult Login(string? aviso)
    {
        ViewBag.Aviso = aviso;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        HttpContext.Session.Clear();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        LoginResponse? resultado;
        try
        {
            resultado = await apiClient.IniciarSesionAsync(model.Correo, model.Clave, cancellationToken);
        }
        catch (HttpRequestException excepcion) when (
            excepcion.StatusCode is { } estado && (int)estado >= 500)
        {
            ModelState.AddModelError(string.Empty, "El servidor de autenticación reportó un error. Inténtalo nuevamente.");
            return View(model);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No fue posible completar el inicio de sesión. Inténtalo nuevamente.");
            return View(model);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "El servidor tardó demasiado en responder. Inténtalo nuevamente.");
            return View(model);
        }
        catch (JsonException)
        {
            ModelState.AddModelError(string.Empty, "La respuesta del servidor no tiene un formato válido.");
            return View(model);
        }

        if (resultado is null)
        {
            ViewBag.ErrorLogin = "Correo o clave incorrectos";
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(resultado.Token)
            || !string.Equals(resultado.Tipo, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(resultado.Rol))
        {
            ModelState.AddModelError(string.Empty, "La respuesta del servidor no contiene datos de sesión válidos.");
            return View(model);
        }

        var destino = resultado.Rol switch
        {
            "ADMINISTRADOR" => "Admin",
            "CAJERO" => "Caja",
            "COCINA" => "Cocina",
            _ => null
        };

        if (destino is null)
        {
            ViewBag.ErrorLogin = "El servidor devolvió un rol no válido.";
            return View(model);
        }

        HttpContext.Session.SetString("Token", resultado.Token);
        HttpContext.Session.SetString("Rol", resultado.Rol);
        return RedirectToAction("Index", destino);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }
}
