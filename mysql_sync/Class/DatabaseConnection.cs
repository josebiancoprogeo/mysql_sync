using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;
using Timer = System.Timers.Timer;

namespace mysql_sync.Class
{
    /// <summary>
    /// Representa a configuração e monitoramento automático da conexão MySQL,
    /// incluindo atualização periódica de status de replicação.
    /// </summary>
    public class DatabaseConnection : IDisposable
    {
        /// <summary>Nome amigável da conexão.</summary>
        public string Name { get; set; }

        /// <summary>Nome amigável da conexão.</summary>
        public bool Master { get; set; }

        /// <summary>String de conexão MySQL.</summary>
        public string ConnectionString { get; set; }

        // Conexão única, mantida aberta
        private MySqlConnection _conn;

        public void Refresh() => SynchronizeChannels();

        public bool StopRefresh { get; set; }

        /// <summary>Lista observável de canais de replicação.</summary>
        public ObservableCollection<ReplicationChannel> Channels { get; } = new ObservableCollection<ReplicationChannel>();

        /// <summary>Lista observável de canais de replicação.</summary>
        public ObservableCollection<Database> Databases { get; } = new ObservableCollection<Database>();

        private  Timer _refreshTimer;
        private const double RefreshIntervalMs = 30000;

        public DatabaseConnection(string connectionString)
        {
            ConnectionString = connectionString;

            if (!string.IsNullOrEmpty(ConnectionString)) {
                Connect();
            }
        }

        public void Connect()
        {
            // abre a única conexão
            _conn = new MySqlConnection(ConnectionString);
            _conn.Open();

            // timer para atualização periódica
            _refreshTimer = new Timer(RefreshIntervalMs) { AutoReset = true };
            _refreshTimer.Elapsed += (s, e) => SynchronizeChannels();
            _refreshTimer.Start();
            // primeira carga
            SynchronizeChannels();
            LoadDatabasesAndTables();
        }

        /// <summary>Testa a conexão ao MySQL.</summary>
        public bool TestConnection()
        {
            try
            {
                // testa numa conexão provisória
                using var tmp = new MySqlConnection(ConnectionString);
                tmp.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SynchronizeChannels()
        {
            if (StopRefresh) return;

            try
            {
                var temp = new List<ReplicationChannel>();

                // SHOW SLAVE STATUS
                using (var cmd = new MySqlCommand("SHOW SLAVE STATUS", _conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                    {
                        var name = rdr["Channel_Name"]?.ToString() ?? "";
                        temp.Add(new ReplicationChannel
                        {
                            ChannelName = name,
                            SlaveStatus = new SlaveStatus
                            {
                                ChannelName = name,
                                SlaveIORunning = rdr["Slave_IO_Running"]?.ToString(),
                                SlaveSQLRunning = rdr["Slave_SQL_Running"]?.ToString(),
                                SecondsBehindMaster = rdr["Seconds_Behind_Master"] as long?
                            }
                        });
                    }

                // replication_applier_status_by_worker
                using (var cmd = new MySqlCommand(
                    "SELECT CHANNEL_NAME, WORKER_ID, APPLYING_TRANSACTION, LAST_ERROR_NUMBER, LAST_ERROR_MESSAGE " +
                    "FROM performance_schema.replication_applier_status_by_worker " +
                    "WHERE LAST_ERROR_MESSAGE <> ''",
                    _conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                    {
                        var ch = rdr["CHANNEL_NAME"]?.ToString() ?? "";
                        var channel = temp.FirstOrDefault(c => c.ChannelName == ch);
                        if (channel == null)
                        {
                            channel = new ReplicationChannel { ChannelName = ch };
                            temp.Add(channel);
                        }
                        channel.ApplierStatuses.Add(new ApplierStatus
                        {
                            WorkerID = rdr["WORKER_ID"] as int? ?? 0,
                            ApplyngTransaction = rdr["APPLYING_TRANSACTION"]?.ToString(),
                            LastErrorNumber = rdr["LAST_ERROR_NUMBER"] as int? ?? 0,
                            LastErrorMessage = rdr["LAST_ERROR_MESSAGE"]?.ToString()
                        });
                    }

                // Sincroniza a coleção com o resultado
                // Remove ausentes
                for (int i = Channels.Count - 1; i >= 0; i--)
                    if (!temp.Any(t => t.ChannelName == Channels[i].ChannelName))
                        Channels.RemoveAt(i);

                // Adiciona ou atualiza existentes
                foreach (var t in temp)
                {
                    var exist = Channels.FirstOrDefault(c => c.ChannelName == t.ChannelName);
                    if (exist == null)
                        Channels.Add(t);
                    else
                    {
                        exist.SlaveStatus = t.SlaveStatus;
                        exist.ApplierStatuses.Clear();
                        foreach (var ap in t.ApplierStatuses)
                            exist.ApplierStatuses.Add(ap);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao sincronizar canais: {ex.Message}");
            }
        }

        // <summary>
        /// Carrega todos os bancos e tabelas (com colunas e PKs) de forma otimizada.
        /// </summary>
        public void LoadDatabasesAndTables()
        {
            try
            {
                // 1) Puxa tudo de uma vez do information_schema
                var rows = new List<(
                    string Schema,
                    string Table,
                    string Column,
                    string DataType,
                    bool IsPk)>();

                const string sql = @"
                    SELECT 
                      TABLE_SCHEMA, 
                      TABLE_NAME, 
                      COLUMN_NAME, 
                      COLUMN_TYPE, 
                      COLUMN_KEY 
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA 
                      NOT IN ('mysql','information_schema','performance_schema','sys');";

                using (var cmd = new MySqlCommand(sql, _conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                    {
                        rows.Add((
                            rdr.GetString("TABLE_SCHEMA"),
                            rdr.GetString("TABLE_NAME"),
                            rdr.GetString("COLUMN_NAME"),
                            rdr.GetString("COLUMN_TYPE"),
                            rdr.GetString("COLUMN_KEY") == "PRI"
                        ));
                    }

                // 2) Agrupa por schema e processa cada grupo em paralelo
                var dbList = rows
                    .GroupBy(r => r.Schema)
                    .AsParallel()
                    .Select(schemaGroup =>
                    {
                        var dbInfo = new Database(schemaGroup.Key);

                        // dentro de cada schema, agrupa por tabela (sequencial—já é em memória)
                        foreach (var tableGroup in schemaGroup.GroupBy(r => r.Table))
                        {
                            var tbl = new Table(tableGroup.Key, dbInfo);

                            // adiciona colunas
                            foreach (var row in tableGroup)
                                tbl.Columns.Add(
                                    new Column(
                                      row.Column,
                                      row.DataType,
                                      row.IsPk
                                    )
                                );

                            dbInfo.Objects.Add(tbl);
                        }

                        return dbInfo;
                    })
                    .OrderBy(db => db.Name)  // opcional: ordena alfabeticamente
                    .ToList();

                // 3) Atualiza o ObservableCollection na ordem correta (UI thread)
                Databases.Clear();
                foreach (var db in dbList)
                    Databases.Add(db);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar bancos e tabelas: {ex.Message}");
            }
        }


        public bool SkipError(ReplicationChannel channel)
        {
            StopRefresh = true;
            try
            {
                if (channel?.ApplierStatuses.Count == 0)
                    return false;

                // STOP SLAVE
                ExecuteNonQuery($"STOP SLAVE FOR CHANNEL '{channel.ChannelName}';");

                // desliga binlog para não replicar o skip
                ExecuteNonQuery("SET sql_log_bin = OFF;");

                // pula o GTID exato
                ExecuteNonQuery($"SET GTID_NEXT = '{channel.ApplierStatuses[0].ApplyngTransaction}';");
                ExecuteNonQuery("BEGIN;");
                ExecuteNonQuery("COMMIT;");
                ExecuteNonQuery("SET GTID_NEXT = 'AUTOMATIC';");

                // reativa binlog
                ExecuteNonQuery("SET sql_log_bin = ON;");

                // START SLAVE
                ExecuteNonQuery($"START SLAVE FOR CHANNEL '{channel.ChannelName}';");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ignorar erro no canal {channel.ChannelName}: {ex.Message}");
                return false;
            }
            finally
            {
                StopRefresh = false;
                // sempre atualiza depois do skip
                SynchronizeChannels();
            }
        }

        // helper para executar comando sem recriar conexão
        private void ExecuteNonQuery(string sql)
        {
            using var cmd = new MySqlCommand(sql, _conn);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Executa o SQL informado usando a conexão já aberta e retorna um DataTable.
        /// </summary>
        public DataTable ExecuteQuery(string sql)
        {
            var dt = new DataTable();
            using (var cmd = new MySqlCommand(sql, _conn))
            using (var reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
            {
                dt.Load(reader);
            }
            return dt;
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();

            // fecha conexão única
            if (_conn?.State == System.Data.ConnectionState.Open)
                _conn.Close();
            _conn?.Dispose();
        }
    }




    // Classes de modelo SlaveStatus, ApplierStatus e ReplicationChannel mantêm-se inalteradas
}
