import type { Job } from '../api/types';

export interface ReportPageProps {
  validateJob: Job;
  onRestart: () => void;
  onUploadAgain: () => void;
}

// Implemented in User Story 2 (pass/fail report + validation.json download).
export function ReportPage(_props: ReportPageProps) {
  return (
    <section className="card">
      <h2>驗證報告</h2>
      <p>此頁面將於 User Story 2 實作。</p>
    </section>
  );
}
