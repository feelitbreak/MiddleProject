import type { ReactNode } from 'react';

type Tone = 'default' | 'filter' | 'warn' | 'hot';

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
  filter: ' alt',
  warn: ' warn',
  hot: ' hot',
};

export default function Window({
  title,
  tone = 'default',
  query,
  className,
  children,
}: WindowProps) {
  return (
    <section className={className ? `win ${className}` : 'win'}>
      <div className={`tb${TONE_CLASS[tone]}`}>
        <span className="box" aria-hidden="true" />
        <span className="t">{title}</span>
        <span className="lines" aria-hidden="true" />
      </div>
      <div className="wb">
        {query !== undefined && (
          <div className="q" title={query}>
            {query}
          </div>
        )}
        {children}
      </div>
    </section>
  );
}
