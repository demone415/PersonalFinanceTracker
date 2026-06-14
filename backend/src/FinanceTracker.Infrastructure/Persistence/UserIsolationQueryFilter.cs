using System.Linq.Expressions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// A DbContext that exposes the current caller so the data-isolation query
/// filter can reference the context instance (see <see cref="UserIsolationQueryFilter"/>).
/// </summary>
public interface IUserIsolatedContext
{
    ICurrentUserService CurrentUser { get; }
}

/// <summary>
/// Builds and applies the data-isolation global query filter (ARCHITECTURE.md
/// §11.2) to every entity implementing <see cref="IUserOwnedEntity"/>:
/// <c>e =&gt; ctx.CurrentUser.IsAdmin || e.UserId == ctx.CurrentUser.UserId</c>.
/// </summary>
/// <remarks>
/// The filter is rooted at the <b>DbContext instance</b> (not a captured service
/// constant) on purpose: EF caches the model per context type but re-binds the
/// context reference per query, so each scoped request sees its own caller and
/// admins bypass the filter. Intentional admin-wide queries opt out with
/// <c>IgnoreQueryFilters()</c>.
/// </remarks>
public static class UserIsolationQueryFilter
{
    public static void Apply<TContext>(ModelBuilder modelBuilder, TContext context)
        where TContext : DbContext, IUserIsolatedContext
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IUserOwnedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Build(entityType.ClrType, context));
        }
    }

    private static LambdaExpression Build(Type entityClrType, IUserIsolatedContext context)
    {
        // e => context.CurrentUser.IsAdmin || e.UserId == context.CurrentUser.UserId
        ParameterExpression entity = Expression.Parameter(entityClrType, "e");

        // Root at the context instance — EF re-binds this per query, unlike a
        // captured service constant which would freeze to the first instance.
        Expression currentUser = Expression.Property(
            Expression.Constant(context), nameof(IUserIsolatedContext.CurrentUser));

        Expression isAdmin = Expression.Property(currentUser, nameof(ICurrentUserService.IsAdmin));
        Expression currentUserId = Expression.Property(currentUser, nameof(ICurrentUserService.UserId));
        Expression userId = Expression.Property(entity, nameof(IUserOwnedEntity.UserId));

        // Lift e.UserId (Guid) to Guid? so it matches the nullable current user id.
        Expression ownedByCaller = Expression.Equal(
            Expression.Convert(userId, typeof(Guid?)), currentUserId);

        return Expression.Lambda(Expression.OrElse(isAdmin, ownedByCaller), entity);
    }
}
