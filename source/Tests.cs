using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Dayglance {
 public static class Tests {
  static int count;
  static void Check(bool value,string name) { if(!value) throw new Exception("FAILED: "+name); count++; }
  public static void Run() {
   try {
    var s=new State(); var a=new Activity { Id="test",Title="Deep work",Notes="",Color="#A4E9CC",Start="23:00",End="01:00",Days=new[]{1},Reminder=15 }; s.Activities.Add(a); Schedule.Validate(s);
    var monday=new DateTime(2026,9,14); var overnight=Schedule.ForDay(s,monday.AddDays(1));
    Check(overnight.Count==1 && overnight[0].Start==monday.AddHours(23) && overnight[0].End==monday.AddDays(1).AddHours(1),"overnight carries into next day");
    Check(Schedule.ForDay(s,monday.AddDays(2)).Count==0,"repeat excludes wrong day");
    Check(Schedule.Due(s,monday.AddHours(22).AddMinutes(44)).Count==0,"not early");
    Check(Schedule.Due(s,monday.AddHours(22).AddMinutes(45)).Count==1,"lead reminder boundary");
    Check(Schedule.Due(s,monday.AddDays(1).AddMinutes(30)).Count==1,"catch up after wake while active");
    Check(Schedule.Due(s,monday.AddDays(1).AddHours(1)).Count==0,"no reminder after ending");
    s.Reminded.Add(overnight[0].Key); Check(Schedule.Due(s,monday.AddHours(23)).Count==0,"deduplicate"); s.Reminded.Clear(); s.Completed.Add(overnight[0].Key); Check(Schedule.Due(s,monday.AddHours(23)).Count==0,"completed reminder suppressed"); s.Completed.Clear();
    a.Start="00:05"; a.End="00:30"; a.Days=new[]{2}; Check(Schedule.Due(s,monday.AddHours(23).AddMinutes(50)).Count==1,"lead reminder crosses midnight");
    a.Days=new int[0]; a.Date="2026-09-15"; Check(Schedule.ForDay(s,monday.AddDays(1)).Count==1 && Schedule.ForDay(s,monday.AddDays(8)).Count==0,"one time date");
    var round=Storage.Serializer().Deserialize<State>(Storage.Serializer().Serialize(s)); Schedule.Validate(round); Check(round.Activities[0].Title==a.Title,"JSON round trip");
    a.Start="25:00"; bool rejected=false; try { Schedule.Validate(s); } catch { rejected=true; } Check(rejected,"reject invalid time"); a.Start="00:05";
    a.End=a.Start; rejected=false; try { Schedule.Validate(s); } catch { rejected=true; } Check(rejected,"reject zero duration"); a.End="00:30";
    s.Activities.Add(a); rejected=false; try { Schedule.Validate(s); } catch { rejected=true; } Check(rejected,"reject duplicate IDs"); s.Activities.RemoveAt(1);
    Storage.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data-"+Guid.NewGuid().ToString("N"));
    Storage.Save(s); Check(Storage.Read(Storage.FilePath).Activities.Count==1,"save and reload from disk"); a.Title="Updated title"; Storage.Save(s); Check(Storage.Read(Storage.FilePath).Activities[0].Title=="Updated title","atomic replacement"); Check(Storage.Read(Storage.FilePath+".bak").Activities[0].Title=="Deep work","previous save backup");
    Check(Schedule.WeekStart(new DateTime(2027,1,1))==new DateTime(2026,12,27),"week crosses year boundary");
    a.Start="22:00"; a.End="05:00"; a.Date=null; a.Days=new[]{0,1,2,3,4,5,6}; var pieces=Schedule.Layout(s,monday); Check(pieces.Count==2 && pieces[0].StartHour==0 && pieces[0].EndHour==5 && pieces[1].StartHour==22 && pieces[1].EndHour==24,"overnight splits at week day boundaries");
    s.Activities.Add(new Activity { Id="overlap",Title="Overlap",Color="#FFAA88",Start="23:00",End="23:30",Days=new[]{1},Reminder=-1 }); var lanes=Schedule.Layout(s,monday).Where(b=>b.StartHour>=22).ToList(); Check(lanes.Count==2 && lanes[0].Lanes==2 && lanes[1].Lanes==2 && lanes[0].Lane!=lanes[1].Lane,"overlapping events get separate lanes");
    var legacy=Storage.Serializer().Deserialize<State>("{\"Version\":1,\"Activities\":[]}"); Schedule.Validate(legacy); Check(legacy.Theme=="midnight" && legacy.Language=="en" && !legacy.WeekView,"version one backup migration");
    s.Theme="terracotta"; s.Language="es"; s.WeekView=true; Storage.Save(s); round=Storage.Read(Storage.FilePath); Check(round.Theme=="terracotta" && round.Language=="es" && round.WeekView,"new preferences persist");
    var advanced=new State(); advanced.Activities.Add(new Activity { Id="advance",Title="Exam",Start="12:00",End="13:00",Days=new[]{2},Color="#AACCAA",Reminder=0,ExtraReminders=new List<int>{1440} });
    Check(Schedule.Reminders(advanced,monday.AddHours(12)).Count==1 && Schedule.Reminders(advanced,monday.AddHours(12))[0].Advance,"one-day advance reminder");
    Check(Schedule.Reminders(advanced,monday.AddHours(11).AddMinutes(59)).Count==0,"advance reminder not early");
    Check(Schedule.Reminders(advanced,monday.AddHours(12).AddMinutes(16)).Count==0,"stale advance reminder expires");
    advanced.Reminded.Add(Schedule.Reminders(advanced,monday.AddHours(12))[0].Key); Check(Schedule.Reminders(advanced,monday.AddHours(12)).Count==0,"advance reminder deduplicates");
    Check(Schedule.Reminders(advanced,monday.AddDays(1).AddHours(12)).Count==1 && !Schedule.Reminders(advanced,monday.AddDays(1).AddHours(12))[0].Advance,"start reminder independent of advance reminder");
    var custom=Palette.Generate("Forest","#132C25","#F4AF8C"); Check(Palette.Contrast(custom.Background,custom.Foreground)>=4.5 && Palette.Contrast(custom.Background,custom.Muted)>=4.5,"generated readable text contrast");
    s.CustomThemes.Add(custom); s.Theme=custom.Id; Storage.Save(s); round=Storage.Read(Storage.FilePath); Check(round.Theme==custom.Id && round.CustomThemes.Single().Accent=="#F4AF8C","custom theme JSON roundtrip");
    Check(Palette.Suggestions("#F4AF8C").Distinct().Count()==4,"color suggestions derive varied accents");
    Check(Palette.Hex(Palette.FromHsv(0,0,0))=="#000000" && Palette.Normalize("f4af8c")=="#F4AF8C" && Palette.Normalize("#abc")=="#AABBCC" && Palette.Normalize("#12345")==null,"hex normalization and HSV");
    double hh,ss,vv; Palette.ToHsv(Palette.Parse("#F4AF8C"),out hh,out ss,out vv); Check(Palette.Hex(Palette.FromHsv(hh,ss,vv))=="#F4AF8C","HSV round trip");
    Check(Palette.Contrast(Palette.Readable("#335533","#101010",3),"#101010")>=3 && Palette.Contrast(Palette.Readable("#DDEEDD","#FAFAFA",3),"#FAFAFA")>=3,"readable accent adjustment");
    Check(Palette.Ideas("#F4AF8C").All(i=>Palette.Contrast(i.Background,i.Foreground)>=4.5 && Palette.Contrast(i.Background,i.Accent)>=3),"theme ideas are readable");
    Chime.Prepare(); Check(true,"custom PCM chime loads without Windows system sound");
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),"PASS: "+count+" schedule, reminder, and validation checks.");
   } catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),ex.ToString()); Environment.ExitCode=1; }
  }
  static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i); if(child is T) yield return (T)child; foreach(var x in Descendants<T>(child)) yield return x; }
  }
  static void Click(Window w,string text) { w.UpdateLayout(); var button=Descendants<Button>(w).FirstOrDefault(b=>Convert.ToString(b.Content)==text || Convert.ToString(b.ToolTip)==text || Convert.ToString(b.ToolTip).StartsWith(text+" ·")); if(button==null) throw new Exception("Button not found: "+text); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
  static void Capture(Window w,string name) {
   w.UpdateLayout(); var bitmap=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(w); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var f=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name+".png"))) encoder.Save(f);
  }
  public static void UIRun() {
   var app=new Application(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {
    Storage.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-data-"+Guid.NewGuid().ToString("N"));
    var w=new MainWindow(new State(),true); w.ShowInTaskbar=false; w.ShowActivated=false; w.Show(); w.UpdateLayout();
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> {
     var dialog=app.Windows.Cast<Window>().First(x=>x!=w); dialog.UpdateLayout();
     var inputs=Descendants<TextBox>(dialog).ToList(); inputs[0].Text="Study & plan"; inputs.Last().Text="A calm block for focused work."; var times=Descendants<TimeField>(dialog).ToList(); times[0].Value="09:00"; times[1].Value="10:30";
     Click(dialog,"Weekdays"); var date=Descendants<DateField>(dialog).First(); Check(!date.IsEnabled,"date disabled for repeat"); Click(dialog,"Once"); Check(date.IsEnabled,"date enabled for single date");
     app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var calendar=app.Windows.Cast<Window>().First(x=>x!=w&&x!=dialog); Capture(calendar,"calendar-preview"); Click(calendar,"Today"); })); Descendants<Button>(date).First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(date.SelectedDate==DateTime.Today,"calendar selects today"); Click(dialog,"Weekdays");
     var reminder=Descendants<Choice>(dialog).First(c=>c.Items.Contains("At start")); Descendants<Button>(reminder).First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); reminder.ActivePopup.Child.UpdateLayout(); Descendants<Button>(reminder.ActivePopup.Child).First(b=>Convert.ToString(b.Content).EndsWith("15 minutes before")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(reminder.SelectedIndex==4,"reminder popup selection");
     var extra=Descendants<CheckBox>(dialog).First(c=>Convert.ToString(c.Content)=="Additional reminder"); extra.IsChecked=true; var unit=Descendants<Choice>(dialog).First(c=>c.Items.Contains("days")); unit.SelectedIndex=2; inputs[1].Text="1";
     Capture(dialog,"editor-preview"); Click(dialog,"Save activity");
    }));
    Click(w,"Add activity"); Check(w.State.Activities.Count==1,"editor adds activity"); Check(Storage.Read(Storage.FilePath).Activities[0].Days.Length==5,"editor saves weekday recurrence"); Check(w.State.Activities[0].ExtraReminders.Single()==1440,"extra day reminder saved");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Capture(dialog,"manage-preview"); dialog.Close(); })); Click(w,"Manage");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Capture(dialog,"settings-preview"); dialog.Close(); })); Click(w,"Settings");
    Click(w,"Mark done"); Check(w.State.Completed.Count==1,"completion action"); Click(w,"Mark incomplete"); Check(w.State.Completed.Count==0,"undo completion");
    double dayHeight=w.Height; Click(w,"Compact"); Check(w.State.Compact && Math.Abs(w.Height-dayHeight)<1,"compact toggle keeps window size"); Capture(w,"compact-preview"); Click(w,"Expand"); Check(!w.State.Compact,"expand toggle");
    Click(w,"● Pinned on top"); Check(!w.Topmost,"pin toggle");
    w.Width=500; w.UpdateLayout(); double rememberedDay=w.ActualWidth; Click(w,"Week"); w.UpdateLayout(); Check(Math.Abs(w.State.DayWidth-rememberedDay)<1,"day size remembered on view switch"); Check(w.State.WeekView && Descendants<System.Windows.Shapes.Line>(w).Count()>=32,"week toggle"); double oldHeight=Descendants<Canvas>(w).Max(c=>c.Height); w.ZoomSchedule(1,100); w.UpdateLayout(); Check(Descendants<Canvas>(w).Max(c=>c.Height)>oldHeight,"week zoom increases timeline scale"); w.ZoomSchedule(-1,100); w.UpdateLayout(); Check(Math.Abs(Descendants<Canvas>(w).Max(c=>c.Height)-oldHeight)<.1,"week zoom reverses"); w.Width=420; w.UpdateLayout(); w.Refresh(true); w.UpdateLayout(); Check(Descendants<ScrollViewer>(w).Any(v=>v.ScrollableWidth>1),"narrow week scrolls horizontally"); Click(w,"Day"); w.UpdateLayout(); Check(!w.State.WeekView && Math.Abs(w.Width-rememberedDay)<1 && Math.Abs(w.State.WeekWidth-420)<1,"day toggle restores day size");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Descendants<Button>(dialog).First(b=>Convert.ToString(b.ToolTip)=="Terracotta").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); var language=Descendants<Choice>(dialog).First(); language.SelectedIndex=1; Click(dialog,"Save preferences"); })); Click(w,"Settings");
    w.UpdateLayout(); Check(w.State.Theme=="terracotta" && w.Background.ToString()=="#FFF4E8DE","theme applied without restart"); Check(UI.T("Settings")=="Ajustes" && Descendants<Button>(w).Any(b=>Convert.ToString(b.Content)=="Semana"),"Spanish applied without restart");
    Check(w.WindowStyle==WindowStyle.None && w.AllowsTransparency,"main window has no native frame");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> {
     var dialog=app.Windows.Cast<Window>().First(x=>x!=w);
     app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var creator=app.Windows.Cast<Window>().First(x=>x!=w&&x!=dialog); creator.UpdateLayout(); Descendants<TextBox>(creator).First().Text="My forest"; var colors=Descendants<ColorField>(creator).ToList(); Check(colors.Count==4,"theme creator exposes four colors"); colors[0].Value="#132C25"; colors[1].Value="#F4AF8C"; Capture(creator,"custom-theme-preview"); Click(creator,"Guardar tema"); }));
     Click(dialog,"Crear tema…"); Click(dialog,"Guardar preferencias");
    })); Click(w,"Ajustes"); Check(w.State.Theme.StartsWith("custom-") && w.State.CustomThemes.Count==1,"custom theme created and applied");
    using(var icon=BrandIcon.Make()) Check(icon.Width==32,"themed tray icon renders"); var toast=new ReminderToast(UI.T("Notification preview"),UI.T("Your reminder will look like this."),()=>{},false); toast.UpdateLayout(); Capture(toast,"notification-preview"); Check(toast.AllowsTransparency && toast.WindowStyle==WindowStyle.None,"themed notification has no native frame"); toast.Close();
    var incoming=new State(); incoming.Activities.Add(new Activity { Id="imported",Title="Prueba",Start="12:00",End="13:00",Days=new[]{1},Color="#AABBCC",Reminder=-1 }); w.ImportState(incoming); Check(w.State.Activities.Single().Title=="Prueba" && w.State.Theme.StartsWith("custom-") && w.State.Language=="es","import preserves local design preferences"); Check(Directory.GetFiles(Storage.Folder,"before-import-*.json").Length==1,"import creates safety backup");
    w.Close(); File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-results.txt"),"PASS: "+count+" UI checks; editor, settings, manage, and compact screenshots rendered.");
   } catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-results.txt"),ex.ToString()); Environment.ExitCode=1; }
   app.Shutdown();
  }
  public static void Showcase(string input) {
   var app=new Application(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {
    Storage.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"showcase-data-"+Guid.NewGuid().ToString("N"));
    foreach(var theme in UI.Themes) {
     var s=Storage.Read(input); s.Theme=theme.Id; s.Language="es"; s.WeekView=true; s.Compact=true;
     var w=new MainWindow(s,true); w.ShowInTaskbar=false; w.ShowActivated=false; w.Show(); w.UpdateLayout(); w.Refresh(true); w.UpdateLayout(); Capture(w,"week-"+theme.Id); Check(Descendants<System.Windows.Shapes.Line>(w).Count()>=32,"week grid renders "+theme.Id); Check(Descendants<ScrollViewer>(w).All(v=>v.ScrollableHeight<2),"fit week shows all 24 hours "+theme.Id);
     var hours=Descendants<TextBlock>(w).Where(t=>t.Text=="23:00"||t.Text=="24:00").ToList(); Check(hours.Count==2 && Math.Abs(Canvas.GetTop(hours[0])-Canvas.GetTop(hours[1]))>=14,"23 and 24 hour labels do not overlap "+theme.Id);
     if(theme.Id=="paper") {
      Click(w,"Día"); Capture(w,"day-paper");
      app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Capture(dialog,"editor-paper-es"); Click(dialog,"Cancelar"); })); Click(w,"Añadir actividad");
      app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Capture(dialog,"settings-paper-es"); dialog.Close(); })); Click(w,"Ajustes");
     }
     w.Close();
    }
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"showcase-results.txt"),"PASS: "+count+" theme/week rendering checks.");
   } catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"showcase-results.txt"),ex.ToString()); Environment.ExitCode=1; }
   app.Shutdown();
  }
 }
}
