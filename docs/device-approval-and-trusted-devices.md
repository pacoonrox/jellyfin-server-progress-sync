# Device approval and trusted devices

The shared Device Approval API is the primary Quick Sign-On flow. Jellyfin's legacy Quick Connect endpoints remain available as a compatibility layer for native clients such as Roku and Swiftfin.

## Security model

A trusted-device record belongs to one Jellyfin user and one client installation. On a later direct login, Jellyfin validates the password first and then may use the installation credential instead of asking for TOTP. Trust never creates or extends a session or overrides account and permission checks. Administrator-granted trust also prevents that installation from triggering the global idle logout policy.

The installation credential is 256 random bits. The server stores its SHA-256 digest, never the reusable value. Native clients should keep it in Keychain, Keystore, or equivalent secure platform storage and preserve it across routine app and OS upgrades. Jellyfin Web uses origin-scoped application storage. Clearing app data or reinstalling creates a new credential and requires verification again. Friendly names, client versions, operating-system data, and IP addresses are telemetry, not identity credentials.

Trust does not renew when used. A password reset, 2FA reset, account-wide forced logout, device reuse anomaly, per-user revoke-all, global revoke-all, or disabling the policy invalidates applicable trust records immediately.

Administrators may mark an observed device as **Never trust**. That state survives later observations and blocks automatic trust issuance until an administrator explicitly trusts the device again or prunes its record.

## User workflow

After password validation requires 2FA, eligible users see **Trust this device for 30 days**, checked by default. When the global inactivity policy is enabled, users cannot self-issue trust; an administrator may still trust an observed installation. After an automatic logout, that installation must complete a fresh direct 2FA check once before any administrator-issued bypass can be used again; the password is always required.

The **Trusted devices** dashboard also owns the server-wide inactivity policy. One timeout applies to every account, including administrators, and can sign out only the inactive device, all devices for that user, or all of that user's devices except the inactive device. Device-only is the default scope and a timeout of zero disables the policy. An administrator-trusted installation remains exempt as an inactivity trigger; if another installation triggers a user-wide logout, trusted installations included by the selected scope are logged out and must complete a fresh 2FA check.

**Quick Sign-On** creates an anonymous five-minute request. It sends no username, password, or intended account. Every signed-in user sees the shared queue. Selecting a device opens an explicit approval prompt. Confirming **Approve** signs the device into the approving user's account. **Not this device** returns it to the queue.

Roku, Swiftfin, and other clients that implement Jellyfin's legacy Quick Connect protocol remain supported. Start Quick Connect in the app, then enter its six-digit code in the **Legacy Quick Connect** section at the bottom of the Quick Sign-On portal. The shared portal setting enables or disables both flows.

Only a session whose current access token was created by direct password authentication (and direct TOTP when the account has 2FA enabled) can select or confirm. Portal sessions and sessions that used a trusted-device TOTP bypass cannot approve another request; the user must perform a fresh direct login with TOTP when required.

## Administration

Open **Dashboard → Trusted devices** to:

- enable or disable the feature and set the default duration (1–3650 days);
- set one global idle timeout and choose its device scope for every user;
- search/filter records grouped by user;
- trust a specifically observed user/installation pair, including automatic-logout accounts;
- rename devices and edit expiration;
- revoke one device, one user's devices, or all credentials;
- log out one device, all devices for one user, or every user's devices;
- review first/last seen, client/platform, trust source/state/times, last IP, and security audit events.

These endpoints require the server-side Administrator role. Queue IP addresses are serialized only for administrators. Regular users have no trusted-device management API.

## Client API summary

- `POST /DeviceApproval/Requests` anonymously creates a request. Supply the installation credential plus optional platform telemetry; standard Jellyfin authorization metadata supplies device/client information.
- `GET /DeviceApproval/Requests/Status?secret=...` polls from the requesting installation. The secret is random, expires after five minutes, and the successful authentication result is consumed once.
- `GET /DeviceApproval/Queue` lists the shared queue for any authenticated user. Requesting IP is administrator-only.
- `POST /DeviceApproval/Queue/{id}/Select` starts approval confirmation.
- `POST /DeviceApproval/Queue/{id}/Confirm` accepts `{ Matches, TrustDevice }` and atomically completes the first valid approval.
- `POST /QuickConnect/Initiate`, `GET /QuickConnect/Connect`, and `POST /Users/AuthenticateWithQuickConnect` provide native-client compatibility.
- `POST /QuickConnect/Authorize?code=...` approves a legacy request for any authenticated user; it has no administrator-only role requirement.
- Successfully authenticated Jellyfin clients are recorded regardless of login method. A client that was already authenticated before an update is recorded when it next establishes an active session; old token records are never scanned or imported. Seerr-family service connections are excluded. Clients without a reusable installation credential remain observed and do not gain a future trusted-device 2FA bypass.

Old clients can continue normal username/password/TOTP login and can use the legacy Quick Connect code flow. They cannot create trusted status without sending a secure installation credential and explicit opt-in.
