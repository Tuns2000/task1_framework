using System.Collections.Concurrent;
using Pr1.MinWebService.Domain;

namespace Pr1.MinWebService.Services;

/// <summary>
/// Простое хранилище в памяти процесса.
/// ConcurrentDictionary обеспечивает потокобезопасность при параллельных запросах.
/// </summary>
public sealed class InMemoryItemRepository : IItemRepository
{
    private readonly ConcurrentDictionary<Guid, Item> _items = new();

    public IReadOnlyCollection<Item> GetAll(string? nameFilter = null, string? sortBy = null, bool descending = false)
    {
        IEnumerable<Item> query = _items.Values;

        // Фильтрация по имени (подстрока, без учёта регистра)
        if (!string.IsNullOrWhiteSpace(nameFilter))
            query = query.Where(x => x.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));

        // Сортировка
        query = sortBy?.ToLowerInvariant() switch
        {
            "price" => descending ? query.OrderByDescending(x => x.Price) : query.OrderBy(x => x.Price),
            "date"  => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _       => descending ? query.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
                                  : query.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
        };

        return query.ToArray();
    }

    public Item? GetById(Guid id)
        => _items.TryGetValue(id, out var item) ? item : null;

    public Item Create(string name, string description, decimal price)
    {
        var id = Guid.NewGuid();
        var item = new Item(id, name, description, price, DateTime.UtcNow);

        _items[id] = item;
        return item;
    }
}
