using mysql_sync.Class;

public class MultiTableComparer
{
    public class TableResult
    {
        public string TableName { get; set; }
        public List<ComparisonResult> Rows { get; set; }
        public int DivergenceCount
            => Rows.Count(r => r.Status != RowStatus.Equal);
    }

    private readonly DatabaseConnection _master;
    private readonly DatabaseConnection _slave;
    private readonly IEnumerable<Table> _tables;
    private readonly IEnumerable<string> _columns; // nomes de colunas selecionadas, incluindo PK

    public IReadOnlyList<TableResult> ResultsByTable { get; private set; }

    public MultiTableComparer(
        DatabaseConnection master,
        DatabaseConnection slave,
        IEnumerable<Table> tables,
        IEnumerable<string> columns)
    {
        _master = master;
        _slave = slave;
        _tables = tables;
        _columns = columns;
    }

    public void Execute()
    {
        var list = new List<TableResult>();
        foreach (var tbl in _tables)
        {
            //// monta e executa exatamente como antes, mas por tabela
            ////var compare = new DataCompare(_master, _slave, tbl,
            ////                  _columns.Select(n => new ColumnSelection(n, n == tbl.Columns.First(c => c.IsPrimaryKey).Name, true)));
            ////compare.Execute();

            //list.Add(new TableResult
            //{
            //    TableName = tbl.Name,
            //    Rows = compare.Results.ToList()
            //});
        }
        ResultsByTable = list;
    }
}
