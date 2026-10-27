import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Platform, StyleSheet, View } from 'react-native';

import { acceptDisclosure, setBackgroundEnabled, startTracking } from '@/location/controller';
import { Body, Button, Card, Heading, Screen } from '@/ui/components';

/**
 * Prominent disclosure, shown before any location permission prompt (required by Google Play).
 * It says what is collected, when (including while the app is closed) and why.
 */
export default function DisclosureScreen() {
  const router = useRouter();
  const { background } = useLocalSearchParams<{ background?: string }>();
  const wantsBackground = background === '1';
  const isWeb = Platform.OS === 'web';
  const [busy, setBusy] = useState(false);

  const onAccept = async () => {
    setBusy(true);
    try {
      await acceptDisclosure();
      // Foreground permission first; the background permission is only asked for when the user opted in.
      const result = await startTracking();
      if (result.ok && wantsBackground) {
        await setBackgroundEnabled(true);
      }
    } finally {
      setBusy(false);
      router.back();
    }
  };

  return (
    <Screen>
      <Heading>Ứng dụng này thu thập dữ liệu vị trí</Heading>

      <Card title="Dữ liệu được thu thập">
        <Body>
          Vị trí chính xác của thiết bị (vĩ độ, kinh độ), độ chính xác, độ cao, tốc độ, hướng di chuyển và thời điểm ghi.
        </Body>
        <Body>
          Từ toạ độ, máy chủ của dự án suy ra thành phố, tỉnh và quốc gia bằng dữ liệu địa danh lưu sẵn; việc này không gửi
          toạ độ cho ai khác.
        </Body>
        {isWeb ? null : (
          <Body>
            Khi ứng dụng đang mở, toạ độ còn được gửi tới dịch vụ định vị của {Platform.OS === 'ios' ? 'Apple' : 'Google'}{' '}
            để tra tên quận/phường. Việc này không diễn ra khi ứng dụng chạy nền.
          </Body>
        )}
      </Card>

      <Card title="Khi nào">
        {isWeb ? (
          <Body>Chỉ khi bạn bật theo dõi và tab này đang mở. Đóng tab là dừng theo dõi.</Body>
        ) : (
          <>
            <Body>Khi bạn bật theo dõi và ứng dụng đang mở.</Body>
            <Body style={styles.emphasis}>
              Nếu bạn bật “Theo dõi nền”, ứng dụng sẽ tiếp tục thu thập vị trí cả khi ứng dụng đã đóng hoặc không được sử
              dụng, kể cả khi màn hình khoá.
            </Body>
            {Platform.OS === 'android' ? (
              <Body muted>Khi theo dõi nền, Android hiển thị một thông báo cố định để bạn luôn biết việc ghi vị trí đang chạy.</Body>
            ) : null}
          </>
        )}
      </Card>

      <Card title="Vì sao">
        <Body>
          Để ghi lại lịch sử vị trí của thiết bị này lên máy chủ của dự án và hiển thị lại cho bạn trong mục Lịch sử. Đây là
          tính năng chính của ứng dụng.
        </Body>
      </Card>

      <Card title="Lưu trữ và quyền của bạn">
        <Body>
          Dữ liệu gắn với một mã thiết bị ẩn danh, không cần tài khoản. Ngoài việc tra tên quận/phường nói trên, dữ liệu
          không được chia sẻ với bên thứ ba và không dùng cho quảng cáo. Máy chủ tự động xoá điểm cũ (hiện tại sau 30 ngày). Bạn có thể dừng theo dõi bất cứ lúc nào và
          xoá toàn bộ dữ liệu trong Cài đặt → “Xoá dữ liệu của tôi”.
        </Body>
      </Card>

      <Body muted>
        Sau khi bạn đồng ý, {isWeb ? 'trình duyệt' : 'hệ thống'} sẽ hỏi quyền truy cập vị trí.
        {wantsBackground && !isWeb
          ? ' Để theo dõi nền, hãy chọn “Luôn cho phép” (Android) hoặc “Luôn luôn” (iOS) khi được hỏi.'
          : ''}
      </Body>

      <View style={styles.actions}>
        <Button label="Đồng ý và tiếp tục" onPress={() => void onAccept()} loading={busy} />
        <Button label="Không đồng ý" variant="secondary" onPress={() => router.back()} disabled={busy} />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  emphasis: { fontWeight: '600' },
  actions: { gap: 10, marginTop: 4 },
});
