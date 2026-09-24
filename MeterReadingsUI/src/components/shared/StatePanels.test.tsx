import { jest } from '@jest/globals';
import { ApolloError } from '@apollo/client';
import { GraphQLError } from 'graphql';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { EmptyPanel, ErrorPanel } from './StatePanels';

function gatewayError(code: string, correlationId?: string): ApolloError {
  return new ApolloError({
    graphQLErrors: [
      new GraphQLError('A database connection could not be established.', {
        extensions: correlationId === undefined ? { code } : { code, correlationId },
      }),
    ],
  });
}

describe('ErrorPanel', () => {
  it('should show the gateway own error code rather than a generic message', () => {
    render(<ErrorPanel error={gatewayError('SERVICE_UNAVAILABLE')} onRetry={jest.fn()} />);

    expect(screen.getByText('SERVICE_UNAVAILABLE')).toBeInTheDocument();
    expect(screen.getByText(/extensions.code = SERVICE_UNAVAILABLE/)).toBeInTheDocument();
  });

  it('should show the correlation id when the gateway sends one', () => {
    render(
      <ErrorPanel error={gatewayError('INTERNAL_SERVER_ERROR', '8f2c-41d0')} onRetry={jest.fn()} />,
    );
    expect(screen.getByText(/correlationId 8f2c-41d0/)).toBeInTheDocument();
  });

  it('should omit the correlation line when there is none', () => {
    render(<ErrorPanel error={gatewayError('BAD_USER_INPUT')} onRetry={jest.fn()} />);
    expect(screen.queryByText(/correlationId/)).not.toBeInTheDocument();
  });

  it('should show the HTTP status when the gateway refused the request outright', () => {
    const refused = Object.assign(new Error('Received status code 429'), { statusCode: 429 });

    render(<ErrorPanel error={new ApolloError({ networkError: refused })} onRetry={jest.fn()} />);

    expect(screen.getByText('HTTP_429')).toBeInTheDocument();
  });

  /** A transport failure carries no GraphQL error, so the code has to fall back to something true. */
  it('should fall back to NETWORK_ERROR when the request never reached the gateway', () => {
    render(
      <ErrorPanel
        error={new ApolloError({ networkError: new Error('down') })}
        onRetry={jest.fn()}
      />,
    );
    expect(screen.getByText('NETWORK_ERROR')).toBeInTheDocument();
  });

  it('should say the panel has not loaded rather than that it is stale', () => {
    render(<ErrorPanel error={gatewayError('SERVICE_UNAVAILABLE')} onRetry={jest.fn()} />);
    expect(screen.getByText(/has not loaded/i)).toBeInTheDocument();
  });

  it('should retry when asked', async () => {
    const onRetry = jest.fn();
    render(<ErrorPanel error={gatewayError('SERVICE_UNAVAILABLE')} onRetry={onRetry} />);

    await userEvent.click(screen.getByRole('button', { name: 'RETRY' }));

    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});

describe('EmptyPanel', () => {
  it('should explain why it is empty rather than only that it is', () => {
    render(<EmptyPanel title="NO READINGS IN RANGE" detail="Nothing matches this filter." />);

    expect(screen.getByText('NO READINGS IN RANGE')).toBeInTheDocument();
    expect(screen.getByText('Nothing matches this filter.')).toBeInTheDocument();
  });

  it('should offer no action when none was given', () => {
    render(<EmptyPanel title="EMPTY" detail="Nothing here." />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('should run the offered action', async () => {
    const onAction = jest.fn();
    render(
      <EmptyPanel
        title="EMPTY"
        detail="Nothing here."
        actionLabel="CLEAR ALL"
        onAction={onAction}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'CLEAR ALL' }));

    expect(onAction).toHaveBeenCalledTimes(1);
  });
});
