using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Joins expression trees into one, so a rule written once elsewhere (the domain's SLA rule)
/// can sit inside a query's projection or filter and still be translated to SQL. Calling a
/// compiled delegate there instead would either fail to translate or run in memory.
/// </summary>
internal static class ExpressionComposition
{
    /// <summary><c>x =&gt; second(first(x))</c>.</summary>
    public static Expression<Func<T, TResult>> Then<T, TMiddle, TResult>(
        this Expression<Func<T, TMiddle>> first,
        Expression<Func<TMiddle, TResult>> second) =>
        Expression.Lambda<Func<T, TResult>>(
            ReplacingExpressionVisitor.Replace(second.Parameters[0], first.Body, second.Body),
            first.Parameters);

    /// <summary><c>x =&gt; projection(x, argument(x))</c>.</summary>
    public static Expression<Func<T, TResult>> With<T, TArgument, TResult>(
        this Expression<Func<T, TArgument, TResult>> projection,
        Expression<Func<T, TArgument>> argument)
    {
        var x = projection.Parameters[0];
        var argumentOfX = ReplacingExpressionVisitor.Replace(argument.Parameters[0], x, argument.Body);

        return Expression.Lambda<Func<T, TResult>>(
            ReplacingExpressionVisitor.Replace(projection.Parameters[1], argumentOfX, projection.Body),
            x);
    }
}
