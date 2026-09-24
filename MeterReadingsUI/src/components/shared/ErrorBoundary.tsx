import { Component, type ErrorInfo, type ReactNode } from 'react';
import { WarningIcon } from './StatePanels';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  message: string | null;
}

/** A class because React offers no hook for catching a render-time throw. */
export default class ErrorBoundary extends Component<
  Readonly<ErrorBoundaryProps>,
  ErrorBoundaryState
> {
  constructor(props: Readonly<ErrorBoundaryProps>) {
    super(props);
    this.state = { message: null };
  }

  static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
    return { message: error instanceof Error ? error.message : String(error) };
  }

  override componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Render failed', error, info.componentStack);
  }

  override render(): ReactNode {
    const { message } = this.state;

    // Wrapped so both branches return an element, which sonarjs/function-return-type wants.
    if (message === null) {
      return <>{this.props.children}</>;
    }

    return (
      <main className="console">
        <div className="empty-state">
          <WarningIcon />
          <span className="empty-title empty-title-error">SCREEN FAILED</span>
          <span className="empty-detail">
            The page stopped rendering. Reloading fetches the application again, which also recovers
            a screen whose code failed to download.
          </span>
          <button type="button" className="button" onClick={() => window.location.reload()}>
            RELOAD
          </button>
        </div>
        <div className="message message-error">
          <div className="message-heading">&#9632; render error</div>
          <div className="message-body">{message}</div>
        </div>
      </main>
    );
  }
}
