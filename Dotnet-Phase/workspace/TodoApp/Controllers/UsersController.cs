using Microsoft.AspNetCore.Mvc;
using TodoApp.DTOs;
using TodoApp.Services;
namespace TodoApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;
        public UsersController(UserService userService)
        {
            _userService = userService;
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoApp.Models.User>>> GetUsers()
        {
            return Ok(await _userService.GetUsers());
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<TodoApp.Models.User>> GetUser(int id)
        {
            return Ok(await _userService.GetUser(id));
        }
        [HttpPost]
        public async Task<ActionResult<TodoApp.Models.User>> CreateUser(UserDTO userDTO)
        {
            var user = await _userService.CreateUser(userDTO);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
    }
}