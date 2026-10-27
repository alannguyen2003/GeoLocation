import { createStore } from '@/lib/store';

import type { TrackingMode } from './types';

export type TrackingState = {
  mode: TrackingMode | null;
  busy: boolean;
  /** Message for the user about tracking, if it needs one. */
  notice: string | null;
};

/** Kept apart from controller.ts so the platform tracking modules can report problems without an import cycle. */
export const trackingStore = createStore<TrackingState>({ mode: null, busy: false, notice: null });

export function setTrackingNotice(notice: string | null): void {
  trackingStore.set((state) => (state.notice === notice ? state : { ...state, notice }));
}
