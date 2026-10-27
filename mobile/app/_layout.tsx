// Defines the background location task at startup. Must stay the first import:
// when the OS wakes the app in the background, only top-level module code runs.
import '@/location/backgroundTask';

import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { initializeTracking } from '@/location/controller';
import { startAutoFlush } from '@/location/uploader';
import { useTheme } from '@/ui/theme';

export default function RootLayout() {
  const theme = useTheme();

  useEffect(() => {
    void initializeTracking();
    return startAutoFlush();
  }, []);

  return (
    <SafeAreaProvider>
      <StatusBar style="auto" />
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: theme.card },
          headerTintColor: theme.text,
          contentStyle: { backgroundColor: theme.background },
        }}>
        <Stack.Screen name="index" options={{ title: 'Location Tracker' }} />
        <Stack.Screen name="disclosure" options={{ title: 'Dữ liệu vị trí', presentation: 'modal' }} />
        <Stack.Screen name="history" options={{ title: 'Lịch sử' }} />
        <Stack.Screen name="settings" options={{ title: 'Cài đặt' }} />
      </Stack>
    </SafeAreaProvider>
  );
}
