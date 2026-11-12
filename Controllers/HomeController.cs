using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using EasyMenu.Web.Models;

namespace EasyMenu.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return HttpContext.Session.GetString("Rol") switch
        {
            "ADMINISTRADOR" => RedirectToAction("Index", "Admin"),
            "CAJERO" => RedirectToAction("Index", "Caja"),
            "COCINA" => RedirectToAction("Index", "Cocina"),
            _ => RedirectToAction("Login", "Account")
        };
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
