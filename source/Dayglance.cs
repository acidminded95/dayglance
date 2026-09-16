using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Dayglance {
 public static partial class UI {
  public static Brush Bg=B("#11151D"), Card=B("#1C2330"), Text=B("#EDF2FA"), Muted=B("#9CAAC0"), Accent=B("#A4E9CC");
  public static Brush B(string s) { return (Brush)new BrushConverter().ConvertFromString(s); }
  public static TextBlock Label(string text,double size,Brush color) { return new TextBlock { Text=T(text),FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6) }; }
  public static Button Button(string text,Action action,bool accent=false) {
   var b=new Button { Content=T(text),Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,6,0),Background=accent?Accent:Card,Foreground=accent?AccentInk:Text,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,FontSize=12,MinHeight=32 };
   var template=new ControlTemplate(typeof(Button)); var border=new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7)); border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
   var content=new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.MarginProperty,new Thickness(10,7,10,7)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center); border.AppendChild(content); template.VisualTree=border; b.Template=template;
   b.Click+=(s,e)=>action(); return b;
  }
  public static Border Box(UIElement child,Brush background,Thickness margin) { return new Border { Child=child,Background=background,CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Margin=margin }; }
  public static StackPanel Row() { return new StackPanel { Orientation=Orientation.Horizontal }; }
  public static TextBox Input(string value) { return new TextBox { Text=value??"",FontSize=14,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12),Background=Card,Foreground=Text,BorderBrush=Muted,CaretBrush=Text }; }
  public static CheckBox Check(string text,bool value) { return Switch(text,value); }
  public static DialogWindow Dialog(Window owner,string title,double width,double height) { return new DialogWindow { Owner=owner,Title=T(title),Width=width,Height=Math.Min(height,SystemParameters.WorkArea.Height),MinWidth=width,MinHeight=320,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Bg,Foreground=Text,FontFamily=new FontFamily("Segoe UI"),ResizeMode=ResizeMode.CanResize,ShowInTaskbar=false }; }
 }
 public partial class MainWindow : Window {
  public State State;
  DateTime selected=DateTime.Today;
  DateTime lastToday=DateTime.Today;
  StackPanel list,hero;
  TextBlock dayLabel,clockLabel,summary;
  ScrollViewer scroll;
  Button pin,compact;
  DispatcherTimer timer;
  Forms.NotifyIcon tray;
  bool exiting,preview;
  string signature="";
  public MainWindow(State state,bool previewMode) {
   State=state; UI.Apply(state); preview=previewMode; Title="Dayglance"; Width=440; Height=760; MinWidth=380; MinHeight=340; WindowStyle=WindowStyle.None; AllowsTransparency=true; ResizeMode=ResizeMode.CanResizeWithGrip;
   Background=UI.Bg; Foreground=UI.Text; FontFamily=new FontFamily("Segoe UI"); Topmost=state.Pinned;
   var area=SystemParameters.WorkArea; Height=Math.Min(Height,area.Height); Left=Math.Max(area.Left,Math.Min(state.Left,area.Right-Width)); Top=Math.Max(area.Top,Math.Min(state.Top,area.Bottom-Height));
   BuildView();
   if(!preview) {
    tray=new Forms.NotifyIcon { Text="Dayglance — your day at a glance",Icon=BrandIcon.Make(),Visible=true }; tray.DoubleClick+=(s,e)=>Restore(); var menu=new Forms.ContextMenuStrip(); menu.Items.Add("Open Dayglance",null,(s,e)=>Restore()); menu.Items.Add("Add activity",null,(s,e)=> { Restore(); Edit(null); }); menu.Items.Add("Quit",null,(s,e)=> { exiting=true; Close(); }); tray.ContextMenuStrip=menu; tray.BalloonTipClicked+=(s,e)=>Restore();
    Closing+=(s,e)=> { if(!exiting) { e.Cancel=true; Hide(); } PersistPosition(); }; Closed+=(s,e)=> { timer.Stop(); tray.Dispose(); };
    timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(10) }; timer.Tick+=(s,e)=> { Refresh(false); Notify(); }; timer.Start();
   }
   SetSize(); Refresh(true); UpdateTrayLanguage(); SizeChanged+=(s,e)=> { if(State.WeekView && weekPanel!=null) RenderWeek(); };
  }
  void BuildView() {
   UI.Apply(State); Background=UI.Bg; Foreground=UI.Text;
   var root=new DockPanel { Margin=new Thickness(20,16,20,14),LastChildFill=true }; Content=new Border { BorderBrush=UI.Line,BorderThickness=new Thickness(1),Child=root };
   var header=new Grid { Margin=new Thickness(0,0,0,18) }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
   var brand=UI.Label("◉  dayglance",20,UI.Text); brand.FontWeight=FontWeights.SemiBold; brand.Cursor=Cursors.SizeAll; brand.ToolTip=UI.T("Drag to position your widget"); brand.MouseLeftButtonDown+=(s,e)=> { if(e.ClickCount==2) ToggleCompact(); else DragMove(); }; header.Children.Add(brand);
   var chrome=UI.Row(); chrome.Children.Add(UI.Button("−",()=>WindowState=WindowState.Minimized)); chrome.Children.Add(UI.Button("×",()=>Close())); Grid.SetColumn(chrome,1); header.Children.Add(chrome); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
   var foot=new StackPanel { Margin=new Thickness(0,12,0,0) }; var actions=new DockPanel();
   pin=UI.Button("",()=> { State.Pinned=!State.Pinned; Topmost=State.Pinned; Save(); Refresh(true); }); pin.Width=40; pin.FontSize=18; DockPanel.SetDock(pin,Dock.Right); actions.Children.Add(pin);
   var leftActions=UI.Row(); var add=UI.Button("+",()=>Edit(null),true); add.ToolTip=UI.T("Add activity"); add.Width=44; add.FontSize=20; leftActions.Children.Add(add); var manage=UI.Button("≡",Manage); manage.ToolTip=UI.T("Manage"); manage.Width=44; manage.FontSize=20; leftActions.Children.Add(manage); var settings=UI.Button("⚙",Settings); settings.ToolTip=UI.T("Settings"); settings.Width=44; settings.FontSize=18; leftActions.Children.Add(settings); actions.Children.Add(leftActions); foot.Children.Add(actions);
   var motto=UI.Label("LOCAL BY DESIGN  ·  YOUR TIME, YOUR WAY",9,UI.Muted); motto.Margin=new Thickness(0,12,0,0); foot.Children.Add(motto); DockPanel.SetDock(foot,Dock.Bottom); root.Children.Add(foot);
   var top=new StackPanel(); var viewRow=new DockPanel { Margin=new Thickness(0,0,0,12) }; compact=UI.Button("",ToggleCompact); compact.Width=42; compact.FontSize=18; DockPanel.SetDock(compact,Dock.Right); viewRow.Children.Add(compact); var views=UI.Row(); views.Children.Add(UI.Button("Day",()=>SetView(false),!State.WeekView)); views.Children.Add(UI.Button("Week",()=>SetView(true),State.WeekView)); viewRow.Children.Add(views); top.Children.Add(viewRow); clockLabel=UI.Label("",12,UI.Muted); top.Children.Add(clockLabel);
   var dates=new Grid(); dates.ColumnDefinitions.Add(new ColumnDefinition()); dates.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
   dayLabel=UI.Label("",23,UI.Text); dayLabel.FontWeight=FontWeights.SemiBold; dates.Children.Add(dayLabel); var nav=UI.Row(); nav.Children.Add(UI.Button("‹",()=> { selected=selected.AddDays(State.WeekView?-7:-1); Refresh(true); })); nav.Children.Add(UI.Button("Today",()=> { selected=DateTime.Today; Refresh(true); })); nav.Children.Add(UI.Button("›",()=> { selected=selected.AddDays(State.WeekView?7:1); Refresh(true); })); Grid.SetColumn(nav,1); dates.Children.Add(nav); top.Children.Add(dates);
   hero=new StackPanel(); top.Children.Add(hero); summary=UI.Label("",11,UI.Muted); summary.Margin=new Thickness(0,12,0,10); top.Children.Add(summary); DockPanel.SetDock(top,Dock.Top); root.Children.Add(top);
   list=new StackPanel(); scroll=new ScrollViewer { Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; if(State.WeekView) { weekPanel=new DockPanel(); weekPanel.SizeChanged+=(s,e)=>RenderWeek(); root.Children.Add(weekPanel); } else { weekPanel=null; root.Children.Add(scroll); }
   hero.Visibility=State.WeekView?Visibility.Collapsed:Visibility.Visible;
   scroll.PreviewMouseWheel+=(s,e)=> { if((Keyboard.Modifiers&ModifierKeys.Control)==0) return; e.Handled=true; ZoomSchedule(e.Delta,0); };
   list.LayoutTransform=new ScaleTransform(dayZoom,dayZoom);
   Refresh(true);
  }
  void Restore() { Show(); WindowState=WindowState.Normal; Activate(); }
  void PersistPosition() { State.Left=Left; State.Top=Top; Save(); }
  bool Save() { try { Storage.Save(State); return true; } catch(Exception ex) { MessageBox.Show(this,UI.T("Your changes could not be saved.")+"\n\n"+UI.T(ex.Message),"Dayglance",MessageBoxButton.OK,MessageBoxImage.Error); return false; } }
  void ToggleCompact() { State.Compact=!State.Compact; weekZoom=1; dayZoom=1; SetSize(); BuildView(); Save(); }
  public void Refresh(bool force) {
   DateTime now=DateTime.Now; if(selected==lastToday) selected=now.Date; lastToday=now.Date; clockLabel.Text=now.ToString("dddd, d MMMM  ·  HH:mm",UI.Culture).ToUpperInvariant();
   var today=Schedule.ForDay(State,now.Date); var active=today.Where(o=>o.Start<=now && o.End>now && !State.Completed.Contains(o.Key)).ToList();
   var entries=Schedule.ForDay(State,selected); var sig=selected.ToString("O")+now.ToString("yyyyMMddHHmm")+State.Completed.Count+State.Activities.Count;
   if(!force && signature==sig) return; signature=sig;
   pin.Content=State.Pinned?"●":"○"; pin.ToolTip=UI.T(State.Pinned?"● Pinned on top":"Pin on top"); compact.Content=State.Compact?"⤢":"⤡"; compact.ToolTip=UI.T(State.Compact?"Expand":"Compact")+" · Ctrl + scroll";
   dayLabel.Text=State.WeekView?Schedule.WeekStart(selected).ToString("d MMM",UI.Culture)+" – "+Schedule.WeekStart(selected).AddDays(6).ToString("d MMM yyyy",UI.Culture):selected==now.Date?UI.T("Today"):selected.ToString("ddd, d MMM",UI.Culture);
   hero.Children.Clear(); var hp=new StackPanel(); hp.Children.Add(UI.Label(active.Count>0?"RIGHT NOW" : "ROOM TO BREATHE",10,UI.Accent));
   if(active.Count>0) {
    var current=active[0]; hp.Children.Add(UI.Label(current.Activity.Title,State.Compact?19:24,UI.Text)); hp.Children.Add(UI.Label(current.Start.ToString("HH:mm")+" – "+current.End.ToString("HH:mm")+"  ·  "+Math.Ceiling((current.End-now).TotalMinutes)+UI.T(" min left"),12,UI.Muted));
    var bar=new ProgressBar { Minimum=0,Maximum=100,Value=(now-current.Start).TotalSeconds/(current.End-current.Start).TotalSeconds*100,Height=4,Foreground=UI.B(current.Activity.Color),Background=UI.Line,BorderThickness=new Thickness(0),Margin=new Thickness(0,6,0,8) }; hp.Children.Add(bar);
    if(active.Count>1) hp.Children.Add(UI.Label(UI.T("Also now: ")+string.Join(", ",active.Skip(1).Select(o=>o.Activity.Title)),11,UI.Muted));
   } else {
    hp.Children.Add(UI.Label(State.Activities.Count==0?"Make room for your day.":"You’re between activities.",State.Compact?18:22,UI.Text));
    Occurrence next=null; for(int i=0;i<8 && next==null;i++) next=Schedule.ForDay(State,now.Date.AddDays(i)).FirstOrDefault(o=>o.Start>now&&!State.Completed.Contains(o.Key));
    hp.Children.Add(UI.Label(next==null?"Add an activity to give your day a little structure.":UI.T("Next: ")+next.Activity.Title+" · "+(next.Start.Date==now.Date?"":next.Start.ToString("ddd ",UI.Culture))+next.Start.ToString("HH:mm"),12,UI.Muted));
   }
   hero.Children.Add(UI.Box(hp,UI.Hero,new Thickness(0,12,0,0)));
   summary.Text=UI.T("SCHEDULE")+"  /  "+entries.Count+" "+UI.T("ACTIVITIES")+"  ·  "+entries.Count(o=>State.Completed.Contains(o.Key))+" "+UI.T("DONE");
   if(State.WeekView) { summary.Text=UI.T("Click an activity to edit. Scroll for more hours."); RenderWeek(); return; }
   var offset=scroll.VerticalOffset; list.Children.Clear();
   if(entries.Count==0) { var empty=new StackPanel { Margin=new Thickness(8,15,8,0) }; empty.Children.Add(UI.Label("A fresh page.",20,UI.Text)); empty.Children.Add(UI.Label("Use + Activity to add something, or Manage to edit your weekly routine.",13,UI.Muted)); list.Children.Add(empty); }
   foreach(var o in entries) {
    bool done=State.Completed.Contains(o.Key), current=o.Start<=now&&o.End>now; var row=new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(6) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
    var stripe=new Border { Background=UI.B(o.Activity.Color),CornerRadius=new CornerRadius(3),Width=3 }; row.Children.Add(stripe);
    var info=new StackPanel { Margin=new Thickness(10,0,8,0) }; var name=UI.Label(o.Activity.Title,15,done?UI.Muted:UI.Text); name.FontWeight=FontWeights.SemiBold; if(done) name.TextDecorations=TextDecorations.Strikethrough; info.Children.Add(name);
    info.Children.Add(UI.Label(o.Start.ToString("HH:mm")+" – "+o.End.ToString("HH:mm")+(o.End.Date>o.Start.Date?UI.T(" (+1 day)"):"")+(current&&!done?"  • "+UI.T("NOW"):""),11,UI.Muted));
    if(!State.Compact && !string.IsNullOrWhiteSpace(o.Activity.Notes)) info.Children.Add(UI.Label(o.Activity.Notes,12,UI.Muted));
    Grid.SetColumn(info,1); row.Children.Add(info); var toggle=UI.Button(done?"✓":"○",()=> { if(State.Completed.Contains(o.Key)) State.Completed.Remove(o.Key); else State.Completed.Add(o.Key); Save(); Refresh(true); }); toggle.ToolTip=UI.T(done?"Mark incomplete":"Mark done"); toggle.VerticalAlignment=VerticalAlignment.Center; Grid.SetColumn(toggle,2); row.Children.Add(toggle);
    var box=UI.Box(row,current&&!done?UI.Hero:UI.Card,new Thickness(0,0,0,8)); box.BorderThickness=new Thickness(1); box.BorderBrush=current&&!done?UI.B(o.Activity.Color):UI.Card; box.MouseLeftButtonDown+=(s,e)=> { if(e.ClickCount==2) Edit(o.Activity); }; box.ToolTip=UI.T("Double-click to edit"); list.Children.Add(box);
   }
   scroll.ScrollToVerticalOffset(offset);
  }
  void Notify() {
   if(!State.Notifications) return;
   try {
    var due=Schedule.Reminders(State,DateTime.Now); if(due.Count==0) return;
    foreach(var reminder in due) State.Reminded.Add(reminder.Key);
    if(!Save()) { foreach(var reminder in due) State.Reminded.Remove(reminder.Key); return; }
    string title=due.Count==1?due[0].Occurrence.Activity.Title:UI.T("Activities coming up");
    string detail=string.Join("\n",due.Take(4).Select(r=>r.Occurrence.Activity.Title+" · "+UI.T("Starts")+" "+r.Occurrence.Start.ToString("ddd d MMM HH:mm",UI.Culture)));
    new ReminderToast(title,detail,Restore,State.Sound);
   } catch(Exception ex) { Debug.WriteLine(ex); }
  }
  void Edit(Activity activity) {
   var w=UI.Dialog(this,activity==null?"Add activity":"Edit activity",480,850); var panel=new StackPanel { Margin=new Thickness(24) }; w.Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
   panel.Children.Add(UI.Label(activity==null?"A little structure.":"Make it yours.",25,UI.Text)); panel.Children.Add(UI.Label("ACTIVITY NAME",11,UI.Muted)); var title=UI.Input(activity==null?"":activity.Title); title.MaxLength=120; panel.Children.Add(title);
   var times=UI.Row(); var start=new TimeField(activity==null?"09:00":activity.Start); start.Width=180; var end=new TimeField(activity==null?"10:00":activity.End); end.Width=180; end.Margin=new Thickness(12,0,0,0); panel.Children.Add(UI.Label("START / END  ·  24-HOUR TIME (HH:MM)",11,UI.Muted)); times.Children.Add(start); times.Children.Add(end); panel.Children.Add(times);
   panel.Children.Add(UI.Label("An earlier end time finishes the following day.",11,UI.Muted));
   panel.Children.Add(UI.Label("REPEAT ON  ·  LEAVE EMPTY FOR ONE DATE",11,UI.Muted)); var days=UI.Row(); var checks=new List<CheckBox>(); int[] order={1,2,3,4,5,6,0}; foreach(int d in order) { var c=UI.Chip(UI.Culture.DateTimeFormat.AbbreviatedDayNames[d],activity!=null&&activity.Days.Contains(d)); c.Margin=new Thickness(0,5,5,8); checks.Add(c); days.Children.Add(c); } panel.Children.Add(days);
   var presets=UI.Row(); presets.Children.Add(UI.Button("Every day",()=>checks.ForEach(c=>c.IsChecked=true))); presets.Children.Add(UI.Button("Weekdays",()=> { for(int i=0;i<7;i++) checks[i].IsChecked=i<5; })); presets.Children.Add(UI.Button("Once",()=>checks.ForEach(c=>c.IsChecked=false))); panel.Children.Add(presets);
   var date=new DateField { SelectedDate=activity!=null&&activity.Days.Length==0?DateTime.ParseExact(activity.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture):selected,Margin=new Thickness(0,10,0,12) }; panel.Children.Add(date); Action updateDate=()=> { date.IsEnabled=!checks.Any(c=>c.IsChecked==true); date.Opacity=date.IsEnabled?1:0.45; }; foreach(var c in checks) { c.Checked+=(s,e)=>updateDate(); c.Unchecked+=(s,e)=>updateDate(); } updateDate();
   panel.Children.Add(UI.Label("COLOR",11,UI.Muted)); string color=activity==null?"#A4E9CC":activity.Color; var colors=UI.Row(); var colorButtons=new List<Button>(); foreach(string hex in new[]{"#A4E9CC","#9CCBFF","#B9AAFF","#F1AED1","#FFD18F","#FF9E94"}) { string choice=hex; var b=UI.Button(color==hex?"✓":" ",()=>{}); b.Width=46; b.Background=UI.B(hex); b.Foreground=UI.Ink(hex); b.Click+=(s,e)=> { color=choice; foreach(var cb in colorButtons) cb.Content=" "; b.Content="✓"; }; colorButtons.Add(b); colors.Children.Add(b); } panel.Children.Add(colors);
   panel.Children.Add(UI.Label("REMINDER",11,UI.Muted)); var reminder=new Choice(); int[] values={-1,0,5,10,15,30,60}; foreach(int n in values) reminder.Items.Add(n==-1?"Off":n==0?"At start":n+UI.T(" minutes before")); reminder.SelectedIndex=activity==null?1:Array.IndexOf(values,activity.Reminder); if(reminder.SelectedIndex<0) { reminder.Items.Add(activity.Reminder+UI.T(" minutes before")); reminder.SelectedIndex=values.Length; } panel.Children.Add(reminder);
   int extraMinutes=activity!=null&&activity.ExtraReminders!=null&&activity.ExtraReminders.Count>0?activity.ExtraReminders[0]:0;
   var extra=UI.Switch("Additional reminder",extraMinutes>0); panel.Children.Add(extra); var advance=UI.Row(); var amount=UI.Input("1"); amount.Width=65; amount.Margin=new Thickness(0,2,8,12); advance.Children.Add(amount); var unit=new Choice { Width=140 }; unit.Items.AddRange(new[]{UI.T("minutes"),UI.T("hours"),UI.T("days")}); unit.SelectedIndex=0;
   if(extraMinutes>0) { unit.SelectedIndex=extraMinutes%1440==0?2:extraMinutes%60==0?1:0; amount.Text=(extraMinutes/(unit.SelectedIndex==2?1440:unit.SelectedIndex==1?60:1)).ToString(); } advance.Children.Add(unit); advance.Children.Add(UI.Label("before",13,UI.Muted)); panel.Children.Add(advance); Action toggleAdvance=()=> { advance.Visibility=extra.IsChecked==true?Visibility.Visible:Visibility.Collapsed; }; extra.Checked+=(s,e)=>toggleAdvance(); extra.Unchecked+=(s,e)=>toggleAdvance(); toggleAdvance();
   panel.Children.Add(UI.Label("NOTES (OPTIONAL)",11,UI.Muted)); var notes=UI.Input(activity==null?"":activity.Notes); notes.Height=65; notes.AcceptsReturn=true; notes.TextWrapping=TextWrapping.Wrap; notes.MaxLength=2000; panel.Children.Add(notes);
   var error=UI.Label("",12,UI.B("#FF9E94")); panel.Children.Add(error); var buttons=UI.Row(); buttons.Children.Add(UI.Button("Save activity",()=> {
    try {
     int extraValue=0; if(extra.IsChecked==true && (!int.TryParse(amount.Text,out extraValue)||extraValue<1||extraValue>43200)) throw new Exception("Use a number from 1 to 43200.");
     var a=new Activity { Id=activity==null?Guid.NewGuid().ToString("N"):activity.Id,Title=title.Text.Trim(),Start=start.Value,End=end.Value,Days=order.Where((d,i)=>checks[i].IsChecked==true).ToArray(),Date=date.SelectedDate.HasValue?date.SelectedDate.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):"",Color=color,Notes=notes.Text.Trim(),Reminder=reminder.SelectedIndex<values.Length?values[reminder.SelectedIndex]:activity.Reminder,ExtraReminders=extra.IsChecked==true?new List<int> { extraValue*(unit.SelectedIndex==2?1440:unit.SelectedIndex==1?60:1) }:new List<int>() };
     var test=new State(); test.Activities.Add(a); Schedule.Validate(test);
     var old=State.Activities.ToList(); if(activity!=null) State.Activities.Remove(activity); State.Activities.Add(a);
     if(!Save()) { State.Activities=old; return; } w.Close(); Refresh(true);
    } catch(Exception ex) { error.Text=UI.T("Check your details: ")+UI.T(ex.Message); }
   },true)); buttons.Children.Add(UI.Button("Cancel",()=>w.Close())); panel.Children.Add(buttons); w.ShowDialog();
  }
  void Manage() {
   var w=UI.Dialog(this,"Manage schedule",540,560); var root=new DockPanel { Margin=new Thickness(24) }; w.Content=root; var heading=UI.Label("Your routine",26,UI.Text); DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading); var all=new StackPanel(); root.Children.Add(new ScrollViewer { Content=all,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
   Action fill=null; fill=()=> { all.Children.Clear(); if(State.Activities.Count==0) all.Children.Add(UI.Label("No activities yet. Close this window and choose + Activity.",14,UI.Muted)); foreach(var a in State.Activities.OrderBy(a=>a.Start).ToList()) { var p=new StackPanel(); p.Children.Add(UI.Label(a.Title,17,UI.Text)); p.Children.Add(UI.Label(a.Start+"–"+a.End+"  ·  "+(a.Days.Length==0?a.Date:string.Join(", ",a.Days.Select(d=>UI.Culture.DateTimeFormat.AbbreviatedDayNames[d]))),12,UI.Muted)); var r=UI.Row(); r.Children.Add(UI.Button("Edit",()=> { Edit(a); fill(); })); r.Children.Add(UI.Button("Delete",()=> { if(MessageBox.Show(w,UI.Language=="es"?"¿Eliminar «"+a.Title+"» y todas sus repeticiones?":"Delete ‘"+a.Title+"’ and all its future repeats?",UI.T("Delete activity"),MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes) { State.Activities.Remove(a); if(!Save()) State.Activities.Add(a); Refresh(true); fill(); } })); p.Children.Add(r); all.Children.Add(UI.Box(p,UI.Card,new Thickness(0,6,0,6))); } }; fill(); w.ShowDialog();
  }
  string StartupPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Dayglance.lnk"); } }

 }
 public static class Program {
  [STAThread] public static void Main(string[] args) {
   if(args.Contains("--self-test")) { Tests.Run(); return; }
   if(args.Contains("--ui-test")) { Tests.UIRun(); return; }
   if(args.Contains("--showcase")) { Tests.Showcase(args.Last()); return; }
   if(args.Contains("--preview")) { Preview(args.Last()); return; }
   bool created; using(var mutex=new Mutex(true,"Local\\Dayglance.Desktop.1",out created)) {
    if(!created) { MessageBox.Show("Dayglance is already running. Open it from the system tray.","Dayglance"); return; }
    State state=new State();
    try { if(File.Exists(Storage.FilePath)) state=Storage.Read(Storage.FilePath); }
    catch(Exception ex) { MessageBox.Show("Dayglance could not read your schedule. Your file has been left untouched.\n\n"+Storage.FilePath+"\n\n"+ex.Message+"\n\nA previous save may be available in schedule.json.bak.","Dayglance",MessageBoxButton.OK,MessageBoxImage.Error); return; }
    var app=new Application(); app.DispatcherUnhandledException+=(s,e)=> { MessageBox.Show("Dayglance encountered an error:\n"+e.Exception.Message,"Dayglance"); e.Handled=true; }; app.Run(new MainWindow(state,false));
   }
  }
  static void Preview(string path) {
   var s=new State(); var now=DateTime.Now;
   foreach(var a in new[]{new Activity { Title="A moment to reset",Start=now.AddMinutes(-75).ToString("HH:mm"),End=now.AddMinutes(-45).ToString("HH:mm"),Color="#B9AAFF",Notes="A walk, some water, a clear head." },new Activity { Title="Make something meaningful",Start=now.AddMinutes(-20).ToString("HH:mm"),End=now.AddMinutes(40).ToString("HH:mm"),Color="#A4E9CC",Notes="One task. A little less noise." },new Activity { Title="Move & recharge",Start=now.AddMinutes(50).ToString("HH:mm"),End=now.AddMinutes(90).ToString("HH:mm"),Color="#FFD18F",Notes="Step away from the screen." }}) { a.Id=Guid.NewGuid().ToString("N"); a.Days=Enumerable.Range(0,7).ToArray(); a.Reminder=0; s.Activities.Add(a); }
   s.Completed.Add(Schedule.ForDay(s,now).First().Key); var app=new Application(); var w=new MainWindow(s,true); w.ShowInTaskbar=false; w.ShowActivated=false; w.Left=-10000; w.Top=-10000; w.Show(); w.UpdateLayout(); var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32); bmp.Render(w); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp)); using(var f=File.Create(path)) encoder.Save(f); w.Close(); app.Shutdown();
  }
 }
}

