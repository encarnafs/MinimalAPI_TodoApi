using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using TodoApi.Data;
using TodoApi.Endpoints;
using TodoApi.Middlewares;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        // Esto fuerza a que TODOS los ProblemDetails (4xx y 5xx) incluyan el TraceId. Por defecto, las excepciones ya lo llevan pero los errores NO
        if (!context.ProblemDetails.Extensions.ContainsKey("traceId"))
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        }
    };
});

builder.Services.AddDbContext<TodoDb>(opt => opt.UseInMemoryDatabase("TodoList"));
//Captura excepciones en Development relacionadas con la BD (como fallos en las migraciones de Entity Framework)
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

if (origins == null || origins.Length == 0)
{
    // O lanzas un error personalizado o usas un valor por defecto
    throw new Exception("¡Falta la configuración de AllowedOrigins en appsettings.json!");
}

//Es el "portero", decide quién tiene permiso para hacer peticiones a la API desde un navegador
//Crea una regla llamada ProdPolicy, que permite peticiones que vengan de las URLs guardadas en origins
//.AllowAnyHeader: permite que el cliente envíe cualquier tipo de cabecera HTTP (Content-Type, Authorization...etc)
//.AllowAnyMethod: permite todos los verbos HTTP (GET, POST, PUT, DELETE..)
//.AllowCredentials: permite que el navegador envíe automáticamente Cookies o cabeceras de Autenticación HTTP
//Combinación Insegura --> Origins sea * y .AllowCredentials. La aplicación lanzará un error al arrancar
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProdPolicy", policy =>
    {
        policy.WithOrigins(origins!)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // <--- Obligatorio para Cookies
    });
});


//Si esa variable de entorno no se carga correctamente por un error de despliegue, el throw detiene la aplicación inmediatamente.
//Es mucho mejor que la API no arranque a que funcione con seguridad nula.
var secretKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(secretKey))
{
    // En desarrollo esto  avisa, en producción evita que la API arranque mal
    throw new InvalidOperationException("La variable de entorno 'Jwt:Key' no está configurada.");
}

// Definimos el esquema por defecto
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Leemos la configuración del appsettings.json
        var jwtSection = builder.Configuration.GetSection("Authentication:Schemes:Bearer");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            //El servidor verifica que el token fue emitido por él mismo (o por el servidor de confianza que definí en el JSON)
            //Si alguien trae un token emitido por "LoQueSea.com", lo rebota
            ValidateIssuer = true,
            ValidIssuer = jwtSection["ValidIssuer"],

            //El servidor mira si el token fue creado específicamente para ser usado en mi API.
            //Evita que un token de "App lo que sea" se use para entrar en mis endpoints.
            ValidateAudience = true,
            ValidAudiences = jwtSection.GetSection("ValidAudiences").Get<string[]>(),

            //El servidor usa mi Jwt:Key (la clave secreta) para verificar la firma digital del token.
            //Si alguien cambia un solo carácter del JWT, la firma ya no coincide y el servidor sabe que el token fue manipulado.
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            
            //Para precisión total de expiración del Token.
            //.NET da 5 minutos de cortesía para la caducidad, pero con esto es estricto
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            // Aquí le decimos: "Si no hay Bearer, busca en la cookie llamada 'X-Access-Token'"
            //JWT es "perezoso" y solo mira en la cabecera de la petición (Authorization: Bearer ...).
            //Con este evento, le das una instrucción extra:
            //"Oye, si no ves nada en la cabecera, no te rindas todavía; abre la caja fuerte de las cookies, busca una llamada X-Access-Token y usa lo que encuentres ahí como si fuera el token" Microsoft Learn.
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["X-Access-Token"];
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Política que mira el Claim de Rol
    options.AddPolicy("SoloParaJefes", policy =>
        policy.RequireRole("Admin"));

    // Política que mira el Claim de Nombre (Muy específica)
    //options.AddPolicy("SoloParaElAdminReal", policy =>
    //    policy.RequireClaim(ClaimTypes.Name, "admin"));
});

var app = builder.Build();

//procesa las solicitudes para ejecutar operaciones de migración de base de datos directamente desde el navegador
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}

//Busca GlobalExceptionHandler
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors("ProdPolicy");
app.UseAuthentication();
app.UseAuthorization();

//(.NET 6 a 9), ya no es obligatorio escribirlo porque builder.
//Build() lo configura internamente de forma automática antes de mapear tus endpoints.
//app.UseRouting();

//Mis módulos
app.MapAuthEndpoints();
app.RegisterTodoItemsEndpoints();

app.Run();

public partial class Program { } // Permite que WebApplicationFactory (Testing) lo encuentre