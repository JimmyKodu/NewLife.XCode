using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NewLife;
using NewLife.Reflection;
using XCode.Configuration;

namespace XCode.EFCore;

/// <summary>XCode 与 EF Core 元数据映射。</summary>
public static class XCodeEntityTypeConfiguration
{
    /// <summary>将全部实体或指定实体应用到模型构建器。</summary>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="entityTypes">待注册的实体类型集合</param>
    public static void Apply(ModelBuilder modelBuilder, IEnumerable<Type>? entityTypes = null)
    {
        if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));

        var types = entityTypes?.ToList();
        if (types == null || types.Count == 0)
        {
            types = typeof(IEntity).GetAllSubclasses().ToList();
            if (types.Count == 0) types = EntityFactory.Entities.Keys.ToList();
        }

        foreach (var type in types)
        {
            if (type == null || !typeof(IEntity).IsAssignableFrom(type)) continue;

            Apply(modelBuilder, type);
        }
    }

    /// <summary>将单个实体类型的字段映射到 EF Core。</summary>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="entityType">实体类型</param>
    public static void Apply(ModelBuilder modelBuilder, Type entityType)
    {
        if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));
        if (entityType == null) throw new ArgumentNullException(nameof(entityType));

        var factory = EntityFactory.CreateFactory(entityType);
        var builder = modelBuilder.Entity(entityType);

        var table = factory.Table;
        builder.ToTable(table.TableName);

        var pks = table.PrimaryKeys;
        if (pks.Length > 0) builder.HasKey(pks.Select(e => e.Name).ToArray());

        foreach (var field in factory.AllFields)
        {
            var property = builder.Property(field.Name);

            if (!field.IsNullable && !field.IsIdentity) property.IsRequired();
            if (field.Length > 0) property.HasMaxLength(field.Length);

            // 仅在实体特性显式指定不同列名时进行映射，避免无意义的重复配置
            if (!field.ColumnName.IsNullOrEmpty() && !String.Equals(field.ColumnName, field.Name, StringComparison.OrdinalIgnoreCase))
                property.HasColumnName(field.ColumnName);

            var dc = field.Field;
            if (dc != null && dc.Precision > 0) property.HasPrecision(dc.Precision, dc.Scale);

            if (field.IsIdentity) property.ValueGeneratedOnAdd();
        }
    }
}
