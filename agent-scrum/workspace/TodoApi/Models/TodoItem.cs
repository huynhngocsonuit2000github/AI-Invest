using System.ComponentModel.DataAnnotations;
using System;

namespace TodoApi.Models
{
    public class TodoItem
    {
        public long Id { get; set; }
        [Required]
        public string Name { get; set; }
        public bool IsComplete { get; set; }
        [Required]
        public DateTime CreatedDate { get; set; }
        [Required]
        public DateTime LastModifiedDate { get; set; }
    }
}