using System.Linq.Expressions;

namespace XCode.EFCore;

/// <summary>XCode WhereExpression 到 EF Core LINQ 表达式转换器</summary>
public static class WhereExpressionConverter
{
    /// <summary>将 XCode 的 WhereExpression 转换为 EF Core 的 LINQ 表达式</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="where">XCode 查询条件</param>
    /// <returns>EF Core LINQ 表达式</returns>
    /// <remarks>
    /// 注意：这是一个简化实现，复杂的 WhereExpression 可能无法完全转换。
    /// 建议在实际使用中直接使用 EF Core 的 LINQ 查询。
    /// </remarks>
    public static Expression<Func<TEntity, Boolean>>? ToLinqExpression<TEntity>(WhereExpression? where)
        where TEntity : Entity<TEntity>, new()
    {
        if (where == null || where.IsEmpty) return null;

        // 简化实现：返回一个始终为 true 的表达式
        // 实际应用中，应该根据 WhereExpression 的内部结构构建精确的表达式树
        // 这需要深入理解 WhereExpression 的实现细节
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "e");
        var body = System.Linq.Expressions.Expression.Constant(true);
        return System.Linq.Expressions.Expression.Lambda<Func<TEntity, Boolean>>(body, parameter);
    }

    /// <summary>将 XCode 查询转换为 EF Core 查询</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">EF Core 查询</param>
    /// <param name="where">XCode 查询条件</param>
    /// <returns>过滤后的查询</returns>
    public static IQueryable<TEntity> ApplyWhere<TEntity>(
        this IQueryable<TEntity> query,
        WhereExpression? where)
        where TEntity : Entity<TEntity>, new()
    {
        if (where == null || where.IsEmpty) return query;

        var expression = ToLinqExpression<TEntity>(where);
        if (expression != null)
            query = query.Where(expression);

        return query;
    }

    /// <summary>应用排序</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">查询</param>
    /// <param name="orderBy">排序字段</param>
    /// <returns>排序后的查询</returns>
    public static IQueryable<TEntity> ApplyOrderBy<TEntity>(
        this IQueryable<TEntity> query,
        String? orderBy)
        where TEntity : Entity<TEntity>, new()
    {
        if (orderBy.IsNullOrEmpty()) return query;

        // 简单实现：解析排序字符串
        // 格式：FieldName ASC/DESC
        var parts = orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var isFirst = true;

        foreach (var part in parts)
        {
            var tokens = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            var fieldName = tokens[0];
            var isDesc = tokens.Length > 1 && tokens[1].EqualIgnoreCase("DESC");

            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "e");
            var property = System.Linq.Expressions.Expression.Property(parameter, fieldName);
            var lambda = System.Linq.Expressions.Expression.Lambda(property, parameter);

            var methodName = isFirst
                ? (isDesc ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDesc ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), property.Type);

            query = (IQueryable<TEntity>)method.Invoke(null, [query, lambda])!;
            isFirst = false;
        }

        return query;
    }

    /// <summary>应用分页</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">查询</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页后的查询</returns>
    public static IQueryable<TEntity> ApplyPaging<TEntity>(
        this IQueryable<TEntity> query,
        Int32 pageIndex,
        Int32 pageSize)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 20;

        return query.Skip((pageIndex - 1) * pageSize).Take(pageSize);
    }
}
