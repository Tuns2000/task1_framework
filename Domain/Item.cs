namespace Pr1.MinWebService.Domain;

/// <summary>
/// Элемент каталога — учебная задача.
/// </summary>
public sealed record Item(Guid Id, string Name, string Description, decimal Price, DateTime CreatedAt);
