import { useEffect, useState } from 'react';
import { getReport, validationJsonUrl } from '../api/client';
import type { Job, ValidationReport } from '../api/types';

export interface ReportPageProps {
  validateJob: Job;
  onRestart: () => void;
  onUploadAgain: () => void;
}

/** US2 — readable pass/fail report with failing cases highlighted + validation.json download. */
export function ReportPage({ validateJob, onRestart, onUploadAgain }: ReportPageProps) {
  const [report, setReport] = useState<ValidationReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getReport(validateJob.jobId)
      .then(setReport)
      .catch((e: Error) => setError(e.message));
  }, [validateJob.jobId]);

  if (error) {
    return (
      <section className="card">
        <h2>驗證報告</h2>
        <div className="error-box">{error}</div>
      </section>
    );
  }

  if (!report) {
    return (
      <section className="card">
        <h2>驗證報告</h2>
        <p>
          <span className="spinner" aria-hidden />
          載入報告中…
        </p>
      </section>
    );
  }

  const passed = report.disposition === 'passed';

  return (
    <section className="card">
      <h2>驗證報告</h2>

      <p>
        總體結果：
        <span className={`badge ${passed ? 'badge-passed' : 'badge-failed'}`}>
          {passed ? '通過 (passed)' : `未通過 (${report.disposition})`}
        </span>
      </p>

      <div className="summary-tiles">
        <div className="tile">
          <div className="tile-number">{report.summary.total}</div>
          <div>測試案例總數</div>
        </div>
        <div className="tile tile-passed">
          <div className="tile-number">{report.summary.passed}</div>
          <div>通過</div>
        </div>
        <div className="tile tile-failed">
          <div className="tile-number">{report.summary.failed}</div>
          <div>未通過</div>
        </div>
      </div>

      {report.summary.failed > 0 && (
        <p className="hint">未通過的案例已標示於下表，請檢查對應 tcId 的作答。</p>
      )}

      <table className="report-table">
        <thead>
          <tr>
            <th>tcId</th>
            <th>結果</th>
            <th>原因</th>
          </tr>
        </thead>
        <tbody>
          {report.cases.map((c) => (
            <tr key={c.tcId} className={c.passed ? '' : 'case-failed'}>
              <td>{c.tcId}</td>
              <td>{c.passed ? '✓ 通過' : '✗ 未通過'}</td>
              <td>{c.reason ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="actions">
        <a className="btn btn-primary" href={validationJsonUrl(validateJob.jobId)} download>
          下載 validation.json
        </a>
        <button type="button" className="btn btn-secondary" onClick={onUploadAgain}>
          重新上傳作答
        </button>
        <button type="button" className="btn btn-secondary" onClick={onRestart}>
          重新開始
        </button>
      </div>
    </section>
  );
}
