using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using System.Collections.Generic;
using TodoApp.Models;

namespace TodoApp.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly TodoAppDbContext _context;

        public UserRepository(TodoAppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }
    }
}