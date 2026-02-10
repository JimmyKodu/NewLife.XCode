# XCode.EFCore - XCode 与 EF Core 10 集成

## 概述

XCode.EFCore 是 NewLife.XCode 数据中间件与 Entity Framework Core 10 的集成层，提供两个 ORM 框架之间的互操作性和迁移支持。

## 特性

- **双向互操作**：支持 XCode 实体与 EF Core DbContext 之间的无缝集成
- **自动映射**：自动将 XCode 实体模型映射到 EF Core 实体配置
- **共享连接**：复用 XCode 的数据库连接配置
- **事务协调**：支持 XCode 和 EF Core 的事务协同
- **查询转换**：将 XCode 的 WhereExpression 转换为 EF Core LINQ 查询
- **多数据库支持**：支持 SQL Server、MySQL、SQLite、PostgreSQL、Oracle

## 安装

```bash
dotnet add package NewLife.XCode.EFCore
```

## 快速开始

### 1. 创建 DbContext

```csharp
using XCode.EFCore;

public class MyDbContext : XCodeDbContext
{
    public MyDbContext() : base()
    {
        // 使用 XCode 的连接配置
        ConnName = "MyConnection";
    }

    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 自动应用 XCode 实体配置
        modelBuilder.ApplyXCodeConfiguration<User>();
    }
}
```

### 2. 使用 XCode 实体

```csharp
// 定义 XCode 实体（使用 xcodetool 生成）
public partial class User : Entity<User>
{
    public Int32 Id { get; set; }
    public String Name { get; set; }
    public String Email { get; set; }
    public DateTime CreateTime { get; set; }
}
```

### 3. 使用 EF Core 查询

```csharp
using var context = new MyDbContext();

// 使用 EF Core LINQ 查询
var users = await context.Users
    .Where(u => u.Name.Contains("张"))
    .OrderBy(u => u.CreateTime)
    .ToListAsync();

// 使用 XCode WhereExpression
var where = User._.Name == "张三" & User._.Email.EndsWith("@example.com");
var filteredUsers = context.Users
    .ApplyWhere(where)
    .ApplyOrderBy("CreateTime DESC")
    .ApplyPaging(1, 20)
    .ToList();
```

### 4. 混合使用 XCode 和 EF Core

```csharp
// 使用 XCode 的缓存查询
var user = User.FindByKey(1);

// 使用 EF Core 的复杂查询
using var context = new MyDbContext();
var stats = await context.Users
    .GroupBy(u => u.CreateTime.Date)
    .Select(g => new { Date = g.Key, Count = g.Count() })
    .ToListAsync();
```

## 高级用法

### 批量应用实体配置

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 批量应用程序集中所有 XCode 实体的配置
    modelBuilder.ApplyXCodeConfigurations(typeof(User).Assembly);
}
```

### 事务协调

```csharp
using var context = new MyDbContext();
using var transaction = await context.BeginTransactionAsync();

try
{
    // EF Core 操作
    context.Users.Add(new User { Name = "新用户" });
    await context.SaveChangesAsync();

    // XCode 操作（使用相同连接）
    var user = new User { Name = "另一个用户" };
    user.Insert();

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

## 配置数据库连接

在 `appsettings.json` 中配置：

```json
{
  "ConnectionStrings": {
    "MyConnection": "Server=localhost;Database=test;Uid=root;Pwd=password;"
  },
  "XCode": {
    "MyConnection": {
      "ConnectionString": "Server=localhost;Database=test;Uid=root;Pwd=password;",
      "Provider": "MySql"
    }
  }
}
```

## 限制与注意事项

1. **目标框架**：XCode.EFCore 仅支持 .NET 10.0，因为 EF Core 10 仅支持 .NET 10
2. **查询转换**：复杂的 WhereExpression 可能无法完全转换为 LINQ，建议使用原生 LINQ
3. **缓存策略**：EF Core 查询不会自动使用 XCode 的缓存，需显式调用 XCode 的查询方法
4. **性能考量**：对于高性能场景，建议直接使用 XCode 或 EF Core，避免频繁转换

## 迁移场景

### 从 XCode 迁移到 EF Core

1. 保留 XCode 实体定义和模型
2. 创建继承自 `XCodeDbContext` 的 DbContext
3. 使用 `ApplyXCodeConfigurations` 自动映射
4. 逐步将查询代码迁移到 LINQ

### 从 EF Core 迁移到 XCode

1. 使用 XCode 的反向工程工具生成实体
2. 利用 XCode 的缓存和分表功能优化性能
3. 保留 EF Core 的复杂查询逻辑

## 相关链接

- [NewLife.XCode 文档](https://newlifex.com/xcode)
- [Entity Framework Core 文档](https://docs.microsoft.com/ef/core/)
- [GitHub 仓库](https://github.com/NewLifeX/NewLife.XCode)

## 许可证

MIT License - 详见 LICENSE 文件
