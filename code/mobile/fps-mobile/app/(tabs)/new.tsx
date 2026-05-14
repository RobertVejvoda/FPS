import { useRouter } from 'expo-router';
import { useState } from 'react';
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
import { colors, radius, spacing } from '@/theme';

const VEHICLE_TYPES = ['Compact', 'Sedan', 'SUV', 'Van', 'Truck', 'Motorcycle'] as const;

type FormState = {
  facilityId: string;
  locationId: string;
  licensePlate: string;
  vehicleType: string;
  isElectric: boolean;
  requiresAccessibleSpot: boolean;
  isCompanyCar: boolean;
  plannedArrival: string;
  plannedDeparture: string;
};

const INITIAL_FORM: FormState = {
  facilityId: '',
  locationId: '',
  licensePlate: '',
  vehicleType: 'Sedan',
  isElectric: false,
  requiresAccessibleSpot: false,
  isCompanyCar: false,
  plannedArrival: '',
  plannedDeparture: '',
};

type FieldErrors = Partial<Record<keyof FormState, string>>;

type SubmitStatus =
  | { kind: 'idle' }
  | { kind: 'submitting' }
  | { kind: 'accepted'; requestId: string; status: string }
  | { kind: 'rejected'; rejectionCode: string | null; reason: string | null }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

function parseDatetime(input: string): string | null {
  const m = input.trim().match(/^(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2})$/);
  if (!m) return null;
  const [, y, mo, d, h, mi] = m;
  const year = Number(y);
  const month = Number(mo);
  const day = Number(d);
  const hour = Number(h);
  const minute = Number(mi);
  if (month < 1 || month > 12 || hour > 23 || minute > 59) return null;

  const date = new Date(year, month - 1, day, hour, minute, 0);
  if (
    date.getFullYear() !== year ||
    date.getMonth() !== month - 1 ||
    date.getDate() !== day ||
    date.getHours() !== hour ||
    date.getMinutes() !== minute
  ) {
    return null;
  }
  return date.toISOString();
}

function validate(form: FormState): FieldErrors {
  const errors: FieldErrors = {};
  if (!form.facilityId.trim()) errors.facilityId = 'Required';
  if (!form.licensePlate.trim()) errors.licensePlate = 'Required';
  const arrival = parseDatetime(form.plannedArrival);
  const departure = parseDatetime(form.plannedDeparture);
  if (!arrival) errors.plannedArrival = 'Use YYYY-MM-DD HH:MM';
  if (!departure) errors.plannedDeparture = 'Use YYYY-MM-DD HH:MM';
  if (arrival && departure && departure <= arrival) {
    errors.plannedDeparture = 'Must be after arrival';
  }
  return errors;
}

export default function NewBookingRoute() {
  const router = useRouter();
  const { apiBaseUrl, bearerToken, clearSession } = useAuth();
  const [form, setForm] = useState<FormState>(INITIAL_FORM);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [submitStatus, setSubmitStatus] = useState<SubmitStatus>({ kind: 'idle' });

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm(prev => ({ ...prev, [key]: value }));
    setFieldErrors(prev => ({ ...prev, [key]: undefined }));
  };

  const handleSubmit = async () => {
    const errors = validate(form);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setSubmitStatus({ kind: 'submitting' });
    const result = await submitBooking(
      { apiBaseUrl, bearerToken },
      {
        facilityId: form.facilityId.trim(),
        locationId: form.locationId.trim() || null,
        licensePlate: form.licensePlate.trim(),
        vehicleType: form.vehicleType,
        isElectric: form.isElectric,
        requiresAccessibleSpot: form.requiresAccessibleSpot,
        isCompanyCar: form.isCompanyCar,
        plannedArrivalTime: parseDatetime(form.plannedArrival)!,
        plannedDepartureTime: parseDatetime(form.plannedDeparture)!,
      },
    );
    if (result.kind === 'unauthenticated') {
      await clearSession();
      router.replace('/login');
      return;
    }
    setSubmitStatus(result);
  };

  if (submitStatus.kind === 'accepted') {
    return (
      <SafeAreaView style={styles.safe}>
        <View style={styles.successContainer}>
          <Text style={styles.successTitle}>Booking Submitted</Text>
          <Text style={styles.successMeta}>Request ID: {submitStatus.requestId}</Text>
          <Text style={styles.successMeta}>Status: {submitStatus.status}</Text>
          <Pressable
            style={({ pressed }) => [styles.primary, pressed && styles.primaryDimmed]}
            onPress={() => {
              setForm(INITIAL_FORM);
              setFieldErrors({});
              setSubmitStatus({ kind: 'idle' });
            }}
            accessibilityRole="button"
          >
            <Text style={styles.primaryLabel}>New Booking</Text>
          </Pressable>
          <Pressable
            style={({ pressed }) => [styles.secondary, pressed && styles.secondaryDimmed]}
            onPress={() => router.push('/(tabs)/bookings')}
            accessibilityRole="button"
          >
            <Text style={styles.secondaryLabel}>My Bookings</Text>
          </Pressable>
        </View>
      </SafeAreaView>
    );
  }

  const isSubmitting = submitStatus.kind === 'submitting';

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
        <Text style={styles.heading}>New Booking</Text>

        <FieldRow label="Facility ID *" error={fieldErrors.facilityId}>
          <TextInput
            style={[styles.input, fieldErrors.facilityId ? styles.inputError : null]}
            value={form.facilityId}
            onChangeText={v => set('facilityId', v)}
            placeholder="e.g. FAC-001"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="none"
          />
        </FieldRow>

        <FieldRow label="Location ID" error={fieldErrors.locationId}>
          <TextInput
            style={styles.input}
            value={form.locationId}
            onChangeText={v => set('locationId', v)}
            placeholder="Optional"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="none"
          />
        </FieldRow>

        <FieldRow label="License Plate *" error={fieldErrors.licensePlate}>
          <TextInput
            style={[styles.input, fieldErrors.licensePlate ? styles.inputError : null]}
            value={form.licensePlate}
            onChangeText={v => set('licensePlate', v)}
            placeholder="e.g. ABC123"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="characters"
          />
        </FieldRow>

        <FieldRow label="Vehicle Type *" error={fieldErrors.vehicleType}>
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

        <FieldRow label="Planned Arrival *" error={fieldErrors.plannedArrival}>
          <TextInput
            style={[styles.input, fieldErrors.plannedArrival ? styles.inputError : null]}
            value={form.plannedArrival}
            onChangeText={v => set('plannedArrival', v)}
            placeholder="YYYY-MM-DD HH:MM"
            placeholderTextColor={colors.textMuted}
            keyboardType="numbers-and-punctuation"
          />
        </FieldRow>

        <FieldRow label="Planned Departure *" error={fieldErrors.plannedDeparture}>
          <TextInput
            style={[styles.input, fieldErrors.plannedDeparture ? styles.inputError : null]}
            value={form.plannedDeparture}
            onChangeText={v => set('plannedDeparture', v)}
            placeholder="YYYY-MM-DD HH:MM"
            placeholderTextColor={colors.textMuted}
            keyboardType="numbers-and-punctuation"
          />
        </FieldRow>

        <ToggleRow
          label="Electric vehicle"
          value={form.isElectric}
          onValueChange={v => set('isElectric', v)}
        />
        <ToggleRow
          label="Requires accessible spot"
          value={form.requiresAccessibleSpot}
          onValueChange={v => set('requiresAccessibleSpot', v)}
        />
        <ToggleRow
          label="Company car"
          value={form.isCompanyCar}
          onValueChange={v => set('isCompanyCar', v)}
        />

        {submitStatus.kind === 'rejected' && (
          <View style={styles.rejectionBox}>
            <Text style={styles.rejectionTitle}>Booking rejected</Text>
            {submitStatus.rejectionCode ? (
              <Text style={styles.rejectionText}>Code: {submitStatus.rejectionCode}</Text>
            ) : null}
            {submitStatus.reason ? (
              <Text style={styles.rejectionText}>{submitStatus.reason}</Text>
            ) : null}
          </View>
        )}

        {(submitStatus.kind === 'unreachable' || submitStatus.kind === 'error') ? (
          <Text style={styles.errorText}>
            {submitStatus.kind === 'unreachable'
              ? submitStatus.message
              : `Error ${submitStatus.status}: ${submitStatus.message}`}
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
            <Text style={styles.primaryLabel}>Submit Booking</Text>
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
  value,
  onValueChange,
}: {
  label: string;
  value: boolean;
  onValueChange: (v: boolean) => void;
}) {
  return (
    <View style={styles.toggleRow}>
      <Text style={styles.label}>{label}</Text>
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
  field: { gap: spacing.xs },
  label: { fontSize: 13, color: colors.textMuted, fontWeight: '500' },
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
  fieldError: { fontSize: 12, color: colors.danger },
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
  },
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
  successContainer: {
    flex: 1,
    padding: spacing.lg,
    gap: spacing.md,
    justifyContent: 'center',
  },
  successTitle: { fontSize: 24, fontWeight: '700', color: colors.text, textAlign: 'center' },
  successMeta: { fontSize: 14, color: colors.textMuted, textAlign: 'center' },
  rejectionBox: {
    backgroundColor: '#fef2f2',
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: '#fecaca',
    gap: spacing.xs,
  },
  rejectionTitle: { fontSize: 14, fontWeight: '600', color: colors.danger },
  rejectionText: { fontSize: 13, color: colors.danger },
  errorText: { fontSize: 13, color: colors.danger, textAlign: 'center' },
});
