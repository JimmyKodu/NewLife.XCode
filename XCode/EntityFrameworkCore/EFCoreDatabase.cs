#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace XCode.EntityFrameworkCore;

/// <summary>EF Core 数据库实现</summary>
/// <remarks>
/// 作为 XCode 与 EF Core 的适配器，实现 IDatabase 接口
/// 使 XCode 可以使用 EF Core 作为底层数据访问引擎
/// </remarks>
public class EFCoreDatabase : DbBase
{
    #region 属性
    private XCodeDbContext? _context;

    /// <summary>获取 DbContext</summary>
    protected XCodeDbContext Context
    {
        get
        {
            if (_context == null)
            {
                lock (this)
                {
                    if (_context == null)
                    {
                        _context = CreateContext();
                    }
                }
            }
            return _context;
        }
    }

    /// <summary>当前事务</summary>
    protected IDbContextTransaction? CurrentTransaction { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public EFCoreDatabase()
    {
        Type = DatabaseType.None;
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            CurrentTransaction?.Dispose();
            _context?.Dispose();
        }
    }
    #endregion

    #region 方法
    /// <summary>创建 DbContext</summary>
    /// <returns></returns>
    protected virtual XCodeDbContext CreateContext()
    {
        var context = new XCodeDbContext(ConnectionString, Type)
        {
            ConnName = ConnName
        };

        // 确保数据库已创建（如果配置了自动迁移）
        try
        {
            if (Migration >= Migration.On)
            {
                context.Database.EnsureCreated();
            }
        }
        catch (Exception ex)
        {
            WriteLog("EnsureCreated 失败：{0}", ex.Message);
        }

        return context;
    }

    /// <summary>创建数据库会话</summary>
    /// <returns></returns>
    public override IDbSession CreateSession()
    {
        return new EFCoreSession(this);
    }

    /// <summary>创建元数据对象</summary>
    /// <returns></returns>
    protected override IMetaData CreateMetaData() => new EFCoreMetaData { Database = this };
    #endregion

    #region 数据库特性
    /// <summary>是否支持批操作</summary>
    public override Boolean SupportBatch => true;

    /// <summary>保留字</summary>
    protected override String ReservedWordsStr
    {
        get
        {
            return Type switch
            {
                DatabaseType.SqlServer => "ADD,ALL,ALTER,AND,ANY,AS,ASC,AUTHORIZATION,BACKUP,BEGIN,BETWEEN,BREAK,BROWSE,BULK,BY,CASCADE,CASE,CHECK,CHECKPOINT,CLOSE,CLUSTERED,COALESCE,COLLATE,COLUMN,COMMIT,COMPUTE,CONSTRAINT,CONTAINS,CONTAINSTABLE,CONTINUE,CONVERT,CREATE,CROSS,CURRENT,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,CURRENT_USER,CURSOR,DATABASE,DBCC,DEALLOCATE,DECLARE,DEFAULT,DELETE,DENY,DESC,DISK,DISTINCT,DISTRIBUTED,DOUBLE,DROP,DUMP,ELSE,END,ERRLVL,ESCAPE,EXCEPT,EXEC,EXECUTE,EXISTS,EXIT,EXTERNAL,FETCH,FILE,FILLFACTOR,FOR,FOREIGN,FREETEXT,FREETEXTTABLE,FROM,FULL,FUNCTION,GOTO,GRANT,GROUP,HAVING,HOLDLOCK,IDENTITY,IDENTITY_INSERT,IDENTITYCOL,IF,IN,INDEX,INNER,INSERT,INTERSECT,INTO,IS,JOIN,KEY,KILL,LEFT,LIKE,LINENO,LOAD,MERGE,NATIONAL,NOCHECK,NONCLUSTERED,NOT,NULL,NULLIF,OF,OFF,OFFSETS,ON,OPEN,OPENDATASOURCE,OPENQUERY,OPENROWSET,OPENXML,OPTION,OR,ORDER,OUTER,OVER,PERCENT,PIVOT,PLAN,PRECISION,PRIMARY,PRINT,PROC,PROCEDURE,PUBLIC,RAISERROR,READ,READTEXT,RECONFIGURE,REFERENCES,REPLICATION,RESTORE,RESTRICT,RETURN,REVERT,REVOKE,RIGHT,ROLLBACK,ROWCOUNT,ROWGUIDCOL,RULE,SAVE,SCHEMA,SECURITYAUDIT,SELECT,SEMANTICKEYPHRASETABLE,SEMANTICSIMILARITYDETAILSTABLE,SEMANTICSIMILARITYTABLE,SESSION_USER,SET,SETUSER,SHUTDOWN,SOME,STATISTICS,SYSTEM_USER,TABLE,TABLESAMPLE,TEXTSIZE,THEN,TO,TOP,TRAN,TRANSACTION,TRIGGER,TRUNCATE,TRY_CONVERT,TSEQUAL,UNION,UNIQUE,UNPIVOT,UPDATE,UPDATETEXT,USE,USER,VALUES,VARYING,VIEW,WAITFOR,WHEN,WHERE,WHILE,WITH,WITHIN GROUP,WRITETEXT",
                DatabaseType.SQLite => "ABORT,ACTION,ADD,AFTER,ALL,ALTER,ANALYZE,AND,AS,ASC,ATTACH,AUTOINCREMENT,BEFORE,BEGIN,BETWEEN,BY,CASCADE,CASE,CAST,CHECK,COLLATE,COLUMN,COMMIT,CONFLICT,CONSTRAINT,CREATE,CROSS,CURRENT_DATE,CURRENT_TIME,CURRENT_TIMESTAMP,DATABASE,DEFAULT,DEFERRABLE,DEFERRED,DELETE,DESC,DETACH,DISTINCT,DROP,EACH,ELSE,END,ESCAPE,EXCEPT,EXCLUSIVE,EXISTS,EXPLAIN,FAIL,FOR,FOREIGN,FROM,FULL,GLOB,GROUP,HAVING,IF,IGNORE,IMMEDIATE,IN,INDEX,INDEXED,INITIALLY,INNER,INSERT,INSTEAD,INTERSECT,INTO,IS,ISNULL,JOIN,KEY,LEFT,LIKE,LIMIT,MATCH,NATURAL,NO,NOT,NOTNULL,NULL,OF,OFFSET,ON,OR,ORDER,OUTER,PLAN,PRAGMA,PRIMARY,QUERY,RAISE,RECURSIVE,REFERENCES,REGEXP,REINDEX,RELEASE,RENAME,REPLACE,RESTRICT,RIGHT,ROLLBACK,ROW,SAVEPOINT,SELECT,SET,TABLE,TEMP,TEMPORARY,THEN,TO,TRANSACTION,TRIGGER,UNION,UNIQUE,UPDATE,USING,VACUUM,VALUES,VIEW,VIRTUAL,WHEN,WHERE,WITH,WITHOUT",
                _ => base.ReservedWordsStr
            };
        }
    }
    #endregion

    #region 辅助方法
    /// <summary>获取 DbConnection</summary>
    /// <returns></returns>
    public DbConnection GetConnection() => Context.Database.GetDbConnection();

    /// <summary>开启事务</summary>
    /// <param name="level">隔离级别</param>
    /// <returns></returns>
    public IDbContextTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted)
    {
        CurrentTransaction = Context.Database.BeginTransaction(level);
        return CurrentTransaction;
    }

    /// <summary>开启事务（异步）</summary>
    /// <param name="level">隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel level = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var task = Context.Database.BeginTransactionAsync(level, cancellationToken);
        task.ContinueWith(t => CurrentTransaction = t.Result, TaskContinuationOptions.OnlyOnRanToCompletion);
        return task;
    }

    /// <summary>提交事务</summary>
    public void CommitTransaction()
    {
        CurrentTransaction?.Commit();
        CurrentTransaction?.Dispose();
        CurrentTransaction = null;
    }

    /// <summary>回滚事务</summary>
    public void RollbackTransaction()
    {
        CurrentTransaction?.Rollback();
        CurrentTransaction?.Dispose();
        CurrentTransaction = null;
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog? Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args)
    {
        Log?.Info($"[EFCore][{ConnName}] " + format, args);
    }
    #endregion
}

/// <summary>EF Core 会话</summary>
internal class EFCoreSession : DbSession
{
    private readonly EFCoreDatabase _database;

    public EFCoreSession(EFCoreDatabase database) : base(database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>开启事务</summary>
    /// <param name="level">隔离级别</param>
    /// <returns></returns>
    public override ITransaction BeginTransaction(IsolationLevel level)
    {
        var trans = _database.BeginTransaction(level);
        return new EFCoreTransaction(trans, _database);
    }
}

/// <summary>EF Core 事务</summary>
internal class EFCoreTransaction : ITransaction
{
    private readonly IDbContextTransaction _transaction;
    private readonly EFCoreDatabase _database;
    private Boolean _disposed;

    public EFCoreTransaction(IDbContextTransaction transaction, EFCoreDatabase database)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public Int32 Count { get; set; }

    public void Commit()
    {
        if (!_disposed)
        {
            _database.CommitTransaction();
        }
    }

    public void Rollback()
    {
        if (!_disposed)
        {
            _database.RollbackTransaction();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _transaction?.Dispose();
        }
    }
}

/// <summary>EF Core 元数据</summary>
internal class EFCoreMetaData : IMetaData
{
    public IDatabase? Database { get; set; }

    public String? Version => "EF Core 10";

    public IList<IDataTable> GetTables(String[]? names = null)
    {
        // TODO: 从 EF Core 模型中提取表信息
        return [];
    }

    public Boolean SetSchema(DDLSchema schema, params IDataTable[] tables)
    {
        // EF Core 使用 Migrations 管理架构变更
        // 这里可以实现基本的建表逻辑
        return false;
    }

    public IDataTable CreateTable(String tableName)
    {
        throw new NotImplementedException();
    }
}
#endif
