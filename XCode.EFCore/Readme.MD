# XCode.EFCore - EF Core 10 集成层

XCode.EFCore 是 NewLife.XCode 数据中间件的 EF Core 10 集成层，允许在 XCode 应用中使用 EF Core 作为底层数据访问引擎。

## 特性

- **双向兼容**：保持 XCode 原有 API，同时支持 EF Core 特性
- **渐进式迁移**：可以逐步将现有 XCode 应用迁移到 EF Core
- **最新技术栈**：支持 .NET 10.0 和 EF Core 10.0

## 安装

```bash
dotnet add package XCode.EFCore
```

## 快速开始

### 1. 创建数据库实例

```csharp
using XCode.EFCore;
using Microsoft.Data.Sqlite;

// 创建 EF Core 数据库适配器
var database = new EFCoreDatabase
{
    ConnName = "MyDb",
    ConnectionString = "Data Source=mydb.db",
    Factory = SqliteFactory.Instance
};
```

### 2. 使用会话执行 SQL

```csharp
using var session = database.CreateSession();

// 执行查询
var result = session.Query("SELECT * FROM Users WHERE Id = @Id", 
    database.CreateParameters(new { Id = 1 }));

// 执行更新
var affected = session.Execute("UPDATE Users SET Name = @Name WHERE Id = @Id",
    database.CreateParameters(new { Name = "Test", Id = 1 }));
```

### 3. 使用实体适配器

```csharp
// 定义实体
public class User : IModel
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(50)]
    public string Name { get; set; }
    
    public object this[string name]
    {
        get => name switch
        {
            nameof(Id) => Id,
            nameof(Name) => Name,
            _ => null
        };
        set
        {
            switch (name)
            {
                case nameof(Id): Id = (int)value; break;
                case nameof(Name): Name = (string)value; break;
            }
        }
    }
}

// 使用适配器
var adapter = new EntityAdapter(database);
var users = adapter.Query<User>();
var user = adapter.Find<User>(1);
adapter.Insert(new User { Name = "New User" });
```

### 4. 使用 EF Core DbContext

```csharp
public class MyDbContext : XCodeDbContext
{
    public DbSet<User> Users { get; set; }
    
    public MyDbContext(DbContextOptions options) : base(options) { }
}

// 使用仓储模式
using var context = new MyDbContext(options);
var repo = new EntityRepository<User>(context);
var users = repo.Query().Where(u => u.Name.Contains("test")).ToList();
```

## 架构说明

```
┌─────────────────────────────────────────┐
│            应用程序代码                  │
├─────────────────────────────────────────┤
│    XCode API  │  EF Core DbContext      │
├───────────────┴─────────────────────────┤
│           XCode.EFCore 适配层            │
│  ┌──────────────┐  ┌──────────────────┐ │
│  │EFCoreDatabase│  │  EntityAdapter   │ │
│  │EFCoreSession │  │EntityRepository  │ │
│  │EFCoreMetaData│  │  XCodeDbContext  │ │
│  └──────────────┘  └──────────────────┘ │
├─────────────────────────────────────────┤
│       Microsoft.EntityFrameworkCore     │
└─────────────────────────────────────────┘
```

## 核心类

| 类 | 说明 |
|----|------|
| `EFCoreDatabase` | 实现 `IDatabase` 接口的 EF Core 适配器 |
| `EFCoreSession` | 实现 `IDbSession` 接口的数据库会话 |
| `EFCoreMetaData` | 实现 `IMetaData` 接口的元数据管理 |
| `EntityAdapter` | XCode 实体操作适配器 |
| `EntityRepository<T>` | EF Core 风格的仓储实现 |
| `XCodeDbContext` | 通用 DbContext 基类 |

## 迁移指南

### 从 XCode 迁移到 EF Core

1. **添加 XCode.EFCore 包**
2. **创建 EFCoreDatabase 实例**替代原有数据库连接
3. **逐步替换实体类**，添加 EF Core 特性
4. **使用 EntityAdapter** 进行基本 CRUD 操作
5. **可选**：引入 DbContext 和 DbSet 进行更复杂的操作

### 保持兼容性

XCode.EFCore 设计为完全兼容 XCode 现有 API：

- `IDatabase` 接口完全实现
- `IDbSession` 接口完全实现
- `IMetaData` 接口完全实现

这意味着你可以：
- 在同一项目中混用 XCode 和 EF Core
- 逐步迁移，无需一次性重写

## 注意事项

1. **目标框架**：XCode.EFCore 仅支持 .NET 10.0，因为 EF Core 10.0 仅支持 .NET 10.0
2. **数据库提供程序**：需要单独安装对应的 EF Core 数据库提供程序包
3. **性能**：EF Core 的变更跟踪可能影响批量操作性能

## 依赖

- NewLife.XCode
- Microsoft.EntityFrameworkCore 10.0.0
- Microsoft.EntityFrameworkCore.Relational 10.0.0

## 许可证

MIT License
