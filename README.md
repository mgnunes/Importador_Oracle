📌 Resumo da Aplicação: Importador de Arquivos para Oracle (WPF)
Esta aplicação WPF (Windows Presentation Foundation) permite que o usuário importe múltiplos arquivos de texto para um banco de dados Oracle. 
Durante a importação, o progresso é exibido em tempo real, tanto em uma barra de progresso quanto em um gauge gráfico (medidor circular de progresso), 
utilizando a biblioteca LiveCharts.Wpf.

🚀 Principais Funcionalidades
✅ Seleção de Arquivos → Permite escolher um ou mais arquivos .txt para importação.
✅ Validação de Data → O usuário informa uma data no formato MM/YYYY para inclusão no banco.
✅ Importação Paralelizada → Usa Parallel.ForEach para processar múltiplas linhas simultaneamente.
✅ Banco de Dados Oracle → Insere os dados no Oracle Database via Oracle.ManagedDataAccess.Client.
✅ Registro de Logs → Exibe logs da importação em tempo real.
✅ Gauge de Progresso → Mostra graficamente o progresso da importação (via LiveCharts.Wpf).
✅ Botões de Controle → Inclui botões para "Limpar Log" e "Fechar Aplicação".
✅ Configuração Externa → A connection string é armazenada no arquivo appsettings.json.

🔧 Tecnologias Utilizadas
C# (WPF - .NET 8.0) → Interface gráfica do usuário (UI).
LiveCharts.Wpf → Exibição do gauge para progresso visual.
Oracle.ManagedDataAccess.Client → Conexão com o banco de dados Oracle.
MVVM Simplificado → Utiliza INotifyPropertyChanged para atualização dinâmica da UI.
Parallel Programming → Usa Parallel.ForEach para processar grandes volumes de dados mais rapidamente.
🛠 Como Utilizar?
1️⃣ Executar o programa.
2️⃣ Selecionar arquivos de texto contendo dados delimitados por vírgula.
3️⃣ Informar a data (MM/YYYY).
4️⃣ Iniciar a importação → O progresso será exibido no gauge e na barra de progresso.
5️⃣ Monitorar logs em tempo real.
6️⃣ Finalizar ou limpar logs com os botões.

Com essa aplicação, a importação de arquivos para o Oracle se torna rápida, eficiente e visualmente intuitiva! 🚀
