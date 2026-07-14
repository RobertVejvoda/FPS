import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { useSession } from '@/api/useSession';
import { useProfileSnapshot } from '@/api/useProfileSnapshot';
import type { VehicleSnapshot } from '@/api/profile';
import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';
import { PlaceholderCard } from '@/components/PlaceholderCard';
import { formatRoles, displayVehicleType } from '@/displayLabels';
import { t, useLocale, type Locale } from '@/i18n';
import { colors, radius, spacing } from '@/theme';

export default function MoreRoute() {
  const router = useRouter();
  const { clearSession, signOut } = useAuth();
  const { state: sessionState } = useSession();
  const { state: profileState, refresh } = useProfileSnapshot();

  useEffect(() => {
    if (profileState.kind === 'unauthenticated') {
      clearSession().then(() => router.replace('/login'));
    }
  }, [profileState.kind, clearSession, router]);

  if (sessionState.kind === 'idle' || sessionState.kind === 'loading') {
    return (
      <Screen>
        <StateView kind="loading" title={t('common.loading')} />
      </Screen>
    );
  }

  if (sessionState.kind === 'unauthenticated') {
    return (
      <Screen>
        <StateView
          kind="unauthenticated"
          title={t('session.notSignedIn')}
          message={t('session.expiredMessage')}
          actionLabel={t('session.signIn')}
          onAction={() => router.replace('/login')}
        />
      </Screen>
    );
  }

  if (sessionState.kind === 'unreachable' || sessionState.kind === 'error') {
    return (
      <Screen>
        <StateView
          kind={sessionState.kind}
          title={t('more.account.cannotLoad')}
          message={t('common.checkConnection')}
        />
      </Screen>
    );
  }

  const { me } = sessionState;

  return (
    <Screen scroll>
      <Text style={styles.heading}>{t('more.heading')}</Text>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>{t('more.account.title')}</Text>
        <FactRow label={t('more.account.role')} value={formatRoles(me.roles ?? [])} />
      </View>

      <LanguageRow />

      <EligibilityPanel state={profileState} onRetry={refresh} />

      <PlaceholderCard
        title={t('more.notificationPrefs.title')}
        description={t('more.notificationPrefs.description')}
      />

      <AboutCard />

      <Pressable
        accessibilityRole="button"
        accessibilityLabel={t('session.signOut')}
        onPress={async () => {
          await signOut();
          router.replace('/login');
        }}
        style={({ pressed }) => [styles.signOut, pressed && styles.signOutPressed]}
        testID="button-sign-out"
      >
        <Text style={styles.signOutLabel}>{t('session.signOut')}</Text>
      </Pressable>
    </Screen>
  );
}

// Language selector row (LOC001 #744). The row label is shown in both
// languages at once ('Language / Jazyk') so it stays legible even to a user
// currently stuck in the wrong language — it is intentionally not looked up
// through the catalog. Each option always shows its own language's name,
// matching standard language-picker convention.
function LanguageRow() {
  const { locale, setLocale } = useLocale();

  return (
    <View style={styles.card} testID="language-row">
      <Text style={styles.cardTitle}>Language / Jazyk</Text>
      <View style={styles.languageOptions}>
        <LanguageOption code="en" label={t('more.language.english')} active={locale === 'en'} onPress={setLocale} />
        <LanguageOption code="cs" label={t('more.language.czech')} active={locale === 'cs'} onPress={setLocale} />
      </View>
    </View>
  );
}

function LanguageOption({ code, label, active, onPress }: { code: Locale; label: string; active: boolean; onPress: (locale: Locale) => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected: active }}
      onPress={() => onPress(code)}
      style={({ pressed }) => [styles.languageOption, active && styles.languageOptionActive, pressed && { opacity: 0.7 }]}
      testID={`language-option-${code}`}
    >
      <Text style={[styles.languageOptionText, active && styles.languageOptionTextActive]}>{label}</Text>
    </Pressable>
  );
}

function AboutCard() {
  return (
    <View style={styles.card}>
      <Text style={styles.cardTitle}>{t('more.about.title')}</Text>
      <Text style={styles.bodyText}>{t('more.about.license')}</Text>
      <Text style={styles.bodyText}>{t('more.about.copyright')}</Text>
      <Text style={styles.bodyText}>
        {t('more.about.trademark')}
      </Text>
      <Pressable
        accessibilityRole="link"
        accessibilityLabel={t('more.about.sourceLinkLabel')}
        onPress={() => { void Linking.openURL('https://github.com/RobertVejvoda/fairspot'); }}
        style={({ pressed }) => [styles.link, pressed && { opacity: 0.6 }]}
      >
        <Text style={styles.linkText}>{t('more.about.sourceLinkText')}</Text>
      </Pressable>
    </View>
  );
}

function EligibilityPanel({
  state,
  onRetry,
}: {
  state: ReturnType<typeof useProfileSnapshot>['state'];
  onRetry: () => void;
}) {
  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <View style={styles.card}>
        <StateView kind="loading" title={t('more.eligibility.loadingProfile')} />
      </View>
    );
  }

  if (state.kind === 'unauthenticated') return null;

  if (state.kind === 'notFound') {
    return (
      <PlaceholderCard
        title={t('more.eligibility.unavailableTitle')}
        description={t('more.eligibility.unavailableMessage')}
        testID="profile-snapshot-not-found"
      />
    );
  }

  if (state.kind === 'unreachable' || state.kind === 'error') {
    return (
      <View style={styles.card}>
        <StateView
          kind={state.kind}
          title={t('more.eligibility.errorTitle')}
          message={t('common.checkConnection')}
          actionLabel={t('common.retry')}
          onAction={onRetry}
          testID="profile-snapshot-error"
        />
      </View>
    );
  }

  const { profile } = state;
  return (
    <>
      <View style={styles.card} testID="profile-eligibility-card">
        <Text style={styles.cardTitle}>{t('more.eligibility.sectionTitle')}</Text>
        <FactRow label={t('more.eligibility.spotEligible')} value={formatBoolean(profile.parkingEligible)} />
        <FactRow label={t('more.eligibility.accessibleEligible')} value={formatBoolean(profile.accessibilityEligible)} />
        <FactRow label={t('more.eligibility.reservedEligible')} value={formatBoolean(profile.reservedSpaceEligible)} />
        <FactRow label={t('more.eligibility.companyCarOnFile')} value={formatBoolean(profile.hasCompanyCar)} />
      </View>

      <View style={styles.card} testID="profile-vehicles-card">
        <Text style={styles.cardTitle}>{t('more.vehicles.title')}</Text>
        {profile.vehicles.length === 0 ? (
          <Text style={styles.bodyText}>{t('more.vehicles.none')}</Text>
        ) : (
          profile.vehicles.map((v) => <VehicleRow key={v.vehicleId} vehicle={v} />)
        )}
      </View>
    </>
  );
}

function FactRow({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

function VehicleRow({ vehicle }: { vehicle: VehicleSnapshot }) {
  return (
    <View style={styles.vehicleRow}>
      <View style={styles.vehicleHeader}>
        <Text style={styles.vehiclePlate}>{vehicle.licensePlate || t('more.vehicles.unknownPlate')}</Text>
        <View style={styles.vehicleTypeRow}>
          {vehicle.isDefault && <Text style={styles.defaultBadge}>{t('more.vehicles.default')}</Text>}
          <Text style={styles.vehicleType}>{displayVehicleType(vehicle.vehicleType)}</Text>
        </View>
      </View>
      <Text style={styles.vehicleMeta}>
        {vehicle.isElectric ? t('more.vehicles.electric') : t('more.vehicles.standard')} · {vehicle.isActive ? t('more.vehicles.active') : t('more.vehicles.inactive')}
      </Text>
    </View>
  );
}

function formatBoolean(value: boolean) {
  return value ? t('common.yes') : t('common.no');
}

const styles = StyleSheet.create({
  heading: { fontSize: 22, fontWeight: '700', color: colors.text },
  card: {
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.sm,
  },
  cardTitle: { fontSize: 16, fontWeight: '700', color: colors.text },
  languageOptions: { flexDirection: 'row', gap: spacing.sm },
  languageOption: {
    flex: 1,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.background,
  },
  languageOptionActive: { borderColor: colors.primary, backgroundColor: colors.primary },
  languageOptionText: { fontSize: 14, fontWeight: '500', color: colors.text },
  languageOptionTextActive: { color: colors.primaryText, fontWeight: '700' },
  row: { gap: 2 },
  rowLabel: { fontSize: 12, color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0 },
  rowValue: { fontSize: 15, color: colors.text, fontWeight: '500' },
  bodyText: { fontSize: 14, lineHeight: 20, color: colors.textMuted },
  link: { alignSelf: 'flex-start', paddingVertical: spacing.xs },
  linkText: { color: colors.primary, fontWeight: '700', fontSize: 14 },
  vehicleRow: {
    paddingVertical: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    gap: spacing.xs,
  },
  vehicleHeader: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  vehiclePlate: { flex: 1, fontSize: 15, fontWeight: '600', color: colors.text },
  vehicleTypeRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
  defaultBadge: { fontSize: 11, fontWeight: '700', color: colors.primary, textTransform: 'uppercase' },
  vehicleType: { fontSize: 13, color: colors.textMuted },
  vehicleMeta: { fontSize: 13, color: colors.textMuted },
  signOut: {
    marginTop: spacing.lg,
    paddingVertical: spacing.md,
    minHeight: 44,
    justifyContent: 'center',
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.danger,
    alignItems: 'center',
  },
  signOutPressed: { opacity: 0.7 },
  signOutLabel: { color: colors.danger, fontWeight: '600', fontSize: 15 },
});
