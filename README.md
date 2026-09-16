# Dayglance 2

A free, local Windows schedule widget. English and Spanish. Full source included under the MIT license. No account, payment, network access, or telemetry.

## Open or update

1. Quit the running version from its tray icon → **Quit / Salir**. The window's × only hides it.
2. Extract this ZIP into a permanent folder and run **Dayglance.exe**.
3. Your existing schedule loads from `%LOCALAPPDATA%\Dayglance\schedule.json`; data is separate from the app folder.
4. If sign-in startup was enabled, open Settings and save preferences in the new copy so the shortcut points to the new executable.

Requires Windows 10/11 with .NET Framework 4.8. No installer or administrator rights. The executable is unsigned. This is a floating desktop window; Windows Show Desktop can hide it.

## Views and controls

- **Day / Día** and **Week / Semana** switch views. Weeks start on Sunday to match the timetable.
- The expand/compact icon at the **top right, beside Day / Week**, controls both views. Compact week fits 24 hours when space permits; expanded week gives activities more room and scrolls. Very small windows can still need scrolling.
- **Ctrl + mouse wheel** over the schedule zooms. Compact/expand resets zoom. Ordinary scrolling moves through the schedule.
- Week view highlights today and shows a live time line. Click a day heading for its day view or an activity to edit it.
- The bottom row has **+** (add), **≡** (manage), **⚙** (settings), and the pin indicator. Hover for labels.
- Drag the heading to move the widget. Pinning keeps it above other windows. The widget and dialogs use themed frames. Drag dialogs by their small heading; close with × or Escape.
- Day view always highlights the actual current activity. The circle button marks that occurrence done; click again to undo.

## Activities

Choose start/end hours and minutes from menus. Minutes use five-minute intervals; imported nonstandard minutes are preserved as extra choices. An earlier end means the next day. Equal start/end times are rejected.

Select repeat-day pills or Every day, Weekdays, or Once. The custom calendar is enabled for one-time activities. Editing a repeating activity changes its whole series, including past views. Single-occurrence overrides are not supported.

## Themes and language

Open **⚙ Settings / Ajustes**, select a theme and English or Español, then save preferences. Changes apply without restarting.

- **Midnight mint:** charcoal and mint.
- **Terracotta:** clay, cream, and soft greens.
- **Midcentury:** dark green and orange.
- **Horario · paper & sage:** paper, sage, and pink, inspired by the image.
- **Lavender dusk:** deep purple and lavender.
- **Cyberpunk:** near-black and orange.

**Create theme / Crear tema** takes a name, background hex color, and accent hex color, such as `#132C25` and `#F4AF8C`. It generates surfaces, borders, and readable text; reports text contrast; and suggests related/complementary accents. Click a suggestion to try it. Save the theme, then save preferences to apply it. Up to 24 custom themes are supported. To make a variation, create another theme from the current one. Activity colors remain independent of interface themes.

## Reminders

- Primary reminder: Off, At start, or 5/10/15/30/60 minutes before.
- **Additional reminder / Aviso adicional:** a second notification in minutes, hours, or days before; range 1 minute to 30 days.
- Settings controls global reminders and sound. **Test reminder / Probar aviso** uses the currently applied theme and the sound switch's current setting.
- Notifications are custom Dayglance popups with a matching clock icon and optional original two-note chime. They dismiss after 18 seconds and can reopen the app.
- Keep the app running, including in the tray. Checks run approximately every 10 seconds. Missed primary reminders catch up while the activity remains active. Extra advance reminders have a 15-minute catch-up window and expire at the activity's start. Completed occurrences do not remind. Saved history prevents duplicates after restart.
- Custom popups are not saved in Windows Notification Center and do not automatically follow Windows Do Not Disturb. Disable reminders in the app when needed. Sound also depends on Windows volume/output settings.

## Import, export, and sharing

Use **Settings → Import / Ajustes → Importar** for schedule JSON. Import confirms replacement and saves a timestamped backup first. Local theme, language, notification, and view settings are preserved. Custom themes from the file merge by ID. Export includes activities, notes, completion/reminder history, and custom themes.

The personal `Horario-2027-1.json` is supplied separately, not embedded in this ZIP. Select **Español**, **Horario · paper & sage**, and **Week / Semana** for the image-inspired view. Its activity reminders are off for testing.

Share this app ZIP with friends: it contains no personal schedule data. Each person has separate local storage. Send exported JSON separately only when you want to share its contents.

Each successful save retains the previous file as `schedule.json.bak`. If data is unreadable, the app leaves it untouched. Quit, preserve the damaged file, and restore the `.bak` as `schedule.json` if needed.

## Inicio rápido en español

1. Cierra la versión anterior desde su icono de bandeja → **Salir**.
2. Extrae el ZIP y abre **Dayglance.exe**. Se conserva tu horario local.
3. Pulsa **⚙**, elige **Español**, selecciona un tema y guarda las preferencias.
4. En **Ajustes → Importar**, abre **Horario-2027-1.json**. Confirma el reemplazo; antes se guardará un respaldo.
5. Selecciona **Semana**. El icono superior derecho alterna compacto/ampliado; **Ctrl + rueda** cambia el zoom.
6. Pulsa **+** para añadir actividades. Los avisos funcionan mientras la app siga abierta, incluso en la bandeja.

## Source and verification

`source/Build.ps1` uses the Windows .NET Framework compiler; no NuGet packages or downloads. Optional `-OutputDirectory` writes the build elsewhere. `MakeIcon.ps1` regenerates the executable icon. Use a writable folder.

- `Dayglance.exe --self-test`: **31** schedule, reminder, validation, palette, audio-load, and persistence checks.
- `Dayglance.exe --ui-test`: **24** checks of controls, calendar, extra reminders, themes, Spanish, backup, zoom, and custom notifications.
- `Dayglance.exe --showcase <schedule.json>`: **18** rendering checks across all six themes, including 24-hour fit and label spacing; screenshots are written beside the executable.

All checks passed on the development PC; tests use isolated data folders. Screens were visually reviewed. Audio data loaded successfully. Output volume, startup after actual sign-in, and operation on other PCs were not independently verified.
