# Administration

Open **Admin** from the game header, or **Menu → Administration** on a phone. The dedicated dashboard lives at `/pyramid/admin` when the application is installed under `/pyramid/`. Sign in with an administrator account; every administration API also enforces the role on the server.

## Explorers and activity

The overview counts explorers, online users, pending email approvals, shared expeditions, completed adventures and suspended accounts. Search explorers by name or email and filter by online, pending, suspended or administrator status. The page refreshes read-only information every 15 seconds while visible, preserving the selected explorer and unfinished forms.

Select an explorer to review their email status, registration and sign-in history, current location, level, score, moves, discoveries, inventory and achievements. Progress reflects the currently active personal or shared adventure. Online status requires a valid session and activity within the last 45 seconds; it is not a guarantee that someone is looking at their screen.

**Activity & email** shows recent administrative actions, successful and failed sign-ins, and outgoing mail status. A sent message means the mail provider accepted it, not that it reached the recipient's inbox. The email form requires an approved address and the explorer's notification preference permits delivery.

## Account actions

| Action | Effect |
| --- | --- |
| Kick | End current sessions. The explorer can sign in again; progress is preserved. |
| Suspend | End sessions and prevent sign-in until access is restored. |
| Restore access | Remove suspension. It does not approve an email address or reset an adventure. |
| Approve email manually | Approve the displayed pending/current address without following an emailed verification link. Requires the exact explorer name and records the administrative approval. |
| Reset personal adventure | Return the personal adventure to its beginning, clearing its progress, inventory, score and achievements. Shared expedition progress remains separate. Requires the exact explorer name. |
| Delete account and data | Remove the account and personal save. Requires the exact explorer name. See [accounts and email](Accounts-and-Email.md) for shared-world handling and retained history. |

For manual approval, check the address shown before submitting. The server rejects approval if that address has changed since the form was prepared. Email approval unlocks gameplay and enables email recovery; it does not remove an existing suspension. Administrators can approve their own email and reset their own personal adventure, but cannot kick, suspend or delete their current account from this screen.

## Shared expedition resets

The **Expeditions** section shows owners, members, locations, online status, content packs, invitations and join requests. Expand **Reset shared expedition** and type its exact name to confirm. This returns the world and every member's expedition progress, inventory and achievements to the beginning. Coordinate with the group before resetting a shared world.

## Portable backups

Use **Backups → Download backup** to export a JSON snapshot of accounts and gameplay. It includes users, password hashes, email addresses, personal saves, shared expeditions, active-game selections, login history and audit records. Treat the download as private account data: store it securely and never attach it to public issues or commit it to Git.

The export excludes server settings and SMTP credentials, Data Protection keys, content-pack files, sessions, email tokens and the outgoing mail queue. Keep matching content packs separately. This export is useful for gameplay recovery and migration; it does not replace a complete host backup. See [deployment](Deployment.md).

To restore:

1. Use the GOD owner account when this installation has an owner binding; otherwise use an administrator. Install compatible versions of the content packs referenced by the backup on the destination server.
2. Open **Backups**, select the JSON file (at most 20 MiB), and check that it is the intended snapshot.
3. Type **REPLACE ALL DATA**, then select **Restore backup**.
4. Sign in using an administrator account from the imported backup.

Restoration replaces all accounts and game progress. Every existing session is revoked, including the administrator's. Selecting a file alone does not import anything. Before replacement, the server writes a recovery export into `ImportRecovery` beneath its private data directory. If the imported backup removes the administrator account you were using, an operator with filesystem access must retrieve that recovery file; keep a known administrator login from the imported backup available before proceeding.

The importer validates the snapshot and its references against installed content. A rejected file leaves the current data in place. Configure the web server's upload limit for the import endpoint as described in the deployment guide.

## Runtime settings

Open **Settings** to manage registration, the public game URL, a site announcement, donation/support links, the default chime preference and email delivery. These settings apply to the current installation without changing gameplay prices or campaign rules. Hosting, TLS certificates and scheduled filesystem backups remain operator-managed.

Closing registration prevents new accounts while existing explorers can still sign in. An announcement is an informational banner; it does not stop gameplay. A saved browser sound preference takes precedence over the server's default chime setting.

Email settings include delivery mode, hostname, port, SMTP username, sender address/name and a replacement password. Port **465** uses implicit TLS; **587** requires STARTTLS. Certificate validation remains enabled. The public game URL is used in verification, recovery and invitation links and must include the application folder. SMTP settings should use the provider's authorized sender and application password where required.

The password field is write-only. The dashboard reports whether a password is configured but never returns it. Leave it blank to retain the current secret, enter a replacement to change it, or disable delivery before selecting **Clear the configured SMTP password**. The SMTP password is encrypted inside the private SQLite settings record and are excluded from portable gameplay exports; preserve the server's key material and protected settings in complete host backups.

Save before using **Send test email**. That button queues one test message to the approved address of the currently signed-in administrator. Merely opening the form or saving settings does not send a test message. Check **Activity & email** for delivery status. Development capture mode writes a private test message instead of contacting a mail provider.

Settings carry a revision number. If another administrator saves first, the server rejects the stale update. Choose **Reload saved settings**, review the current values, then reapply your changes. Reload discards unsaved edits; the dashboard's normal background refresh never overwrites the settings form.

## Lists and log maintenance

Explorer, expedition, member and activity lists scroll within the dashboard. Focus a scroll area to use the keyboard, or swipe it on a phone. **Clear filters** resets the explorer search and status filter; it never deletes accounts or adventures.

**Activity & email** has separate clearing controls for administrator activity, sign-in history and completed email delivery records. Expand the desired control and type **CLEAR LOGS**. Clearing sign-in history also clears total-login and last-login statistics derived from those records. Clearing administrator activity retains a new audit record of the clearing action. Clearing completed mail records leaves pending delivery queued. No control bulk-deletes accounts or gameplay, and the other log categories remain unchanged.


## Roles and the installation owner

**Explorer** and **Administrator** are the assignable account roles. Choose another explorer, review their current role, select the new role and type their exact name to confirm. Role changes end that account's existing sessions; the explorer signs in again with the new permissions. Saved adventures are preserved. Stale role changes are rejected, and an administrator cannot change their own role through the dashboard.

**GOD** identifies the installation owner. It is provisioned only by an operator using the explicit `Pyramid:BootstrapGod` option for an existing active account, together with `Pyramid:BootstrapOnly true`. Registration, the role picker and ordinary administrator APIs cannot grant this role. Remove bootstrap arguments after the one-time operation.

Ordinary administrators cannot demote, kick, suspend, delete, manually approve or reset the bound owner's account, or reset shared expeditions containing that owner. Once an owner is bound, full backup import is owner-only. Imported data must retain that same owner identity as an active administrator; an import attempting to rename, remove, suspend or demote it is rejected before replacement.

The owner binding belongs to the installation and is excluded from portable gameplay exports. Importing a file cannot install a different owner. Keep the binding in complete host backups and follow operator recovery procedures when moving an installation. The role catalog reserves no working moderator role: only roles actually implemented by the server can be selected.

Runtime settings saved through this dashboard take precedence over initial environment settings, including email mode and sender configuration. Changing an environment variable later does not replace that saved record. The GOD role currently provides owner protection and restore authority; it does not enable puzzle cheats or a debug console.

Adventure installation remains an operator task. The browser designer stores drafts on the author's device and exports JSON; the dashboard does not accept uploads or automatically publish user-authored packs. See [Modding](Modding.md) for validation and installation.
