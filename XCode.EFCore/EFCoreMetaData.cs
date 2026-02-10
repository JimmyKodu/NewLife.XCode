using System.Data;
using System.Data.Common;
using NewLife;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace XCode.EFCore;

/// <summary>EF Core 元数据管理</summary>
/// <remarks>
/// 实现 XCode 的 IMetaData 接口，提供数据库结构管理能力。
/// </remarks>
public class EFCoreMetaData : DisposeBase, IMetaData
{
    #region 属性
    /// <summary>数据库</summary>
    public IDatabase Database { get; }

    /// <summary>EF Core 数据库</summary>
    public EFCoreDatabase EFDatabase => (EFCoreDatabase)Database;

    /// <summary>所有元数据集合</summary>
    public ICollection<String> MetaDataCollections { get; } = new List<String> { "Tables", "Columns", "Indexes" };

    /// <summary>保留关键字</summary>
    public ICollection<String> ReservedWords { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER",
        "TABLE", "INDEX", "VIEW", "DATABASE", "SCHEMA", "PRIMARY", "KEY", "FOREIGN",
        "REFERENCES", "CONSTRAINT", "UNIQUE", "NOT", "NULL", "DEFAULT", "CHECK",
        "AND", "OR", "IN", "BETWEEN", "LIKE", "IS", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END",
        "ORDER", "BY", "ASC", "DESC", "GROUP", "HAVING", "LIMIT", "OFFSET", "UNION", "JOIN",
        "LEFT", "RIGHT", "INNER", "OUTER", "CROSS", "ON", "AS", "DISTINCT", "ALL", "TOP"
    };
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public EFCoreMetaData(EFCoreDatabase database)
    {
        Database = database;
    }
    #endregion

    #region 表架构
    /// <summary>取得所有表构架</summary>
    public IList<IDataTable> GetTables() => GetTables(null);

    /// <summary>取得所有表构架</summary>
    public IList<IDataTable> GetTables(String[]? names)
    {
        var list = new List<IDataTable>();

        try
        {
            using var conn = Database.OpenConnection();

            // 获取表信息
            var tableSchema = GetSchema(conn, "Tables", null);
            if (tableSchema == null) return list;

            foreach (DataRow row in tableSchema.Rows)
            {
                var name = row["TABLE_NAME"]?.ToString();
                if (name.IsNullOrEmpty()) continue;

                // 过滤系统表
                if (name.StartsWithIgnoreCase("sqlite_", "sys", "__EF")) continue;

                // 如果指定了表名过滤
                if (names != null && names.Length > 0 && !names.Any(n => n.EqualIgnoreCase(name)))
                    continue;

                var table = GetTable(conn, name);
                if (table != null) list.Add(table);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return list;
    }

    /// <summary>取得所有表名</summary>
    public IList<String> GetTableNames()
    {
        var list = new List<String>();

        try
        {
            using var conn = Database.OpenConnection();

            var tableSchema = GetSchema(conn, "Tables", null);
            if (tableSchema == null) return list;

            foreach (DataRow row in tableSchema.Rows)
            {
                var name = row["TABLE_NAME"]?.ToString();
                if (!name.IsNullOrEmpty() && !name.StartsWithIgnoreCase("sqlite_", "sys", "__EF"))
                    list.Add(name);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return list;
    }

    private IDataTable? GetTable(DbConnection conn, String tableName)
    {
        var table = DAL.CreateTable();
        table.TableName = tableName;
        table.Name = tableName;
        table.DbType = Database.Type;

        // 获取列信息
        try
        {
            var columns = GetSchema(conn, "Columns", new[] { null, null, tableName, null });
            if (columns != null)
            {
                foreach (DataRow row in columns.Rows)
                {
                    var col = table.CreateColumn();
                    col.ColumnName = row["COLUMN_NAME"]?.ToString() ?? String.Empty;
                    col.Name = col.ColumnName;
                    col.RawType = row["DATA_TYPE"]?.ToString() ?? "String";
                    col.Nullable = row["IS_NULLABLE"]?.ToString()?.EqualIgnoreCase("YES", "true", "1") ?? true;
                    col.DataType = ParseDataType(col.RawType);

                    // 获取长度
                    if (row.Table.Columns.Contains("CHARACTER_MAXIMUM_LENGTH"))
                    {
                        var len = row["CHARACTER_MAXIMUM_LENGTH"];
                        if (len != DBNull.Value) col.Length = Convert.ToInt32(len);
                    }

                    // 获取默认值
                    if (row.Table.Columns.Contains("COLUMN_DEFAULT"))
                    {
                        col.DefaultValue = row["COLUMN_DEFAULT"]?.ToString();
                    }

                    table.Columns.Add(col);
                }
            }
        }
        catch { }

        // 获取主键信息
        try
        {
            var indexes = GetSchema(conn, "IndexColumns", new[] { null, null, tableName, null, null });
            if (indexes != null)
            {
                foreach (DataRow row in indexes.Rows)
                {
                    var colName = row["COLUMN_NAME"]?.ToString();
                    if (colName.IsNullOrEmpty()) continue;

                    var col = table.Columns.FirstOrDefault(c => c.ColumnName.EqualIgnoreCase(colName));
                    if (col != null)
                    {
                        var indexName = row["INDEX_NAME"]?.ToString() ?? String.Empty;
                        if (indexName.Contains("PK", StringComparison.OrdinalIgnoreCase) ||
                            indexName.Contains("PRIMARY", StringComparison.OrdinalIgnoreCase))
                        {
                            col.PrimaryKey = true;
                        }
                    }
                }
            }
        }
        catch { }

        return table;
    }

    private DataTable? GetSchema(DbConnection conn, String collection, String?[]? restrictions)
    {
        try
        {
            return conn.GetSchema(collection, restrictions);
        }
        catch
        {
            return null;
        }
    }

    private Type ParseDataType(String rawType)
    {
        rawType = rawType.ToLowerInvariant();

        return rawType switch
        {
            "int" or "integer" or "int32" => typeof(Int32),
            "bigint" or "int64" or "long" => typeof(Int64),
            "smallint" or "int16" or "short" => typeof(Int16),
            "tinyint" or "byte" => typeof(Byte),
            "bit" or "boolean" or "bool" => typeof(Boolean),
            "decimal" or "numeric" or "money" => typeof(Decimal),
            "float" or "double" => typeof(Double),
            "real" or "single" => typeof(Single),
            "datetime" or "datetime2" or "date" or "time" or "timestamp" => typeof(DateTime),
            "uniqueidentifier" or "guid" => typeof(Guid),
            "binary" or "varbinary" or "blob" or "image" => typeof(Byte[]),
            _ => typeof(String)
        };
    }
    #endregion

    #region 表操作
    /// <summary>设置表模型，检查数据表是否匹配</summary>
    public void SetTables(Migration mode, params IDataTable[] tables)
    {
        if (mode == Migration.Off) return;

        foreach (var table in tables)
        {
            try
            {
                SetTable(table, mode);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }

    private void SetTable(IDataTable table, Migration mode)
    {
        using var conn = Database.OpenConnection();

        // 检查表是否存在
        var exists = TableExists(conn, table.TableName);

        if (!exists && mode >= Migration.On)
        {
            // 创建表
            CreateTable(conn, table);
        }
        else if (exists && mode >= Migration.Full)
        {
            // 更新表结构
            UpdateTable(conn, table);
        }
    }

    private Boolean TableExists(DbConnection conn, String tableName)
    {
        try
        {
            var tables = GetSchema(conn, "Tables", null);
            if (tables == null) return false;

            return tables.Rows.Cast<DataRow>()
                .Any(r => r["TABLE_NAME"]?.ToString()?.EqualIgnoreCase(tableName) == true);
        }
        catch
        {
            return false;
        }
    }

    private void CreateTable(DbConnection conn, IDataTable table)
    {
        var sql = GetSchemaSQL(DDLSchema.CreateTable, table);
        if (sql.IsNullOrEmpty()) return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        // 创建索引
        foreach (var index in table.Indexes)
        {
            var indexSql = GetSchemaSQL(DDLSchema.CreateIndex, index);
            if (!indexSql.IsNullOrEmpty())
            {
                cmd.CommandText = indexSql;
                try { cmd.ExecuteNonQuery(); } catch { }
            }
        }
    }

    private void UpdateTable(DbConnection conn, IDataTable table)
    {
        // 获取现有表结构
        var existing = GetTable(conn, table.TableName);
        if (existing == null) return;

        // 检查并添加新列
        foreach (var col in table.Columns)
        {
            var exists = existing.Columns.Any(c => c.ColumnName.EqualIgnoreCase(col.ColumnName));
            if (!exists)
            {
                var sql = GetSchemaSQL(DDLSchema.AddColumn, table, col);
                if (!sql.IsNullOrEmpty())
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    try { cmd.ExecuteNonQuery(); } catch { }
                }
            }
        }
    }

    /// <summary>获取数据定义语句</summary>
    public String? GetSchemaSQL(DDLSchema schema, params Object?[] values)
    {
        return schema switch
        {
            DDLSchema.CreateTable when values.Length > 0 && values[0] is IDataTable table => GetCreateTableSql(table),
            DDLSchema.CreateIndex when values.Length > 0 && values[0] is IDataIndex index => GetCreateIndexSql(index),
            DDLSchema.AddColumn when values.Length > 1 && values[0] is IDataTable t && values[1] is IDataColumn c => GetAddColumnSql(t, c),
            DDLSchema.DropTable when values.Length > 0 && values[0] is IDataTable dt => $"DROP TABLE IF EXISTS {Database.FormatName(dt)}",
            DDLSchema.DropIndex when values.Length > 0 && values[0] is IDataIndex di => $"DROP INDEX IF EXISTS {di.Name}",
            _ => null
        };
    }

    /// <summary>设置数据定义模式</summary>
    public Object? SetSchema(DDLSchema schema, params Object?[] values)
    {
        var sql = GetSchemaSQL(schema, values);
        if (sql.IsNullOrEmpty()) return null;

        using var session = Database.CreateSession();
        return session.Execute(sql);
    }

    private String GetCreateTableSql(IDataTable table)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"CREATE TABLE {Database.FormatName(table)} (");

        var first = true;
        foreach (var col in table.Columns)
        {
            if (!first) sb.Append(", ");
            first = false;

            sb.Append($"{Database.FormatName(col)} {GetColumnType(col)}");

            if (col.PrimaryKey)
                sb.Append(" PRIMARY KEY");

            if (col.Identity)
                sb.Append(" AUTOINCREMENT");

            if (!col.Nullable)
                sb.Append(" NOT NULL");

            if (!col.DefaultValue.IsNullOrEmpty())
                sb.Append($" DEFAULT {col.DefaultValue}");
        }

        sb.Append(')');
        return sb.ToString();
    }

    private String GetColumnType(IDataColumn col)
    {
        var type = col.DataType;

        if (type == typeof(Int32) || type == typeof(Int16) || type == typeof(Byte))
            return "INTEGER";
        if (type == typeof(Int64))
            return "BIGINT";
        if (type == typeof(Boolean))
            return "BOOLEAN";
        if (type == typeof(Decimal))
            return $"DECIMAL({col.Precision},{col.Scale})";
        if (type == typeof(Double) || type == typeof(Single))
            return "REAL";
        if (type == typeof(DateTime))
            return "DATETIME";
        if (type == typeof(Guid))
            return "UNIQUEIDENTIFIER";
        if (type == typeof(Byte[]))
            return "BLOB";

        // 字符串
        if (col.Length <= 0 || col.Length > 4000)
            return "TEXT";

        return $"VARCHAR({col.Length})";
    }

    private String GetCreateIndexSql(IDataIndex index)
    {
        var table = index.Table;
        if (table == null) return String.Empty;

        var unique = index.Unique ? "UNIQUE " : "";
        var indexName = index.Name ?? $"IX_{table.TableName}_{String.Join("_", index.Columns)}";
        var cols = String.Join(", ", index.Columns.Select(c => Database.FormatName(c)));

        return $"CREATE {unique}INDEX {indexName} ON {Database.FormatName(table)} ({cols})";
    }

    private String GetAddColumnSql(IDataTable table, IDataColumn col)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"ALTER TABLE {Database.FormatName(table)} ADD {Database.FormatName(col)} {GetColumnType(col)}");

        if (!col.Nullable)
            sb.Append(" NOT NULL");

        if (!col.DefaultValue.IsNullOrEmpty())
            sb.Append($" DEFAULT {col.DefaultValue}");

        return sb.ToString();
    }
    #endregion
}
