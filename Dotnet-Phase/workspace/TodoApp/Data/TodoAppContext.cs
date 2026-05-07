using Microsoft.EntityFrameworkCore;
using TodoApp.Models;
namespace TodoApp.Data
{
    public class TodoAppContext : DbContext
    {
        public TodoAppContext(DbContextOptions<TodoAppContext> options) : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Todo> Todos { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Role> Roles { get; set; }
    }
}