using EasyMenu.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Web.Controllers;

[RequireRole("CAJERO")]
public sealed class CajaController : Controller
{
    public IActionResult Index() => View();
}
