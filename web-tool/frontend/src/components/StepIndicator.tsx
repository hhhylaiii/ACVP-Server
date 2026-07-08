export type Step = 'select' | 'generate' | 'upload' | 'report';

const steps: { id: Step; label: string }[] = [
  { id: 'select', label: '1. 選擇演算法' },
  { id: 'generate', label: '2. 產生測試向量' },
  { id: 'upload', label: '3. 上傳作答' },
  { id: 'report', label: '4. 查看報告' },
];

export function StepIndicator({ current }: { current: Step }) {
  const currentIndex = steps.findIndex((s) => s.id === current);
  return (
    <nav className="steps" aria-label="進度">
      {steps.map((s, i) => (
        <span
          key={s.id}
          className={`step ${i === currentIndex ? 'step-current' : ''} ${i < currentIndex ? 'step-done' : ''}`}
          aria-current={i === currentIndex ? 'step' : undefined}
        >
          {s.label}
        </span>
      ))}
    </nav>
  );
}
