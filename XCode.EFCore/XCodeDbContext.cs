using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace XCode.EFCore;

/// <summary>XCode EF Core DbContext 基类</summary>
/// <remarks>
/// 提供一个通用的 DbContext 实现，可以在 XCode 中使用 EF Core 特性。
/// 支持动态配置和模型构建。
/// </remarks>
public class XCodeDbContext : DbContext
{
    /// <summary>实例化</summary>
    public XCodeDbContext() : base() { }

    /// <summary>使用指定选项实例化</summary>
    public XCodeDbContext(DbContextOptions options) : base(options) { }

    /// <summary>配置模型</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 可以在这里添加自定义模型配置
        OnConfigureModel(modelBuilder);
    }

    /// <summary>自定义模型配置</summary>
    protected virtual void OnConfigureModel(ModelBuilder modelBuilder) { }
}

/// <summary>泛型 DbContext，用于特定实体类型</summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class XCodeDbContext<TEntity> : XCodeDbContext where TEntity : class
{
    /// <summary>实体集</summary>
    public DbSet<TEntity> Entities { get; set; } = null!;

    /// <summary>实例化</summary>
    public XCodeDbContext() : base() { }

    /// <summary>使用指定选项实例化</summary>
    public XCodeDbContext(DbContextOptions options) : base(options) { }
}

/// <summary>EF Core 扩展方法</summary>
public static class EFCoreExtensions
{
    /// <summary>将 XCode 实体注册到 EF Core DbContext</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="tableName">表名（可选）</param>
    /// <returns>实体类型构建器</returns>
    public static EntityTypeBuilder<TEntity> RegisterXCodeEntity<TEntity>(
        this ModelBuilder modelBuilder,
        String? tableName = null) where TEntity : class
    {
        var builder = modelBuilder.Entity<TEntity>();

        if (!String.IsNullOrEmpty(tableName))
            builder.ToTable(tableName);

        return builder;
    }

    /// <summary>创建 SQLite 的 DbContextOptions</summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>DbContext 选项</returns>
    public static DbContextOptions<TContext> CreateSqliteOptions<TContext>(String connectionString)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        // 需要安装 Microsoft.EntityFrameworkCore.Sqlite 包
        // builder.UseSqlite(connectionString);
        return builder.Options;
    }

    /// <summary>创建 SqlServer 的 DbContextOptions</summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>DbContext 选项</returns>
    public static DbContextOptions<TContext> CreateSqlServerOptions<TContext>(String connectionString)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        // 需要安装 Microsoft.EntityFrameworkCore.SqlServer 包
        // builder.UseSqlServer(connectionString);
        return builder.Options;
    }
}

/// <summary>EF Core EntityTypeBuilder 封装</summary>
public class EntityTypeBuilder<TEntity> where TEntity : class
{
    private readonly Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> _builder;

    internal EntityTypeBuilder(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
    {
        _builder = builder;
    }

    /// <summary>设置表名</summary>
    public EntityTypeBuilder<TEntity> ToTable(String name)
    {
        _builder.ToTable(name);
        return this;
    }

    /// <summary>设置主键</summary>
    public EntityTypeBuilder<TEntity> HasKey(params String[] propertyNames)
    {
        _builder.HasKey(propertyNames);
        return this;
    }

    /// <summary>忽略属性</summary>
    public EntityTypeBuilder<TEntity> Ignore(String propertyName)
    {
        _builder.Ignore(propertyName);
        return this;
    }

    /// <summary>隐式转换</summary>
    public static implicit operator EntityTypeBuilder<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        => new(builder);
}
