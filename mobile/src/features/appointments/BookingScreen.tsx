import { useRouter } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { uuid } from 'expo-modules-core';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { useSession } from '@/features/auth/session/useSession';
import { ApiError } from '@/services/api/api-error';
import { colors } from '@/theme/colors';
import { bookingApi } from './booking.api';
import { notificationMockRepo } from './notification.mock';
import { notificationRepoPersist } from './notification.repo.persist';
import { appointmentRepo } from './appointment.repo';
import { addMockBooking } from './appointments.mock';
import type { AvailabilitySlot, BookingOptions, BookingResult } from './booking.types';

type Relation = 'Self' | 'Relative';
type FieldName = 'bookerName' | 'contact' | 'attendeeName';
const nextDate = () => new Date(Date.now() + 86400000).toISOString().slice(0, 10);
const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

export default function BookingScreen() {
  const router = useRouter();
  const { state } = useSession();
  const token = state.status === 'authenticated' ? state.accessToken : undefined;
  const [options, setOptions] = useState<BookingOptions | null>(null);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldName, string>>>({});
  const [step, setStep] = useState(0);
  const [relation, setRelation] = useState<Relation>('Self');
  const [branchId, setBranchId] = useState<number | null>(null);
  const [dentistId, setDentistId] = useState<number | null>(null);
  const [date, setDate] = useState(nextDate());
  const [reason, setReason] = useState('');
  const [slots, setSlots] = useState<AvailabilitySlot[]>([]);
  const [slot, setSlot] = useState<AvailabilitySlot | null>(null);
  const [requestKey, setRequestKey] = useState<string | null>(null);
  const [bookerName, setBookerName] = useState(state.status === 'authenticated' ? state.user.fullName : '');
  const [contact, setContact] = useState('');
  const [attendeeName, setAttendeeName] = useState(state.status === 'authenticated' ? state.user.fullName : '');
  const [result, setResult] = useState<BookingResult | null>(null);
  const prefilledAccount = useRef<number | null>(null);
  const account = state.status === 'authenticated' ? state.user : null;
  useEffect(() => {
    if (!account || prefilledAccount.current === account.id) return;
    const changedAccount = prefilledAccount.current !== null;
    prefilledAccount.current = account.id;
    const accountContact = account.phoneNumber || account.email || '';
    setBookerName(value => changedAccount ? account.fullName : value || account.fullName);
    setContact(value => changedAccount ? accountContact : value || accountContact);
    setAttendeeName(value => relation === 'Self' ? changedAccount ? account.fullName : value || account.fullName : changedAccount ? '' : value);
  }, [account?.id, account?.fullName, account?.phoneNumber, account?.email, relation]);

  const services = useMemo(() => options?.services.filter(x => x.branchId === branchId) ?? [], [options, branchId]);
  const dentists = useMemo(() => options?.employees.filter(x => x.branchId === branchId && x.canDentist) ?? [], [options, branchId]);
  const service = services[0];
  const serviceId = service?.id ?? null;
  const dentist = dentists.find(x => x.id === dentistId);
  const branch = options?.branches.find(x => x.id === branchId);

  useEffect(() => {
    let active = true;
    setLoadingOptions(true);
    bookingApi.options(token).then(value => { if (active) setOptions(value); })
      .catch(() => { if (active) setError('Không tải được chi nhánh và dịch vụ. Vui lòng thử lại.'); })
      .finally(() => { if (active) setLoadingOptions(false); });
    return () => { active = false; };
  }, [token]);

  useEffect(() => {
    setSlots([]); setSlot(null); setRequestKey(null);
    if (!branchId || !dentistId || !service || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return;
    let active = true;
    setLoadingSlots(true); setError(null);
    bookingApi.slots(branchId, dentistId, service.id, date, service.publicDurationMinutes, token)
      .then(value => { if (active) setSlots(value.slots); })
      .catch(e => { if (active) setError(e instanceof ApiError ? e.message : 'Không thể kết nối máy chủ. Vui lòng thử lại.'); })
      .finally(() => { if (active) setLoadingSlots(false); });
    return () => { active = false; };
  }, [branchId, dentistId, serviceId, date, token, service?.publicDurationMinutes]);

  function chooseSlot(value: AvailabilitySlot) {
    setSlot(value); setRequestKey(uuid.v4()); setError(null);
  }

  function validateContact(): boolean {
    const errors: Partial<Record<FieldName, string>> = {};
    if (!bookerName.trim() || bookerName.trim().length > 100) errors.bookerName = 'Nhập họ tên người đặt (1–100 ký tự).';
    const value = contact.trim();
    if (!/^0\d{9}$/.test(value) && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value))
      errors.contact = 'Nhập số điện thoại Việt Nam hoặc email hợp lệ.';
    if (!attendeeName.trim() || attendeeName.trim().length > 100) errors.attendeeName = 'Nhập họ tên người khám (1–100 ký tự).';
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function confirm() {
    if (!slot || !service || !branchId || !dentistId || submitting || !validateContact()) return;
    setSubmitting(true); setError(null);
    try {
      const key = requestKey ?? uuid.v4();
      if (!requestKey) setRequestKey(key);
      const value = await bookingApi.create({
        branchId, booker: { name: bookerName.trim(), phone: /^0\d{9}$/.test(contact.trim()) ? contact.trim() : null,
          email: /^0\d{9}$/.test(contact.trim()) ? null : contact.trim() },
        attendee: { patientId: null, name: attendeeName.trim(), birthDate: null },
        attendeeRelation: relation, kind: 'Standard', source: 'App', serviceId: service.id,
        reason: reason.trim() || null, dentistId, assistantId: null, chairId: null,
        startAt: slot.start, durationMinutes: service.publicDurationMinutes, followUpId: null,
        exceptionReason: null, requestKey: key, scheduleVersion: slot.scheduleVersion,
        assistantScheduleVersion: null, bookingSettingsVersion: slot.bookingSettingsVersion,
      }, token);
      setResult(value); setStep(3);
      // S26+ — fix bug "đặt lịch xong list trống" + "thông báo biến mất":
      //  1. Inject appointment mới vào persist store để AppointmentHubScreen thấy ngay.
      //  2. Tạo notification 'AppointmentBooked' qua persist repo (mock cũ không persist).
      const accountId = state.status === 'authenticated' ? state.user.id : null;
      const branch = options?.branches.find(x => x.id === branchId);
      const dentist = options?.employees.find(x => x.id === dentistId);
      const svc = options?.services.find(x => x.id === serviceId);
      addMockBooking(value, {
        branchId: branchId ?? 0,
        branchName: branch?.name ?? '',
        dentistId: dentistId ?? 0,
        dentistName: dentist?.name ?? '',
        serviceId: serviceId ?? 0,
        serviceName: svc?.name ?? '',
        durationMinutes: svc?.publicDurationMinutes ?? 30,
        bookerName: bookerName.trim(),
        attendeeName: attendeeName.trim(),
        attendeeKind: relation === 'Self' ? 'SELF' : 'RELATIVE',
      });
      void appointmentRepo.addFromBooking(value, {
        accountId,
        bookerName: bookerName.trim(),
        attendeeName: attendeeName.trim(),
        branchName: branch?.name ?? '',
        dentistName: dentist?.name ?? '',
        serviceName: svc?.name ?? '',
        durationMinutes: svc?.publicDurationMinutes ?? 0,
        branchId: branchId ?? 0,
        dentistId: dentistId ?? 0,
        serviceId: serviceId ?? 0,
      }).catch(err => console.warn('[BookingScreen] appointmentRepo.addFromBooking failed', err));
      void notificationRepoPersist.create({
        accountId,
        kind: 'AppointmentBooked',
        title: 'Đặt lịch thành công',
        body: `Lịch khám ${value.code} đã được tạo. Trạng thái: ${value.status}.`,
        appointmentId: value.code,
        requestId: null,
        deepLink: accountId !== null ? `/appointments/${value.code}` : null,
        channels: accountId !== null ? ['InApp', 'Email'] : ['InApp'],
        dedupeKey: `appt:${value.code}:booked`,
      }).catch(err => console.warn('[BookingScreen] notification.create failed', err));
      // Lập reminder 24h/2h cho account (recheck khi load lại trang này).
      if (accountId !== null) {
        void notificationMockRepo.rebuildRemindersForAppointment(value.code, value.version);
      }
    } catch (e) {
      const code = e instanceof ApiError ? e.code : undefined;
      setError(e instanceof ApiError ? `${code ? `${code}: ` : ''}${e.message}` : 'Không thể kết nối máy chủ. Vui lòng thử lại.');
      if (code && ['MSG24', 'MSG25', 'MSG26', 'MSG27', 'MSG28', 'MSG111'].includes(code)) {
        setSlot(null); setStep(0);
        if (branchId && dentistId && service) {
          bookingApi.slots(branchId, dentistId, service.id, date, service.publicDurationMinutes, token)
            .then(value => setSlots(value.slots)).catch(() => setSlots([]));
        }
      }
    } finally { setSubmitting(false); }
  }

  if (loadingOptions) return <View style={styles.center}><ActivityIndicator color={colors.primary} /><Text>Đang tải dữ liệu đặt lịch…</Text></View>;

  return <SafeAreaView style={styles.safe} edges={['bottom']}>
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text style={styles.title}>Đặt lịch khám</Text>
      {error && <Text style={styles.error} accessibilityRole="alert">{error}</Text>}
      {step === 0 && <>
        <Text style={styles.section}>1. Chọn lịch</Text>
        <Text>Người khám</Text>
        <View style={styles.choices}>
          <Choice label="Bản thân" selected={relation === 'Self'} onPress={() => { if (relation !== 'Self') { setRelation('Self'); if (account) setAttendeeName(account.fullName); } }} />
          <Choice label="Người thân" selected={relation === 'Relative'} onPress={() => { if (relation !== 'Relative') { setRelation('Relative'); setAttendeeName(''); } }} />
        </View>
        <Text>Chi nhánh</Text>
        <View style={styles.choices}>{options?.branches.map(x => <Choice key={x.id} label={x.name} selected={branchId === x.id}
          onPress={() => { setBranchId(x.id); setDentistId(null); }} />)}</View>
        {!options?.branches.length && <Text>Chưa có chi nhánh nhận đặt lịch.</Text>}
        <Text>Dịch vụ</Text>
        <Text>{service ? `Khám · ${service.publicDurationMinutes} phút` : 'Chi nhánh chưa có dịch vụ Khám khả dụng.'}</Text>
        <Field label="Lý do khám (không bắt buộc)" value={reason} onChangeText={setReason} multiline />
        <Text>Bác sĩ</Text>
        <View style={styles.choices}>{dentists.map(x => <Choice key={x.id} label={x.name} selected={dentistId === x.id}
          onPress={() => setDentistId(x.id)} />)}</View>
        <Field label="Ngày khám (YYYY-MM-DD)" value={date} onChangeText={setDate} />
        <Text>Khung giờ · Asia/Ho_Chi_Minh</Text>
        {loadingSlots && <ActivityIndicator color={colors.primary} />}
        {!loadingSlots && branchId && dentistId && service && slots.length === 0 && <Text>Không có khung giờ phù hợp. Hãy chọn ngày hoặc bác sĩ khác.</Text>}
        <View style={styles.choices}>{slots.map(x => <Choice key={x.start} label={formatTime(x.start)} selected={slot?.start === x.start}
          onPress={() => chooseSlot(x)} />)}</View>
        <PrimaryButton label="Tiếp tục" disabled={!slot} onPress={() => { setError(null); setStep(1); }} />
      </>}
      {step === 1 && <>
        <Text style={styles.section}>2. Người đặt và người khám</Text>
        <Field label="Họ tên người đặt" value={bookerName} onChangeText={setBookerName} error={fieldErrors.bookerName} />
        <Field label="Số điện thoại Việt Nam hoặc email" value={contact} onChangeText={setContact} error={fieldErrors.contact} />
        <Field label="Họ tên người khám" value={attendeeName} onChangeText={setAttendeeName} error={fieldErrors.attendeeName} />
        {relation === 'Relative' && <Text>Đặt lịch cho người thân không cấp quyền xem hồ sơ bệnh án của họ.</Text>}
        <PrimaryButton label="Xem lại" onPress={() => { if (validateContact()) { setError(null); setStep(2); } }} />
        <Choice label="Quay lại chọn lịch" selected={false} onPress={() => setStep(0)} />
      </>}
      {step === 2 && <>
        <Text style={styles.section}>3. Xem lại và xác nhận</Text>
        <Text>Chi nhánh: {branch?.name}</Text><Text>Bác sĩ: {dentist?.name}</Text>
        <Text>Dịch vụ: {service?.name}</Text><Text>Thời gian: {slot && formatTime(slot.start)}</Text>
        <Text>Người đặt: {bookerName} · {contact}</Text><Text>Người khám: {attendeeName} ({relation === 'Self' ? 'Bản thân' : 'Người thân'})</Text>
        <PrimaryButton label={submitting ? 'Đang xác nhận…' : 'Xác nhận đặt lịch'} loading={submitting} onPress={() => void confirm()} />
        <Choice label="Sửa thông tin" selected={false} onPress={() => setStep(1)} />
      </>}
      {step === 3 && result && <>
        <Text style={styles.section}>Đặt lịch thành công</Text>
        <Text style={styles.code}>{result.code}</Text><Text>Trạng thái: {result.status}</Text>
        <Text>Thời gian: {formatTime(result.startAt)} – {formatTime(result.endAt)}</Text>
        <Text>Hãy lưu mã lịch hẹn để liên hệ phòng khám khi cần.</Text>
        <PrimaryButton
          label="Xem chi tiết lịch hẹn"
          onPress={() => router.replace(`/(account)/appointments/${encodeURIComponent(result.code)}` as never)}
        />
        <Choice label="Về danh sách lịch hẹn" selected={false} onPress={() => router.replace('/(account)/appointments')} />
      </>}
    </ScrollView>
  </SafeAreaView>;
}

function Choice({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return <Pressable accessibilityRole="button" accessibilityState={{ selected }} onPress={onPress}
    style={[styles.choice, selected && styles.selected]}><Text style={selected && styles.selectedText}>{label}</Text></Pressable>;
}

function Field({ label, value, onChangeText, error, multiline = false }: { label: string; value: string;
  onChangeText: (value: string) => void; error?: string; multiline?: boolean }) {
  return <View style={styles.field}><Text>{label}</Text><TextInput value={value} onChangeText={onChangeText}
    multiline={multiline} style={[styles.input, multiline && styles.multiline]} placeholder={label} />
    {error && <Text style={styles.error}>{error}</Text>}</View>;
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background }, content: { padding: 20, gap: 12, paddingBottom: 40 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12 },
  title: { fontSize: 24, fontWeight: '700', color: colors.text }, section: { fontSize: 18, fontWeight: '700', color: colors.text },
  choices: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 }, choice: { padding: 10, borderWidth: 1, borderColor: colors.border, borderRadius: 8 },
  selected: { borderColor: colors.primary, backgroundColor: colors.infoSoft }, selectedText: { color: colors.primary, fontWeight: '700' },
  field: { gap: 4 }, input: { borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: 10, color: colors.text },
  multiline: { minHeight: 64 }, error: { color: colors.danger }, code: { fontSize: 20, fontWeight: '700', color: colors.primary },
});
