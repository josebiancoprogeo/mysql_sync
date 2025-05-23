using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    /// <summary>
    /// Representa uma coluna de uma tabela ou view.
    /// </summary>
    public class Column
    {
        public string Name { get; }
        public string DataType { get; }
        public bool IsPrimaryKey { get; }

        public Column(string name, string dataType, bool isPrimaryKey)
        {
            Name = name;
            DataType = dataType;
            IsPrimaryKey = isPrimaryKey;
        }
    }
}
