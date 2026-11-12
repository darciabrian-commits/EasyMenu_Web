using EasyMenu.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Web.Controllers;

[RequireRole("ADMINISTRADOR")]
public sealed class AdminController : Controller
{
    public IActionResult Index() => View();
}
