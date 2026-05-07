using System.Threading.Tasks;
using System.Collections.Generic;
using TodoApp.Models;

namespace TodoApp.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetUsersAsync();
    }
}