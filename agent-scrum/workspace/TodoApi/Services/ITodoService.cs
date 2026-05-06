using System.Collections.Generic;
using System.Threading.Tasks;
using TodoApi.Models;

namespace TodoApi.Services
{
    public interface ITodoService
    {
        Task<IEnumerable<TodoItem>> GetTodoItemsAsync(int pageSize, int pageNumber);
        Task<TodoItem> GetTodoItemAsync(long id);
        Task<TodoItem> CreateTodoItemAsync(TodoItem todoItem);
        Task UpdateTodoItemAsync(long id, TodoItem todoItem);
        Task DeleteTodoItemAsync(long id);
    }
}
