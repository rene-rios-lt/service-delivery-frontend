# Persona Views

## How Personas Map to Views

The user's **role** (from their JWT) determines which view they see. The **platform** (Desktop, Web, Mobile) determines the layout and form factor. Any persona can use any platform — a dispatcher on mobile gets the dispatcher view in a mobile layout, not the requester view.

| Role | Primary Platform | View |
|------|-----------------|------|
| Dispatcher | Desktop | Fleet command center |
| ServiceRep | Mobile | Job offer and active job |
| Requester | Web / Mobile | Request submission and tracking |

---

## Dispatcher View

The dispatcher manages the fleet and handles service requests from a command center perspective.

### Fleet Map
- Full-screen Google Map showing Iowa
- All claimed vehicles plotted as markers, color-coded by rep state:
  - **Gray** — Offline (unclaimed vehicle, no rep)
  - **Green** — Available
  - **Blue** — En Route
  - **Yellow** — Within 15 Miles
  - **Orange** — On Site
- Clicking a vehicle marker opens a detail panel (rep name, vehicle ID, current state, active request if any)

### Incoming Request Panel
- List of pending and active service requests
- Each entry shows: requester name, tier badge (Bronze/Silver/Gold), DTC title, location, time waiting
- Suggested match highlighted: nearest qualified rep with distance, ETA, and equipment match indicator
- Requests sorted by tier (Gold first), then by time waiting

### Assignment Controls
- **Assign** button on each pending request — confirms the suggested match and sends the job offer
- **Redirect** button appears on En Route reps serving lower-tier requests when a higher-tier request is pending
  - Redirect button is disabled (grayed out) when rep is Within 15 Miles or On Site
  - Redirect button is disabled during a rep's 5-minute cooldown, unless the incoming request is Gold tier

### Notifications Panel
- Alert when a request has no qualified rep available
- Alert when a rep goes Offline mid-job
- Alert when all qualified reps have declined a request
- Alerts are dismissible; dismissed alerts stay in a log

### Vehicle Management
- Vehicle list view: all 8 vehicles, claim status, rep name (if claimed)
- **Force Release** button per vehicle — available to dispatcher only; releases a claimed vehicle back to the pool

---

## Service Rep View

### Vehicle Selection Screen
Shown immediately after login, before the rep is active in the fleet.
- List of available (unclaimed) vehicles, each showing its vehicle ID and equipment capabilities (list of DTC codes it can handle)
- Rep selects one — vehicle is locked to them for the day
- Once selected, rep state transitions to Available and they appear on the dispatcher's map

### Job Offer Screen
Shown when a job offer arrives (pushed via SignalR).
- 60-second countdown timer (visible, prominent)
- Requester name
- Tier badge (Bronze / Silver / Gold)
- DTC title (human-readable, e.g. "Hydraulic system fault")
- Distance to requester (miles)
- ETA (minutes)
- Google Map with a pin at the requester's location
- **Accept** and **Decline** buttons

If the countdown expires with no action, the offer is marked Expired and the screen returns to idle.

### Active Job Screen (En Route / Within 15 Miles)
Shown after accepting a job offer.
- Google Map with rep's current position and requester's location pinned
- ETA (updates in real time as position updates come in)
- Requester name and DTC title
- **"I've Arrived"** button — triggers On Site state transition

### On Site Screen
Shown after tapping "I've Arrived".
- Requester name, DTC title, and request details
- **"Mark Complete"** button — triggers job completion, returns rep to Available state

### Redirect Notification
If the dispatcher redirects the rep while En Route:
- Current job offer screen updates to show the new destination
- New requester name, tier, DTC, distance, and ETA shown
- Map pin updates to new location
- No accept/decline — redirect is a hard assignment

---

## Requester View

### Service Request Submission
- GPS location auto-detected on page load (browser/device geolocation API)
- Location displayed on a small Google Map for confirmation (read-only, no manual adjustment)
- DTC dropdown: 10 options with human-readable titles only (no technical codes shown)
- **Submit** button

### Pending State
Shown immediately after submission while no rep is assigned.
- Spinner animation
- Message: "We're finding the best available technician for you."
- No map shown — requester does not see the fleet during matching

### Tracking State (Assigned / En Route / Within 15 Miles)
Shown once a rep accepts the job offer.
- Google Map with live position of the assigned rep (updates every 3 seconds)
- Rep name
- ETA (updates in real time)
- Rep state indicator (En Route / Almost There when Within 15 Miles)

### Redirect Notification
Shown when rep is redirected and a new rep accepts the displaced request.
- Message: "Our apologies, we needed to redirect [original rep name]. [new rep name] is heading your way."
- New ETA shown
- Map updates to show new rep's position
- Notification is displayed inline — no modal or interrupt

### Completion Screen
Shown when the rep marks the job complete.
- Message: "Your service is complete."
- No further action required — session ends
