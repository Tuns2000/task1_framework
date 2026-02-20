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

// Конвейер обработки запросов:
// 1. RequestIdMiddleware — генерирует уникальный идентификатор запроса (X-Request-Id)
// 2. ErrorHandlingMiddleware — перехватывает исключения и формирует единый формат ответа об ошибке
// 3. TimingAndLogMiddleware — замеряет время выполнения и пишет в журнал
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TimingAndLogMiddleware>();

// --- Точки доступа ---

// Корневой путь → перенаправление на список элементов
app.MapGet("/", () => Results.Redirect("/api/items"));

// GET /api/items — список элементов с опциональной фильтрацией и сортировкой
app.MapGet("/api/items", (IItemRepository repo,
    string? name,
    string? sortBy,
    bool? desc) =>
{
    var items = repo.GetAll(nameFilter: name, sortBy: sortBy, descending: desc ?? false);
    return Results.Ok(items);
});

// GET /api/items/{id} — получение одного элемента по идентификатору
app.MapGet("/api/items/{id:guid}", (Guid id, IItemRepository repo) =>
{
    var item = repo.GetById(id);
    if (item is null)
        throw new NotFoundException($"Элемент с идентификатором {id} не найден");

    return Results.Ok(item);
});

// Ограничение на размер входных данных (символы)
const int MaxNameLength = 200;
const int MaxDescriptionLength = 1000;

// POST /api/items — создание нового элемента с валидацией
app.MapPost("/api/items", (HttpContext ctx, CreateItemRequest request, IItemRepository repo) =>
{
    // Правило 1: имя обязательно
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ValidationException("Поле name не должно быть пустым");

    // Правило 2: имя не слишком длинное
    if (request.Name.Length > MaxNameLength)
        throw new ValidationException($"Поле name не должно превышать {MaxNameLength} символов");

    // Правило 3: описание не слишком длинное
    if (request.Description is not null && request.Description.Length > MaxDescriptionLength)
        throw new ValidationException($"Поле description не должно превышать {MaxDescriptionLength} символов");

    // Правило 4: цена не отрицательная
    if (request.Price < 0)
        throw new ValidationException("Поле price не может быть отрицательным");

    var created = repo.Create(
        request.Name.Trim(),
        request.Description?.Trim() ?? string.Empty,
        request.Price);

    var location = $"/api/items/{created.Id}";
    ctx.Response.Headers.Location = location;

    return Results.Created(location, created);
});

app.Run();

// Нужен для проекта с испытаниями
public partial class Program { }
