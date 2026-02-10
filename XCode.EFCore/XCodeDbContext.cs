using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace XCode.EFCore;

/// <summary>XCode 的 EF Core 10 上下文</summary>
public class XCodeDbContext : DbContext
{
    private readonly IReadOnlyList<Type> _entityTypes;

    /// <summary>使用实体类型列表创建上下文</summary>
    /// <param name="options">上下文配置</param>
    /// <param name="entityTypes">实体类型集合</param>
    public XCodeDbContext(DbContextOptions options, IEnumerable<Type> entityTypes) : base(options)
    {
        if (entityTypes == null) throw new ArgumentNullException(nameof(entityTypes));

        _entityTypes = entityTypes.Where(entityType => entityType != null).Distinct().ToArray();
    }

    /// <summary>使用连接名扫描实体类型</summary>
    /// <param name="options">上下文配置</param>
    /// <param name="connName">连接名</param>
    public XCodeDbContext(DbContextOptions options, String connName) : this(options, ResolveEntities(connName))
    {
    }

    private static IEnumerable<Type> ResolveEntities(String connName)
    {
        if (connName == null) throw new ArgumentNullException(nameof(connName));

        return EntityFactory.LoadEntities(connName);
    }

    /// <summary>构建模型映射</summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in _entityTypes)
        {
            XCodeEntityTypeConfiguration.Configure(modelBuilder, entityType);
        }
    }
}
