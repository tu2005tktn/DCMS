import { useRouter } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { uuid } from 'expo-modules-core';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { useSession } from '@/features/auth/session/useSession';
import { ApiError } from '@/services/api/api-error';
import { colors } from '@/theme/colors';
import { addDays, clinicDate } from './appointments.rules';
import { bookingApi } from './booking.api';
import { notificationMockRepo } from './notification.mock';
import { notificationRepoPersist } from './notification.repo.persist';
import { appointmentRepo } from './appointment.repo';
import { addMockBooking } from './appointments.mock';
import type { AvailabilitySlot, BookingOptions, BookingResult } from './booking.types';

type Relation = 'Self' | 'Relative';
type FieldName = 'bookerName' | 'contact' | 'attendeeName';
const nextDate = () => addDays(clinicDate(new Date()), 1);
const shortDate = (value: string) => { const [, month, day] = value.split('-'); return `${day}/${month}`; };
const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

export default function BookingScreen() {
  const router = useRouter();
  const { state } = useSession();
  const token = state.status === 'authenticated' ? state.accessToken : undefined;
  const [options, setOptions] = useState<BookingOptions | null>(null);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [optionsRevision, setOptionsRevision] = useState(0);
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
  }, [token, optionsRevision]);

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

  if (loadingOptions) return <View style={styles.center}><ActivityIndicator color={colors.primary} /><Text style={styles.muted}>Đang tải thông tin đặt lịch…</Text></View>;

  return <SafeAreaView style={styles.safe} edges={['bottom']}>
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Pressable accessibilityRole="button" onPress={() => router.replace('/(home)/')} style={styles.backLink}><Text style={styles.backLinkText}>← Về trang chủ</Text></Pressable>
      <Text style={styles.title}>Đặt lịch khám</Text>
      <Text style={styles.muted}>Chọn thời gian phù hợp, điền thông tin rồi xác nhận lịch hẹn.</Text>
      <View style={styles.steps}>{['Chọn lịch', 'Thông tin', 'Xác nhận'].map((label, index) =>
        <View key={label} style={[styles.step, step === index && styles.stepCurrent, step > index && styles.stepDone]}>
          <Text style={[styles.stepText, step === index && styles.stepTextCurrent]}>{index + 1}. {label}</Text>
        </View>)}</View>
      {error && <Text style={styles.error} accessibilityRole="alert">{error}</Text>}
      {!options && !loadingOptions && <PrimaryButton label="Tải lại chi nhánh và dịch vụ" onPress={() => setOptionsRevision(value => value + 1)} />}
      {step === 0 && options && <>
        <Text style={styles.section}>Chọn lịch khám</Text>
        <Text style={styles.label}>Người khám</Text>
        <View style={styles.choices}>
          <Choice label="Bản thân" selected={relation === 'Self'} onPress={() => { if (relation !== 'Self') { setRelation('Self'); if (account) setAttendeeName(account.fullName); } }} />
          <Choice label="Người thân" selected={relation === 'Relative'} onPress={() => { if (relation !== 'Relative') { setRelation('Relative'); setAttendeeName(''); } }} />
        </View>
        <Text style={styles.label}>Chi nhánh</Text>
        <View style={styles.choices}>{options?.branches.map(x => <Choice key={x.id} label={x.name} selected={branchId === x.id}
          onPress={() => { setBranchId(x.id); setDentistId(null); }} />)}</View>
        {!options?.branches.length && <Text>Chưa có chi nhánh nhận đặt lịch.</Text>}
        <Text style={styles.label}>Dịch vụ</Text>
        <Text style={styles.muted}>{service ? `Khám · ${service.publicDurationMinutes} phút` : 'Chi nhánh chưa có dịch vụ Khám khả dụng.'}</Text>
        <Field label="Lý do khám (không bắt buộc)" value={reason} onChangeText={setReason} multiline />
        <Text style={styles.label}>Nha sĩ</Text>
        <View style={styles.choices}>{dentists.map(x => <Choice key={x.id} label={x.name} selected={dentistId === x.id}
          onPress={() => setDentistId(x.id)} />)}</View>
        <Text style={styles.label}>Ngày khám gần nhất</Text>
        <View style={styles.choices}>{Array.from({ length: 7 }, (_, index) => addDays(nextDate(), index)).map(day =>
          <Choice key={day} label={shortDate(day)} selected={date === day} onPress={() => setDate(day)} />)}</View>
        <Field label="Hoặc nhập ngày khác (YYYY-MM-DD)" value={date} onChangeText={setDate} />
        <Text style={styles.label}>Khung giờ · giờ Việt Nam</Text>
        {loadingSlots && <ActivityIndicator color={colors.primary} />}
        {!loadingSlots && branchId && dentistId && service && slots.length === 0 && <Text>Không có khung giờ phù hợp. Hãy chọn ngày hoặc bác sĩ khác.</Text>}
        <View style={styles.choices}>{slots.map(x => <Choice key={x.start} label={formatTime(x.start)} selected={slot?.start === x.start}
          onPress={() => chooseSlot(x)} />)}</View>
        <PrimaryButton label="Tiếp tục" disabled={!slot} onPress={() => { setError(null); setStep(1); }} />
      </>}
      {step === 1 && <>
        <Text style={styles.section}>Thông tin người đặt và người khám</Text>
        <Text style={styles.muted}>Phòng khám sẽ dùng thông tin này để xác nhận lịch hẹn.</Text>
        <Field label="Họ tên người đặt" value={bookerName} onChangeText={setBookerName} error={fieldErrors.bookerName} />
        <Field label="Số điện thoại Việt Nam hoặc email" value={contact} onChangeText={setContact} error={fieldErrors.contact} />
        <Field label="Họ tên người khám" value={attendeeName} onChangeText={setAttendeeName} error={fieldErrors.attendeeName} />
        {relation === 'Relative' && <Text>Đặt lịch cho người thân không cấp quyền xem hồ sơ bệnh án của họ.</Text>}
        <PrimaryButton label="Xem lại" onPress={() => { if (validateContact()) { setError(null); setStep(2); } }} />
        <Choice label="Quay lại chọn lịch" selected={false} onPress={() => setStep(0)} />
      </>}
      {step === 2 && <>
        <Text style={styles.section}>Kiểm tra lịch hẹn</Text>
        <View style={styles.summary}>
          <Summary label="Chi nhánh" value={branch?.name} /><Summary label="Nha sĩ" value={dentist?.name} />
          <Summary label="Dịch vụ" value={service?.name} /><Summary label="Thời gian" value={slot ? formatTime(slot.start) : undefined} />
          <Summary label="Người đặt" value={`${bookerName} · ${contact}`} />
          <Summary label="Người khám" value={`${attendeeName} (${relation === 'Self' ? 'Bản thân' : 'Người thân'})`} />
        </View>
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

function Summary({ label, value }: { label: string; value?: string }) {
  return <View style={styles.summaryRow}><Text style={styles.summaryLabel}>{label}</Text><Text style={styles.summaryValue}>{value || '—'}</Text></View>;
}

function Field({ label, value, onChangeText, error, multiline = false }: { label: string; value: string;
  onChangeText: (value: string) => void; error?: string; multiline?: boolean }) {
  return <View style={styles.field}><Text style={styles.label}>{label}</Text><TextInput value={value} onChangeText={onChangeText}
    multiline={multiline} style={[styles.input, multiline && styles.multiline]} placeholder={label} placeholderTextColor={colors.muted} accessibilityLabel={label} />
    {error && <Text style={styles.error}>{error}</Text>}</View>;
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background }, content: { padding: 20, gap: 14, paddingBottom: 40 },
  backLink: { minHeight: 44, justifyContent: 'center', alignSelf: 'flex-start' }, backLinkText: { color: colors.primary, fontWeight: '700' },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12 },
  title: { fontSize: 28, fontWeight: '800', color: colors.text }, section: { fontSize: 19, fontWeight: '800', color: colors.text, marginTop: 8 },
  muted: { color: colors.muted, lineHeight: 21 }, label: { color: colors.text, fontWeight: '700' },
  steps: { flexDirection: 'row', gap: 6, marginVertical: 8 }, step: { flex: 1, borderRadius: 10, minHeight: 44, alignItems: 'center', justifyContent: 'center', backgroundColor: '#E6ECF4' },
  stepCurrent: { backgroundColor: colors.primary }, stepDone: { backgroundColor: colors.infoSoft }, stepText: { color: colors.muted, fontSize: 12, fontWeight: '700' }, stepTextCurrent: { color: colors.onPrimary },
  choices: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 }, choice: { minHeight: 44, justifyContent: 'center', paddingHorizontal: 14, borderWidth: 1, borderColor: colors.border, borderRadius: 12, backgroundColor: colors.surface },
  selected: { borderColor: colors.primary, backgroundColor: colors.infoSoft }, selectedText: { color: colors.primary, fontWeight: '700' },
  field: { gap: 6 }, input: { minHeight: 48, borderWidth: 1, borderColor: colors.border, borderRadius: 12, padding: 12, color: colors.text, backgroundColor: colors.surface },
  multiline: { minHeight: 88, textAlignVertical: 'top' }, error: { color: colors.danger }, code: { fontSize: 24, fontWeight: '800', color: colors.primary },
  summary: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: 16, padding: 16, gap: 12 },
  summaryRow: { gap: 3, borderBottomWidth: 1, borderBottomColor: colors.border, paddingBottom: 8 }, summaryLabel: { color: colors.muted, fontSize: 13 }, summaryValue: { color: colors.text, fontSize: 16, fontWeight: '700' },
});
