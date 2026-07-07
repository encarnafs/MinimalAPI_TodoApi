using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TodoApi.Data;
using TodoApi.DTOs;
using TodoApi.Models;
namespace TodoApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        //ENDPOINT LOGIN
        app.MapPost("/login", async Task<IResult> (LoginRequestDTO user, IConfiguration config, HttpContext context, TodoDb db) =>
        {
            // 1. Validar las credenciales del usuario (esto es solo un ejemplo, en producción deberías usar una base de datos y hashing de contraseñas)
            if (user.Username != "admin" || user.Password != "12345")
            {
                return TypedResults.Unauthorized();
            }

            // 2. Obtener JWT
            var secretKey = config["Jwt:Key"];

            // Validar que la clave JWT no sea nula o demasiado corta
            if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
            {
                return TypedResults.Problem("La clave JWT no es válida o es demasiado corta");
            }

            // 3. Crear la clave de seguridad y las credenciales de firma
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 4. Crear los claims para el token
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, "Admin")
            };

            // 5. Crear el descriptor del token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = credentials,
                Issuer = config["Authentication:Schemes:Bearer:ValidIssuer"],
                Audience = config["Authentication:Schemes:Bearer:ValidAudiences:0"]
            };

            // 6. Crear el token JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var stringToken = tokenHandler.WriteToken(token);

            // 7. Generar Refresh Token y guardarlo en la base de datos
            var refreshToken = GenerarRefreshToken(user.Username);
            //Guardar en  TodoDb InMemory
            db.RefreshTokens.Add(refreshToken);
            await db.SaveChangesAsync();

            // 8. Guardar ambos tokens en cookies HttpOnly
            EscribirCookiesAutenticacion(context,stringToken,refreshToken.Token);

            return TypedResults.Ok(new { Message = "Autenticación satisfactoria" });
        });


        // ENDPOINT REFRESH (Rotación de Token)
        app.MapPost("/refresh", async Task<IResult> (HttpContext context, TodoDb db, IConfiguration config) =>
        {
            // 1. Extraer el Refresh Token de la cookie
            var refreshTokenCookie = context.Request.Cookies["X-Refresh-Token"];
            if (string.IsNullOrEmpty(refreshTokenCookie)) return TypedResults.Unauthorized();

            // 2. Buscarlo en la base de datos y verificar que no haya expirado
            var storedToken = await db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshTokenCookie && t.ExpiryDate > DateTime.UtcNow);

            if (storedToken == null) return TypedResults.Unauthorized();

            // 3. --- ROTACIÓN: Seguridad en producción ---
            // Borrar el viejo para que no se pueda volver a usar
            db.RefreshTokens.Remove(storedToken);

            // Crear uno nuevo y lo guardo en la base de datos
            var newRefreshToken =  GenerarRefreshToken(storedToken.Username);
            db.RefreshTokens.Add(newRefreshToken);
            await db.SaveChangesAsync();

            // 4. Generar el nuevo JWT (Access Token)
            // Aquí puedes extraer tu lógica de generación de JWT a un método privado para no repetir código
            var nuevoStringToken = GenerarJwtToken(storedToken.Username, config);


            // 5. Guardar ambos tokens en cookies HttpOnly
            EscribirCookiesAutenticacion(context,nuevoStringToken,newRefreshToken.Token);

            return TypedResults.Ok(new { Message = "Token renovado con éxito" });
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
            context.Response.Cookies.Delete("X-Refresh-Token"); 
            return TypedResults.Ok(new { Message = "Sesión cerrada" });
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

    private static UserRefreshToken GenerarRefreshToken(string username)
    {
        var newRefreshTokenValue =  Guid.NewGuid().ToString();

        return new UserRefreshToken
        {
            Username = username,
            Token = newRefreshTokenValue,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };
    }

    private static void EscribirCookiesAutenticacion(
    HttpContext context,
    string accessToken,
    string refreshToken)
    {
        context.Response.Cookies.Append("X-Access-Token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(15)
        });

        context.Response.Cookies.Append("X-Refresh-Token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        });
    }
}