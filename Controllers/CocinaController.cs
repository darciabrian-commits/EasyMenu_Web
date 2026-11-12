using EasyMenu.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Web.Controllers;

[RequireRole("COCINA")]
public sealed class CocinaController : Controller
{
    public IActionResult Index() => View();
}
