import { useCallback, useEffect, useRef, useState } from 'react';
import { serverNow } from './api.js';

/** Calls fn immediately and then every intervalMs. `key` restarts the loop when it changes. */
export function usePolling(fn, intervalMs, key, enabled = true) {
  const [state, setState] = useState({ data: null, error: null, loading: enabled });
  const fnRef = useRef(fn);
  fnRef.current = fn;
  const inFlight = useRef(false);
  const alive = useRef(true);

  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);

  const refresh = useCallback(async () => {
    if (inFlight.current) return;
    inFlight.current = true;
    try {
      const data = await fnRef.current();
      if (alive.current) setState({ data, error: null, loading: false });
    } catch (error) {
      if (alive.current) setState((s) => ({ ...s, error, loading: false }));
    } finally {
      inFlight.current = false;
    }
  }, []);

  useEffect(() => {
    if (!enabled) {
      setState({ data: null, error: null, loading: false });
      return undefined;
    }
    setState((s) => ({ ...s, loading: true }));
    refresh();
    const id = setInterval(() => {
      if (!document.hidden) refresh();
    }, intervalMs);
    const onVisible = () => {
      if (!document.hidden) refresh();
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      clearInterval(id);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [enabled, intervalMs, key, refresh]);

  return { ...state, refresh };
}

/** Current server-corrected time in ms, re-rendered every second. */
export function useNow(intervalMs = 1000) {
  const [now, setNow] = useState(serverNow());
  useEffect(() => {
    const id = setInterval(() => setNow(serverNow()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}
