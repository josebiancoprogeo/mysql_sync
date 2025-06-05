using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using MySqlX.XDevAPI.Common;
using MySqlX.XDevAPI.Relational;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Channels;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;
using Timer = System.Timers.Timer;

namespace mysql_sync.Class
{
    /// <summary>
    /// Representa a configuração e monitoramento automático da conexão MySQL,
    /// agora abrindo e fechando a cada operação para suportar paralelismo sem conflitos.
    /// </summary>
    public class DatabaseConnection : IDisposable
    {
        /// <summary>Nome amigável da conexão.</summary>
        public string Name { get; set; }

        /// <summary>Nome amigável da conexão.</summary>
        public bool Master { get; set; }

        /// <summary>String de conexão MySQL.</summary>
        public string ConnectionString { get; set; }

        /// <summary>Flag para pausar as atualizações periódicas.</summary>
        public bool StopRefresh { get; set; }

        public void Refresh() => SynchronizeChannels();

        /// <summary>Lista observável de canais de replicação.</summary>
        public ObservableCollection<ReplicationChannel> Channels { get; } = new ObservableCollection<ReplicationChannel>();

        /// <summary>Lista observável de canais de replicação.</summary>
        public ObservableCollection<Database> Databases { get; } = new ObservableCollection<Database>();

        private Timer _refreshTimer;
        private const double RefreshIntervalMs = 30000;

        public DatabaseConnection(string connectionString)
        {
            ConnectionString = connectionString;

            if (!string.IsNullOrEmpty(ConnectionString))
            {
                StartRefreshCycle();
            }
        }

        /// <summary>
        /// Começa o ciclo de atualização periódica (timer) e faz a carga inicial.
        /// </summary>
        public void StartRefreshCycle()
        {
            // Timer para atualização periódica
            //_refreshTimer = new Timer(RefreshIntervalMs) { AutoReset = true };
            //_refreshTimer.Elapsed += (s, e) =>
            //{
            //    if (!StopRefresh)
            //    {
            //        SynchronizeChannels();
            //    }
            //};
            //_refreshTimer.Start();

            // Primeira carga (sem thread pool conflitante)
            SynchronizeChannels();
            LoadDatabasesAndTables();
        }

        /// <summary>Testa a conexão ao MySQL abrindo e fechando imediatamente.</summary>
        public bool TestConnection()
        {
            try
            {
                using var tmp = new MySqlConnection(ConnectionString);
                tmp.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sincroniza os canais de replicação: abre CONEXÃO, executa SHOW SLAVE STATUS e consulta ERRORs,
        /// depois popula ou atualiza o ObservableCollection Channels.
        /// </summary>
        public void SynchronizeChannels()
        {
            if (StopRefresh) return;

            try
            {
                var temp = new List<ReplicationChannel>();

                // 1) Abre uma conexão temporária para SHOW SLAVE STATUS
                using (var conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    using var cmd1 = new MySqlCommand("SHOW SLAVE STATUS", conn);
                    using var rdr1 = cmd1.ExecuteReader();
                    while (rdr1.Read())
                    {
                        var name = rdr1["Channel_Name"]?.ToString() ?? "";
                        temp.Add(new ReplicationChannel
                        {
                            ChannelName = name,
                            SlaveStatus = new SlaveStatus
                            {
                                ChannelName = name,
                                SlaveIORunning = rdr1["Slave_IO_Running"]?.ToString(),
                                SlaveSQLRunning = rdr1["Slave_SQL_Running"]?.ToString(),
                                SecondsBehindMaster = rdr1["Seconds_Behind_Master"] as long?
                            }
                        });
                    }
                    rdr1.Close();

                    // 2) Consulta performance_schema.replication_applier_status_by_worker
                    using var cmd2 = new MySqlCommand(
                        @"SELECT CHANNEL_NAME, WORKER_ID, APPLYING_TRANSACTION, LAST_ERROR_NUMBER, LAST_ERROR_MESSAGE
                          FROM performance_schema.replication_applier_status_by_worker
                          WHERE LAST_ERROR_MESSAGE <> ''", conn);
                    using var rdr2 = cmd2.ExecuteReader();
                    while (rdr2.Read())
                    {
                        var ch = rdr2["CHANNEL_NAME"]?.ToString() ?? "";
                        var channel = temp.FirstOrDefault(c => c.ChannelName == ch);
                        if (channel == null)
                        {
                            channel = new ReplicationChannel { ChannelName = ch };
                            temp.Add(channel);
                        }
                        channel.ApplierStatuses.Add(new ApplierStatus
                        {
                            WorkerID = rdr2["WORKER_ID"] as int? ?? 0,
                            ApplyngTransaction = rdr2["APPLYING_TRANSACTION"]?.ToString(),
                            LastErrorNumber = rdr2["LAST_ERROR_NUMBER"] as int? ?? 0,
                            LastErrorMessage = rdr2["LAST_ERROR_MESSAGE"]?.ToString()
                        });
                    }
                    rdr2.Close();
                }

                Channels.Clear();
                foreach (var item in temp.ToArray())
                {
                    Channels.Add(item);
                }
                

                //// 3) Atualiza o ObservableCollection Channels de forma fosse responsável pela UI thread
                //// Remove ausentes
                //for (int i = Channels.Count - 1; i >= 0; i--)
                //{
                //    if (!temp.Any(t => t.ChannelName == Channels[i].ChannelName))
                //        Channels.RemoveAt(i);
                //}

                //// Adiciona ou atualiza existentes
                //foreach (var t in temp)
                //{
                //    var exist = Channels.FirstOrDefault(c => c.ChannelName == t.ChannelName);
                //    if (exist == null)
                //    {
                //        Channels.Add(t);
                //    }
                //    else
                //    {
                //        exist.SlaveStatus.SlaveSQLRunning = t.SlaveStatus.SlaveSQLRunning;
                //        exist.SlaveStatus.SlaveIORunning = t.SlaveStatus.SlaveIORunning;
                //        exist.SlaveStatus.SecondsBehindMaster = t.SlaveStatus.SecondsBehindMaster;

                //        exist.ApplierStatuses.Clear();
                //        foreach (var ap in t.ApplierStatuses.ToArray())
                //            exist.ApplierStatuses.Add(ap);
                //    }
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao sincronizar canais: {ex.Message}");
            }
        }

        /// <summary>
        /// Carrega todos os bancos e tabelas (com colunas e PKs) em background,
        /// abrindo/fechando conexão a cada chamada.
        /// </summary>
        public void LoadDatabasesAndTables()
        {
            try
            {
                var rows = new List<(string Schema, string Table, string Column, string DataType, bool IsPk)>();

                const string sql = @"
                    SELECT c.TABLE_SCHEMA,
                           c.TABLE_NAME,
                           c.COLUMN_NAME,
                           c.COLUMN_TYPE,
                           c.COLUMN_KEY
                      FROM information_schema.COLUMNS AS c
                      JOIN information_schema.TABLES AS t
                        ON c.TABLE_SCHEMA = t.TABLE_SCHEMA
                       AND c.TABLE_NAME   = t.TABLE_NAME
                     WHERE c.TABLE_SCHEMA
                       NOT IN ('mysql','information_schema','performance_schema','sys')
                       AND t.TABLE_TYPE = 'BASE TABLE';";

                using (var conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    using var cmd = new MySqlCommand(sql, conn);
                    using var rdr = cmd.ExecuteReader();
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
                    rdr.Close();
                }

                // Agrupa e monta objetos Database/Table em paralelo
                var dbList = rows
                    .GroupBy(r => r.Schema)
                    .AsParallel()
                    .Select(schemaGroup =>
                    {
                        var dbInfo = new Database(schemaGroup.Key);

                        foreach (var tableGroup in schemaGroup.GroupBy(r => r.Table))
                        {
                            var tbl = new Table(tableGroup.Key, dbInfo);
                            foreach (var row in tableGroup)
                            {
                                tbl.Columns.Add(new Column(
                                    row.Column,
                                    row.DataType,
                                    row.IsPk
                                ));
                            }
                            dbInfo.Objects.Add(tbl);
                        }

                        return dbInfo;
                    })
                    .OrderBy(db => db.Name)
                    .ToList();

                // Atualiza ObservableCollection na ordem correta (UI thread)
                Databases.Clear();
                foreach (var db in dbList)
                    Databases.Add(db);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar bancos e tabelas: {ex.Message}");
            }
        }


        /// <summary>
        /// Ignora o erro no canal de replicação (skip),
        /// usando uma nova conexão em cada ExecuteNonQuery.
        /// </summary>
        public bool SkipError(ReplicationChannel channel)
        {
            var chn = Channels.SingleOrDefault(x => x.ChannelName == channel.ChannelName);
            if (chn?.ApplierStatuses.Count == 0)
                return false;

            StopRefresh = true;
            try
            {
                List<string> sqls = new List<string>();

                sqls.Add($"STOP SLAVE FOR CHANNEL '{chn.ChannelName}';");
                sqls.Add("SET sql_log_bin = OFF;");
                sqls.Add($"SET GTID_NEXT = '{chn.ApplierStatuses[0].ApplyngTransaction}';");
                sqls.Add("BEGIN;");
                sqls.Add("COMMIT;");
                sqls.Add("SET GTID_NEXT = 'AUTOMATIC';");
                sqls.Add("SET sql_log_bin = ON;");
                sqls.Add($"START SLAVE FOR CHANNEL '{chn.ChannelName}';");

                ExecuteNonQueryBatch( sqls );
                //// STOP SLAVE
                //ExecuteNonQuery($"STOP SLAVE FOR CHANNEL '{chn.ChannelName}';");

                //// Desliga o binlog para não replicar o skip
                //ExecuteNonQuery("SET sql_log_bin = OFF;");

                //// Pula o GTID exato
                //ExecuteNonQuery($"SET GTID_NEXT = '{chn.ApplierStatuses[0].ApplyngTransaction}';");
                //ExecuteNonQuery("BEGIN;");
                //ExecuteNonQuery("COMMIT;");
                //ExecuteNonQuery("SET GTID_NEXT = 'AUTOMATIC';");

                //// Reativa binlog
                //ExecuteNonQuery("SET sql_log_bin = ON;");

                //// START SLAVE
                //ExecuteNonQuery($"START SLAVE FOR CHANNEL '{chn.ChannelName}';");

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
                // Atualiza imediatamente após o skip
                SynchronizeChannels();
            }
        }

        /// <summary>
        /// Executa um comando NON-QUERY (INSERT/UPDATE/DELETE/etc),
        /// abrindo e fechando conexão a cada invocação.
        /// </summary>
        private void ExecuteNonQuery(string sql)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            conn.Close();
        }

        /// <summary>
        /// Executa uma lista de comandos SQL em sequência usando UMA conexão apenas.
        /// Se algum comando falhar, ele anota no console e continua para o próximo.
        /// </summary>
        public void ExecuteNonQueryBatch(IEnumerable<string> sqlCommands)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            foreach (var sql in sqlCommands)
            {
                try
                {
                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.ExecuteNonQuery();
                }
                catch (MySql.Data.MySqlClient.MySqlException mex)
                {
                    Console.WriteLine($"[Batch] Falha em: \"{sql}\" → {mex.Message}");
                    // continua para o próximo sql
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Batch] Erro inesperado em: \"{sql}\" → {ex.Message}");
                }
            }
            conn.Close();
        }

        /// <summary>
        /// Executa a query de forma assíncrona, abrindo e fechando uma conexão nova a cada chamada.
        /// Ideal para paralelismo com Task.WhenAll ou Parallel.ForEachAsync.
        /// </summary>
        /// <param name="sql">Comando SQL a ser executado.</param>
        /// <param name="commandTimeoutSeconds">
        /// Tempo, em segundos, que o comando pode levar antes de dar timeout (padrão 300s = 5min).
        /// </param>
        /// <param name="cancellationToken">Token para cancelamento opcional.</param>
        /// <returns>DataTable com o resultado.</returns>
        public async Task<DataTable> ExecuteQueryAsync(
            string sql,
            int commandTimeoutSeconds = 300,
            CancellationToken cancellationToken = default)
        {
            var dt = new DataTable();

            // Cada chamada “aluga” uma conexão do pool e a devolve ao fechar.
            await using var conn = new MySqlConnection(ConnectionString);
            await using var cmd = new MySqlCommand(sql, conn)
            {
                CommandTimeout = commandTimeoutSeconds  // aumenta o tempo de espera
            };

            // Abre a conexão de forma assíncrona (pode também dar Timeout na própria Open se quiser)
            await conn.OpenAsync(cancellationToken);

            // Executa o reader de forma assíncrona
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            // Carrega o resultado no DataTable (este Fill pode demorar, mas não usa cmd.CommandTimeout)
            dt.Load(reader);

            return dt;
        }

        /// <summary>Libera recursos e para o timer.</summary>
        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
        }

        /// <summary>
        /// Deleta uma linha de tabela sem replicar (usa SET sql_log_bin = OFF/ON),
        /// abrindo/fechando conexão para cada comando.
        /// </summary>
        internal void DeleteRow(string tableName, string columnName, string key)
        {
            StopRefresh = true;
            try
            {
                ExecuteNonQuery("SET sql_log_bin = OFF;");
                ExecuteNonQuery($"DELETE FROM `{tableName}` WHERE `{columnName}` = {key};");
                ExecuteNonQuery("SET sql_log_bin = ON;");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao deletar objeto {tableName}, {columnName} = {key} : {ex.Message}");
            }
            finally
            {
                StopRefresh = false;
                SynchronizeChannels();
            }
        }

        /**
         * <summary>
         * Insere uma linha em <tableName> usando somente as colunas listadas em selectedColumns,
         * pegando valores do DataRow rowData.  
         * “geometry” no Column.DataType é tratado via GeomFromWKB.
         * Internamente abre/fecha conexão para cada insert (para respeitar paralelismo).
         * </summary>
         */
        public void InsertRow(Table tab, DataRow rowData)
        {
            if (tab.Columns == null || tab.Columns.Count == 0)
                throw new ArgumentException("Não há colunas selecionadas para inserção.");

            // Monta lista de nomes de colunas e valores
            var columnNames = new List<string>();
            var valueLiterals = new List<string>();

            foreach (var col in tab.Columns)
            {
                string colName = col.Name;
                columnNames.Add($"`{colName}`");

                var dataTypeLower = col.DataType.ToLowerInvariant();
                var rawValue = rowData[colName];

                // Se for NULL no DataRow:
                if (rawValue == DBNull.Value)
                {
                    valueLiterals.Add("NULL");
                    continue;
                }

                // 1) Coluna geometry (por exemplo: "point", "linestring", "polygon", etc)
                if (dataTypeLower.Contains("geometry")
                    || dataTypeLower.Contains("point")
                    || dataTypeLower.Contains("linestring")
                    || dataTypeLower.Contains("polygon"))
                {
                    // Trata apenas arrays de bytes (WKB)
                    if (rawValue is byte[] arr)
                    {
                        // Se tem pelo menos 5 bytes, supomos que os primeiros 4 são SRID
                        byte[] wkbPure;
                        if (arr.Length > 4)
                        {
                            wkbPure = new byte[arr.Length - 4];
                            Array.Copy(arr, 4, wkbPure, 0, wkbPure.Length);
                        }
                        else
                        {
                            // WKB inválido ou sem conteúdo; insere NULL
                            valueLiterals.Add("NULL");
                            continue;
                        }

                        // Converte para hex (ex: "0x010203...")
                        string hex = BitConverter.ToString(wkbPure).Replace("-", "");
                        // Usa GeomFromWKB(hex, SRID) — aqui usamos SRID fixo 4326; ajuste se necessário
                        valueLiterals.Add($"St_SwapXY(ST_GeomFromWKB(0x{hex}, 4326))");
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Para coluna '{colName}' (geometry), esperava byte[]. Tipo recebido: {rawValue.GetType().Name}"
                        );
                    }
                }
                // 2) Coluna textual (string, datetime, etc)
                else if (rawValue is string s)
                {
                    // Escapa aspas simples seguindo regra SQL
                    string escaped = s.Replace("'", "''");
                    valueLiterals.Add($"'{escaped}'");
                }
                // 3) DataTime
                else if (rawValue is DateTime dt)
                {
                    string fmt = dt.ToString("yyyy-MM-dd HH:mm:ss");
                    valueLiterals.Add($"'{fmt}'");
                }
                // 4) Numérico (int, long, decimal, double, etc)
                else if (rawValue is IFormattable num)
                {
                    // Directamente a ToString usando InvariantCulture
                    valueLiterals.Add(num.ToString(null, System.Globalization.CultureInfo.InvariantCulture));
                }
                // 5) Booleano
                else if (rawValue is bool b)
                {
                    valueLiterals.Add(b ? "1" : "0");
                }
                // 6) Qualquer outro tipo convertido para string e entre aspas
                else
                {
                    string rawEscaped = rawValue.ToString().Replace("'", "''");
                    valueLiterals.Add($"'{rawEscaped}'");
                }
            }

            // Constroi a cláusula INSERT
            string columnsJoined = string.Join(", ", columnNames);
            string valuesJoined = string.Join(", ", valueLiterals);
            string sql = $"INSERT INTO `{tab.Name}` ({columnsJoined}) VALUES ({valuesJoined});";

            // Executa em uma nova conexão (sem envolvimento de replicação)
            StopRefresh = true;
            try
            {
                ExecuteNonQuery("SET sql_log_bin = OFF;");
                ExecuteNonQuery(sql);
                //ExecuteNonQuery("SET sql_log_bin = ON;");
            }
            catch (Exception ex)
            {

                Console.WriteLine($"[InsertRow] Erro ao inserir em {tab.Name}: {ex.Message}\nSQL: {sql}");
            }
            finally
            {
                ExecuteNonQuery("SET sql_log_bin = ON;");
                StopRefresh = false;
                SynchronizeChannels();
            }
        }

        internal async Task<DataRow> SelectRowByIDAsync(Table tab, string key)
        {
            // 1) monta SELECT dinamicamente
            var pkCol = tab.Columns.First(c => c.IsPrimaryKey).Name;
            var otherCols = tab.Columns.Where(c => !c.IsPrimaryKey).Select(c => $"`{c.Name}`");
            var selectCols = $"`{pkCol}`"
                           + (otherCols.Any() ? ", " + string.Join(", ", otherCols) : "");

            var sql = $"SELECT {selectCols} FROM `{tab.Parent.Name}`.`{tab.Name}` WHERE `{pkCol}` = '{key}' limit 1";

            // recupera os DataTables
            DataTable respDt = await ExecuteQueryAsync(sql);

            return (respDt.Rows.Count > 0) ? respDt.Rows[0] : null;
        }
    }
}
