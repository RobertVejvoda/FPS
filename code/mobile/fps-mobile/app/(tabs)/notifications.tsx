import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';

// Push and SSE bridges are not in MOB001. The tab exists so the route shape is
// stable when notification surfacing lands in a later slice.
export default function NotificationsRoute() {
  return (
    <Screen>
      <StateView
        kind="empty"
        title="Notifications"
        message="Booking notifications will appear here once the mobile notification slice ships."
      />
    </Screen>
  );
}
