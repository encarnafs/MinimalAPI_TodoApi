using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using TodoApi.Data;
using TodoApi.DTOs;
using TodoApi.Models;

namespace TodoApi.Test.Integration
{

    public class AuthIntegrationTests(WebApplicationFactory<Program> factory) : IntegrationTestBase(factory)
    {
        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOk_AndSetsCookies()
        {
            // Arrange
            var loginDto = new LoginRequestDTO
            {
                Username = "admin",
                Password = "12345"
            };
            
            // Act
            var response = await Client.PostAsJsonAsync("/login", loginDto);

            // Assert

            // --- Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // var hasCookies = response.Headers.Contains("Set-Cookie");
            // Assert.True(hasCookies, "La respuesta debería contener headers 'Set-Cookie'");

            //Fluent Assertions
            // 1. Verificamos el Status Code de forma legible
            response.StatusCode.Should().Be(HttpStatusCode.OK, "porque las credenciales son válidas");

            // 2. Verificamos que existan las cabeceras Set-Cookie
            response.Headers.Should().ContainKey("Set-Cookie");

            // 3. Verificamos el contenido específico de las cookies
            // Extraemos los valores para poder inspeccionar qué tokens han llegado
            var cookieValues = response.Headers.GetValues("Set-Cookie");

            cookieValues.Should().Contain(c => c.Contains("X-Access-Token"),
                "debería recibir el token de acceso para las peticiones");

            cookieValues.Should().Contain(c => c.Contains("X-Refresh-Token"),
                "debería recibir el token de refresco para renovar la sesión");
        }

        // Verifica que la API no revela si el usuario existe.
        // Tanto un usuario inexistente como una contraseña incorrecta
        // deben devolver 401 Unauthorized.
        [Fact]
        public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
        {
            // Arrange
            var loginDto = new LoginRequestDTO
            {
                Username = "non_existent_user",
                Password = "some_password"
            };
            // Act
            var response = await Client.PostAsJsonAsync("/login", loginDto);

            // Assert

            // --- Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "porque la API no debe revelar si el usuario existe o si las credenciales son incorrectas");
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginDto = new LoginRequestDTO
            {
                Username = "admin",
                Password = "wrong_password"
            };

            // Act
            var response = await Client.PostAsJsonAsync("/login", loginDto);

            // --- Assert Clásico (XUnit) ---
            //Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            // --- Fluent Assertions ---
            // Verifico que el status code sea 401 Unauthorized
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "porque las credenciales proporcionadas no son válidas");

            // Opcional: Verifico que NO se hayan establecido cookies de sesión en un login fallido
            response.Headers.Contains("Set-Cookie").Should().BeFalse(
                "porque no se deben emitir cookies de autenticación cuando el inicio de sesión falla");
        }

        [Fact]
        public async Task Logout_ClearsCookies_And_RemovesRefreshTokenFromDb()
        {
            // Arrange 
            // 1. LOGIN para obtener la cookie de Refresh Token
            var loginDto = new LoginRequestDTO
            {
                Username = "admin",
                Password = "12345"
            };

            var loginResponse = await Client.PostAsJsonAsync("/login", loginDto);

            var setCookieHeader = loginResponse.Headers.Contains("Set-Cookie")
                ? loginResponse.Headers.GetValues("Set-Cookie").FirstOrDefault(c => c.Contains("X-Refresh-Token"))
                : null;

            var tokenValue = ExtractCookieValue(setCookieHeader, "X-Refresh-Token");

            // Assert: Verifico que el token de refresco se haya recibido correctamente

            // --- Clásico (XUnit) ---
            // Assert.NotNull(tokenValue);

            // Fluent Assertions
            tokenValue.Should().NotBeNull("porque el login debe generar un Refresh Token para este test");

            // Act
            // 2. LOGOUT
            var logoutResponse = await Client.PostAsync("/logout", null);

            // Assert: Verificamos que la respuesta del logout sea 200 OK

            // --- Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

            // Fluent Assertions
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, "porque el cierre de sesión debe completarse correctamente");

            // 3. VERIFICACIÓN en Base de Datos
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TodoDb>();
            var tokenExists = await db.RefreshTokens.AnyAsync(t => t.Token == tokenValue);

            // Assert: Verifico que el Refresh Token haya sido eliminado de la base de datos

            // ---  Clásico (XUnit) ---
            // Assert.False(tokenExists, "El Refresh Token debería haber sido eliminado de la DB tras el logout");

            // Fluent Assertions
            tokenExists.Should().BeFalse("el Refresh Token debe ser eliminado de la base de datos para invalidar la sesión");
        }

        [Fact]
        public async Task AfterLogout_CannotAccessProtectedEndpoints()
        {
            // Arrange
            // 1. Login previo
            var loginDto = new LoginRequestDTO
            {
                Username = "admin",
                Password = "12345"
            };

            // 2. Realizo el login para obtener el Refresh Token
            var loginResponse = await Client.PostAsJsonAsync("/login", loginDto);

            // 3. Logout
            await Client.PostAsync("/logout", null);

            // Act
            // 4. Intentar acceder a un recurso que requiere auth (uso uno simple)
            var protectedResponse = await Client.GetAsync("/todoitems");

            // Assert: Verifico que la respuesta sea 401 Unauthorized tras el logout
            // --- Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);

            // --- Fluent Assertions ---
            protectedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "porque tras el logout las cookies de sesión deben ser invalidadas o eliminadas");
        }

        [Fact]
        public async Task RequestWithModifiedRefreshToken_ReturnsUnauthorized()
        {
            // Arrange
            // 1. LOGIN inicial para obtener un token válido en la cookie y en la DB
            var loginDto = new LoginRequestDTO
            {
                Username = "admin",
                Password = "12345"
            };

            var loginResponse = await Client.PostAsJsonAsync("/login", loginDto);

            var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie")
                .FirstOrDefault(c => c.Contains("X-Refresh-Token"));

            var originalToken = ExtractCookieValue(setCookieHeader, "X-Refresh-Token");

            // 2. MODIFICAR el token en la DB (Simulo manipulación)
            await ExecuteInScopeAsync(async (db) =>
            {
                var tokenRecord = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == originalToken);

                // Verificamos que el login realmente guardó el token antes de seguir
                tokenRecord.Should().NotBeNull("porque el login previo debe haber persistido el token");

                tokenRecord.Token = "TOKEN_MANIPULADO_123";
                await db.SaveChangesAsync();
            });

            // Act

            // 3. ACT: Intentar refrescar con la cookie original (que ya no coincide con la DB)
            var refreshResponse = await Client.PostAsync("/refresh", null);

            // Assert

            // --- Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

            // --- Fluent Assertions ---
            refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "porque el valor del token en la base de datos ha sido modificado y ya no coincide con la cookie");
        }

        [Fact]
        public async Task RequestWithExpiredRefreshToken_ReturnsUnauthorized()
        {
            // Arrange
            // 1. LOGIN: Obtenemos un token válido (que expira en 7 días según tu API)
            var loginDto = new LoginRequestDTO
            {
                Username = "admin",
                Password = "12345"
            };

            var loginResponse = await Client.PostAsJsonAsync("/login", loginDto);

            var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").FirstOrDefault(c => c.Contains("X-Refresh-Token"));
            var originalToken = ExtractCookieValue(setCookieHeader, "X-Refresh-Token");

            // 2. EXPIRAR el token manualmente en la DB
            await ExecuteInScopeAsync(async (db) =>
            {
                var tokenRecord = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == originalToken);
                // Precondición del Arrange
                tokenRecord.Should().NotBeNull("el login debe haber persistido el token para poder expirarlo");

                // Pongo una fecha de expiración de hace una hora
                tokenRecord.ExpiryDate = DateTime.UtcNow.AddHours(-1);
                await db.SaveChangesAsync();
            });

            // 3. ACT: Intentar usar ese token caducado
            var refreshResponse = await Client.PostAsync("/refresh", null);

            // Assert

            // --- Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

            // --- Fluent Assertions ---
            refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "porque el sistema debe rechazar Refresh Tokens caducados");
        }

        // --- Helpers ---

        private static string? ExtractCookieValue(string? header, string name)
        {
            if (string.IsNullOrEmpty(header)) return null;

            var parts = header.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0].Trim() == name)
                {
                    return kv[1].Trim();
                }
            }
            return null;
        }

        private async Task ExecuteInScopeAsync(Func<TodoDb, Task> action)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TodoDb>();
            await action(db);
        }
    }
}


