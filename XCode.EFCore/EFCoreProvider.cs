using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using XCode.DataAccessLayer;

namespace XCode.EFCore;

/// <summary>EF Core 数据库提供程序</summary>
/// <remarks>
/// 提供 EF Core 与 XCode DAL 层的集成能力。
/// 允许通过标准 XCode API 使用 EF Core 作为底层数据访问引擎。
/// </remarks>
public static class EFCoreProvider
{
    /// <summary>注册 EF Core 作为数据库提供程序</summary>
    /// <param name="connName">连接名称</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="factory">数据库提供程序工厂</param>
    /// <param name="optionsBuilder">DbContext 选项构建器</param>
    public static void Register(
        String connName,
        String connectionString,
        DbProviderFactory factory,
        Func<String, DbContextOptions>? optionsBuilder = null)
    {
        // 添加连接字符串
        DAL.AddConnStr(connName, connectionString, null, "EFCore");

        // 注册解析事件
        DAL.OnResolve += (sender, e) =>
        {
            if (e.Name == connName)
            {
                var database = new EFCoreDatabase
                {
                    ConnName = connName,
                    ConnectionString = connectionString,
                    Factory = factory,
                    OptionsBuilder = optionsBuilder
                };

                // 这里我们需要一种方式将 database 注入到 DAL 中
                // 由于 DAL 的设计，我们可能需要通过其他方式集成
            }
        };
    }

    /// <summary>创建 EF Core 数据库实例</summary>
    /// <param name="connName">连接名称</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="factory">数据库提供程序工厂</param>
    /// <returns></returns>
    public static EFCoreDatabase CreateDatabase(
        String connName,
        String connectionString,
        DbProviderFactory factory)
    {
        return new EFCoreDatabase
        {
            ConnName = connName,
            ConnectionString = connectionString,
            Factory = factory
        };
    }

    /// <summary>创建带有 DbContext 支持的数据库实例</summary>
    /// <typeparam name="TContext">DbContext 类型</typeparam>
    /// <param name="connName">连接名称</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="factory">数据库提供程序工厂</param>
    /// <param name="optionsBuilder">选项构建器</param>
    /// <returns></returns>
    public static EFCoreDatabase CreateDatabase<TContext>(
        String connName,
        String connectionString,
        DbProviderFactory factory,
        Func<String, DbContextOptions> optionsBuilder) where TContext : DbContext
    {
        return new EFCoreDatabase
        {
            ConnName = connName,
            ConnectionString = connectionString,
            Factory = factory,
            OptionsBuilder = optionsBuilder,
            DbContextFactory = options => (TContext)Activator.CreateInstance(typeof(TContext), options)!
        };
    }
}

/// <summary>EF Core 数据库类型</summary>
public enum EFCoreDatabaseType
{
    /// <summary>SQLite</summary>
    SQLite,

    /// <summary>SQL Server</summary>
    SqlServer,

    /// <summary>MySQL</summary>
    MySQL,

    /// <summary>PostgreSQL</summary>
    PostgreSQL,

    /// <summary>Oracle</summary>
    Oracle,

    /// <summary>其他</summary>
    Other
}

/// <summary>EF Core 配置</summary>
public class EFCoreOptions
{
    /// <summary>连接名称</summary>
    public String ConnName { get; set; } = String.Empty;

    /// <summary>连接字符串</summary>
    public String ConnectionString { get; set; } = String.Empty;

    /// <summary>数据库类型</summary>
    public EFCoreDatabaseType DatabaseType { get; set; }

    /// <summary>是否启用敏感数据日志</summary>
    public Boolean EnableSensitiveDataLogging { get; set; }

    /// <summary>是否启用详细错误</summary>
    public Boolean EnableDetailedErrors { get; set; }

    /// <summary>命令超时（秒）</summary>
    public Int32 CommandTimeout { get; set; } = 30;

    /// <summary>最大重试次数</summary>
    public Int32 MaxRetryCount { get; set; } = 3;

    /// <summary>最大重试延迟（秒）</summary>
    public Int32 MaxRetryDelay { get; set; } = 30;
}
