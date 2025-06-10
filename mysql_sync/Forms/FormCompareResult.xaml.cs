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
using System.Collections.ObjectModel;

namespace mysql_sync.Forms
{
    public partial class FormCompareResult : Window
    {
        private readonly ObservableCollection<MultiTableComparer.TableResult> _results;

        public ObservableCollection<MultiTableComparer.TableResult> ResultsCollection => _results;

        public MultiTableComparer selectedTable;



        public FormCompareResult(ObservableCollection<MultiTableComparer.TableResult> results, int totalTablesToCompare)
        {
            InitializeComponent();

            // Carrega resultados
            _results = results ?? throw new ArgumentNullException(nameof(results));

            // 1) Configura o painel “Comparando”:
            pbCompare.Minimum = 0;
            pbCompare.Maximum = totalTablesToCompare;
            pbCompare.Value = 0;
            tbCompareStatus.Text = $"Comparando 0/{totalTablesToCompare}";
            PanelCompare.Visibility = Visibility.Visible;
            // Sempre que adicionar um novo TableResult, incrementa o pbCompare e atualiza o texto
            _results.CollectionChanged += (s, ev) =>
            {
                // Garante que estamos no UI Thread:
                Dispatcher.Invoke(() =>
                {
                    pbCompare.Value = _results.Count;
                    tbCompareStatus.Text = $"Comparando {_results.Count}/{totalTablesToCompare}";

                    lvTables.ItemsSource = _results.Where(x => x.Rows.Count > 0);

                    if (_results.Count >= totalTablesToCompare)
                    {
                        PanelCompare.Visibility = Visibility.Collapsed;
                    }

                    AjustarColunasDeProgresso();
                });
            };

            // 2) O painel “Batch” começa oculto. Será mostrado quando o usuário clicar em algum botão:
            PanelBatch.Visibility = Visibility.Collapsed;

            // 3) Vincula a lista de tabelas e seleciona a primeira
            lvTables.DisplayMemberPath = "TableDisplay";
            lvTables.ItemsSource = _results.Where(x => x.Rows.Count > 0);
            if (_results.Count > 0)
                lvTables.SelectedIndex = 0;
        }

        // Reajusta as larguras das colunas de progresso conforme visibilidades
        private void AjustarColunasDeProgresso()
        {
            //// Se ambos os painéis estiverem visíveis, cada um ocupa metade
            //if (PanelCompare.Visibility == Visibility.Visible
            //    && PanelBatch.Visibility == Visibility.Visible)
            //{
            //    ColCompare.Width = new GridLength(1, GridUnitType.Star);
            //    ColBatch.Width = new GridLength(1, GridUnitType.Star);
            //}
            //// Se apenas “Comparando” estiver visível, ocupa todo o espaço
            //else if (PanelCompare.Visibility == Visibility.Visible)
            //{
            //    ColCompare.Width = new GridLength(1, GridUnitType.Star);
            //    ColBatch.Width = new GridLength(0, GridUnitType.Pixel);
            //}
            //// Se apenas “Batch” estiver visível, ocupa todo o espaço
            //else if (PanelBatch.Visibility == Visibility.Visible)
            //{
            //    ColCompare.Width = new GridLength(0, GridUnitType.Pixel);
            //    ColBatch.Width = new GridLength(1, GridUnitType.Star);
            //}
            //// Se nenhum estiver visível, escondemos ambos
            //else
            //{
            //    ColCompare.Width = new GridLength(0, GridUnitType.Pixel);
            //    ColBatch.Width = new GridLength(0, GridUnitType.Pixel);
            //}
        }

        private void lvTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(lvTables.SelectedItem is MultiTableComparer.TableResult tblResult)) return;

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

            // Colunas selecionadas (M_ e S_)
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
            if (!(lvTables.SelectedItem is MultiTableComparer.TableResult currentTable)) return;
            var selectedRows = dgDetails.SelectedItems.Cast<ComparisonResult>().ToList();

            if (selectedRows.Count > 1)
            {
                foreach (var r in selectedRows)
                    r.IsSelected = true;
            }
            else
            {
                foreach (var r in currentTable.Rows.Where(r => r.Status != RowStatus.Equal))
                    r.IsSelected = true;
            }

            dgDetails.Items.Refresh();
        }

        /// <summary>
        /// Desmarca todas as linhas atuais do DataGrid (IsSelected = false).
        /// </summary>
        private void HeaderChk_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!(lvTables.SelectedItem is MultiTableComparer.TableResult currentTable)) return;
            var selectedRows = dgDetails.SelectedItems.Cast<ComparisonResult>().ToList();

            if (selectedRows.Count > 1)
            {
                foreach (var r in selectedRows)
                    r.IsSelected = false;
            }
            else
            {
                foreach (var r in currentTable.Rows.Where(r => r.Status != RowStatus.Equal))
                    r.IsSelected = false;
            }

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

        private async void btnInsertSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!(lvTables.SelectedItem is MultiTableComparer.TableResult tblResult)) return;
            //var tblResult = item.TableResult;

            var toProcess = tblResult.Rows
                             .Where(r => r.IsSelected && r.Status != RowStatus.Different)
                             .ToList();
            if (toProcess.Count == 0) return;

            // 1) Exibe o painel Batch e ajusta colunas
            PanelBatch.Visibility = Visibility.Visible;
            //AjustarColunasDeProgresso();

            // 2) Configura o texto e a barra
            pbBatch.Minimum = 0;
            pbBatch.Maximum = toProcess.Count;
            pbBatch.Value = 0;
            tbBatchStatus.Text = $"Inserindo 0/{toProcess.Count}";
            await Task.Run(() =>
            {
                // 3) Loop de inserções
                for (int i = 0; i < toProcess.Count; i++)
                {
                    var r = toProcess[i];
                    if (r.Status == RowStatus.OnlyInSlave)
                        tblResult.InsertMaster(r);
                    else
                        tblResult.InsertSlave(r);

                    // Toda UI update pelo Dispatcher
                    Dispatcher.Invoke(() =>
                    {
                        tblResult.Rows.Remove(r);
                        pbBatch.Value = i + 1;
                        tbBatchStatus.Text = $"Inserindo {i + 1}/{toProcess.Count}";
                    });
                }
            });


            // 4) Finaliza: esconde painel Batch e ajusta colunas
            PanelBatch.Visibility = Visibility.Collapsed;
            //AjustarColunasDeProgresso();

            // 5) Atualiza grid
            lvTables_SelectionChanged(lvTables, null);
        }

        private async void btnUpdateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            bool updateMaster = btn.Name.Contains("Master", StringComparison.OrdinalIgnoreCase);

            if (!(lvTables.SelectedItem is MultiTableComparer.TableResult tblResult)) return;
            //var tblResult = item.TableResult;

            var toProcess = tblResult.Rows
                             .Where(r => r.IsSelected && r.Status == RowStatus.Different)
                             .ToList();
            if (toProcess.Count == 0) return;

            PanelBatch.Visibility = Visibility.Visible;
            //AjustarColunasDeProgresso();

            pbBatch.Minimum = 0;
            pbBatch.Maximum = toProcess.Count;
            pbBatch.Value = 0;
            tbBatchStatus.Text = updateMaster
                                 ? $"Atualizando Master 0/{toProcess.Count}"
                                 : $"Atualizando Slave 0/{toProcess.Count}";

            await Task.Run(() =>
            {
                for (int i = 0; i < toProcess.Count; i++)
                {
                    var r = toProcess[i];
                    if (updateMaster)
                        tblResult.UpdateMaster(r);
                    else
                        tblResult.UpdateSlave(r);


                    // Toda UI update pelo Dispatcher
                    Dispatcher.Invoke(() =>
                    {
                        tblResult.Rows.Remove(r);
                        pbBatch.Value = i + 1;
                        tbBatchStatus.Text = updateMaster
                                         ? $"Atualizando Master {i + 1}/{toProcess.Count}"
                                         : $"Atualizando Slave {i + 1}/{toProcess.Count}";
                    });
                }
            });

            PanelBatch.Visibility = Visibility.Collapsed;
            //AjustarColunasDeProgresso();
            lvTables_SelectionChanged(lvTables, null);
        }

        private async void btnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!(lvTables.SelectedItem is MultiTableComparer.TableResult tblResult)) return;

            //if (!(lvTables.SelectedItem is TableListItem item)) return;
            //var tblResult = item.TableResult;

            var toProcess = tblResult.Rows
                             .Where(r => r.IsSelected &&
                                         (r.Status == RowStatus.OnlyInMaster || r.Status == RowStatus.OnlyInSlave))
                             .ToList();
            if (toProcess.Count == 0) return;

            PanelBatch.Visibility = Visibility.Visible;
            AjustarColunasDeProgresso();

            pbBatch.Minimum = 0;
            pbBatch.Maximum = toProcess.Count;
            pbBatch.Value = 0;
            tbBatchStatus.Text = $"Deletando 0/{toProcess.Count}";
            // Executa em background
            await Task.Run(() =>
            {
                for (int i = 0; i < toProcess.Count; i++)
                {
                    var r = toProcess[i];
                    if (r.Status == RowStatus.OnlyInMaster)
                        tblResult.deleteMaster(r.Key.ToString());
                    else
                        tblResult.deleteSlave(r.Key.ToString());
                    tblResult.Rows.Remove(r);

                    // Toda UI update pelo Dispatcher
                    Dispatcher.Invoke(() =>
                    {
                        tblResult.Rows.Remove(r);
                        pbBatch.Value = i + 1;
                        tbBatchStatus.Text = $"Deletando {i + 1}/{toProcess.Count}";
                    });
                }
            });

            PanelBatch.Visibility = Visibility.Collapsed;
            //AjustarColunasDeProgresso();
            lvTables_SelectionChanged(lvTables, null);
        }
    }
}