/**
 * (home) layout — màn hình menu trung tâm, là điểm vào app.
 *
 * Theo cấu trúc hiện tại của DCMS Mobile (chưa có flow dashboard chính thức), màn hình
 * này đóng vai trò "bản đồ" cho tester: liệt kê toàn bộ các screen đang triển khai
 * (auth + account), chia theo nhóm, đồng thời làm nơi điều hướng tới các màn hình chính
 * thức (đăng nhập, trang chủ tài khoản) tuỳ trạng thái phiên.
 *
 * Khi DCMS có dashboard chính thức (UC-31, S05), route này sẽ thay bằng dashboard thật.
 */

import { Stack } from 'expo-router';
import { colors } from '@/theme/colors';

export default function HomeLayout() {
  return <Stack screenOptions={{ headerTitle: 'DCMS Mobile', headerTintColor: colors.primary }} />;
}
