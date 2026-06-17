import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const KEY = 'fps.accessToken';
// Non-sensitive flag: set after explicit sign-out to force interactive OIDC login next time.
const FORCE_PROMPT_KEY = 'fps.forcePromptLogin';

const isWeb = Platform.OS === 'web';

export async function loadAccessToken(): Promise<string | null> {
  if (isWeb) return AsyncStorage.getItem(KEY);
  return SecureStore.getItemAsync(KEY);
}

export async function saveAccessToken(token: string): Promise<void> {
  if (isWeb) {
    await AsyncStorage.setItem(KEY, token);
    return;
  }
  await SecureStore.setItemAsync(KEY, token);
}

export async function clearAccessToken(): Promise<void> {
  if (isWeb) {
    await AsyncStorage.removeItem(KEY);
    return;
  }
  await SecureStore.deleteItemAsync(KEY);
}

// Force-prompt flag: not sensitive, always stored in AsyncStorage.
export async function loadForcePromptLogin(): Promise<boolean> {
  const value = await AsyncStorage.getItem(FORCE_PROMPT_KEY);
  return value === '1';
}

export async function saveForcePromptLogin(): Promise<void> {
  await AsyncStorage.setItem(FORCE_PROMPT_KEY, '1');
}

export async function clearForcePromptLogin(): Promise<void> {
  await AsyncStorage.removeItem(FORCE_PROMPT_KEY);
}
