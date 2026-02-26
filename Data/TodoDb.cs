using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Data
{
    class TodoDb(DbContextOptions<TodoDb> options) : DbContext(options)
    {
        public DbSet<Todo> Todos => Set<Todo>();
        public DbSet<UserRefreshToken> RefreshTokens => Set<UserRefreshToken>();
    }
}
