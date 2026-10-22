# WoW addons

## Install Grub Roster Probe

1. Close WoW, or return to character selection.
2. Copy the **`GrubRosterProbe` folder** into the Forever client's
   `Interface/AddOns/` directory. The final path must be
   `Interface/AddOns/GrubRosterProbe/GrubRosterProbe.toc`, not a nested duplicate.
   Use the installation opened by Battle.net for Forever; do not assume a retail
   or Classic installation is the correct one.
3. Enable **Grub Roster Probe** in the character-selection AddOns list and log in.
4. Run `/grubprobe` to open a copyable report of the current cache.
5. Click **Request refresh**, wait a few seconds, then **Capture**. Click
   **Select all**, then press **Ctrl+C** (Cmd+C on macOS).

The manifest targets interface `16001`. If the beta changes it, check the client
with `/dump select(4, GetBuildInfo())`. For this test addon, enable loading
out-of-date addons or update the manifest to the number reported by your client;
do not assume a successful load means every probed API works.

Commands: `/grubprobe`, `/grubprobe capture`, `/grubprobe refresh`.
See [probe behaviour and the in-game test checklist](docs/roster-probe.md).

## Local tests

Node.js 24 is needed for development tests only, not for installing the addon.
From `addons/`:

```sh
npm ci
npm test
```

The tests parse the Lua as 5.1 and execute it with mocked WoW APIs in Wasmoon's
Lua runtime. They also exercise the window/command callbacks using frame stubs.
This checks collection and export logic, not the actual client API, restrictions,
fonts, scrolling, or clipboard behaviour. The in-game checklist is still required.

There is no build step or runtime dependency. Copy only `GrubRosterProbe/` into
WoW, not `node_modules/`, the tests, or the rest of this repository.
