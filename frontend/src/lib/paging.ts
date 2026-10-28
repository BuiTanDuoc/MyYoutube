import type { Paged } from '../api/types';

/** getNextPageParam for useInfiniteQuery over the API's paged results. */
export const nextPage = (last: Paged<unknown>): number | undefined => (last.page < last.totalPages ? last.page + 1 : undefined);
