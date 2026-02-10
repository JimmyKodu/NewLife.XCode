using Microsoft.EntityFrameworkCore.Storage;
using XCode.DataAccessLayer;

namespace XCode.EFCore;

/// <summary>XCode 与 EF Core 集成的 DbContext 基类</summary>
/// <remarks>
/// 提供 XCode 实体与 EF Core 之间的桥梁，支持：
/// 1. XCode 实体映射到 EF Core
/// 2. 共享数据库连接
/// 3. 事务协调
/// 4. 缓存策略集成
/// </remarks>
public abstract class XCodeDbContext : DbContext
{
    /// <summary>数据库连接名称</summary>
    public String? ConnName { get; set; }

    /// <summary>构造函数</summary>
    protected XCodeDbContext()
    {
    }

    /// <summary>构造函数</summary>
    /// <param name="options">配置选项</param>
    protected XCodeDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>配置数据库连接</summary>
    /// <param name="optionsBuilder">选项构建器</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && !ConnName.IsNullOrEmpty())
        {
            var dal = DAL.Create(ConnName);
            if (dal != null)
            {
                // 根据 XCode 的数据库类型配置 EF Core
                var connStr = dal.Db.ConnectionString;
                var dbType = dal.DbType;

                switch (dbType)
                {
                    case DatabaseType.SqlServer:
                        optionsBuilder.UseSqlServer(connStr);
                        break;
                    case DatabaseType.MySql:
                        optionsBuilder.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
                        break;
                    case DatabaseType.SQLite:
                        optionsBuilder.UseSqlite(connStr);
                        break;
                    case DatabaseType.PostgreSQL:
                        optionsBuilder.UseNpgsql(connStr);
                        break;
                    case DatabaseType.Oracle:
                        optionsBuilder.UseOracle(connStr);
                        break;
                    default:
                        throw new NotSupportedException($"数据库类型 {dbType} 暂不支持 EF Core 集成");
                }
            }
        }

        base.OnConfiguring(optionsBuilder);
    }

    /// <summary>配置模型</summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用所有实体配置
        ApplyEntityConfigurations(modelBuilder);
    }

    /// <summary>应用实体配置</summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected virtual void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        // 子类可重写此方法来配置实体映射
    }

    /// <summary>开始事务</summary>
    /// <returns>事务对象</returns>
    public IDbContextTransaction BeginTransaction()
    {
        return Database.BeginTransaction();
    }

    /// <summary>异步开始事务</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事务对象</returns>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }
}
