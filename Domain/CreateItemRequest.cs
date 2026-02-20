namespace Pr1.MinWebService.Domain;

/// <summary>
/// Входные данные для создания элемента.
/// </summary>
public sealed record CreateItemRequest(string Name, string Description, decimal Price);
