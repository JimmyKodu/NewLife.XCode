using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using NewLife;
using XCode.DataAccessLayer;

namespace XCode.EFCore;

/// <summary>EF Core 数据库适配器</summary>
/// <remarks>
/// 该类实现 XCode 的 IDatabase 接口，使用 EF Core 作为底层数据访问引擎。
/// 允许在 XCode 应用中无缝切换到 EF Core，同时保持现有 API 兼容性。
/// </remarks>
public class EFCoreDatabase : DisposeBase, IDatabase
{
    #region 属性
    /// <summary>数据库类型</summary>
    public DatabaseType Type => DatabaseType.None;

    /// <summary>数据库提供者工厂</summary>
    public DbProviderFactory Factory { get; set; } = null!;

    /// <summary>链接名</summary>
    public String ConnName { get; set; } = String.Empty;

    /// <summary>链接字符串</summary>
    public String ConnectionString { get; set; } = String.Empty;

    /// <summary>数据库提供者</summary>
    public String? Provider { get; set; }

    /// <summary>拥有者</summary>
    public String? Owner { get; set; }

    /// <summary>数据库名</summary>
    public String? DatabaseName { get; private set; }

    /// <summary>数据库服务器版本</summary>
    public String ServerVersion => _serverVersion ?? String.Empty;
    private String? _serverVersion;

    /// <summary>是否输出SQL</summary>
    public Boolean ShowSQL { get; set; }

    /// <summary>参数化添删改查</summary>
    public Boolean UseParameter { get; set; } = true;

    /// <summary>失败重试次数</summary>
    public Int32 RetryOnFailure { get; set; }

    /// <summary>反向工程模式</summary>
    public Migration Migration { get; set; } = DataAccessLayer.Migration.On;

    /// <summary>名称格式化</summary>
    public NameFormats NameFormat { get; set; }

    /// <summary>批大小</summary>
    public Int32 BatchSize { get; set; } = 5000;

    /// <summary>命令超时</summary>
    public Int32 CommandTimeout { get; set; }

    /// <summary>长文本长度</summary>
    public Int32 LongTextLength => 4000;

    /// <summary>本连接数据只读</summary>
    public Boolean Readonly { get; set; }

    /// <summary>数据层缓存有效期</summary>
    public Int32 DataCache { get; set; }

    /// <summary>表前缀</summary>
    public String? TablePrefix { get; set; }

    /// <summary>DbContext 工厂</summary>
    public Func<DbContextOptions, DbContext>? DbContextFactory { get; set; }

    /// <summary>DbContextOptions 构建器</summary>
    public Func<String, DbContextOptions>? OptionsBuilder { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化 EF Core 数据库适配器</summary>
    public EFCoreDatabase()
    {
        var set = XCodeSetting.Current;
        Migration = set.Migration;
        ShowSQL = set.ShowSQL;
        UseParameter = set.UseParameter;
    }

    /// <summary>销毁</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
    }
    #endregion

    #region 方法
    /// <summary>创建数据库会话</summary>
    public IDbSession CreateSession() => new EFCoreSession(this);

    /// <summary>创建元数据对象</summary>
    public IMetaData CreateMetaData() => new EFCoreMetaData(this);

    /// <summary>创建连接</summary>
    public DbConnection OpenConnection()
    {
        var conn = Factory.CreateConnection() ?? throw new InvalidOperationException("无法创建数据库连接");
        conn.ConnectionString = ConnectionString;
        conn.Open();

        if (_serverVersion == null)
        {
            _serverVersion = conn.ServerVersion;
            DatabaseName = conn.Database;
        }

        return conn;
    }

    /// <summary>异步打开连接</summary>
    public async Task<DbConnection> OpenConnectionAsync()
    {
        var conn = Factory.CreateConnection() ?? throw new InvalidOperationException("无法创建数据库连接");
        conn.ConnectionString = ConnectionString;
        await conn.OpenAsync().ConfigureAwait(false);

        if (_serverVersion == null)
        {
            _serverVersion = conn.ServerVersion;
            DatabaseName = conn.Database;
        }

        return conn;
    }

    /// <summary>是否支持该提供者</summary>
    public Boolean Support(String providerName) =>
        providerName.EqualIgnoreCase("EFCore", "EntityFramework", "EntityFrameworkCore");

    /// <summary>创建 DbContext</summary>
    public DbContext CreateDbContext()
    {
        if (OptionsBuilder == null)
            throw new InvalidOperationException("请先设置 OptionsBuilder");

        var options = OptionsBuilder(ConnectionString);

        if (DbContextFactory != null)
            return DbContextFactory(options);

        return new XCodeDbContext(options);
    }
    #endregion

    #region 分页
    /// <summary>构造分页SQL</summary>
    /// <remarks>
    /// 根据不同数据库使用不同的分页语法：
    /// - SQL Server 2012+/PostgreSQL: OFFSET...FETCH NEXT（需要ORDER BY）
    /// - SQLite/MySQL: LIMIT...OFFSET
    /// - Oracle 12c+: OFFSET...FETCH NEXT
    /// 默认使用 LIMIT...OFFSET 语法，因为兼容性最好
    /// </remarks>
    public String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn)
    {
        if (maximumRows <= 0) return sql;

        // 使用 LIMIT...OFFSET 语法，兼容 SQLite/MySQL/PostgreSQL
        // SQL Server 需要 ORDER BY 才能使用 OFFSET FETCH
        return $"{sql} LIMIT {maximumRows} OFFSET {startRowIndex}";
    }

    /// <summary>构造分页SQL</summary>
    public SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        if (maximumRows <= 0) return builder;

        // 使用 LIMIT...OFFSET 语法
        builder.Limit = $"LIMIT {maximumRows} OFFSET {startRowIndex}";
        return builder;
    }
    #endregion

    #region 数据库特性
    /// <summary>格式化名称</summary>
    public String FormatName(String name) => $"\"{name}\"";

    /// <summary>格式化表名</summary>
    public String FormatName(IDataTable table, Boolean formatKeyword = true)
    {
        var name = table.TableName;
        if (!TablePrefix.IsNullOrEmpty()) name = TablePrefix + name;
        return formatKeyword ? FormatName(name) : name;
    }

    /// <summary>格式化字段名</summary>
    public String FormatName(IDataColumn column) => FormatName(column.ColumnName);

    /// <summary>格式化数据为SQL数据</summary>
    public String FormatValue(IDataColumn column, Object? value)
    {
        if (value == null) return "NULL";

        return value switch
        {
            String str => $"'{str.Replace("'", "''")}'",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            Boolean b => b ? "1" : "0",
            _ => value.ToString() ?? "NULL"
        };
    }

    /// <summary>格式化时间为SQL字符串</summary>
    public String FormatDateTime(DateTime dateTime) => $"'{dateTime:yyyy-MM-dd HH:mm:ss}'";

    /// <summary>格式化时间为SQL字符串</summary>
    public String FormatDateTime(IDataColumn column, DateTime dateTime) => FormatDateTime(dateTime);

    /// <summary>格式化模糊搜索字符串</summary>
    public String FormatLike(IDataColumn column, String format, String value)
    {
        var escapedValue = value.Replace("'", "''").Replace("%", "[%]").Replace("_", "[_]");
        return String.Format(format, escapedValue);
    }

    /// <summary>参数化格式化模糊搜索字符串</summary>
    public String FormatLike(IDataColumn column, String format) => format;

    /// <summary>格式化参数名</summary>
    public String FormatParameterName(String name) => $"@{name}";

    /// <summary>字符串相加</summary>
    public String StringConcat(String left, String right) => $"({left} || {right})";

    /// <summary>创建参数</summary>
    public IDataParameter CreateParameter(String name, Object? value, IDataColumn? field)
    {
        var p = Factory.CreateParameter() ?? throw new InvalidOperationException("无法创建参数");
        p.ParameterName = FormatParameterName(name);
        p.Value = value ?? DBNull.Value;
        return p;
    }

    /// <summary>创建参数</summary>
    public IDataParameter CreateParameter(String name, Object? value, Type? type = null)
    {
        var p = Factory.CreateParameter() ?? throw new InvalidOperationException("无法创建参数");
        p.ParameterName = FormatParameterName(name);
        p.Value = value ?? DBNull.Value;
        return p;
    }

    /// <summary>创建参数数组</summary>
    public IDataParameter[] CreateParameters(IDictionary<String, Object>? ps)
    {
        if (ps == null || ps.Count == 0) return [];

        var list = new List<IDataParameter>();
        foreach (var item in ps)
        {
            list.Add(CreateParameter(item.Key, item.Value));
        }
        return [.. list];
    }

    /// <summary>根据对象成员创建参数数组</summary>
    public IDataParameter[] CreateParameters(Object? model)
    {
        if (model == null) return [];

        var ps = new Dictionary<String, Object>();
        foreach (var pi in model.GetType().GetProperties())
        {
            var val = pi.GetValue(model);
            if (val != null) ps[pi.Name] = val;
        }
        return CreateParameters(ps);
    }

    /// <summary>生成批量删除SQL</summary>
    public String? BuildDeleteSql(String tableName, String where, Int32 batchSize) => null;
    #endregion
}
