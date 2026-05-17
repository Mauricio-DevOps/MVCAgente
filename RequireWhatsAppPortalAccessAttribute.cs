using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mvc;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireWhatsAppPortalAccessAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || PortalAccess.HasWhatsAppAccess(user))
        {
            return;
        }

        context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
