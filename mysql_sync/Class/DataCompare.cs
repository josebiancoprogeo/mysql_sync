using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using mysql_sync.Class;
using mysql_sync.Forms;

public class DataCompare
{
    private readonly DatabaseConnection _master;
    private readonly DatabaseConnection _slave;
    private readonly Table _table;  // suposição: compara uma tabela por vez
    private readonly IEnumerable<ColumnSelection> _columns;

    public IReadOnlyList<ComparisonResult> Results { get; private set; }

    public DataCompare(
        DatabaseConnection masterConn,
        DatabaseConnection slaveConn,
        Table table,
        IEnumerable<ColumnSelection> columns)
    {
        _master = masterConn;
        _slave = slaveConn;
        _table = table;
        _columns = columns.Where(c => c.IsSelected);
    }

    public void Execute()
    {
        // 1) monta SELECT dinamicamente
        var pkCol = _columns.First(c => c.IsPrimaryKey).Name;
        var otherCols = _columns.Where(c => !c.IsPrimaryKey)
                                 .Select(c => $"`{c.Name}`");
        var selectCols = $"`{pkCol}`"
                       + (otherCols.Any() ? ", " + string.Join(", ", otherCols) : "");

        var sql = $"SELECT {selectCols} FROM `{_table.Parent.Name}`.`{_table.Name}`";

        // 2) executa nos dois lados
        var masterDt = _master.ExecuteQuery(sql);
        var slaveDt = _slave.ExecuteQuery(sql);

        // 3) indexa por PK
        var masterDict = masterDt.Rows.Cast<DataRow>()
                          .ToDictionary(r => r[pkCol]);
        var slaveDict = slaveDt.Rows.Cast<DataRow>()
                          .ToDictionary(r => r[pkCol]);

        // 4) junta chaves
        var allKeys = new HashSet<object>(masterDict.Keys);
        allKeys.UnionWith(slaveDict.Keys);

        var results = new List<ComparisonResult>(allKeys.Count);
        foreach (var key in allKeys)
        {
            masterDict.TryGetValue(key, out var mRow);
            slaveDict.TryGetValue(key, out var sRow);

            var status = RowStatus.OnlyInMaster;
            if (mRow != null && sRow != null)
            {
                // compara coluna a coluna
                var equal = _columns.All(c =>
                    Equals(mRow[c.Name], sRow[c.Name]));
                status = equal ? RowStatus.Equal : RowStatus.Different;
            }
            else if (sRow != null)
            {
                status = RowStatus.OnlyInSlave;
            }

            results.Add(new ComparisonResult
            {
                Key = key,
                MasterRow = mRow,
                SlaveRow = sRow,
                Status = status
            });
        }

        Results = results;
    }

    private DataTable LoadDataTable(DatabaseConnection conn, string sql)
    {
        return conn.ExecuteQuery(sql);
    }
}
