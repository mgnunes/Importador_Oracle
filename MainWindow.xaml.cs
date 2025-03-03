using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.ComponentModel;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.SqlServer.Server;
using Microsoft.Win32;
using Oracle.ManagedDataAccess.Client;
using LiveCharts;
using LiveCharts.Wpf;

namespace OracleImporter
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }
        private int _processedLines;
        private int _totalLines;
        private string _logText;
        private int _progressPercentage;
        private bool _canStart = false;
        private string _nomearquivo;
        private bool _possuicampovlrateio = false;
        private bool _possuiglosa = false;
        private bool _naoapagou = true;
        private int _totinconsist;

        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(nameof(LogText)); }
        }

        public int ProgressPercentage
        {
            get => _progressPercentage;
            set { _progressPercentage = value; OnPropertyChanged(nameof(ProgressPercentage)); }
        }
        public Func<double, string> ProgressFormatter => value => $"{value}%";

        public bool CanStart
        {
            get => _canStart;
            set { _canStart = value; OnPropertyChanged(nameof(CanStart)); }
        }

        public object ConfigurationManager { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivos de Texto|*.txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilePath = openFileDialog.FileName; // Agora a UI será atualizada automaticamente
                Log($"Arquivo selecionado: {FilePath}");
                CanStart = true;
                DateTextBox.Focus();
                _nomearquivo = Path.GetFileName(FilePath);
                DateTextBox.Text = _nomearquivo.Substring(19, 2) + "/" + _nomearquivo.Substring(21, 4);

                if (_nomearquivo.Substring(0, 18) == "RA-MEN-EVE-EMI-REG")
                {
                    _possuicampovlrateio = true;
                }
            }
            
        }

        private async void StartImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePath)) 
            {
                Log("Nenhum arquivo selecionado.");
                return;
            }

            if (DateTextBox == null || string.IsNullOrEmpty(DateTextBox.Text))
            {
                Log("Informe a data no formato MM/YYYY.");
                DateTextBox.Focus();
                return;
            }

            string dateInput = DateTextBox.Text;
            if (!ValidateDate(dateInput, out string formattedDate))
            {
                Log("Data inválida! Insira no formato MM/YYYY.");
                DateTextBox.Focus();
                return;
            }

            CanStart = false; // Bloqueia o botão durante a importação
            _possuicampovlrateio = false;
            _possuiglosa = false;
            _naoapagou = true;
            
            Log("Importação iniciada...");

            try
            {
                await ImportFileAsync(FilePath, formattedDate);
                //Log("Sincronismo concluído com sucesso!");
                
            }
            catch (Exception ex)
            {
                Log($"Erro na importação: {ex.Message}");
            }

            CanStart = true; // Habilita o botão novamente após conclusão
        }

        private string LoadConnectionString()
        {
            try
            {
                string configPath = "appsettings.json";
                if (!File.Exists(configPath))
                {
                    Log("Arquivo de configuração não encontrado!");
                    return null;
                }

                string json = File.ReadAllText(configPath);
                JsonNode config = JsonNode.Parse(json);
                return config["Database"]["ConnectionString"]?.ToString();
            }
            catch (Exception ex)
            {
                Log($"Erro ao carregar configuração: {ex.Message}");
                return null;
            }
        }

        private async Task ImportFileAsync(string filePath, string date)
        {
            string connectionString = LoadConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                Log("Erro: ConnectionString não carregada.");
                return;
            }
            if (!File.Exists(filePath))
            {
                Log("Arquivo não encontrado!");
                return;
            }

            string[] lines = File.ReadAllLines(filePath);
            _totalLines = lines.Length;
            _processedLines = 0;
            _totinconsist = 0;
            _naoapagou = true;

            Log($"Iniciando importação... Total de linhas: {_totalLines}");

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            await Task.Run(() =>
            {
                Parallel.ForEach(lines, options, line =>
                {
                    ProcessLine(line, connectionString, date);
                    UpdateProgress();
                });
            });
            if (_totinconsist > 0)
            {
                Log("Importação concluída com advertências!");
            }
            else
            {
                Log("Importação concluída com sucesso!");
            }
        }

        private void ProcessLine(string line, string connectionString, string date)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                string[] data = line.Split('#');

                
                //if (_processedLines < 5)
                //{
                //    Log($"Linha ignorada: '{line}'");
                //    return;
                //}

                _nomearquivo = Path.GetFileName(FilePath);

                if (_nomearquivo.Substring(0,18) == "RA-MEN-EVE-EMI-REG")
                {
                    _possuicampovlrateio = true;
                }
                if (_nomearquivo.Substring(0, 18) == "RA-MEN-EVE-REC-REG")
                {
                    _possuiglosa = true;
                }

                string sql = "INSERT INTO ARQUIVOS_RA (" +
                                "    ID_ARQUIVOS_RA," +
                                "    PERIODO_IMPLANTACAO," +
                                "    NATUREZA_CONTRATACAO," +
                                "    COBERTURA," +
                                "    DIA," +
                                "    NO_EVENTO," +
                                "    CONTRATO," +
                                "    NUMERO," +
                                "    DATA_AVISO," +
                                "    VENCIMENTO_TITULO,"+
                                "    CNPJ_CONTRATO," +
                                "    BENEFICIARIO_ATENDIDO," +
                                "    BENEFICIARIO_TITULAR," +
                                "    CPF_BENEFICIARIO," +
                                "    FAVORECIDO," +
                                "    DATA_EVENTO," +
                                "    INICIO_VIGENCIA," +
                                "    FIM_VIGENCIA," +
                                "    VALOR_REEMBOLSO," +
                                "    VALOR_REDE,";
                if (_possuicampovlrateio)
                {
                    sql = sql + "    VALOR_RATEIO,";
                }
                sql = sql +     "    VALOR_REVISAO," +
                                "    VALOR_TOTAL," +
                                "    TAXA_ADMIN," +
                                "    PERCENTUAL_TAXA_ACORDO," +
                                "    VALOR_TAXA_CALC," +
                                "    VALOR_TOTAL_TAXA_CALC," +
                                "    MODALIDADE," +
                                "    TIPO_CONTRATO," +
                                "    NUMERO_REGISTRO_PRODUTO," +
                                "    CPF_CNPJ_PRESTADOR," +
                                "    TIPO_PRESTADOR," +
                                "    EVENTO_REMESSA," +
                                "    EVENTO_CONTA,";
                if (_possuiglosa)
                {
                    sql = sql + "   DEVOLUCAO_REEMBOLSO," +
                                "   DEVOLUCAO_REDE," +
                                "   DEVOLUCAO_REVISAO," +
                                "   DEVOLUCAO_TOTAL,";

                }

                sql = sql + "    DATA_INCLUSAO," +
                            "    DATA_ADESAO," +
                            "    DATA_EXCLUSAO," +
                            "    ACAO_JUDICIAL," +
                            "    NUMERO_ACAO_JUDICIAL," +
                            "    TIPO_EVENTO," +
                            "    VINCULACAO_PRESTADOR," +
                            "    TIPO_EVENTO_GRUPO," +
                            "    RISCO_COMPARTILHADO," +
                            "    NUMERO_DOCUMENTO," +
                            "    TIPO_DOCUMENTO," +
                            "    MES," +
                            "    NOME_ARQUIVO" +
                            ") VALUES (" +
                            "    SEQ_ARQUIVOS_RA.NEXTVAL,                                                                            " + /*+ -- Gera o próximo valor da sequência para a chave primária" +*/
                            "    :valor1,                                                                                            " + /*+ -- PERIODO_IMPLANTACAO" +*/
                            "    :valor2,                                                                                            " + /*+ -- NATUREZA_CONTRATACAO" +*/
                            "    :valor3,                                                                                            " + /*+ -- COBERTURA" +*/
                            "    :valor4,                                                                                            " + /*+ -- DIA" +*/
                            "    :valor5,                                                                                            " + /*+ -- NO_EVENTO" +*/
                            "    :valor6,                                                                                            " + /*+ -- CONTRATO" +*/
                            "    :valor7,                                                                                            " + /*+ -- NUMERO" +*/
                            "    to_date(:valor8, 'dd/mm/rrrr'),                                                                     " + /*+ -- DATA_AVISO/DATA_RECUPERACAO" +*/
                            "    to_date(:valor9, 'dd/mm/rrrr'),                                                                     " + /*+ -- VENCIMENTO_TITULO" +*/
                            "    :valor10,                                                                                           " + /*+ -- CNPJ_CONTRATO" +*/
                            "    :valor11,                                                                                           " + /*+ -- BENEFICIARIO_ATENDIDO" +*/
                            "    :valor12,                                                                                           " + /*+ -- BENEFICIARIO_TITULAR" +*/
                            "    :valor13,                                                                                           " + /*+ -- CPF_BENEFICIARIO" +*/
                            "    :valor14,                                                                                           " + /*+ -- FAVORECIDO" +*/
                            "    to_date(:valor15, 'dd/mm/rrrr'),                                                                    " + /*+ -- DATA_EVENTO" +*/
                            "    to_date(:valor16, 'dd/mm/rrrr'),                                                                    " + /*+ -- INICIO_VIGENCIA" +*/
                            "    to_date(:valor17, 'dd/mm/rrrr'),                                                                    " + /*+ -- FIM_VIGENCIA" +*/
                            "    to_number(replace(replace(:valor18, ' ', ''), ',', '.'), '9999999999999.99'),                       " + /*+ -- VALOR_REEMBOLSO" +*/
                            "    to_number(replace(replace(:valor19, ' ', ''), ',', '.'), '9999999999999.99'),                       " ; /*+ -- VALOR_REDE" +*/
                if (_possuicampovlrateio)
                {
                    sql = sql + "    to_number(replace(replace(:valor199, ' ', ''), ',', '.'), '9999999999999.99'),                  "; /*+ -- VALOR_RATEIO" +*/
                }

                sql = sql +     "    to_number(replace(replace(:valor20, ' ', ''), ',', '.'), '9999999999999.99'),                   " + /*+ -- VALOR_REVISAO" +*/
                                "    to_number(replace(replace(:valor21, ' ', ''), ',', '.'), '9999999999999.99'),                   " + /*+ -- VALOR_TOTAL" +*/
                                "    to_number(replace(replace(:valor22, ' ', ''), ',', '.'), '9999999999999.99'),                   " + /*+ -- TAXA_ADMIN" +*/
                                "    to_number(replace(replace(:valor23, ' ', ''), ',', '.'), '9999999999999.99'),                   " + /*+ -- PERCENTUAL_TAXA_ACORDO" +*/
                                "    to_number(replace(replace(:valor24, ' ', ''), ',', '.'), '9999999999999.99'),                   " + /*+ -- VALOR_TAXA_CALC" +*/
                                "    to_number(replace(replace(:valor25, ' ', ''), ',', '.'), '9999999999999.99'),                   " + /*+ -- VALOR_TOTAL_TAXA_CALC" +*/
                                "    :valor26,                                                                                       " + /*+ -- MODALIDADE" +*/
                                "    :valor27,                                                                                       " + /*+ -- TIPO_CONTRATO" +*/
                                "    :valor28,                                                                                       " + /*+ -- NUMERO_REGISTRO_PRODUTO" +*/
                                "    :valor29,                                                                                       " + /*+ -- CPF_CNPJ_PRESTADOR" +*/
                                "    :valor30,                                                                                       " + /*+ -- TIPO_PRESTADOR" +*/
                                "    to_number(:valor31),                                                                            " + /*+ -- EVENTO_REMESSA" +*/
                                "    :valor32,                                                                                       " ; /*+ -- EVENTO_CONTA" +*/
                if (_possuiglosa)
                {
                    sql = sql + "    to_number(replace(replace(:valor321, ' ', ''), ',', '.'), '9999999999999.99'),                  " + /*+ -- DEVOLUCAO_REEMBOLSO" +*/
                                "    to_number(replace(replace(:valor322, ' ', ''), ',', '.'), '9999999999999.99'),                  " + /*+ -- DEVOLUCAO_REDE" +*/
                                "    to_number(replace(replace(:valor323, ' ', ''), ',', '.'), '9999999999999.99'),                  " + /*+ -- DEVOLUCAO_REVISAO" +*/
                                "    to_number(replace(replace(:valor324, ' ', ''), ',', '.'), '9999999999999.99'),                  " ; /*+ -- DEVOLUCAO_TOTAL" +*/
                }
                sql = sql +     "    to_date(:valor33,   'dd/mm/rrrr'),                                                              " + /*+ -- DATA_INCLUSAO" +*/
                                "    to_date(:valor34,   'dd/mm/rrrr'),                                                              " + /*+ -- DATA_ADESAO" +*/
                                "    to_date(:valor35,   'dd/mm/rrrr'),                                                              " + /*+ -- DATA_EXCLUSAO" +*/
                                "    :valor36,                                                                                       " + /*+ -- ACAO_JUDICIAL" +*/
                                "    :valor37,                                                                                       " + /*+ -- NUMERO_ACAO_JUDICIAL" +*/
                                "    :valor38,                                                                                       " + /*+ -- TIPO_EVENTO" +*/
                                "    :valor39,                                                                                       " + /*+ -- VINCULACAO_PRESTADOR" +*/
                                "    :valor40,                                                                                       " + /*+ -- TIPO_EVENTO_GRUPO" +*/
                                "    :valor41,                                                                                       " + /*+ -- RISCO_COMPARTILHADO" +*/
                                "    :valor42,                                                                                       " + /*+ -- NUMERO_DOCUMENTO" +*/
                                "    :valor43,                                                                                       " + /*+ -- TIPO_DOCUMENTO" +*/
                                "    :valor44,                                                                                       " + /*+ -- MES" +*/
                                "    :valor45                                                                                        " + /*+ -- NOME_ARQUIVO" +*/
                                ")";

                //Log($"Consulta SQL: '{sql}'");

                using (OracleConnection connection = new OracleConnection(connectionString))
                {
                    connection.Open();
                    // Deletando os dados em caso de Reimportação
                    if (_naoapagou)
                    {
                        _naoapagou = false;
                        using (OracleCommand command = new OracleCommand("Delete from ARQUIVOS_RA AR where AR.NOME_ARQUIVO =:NOME_ARQUIVO ", connection))
                        {
                            command.Parameters.Add(":NOME_ARQUIVO", OracleDbType.Varchar2).Value = FilePath;
                            command.ExecuteNonQuery();
                        }
                    }

                    // Importando 
                    using (OracleCommand command = new OracleCommand(sql, connection))
                    {   if (_possuicampovlrateio)
                        {
                            command.Parameters.Add(":valor1",  OracleDbType.Varchar2).Value = data[0];                  /* -- PERIODO_IMPLANTACAO" +*/
                            command.Parameters.Add(":valor2",  OracleDbType.Varchar2).Value = data[1];                  /* -- NATUREZA_CONTRATACAO" +*/
                            command.Parameters.Add(":valor3",  OracleDbType.Varchar2).Value = data[2];                  /* -- COBERTURA" +*/
                            command.Parameters.Add(":valor4",  OracleDbType.Varchar2).Value = data[3];                  /* -- DIA" +*/
                            command.Parameters.Add(":valor5",  OracleDbType.Varchar2).Value = data[4];                  /* -- NO_EVENTO" +*/
                            command.Parameters.Add(":valor6",  OracleDbType.Varchar2).Value = data[5];                  /* -- CONTRATO" +*/
                            command.Parameters.Add(":valor7",  OracleDbType.Varchar2).Value = data[6];                  /* -- NUMERO" +*/
                            command.Parameters.Add(":valor8",  OracleDbType.Varchar2).Value = data[7];                  /* -- DATA_AVISO" +*/
                            command.Parameters.Add(":valor9",  OracleDbType.Varchar2).Value = data[8];                  /* -- VENCIMENTO_TITULO" +*/
                            command.Parameters.Add(":valor10", OracleDbType.Varchar2).Value = data[9];                  /* -- CNPJ_CONTRATO" +*/
                            command.Parameters.Add(":valor11", OracleDbType.Varchar2).Value = data[10];                 /* -- BENEFICIARIO_ATENDIDO" +*/
                            command.Parameters.Add(":valor12", OracleDbType.Varchar2).Value = data[11];                 /* -- BENEFICIARIO_TITULAR" +*/
                            command.Parameters.Add(":valor13", OracleDbType.Varchar2).Value = data[12];                 /* -- CPF_BENEFICIARIO" +*/
                            command.Parameters.Add(":valor14", OracleDbType.Varchar2).Value = data[13];                 /* -- FAVORECIDO" +*/
                            command.Parameters.Add(":valor15", OracleDbType.Varchar2).Value = data[14];                 /* -- DATA_EVENTO" +*/
                            command.Parameters.Add(":valor16", OracleDbType.Varchar2).Value = data[15];                 /* -- INICIO_VIGENCIA" +*/
                            command.Parameters.Add(":valor17", OracleDbType.Varchar2).Value = data[16];                 /* -- FIM_VIGENCIA" +*/
                            command.Parameters.Add(":valor18", OracleDbType.Varchar2).Value = data[17];                 /* -- VALOR_REEMBOLSO" +*/
                            command.Parameters.Add(":valor19", OracleDbType.Varchar2).Value = data[18];                 /* -- VALOR_REDE" +*/
                            command.Parameters.Add(":valor199",OracleDbType.Varchar2).Value = data[19];                 /* -- VALOR_RATEIO" +*/
                            command.Parameters.Add(":valor20", OracleDbType.Varchar2).Value = data[20];                 /* -- VALOR_REVISAO" +*/
                            command.Parameters.Add(":valor21", OracleDbType.Varchar2).Value = data[21];                 /* -- VALOR_TOTAL" +*/
                            command.Parameters.Add(":valor22", OracleDbType.Varchar2).Value = data[22];                 /* -- TAXA_ADMIN" +*/
                            command.Parameters.Add(":valor23", OracleDbType.Varchar2).Value = data[23];                 /* -- PERCENTUAL_TAXA_ACORDO" +*/
                            command.Parameters.Add(":valor24", OracleDbType.Varchar2).Value = data[24];                 /* -- VALOR_TAXA_CALC" +*/
                            command.Parameters.Add(":valor25", OracleDbType.Varchar2).Value = data[25];                 /* -- VALOR_TOTAL_TAXA_CALC" +*/
                            command.Parameters.Add(":valor26", OracleDbType.Varchar2).Value = data[26];                 /* -- MODALIDADE" +*/
                            command.Parameters.Add(":valor27", OracleDbType.Varchar2).Value = data[27];                 /* -- TIPO_CONTRATO" +*/
                            command.Parameters.Add(":valor28", OracleDbType.Varchar2).Value = data[28];                 /* -- NUMERO_REGISTRO_PRODUTO" +*/
                            command.Parameters.Add(":valor29", OracleDbType.Varchar2).Value = data[29];                 /* -- CPF_CNPJ_PRESTADOR" +*/
                            command.Parameters.Add(":valor30", OracleDbType.Varchar2).Value = data[30];                 /* -- TIPO_PRESTADOR" +*/
                            command.Parameters.Add(":valor31", OracleDbType.Varchar2).Value = data[31];                 /* -- EVENTO_REMESSA" +*/
                            command.Parameters.Add(":valor32", OracleDbType.Varchar2).Value = data[32];                 /* -- EVENTO_CONTA" +*/
                            command.Parameters.Add(":valor33", OracleDbType.Varchar2).Value = data[33];                 /* -- DATA_INCLUSAO" +*/
                            command.Parameters.Add(":valor34", OracleDbType.Varchar2).Value = data[34];                 /* -- DATA_ADESAO" +*/
                            command.Parameters.Add(":valor35", OracleDbType.Varchar2).Value = data[35];                 /* -- DATA_EXCLUSAO" +*/
                            command.Parameters.Add(":valor36", OracleDbType.Varchar2).Value = data[36];                 /* -- ACAO_JUDICIAL" +*/
                            command.Parameters.Add(":valor37", OracleDbType.Varchar2).Value = data[37];                 /* -- NUMERO_ACAO_JUDICIAL" +*/
                            command.Parameters.Add(":valor38", OracleDbType.Varchar2).Value = data[38];                 /* -- TIPO_EVENTO" +*/
                            command.Parameters.Add(":valor39", OracleDbType.Varchar2).Value = data[39];                 /* -- VINCULACAO_PRESTADOR" +*/
                            command.Parameters.Add(":valor40", OracleDbType.Varchar2).Value = data[40];                 /* -- TIPO_EVENTO_GRUPO" +*/
                            command.Parameters.Add(":valor41", OracleDbType.Varchar2).Value = data[41];                 /* -- RISCO_COMPARTILHADO" +*/
                            command.Parameters.Add(":valor42", OracleDbType.Varchar2).Value = data[42];                 /* -- NUMERO_DOCUMENTO" +*/
                            command.Parameters.Add(":valor43", OracleDbType.Varchar2).Value = data[43];                 /* -- TIPO_DOCUMENTO" +*/

                        }
                    else if (_possuiglosa)
                        {
                            command.Parameters.Add(":valor1",  OracleDbType.Varchar2).Value  = data[0];                  /* -- PERIODO_IMPLANTACAO" +*/
                            command.Parameters.Add(":valor2",  OracleDbType.Varchar2).Value  = data[1];                  /* -- NATUREZA_CONTRATACAO" +*/
                            command.Parameters.Add(":valor3",  OracleDbType.Varchar2).Value  = data[2];                  /* -- COBERTURA" +*/
                            command.Parameters.Add(":valor4",  OracleDbType.Varchar2).Value  = data[3];                  /* -- DIA" +*/
                            command.Parameters.Add(":valor5",  OracleDbType.Varchar2).Value  = data[4];                  /* -- NO_EVENTO" +*/
                            command.Parameters.Add(":valor6",  OracleDbType.Varchar2).Value  = data[5];                  /* -- CONTRATO" +*/
                            command.Parameters.Add(":valor7",  OracleDbType.Varchar2).Value  = data[6];                  /* -- NUMERO" +*/
                            command.Parameters.Add(":valor9",  OracleDbType.Varchar2).Value  = data[7];                  /* -- VENCIMENTO_TITULO" +*/
                            command.Parameters.Add(":valor8",  OracleDbType.Varchar2).Value  = data[8];                  /* -- DATA_RECUPERACAO" +*/
                            command.Parameters.Add(":valor10", OracleDbType.Varchar2).Value  = data[9];                  /* -- CNPJ_CONTRATO" +*/
                            command.Parameters.Add(":valor11", OracleDbType.Varchar2).Value  = data[10];                 /* -- BENEFICIARIO_ATENDIDO" +*/
                            command.Parameters.Add(":valor12", OracleDbType.Varchar2).Value  = data[11];                 /* -- BENEFICIARIO_TITULAR" +*/
                            command.Parameters.Add(":valor13", OracleDbType.Varchar2).Value  = data[12];                 /* -- CPF_BENEFICIARIO" +*/
                            command.Parameters.Add(":valor14", OracleDbType.Varchar2).Value  = data[13];                 /* -- FAVORECIDO" +*/
                            command.Parameters.Add(":valor15", OracleDbType.Varchar2).Value  = data[14];                 /* -- DATA_EVENTO" +*/
                            command.Parameters.Add(":valor16", OracleDbType.Varchar2).Value  = data[15];                 /* -- INICIO_VIGENCIA" +*/
                            command.Parameters.Add(":valor17", OracleDbType.Varchar2).Value  = data[16];                 /* -- FIM_VIGENCIA" +*/
                            command.Parameters.Add(":valor18", OracleDbType.Varchar2).Value  = data[17];                 /* -- VALOR_REEMBOLSO" +*/
                            command.Parameters.Add(":valor19", OracleDbType.Varchar2).Value  = data[18];                 /* -- VALOR_REDE" +*/
                            command.Parameters.Add(":valor20", OracleDbType.Varchar2).Value  = data[19];                 /* -- VALOR_REVISAO" +*/
                            command.Parameters.Add(":valor21", OracleDbType.Varchar2).Value  = data[20];                 /* -- VALOR_TOTAL" +*/
                            command.Parameters.Add(":valor22", OracleDbType.Varchar2).Value  = data[21];                 /* -- TAXA_ADMIN" +*/
                            command.Parameters.Add(":valor23", OracleDbType.Varchar2).Value  = data[22];                 /* -- PERCENTUAL_TAXA_ACORDO" +*/
                            command.Parameters.Add(":valor24", OracleDbType.Varchar2).Value  = data[23];                 /* -- VALOR_TAXA_CALC" +*/
                            command.Parameters.Add(":valor25", OracleDbType.Varchar2).Value  = data[24];                 /* -- VALOR_TOTAL_TAXA_CALC" +*/
                            command.Parameters.Add(":valor26", OracleDbType.Varchar2).Value  = data[25];                 /* -- MODALIDADE" +*/
                            command.Parameters.Add(":valor27", OracleDbType.Varchar2).Value  = data[26];                 /* -- TIPO_CONTRATO" +*/
                            command.Parameters.Add(":valor28", OracleDbType.Varchar2).Value  = data[27];                 /* -- NUMERO_REGISTRO_PRODUTO" +*/
                            command.Parameters.Add(":valor29", OracleDbType.Varchar2).Value  = data[28];                 /* -- CPF_CNPJ_PRESTADOR" +*/
                            command.Parameters.Add(":valor30", OracleDbType.Varchar2).Value  = data[29];                 /* -- TIPO_PRESTADOR" +*/
                            command.Parameters.Add(":valor31", OracleDbType.Varchar2).Value  = data[30];                 /* -- EVENTO_REMESSA" +*/
                            command.Parameters.Add(":valor32", OracleDbType.Varchar2).Value  = data[31];                 /* -- EVENTO_CONTA" +*/
                            command.Parameters.Add(":valor321",OracleDbType.Varchar2).Value  = data[32];                 /* -- DEVOLUCAO_REEMBOLSO" +*/
                            command.Parameters.Add(":valor322",OracleDbType.Varchar2).Value  = data[33];                 /* -- DEVOLUCAO_REDE" +*/
                            command.Parameters.Add(":valor323",OracleDbType.Varchar2).Value  = data[34];                 /* -- DEVOLUCAO_REVISAO" +*/
                            command.Parameters.Add(":valor324",OracleDbType.Varchar2).Value  = data[35];                 /* -- DEVOLUCAO_TOTAL" +*/
                            command.Parameters.Add(":valor33", OracleDbType.Varchar2).Value  = data[36];                 /* -- DATA_INCLUSAO" +*/
                            command.Parameters.Add(":valor34", OracleDbType.Varchar2).Value  = data[37];                 /* -- DATA_ADESAO" +*/
                            command.Parameters.Add(":valor35", OracleDbType.Varchar2).Value  = data[38];                 /* -- DATA_EXCLUSAO" +*/
                            command.Parameters.Add(":valor36", OracleDbType.Varchar2).Value  = data[39];                 /* -- ACAO_JUDICIAL" +*/
                            command.Parameters.Add(":valor37", OracleDbType.Varchar2).Value  = data[40];                 /* -- NUMERO_ACAO_JUDICIAL" +*/
                            command.Parameters.Add(":valor38", OracleDbType.Varchar2).Value  = data[41];                 /* -- TIPO_EVENTO" +*/
                            command.Parameters.Add(":valor39", OracleDbType.Varchar2).Value  = data[42];                 /* -- VINCULACAO_PRESTADOR" +*/
                            command.Parameters.Add(":valor40", OracleDbType.Varchar2).Value  = data[43];                 /* -- TIPO_EVENTO_GRUPO" +*/
                            command.Parameters.Add(":valor41", OracleDbType.Varchar2).Value  = data[44];                 /* -- RISCO_COMPARTILHADO" +*/
                            command.Parameters.Add(":valor42", OracleDbType.Varchar2).Value  = data[45];                 /* -- NUMERO_DOCUMENTO" +*/
                            command.Parameters.Add(":valor43", OracleDbType.Varchar2).Value  = data[46];                 /* -- TIPO_DOCUMENTO" +*/
                        }
                        else
                        {
                            command.Parameters.Add(":valor1",  OracleDbType.Varchar2).Value = data[0];                  /* -- PERIODO_IMPLANTACAO" +*/
                            command.Parameters.Add(":valor2",  OracleDbType.Varchar2).Value = data[1];                  /* -- NATUREZA_CONTRATACAO" +*/
                            command.Parameters.Add(":valor3",  OracleDbType.Varchar2).Value = data[2];                  /* -- COBERTURA" +*/
                            command.Parameters.Add(":valor4",  OracleDbType.Varchar2).Value = data[3];                  /* -- DIA" +*/
                            command.Parameters.Add(":valor5",  OracleDbType.Varchar2).Value = data[4];                  /* -- NO_EVENTO" +*/
                            command.Parameters.Add(":valor6",  OracleDbType.Varchar2).Value = data[5];                  /* -- CONTRATO" +*/
                            command.Parameters.Add(":valor7",  OracleDbType.Varchar2).Value = data[6];                  /* -- NUMERO" +*/
                            command.Parameters.Add(":valor8",  OracleDbType.Varchar2).Value = data[7];                  /* -- DATA_AVISO" +*/
                            command.Parameters.Add(":valor9",  OracleDbType.Varchar2).Value = data[8];                  /* -- VENCIMENTO_TITULO" +*/
                            command.Parameters.Add(":valor10", OracleDbType.Varchar2).Value = data[9];                  /* -- CNPJ_CONTRATO" +*/
                            command.Parameters.Add(":valor11", OracleDbType.Varchar2).Value = data[10];                 /* -- BENEFICIARIO_ATENDIDO" +*/
                            command.Parameters.Add(":valor12", OracleDbType.Varchar2).Value = data[11];                 /* -- BENEFICIARIO_TITULAR" +*/
                            command.Parameters.Add(":valor13", OracleDbType.Varchar2).Value = data[12];                 /* -- CPF_BENEFICIARIO" +*/
                            command.Parameters.Add(":valor14", OracleDbType.Varchar2).Value = data[13];                 /* -- FAVORECIDO" +*/
                            command.Parameters.Add(":valor15", OracleDbType.Varchar2).Value = data[14];                 /* -- DATA_EVENTO" +*/
                            command.Parameters.Add(":valor16", OracleDbType.Varchar2).Value = data[15];                 /* -- INICIO_VIGENCIA" +*/
                            command.Parameters.Add(":valor17", OracleDbType.Varchar2).Value = data[16];                 /* -- FIM_VIGENCIA" +*/
                            command.Parameters.Add(":valor18", OracleDbType.Varchar2).Value = data[17];                 /* -- VALOR_REEMBOLSO" +*/
                            command.Parameters.Add(":valor19", OracleDbType.Varchar2).Value = data[18];                 /* -- VALOR_REDE" +*/
                            command.Parameters.Add(":valor20", OracleDbType.Varchar2).Value = data[19];                 /* -- VALOR_REVISAO" +*/
                            command.Parameters.Add(":valor21", OracleDbType.Varchar2).Value = data[20];                 /* -- VALOR_TOTAL" +*/
                            command.Parameters.Add(":valor22", OracleDbType.Varchar2).Value = data[21];                 /* -- TAXA_ADMIN" +*/
                            command.Parameters.Add(":valor23", OracleDbType.Varchar2).Value = data[22];                 /* -- PERCENTUAL_TAXA_ACORDO" +*/
                            command.Parameters.Add(":valor24", OracleDbType.Varchar2).Value = data[23];                 /* -- VALOR_TAXA_CALC" +*/
                            command.Parameters.Add(":valor25", OracleDbType.Varchar2).Value = data[24];                 /* -- VALOR_TOTAL_TAXA_CALC" +*/
                            command.Parameters.Add(":valor26", OracleDbType.Varchar2).Value = data[25];                 /* -- MODALIDADE" +*/
                            command.Parameters.Add(":valor27", OracleDbType.Varchar2).Value = data[26];                 /* -- TIPO_CONTRATO" +*/
                            command.Parameters.Add(":valor28", OracleDbType.Varchar2).Value = data[27];                 /* -- NUMERO_REGISTRO_PRODUTO" +*/
                            command.Parameters.Add(":valor29", OracleDbType.Varchar2).Value = data[28];                 /* -- CPF_CNPJ_PRESTADOR" +*/
                            command.Parameters.Add(":valor30", OracleDbType.Varchar2).Value = data[29];                 /* -- TIPO_PRESTADOR" +*/
                            command.Parameters.Add(":valor31", OracleDbType.Varchar2).Value = data[30];                 /* -- EVENTO_REMESSA" +*/
                            command.Parameters.Add(":valor32", OracleDbType.Varchar2).Value = data[31];                 /* -- EVENTO_CONTA" +*/
                            command.Parameters.Add(":valor33", OracleDbType.Varchar2).Value = data[32];                 /* -- DATA_INCLUSAO" +*/
                            command.Parameters.Add(":valor34", OracleDbType.Varchar2).Value = data[33];                 /* -- DATA_ADESAO" +*/
                            command.Parameters.Add(":valor35", OracleDbType.Varchar2).Value = data[34];                 /* -- DATA_EXCLUSAO" +*/
                            command.Parameters.Add(":valor36", OracleDbType.Varchar2).Value = data[35];                 /* -- ACAO_JUDICIAL" +*/
                            command.Parameters.Add(":valor37", OracleDbType.Varchar2).Value = data[36];                 /* -- NUMERO_ACAO_JUDICIAL" +*/
                            command.Parameters.Add(":valor38", OracleDbType.Varchar2).Value = data[37];                 /* -- TIPO_EVENTO" +*/
                            command.Parameters.Add(":valor39", OracleDbType.Varchar2).Value = data[38];                 /* -- VINCULACAO_PRESTADOR" +*/
                            command.Parameters.Add(":valor40", OracleDbType.Varchar2).Value = data[39];                 /* -- TIPO_EVENTO_GRUPO" +*/
                            command.Parameters.Add(":valor41", OracleDbType.Varchar2).Value = data[40];                 /* -- RISCO_COMPARTILHADO" +*/
                            command.Parameters.Add(":valor42", OracleDbType.Varchar2).Value = data[41];                 /* -- NUMERO_DOCUMENTO" +*/
                            command.Parameters.Add(":valor43", OracleDbType.Varchar2).Value = data[42];                 /* -- TIPO_DOCUMENTO" +*/
                        }


                        command.Parameters.Add(":valor44", OracleDbType.Varchar2).Value = "2";                          /* -- MES" +*/
                        command.Parameters.Add(":valor45", OracleDbType.Varchar2).Value = FilePath;                     /* -- NOME_ARQUIVO" +*/
                        command.ExecuteNonQuery();                                                         
                    }
                }

                //Log($"Linha inserida com sucesso: {line}");
            }
            catch (Exception ex)
            {
                Log($"Erro ao processar a linha '{line}': {ex.Message}");
                _totinconsist = _totinconsist + 1;

            }
        }

        private bool ValidateDate(string input, out string formattedDate)
        {
            formattedDate = "";
            if (string.IsNullOrWhiteSpace(input) || input.Length != 7 || input[2] != '/')
                return false;

            string[] parts = input.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int year))
                return false;

            if (month < 1 || month > 12)
                return false;

            formattedDate = $"{parts[0]}/{parts[1]}"; // Mantendo formato MM/YYYY
            return true;
        }
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogText = string.Empty;
            CanStart = false;
            _possuiglosa = false;
            _possuicampovlrateio = false;
            _filePath = string.Empty;
            DateTextBox.Clear();
            DateTextBox.Focus();
            OnPropertyChanged(nameof(LogText));
            FilePath = string.Empty;
          
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void UpdateProgress()
        {
            Interlocked.Increment(ref _processedLines);
            ProgressPercentage = (_processedLines * 100) / _totalLines;            
        }

        private void Log(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogText += $"{DateTime.Now:HH:mm:ss} - {message}\n";
                OnPropertyChanged(nameof(LogText)); // Atualiza a UI
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
