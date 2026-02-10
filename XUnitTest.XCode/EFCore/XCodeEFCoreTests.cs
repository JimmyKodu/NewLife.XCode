using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using XCode;
using XCode.DataAccessLayer;
using XCode.EFCore;
using Xunit;

namespace XUnitTest.XCode.EFCore;

/// <summary>XCode EF Core 集成测试</summary>
[DisplayName("XCode EF Core 集成测试")]
public class XCodeEFCoreTests
{
    /// <summary>测试 XCodeEntityTypeConfiguration 正确映射表名</summary>
    [Fact]
    [DisplayName("映射表名")]
    public void MapTableName()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TestUser));
        Assert.NotNull(entityType);
        Assert.Equal("TestUser", entityType.GetTableName());
    }

    /// <summary>测试主键映射</summary>
    [Fact]
    [DisplayName("映射主键")]
    public void MapPrimaryKey()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TestUser));
        Assert.NotNull(entityType);

        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal("Id", pk.Properties[0].Name);
    }

    /// <summary>测试列名映射</summary>
    [Fact]
    [DisplayName("映射列名")]
    public void MapColumnName()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TestUser));
        Assert.NotNull(entityType);

        var nameProp = entityType.FindProperty("Name");
        Assert.NotNull(nameProp);
        Assert.Equal("UserName", nameProp.GetColumnName());
    }

    /// <summary>测试字符串长度映射</summary>
    [Fact]
    [DisplayName("映射字符串长度")]
    public void MapStringLength()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TestUser));
        Assert.NotNull(entityType);

        var nameProp = entityType.FindProperty("Name");
        Assert.NotNull(nameProp);
        Assert.Equal(50, nameProp.GetMaxLength());
    }

    /// <summary>测试可空性映射</summary>
    [Fact]
    [DisplayName("映射可空性")]
    public void MapNullability()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TestUser));
        Assert.NotNull(entityType);

        // Name 不可空
        var nameProp = entityType.FindProperty("Name");
        Assert.NotNull(nameProp);
        Assert.False(nameProp.IsNullable);

        // Remark 可空
        var remarkProp = entityType.FindProperty("Remark");
        Assert.NotNull(remarkProp);
        Assert.True(remarkProp.IsNullable);
    }

    /// <summary>测试索引映射</summary>
    [Fact]
    [DisplayName("映射索引")]
    public void MapIndexes()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(TestUser));
        Assert.NotNull(entityType);

        var indexes = entityType.GetIndexes().ToList();
        Assert.True(indexes.Count >= 1);

        // 检查唯一索引
        var uniqueIndex = indexes.FirstOrDefault(i => i.IsUnique);
        Assert.NotNull(uniqueIndex);
    }

    /// <summary>测试 CRUD 操作</summary>
    [Fact]
    [DisplayName("EF Core CRUD 操作")]
    public void CrudOperations()
    {
        using var context = CreateContext();
        context.Database.EnsureCreated();

        // Insert
        var user = new TestUser { Name = "test_user", Age = 25, Remark = "测试用户" };
        context.Set<TestUser>().Add(user);
        context.SaveChanges();
        Assert.True(user.Id > 0);

        // Read
        var found = context.Set<TestUser>().Find(user.Id);
        Assert.NotNull(found);
        Assert.Equal("test_user", found.Name);
        Assert.Equal(25, found.Age);

        // Update
        found.Age = 30;
        context.SaveChanges();
        var updated = context.Set<TestUser>().Find(user.Id);
        Assert.NotNull(updated);
        Assert.Equal(30, updated.Age);

        // Delete
        context.Set<TestUser>().Remove(updated);
        context.SaveChanges();
        var deleted = context.Set<TestUser>().Find(user.Id);
        Assert.Null(deleted);
    }

    /// <summary>测试 LINQ 查询</summary>
    [Fact]
    [DisplayName("LINQ 查询")]
    public void LinqQuery()
    {
        using var context = CreateContext();
        context.Database.EnsureCreated();

        // 插入测试数据
        context.Set<TestUser>().AddRange(
            new TestUser { Name = "alice", Age = 20 },
            new TestUser { Name = "bob", Age = 30 },
            new TestUser { Name = "charlie", Age = 25 }
        );
        context.SaveChanges();

        // Where
        var adults = context.Set<TestUser>().Where(u => u.Age >= 25).ToList();
        Assert.Equal(2, adults.Count);

        // OrderBy
        var ordered = context.Set<TestUser>().OrderBy(u => u.Age).ToList();
        Assert.Equal("alice", ordered[0].Name);

        // Count
        var count = context.Set<TestUser>().Count();
        Assert.Equal(3, count);
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new TestDbContext(options);
        // SQLite 内存数据库需要保持连接
        context.Database.OpenConnection();

        return context;
    }
}

/// <summary>测试用 DbContext</summary>
public class TestDbContext : XCodeDbContext
{
    public TestDbContext(DbContextOptions options) : base(options) { }

    protected override IEnumerable<Type> GetXCodeEntityTypes()
    {
        yield return typeof(TestUser);
    }
}

/// <summary>测试用户实体</summary>
[BindTable("TestUser", Description = "测试用户", ConnName = "test", DbType = DatabaseType.None)]
[BindIndex("IU_TestUser_Name", true, "Name")]
public class TestUser
{
    /// <summary>编号</summary>
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get; set; }

    /// <summary>名称</summary>
    [DataObjectField(false, false, false, 50)]
    [BindColumn("UserName", "名称", "")]
    public String Name { get; set; } = String.Empty;

    /// <summary>年龄</summary>
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Age", "年龄", "")]
    public Int32 Age { get; set; }

    /// <summary>备注</summary>
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Remark", "备注", "")]
    public String? Remark { get; set; }
}
