import { useEffect, useState } from 'react';
import { getReport, validationJsonUrl } from '../api/client';
import type { Job, ValidationReport } from '../api/types';
import { IconAlert, IconCheck } from '../components/icons';

export interface ReportPageProps {
  validateJob: Job;
  onRestart: () => void;
  onUploadAgain: () => void;
}

/** US2 — readable pass/fail report with failing cases highlighted + validation.json download. */
export function ReportPage({ validateJob, onRestart, onUploadAgain }: ReportPageProps) {
  const [report, setReport] = useState<ValidationReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [failedOnly, setFailedOnly] = useState(false);

  useEffect(() => {
    getReport(validateJob.jobId)
      .then(setReport)
      .catch((e: Error) => setError(e.message));
  }, [validateJob.jobId]);

  if (error) {
    return (
      <section className="card">
        <header className="card-header">
          <span className="overline">Step 4</span>
          <h2>Validation Report</h2>
        </header>
        <div className="error-box">{error}</div>
      </section>
    );
  }

  if (!report) {
    return (
      <section className="card">
        <header className="card-header">
          <span className="overline">Step 4</span>
          <h2>Validation Report</h2>
        </header>
        <p className="loading-line">
          <span className="spinner" aria-hidden />
          Loading report…
        </p>
      </section>
    );
  }

  const passed = report.disposition === 'passed';
  const { total, passed: passedCount, failed: failedCount } = report.summary;
  const passRate = total > 0 ? Math.round((passedCount / total) * 100) : 0;
  const visibleCases = failedOnly ? report.cases.filter((c) => !c.passed) : report.cases;

  return (
    <section className="card">
      <header className="card-header">
        <span className="overline">Step 4</span>
        <h2>Validation Report</h2>
      </header>

      <div className={`verdict ${passed ? 'verdict-pass' : 'verdict-fail'}`}>
        <span className="verdict-icon">{passed ? <IconCheck /> : <IconAlert />}</span>
        <div className="verdict-text">
          <span className="verdict-title">
            {passed ? 'Validation Passed' : 'Validation Failed'}
          </span>
          <span className="verdict-sub">
            Overall disposition:
            <span className={`badge ${passed ? 'badge-passed' : 'badge-failed'}`}>
              {report.disposition}
            </span>
          </span>
        </div>
        <div className="verdict-rate">
          <span className="rate-number">{passRate}%</span>
          <span className="rate-label">pass rate</span>
        </div>
      </div>

      <div className="pass-bar" role="presentation">
        <div className="pass-bar-fill" style={{ width: `${passRate}%` }} />
      </div>

      <div className="summary-tiles">
        <div className="tile">
          <div className="tile-number">{total}</div>
          <div className="tile-label">Total Test Cases</div>
        </div>
        <div className="tile tile-passed">
          <div className="tile-number">{passedCount}</div>
          <div className="tile-label">Passed</div>
        </div>
        <div className="tile tile-failed">
          <div className="tile-number">{failedCount}</div>
          <div className="tile-label">Failed</div>
        </div>
      </div>

      <div className="table-toolbar">
        <span className="hint">
          {failedOnly
            ? `Showing ${visibleCases.length} failed case${visibleCases.length === 1 ? '' : 's'} of ${total} total`
            : `${total} test case${total === 1 ? '' : 's'} in total`}
          {failedCount > 0 && !failedOnly && '; failed cases are highlighted below'}
        </span>
        {failedCount > 0 && (
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() => setFailedOnly((prev) => !prev)}
          >
            {failedOnly ? 'Show all cases' : 'Show failed only'}
          </button>
        )}
      </div>

      <div className="table-scroll">
        <table className="report-table">
          <thead>
            <tr>
              <th>tcId</th>
              <th>Result</th>
              <th>Reason</th>
            </tr>
          </thead>
          <tbody>
            {visibleCases.map((c) => (
              <tr key={c.tcId} className={c.passed ? '' : 'case-failed'}>
                <td className="mono">{c.tcId}</td>
                <td>
                  <span className={`case-pill ${c.passed ? 'case-pill-pass' : 'case-pill-fail'}`}>
                    {c.passed ? '✓ Pass' : '✗ Fail'}
                  </span>
                </td>
                <td>{c.reason ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="actions">
        <a className="btn btn-primary" href={validationJsonUrl(validateJob.jobId)} download>
          Download validation.json
        </a>
        <button type="button" className="btn btn-secondary" onClick={onUploadAgain}>
          Upload Again
        </button>
        <button type="button" className="btn btn-secondary" onClick={onRestart}>
          Start Over
        </button>
      </div>
    </section>
  );
}
