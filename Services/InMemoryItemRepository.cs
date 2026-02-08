using System.Collections.Concurrent;
using Pr1.MinWebService.Domain;

namespace Pr1.MinWebService.Services;

/// <summary>
/// Простое хранилище в памяти процесса.
/// </summary>
public sealed class InMemoryItemRepository : IItemRepository
{
    private readonly ConcurrentDictionary<Guid, Item> _items = new();

    public IReadOnlyCollection<Item> GetAll()
        => _items.Values
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public Item? GetById(Guid id)
        => _items.TryGetValue(id, out var item) ? item : null;

    public Item Create(string name, decimal price)
    {
        var id = Guid.NewGuid();
        var item = new Item(id, name, price);

        _items[id] = item;
        return item;
    }
}
