using System.ComponentModel.DataAnnotations;
namespace TodoApp.Models
{
    public class Todo
    {
        public int Id { get; set; }
        [Required]
        public string Title { get; set; }
        public bool Completed { get; set; }
        public User User { get; set; }
    }
}