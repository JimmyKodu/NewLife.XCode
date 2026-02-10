using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using NewLife;
using NewLife.Data;
using XCode.DataAccessLayer;

namespace XCode.EFCore;

/// <summary>EF Core 数据库会话</summary>
/// <remarks>
/// 实现 XCode 的 IDbSession 接口，使用 EF Core 执行数据库操作。
/// </remarks>
public class EFCoreSession : DisposeBase, IDbSession
{
    #region 属性
    /// <summary>数据库</summary>
    public IDatabase Database { get; }

    /// <summary>EF Core 数据库</summary>
    public EFCoreDatabase EFDatabase => (EFCoreDatabase)Database;

    /// <summary>数据库事务</summary>
    public ITransaction? Transaction { get; private set; }

    /// <summary>是否输出SQL</summary>
    public Boolean ShowSQL { get; set; }

    private DbContext? _context;
    private DbConnection? _connection;
    private DbTransaction? _transaction;
    private Int32 _transactionCount;
    #endregion

    #region 构造
    /// <summary>实例化 EF Core 会话</summary>
    public EFCoreSession(EFCoreDatabase database)
    {
        Database = database;
        ShowSQL = database.ShowSQL;
    }

    /// <summary>销毁</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        try
        {
            _transaction?.Dispose();
            _connection?.Dispose();
            _context?.Dispose();
        }
        catch { }
    }
    #endregion

    #region 打开/关闭
    /// <summary>打开连接并执行操作</summary>
    public TResult Process<TResult>(Func<DbConnection, TResult> callback)
    {
        var conn = _connection;
        var needClose = conn == null;

        if (conn == null)
        {
            conn = Database.OpenConnection();
            if (_transactionCount == 0) _connection = conn;
        }

        try
        {
            return callback(conn);
        }
        finally
        {
            if (needClose && _transactionCount == 0)
            {
                conn.Close();
                conn.Dispose();
                _connection = null;
            }
        }
    }

    /// <summary>异步打开连接并执行操作</summary>
    public async Task<TResult> ProcessAsync<TResult>(Func<DbConnection, Task<TResult>> callback)
    {
        var conn = _connection;
        var needClose = conn == null;

        if (conn == null)
        {
            conn = await Database.OpenConnectionAsync().ConfigureAwait(false);
            if (_transactionCount == 0) _connection = conn;
        }

        try
        {
            return await callback(conn).ConfigureAwait(false);
        }
        finally
        {
            if (needClose && _transactionCount == 0)
            {
                await conn.CloseAsync().ConfigureAwait(false);
                await conn.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
        }
    }

    /// <summary>设置是否显示SQL</summary>
    public IDisposable SetShowSql(Boolean showSql)
    {
        var old = ShowSQL;
        ShowSQL = showSql;
        return new ShowSqlScope(this, old);
    }

    private class ShowSqlScope : IDisposable
    {
        private readonly EFCoreSession _session;
        private readonly Boolean _oldValue;

        public ShowSqlScope(EFCoreSession session, Boolean oldValue)
        {
            _session = session;
            _oldValue = oldValue;
        }

        public void Dispose() => _session.ShowSQL = _oldValue;
    }
    #endregion

    #region 事务
    /// <summary>开始事务</summary>
    public Int32 BeginTransaction(IsolationLevel level)
    {
        _transactionCount++;

        if (_transactionCount == 1)
        {
            _connection = Database.OpenConnection();
            _transaction = _connection.BeginTransaction(level);
        }

        return _transactionCount;
    }

    /// <summary>提交事务</summary>
    public Int32 Commit()
    {
        if (_transactionCount <= 0) return 0;

        _transactionCount--;

        if (_transactionCount == 0 && _transaction != null)
        {
            _transaction.Commit();
            _transaction.Dispose();
            _transaction = null;

            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }

        return _transactionCount;
    }

    /// <summary>回滚事务</summary>
    public Int32 Rollback(Boolean ignoreException = true)
    {
        if (_transactionCount <= 0) return 0;

        _transactionCount--;

        if (_transactionCount == 0 && _transaction != null)
        {
            try
            {
                _transaction.Rollback();
            }
            catch
            {
                if (!ignoreException) throw;
            }
            finally
            {
                _transaction.Dispose();
                _transaction = null;

                _connection?.Close();
                _connection?.Dispose();
                _connection = null;
            }
        }

        return _transactionCount;
    }
    #endregion

    #region 基本方法 查询/执行
    /// <summary>执行SQL查询，返回记录集</summary>
    public DataSet Query(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        return Process(conn =>
        {
            using var cmd = CreateCommand(conn, sql, type, ps);
            using var adapter = EFDatabase.Factory.CreateDataAdapter()
                ?? throw new InvalidOperationException("无法创建 DataAdapter");
            adapter.SelectCommand = cmd;

            var ds = new DataSet();
            adapter.Fill(ds);
            return ds;
        });
    }

    /// <summary>执行DbCommand，返回记录集</summary>
    public DataSet Query(DbCommand cmd)
    {
        return Process(conn =>
        {
            cmd.Connection = conn;
            if (_transaction != null) cmd.Transaction = _transaction;

            using var adapter = EFDatabase.Factory.CreateDataAdapter()
                ?? throw new InvalidOperationException("无法创建 DataAdapter");
            adapter.SelectCommand = cmd;

            var ds = new DataSet();
            adapter.Fill(ds);
            return ds;
        });
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    public DbTable Query(SelectBuilder builder)
    {
        var sql = builder.ToString();
        var ps = builder.Parameters?.ToArray();
        return Query(sql, ps);
    }

    /// <summary>执行SQL查询，返回记录集</summary>
    public DbTable Query(String sql, IDataParameter[]? ps)
    {
        return Process(conn =>
        {
            using var cmd = CreateCommand(conn, sql, CommandType.Text, ps);
            using var reader = cmd.ExecuteReader();

            var dt = new DbTable();
            dt.Read(reader);
            return dt;
        });
    }

    /// <summary>执行SQL查询，返回总记录数</summary>
    public Int64 QueryCount(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var result = ExecuteScalar<Object>(sql, type, ps);
        return result == null ? 0 : Convert.ToInt64(result);
    }

    /// <summary>执行SQL查询，返回总记录数</summary>
    public Int64 QueryCount(SelectBuilder builder)
    {
        var sql = builder.SelectCount().ToString();
        var ps = builder.Parameters?.ToArray();
        return QueryCount(sql, CommandType.Text, ps);
    }

    /// <summary>快速查询单表记录数</summary>
    public Int64 QueryCountFast(String tableName)
    {
        var sql = $"SELECT COUNT(*) FROM {Database.FormatName(tableName)}";
        return QueryCount(sql);
    }

    /// <summary>执行SQL语句，返回受影响的行数</summary>
    public Int32 Execute(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        return Process(conn =>
        {
            using var cmd = CreateCommand(conn, sql, type, ps);
            return cmd.ExecuteNonQuery();
        });
    }

    /// <summary>执行DbCommand，返回受影响的行数</summary>
    public Int32 Execute(DbCommand cmd)
    {
        return Process(conn =>
        {
            cmd.Connection = conn;
            if (_transaction != null) cmd.Transaction = _transaction;
            return cmd.ExecuteNonQuery();
        });
    }

    /// <summary>执行插入语句并返回新增行的自动编号</summary>
    public Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        return Process(conn =>
        {
            // 添加返回自增 ID 的语句
            var identitySql = sql + "; SELECT LAST_INSERT_ROWID();";

            using var cmd = CreateCommand(conn, identitySql, type, ps);
            var result = cmd.ExecuteScalar();
            return result == null ? 0 : Convert.ToInt64(result);
        });
    }

    /// <summary>执行SQL语句，返回结果中的第一行第一列</summary>
    [return: MaybeNull]
    public T ExecuteScalar<T>(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        return Process(conn =>
        {
            using var cmd = CreateCommand(conn, sql, type, ps);
            var result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return default;

            return (T)Convert.ChangeType(result, typeof(T));
        });
    }

    /// <summary>创建DbCommand</summary>
    public DbCommand CreateCommand(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        var cmd = EFDatabase.Factory.CreateCommand()
            ?? throw new InvalidOperationException("无法创建 DbCommand");

        cmd.CommandText = sql;
        cmd.CommandType = type;

        if (Database.CommandTimeout > 0)
            cmd.CommandTimeout = Database.CommandTimeout;

        if (ps != null && ps.Length > 0)
        {
            foreach (var p in ps)
            {
                cmd.Parameters.Add(p);
            }
        }

        return cmd;
    }

    private DbCommand CreateCommand(DbConnection conn, String sql, CommandType type, IDataParameter[]? ps)
    {
        var cmd = CreateCommand(sql, type, ps);
        cmd.Connection = conn;
        if (_transaction != null) cmd.Transaction = _transaction;
        return cmd;
    }
    #endregion

    #region 批量操作
    /// <summary>批量插入</summary>
    public Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        var count = 0;
        var batchSize = Database.BatchSize;

        var sql = BuildInsertSql(table, columns);

        foreach (var batch in list.Chunk(batchSize))
        {
            count += Process(conn =>
            {
                var total = 0;
                foreach (var item in batch)
                {
                    var ps = BuildParameters(columns, item);
                    using var cmd = CreateCommand(conn, sql, CommandType.Text, ps);
                    total += cmd.ExecuteNonQuery();
                }
                return total;
            });
        }

        return count;
    }

    /// <summary>批量忽略插入</summary>
    public Int32 InsertIgnore(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        // 简化实现，直接调用 Insert（实际应用中需根据数据库类型实现 INSERT OR IGNORE）
        return Insert(table, columns, list);
    }

    /// <summary>批量替换</summary>
    public Int32 Replace(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list)
    {
        // 简化实现，直接调用 Insert（实际应用中需根据数据库类型实现 REPLACE）
        return Insert(table, columns, list);
    }

    /// <summary>批量更新</summary>
    public Int32 Update(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
    {
        var count = 0;
        var pk = table.PrimaryKeys.FirstOrDefault();
        if (pk == null) return 0;

        var updateCols = columns.Where(c => !c.PrimaryKey).ToArray();
        if (updateColumns != null && updateColumns.Count > 0)
            updateCols = updateCols.Where(c => updateColumns.Contains(c.Name)).ToArray();

        var sql = BuildUpdateSql(table, updateCols, pk);

        foreach (var item in list)
        {
            var ps = BuildParameters(updateCols, item);
            var pkValue = item[pk.Name];
            var pkParam = Database.CreateParameter(pk.Name, pkValue, pk);
            var allPs = ps.Append(pkParam).ToArray();

            count += Execute(sql, CommandType.Text, allPs);
        }

        return count;
    }

    /// <summary>批量插入或更新</summary>
    public Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list)
    {
        // 简化实现：先尝试更新，失败则插入
        var count = 0;
        var pk = table.PrimaryKeys.FirstOrDefault();

        foreach (var item in list)
        {
            if (pk != null)
            {
                var pkValue = item[pk.Name];
                if (pkValue != null && !pkValue.Equals(0))
                {
                    // 尝试更新
                    var updated = Update(table, columns, updateColumns, addColumns, new[] { item });
                    if (updated > 0)
                    {
                        count += updated;
                        continue;
                    }
                }
            }

            // 插入
            count += Insert(table, columns, new[] { item });
        }

        return count;
    }

    private String BuildInsertSql(IDataTable table, IDataColumn[] columns)
    {
        var tableName = Database.FormatName(table);
        var cols = String.Join(", ", columns.Select(c => Database.FormatName(c)));
        var values = String.Join(", ", columns.Select(c => Database.FormatParameterName(c.Name)));

        return $"INSERT INTO {tableName} ({cols}) VALUES ({values})";
    }

    private String BuildUpdateSql(IDataTable table, IDataColumn[] columns, IDataColumn pk)
    {
        var tableName = Database.FormatName(table);
        var sets = String.Join(", ", columns.Select(c => $"{Database.FormatName(c)} = {Database.FormatParameterName(c.Name)}"));
        var where = $"{Database.FormatName(pk)} = {Database.FormatParameterName(pk.Name)}";

        return $"UPDATE {tableName} SET {sets} WHERE {where}";
    }

    private IDataParameter[] BuildParameters(IDataColumn[] columns, IModel item)
    {
        var list = new List<IDataParameter>();
        foreach (var col in columns)
        {
            var value = item[col.Name];
            list.Add(Database.CreateParameter(col.Name, value, col));
        }
        return [.. list];
    }
    #endregion

    #region 高级
    /// <summary>清空数据表</summary>
    public Int32 Truncate(String tableName)
    {
        var sql = $"DELETE FROM {Database.FormatName(tableName)}";
        return Execute(sql);
    }
    #endregion

    #region 构架
    /// <summary>返回数据源的架构信息</summary>
    public DataTable GetSchema(DbConnection? conn, String collectionName, String?[]? restrictionValues)
    {
        if (conn == null)
        {
            return Process(c => c.GetSchema(collectionName, restrictionValues));
        }

        return conn.GetSchema(collectionName, restrictionValues);
    }
    #endregion
}
