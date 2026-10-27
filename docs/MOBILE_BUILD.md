# Hướng dẫn build và cài app Location Tracker lên điện thoại

Tài liệu này dành cho người **chưa từng dùng EAS**. Mỗi bước là một lệnh có thể copy, kèm theo kết quả bạn sẽ thấy khi nó chạy đúng. Mọi lệnh đều chạy trong thư mục `mobile` trừ khi ghi khác.

Thông tin dự án dùng trong tài liệu:

| Mục | Giá trị |
|---|---|
| Android package | `com.nashtech.poc.locationtracker` (trong `app.json`) |
| iOS bundle identifier | `com.nashtech.poc.locationtracker` (trong `app.json`) |
| API URL | `EXPO_PUBLIC_API_URL` trong `eas.json`, mặc định `https://location-tracker-api.onrender.com` |
| Build profile | `development`, `preview`, `production` (trong `eas.json`) |

---

## 1. Tổng quan

Cùng một codebase Expo, nhưng hai cách phát hành khác nhau:

- **Web** được build thành trang tĩnh và deploy lên **Vercel**. Không cần EAS.
- **Điện thoại** không deploy lên server nào cả. EAS Build tạo ra **một file cài đặt** (APK cho Android, bản ad hoc cho iOS), bạn tải về và cài lên máy.

Nên dùng bản nào khi nào:

| Loại | Dùng khi | Theo dõi khi app mở | Theo dõi nền |
|---|---|---|---|
| **Expo Go** (app có sẵn trên store) | Xem nhanh giao diện | Có | **Không** |
| **Development build** (`development`) | Lập trình viên sửa code, xem thay đổi ngay | Có | Có |
| **Preview APK** (`preview`, Android) | Gửi cho người test, chạy độc lập không cần máy tính | Có | Có |
| **iOS** (`preview`, ad hoc) | Test trên iPhone đã đăng ký, cần tài khoản Apple trả phí | Có | Có |

> **Quan trọng:** theo dõi nền (khi app đóng hoặc màn hình khoá) **không chạy trong Expo Go và không chạy trên web**. Muốn test theo dõi nền, phải dùng development build hoặc preview APK.

---

## 2. Chuẩn bị

1. **Node.js LTS**. Kiểm tra:
   ```bash
   node --version
   ```
   Thấy ví dụ `v24.x.x` là được.

2. **Tài khoản Expo**: đăng ký miễn phí tại https://expo.dev/signup.

3. **Cài EAS CLI** và đăng nhập:
   ```bash
   npm install -g eas-cli
   eas login
   eas whoami
   ```
   `eas login` mở trình duyệt để đăng nhập (dùng `eas login --no-browser` nếu muốn nhập ngay trong terminal). `eas whoami` in ra tên tài khoản Expo của bạn là đã đăng nhập thành công.

4. **Cài thư viện của dự án** (một lần, hoặc sau mỗi lần `package.json` thay đổi):
   ```bash
   cd mobile
   npm install
   ```

5. **iOS**: cần tài khoản **Apple Developer Program trả phí** (99 USD/năm) nếu muốn build qua EAS và chia sẻ cho người khác.

6. **Build Android trên máy mình** (tuỳ chọn, mục 7): cần **Android Studio** cùng **Android SDK** và **JDK** (Android Studio đã kèm sẵn JDK).

---

## 3. Thiết lập dự án (làm một lần)

```bash
cd mobile
eas init
```
Lệnh này tạo (hoặc liên kết) dự án trên expo.dev với tài khoản của bạn và ghi `extra.eas.projectId` vào `app.json`. Khi chạy đúng, bạn sẽ thấy dòng báo dự án `@<tài-khoản>/location-tracker` đã được tạo/liên kết. Hãy commit thay đổi này của `app.json`.

```bash
eas build:configure
```
Dự án **đã có sẵn** `eas.json`, nên lệnh này chỉ kiểm tra cấu hình và báo dự án đã được thiết lập cho EAS Build. Nếu nó hỏi chọn nền tảng, chọn `All`.

Kiểm tra lại hai chỗ:

1. **Định danh app** trong `app.json`: `android.package` và `ios.bundleIdentifier` (hiện là `com.nashtech.poc.locationtracker`). Nếu muốn đổi, hãy đổi **trước lần build đầu tiên**; đổi sau này thì app cũ và app mới bị coi là hai app khác nhau.
2. **API URL** trong `eas.json`: thay `https://location-tracker-api.onrender.com` ở cả ba profile bằng URL thật của service trên Render (xem trong Render Dashboard → service `location-tracker-api`). URL phải là **HTTPS** và **không** có dấu `/` ở cuối.

> `EXPO_PUBLIC_API_URL` được **gắn cứng vào app lúc build**. Đổi URL thì phải build lại.

---

## 4. Android: preview APK (cách chính cho PoC)

```bash
eas build --profile preview --platform android
```

Điều sẽ xảy ra:

1. Lần đầu, EAS hỏi có muốn tạo **Android keystore** không. Chọn **Yes** để EAS tự tạo và giữ giúp bạn (không cần tự quản lý file keystore).
2. Code được tải lên và terminal in ra một link dạng `https://expo.dev/accounts/<tài-khoản>/projects/location-tracker/builds/<id>`. Mở link này để xem tiến trình và log build.
3. Gói miễn phí của Expo xếp build vào **hàng đợi ưu tiên thấp**, nên có thể phải chờ vài phút đến lâu hơn trước khi build thật sự bắt đầu. Cứ để terminal chạy, hoặc đóng terminal và theo dõi trên expo.dev; build vẫn tiếp tục.
4. Khi xong, terminal và trang build hiện link cài đặt.

**Cài lên điện thoại Android:**

- Mở link cài đặt **trên điện thoại**, bấm **Install** để tải file `.apk`. Hoặc mở trang build trên máy tính và quét mã QR bằng camera điện thoại.
- Android sẽ chặn cài app ngoài Play Store. Khi được hỏi, cho phép trình duyệt (ví dụ Chrome) **"Cài đặt ứng dụng không rõ nguồn gốc" / "Install unknown apps"**, rồi quay lại cài tiếp.
- Mở app **Location Tracker** là xong.

**Chia sẻ cho người test:** gửi họ link trang build. Mặc định **bất kỳ ai có link** đều cài được.

**Giới hạn người được cài:** trên expo.dev, vào **Project settings** của dự án và tắt tuỳ chọn **Unauthenticated access to internal builds**. Khi đó người test phải đăng nhập bằng một tài khoản Expo có quyền với dự án mới mở được link.

---

## 5. Android: development build

Dành cho lập trình viên: cài **một lần**, sau đó sửa JavaScript là thấy ngay, không phải build lại.

```bash
eas build --profile development --platform android
```
Cài file APK giống mục 4. App cài được là một "dev client" có tên Location Tracker.

Sau đó, mỗi lần làm việc:

```bash
npx expo start --dev-client
```
Terminal hiện mã QR. Mở app dev client trên điện thoại, chọn server đang chạy (hoặc quét mã QR). Điện thoại và máy tính phải **cùng mạng Wi-Fi**.

- **Không cần build lại** khi chỉ sửa code JavaScript/TypeScript và giao diện: app tự reload.
- **Phải build lại** khi thay đổi phần native: thêm package native mới, đổi plugin hoặc quyền trong `app.json`, đổi `EXPO_PUBLIC_API_URL` trong `eas.json`. Xem bảng ở mục 10.

> Muốn dùng backend chạy trên máy mình thay vì Render: tạo file `mobile/.env.local` với `EXPO_PUBLIC_API_URL=http://<IP-LAN-của-máy-tính>:8080` (xem `mobile/.env.example`), rồi khởi động lại `npx expo start --dev-client`. Không dùng `localhost` vì với điện thoại, `localhost` là chính điện thoại.

---

## 6. iOS

### Có tài khoản Apple Developer trả phí

1. Đăng ký từng iPhone sẽ cài app:
   ```bash
   eas device:create
   ```
   Lệnh tạo một link/mã QR. Mở nó **trên iPhone** bằng Safari và làm theo hướng dẫn để cài profile đăng ký. Làm lại cho mỗi máy.
   - Apple giới hạn **tối đa 100 iPhone mỗi năm** cho cách phân phối này.
   - Với tài khoản Apple mới đăng ký hoặc vừa gia hạn, Apple có thể mất **24 đến 72 giờ** để xử lý thiết bị mới; trong thời gian đó build có thể lỗi, hãy chờ rồi thử lại.

2. Build:
   ```bash
   eas build --profile preview --platform ios
   ```
   Lần đầu, EAS yêu cầu đăng nhập Apple ID và tự tạo certificate cùng provisioning profile. Chọn để EAS quản lý giúp.

3. Mở link cài đặt trên iPhone đã đăng ký và bấm **Install**. Trên iOS 16 trở lên, có thể cần bật **Developer Mode** (Cài đặt → Quyền riêng tư & Bảo mật → Chế độ nhà phát triển) rồi khởi động lại máy.

Thêm iPhone mới sau này: chạy lại `eas device:create`, rồi **build lại** để máy mới có trong provisioning profile.

### Không có tài khoản trả phí

Không thể chia sẻ bản build iOS cho người khác. Cách duy nhất là dùng **máy Mac có Xcode**, cắm iPhone của chính bạn và chạy:

```bash
npx expo run:ios --device
```
Lệnh hỏi chọn iPhone đang cắm, build bằng Xcode và cài thẳng lên máy. Với Apple ID miễn phí, app hết hạn sau vài ngày và phải chạy lại lệnh.

---

## 7. Build trên máy mình (tuỳ chọn, không phải chờ hàng đợi EAS)

**Bản debug** lên điện thoại cắm USB (đã bật USB debugging) hoặc máy ảo Android:
```bash
npx expo run:android
```
Lệnh tạo thư mục `android/` (đã nằm trong `.gitignore`), build và mở app. Lần đầu khá lâu.

**APK preview** tạo ngay trên máy:
```bash
eas build --profile preview --platform android --local
```
Cần Android SDK và JDK như mục 2. Khi xong, file `.apk` nằm trong thư mục `mobile` (dùng thêm `--output <đường-dẫn>` để chọn nơi lưu). EAS chỉ hỗ trợ build local trên **macOS và Linux**. Windows không được hỗ trợ; WSL có thể chạy được nhưng Expo không chính thức kiểm thử, nên trên Windows hãy dùng `npx expo run:android` hoặc build trên EAS.

---

## 8. Checklist test trên điện thoại thật

- [ ] Bấm **Bắt đầu theo dõi** lần đầu: màn hình **"Ứng dụng này thu thập dữ liệu vị trí"** hiện ra **trước** hộp thoại xin quyền của hệ thống.
- [ ] Đồng ý và cấp quyền: trạng thái thành **"Đang theo dõi (khi ứng dụng mở)"**, mục **Điểm gần nhất** có toạ độ sau khi đi bộ vài chục mét.
- [ ] Bật **Theo dõi nền**: hệ thống hỏi quyền **"Luôn cho phép" / "Allow all the time"** (Android) hoặc **"Luôn luôn" / "Always"** (iOS). Trạng thái thành **"Đang theo dõi (cả khi ứng dụng đóng)"**.
- [ ] Android: khi đang theo dõi nền, có **thông báo cố định** "Location Tracker đang ghi vị trí".
- [ ] Khoá màn hình, đi bộ vài phút, mở lại app → **Lịch sử**: có các điểm với nguồn **"Chạy nền"**.
- [ ] **Lịch sử** và **Điểm gần nhất** có dòng **Khu vực**, ví dụ "Hoàn Kiếm, Hanoi, Vietnam". Thành phố, tỉnh và quốc gia do máy chủ suy ra nên luôn có (trừ khi ở xa mọi khu dân cư). Tên quận/phường do điện thoại tra, chỉ có khi app đang mở và có mạng, nên các điểm ghi lúc chạy nền thường không có phần này. Đây là bình thường.
- [ ] Bật **chế độ máy bay**, đi thêm một đoạn: **Đang chờ gửi** tăng. Tắt chế độ máy bay: số này về 0 và điểm xuất hiện trong Lịch sử.
- [ ] **Cài đặt → Xoá dữ liệu của tôi**: theo dõi dừng, Lịch sử trống, mã thiết bị thành "Chưa đăng ký".

---

## 9. Cài đặt pin trên Android

Một số hãng (Xiaomi, Oppo, Vivo, Samsung, Huawei...) tắt app chạy nền rất mạnh tay. **Trước khi kết luận theo dõi nền không chạy**, hãy:

1. Mở **Cài đặt → Ứng dụng → Location Tracker → Pin** và chọn **Không hạn chế / Unrestricted** (hoặc tắt **Tối ưu hoá pin / Battery optimization** cho app).
2. Nếu máy có mục **Tự khởi chạy / Autostart** (thường thấy trên Xiaomi, Oppo, Vivo), bật cho Location Tracker.
3. Không vuốt tắt app khỏi danh sách đa nhiệm khi đang test.

Tên menu khác nhau giữa các hãng; trang https://dontkillmyapp.com có hướng dẫn cho từng hãng.

---

## 10. Khi nào phải build lại

| Thay đổi | Cần build mới? |
|---|---|
| `EXPO_PUBLIC_API_URL` trong `eas.json` | **Có** |
| Thêm/gỡ/nâng cấp package native (ví dụ cài bằng `npx expo install` một module có code native) | **Có** |
| Quyền, chuỗi xin quyền, cấu hình plugin trong `app.json` (ví dụ `expo-location`) | **Có** |
| Icon, tên app, splash, `android.package`, `ios.bundleIdentifier` | **Có** |
| Nâng cấp Expo SDK | **Có** |
| Code JavaScript/TypeScript, giao diện, logic trong `app/` và `src/` (khi dùng development build) | Không, chỉ cần `npx expo start --dev-client` |
| Code JavaScript/TypeScript khi người test dùng **preview APK** | Có, preview APK không tự lấy code mới |

---

## 11. Xử lý sự cố

**Build lỗi ở bước credentials (keystore, certificate, provisioning profile).**
Chạy lại lệnh build và khi được hỏi, chọn để **EAS tự tạo/quản lý** credentials. Không tự tạo keystore bằng tay nếu không cần.

**App cài được nhưng không gửi được dữ liệu ("Không kết nối được tới máy chủ").**
- Kiểm tra **Cài đặt → Thông tin → Máy chủ** trong app có đúng URL Render không. Nếu sai: sửa `EXPO_PUBLIC_API_URL` trong `eas.json` rồi **build lại**.
- URL phải là **HTTPS**. Bản preview không cho kết nối HTTP thường.
- Mở `https://<service>.onrender.com/health` trên trình duyệt điện thoại. Service miễn phí của Render **ngủ sau 15 phút không dùng** và mất khoảng **1 phút** để thức dậy; yêu cầu đầu tiên sau đó thường bị timeout. Đây là bình thường: app giữ điểm trong hàng đợi và tự gửi lại.

**Đã từ chối quyền vị trí và hệ thống không hỏi lại nữa.**
Mở **Cài đặt hệ thống → Ứng dụng → Location Tracker → Quyền → Vị trí** và chọn **Chỉ cho phép khi dùng ứng dụng** hoặc **Luôn cho phép**. Trên iOS: **Cài đặt → Location Tracker → Vị trí**.

**Theo dõi nền tự dừng.**
- Làm theo mục 9 (pin không hạn chế, autostart).
- Nếu người dùng **buộc dừng (Force stop)** hoặc vuốt tắt app, nhiều máy Android sẽ không chạy lại theo dõi nền cho tới khi mở app lại. Mở app, theo dõi sẽ tự tiếp tục.
- Kiểm tra quyền vị trí vẫn là **"Luôn cho phép"**: Android có thể tự thu hồi quyền của app lâu không dùng.

**iOS báo thiết bị không có trong provisioning profile / không cài được.**
iPhone đó chưa được đăng ký hoặc được đăng ký sau lần build. Chạy `eas device:create` cho máy đó, chờ Apple xử lý nếu là tài khoản mới, rồi chạy lại `eas build --profile preview --platform ios`.

**Mọi dữ liệu biến mất sau khoảng một tháng.**
Database miễn phí của Render **hết hạn sau 30 ngày** kể từ khi tạo. Khi tạo lại database, app tự đăng ký thiết bị mới ở lần gửi tiếp theo; dữ liệu cũ không khôi phục được.
