# XCode.EFCore 集成更新日志

## v1.0.2026.0210 (2026-02-10)

### 新功能
- **XCode.EFCore 项目**：全新的 EF Core 10 集成层
  - 支持 XCode 实体与 EF Core DbContext 互操作
  - 自动映射 XCode 实体元数据到 EF Core 实体配置
  - 支持从 XCode DAL 配置自动推断数据库类型和连接字符串
  - 提供查询转换、分页、排序等辅助功能

### 核心组件

#### XCodeDbContext
- 继承自 `DbContext` 的基类
- 支持通过 `ConnName` 属性使用 XCode 的数据库连接配置
- 自动配置 SQL Server、MySQL、SQLite、PostgreSQL、Oracle
- 提供事务协调支持

#### XCodeEntityTypeConfiguration
- 自动配置实体表名、列名
- 自动配置主键、索引
- 自动配置字段长度、精度、默认值
- 自动配置自增标识

#### WhereExpressionConverter
- 提供 XCode WhereExpression 到 EF Core LINQ 的转换（基础实现）
- 支持排序（`ApplyOrderBy`）
- 支持分页（`ApplyPaging`）

### 数据库支持
- SQL Server ✓
- MySQL ✓
- SQLite ✓
- PostgreSQL ✓
- Oracle ✓

### 目标框架
- **.NET 10.0** (必需，因为 EF Core 10 仅支持 .NET 10)

### 已知限制
1. 复杂的 WhereExpression 可能无法完全转换为 LINQ，建议使用原生 LINQ
2. EF Core 查询不会自动使用 XCode 的缓存，需显式调用 XCode 的查询方法
3. 对于高性能场景，建议直接使用 XCode 或 EF Core，避免频繁转换
4. MySQL 和 PostgreSQL 提供程序使用 9.x 版本（与 EF Core 10 存在版本不匹配警告，但可以工作）

### 使用示例

```csharp
// 创建 DbContext
public class MyDbContext : XCodeDbContext
{
    public MyDbContext() : base()
    {
        ConnName = "MyConnection";  // 使用 XCode 的连接配置
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // 自动应用 XCode 实体配置
        modelBuilder.ApplyXCodeConfiguration<User>();
    }
}

// 使用 EF Core LINQ 查询
using var context = new MyDbContext();
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

### 文档
- [完整使用指南](Readme.MD)
- [GitHub 仓库](https://github.com/NewLifeX/NewLife.XCode)

### 许可证
MIT License
