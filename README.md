<h1 align="center" id="title">🚀Minimal API</h1>

<p id="description">He implementado una API con ASP.NET Core que gestiona tareas de forma segura mediante <b>JWT</b> y <b>Cookies HttpOnly</b>. Al usar cookies con los <b>flags HttpOnly y Secure</b> protejo los tokens contra ataques <b>XSS</b>. Además aplico la política <b>SameSite=Strict</b> para mitigar <b>ataques CSRF</b> asegurando que el navegador solo envíe los tokens en peticiones originadas desde mi propio sitio. La estrategia de rotación utiliza un <b>Access Token</b> de corta duración (15 min) y un <b>Refresh Token</b> de larga duración (7 días). Si un acceso expira (401) el sistema permite obtener un nuevo Access Token sin que el usuario deba reautenticarse manteniendo la sesión fluida y segura.</p>

## 🚀 Características principales

* Desarrollo de una API REST utilizando ASP.NET Core Minimal APIs.
* Autenticación basada en JWT.
* Gestión segura de tokens mediante Cookies HttpOnly.
* Refresh Tokens y rotación de tokens.
* Protección frente a ataques XSS y CSRF.
* Autorización basada en Roles y Policies.
* Middleware global para la gestión centralizada de excepciones.
* Respuestas de error estandarizadas mediante ProblemDetails.
* Configuración de CORS.
* Pruebas de integración automatizadas.
* Pruebas manuales mediante archivos `.http`.
* Uso de Inyección de Dependencias y separación de responsabilidades.


<h2>🛠️ Instalación:</h2>

<p>1. Clave de Seguridad: Configura una cadena de texto larga y aleatoria en la variable de entorno Jwt__Key (o en tu appsettings.json). Esta clave se usará para firmar los tokens.</p>

```json
{   
   "Jwt": {
     "Key": "TU_CLAVE_SECRETA_SUPER_SEGURA_DE_32_CARACTERES",
     "ValidIssuer": "https://localhost:{PORTX}",
     "ValidAudience": [
            "https://localhost:{PORTX}",
            "https://localhost:{PORTY}"   //Si acepta conexiones de otro servidor
      ]
    }
}
```

<p>2. Ejecutar API: Inicia el proyecto.</p>

<p>3. Autenticación: Realiza una petición al endpoint /login. El servidor emitirá las cookies HttpOnly automáticamente. Para endpoints protegidos el navegador gestionará los tokens de forma transparente.</p>

  
  
<h2>🏗️ Stack Tecnológico</h2>

Technologies used in the project:

*   ASP.NET Core 
*   Seguridad: Autenticación JWT con esquema de Refresh Tokens.
*   Seguridad: Almacenamiento seguro en cookies HttpOnly / SameSite=Strict.
*   Seguridad: Autorización basada en Roles/Policies.

<h2>🏛️  Arquitectura o Patrones</h2>

<p>* Minimal APIs: Estructura ligera y de alto rendimiento.</p>
<p>* Inyección de Dependencias (DI): Uso del contenedor nativo de .NET para un código desacoplado y testeable.</p>

<h2>📍 Endpoints Principales (API Reference)</h2>
<img width="775" height="348" alt="EndPoints" src="https://github.com/user-attachments/assets/4eeadf0d-45e8-46b9-a6e3-6069eec736ca" />

<h2>📋 Requisitos Previos (Prerequisites)</h2>
<p>* .NET 9</p>

<h2>⚡ Pruebas / Testing:</h2>
  
<p>He incluido un archivo TodoApi.http para que puedas probar los endpoints directamente desde Visual Studio o VS Code sin necesidad de Postman.</p>

## Mejoras futuras
- Incorporar persistencia con BBDD.
- Logging estructurado con Serilog.
- Dockerización.
- Rate Limiting.
- Pruebas unitarias
- Versionado de API.

