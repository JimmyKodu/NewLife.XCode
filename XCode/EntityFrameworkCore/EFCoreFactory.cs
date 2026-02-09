#if NET10_0_OR_GREATER
using System;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace XCode.EntityFrameworkCore;

/// <summary>EF Core 集成工厂</summary>
/// <remarks>
/// 提供 XCode 与 EF Core 的集成点，允许在运行时切换使用 EF Core 或传统 DAL
/// </remarks>
public static class EFCoreFactory
{
    /// <summary>是否启用 EF Core</summary>
    /// <remarks>
    /// 默认 false，使用传统 DAL
    /// 设置为 true 后，新创建的 DAL 将使用 EF Core 作为底层引擎
    /// </remarks>
    public static Boolean UseEFCore { get; set; }

    /// <summary>为指定连接创建 EF Core 数据库实例</summary>
    /// <param name="connName">连接名</param>
    /// <returns></returns>
    public static EFCoreDatabase CreateDatabase(String connName)
    {
        if (connName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(connName));

        var dal = DAL.Create(connName);
        if (dal == null) throw new InvalidOperationException($"未找到连接名为 {connName} 的数据库配置");

        var db = new EFCoreDatabase
        {
            ConnName = connName,
            ConnectionString = dal.ConnStr,
            Type = dal.DbType
        };

        return db;
    }

    /// <summary>检查当前环境是否支持 EF Core</summary>
    /// <returns></returns>
    public static Boolean IsEFCoreSupported()
    {
        // .NET 8.0+ 环境支持 EF Core
        return true;
    }

    /// <summary>获取 EF Core 版本信息</summary>
    /// <returns></returns>
    public static String GetEFCoreVersion()
    {
        var assembly = typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly;
        return assembly.GetName().Version?.ToString() ?? "Unknown";
    }
}
#endif
