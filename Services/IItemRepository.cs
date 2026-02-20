using Pr1.MinWebService.Domain;

namespace Pr1.MinWebService.Services;

public interface IItemRepository
{
    IReadOnlyCollection<Item> GetAll(string? nameFilter = null, string? sortBy = null, bool descending = false);

    Item? GetById(Guid id);

    Item Create(string name, string description, decimal price);
}
