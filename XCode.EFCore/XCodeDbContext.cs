using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NewLife.Reflection;

namespace XCode.EFCore;

/// <summary>基于 XCode 元数据的 EF Core 上下文。</summary>
public class XCodeDbContext(DbContextOptions options, IEnumerable<Type>? entityTypes = null) : DbContext(options)
{
    private readonly IReadOnlyCollection<Type>? _entityTypes = entityTypes?.ToList();

    /// <summary>应用模型构建逻辑。</summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));

        base.OnModelCreating(modelBuilder);

        XCodeEntityTypeConfiguration.Apply(modelBuilder, _entityTypes);
    }
}
