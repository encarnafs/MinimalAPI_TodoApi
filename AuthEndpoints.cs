using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace TodoApi;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        //ENDPOINT LOGIN
        app.MapPost("/login", async (LoginRequest user, IConfiguration config, HttpContext context, TodoDb db) =>
        {
            if (user.Username != "admin" || user.Password != "12345")
            {
                return Results.Unauthorized();
            }

            var secretKey = config["Jwt:Key"];

            if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
            {
                return Results.Problem("La clave JWT no es válida o es demasiado corta");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = credentials,
                Issuer = config["Authentication:Schemes:Bearer:ValidIssuer"],
                Audience = config["Authentication:Schemes:Bearer:ValidAudiences:0"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var stringToken = tokenHandler.WriteToken(token);

            ///
            var refreshToken = Guid.NewGuid().ToString();

            //Guardar en  TodoDb InMemory
            db.RefreshTokens.Add(new UserRefreshToken
            {
                Username = user.Username,
                Token = refreshToken,
                ExpiryDate = DateTime.UtcNow.AddDays(7) // El refresh dura mucho más
            });
            await db.SaveChangesAsync();
            ///

            context.Response.Cookies.Append("X-Access-Token", stringToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // false si usas HTTP en localhost
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddMinutes(15)
            });

            //Envio cookie al navegador
            context.Response.Cookies.Append("X-Refresh-Token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return Results.Ok(new { Message = "Autenticación satisfactoria" });
        });


        // ENDPOINT REFRESH (Rotación de Token)
        app.MapPost("/refresh", async (HttpContext context, TodoDb db, IConfiguration config) =>
        {
            // 1. Extraer el Refresh Token de la cookie
            var refreshTokenCookie = context.Request.Cookies["X-Refresh-Token"];
            if (string.IsNullOrEmpty(refreshTokenCookie)) return Results.Unauthorized();

            // 2. Buscarlo en la base de datos y verificar que no haya expirado
            var storedToken = await db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshTokenCookie && t.ExpiryDate > DateTime.UtcNow);

            if (storedToken == null) return Results.Unauthorized();

            // 3. --- ROTACIÓN: Seguridad en producción ---
            // Borramos el viejo para que no se pueda volver a usar
            db.RefreshTokens.Remove(storedToken);

            // Creamos uno nuevo
            var newRefreshTokenValue = Guid.NewGuid().ToString();
            var newRefreshToken = new UserRefreshToken
            {
                Username = storedToken.Username,
                Token = newRefreshTokenValue,
                ExpiryDate = DateTime.UtcNow.AddDays(7)
            };

            db.RefreshTokens.Add(newRefreshToken);
            await db.SaveChangesAsync();

            // 4. Generar el nuevo JWT (Access Token)
            // Aquí puedes extraer tu lógica de generación de JWT a un método privado para no repetir código
            var nuevoStringToken = GenerarJwtToken(storedToken.Username, config);

            // 5. Actualizar AMBAS Cookies
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Cámbialo a false si estás en desarrollo sin HTTPS
                SameSite = SameSiteMode.Strict,
            };

            // Actualizamos el JWT (ej: 5-15 min)
            context.Response.Cookies.Append("X-Access-Token", nuevoStringToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddMinutes(15)
            });

            // Actualizamos el Refresh Token (ej: 7 días)
            context.Response.Cookies.Append("X-Refresh-Token", newRefreshTokenValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return Results.Ok(new { Message = "Token renovado con éxito" });
        });


        //ENDPOINT LOGOUT
        app.MapPost("/logout", async (HttpContext context, TodoDb db) =>
        {
            // 1.Extraer el refresh token para borrarlo de la DB
            var refreshToken = context.Request.Cookies["X-Refresh-Token"];
            if (refreshToken != null)
            {
                var dbToken = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
                if (dbToken != null) db.RefreshTokens.Remove(dbToken);
                await db.SaveChangesAsync();
            }

            // Esto le dice al navegador que borre la cookie inmediatamente
            context.Response.Cookies.Delete("X-Access-Token");
            context.Response.Cookies.Delete("X-Refresh-Token"); // Añade esta línea
            return Results.Ok(new { Message = "Sesión cerrada" });
        });
    }
    private static string GenerarJwtToken(string username, IConfiguration config)
    {
        var secretKey = config["Jwt:Key"];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[] {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, "Admin")
    };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = credentials,
            Issuer = config["Authentication:Schemes:Bearer:ValidIssuer"],
            Audience = config["Authentication:Schemes:Bearer:ValidAudiences:0"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public record LoginRequest(string Username, string Password);

