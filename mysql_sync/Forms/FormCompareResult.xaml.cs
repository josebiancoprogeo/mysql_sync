using System;
using System.Data;
using System.Linq;
using System.Windows;
using mysql_sync.Class;

namespace mysql_sync.Forms
{
    public partial class FormCompareResult : Window
    {
        public FormCompareResult(DataCompare comparer)
        {
            InitializeComponent();

            // *** MASTER ***
            var masterRows = comparer.Results
                .Where(r => r.MasterRow != null)
                .Select(r => r.MasterRow)
                .ToList();

            DataTable dtMaster = new DataTable();
            if (masterRows.Any())
            {
                // clona esquema
                dtMaster = masterRows.First().Table.Clone();

                // importa as linhas
                foreach (var row in masterRows)
                    dtMaster.ImportRow(row);
            }

            dgMaster.ItemsSource = dtMaster.DefaultView;

            // *** SLAVE ***
            var slaveRows = comparer.Results
                .Where(r => r.SlaveRow != null)
                .Select(r => r.SlaveRow)
                .ToList();

            DataTable dtSlave = new DataTable();
            if (slaveRows.Any())
            {
                dtSlave = slaveRows.First().Table.Clone();
                foreach (var row in slaveRows)
                    dtSlave.ImportRow(row);
            }

            dgSlave.ItemsSource = dtSlave.DefaultView;

            // *** RESULTADOS DE COMPARAÇÃO ***
            dgCompare.ItemsSource = comparer.Results
                .Where(r => r.Status != RowStatus.Equal)
                .ToList();
        }
    }
}
