using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EasyMenu.Web.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute(params string[] rolesPermitidos) : ActionFilterAttribute
{
    private static readonly HashSet<string> RolesReconocidos = new(StringComparer.Ordinal)
    {
        "ADMINISTRADOR",
        "CAJERO",
        "COCINA"
    };

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var sesion = context.HttpContext.Session;
        var token = sesion.GetString("Token");
        var rol = sesion.GetString("Rol");

        if (string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(rol)
            || !RolesReconocidos.Contains(rol))
        {
            sesion.Clear();
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }

        if (!rolesPermitidos.Contains(rol, StringComparer.Ordinal))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
