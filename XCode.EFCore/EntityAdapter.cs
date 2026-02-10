using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NewLife;
using NewLife.Data;
using NewLife.Reflection;
using XCode.DataAccessLayer;

namespace XCode.EFCore;

/// <summary>XCode 实体到 EF Core 的适配器</summary>
/// <remarks>
/// 提供 XCode 实体与 EF Core 实体之间的桥接能力。
/// 允许在 EF Core DbContext 中操作 XCode 实体。
/// </remarks>
public class EntityAdapter
{
    /// <summary>数据库实例</summary>
    public EFCoreDatabase Database { get; }

    /// <summary>实例化</summary>
    public EntityAdapter(EFCoreDatabase database)
    {
        Database = database;
    }

    /// <summary>查询实体列表</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="where">查询条件</param>
    /// <returns>实体列表</returns>
    public IList<TEntity> Query<TEntity>(String? where = null) where TEntity : class, IModel, new()
    {
        using var session = Database.CreateSession();

        var table = GetTableInfo<TEntity>();
        var sql = BuildSelectSql(table, where);

        var dt = ((EFCoreSession)session).Query(sql, null);
        return LoadData<TEntity>(dt);
    }

    /// <summary>查询单个实体</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="key">主键值</param>
    /// <returns>实体对象，未找到时返回 null</returns>
    public TEntity? Find<TEntity>(Object key) where TEntity : class, IModel, new()
    {
        using var session = Database.CreateSession();

        var table = GetTableInfo<TEntity>();
        var pk = table.PrimaryKeys.FirstOrDefault();
        if (pk == null) return default;

        var where = $"{Database.FormatName(pk)} = {Database.FormatValue(pk, key)}";
        var sql = BuildSelectSql(table, where);

        var dt = ((EFCoreSession)session).Query(sql, null);
        var list = LoadData<TEntity>(dt);
        return list.FirstOrDefault();
    }

    /// <summary>插入实体</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="entity">实体</param>
    /// <returns>受影响的行数</returns>
    public Int32 Insert<TEntity>(TEntity entity) where TEntity : class, IModel
    {
        using var session = Database.CreateSession();

        var table = GetTableInfo<TEntity>();
        var columns = table.Columns.Where(c => !c.Identity).ToArray();

        return ((EFCoreSession)session).Insert(table, columns, new[] { entity });
    }

    /// <summary>更新实体</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="entity">实体</param>
    /// <returns>受影响的行数</returns>
    public Int32 Update<TEntity>(TEntity entity) where TEntity : class, IModel
    {
        using var session = Database.CreateSession();

        var table = GetTableInfo<TEntity>();
        var columns = table.Columns.Where(c => !c.PrimaryKey).ToArray();

        return ((EFCoreSession)session).Update(table, columns, null, null, new[] { entity });
    }

    /// <summary>删除实体</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="entity">实体</param>
    /// <returns>受影响的行数</returns>
    public Int32 Delete<TEntity>(TEntity entity) where TEntity : class, IModel
    {
        using var session = Database.CreateSession();

        var table = GetTableInfo<TEntity>();
        var pk = table.PrimaryKeys.FirstOrDefault();
        if (pk == null) return 0;

        var pkValue = entity[pk.Name];
        var where = $"{Database.FormatName(pk)} = {Database.FormatValue(pk, pkValue)}";
        var sql = $"DELETE FROM {Database.FormatName(table)} WHERE {where}";

        return session.Execute(sql);
    }

    /// <summary>保存实体（自动判断插入或更新）</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="entity">实体</param>
    /// <returns>受影响的行数</returns>
    public Int32 Save<TEntity>(TEntity entity) where TEntity : class, IModel, new()
    {
        var table = GetTableInfo<TEntity>();
        var pk = table.PrimaryKeys.FirstOrDefault();

        if (pk != null)
        {
            var pkValue = entity[pk.Name];
            if (pkValue != null && !IsDefaultValue(pkValue))
            {
                // 尝试更新
                var updated = Update(entity);
                if (updated > 0)
                    return updated;
            }
        }

        return Insert(entity);
    }

    /// <summary>批量插入</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="entities">实体列表</param>
    /// <returns>受影响的行数</returns>
    public Int32 InsertBatch<TEntity>(IEnumerable<TEntity> entities) where TEntity : class, IModel
    {
        using var session = Database.CreateSession();

        var table = GetTableInfo<TEntity>();
        var columns = table.Columns.Where(c => !c.Identity).ToArray();

        return ((EFCoreSession)session).Insert(table, columns, entities);
    }

    #region 辅助方法
    private IDataTable GetTableInfo<TEntity>()
    {
        var type = typeof(TEntity);
        var tableName = type.Name;

        // 尝试从属性获取表名
        var tableAttr = type.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();
        if (tableAttr != null)
            tableName = tableAttr.Name;

        // 使用 DAL.CreateTable() 创建表对象
        var table = DAL.CreateTable();
        table.Name = type.Name;
        table.TableName = tableName;
        table.DbType = Database.Type;

        // 获取属性
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // 跳过非映射属性
            var notMapped = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute>();
            if (notMapped != null) continue;

            var col = table.CreateColumn();
            col.Name = prop.Name;
            col.ColumnName = prop.Name;
            col.DataType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            // 检查主键
            var keyAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>();
            if (keyAttr != null)
                col.PrimaryKey = true;

            // 检查列名
            var columnAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>();
            if (columnAttr != null && !String.IsNullOrEmpty(columnAttr.Name))
                col.ColumnName = columnAttr.Name;

            // 检查字符串长度
            var maxLength = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.MaxLengthAttribute>();
            if (maxLength != null)
                col.Length = maxLength.Length;

            // 检查必填
            var required = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>();
            col.Nullable = required == null;

            table.Columns.Add(col);
        }

        // 如果没有主键，尝试查找 Id 属性
        if (!table.Columns.Any(c => c.PrimaryKey))
        {
            var idCol = table.Columns.FirstOrDefault(c => c.Name.EqualIgnoreCase("Id"));
            if (idCol != null)
                idCol.PrimaryKey = true;
        }

        return table;
    }

    private String BuildSelectSql(IDataTable table, String? where)
    {
        var cols = String.Join(", ", table.Columns.Select(c => Database.FormatName(c)));
        var sql = $"SELECT {cols} FROM {Database.FormatName(table)}";

        if (!String.IsNullOrEmpty(where))
            sql += $" WHERE {where}";

        return sql;
    }

    private IList<TEntity> LoadData<TEntity>(DbTable dt) where TEntity : class, IModel, new()
    {
        var list = new List<TEntity>();
        if (dt == null || dt.Rows == null) return list;

        var type = typeof(TEntity);
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var row in dt.Rows)
        {
            var entity = new TEntity();

            for (var i = 0; i < dt.Columns.Length; i++)
            {
                var colName = dt.Columns[i];
                var value = row[i];

                if (value == null || value == DBNull.Value) continue;

                if (props.TryGetValue(colName, out var prop) && prop.CanWrite)
                {
                    try
                    {
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        var convertedValue = Convert.ChangeType(value, targetType);
                        prop.SetValue(entity, convertedValue);
                    }
                    catch { }
                }
            }

            list.Add(entity);
        }

        return list;
    }

    private Boolean IsDefaultValue(Object value)
    {
        if (value == null) return true;

        var type = value.GetType();
        if (type.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }

        return false;
    }
    #endregion
}

/// <summary>EF Core 实体仓储</summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class EntityRepository<TEntity> where TEntity : class, new()
{
    private readonly DbContext _context;
    private readonly DbSet<TEntity> _dbSet;

    /// <summary>实例化</summary>
    public EntityRepository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    /// <summary>查询所有</summary>
    public IQueryable<TEntity> Query() => _dbSet.AsQueryable();

    /// <summary>根据条件查询</summary>
    public IQueryable<TEntity> Query(Expression<Func<TEntity, Boolean>> predicate)
        => _dbSet.Where(predicate);

    /// <summary>根据ID查找</summary>
    public TEntity? Find(params Object?[]? keyValues) => _dbSet.Find(keyValues);

    /// <summary>异步根据ID查找</summary>
    public async Task<TEntity?> FindAsync(params Object?[]? keyValues)
        => await _dbSet.FindAsync(keyValues).ConfigureAwait(false);

    /// <summary>添加实体</summary>
    public void Add(TEntity entity) => _dbSet.Add(entity);

    /// <summary>更新实体</summary>
    public void Update(TEntity entity) => _dbSet.Update(entity);

    /// <summary>删除实体</summary>
    public void Remove(TEntity entity) => _dbSet.Remove(entity);

    /// <summary>保存更改</summary>
    public Int32 SaveChanges() => _context.SaveChanges();

    /// <summary>异步保存更改</summary>
    public async Task<Int32> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>批量添加</summary>
    public void AddRange(IEnumerable<TEntity> entities) => _dbSet.AddRange(entities);

    /// <summary>批量删除</summary>
    public void RemoveRange(IEnumerable<TEntity> entities) => _dbSet.RemoveRange(entities);
}
