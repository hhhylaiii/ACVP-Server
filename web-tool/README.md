# FIPS 203/204 Validation Web Tool

自架的網頁工具：讓非密碼學專業的操作人員在瀏覽器中完成 FIPS 203 (ML-KEM) / FIPS 204 (ML-DSA)
的 ACVP 測試向量產生與作答批改，不需要命令列。底層完全重用本 repo 既有的 Gen/Val 引擎
（`IGenValInvoker` + Orleans Silo），**引擎的密碼學行為未修改**（本 repo 自 PR #8 起為
僅含 ML-KEM/ML-DSA 的精簡 fork）。

A self-hosted web tool that lets a non-cryptographer generate ACVP test vectors for
FIPS 203 (ML-KEM) / FIPS 204 (ML-DSA), hand them to their own module offline, upload the
responses and read a pass/fail report — reusing the repository's existing Gen/Val engine.

## 支援範圍 (Scope)

| 演算法 | 模式 | 參數集 |
|--------|------|--------|
| ML-KEM (FIPS 203) | keyGen, encapDecap | ML-KEM-512 / 768 / 1024 |
| ML-DSA (FIPS 204) | keyGen, sigGen, sigVer | ML-DSA-44 / 65 / 87 |

## 快速開始 — Docker（單一指令自架）

```bash
cd web-tool/deploy
docker compose up --build
# 開啟 http://localhost:8080
```

單一容器內同時執行未修改的 Orleans Silo 與 Web 工具；所有測試資料只存在本機
（volume `webtool-artifacts`），不對外傳輸任何資料。

> 注意：引擎要求 `MaxConcurrentWork`（預設 3）低於可用 CPU 數，請給容器至少 4 顆 CPU。

API 管理頁面（Swagger UI）：**`http://localhost:8080/swagger`** — 由
`docker-compose.yml` 中的 `WebTool__Swagger__Enabled=true` 開啟；此工具完全在本機
運行，不會外洩資料，若仍想關閉，移除該環境變數即可。

## 快速開始 — 本機開發（三個終端機）

```bash
# 終端機 1 — Orleans Silo（既有、未修改）
cd gen-val/samples/NIST.CVP.ACVTS.Orleans.ServerHost
dotnet run --console            # dashboard 在 :8081

# 終端機 2 — Web API（http://localhost:5210）
cd web-tool/backend/src/Acvp.WebTool.Api
dotnet run

# 終端機 3 — 前端（http://localhost:5173，/api 會 proxy 到 5210）
cd web-tool/frontend
npm install && npm run dev
```

### API 管理頁面（Swagger UI）

互動式 API 文件：可瀏覽全部 `/api/*` 端點（依 Capabilities / Generation /
Validation / Jobs / Reports / Health 分組），並用「Try it out」直接試打。

| 啟動方式 | 網址 | 開啟條件 |
|----------|------|----------|
| 本機開發（`dotnet run`） | `http://localhost:5210/swagger` | Development 環境固定開啟 |
| Docker 自架 | `http://localhost:8080/swagger` | compose 已預設 `WebTool__Swagger__Enabled=true` |
| 其他部署 | `<host>/swagger` | 預設關閉；設定 `WebTool:Swagger:Enabled = true`（環境變數 `WebTool__Swagger__Enabled=true`）開啟 |

> 若瀏覽器曾在加入此功能前開過 `/swagger` 而看到前端頁面，是快取所致，
> 按 ⌘+Shift+R 強制重新整理即可。

### 疑難排解：終端機 1 出現 `Failed to bind to address http://[::]:8081`

Silo 是長駐程序；若前一次啟動的 Silo 沒有真正結束，重新執行 `dotnet run --console`
時新實例的 dashboard 會因 8081 被占用而印出一大段 `fail` stack trace。
**這不會讓 Silo 退出**（核心 gateway 30000 照常運作），但殘留多個 Silo 會讓連線
狀態混亂。啟動前先清乾淨即可：

```bash
pkill -f NIST.CVP.ACVTS.Orleans.ServerHost   # 停掉所有殘留 Silo
lsof -nP -iTCP:8081 -sTCP:LISTEN             # 確認 8081 已釋放
```

另外，macOS/Linux 上啟動時印出的 `PlatformNotSupportedException`（Windows 效能
計數器）警告為上游程式碼的已知現象，可安全忽略。

## 操作流程（瀏覽器）

1. **選擇演算法**：演算法 → 模式 → 參數集（不支援的組合點不到）。
2. **產生測試向量**：背景工作 + 進度輪詢；完成後下載測試向量包
   （`prompt.json` + `example-responses.json` + `INSTRUCTIONS.md`）。
3. **作答**：把 `prompt.json` 交給工程師；使用 `integration-pack/harness_mlkem.py` 或
   `harness_mldsa.py`，只需填入呼叫貴模組的那一行即可產生 `responses.json`
   （欄位規格見 `integration-pack/field-mapping.md`）。示範時可直接上傳
   `example-responses.json`（必定全數通過）。
4. **上傳作答 → 查看報告**：逐題 pass/fail、統計、失敗原因；可下載 `validation.json`。

## 測試 (Testing)

```bash
# 後端（單元 + 整合；契約測試使用確定性假引擎，不需 Silo）
dotnet test web-tool/backend

# 前端
cd web-tool/frontend && npm test

# 黃金比對（FR-007）：需要 Orleans Silo 運行中
ACVP_WEBTOOL_GOLDEN_PARITY=1 dotnet test web-tool/backend \
  --filter "GoldenParity"
```

黃金比對測試覆蓋全部 5 種模式：正確作答判 passed、破壞單一答案判定翻轉，
並以 `GenValAppRunner` CLI 交叉核對 disposition 完全一致。

## 架構 (Architecture)

```
Browser SPA (React + Vite)
   │  /api （同源；開發時由 Vite proxy）
   ▼
ASP.NET Core 8 Minimal API  ──  JobQueue（背景工作，Queued→Running→Succeeded/Failed）
   │        │
   │        ├─ ArtifactStore（每 job 一個目錄；internalProjection/expectedResults 僅存伺服器端）
   │        └─ GenValService ── IGenValInvoker（既有 gen-val 引擎，in-process）
   ▼
Orleans Silo（NIST.CVP.ACVTS.Orleans.ServerHost，未修改）
```

## 資料隱私 (Data privacy)

- `internalProjection.json` / `expectedResults.json`（答案卷）只存在伺服器端，
  任何 API 路徑都拿不到（由測試強制驗證）。
- 工具絕不要求、儲存或傳輸模組原始碼或私鑰；交換的只有 prompt 與 response 檔。
- 無外部執行期依賴；完全在操作者自己的環境內運行。

## 錯誤代碼 (Safe error codes)

`UNSUPPORTED_SELECTION`、`INVALID_CONFIGURATION`、`MISSING_FIELD`（附 tcId/欄位/建議）、
`UNKNOWN_TCID`、`MISMATCHED_VECTORSET`、`MALFORMED_UPLOAD`、`UPLOAD_TOO_LARGE`、
`ENGINE_UNAVAILABLE`、`JOB_NOT_FOUND`、`JOB_NOT_READY`、`UNEXPECTED_ERROR`。
所有錯誤皆不含 stack trace。

## 相關文件

- 規格與設計：`specs/001-validation-web-tool/`（spec / plan / data-model / openapi / tasks）
- 整合支援包：`web-tool/integration-pack/`
- CLI 示範（黃金比對的參考流程）：`fips-203-204-demo/`
