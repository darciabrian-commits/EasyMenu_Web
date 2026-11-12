using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Mvc;

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
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        LoginResponse? resultado;
        try
        {
            resultado = await apiClient.IniciarSesionAsync(model.Correo, model.Clave, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No fue posible conectar con el servidor. Inténtalo nuevamente.");
            return View(model);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "El servidor tardó demasiado en responder. Inténtalo nuevamente.");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(resultado?.Token) || string.IsNullOrWhiteSpace(resultado.Rol))
        {
            ViewBag.ErrorLogin = "Correo o clave incorrectos";
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
            ViewBag.ErrorLogin = "Correo o clave incorrectos";
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
