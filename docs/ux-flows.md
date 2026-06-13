# UX Flows

## Flow 1 — Normal Request Lifecycle

```
[Requester]
  Logs in → sees submission screen
  GPS auto-detects location
  Selects DTC from dropdown
  Taps Submit

[Requester UI]
  → Pending state: spinner + "finding your technician"

[Backend]
  → Runs matching algorithm
  → Finds nearest qualified Available/En Route rep
  → Sends job offer to rep

[Service Rep UI]
  → Job offer screen appears with 60s countdown
  → Rep reviews: requester name, tier, DTC, distance, ETA, map pin
  → Rep taps Accept

[Requester UI]
  → Tracking state: rep name, ETA, live map with rep position
  → ETA updates every 3 seconds as rep moves

[Service Rep UI]
  → Active job screen: map with destination, ETA, "I've Arrived" button
  → Rep arrives, taps "I've Arrived"

[Requester UI]
  → Rep state shows "Almost There" (Within 15 Miles indicator)

[Service Rep UI]
  → On Site screen: request details, "Mark Complete" button
  → Rep completes work, taps "Mark Complete"

[Requester UI]
  → Completion screen: "Your service is complete."

[Dispatcher UI]
  → Request disappears from map
  → Rep marker returns to green (Available)
```

---

## Flow 2 — Rep Declines

```
[Backend]
  → Sends job offer to best qualified rep

[Service Rep UI]
  → Rep taps Decline (or 60s countdown expires)

[Backend]
  → Marks offer Declined/Expired
  → Rep is permanently skipped for this request
  → Finds next best qualified rep
  → Sends new job offer

[Repeats until accepted or all reps exhausted]

If all reps decline/expire:
  → Request stays in Pending
  → Dispatcher receives notification: "[DTC title] request has no available technician"
  → System re-runs matching when next rep becomes Available
```

---

## Flow 3 — Priority Redirect (Gold Example)

```
[Gold Requester]
  Submits a service request

[Dispatcher UI]
  → New request appears in queue with Gold badge
  → Suggested match: nearest En Route rep currently serving a Bronze request
  → Redirect button is enabled (rep is En Route, not Within 15 Miles/On Site, no cooldown)
  → Dispatcher taps Redirect

[Backend]
  → Bronze request → Pending
  → Rep is hard-assigned to Gold request (no accept/decline)
  → Redirect cooldown starts (5 minutes) for this rep

[Service Rep UI]
  → Active job screen updates to new destination
  → New requester name, tier, DTC, map pin shown

[Dispatcher UI]
  → Matching algorithm runs for displaced Bronze request
  → Next best qualified rep receives job offer
  → Bronze request progresses normally from here

[Bronze Requester UI]
  → Continues to show spinner during re-match
  → Once new rep accepts: "Our apologies, we needed to redirect [old rep name].
     [new rep name] is heading your way." + new ETA
  → Tracking state resumes with new rep

[Gold Requester UI]
  → Tracking state: Gold rep's position, ETA
  → Proceeds through normal Flow 1 from here
```

---

## Flow 4 — No Rep Available

```
[Requester]
  Submits a service request

[Backend]
  → Matching algorithm finds no qualified rep
     (none Available/En Route with matching equipment, or all have declined)

[Requester UI]
  → Remains in Pending state: spinner + "finding your technician"
  → No error shown — requester waits

[Dispatcher UI]
  → Notification: "[DTC title] request from [requester name] has no available technician"
  → Request appears in queue with Pending status

[Backend]
  → When any rep transitions to Available (job complete, vehicle claim):
     re-runs matching for all Pending requests
  → If match found: sends job offer, proceeds as Flow 1
```

---

## Flow 5 — Rep Goes Offline Mid-Job

```
[Service Rep]
  App crashes or rep logs out while En Route or On Site

[Backend]
  → Detects session end (human logs out)
  → Active job → Pending, RE-MATCHED to another available rep
  → Rep + vehicle go off-duty — the simulator does NOT re-assume them; the truck parks
  → Dispatcher notified: "[Rep name] went offline. [DTC title] request re-queued."

[Dispatcher UI]
  → Rep marker disappears from map (Offline)
  → Notification appears in notifications panel
  → Vehicle shows as idle/off-duty with no active rep (parked, not driven by the simulator)

[Requester UI]
  → If previously in tracking state → returns to Pending spinner
  → No explicit error message — just "finding your technician" again

[Backend]
  → Runs matching algorithm for the re-matched request
  → Proceeds as Flow 1 or Flow 4 depending on availability

[Vehicle Re-takeover]
  → The simulator never re-assumes the logged-out human's rep/vehicle
  → The idle vehicle returns to the takeover dropdown — any idle rep can take it over again (see Flow 6)
```

---

## Flow 6 — Human Takes Over a Truck

```
[Service Rep]
  Logs in on a device (mobile) as one of the seeded rep accounts (rep1…rep8)

[Service Rep UI]
  → "Take over an idle vehicle" screen appears
  → Dropdown lists IDLE vehicles only (not en route to a job, not on a job)
  → Each entry shows vehicle ID + equipment capabilities (DTC codes)
  → Rep selects one idle vehicle

[Backend]
  → Validates: rep is idle (no active job) AND selected vehicle is idle
  → Performs takeover: vehicle is claimed for this rep
  → Takeover SUPERSEDES whatever the simulator had assigned to that vehicle
  → Rep state → Available

[Dispatcher UI]
  → Vehicle marker turns green (Available) under the human rep's name

[Service Rep UI]
  → Moves to the idle/waiting view — ready for dispatch
  → From here: the SIMULATOR drives the truck's position on the map,
     the HUMAN makes every decision (Accept/Decline, "I've Arrived", "Mark Complete")
  → A job offer proceeds as Flow 1, with the human tapping each action and the
     simulator navigating the truck between them

[On Logout]
  → Takeover is sticky: rep + vehicle go off-duty
  → The simulator does NOT re-assume them (see Flow 5)
  → If the human was mid-job, that request is re-matched to another rep
```
