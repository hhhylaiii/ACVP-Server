# FIPS 203/204 演算法驗證網頁工具

> 一個**自裝式（self-hosted）網頁工具**，讓**非密碼專業**的使用者不需任何 CLI 指令，
> 即可驗證其密碼模組（IUT）的 **FIPS 203（ML-KEM）** 與 **FIPS 204（ML-DSA）** 實作是否正確。
> 底層直接複用 NIST [ACVP-Server](https://github.com/usnistgov/ACVP-Server) 的 Gen/Val 引擎與 Orleans Silo。

本 repo fork 自 NIST 官方 [`usnistgov/ACVP-Server`](https://github.com/usnistgov/ACVP-Server)，
在其之上新增本產學合作專案的網頁工具、展示與文件。引擎的密碼學行為未修改，但為了聚焦
本專案範圍，`gen-val/` 已裁剪為**只保留 ML-KEM / ML-DSA 及其 SHA/SHAKE 相依**的精簡分支
（PR #8，追蹤內容約 958 MB → 94 MB），此後以「維護自有精簡 fork」取代「上游零修改」原則。

---

## 專案目標

為合作公司建立一套可在公司內網一鍵啟動的驗證工具：公司端的密碼模組（IUT）**永遠在公司端執行**，
網頁只負責**出題、收答、批改**，不接觸其原始碼或金鑰 → 天然符合「資料不出門」需求。

### 範圍（本學期凍結）

| 標準 | 演算法 | 模式 | 參數集 |
|------|--------|------|--------|
| FIPS 203 | ML-KEM | `keyGen`、`encapDecap` | ML-KEM-512/768/1024 |
| FIPS 204 | ML-DSA | `keyGen`、`sigGen`、`sigVer` | ML-DSA-44/65/87 |

**不做**：其他演算法、雲端多租戶、帳號權限系統。

---

## 系統架構（已實作）

公司端執行 `docker compose up --build` 即啟動全套，資料完全留在內網：

```
┌──────────────────────────────────────────────────────┐
│  單一容器（Docker Compose）                             │
│  ┌────────────────┐      ┌──────────────────────────┐ │
│  │ Web (ASP.NET 8) │ ───▶ │ Orleans Silo (既有專案)   │ │
│  │  REST API       │ localhost │ 執行全部 crypto       │ │
│  │  IGenValInvoker │ clustering│ (ML-KEM / ML-DSA)    │ │
│  │  前端 SPA        │      └──────────────────────────┘ │
│  └────────────────┘                                    │
└──────────────────────────────────────────────────────┘
        ▲ 瀏覽器（公司非技術人員）http://localhost:8080
```

| 層 | 選擇 |
|----|------|
| 後端 | ASP.NET Core 8 Minimal API（複用 `gen-val` 的 Generation/Common 函式庫，in-process `IGenValInvoker`）|
| 運算 | 既有 Orleans Silo（引擎行為未修改，當作 crypto 後端）|
| 前端 | React 18 + Vite + TypeScript SPA |
| 封裝 | Docker Compose（Silo + API 同容器，localhost clustering）|

原始計劃見 [專案計劃書](project-docs/planning/PROJECT_PLAN.md)；實作規格與設計見
[`specs/001-validation-web-tool/`](specs/001-validation-web-tool/)。

---

## 目前進度（2026-07-09）

| 階段 | 狀態 |
|------|------|
| **CLI demo（5 種模式端到端跑通）** | ✅ 完成，見 [`fips-203-204-demo/`](fips-203-204-demo/) |
| 後端 API（`IGenValInvoker` 程式化呼叫）| ✅ 完成（PR #3–#4）|
| 上傳作答 + 驗證報告 | ✅ 完成（PR #5）|
| 公司整合支援包（harness / 欄位規格 / 精確錯誤）| ✅ 完成（PR #6）|
| Docker 封裝 + 交付文件 | ✅ 完成（PR #7）|
| repo 裁剪為 FIPS 203/204 精簡 fork | ✅ 完成（PR #8）|

合併後驗證：3 個 solution 建置乾淨、後端 99 + 前端 6 個測試全綠、
黃金比對（golden parity）5 種模式 7/7 通過（與 CLI oracle 交叉核對）、live E2E 走查通過。
待辦：Playwright smoke 自動化（T056）、跨容器 Orleans clustering — 見
[`specs/001-validation-web-tool/plan.md`](specs/001-validation-web-tool/plan.md) Backlog。

> ⚠️ demo 與範例的 IUT 腳本為 **Mock Harness**：它讀取 server 隨機產生的 `expectedResults.json`
> 來自動作答，用以展示完整 ACVP 生命週期（出題→作答→批改→Disposition），**不執行真實 PQC 運算**。
> 真實對接時，公司只需在 `web-tool/integration-pack/` 的 harness 中填入呼叫自家模組的那一行。

---

## 快速開始（網頁工具）

```bash
cd web-tool/deploy
docker compose up --build
# 開啟 http://localhost:8080
```

本機開發（三個終端機）與測試指令見 [`web-tool/README.md`](web-tool/README.md)。

---

## 快速開始（CLI demo）

需求：.NET 8 SDK、Python 3。

```bash
# 1. 啟動 Orleans 伺服器（保持運行）
cd ~/Project/ACVP-Server/gen-val/samples/NIST.CVP.ACVTS.Orleans.ServerHost
dotnet run --console

# 2. 另開終端機，執行 demo（以 ML-KEM keyGen 為例）
cd ~/Project/ACVP-Server/fips-203-204-demo
RUNNER=~/Project/ACVP-Server/gen-val/samples/GenValAppRunner/src

dotnet run --project "$RUNNER" -- -g registration_mlkem_keyGen.json   # 出題
python3 iut_mlkem.py prompt.json                                       # 作答
dotnet run --project "$RUNNER" -- -n internalProjection.json -b responses.json  # 批改
python3 -c "import json;print('Disposition:', json.load(open('validation.json'))['disposition'])"
```

五種模式的完整指令與「故意答錯讓它 fail」的驗證，見
[`fips-203-204-demo/README_DEMO_PQC-zh-TW.md`](fips-203-204-demo/README_DEMO_PQC-zh-TW.md)。

---

## 專案文件導覽

規格與程式碼在 repo 根目錄，其餘自行新增的文件集中於 [`project-docs/`](project-docs/)：

| 分類 | 內容 |
|------|------|
| [`specs/001-validation-web-tool/`](specs/001-validation-web-tool/) | 網頁工具規格、實作計劃、資料模型、OpenAPI 契約、任務清單（含完成狀態）|
| [`web-tool/`](web-tool/) | 網頁工具原始碼（backend / frontend / integration-pack / deploy），使用說明見其 [README](web-tool/README.md) |
| [planning/](project-docs/planning/) | 計劃書、demo 實作計劃、建置成果 |
| [reports/](project-docs/reports/) | [Seed 生成報告](project-docs/reports/SEED_REPORT.md)、[NIST 合規性分析](project-docs/reports/SEED_COMPLIANCE.md) |
| [reference/](project-docs/reference/) | FIPS 203/204 演算法整理、ACVP 規格草稿 |
| [gantt/](project-docs/gantt/) | 甘特圖與工作計劃 |

---

## 上游與授權

- `gen-val/` 原始碼來自 NIST [`usnistgov/ACVP-Server`](https://github.com/usnistgov/ACVP-Server)，
  已於 PR #8 裁剪為僅含 ML-KEM / ML-DSA 及其相依的精簡版；上游原始 README 與建置/測試說明
  請見上游 repo 與其 [Wiki](https://github.com/usnistgov/ACVP-Server/wiki)。
- 授權沿用上游 NIST 條款，見上游 repo 的[授權說明](https://github.com/usnistgov/ACVP-Server)。
- 本 fork 新增內容（`web-tool/`、`specs/`、`project-docs/`、`fips-203-204-demo/`）為本專案產學合作成果。
</content>
