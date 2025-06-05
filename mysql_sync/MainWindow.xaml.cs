using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using mysql_sync.Class;
using mysql_sync.Forms;
using MySql.Data.MySqlClient;
using System.Windows.Controls;

namespace mysql_sync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string ConfigFileName = "connections.json";

        private DatabaseConnection _selectedConnection;

        // no topo da classe
        private readonly List<(Database Db, Table Tbl)> _selected = new List<(Database, Table)>();


        /// <summary>
        /// Coleção de conexões gerenciadas pela aplicação.
        /// </summary>
        public ObservableCollection<DatabaseConnection> Connections { get; } = new ObservableCollection<DatabaseConnection>();

        /// <summary>
        /// Conexão atualmente selecionada.
        /// </summary>
        public DatabaseConnection SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (_selectedConnection != value)
                {
                    _selectedConnection = value;
                    OnPropertyChanged(nameof(SelectedConnection));
                    // Carrega canais assim que selecionada
                    _selectedConnection?.SynchronizeChannels();
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadConnections();
        }

        /// <summary>
        /// Carrega as strings de conexão salvas e instancia objetos DatabaseConnection.
        /// </summary>
        private void LoadConnections()
        {
            try
            {
                if (!File.Exists(ConfigFileName)) return;

                var json = File.ReadAllText(ConfigFileName, Encoding.UTF8);
                var list = JsonSerializer.Deserialize<List<ConnectionConfig>>(json);
                if (list == null) return;

                foreach (var cfg in list)
                {
                    var db = new DatabaseConnection(cfg.ConnectionString)
                    {
                        Name = cfg.Name
                    };
                    Connections.Add(db);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar configurações: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Persiste as strings de conexão configuradas.
        /// </summary>
        private void SaveConnections()
        {
            try
            {
                var list = Connections
                    .Select(c => new ConnectionConfig
                    {
                        Name = c.Name,
                        ConnectionString = c.ConnectionString
                    })
                    .ToList();
                var json = JsonSerializer.Serialize(list,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFileName, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar configurações: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Adiciona nova conexão.
        /// </summary>
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var form = new FormDBConnection { Owner = this };
            var newConn = new DatabaseConnection("") { Name = "" };
            form.DataContext = newConn;
            if (form.ShowDialog() == true)
            {
                if (newConn.TestConnection())
                {
                    Connections.Add(newConn);
                    SaveConnections();
                    newConn.StartRefreshCycle();
                }
                else
                {
                    MessageBox.Show("Não foi possível conectar com as credenciais informadas.", "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// Remove a conexão selecionada.
        /// </summary>
        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConnection != null)
            {
                var result = MessageBox.Show($"Remover a conexão '{SelectedConnection.Name}'?", "Confirmar Remoção", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Connections.Remove(SelectedConnection);
                    SaveConnections();
                    SelectedConnection = null;
                }
            }
        }

        /// <summary>
        /// Edita a conexão selecionada no duplo clique.
        /// </summary>
        private void lstConnections_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SelectedConnection != null)
            {
                var clone = new DatabaseConnection(SelectedConnection.ConnectionString) { Name = SelectedConnection.Name };
                var form = new FormDBConnection { Owner = this, DataContext = clone };
                if (form.ShowDialog() == true)
                {
                    if (clone.TestConnection())
                    {
                        SelectedConnection.ConnectionString = clone.ConnectionString;
                        SelectedConnection.Name = clone.Name;
                        SaveConnections();
                    }
                    else
                    {
                        MessageBox.Show("Não foi possível conectar com as credenciais informadas.", "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        private void MarkAsMaster_Click(object sender, RoutedEventArgs e)
        {
            // pega o DatabaseConnection ligado ao MenuItem
            if (sender is MenuItem mi
                && mi.DataContext is DatabaseConnection conn)
            {
                // zera todos
                foreach (var c in Connections)
                    c.Master = false;

                // marca só o clicado
                conn.Master = true;

                // se DatabaseConnection implementar INotifyPropertyChanged
                // e notificar Master, a UI já reflete. Senão:
                tvConnections.Items.Refresh();
            }
        }
        // Seleção padrão (conexão) – já existia
        private void tvConnections_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Só “pega” a seleção se for uma DatabaseConnection
            if (e.NewValue is DatabaseConnection conn)
                SelectedConnection = conn;
            else
                SelectedConnection = null;
        }

        // Handler para multi‐select das tabelas
        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem tvi
                && tvi.DataContext is Table tbl)
            {
                // encontra o Database pai
                var parentDb = Connections
                    .SelectMany(c => c.Databases)
                    .FirstOrDefault(d => d.Objects.Contains(tbl));

                if (parentDb != null
                 && !_selected.Any(x => x.Tbl == tbl))
                {
                    _selected.Add((parentDb, tbl));
                }
            }
            e.Handled = true;
        }

        private void TreeViewItem_Unselected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem tvi
                && tvi.DataContext is Table tbl)
            {
                _selected.RemoveAll(x => x.Tbl == tbl);
            }
            e.Handled = true;
        }

        private async void CompareTables_Click(object sender, RoutedEventArgs e)
        {
            // 1) coleta todas as tabelas marcadas em qualquer conexão
            var marked = Connections
              .SelectMany(c => c.Databases)
              .SelectMany(d => d.Objects.OfType<Table>())
              .Where(t => t.IsSelected)
              .ToList();

            if (marked.Count < 1)
            {
                MessageBox.Show("Marque ao menos uma tabela para comparar.",
                                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2) garante que todas vêm do mesmo database
            var distinctDbs = marked.Select(t => t.Parent).Distinct().ToList();
            if (distinctDbs.Count > 1)
            {
                MessageBox.Show("Só é possível comparar tabelas de um único database por vez.",
                                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dbInfo = distinctDbs[0];

            // 3) pega a conexão Master
            var master = Connections.FirstOrDefault(c => c.Master);
            if (master == null)
            {
                MessageBox.Show("Defina uma conexão Master antes de comparar.",
                                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var slave = Connections.FirstOrDefault(c => c.Master == false);
            if (slave == null)
            {
                MessageBox.Show("Defina uma conexão Slave antes de comparar.",
                                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 4) filtra as instâncias de Table na Master  
            var masterDb = master.Databases.FirstOrDefault(d => d.Name == dbInfo.Name);
            if (masterDb == null)
            {
                MessageBox.Show($"Database '{dbInfo.Name}' não existe na conexão Master.",
                                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var masterTables = masterDb.Objects
                .OfType<Table>()
                .Where(t => marked.Any(m => m.Name == t.Name))
                .ToList();
            if (masterTables.Count == 0)
            {
                MessageBox.Show($"Nenhuma das tabelas marcadas foi encontrada em '{dbInfo.Name}' da Master.",
                                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 5) Abre o FormCompare e só continua se o usuário clicar em "OK"
            var form = new FormCompare(dbInfo.Name, masterTables) { Owner = this };
            var result = form.ShowDialog();
            if (result != true)
                return;

            // 6) Ao fechar com OK, pega a tabela e colunas selecionadas no próprio form
            //    (FormCompare deve expor estas duas propriedades)
            //var tableToCompare = form.SelectedTable;          // instância de Table
            //var selectedColumns = form.SelectedColumns.Select(c => c.Name).ToList();

            // 7) Dispara o compare
            var comparer = new MultiTableComparer(
                master: master,
                slave: slave,           // sua instância de slave
                tables: masterTables    // lista de Table vindas do Master

            );

            await comparer.Execute();

            // 8) Mostra o resultado
            var resultForm = new FormCompareResult(comparer) { Owner = this };
            resultForm.ShowDialog();
        }
    }
}
