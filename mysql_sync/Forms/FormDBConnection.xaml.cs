using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace mysql_sync.Forms
{
    /// <summary>
    /// Lógica interna para FormDBConnection.xaml
    /// </summary>
    public partial class FormDBConnection : Window
    {
        public FormDBConnection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Confirma a edição/criação da conexão.
        /// </summary>
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            // Fecha o diálogo sinalizando sucesso
            this.DialogResult = true;
            this.Close();
        }
        /// <summary>
        /// Cancela a operação.
        /// </summary>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Fecha o diálogo sem confirmar
            this.DialogResult = false;
            this.Close();
        }
    }
}
