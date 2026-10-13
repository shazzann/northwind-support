using System.Linq.Expressions;

namespace SupportDesk.Infrastructure.Queries;

internal static class QueryableExtensions
{
    /// <summary>
    /// Adds the predicate only when <paramref name="condition"/> holds, so optional filters read
    /// as one line each. The predicate stays an expression and is translated to SQL.
    /// </summary>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> source,
        bool condition,
        Expression<Func<T, bool>> predicate) =>
        condition ? source.Where(predicate) : source;
}
