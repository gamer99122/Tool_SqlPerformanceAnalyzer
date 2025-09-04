# SQL Performance Analyzer

一個基於 WPF 的 SQL Server 效能分析工具，用於快速查詢和分析資料庫效能相關統計資訊。

## 功能特色

### 🔍 三種效能分析查詢
- **IO 耗用最大的 Stored Procedure** - 分析存儲過程的 IO 使用情況
- **平均每秒執行次數排序** - 找出執行頻率最高的 SQL 語句
- **執行次數排序** - 統計查詢執行次數和效能指標

### 🔧 連線管理
- 支援 **Windows 驗證** 和 **SQL Server 驗證**
- 預設伺服器選項 (192.17.2.1, 192.17.2.2)
- 自動載入指定伺服器的資料庫清單
- **連線設定記憶功能** - 自動儲存和載入上次的連線設定

### 📊 結果顯示
- 美觀的 DataGrid 呈現查詢結果
- 支援欄位排序和大小調整
- 交替行背景色，提升可讀性
- 滑鼠懸停和選取效果

## 系統需求

- **.NET 8.0** 或更高版本
- **Windows 作業系統**
- 對目標 SQL Server 具有 **VIEW SERVER STATE** 權限

## 安裝與建置

### 1. 複製專案
```bash
git clone https://github.com/gamer99122/Tool_SqlPerformanceAnalyzer.git
cd Tool_SqlPerformanceAnalyzer
```

### 2. 還原套件
```bash
dotnet restore
```

### 3. 建置專案
```bash
dotnet build
```

### 4. 執行應用程式
```bash
dotnet run
```

## 使用說明

### 步驟 1: 設定資料庫連線
1. 從下拉選單選擇伺服器 (或手動輸入)
2. 選擇目標資料庫 (會自動載入可用資料庫清單)
3. 選擇驗證方式：
   - **Windows 驗證**：使用當前 Windows 帳戶
   - **SQL Server 驗證**：輸入 SQL Server 帳號密碼

### 步驟 2: 測試連線
- 點擊 **「測試連線」** 確認連線設定正確

### 步驟 3: 執行分析
選擇所需的分析類型：
- **「IO 耗用最大的 SP」** - 分析 Stored Procedure 的 IO 效能
- **「平均每秒執行次數」** - 找出高頻執行的查詢
- **「執行次數排序」** - 查看執行次數統計

### 步驟 4: 儲存設定
- 點擊 **「儲存連線設定」** 保存當前連線配置
- 下次啟動時會自動載入上次的設定

## 分析查詢說明

### 1. IO 耗用最大的 Stored Procedure
```sql
-- 查詢 IO 耗用最大的 Stored Procedure  
SELECT TOP 15
    OBJECT_SCHEMA_NAME(st.objectid) AS '結構描述',
    OBJECT_NAME(st.objectid) AS 'SP名稱',
    qs.execution_count AS '執行次數',
    qs.total_logical_reads AS '總邏輯讀取次數',
    -- ...更多欄位
```
分析存儲過程的邏輯讀取、實體讀取、寫入次數等 IO 指標。

### 2. 平均每秒執行次數
```sql
SELECT TOP 50
    DB_NAME(pa.database_id) AS [資料庫名稱],
    qs.execution_count AS [執行次數],
    CAST(qs.execution_count * 1.0 / 
         NULLIF(DATEDIFF(SECOND, qs.creation_time, GETDATE()), 0) 
         AS DECIMAL(18,2)) AS [平均每秒執行次數],
    -- ...更多統計資料
```
找出執行頻率最高的 SQL 查詢，包含執行時間、CPU 時間等指標。

### 3. 執行次數排序
與第二個查詢類似，但按總執行次數排序，限制回傳前 10 筆記錄。

## 技術架構

- **框架**: .NET 8.0 + WPF
- **資料庫**: Microsoft SQL Server
- **套件**: Microsoft.Data.SqlClient 6.1.1

## 專案結構
```
Tool_SqlPerformanceAnalyzer/
├── MainWindow.xaml          # 主視窗 UI 設計
├── MainWindow.xaml.cs       # 主視窗邏輯程式碼
├── App.xaml                 # 應用程式資源設定
├── App.xaml.cs              # 應用程式進入點
└── Tool_SqlPerformanceAnalyzer.csproj  # 專案設定檔
```

## 設定檔案

應用程式會在執行目錄下建立 `connection_config.txt` 檔案來儲存連線設定：
```
192.17.2.1          # 伺服器名稱
MyDatabase           # 資料庫名稱  
True                 # 是否使用 Windows 驗證
MyUsername           # SQL Server 驗證帳號 (Windows 驗證時為空)
```

## 權限需求

執行分析查詢需要以下權限：
- `VIEW SERVER STATE` - 查看伺服器狀態資訊
- 目標資料庫的 `CONNECT` 權限

## 注意事項

⚠️ **重要提醒**
- 此工具查詢的是 SQL Server 的動態管理檢視 (DMV)
- 查詢結果反映的是自 SQL Server 上次重啟後的累積統計
- 建議在效能分析期間避免重啟 SQL Server 服務
- 大型生產環境建議在維護時段執行分析


## 更新日誌
### v1.0.0 (2025-09-04)
- ✨ 初始版本發布
- ✅ 支援三種效能分析查詢
- ✅ 連線設定記憶功能
- ✅ 美化的 DataGrid 顯示介面
- ✅ 自動載入資料庫清單