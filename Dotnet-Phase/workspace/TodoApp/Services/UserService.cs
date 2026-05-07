using TodoApp.Models;
using TodoApp.DTOs;
using Microsoft.EntityFrameworkCore;
namespace TodoApp.Services
{
    public class UserService
    {
        private readonly TodoAppContext _context;
        public UserService(TodoAppContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<User>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }
        public async Task<User> GetUser(int id)
        {
            return await _context.Users.FindAsync(id);
        }
        public async Task<User> CreateUser(UserDTO userDTO)
        {
            var user = new User { Username = userDTO.Username, Password = userDTO.Password };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }
}