import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useAsyncData } from './useAsyncData';

/** A promise the test resolves by hand, to control the order responses arrive in. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

describe('useAsyncData', () => {
  it('keeps the latest result when an earlier request answers last', async () => {
    const slow = deferred<string>();
    const fast = deferred<string>();
    const slowLoad = () => slow.promise;
    const fastLoad = () => fast.promise;

    const { result, rerender } = renderHook(({ load }) => useAsyncData(load, 'Failed'), {
      initialProps: { load: slowLoad },
    });

    rerender({ load: fastLoad });

    await act(async () => {
      fast.resolve('latest');
      await fast.promise;
    });

    await act(async () => {
      slow.resolve('stale');
      await slow.promise;
    });

    expect(result.current.data).toBe('latest');
    expect(result.current.isLoading).toBe(false);
  });
});
