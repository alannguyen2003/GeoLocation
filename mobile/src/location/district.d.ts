// Type declarations shared by district.native.ts and district.web.ts.
import type { QueuedPoint } from './types';

/**
 * Adds a ward/district name to points that lack one, when it can be had cheaply.
 * Never throws and never delays an upload by more than one short lookup.
 */
export declare function addDistricts(points: QueuedPoint[]): Promise<QueuedPoint[]>;
