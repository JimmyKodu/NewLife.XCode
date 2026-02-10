using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace XCode.EFCore;

/// <summary>XCode 数据库上下文。自动扫描并注册带有 BindTable 特性的 XCode 实体类型到 EF Core 模型</summary>
public class XCodeDbContext : DbContext
{
    private readonly Assembly[] _assemblies;

    /// <summary>实例化 XCode 数据库上下文</summary>
    /// <param name="options">DbContext 选项</param>
    /// <param name="assemblies">包含实体类型的程序集。如果为空，则扫描调用程序集</param>
    public XCodeDbContext(DbContextOptions options, params Assembly[] assemblies) : base(options)
    {
        _assemblies = assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()];
    }

    /// <summary>实例化 XCode 数据库上下文（无参构造，子类使用）</summary>
    protected XCodeDbContext() : base()
    {
        _assemblies = [GetType().Assembly];
    }

    /// <summary>实例化 XCode 数据库上下文（子类使用）</summary>
    /// <param name="options">DbContext 选项</param>
    protected XCodeDbContext(DbContextOptions options) : base(options)
    {
        _assemblies = [GetType().Assembly];
    }

    /// <summary>模型创建时自动应用 XCode 实体配置</summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 扫描程序集中所有带 BindTable 特性的类型
        var entityTypes = GetXCodeEntityTypes();
        foreach (var entityType in entityTypes)
        {
            ApplyEntityConfiguration(modelBuilder, entityType);
        }
    }

    /// <summary>获取所有 XCode 实体类型</summary>
    /// <returns>实体类型集合</returns>
    protected virtual IEnumerable<Type> GetXCodeEntityTypes()
    {
        foreach (var assembly in _assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (type.GetCustomAttribute<BindTableAttribute>() == null) continue;

                yield return type;
            }
        }
    }

    private static void ApplyEntityConfiguration(ModelBuilder modelBuilder, Type entityType)
    {
        // 构造 XCodeEntityTypeConfiguration<TEntity> 并应用
        var configType = typeof(XCodeEntityTypeConfiguration<>).MakeGenericType(entityType);
        var config = Activator.CreateInstance(configType);
        if (config == null) return;

        // 调用 modelBuilder.ApplyConfiguration(config)
        var method = typeof(ModelBuilder)
            .GetMethods()
            .First(m => m.Name == nameof(ModelBuilder.ApplyConfiguration)
                        && m.GetGenericArguments().Length == 1
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>));

        var genericMethod = method.MakeGenericMethod(entityType);
        genericMethod.Invoke(modelBuilder, [config]);
    }
}
