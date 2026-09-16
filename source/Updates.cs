using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Dayglance {
 public class UpdateInfo { public Version Version; public string Tag,Notes,DownloadUrl,PageUrl; }
 // Checks the latest GitHub release and applies it: download the release ZIP, then a helper script waits for
 // Dayglance to exit, replaces the previous app files (listed in dayglance-files.txt), and restarts the app.
 // Schedule data lives in %LOCALAPPDATA% and is never touched.
 public static class Updater {
  public static Version Current { get { return ParseTag(AppInfo.Version); } }
  public static Version ParseTag(string tag) { Version version; string text=(tag??"").Trim().TrimStart('v','V'); return Version.TryParse(text,out version)?version:null; }
  public static bool IsNewer(UpdateInfo info) { return info!=null && info.Version!=null && Current!=null && info.Version>Current; }
  static WebClient Client() {
   ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072; // TLS 1.2
   var client=new WebClient(); client.Headers[HttpRequestHeader.UserAgent]="Dayglance/"+AppInfo.Version; return client;
  }
  public static UpdateInfo FetchLatest() {
   using(var client=Client()) {
    client.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";
    var data=new JavaScriptSerializer().DeserializeObject(client.DownloadString("https://api.github.com/repos/"+AppInfo.Repository+"/releases/latest")) as Dictionary<string,object>;
    if(data==null) throw new Exception("Unexpected response from GitHub.");
    var info=new UpdateInfo { Tag=Value(data,"tag_name"),Notes=Value(data,"body"),PageUrl=Value(data,"html_url") }; info.Version=ParseTag(info.Tag);
    object assets; if(data.TryGetValue("assets",out assets) && assets is IEnumerable)
     foreach(var item in (IEnumerable)assets) { var asset=item as Dictionary<string,object>; if(asset==null) continue; string name=Value(asset,"name");
      if(name.StartsWith("Dayglance",StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)) { info.DownloadUrl=Value(asset,"browser_download_url"); break; } }
    return info;
   }
  }
  static string Value(Dictionary<string,object> data,string key) { object value; return data.TryGetValue(key,out value) && value!=null?Convert.ToString(value):""; }
  // Downloads and unpacks the release, then starts the helper script. The caller must exit the app right after.
  public static void Install(UpdateInfo info) {
   if(string.IsNullOrEmpty(info.DownloadUrl)) throw new Exception("This release has no Dayglance ZIP to install.");
   string work=Path.Combine(Path.GetTempPath(),"Dayglance-update-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
   string zip=Path.Combine(work,"update.zip"); using(var client=Client()) client.DownloadFile(info.DownloadUrl,zip);
   string files=Path.Combine(work,"files"); System.IO.Compression.ZipFile.ExtractToDirectory(zip,files);
   if(!File.Exists(Path.Combine(files,"Dayglance.exe"))) { var inner=Directory.GetDirectories(files).FirstOrDefault(d=>File.Exists(Path.Combine(d,"Dayglance.exe"))); if(inner==null) throw new Exception("The update package does not contain Dayglance.exe."); files=inner; }
   string target=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\','/');
   string script=Path.Combine(work,"apply-update.ps1"); File.WriteAllText(script,Script,new UTF8Encoding(true));
   var start=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+script+"\" -ProcessId "+Process.GetCurrentProcess().Id+" -Source \""+files+"\" -Target \""+target+"\"") { UseShellExecute=false,CreateNoWindow=true };
   Process.Start(start);
  }
  const string Script=@"param([int]$ProcessId,[string]$Source,[string]$Target)
$ErrorActionPreference = 'Stop'
$logFolder = Join-Path $env:LOCALAPPDATA 'Dayglance'; New-Item -ItemType Directory -Force -Path $logFolder | Out-Null
$log = Join-Path $logFolder 'update.log'
function Log([string]$message) { Add-Content -LiteralPath $log -Value ((Get-Date -Format o) + '  ' + $message) }
$backup = Join-Path $Target '.dayglance-previous'
try {
 Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue
 Start-Sleep -Milliseconds 400
 $manifest = Join-Path $Target 'dayglance-files.txt'
 if (Test-Path -LiteralPath $manifest) { $old = Get-Content -LiteralPath $manifest } else { $old = @('Dayglance.exe','Dayglance.exe.config','README.md','LICENSE.txt','Schedule-format.md') }
 if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
 New-Item -ItemType Directory -Force -Path $backup | Out-Null
 foreach ($name in $old) {
  if ([string]::IsNullOrWhiteSpace($name) -or $name -match '\.\.|:|^[\\/]') { continue }
  $path = Join-Path $Target $name
  if (Test-Path -LiteralPath $path -PathType Leaf) { Move-Item -LiteralPath $path -Destination (Join-Path $backup ([IO.Path]::GetFileName($name))) -Force }
 }
 Copy-Item -Path (Join-Path $Source '*') -Destination $Target -Recurse -Force
 Remove-Item -LiteralPath $backup -Recurse -Force
 Log ('Updated ' + $Target)
} catch {
 Log ('Update failed: ' + $_)
 if (Test-Path -LiteralPath $backup) { Get-ChildItem -LiteralPath $backup -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $Target -Force } }
}
Start-Process -FilePath (Join-Path $Target 'Dayglance.exe')
";
 }
}
