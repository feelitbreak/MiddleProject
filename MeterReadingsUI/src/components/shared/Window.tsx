import type { ReactNode } from 'react';

type Tone = 'default' | 'filter' | 'warning' | 'error';

interface WindowProps {
  title: string;
  tone?: Tone;
  /** The query this window runs, shown under the title bar so each panel names its own source. */
  query?: string;
  className?: string;
  children: ReactNode;
}

const TONE_CLASS: Record<Tone, string> = {
  default: '',
  filter: ' window-titlebar-filter',
  warning: ' window-titlebar-warning',
  error: ' window-titlebar-error',
};

export default function Window({
  title,
  tone = 'default',
  query,
  className,
  children,
}: WindowProps) {
  return (
    <section className={className ? `window ${className}` : 'window'}>
      <div className={`window-titlebar${TONE_CLASS[tone]}`}>
        <span className="window-button" aria-hidden="true" />
        <span className="window-title">{title}</span>
        <span className="titlebar-lines" aria-hidden="true" />
      </div>
      <div className="window-body">
        {query !== undefined && (
          <div className="query-line" title={query}>
            {query}
          </div>
        )}
        {children}
      </div>
    </section>
  );
}
