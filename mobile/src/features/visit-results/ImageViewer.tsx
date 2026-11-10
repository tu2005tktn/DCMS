/**
 * Full-screen viewer for Ready images of one visit (docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §2.3).
 * Swipe or ‹ › to change image; pinch or + / − to zoom (1×–4×); drag to pan while zoomed.
 * Built on PanResponder so it needs no extra native gesture package; buttons also work with a mouse on the emulator.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Image, Modal, PanResponder, Pressable, StyleSheet, Text, View, type GestureResponderEvent } from 'react-native';
import { formatDate } from '@/features/appointments/appointments.rules';
import { useAuthorizedImage } from './useAuthorizedImage';
import { documentTypeLabels, sourceLabels, type OwnDocument } from './visit-results.types';

const minScale = 1, maxScale = 4;
const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));
const distance = (e: GestureResponderEvent) => {
  const [a, b] = e.nativeEvent.touches;
  return a && b ? Math.hypot(a.pageX - b.pageX, a.pageY - b.pageY) : 0;
};

export default function ImageViewer({ images, index, token, onIndex, onClose, onDenied }:
  { images: OwnDocument[]; index: number | null; token: string; onIndex: (next: number) => void; onClose: () => void; onDenied: (status: number) => void }) {
  const [scale, setScale] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [attempt, setAttempt] = useState(0);
  const gesture = useRef({ startScale: 1, startDistance: 0, startOffset: { x: 0, y: 0 }, swipe: 0 });
  const current = index === null ? null : images[index] ?? null;
  const image = useAuthorizedImage(token, current?.documentId ?? null, attempt);
  const failed = image.failed, loading = image.loading;

  useEffect(() => { setScale(1); setOffset({ x: 0, y: 0 }); }, [index, attempt]);
  useEffect(() => { if (image.status === 401 || image.status === 403) onDenied(image.status); }, [image.status]);

  const go = (step: number) => {
    if (index === null) return;
    const next = index + step;
    if (next >= 0 && next < images.length) onIndex(next);
  };
  const zoom = (step: number) => setScale(value => { const next = clamp(value + step, minScale, maxScale); if (next === 1) setOffset({ x: 0, y: 0 }); return next; });

  const responder = useMemo(() => PanResponder.create({
    onStartShouldSetPanResponder: () => true,
    onMoveShouldSetPanResponder: () => true,
    onPanResponderGrant: e => {
      gesture.current = { startScale: scale, startDistance: distance(e), startOffset: offset, swipe: 0 };
    },
    onPanResponderMove: (e, g) => {
      const d = distance(e);
      if (d > 0) {
        if (gesture.current.startDistance === 0) gesture.current.startDistance = d;
        setScale(clamp(gesture.current.startScale * (d / gesture.current.startDistance), minScale, maxScale));
      } else if (scale > 1) {
        setOffset({ x: gesture.current.startOffset.x + g.dx, y: gesture.current.startOffset.y + g.dy });
      } else {
        gesture.current.swipe = g.dx;
      }
    },
    onPanResponderRelease: () => {
      if (scale <= 1) {
        setOffset({ x: 0, y: 0 });
        if (gesture.current.swipe < -60) go(1); else if (gesture.current.swipe > 60) go(-1);
      }
    },
  }), [scale, offset, index, images.length]);

  if (!current) return null;
  return <Modal visible animationType="fade" onRequestClose={onClose} statusBarTranslucent>
    <View style={styles.screen}>
      <View style={styles.top}>
        <Text style={styles.counter}>{(index ?? 0) + 1} / {images.length}</Text>
        <Pressable accessibilityRole="button" accessibilityLabel="Đóng" onPress={onClose} style={styles.iconButton}><Text style={styles.icon}>✕</Text></Pressable>
      </View>
      <View style={styles.stage} {...responder.panHandlers}>
        {failed
          ? <View style={styles.failed}>
            <Text style={styles.failedText}>Không tải được ảnh. Kiểm tra kết nối rồi thử lại.</Text>
            <Pressable accessibilityRole="button" onPress={() => setAttempt(v => v + 1)} style={styles.retry}><Text style={styles.retryText}>Thử lại</Text></Pressable>
          </View>
          : image.uri && <Image source={{ uri: image.uri }} resizeMode="contain"
            accessibilityLabel={documentTypeLabels[current.documentType] ?? current.fileName}
            style={[styles.image, { transform: [{ translateX: offset.x }, { translateY: offset.y }, { scale }] }]} />}
        {loading && !failed && <ActivityIndicator style={StyleSheet.absoluteFill} color="#fff" size="large" />}
      </View>
      <View style={styles.caption}>
        <Text style={styles.captionTitle}>{documentTypeLabels[current.documentType] ?? current.documentType}</Text>
        <Text style={styles.captionText}>Ngày {formatDate(`${current.sourceDate}T12:00:00+07:00`)} · Nguồn: {sourceLabels[current.source] ?? current.source}</Text>
      </View>
      <View style={styles.controls}>
        <Control label="‹" name="Ảnh trước" disabled={(index ?? 0) === 0} onPress={() => go(-1)} />
        <Control label="−" name="Thu nhỏ" disabled={scale <= minScale} onPress={() => zoom(-0.5)} />
        <Text style={styles.zoom}>{Math.round(scale * 100)}%</Text>
        <Control label="+" name="Phóng to" disabled={scale >= maxScale} onPress={() => zoom(0.5)} />
        <Control label="›" name="Ảnh sau" disabled={(index ?? 0) >= images.length - 1} onPress={() => go(1)} />
      </View>
    </View>
  </Modal>;
}

function Control({ label, name, disabled, onPress }: { label: string; name: string; disabled?: boolean; onPress: () => void }) {
  return <Pressable accessibilityRole="button" accessibilityLabel={name} accessibilityState={{ disabled }} disabled={disabled} onPress={onPress}
    style={[styles.iconButton, disabled && styles.disabled]}><Text style={styles.icon}>{label}</Text></Pressable>;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000' },
  top: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingHorizontal: 16, paddingTop: 36, paddingBottom: 8 },
  counter: { color: '#fff', fontSize: 15 },
  stage: { flex: 1, overflow: 'hidden', justifyContent: 'center' },
  image: { width: '100%', height: '100%' },
  failed: { alignItems: 'center', gap: 12, padding: 24 },
  failedText: { color: '#fff', textAlign: 'center' },
  retry: { backgroundColor: '#fff', borderRadius: 8, paddingHorizontal: 16, paddingVertical: 8 },
  retryText: { color: '#000', fontWeight: '600' },
  caption: { paddingHorizontal: 16, paddingVertical: 8, gap: 2 },
  captionTitle: { color: '#fff', fontWeight: '600', fontSize: 15 },
  captionText: { color: '#D0D5DD', fontSize: 13 },
  controls: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', gap: 12, paddingBottom: 32, paddingTop: 8 },
  iconButton: { minWidth: 48, height: 48, borderRadius: 24, backgroundColor: 'rgba(255,255,255,0.15)', alignItems: 'center', justifyContent: 'center' },
  icon: { color: '#fff', fontSize: 24 },
  zoom: { color: '#fff', minWidth: 52, textAlign: 'center' },
  disabled: { opacity: 0.35 },
});
