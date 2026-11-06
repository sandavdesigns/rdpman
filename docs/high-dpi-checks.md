# Windows high-DPI checks

These visual checks require Windows; a cross-build on macOS cannot verify them.

- Start at 100%, 150%, 200%, and 250% display scaling. Check sidebar titles,
  machine names, SSH/LIVE badges, clipboard toggles, and all Setup tabs for clipping.
- Move the open window between monitors with different scaling, in both directions.
  Repeat with an open settings dialog and an active RDP/SSH session.
- Populate enough connected and disconnected servers to overflow both lists.
  Drag each scrollbar, click its track, use the mouse wheel, and navigate with
  arrows, Page Up/Down, Home, and End. The thumb must follow the visible rows.
- Scroll to the first and last server and select it. Toggle clipboard redirection
  after scrolling at each scale; only the clicked server should change.
- Check empty, short, and filtered lists. No scrollbar thumb should appear when
  everything fits. Clear the filter while scrolled to the end.
- Resize the window, toggle fullscreen, and reconnect. Both server lists must
  remain reachable, including when all servers are connected.
- Check credential/group lists and color choices at each scale.
