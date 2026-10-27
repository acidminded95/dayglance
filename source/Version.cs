[assembly: System.Reflection.AssemblyVersion("2.1.2")]
[assembly: System.Reflection.AssemblyFileVersion("2.1.2")]
[assembly: System.Reflection.AssemblyTitle("Dayglance")]
[assembly: System.Reflection.AssemblyProduct("Dayglance")]

namespace Dayglance {
 // Bump Version for every release and tag the commit as v<Version> (the release workflow checks they match).
 public static class AppInfo {
  public const string Version="2.1.2";
  public const string Repository="acidminded95/dayglance";
  // Build.ps1 -Store compiles with STORE: no self-updater (the Store updates the app) and a manifest startup task.
#if STORE
  public const bool Store=true;
#else
  public const bool Store=false;
#endif
 }
}
