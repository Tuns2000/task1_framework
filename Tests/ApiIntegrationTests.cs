using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pr1.MinWebService.Domain;

namespace Pr1.MinWebService.Tests;

/// <summary>
/// Перехватчик записей журнала для проверки логирования в тестах.
/// </summary>
public sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly TestLogger _logger = new();
    public List<string> Messages => _logger.Messages;
    public ILogger CreateLogger(string categoryName) => _logger;
    public void Dispose() { }

    private sealed class TestLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}

/// <summary>
/// Интеграционные тесты веб службы — проверка полного конвейера обработки запросов.
/// Используется WebApplicationFactory для поднятия тестового сервера в памяти.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // ───────── GET /api/items ─────────

    [Fact]
    public async Task GetAll_ReturnsOkAndEmptyList()
    {
        var response = await _client.GetAsync("/api/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<Item[]>(JsonOpts);
        Assert.NotNull(items);
    }

    // ───────── POST /api/items → GET /api/items/{id} ─────────

    [Fact]
    public async Task CreateAndGet_RoundTrip()
    {
        // Создаём элемент
        var payload = new { Name = "Карандаш", Description = "HB графитовый", Price = 25.0m };
        var createResponse = await _client.PostAsJsonAsync("/api/items", payload);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<Item>(JsonOpts);
        Assert.NotNull(created);
        Assert.Equal("Карандаш", created!.Name);
        Assert.Equal("HB графитовый", created.Description);
        Assert.Equal(25.0m, created.Price);

        // Получаем по идентификатору
        var getResponse = await _client.GetAsync($"/api/items/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<Item>(JsonOpts);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }

    // ───────── Валидация: пустое имя ─────────

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        var payload = new { Name = "", Description = "Описание", Price = 10m };
        var response = await _client.PostAsJsonAsync("/api/items", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
        Assert.False(string.IsNullOrEmpty(error.RequestId));
    }

    // ───────── Валидация: отрицательная цена ─────────

    [Fact]
    public async Task Create_NegativePrice_Returns400()
    {
        var payload = new { Name = "Ластик", Description = "Белый", Price = -5m };
        var response = await _client.PostAsJsonAsync("/api/items", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
        Assert.Contains("price", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ───────── Валидация: слишком длинное имя ─────────

    [Fact]
    public async Task Create_NameTooLong_Returns400()
    {
        var longName = new string('А', 201);
        var payload = new { Name = longName, Description = "", Price = 10m };
        var response = await _client.PostAsJsonAsync("/api/items", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
    }

    // ───────── Валидация: слишком длинное описание ─────────

    [Fact]
    public async Task Create_DescriptionTooLong_Returns400()
    {
        var longDesc = new string('Б', 1001);
        var payload = new { Name = "Линейка", Description = longDesc, Price = 10m };
        var response = await _client.PostAsJsonAsync("/api/items", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        Assert.NotNull(error);
        Assert.Equal("validation", error!.Code);
    }

    // ───────── 404 по несуществующему идентификатору ─────────

    [Fact]
    public async Task GetById_NonExistent_Returns404WithRequestId()
    {
        var response = await _client.GetAsync($"/api/items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        Assert.NotNull(error);
        Assert.Equal("not_found", error!.Code);
        Assert.False(string.IsNullOrEmpty(error.RequestId), "Ответ об ошибке должен содержать RequestId");
    }

    // ───────── Заголовок X-Request-Id присутствует в ответе ─────────

    [Fact]
    public async Task Response_ContainsXRequestIdHeader()
    {
        var response = await _client.GetAsync("/api/items");

        Assert.True(response.Headers.Contains("X-Request-Id"));
        var requestId = response.Headers.GetValues("X-Request-Id").First();
        Assert.False(string.IsNullOrEmpty(requestId));
    }

    // ───────── Клиент может передать свой X-Request-Id ─────────

    [Fact]
    public async Task Request_CustomRequestId_IsPreserved()
    {
        var customId = "my-test-id-12345";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        request.Headers.Add("X-Request-Id", customId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Request-Id"));
        Assert.Equal(customId, response.Headers.GetValues("X-Request-Id").First());
    }

    // ───────── Фильтрация по имени через query-параметр ─────────

    [Fact]
    public async Task GetAll_FilterByName_ReturnsMatchingItems()
    {
        // Создаём два элемента
        await _client.PostAsJsonAsync("/api/items", new { Name = "Фильтр-Альфа", Description = "", Price = 1m });
        await _client.PostAsJsonAsync("/api/items", new { Name = "Фильтр-Бета", Description = "", Price = 2m });
        await _client.PostAsJsonAsync("/api/items", new { Name = "Другой", Description = "", Price = 3m });

        var response = await _client.GetAsync("/api/items?name=Фильтр");
        var items = await response.Content.ReadFromJsonAsync<Item[]>(JsonOpts);

        Assert.NotNull(items);
        Assert.True(items!.Length >= 2);
        Assert.All(items, i => Assert.Contains("Фильтр", i.Name));
    }

    // ───────── Сортировка по цене ─────────

    [Fact]
    public async Task GetAll_SortByPrice_ReturnsSorted()
    {
        await _client.PostAsJsonAsync("/api/items", new { Name = "СортЦена-Дорого", Description = "", Price = 9999m });
        await _client.PostAsJsonAsync("/api/items", new { Name = "СортЦена-Дёшево", Description = "", Price = 0.01m });

        var response = await _client.GetAsync("/api/items?sortBy=price");
        var items = await response.Content.ReadFromJsonAsync<Item[]>(JsonOpts);

        Assert.NotNull(items);
        Assert.True(items!.Length >= 2);
        // Первый элемент должен быть дешевле последнего
        Assert.True(items.First().Price <= items.Last().Price);
    }

    // ───────── Журнал содержит requestId и timeMs ─────────

    [Fact]
    public async Task Log_ContainsRequestIdAndTimeMs()
    {
        // Создаём отдельный экземпляр приложения с перехватчиком журнала
        var logProvider = new TestLoggerProvider();

        await using var customFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ILoggerProvider>(logProvider);
                });
            });

        var client = customFactory.CreateClient();
        await client.GetAsync("/api/items");

        // Ищем запись TimingAndLogMiddleware в журнале
        var timingLog = logProvider.Messages
            .FirstOrDefault(m => m.Contains("requestId=") && m.Contains("timeMs="));

        Assert.NotNull(timingLog);
        Assert.Contains("requestId=", timingLog!);
        Assert.Contains("timeMs=", timingLog);
        Assert.Contains("method=GET", timingLog);
        Assert.Contains("path=/api/items", timingLog);
        Assert.Contains("status=200", timingLog);
    }
}
