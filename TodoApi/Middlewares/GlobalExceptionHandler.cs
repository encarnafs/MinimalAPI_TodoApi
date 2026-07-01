using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService, IHostEnvironment environment)
        {
            _logger = logger;
            _problemDetailsService = problemDetailsService;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Si llegó aquí, es porque algo NO se controló (un bug, red caída, etc.)
            _logger.LogError(
                exception,
                "Error no controlado en {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Si es desarrollo, devulevo los detalles del error por seguridad.
            // Si es producción, devuelvo un mensaje genérico para no dar pistas a un atacante.
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server Error",
                Detail = _environment.IsDevelopment()
                    ? exception.Message
                    : "Ocurrió un error inesperado en el servidor."
            };

            await _problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception
            });

            // Error manejado, no se propaga más allá de este middleware
            return true;
        }
    }
}
