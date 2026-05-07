using TodoApp.Models;
namespace TodoApp.Repositories
{
    public class TodoRepository
    {
        private readonly Data.TodoAppContext _context;
        public TodoRepository(Data.TodoAppContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<Todo>> GetTodos()
        {
            return await _context.Todos.ToListAsync();
        }
        public async Task<Todo> GetTodo(int id)
        {
            return await _context.Todos.FindAsync(id);
        }
        public async Task CreateTodo(Todo todo)
        {
            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();
        }
    }
}