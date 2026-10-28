# Frontier privacy and release gates

Status: implementation checklist, not legal approval or a store-compliance certification.
Anonymous Authentication and Leaderboards are enabled in the authored build at the developer's direction.
Cloud Save, Analytics and local notifications are also enabled at the developer's direction.
Native account recovery and Remote Config remain disabled. Feature availability does not grant
player consent or OS permission.

## Current behavior

- Gameplay works offline. Analytics, rewarded ads and online account/cloud/leaderboard access are separate,
  policy-versioned local opt-ins. No automatic cloud upload exists. Online ranks accept all players,
  without an age gate, after online opt-in. The Ranks screen explains that Unity receives a pseudonymous
  player ID, Frontier Score and normal connection data, not a chosen public name/email/birthday.
  Posting a score is explicit, never triggered by launch or normal progression. Consent policy version 3
  requires a fresh decision for this changed behavior. This is data minimization, not a certification
  that anonymous authentication is exempt from children's privacy requirements; publish the actual
  behavior and retention/support details in the public policy before distributing this build.
- `Lifetime_Earnings_Leaderboard` is the configured UGS board ID. The rebuilt game submits its existing
  **Frontier Score**, not currency earnings (the ID is retained for the developer's existing dashboard).
  Configure the board as descending, **Keep Best**, non-bucketed, with player score writes allowed.
  Client-reported scores are casual rankings, not server-validated anti-cheat. Set up authoritative
  settlement before treating them as prizes or secure competitive records.
  The SDK returns zero-based ranks; the provider converts them to one-based display ranks.
  UGS profile names and metadata are never shown or uploaded by this path; entries use local Pilot labels.
  Submission returns the player's own rank even when outside the displayed top 20.
- Local job timers now advance from a startup UTC anchor plus monotonic session elapsed time, ignoring
  device-date edits during the running session. UTC at startup is still untrusted; edits across process
  restarts, offline accrual and cross-device timing require persistent rewind detection and server settlement.
  This protection is not sufficient evidence for competitive progression or trusted live events.
- Manual cloud comparison, backup, restore and local undo require an explicit player action. Destructive
  backup/restore uses a second confirmation and a fresh cloud record/write lock.
  Settings > Privacy & Online Services must permit online services before comparing saves.
  Compare explicitly signs into the installation's anonymous identity if needed; it never
  requires opening Ranks first, and sign-in itself does not upload a snapshot.
  Cloud backups belong to the anonymous UGS identity on this installation. Apple/Google account
  recovery is not enabled, so reinstalling/clearing credentials or switching devices cannot recover
  that identity through this build. Do not present this as recoverable cross-device backup.
- Settings offers independent local audio preferences and Reset Progress. Reset starts a new local game and
  removes local progression recovery copies; it is not an account-deletion request or a reset of every device setting.
- Online Save & Account Deletion is separate from Reset Progress. It requires configured authentication and
  an already signed-in account and a second confirmation bound to that player's ID. Leaderboard-only
  accounts can be deleted without enabling Cloud Save; cloud removal is attempted only when configured.
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
  feature flag and a linked Unity project. Custom event emitters additionally require reviewed schemas
  configured in the Unity Dashboard.
  The app now initializes the enabled adapter on saved/new analytics opt-in, without signing into
  Authentication or opting into cloud/ranks. Failed initialization is logged and retried on explicit
  preference changes or returning to the app. This enables SDK standard session analytics; no custom
  gameplay event emitters or dashboard schemas are added by this switch.
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
- Local crew reminders use Mobile Notifications 2.5.0 and are enabled for native Android/iOS builds.
  Native-device permission/delivery/cancellation checks remain release requirements, not verified by
  the feature flag. The separate device preference starts off; Settings can enable it on supported builds.
  Permission is requested only when an active saved crew timer
  exists, not on launch. Denial does not repeatedly prompt; off/on permits an explicit retry.
  Construction/upgrade/clearance reminders share eight reserved IDs, are cancelled when their timers disappear
  or reminders are disabled, and never grant rewards or change jobs. Failed save writes suppress new reminders.
  The native build guard rejects launch authorization, push/APNs capability and exact-alarm permission.
  OS delivery timing is advisory and device-controlled; restarting the device is not configured to reschedule.
  This is local notification functionality, not a push service or server-authoritative timer system.
- Rewarded ads use the LevelPlay 9.3.2 package, native LevelPlay 9.3.0 and Unity Ads adapter 5.4.0.0.
  iOS identifiers are configured in `RewardedAdsPolicy`; Android is not configured. Real ad requests
  remain disabled until `iosSetupVerified` is explicitly enabled after the release steps below and a
  valid public HTTPS `FrontierUnityServicesConfig.privacyPolicyUrl` exists. Editor/desktop never
  initialize the live provider. The service persists per-placement counts/cooldowns in device preferences
  (not the progression save), rejects invalid stored usage and observed clock rewind, and does not deliver
  a provider reward until its usage write succeeds. Withdrawal during playback discards the pending reward,
  including withdrawal followed by re-opt-in. A configured provider exposes the harvest double-yield offer
  only when the result can fit a bonus; the extra inventory is written in the same save transaction before
  it is published to the backpack. Failed bonus writes leave the base harvest intact and report the failure.
  A fresh SDK ad instance isolates each request. Duplicate callbacks cannot grant again; reward and close
  can arrive in either order. Initialization/loading time out after 45 seconds, playback after five minutes,
  and a closed ad waits up to 30 seconds for its reward callback. Missing confirmation explicitly fails
  without a reward, rather than treating closure as completion. This is not durable server-side
  settlement: app termination or callbacks after those deadlines need a future server reward journal.
  This cannot undo an SDK request already in progress.
  Device UTC and preferences are not server authority or cross-device anti-cheat; forward clock changes
  and cleared preferences are outside this local protection. The UI remains hidden while the provider is
  unavailable. Authored daily caps/cooldowns are double-yield 8/180s, emergency-fuel 3/600s,
  timer-skip 5/300s, revive 3/0s, daily-supply 1/0s and free-shards 2/14400s.
  Only harvest double-yield has gameplay reward wiring today. Other placements and the requested
  missing-resource ad offer are not exposed yet; positive policy caps do not implement their gameplay rewards.

## iOS LevelPlay activation

- `enableIosDevelopmentTests` enables a separate development-only test-suite action, not gameplay ads.
  Export an iOS **Development Build**, enable rewarded-ad consent in Settings, then use the developer
  **Menu > Debug > Ads & online services > Open LevelPlay test suite** action. Debug now shows
  availability/status in the Editor too, with the native action disabled and an explanation.
  The suite can open only on an iOS Development Build; developer menus remain absent in release.
  Connection results appear inline without needing a second tap. After it connects, select the Unity Ads bidder, choose **Test**
  rather than **Live**, then load/show **Rewarded**. The SDK test suite also offers live ads:
  the `is_test_suite` flag does not force all traffic to test mode. Do not select live auctions.
  Register the iPhone as a LevelPlay test device as an additional dashboard safeguard.
  Development testers must be informed adults; do not distribute this diagnostic build to children.
  This path sets child-directed treatment, device-ID opt-out, no personalized consent and do-not-sell.
  Native initialization still requires the local ad opt-in and policy URL. No automatic ad request
  or reward grant is made by opening the diagnostic action. It does not verify the production setup
  or enable other online services; `iosSetupVerified` remains false.

- The bundle ID is `com.GameDevBois.spheer`. Keep App Keys/ad unit IDs in Frontier's policy asset,
  not LevelPlay's auto-initialize settings: initialization must remain behind Frontier consent.
- Native dependencies are pinned in `Assets/LevelPlay/Editor` using the vendor's dependency declarations
  and bundled network metadata. The existing dependency resolver handles CocoaPods when exporting on a Mac.
  LevelPlay's postprocessor supplies installed networks' SKAdNetwork entries; check the generated plist,
  privacy manifests, CocoaPods installation and actual device build rather than assuming export proves success.
- Configure Unity Ads bidding in LevelPlay and register the iPhone as a dashboard test device before
  requesting ads. Setting an App Key does not enable test ads. Verify loading, full completion, early close,
  offline/no fill, consent withdrawal and return-from-background on the iPhone.
- Publish the real HTTPS privacy policy and complete audience/region-specific consent review before
  changing `iosSetupVerified`. This adapter deliberately sends GDPR consent false, do-not-sell true and
  device-ID opt-out true before initialization; an ad opt-in is not tracking consent. No ATT request or
  personalized tracking is enabled. Any move to personalized ads needs appropriate CMP/ATT handling first.
- Set the LevelPlay rewarded unit's default reward to `Reward` / `1`, with dashboard pacing/capping off.
  The provider uses that default placement and returns the requested game placement ID;
  Frontier applies the game's limits and reward amounts. Dashboard placement-level reporting is not wired.
- Keep banners and forced interstitials off. Leaderboards are independent of ad activation:
  anonymous authentication and explicit Frontier Score posting are enabled as described above.

## Required before public release or enabling additional providers

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
- [ ] Define server-side deletion/retention for leaderboard records, analytics and ad processors.
  Prevent other active devices from recreating deleted data; verify token revocation and backend cleanup.
- [ ] Review Analytics retention in the Unity Dashboard, document standard session-event fields and the
  deletion path, and validate consent grant/withdrawal with test identities. Configure reviewed event
  schemas before adding custom gameplay emitters. The child-inclusive audience requires its own
  appropriate privacy/parental-consent review; the current opt-in toggle is not parental verification.
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
