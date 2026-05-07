using System.ComponentModel.DataAnnotations;
namespace TodoApp.Models
{
    public class Group
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public ICollection<User> Users { get; set; }
    }
}