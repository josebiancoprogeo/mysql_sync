﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    public enum RowStatus
    {
        OnlyInMaster,
        OnlyInSlave,
        Equal,
        Different
    }

    public class ComparisonResult
    {
        public object Key { get; set; }
        public DataRow MasterRow { get; set; }
        public DataRow SlaveRow { get; set; }
        public RowStatus Status { get; set; }
    }
}
