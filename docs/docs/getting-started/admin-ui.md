---
sidebar_position: 3
---

# 🛠️ Admin UI

ImmichFrame ships with a built-in admin interface at **`/admin`** where every setting —
display options, weather, calendars, and your Immich accounts — can be edited from the
browser. Changes are applied **live**, without restarting the container. That does not
apply when settings come from a config file
([declarative file config](./configuration.md#declarative-file-config)): `/admin` only
explains that, and edits apply on restart.

## What's on each page

| Page | What it does |
| --- | --- |
| **Photos** | Pick an account, then choose what to *include* (favorites, memories, albums, people, tags), what to *never show* (albums), and how to *narrow it down* (date taken, minimum rating, videos, archived). Albums, people and tags are picked by name from the account's library, with photo counts, and the page shows how many photos the current choices match. *Up Next* shows a sample from the frame's real pool, so screenshots or receipts that slip in are easy to spot. |
| **Slideshow & Look** | A live *Preview* (click it to see it large), then timing, layout, photo size, movement, what appears on the photo (clock, date taken, place, people…), text and colors, language, weather and calendars. Date and clock formats are picked from examples, with a *Custom* option for any [date-fns pattern](https://date-fns.org/docs/format). |
| **Screens** | Every device that has shown the frame, with when it was last seen; rename or forget one from its ⋯ menu. *Add a Screen* gives per-device steps, the address and secret with copy buttons, and a QR code that opens the frame already signed in. |
| **Access & Accounts** | Immich accounts (server address, API key or key file, *Test Connection*), the frame secret, the admin password, and advanced options (image cache, refresh interval, webhook). *Recent Changes* lists every save with what it changed and can undo it; *Backup* downloads all settings as one file (secrets left out) or restores one. |

Type in **Search** at the top of the sidebar (or of the list on a phone) to jump straight to
any setting. Edits are collected until you press **Save** in the bar at the bottom (or ⌘S /
Ctrl+S); **Discard** goes back to the saved settings. On a phone, the admin opens on a list
of the four pages, like the Settings app.

A screen appears under *Screens* once it sends a `clientIdentifier` (every web client does;
set a readable one with `?client=Living%20room`). When a frame secret is set, only requests
with the right secret are recorded. A screen that asks with a wrong secret, or none, is listed
under *Turned away* instead (kept in memory for a week), so a device left with an old secret
after you change it is easy to find. It leaves the list once it connects with the right one.

## Enabling the admin UI

The admin UI is protected by a dedicated admin password. Which screen `/admin` shows
depends on the state of your instance:

| State        | When                                                                 | What you see                                                       |
| ------------ | -------------------------------------------------------------------- | ------------------------------------------------------------------ |
| **Setup**    | Fresh install: no admin password anywhere and nothing configured yet | An anonymous onboarding screen where you choose the admin password |
| **Login**    | An admin password exists (environment variable or database)          | The password prompt                                                |
| **Disabled** | The instance is already configured but has no admin password         | A notice — onboarding stays shut so nobody can claim your instance |
| **File**     | `IMMICHFRAME_CONFIG_SOURCE=file`                                     | A notice — settings come from the config file and cannot be edited |

So on a brand-new instance you do **not** need to set anything: open `/admin` and pick a
password. To choose it up front instead — or to open the admin UI on an instance that is
already configured — set the `IMMICHFRAME_ADMIN_PASSWORD` environment variable:

```yaml title="docker-compose.yml"
services:
  immichframe:
    image: ghcr.io/immichframe/immichframe:latest
    environment:
      IMMICHFRAME_ADMIN_PASSWORD: your-secret-password
    volumes:
      - ./config:/app/Config
```

Alternatively you can set the `AdminPassword` setting (in the `General` section) via the
admin UI itself once you are logged in. The environment variable **always wins** over a
password stored in the database — if you ever lock yourself out by saving a wrong password,
set `IMMICHFRAME_ADMIN_PASSWORD` and restart the container to regain access. It does not
open the admin UI when `IMMICHFRAME_CONFIG_SOURCE=file`.

:::caution
The admin UI exposes your Immich API keys to authenticated admins and transmits them in
plain text. Run ImmichFrame behind HTTPS (e.g. a reverse proxy) if it is reachable from
outside your trusted network.
:::

## Where settings are stored

Settings edited in the admin UI are stored in a SQLite database (`immichframe.db`) inside
the config directory (`/app/Config` in Docker). This makes the config directory the one
place to persist:

- Mount `/app/Config` as a **writable** volume, otherwise saving settings fails.
  This does not apply when
  [`IMMICHFRAME_CONFIG_SOURCE=file`](./configuration.md#declarative-file-config) is set —
  a read-only mount works in that mode.
  The container runs as uid `1000`, so a bind-mounted host directory has to belong to it:
  `chown -R 1000:1000 /path/to/config`. If it does not, the container stops at startup with
  `Cannot open the settings database in '/app/Config'`.
- **The database is the source of truth** unless
  [`IMMICHFRAME_CONFIG_SOURCE=file`](./configuration.md#declarative-file-config) is set.
  On the very first start, an existing `Settings.json`, `Settings.yml` or `Settings.yaml`
  is imported into the database once. After that, changes to that file are ignored (a log
  line reminds you of this at startup) and points at the file-mode variable if you want the
  file to stay in charge. Configuration from environment variables is **not** imported —
  accounts and display settings that used to be set through the environment have to be
  migrated by hand.
- Back up the config directory to keep your settings.

## Zero-configuration start

ImmichFrame now also starts **without any configuration**: the slideshow shows a hint that
no accounts are configured, and you can do the entire initial setup — including adding
your first Immich account — through the admin UI.

Related change: the container no longer exits when an Immich server is unreachable or
incompatible at startup. It logs a critical warning instead, so you can fix the account
settings via the admin UI. In file mode the log tells you to fix the config file and restart.

## Applying changes

With [`IMMICHFRAME_CONFIG_SOURCE=file`](./configuration.md#declarative-file-config), none of
the below applies: edit the config file and restart.

- **Display settings** (interval, layout, clock, …) are served live; slideshow devices pick
  them up the next time the page loads.
- **Account changes** rebuild the asset pipeline in the background — no restart needed.
- **Weather and calendar** changes take effect within ~15 minutes (internal caches).
- Every account has a **Test** button to verify the server URL and API key before saving.
- The last 50 saves are kept, so any change can be undone from *Recent changes*. Undoing
  never changes the admin password.

The album, people and tag pickers use the account's API key. A key with only read
permissions (`asset.read`, `album.read`, `person.read`, `tag.read`, `memory.read`) is
enough; with `user.read` as well, each account is labelled with its Immich user name.
