/**
 * S26+ — RealtimeToast.
 *
 * Hiển thị banner notification mỗi khi nhận event `notificationCreated` từ SignalR.
 * Auto-dismiss sau 5s; tap để đi tới notification detail hoặc deep link.
 *
 * Phải đặt ở root layout (ngoài navigation stack) để hiển thị trên mọi màn hình.
 *
 * Không dùng thư viện toast bên ngoài — dùng Animated API gốc để tránh phụ thuộc.
 */

import { useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  Animated,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { onNotificationToast } from './realtime-client';
import type { NotificationDto } from './notification.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

const AUTO_DISMISS_MS = 5_000;

interface ToastState {
  visible: boolean;
  item: NotificationDto | null;
}

export function RealtimeToast() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [toast, setToast] = useState<ToastState>({ visible: false, item: null });
  const opacity = useRef(new Animated.Value(0)).current;
  const dismissTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const hide = useCallback(() => {
    if (dismissTimer.current) { clearTimeout(dismissTimer.current); dismissTimer.current = null; }
    Animated.timing(opacity, { toValue: 0, duration: 250, useNativeDriver: true }).start(() => {
      setToast({ visible: false, item: null });
    });
  }, [opacity]);

  const show = useCallback((item: NotificationDto) => {
    if (dismissTimer.current) clearTimeout(dismissTimer.current);
    setToast({ visible: true, item });
    opacity.setValue(0);
    Animated.timing(opacity, { toValue: 1, duration: 300, useNativeDriver: true }).start();
    dismissTimer.current = setTimeout(hide, AUTO_DISMISS_MS);
  }, [opacity, hide]);

  useEffect(() => {
    const unsub = onNotificationToast(show);
    return () => { unsub(); if (dismissTimer.current) clearTimeout(dismissTimer.current); };
  }, [show]);

  if (!toast.visible || !toast.item) return null;

  const item = toast.item;
  const deepLink = item.deepLink ?? '/(home)/notifications';

  const handlePress = () => {
    hide();
    if (item.deepLink) {
      router.push(item.deepLink as never);
    } else {
      router.push('/(home)/notifications');
    }
  };

  return (
    <Animated.View
      style={[
        styles.container,
        { top: insets.top + spacing.sm, opacity },
      ]}
      pointerEvents={toast.visible ? 'auto' : 'none'}
    >
      <Pressable
        onPress={handlePress}
        style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
        accessibilityRole="button"
        accessibilityLabel={`Thông báo: ${item.title}`}
      >
        <View style={styles.content}>
          <View style={styles.header}>
            <Text style={styles.label}>Thông báo mới</Text>
            <View style={styles.dot} />
          </View>
          <Text style={styles.title} numberOfLines={1}>{item.title}</Text>
          <Text style={styles.body} numberOfLines={2}>{item.body}</Text>
        </View>
        <Pressable onPress={hide} style={styles.close} hitSlop={12} accessibilityLabel="Đóng thông báo">
          <Text style={styles.closeText}>✕</Text>
        </Pressable>
      </Pressable>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    left: spacing.md,
    right: spacing.md,
    zIndex: 9999,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 14,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.primary,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.18,
    shadowRadius: 12,
    elevation: 8,
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: spacing.sm,
  },
  cardPressed: { opacity: 0.85 },
  content: { flex: 1 },
  header: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs, marginBottom: 2 },
  label: { fontSize: typography.caption, fontWeight: '700', color: colors.primary },
  dot: { width: 6, height: 6, borderRadius: 3, backgroundColor: colors.primary },
  title: { fontSize: typography.body, fontWeight: '700', color: colors.text },
  body: { fontSize: typography.caption + 1, color: colors.text, marginTop: 2 },
  close: { padding: spacing.xs },
  closeText: { fontSize: 16, color: colors.muted },
});
