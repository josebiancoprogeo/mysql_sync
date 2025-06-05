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
    private readonly IEnumerable<Column> _columns;

    public IReadOnlyList<ComparisonResult> Results { get; private set; }

    public DataCompare(
        DatabaseConnection masterConn,
        DatabaseConnection slaveConn,
        Table table
        )
    {
        _master = masterConn;
        _slave = slaveConn;
        _table = table;
        _columns = table.Columns.Where(c => c.IsSelected || c.IsPrimaryKey).ToList();
    }

    public async Task ExecuteAsync()
    {
        // 1) monta SELECT dinamicamente
        var pkCol = _columns.First(c => c.IsPrimaryKey).Name;
        var otherCols = _columns.Where(c => !c.IsPrimaryKey)
                                 .Select(c => $"`{c.Name}`");
        var selectCols = $"`{pkCol}`"
                       + (otherCols.Any() ? ", " + string.Join(", ", otherCols) : "");

        var sql = $"SELECT {selectCols} FROM `{_table.Parent.Name}`.`{_table.Name}`";

        var masterTask = _master.ExecuteQueryAsync(sql);
        var slaveTask = _slave.ExecuteQueryAsync(sql);

        // aguarda ambas terminarem
        await Task.WhenAll(masterTask, slaveTask);

        // recupera os DataTables
        DataTable masterDt = await masterTask;
        DataTable slaveDt = await slaveTask;

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

    private async Task<DataTable> LoadDataTableAsync(DatabaseConnection conn, string sql)
    {
        var dtTask = conn.ExecuteQueryAsync(sql);

        await Task.WhenAll(dtTask);

        DataTable Dt = await dtTask;

        return Dt;
    }
}
