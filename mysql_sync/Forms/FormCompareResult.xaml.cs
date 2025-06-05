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
using System.Windows.Data;

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

            // 1) Coluna de checkbox com “Check All” no header
            var headerChk = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            headerChk.Checked += HeaderChk_Checked;
            headerChk.Unchecked += HeaderChk_Unchecked;

            var chkColumn = new DataGridCheckBoxColumn
            {
                Header = headerChk,
                Binding = new System.Windows.Data.Binding("IsSelected"),
                Width = 30,
                IsReadOnly = false
            };
            dgDetails.Columns.Add(chkColumn);

            // PK
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "PK",
                Binding = new System.Windows.Data.Binding("Key"),
                IsReadOnly = true
            });

            // Status
            dgDetails.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new System.Windows.Data.Binding("Status"),
                IsReadOnly = true
            });


            // Colunas selecionadas
            if (tblResult.SelectedColumns != null)
            {
                foreach (var col in tblResult.SelectedColumns)
                {
                    dgDetails.Columns.Add(new DataGridTextColumn
                    {
                        Header = $"M_{col.Name}",
                        Binding = new System.Windows.Data.Binding($"MasterRow[{col.Name}]"),
                        IsReadOnly = true
                    });
                    dgDetails.Columns.Add(new DataGridTextColumn
                    {
                        Header = $"S_{col.Name}",
                        Binding = new System.Windows.Data.Binding($"SlaveRow[{col.Name}]"), 
                        IsReadOnly = true

                        
                    });
                }
            }

            dgDetails.ItemsSource = tblResult.Rows
                .Where(r => r.Status != RowStatus.Equal)
                .ToList();
        }

        /// <summary>
        /// Marca todas as linhas atuais do DataGrid como IsSelected = true.
        /// </summary>
        private void HeaderChk_Checked(object sender, RoutedEventArgs e)
        {
            if (!(lvTables.SelectedItem is TableListItem currentItem)) return;
            var rows = currentItem.TableResult.Rows;

            foreach (var r in rows)
                r.IsSelected = true;

            // Como ItemsSource é uma lista nova a cada Select, basta chamar Refresh:
            dgDetails.Items.Refresh();
        }

        /// <summary>
        /// Desmarca todas as linhas atuais do DataGrid (IsSelected = false).
        /// </summary>
        private void HeaderChk_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!(lvTables.SelectedItem is TableListItem currentItem)) return;
            var rows = currentItem.TableResult.Rows;

            foreach (var r in rows)
                r.IsSelected = false;

            dgDetails.Items.Refresh();
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

        private void DeleteFromSlave(ComparisonResult r)
        {
            var tab = _results.SingleOrDefault(x => x.TableName == r.SlaveRow.Table.TableName);
            if (tab != null)
            {
                tab.deleteSlave(r.Key.ToString());
                Console.WriteLine(tab.ToString());
                RemoveRowFromGrid(r);
            }
        }
        private void DeleteFromMaster(ComparisonResult r)
        {
            var tab = _results.SingleOrDefault(x => x.TableName == r.MasterRow.Table.TableName);
            if (tab != null)
            {
                tab.deleteMaster(r.Key.ToString());
                Console.WriteLine(tab.ToString());
                RemoveRowFromGrid(r);
            }
        }

        // Por exemplo, em FormCompareResult.xaml.cs:
        private void InsertInMaster(ComparisonResult r)
        {
            // pega o TableResult atual
            var item = (TableListItem)lvTables.SelectedItem;
            var tblResult = item.TableResult;
            tblResult.InsertMaster(r);
            RemoveRowFromGrid(r);
        }

        private void InsertInSlave(ComparisonResult r)
        {
            var item = (TableListItem)lvTables.SelectedItem;
            var tblResult = item.TableResult;
            tblResult.InsertSlave(r);
            RemoveRowFromGrid(r);
        }

        private void UpdateSlaveFromMaster(ComparisonResult r)
        {
            var item = (TableListItem)lvTables.SelectedItem;
            var tblResult = item.TableResult;
            tblResult.UpdateSlave(r);
            RemoveRowFromGrid(r);
        }
        private void UpdateMasterFromSlave(ComparisonResult r)
        {
            var item = (TableListItem)lvTables.SelectedItem;
            var tblResult = item.TableResult;
            tblResult.UpdateMaster(r);
            RemoveRowFromGrid(r);
        }

        // Remove a linha da grid e atualiza visualmente
        private void RemoveRowFromGrid(ComparisonResult r)
        {
            var selected = (TableListItem)lvTables.SelectedItem;
            selected.TableResult.Rows.Remove(r);
            // Atualiza colunas e itens
            lvTables_SelectionChanged(lvTables, null);
        }


        //
        // ===== Métodos de ação em massa (botões) =====
        //

        private void btnInsertSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!(lvTables.SelectedItem is TableListItem item)) return;
            var tblResult = item.TableResult;
            // Para cada linha marcada, e que esteja OnlyInSlave, faz Insert no Master
            var toProcess = tblResult.Rows.Where(r => r.IsSelected && r.Status == RowStatus.OnlyInSlave).ToList();
            foreach (var r in toProcess)
            {
                if (r.Status == RowStatus.OnlyInMaster)
                    tblResult.InsertSlave(r);
                else
                    tblResult.InsertMaster(r);
                tblResult.Rows.Remove(r);
            }
            lvTables_SelectionChanged(lvTables, null);
        }

        private void btnUpdateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            bool updateMaster = btn.Name.Contains("Master", StringComparison.OrdinalIgnoreCase);

            if (!(lvTables.SelectedItem is TableListItem item)) return;
            var tblResult = item.TableResult;
            // Linhas marcadas e com Status = Different
            var toProcess = tblResult.Rows.Where(r => r.IsSelected && r.Status == RowStatus.Different).ToList();
            foreach (var r in toProcess)
            {
                // Atualiza Slave a partir de Master
                if (updateMaster)
                    tblResult.UpdateMaster(r);
                else
                    tblResult.UpdateSlave(r);
                tblResult.Rows.Remove(r);
            }
            lvTables_SelectionChanged(lvTables, null);
        }

        private void btnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!(lvTables.SelectedItem is TableListItem item)) return;
            var tblResult = item.TableResult;
            // Linhas marcadas: podem ser OnlyInMaster ou OnlyInSlave
            var toProcess = tblResult.Rows.Where(r => r.IsSelected && (r.Status == RowStatus.OnlyInMaster || r.Status == RowStatus.OnlyInSlave)).ToList();
            foreach (var r in toProcess)
            {
                if (r.Status == RowStatus.OnlyInMaster)
                    tblResult.deleteMaster(r.Key.ToString()); // deleta do Slave
                else // OnlyInSlave
                    tblResult.deleteSlave(r.Key.ToString());
                tblResult.Rows.Remove(r);
            }
            lvTables_SelectionChanged(lvTables, null);
        }
    }
}