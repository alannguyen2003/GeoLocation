// Type declarations shared by deviceStore.native.ts and deviceStore.web.ts.
// Metro picks the platform file at bundle time; TypeScript resolves imports to this file.
import type { DeviceCredentials } from './types';

export declare function loadCredentials(): Promise<DeviceCredentials | null>;
export declare function saveCredentials(credentials: DeviceCredentials): Promise<void>;
export declare function clearCredentials(): Promise<void>;
