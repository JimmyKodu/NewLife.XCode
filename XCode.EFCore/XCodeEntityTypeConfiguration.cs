using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XCode.Configuration;

namespace XCode.EFCore;

/// <summary>XCode 实体到 EF Core 实体的映射配置</summary>
/// <typeparam name="TEntity">XCode 实体类型</typeparam>
public class XCodeEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : Entity<TEntity>, new()
{
    /// <summary>配置实体</summary>
    /// <param name="builder">实体类型构建器</param>
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        // 获取表元数据
        var table = Entity<TEntity>.Meta.Table;
        if (table == null) return;

        // 配置表名
        if (!table.TableName.IsNullOrEmpty())
            builder.ToTable(table.TableName);

        // 配置主键
        var primaryKeys = table.PrimaryKeys;
        if (primaryKeys != null && primaryKeys.Length > 0)
        {
            var keyNames = primaryKeys.Select(k => k.Name).ToArray();
            if (keyNames.Length > 0)
                builder.HasKey(keyNames);
        }

        // 配置字段
        var allFields = Entity<TEntity>.Meta.AllFields;
        if (allFields != null && allFields.Length > 0)
        {
            foreach (var field in allFields)
            {
                try
                {
                    var property = builder.Property(field.Name);

                    // 配置列名
                    if (!field.ColumnName.IsNullOrEmpty() && field.ColumnName != field.Name)
                        property.HasColumnName(field.ColumnName);

                    // 配置必填
                    if (!field.IsNullable)
                        property.IsRequired();

                    // 配置长度
                    if (field.Length > 0 && field.Type == typeof(String))
                        property.HasMaxLength(field.Length);

                    // 配置精度（从 Column 特性获取）
                    var col = field.Column;
                    if (col != null && col.Precision > 0 && col.Scale > 0)
                        property.HasPrecision(col.Precision, col.Scale);

                    // 配置默认值（从 Column 特性获取）
                    if (col != null && !col.DefaultValue.IsNullOrEmpty())
                        property.HasDefaultValueSql(col.DefaultValue);

                    // 配置自增
                    if (field.IsIdentity)
                        property.ValueGeneratedOnAdd();
                }
                catch
                {
                    // 忽略配置失败的字段
                }
            }
        }

        // 配置索引（从 DataTable 获取）
        if (table.DataTable != null && table.DataTable.Indexes != null)
        {
            var indexes = table.DataTable.Indexes;
            foreach (var index in indexes)
            {
                if (index.Columns == null || index.Columns.Length == 0) continue;

                try
                {
                    // index.Columns 是字符串数组
                    var indexBuilder = builder.HasIndex(index.Columns);

                    if (index.Unique)
                        indexBuilder.IsUnique();
                }
                catch
                {
                    // 忽略配置失败的索引
                }
            }
        }
    }
}

/// <summary>XCode 实体映射扩展方法</summary>
public static class XCodeEntityConfigurationExtensions
{
    /// <summary>应用 XCode 实体配置</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="modelBuilder">模型构建器</param>
    /// <returns>模型构建器</returns>
    public static ModelBuilder ApplyXCodeConfiguration<TEntity>(this ModelBuilder modelBuilder)
        where TEntity : Entity<TEntity>, new()
    {
        modelBuilder.ApplyConfiguration(new XCodeEntityTypeConfiguration<TEntity>());
        return modelBuilder;
    }

    /// <summary>批量应用 XCode 实体配置</summary>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="assembly">包含实体的程序集</param>
    /// <returns>模型构建器</returns>
    public static ModelBuilder ApplyXCodeConfigurations(this ModelBuilder modelBuilder, Assembly assembly)
    {
        var entityTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && IsXCodeEntity(t))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            var configType = typeof(XCodeEntityTypeConfiguration<>).MakeGenericType(entityType);
            var config = Activator.CreateInstance(configType);
            if (config != null)
            {
                var applyMethod = typeof(ModelBuilder)
                    .GetMethod(nameof(ModelBuilder.ApplyConfiguration), BindingFlags.Public | BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType);
                applyMethod?.Invoke(modelBuilder, [config]);
            }
        }

        return modelBuilder;
    }

    private static Boolean IsXCodeEntity(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(Entity<>))
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }
}
