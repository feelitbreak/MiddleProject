import { Component, type ErrorInfo, type ReactNode } from 'react';
import { WarningIcon } from './StatePanels';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  failed: boolean;
}

/** A class because React offers no hook for catching a render-time throw. */
export default class ErrorBoundary extends Component<
  Readonly<ErrorBoundaryProps>,
  ErrorBoundaryState
> {
  constructor(props: Readonly<ErrorBoundaryProps>) {
    super(props);
    this.state = { failed: false };
  }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { failed: true };
  }

  override componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Render failed', error, info.componentStack);
  }

  override render(): ReactNode {
    // Wrapped so both branches return an element, which sonarjs/function-return-type wants.
    if (!this.state.failed) {
      return <>{this.props.children}</>;
    }

    return (
      <main className="console">
        <div className="empty-state">
          <WarningIcon />
          <span className="empty-title empty-title-error">SOMETHING WENT WRONG</span>
          <span className="empty-detail">
            This page could not be displayed. Reloading usually fixes it.
          </span>
          <button type="button" className="button" onClick={() => window.location.reload()}>
            RELOAD
          </button>
        </div>
      </main>
    );
  }
}
