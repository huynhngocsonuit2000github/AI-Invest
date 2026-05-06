using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;
using TodoApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace TodoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoItemsController : ControllerBase
    {
        private readonly TodoContext _context;

        public TodoItemsController(TodoContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItems(int page = 1, int pageSize = 10)
        {
            return await _context.TodoItems.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }
    }
}