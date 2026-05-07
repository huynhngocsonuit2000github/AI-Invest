using System.Threading.Tasks;
using System.Collections.Generic;

namespace TodoApp.Services
{
    using TodoApp.Models;
public interface IUserService
    {
        Task<IEnumerable<User>> GetUsersAsync();
    }
}