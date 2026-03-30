using System.ComponentModel.DataAnnotations;

namespace BlazorTodo.Models;

public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public class TaskDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
