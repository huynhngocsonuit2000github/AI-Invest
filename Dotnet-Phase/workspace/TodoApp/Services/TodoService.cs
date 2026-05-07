using TodoApp.Models;
using TodoApp.DTOs;
using Microsoft.EntityFrameworkCore;
namespace TodoApp.Services
{
    public class TodoService
    {
        private readonly TodoAppContext _context;
        public TodoService(TodoAppContext context)
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
        public async Task<Todo> CreateTodo(TodoDTO todoDTO)
        {
            var todo = new Todo { Title = todoDTO.Title };
            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();
            return todo;
        }
    }
}