using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XCode.Configuration;

namespace XCode.EFCore;

/// <summary>基于 XCode 元数据的 EF Core 10 实体映射</summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public sealed class XCodeEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class, IEntity
{
    /// <summary>配置实体映射</summary>
    /// <param name="builder">实体类型构建器</param>
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var table = Entity<TEntity>.Meta.Table;
        XCodeEntityTypeConfiguration.Configure(builder, table);
    }
}

/// <summary>基于 XCode 元数据的 EF Core 10 映射辅助</summary>
public static class XCodeEntityTypeConfiguration
{
    /// <summary>使用 XCode 元数据配置 EF Core 映射</summary>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="entityType">实体类型</param>
    public static void Configure(ModelBuilder modelBuilder, Type entityType)
    {
        if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));
        if (entityType == null) throw new ArgumentNullException(nameof(entityType));
        if (!typeof(IEntity).IsAssignableFrom(entityType))
            throw new ArgumentOutOfRangeException(nameof(entityType), "实体类型必须实现 IEntity。");

        var table = TableItem.Create(entityType);
        var builder = modelBuilder.Entity(entityType);
        Configure(builder, table);
    }

    internal static void Configure(EntityTypeBuilder builder, TableItem table)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (table == null) throw new ArgumentNullException(nameof(table));

        if (table.DataTable.IsView)
            builder.ToView(table.TableName);
        else
            builder.ToTable(table.TableName);

        var primaryKeys = table.PrimaryKeys;
        if (primaryKeys.Length > 0)
        {
            var keyNames = primaryKeys.Select(item => item.Name).ToArray();
            builder.HasKey(keyNames);
        }
        else
            builder.HasNoKey();

        foreach (var field in table.Fields)
        {
            if (field.Property == null) continue;

            var property = builder.Property(field.Name);
            if (!String.IsNullOrEmpty(field.ColumnName)) property.HasColumnName(field.ColumnName);
            if (field.Length > 0) property.HasMaxLength(field.Length);
            if (!field.IsNullable) property.IsRequired();
            if (field.IsIdentity) property.ValueGeneratedOnAdd();
        }

        var indexedColumns = new HashSet<String>(table.Fields.Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var index in table.DataTable.Indexes)
        {
            if (index.PrimaryKey) continue;
            if (index.Columns == null || index.Columns.Length == 0) continue;
            if (!index.Columns.All(column => indexedColumns.Contains(column))) continue;

            var indexBuilder = builder.HasIndex(index.Columns);
            if (!String.IsNullOrEmpty(index.Name)) indexBuilder.HasDatabaseName(index.Name);
            if (index.Unique) indexBuilder.IsUnique();
        }
    }
}
