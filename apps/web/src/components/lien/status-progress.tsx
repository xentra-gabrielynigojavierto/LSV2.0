'use client';

interface StatusProgressProps {
  steps: string[];
  currentStep: string;
  size?: 'sm' | 'md';
}

export function StatusProgress({ steps, currentStep, size = 'md' }: StatusProgressProps) {
  const currentIdx = steps.indexOf(currentStep);
  const dotSize = size === 'sm' ? 'w-6 h-6 text-xs' : 'w-8 h-8 text-sm';
  const lineHeight = size === 'sm' ? 'h-0.5' : 'h-1';

  return (
    <div className="flex items-center w-full">
      {steps.map((step, i) => {
        const isPast = i < currentIdx;
        const isCurrent = i === currentIdx;
        const isFuture = i > currentIdx;
        return (
          <div key={step} className="flex items-center flex-1 last:flex-none">
            <div className="flex flex-col items-center gap-1.5">
              <div className={`${dotSize} rounded-full flex items-center justify-center font-medium ${
                isPast ? 'bg-green-500 text-white' : isCurrent ? 'bg-primary text-white ring-2 ring-primary/20' : 'bg-gray-100 text-gray-400'
              }`}>
                {isPast ? <i className="ri-check-line" /> : i + 1}
              </div>
              <span className={`text-xs font-medium whitespace-nowrap ${
                isPast ? 'text-green-600' : isCurrent ? 'text-primary' : 'text-gray-400'
              }`}>{step}</span>
            </div>
            {i < steps.length - 1 && (
              <div className={`flex-1 mx-2 ${lineHeight} rounded-full ${isPast ? 'bg-green-500' : 'bg-gray-200'}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}
