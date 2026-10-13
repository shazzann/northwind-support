using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to <see cref="Category"/> aggregates.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>Loads a category, untracked, or null when it does not exist.</summary>
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);
}
