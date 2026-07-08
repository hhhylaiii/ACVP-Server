import type { Job } from '../api/types';

export interface UploadPageProps {
  generateJob: Job;
  onValidated: (job: Job) => void;
  onBack: () => void;
}

// Implemented in User Story 2 (responses.json upload + validate job polling).
export function UploadPage(_props: UploadPageProps) {
  return (
    <section className="card">
      <h2>上傳作答</h2>
      <p>此頁面將於 User Story 2 實作。</p>
    </section>
  );
}
