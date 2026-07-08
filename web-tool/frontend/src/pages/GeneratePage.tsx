import type { AlgorithmConfiguration, Job } from '../api/types';

export interface GeneratePageProps {
  configuration: AlgorithmConfiguration;
  job: Job | null;
  onJobChanged: (job: Job | null) => void;
  onBack: () => void;
  onContinue: () => void;
}

// Implemented in User Story 1 (generate job + status polling + package download).
export function GeneratePage(_props: GeneratePageProps) {
  return (
    <section className="card">
      <h2>產生測試向量</h2>
      <p>此頁面將於 User Story 1 實作。</p>
    </section>
  );
}
