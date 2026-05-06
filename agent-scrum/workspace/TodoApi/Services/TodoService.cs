using System.Collections.Generic;
using System.Threading.Tasks;
using TodoApi.Models;

namespace TodoApi.Services
{
    public class TodoService : ITodoService
    {
        private List<TodoItem> _todoItems = new List<TodoItem>();

        public async Task<IEnumerable<TodoItem>> GetTodoItemsAsync()
        {
            return _todoItems;
        }

        public async Task<TodoItem> GetTodoItemAsync(long id)
        {
            return _todoItems.Find(i => i.Id == id);
        }

        public async Task<TodoItem> CreateTodoItemAsync(TodoItem todoItem)
        {
            _todoItems.Add(todoItem);
            return todoItem;
        }

        public async Task UpdateTodoItemAsync(long id, TodoItem todoItem)
        {
            var existingItem = _todoItems.Find(i => i.Id == id);
            if (existingItem == null)
            {
                throw new InvalidOperationException();
            }

            existingItem.Name = todoItem.Name;
            existingItem.IsComplete = todoItem.IsComplete;
        }

        public async Task DeleteTodoItemAsync(long id)
        {
            var item = _todoItems.Find(i => i.Id == id);
            if (item!= null)
            {
                _todoItems.Remove(item);
            }
        }
    }
}