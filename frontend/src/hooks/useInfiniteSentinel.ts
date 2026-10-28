import { useCallback, useRef } from 'react';

/** Ref callback for an element at the end of a list: calls onReach when it scrolls into view. */
export function useInfiniteSentinel(onReach: () => void, enabled: boolean) {
  const observer = useRef<IntersectionObserver | null>(null);

  return useCallback(
    (node: HTMLElement | null) => {
      observer.current?.disconnect();
      if (!node || !enabled) return;

      observer.current = new IntersectionObserver(
        (entries) => {
          if (entries[0]?.isIntersecting) onReach();
        },
        { rootMargin: '400px' }
      );
      observer.current.observe(node);
    },
    [onReach, enabled]
  );
}
