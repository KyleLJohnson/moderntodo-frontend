using System.Net.Http.Json;
using BlazorTodo.Models;

namespace BlazorTodo.Services;

public class TaskApiService
{
    private readonly HttpClient _http;

    public TaskApiService(HttpClient http) => _http = http;

    public Task<List<TaskDto>?> GetTasksAsync(bool? completed = null)
    {
        var url = completed.HasValue ? $"api/tasks?completed={completed.Value}" : "api/tasks";
        return _http.GetFromJsonAsync<List<TaskDto>>(url);
    }

    public Task<TaskDto?> CreateTaskAsync(TaskDto task) =>
        _http.PostAsJsonAsync("api/tasks", task)
             .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<TaskDto>())
             .Unwrap();

    public Task<TaskDto?> UpdateTaskAsync(int id, TaskDto task) =>
        _http.PutAsJsonAsync($"api/tasks/{id}", task)
             .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<TaskDto>())
             .Unwrap();

    public async Task DeleteTaskAsync(int id) =>
        await _http.DeleteAsync($"api/tasks/{id}");
}
