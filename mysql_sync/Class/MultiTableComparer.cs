using mysql_sync.Class;
using mysql_sync.Forms;
using System.Data;

public class MultiTableComparer
{
    public class TableResult
    {
        public string TableName { get; set; }
        public List<ComparisonResult> Rows { get; set; }
        public List<Column> SelectedColumns { get; set; }
        public Column PrimaryKey { get; set; }

        public MultiTableComparer Parent { get; set; }

        public int DivergenceCount
            => Rows.Count(r => r.Status != RowStatus.Equal);

        /// <summary>
        /// Insere no MASTER os valores vindos de `SlaveRow` (usado quando Status = OnlyInSlave).
        /// </summary>
        internal void InsertMaster(ComparisonResult r)
        {
            Parent.InsertRowIntoMaster(
                TableName,
                r.Key.ToString()
            );
        }

        /// <summary>
        /// Insere no SLAVE os valores vindos de `MasterRow` (usado quando Status = OnlyInMaster).
        /// </summary>
        internal void InsertSlave(ComparisonResult r)
        {
            Parent.InsertRowIntoSlave(
                TableName,
                r.Key.ToString()
            );
        }


        internal void deleteMaster(string key)
        {
            Parent.deleteMaster(TableName, PrimaryKey.Name, key);
        }

        internal void deleteSlave(string key)
        {
            Parent.deleteSlave(TableName, PrimaryKey.Name, key);
        }

        internal void UpdateSlave(ComparisonResult r)
        {
            Parent.UpdateRowIntoSlave(
               TableName,
               r.Key.ToString()
           );
        }

        internal void UpdateMaster(ComparisonResult r)
        {
            Parent.UpdateRowIntoMaster(
                TableName,
                r.Key.ToString()
            );
        }
    }



    private readonly DatabaseConnection _master;
    private readonly DatabaseConnection _slave;
    private readonly IEnumerable<Table> _tables;


    public IReadOnlyList<TableResult> ResultsByTable { get; private set; }

    public MultiTableComparer(
        DatabaseConnection master,
        DatabaseConnection slave,
        IEnumerable<Table> tables)
    {
        _master = master;
        _slave = slave;
        _tables = tables;

    }

    #region Insert
    /// <summary>
    /// Insere no banco MASTER os valores passados em `rowData`.
    /// </summary>
    public async void InsertRowIntoMaster(
        string tableName,
        string key)
    {
        var tab = _tables.SingleOrDefault(x => x.Name == tableName);
        if (tab != null)
        {
            var rowData = await _slave.SelectRowByIDAsync(tab, key);
            _master.InsertRow(tab, rowData);
        }
    }

    /// <summary>
    /// Insere no banco SLAVE os valores passados em `rowData`.
    /// </summary>
    public async void InsertRowIntoSlave(
        string tableName,
        string key)
    {
        var tab = _tables.SingleOrDefault(x => x.Name == tableName);
        if (tab != null)
        {
            var rowData = await _master.SelectRowByIDAsync(tab, key);
            _slave.InsertRow(tab, rowData);
        }
        
    }
    #endregion

    #region Update
    private async void UpdateRowIntoMaster(string tableName, string key)
    {
        var tab = _tables.SingleOrDefault(x => x.Name == tableName);
        if (tab != null)
        {
            var rowData = await _slave.SelectRowByIDAsync(tab, key);
            _master.UpdateRow(tab, rowData);
        }
    }

    private async void UpdateRowIntoSlave(string tableName, string key)
    {
        var tab = _tables.SingleOrDefault(x => x.Name == tableName);
        if (tab != null)
        {
            var rowData = await _master.SelectRowByIDAsync(tab, key);
            _slave.UpdateRow(tab, rowData);
        }
    }
    #endregion

    #region delete
    private void deleteMaster(string tableName, string columName, string key)
    {
        _master.DeleteRow(tableName, columName, key);
    }

    private void deleteSlave(string tableName, string columName, string key)
    {
        _slave.DeleteRow(tableName, columName, key);
    }
    #endregion

    #region comparação
    public async Task Execute()
    {
        var list = new List<TableResult>();
        foreach (var tbl in _tables)
        {
            // monta e executa exatamente como antes, mas por tabela
            var compare = new DataCompare(_master, _slave, tbl);
            await compare.ExecuteAsync();

            list.Add(new TableResult
            {
                TableName = tbl.Name,
                Rows = compare.Results.Where(x => x.Status != RowStatus.Equal).ToList(),
                SelectedColumns = tbl.Columns.Where(x => x.IsSelected).ToList(),
                PrimaryKey = tbl.Columns.SingleOrDefault(x => x.IsPrimaryKey),
                Parent = this
            });
        }
        ResultsByTable = list;
    }
    #endregion
}

