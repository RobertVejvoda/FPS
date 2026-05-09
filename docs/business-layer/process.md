---
title: Slot Allocation Process
---

The whole solution must be fair to everyone, so how to make it fair, so that nobody has advantage over the others?

**The Problem**

During a typical work week (Monday to Friday), there are not enough parking slots for everyone. To ensure fairness, an allocation algorithm is necessary. Simple randomization is insufficient as it may result in some users parking more frequently than others.

### Fair Allocation Algorithm

To ensure a fair distribution of available slots for all users, we can use a weighted lottery system. This system assigns weights to users based on their previous allocations and requests, ensuring that users who have parked less frequently or requested fewer times have a higher chance of being allocated a slot.

The steps for the weighted lottery system are as follows:

1. **Calculate Weights**: For each user, calculate a weight based on their **previous allocations (PA)** and **previous requests (PR)**. The weight is inversely proportional to the sum of PA and PR.
   
$$
\text{Weight} = \frac{1}{1 + \text{PA} + \text{PR}}
$$

2. **Generate Lottery Tickets**: Assign each user a number of lottery tickets proportional to their weight. Users with higher weights receive more tickets, increasing their chances of being selected.

3. **Draw Lottery**: Randomly draw tickets to allocate the available slots. Users with more tickets have a higher probability of being selected, ensuring a fair distribution based on their previous allocations and requests.

4. **Update Records**: After the allocation, update the records for each user, incrementing their PA and PR as appropriate.

This weighted lottery system ensures that users who have parked less frequently or requested fewer times are given a fair chance of being allocated a slot, promoting equity in the distribution process.

### Example Calculation

Let's consider an example with 4 users and 2 available parking slots. The users have the following previous allocations (PA) and previous requests (PR):

| User | PA | PR |
|------|----|----|
| A    | 2  | 3  |
| B    | 1  | 2  |
| C    | 0  | 1  |
| D    | 1  | 1  |

1. **Calculate Weights**:
    - User A: Weight = 1 / (1 + 2 + 3) = 1/6
    - User B: Weight = 1 / (1 + 1 + 2) = 1/4
    - User C: Weight = 1 / (1 + 0 + 1) = 1/2
    - User D: Weight = 1 / (1 + 1 + 1) = 1/3

2. **Normalize Weights**: Normalize the weights so that they sum up to 1.
    - Total Weight = 1/6 + 1/4 + 1/2 + 1/3 = 1/6 + 3/12 + 6/12 + 4/12 = 1/6 + 13/12
    - Normalized Weights:
        - User A: (1/6) / (1/6 + 13/12) = 1/6 / (15/12) = 1/6 * 12/15 = 2/15
        - User B: (1/4) / (1/6 + 13/12) = 1/4 / (15/12) = 1/4 * 12/15 = 3/15
        - User C: (1/2) / (1/6 + 13/12) = 1/2 / (15/12) = 1/2 * 12/15 = 6/15
        - User D: (1/3) / (1/6 + 13/12) = 1/3 / (15/12) = 1/3 * 12/15 = 4/15

3. **Generate Lottery Tickets**:
    - User A: 2 tickets
    - User B: 3 tickets
    - User C: 6 tickets
    - User D: 4 tickets

4. **Draw Lottery**:
    - Randomly draw 2 tickets from the pool of 15 tickets.
    - Suppose tickets drawn are for User C and User B.

5. **Update Records**:
    - User C: PA = 1, PR = 2
    - User B: PA = 2, PR = 3

This example demonstrates how the weighted lottery system ensures a fair allocation of parking slots based on previous allocations and requests.

### Weekly Allocation Example

Let's consider an example with 5 users (Alice, Bob, Charlie, David, Emma) and 3 available parking slots for a whole week (Monday to Friday). The users have the following previous allocations (PA) and previous requests (PR) at the start of the week:

| User   | PA | PR |
|--------|----|----|
| Alice  | 2  | 3  |
| Bob    | 1  | 2  |
| Charlie| 0  | 1  |
| David  | 1  | 1  |
| Emma   | 0  | 0  |

#### Monday
1. **Calculate Weights**:
    - Alice: Weight = 1 / (1 + 2 + 3) = 1/6
    - Bob: Weight = 1 / (1 + 1 + 2) = 1/4
    - Charlie: Weight = 1 / (1 + 0 + 1) = 1/2
    - David: Weight = 1 / (1 + 1 + 1) = 1/3
    - Emma: Weight = 1 / (1 + 0 + 0) = 1

2. **Normalize Weights**:
    - Total Weight = 1/6 + 1/4 + 1/2 + 1/3 + 1 = 1/6 + 3/12 + 6/12 + 4/12 + 12/12 = 1/6 + 25/12
    - Normalized Weights:
        - Alice: (1/6) / (1/6 + 25/12) = 1/6 / (27/12) = 1/6 * 12/27 = 2/27
        - Bob: (1/4) / (1/6 + 25/12) = 1/4 / (27/12) = 1/4 * 12/27 = 3/27
        - Charlie: (1/2) / (1/6 + 25/12) = 1/2 / (27/12) = 1/2 * 12/27 = 6/27
        - David: (1/3) / (1/6 + 25/12) = 1/3 / (27/12) = 1/3 * 12/27 = 4/27
        - Emma: 1 / (1/6 + 25/12) = 1 / (27/12) = 12/27

3. **Generate Lottery Tickets**:
    - Alice: 2 tickets
    - Bob: 3 tickets
    - Charlie: 6 tickets
    - David: 4 tickets
    - Emma: 12 tickets

4. **Draw Lottery**:
    - Randomly draw 3 tickets from the pool of 27 tickets.
    - Suppose tickets drawn are for Emma, Charlie, and Bob.

5. **Update Records**:
    - Emma: PA = 1, PR = 1
    - Charlie: PA = 1, PR = 2
    - Bob: PA = 2, PR = 3

#### Tuesday
1. **Calculate Weights**:
    - Alice: Weight = 1 / (1 + 2 + 3) = 1/6
    - Bob: Weight = 1 / (1 + 2 + 3) = 1/6
    - Charlie: Weight = 1 / (1 + 1 + 2) = 1/4
    - David: Weight = 1 / (1 + 1 + 1) = 1/3
    - Emma: Weight = 1 / (1 + 1 + 1) = 1/3

2. **Normalize Weights**:
    - Total Weight = 1/6 + 1/6 + 1/4 + 1/3 + 1/3 = 1/6 + 1/6 + 3/12 + 4/12 + 4/12 = 1/6 + 11/12
    - Normalized Weights:
        - Alice: (1/6) / (1/6 + 11/12) = 1/6 / (13/12) = 1/6 * 12/13 = 2/13
        - Bob: (1/6) / (1/6 + 11/12) = 1/6 / (13/12) = 1/6 * 12/13 = 2/13
        - Charlie: (1/4) / (1/6 + 11/12) = 1/4 / (13/12) = 1/4 * 12/13 = 3/13
        - David: (1/3) / (1/6 + 11/12) = 1/3 / (13/12) = 1/3 * 12/13 = 4/13
        - Emma: (1/3) / (1/6 + 11/12) = 1/3 / (13/12) = 1/3 * 12/13 = 4/13

3. **Generate Lottery Tickets**:
    - Alice: 2 tickets
    - Bob: 2 tickets
    - Charlie: 3 tickets
    - David: 4 tickets
    - Emma: 4 tickets

4. **Draw Lottery**:
    - Randomly draw 3 tickets from the pool of 15 tickets.
    - Suppose tickets drawn are for David, Emma, and Charlie.

5. **Update Records**:
    - David: PA = 2, PR = 2
    - Emma: PA = 2, PR = 2
    - Charlie: PA = 2, PR = 3

#### Wednesday
1. **Calculate Weights**:
    - Alice: Weight = 1 / (1 + 2 + 3) = 1/6
    - Bob: Weight = 1 / (1 + 2 + 3) = 1/6
    - Charlie: Weight = 1 / (1 + 2 + 3) = 1/6
    - David: Weight = 1 / (1 + 2 + 2) = 1/5
    - Emma: Weight = 1 / (1 + 2 + 2) = 1/5

2. **Normalize Weights**:
    - Total Weight = 1/6 + 1/6 + 1/6 + 1/5 + 1/5 = 3/18 + 2/10 = 3/18 + 3.6/18 = 6.6/18
    - Normalized Weights:
        - Alice: (1/6) / (6.6/18) = 1/6 / (6.6/18) = 1/6 * 18/6.6 = 3/11
        - Bob: (1/6) / (6.6/18) = 1/6 / (6.6/18) = 1/6 * 18/6.6 = 3/11
        - Charlie: (1/6) / (6.6/18) = 1/6 / (6.6/18) = 1/6 * 18/6.6 = 3/11
        - David: (1/5) / (6.6/18) = 1/5 / (6.6/18) = 1/5 * 18/6.6 = 3.6/11
        - Emma: (1/5) / (6.6/18) = 1/5 / (6.6/18) = 1/5 * 18/6.6 = 3.6/11

3. **Generate Lottery Tickets**:
    - Alice: 3 tickets
    - Bob: 3 tickets
    - Charlie: 3 tickets
    - David: 4 tickets
    - Emma: 4 tickets

4. **Draw Lottery**:
    - Randomly draw 3 tickets from the pool of 17 tickets.
    - Suppose tickets drawn are for Alice, Bob, and David.

5. **Update Records**:
    - Alice: PA = 3, PR = 4
    - Bob: PA = 3, PR = 4
    - David: PA = 3, PR = 3

#### Thursday
1. **Calculate Weights**:
    - Alice: Weight = 1 / (1 + 3 + 4) = 1/8
    - Bob: Weight = 1 / (1 + 3 + 4) = 1/8
    - Charlie: Weight = 1 / (1 + 2 + 3) = 1/6
    - David: Weight = 1 / (1 + 3 + 3) = 1/7
    - Emma: Weight = 1 / (1 + 2 + 2) = 1/5

2. **Normalize Weights**:
    - Total Weight = 1/8 + 1/8 + 1/6 + 1/7 + 1/5 = 1/8 + 1/8 + 4/24 + 3/21 + 4.8/24 = 0.125 + 0.125 + 0.1667 + 0.1429 + 0.2 = 0.7596
    - Normalized Weights:
        - Alice: (1/8) / 0.7596 = 0.125 / 0.7596 = 0.1645
        - Bob: (1/8) / 0.7596 = 0.125 / 0.7596 = 0.1645
        - Charlie: (1/6) / 0.7596 = 0.1667 / 0.7596 = 0.2194
        - David: (1/7) / 0.7596 = 0.1429 / 0.7596 = 0.1881
        - Emma: (1/5) / 0.7596 = 0.2 / 0.7596 = 0.2632

3. **Generate Lottery Tickets**:
    - Alice: 2 tickets
    - Bob: 2 tickets
    - Charlie: 3 tickets
    - David: 3 tickets
    - Emma: 4 tickets

4. **Draw Lottery**:
    - Randomly draw 3 tickets from the pool of 14 tickets.
    - Suppose tickets drawn are for Emma, Charlie, and David.

5. **Update Records**:
    - Emma: PA = 3, PR = 3
    - Charlie: PA = 3, PR = 4
    - David: PA = 4, PR = 4

#### Friday
1. **Calculate Weights**:
    - Alice: Weight = 1 / (1 + 3 + 4) = 1/8
    - Bob: Weight = 1 / (1 + 3 + 4) = 1/8
    - Charlie: Weight = 1 / (1 + 3 + 4) = 1/8
    - David: Weight = 1 / (1 + 4 + 4) = 1/9
    - Emma: Weight = 1 / (1 + 3 + 3) = 1/7

2. **Normalize Weights**:
    - Total Weight = 1/8 + 1/8 + 1/8 + 1/9 + 1/7 = 0.125 + 0.125 + 0.125 + 0.1111 + 0.1429 = 0.629
    - Normalized Weights:
        - Alice: (1/8) / 0.629 = 0.125 / 0.629 = 0.1987
        - Bob: (1/8) / 0.629 = 0.125 / 0.629 = 0.1987
        - Charlie: (1/8) / 0.629 = 0.125 / 0.629 = 0.1987
        - David: (1/9) / 0.629 = 0.1111 / 0.629 = 0.1766
        - Emma: (1/7) / 0.629 = 0.1429 / 0.629 = 0.2273

3. **Generate Lottery Tickets**:
    - Alice: 2 tickets
    - Bob: 2 tickets
    - Charlie: 2 tickets
    - David: 2 tickets
    - Emma: 3 tickets

4. **Draw Lottery**:
    - Randomly draw 3 tickets from the pool of 11 tickets.
    - Suppose tickets drawn are for Alice, Bob, and Emma.

5. **Update Records**:
    - Alice: PA = 4, PR = 5
    - Bob: PA = 4, PR = 5
    - Emma: PA = 4, PR = 4


### Weekly Summary

The following table summarizes the number of requests and allocations for each user over the week:

| User    | Requests | Allocations |
|---------|----------|-------------|
| Alice   | 5        | 4           |
| Bob     | 5        | 4           |
| Charlie | 4        | 3           |
| David   | 4        | 4           |
| Emma    | 4        | 4           |

The daily allocations are as follows:

| Day      | Allocations          |
|----------|----------------------|
| Monday   | Emma, Charlie, Bob   |
| Tuesday  | David, Emma, Charlie |
| Wednesday| Alice, Bob, David    |
| Thursday | Emma, Charlie, David |
| Friday   | Alice, Bob, Emma     |


## Allocation Process - Requests sent to future time slot

1. Booking requestors submit requests for parking in future time slots.
2. The Booking Controller accepts the requests and adds them to the queue, rejecting any duplicate requests.
3. The allocation process is triggered daily at a specified time. It can be configured.
4. The Booking Processor locks the time slot, preventing further requests for that slot.
5. The Booking Processor runs the allocation algorithm (as described above) to assign parking slots.
6. Notifications are sent to the requestors informing them of the allocation results.

## Allocation Process - Requests sent to current time slot

This scenario deals with requests sent for the same day, for example, during the commute to the office. It assumes that the requestor has checked the availability in the garage for the current time slot, allocation process has already run and there are still some parking slots available.

1. Booking requestors submit requests for parking in the current time slot (today).
2. The Booking Controller accepts the request and adds it to the queue; duplicate requests are rejected.
3. The slot is allocated immediatelly.
4. A notification is sent to the requestor confirming the allocation.

### Example Scenarios

| Current Time (UTC) | TimeSlot.Start      | Valid? | Reason                                |
|---------------------|---------------------|--------|---------------------------------------|
| 2025-03-30 10:00    | 2025-03-30 09:00   | ❌     | Start time is earlier than the current day. |
| 2025-03-30 10:00    | 2025-03-30 11:00   | ✅     | Start time is later today.            |
| 2025-03-30 10:00    | 2025-03-31 09:00   | ✅     | Start time is in the future.          |


**Predefined Parking Slots for company cars**

- Users with company cars have predefined parking slots. 
- These users must send parking requests too, either manually or within an automated schedule. 
- These requests take precedence over others outside of allocation process.

**Constraints**

- Consider parking slot availability within time slot
- Consider parking slot capabilities - vehicle types, EV chargers, ...

    
## Cancel Booking Request

1. Booking requestor can send a cancellation request any time.
2. If the Draw process has already run (issued lock), it'll cancel the parking slot allocation; 
3. Allocation process runs again.

## Penalties and Compensations

The Profile Adjuster can add or remove allocations from users as a penalty or benefit. This adjustment is also applied when a parking slot is not occupied by the requestor.

## Further Improvements

Eco-friendly vehicles should have a percentage advantage over others, considering parking slot capability. Similarly, motorcycles should be allowed to share slots where possible.

### Confirmation of Parking Slot Usage

To ensure accurate tracking of parking slot usage, users must confirm their parking slot allocation upon entering the garage. This can be achieved by using a card reader system. When a user enters the garage, they swipe their access card at the card reader, which automatically logs the usage of the allocated parking slot. 

This approach provides several benefits:
- **Accurate Tracking**: It allows the system to track the exact duration for which a user occupies a parking slot.
- **Usage Verification**: Confirms that the allocated slot is being used by the intended user.
- **Data Collection**: Collects data on parking slot usage patterns, which can be used for future improvements in the allocation process.
- **Automated Notifications**: Sends automated notifications to users confirming their parking slot usage and duration.

By implementing this system, we can ensure fair and efficient use of parking resources while maintaining accurate records of parking slot usage.

## Example of allocation

![Example](../images/fps-booking-allocation-example.png)
