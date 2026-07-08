import type { AlgorithmConfiguration } from '../api/types';

export interface SelectPageProps {
  onConfirmed: (config: AlgorithmConfiguration) => void;
}

// Implemented in User Story 1 (selection matrix fed by GET /api/capabilities).
export function SelectPage(_props: SelectPageProps) {
  return (
    <section className="card">
      <h2>選擇演算法</h2>
      <p>此頁面將於 User Story 1 實作。</p>
    </section>
  );
}
