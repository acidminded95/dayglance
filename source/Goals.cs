using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Dayglance {
 // A longer-term goal. Period names its span: "2026-10-25" (the week starting that day), "2026-10" (a month), "2026-Q4" (a quarter) or "2026" (a year).
 public class Goal {
  public string Id { get; set; }
  public string Text { get; set; }
  public string Period { get; set; }
  public bool Done { get; set; }
 }
 public static class GoalPlan {
  public const int MaxPerPeriod=5;
  public static readonly string[] Spans={"week","month","quarter","year"};
  static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
  public static string Period(string span,DateTime day) {
   if(span=="week") return Schedule.WeekStart(day).ToString("yyyy-MM-dd",Invariant);
   if(span=="month") return day.ToString("yyyy-MM",Invariant);
   if(span=="quarter") return day.Year.ToString(Invariant)+"-Q"+((day.Month+2)/3);
   return day.Year.ToString(Invariant);
  }
  // The span a period key belongs to, or null when the key is not valid.
  public static string SpanOf(string period) { DateTime start; return Parse(period,out start); }
  public static DateTime Start(string period) { DateTime start; Parse(period,out start); return start; }
  static string Parse(string period,out DateTime start) {
   start=DateTime.MinValue; if(period==null) return null;
   if(period.Length==10 && DateTime.TryParseExact(period,"yyyy-MM-dd",Invariant,DateTimeStyles.None,out start)) return "week";
   if(period.Length==7 && DateTime.TryParseExact(period,"yyyy-MM",Invariant,DateTimeStyles.None,out start)) return "month";
   int year,quarter;
   if(period.Length==7 && period[4]=='-' && period[5]=='Q' && int.TryParse(period.Substring(0,4),NumberStyles.None,Invariant,out year) && year>=1 && int.TryParse(period.Substring(6),NumberStyles.None,Invariant,out quarter) && quarter>=1 && quarter<=4) { start=new DateTime(year,quarter*3-2,1); return "quarter"; }
   if(period.Length==4 && int.TryParse(period,NumberStyles.None,Invariant,out year) && year>=1) { start=new DateTime(year,1,1); return "year"; }
   start=DateTime.MinValue; return null;
  }
  // The period a number of steps after (or, when negative, before) this one, in the same span.
  public static string Shift(string period,int steps) {
   string span=SpanOf(period); var start=Start(period);
   return Period(span,span=="week"?start.AddDays(7*steps):span=="month"?start.AddMonths(steps):span=="quarter"?start.AddMonths(3*steps):start.AddYears(steps));
  }
  public static string Previous(string period) { return Shift(period,-1); }
  // The day whose month, quarter and year a week shows: today in the current week, otherwise the week's middle day.
  public static DateTime Anchor(DateTime day,DateTime today) { var first=Schedule.WeekStart(day); return today>=first && today<first.AddDays(7)?today.Date:first.AddDays(3); }
  public static List<Goal> For(State state,string period) { return state.Goals.Where(g=>g.Period==period).ToList(); }
  // Moves unfinished goals from the previous period into this one, as many as fit. Returns how many moved.
  public static int CarryOver(State state,string period) {
   int room=MaxPerPeriod-For(state,period).Count; if(room<=0) return 0;
   var moving=For(state,Previous(period)).Where(g=>!g.Done).Take(room).ToList(); foreach(var g in moving) g.Period=period; return moving.Count;
  }
  public static int Unfinished(State state,string period) { return For(state,period).Count(g=>!g.Done); }
  public static void Validate(State s) {
   var ids=new HashSet<string>();
   if(s.Goals.Count>2000 || s.Goals.Any(g=>g==null || string.IsNullOrWhiteSpace(g.Id) || g.Id.Length>40 || !ids.Add(g.Id) || string.IsNullOrWhiteSpace(g.Text) || g.Text.Length>200 || SpanOf(g.Period)==null) || s.Goals.GroupBy(g=>g.Period).Any(p=>p.Count()>MaxPerPeriod))
    throw new Exception("Goals need 1–200 characters, with up to 5 for each week, month, quarter or year.");
  }
 }
 // Goals: one pill per span that has goals for the week shown, between the week dates and the Up next card; in day view, on a card to the right of the date.
 // A pill opens a popover to tick, add or remove that span's goals. When the pills don't fit, they merge into a single Goals pill.
 public partial class MainWindow {
  DockPanel dateRow; StackPanel dateText; Border goalsHost; StackPanel goalPills; Button goalsMerged;
  internal Popup GoalsPopup;
  internal bool GoalsMerged { get { return goalsMerged!=null && goalsMerged.Visibility==Visibility.Visible; } }
  static string Capitalized(string text) { return text.Length==0?text:char.ToUpper(text[0],UI.Culture)+text.Substring(1); }
  static string GoalLabel(string span,DateTime anchor) {
   if(span=="week") return UI.T("Week");
   if(span=="month") return Capitalized(anchor.ToString("MMMM",UI.Culture));
   if(span=="quarter") return UI.T("Q")+((anchor.Month+2)/3);
   return anchor.Year.ToString(CultureInfo.InvariantCulture);
  }
  static string GoalTitle(string span,DateTime anchor) {
   if(span=="week") return UI.T("Week of ")+Schedule.WeekStart(anchor).ToString("d MMM",UI.Culture);
   if(span=="month") return Capitalized(anchor.ToString("MMMM yyyy",UI.Culture));
   if(span=="quarter") return GoalLabel(span,anchor)+" "+anchor.Year;
   return GoalLabel(span,anchor);
  }
  static readonly Dictionary<string,string> GoalTips=new Dictionary<string,string> { {"week","Weekly goals"},{"month","Monthly goals"},{"quarter","Quarterly goals"},{"year","Yearly goals"} };
  static readonly Dictionary<string,string> CarryText=new Dictionary<string,string> { {"week","Move {0} unfinished from last week"},{"month","Move {0} unfinished from last month"},{"quarter","Move {0} unfinished from last quarter"},{"year","Move {0} unfinished from last year"} };
  static readonly Dictionary<string,string> SpanNames=new Dictionary<string,string> { {"week","Week"},{"month","Month"},{"quarter","Quarter"},{"year","Year"} };
  Button GoalPill(string label,int done,int total,string tooltip,Action open) {
   bool complete=total>0 && done==total; var text=new TextBlock { FontSize=12 };
   text.Inlines.Add(new Run(label) { FontWeight=FontWeights.SemiBold }); if(total>0) text.Inlines.Add(new Run("  "+done+"/"+total) { Foreground=complete?UI.AccentInk:UI.Muted });
   var pill=UI.Button("",open,complete); pill.Content=text; pill.Margin=new Thickness(0,0,6,0); pill.MinHeight=28; pill.ToolTip=tooltip; pill.VerticalAlignment=VerticalAlignment.Bottom; return pill;
  }
  // The day whose week, month, quarter and year the goals follow: the day shown in day view, see GoalPlan.Anchor for the week view.
  DateTime GoalAnchor() { return State.WeekView?GoalPlan.Anchor(selected,AppClock.Today):selected.Date; }
  // Rebuilds the pills for the week shown. Skipped while the popover is open, so it stays anchored to its pill.
  void FillGoals() {
   if(goalsHost==null || (GoalsPopup!=null && GoalsPopup.IsOpen)) return; goalsHost.Visibility=Visibility.Visible;
   var anchor=GoalAnchor(); goalPills.Children.Clear(); int done=0,total=0;
   foreach(var span in GoalPlan.Spans) {
    var goals=GoalPlan.For(State,GoalPlan.Period(span,anchor)); if(goals.Count==0) continue;
    int d=goals.Count(g=>g.Done); done+=d; total+=goals.Count; string spanKey=span; Button pill=null;
    pill=GoalPill(GoalLabel(span,anchor),d,goals.Count,UI.T(GoalTips[span])+" · "+d+"/"+goals.Count,()=>OpenGoals(pill,spanKey)); goalPills.Children.Add(pill);
   }
   Button add=null; add=total==0?UI.Button("+ Goal",()=>OpenGoals(add,null)):UI.Icon("","Add a goal",()=>OpenGoals(add,null));
   add.ToolTip=UI.T("Add a goal"); add.VerticalAlignment=VerticalAlignment.Bottom;
   if(total==0) { add.Background=Brushes.Transparent; add.Foreground=UI.Muted; add.MinHeight=28; add.Margin=new Thickness(0); } else { add.Width=28; add.Height=28; add.FontSize=11; add.Foreground=UI.Muted; }
   goalPills.Children.Add(add);
   goalsMerged=total==0?UI.Button("+ Goal",()=>OpenGoals(goalsMerged,null)):GoalPill(UI.T("Goals"),done,total,UI.T("Goals")+" · "+done+"/"+total,()=>OpenGoals(goalsMerged,null));
   if(total==0) { goalsMerged.Background=Brushes.Transparent; goalsMerged.Foreground=UI.Muted; goalsMerged.MinHeight=28; goalsMerged.ToolTip=UI.T("Add a goal"); }
   goalsMerged.Margin=new Thickness(0); goalsMerged.VerticalAlignment=VerticalAlignment.Bottom; goalsMerged.HorizontalAlignment=HorizontalAlignment.Right; goalPills.HorizontalAlignment=HorizontalAlignment.Left;
   var old=goalsHost.Child as Grid; if(old!=null) old.Children.Clear(); var both=new Grid(); both.Children.Add(goalPills); both.Children.Add(goalsMerged); goalsHost.Child=both;
   bool card=!State.WeekView && total>0; goalsHost.Background=card?UI.Hero:null; goalsHost.CornerRadius=new CornerRadius(10); goalsHost.Padding=card?new Thickness(8,6,6,6):new Thickness(0);
   FitGoals();
  }
  // Shows every pill when they fit (beside the week dates, or on the day view card); otherwise one merged pill, and the dates trim.
  void FitGoals() {
   if(goalsHost==null || goalsMerged==null) return;
   var unlimited=new Size(double.PositiveInfinity,double.PositiveInfinity);
   goalPills.Visibility=Visibility.Visible; goalsMerged.Visibility=Visibility.Visible; goalPills.Measure(unlimited); dateText.Measure(unlimited); weekNow.Measure(unlimited);
   double room=dateRow.ActualWidth-2-dateText.DesiredSize.Width-(weekNow.Visibility==Visibility.Visible?weekNow.DesiredSize.Width:0)-goalsHost.Margin.Left-goalsHost.Margin.Right-goalsHost.Padding.Left-goalsHost.Padding.Right;
   bool fits=dateRow.ActualWidth>0 && goalPills.DesiredSize.Width<=room;
   goalPills.Visibility=fits?Visibility.Visible:Visibility.Collapsed; goalsMerged.Visibility=fits?Visibility.Collapsed:Visibility.Visible; goalsHost.Width=fits && State.WeekView?Math.Max(0,room):double.NaN; // in day view the card sits at the right
  }
  // Popover for one span's goals, or (span null) every span that has goals, with a span picker for new ones.
  void OpenGoals(FrameworkElement target,string span) {
   if(target==null || (GoalsPopup!=null && GoalsPopup.IsOpen)) return;
   var anchor=GoalAnchor(); string addSpan=span??"week",editing=null; int addOffset=0; bool focusInput=false;
   var panel=new StackPanel { Width=290 };
   var popup=new Popup { PlacementTarget=target,Placement=PlacementMode.Bottom,VerticalOffset=6,StaysOpen=false,AllowsTransparency=true };
   // The popover takes its font from its target, and the + button uses the icon font, so set the text font here.
   var frame=new Border { LayoutTransform=new ScaleTransform(UI.Scale,UI.Scale),Child=new ScrollViewer { Content=panel,MaxHeight=520,VerticalScrollBarVisibility=ScrollBarVisibility.Auto },Background=UI.Card,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(14,12,14,12) }; TextElement.SetFontFamily(frame,new FontFamily("Segoe UI")); popup.Child=frame;
   Action fill=null;
   // One goal: tick, edit and remove; while editing, a text box that saves on Enter or when it loses focus, and Esc cancels.
   Action<Goal> addGoalRow=goal=> {
    var entry=goal; var row=new DockPanel { Margin=new Thickness(0,1,0,1) };
    if(editing==entry.Id) {
     var box=UI.Input(entry.Text); box.MaxLength=200; box.FontSize=12; box.Padding=new Thickness(5,3,5,3); box.Margin=new Thickness(0); row.Children.Add(box);
     Action<bool> finish=keep=> {
      if(editing!=entry.Id) return; editing=null; string text=(box.Text??"").Trim();
      if(keep && text.Length>0 && text!=entry.Text) { string before=entry.Text; entry.Text=text; if(!Save()) entry.Text=before; }
      fill();
     };
     box.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter) { e.Handled=true; finish(true); } else if(e.Key==Key.Escape) { e.Handled=true; finish(false); } };
     box.LostKeyboardFocus+=(s,e)=>finish(true);
     Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=> { box.Focus(); box.SelectAll(); }));
    } else {
     var remove=UI.Icon("\uE711","Remove goal",()=> { int at=State.Goals.IndexOf(entry); State.Goals.Remove(entry); if(!Save()) State.Goals.Insert(at,entry); fill(); });
     var edit=UI.Icon("\uE70F","Edit goal",()=> { editing=entry.Id; fill(); });
     foreach(var icon in new[]{remove,edit}) { icon.Width=24; icon.Height=24; icon.FontSize=10; icon.Foreground=UI.Muted; icon.Margin=new Thickness(0); icon.VerticalAlignment=VerticalAlignment.Top; DockPanel.SetDock(icon,Dock.Right); row.Children.Add(icon); }
     var tick=UI.Tick(entry.Text,entry.Done,UI.Text,UI.Accent,UI.AccentInk); tick.VerticalAlignment=VerticalAlignment.Center; tick.Click+=(s,e)=> { entry.Done=!entry.Done; if(!Save()) entry.Done=!entry.Done; fill(); }; row.Children.Add(tick);
    }
    panel.Children.Add(row);
   };
   fill=()=> {
    panel.Children.Clear(); bool any=false;
    foreach(var sp in GoalPlan.Spans) {
     string period=GoalPlan.Period(sp,anchor); var goals=GoalPlan.For(State,period);
     int carry=Math.Min(GoalPlan.Unfinished(State,GoalPlan.Previous(period)),GoalPlan.MaxPerPeriod-goals.Count);
     if(span!=null ? sp!=span : goals.Count==0 && carry==0) continue;
     var heading=UI.Label(GoalTitle(sp,anchor).ToUpper(UI.Culture)+(goals.Count>0?"  ·  "+goals.Count(g=>g.Done)+"/"+goals.Count:""),10,UI.Accent); heading.Margin=new Thickness(0,any?10:0,0,4); panel.Children.Add(heading); any=true;
     foreach(var goal in goals) addGoalRow(goal);
     if(carry>0) {
      var move=UI.Button(string.Format(UI.T(CarryText[sp]),carry),()=> { var before=State.Goals.Select(g=>g.Period).ToList(); GoalPlan.CarryOver(State,period); if(!Save()) for(int i=0;i<before.Count;i++) State.Goals[i].Period=before[i]; fill(); });
      move.Background=Brushes.Transparent; move.Foreground=UI.Accent; move.MinHeight=26; move.FontSize=11; move.Margin=new Thickness(-10,0,0,0); move.HorizontalAlignment=HorizontalAlignment.Left; panel.Children.Add(move);
     }
    }
    if(span==null && !any) { var hint=UI.Label("Set a goal for this week, month, quarter or year.",12,UI.Muted); hint.Margin=new Thickness(0,0,0,4); panel.Children.Add(hint); }
    // Adding: choose the span (from + only) and step to the period the goal is for, such as next month.
    var caption=UI.Label("ADD A GOAL",10,UI.Muted); caption.Margin=new Thickness(0,any?14:6,0,4); panel.Children.Add(caption);
    if(span==null) {
     var picker=new WrapPanel { Margin=new Thickness(0,0,0,2) };
     foreach(var sp in GoalPlan.Spans) { string spanKey=sp; var choice=UI.Button(SpanNames[sp],()=> { addSpan=spanKey; addOffset=0; focusInput=true; fill(); },sp==addSpan); choice.MinHeight=26; choice.FontSize=11; choice.Margin=new Thickness(0,0,4,4); picker.Children.Add(choice); }
     panel.Children.Add(picker);
    }
    string addPeriod=GoalPlan.Shift(GoalPlan.Period(addSpan,anchor),addOffset);
    var stepper=new DockPanel { Margin=new Thickness(0,0,0,6) };
    var back=UI.Icon("\uE76B","Previous",()=> { addOffset--; focusInput=true; fill(); }); var forward=UI.Icon("\uE76C","Next",()=> { addOffset++; focusInput=true; fill(); });
    foreach(var step in new[]{back,forward}) { step.Width=26; step.Height=26; step.FontSize=10; step.Margin=new Thickness(0); }
    DockPanel.SetDock(back,Dock.Left); DockPanel.SetDock(forward,Dock.Right); stepper.Children.Add(back); stepper.Children.Add(forward);
    var periodName=new TextBlock { Text=GoalTitle(addSpan,GoalPlan.Start(addPeriod)),FontSize=12,FontWeight=addOffset==0?FontWeights.Normal:FontWeights.SemiBold,Foreground=addOffset==0?UI.Muted:UI.Accent,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,ToolTip=UI.T("The period this goal is for") };
    stepper.Children.Add(periodName); panel.Children.Add(stepper);
    // Goals of another period are listed here, so they can be checked and edited before that period comes around.
    var chosen=GoalPlan.For(State,addPeriod); if(addOffset!=0) foreach(var goal in chosen) addGoalRow(goal);
    if(chosen.Count>=GoalPlan.MaxPerPeriod) { var note=UI.Label("Up to 5 goals for each period.",11,UI.Muted); note.Margin=new Thickness(0,6,0,0); panel.Children.Add(note); }
    else {
     var input=UI.Input(""); input.MaxLength=200; input.FontSize=13; input.Padding=new Thickness(6,5,6,5); input.Margin=new Thickness(0); input.ToolTip=UI.T("Type a goal and press Enter");
     Action add=()=> {
      string text=(input.Text??"").Trim(); if(text.Length==0) return;
      var goal=new Goal { Id=Guid.NewGuid().ToString("N"),Text=text,Period=addPeriod }; State.Goals.Add(goal); if(!Save()) { State.Goals.Remove(goal); return; }
      focusInput=true; fill();
     };
     input.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter) { e.Handled=true; add(); } };
     var addRow=new DockPanel { Margin=new Thickness(0,addOffset!=0&&chosen.Count>0?6:0,0,0) }; var addButton=UI.Button("Add",add,true); addButton.MinHeight=28; addButton.Margin=new Thickness(6,0,0,0); DockPanel.SetDock(addButton,Dock.Right); addRow.Children.Add(addButton); addRow.Children.Add(input); panel.Children.Add(addRow);
     if(focusInput && editing==null) { focusInput=false; Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=>input.Focus())); }
    }
   };
   fill();
   popup.KeyDown+=(s,e)=> { if(e.Key==Key.Escape) popup.IsOpen=false; };
   popup.Closed+=(s,e)=> { if(GoalsPopup==popup) GoalsPopup=null; Refresh(true); };
   GoalsPopup=popup; popup.IsOpen=true;
  }
 }
}
