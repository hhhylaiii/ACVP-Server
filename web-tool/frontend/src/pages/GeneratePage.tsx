import { useState } from 'react';
import { ApiError, pollJobUntilDone, promptPackageUrl, startGenerate } from '../api/client';
import type { AlgorithmConfiguration, Job } from '../api/types';

export interface GeneratePageProps {
  configuration: AlgorithmConfiguration;
  job: Job | null;
  onJobChanged: (job: Job | null) => void;
  onBack: () => void;
  onContinue: () => void;
}

/** US1 — start the generate job, poll status, offer the prompt-package download. */
export function GeneratePage({
  configuration,
  job,
  onJobChanged,
  onBack,
  onContinue,
}: GeneratePageProps) {
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const generate = async () => {
    setWorking(true);
    setError(null);
    try {
      const accepted = await startGenerate(configuration);
      onJobChanged(accepted);
      const finished = await pollJobUntilDone(accepted.jobId, onJobChanged);
      if (finished.status === 'Failed') {
        setError(finished.error?.message ?? '產生失敗，請稍後重試。');
      }
    } catch (e) {
      setError(e instanceof ApiError ? e.message : '無法連線到伺服器，請確認服務是否啟動。');
      onJobChanged(null);
    } finally {
      setWorking(false);
    }
  };

  const succeeded = job?.status === 'Succeeded';

  return (
    <section className="card">
      <h2>產生測試向量</h2>
      <p>
        設定：<strong>{configuration.algorithm}</strong> / <strong>{configuration.mode}</strong>（
        {configuration.parameterSets.join('、')}）
      </p>

      {error && <div className="error-box">{error}</div>}

      {!job && (
        <div className="actions">
          <button type="button" className="btn btn-secondary" onClick={onBack} disabled={working}>
            上一步
          </button>
          <button type="button" className="btn btn-primary" onClick={generate} disabled={working}>
            {working ? '產生中…' : '產生測試向量'}
          </button>
        </div>
      )}

      {job && !succeeded && job.status !== 'Failed' && (
        <p>
          <span className="spinner" aria-hidden />
          工作進行中（{job.status}）… 大型向量集可能需要數分鐘，請稍候。
        </p>
      )}

      {job && (
        <p>
          工作狀態：
          <span
            className={`badge ${
              succeeded
                ? 'badge-succeeded'
                : job.status === 'Failed'
                  ? 'badge-failed'
                  : 'badge-running'
            }`}
          >
            {job.status}
          </span>
        </p>
      )}

      {succeeded && (
        <>
          <p className="hint">
            測試向量包內含 prompt.json（題目）、example-responses.json（作答格式範例）與說明文件。
          </p>
          <div className="actions">
            <a className="btn btn-primary" href={promptPackageUrl(job.jobId)} download>
              下載測試向量包
            </a>
            <button type="button" className="btn btn-primary" onClick={onContinue}>
              下一步：上傳作答
            </button>
            <button type="button" className="btn btn-secondary" onClick={onBack}>
              重新選擇
            </button>
          </div>
        </>
      )}

      {job?.status === 'Failed' && (
        <div className="actions">
          <button type="button" className="btn btn-secondary" onClick={onBack}>
            上一步
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              onJobChanged(null);
              void generate();
            }}
          >
            重試
          </button>
        </div>
      )}
    </section>
  );
}
