import { jest } from '@jest/globals';
import { render, screen } from '@testing-library/react';
import ErrorBoundary from './ErrorBoundary';

function Broken(): never {
  throw new Error('render exploded');
}

describe('ErrorBoundary', () => {
  it('should render its children when nothing throws', () => {
    render(
      <ErrorBoundary>
        <p>fine</p>
      </ErrorBoundary>,
    );

    expect(screen.getByText('fine')).toBeInTheDocument();
  });

  it('should replace a failed render with a plain message and a reload', () => {
    const consoleError = jest.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <ErrorBoundary>
        <Broken />
      </ErrorBoundary>,
    );

    expect(screen.getByText('SOMETHING WENT WRONG')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'RELOAD' })).toBeInTheDocument();
    expect(screen.queryByText('render exploded')).not.toBeInTheDocument();
    consoleError.mockRestore();
  });

  it('should still log the error for developers', () => {
    const consoleError = jest.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <ErrorBoundary>
        <Broken />
      </ErrorBoundary>,
    );

    expect(consoleError.mock.calls.some(([first]) => first === 'Render failed')).toBe(true);
    consoleError.mockRestore();
  });
});
