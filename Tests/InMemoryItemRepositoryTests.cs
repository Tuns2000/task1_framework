using Pr1.MinWebService.Services;

namespace Pr1.MinWebService.Tests;

/// <summary>
/// Юнит-тесты для InMemoryItemRepository — проверка логики хранилища в памяти.
/// </summary>
public class InMemoryItemRepositoryTests
{
    private readonly InMemoryItemRepository _repo = new();

    [Fact]
    public void GetAll_EmptyRepository_ReturnsEmpty()
    {
        var items = _repo.GetAll();
        Assert.Empty(items);
    }

    [Fact]
    public void Create_ReturnsItemWithGeneratedId()
    {
        var item = _repo.Create("Ручка", "Шариковая ручка", 15.50m);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("Ручка", item.Name);
        Assert.Equal("Шариковая ручка", item.Description);
        Assert.Equal(15.50m, item.Price);
    }

    [Fact]
    public void GetById_AfterCreate_ReturnsSameItem()
    {
        var created = _repo.Create("Тетрадь", "48 листов", 45m);
        var found = _repo.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal(created.Name, found.Name);
    }

    [Fact]
    public void GetById_NonExistent_ReturnsNull()
    {
        var found = _repo.GetById(Guid.NewGuid());
        Assert.Null(found);
    }

    [Fact]
    public void GetAll_ReturnsAllCreatedItems()
    {
        _repo.Create("Альфа", "Описание А", 10m);
        _repo.Create("Бета", "Описание Б", 20m);

        var items = _repo.GetAll();
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void GetAll_SortedByNameByDefault()
    {
        _repo.Create("Яблоко", "", 50m);
        _repo.Create("Арбуз", "", 30m);

        var items = _repo.GetAll();
        Assert.Equal("Арбуз", items.First().Name);
        Assert.Equal("Яблоко", items.Last().Name);
    }

    [Fact]
    public void GetAll_FilterByName()
    {
        _repo.Create("Молоко", "", 80m);
        _repo.Create("Масло", "", 120m);
        _repo.Create("Хлеб", "", 40m);

        var filtered = _repo.GetAll(nameFilter: "мол");
        Assert.Single(filtered);
        Assert.Equal("Молоко", filtered.First().Name);
    }

    [Fact]
    public void GetAll_SortByPrice()
    {
        _repo.Create("Дорогой", "", 999m);
        _repo.Create("Дешёвый", "", 1m);

        var items = _repo.GetAll(sortBy: "price");
        Assert.Equal(1m, items.First().Price);
        Assert.Equal(999m, items.Last().Price);
    }

    [Fact]
    public void GetAll_SortByPriceDescending()
    {
        _repo.Create("Дорогой", "", 999m);
        _repo.Create("Дешёвый", "", 1m);

        var items = _repo.GetAll(sortBy: "price", descending: true);
        Assert.Equal(999m, items.First().Price);
        Assert.Equal(1m, items.Last().Price);
    }

    [Fact]
    public void Create_SetsCreatedAtToRecentTime()
    {
        var before = DateTime.UtcNow;
        var item = _repo.Create("Часы", "Наручные", 500m);
        var after = DateTime.UtcNow;

        Assert.InRange(item.CreatedAt, before, after);
    }

    [Fact]
    public void GetAll_SortByDate()
    {
        _repo.Create("Первый", "", 10m);
        Thread.Sleep(10); // гарантируем разницу во времени
        _repo.Create("Второй", "", 20m);

        var items = _repo.GetAll(sortBy: "date");
        Assert.Equal("Первый", items.First().Name);
        Assert.Equal("Второй", items.Last().Name);
    }
}
