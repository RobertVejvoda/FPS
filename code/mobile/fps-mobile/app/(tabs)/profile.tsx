import { Redirect } from 'expo-router';

// Profile content moved to the More tab (UX004).
// This redirect keeps any deep links to /(tabs)/profile working.
export default function ProfileRedirect() {
  return <Redirect href="/(tabs)/more" />;
}
