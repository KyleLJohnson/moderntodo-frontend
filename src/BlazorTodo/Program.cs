using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorTodo;
using BlazorTodo.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// In production (SWA) the Blazor app is served from the same origin as the
// Functions API (/api/*), so we use BaseAddress. Locally, point at the
// Functions host port via ASPNETCORE_FUNCTIONS_URL or fall back to localhost.
var apiBase = builder.Configuration["ApiBaseUrl"]
    ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<TaskApiService>();

await builder.Build().RunAsync();
