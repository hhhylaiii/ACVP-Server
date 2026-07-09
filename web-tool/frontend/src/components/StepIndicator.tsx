import { IconCheck } from './icons';

export type Step = 'select' | 'generate' | 'upload' | 'report';

const steps: { id: Step; label: string; description: string }[] = [
  { id: 'select', label: 'Select Algorithm', description: 'Algorithm / mode / parameter sets' },
  { id: 'generate', label: 'Generate Vectors', description: 'Download prompt package' },
  { id: 'upload', label: 'Upload Responses', description: 'responses.json' },
  { id: 'report', label: 'View Report', description: 'Per-case results & download' },
];

export function StepIndicator({ current }: { current: Step }) {
  const currentIndex = steps.findIndex((s) => s.id === current);
  return (
    <nav className="steps" aria-label="Progress">
      <ol className="steps-list">
        {steps.map((s, i) => {
          const state = i === currentIndex ? 'current' : i < currentIndex ? 'done' : 'todo';
          return (
            <li
              key={s.id}
              className={`step step-${state}`}
              aria-current={state === 'current' ? 'step' : undefined}
            >
              <span className="step-marker">
                {state === 'done' ? <IconCheck className="step-check" /> : i + 1}
              </span>
              <span className="step-text">
                <span className="step-label">{s.label}</span>
                <span className="step-desc">{s.description}</span>
              </span>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
