using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    public class TableListItem
    {
        public MultiTableComparer.TableResult TableResult { get; }
        public string TableDisplay { get; }

        public TableListItem(MultiTableComparer.TableResult tr)
        {
            TableResult = tr;
            TableDisplay = $"{tr.TableName} ({tr.DivergenceCount})";
        }

        // assim você pode fazer DisplayMemberPath="TableDisplay"
    }
}
