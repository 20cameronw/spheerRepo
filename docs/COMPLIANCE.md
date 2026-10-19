# Frontier privacy and release gates

Status: implementation checklist, not legal approval or a store-compliance certification.
Online services, Cloud Save, native account recovery, Analytics and Remote Config remain disabled in the authored build.
Do not activate them solely because their adapters compile.

## Current behavior

- Gameplay works offline. Analytics, rewarded ads and online account/cloud/leaderboard access are separate,
  policy-versioned local opt-ins. No automatic cloud upload or competitive score submission exists.
- Local job timers now advance from a startup UTC anchor plus monotonic session elapsed time, ignoring
  device-date edits during the running session. UTC at startup is still untrusted; edits across process
  restarts, offline accrual and cross-device timing require persistent rewind detection and server settlement.
  This protection is not sufficient evidence for competitive progression or trusted live events.
- Manual cloud comparison, backup, restore and local undo require an explicit player action. Destructive
  backup/restore uses a second confirmation and a fresh cloud record/write lock.
- Settings offers independent local audio preferences and Reset Progress. Reset starts a new local game and
  removes local progression recovery copies; it is not an account-deletion request or a reset of every device setting.
- Online Save & Account Deletion is separate from Reset Progress. It requires configured authentication and
  cloud providers, an already signed-in account and a second confirmation bound to that player's ID.
  Closing Settings, cancelling or changing the signed-in account invalidates the confirmation.
- Explicit erasure remains available after consent withdrawal. It does not sign in, create an anonymous account,
  grant consent, upload progress or replace/delete the local game and its retained restore backup.
- Deletion removes only the player's `frontier_save_v2` Cloud Save key, with a freshly obtained write lock,
  before deleting the UGS Authentication account. Missing cloud data is already erased; a cloud failure or
  conflict stops account deletion. A subsequent account failure explicitly reports that cloud removal already
  happened and permits a new confirmed retry. Successful UGS account deletion clears its local session credentials.
- This client flow does **not** promise erasure of leaderboard, analytics, advertising-network or platform
  purchase records. Those systems require their own documented deletion/retention paths before activation.
- The UGS Analytics adapter starts collection only after the separate analytics opt-in and stops it when that
  choice is withdrawn. Events are not queued by Frontier before consent. Activation also requires an explicit
  feature flag, a linked Unity project and custom event schemas configured in the Unity Dashboard.
- Remote Config is separately feature-flagged and requires online-services consent, configured online-service
  prerequisites and an already signed-in identity. Fetching never creates or signs into an account. Returned
  setting values are exposed as JSON-formatted strings; responses are discarded if consent is withdrawn while
  a fetch is in flight, including withdrawal followed by re-opt-in. No live tuning is enabled by default.
  The scene can select a previously validated device-local travel snapshot without waiting for networking,
  then fetch once per existing identity in the background. Only registered `travel.*` numeric keys are
  supported; the entire response is rejected for other keys, invalid ranges, fractional plating tiers or
  inconsistent duration/plating thresholds. A new snapshot applies at the next scene startup, never
  repricing an active session. The cache is invalidated by bundled tuning changes and removed on online
  consent withdrawal; disabled providers, missing consent or unsigned identities do not read it. Snapshots
  are bound to the same signed-in player ID so account-specific overrides cannot leak into another account;
  cold startup with no initialized identity uses bundled defaults instead of waiting for authentication.
  It is not trusted server data,
  cross-device progression authority, or general catalog/event/score/purchase tuning.
- Local crew reminders use Mobile Notifications 2.5.0 and are feature-flagged off until native-device
  permission/delivery/cancellation checks are complete. The separate device preference starts off; Settings
  can enable it on supported enabled builds. Permission is requested only when an active saved crew timer
  exists, not on launch. Denial does not repeatedly prompt; off/on permits an explicit retry.
  Construction/upgrade/clearance reminders share eight reserved IDs, are cancelled when their timers disappear
  or reminders are disabled, and never grant rewards or change jobs. Failed save writes suppress new reminders.
  The native build guard rejects launch authorization, push/APNs capability and exact-alarm permission.
  OS delivery timing is advisory and device-controlled; restarting the device is not configured to reschedule.
  This is local notification functionality, not a push service or server-authoritative timer system.
- Rewarded ads remain unavailable. The local `RewardedAdsPolicy` asset is default-off with zero daily caps
  for every planned placement. The service persists per-placement counts/cooldowns in device preferences
  (not the progression save), rejects invalid stored usage and observed clock rewind, and does not deliver
  a provider reward until its usage write succeeds. Withdrawal during playback discards the pending reward,
  including withdrawal followed by re-opt-in. This cannot undo an SDK request already in progress.
  Device UTC and preferences are not server authority or cross-device anti-cheat; forward clock changes
  and cleared preferences are outside this local protection. Provider reward verification, durable gameplay
  grants, per-job limits, SDK privacy settings and native-device evidence remain required.

## Required before enabling providers

- [ ] Publish a real HTTPS privacy policy, in-app link and support contact. Describe actual data categories,
  processors, purposes, retention, opt-out and deletion behavior; do not use placeholder legal text.
- [ ] Resolve target audience, age treatment and applicable consent requirements with the product owner/legal reviewer.
  Confirm current Apple and Google requirements rather than treating this checklist as legal advice.
- [ ] Configure the correct Unity project/environment, authentication providers, platform identifiers, Apple
  capability and Google OAuth/Play Games configuration. Use native test accounts, not production customer data.
- [ ] Verify enabled-provider sign-in/link/recovery, revoked credentials, expired sessions, offline errors and
  local/cloud conflicts on physical iOS and Android devices.
- [ ] Verify deletion on real test accounts, including a cloud-write conflict, partial failure, retry, absence
  of the cloud key, invalidated account/session, and unchanged local progression/restore backup.
- [ ] Implement and verify the public web deletion-request route required for Google Play account-enabled apps.
  Define identity verification and support escalation without requesting passwords or tokens.
- [ ] Define server-side deletion/retention for leaderboard records and any future analytics/ad processors.
  Prevent other active devices from recreating deleted data; verify token revocation and backend cleanup.
- [ ] Before enabling Analytics, configure reviewed event schemas and retention in the Unity Dashboard, document
  the collected fields and deletion path, and validate consent grant/withdrawal with test identities.
- [ ] Before enabling Remote Config, configure the project/environment and reviewed settings; verify the signed-in
  account requirement and ensure each setting has a safe local default and validated bounds.
- [ ] Verify deletion remains accessible after withdrawing consent and explain any necessary reauthentication.
  Do not require analytics/ad opt-in to request erasure.
- [ ] Complete Apple App Privacy and Google Data Safety declarations from the actual shipped SDK inventory,
  including third-party collection, identifiers, purchases and usage data where applicable.
- [ ] Complete store age ratings, Google "Contains ads" and advertising-ID declarations when ads are present.

## Ads and purchases

The pending-purchase protocol now requires receipt evidence and an injected verifier, checks the returned
product/transaction/store identity, and requires atomic reward/journal persistence before store confirmation.
The current-save grant adapter writes the real shard wallet and store/transaction journal atomically before
publishing its reward. Only test verifier/acknowledger implementations exist; the offline factory still
exposes unavailable purchases. Never treat product/transaction strings as proof of payment or plug in a
success-only verifier. Store adapters must validate the native receipt, bundle/app identity, paid state,
product and quantity before constructing a verified purchase. The current save journal prevents replay only
within that saved history. Until authoritative purchase recovery exists, gameplay reset refuses a save
containing any purchase journal entries, and cloud restore/undo refuses when either the current or incoming
save has entries. This includes equal journals: restoring an earlier wallet could otherwise restore already
spent currency. Refusal leaves the current save, context and recovery copies unchanged; malformed history
does not bypass the guard. Saves without store purchase history retain their existing reset/restore behavior.
This is a fail-closed safety gate, not purchase reconciliation or a shipped paid-save recovery experience.
Rolling-backup recovery and multiple devices can still replace history; authoritative reconciliation across
those boundaries and a reviewed reset/restore policy remain required before IAP activation.
Do not log receipt payloads or put them into analytics.

- [ ] Initialize advertising only after the relevant consent/audience choices. Propagate actual GDPR/CCPA/COPPA
  flags to the selected SDK before any load; configure mediation test mode and physical-device validation.
- [ ] Implement the iOS ATT explainer/request only when required by actual tracking behavior. Do not treat a
  local ad-preference toggle as ATT authorization.
- [ ] Keep rewards opt-in, capped and idempotent. Test denial, unavailable inventory, interruption and duplicate callbacks.
- [ ] Configure real store products and use store-localized prices. Validate receipts and persist transaction
  IDs before acknowledging grants. Test pending, duplicate, cancelled and restored purchases.
- [ ] Document which purchases restore; consumables are not automatically restorable. Do not promise account
  erasure removes platform-held purchase history.

## Release evidence still needed

Physical-device native builds, signing, performance/thermal/accessibility and store-review evidence remain
separate gates. Automated service/consent/deletion and UI confirmation checks cover local implementation only;
they make no live provider requests and do not close these release requirements.
An Android IL2CPP/arm64 development APK now builds successfully with bundled English localization and
the native notification/haptics code. Its APK v2 signature verifies, but it is debug-signed, has not been
installed/validated on a physical device, and does not establish store/legal/provider readiness. Final
application identifiers and release signing remain unapproved; the current Android development identity
is retained unchanged.
