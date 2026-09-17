# Device approval and trusted devices

The shared Device Approval API is the primary Quick Sign-On flow. Jellyfin's legacy Quick Connect endpoints remain available as a compatibility layer for native clients such as Roku and Swiftfin.

## Security model

A trusted-device record belongs to one Jellyfin user and one client installation. On a later direct login, Jellyfin validates the password first and then may use the installation credential instead of asking for TOTP. Trust never creates a session by itself, extends a session, changes idle logout, or overrides account and permission checks.

The installation credential is 256 random bits. The server stores its SHA-256 digest, never the reusable value. Native clients should keep it in Keychain, Keystore, or equivalent secure platform storage and preserve it across routine app and OS upgrades. Jellyfin Web uses origin-scoped application storage. Clearing app data or reinstalling creates a new credential and requires verification again. Friendly names, client versions, operating-system data, and IP addresses are telemetry, not identity credentials.

Trust does not renew when used. A password reset, 2FA reset, account-wide forced logout, device reuse anomaly, per-user revoke-all, global revoke-all, or disabling the policy invalidates applicable trust records immediately.

Administrators may mark an observed device as **Never trust**. That state survives later observations and blocks automatic trust issuance until an administrator explicitly trusts the device again or prunes its record.

## User workflow

After password validation requires 2FA, eligible users see **Trust this device for 30 days**. It is opt-in and unchecked. Accounts with a nonzero inactivity-logout value cannot self-issue trust. They continue to log out on the existing inactivity schedule; an administrator may later trust one observed installation for that account. After an automatic logout, that installation must complete a fresh direct 2FA check once before any administrator-issued bypass can be used again; the password is always required.

**Quick Sign-On** creates an anonymous five-minute request. It sends no username, password, or intended account. Every signed-in user sees the shared queue. Selecting a device opens an explicit approval prompt. Confirming **Approve** signs the device into the approving user's account. **Not this device** returns it to the queue.

Roku, Swiftfin, and other clients that implement Jellyfin's legacy Quick Connect protocol remain supported. Start Quick Connect in the app, then enter its six-digit code in the **Legacy Quick Connect** section at the bottom of the Quick Sign-On portal. The shared portal setting enables or disables both flows.

Only a session whose current access token was created by direct password authentication (and direct TOTP when the account has 2FA enabled) can select or confirm. Portal sessions and sessions that used a trusted-device TOTP bypass cannot approve another request; the user must perform a fresh direct login with TOTP when required.

## Administration

Open **Dashboard → Trusted devices** to:

- enable or disable the feature and set the default duration (1–3650 days);
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

Old clients can continue normal username/password/TOTP login and can use the legacy Quick Connect code flow. They cannot create trusted status without sending a secure installation credential and explicit opt-in.
