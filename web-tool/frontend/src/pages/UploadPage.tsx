import { useState } from 'react';
import { ApiError, pollJobUntilDone, startValidate } from '../api/client';
import type { Job, SafeError } from '../api/types';

export interface UploadPageProps {
  generateJob: Job;
  onValidated: (job: Job) => void;
  onBack: () => void;
}

/** US2 — upload the IUT-produced responses.json and poll the validate job. */
export function UploadPage({ generateJob, onValidated, onBack }: UploadPageProps) {
  const [file, setFile] = useState<File | null>(null);
  const [working, setWorking] = useState(false);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<SafeError | null>(null);

  const upload = async () => {
    if (!file) {
      return;
    }
    setWorking(true);
    setError(null);
    setStatus('上傳中…');
    try {
      const accepted = await startValidate(generateJob.jobId, file);
      setStatus('批改中…');
      const finished = await pollJobUntilDone(accepted.jobId, (job) =>
        setStatus(`批改中…（${job.status}）`),
      );
      if (finished.status === 'Succeeded') {
        onValidated(finished);
      } else {
        setError(
          finished.error ?? { code: 'UNEXPECTED_ERROR', message: '批改失敗，請稍後重試。' },
        );
        setStatus(null);
      }
    } catch (e) {
      if (e instanceof ApiError && e.safeError) {
        setError(e.safeError);
      } else {
        setError({ code: 'NETWORK', message: '無法連線到伺服器，請確認服務是否啟動。' });
      }
      setStatus(null);
    } finally {
      setWorking(false);
    }
  };

  return (
    <section className="card">
      <h2>上傳作答</h2>
      <p className="hint">
        請上傳貴公司模組針對 prompt.json 產生的 responses.json。 若只是想看流程，也可以直接上傳測試向量包內的
        example-responses.json（示範用，必定全數通過）。
      </p>

      <div className="field">
        <label className="field-label" htmlFor="responses-file">
          responses.json 檔案
        </label>
        <input
          id="responses-file"
          type="file"
          accept=".json,application/json"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          disabled={working}
        />
      </div>

      {error && (
        <div className="error-box" role="alert">
          <strong>{error.code}</strong>：{error.message}
          {error.tcId != null && <div>測試案例 tcId：{error.tcId}</div>}
          {error.field && <div>欄位：{error.field}</div>}
          {error.hint && <div>建議：{error.hint}</div>}
        </div>
      )}

      {status && (
        <p>
          <span className="spinner" aria-hidden />
          {status}
        </p>
      )}

      <div className="actions">
        <button type="button" className="btn btn-secondary" onClick={onBack} disabled={working}>
          上一步
        </button>
        <button
          type="button"
          className="btn btn-primary"
          onClick={upload}
          disabled={!file || working}
        >
          上傳並批改
        </button>
      </div>
    </section>
  );
}
