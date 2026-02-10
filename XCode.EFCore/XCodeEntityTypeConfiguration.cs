using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XCode.EFCore;

/// <summary>XCode 实体类型配置。读取 BindTable/BindColumn/DataObjectField 等特性，自动映射到 EF Core 模型</summary>
public class XCodeEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class
{
    /// <summary>配置实体类型</summary>
    /// <param name="builder">实体类型构建器</param>
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        var entityType = typeof(TEntity);

        ConfigureTable(builder, entityType);
        ConfigureColumns(builder, entityType);
    }

    private static void ConfigureTable(EntityTypeBuilder<TEntity> builder, Type entityType)
    {
        // 读取 BindTable 特性获取表名
        var bindTable = entityType.GetCustomAttribute<BindTableAttribute>();
        if (bindTable != null)
        {
            builder.ToTable(bindTable.Name);

            // 视图标记
            if (bindTable.IsView)
                builder.ToView(bindTable.Name);
        }

        // 读取 BindIndex 特性配置索引
        var bindIndexes = entityType.GetCustomAttributes<BindIndexAttribute>();
        foreach (var idx in bindIndexes)
        {
            if (String.IsNullOrEmpty(idx.Columns)) continue;

            var columns = idx.Columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (columns.Length == 0) continue;

            var indexBuilder = builder.HasIndex(columns);

            if (!String.IsNullOrEmpty(idx.Name))
                indexBuilder.HasDatabaseName(idx.Name);

            if (idx.Unique)
                indexBuilder.IsUnique();
        }
    }

    private static void ConfigureColumns(EntityTypeBuilder<TEntity> builder, Type entityType)
    {
        var keyProperties = new List<String>();

        foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // 跳过无 getter/setter 的属性
            if (!property.CanRead || !property.CanWrite) continue;

            // 跳过不可映射的类型（如集合、导航属性等）
            if (!IsSimpleType(property.PropertyType)) continue;

            var bindColumn = property.GetCustomAttribute<BindColumnAttribute>();
            var dataField = property.GetCustomAttribute<DataObjectFieldAttribute>();

            // 确定数据库列名
            var columnName = bindColumn?.Name ?? property.Name;
            var propBuilder = builder.Property(property.Name);

            if (!String.IsNullOrEmpty(columnName) && columnName != property.Name)
                propBuilder.HasColumnName(columnName);

            // 主键识别
            if (dataField?.PrimaryKey == true)
                keyProperties.Add(property.Name);

            // 自增标识
            if (dataField?.IsIdentity == true)
                propBuilder.ValueGeneratedOnAdd();

            // 可空性
            if (dataField != null)
                propBuilder.IsRequired(!dataField.IsNullable);

            // 字符串长度
            if (dataField?.Length > 0 && property.PropertyType == typeof(String))
                propBuilder.HasMaxLength(dataField.Length);

            // 精度和位数（Decimal 类型）
            if (bindColumn != null && bindColumn.Precision > 0)
                propBuilder.HasPrecision(bindColumn.Precision, bindColumn.Scale);
        }

        // 设置组合主键或单主键
        if (keyProperties.Count == 1)
            builder.HasKey(keyProperties[0]);
        else if (keyProperties.Count > 1)
            builder.HasKey([.. keyProperties]);
    }

    private static Boolean IsSimpleType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t.IsPrimitive || t.IsEnum) return true;
        if (t == typeof(String) || t == typeof(DateTime) || t == typeof(DateTimeOffset) ||
            t == typeof(Decimal) || t == typeof(Guid) || t == typeof(TimeSpan) ||
            t == typeof(Byte[]))
            return true;

        return false;
    }
}
