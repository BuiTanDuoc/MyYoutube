import { describe, expect, it } from 'vitest';
import { formatBytes, formatDuration } from './format';

describe('formatDuration', () => {
  it('formats minutes and seconds', () => {
    expect(formatDuration(0)).toBe('0:00');
    expect(formatDuration(65)).toBe('1:05');
    expect(formatDuration(599.9)).toBe('9:59');
  });

  it('adds hours when needed', () => {
    expect(formatDuration(3600)).toBe('1:00:00');
    expect(formatDuration(3725)).toBe('1:02:05');
  });
});

describe('formatBytes', () => {
  it('picks a sensible unit', () => {
    expect(formatBytes(512)).toBe('1 KB');
    expect(formatBytes(5 * 1024 ** 2)).toBe('5.0 MB');
    expect(formatBytes(2 * 1024 ** 3)).toBe('2.0 GB');
  });
});
