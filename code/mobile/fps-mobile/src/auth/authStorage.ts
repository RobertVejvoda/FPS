import * as SecureStore from 'expo-secure-store';

const KEY = 'fps.accessToken';

export async function loadAccessToken(): Promise<string | null> {
  return SecureStore.getItemAsync(KEY);
}

export async function saveAccessToken(token: string): Promise<void> {
  await SecureStore.setItemAsync(KEY, token);
}

export async function clearAccessToken(): Promise<void> {
  await SecureStore.deleteItemAsync(KEY);
}
