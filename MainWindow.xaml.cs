using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Tool_SqlPerformanceAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string configFilePath = "connection_config.txt";

        // SQL 查詢語法
        private readonly string query1 = @"
-- 查詢 IO 耗用最大的 Stored Procedure  
SELECT TOP 15
    OBJECT_SCHEMA_NAME(st.objectid) AS '結構描述',
    OBJECT_NAME(st.objectid) AS 'SP名稱',
    qs.execution_count AS '執行次數',
    qs.total_logical_reads AS '總邏輯讀取次數',
    qs.total_physical_reads AS '總實體讀取次數',
    qs.total_logical_writes AS '總邏輯寫入次數',
    -- 計算平均IO
    CASE WHEN qs.execution_count > 0 
         THEN qs.total_logical_reads / qs.execution_count 
         ELSE 0 END AS '平均邏輯讀取次數',
    CASE WHEN qs.execution_count > 0 
         THEN qs.total_physical_reads / qs.execution_count 
         ELSE 0 END AS '平均實體讀取次數',
    CASE WHEN qs.execution_count > 0 
         THEN qs.total_logical_writes / qs.execution_count 
         ELSE 0 END AS '平均邏輯寫入次數',
    qs.total_elapsed_time/1000 AS '總執行時間毫秒',
    qs.creation_time AS '計畫建立時間',
    qs.last_execution_time AS '最後執行時間'
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE st.objectid IS NOT NULL                           -- 只要SP、Function等
  AND OBJECT_NAME(st.objectid) IS NOT NULL              -- 確保物件名稱存在
  AND qs.execution_count > 0                            -- 確保有執行過
ORDER BY qs.total_logical_reads DESC;                   -- 依總邏輯讀取排序";

        private readonly string query2 = @"
-- 需要 VIEW SERVER STATE 權限
SELECT TOP 50
    DB_NAME(pa.database_id)                                   AS [資料庫名稱],
    qs.execution_count                                        AS [執行次數],
    CAST(qs.execution_count * 1.0 /
         NULLIF(DATEDIFF(SECOND, qs.creation_time, GETDATE()), 0) AS DECIMAL(18,2))
                                                             AS [平均每秒執行次數],
    CAST(qs.total_elapsed_time * 1.0 / qs.execution_count / 1000 AS DECIMAL(18,2))
                                                             AS [平均執行時間_毫秒],
    CAST(qs.total_worker_time  * 1.0 / qs.execution_count / 1000 AS DECIMAL(18,2))
                                                             AS [平均CPU時間_毫秒],
    CAST(qs.total_logical_reads  * 1.0 / qs.execution_count       AS DECIMAL(18,2))
                                                             AS [平均讀取頁數],
    CAST(qs.total_logical_writes * 1.0 / qs.execution_count       AS DECIMAL(18,2))
                                                             AS [平均寫入頁數],
    SUBSTRING(qt.text,
              (qs.statement_start_offset/2)+1,
              ((CASE qs.statement_end_offset
                    WHEN -1 THEN DATALENGTH(qt.text)
                    ELSE qs.statement_end_offset END
                - qs.statement_start_offset)/2)+1)            AS [SQL語法],
    qs.creation_time                                          AS [快取建立時間],
    qs.last_execution_time                                    AS [最後執行時間]
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
OUTER APPLY (
    SELECT TOP 1 CONVERT(INT, value) AS database_id
    FROM sys.dm_exec_plan_attributes(qs.plan_handle)
    WHERE attribute = 'dbid'
) pa
ORDER BY [平均每秒執行次數] DESC;";

        private readonly string query3 = @"
-- 需要 VIEW SERVER STATE 權限
SELECT TOP 10
    DB_NAME(pa.database_id)                                   AS [資料庫名稱],
    qs.execution_count                                        AS [執行次數],
    CAST(qs.execution_count * 1.0 /
         NULLIF(DATEDIFF(SECOND, qs.creation_time, GETDATE()), 0) AS DECIMAL(18,2))
                                                             AS [平均每秒執行次數],
    CAST(qs.total_elapsed_time * 1.0 / qs.execution_count / 1000 AS DECIMAL(18,2))
                                                             AS [平均執行時間_毫秒],
    CAST(qs.total_worker_time  * 1.0 / qs.execution_count / 1000 AS DECIMAL(18,2))
                                                             AS [平均CPU時間_毫秒],
    CAST(qs.total_logical_reads  * 1.0 / qs.execution_count       AS DECIMAL(18,2))
                                                             AS [平均讀取頁數],
    CAST(qs.total_logical_writes * 1.0 / qs.execution_count       AS DECIMAL(18,2))
                                                             AS [平均寫入頁數],
    SUBSTRING(qt.text,
              (qs.statement_start_offset/2)+1,
              ((CASE qs.statement_end_offset
                    WHEN -1 THEN DATALENGTH(qt.text)
                    ELSE qs.statement_end_offset END
                - qs.statement_start_offset)/2)+1)            AS [SQL語法],
    qs.creation_time                                          AS [快取建立時間],
    qs.last_execution_time                                    AS [最後執行時間]
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
OUTER APPLY (
    SELECT TOP 1 CONVERT(INT, value) AS database_id
    FROM sys.dm_exec_plan_attributes(qs.plan_handle)
    WHERE attribute = 'dbid'
) pa
ORDER BY [執行次數] DESC;";

        public MainWindow()
        {
            InitializeComponent();
            LoadConnectionConfig();
        }

        private string GetConnectionString()
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = cmbServer.Text,
                InitialCatalog = cmbDatabase.Text,
                ConnectTimeout = 30,
                TrustServerCertificate = true,
                Encrypt = false
            };

            if (rbWindows.IsChecked == true)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = txtUsername.Text;
                builder.Password = txtPassword.Password;
            }

            return builder.ConnectionString;
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(cmbServer.Text) || string.IsNullOrWhiteSpace(cmbDatabase.Text))
            {
                MessageBox.Show("請輸入伺服器名稱和資料庫名稱", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (rbSql.IsChecked == true && (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Password)))
            {
                MessageBox.Show("使用 SQL Server 驗證時，請輸入使用者名稱和密碼", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                btnTest.IsEnabled = false;
                txtStatus.Text = "正在測試連線...";

                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    await connection.OpenAsync();
                    txtStatus.Text = "連線測試成功！";
                    MessageBox.Show("連線測試成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"連線失敗: {ex.Message}";
                MessageBox.Show($"連線失敗:\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnTest.IsEnabled = true;
            }
        }

        private async void btnQuery1_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuery(query1, "IO 耗用最大的 Stored Procedure");
        }

        private async void btnQuery2_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuery(query2, "平均每秒執行次數");
        }

        private async void btnQuery3_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuery(query3, "執行次數排序");
        }

        private async System.Threading.Tasks.Task ExecuteQuery(string query, string queryName)
        {
            if (string.IsNullOrWhiteSpace(cmbServer.Text) || string.IsNullOrWhiteSpace(cmbDatabase.Text))
            {
                MessageBox.Show("請先設定資料庫連線資訊", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetButtonsEnabled(false);
                txtStatus.Text = $"正在執行查詢: {queryName}...";
                dgResults.ItemsSource = null;

                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 60;

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            var dataTable = new DataTable();
                            await System.Threading.Tasks.Task.Run(() => adapter.Fill(dataTable));

                            dgResults.ItemsSource = dataTable.DefaultView;
                            txtStatus.Text = $"查詢完成: {queryName} - 共 {dataTable.Rows.Count} 筆記錄";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"查詢失敗: {ex.Message}";
                MessageBox.Show($"查詢執行失敗:\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            btnQuery1.IsEnabled = enabled;
            btnQuery2.IsEnabled = enabled;
            btnQuery3.IsEnabled = enabled;
            btnTest.IsEnabled = enabled;
        }

        // 伺服器選擇改變事件
        private async void cmbServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbServer.SelectedItem != null && !string.IsNullOrWhiteSpace(cmbServer.Text))
            {
                await LoadDatabaseList();
            }
        }

        // 重新整理資料庫清單按鈕事件
        private async void btnRefreshDB_Click(object sender, RoutedEventArgs e)
        {
            await LoadDatabaseList();
        }

        // 載入資料庫清單
        private async System.Threading.Tasks.Task LoadDatabaseList()
        {
            if (string.IsNullOrWhiteSpace(cmbServer.Text))
            {
                return;
            }

            try
            {
                btnRefreshDB.IsEnabled = false;
                txtStatus.Text = "正在載入資料庫清單...";
                cmbDatabase.Items.Clear();

                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = cmbServer.Text,
                    InitialCatalog = "master", // 連接到 master 資料庫查詢清單
                    ConnectTimeout = 30,
                    TrustServerCertificate = true,
                    Encrypt = false
                };

                if (rbWindows.IsChecked == true)
                {
                    builder.IntegratedSecurity = true;
                }
                else if (!string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    builder.UserID = txtUsername.Text;
                    builder.Password = txtPassword.Password;
                }
                else
                {
                    txtStatus.Text = "請先設定驗證資訊";
                    return;
                }

                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync();

                    var query = "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name"; // 排除系統資料庫
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                cmbDatabase.Items.Add(reader["name"].ToString());
                            }
                        }
                    }
                }

                txtStatus.Text = $"載入完成，共 {cmbDatabase.Items.Count} 個資料庫";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"載入資料庫清單失敗: {ex.Message}";
                MessageBox.Show($"無法載入資料庫清單:\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                btnRefreshDB.IsEnabled = true;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = $"{cmbServer.Text}\n{cmbDatabase.Text}\n{rbWindows.IsChecked}";

                // 只有在使用 SQL Server 驗證時才儲存帳號
                if (rbSql.IsChecked == true)
                {
                    config += $"\n{txtUsername.Text}";
                }

                File.WriteAllText(configFilePath, config);
                MessageBox.Show("連線設定已儲存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"儲存設定失敗:\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            LoadConnectionConfig();
        }

        private void LoadConnectionConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    var lines = File.ReadAllLines(configFilePath);
                    if (lines.Length >= 3)
                    {
                        cmbServer.Text = lines[0];
                        cmbDatabase.Text = lines[1];
                        rbWindows.IsChecked = bool.Parse(lines[2]);
                        rbSql.IsChecked = !bool.Parse(lines[2]);

                        // 如果有第4行，就載入使用者名稱，沒有就設為空字串
                        if (lines.Length >= 4)
                        {
                            txtUsername.Text = lines[3];
                        }
                        else
                        {
                            txtUsername.Text = "";
                        }

                        txtStatus.Text = "已載入連線設定";
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"載入設定失敗: {ex.Message}";
            }
        }
    }
}