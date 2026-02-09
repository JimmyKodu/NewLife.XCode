#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace XCode.EntityFrameworkCore;

/// <summary>XCode DbContext 基类</summary>
/// <remarks>
/// 提供 XCode 实体与 EF Core 的桥接，支持：
/// 1. 从 XCode 元数据动态构建 EF Core 模型
/// 2. 多数据库支持（SQL Server、SQLite、MySQL、PostgreSQL等）
/// 3. 与 XCode 现有 API 无缝集成
/// </remarks>
public class XCodeDbContext : DbContext
{
    #region 属性
    /// <summary>连接字符串</summary>
    public String ConnectionString { get; }

    /// <summary>数据库类型</summary>
    public DatabaseType DbType { get; }

    /// <summary>连接名称</summary>
    public String? ConnName { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="dbType">数据库类型</param>
    public XCodeDbContext(String connectionString, DatabaseType dbType)
    {
        if (connectionString.IsNullOrEmpty()) throw new ArgumentNullException(nameof(connectionString));

        ConnectionString = connectionString;
        DbType = dbType;
    }

    /// <summary>实例化</summary>
    /// <param name="options">DbContext 选项</param>
    public XCodeDbContext(DbContextOptions<XCodeDbContext> options) : base(options)
    {
        ConnectionString = String.Empty;
        DbType = DatabaseType.None;
    }
    #endregion

    #region 配置
    /// <summary>配置数据库提供程序</summary>
    /// <param name="optionsBuilder">选项构建器</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;

        // 根据数据库类型选择相应的提供程序
        switch (DbType)
        {
            case DatabaseType.SqlServer:
                optionsBuilder.UseSqlServer(ConnectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure();
                    sqlOptions.CommandTimeout(30);
                });
                break;

            case DatabaseType.SQLite:
                optionsBuilder.UseSqlite(ConnectionString, sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                });
                break;

            case DatabaseType.MySql:
                // MySQL 支持需要额外的 Provider
                // optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
                throw new NotSupportedException($"MySQL 支持需要引用 Pomelo.EntityFrameworkCore.MySql 包");

            case DatabaseType.PostgreSQL:
                // PostgreSQL 支持需要额外的 Provider
                // optionsBuilder.UseNpgsql(ConnectionString);
                throw new NotSupportedException($"PostgreSQL 支持需要引用 Npgsql.EntityFrameworkCore.PostgreSQL 包");

            case DatabaseType.Oracle:
                throw new NotSupportedException($"Oracle 支持需要引用 Oracle.EntityFrameworkCore 包");

            default:
                throw new NotSupportedException($"不支持的数据库类型：{DbType}");
        }

        // 启用敏感数据日志（开发环境）
#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();
#endif
    }

    /// <summary>配置实体模型</summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 从 XCode EntityFactory 获取所有实体元数据
        var factories = EntityFactory.GetFactories();

        foreach (var factory in factories)
        {
            // 仅配置与当前连接名匹配的实体
            if (!ConnName.IsNullOrEmpty() && factory.ConnName != ConnName)
                continue;

            try
            {
                ConfigureEntity(modelBuilder, factory);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"配置实体 {factory.EntityType.Name} 失败：{ex.Message}");
            }
        }
    }

    /// <summary>配置单个实体</summary>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="factory">实体工厂</param>
    protected virtual void ConfigureEntity(ModelBuilder modelBuilder, IEntityFactory factory)
    {
        if (factory == null) return;

        var entityType = factory.EntityType;
        if (entityType == null) return;

        // 使用反射调用泛型方法 Entity<T>
        var entityMethod = typeof(ModelBuilder).GetMethod(nameof(ModelBuilder.Entity), 1, [])
            ?.MakeGenericMethod(entityType);

        if (entityMethod == null) return;

        var entityTypeBuilder = entityMethod.Invoke(modelBuilder, null);
        if (entityTypeBuilder == null) return;

        var table = factory.Table;
        if (table == null) return;

        // 配置表名
        var toTableMethod = entityTypeBuilder.GetType()
            .GetMethod(nameof(Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.ToTable),
                [typeof(String)]);

        toTableMethod?.Invoke(entityTypeBuilder, [table.TableName]);

        // 配置主键
        ConfigurePrimaryKey(entityTypeBuilder, table);

        // 配置字段
        ConfigureProperties(entityTypeBuilder, table);

        // 配置索引
        ConfigureIndexes(entityTypeBuilder, table);
    }

    /// <summary>配置主键</summary>
    /// <param name="entityTypeBuilder">实体类型构建器</param>
    /// <param name="table">表元数据</param>
    protected virtual void ConfigurePrimaryKey(Object entityTypeBuilder, IDataTable table)
    {
        var pks = table.Columns.Where(c => c.PrimaryKey).ToArray();
        if (pks.Length == 0) return;

        // 获取 HasKey 方法
        var hasKeyMethod = entityTypeBuilder.GetType()
            .GetMethods()
            .FirstOrDefault(m => m.Name == "HasKey" && m.GetParameters().Length == 1);

        if (hasKeyMethod == null) return;

        // 构造主键表达式
        var pkNames = pks.Select(pk => pk.Name).ToArray();

        // 简化处理：直接使用属性名数组
        hasKeyMethod.Invoke(entityTypeBuilder, new Object[] { pkNames });
    }

    /// <summary>配置属性</summary>
    /// <param name="entityTypeBuilder">实体类型构建器</param>
    /// <param name="table">表元数据</param>
    protected virtual void ConfigureProperties(Object entityTypeBuilder, IDataTable table)
    {
        foreach (var column in table.Columns)
        {
            try
            {
                ConfigureProperty(entityTypeBuilder, column);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"配置属性 {column.Name} 失败：{ex.Message}");
            }
        }
    }

    /// <summary>配置单个属性</summary>
    /// <param name="entityTypeBuilder">实体类型构建器</param>
    /// <param name="column">列元数据</param>
    protected virtual void ConfigureProperty(Object entityTypeBuilder, IDataColumn column)
    {
        // 获取 Property 方法
        var propertyMethod = entityTypeBuilder.GetType()
            .GetMethods()
            .FirstOrDefault(m => m.Name == "Property" &&
                                 m.GetParameters().Length == 1 &&
                                 m.GetParameters()[0].ParameterType == typeof(String));

        if (propertyMethod == null) return;

        var propertyBuilder = propertyMethod.Invoke(entityTypeBuilder, [column.Name]);
        if (propertyBuilder == null) return;

        // 配置列名（如果与属性名不同）
        if (!column.ColumnName.IsNullOrEmpty() && column.ColumnName != column.Name)
        {
            var hasColumnNameMethod = propertyBuilder.GetType()
                .GetMethod("HasColumnName", [typeof(String)]);
            hasColumnNameMethod?.Invoke(propertyBuilder, [column.ColumnName]);
        }

        // 配置必填项
        if (!column.Nullable)
        {
            var isRequiredMethod = propertyBuilder.GetType()
                .GetMethod("IsRequired", Type.EmptyTypes);
            isRequiredMethod?.Invoke(propertyBuilder, null);
        }

        // 配置最大长度
        if (column.Length > 0 && column.Length < 10000)
        {
            var hasMaxLengthMethod = propertyBuilder.GetType()
                .GetMethod("HasMaxLength", [typeof(Int32)]);
            hasMaxLengthMethod?.Invoke(propertyBuilder, [column.Length]);
        }

        // 配置精度（decimal 类型）
        if (column.Precision > 0 && column.Scale >= 0)
        {
            var hasPrecisionMethod = propertyBuilder.GetType()
                .GetMethod("HasPrecision", [typeof(Int32), typeof(Int32)]);
            hasPrecisionMethod?.Invoke(propertyBuilder, [column.Precision, column.Scale]);
        }
    }

    /// <summary>配置索引</summary>
    /// <param name="entityTypeBuilder">实体类型构建器</param>
    /// <param name="table">表元数据</param>
    protected virtual void ConfigureIndexes(Object entityTypeBuilder, IDataTable table)
    {
        if (table.Indexes == null || table.Indexes.Count == 0) return;

        foreach (var index in table.Indexes)
        {
            try
            {
                ConfigureIndex(entityTypeBuilder, index);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"配置索引 {index.Name} 失败：{ex.Message}");
            }
        }
    }

    /// <summary>配置单个索引</summary>
    /// <param name="entityTypeBuilder">实体类型构建器</param>
    /// <param name="index">索引元数据</param>
    protected virtual void ConfigureIndex(Object entityTypeBuilder, IDataIndex index)
    {
        if (index.Columns == null || index.Columns.Length == 0) return;

        // 获取 HasIndex 方法
        var hasIndexMethod = entityTypeBuilder.GetType()
            .GetMethods()
            .FirstOrDefault(m => m.Name == "HasIndex" &&
                                 m.GetParameters().Length == 1 &&
                                 m.GetParameters()[0].ParameterType == typeof(String[]));

        if (hasIndexMethod == null) return;

        var indexBuilder = hasIndexMethod.Invoke(entityTypeBuilder, new Object[] { index.Columns });
        if (indexBuilder == null) return;

        // 配置唯一索引
        if (index.Unique)
        {
            var isUniqueMethod = indexBuilder.GetType()
                .GetMethod("IsUnique", Type.EmptyTypes);
            isUniqueMethod?.Invoke(indexBuilder, null);
        }
    }
    #endregion

    #region 静态方法
    /// <summary>创建 DbContext 实例</summary>
    /// <param name="connName">连接名</param>
    /// <returns></returns>
    public static XCodeDbContext Create(String connName)
    {
        var dal = DAL.Create(connName);
        if (dal == null) throw new ArgumentException($"未找到连接名为 {connName} 的数据库配置", nameof(connName));

        var context = new XCodeDbContext(dal.ConnStr, dal.DbType)
        {
            ConnName = connName
        };

        return context;
    }
    #endregion
}
#endif
