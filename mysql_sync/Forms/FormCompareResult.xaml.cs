using mysql_sync.Class;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace mysql_sync.Forms
{
    public partial class FormCompareResult : Window
    {
        private readonly IList<MultiTableComparer.TableResult> _results;

        public MultiTableComparer selectedTable;

        public FormCompareResult(MultiTableComparer comparer)
        {
            InitializeComponent();

            // Carrega resultados
            _results = comparer.ResultsByTable?.ToList() ?? new List<MultiTableComparer.TableResult>();
            if (_results.Count == 0)
            {
                MessageBox.Show("Não há resultados para exibir.");
                Close();
                return;
            }

            // Popula lista de tabelas
            lvTables.DisplayMemberPath = "TableDisplay";
            lvTables.ItemsSource = _results
                .Select(tr => new TableListItem(tr))
                .ToList();

            // Seleciona a primeira tabela
            lvTables.SelectedIndex = 0;
        }

        private void lvTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(lvTables.SelectedItem is TableListItem item)) return;
            var tblResult = item.TableResult;
            if (tblResult == null) return;

            dgDetails.Columns.Clear();

            // PK
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "PK",
                Binding = new System.Windows.Data.Binding("Key")
            });

            // Status
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new System.Windows.Data.Binding("Status")
            });

            
            // Colunas selecionadas
            if (tblResult.SelectedColumns != null)
            {
                foreach (var col in tblResult.SelectedColumns)
                {
                    dgDetails.Columns.Add(new DataGridTextColumn
                    {
                        Header = $"M_{col.Name}",
                        Binding = new System.Windows.Data.Binding($"MasterRow[{col.Name}]")
                    });
                    dgDetails.Columns.Add(new DataGridTextColumn
                    {
                        Header = $"S_{col.Name}",
                        Binding = new System.Windows.Data.Binding($"SlaveRow[{col.Name}]")
                    });
                }
            }

            dgDetails.ItemsSource = tblResult.Rows
                .Where(r => r.Status != RowStatus.Equal)
                .ToList();
        }

        // Handle ao clicar com botão direito na linha
        private void dgRow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!(sender is DataGridRow row)) return;
            if (!(row.Item is ComparisonResult result)) return;

            var menu = new ContextMenu();
            switch (result.Status)
            {
                case RowStatus.OnlyInMaster:
                    menu.Items.Add(CreateMenuItem("Inserir no slave", (_, __) => InsertInSlave(result)));
                    menu.Items.Add(CreateMenuItem("Deletar do master", (_, __) => DeleteFromMaster(result)));
                    break;
                case RowStatus.OnlyInSlave:
                    menu.Items.Add(CreateMenuItem("Inserir no master", (_, __) => InsertInMaster(result)));
                    menu.Items.Add(CreateMenuItem("Deletar do slave", (_, __) => DeleteFromSlave(result)));
                    break;
                case RowStatus.Different:
                    menu.Items.Add(CreateMenuItem("Update slave ← master", (_, __) => UpdateSlaveFromMaster(result)));
                    menu.Items.Add(CreateMenuItem("Update master ← slave", (_, __) => UpdateMasterFromSlave(result)));
                    break;
            }

            menu.PlacementTarget = row;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        // Cria MenuItem
        private MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += handler;
            return mi;
        }

        private void DeleteFromSlave(ComparisonResult r) {
            var tab = _results.SingleOrDefault(x => x.TableName == r.SlaveRow.Table.TableName);
            if (tab != null) {
                tab.deleteSlave(r.Key.ToString());
                Console.WriteLine(tab.ToString());
            }
        }
        private void DeleteFromMaster(ComparisonResult r)
        {
            var tab = _results.SingleOrDefault(x => x.TableName == r.MasterRow.Table.TableName);
            if (tab != null)
            {
                tab.deleteMaster(r.Key.ToString());
                Console.WriteLine(tab.ToString());
            }
        }

        // Stubs de ação
        private void InsertInSlave(ComparisonResult r) { /* implementar */ }

        private void InsertInMaster(ComparisonResult r) { /* implementar */ }

        private void UpdateSlaveFromMaster(ComparisonResult r) { /* implementar */ }
        private void UpdateMasterFromSlave(ComparisonResult r) { /* implementar */ }
    }
}