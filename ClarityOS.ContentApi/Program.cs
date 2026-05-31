using ClarityOS.ContentApi.Data;
using ClarityOS.ContentApi.Data.Repositories;
using ClarityOS.ContentApi.LlmProxy;
using ClarityOS.ContentApi.Middleware;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("ClarityOsDb"));

builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IProposalRepository, ProposalRepository>();

builder.Services.AddHttpClient<ILlmProxyClient, LlmProxyClient>(client =>
{
    var baseUrl = builder.Configuration["LlmProxy:BaseUrl"] ?? "http://localhost:5002";
    if (!baseUrl.EndsWith('/')) baseUrl += '/';
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
