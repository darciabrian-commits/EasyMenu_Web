using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EasyMenu.Web.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute(string rolRequerido) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var sesion = context.HttpContext.Session;
        var token = sesion.GetString("Token");
        var rol = sesion.GetString("Rol");

        if (string.IsNullOrEmpty(token))
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }

        if (!string.Equals(rol, rolRequerido, StringComparison.Ordinal))
        {
            context.Result = new RedirectToActionResult(
                "Login",
                "Account",
                new { aviso = "No tienes permiso para acceder a esta pantalla." });
        }
    }
}
