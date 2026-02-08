using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using Pr1.MinWebService.Domain;
using Pr1.MinWebService.Errors;
using Pr1.MinWebService.Middlewares;
using Pr1.MinWebService.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка сериализации, чтобы ответы были компактнее
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton<IItemRepository, InMemoryItemRepository>();

var app = builder.Build();

// Конвейер обработки запросов
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TimingAndLogMiddleware>();

// Точка доступа для чтения списка
app.MapGet("/api/items", (IItemRepository repo) =>
{
    return Results.Ok(repo.GetAll());
});

// Точка доступа для чтения по идентификатору
app.MapGet("/api/items/{id:guid}", (Guid id, IItemRepository repo) =>
{
    var item = repo.GetById(id);
    if (item is null)
        throw new NotFoundException("Элемент не найден");

    return Results.Ok(item);
});

// Точка доступа для создания
app.MapPost("/api/items", (HttpContext ctx, CreateItemRequest request, IItemRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ValidationException("Поле name не должно быть пустым");

    if (request.Price < 0)
        throw new ValidationException("Поле price не может быть отрицательным");

    var created = repo.Create(request.Name.Trim(), request.Price);

    // Адрес созданного ресурса без привязки к конкретному хосту
    var location = $"/api/items/{created.Id}";
    ctx.Response.Headers.Location = location;

    return Results.Created(location, created);
});

app.Run();

// Нужен для проекта с испытаниями
public partial class Program { }
