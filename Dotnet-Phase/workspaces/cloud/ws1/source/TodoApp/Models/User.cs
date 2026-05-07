using System.ComponentModel.DataAnnotations;

namespace TodoApp.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
    }
}