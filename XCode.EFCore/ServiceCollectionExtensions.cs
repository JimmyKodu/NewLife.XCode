using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace XCode.EFCore;

/// <summary>XCode EF Core 服务扩展</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>添加 XCode EF Core 数据库上下文</summary>
    /// <param name="services">服务集合</param>
    /// <param name="optionsAction">DbContext 选项配置</param>
    /// <param name="assemblies">包含实体类型的程序集</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXCodeDbContext(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction,
        params Assembly[] assemblies)
    {
        services.AddDbContext<XCodeDbContext>((sp, options) =>
        {
            optionsAction(options);
        });

        // 注册工厂以传递程序集参数
        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<XCodeDbContext>>();
            return new XCodeDbContext(options, assemblies);
        });

        return services;
    }
}
