# Dayglance in the Microsoft Store

Everything needed to publish Dayglance as a **private-audience** Store app from the **Acidminded** Partner Center account: how to build and test the package, and the text to paste into each Partner Center section.

## 1. Paste the product identity

Partner Center → Dayglance → Product management → **Product identity**. Copy into `store/AppxManifest.xml`, character for character:

| Partner Center | Manifest |
| --- | --- |
| Package/Identity/Name | `Acidminded.Dayglance` |
| Package/Identity/Publisher | `CN=FD4B3F58-7FC4-48B4-ABAF-E0A05B0FB110` |
| Package/Identity/PublisherDisplayName | `<PublisherDisplayName>` (`Acidminded`) |

## 2. Build the package

Needs the Windows 10/11 SDK (it brings `makeappx.exe`, `makepri.exe`, `signtool.exe` and the App Certification Kit).

```powershell
.\source\Package-Store.ps1
# -> dist\store\Dayglance-2.1.1.0.msix (unsigned, for upload)
```

The package version is `Version.cs` plus `.0` (2.1.1 → 2.1.1.0). Every upload must be higher than the last, so bump `Version.cs` for the update rehearsal (2.1.2 → 2.1.2.0). The **Store package** GitHub workflow builds the same MSIX on every pull request and keeps it as a downloadable artifact.

What the Store build (`Build.ps1 -Store`) changes:

- No self-updater: `Updates.cs`, the update timer and the update settings are compiled out. The Store build makes no network requests.
- Start at sign-in uses the manifest's `windows.startupTask` (off by default). Settings → Reminders shows a button that opens Windows Settings → Apps → Startup instead of the old Startup-folder shortcut.
- Data stays in `%LOCALAPPDATA%\Dayglance` as before; Windows redirects it to `%LOCALAPPDATA%\Packages\<package family name>\LocalCache\Local\Dayglance` and deletes it on uninstall.

## 3. Install locally and run WACK

An MSIX installs only when signed. Sign a **copy** with a throwaway certificate whose subject equals the manifest Publisher (PowerShell as admin, from `dist\store`):

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=FD4B3F58-7FC4-48B4-ABAF-E0A05B0FB110" -KeyUsage DigitalSignature -FriendlyName "Dayglance local test" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$pw = ConvertTo-SecureString -String "local-test" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath dayglance-test.pfx -Password $pw
Import-PfxCertificate -FilePath dayglance-test.pfx -CertStoreLocation Cert:\LocalMachine\TrustedPeople -Password $pw
Copy-Item Dayglance-2.1.1.0.msix Dayglance-local.msix
signtool sign /fd SHA256 /f dayglance-test.pfx /p local-test Dayglance-local.msix
Add-AppxPackage .\Dayglance-local.msix
```

If you already run the ZIP version, quit it from the tray first (both use the same single-instance lock, and the ZIP version's data is not shared with the Store version).

Check: Start menu tile and taskbar icon, launch, add an activity, quit from the tray, relaunch (activity still there), Test reminder, Settings → Data & updates says updates come through the Store, Settings → Reminders → Open startup settings shows Dayglance (turn it on, sign out and in). Then **Windows App Cert Kit → Validate Store App → Dayglance** and fix every failure. Upload the unsigned `Dayglance-2.1.1.0.msix`. `*.pfx` is git-ignored; never commit it.

## 4. Partner Center sections

### Pricing and availability

- Markets: all markets
- Visibility: **Private audience**. Known user group "Dayglance testers": your Microsoft account plus a second account. Never choose Public.
- Pricing: Free, no free trial, no sale
- Publish date: as soon as possible after certification

### Properties

- Category: Productivity (no subcategory)
- Privacy policy URL: https://acidminded.com/dayglance/privacy (Spanish version at https://acidminded.com/dayglance/privacidad; text in `docs/privacy.md` and `docs/privacidad.md`). Partner Center takes one URL; the English page links to the Spanish one.
- Website: https://github.com/acidminded95/dayglance
- Support contact info: support@acidminded.com
- Product declarations: defaults; leave the accessibility box unticked unless tested with a screen reader and keyboard only
- System requirements: minimum memory 4 GB
- Generative AI: No

### Age ratings (IARC)

Category **All other app types**. No to violence, fear, sexual content, crude humor, drugs, alcohol, tobacco, gambling and language. Users interact or share content: No. Shares location or personal info: No. Digital purchases: No. Unrestricted internet: No. Expected: Everyone / PEGI 3.

### Packages

Upload `Dayglance-2.1.1.0.msix`. Device family: Windows 10/11 Desktop only.

### Store listing (English, United States)

**Description**

> Dayglance is a small schedule widget that keeps your day in view: what you're doing now, what comes next, and gentle reminders before each activity starts.
>
> Plan a routine once and let it repeat. Each activity has a start and end time, a color, optional notes and the days it repeats on, or a single date. The day view lists your activities as cards with the current one highlighted and a progress bar; the week view lays out up to seven days with a live time line. In the week view, click an empty slot to add an activity there; click any card to edit it.
>
> Shrink Dayglance to a mini widget that shows only the current or next activity, pin it on top of other windows, or hide it in the system tray. Reminders appear as small themed cards with an optional soft chime, at the start time and, if you like, minutes, hours or days ahead.
>
> Make it yours with six built-in themes or your own, created from four colors with readable-contrast suggestions. Choose 12- or 24-hour time, three card styles and four text sizes. Dayglance speaks English and Spanish.
>
> Your schedule stays on your PC. There's no account, no ads, no analytics and no internet connection. Export your schedule to a file for backup or to share it, and import it on another PC.
>
> Reminders work while Dayglance is running, including from the tray. Turn on "start at sign-in" in Windows Settings → Apps → Startup to have it ready every day. Uninstalling Dayglance removes its saved schedule, so export a backup first if you want to keep it.

**Short description**

> Your day at a glance: a schedule widget with gentle reminders, themes and a mini view. Free, private, no account.

**What's new in this version**

> First Microsoft Store release.

**Product features**

- Day view with the current activity highlighted and its progress
- Week view of three to seven days with a live time line
- Mini widget that shows what's on now or next
- Repeating or one-time activities with colors and notes
- Reminders at the start time and an optional earlier reminder
- Six themes plus your own custom themes
- English and Spanish, 12- or 24-hour time
- Export and import your schedule as a file
- No account, no ads, no analytics; your schedule stays on your PC

**Search terms:** schedule, routine, planner, daily planner, reminders, widget, time blocking

**Copyright:** © 2026 Acidminded

**Screenshots** (`store/listing/screenshots/`, 1920×1080, rendered from the sample schedule):
1. `1-week-view.png`: "Your week at a glance, with a live time line"
2. `2-day-and-mini-widget.png`: "Day view and the mini widget"
3. `3-editor-and-terracotta.png`: "Add activities with colors, repeats and reminders"
4. `4-settings-and-lavender.png`: "Six themes, or create your own"

**Logos** (`store/listing/`): app tile icon `app-tile-icon-300.png` (300×300), box art `box-art-1080x1080.png`, poster art `poster-art-720x1080.png`.

### Store listing (Spanish, Mexico)

Uses the same screenshots and logos as the English listing.

**Descripción**

> Dayglance es un pequeño widget de horario que mantiene tu día a la vista: lo que estás haciendo ahora, lo que sigue y avisos suaves antes de que empiece cada actividad.
>
> Planea tu rutina una vez y deja que se repita. Cada actividad tiene hora de inicio y fin, un color, notas opcionales y los días en que se repite, o una sola fecha. La vista de día muestra tus actividades como tarjetas, con la actual resaltada y una barra de progreso; la vista de semana muestra hasta siete días con una línea de la hora actual. En la vista de semana, haz clic en un espacio libre para añadir una actividad ahí; haz clic en cualquier tarjeta para editarla.
>
> Reduce Dayglance a un mini widget que muestra solo la actividad actual o la siguiente, fíjalo sobre las demás ventanas u ocúltalo en la bandeja del sistema. Los avisos aparecen como pequeñas tarjetas con tu tema y un sonido suave opcional, a la hora de inicio y, si quieres, minutos, horas o días antes.
>
> Hazlo tuyo con seis temas incluidos o con los tuyos, creados a partir de cuatro colores con sugerencias de contraste legible. Elige formato de 12 o 24 horas, tres estilos de tarjeta y cuatro tamaños de texto. Dayglance está en español e inglés.
>
> Tu horario se queda en tu PC. Sin cuenta, sin anuncios, sin analítica y sin conexión a internet. Exporta tu horario a un archivo como respaldo o para compartirlo, e impórtalo en otro equipo.
>
> Los avisos funcionan mientras Dayglance esté abierto, incluso desde la bandeja. Para tenerlo listo cada día, activa el inicio automático en Configuración de Windows → Aplicaciones → Inicio. Al desinstalar Dayglance se borra el horario guardado, así que exporta un respaldo antes si quieres conservarlo.

**Descripción breve**

> Tu día de un vistazo: un widget de horario con avisos suaves, temas y vista reducida. Gratis, privado, sin cuenta.

**Novedades de esta versión**

> Primera versión en Microsoft Store.

**Características del producto**

- Vista de día con la actividad actual resaltada y su progreso
- Vista de semana de tres a siete días con la hora actual
- Mini widget con lo que está pasando ahora o lo que sigue
- Actividades que se repiten o de una sola vez, con colores y notas
- Avisos a la hora de inicio y un aviso adicional anticipado
- Seis temas y tus propios temas personalizados
- Español e inglés, formato de 12 o 24 horas
- Exporta e importa tu horario como archivo
- Sin cuenta, sin anuncios, sin analítica; tu horario se queda en tu PC

**Términos de búsqueda:** horario, rutina, agenda, planificador, recordatorios, widget, organizador

**Copyright:** © 2026 Acidminded

**Textos de las capturas:**
1. "Tu semana de un vistazo, con la hora actual"
2. "Vista de día y mini widget"
3. "Añade actividades con colores, repeticiones y avisos"
4. "Seis temas, o crea el tuyo"

The screenshots show the English interface. A Spanish set would need a small change to `--screenshots`, which is fixed to English (Tests.cs); optional, since Spanish listings may use English screenshots.

### Submission options

- Publishing hold: publish as soon as it passes certification
- Restricted capabilities → **runFullTrust**:

> Dayglance is a desktop (WPF, .NET Framework 4.8) app packaged as MSIX. It needs runFullTrust because it runs as a standard Win32 desktop process. It stores its data only in its own app data folder, installs no drivers or services, makes no system-wide changes and does not access other apps' data.

- **Notes for certification:**

> No account or sign-in is needed, and the app makes no network requests. On first launch Dayglance opens a small widget window. To try it in two minutes: click + in the title bar, enter a title, set a start time a minute or two from now, keep "Remind me when it starts" on and Save. The activity appears in the day view, and a small reminder card appears in the bottom-right corner at the start time. Day / Week switches views; the expand/shrink button switches to the mini widget. Settings (gear icon) → Reminders → Test reminder shows a reminder immediately. Closing the window with × hides Dayglance in the system tray (it keeps running so reminders work); right-click the tray icon → Quit to exit. Start-at-sign-in is an optional startup task, off by default, which the user turns on in Windows Settings → Apps → Startup.

## 5. Lessons log

| Area | What happened | Time it took | Fix | Applies to the next app? |
| --- | --- | --- | --- | --- |
| Name reservation and identity paste |  |  |  |  |
| MSIX build errors |  |  |  |  |
| WACK results |  |  |  |  |
| runFullTrust: extra questions or delay? |  |  |  |  |
| Privacy policy accepted as written? |  |  |  |  |
| IARC rating received |  |  |  |  |
| Listing assets rejected or resized? |  |  |  |  |
| Certification time and report |  |  |  |  |
| Private audience: how a tester gets in (link, sign-in, Store app) |  |  |  |  |
| Install size, install time, data folder, uninstall |  |  |  |  |
| Update submission: time to reach testers |  |  |  |  |
| Emails or messages from Microsoft |  |  |  |  |
| Packaging a csc-only WPF app | No Visual Studio project, so the MSIX is a hand-written manifest + `makepri` + `makeappx` (`source/Package-Store.ps1`). | 1 session | Electron gets this from electron-builder's `appx` target instead. | Partly: the identity, tiles, startupTask and runFullTrust text carry over. |
| Self-updater and Startup-folder shortcut | Both had to go for MSIX. Compiled out with a `STORE` define so the GitHub ZIP keeps working unchanged. | 1 session | Electron: disable electron-updater when `process.windowsStore` is true; use the appx `startupTask`. | Yes |
