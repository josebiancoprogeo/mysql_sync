using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    /// <summary>
    /// Representa um canal de replicação com seus status.
    /// </summary>
    public class ReplicationChannel
    {
        public string ChannelName { get; set; }
        public SlaveStatus SlaveStatus { get; set; }
        public ObservableCollection<ApplierStatus> ApplierStatuses { get; } = new ObservableCollection<ApplierStatus>();
    }
}
