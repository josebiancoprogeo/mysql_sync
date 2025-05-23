using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{

    /// <summary>
    /// Representa uma tabela no banco.
    /// </summary>
    public class Table : DatabaseObject, INotifyPropertyChanged
    {
        public Database Parent { get; set; }

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

        public ObservableCollection<Column> Columns { get; }
            = new ObservableCollection<Column>();

        public Table(string name, Database parent) : base(name)
        {
            Parent = parent;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
