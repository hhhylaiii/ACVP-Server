# 專案文件區（project-docs）

本目錄收錄本產學合作專案（FIPS 203/204 ACVP 驗證網頁工具）自行新增的所有文件，
與上游 NIST ACVP-Server 原始碼（`gen-val/`、`docs/`、`_config/`）分開存放。

## 目錄結構

| 資料夾 | 內容 |
|--------|------|
| [`planning/`](planning/) | 計劃與設計文件 |
| [`reports/`](reports/) | 技術分析報告 |
| [`reference/`](reference/) | 演算法整理與 NIST 規格草稿 |
| [`gantt/`](gantt/) | 甘特圖與工作計劃 |

## 文件清單

### planning/ — 計劃與設計
- [PROJECT_PLAN.md](planning/PROJECT_PLAN.md) — 產學合作正式計劃（架構、6 個 Sprint、驗收標準、風險清單）
- [implementation_plan.md](planning/implementation_plan.md) — FIPS 203/204 CLI demo 流程實作計劃
- [walkthrough.md](planning/walkthrough.md) — demo 建立成果與驗證結果

### reports/ — 技術報告
- [SEED_REPORT.md](reports/SEED_REPORT.md) — seed 從哪來、如何展開成金鑰的詳細呼叫鏈
- [SEED_COMPLIANCE.md](reports/SEED_COMPLIANCE.md) — seed 生成是否符合 NIST 標準的合規性分析

### reference/ — 參考資料
- [FIPS-203-204-algo.md](reference/FIPS-203-204-algo.md) — 兩個 FIPS 標準全部演算法的虛擬碼整理
- pages.nist.gov_ACVP_draft-celi-acvp-ml-dsa.txt.pdf — ML-DSA ACVP 協定規格草稿
- pages.nist.gov_ACVP_draft-celi-acvp-ml-kem.txt.pdf — ML-KEM ACVP 協定規格草稿

### gantt/ — 進度
- WORK_PLAN.txt — 工作計劃
- gantt.png — 甘特圖

## 相關目錄（不在本資料夾）

- [`../fips-203-204-demo/`](../fips-203-204-demo/) — CLI demo 工作目錄（registration、IUT 腳本、執行說明）。
  腳本與 README 需在該目錄內執行，故保留於專案根目錄。
- `../gen-val/` — 上游 NIST ACVP-Server 的 Gen/Val 引擎與 Orleans 原始碼。
</content>
