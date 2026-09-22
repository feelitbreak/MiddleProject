import type { ApolloError } from '@apollo/client';

/** The gateway always sends a code; showing it beats a generic apology nobody can act on. */
function errorCode(error: ApolloError): string {
  const code = error.graphQLErrors[0]?.extensions?.['code'];
  return typeof code === 'string' ? code : 'NETWORK_ERROR';
}

function correlationId(error: ApolloError): string | null {
  const id = error.graphQLErrors[0]?.extensions?.['correlationId'];
  return typeof id === 'string' ? id : null;
}

function WarningIcon() {
  return (
    <svg width="84" height="84" viewBox="0 0 24 24" shapeRendering="crispEdges" aria-hidden="true">
      <g fill="#FF8FA9">
        <rect x="11" y="2" width="2" height="2" />
        <rect x="9" y="4" width="6" height="2" />
        <rect x="7" y="6" width="10" height="2" />
        <rect x="5" y="8" width="14" height="2" />
        <rect x="3" y="10" width="18" height="8" />
        <rect x="3" y="18" width="18" height="2" />
      </g>
      <g fill="#B3234C">
        <rect x="11" y="11" width="2" height="5" />
        <rect x="11" y="17" width="2" height="2" />
      </g>
    </svg>
  );
}

function EmptyIcon() {
  return (
    <svg width="84" height="84" viewBox="0 0 24 24" shapeRendering="crispEdges" aria-hidden="true">
      <g fill="#B9A9F0">
        <rect x="3" y="5" width="18" height="2" />
        <rect x="3" y="7" width="2" height="12" />
        <rect x="19" y="7" width="2" height="12" />
        <rect x="3" y="17" width="18" height="2" />
      </g>
      <g fill="#2A2140">
        <rect x="7" y="11" width="2" height="2" />
        <rect x="11" y="11" width="2" height="2" />
        <rect x="15" y="11" width="2" height="2" />
      </g>
    </svg>
  );
}

export function ErrorPanel({
  error,
  onRetry,
}: Readonly<{ error: ApolloError; onRetry: () => void }>) {
  const code = errorCode(error);
  const correlation = correlationId(error);
  return (
    <>
      <div className="empty-state">
        <WarningIcon />
        <span className="empty-title empty-title-error">{code}</span>
        <span className="empty-detail">
          Nothing below is stale &mdash; it simply has not loaded. The gateway did not answer this
          query.
        </span>
        <button type="button" className="button" onClick={onRetry}>
          RETRY
        </button>
      </div>
      <div className="message message-error">
        <div className="message-heading">&#9632; extensions.code = {code}</div>
        <div className="message-body">{error.message}</div>
        {correlation !== null && <div className="message-meta">correlationId {correlation}</div>}
      </div>
    </>
  );
}

export function EmptyPanel({
  title,
  detail,
  actionLabel,
  onAction,
}: Readonly<{
  title: string;
  detail: string;
  actionLabel?: string;
  onAction?: () => void;
}>) {
  return (
    <div className="empty-state">
      <EmptyIcon />
      <span className="empty-title">{title}</span>
      <span className="empty-detail">{detail}</span>
      {actionLabel !== undefined && onAction !== undefined && (
        <button type="button" className="button" onClick={onAction}>
          {actionLabel}
        </button>
      )}
    </div>
  );
}
