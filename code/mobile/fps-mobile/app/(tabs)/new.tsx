import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';

// New booking submission lives in a later mobile slice; the entry point is here so
// navigation analytics and deep links can settle around the final URL early.
export default function NewBookingRoute() {
  return (
    <Screen>
      <StateView
        kind="empty"
        title="New Booking"
        message="The booking-request flow will be added in a later slice. Use the web client for now."
      />
    </Screen>
  );
}
