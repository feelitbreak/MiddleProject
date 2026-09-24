/**
 * @jest-environment node
 */
// Node has a global fetch, which HttpLink requires at import; jsdom does not.
import { shouldRetry } from './apollo';

describe('shouldRetry', () => {
  it('should retry a request that got no response at all', () => {
    expect(shouldRetry(new TypeError('Failed to fetch'))).toBe(true);
  });

  it('should retry a server error', () => {
    expect(shouldRetry({ statusCode: 503 })).toBe(true);
  });

  it.each([400, 401, 429])('should not retry a %i, which would only repeat', (statusCode) => {
    expect(shouldRetry({ statusCode })).toBe(false);
  });
});
