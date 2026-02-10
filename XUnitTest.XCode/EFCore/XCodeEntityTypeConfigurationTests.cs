using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using XCode.EFCore;
using Xunit;
using XUnitTest.XCode.TestEntity;

namespace XUnitTest.XCode.EFCore;

public class XCodeEntityTypeConfigurationTests
{
    [Fact(DisplayName = "EF Core 映射应同步 XCode 元数据")]
    public void Configure_Log2_Metadata()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());

        modelBuilder.ApplyConfiguration(new XCodeEntityTypeConfiguration<Log2>());

        var entityType = modelBuilder.Model.FindEntityType(typeof(Log2));
        Assert.NotNull(entityType);
        Assert.Equal("Log2", entityType.GetTableName());

        var primaryKey = entityType.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(nameof(Log2.ID), primaryKey.Properties[0].Name);

        var idProperty = entityType.FindProperty(nameof(Log2.ID));
        Assert.Equal(ValueGenerated.OnAdd, idProperty.ValueGenerated);

        var categoryProperty = entityType.FindProperty(nameof(Log2.Category));
        Assert.Equal(50, categoryProperty.GetMaxLength());
        Assert.True(categoryProperty.IsNullable);

        Assert.Equal(4, entityType.GetIndexes().Count());
    }
}
