﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using mysql_sync.Class;
using mysql_sync.Forms;
using MySqlX.XDevAPI.Common;

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
        try
        {
            var results = new List<ComparisonResult>();
            if (_columns.Where(c => c.IsPrimaryKey).Count() > 0)
            {

                // 1) monta SELECT dinamicamente
                var pkCols = _columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
                var otherCols = _columns.Where(c => !c.IsPrimaryKey)
                                         .Select(c => $"`{c.Name}`");

                var pkSelect = string.Join(", ", pkCols.Select(c => $"`{c}`"));
                var otherSelect = string.Join(", ", _columns.Where(c => !c.IsPrimaryKey).Select(c => $"`{c.Name}`"));

                var selectCols = pkSelect + 
                            (string.IsNullOrEmpty(otherSelect) ? "" : ", " + otherSelect);

                var sql = $"SELECT {selectCols} FROM `{_table.Parent.Name}`.`{_table.Name}`";

                var masterTask = _master.ExecuteQueryAsync(sql);
                var slaveTask = _slave.ExecuteQueryAsync(sql);

                // aguarda ambas terminarem
                await Task.WhenAll(masterTask, slaveTask);

                // recupera os DataTables
                DataTable masterDt = await masterTask;
                DataTable slaveDt = await slaveTask;

                Func<DataRow, string> getKey = row => string.Join("|", pkCols.Select(c => (row[c] ?? "").ToString()));

                // 3) indexa por PK
                var masterDict = masterDt.Rows.Cast<DataRow>()
                                  .ToDictionary(r => getKey(r));
                var slaveDict = slaveDt.Rows.Cast<DataRow>()
                                  .ToDictionary(r => getKey(r));

                // 4) junta chaves
                var allKeys = new HashSet<object>(masterDict.Keys);
                allKeys.UnionWith(slaveDict.Keys);

                
                foreach (var key in allKeys)
                {
                    masterDict.TryGetValue((string)key, out var mRow);
                    slaveDict.TryGetValue((string)key, out var sRow);

                    var status = RowStatus.OnlyInMaster;
                    if (mRow != null && sRow != null)
                    {
                        // compara coluna a coluna
                        //var equal = _columns.All(c =>
                        //    Equals(mRow[c.Name], sRow[c.Name]));
                        //status = equal ? RowStatus.Equal : RowStatus.Different;
                        bool equal = _columns.All(c =>
                            {
                                var mVal = mRow[c.Name];
                                var sVal = sRow[c.Name];
                                if (mVal is byte[] mb && sVal is byte[] sb)
                                    return mb.SequenceEqual(sb);
                                return Equals(mRow[c.Name], sRow[c.Name]);
                            }
                            );
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
            }
            Results = results;
        }
        catch (Exception ex)
        {
            var results = new List<ComparisonResult>();
            results.Add(new ComparisonResult
            {
                Key = -1,
                Status = RowStatus.Error
            });
            Results = results;
        }
    }

    private async Task<DataTable> LoadDataTableAsync(DatabaseConnection conn, string sql)
    {
        var dtTask = conn.ExecuteQueryAsync(sql);

        await Task.WhenAll(dtTask);

        DataTable Dt = await dtTask;

        return Dt;
    }
}
