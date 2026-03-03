using Microsoft.AspNetCore.Diagnostics;

namespace TodoApi.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        ValueTask<bool> IExceptionHandler.TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
