import { useEffect, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, View } from 'react-native';

import { getLocations } from '@/api/client';
import { describeError } from '@/api/http';
import type { LocationPointResponse } from '@/api/types';
import { getStoredDevice } from '@/identity/ensureDevice';
import { flushQueue } from '@/location/uploader';
import { Body, Button, Card, Row, Screen } from '@/ui/components';
import { formatAccuracy, formatCoordinate, formatDateTime, formatPlace, sourceLabels } from '@/ui/format';
import { useTheme } from '@/ui/theme';

const PAGE_SIZE = 200;

type State =
  | { status: 'loading' }
  | { status: 'ready'; points: LocationPointResponse[] }
  | { status: 'error'; message: string };

async function fetchHistory(): Promise<State> {
  try {
    // A device that never registered has no history; do not register just to show an empty list.
    if (!(await getStoredDevice())) {
      return { status: 'ready', points: [] };
    }
    // Send what is still queued first so the list includes it.
    await flushQueue();
    return { status: 'ready', points: await getLocations(PAGE_SIZE) };
  } catch (error) {
    return { status: 'error', message: describeError(error) };
  }
}

export default function HistoryScreen() {
  const theme = useTheme();
  const [state, setState] = useState<State>({ status: 'loading' });
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => {
    let active = true;
    void fetchHistory().then((next) => {
      if (active) {
        setState(next);
      }
    });
    return () => {
      active = false;
    };
  }, []);

  const onRefresh = async () => {
    setRefreshing(true);
    setState(await fetchHistory());
    setRefreshing(false);
  };

  if (state.status === 'loading') {
    return (
      <Screen scroll={false}>
        <View style={styles.center}>
          <ActivityIndicator color={theme.primary} />
          <Body muted style={styles.centerText}>
            Đang tải… Nếu máy chủ vừa “ngủ”, lần tải đầu có thể mất khoảng 1 phút.
          </Body>
        </View>
      </Screen>
    );
  }

  if (state.status === 'error') {
    return (
      <Screen scroll={false}>
        <Card>
          <Body>{state.message}</Body>
          <Button label="Thử lại" onPress={() => void onRefresh()} loading={refreshing} />
        </Card>
      </Screen>
    );
  }

  return (
    <Screen scroll={false}>
      <FlatList
        data={state.points}
        keyExtractor={(point) => String(point.id)}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}
        ListHeaderComponent={
          <Body muted>
            {state.points.length > 0
              ? `${state.points.length} điểm gần nhất trên máy chủ, mới nhất ở trên. Kéo xuống để làm mới.`
              : 'Chưa có điểm nào trên máy chủ.'}
          </Body>
        }
        renderItem={({ item }) => (
          <Card>
            <Body style={styles.time}>{formatDateTime(item.recordedAt)}</Body>
            <Row label="Khu vực" value={formatPlace(item) ?? 'Không xác định'} />
            <Row label="Toạ độ" value={`${formatCoordinate(item.latitude)}, ${formatCoordinate(item.longitude)}`} />
            <Row label="Độ chính xác" value={formatAccuracy(item.accuracy)} />
            <Row label="Nguồn" value={sourceLabels[item.source] ?? item.source} />
          </Card>
        )}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12 },
  centerText: { textAlign: 'center' },
  list: { gap: 10, paddingBottom: 24 },
  time: { fontWeight: '600' },
});
