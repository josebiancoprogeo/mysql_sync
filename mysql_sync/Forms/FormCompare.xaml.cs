using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using mysql_sync.Class;
using System.Windows.Data;

namespace mysql_sync.Forms
{
    public partial class FormCompare : Window
    {
        private readonly ObservableCollection<Table> _tables;


        public Table SelectedTable => (Table)lvTables.SelectedItem;

        private ObservableCollection<Column> _selectedColumn;


        // MUDAR AQUI: adicionamos o parâmetro databaseName
        public FormCompare(string databaseName, List<Table> tables)
        {
            InitializeComponent();

            Title = $"Comparar Tabelas — Database: {databaseName}";

            _tables = new ObservableCollection<Table>(tables);
            lvTables.ItemsSource = _tables;

        }

        private void lvTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvTables.SelectedItem is Table tbl)
            {
                // ALTERAÇÃO AQUI: bind direto às Column.IsSelected de cada Column
                lvColumns.ItemsSource = tbl.Columns;
                _selectedColumn = tbl.Columns;
            }
        }

        // Marca todas as colunas(exceto PKs que já vêm desabilitados)
        private void chkSelectAllColumns_Checked(object sender, RoutedEventArgs e)
        {
            if (SelectedTable != null)
            {
                foreach (var cs in SelectedTable.Columns)
                {
                    if (!cs.IsPrimaryKey)    // permite que PKs permaneçam sempre selecionadas/desativadas conforme
                        cs.IsSelected = true;
                }

                // força o ListView de colunas a redesenhar todas as CheckBoxes
                CollectionViewSource.GetDefaultView(lvColumns.ItemsSource).Refresh();
            }
        }

        // Desmarca todas as colunas (exceto PKs)
        private void chkSelectAllColumns_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var cs in SelectedTable.Columns)
            {
                if (!cs.IsPrimaryKey)
                    cs.IsSelected = false;
            }
            // força o ListView de colunas a redesenhar todas as CheckBoxes
            CollectionViewSource.GetDefaultView(lvColumns.ItemsSource).Refresh();
        }

        private void btnCompare_Click(object sender, RoutedEventArgs e)
        {
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
