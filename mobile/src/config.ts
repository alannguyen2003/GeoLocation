// Inlined at build time by Expo, so it must be read as the literal `process.env.EXPO_PUBLIC_API_URL`.
// Changing it means a new EAS build (mobile) or a redeploy (web).
export const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? '').replace(/\/+$/, '');

export const isApiConfigured = API_URL.length > 0;
