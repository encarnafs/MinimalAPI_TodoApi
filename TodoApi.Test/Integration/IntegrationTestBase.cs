using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoApi.Test.Integration
{
    // 'abstract' impide que se ejecute como un test por sí solo
    public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
    {
        protected readonly HttpClient Client;
        protected readonly WebApplicationFactory<Program> Factory;

        protected IntegrationTestBase(WebApplicationFactory<Program> factory)
        {
            Factory = factory;
            Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost")
            });
        }

        // LOGIN
        // Método compartido: Todos los tests pueden usarlo sin repetir código
        protected async Task AuthenticateAsync()
        {
            var loginData = new { Username = "admin", Password = "12345" };
            await Client.PostAsJsonAsync("/login", loginData);
        }
    }
}
