import { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AxiosAdapter, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import App from './App';
import { api } from './api/client';
import { tokens } from './api/tokens';
import { AuthProvider } from './auth/AuthContext';
import { ToastProvider } from './components/Toast';
import { ThemeProvider } from './theme/ThemeContext';

const owner = { id: 'o1', userName: 'owner', displayName: 'Owner Name', avatarUrl: null };

const item = (id: string, title: string) => ({
  id,
  title,
  thumbnailUrl: null,
  durationSeconds: 125,
  visibility: 'Public',
  status: 'Ready',
  processingProgress: 100,
  viewCount: 1234,
  owner,
  createdAt: '2026-10-01T10:00:00Z',
  publishedAt: '2026-10-01T10:05:00Z'
});

const paged = (items: unknown[]) => ({ items, page: 1, pageSize: 24, totalCount: items.length, totalPages: 1 });

const video = {
  ...item('v1', 'My great video'),
  description: 'A description',
  streamUrl: '/api/stream/v1/master.m3u8',
  renditions: [],
  width: 1280,
  height: 720,
  likeCount: 7,
  myReaction: null,
  resumePositionSeconds: null,
  dislikeCount: null,
  sizeBytes: null,
  processingProgress: null,
  processingError: null
};

const routes: Record<string, () => unknown> = {
  '/api/feed/recommended': () => paged([item('v1', 'First video'), item('v2', 'Second video')]),
  '/api/videos/v1': () => video,
  '/api/videos/v1/related': () => [item('v2', 'Related one')],
  '/api/search/suggestions': () => []
};

const mockAdapter: AxiosAdapter = async (config: InternalAxiosRequestConfig) => {
  const handler = routes[config.url ?? ''];
  if (!handler) {
    throw new AxiosError(`not mocked: ${config.url}`, '404', config, null, { status: 404, statusText: '', data: {}, headers: {}, config });
  }
  return { data: handler(), status: 200, statusText: 'OK', headers: {}, config };
};

function renderApp(path: string, children?: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ThemeProvider>
        <ToastProvider>
          <MemoryRouter initialEntries={[path]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
            <AuthProvider>{children ?? <App />}</AuthProvider>
          </MemoryRouter>
        </ToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

beforeAll(() => {
  // jsdom lacks these browser APIs
  class FakeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  (globalThis as unknown as { IntersectionObserver: unknown }).IntersectionObserver = FakeObserver;
  window.matchMedia = ((query: string) => ({
    matches: false,
    media: query,
    addEventListener() {},
    removeEventListener() {},
    addListener() {},
    removeListener() {},
    onchange: null,
    dispatchEvent: () => false
  })) as unknown as typeof window.matchMedia;
});

beforeEach(async () => {
  localStorage.clear();
  tokens.clear();
  api.defaults.adapter = mockAdapter;
  await act(async () => {
    await i18n.changeLanguage('en');
  });
});

afterEach(() => {
  cleanup(); // vitest has no globals, so Testing Library cannot register its own cleanup
  api.defaults.adapter = undefined;
});

describe('app smoke tests', () => {
  it('renders the home feed for an anonymous visitor', async () => {
    renderApp('/');
    expect(await screen.findByText('First video')).toBeTruthy();
    expect(screen.getByText('Second video')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Sign in' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Popular' })).toBeTruthy();
  });

  it('opens the sign-in page and switches the language to Vietnamese', async () => {
    renderApp('/');
    await screen.findByText('First video');

    fireEvent.click(screen.getByRole('link', { name: 'Sign in' }));
    expect(await screen.findByLabelText('Email or username')).toBeTruthy();

    await act(async () => {
      await i18n.changeLanguage('vi');
    });
    expect(await screen.findByLabelText('Email hoặc tên đăng nhập')).toBeTruthy();
    expect(document.documentElement.lang).toBe('vi');
  });

  it('renders the watch page with title, likes, description and the related list', async () => {
    renderApp('/watch/v1');
    expect(await screen.findByRole('heading', { name: 'My great video' })).toBeTruthy();
    expect(screen.getByText('A description')).toBeTruthy();
    expect(await screen.findByText('Related one')).toBeTruthy();
    expect(screen.getByRole('button', { name: /Like/ }).textContent).toContain('7');
  });

  it('shows a friendly message for a video that does not exist', async () => {
    renderApp('/watch/missing');
    expect(await screen.findByText("This video isn't available")).toBeTruthy();
  });

  it('protects the upload page: anonymous users are sent to sign in', async () => {
    renderApp('/upload');
    expect(await screen.findByLabelText('Email or username')).toBeTruthy();
  });
});
