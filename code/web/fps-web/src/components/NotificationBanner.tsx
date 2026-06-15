import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchNotifications, markNotificationRead, type NotificationItem } from '../api/notifications';
import { displayLocation } from '../displayLabels';

// Important notifications are critical operational events that require user attention
function isImportantNotification(type: string): boolean {
  const importantTypes = [
    'booking.slotAllocated',
    'booking.requestRejected',
    'booking.slotCancelled',
    'booking.requestCancelled',
    'booking.drawCompleted',
    'booking.manualCorrectionApplied',
    'booking.noShowRecorded',
    'booking.penaltyApplied',
    // HR-audience variants (NOTIF #478) — surface as banners on relevant
    // pages so HR doesn't miss a new request or a draw completing.
    'booking.requestSubmitted.hr',
    'booking.requestCancelled.hr',
    'booking.drawCompleted.hr',
  ];
  return importantTypes.includes(type);
}

interface NotificationBannerProps {
  className?: string;
  style?: React.CSSProperties;
}

export function NotificationBanner({ className, style }: NotificationBannerProps) {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [notification, setNotification] = useState<NotificationItem | null>(null);
  const [dismissing, setDismissing] = useState(false);
  const [visible, setVisible] = useState(false);

  const load = useCallback(() => {
    if (!apiBaseUrl || !bearerToken) return;

    fetchNotifications({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') {
        clear();
        navigate('/session');
        return;
      }
      if (result.kind === 'ok') {
        // Find the first unread important notification
        const important = result.data.items.find(
          n => !n.isRead && isImportantNotification(n.notificationType)
        );
        if (important) {
          setNotification(important);
          setVisible(true);
        } else {
          setNotification(null);
          setVisible(false);
        }
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => {
    load();
    // Poll for new notifications every 30 seconds
    const interval = setInterval(load, 30000);
    return () => clearInterval(interval);
  }, [load]);

  async function handleDismiss() {
    if (!notification) return;

    setDismissing(true);
    const result = await markNotificationRead({ apiBaseUrl, bearerToken }, notification.id);
    setDismissing(false);

    if (result.kind === 'unauthenticated') {
      clear();
      navigate('/session');
      return;
    }

    if (result.kind === 'ok') {
      setVisible(false);
      // Wait for animation, then reload to check for next notification
      setTimeout(() => {
        setNotification(null);
        load();
      }, 300);
    }
  }

  function handleViewAll() {
    navigate('/notifications');
  }

  if (!notification || !visible) return null;

  return (
    <div
      className={className}
      style={{
        padding: '12px 16px',
        background: '#fffbeb',
        border: '1px solid #fbbf24',
        borderRadius: 8,
        display: 'flex',
        alignItems: 'flex-start',
        gap: 12,
        transition: 'opacity 0.3s ease',
        opacity: visible ? 1 : 0,
        ...style,
      }}
    >
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontWeight: 600, fontSize: 14, color: '#92400e', marginBottom: 4 }}>
          {notification.messageText}
        </div>
        {notification.relatedDate && (
          <div style={{ fontSize: 13, color: '#78350f' }}>
            {notification.relatedDate}
            {notification.relatedTimeSlot ? ` · ${notification.relatedTimeSlot}` : ''}
            {displayLocation(notification.locationId) ? ` · ${displayLocation(notification.locationId)}` : ''}
          </div>
        )}
      </div>
      <div style={{ display: 'flex', gap: 8, flexShrink: 0, flexWrap: 'wrap' }}>
        <button
          onClick={handleViewAll}
          disabled={dismissing}
          style={{
            padding: '5px 12px',
            borderRadius: 6,
            border: '1px solid #d97706',
            background: '#fff',
            color: '#92400e',
            fontSize: 13,
            fontWeight: 600,
            cursor: dismissing ? 'default' : 'pointer',
            opacity: dismissing ? 0.5 : 1,
          }}
        >
          View all
        </button>
        <button
          onClick={handleDismiss}
          disabled={dismissing}
          style={{
            padding: '5px 12px',
            borderRadius: 6,
            border: 'none',
            background: '#d97706',
            color: '#fff',
            fontSize: 13,
            fontWeight: 600,
            cursor: dismissing ? 'default' : 'pointer',
            opacity: dismissing ? 0.5 : 1,
          }}
        >
          {dismissing ? '…' : 'Dismiss'}
        </button>
      </div>
    </div>
  );
}
