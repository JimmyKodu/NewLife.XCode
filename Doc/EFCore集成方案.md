# XCode EF Core 10 集成方案

## 1. 背景与目标

### 1.1 当前状况
- XCode 是成熟的自研 ORM 框架，支持 .NET 4.5 到 .NET 10
- 拥有独特的多级缓存、分表分库、自动迁移等特性
- 20+ 年演进，广泛应用于生产环境

### 1.2 集成目标
- **不是**完全替换 XCode，而是提供 EF Core 作为可选的底层引擎
- 保持 XCode 现有 API 不变，确保向后兼容
- 允许用户选择使用传统 XCode DAL 或 EF Core 引擎
- 利用 EF Core 的现代化特性（如 Change Tracking、LINQ Provider等）

## 2. 技术约束

### 2.1 框架兼容性
| 框架版本 | EF Core 10 支持 | XCode 当前支持 | 方案 |
|---------|----------------|---------------|------|
| net45/net461 | ❌ 不支持 | ✅ 支持 | 保持传统 DAL |
| netstandard2.0/2.1 | ❌ 不支持 | ✅ 支持 | 保持传统 DAL |
| net8.0+ | ✅ 支持 | ✅ 支持 | 可选 EF Core |

**结论**：EF Core 集成仅适用于 .NET 8.0+ 目标框架，需要条件编译

### 2.2 NuGet 包依赖
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == 'net9.0' OR '$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
</ItemGroup>
```

## 3. 架构设计

### 3.1 分层架构

```
┌─────────────────────────────────────┐
│     XCode Public API (Entity<T>)    │  ← 保持不变
├─────────────────────────────────────┤
│     IEntityOperate / EntityBase     │  ← 保持不变
├─────────────────────────────────────┤
│      DAL Abstraction Layer          │  ← 保持不变
├──────────────┬──────────────────────┤
│  传统 DAL    │   EF Core Adapter    │  ← 新增 EF Core 适配层
│  (DbBase等) │   (EFCoreDatabase)   │
├──────────────┴──────────────────────┤
│        数据库 Providers              │
└─────────────────────────────────────┘
```

### 3.2 核心组件

#### 3.2.1 EFCoreDatabase (实现 IDatabase)
- 包装 DbContext
- 实现 XCode 的 IDatabase 接口
- 转换 XCode 查询表达式到 LINQ

#### 3.2.2 XCodeDbContext
- 继承 DbContext
- 动态配置实体模型（通过 XCode 元数据）
- 支持多数据库切换

#### 3.2.3 EntityTypeBuilder<T> 适配器
- 将 XCode 的 TableItem/FieldItem 转换为 EF Core 实体配置
- 处理主键、索引、关系等映射

## 4. 实现计划

### 阶段 1：基础设施 (当前)
- [x] 创建设计文档
- [ ] 添加 EF Core 10 NuGet 包引用（仅 net8.0+）
- [ ] 创建 XCode.EntityFrameworkCore 项目（或在现有项目中用条件编译）
- [ ] 实现 XCodeDbContext 基类

### 阶段 2：适配层实现
- [ ] 实现 EFCoreDatabase : DbBase
- [ ] 实现 EntityTypeConfiguration 映射
- [ ] 实现查询表达式转换（WhereExpression → Expression<Func<T, bool>>）
- [ ] 实现基本 CRUD 操作适配

### 阶段 3：高级特性
- [ ] 实现缓存层集成（利用 EF Core 的 Change Tracker）
- [ ] 实现事务支持
- [ ] 实现分页查询
- [ ] 实现批量操作

### 阶段 4：数据库提供程序
- [ ] SQL Server 支持
- [ ] SQLite 支持
- [ ] MySQL 支持
- [ ] PostgreSQL 支持
- [ ] 其他数据库（按需）

### 阶段 5：测试与文档
- [ ] 单元测试覆盖
- [ ] 集成测试（多数据库）
- [ ] 性能对比测试
- [ ] 使用文档
- [ ] 迁移指南

## 5. 使用方式

### 5.1 启用 EF Core 引擎（.NET 8.0+）

```csharp
// 在应用启动时配置
XCodeSetting.Current.UseEFCore = true;

// 或者为特定连接启用
DAL.AddConnStr("MyDb", connStr, null, "SqlServer", useEFCore: true);
```

### 5.2 保持现有代码不变

```csharp
// 用户代码无需修改
var user = User.FindById(1);
user.Name = "Updated";
user.Update();

var list = User.FindAll(User._.Age > 18);
```

### 5.3 直接使用 DbContext（高级场景）

```csharp
#if NET8_0_OR_GREATER
using (var context = XCodeDbContext.Create("MyDb"))
{
    var users = context.Set<User>().Where(u => u.Age > 18).ToList();
}
#endif
```

## 6. 优势与挑战

### 6.1 优势
✅ 利用 EF Core 成熟的 LINQ Provider
✅ 更好的 Visual Studio 工具支持
✅ 社区生态丰富
✅ 持续更新的数据库提供程序
✅ 可选择性使用，不影响现有用户

### 6.2 挑战
⚠️ 需要维护两套底层实现
⚠️ EF Core 性能可能不如优化的原生 SQL
⚠️ 某些 XCode 特性可能难以映射（如复杂缓存策略）
⚠️ 增加项目复杂度

## 7. 性能考量

### 7.1 预期性能对比

| 场景 | 传统 XCode | EF Core 引擎 | 说明 |
|------|-----------|-------------|------|
| 简单查询 | 优 | 良 | 原生 SQL 更快 |
| 复杂 LINQ | 良 | 优 | EF Core 优化更好 |
| 批量操作 | 优 | 良 | XCode 批量优化多 |
| 缓存命中 | 优 | 优 | 缓存层独立 |
| 首次启动 | 快 | 较慢 | EF Core 需编译模型 |

### 7.2 性能优化策略
- 保持 XCode 缓存层独立于 EF Core
- 使用 Compiled Queries
- 启用 Query Splitting（适当场景）
- 使用 AsNoTracking（只读查询）

## 8. 迁移路径

### 8.1 对于现有项目
1. 升级到 .NET 8.0 或更高版本
2. 添加 EF Core 配置选项
3. 逐步测试和验证
4. 根据性能测试决定是否切换

### 8.2 对于新项目
1. 可直接选择 EF Core 引擎
2. 享受现代化工具支持
3. 保留切换回传统 DAL 的能力

## 9. 风险评估

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| 破坏现有功能 | 高 | 条件编译，充分测试，向后兼容 |
| 性能退化 | 中 | 性能测试，保留传统 DAL 选项 |
| 维护成本增加 | 中 | 共享尽可能多的代码，文档齐全 |
| 用户困惑 | 低 | 清晰文档，合理默认值 |

## 10. 时间线

- **第 1 周**：基础设施搭建
- **第 2-3 周**：适配层实现
- **第 4 周**：基本测试
- **第 5-6 周**：高级特性和多数据库支持
- **第 7 周**：性能测试和优化
- **第 8 周**：文档和发布

## 11. 决策点

### 需要确认的问题：
1. ❓ 是否真的需要 EF Core 集成？优势是否值得增加的复杂度？
2. ❓ 目标用户群体是否需要 EF Core 的特定功能？
3. ❓ 是否有足够的资源维护双引擎？
4. ❓ 是否考虑作为独立的 NuGet 包发布（NewLife.XCode.EFCore）？

**建议**：考虑创建独立的 `NewLife.XCode.EntityFrameworkCore` 扩展包，而不是直接集成到核心，以降低复杂度和维护成本。

---

## 附录 A：代码示例

### EFCoreDatabase 实现框架

```csharp
#if NET8_0_OR_GREATER
using Microsoft.EntityFrameworkCore;

namespace XCode.DataAccessLayer;

/// <summary>EF Core 数据库引擎</summary>
public class EFCoreDatabase : DbBase
{
    private XCodeDbContext? _context;

    /// <summary>获取 DbContext</summary>
    protected XCodeDbContext Context
    {
        get
        {
            if (_context == null)
            {
                _context = new XCodeDbContext(ConnectionString, DbType);
                ConfigureContext(_context);
            }
            return _context;
        }
    }

    /// <summary>配置 DbContext</summary>
    protected virtual void ConfigureContext(XCodeDbContext context)
    {
        // 配置实体映射
        // 配置查询行为
    }

    // 实现 IDatabase 接口方法...
}
#endif
```

### XCodeDbContext 实现框架

```csharp
#if NET8_0_OR_GREATER
using Microsoft.EntityFrameworkCore;

namespace XCode.EntityFrameworkCore;

/// <summary>XCode DbContext</summary>
public class XCodeDbContext : DbContext
{
    private readonly String _connectionString;
    private readonly DatabaseType _dbType;

    public XCodeDbContext(String connectionString, DatabaseType dbType)
    {
        _connectionString = connectionString;
        _dbType = dbType;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 根据数据库类型选择提供程序
        switch (_dbType)
        {
            case DatabaseType.SqlServer:
                optionsBuilder.UseSqlServer(_connectionString);
                break;
            case DatabaseType.SQLite:
                optionsBuilder.UseSqlite(_connectionString);
                break;
            case DatabaseType.MySql:
                optionsBuilder.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString));
                break;
            // 其他数据库...
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 从 XCode 元数据动态构建模型
        var factories = EntityFactory.GetFactories();
        foreach (var factory in factories)
        {
            ConfigureEntity(modelBuilder, factory);
        }
    }

    private void ConfigureEntity(ModelBuilder modelBuilder, IEntityFactory factory)
    {
        // 动态配置实体映射
    }
}
#endif
```

---

*文档版本：1.0*
*创建时间：2026-02-09*
*作者：XCode 团队*
