using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TodoApi.Models;
using TodoApi.Services;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TodoController : ControllerBase
    {
        private readonly ITodoService _todoService;

        public TodoController(ITodoService todoService)
        {
            _todoService = todoService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItems(int pageSize, int pageNumber)
        {
            return await _todoService.GetTodoItemsAsync(pageSize, pageNumber);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TodoItem>> GetTodoItem(long id)
        {
            return await _todoService.GetTodoItemAsync(id);
        }

        [HttpPost]
        public async Task<ActionResult<TodoItem>> CreateTodoItem(TodoItem todoItem)
        {
            return await _todoService.CreateTodoItemAsync(todoItem);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateTodoItem(long id, TodoItem todoItem)
        {
            await _todoService.UpdateTodoItemAsync(id, todoItem);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTodoItem(long id)
        {
            await _todoService.DeleteTodoItemAsync(id);
            return NoContent();
        }
    }
}
