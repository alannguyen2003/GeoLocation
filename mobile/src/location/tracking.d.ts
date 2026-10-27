// Type declarations shared by tracking.native.ts and tracking.web.ts.
// Metro picks the platform file at bundle time; TypeScript resolves imports to this file.
import type { TrackingSettings } from './settings';
import type { StartResult, TrackingMode } from './types';

/** False on the web: the browser stops tracking when the tab closes. */
export declare const supportsBackgroundTracking: boolean;

export type StartOptions = {
  /** False when resuming at app start: never show a permission prompt, use what is already granted. */
  prompt: boolean;
};

export declare function startTracking(settings: TrackingSettings, options: StartOptions): Promise<StartResult>;
export declare function stopTracking(): Promise<void>;
export declare function getActiveMode(): Promise<TrackingMode | null>;
export declare function hasForegroundPermission(): Promise<boolean>;
/** Asks for "Allow all the time" / "Always". Only call when the user turns on background tracking. */
export declare function requestBackgroundPermission(): Promise<boolean>;
