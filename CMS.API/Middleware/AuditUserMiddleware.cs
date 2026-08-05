using System.Security.Claims;
using CMS.Data;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Middleware
{
    public class AuditUserMiddleware
    {
        private readonly RequestDelegate _next;

        public AuditUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext db)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var identity = context.User.Identity as ClaimsIdentity;

                // Si el token ya trae cms_username (login local o token propio), no hace falta consultar la BD
                if (!identity!.HasClaim(c => c.Type == "cms_username"))
                {
                    // Intentar por OID (Azure AD)
                    var oid = context.User.FindFirst("oid")?.Value;
                    if (!string.IsNullOrEmpty(oid) && Guid.TryParse(oid, out var azureOid))
                    {
                        var user = await db.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.AZURE_OID == azureOid);

                        if (user != null)
                        {
                            identity.AddClaim(new Claim("cms_username", user.USER_NAME));
                            identity.AddClaim(new Claim("cms_user_id", user.ID_USER.ToString()));
                        }
                    }
                    else
                    {
                        // Intentar por userId (token local sin cms_username — tokens anteriores al fix)
                        var userIdVal = context.User.FindFirst("userId")?.Value;
                        if (!string.IsNullOrEmpty(userIdVal) && int.TryParse(userIdVal, out var userId))
                        {
                            var user = await db.Users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.ID_USER == userId);

                            if (user != null)
                            {
                                identity.AddClaim(new Claim("cms_username", user.USER_NAME));
                                identity.AddClaim(new Claim("cms_user_id", user.ID_USER.ToString()));
                            }
                        }
                    }
                }
            }

            await _next(context);
        }
    }
}
