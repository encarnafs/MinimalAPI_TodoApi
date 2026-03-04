using Microsoft.AspNetCore.Diagnostics;

namespace TodoApi.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Si llegó aquí, es porque algo NO se controló (un bug, red caída, etc.)
            _logger.LogError(exception, "Error no controlado");

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Solo devolvemos los detalles si es desarrollo, por seguridad
            var response = new
            {
                Status = 500,
                Title = "Server Error",
                Detail = "Ocurrió un error inesperado en el servidor."
            };

            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

            return true; // Decimos que el error está manejado
        }
    }
}
