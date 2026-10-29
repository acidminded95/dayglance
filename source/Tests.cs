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
    Check(Schedule.NextHalfHour(monday.AddHours(9).AddMinutes(10))==monday.AddHours(9.5) && Schedule.NextHalfHour(monday.AddHours(9).AddMinutes(30))==monday.AddHours(10) && Schedule.NextHalfHour(monday.AddHours(23).AddMinutes(45))==monday.AddDays(1),"new activities start at the next half hour");
    Check(Schedule.DurationMinutes("09:00","10:30")==90 && Schedule.DurationMinutes("23:00","01:00")==120 && Schedule.AddMinutes("23:30",60)=="00:30","durations wrap past midnight");
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
    var legacy=Storage.Serializer().Deserialize<State>("{\"Version\":1,\"Activities\":[]}"); Schedule.Validate(legacy); Check(legacy.Theme=="midnight" && legacy.Language=="en" && !legacy.WeekView,"version one backup migration"); Check(!legacy.Onboarded,"older data has not seen the welcome tour");
    s.Theme="terracotta"; s.ThemeMode="system"; s.Language="es"; s.WeekView=true; Storage.Save(s); round=Storage.Read(Storage.FilePath); Check(round.Theme=="terracotta" && round.ThemeMode=="system" && round.Language=="es" && round.WeekView,"new preferences persist");
    var advanced=new State(); advanced.Activities.Add(new Activity { Id="advance",Title="Exam",Start="12:00",End="13:00",Days=new[]{2},Color="#AACCAA",Reminder=0,ExtraReminders=new List<int>{1440} });
    Check(Schedule.Reminders(advanced,monday.AddHours(12)).Count==1 && Schedule.Reminders(advanced,monday.AddHours(12))[0].Advance,"one-day advance reminder");
    Check(Schedule.Reminders(advanced,monday.AddHours(11).AddMinutes(59)).Count==0,"advance reminder not early");
    Check(Schedule.Reminders(advanced,monday.AddHours(12).AddMinutes(16)).Count==0,"stale advance reminder expires");
    advanced.Reminded.Add(Schedule.Reminders(advanced,monday.AddHours(12))[0].Key); Check(Schedule.Reminders(advanced,monday.AddHours(12)).Count==0,"advance reminder deduplicates");
    Check(Schedule.Reminders(advanced,monday.AddDays(1).AddHours(12)).Count==1 && !Schedule.Reminders(advanced,monday.AddDays(1).AddHours(12))[0].Advance,"start reminder independent of advance reminder");
    // Checklists: a tick belongs to one occurrence, so a repeating activity comes around again unticked.
    var listed=new State(); var walk=new Activity { Id="walk",Title="Walk",Start="07:00",End="08:00",Days=new[]{1,2},Color="#AACCAA",Reminder=-1,Checklist=new List<ChecklistItem> { new ChecklistItem { Id="water",Text="Water" },new ChecklistItem { Id="keys",Text="Keys" } } }; listed.Activities.Add(walk); Schedule.Validate(listed);
    var walkMonday=Schedule.ForDay(listed,monday).Single(); var walkTuesday=Schedule.ForDay(listed,monday.AddDays(1)).Single();
    Schedule.ToggleItem(listed,walkMonday,walk.Checklist[0]); Check(Schedule.CheckedCount(listed,walkMonday)==1 && Schedule.CheckedCount(listed,walkTuesday)==0,"checklist ticks belong to one occurrence");
    Schedule.ToggleItem(listed,walkMonday,walk.Checklist[0]); Check(Schedule.CheckedCount(listed,walkMonday)==0 && listed.Checked.Count==0,"checklist untick");
    Schedule.ToggleItem(listed,walkMonday,walk.Checklist[1]); round=Storage.Serializer().Deserialize<State>(Storage.Serializer().Serialize(listed)); Schedule.Validate(round); Check(round.Activities[0].Checklist.Select(i=>i.Text).SequenceEqual(new[]{"Water","Keys"}) && Schedule.CheckedCount(round,Schedule.ForDay(round,monday).Single())==1,"checklist JSON round trip");
    var plain=Storage.Serializer().Deserialize<State>("{\"Version\":1,\"Activities\":[{\"Id\":\"old\",\"Title\":\"Old\",\"Color\":\"#AABBCC\",\"Start\":\"09:00\",\"End\":\"10:00\",\"Days\":[1],\"Reminder\":0}]}"); Schedule.Validate(plain); Check(plain.Activities[0].Checklist.Count==0 && plain.Checked.Count==0,"older data loads with empty checklists");
    walk.Checklist.Add(new ChecklistItem { Id="water",Text="Again" }); rejected=false; try { Schedule.Validate(listed); } catch { rejected=true; } Check(rejected,"reject duplicate checklist item IDs"); walk.Checklist.RemoveAt(2);
    walk.Checklist[0].Text=" "; rejected=false; try { Schedule.Validate(listed); } catch { rejected=true; } Check(rejected,"reject empty checklist item"); walk.Checklist[0].Text=new string('x',201); rejected=false; try { Schedule.Validate(listed); } catch { rejected=true; } Check(rejected,"reject long checklist item"); walk.Checklist[0].Text="Water";
    walk.Checklist.AddRange(Enumerable.Range(0,28).Select(n=>new ChecklistItem { Id="n"+n,Text="Item "+n })); Schedule.Validate(listed); walk.Checklist.Add(new ChecklistItem { Id="extra",Text="One too many" }); rejected=false; try { Schedule.Validate(listed); } catch { rejected=true; } Check(rejected,"checklist allows 30 items and rejects 31"); walk.Checklist.RemoveRange(2,29);
    listed.Checked.Add(null); rejected=false; try { Schedule.Validate(listed); } catch { rejected=true; } Check(rejected,"reject invalid tick history"); listed.Checked.RemoveAt(listed.Checked.Count-1);
    walk.Checklist=null; Schedule.Validate(listed); Check(walk.Checklist!=null && walk.Checklist.Count==0,"missing checklist becomes empty");
    // Goals: one period per week, month, quarter or year; unfinished goals can move to the next period.
    var wednesday=new DateTime(2026,10,28);
    Check(GoalPlan.Period("week",wednesday)=="2026-10-25" && GoalPlan.Period("month",wednesday)=="2026-10" && GoalPlan.Period("quarter",wednesday)=="2026-Q4" && GoalPlan.Period("year",wednesday)=="2026","goal periods for each span");
    Check(GoalPlan.Previous("2026-10-25")=="2026-10-18" && GoalPlan.Previous("2026-01")=="2025-12" && GoalPlan.Previous("2026-Q1")=="2025-Q4" && GoalPlan.Previous("2026")=="2025","previous goal periods");
    Check(GoalPlan.SpanOf("2026-Q2")=="quarter" && GoalPlan.SpanOf("2026-Q5")==null && GoalPlan.SpanOf("2026-13")==null && GoalPlan.SpanOf("abcd")==null && GoalPlan.SpanOf(null)==null,"goal period keys are checked");
    Check(GoalPlan.Anchor(wednesday,wednesday)==wednesday && GoalPlan.Anchor(new DateTime(2026,11,2),wednesday)==new DateTime(2026,11,4),"a week's goals follow today, or its middle day");
    var planned=new State(); planned.Goals.Add(new Goal { Id="g1",Text="Read a book",Period="2026-10-18" }); planned.Goals.Add(new Goal { Id="g2",Text="Run 20 km",Period="2026-10-18",Done=true }); Schedule.Validate(planned);
    Check(GoalPlan.CarryOver(planned,"2026-10-25")==1 && planned.Goals[0].Period=="2026-10-25" && planned.Goals[1].Period=="2026-10-18","carry over moves only unfinished goals");
    round=Storage.Serializer().Deserialize<State>(Storage.Serializer().Serialize(planned)); Schedule.Validate(round); Check(round.Goals.Count==2 && round.Goals[1].Done && round.Goals[0].Text=="Read a book","goals JSON round trip"); Check(plain.Goals.Count==0,"older data loads without goals");
    planned.Goals.AddRange(Enumerable.Range(0,5).Select(n=>new Goal { Id="w"+n,Text="Goal "+n,Period="2026-10-25" })); rejected=false; try { Schedule.Validate(planned); } catch { rejected=true; } Check(rejected,"up to 5 goals per period"); planned.Goals.RemoveAt(planned.Goals.Count-1); Schedule.Validate(planned);
    Check(GoalPlan.CarryOver(planned,"2026-11-01")==5 && GoalPlan.For(planned,"2026-11-01").Count==5,"carry over moves only as many as fit");
    planned.Goals[0].Text=" "; rejected=false; try { Schedule.Validate(planned); } catch { rejected=true; } Check(rejected,"reject empty goal"); planned.Goals[0].Text="Read a book";
    planned.Goals[0].Period="2026-W44"; rejected=false; try { Schedule.Validate(planned); } catch { rejected=true; } Check(rejected,"reject unknown goal period");
    var custom=Palette.Generate("Forest","#132C25","#F4AF8C"); Check(Palette.Contrast(custom.Background,custom.Foreground)>=4.5 && Palette.Contrast(custom.Background,custom.Muted)>=4.5,"generated readable text contrast");
    s.CustomThemes.Add(custom); s.Theme=custom.Id; Storage.Save(s); round=Storage.Read(Storage.FilePath); Check(round.Theme==custom.Id && round.CustomThemes.Single().Accent=="#F4AF8C","custom theme JSON roundtrip");
    Check(Palette.Suggestions("#F4AF8C").Distinct().Count()==4,"color suggestions derive varied accents");
    Check(Palette.Hex(Palette.FromHsv(0,0,0))=="#000000" && Palette.Normalize("f4af8c")=="#F4AF8C" && Palette.Normalize("#abc")=="#AABBCC" && Palette.Normalize("#12345")==null,"hex normalization and HSV");
    double hh,ss,vv; Palette.ToHsv(Palette.Parse("#F4AF8C"),out hh,out ss,out vv); Check(Palette.Hex(Palette.FromHsv(hh,ss,vv))=="#F4AF8C","HSV round trip");
    Check(Palette.Contrast(Palette.Readable("#335533","#101010",3),"#101010")>=3 && Palette.Contrast(Palette.Readable("#DDEEDD","#FAFAFA",3),"#FAFAFA")>=3,"readable accent adjustment");
    foreach(var native in UI.Themes) {
     var other=UI.Variants.Single(v=>v.Id==native.Id); Check(Palette.IsDark(native.Background)!=Palette.IsDark(other.Background),"each theme has a light and a dark palette: "+native.Id);
     foreach(var t in new[]{native,other}) Check(Palette.Contrast(t.Background,t.Foreground)>=7 && Palette.Contrast(t.Surface,t.Foreground)>=7 && Palette.Contrast(t.Background,t.Muted)>=4.5 && Palette.Contrast(t.Background,t.Accent)>=4.5,"theme palette is readable: "+t.Id);
     Check(UI.ForMode(native,null)==native && !Palette.IsDark(UI.ForMode(native,"light").Background) && Palette.IsDark(UI.ForMode(native,"dark").Background),"theme mode picks the palette: "+native.Id);
    }
    Check(UI.ForMode(Palette.Generate("Mine","#132C25","#F4AF8C"),"light").Background=="#132C25","custom themes keep their colors in every mode");
    Check(Palette.Ideas("#F4AF8C").All(i=>Palette.Contrast(i.Background,i.Foreground)>=4.5 && Palette.Contrast(i.Background,i.Accent)>=3),"theme ideas are readable");
    var legacyLead=new State(); legacyLead.Activities.Add(new Activity { Id="lead",Title="Lead",Start="10:00",End="11:00",Days=new[]{1},Color="#AACCAA",Reminder=30 }); Check(Schedule.MigrateReminders(legacyLead) && legacyLead.Activities[0].Reminder==0 && legacyLead.Activities[0].ExtraReminders.Single()==30 && !Schedule.MigrateReminders(legacyLead),"legacy lead reminder migrates to additional reminder");
    UI.Language="en"; UI.Hour12=true; Check(UI.Clock(new DateTime(2026,9,16,13,5,0))=="1:05 PM" && UI.HourLabel(0)=="12 AM" && UI.ClockText("12:30")=="12:30 PM","12-hour clock formatting"); UI.Hour12=false; Check(UI.Clock(new DateTime(2026,9,16,13,5,0))=="13:05","24-hour clock formatting");
#if !STORE
    Check(Updater.ParseTag("v2.10.0")>Updater.ParseTag("v2.9.3") && Updater.ParseTag("2.1.0")==new Version(2,1,0) && Updater.ParseTag("latest")==null && !Updater.IsNewer(new UpdateInfo { Version=Updater.ParseTag(AppInfo.Version) }) && Updater.IsNewer(new UpdateInfo { Version=new Version(99,0) }),"release tag comparison");
#endif
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
     var inputs=Descendants<TextBox>(dialog).ToList(); inputs[0].Text="Study & plan"; inputs.Last().Text="A calm block for focused work."; var times=Descendants<TimeField>(dialog).ToList(); Check(Schedule.DurationMinutes(times[0].Value,times[1].Value)==60 && times[0].Value.EndsWith("0"),"new activity defaults to one hour from the next half hour");
     Func<string,bool> highlighted=text=>Descendants<Button>(dialog).First(b=>Convert.ToString(b.Content)==text).Background==UI.Accent; Check(highlighted("Once") && !highlighted("Weekdays"),"Once is preselected for a new activity");
     times[0].Value="09:00"; times[1].Value="10:30";
     Click(dialog,"Weekdays"); var date=Descendants<DateField>(dialog).First(); Check(!date.IsEnabled,"date disabled for repeat"); Check(highlighted("Weekdays") && !highlighted("Once"),"picking days deselects Once"); Click(dialog,"Once"); Check(date.IsEnabled,"date enabled for single date");
     app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var calendar=app.Windows.Cast<Window>().First(x=>x!=w&&x!=dialog); Capture(calendar,"calendar-preview"); Click(calendar,"Today"); })); Descendants<Button>(date).First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(date.SelectedDate==DateTime.Today,"calendar selects today"); Click(dialog,"Weekdays");
     var reminder=Descendants<CheckBox>(dialog).First(c=>Convert.ToString(c.Content)=="Remind me when it starts"); Check(reminder.IsChecked==true,"start reminder on by default");
     var extra=Descendants<CheckBox>(dialog).First(c=>Convert.ToString(c.Content)=="Additional reminder"); extra.IsChecked=true; inputs=Descendants<TextBox>(dialog).ToList(); inputs[1].Text="1"; inputs[2].Text="2"; inputs[3].Text="30";
     Click(dialog,"Add item"); Click(dialog,"Add item"); Click(dialog,"Add item"); inputs=Descendants<TextBox>(dialog).ToList(); inputs[inputs.Count-3].Text="Outline"; inputs[inputs.Count-2].Text="Inbox zero";
     Capture(dialog,"editor-preview"); Click(dialog,"Save activity");
    }));
    Click(w,"Add activity"); Check(w.State.Activities.Count==1,"editor adds activity"); Check(Storage.Read(Storage.FilePath).Activities[0].Days.Length==5,"editor saves weekday recurrence"); Check(w.State.Activities[0].ExtraReminders.Single()==1440+150 && w.State.Activities[0].Reminder==0,"start reminder plus days/hours/minutes lead saved");
    Check(w.State.Activities[0].Checklist.Select(i=>i.Text).SequenceEqual(new[]{"Outline","Inbox zero"}),"editor saves the checklist and drops empty rows");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Capture(dialog,"manage-preview"); dialog.Close(); })); Click(w,"Manage");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Capture(dialog,"settings-preview"); dialog.Close(); })); Click(w,"Settings");
    Click(w,"Mark done"); Check(w.State.Completed.Count==1,"completion action"); Click(w,"Mark incomplete"); Check(w.State.Completed.Count==0,"undo completion");
    Func<string,CheckBox> tickFor=text=> { w.UpdateLayout(); return Descendants<CheckBox>(w).First(c=>c.Content is TextBlock && ((TextBlock)c.Content).Text==text); };
    tickFor("Outline").RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); Check(w.State.Checked.Count==1 && tickFor("Outline").IsChecked==true && tickFor("Inbox zero").IsChecked==false,"tick a checklist item on the day card"); Check(Storage.Read(Storage.FilePath).Checked.Count==1,"checklist tick is saved");
    tickFor("Outline").RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); Check(w.State.Checked.Count==0 && tickFor("Outline").IsChecked==false,"untick a checklist item");
    double fullWidth=w.Width,fullHeight=w.Height; Click(w,"Mini widget"); w.UpdateLayout(); Check(w.State.Compact && w.Width<fullWidth,"mini widget is smaller"); Capture(w,"compact-preview"); Click(w,"Expand"); w.UpdateLayout(); Check(Math.Abs(w.Width-fullWidth)<1 && Math.Abs(w.Height-fullHeight)<1,"expand restores full size"); Check(!w.State.Compact,"expand toggle");
    Check(!w.Topmost,"not pinned on top by default"); Click(w,"Pin on top"); Check(w.Topmost && w.State.Pinned,"pin toggle"); Click(w,"● Pinned on top"); Check(!w.Topmost,"unpin toggle");
    w.Width=500; w.UpdateLayout(); double sharedWidth=w.ActualWidth,sharedHeight=w.ActualHeight; Click(w,"Week"); w.UpdateLayout(); Check(Math.Abs(w.Width-sharedWidth)<1 && Math.Abs(w.Height-sharedHeight)<1,"week view keeps the window size");Check(w.State.WeekView && Descendants<System.Windows.Shapes.Line>(w).Count()>=32,"week toggle"); double oldHeight=Descendants<Canvas>(w).Max(c=>c.Height); w.ZoomSchedule(1,100); w.UpdateLayout(); Check(Descendants<Canvas>(w).Max(c=>c.Height)>oldHeight,"week zoom increases timeline scale"); w.ZoomSchedule(-1,100); w.UpdateLayout(); Check(Math.Abs(Descendants<Canvas>(w).Max(c=>c.Height)-oldHeight)<.1,"week zoom reverses"); w.Width=420; w.UpdateLayout(); w.Refresh(true); w.UpdateLayout(); Check(Descendants<ScrollViewer>(w).Any(v=>v.ScrollableWidth>1),"narrow week scrolls horizontally"); Click(w,"Day"); w.UpdateLayout(); Check(!w.State.WeekView && Math.Abs(w.Width-420)<1 && Math.Abs(w.State.WindowWidth-420)<1,"day toggle keeps the shared window size");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w); Click(dialog,"Light"); dialog.UpdateLayout(); Descendants<Button>(dialog).First(b=>Convert.ToString(b.ToolTip)=="Terracotta garden").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); var language=Descendants<Choice>(dialog).First(c=>c.Items.Contains("Español")); language.SelectedIndex=1; Click(dialog,"Save preferences"); })); Click(w,"Settings");
    w.UpdateLayout(); Check(w.State.Theme=="terracotta" && w.State.ThemeMode=="light" && w.Background.ToString()=="#FFF4E8DE","theme applied without restart"); Check(UI.T("Settings")=="Ajustes" && Descendants<Button>(w).Any(b=>Convert.ToString(b.Content)=="Semana"),"Spanish applied without restart");
    Check(w.WindowStyle==WindowStyle.None && w.AllowsTransparency,"main window has no native frame");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> {
     var dialog=app.Windows.Cast<Window>().First(x=>x!=w);
     app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var creator=app.Windows.Cast<Window>().First(x=>x!=w&&x!=dialog); creator.UpdateLayout(); Descendants<TextBox>(creator).First().Text="My forest"; var colors=Descendants<ColorField>(creator).ToList(); Check(colors.Count==4,"theme creator exposes four colors"); colors[0].Value="#132C25"; colors[1].Value="#F4AF8C"; Capture(creator,"custom-theme-preview"); Click(creator,"Guardar tema"); }));
     Click(dialog,"Crear tema…"); Click(dialog,"Guardar preferencias");
    })); Click(w,"Ajustes"); Check(w.State.Theme.StartsWith("custom-") && w.State.CustomThemes.Count==1,"custom theme created and applied");
    using(var icon=BrandIcon.Make()) Check(icon.Width==32,"themed tray icon renders"); var toast=new ReminderToast(UI.T("Notification preview"),UI.T("Your reminder will look like this."),()=>{},false); toast.UpdateLayout(); Capture(toast,"notification-preview"); Check(toast.AllowsTransparency && toast.WindowStyle==WindowStyle.None,"themed notification has no native frame"); toast.Close();
    var incoming=new State(); incoming.Activities.Add(new Activity { Id="imported",Title="Prueba",Start="12:00",End="13:00",Days=new[]{1},Color="#AABBCC",Reminder=-1 }); w.ImportState(incoming); Check(w.State.Activities.Single().Title=="Prueba" && w.State.Theme.StartsWith("custom-") && w.State.Language=="es","import preserves local design preferences"); Check(Directory.GetFiles(Storage.Folder,"before-import-*.json").Length==1,"import creates safety backup");
    // Goals (Spanish): no pills until a goal is set, one pill per span with goals, merged into one pill on a narrow window.
    Click(w,"Semana"); Click(w,"Hoy"); w.Width=1200; w.UpdateLayout(); w.Refresh(true); w.UpdateLayout();
    Func<string,bool> goalPill=tip=> { w.UpdateLayout(); return Descendants<Button>(w).Any(b=>b.IsVisible && (Convert.ToString(b.ToolTip)==tip || Convert.ToString(b.ToolTip).StartsWith(tip+" ·"))); };
    Check(!goalPill("Metas de la semana") && Descendants<Button>(w).Any(b=>b.IsVisible && Convert.ToString(b.Content)=="+ Meta"),"no goal pills until a goal is set");
    w.State.Goals.Add(new Goal { Id="gw",Text="Leer 50 páginas",Period=GoalPlan.Period("week",AppClock.Today) }); w.State.Goals.Add(new Goal { Id="gy",Text="Correr un medio maratón",Period=GoalPlan.Period("year",AppClock.Today),Done=true }); w.Refresh(true);
    Check(goalPill("Metas de la semana · 0/1") && goalPill("Metas del año · 1/1") && !goalPill("Metas del mes") && !goalPill("Metas del trimestre") && !w.GoalsMerged,"only spans with goals get a pill");
    w.Width=420; w.UpdateLayout(); w.Refresh(true); w.UpdateLayout(); Check(w.GoalsMerged && goalPill("Metas"),"goal pills merge into one on a narrow window");
    Click(w,"Metas"); Check(w.GoalsPopup!=null && w.GoalsPopup.IsOpen,"goals popover opens");
    var goalsView=(FrameworkElement)w.GoalsPopup.Child; goalsView.UpdateLayout(); Descendants<TextBox>(goalsView).First().Text="Llamar a mamá"; Descendants<Button>(goalsView).First(b=>Convert.ToString(b.Content)=="Añadir").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    goalsView.UpdateLayout(); Descendants<CheckBox>(goalsView).First(c=>c.Content is TextBlock && ((TextBlock)c.Content).Text=="Leer 50 páginas").RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
    var savedGoals=Storage.Read(Storage.FilePath).Goals; Check(savedGoals.Count==3 && savedGoals.Single(g=>g.Id=="gw").Done && savedGoals.Any(g=>g.Text=="Llamar a mamá" && g.Period==GoalPlan.Period("week",AppClock.Today)),"goals are added, ticked and saved from the popover");
    w.GoalsPopup.IsOpen=false; app.Dispatcher.Invoke(DispatcherPriority.Background,new Action(()=> {})); Check(w.GoalsPopup==null && goalPill("Metas · 2/3"),"closing the popover updates the pill"); w.Width=1200; w.UpdateLayout(); Click(w,"Día");
    // Welcome tour (Spanish at this point): name, weekly days, reminder, then the tips page.
    int before=w.State.Activities.Count; w.State.Onboarded=false;
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> {
     var tour=app.Windows.Cast<Window>().First(x=>x!=w); Capture(tour,"welcome-preview"); Click(tour,"Añadir mi primera actividad");
     Click(tour,"Siguiente"); Check(w.State.Activities.Count==before,"tour requires a name"); tour.UpdateLayout(); Descendants<TextBox>(tour).First().Text="Leer";
     Click(tour,"Siguiente"); Click(tour,"Lun–Vie"); Click(tour,"Siguiente"); Capture(tour,"welcome-reminder-preview"); Click(tour,"Añadir a mi día"); Capture(tour,"welcome-tips-preview"); Click(tour,"Entendido");
    })); w.Welcome();
    var toured=w.State.Activities.Last(); Check(w.State.Activities.Count==before+1 && toured.Title=="Leer" && toured.Days.Length==5 && toured.Reminder==0 && w.State.Onboarded,"welcome tour adds a weekday activity and marks the tour seen"); Check(Storage.Read(Storage.FilePath).Onboarded,"tour state persists");
    w.Close(); File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-results.txt"),"PASS: "+count+" UI checks; editor, settings, manage, and compact screenshots rendered.");
   } catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-results.txt"),ex.ToString()); Environment.ExitCode=1; }
   app.Shutdown();
  }
  // Renders README screenshots from a sample schedule at a fixed moment (Wed 16 Sep 2026, 10:20), at 2x resolution.
  // Usage: Dayglance.exe --screenshots <schedule.json> <output folder>
  public static void Screenshots(string input,string output) {
   var app=new Application(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   string results=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"screenshots-results.txt");
   try {
    output=Path.GetFullPath(output); Directory.CreateDirectory(output); input=Path.GetFullPath(input);
    Storage.Folder=Path.Combine(Path.GetTempPath(),"dayglance-screenshots-"+Guid.NewGuid().ToString("N"));
    AppClock.Fixed=new DateTime(2026,9,16,10,20,0);
    var written=new List<string>();
    Func<string,State> load=theme=> { var s=Storage.Read(input); s.Theme=theme; s.Language="en"; s.Pinned=false; s.Notifications=false; s.Compact=false; s.WeekView=false; return s; };
    Func<State,double,double,MainWindow> open=(s,width,height)=> {
     var w=new MainWindow(s,true); w.ShowInTaskbar=false; w.ShowActivated=false; w.Show(); w.Width=width; w.Height=height; w.Left=-20000; w.Top=-20000;
     w.UpdateLayout(); w.Refresh(true); w.UpdateLayout(); return w;
    };
    Action<Window,string> shoot=(window,name)=> { Render(window,Path.Combine(output,name),2); written.Add(name); };

    var day=load("midnight"); day.CardStyle="band"; var w1=open(day,520,860); shoot(w1,"day-view.png");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w1); dialog.UpdateLayout(); shoot(dialog,"settings.png"); dialog.Close(); })); Click(w1,"Settings");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> { var dialog=app.Windows.Cast<Window>().First(x=>x!=w1); dialog.UpdateLayout(); shoot(dialog,"activity-editor.png"); dialog.Close(); })); Click(w1,"Add activity");
    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> {
     var tour=app.Windows.Cast<Window>().First(x=>x!=w1); tour.UpdateLayout(); shoot(tour,"welcome-tour.png");
     Click(tour,"Add my first activity"); tour.UpdateLayout(); Descendants<TextBox>(tour).First().Text="Morning walk"; Click(tour,"Next"); Click(tour,"Weekdays"); tour.UpdateLayout(); shoot(tour,"welcome-tour-when.png"); tour.Close();
    })); w1.Welcome();
    w1.Close();

    var week=load("midnight"); week.WeekView=true; week.WeekHighlight="glow"; var w2=open(week,1280,860); w2.ScrollWeekToHour(6.5); w2.UpdateLayout(); shoot(w2,"week-view.png"); w2.Close();
    var mini=load("midnight"); mini.Compact=true; mini.MiniWidth=340; mini.MiniHeight=380; var w3=open(mini,340,380); shoot(w3,"mini-widget.png"); w3.Close();
    var light=load("terracotta"); light.CardStyle="full"; var w4=open(light,520,860); shoot(w4,"day-terracotta.png"); w4.Close();
    var twelve=load("lavender"); twelve.WeekView=true; twelve.TimeFormat="12"; twelve.WeekHighlight="outline"; var w5=open(twelve,1280,860); w5.ScrollWeekToHour(6.5); w5.UpdateLayout(); shoot(w5,"week-lavender-12h.png"); w5.Close();

    File.WriteAllText(results,"PASS: "+written.Count+" screenshots written to "+output+"\r\n"+string.Join("\r\n",written));
   } catch(Exception ex) { File.WriteAllText(results,ex.ToString()); Environment.ExitCode=1; }
   AppClock.Fixed=null; app.Shutdown();
  }
  static void Render(Window w,string path,double scale) {
   w.UpdateLayout(); var bitmap=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth*scale),(int)Math.Ceiling(w.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32); bitmap.Render(w);
   var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var f=File.Create(path)) encoder.Save(f);
  }
  public static void Showcase(string input) {
   var app=new Application(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {
    Storage.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"showcase-data-"+Guid.NewGuid().ToString("N"));
    foreach(var theme in UI.Themes) foreach(string mode in new[]{"light","dark"}) {
     var s=Storage.Read(input); s.Theme=theme.Id; s.ThemeMode=mode; s.Language="es"; s.WeekView=true; s.Compact=false;
     var w=new MainWindow(s,true); w.ShowInTaskbar=false; w.ShowActivated=false; w.Width=1200; w.Height=Math.Min(950,SystemParameters.WorkArea.Height); w.Show(); w.UpdateLayout(); w.Refresh(true); w.UpdateLayout(); Capture(w,"week-"+theme.Id+"-"+mode); Check(Descendants<System.Windows.Shapes.Line>(w).Count()>=32,"week grid renders "+theme.Id+" "+mode);
     var hours=Descendants<TextBlock>(w).Where(t=>t.Text=="23:00"||t.Text=="24:00").ToList(); Check(hours.Count==2 && Math.Abs(Canvas.GetTop(hours[0])-Canvas.GetTop(hours[1]))>=14,"23 and 24 hour labels do not overlap "+theme.Id+" "+mode);
     if(theme.Id=="paper" && mode=="light") {
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
