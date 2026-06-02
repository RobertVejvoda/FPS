import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthContext';
import { submitBooking } from '@/api/bookings';
import { fetchDrawStatus, type DrawStatusResult } from '@/api/draws';
import { fetchProfileSnapshot, type ProfileSnapshot } from '@/api/profile';
import { formatBookingRef, formatCutOffAt, humanizeRejectionReason } from '@/displayLabels';
import { DEMO_FACILITY_ID, DEMO_LOCATION_ID, DEFAULT_TIME_SLOT_START, DEFAULT_TIME_SLOT_END } from '@/demoDefaults';
import { colors, radius, spacing } from '@/theme';

const VEHICLE_TYPES = ['Compact', 'Sedan', 'SUV', 'Van', 'Truck', 'Motorcycle'] as const;

// DEMO_FACILITY_ID and DEMO_LOCATION_ID are imported from @/demoDefaults

type FormState = {
  facilityId: string;
  locationId: string;
  selectedVehicleId: string;
  licensePlate: string;
  vehicleType: string;
  isElectric: boolean;
  requiresAccessibleSpot: boolean;
  isCompanyCar: boolean;
  dateOffset: number;
  arrivalHour: number;
  arrivalMinute: number;
  departureHour: number;
  departureMinute: number;
};

type FieldErrors = Partial<Record<'licensePlate' | 'vehicleType' | 'plannedDeparture', string>>;

type SubmitStatus =
  | { kind: 'idle' }
  | { kind: 'submitting' }
  | { kind: 'accepted'; requestId: string; requestedDate: string }
  | { kind: 'rejected'; rejectionCode: string | null; reason: string | null }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

const ARRIVAL_TIMES = [
  { hour: 6, minute: 0 }, { hour: 6, minute: 30 },
  { hour: 7, minute: 0 }, { hour: 7, minute: 30 },
  { hour: 8, minute: 0 }, { hour: 8, minute: 30 },
  { hour: 9, minute: 0 }, { hour: 9, minute: 30 },
  { hour: 10, minute: 0 }, { hour: 11, minute: 0 },
  { hour: 12, minute: 0 },
];

const DEPARTURE_TIMES = [
  { hour: 12, minute: 0 }, { hour: 13, minute: 0 },
  { hour: 14, minute: 0 }, { hour: 15, minute: 0 },
  { hour: 16, minute: 0 }, { hour: 17, minute: 0 },
  { hour: 17, minute: 30 }, { hour: 18, minute: 0 },
  { hour: 18, minute: 30 }, { hour: 19, minute: 0 },
  { hour: 20, minute: 0 },
];

function availableDates(): Array<{ offset: number; label: string }> {
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date();
    d.setDate(d.getDate() + i);
    return {
      offset: i,
      label: i === 0
        ? 'Today'
        : d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' }),
    };
  });
}

function formatTimeLabel(hour: number, minute: number): string {
  const ampm = hour >= 12 ? 'PM' : 'AM';
  const h = hour % 12 || 12;
  return `${h}:${String(minute).padStart(2, '0')} ${ampm}`;
}

function toISO(offsetDays: number, hour: number, minute: number): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  d.setHours(hour, minute, 0, 0);
  return d.toISOString();
}

function dateStrFromOffset(offset: number): string {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function initialForm(): FormState {
  return {
    facilityId: DEMO_FACILITY_ID,
    locationId: DEMO_LOCATION_ID,
    selectedVehicleId: '',
    licensePlate: '',
    vehicleType: 'Sedan',
    isElectric: false,
    requiresAccessibleSpot: false,
    isCompanyCar: false,
    dateOffset: 1,
    arrivalHour: 6,
    arrivalMinute: 0,
    departureHour: 20,
    departureMinute: 0,
  };
}

function validate(form: FormState): FieldErrors {
  const errors: FieldErrors = {};
  if (!form.licensePlate.trim()) errors.licensePlate = 'Select a vehicle or enter license plate';
  const arrivalMins = form.arrivalHour * 60 + form.arrivalMinute;
  const departureMins = form.departureHour * 60 + form.departureMinute;
  if (departureMins <= arrivalMins) {
    errors.plannedDeparture = 'Departure must be after arrival';
  }
  return errors;
}

export default function NewBookingRoute() {
  const router = useRouter();
  const { offset: offsetParam } = useLocalSearchParams<{ offset?: string }>();
  const { apiBaseUrl, bearerToken, clearSession } = useAuth();
  const [profile, setProfile] = useState<ProfileSnapshot | null>(null);
  const [profileLoading, setProfileLoading] = useState(true);
  const [form, setForm] = useState<FormState>(() => {
    const offset = offsetParam !== undefined ? Math.max(0, parseInt(offsetParam, 10) || 0) : 1;
    return { ...initialForm(), dateOffset: offset };
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>({ kind: 'idle' });
  const [drawStatus, setDrawStatus] = useState<DrawStatusResult | null>(null);
  const dates = availableDates();

  useEffect(() => {
    if (offsetParam === undefined) return;
    const parsed = Math.max(0, parseInt(offsetParam, 10) || 0);
    setForm(prev => ({ ...prev, dateOffset: parsed }));
  }, [offsetParam]);

  useEffect(() => {
    let cancelled = false;
    setDrawStatus(null);
    const date = dateStrFromOffset(form.dateOffset);
    fetchDrawStatus({ apiBaseUrl, bearerToken }, { date, locationId: DEMO_LOCATION_ID, timeSlotStart: DEFAULT_TIME_SLOT_START, timeSlotEnd: DEFAULT_TIME_SLOT_END }).then((res) => {
      if (!cancelled) setDrawStatus(res);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, form.dateOffset]);

  useEffect(() => {
    fetchProfileSnapshot({ apiBaseUrl, bearerToken }).then((res) => {
      if (res.kind === 'unauthenticated') {
        clearSession().then(() => router.replace('/login'));
        return;
      }
      if (res.kind === 'ok') setProfile(res.profile);
      setProfileLoading(false);
    });
  }, [apiBaseUrl, bearerToken, clearSession, router]);

  useEffect(() => {
    if (!profile) return;
    const active = profile.vehicles.filter(v => v.isActive);
    const preselect = active.find(v => v.isDefault) ?? (active.length === 1 ? active[0] : undefined);
    if (!preselect) return;
    setForm(prev => {
      if (prev.selectedVehicleId) return prev;
      return {
        ...prev,
        selectedVehicleId: preselect.vehicleId,
        licensePlate: preselect.licensePlate,
        vehicleType: preselect.vehicleType,
        isElectric: preselect.isElectric,
      };
    });
  }, [profile]);

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm(prev => ({ ...prev, [key]: value }));
    setFieldErrors(prev => ({ ...prev, [key]: undefined }));
  };

  const selectVehicle = (vehicleId: string) => {
    const vehicle = profile?.vehicles.find((v) => v.vehicleId === vehicleId);
    if (vehicle) {
      setForm(prev => ({
        ...prev,
        selectedVehicleId: vehicleId,
        licensePlate: vehicle.licensePlate,
        vehicleType: vehicle.vehicleType,
        isElectric: vehicle.isElectric,
      }));
      setFieldErrors(prev => ({ ...prev, licensePlate: undefined, vehicleType: undefined }));
    }
  };

  const handleSubmit = async () => {
    const errors = validate(form);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setSubmitStatus({ kind: 'submitting' });
    const requestedDate = dateStrFromOffset(form.dateOffset);
    const result = await submitBooking(
      { apiBaseUrl, bearerToken },
      {
        facilityId: form.facilityId,
        locationId: form.locationId || null,
        licensePlate: form.licensePlate.trim(),
        vehicleType: form.vehicleType,
        isElectric: form.isElectric,
        requiresAccessibleSpot: form.requiresAccessibleSpot,
        isCompanyCar: form.isCompanyCar,
        plannedArrivalTime: toISO(form.dateOffset, form.arrivalHour, form.arrivalMinute),
        plannedDepartureTime: toISO(form.dateOffset, form.departureHour, form.departureMinute),
      },
    );
    if (result.kind === 'unauthenticated') {
      await clearSession();
      router.replace('/login');
      return;
    }
    if (result.kind === 'accepted') {
      setSubmitStatus({ kind: 'accepted', requestId: result.requestId, requestedDate });
    } else {
      setSubmitStatus(result);
    }
  };

  if (submitStatus.kind === 'accepted') {
    const ref = formatBookingRef(submitStatus.requestId, submitStatus.requestedDate);
    return (
      <SafeAreaView style={styles.safe}>
        <View style={styles.successContainer}>
          <Text style={styles.successTitle}>Request submitted</Text>
          <Text style={styles.successBody}>
            Your spot request is waiting for the next allocation draw.
          </Text>
          <View style={styles.refCard}>
            <Text style={styles.refLabel}>Reference</Text>
            <Text style={styles.refValue}>{ref}</Text>
          </View>
          <Pressable
            style={({ pressed }) => [styles.primary, pressed && styles.primaryDimmed]}
            onPress={() => {
              setForm(initialForm());
              setFieldErrors({});
              setSubmitStatus({ kind: 'idle' });
            }}
            accessibilityRole="button"
          >
            <Text style={styles.primaryLabel}>New request</Text>
          </Pressable>
          <Pressable
            style={({ pressed }) => [styles.secondary, pressed && styles.secondaryDimmed]}
            onPress={() => router.push('/(tabs)/bookings')}
            accessibilityRole="button"
          >
            <Text style={styles.secondaryLabel}>My spots</Text>
          </Pressable>
        </View>
      </SafeAreaView>
    );
  }

  const isSubmitting = submitStatus.kind === 'submitting';

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
        <Text style={styles.heading}>Request a spot</Text>

        {profileLoading ? (
          <Text style={styles.mutedText}>Loading vehicles…</Text>
        ) : (
          <>
            <FieldRow label="Location">
              <View style={styles.readOnlyRow}>
                <Text style={styles.readOnlyValue}>Prague · Headquarters</Text>
              </View>
            </FieldRow>

            <FieldRow label="Date">
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chipRow}>
                {dates.map(({ offset, label }) => (
                  <Pressable
                    key={offset}
                    style={({ pressed }) => [
                      styles.chip,
                      form.dateOffset === offset && styles.chipActive,
                      pressed && styles.chipPressed,
                    ]}
                    onPress={() => set('dateOffset', offset)}
                    accessibilityRole="button"
                  >
                    <Text style={[styles.chipText, form.dateOffset === offset && styles.chipTextActive]}>
                      {label}
                    </Text>
                  </Pressable>
                ))}
              </ScrollView>
            </FieldRow>

            {/* Schedule banner (DRAW005) */}
            {drawStatus?.kind === 'ok' && (
              <View style={[styles.scheduleBanner,
                drawStatus.data.requestWindowStatus === 'open' ? styles.scheduleBannerOpen : styles.scheduleBannerClosed]}>
                <Text style={styles.scheduleText}>{drawStatus.data.safeMessage}</Text>
                {drawStatus.data.nextDrawAt && (
                  <Text style={styles.scheduleSubText}>
                    Next draw: {formatCutOffAt(drawStatus.data.nextDrawAt, drawStatus.data.timeZone)}
                  </Text>
                )}
                {drawStatus.data.cutOffAt && (
                  <Text style={styles.scheduleSubText}>
                    Cut-off: {formatCutOffAt(drawStatus.data.cutOffAt, drawStatus.data.timeZone)}
                  </Text>
                )}
              </View>
            )}

            <FieldRow label="Arrival time">
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chipRow}>
                {ARRIVAL_TIMES.map(({ hour, minute }) => {
                  const active = form.arrivalHour === hour && form.arrivalMinute === minute;
                  return (
                    <Pressable
                      key={`${hour}-${minute}`}
                      style={({ pressed }) => [styles.chip, active && styles.chipActive, pressed && styles.chipPressed]}
                      onPress={() => { set('arrivalHour', hour); set('arrivalMinute', minute); }}
                      accessibilityRole="button"
                    >
                      <Text style={[styles.chipText, active && styles.chipTextActive]}>
                        {formatTimeLabel(hour, minute)}
                      </Text>
                    </Pressable>
                  );
                })}
              </ScrollView>
            </FieldRow>

            <FieldRow label="Departure time" error={fieldErrors.plannedDeparture}>
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chipRow}>
                {DEPARTURE_TIMES.map(({ hour, minute }) => {
                  const active = form.departureHour === hour && form.departureMinute === minute;
                  return (
                    <Pressable
                      key={`${hour}-${minute}`}
                      style={({ pressed }) => [styles.chip, active && styles.chipActive, pressed && styles.chipPressed]}
                      onPress={() => { set('departureHour', hour); set('departureMinute', minute); }}
                      accessibilityRole="button"
                    >
                      <Text style={[styles.chipText, active && styles.chipTextActive]}>
                        {formatTimeLabel(hour, minute)}
                      </Text>
                    </Pressable>
                  );
                })}
              </ScrollView>
            </FieldRow>

            {profile && profile.vehicles.filter(v => v.isActive).length > 0 ? (
              <FieldRow label="Vehicle" error={fieldErrors.licensePlate}>
                <View style={styles.vehicleList}>
                  {profile.vehicles.filter(v => v.isActive).map((v) => (
                    <Pressable
                      key={v.vehicleId}
                      style={({ pressed }) => [
                        styles.vehicleCard,
                        form.selectedVehicleId === v.vehicleId && styles.vehicleCardActive,
                        pressed && styles.vehicleCardPressed,
                      ]}
                      onPress={() => selectVehicle(v.vehicleId)}
                      accessibilityRole="button"
                    >
                      <Text style={styles.vehicleCardPlate}>{v.licensePlate}</Text>
                      <Text style={styles.vehicleCardMeta}>
                        {v.vehicleType} · {v.isElectric ? 'Electric' : 'Standard'}
                      </Text>
                    </Pressable>
                  ))}
                </View>
              </FieldRow>
            ) : (
              <>
                <FieldRow label="License plate" error={fieldErrors.licensePlate}>
                  <TextInput
                    style={[styles.input, fieldErrors.licensePlate ? styles.inputError : null]}
                    value={form.licensePlate}
                    onChangeText={v => set('licensePlate', v)}
                    placeholder="e.g. ABC123"
                    placeholderTextColor={colors.textMuted}
                    autoCapitalize="characters"
                  />
                  <Text style={styles.hint}>
                    No vehicles in profile. Add vehicles in More → Your Vehicles to speed up spot requests.
                  </Text>
                </FieldRow>

                <FieldRow label="Vehicle type" error={fieldErrors.vehicleType}>
                  <View style={styles.pills}>
                    {VEHICLE_TYPES.map(vt => (
                      <Pressable
                        key={vt}
                        style={({ pressed }) => [
                          styles.pill,
                          form.vehicleType === vt && styles.pillActive,
                          pressed && styles.pillPressed,
                        ]}
                        onPress={() => set('vehicleType', vt)}
                        accessibilityRole="button"
                      >
                        <Text style={[styles.pillText, form.vehicleType === vt && styles.pillTextActive]}>
                          {vt}
                        </Text>
                      </Pressable>
                    ))}
                  </View>
                </FieldRow>
              </>
            )}

            {form.selectedVehicleId ? null : (
              <ToggleRow
                label="Electric vehicle"
                hint="Enables EV charging spot allocation when available."
                value={form.isElectric}
                onValueChange={v => set('isElectric', v)}
              />
            )}
            <ToggleRow
              label="Accessible spot required"
              hint="Requests a space close to an entrance or lift."
              value={form.requiresAccessibleSpot}
              onValueChange={v => set('requiresAccessibleSpot', v)}
            />
            <ToggleRow
              label="Company car"
              hint="Indicates this vehicle is owned or leased by your employer."
              value={form.isCompanyCar}
              onValueChange={v => set('isCompanyCar', v)}
            />
          </>
        )}

        {submitStatus.kind === 'rejected' && (
          <View style={styles.rejectionBox}>
            <Text style={styles.rejectionTitle}>Request not fulfilled</Text>
            <Text style={styles.rejectionText}>
              {humanizeRejectionReason(submitStatus.rejectionCode, submitStatus.reason)}
            </Text>
          </View>
        )}

        {(submitStatus.kind === 'unreachable' || submitStatus.kind === 'error') ? (
          <Text style={styles.errorText}>
            {submitStatus.kind === 'unreachable'
              ? submitStatus.message
              : `Something went wrong. Please try again.`}
          </Text>
        ) : null}

        <Pressable
          style={({ pressed }) => [styles.primary, (isSubmitting || pressed) && styles.primaryDimmed]}
          disabled={isSubmitting}
          onPress={handleSubmit}
          accessibilityRole="button"
          testID="button-submit"
        >
          {isSubmitting ? (
            <ActivityIndicator color={colors.primaryText} />
          ) : (
            <Text style={styles.primaryLabel}>Submit request</Text>
          )}
        </Pressable>
      </ScrollView>
    </SafeAreaView>
  );
}

function FieldRow({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      {children}
      {error ? <Text style={styles.fieldError}>{error}</Text> : null}
    </View>
  );
}

function ToggleRow({
  label,
  hint,
  value,
  onValueChange,
}: {
  label: string;
  hint?: string;
  value: boolean;
  onValueChange: (v: boolean) => void;
}) {
  return (
    <View style={styles.toggleRow}>
      <View style={styles.toggleLabelWrap}>
        <Text style={styles.label}>{label}</Text>
        {hint ? <Text style={styles.hint}>{hint}</Text> : null}
      </View>
      <Switch
        value={value}
        onValueChange={onValueChange}
        trackColor={{ true: colors.primary, false: colors.border }}
        thumbColor={colors.primaryText}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { padding: spacing.lg, gap: spacing.md, flexGrow: 1 },
  heading: { fontSize: 22, fontWeight: '700', color: colors.text },
  mutedText: { fontSize: 14, color: colors.textMuted },
  field: { gap: spacing.xs },
  label: { fontSize: 13, color: colors.textMuted, fontWeight: '500' },
  hint: { fontSize: 12, color: colors.textMuted, marginTop: 2 },
  fieldError: { fontSize: 12, color: colors.danger },
  readOnlyRow: {
    backgroundColor: colors.cardBackground,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.sm,
  },
  readOnlyValue: { fontSize: 14, color: colors.text },
  chipRow: { flexDirection: 'row', gap: spacing.xs, paddingVertical: spacing.xs },
  chip: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    backgroundColor: colors.cardBackground,
  },
  chipActive: { borderColor: colors.primary, backgroundColor: colors.primary },
  chipPressed: { opacity: 0.7 },
  chipText: { fontSize: 13, color: colors.text, fontWeight: '500' },
  chipTextActive: { color: colors.primaryText, fontWeight: '600' },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 15,
    color: colors.text,
    backgroundColor: colors.cardBackground,
  },
  inputError: { borderColor: colors.danger },
  vehicleList: { gap: spacing.sm },
  vehicleCard: {
    borderWidth: 2,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    backgroundColor: colors.cardBackground,
  },
  vehicleCardActive: { borderColor: colors.primary, backgroundColor: '#eff6ff' },
  vehicleCardPressed: { opacity: 0.7 },
  vehicleCardPlate: { fontSize: 16, fontWeight: '600', color: colors.text },
  vehicleCardMeta: { fontSize: 13, color: colors.textMuted, marginTop: spacing.xs },
  pills: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs },
  pill: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    backgroundColor: colors.cardBackground,
  },
  pillActive: { borderColor: colors.primary, backgroundColor: colors.primary },
  pillPressed: { opacity: 0.7 },
  pillText: { fontSize: 13, color: colors.text },
  pillTextActive: { color: colors.primaryText, fontWeight: '600' },
  toggleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: spacing.xs,
    gap: spacing.md,
  },
  toggleLabelWrap: { flex: 1, gap: 2 },
  primary: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
    minHeight: 48,
    justifyContent: 'center',
    marginTop: spacing.sm,
  },
  primaryDimmed: { opacity: 0.5 },
  primaryLabel: { color: colors.primaryText, fontWeight: '700', fontSize: 16 },
  secondary: {
    borderWidth: 1,
    borderColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
    minHeight: 48,
    justifyContent: 'center',
  },
  secondaryDimmed: { opacity: 0.5 },
  secondaryLabel: { color: colors.primary, fontWeight: '600', fontSize: 16 },
  successContainer: { flex: 1, padding: spacing.lg, gap: spacing.md, justifyContent: 'center' },
  successTitle: { fontSize: 24, fontWeight: '700', color: colors.text, textAlign: 'center' },
  successBody: { fontSize: 15, color: colors.textMuted, textAlign: 'center', lineHeight: 22 },
  refCard: {
    backgroundColor: colors.cardBackground,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    alignItems: 'center',
    gap: spacing.xs,
  },
  refLabel: { fontSize: 12, color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0.5 },
  refValue: { fontSize: 18, fontWeight: '700', color: colors.text, letterSpacing: 1 },
  rejectionBox: {
    backgroundColor: '#fef2f2',
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: '#fecaca',
    gap: spacing.xs,
  },
  rejectionTitle: { fontSize: 14, fontWeight: '600', color: colors.danger },
  rejectionText: { fontSize: 13, color: colors.danger, lineHeight: 18 },
  errorText: { fontSize: 13, color: colors.danger, textAlign: 'center' },
  scheduleBanner: { borderRadius: radius.md, padding: spacing.sm, borderWidth: 1 },
  scheduleBannerOpen: { backgroundColor: '#f0fdf4', borderColor: '#bbf7d0' },
  scheduleBannerClosed: { backgroundColor: '#f8fafc', borderColor: colors.border },
  scheduleText: { fontSize: 13, color: colors.text, lineHeight: 18 },
  scheduleSubText: { fontSize: 12, color: colors.textMuted, marginTop: 2 },
});
