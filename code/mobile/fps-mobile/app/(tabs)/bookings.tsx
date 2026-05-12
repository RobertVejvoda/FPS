import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';

// MOB002 will implement the read-only My Bookings list against GET /bookings.
// MOB001 only owns the navigation entry and an empty/explanatory state.
export default function BookingsRoute() {
  return (
    <Screen>
      <StateView
        kind="empty"
        title="My Bookings"
        message="This list will show your booking requests once the MOB002 slice ships."
      />
    </Screen>
  );
}
