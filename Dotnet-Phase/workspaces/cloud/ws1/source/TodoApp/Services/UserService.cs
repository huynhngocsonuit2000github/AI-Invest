using System.Threading.Tasks;
using System.Collections.Generic;
using TodoApp.Models;
using TodoApp.Repositories;

namespace TodoApp.Services
{
    using System.Collections.Generic;
using TodoApp.Models;
public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            return await _userRepository.GetUsersAsync();
        }
    }
}