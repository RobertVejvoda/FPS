import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const KEY = 'fps.accessToken';

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
