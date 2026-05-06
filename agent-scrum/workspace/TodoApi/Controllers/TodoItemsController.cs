using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using TodoApi.Models;
using TodoApi.Services;

namespace TodoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoItemsController : ControllerBase
    {
        private readonly ITodoService _todoService;

        public TodoItemsController(ITodoService todoService)
        {
            _todoService = todoService;
        }

        // GET api/todoitems
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItems()
        {
            return await _todoService.GetTodoItemsAsync();
        }

        // GET api/todoitems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TodoItem>> GetTodoItem(long id)
        {
            return await _todoService.GetTodoItemAsync(id);
        }

        // POST api/todoitems
        [HttpPost]
        public async Task<ActionResult<TodoItem>> CreateTodoItem(TodoItem todoItem)
        {
            return await _todoService.CreateTodoItemAsync(todoItem);
        }

        // PUT api/todoitems/5
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateTodoItem(long id, TodoItem todoItem)
        {
            return await _todoService.UpdateTodoItemAsync(id, todoItem);
        }

        // DELETE api/todoitems/5
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTodoItem(long id)
        {
            return await _todoService.DeleteTodoItemAsync(id);
        }
    }
}