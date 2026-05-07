using System.ComponentModel.DataAnnotations;
namespace TodoApp.DTOs
{
    public class TodoDTO
    {
        [Required]
        public string Title { get; set; }
    }
}