using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mysql_sync.Class
{
    /// <summary>
    /// Representa uma coluna de uma tabela ou view.
    /// </summary>
    public class Column
    {
        public string Name { get; }
        public string DataType { get; }
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

        public Column(string name, string dataType, bool isPrimaryKey)
        {
            Name = name;
            DataType = dataType;
            IsPrimaryKey = isPrimaryKey;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
