using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using TodoApi.Data;
using TodoApi.Models;
using FluentAssertions;

namespace TodoApi.Test.Integration
{
    public class TodoItemsIntegrationTests(WebApplicationFactory<Program> factory)
     : IntegrationTestBase(factory)
    {
        [Fact]
        public async Task GetTodos_WhenAuthenticated_ReturnsOk()
        {
            // 1. Arrange: Asegurar que hay algo que leer
            await AuthenticateAsync();
            await ExecuteInScopeAsync(async (db) =>
            {
                // Limpiamos y añadimos uno de prueba
                db.Todos.RemoveRange(db.Todos);
                db.Todos.Add(new Todo { Name = "Tarea de Test", IsComplete = false });
                await db.SaveChangesAsync();
            });

            // 2. Act
            var response = await Client.GetAsync("/todoitems");

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // response.EnsureSuccessStatusCode();
            // var items = await response.Content.ReadFromJsonAsync<List<Todo>>();
            // Assert.NotNull(items);
            // Assert.NotEmpty(items);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.OK, "porque el usuario está autenticado");

            var items = await response.Content.ReadFromJsonAsync<List<Todo>>();

            items.Should().NotBeNull("la API siempre debe devolver una lista, aunque esté vacía");
            items.Should().NotBeEmpty("hemos insertado una tarea previamente en la base de datos");

            // Un paso más allá: verificar el contenido
            items.Should().ContainSingle(t => t.Name == "Tarea de Test",
                "debe retornar exactamente la tarea que insertamos");
        }

        [Fact]
        public async Task CreateTodo_WhenAuthenticated_ReturnsCreated()
        {
            // 1. Arrange
            await AuthenticateAsync();
            var nuevoTodo = new { Name = "Aprender Integration Testing", IsComplete = false };

            // 2. Act
            var response = await Client.PostAsJsonAsync("/todoitems", nuevoTodo);

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            // var creado = await response.Content.ReadFromJsonAsync<Todo>();
            // Assert.NotNull(creado);
            // Assert.Equal(nuevoTodo.Name, creado.Name);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.Created,
                "porque el recurso se ha creado correctamente en el servidor");

            var creado = await response.Content.ReadFromJsonAsync<Todo>();

            creado.Should().NotBeNull("el cuerpo de la respuesta debe contener el objeto creado");

            // Verificamos que el objeto retornado coincida con lo que enviamos
            creado.Name.Should().Be(nuevoTodo.Name,
                "el nombre del Todo guardado debe ser idéntico al enviado");

            // Tip Pro: Comparar múltiples propiedades a la vez
            creado.Should().BeEquivalentTo(nuevoTodo, options => options.ExcludingMissingMembers(),
                "todas las propiedades enviadas deben coincidir en el objeto creado");
        }

        [Fact]
        public async Task CreateTodo_WhenNotAuthenticated_ReturnsUnauthorized()
        {
            // 1. Arrange: NO llamamos a AuthenticateAsync para simular un usuario anónimo
            var nuevoTodo = new { Name = "Tarea Anónima", IsComplete = false };

            // 2. Act
            var response = await Client.PostAsJsonAsync("/todoitems", nuevoTodo);

            // 3.Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

                    // --- Fluent Assertions ---
                    // Verificamos que el servidor rechace la petición antes de procesar el body
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "porque el endpoint /todoitems requiere autenticación previa para crear recursos");
        }

        [Fact]
        public async Task DeleteTodo_WhenAuthenticated_ReturnsNoContent()
        {
            // 1. Arrange: Crear un recurso para borrarlo después
            await AuthenticateAsync();
            int todoId = 0;

            await ExecuteInScopeAsync(async (db) =>
            {
                var todo = new Todo { Name = "Para borrar", IsComplete = false };
                db.Todos.Add(todo);
                await db.SaveChangesAsync();
                todoId = todo.Id; // Guardamos el ID generado
            });

            // 2. Act
            var response = await Client.DeleteAsync($"/todoitems/{todoId}");

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.NoContent,
                "porque el estándar REST para borrado exitoso sin cuerpo es 204 NoContent");

            // Verificar que ya no existe en la DB (Verificación de persistencia)
            await ExecuteInScopeAsync(async (db) =>
            {
                var existe = await db.Todos.AnyAsync(t => t.Id == todoId);

                // --- Assert Clásico (XUnit) ---
                // Assert.False(existe);

                // Fluent Assertions
                existe.Should().BeFalse("el registro debe haber sido eliminado físicamente de la base de datos");
            });
        }


        [Fact]
        public async Task DeleteTodo_WhenAuthenticated_ReturnsOK()
        {
            // 1. Arrange: Crear un recurso para borrarlo después
            await AuthenticateAsync();
            int todoId = 0;

            await ExecuteInScopeAsync(async (db) =>
            {
                var todo = new Todo { Name = "Para borrar", IsComplete = false };
                db.Todos.Add(todo);
                await db.SaveChangesAsync();
                todoId = todo.Id; // Guardamos el ID generado
            });

            // 2. Act
            var response = await Client.DeleteAsync($"/todoitems/{todoId}");

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.NoContent,
                "porque el método DeleteTodo de la API devuelve TypedResults.NoContent()");

            // Verificación final en Base de Datos
            await ExecuteInScopeAsync(async (db) =>
            {
                var existe = await db.Todos.AnyAsync(t => t.Id == todoId);

                // --- Assert Clásico (XUnit) ---
                // Assert.False(existe);

                // Fluent Assertions
                existe.Should().BeFalse("el registro debe haber sido eliminado de la base de datos");
            });
        }

        [Fact]
        public async Task GetTodos_FilterCompleted_ReturnsOnlyCompletedItems()
        {
            // 1. Arrange: Insertamos datos mezclados
            await AuthenticateAsync();

            await ExecuteInScopeAsync(async (db) =>
            {
                db.Todos.RemoveRange(db.Todos);
                db.Todos.AddRange(
                    new Todo { Name = "Tarea Completa", IsComplete = true },
                    new Todo { Name = "Tarea Pendiente", IsComplete = false }
                );
                await db.SaveChangesAsync();
            });

            // 2. Act: Llamamos al endpoint con el parámetro de filtro
            var response = await Client.GetAsync("/todoitems/complete");

            // 3. Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "porque el endpoint de tareas completadas debe estar accesible");

            var items = await response.Content.ReadFromJsonAsync<List<Todo>>();

            // Verificaciones potentes con Fluent Assertions
            items.Should().NotBeNull();

            // Verificamos que la lista NO contenga la tarea pendiente
            items.Should().NotContain(t => t.IsComplete == false,
                "el endpoint /complete nunca debe devolver tareas pendientes");

            // Verificamos que al menos contenga la que marcamos como true
            items.Should().ContainSingle(t => t.Name == "Tarea Completa",
                "debe retornar la tarea que insertamos como completada");
        }

        [Fact]
        public async Task GetTodoById_WhenItemExists_ReturnsOk_WithItem()
        {
            // 1. Arrange: Insertamos un elemento específico
            await AuthenticateAsync();
            int todoId = 0;
            var nombreEsperado = "Tarea para buscar por ID";

            await ExecuteInScopeAsync(async (db) =>
            {
                var todo = new Todo { Name = nombreEsperado, IsComplete = false };
                db.Todos.Add(todo);
                await db.SaveChangesAsync();
                todoId = todo.Id; // Capturamos el ID generado por la DB
            });

            // 2. Act: Consultamos el endpoint con el ID real
            var response = await Client.GetAsync($"/todoitems/{todoId}");

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // var item = await response.Content.ReadFromJsonAsync<Todo>();
            // Assert.NotNull(item);
            // Assert.Equal(nombreEsperado, item.Name);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "porque el ID solicitado existe en la base de datos");

            var item = await response.Content.ReadFromJsonAsync<Todo>();

            item.Should().NotBeNull("el cuerpo de la respuesta debe contener el objeto Todo solicitado");

            // Verificamos que los datos coincidan
            item.Id.Should().Be(todoId);
            item.Name.Should().Be(nombreEsperado,
                "el nombre devuelto debe coincidir con el que guardamos en la DB");
        }

        [Fact]
        public async Task UpdateTodo_WhenAuthenticated_ReturnsNoContent()
        {
            // 1. Arrange: Creamos el recurso original
            await AuthenticateAsync();
            var todo = new Todo { Name = "Original", IsComplete = false };

            await ExecuteInScopeAsync(async (db) =>
            {
                db.Todos.Add(todo);
                await db.SaveChangesAsync();
            });

            // Modificamos el objeto directamente para enviarlo como "nuevo estado"
            todo.Name = "Modificado";
            todo.IsComplete = true;

            // 2. Act: Enviamos el objeto 'todo' actualizado
            var response = await Client.PutAsJsonAsync($"/todoitems/{todo.Id}", todo);

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.NoContent,
                "porque la API debe procesar la actualización y devolver 204");

            // Verificación de persistencia real
            await ExecuteInScopeAsync(async (db) =>
            {
                var todoEnDb = await db.Todos.FindAsync(todo.Id);

                todoEnDb.Should().NotBeNull();
                todoEnDb.Name.Should().Be("Modificado", "el cambio de nombre debe persistir en la DB");
                todoEnDb.IsComplete.Should().BeTrue("el estado debe haber cambiado a completado");
            });
        }

        // --- Helpers ---

        private async Task ExecuteInScopeAsync(Func<TodoDb, Task> action)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TodoDb>();
            await action(db);
        }
    }
}
