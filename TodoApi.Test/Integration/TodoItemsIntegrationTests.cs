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
                // Limpio y añado uno de prueba
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
            response.StatusCode.Should().Be(HttpStatusCode.OK, "porque el usuario autenticado tiene permiso para consultar sus tareas");

            var items = await response.Content.ReadFromJsonAsync<List<TodoItemDTO>>();

            items.Should().NotBeNull("la API siempre debe devolver una lista, aunque esté vacía");
            items.Should().NotBeEmpty("he insertado una tarea previamente en la base de datos");

            // Un paso más allá: verificar el contenido
            items.Should().ContainSingle(t =>
                t.Name == "Tarea de Test" &&
                t.IsComplete == false,
                "porque debe devolver exactamente la tarea que inserté");
        }

        [Fact]
        public async Task CreateTodo_WhenAuthenticated_ReturnsCreated()
        {
            // 1. Arrange
            await AuthenticateAsync();

            var nuevoTodo = new TodoItemDTO
            {
                Name = "Aprender Integration Testing",
                IsComplete = false
            };

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

            var creado = await response.Content.ReadFromJsonAsync<TodoItemDTO>();

            creado.Should().NotBeNull("porque el endpoint devuelve el recurso recién creado");

            // Comparar múltiples propiedades a la vez
            creado.Should().BeEquivalentTo(nuevoTodo, options => options.ExcludingMissingMembers(),
                "porque el recurso devuelto debe reflejar los datos enviados por el cliente");
        }

        [Fact]
        public async Task CreateTodo_WhenNotAuthenticated_ReturnsUnauthorized()
        {
            // 1. Arrange: NO llamo a AuthenticateAsync para simular un usuario anónimo
            var nuevoTodo = new TodoItemDTO
            {
                Name = "Tarea Anónima",
                IsComplete = false
            };

            // 2. Act
            var response = await Client.PostAsJsonAsync("/todoitems", nuevoTodo);

            // 3.Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            // --- Fluent Assertions ---
            // Verifico que el servidor rechace la petición antes de procesar el body
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "porque solo los usuarios autenticados pueden crear tareas");

            // Verificación de que no se haya creado ningún registro en la base de datos
            await ExecuteInScopeAsync(async db =>
            {
                (await db.Todos.CountAsync()).Should().Be(0,
                    "porque una petición no autenticada no debe crear registros");
            });
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
                "porque una eliminación correcta devuelve 204 No Content según las convenciones REST");

            // Verifico que el recurso ya no existe en la base de datos.
            await ExecuteInScopeAsync(async (db) =>
            {
                var existe = await db.Todos.AnyAsync(t => t.Id == todoId);

                // --- Assert Clásico (XUnit) ---
                // Assert.False(existe);

                // Fluent Assertions
                existe.Should().BeFalse("porque el registro debe haber sido eliminado físicamente de la base de datos");
            });
        }


        [Fact]
        public async Task DeleteTodo_WhenTodoDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            await AuthenticateAsync();

            const int nonExistingId = 999999;

            // Act
            var response = await Client.DeleteAsync($"/todoitems/{nonExistingId}");

            // Assert

            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "porque no es posible eliminar un recurso que no existe");
        }

        [Fact]
        public async Task GetTodos_FilterCompleted_ReturnsOnlyCompletedItems()
        {
            // 1. Arrange: Inserto datos mezclados
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

            // 2. Act: LLamo al endpoint con el parámetro de filtro
            var response = await Client.GetAsync("/todoitems/complete");

            // 3. Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "porque un usuario autenticado puede consultar las tareas completadas");

            var items = await response.Content.ReadFromJsonAsync<List<TodoItemDTO>>();

            // Verificaciones potentes con Fluent Assertions
            items.Should().NotBeNull(
                "porque el endpoint siempre debe devolver una colección");

            // Verifico que la lista NO contenga la tarea pendiente
            items.Should().NotContain(t => !t.IsComplete,
                "porque el endpoint /complete nunca debe devolver tareas pendientes");

            // Verifico que al menos contenga la que marco como true
            items.Should().ContainSingle(t =>
                t.Name == "Tarea Completa" &&
                t.IsComplete,
                "porque debe devolver únicamente la tarea completada que insertamos");
        }

        
        [Fact]
        public async Task GetTodoById_WhenItemExists_ReturnsOk_WithItem()
        {
            // 1. Arrange: Inserto un elemento específico
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

            // 2. Act: Consulto el endpoint con el ID real
            var response = await Client.GetAsync($"/todoitems/{todoId}");

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // var item = await response.Content.ReadFromJsonAsync<Todo>();
            // Assert.NotNull(item);
            // Assert.Equal(nombreEsperado, item.Name);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "porque el recurso solicitado existe");

            var item = await response.Content.ReadFromJsonAsync<TodoItemDTO>();

            item.Should().NotBeNull("porque el recurso solicitado existe");

            // Verifico que los datos coincidan
            item.Id.Should().Be(todoId, "porque debe devolver el recurso solicitado");
            item.Name.Should().Be(nombreEsperado,
                "porque debe devolver los datos del recurso almacenado");
        }

        [Fact]
        public async Task UpdateTodo_WhenAuthenticated_ReturnsNoContent()
        {
            // 1. Arrange: Creo el recurso original
            await AuthenticateAsync();
            var todo = new Todo 
            { 
                Name = "Original", 
                IsComplete = false
            };

            await ExecuteInScopeAsync(async (db) =>
            {
                db.Todos.Add(todo);
                await db.SaveChangesAsync();
            });

            // Preparo el nuevo estado que enviaré al endpoint
            var todoActualizado = new TodoItemDTO
            {
                //Id = todo.Id, El identificador viaja en la URL, no en el body
                Name = "Modificado",
                IsComplete = true
            };

            // 2. Act: Envío el DTO con los datos actualizados
            var response = await Client.PutAsJsonAsync($"/todoitems/{todo.Id}", todoActualizado);

            // 3. Assert
            // --- Assert Clásico (XUnit) ---
            // Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // --- Fluent Assertions ---
            response.StatusCode.Should().Be(HttpStatusCode.NoContent,
                "porque la actualización del recurso se ha realizado correctamente");

            // Verificación de persistencia real
            await ExecuteInScopeAsync(async (db) =>
            {
                var todoEnDb = await db.Todos.FindAsync(todo.Id);

                todoEnDb.Should().NotBeNull("porque el recurso ya existía antes de la actualización");
                todoEnDb.Name.Should().Be("Modificado", "porque el nuevo nombre debe persistirse en la base de datos");
                todoEnDb.IsComplete.Should().BeTrue("porque la actualización debe persistir el nuevo estado");
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
