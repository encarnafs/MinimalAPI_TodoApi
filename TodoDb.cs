using Microsoft.EntityFrameworkCore;

namespace TodoApi
{
    class TodoDb(DbContextOptions<TodoDb> options) : DbContext(options)
    {
        public DbSet<Todo> Todos => Set<Todo>();
        public DbSet<UserRefreshToken> RefreshTokens => Set<UserRefreshToken>();
    }
}
