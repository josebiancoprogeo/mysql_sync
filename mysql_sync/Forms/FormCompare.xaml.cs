using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using mysql_sync.Class;

namespace mysql_sync.Forms
{
    public partial class FormCompare : Window
    {
        private readonly ObservableCollection<Table> _tables;
        private readonly ObservableCollection<ColumnSelection> _columnSelections
            = new ObservableCollection<ColumnSelection>();

        public Table SelectedTable => (Table)lvTables.SelectedItem;
        public IEnumerable<ColumnSelection> SelectedColumns => _columnSelections;

        // MUDAR AQUI: adicionamos o parâmetro databaseName
        public FormCompare(string databaseName, List<Table> tables)
        {
            InitializeComponent();

            // opcional: mostrar no título
            Title = $"Comparar Tabelas — Database: {databaseName}";

            _tables = new ObservableCollection<Table>(tables);
            lvTables.ItemsSource = _tables;

            lvColumns.ItemsSource = _columnSelections;
        }

        private void lvTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _columnSelections.Clear();

            if (lvTables.SelectedItem is Table tbl)
            {
                // para cada coluna da tabela, cria um ColumnSelection
                foreach (var col in tbl.Columns)
                {
                    _columnSelections.Add(new ColumnSelection(
                        col.Name,
                        col.IsPrimaryKey,   // marca PK
                        col.IsPrimaryKey    // PK sempre selecionada
                    ));
                }
            }
        }

        private void btnCompare_Click(object sender, RoutedEventArgs e)
        {
            // garante que haja uma tabela selecionada
            if (lvTables.SelectedItem == null)
            {
                MessageBox.Show("Selecione uma tabela para comparar.", "Aviso",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // retorna DialogResult=true para disparar a comparação
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // simplesmente fecha sem prosseguir
            DialogResult = false;
        }
    }

    // view-model para CheckBox de coluna
    public class ColumnSelection : INotifyPropertyChanged
    {
        public string Name { get; }
        public bool IsPrimaryKey { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public ColumnSelection(string name, bool isPrimaryKey, bool isSelected)
        {
            Name = name;
            IsPrimaryKey = isPrimaryKey;
            _isSelected = isSelected;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    // converter simples para inverter bool
    public class InverseBooleanConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value,
            System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : (object)false;

        public object ConvertBack(object value,
            System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : (object)false;
    }
}
