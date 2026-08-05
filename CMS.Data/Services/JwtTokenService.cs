using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CMS.Data.Services
{
    public class JwtTokenService
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expirationMinutes;

        public JwtTokenService(IConfiguration config)
        {
            _secretKey = config["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JwtSettings:SecretKey no configurada");
            _issuer = config["JwtSettings:Issuer"] ?? "CMS.API";
            _audience = config["JwtSettings:Audience"] ?? "CMS.UI";
            _expirationMinutes = int.Parse(config["JwtSettings:ExpirationMinutes"] ?? "60");
        }

        /// <summary>
        /// Genera un token JWT con roles y permisos
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="userName">Nombre de usuario</param>
        /// <param name="email">Email</param>
        /// <param name="roles">Lista de roles (ej: "Admin", "User")</param>
        /// <param name="permissions">Lista de permisos</param>
        /// <param name="companyId">ID de compañía opcional</param>
        public string GenerateToken(int userId, string userName, string email, List<string> roles, List<string> permissions, int? companyId = null)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, userName),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("userId", userId.ToString()),
                // cms_username se usa en auditoría (created_by / updated_by)
                new Claim("cms_username", userName)
            };

            // Agregar companyId si se proporciona (para auth local)
            if (companyId.HasValue)
            {
                claims.Add(new Claim("companyId", companyId.Value.ToString()));
            }

            // ⭐ Agregar roles como claims (necesario para [Authorize(Roles = "...")]
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Agregar permisos como claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Genera un token JWT (sobrecarga para compatibilidad - solo permisos, sin roles explícitos)
        /// </summary>
        [Obsolete("Usar la sobrecarga que acepta roles explícitamente")]
        public string GenerateToken(int userId, string userName, string email, List<string> permissions, int? companyId = null)
        {
            return GenerateToken(userId, userName, email, new List<string>(), permissions, companyId);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = ClaimTypes.Role // ⭐ Asegurar que use ClaimTypes.Role
                }, out SecurityToken validatedToken);

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}