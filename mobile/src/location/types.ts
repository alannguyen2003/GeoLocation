export type PointSource = 'foreground' | 'background' | 'web';

/** A point waiting in the local queue. The shape matches the API's batch item. */
export type QueuedPoint = {
  clientPointId: string;
  latitude: number;
  longitude: number;
  accuracy: number | null;
  altitude: number | null;
  speed: number | null;
  heading: number | null;
  /** ISO 8601, UTC. */
  recordedAt: string;
  source: PointSource;
  /** Ward/district from the phone's geocoder, added just before upload when available. */
  district?: string | null;
};

/** Where a point is, as stored on the server. Any part can be missing. */
export type PlaceNames = {
  district: string | null;
  city: string | null;
  region: string | null;
  country: string | null;
};

export type TrackingMode = 'foreground' | 'background' | 'web';

export type StartResult =
  | {
      ok: true;
      mode: TrackingMode;
      warning?: 'background-denied' | 'background-unavailable' | 'position-error';
      /** User-facing explanation that replaces the default text for the warning. */
      detail?: string;
    }
  | { ok: false; reason: 'foreground-denied' | 'services-disabled' | 'insecure-context' | 'unsupported' };
