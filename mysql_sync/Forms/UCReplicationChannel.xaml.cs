using System;
using System.Windows;
using System.Windows.Controls;
using mysql_sync.Class;
using Mysqlx;

namespace mysql_sync.Forms
{
    /// <summary>
    /// Interação lógica para UCReplicationChannel.xaml
    /// </summary>
    public partial class UCReplicationChannel : UserControl
    {
        public UCReplicationChannel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Canal de replicação a ser exibido no controle.
        /// </summary>
        public ReplicationChannel Channel
        {
            get => (ReplicationChannel)GetValue(ChannelProperty);
            set => SetValue(ChannelProperty, value);
        }

        /// <summary>
        /// DependencyProperty para Channel, permitindo binding no XAML.
        /// </summary>
        public static readonly System.Windows.DependencyProperty ChannelProperty =
            System.Windows.DependencyProperty.Register(
                nameof(Channel),
                typeof(ReplicationChannel),
                typeof(UCReplicationChannel),
                new System.Windows.PropertyMetadata(null, OnChannelChanged));

        public static readonly DependencyProperty ConnectionProperty =
            DependencyProperty.Register(
              nameof(Connection),
              typeof(DatabaseConnection),
              typeof(UCReplicationChannel),
              new PropertyMetadata(null)
            );

        public DatabaseConnection Connection
        {
            get => (DatabaseConnection)GetValue(ConnectionProperty);
            set => SetValue(ConnectionProperty, value);
        }

        private static void OnChannelChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is UCReplicationChannel control)
            {
                control.DataContext = e.NewValue as ReplicationChannel;
            }
        }

        private void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;
            Connection?.Refresh();
            ((Button)sender).IsEnabled = true;
        }

        private async void Skip_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            btnSkip.IsEnabled = false;
            var channelName = Channel?.ChannelName;
            var connection = Connection;
            var channel = Channel;

            if (Channel == null || Connection == null) return;
            bool ok;
            try
            {
                if (Channel == null || Connection == null)
                    return;

                // roda o skip em background
                ok = await System.Threading.Tasks.Task.Run(() =>
                            connection.SkipError(channel)
                        );

                if (!ok)
                {
                    System.Windows.MessageBox.Show(
                        $"Falha ao ignorar erro no canal {Channel.ChannelName}",
                        "Erro",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                else
                {
                    // opcional: atualiza imediatamente após skip
                    Connection.Refresh();
                }
            }
            catch (Exception ex)
            {
                // trate exceções inesperadas aqui
                System.Windows.MessageBox.Show(
                    $"Erro inesperado: {ex.Message}",
                    "Erro",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                // sempre re-habilita o botão
                btnSkip.IsEnabled = true;
            }
        }
    }
}
