using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace Dayglance {
 public partial class MainWindow {
  DockPanel weekPanel;
  ScrollViewer weekScroll;
  double weekZoom=1,dayZoom=1,weekColumn=-1;
  DateTime weekFirst;
  bool sizing;
  bool focusNow,pulseNow; Border weekNowTarget,weekNowCard; Ellipse weekNowDot; // set when compact/expand toggles in week view: scroll the current activity into sight on the next render
  // Day and week share one window size and position; the mini widget keeps its own. Positions are clamped to the virtual screen so multi-monitor placement survives restarts.
  void SetSize() {
   // Read the stored geometry and raise the guard BEFORE touching MinWidth/Width: changing them moves/resizes the window,
   // and the resulting LocationChanged/SizeChanged would otherwise overwrite the size we are about to restore.
   var area=SystemParameters.WorkArea; double scale=UI.Scale,width,height,left,top,minWidth,minHeight;
   bool mini=State.Compact;
   if(mini) {
    minWidth=240*scale; minHeight=150*scale; width=State.MiniWidth; height=State.MiniHeight; left=State.MiniLeft; top=State.MiniTop;
    if(width<minWidth) width=300*scale; if(height<minHeight) height=190*scale;
    if(State.MiniWidth<1) { left=(double.IsNaN(Left)?State.Left:Left)+(double.IsNaN(Width)?State.WindowWidth:Width)-width; top=double.IsNaN(Top)?State.Top:Top; }
   } else {
    minWidth=Math.Min(380*scale,area.Width); minHeight=Math.Min(340*scale,area.Height); width=State.WindowWidth; height=State.WindowHeight; left=State.Left; top=State.Top;
    if(width<minWidth) width=520*scale; if(height<minHeight) height=820*scale;
   }
   sizing=true;
   try {
    MinWidth=0; MinHeight=0;
    Width=Math.Min(width,area.Width); Height=Math.Min(height,area.Height);
    MinWidth=minWidth; MinHeight=minHeight;
    double vl=SystemParameters.VirtualScreenLeft,vt=SystemParameters.VirtualScreenTop,vr=vl+SystemParameters.VirtualScreenWidth,vb=vt+SystemParameters.VirtualScreenHeight;
    Left=Math.Max(vl,Math.Min(left,vr-Width)); Top=Math.Max(vt,Math.Min(top,vb-Height));
    // The virtual screen can include dead corners between monitors: if the title bar would not land on any monitor, place the window on the primary one.
    if(!OnSomeScreen(Left,Top,Width)) { Left=Math.Max(area.Left,area.Right-Width-24); Top=area.Top+24; }
    // Store exactly what was applied, so a late layout event can only confirm these values.
    if(mini) { State.MiniWidth=Width; State.MiniHeight=Height; State.MiniLeft=Left; State.MiniTop=Top; }
    else { State.WindowWidth=Width; State.WindowHeight=Height; State.Left=Left; State.Top=Top; }
   } finally { sizing=false; }
  }
  static bool OnSomeScreen(double left,double top,double width) {
   try {
    if(double.IsNaN(left)||double.IsNaN(top)) return false;
    double dpi=System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width/Math.Max(1,SystemParameters.PrimaryScreenWidth);
    var bar=new System.Drawing.Rectangle((int)((left+24)*dpi),(int)(top*dpi),(int)(Math.Max(60,width-48)*dpi),(int)(32*dpi));
    return System.Windows.Forms.Screen.AllScreens.Any(sc=>sc.WorkingArea.IntersectsWith(bar));
   } catch { return true; }
  }
  void RememberSize() {
   if(sizing || WindowState!=WindowState.Normal || ActualWidth<1 || ActualHeight<1 || double.IsNaN(Left) || double.IsNaN(Top)) return;
   if(State.Compact) { State.MiniWidth=ActualWidth; State.MiniHeight=ActualHeight; State.MiniLeft=Left; State.MiniTop=Top; }
   else { State.WindowWidth=ActualWidth; State.WindowHeight=ActualHeight; State.Left=Left; State.Top=Top; }
  }
  System.Windows.Threading.DispatcherTimer geometryTimer;
  // Saves size/position shortly after the user stops moving or resizing, so it survives even if the app is killed.
  void QueueGeometrySave() {
   if(preview || sizing) return;
   if(geometryTimer==null) { geometryTimer=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromSeconds(1.5) }; geometryTimer.Tick+=(s,e)=> { geometryTimer.Stop(); if(!settingsOpen) Save(); }; }
   geometryTimer.Stop(); geometryTimer.Start();
  }
  Grid miniHost; int miniCapacity=-1; double miniPrimaryHeight=90;
  // Small always-available widget: the current (or next) activity, plus neighbouring activities when the window is tall enough.
  void BuildMini() {
   weekPanel=null; weekScroll=null;
   var root=new DockPanel { Margin=new Thickness(12,6,8,10),LastChildFill=true,Background=Brushes.Transparent };
   Content=new Border { BorderBrush=UI.Line,BorderThickness=new Thickness(1),Child=root,LayoutTransform=new ScaleTransform(UI.Scale,UI.Scale) };
   root.MouseLeftButtonDown+=(s,e)=> { if(e.ClickCount==2) ToggleCompact(); else if(e.LeftButton==MouseButtonState.Pressed) DragMove(); };
   var header=new DockPanel { Margin=new Thickness(0,0,0,4) };
   var tools=UI.Row(); tools.VerticalAlignment=VerticalAlignment.Center;
   pin=UI.Icon("","Pin on top",()=> { State.Pinned=!State.Pinned; Topmost=State.Pinned; Save(); Refresh(true); }); pin.Width=28; pin.Height=28; tools.Children.Add(pin);
   compact=UI.Icon("","Expand",ToggleCompact); compact.Width=28; compact.Height=28; tools.Children.Add(compact);
   var hide=UI.Icon("","Hide to tray",()=>Close()); hide.Width=28; hide.Height=28; hide.FontSize=11; tools.Children.Add(hide);
   DockPanel.SetDock(tools,Dock.Right); header.Children.Add(tools);
   var brand=UI.Row(); brand.VerticalAlignment=VerticalAlignment.Center; var mark=(FrameworkElement)BrandIcon.Visual(); mark.Margin=new Thickness(0); brand.Children.Add(new Viewbox { Width=14,Height=14,Child=mark,Margin=new Thickness(0,0,7,0) });
   clockLabel=UI.Label("",11,UI.Muted); clockLabel.Margin=new Thickness(0); clockLabel.VerticalAlignment=VerticalAlignment.Center; brand.Children.Add(clockLabel); header.Children.Add(brand);
   DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
   hero=new StackPanel { VerticalAlignment=VerticalAlignment.Center }; miniHost=new Grid { ClipToBounds=true,Background=Brushes.Transparent }; miniHost.Children.Add(hero); root.Children.Add(miniHost);
   miniCapacity=-1; miniHost.SizeChanged+=(s,e)=> { if(MiniCapacity()!=miniCapacity) Refresh(true); };
   Refresh(true);
  }
  const double MiniRowHeight=50; // secondary card height including its margins
  int MiniCapacity() { if(miniHost==null || miniHost.ActualHeight<1) return 0; return Math.Max(0,Math.Min(4,(int)Math.Floor((miniHost.ActualHeight-miniPrimaryHeight-2)/MiniRowHeight))); }
  // Card with a hover "Edit" pill; clicking the card itself opens the full schedule at that activity.
  FrameworkElement MiniCard(Occurrence o,bool primary,bool running,DateTime now) {
   var body=new StackPanel(); Border card;
   if(primary) {
    card=new Border { Background=UI.Hero,CornerRadius=new CornerRadius(10),Padding=new Thickness(12,8,12,10),Child=body };
    var eyebrow=new DockPanel(); var dot=new Ellipse { Width=8,Height=8,Fill=UI.B(o.Activity.Color),Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center }; DockPanel.SetDock(dot,Dock.Left); eyebrow.Children.Add(dot);
    eyebrow.Children.Add(new TextBlock { Text=UI.T(running?"RIGHT NOW":"UP NEXT"),FontSize=9,Foreground=UI.Accent,VerticalAlignment=VerticalAlignment.Center }); body.Children.Add(eyebrow);
    body.Children.Add(new TextBlock { Text=o.Activity.Title,FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=UI.Text,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,54,0) });
    string detail=running?UI.Clock(o.Start)+" – "+UI.Clock(o.End)+"  ·  "+Math.Ceiling((o.End-now).TotalMinutes)+UI.T(" min left"):(o.Start.Date==now.Date?"":o.Start.ToString("ddd ",UI.Culture))+UI.Clock(o.Start)+" – "+UI.Clock(o.End);
    body.Children.Add(new TextBlock { Text=detail,FontSize=11,Foreground=UI.Muted,TextTrimming=TextTrimming.CharacterEllipsis });
    if(running) body.Children.Add(new ProgressBar { Minimum=0,Maximum=100,Value=(now-o.Start).TotalSeconds/(o.End-o.Start).TotalSeconds*100,Height=3,Foreground=UI.B(o.Activity.Color),Background=UI.Line,BorderThickness=new Thickness(0),Margin=new Thickness(0,6,0,0) });
   } else {
    // Neighbouring activities: outline only, on the window background, so the current card stays dominant.
    bool done=State.Completed.Contains(o.Key)||o.End<=now;
    card=new Border { Background=Brushes.Transparent,BorderBrush=UI.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(10,4,10,5),Margin=new Thickness(0,3,0,3),Child=body,Opacity=done?.55:.85 };
    var titleRow=new DockPanel(); var dot=new Ellipse { Width=7,Height=7,Fill=UI.B(o.Activity.Color),Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center }; DockPanel.SetDock(dot,Dock.Left); titleRow.Children.Add(dot);
    titleRow.Children.Add(new TextBlock { Text=o.Activity.Title,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=UI.Text,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,0,48,0) }); body.Children.Add(titleRow);
    body.Children.Add(new TextBlock { Text=(o.Start.Date==now.Date?"":o.Start.ToString("ddd ",UI.Culture))+UI.Clock(o.Start)+" – "+UI.Clock(o.End),FontSize=11,Foreground=UI.Muted,Margin=new Thickness(13,0,0,0) });
   }
   var holder=new Grid { Cursor=Cursors.Hand,ToolTip=UI.T("Open schedule") }; holder.Children.Add(card);
   var edit=new Border { Background=UI.Accent,CornerRadius=new CornerRadius(8),Padding=new Thickness(6,5,6,5),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,primary?8:6,8,0),Visibility=Visibility.Hidden,Cursor=Cursors.Hand,ToolTip=UI.T("Edit activity"),
    Child=new TextBlock { Text="\uE70F",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=12,Foreground=UI.AccentInk } };
   edit.MouseLeftButtonDown+=(s,e)=>e.Handled=true; edit.MouseLeftButtonUp+=(s,e)=> { e.Handled=true; Edit(o.Activity); };
   holder.Children.Add(edit);
   holder.MouseEnter+=(s,e)=>edit.Visibility=Visibility.Visible; holder.MouseLeave+=(s,e)=>edit.Visibility=Visibility.Hidden;
   holder.MouseLeftButtonDown+=(s,e)=>e.Handled=true;
   holder.MouseLeftButtonUp+=(s,e)=> { e.Handled=true; ExpandTo(o); };
   return holder;
  }
  void ExpandTo(Occurrence o) {
   string key=o.Key; DateTime day=o.Start.Date;
   ToggleCompact(()=> { if(State.WeekView) FocusWeekNow(); else if(day==DateTime.Today) FocusActivity(key,true); });
  }
  void RefreshMini(System.Collections.Generic.List<Occurrence> today,System.Collections.Generic.List<Occurrence> active,DateTime now,bool activityChanged) {
   clockLabel.Text=now.ToString("ddd d MMM",UI.Culture)+"  ·  "+UI.Clock(now).ToUpperInvariant();
   hero.Children.Clear();
   bool running=active.Count>0;
   var upcoming=new System.Collections.Generic.List<Occurrence>();
   for(int i=0;i<8 && upcoming.Count<8;i++) foreach(var o in Schedule.ForDay(State,now.Date.AddDays(i))) if(o.Start>now && !State.Completed.Contains(o.Key) && !upcoming.Any(u=>u.Key==o.Key)) upcoming.Add(o);
   upcoming=upcoming.OrderBy(o=>o.Start).ToList();
   Occurrence shown=running?active[0]:upcoming.FirstOrDefault();
   if(shown==null) {
    var empty=new Border { Background=UI.Hero,CornerRadius=new CornerRadius(10),Padding=new Thickness(12,8,12,10) }; var body=new StackPanel(); empty.Child=body;
    body.Children.Add(new TextBlock { Text=UI.T("ROOM TO BREATHE"),FontSize=9,Foreground=UI.Accent });
    body.Children.Add(new TextBlock { Text=UI.T(State.Activities.Count==0?"Make room for your day.":"You’re between activities."),FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=UI.Text,TextWrapping=TextWrapping.Wrap });
    hero.Children.Add(empty); miniCapacity=MiniCapacity(); return;
   }
   var primary=MiniCard(shown,true,running,now);
   primary.Measure(new Size(Math.Max(120,miniHost.ActualWidth),double.PositiveInfinity)); miniPrimaryHeight=primary.DesiredSize.Height;
   int slots=MiniCapacity(); miniCapacity=slots;
   var nexts=upcoming.Where(o=>o.Key!=shown.Key).ToList();
   var previous=today.Where(o=>o.End<=now && o.Key!=shown.Key).OrderByDescending(o=>o.End).FirstOrDefault();
   var above=new System.Collections.Generic.List<Occurrence>(); var below=new System.Collections.Generic.List<Occurrence>();
   if(slots>=2 && previous!=null && nexts.Count>0) { above.Add(previous); slots--; }
   foreach(var o in nexts) { if(slots<=0) break; below.Add(o); slots--; }
   if(slots>0 && previous!=null && above.Count==0) above.Add(previous);
   foreach(var o in above) hero.Children.Add(MiniCard(o,false,false,now));
   hero.Children.Add(primary);
   foreach(var o in below) hero.Children.Add(MiniCard(o,false,false,now));
   if(below.Count==0 && running) {
    var after=nexts.FirstOrDefault();
    hero.Children.Add(new TextBlock { Text=after==null?UI.T("Nothing else today"):UI.T("After")+" · "+after.Activity.Title+"  "+UI.Clock(after.Start),FontSize=11,Foreground=UI.Muted,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(4,6,0,0) });
   }
   if(activityChanged) UI.SlideIn(primary,0,18);
  }
  System.Collections.Generic.List<UIElement>[] weekParts; double weekVisibleWidth,weekTargetOffset; int weekEnterFocus=-1;
  // Day <-> week: the focused day's column morphs between the full width and its slot while the other days slide in/out from the sides.
  public void SetView(bool week) {
   if(State.WeekView==week) return;
   Action change=()=> { RememberSize(); State.WeekView=week; weekColumn=-1; focusNow=week; BuildView(); if(!preview) Save(); };
   if(preview || State.Compact || !SystemParameters.ClientAreaAnimation || transitioning) { change(); return; }
   if(week) { weekEnterFocus=Math.Max(0,Math.Min(6,(selected.Date-Schedule.WeekStart(selected)).Days)); change(); if(scroll==null) return; return; }
   int focus=(selected.Date-weekFirst).Days;
   if(weekParts==null || weekScroll==null || focus<0 || focus>6) { Transition(change,null); return; }
   transitioning=true;
   AnimateWeekColumns(focus,false,weekScroll.HorizontalOffset,()=> { transitioning=false; change(); if(scroll!=null) UI.SlideIn(scroll,0,10); });
  }
  readonly System.Collections.Generic.HashSet<Border> weekCardSet=new System.Collections.Generic.HashSet<Border>(); Canvas weekCanvas;
  void AnimateWeekColumns(int focus,bool entering,double offset,Action done) {
   if(weekParts==null || weekColumn<=0 || weekCanvas==null) { if(done!=null) done(); return; }
   double column=weekColumn,span=Math.Max(column,weekVisibleWidth);
   var ease=new System.Windows.Media.Animation.CubicEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut };
   var baseDuration=TimeSpan.FromMilliseconds(entering?460:340);
   // Where the focused day's cards live in day view: a slim line, a color band or the whole card.
   string style=State.CardStyle; double dayLeft=style=="stripe"?offset+16:offset+2,dayWidth=style=="stripe"?4:style=="band"?66:Math.Max(40,span-4);
   bool reported=false;
   for(int d=0;d<7;d++) foreach(var part in weekParts[d]) {
    var element=part as FrameworkElement; if(element==null) continue;
    var duration=TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds+(d==focus?0:35*Math.Abs(d-focus)));
    if(d==focus) {
     var card=element as Border;
     if(card!=null && weekCardSet.Contains(card)) {
      // Only a colored ghost morphs; the real card (with its text) is hidden meanwhile, so nothing is stretched.
      double left=Canvas.GetLeft(card),top=Canvas.GetTop(card),baseOpacity=card.Opacity;
      var ghost=new Border { Width=entering?dayWidth:card.Width,Height=card.Height,CornerRadius=new CornerRadius(5),Background=card.Background,IsHitTestVisible=false,Opacity=baseOpacity };
      Canvas.SetLeft(ghost,entering?dayLeft:left); Canvas.SetTop(ghost,top); Panel.SetZIndex(ghost,13); weekCanvas.Children.Add(ghost);
      var slide=new System.Windows.Media.Animation.DoubleAnimation(entering?dayLeft:left,entering?left:dayLeft,duration) { EasingFunction=ease };
      var grow=new System.Windows.Media.Animation.DoubleAnimation(entering?dayWidth:card.Width,entering?card.Width:dayWidth,duration) { EasingFunction=ease };
      var target=card; var shape=ghost;
      if(entering) {
       target.Opacity=0;
       grow.Completed+=(s,e)=> { target.Opacity=baseOpacity; target.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,baseOpacity,TimeSpan.FromMilliseconds(160)) { FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop }); var parent=shape.Parent as Panel; if(parent!=null) parent.Children.Remove(shape); };
      } else target.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(baseOpacity,0,TimeSpan.FromMilliseconds(90)));
      if(!reported && done!=null) { reported=true; grow.Completed+=(s,e)=>done(); }
      ghost.BeginAnimation(Canvas.LeftProperty,slide); ghost.BeginAnimation(FrameworkElement.WidthProperty,grow);
     } else {
      double opacity=element.Opacity;
      element.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(entering?0:opacity,entering?opacity:0,duration) { EasingFunction=ease });
     }
    } else {
     double distance=(d<focus?-1:1)*column*1.4; var move=new TranslateTransform(); element.RenderTransform=move; double opacity=element.Opacity;
     move.BeginAnimation(TranslateTransform.XProperty,new System.Windows.Media.Animation.DoubleAnimation(entering?distance:0,entering?0:distance,duration) { EasingFunction=ease });
     element.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(entering?0:opacity,entering?opacity:0,duration) { EasingFunction=ease });
    }
   }
   if(!reported && done!=null) { var wait=new System.Windows.Threading.DispatcherTimer { Interval=baseDuration }; wait.Tick+=(s,e)=> { wait.Stop(); done(); }; wait.Start(); }
  }
  public void ZoomSchedule(int direction,double anchor) {
   if(!State.WeekView) { dayZoom=Math.Max(.8,Math.Min(1.7,dayZoom*(direction>0?1.1:1/1.1))); list.LayoutTransform=new ScaleTransform(dayZoom,dayZoom); return; }
   double before=weekZoom,previous=weekScroll.VerticalOffset; weekZoom=Math.Max(.65,Math.Min(3,weekZoom*(direction>0?1.15:1/1.15))); RenderWeek(); weekScroll.UpdateLayout(); weekScroll.ScrollToVerticalOffset((previous+anchor)*weekZoom/before-anchor);
  }
  // Week grid: a fixed hour gutter, day headings and a body that shows 3–7 day columns.
  // With fewer than seven columns the body scrolls horizontally and resizing keeps today centered.
  void RenderWeek() {
   if(weekPanel==null || ActualWidth<1) return;
   double offset=weekScroll==null?0:weekScroll.VerticalOffset,hOffset=weekScroll==null?-1:weekScroll.HorizontalOffset;
   weekPanel.Children.Clear(); weekCardSet.Clear(); weekParts=new System.Collections.Generic.List<UIElement>[7]; for(int part=0;part<7;part++) weekParts[part]=new System.Collections.Generic.List<UIElement>(); weekNowTarget=null; weekNowCard=null; weekNowDot=null; DateTime first=Schedule.WeekStart(selected),now=DateTime.Now;
   const double bar=10,hbar=8,minColumn=110; double gutter=UI.Hour12?54:44;
   double total=weekPanel.ActualWidth>100?weekPanel.ActualWidth:ActualWidth-40, body=Math.Max(150,total-gutter-bar);
   int visible=Math.Max(3,Math.Min(7,(int)Math.Floor(body/minColumn))); double column=body/visible,width=column*7; bool sliding=visible<7;
   double viewport=(weekPanel.ActualHeight>100?weekPanel.ActualHeight:ActualHeight-300)-(sliding?hbar:0);
   double rowHeight=Math.Max(14,42*weekZoom), canvasHeight=rowHeight*24+28;
   var headRow=new DockPanel { Height=43 }; DockPanel.SetDock(headRow,Dock.Top); weekPanel.Children.Add(headRow);
   var corner=new Border { Width=gutter }; DockPanel.SetDock(corner,Dock.Left); headRow.Children.Add(corner);
   var headings=new Canvas { Height=43,Width=width,HorizontalAlignment=HorizontalAlignment.Left };
   var headScroll=new ScrollViewer { Content=headings,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,bar,0) }; headRow.Children.Add(headScroll);
   for(int d=0;d<7;d++) {
    DateTime date=first.AddDays(d); bool today=date==now.Date;
    var button=UI.Button(date.ToString(column<100?"ddd d":"ddd  d",UI.Culture),()=>Navigate(false,date),today); button.Width=column-4; button.Margin=new Thickness(0); button.ToolTip=UI.T("Show this day"); Canvas.SetLeft(button,d*column); headings.Children.Add(button); weekParts[d].Add(button);
   }
   var grid=new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(gutter) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); weekPanel.Children.Add(grid);
   var hours=new Canvas { Width=gutter,Height=canvasHeight+(sliding?hbar:0),Background=UI.Card,ClipToBounds=true };
   var gutterScroll=new ScrollViewer { Content=hours,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; grid.Children.Add(gutterScroll);
   var canvas=new Canvas { Width=width,Height=canvasHeight,Background=UI.Card,ClipToBounds=true }; weekCanvas=canvas;
   for(int d=0;d<7;d++) {
    var date=first.AddDays(d); if(date==now.Date) { var shade=new Rectangle { Width=column,Height=rowHeight*24,Fill=UI.Hero }; Canvas.SetLeft(shade,d*column); canvas.Children.Add(shade); weekParts[d].Add(shade); }
    canvas.Children.Add(new Line { X1=d*column,X2=d*column,Y1=0,Y2=rowHeight*24,Stroke=UI.Line,StrokeThickness=0.5 });
   }
   for(int hour=0;hour<=24;hour++) {
    var label=UI.Label(UI.HourLabel(hour),10,UI.Muted); label.Margin=new Thickness(0); Canvas.SetTop(label,hour*rowHeight+2); hours.Children.Add(label);
    canvas.Children.Add(new Line { X1=0,X2=width,Y1=hour*rowHeight,Y2=hour*rowHeight,Stroke=UI.Line,StrokeThickness=0.5 });
   }
   for(int d=0;d<7;d++) foreach(var block in Schedule.Layout(State,first.AddDays(d))) {
    var o=block.Occurrence; bool done=State.Completed.Contains(o.Key),current=o.Start<=now&&o.End>now;
    double laneWidth=(column-4)/block.Lanes, height=Math.Max(5,(block.EndHour-block.StartHour)*rowHeight-2);
    var text=new StackPanel(); var title=UI.Label((done?"✓ ":"")+o.Activity.Title,block.Lanes>1?9:11,UI.Ink(o.Activity.Color)); title.Margin=new Thickness(0); title.FontWeight=FontWeights.SemiBold; title.MaxHeight=height<37?height-2:Math.Max(15,height-20); title.TextTrimming=TextTrimming.CharacterEllipsis; text.Children.Add(title);
    if(height>=39) { var time=UI.Label(UI.Clock(o.Start)+"–"+UI.Clock(o.End),9,UI.Ink(o.Activity.Color)); time.Margin=new Thickness(0); text.Children.Add(time); }
    var card=new Border { Child=text,Width=Math.Max(8,laneWidth-2),Height=height,Padding=new Thickness(4,height<25?1:3,3,1),Background=UI.B(o.Activity.Color),CornerRadius=new CornerRadius(5),BorderBrush=current?UI.Text:UI.B(o.Activity.Color),BorderThickness=new Thickness(current?2:0),Opacity=done?0.58:0.95,ClipToBounds=true,Cursor=Cursors.Hand };
    card.ToolTip=o.Activity.Title+"\n"+o.Start.ToString("ddd ",UI.Culture)+UI.Clock(o.Start)+"–"+o.End.ToString("ddd ",UI.Culture)+UI.Clock(o.End)+(string.IsNullOrWhiteSpace(o.Activity.Notes)?"":"\n"+o.Activity.Notes)+"\n"+UI.T("Edit activity");
    card.MouseLeftButtonUp+=(s,e)=> { e.Handled=true; Edit(o.Activity); }; double cardLeft=d*column+3+block.Lane*laneWidth,cardTop=block.StartHour*rowHeight+1;
    if(current && !done) {
     // Accent ring drawn just outside the card so it never covers the title. In "line" mode it stays invisible until a Right now pulse.
     if(State.WeekHighlight!="line") card.BorderThickness=new Thickness(0);
     var halo=new Border { Width=card.Width+6,Height=height+6,CornerRadius=new CornerRadius(7),BorderBrush=UI.Accent,BorderThickness=new Thickness(2.5),IsHitTestVisible=false,Opacity=State.WeekHighlight=="line"?0:1 };
     if(State.WeekHighlight=="glow") {
      var glow=new System.Windows.Media.Effects.DropShadowEffect { Color=((SolidColorBrush)UI.Accent).Color,ShadowDepth=0,BlurRadius=16,Opacity=.95 }; halo.Effect=glow;
      glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(.25,1,TimeSpan.FromMilliseconds(1100)) { AutoReverse=true,RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever,EasingFunction=new System.Windows.Media.Animation.SineEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut } });
     }
     Canvas.SetLeft(halo,cardLeft-3); Canvas.SetTop(halo,cardTop-3); Panel.SetZIndex(halo,11); Panel.SetZIndex(card,12); canvas.Children.Add(halo); weekParts[d].Add(halo);
     if(first.AddDays(d)==now.Date && weekNowTarget==null) { weekNowTarget=halo; weekNowCard=card; }
    }
    Canvas.SetLeft(card,cardLeft); Canvas.SetTop(card,cardTop); canvas.Children.Add(card); weekParts[d].Add(card); weekCardSet.Add(card);
   }
   if(now.Date>=first && now.Date<first.AddDays(7)) {
    double y=now.TimeOfDay.TotalHours*rowHeight, x=(int)now.DayOfWeek*column;
    canvas.Children.Add(new Line { X1=0,X2=width,Y1=y,Y2=y,Stroke=UI.Accent,StrokeThickness=1.5,IsHitTestVisible=false });
    var dot=new Ellipse { Width=8,Height=8,Fill=UI.Accent,IsHitTestVisible=false }; Canvas.SetLeft(dot,x-4); Canvas.SetTop(dot,y-4); canvas.Children.Add(dot); Panel.SetZIndex(dot,12); weekNowDot=dot;
    var tag=new Border { Background=UI.Accent,CornerRadius=new CornerRadius(3),Padding=new Thickness(2),Child=new TextBlock { Text=UI.Clock(now),FontSize=10,Foreground=UI.AccentInk },IsHitTestVisible=false }; Canvas.SetTop(tag,y-9); hours.Children.Add(tag);
   }
   // Empty-slot hover and click: opens the editor for that day at the hovered hour.
   var slot=new Rectangle { Width=Math.Max(4,column-2),Height=Math.Max(4,rowHeight-2),RadiusX=5,RadiusY=5,Fill=UI.Accent,Opacity=.18,IsHitTestVisible=false,Visibility=Visibility.Collapsed }; Panel.SetZIndex(slot,1); canvas.Children.Add(slot);
   Func<MouseEventArgs,bool> onGrid=e=>e.OriginalSource==canvas || e.OriginalSource is Line || (e.OriginalSource is Rectangle && e.OriginalSource!=slot);
   Func<Point,DateTime> slotAt=pt=>first.AddDays(Math.Max(0,Math.Min(6,(int)Math.Floor(pt.X/column)))).AddHours(Math.Max(0,Math.Min(23,(int)Math.Floor(pt.Y/rowHeight))));
   canvas.MouseMove+=(s,e)=> {
    var pt=e.GetPosition(canvas); if(!onGrid(e) || pt.Y>=rowHeight*24) { slot.Visibility=Visibility.Collapsed; canvas.Cursor=null; return; }
    var at=slotAt(pt); Canvas.SetLeft(slot,(at.Date-first).Days*column+1); Canvas.SetTop(slot,at.Hour*rowHeight+1); slot.Visibility=Visibility.Visible; canvas.Cursor=Cursors.Hand;
   };
   canvas.MouseLeave+=(s,e)=> { slot.Visibility=Visibility.Collapsed; canvas.Cursor=null; };
   canvas.MouseLeftButtonUp+=(s,e)=> { var pt=e.GetPosition(canvas); if(e.Handled || !onGrid(e) || pt.Y>=rowHeight*24) return; e.Handled=true; slot.Visibility=Visibility.Collapsed; Edit(null,slotAt(pt)); };
   weekScroll=new ScrollViewer { Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=sliding?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled }; Grid.SetColumn(weekScroll,1); grid.Children.Add(weekScroll);
   var bodyScroll=weekScroll;
   bodyScroll.ScrollChanged+=(s,e)=> { gutterScroll.ScrollToVerticalOffset(e.VerticalOffset); headScroll.ScrollToHorizontalOffset(e.HorizontalOffset); };
   bodyScroll.ScrollToVerticalOffset(offset);
   weekTargetOffset=0;
   if(sliding) {
    bool recenter=hOffset<0 || Math.Abs(column-weekColumn)>.01 || first!=weekFirst;
    int focus=now.Date>=first&&now.Date<first.AddDays(7)?(int)now.DayOfWeek:(int)selected.DayOfWeek;
    double target=Math.Max(0,Math.Min(width-visible*column,recenter?(focus+.5)*column-visible*column/2:hOffset)); weekTargetOffset=target;
    bodyScroll.ScrollToHorizontalOffset(target); headScroll.ScrollToHorizontalOffset(target);
    // Re-apply once layout has measured the new extent, so the offset is not clamped against the old one.
    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=> { if(weekScroll!=bodyScroll) return; bodyScroll.ScrollToHorizontalOffset(target); headScroll.ScrollToHorizontalOffset(target); }));
   }
   if(focusNow) {
    if(now.Date<first || now.Date>=first.AddDays(7)) focusNow=false;
    else {
     double startHour=now.TimeOfDay.TotalHours,endHour=startHour;
     var live=Schedule.Layout(State,now.Date).Where(b=>b.Occurrence.Start<=now && b.Occurrence.End>now && !State.Completed.Contains(b.Occurrence.Key)).OrderBy(b=>b.StartHour).FirstOrDefault();
     if(live!=null) { startHour=live.StartHour; endHour=live.EndHour; }
     double focusTop=startHour*rowHeight,focusBottom=endHour*rowHeight;
     // Runs after layout so the viewport height is known; renders replaced before then re-schedule it.
     Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=> {
      if(weekScroll!=bodyScroll || !focusNow) return; focusNow=false;
      double view=bodyScroll.ViewportHeight; if(view<1) return;
      double wanted=focusBottom-focusTop<=view-24?(focusTop+focusBottom)/2-view/2:focusTop-12;
      bodyScroll.ScrollToVerticalOffset(Math.Max(0,Math.Min(bodyScroll.ScrollableHeight,wanted)));
      if(pulseNow) { pulseNow=false; var spot=(FrameworkElement)weekNowTarget??weekNowDot; UI.Attention(spot); SpotlightWeek(canvas,hours,(FrameworkElement)weekNowCard??weekNowDot); }
     }));
    }
   }
   weekColumn=column; weekFirst=first; weekVisibleWidth=visible*column;
   if(weekEnterFocus>=0 && weekPanel.ActualWidth>100) { int enter=weekEnterFocus; weekEnterFocus=-1; AnimateWeekColumns(enter,true,weekTargetOffset,null); }
   bodyScroll.PreviewMouseWheel+=(s,e)=> {
    if((Keyboard.Modifiers&ModifierKeys.Control)!=0) { e.Handled=true; ZoomSchedule(e.Delta,e.GetPosition(bodyScroll).Y); }
    else if(sliding && (Keyboard.Modifiers&ModifierKeys.Shift)!=0) { e.Handled=true; bodyScroll.ScrollToHorizontalOffset(bodyScroll.HorizontalOffset-e.Delta*.6); }
   };
   gutterScroll.PreviewMouseWheel+=(s,e)=> { e.Handled=true; bodyScroll.ScrollToVerticalOffset(bodyScroll.VerticalOffset-e.Delta*.4); };
   headScroll.PreviewMouseWheel+=(s,e)=> { e.Handled=true; if(sliding) bodyScroll.ScrollToHorizontalOffset(bodyScroll.HorizontalOffset-e.Delta*.6); };
  }
  void UpdateTrayLanguage() {
   if(tray==null) return;
   var oldIcon=tray.Icon; tray.Icon=BrandIcon.Make(); if(oldIcon!=null) oldIcon.Dispose();
   tray.ContextMenuStrip.Items[0].Text=UI.T("Open Dayglance"); tray.ContextMenuStrip.Items[1].Text=UI.T("Add activity"); tray.ContextMenuStrip.Items[2].Text=UI.T("Quit");
  }
  readonly System.Collections.Generic.Dictionary<string,Border> dayCards=new System.Collections.Generic.Dictionary<string,Border>();
  // Builds one day-view card. style: "stripe" (slim line), "band" (colored time column) or "full" (whole card in the activity color).
  Border DayCard(Occurrence o,DateTime now,string style,bool interactive) {
   bool done=State.Completed.Contains(o.Key),current=o.Start<=now&&o.End>now&&!done,band=style=="band",full=style=="full";
   string hex=o.Activity.Color; Brush color=UI.B(hex),ink=UI.Ink(hex);
   var row=new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width=band?new GridLength(66):full?new GridLength(0):new GridLength(6) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
   if(band) {
    var times=new StackPanel { VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(4,10,4,10) };
    var startText=new TextBlock { Text=UI.Clock(o.Start),FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=ink,HorizontalAlignment=HorizontalAlignment.Center }; times.Children.Add(startText);
    times.Children.Add(new TextBlock { Text=UI.Clock(o.End),FontSize=11,Foreground=ink,Opacity=.8,HorizontalAlignment=HorizontalAlignment.Center });
    row.Children.Add(new Border { Background=color,CornerRadius=new CornerRadius(11,0,0,11),Child=times });
   } else if(!full) row.Children.Add(new Border { Background=color,CornerRadius=new CornerRadius(3),Width=3 });
   Brush titleBrush=full?ink:done?UI.Muted:UI.Text,subBrush=full?ink:UI.Muted;
   var info=new StackPanel { Margin=band?new Thickness(12,10,8,10):new Thickness(full?0:10,0,8,0),VerticalAlignment=VerticalAlignment.Center };
   var name=UI.Label(o.Activity.Title,15,titleBrush); name.FontWeight=FontWeights.SemiBold; if(done) name.TextDecorations=TextDecorations.Strikethrough; info.Children.Add(name);
   string when=band?"":UI.Clock(o.Start)+" – "+UI.Clock(o.End);
   if(o.End.Date>o.Start.Date) when+=UI.T(" (+1 day)"); if(current) when+=(when.Length>0?"  • ":"• ")+UI.T("NOW");
   when=when.Trim(); if(when.Length>0) { var whenLabel=UI.Label(when,11,subBrush); if(full) whenLabel.Opacity=.85; info.Children.Add(whenLabel); }
   if(!string.IsNullOrWhiteSpace(o.Activity.Notes)) { var notes=UI.Label(o.Activity.Notes,12,subBrush); if(full) notes.Opacity=.85; info.Children.Add(notes); }
   if(info.Children.Count>0) ((FrameworkElement)info.Children[info.Children.Count-1]).Margin=new Thickness(0);
   Grid.SetColumn(info,1); row.Children.Add(info);
   var toggle=UI.Button(done?"✓":"○",()=> { if(!interactive) return; if(State.Completed.Contains(o.Key)) State.Completed.Remove(o.Key); else State.Completed.Add(o.Key); Save(); Refresh(true); });
   toggle.ToolTip=UI.T(done?"Mark incomplete":"Mark done"); toggle.VerticalAlignment=VerticalAlignment.Center; if(band) toggle.Margin=new Thickness(0,0,10,0);
   if(full) { toggle.Background=new SolidColorBrush(Palette.IsDark(hex)?Color.FromArgb(46,255,255,255):Color.FromArgb(30,0,0,0)); toggle.Foreground=ink; }
   Grid.SetColumn(toggle,2); row.Children.Add(toggle);
   var box=UI.Box(row,full?color:current?UI.Hero:UI.Card,new Thickness(0,0,0,8)); if(band) box.Padding=new Thickness(0);
   box.BorderThickness=new Thickness(current?2:1); box.BorderBrush=current?UI.Accent:full?color:UI.Card; if(full&&done) box.Opacity=.6; // the running activity is always outlined in the accent color
   if(interactive) { box.MouseLeftButtonDown+=(s,e)=> { if(e.ClickCount==2) Edit(o.Activity); }; box.ToolTip=UI.T("Double-click to edit"); }
   return box;
  }
  // Scrolls the day list so the activity is the first visible card, or the second when the previous and next cards also fit.
  void FocusActivity(string key) { FocusActivity(key,false); }
  void FocusActivity(string key,bool pulse) {
   if(State.WeekView || State.Compact) return;
   if(selected!=DateTime.Today) { selected=DateTime.Today; Refresh(true); }
   Border card; if(!dayCards.TryGetValue(key,out card)) return;
   scroll.UpdateLayout(); var cards=list.Children.OfType<Border>().Where(c=>dayCards.ContainsValue(c)).ToList(); int index=cards.IndexOf(card); if(index<0) return;
   Func<FrameworkElement,double> top=el=>el.TranslatePoint(new Point(0,0),scroll).Y+scroll.VerticalOffset;
   Border previous=index>0?cards[index-1]:null,next=index<cards.Count-1?cards[index+1]:null,last=next??card;
   double target=top(card);
   if(previous!=null && top(last)+last.ActualHeight*dayZoom-top(previous)<=scroll.ViewportHeight+1) target=top(previous);
   scroll.ScrollToVerticalOffset(Math.Max(0,target));
   if(pulse) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=> { UI.Dim(cards.Where(c=>c!=card).Cast<FrameworkElement>(),12); UI.GlowAround(card,12); }));
  }
  // Hover target between two day cards with free time: shows "+ start – end" and opens the editor for the earliest hour of that gap.
  FrameworkElement GapRow(DateTime from,DateTime to) {
   DateTime end=from.AddHours(1)<to?from.AddHours(1):to;
   var row=new Grid { Height=24,Margin=new Thickness(0,-5,0,3),Background=Brushes.Transparent,Cursor=Cursors.Hand,ToolTip=UI.T("Add an activity in this free time") };
   var content=new Grid { Opacity=0,IsHitTestVisible=false };
   content.Children.Add(new Rectangle { Height=1.5,Fill=UI.Accent,Opacity=.55,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,14,0) });
   content.Children.Add(new Border { Background=UI.Accent,CornerRadius=new CornerRadius(11),Padding=new Thickness(10,2,12,2),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=new TextBlock { Text="+   "+UI.Clock(from)+" – "+UI.Clock(end),FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=UI.AccentInk } });
   row.Children.Add(content);
   row.MouseEnter+=(s,e)=>content.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(1,TimeSpan.FromMilliseconds(120)));
   row.MouseLeave+=(s,e)=>content.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,TimeSpan.FromMilliseconds(160)));
   row.MouseLeftButtonUp+=(s,e)=> { e.Handled=true; Edit(null,from,end); };
   return row;
  }
  // Compact "Right now" card shown next to the week range.
  void FillWeekNow(System.Collections.Generic.List<Occurrence> active,DateTime now) {
   Occurrence shown=active.Count>0?active[0]:null;
   for(int i=0;i<8 && shown==null;i++) shown=Schedule.ForDay(State,now.Date.AddDays(i)).FirstOrDefault(o=>o.Start>now&&!State.Completed.Contains(o.Key));
   if(shown==null) { weekNow.Visibility=Visibility.Collapsed; return; }
   var panel=new DockPanel(); var dot=new Ellipse { Width=9,Height=9,Fill=UI.B(shown.Activity.Color),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,9,0) }; DockPanel.SetDock(dot,Dock.Left); panel.Children.Add(dot);
   var text=new StackPanel(); panel.Children.Add(text);
   text.Children.Add(new TextBlock { Text=UI.T(active.Count>0?"RIGHT NOW":"UP NEXT"),FontSize=9,Foreground=UI.Accent });
   text.Children.Add(new TextBlock { Text=shown.Activity.Title,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=UI.Text,TextTrimming=TextTrimming.CharacterEllipsis });
   string detail=active.Count>0?UI.Clock(shown.Start)+"–"+UI.Clock(shown.End)+"  ·  "+Math.Ceiling((shown.End-now).TotalMinutes)+UI.T(" min left"):(shown.Start.Date==now.Date?"":shown.Start.ToString("ddd ",UI.Culture))+UI.Clock(shown.Start);
   text.Children.Add(new TextBlock { Text=detail,FontSize=11,Foreground=UI.Muted,TextTrimming=TextTrimming.CharacterEllipsis });
   weekNow.Child=panel;
  }
  // Dims the week grid (and hour gutter) around the pulsing target, fading in and out over the length of the attention pulse.
  void SpotlightWeek(Canvas canvas,Canvas hours,FrameworkElement target) {
   if(target==null || double.IsNaN(target.Width) || double.IsNaN(target.Height)) return;
   const double pad=0; double left=Canvas.GetLeft(target),top=Canvas.GetTop(target); if(double.IsNaN(left)||double.IsNaN(top)) return;
   var hole=new RectangleGeometry(new Rect(left-pad,top-pad,target.Width+pad*2,target.Height+pad*2),5,5);
   var veil=new System.Windows.Shapes.Path { Data=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,canvas.Width,canvas.Height)),hole),Fill=Brushes.Black,Opacity=0,IsHitTestVisible=false };
   var gutterVeil=new Rectangle { Width=hours.Width,Height=hours.Height,Fill=Brushes.Black,Opacity=0,IsHitTestVisible=false };
   // Shade sits above the grid and other cards (z 10) but below the current card's glow ring (11) and the card itself (12).
   Panel.SetZIndex(veil,10); Panel.SetZIndex(gutterVeil,10); canvas.Children.Add(veil); hours.Children.Add(gutterVeil);
   foreach(var shade in new FrameworkElement[]{veil,gutterVeil}) {
    var element=shade; var ease=new System.Windows.Media.Animation.SineEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut };
    var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(.5,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450)),ease));
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(.5,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000))));
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2700)),ease));
    fade.Completed+=(s,e)=> { var parent=element.Parent as Panel; if(parent!=null) parent.Children.Remove(element); };
    element.BeginAnimation(UIElement.OpacityProperty,fade);
   }
  }
  void FocusWeekNow() {
   if(!State.WeekView || State.Compact) return;
   var now=DateTime.Now; var first=Schedule.WeekStart(selected); if(now.Date<first || now.Date>=first.AddDays(7)) selected=now.Date;
   focusNow=true; pulseNow=true; weekColumn=-1; Refresh(true);
  }
  // Back/forward history for view and day jumps (week headings, Day/Week switch, Today), driven by mouse X buttons and Alt+Left/Right. Arrow stepping is not recorded.
  struct NavState { public bool Week; public DateTime Day; }
  readonly System.Collections.Generic.List<NavState> backStack=new System.Collections.Generic.List<NavState>(),forwardStack=new System.Collections.Generic.List<NavState>();
  bool Navigate(bool week,DateTime day) {
   if(week==State.WeekView && day.Date==selected.Date) return false;
   backStack.Add(new NavState { Week=State.WeekView,Day=selected }); if(backStack.Count>50) backStack.RemoveAt(0); forwardStack.Clear();
   Go(week,day); return true;
  }
  void Go(bool week,DateTime day) { selected=day.Date; if(week!=State.WeekView) SetView(week); else Refresh(true); }
  public void GoBack() { if(backStack.Count==0) return; var target=backStack[backStack.Count-1]; backStack.RemoveAt(backStack.Count-1); forwardStack.Add(new NavState { Week=State.WeekView,Day=selected }); Go(target.Week,target.Day); }
  public void GoForward() { if(forwardStack.Count==0) return; var target=forwardStack[forwardStack.Count-1]; forwardStack.RemoveAt(forwardStack.Count-1); backStack.Add(new NavState { Week=State.WeekView,Day=selected }); Go(target.Week,target.Day); }
  void EnableHistoryInput() {
   PreviewMouseDown+=(s,e)=> { if(e.ChangedButton==MouseButton.XButton1) { e.Handled=true; GoBack(); } else if(e.ChangedButton==MouseButton.XButton2) { e.Handled=true; GoForward(); } };
   PreviewKeyDown+=(s,e)=> { if((Keyboard.Modifiers&ModifierKeys.Alt)!=0 && e.Key==Key.System) { if(e.SystemKey==Key.Left) { e.Handled=true; GoBack(); } else if(e.SystemKey==Key.Right) { e.Handled=true; GoForward(); } } };
  }
  // A modal dialog disables its owner, so a click on the widget arrives as WM_SETCURSOR with HTERROR. Use it to light-dismiss the active dialog.
  void EnableLightDismiss() {
   SourceInitialized+=(s,e)=> {
    var source=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle); if(source==null) return;
    source.AddHook((IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)=> {
     if(msg!=0x0020) return IntPtr.Zero;
     long value=lParam.ToInt64(); int hit=(short)(value&0xFFFF),mouse=(int)((value>>16)&0xFFFF);
     if(hit!=-2 || (mouse!=0x0201 && mouse!=0x0204)) return IntPtr.Zero;
     var dialog=OwnedWindows.OfType<DialogWindow>().LastOrDefault(d=>d.LightDismiss && d.IsVisible && d.IsActive && d.OwnedWindows.Count==0);
     if(dialog==null) return IntPtr.Zero;
     handled=true; Dispatcher.BeginInvoke(new Action(()=>dialog.Close())); return IntPtr.Zero;
    });
   };
  }
  // Small week-grid sample for the "current activity in week view" setting.
  FrameworkElement WeekHighlightPreview(string mode) {
   const double width=312,height=116,column=104,row=29;
   var canvas=new Canvas { Width=width,Height=height,Background=UI.Card,ClipToBounds=true };
   var shade=new Rectangle { Width=column,Height=height,Fill=UI.Hero }; Canvas.SetLeft(shade,column); canvas.Children.Add(shade);
   for(int c=1;c<3;c++) canvas.Children.Add(new Line { X1=c*column,X2=c*column,Y1=0,Y2=height,Stroke=UI.Line,StrokeThickness=.5 });
   for(int r=0;r<5;r++) canvas.Children.Add(new Line { X1=0,X2=width,Y1=r*row,Y2=r*row,Stroke=UI.Line,StrokeThickness=.5 });
   Func<string,string,double,double,double,Border> block=(title,hex,x,y,h)=> {
    var text=new TextBlock { Text=title,FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=UI.Ink(hex),TextTrimming=TextTrimming.CharacterEllipsis };
    var card=new Border { Child=text,Width=column-8,Height=h,Padding=new Thickness(5,3,4,1),Background=UI.B(hex),CornerRadius=new CornerRadius(5),Opacity=.95 };
    Canvas.SetLeft(card,x+4); Canvas.SetTop(card,y); Panel.SetZIndex(card,5); canvas.Children.Add(card); return card;
   };
   block("","#9CCBFF",0,row*.5,row*1.6); block("","#FFD18F",column*2,row*2.3,row*1.5);
   string accentHex=Palette.Hex(((SolidColorBrush)UI.Accent).Color);
   var current=block(UI.T("Sample activity"),"#B9AAFF",column,row*1.1,row*2);
   if(mode=="line") { current.BorderBrush=UI.Text; current.BorderThickness=new Thickness(2); }
   else {
    var halo=new Border { Width=current.Width+6,Height=current.Height+6,CornerRadius=new CornerRadius(7),BorderBrush=UI.Accent,BorderThickness=new Thickness(2.5),IsHitTestVisible=false };
    if(mode=="glow") {
     var glow=new System.Windows.Media.Effects.DropShadowEffect { Color=((SolidColorBrush)UI.Accent).Color,ShadowDepth=0,BlurRadius=16,Opacity=.95 }; halo.Effect=glow;
     glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(.25,1,TimeSpan.FromMilliseconds(1100)) { AutoReverse=true,RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever });
    }
    Canvas.SetLeft(halo,column+1); Canvas.SetTop(halo,row*1.1-3); Panel.SetZIndex(halo,4); canvas.Children.Add(halo);
   }
   double y=row*2.4; canvas.Children.Add(new Line { X1=0,X2=width,Y1=y,Y2=y,Stroke=UI.Accent,StrokeThickness=1.5 });
   var dot=new Ellipse { Width=8,Height=8,Fill=UI.Accent }; Canvas.SetLeft(dot,column-4); Canvas.SetTop(dot,y-4); Panel.SetZIndex(dot,6); canvas.Children.Add(dot);
   return new Border { Child=canvas,CornerRadius=new CornerRadius(10),BorderBrush=UI.Line,BorderThickness=new Thickness(1),Margin=new Thickness(0,8,0,4),HorizontalAlignment=HorizontalAlignment.Left,ClipToBounds=true };
  }
  bool checkingUpdates; string announcedUpdate;
  // Background GitHub check. Automatic checks (every few hours, if enabled) announce a newer release once per session with a small card.
  void CheckForUpdates(bool manual,Action<UpdateInfo,Exception> done) {
   if(preview || checkingUpdates || (!manual && !State.AutoUpdateCheck)) return;
   checkingUpdates=true;
   System.Threading.ThreadPool.QueueUserWorkItem(_=> {
    UpdateInfo info=null; Exception error=null;
    try { info=Updater.FetchLatest(); } catch(Exception ex) { error=ex; }
    Dispatcher.BeginInvoke(new Action(()=> {
     checkingUpdates=false;
     if(done!=null) { done(info,error); return; }
     if(error==null && Updater.IsNewer(info) && announcedUpdate!=info.Tag) { announcedUpdate=info.Tag; new ReminderToast(UI.T("Update available"),"Dayglance "+info.Version+"  ·  "+UI.T("Click to install"),()=>OfferUpdate(info),false); }
    }));
   });
  }
  void OfferUpdate(UpdateInfo info) {
   Restore();
   string question=UI.Language=="es"?"¿Instalar Dayglance "+info.Version+" ahora? Dayglance se cerrará, se actualizará y volverá a abrirse. Tu horario y tus ajustes se conservan.":"Install Dayglance "+info.Version+" now? Dayglance will close, update and reopen. Your schedule and settings are kept.";
   if(MessageBox.Show(this,question,"Dayglance",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes) InstallUpdate(info);
  }
  void InstallUpdate(UpdateInfo info) {
   Mouse.OverrideCursor=Cursors.Wait;
   System.Threading.ThreadPool.QueueUserWorkItem(_=> {
    Exception error=null; try { Updater.Install(info); } catch(Exception ex) { error=ex; }
    Dispatcher.BeginInvoke(new Action(()=> {
     Mouse.OverrideCursor=null;
     if(error!=null) { MessageBox.Show(this,UI.T("The update could not be installed.")+"\n\n"+error.Message,"Dayglance",MessageBoxButton.OK,MessageBoxImage.Error); return; }
     PersistPosition(); exiting=true; Close(); Application.Current.Shutdown();
    }));
   });
  }
  bool settingsOpen;
  // Settings preview their theme and language on the dialog itself; the main widget only changes after saving.
  void Settings() {
   var w=UI.Dialog(this,"Dayglance settings",520,700); w.LightDismiss=true; var p=new StackPanel { Margin=new Thickness(24,8,24,12) }; var scroller=new ScrollViewer { Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
   var settingsShell=new DockPanel(); var tabBar=new Border { Margin=new Thickness(24,4,24,6) }; DockPanel.SetDock(tabBar,Dock.Top); settingsShell.Children.Add(tabBar);
   var footer=new Border { Padding=new Thickness(24,12,24,20),BorderThickness=new Thickness(0,1,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); settingsShell.Children.Add(footer); settingsShell.Children.Add(scroller); w.Content=settingsShell;
   string themeId=State.Theme,language=State.Language,cardStyle=State.CardStyle,weekHighlight=State.WeekHighlight,tab="appearance",timeFormat=State.TimeFormat; double scale=State.UiScale; bool autoUpdate=State.AutoUpdateCheck,notifications=State.Notifications,sound=State.Sound,startup=File.Exists(StartupPath),saved=false;
   Choice languageChoice=null; Action render=null;
   Action applyPending=()=> {
    UI.Apply(new State { Theme=themeId,Language=language,CustomThemes=State.CustomThemes,UiScale=scale,TimeFormat=timeFormat });
    w.Title=UI.T("Dayglance settings"); w.Width=520*UI.Scale; w.Restyle(); render();
   };
   Func<string,UIElement> section=text=> { var label=UI.Label(text,11,UI.Muted); label.Margin=new Thickness(0,18,0,6); return label; };
   render=()=> {
    double y=scroller.VerticalOffset; p.Children.Clear();
    // Tabs keep each group short: look & language, schedule display, reminders & startup, data.
    var tabs=UI.Row(); string[] keys={"appearance","reminders","data"}; string[] names={"Appearance","Reminders","Data & updates"};
    for(int i=0;i<keys.Length;i++) { string key=keys[i]; var b=UI.Button(names[i],()=> { if(tab==key) return; tab=key; render(); scroller.ScrollToVerticalOffset(0); },tab==key); b.Margin=new Thickness(0); b.MinHeight=30; if(tab!=key) b.Background=Brushes.Transparent; tabs.Children.Add(b); }
    tabBar.Child=new Border { Child=tabs,Background=UI.Card,CornerRadius=new CornerRadius(10),Padding=new Thickness(3),HorizontalAlignment=HorizontalAlignment.Left };
    if(tab=="appearance") {
     var heading=section("THEME"); ((FrameworkElement)heading).Margin=new Thickness(0,8,0,6); p.Children.Add(heading);
     var themeGrid=new UniformGrid { Columns=2,Margin=new Thickness(0,2,0,8) };
     foreach(var theme in UI.AvailableThemes(State)) {
      var info=new StackPanel(); var name=UI.Label(theme.Name,13,UI.B(theme.Foreground)); name.Margin=new Thickness(0,0,0,9); info.Children.Add(name);
      var swatches=UI.Row(); foreach(string hex in new[]{theme.Background,theme.Surface,theme.Foreground,theme.Accent}) swatches.Children.Add(new Border { Background=UI.B(hex),Width=24,Height=13,CornerRadius=new CornerRadius(4),Margin=new Thickness(0,0,5,0),BorderBrush=UI.B(theme.Line),BorderThickness=new Thickness(1) }); info.Children.Add(swatches);
      bool chosen=theme.Id==themeId;
      var tile=new Border { Child=info,Background=UI.B(theme.Surface),Padding=new Thickness(10),Margin=new Thickness(0,0,8,8),CornerRadius=new CornerRadius(10),BorderBrush=chosen?UI.Accent:UI.Line,BorderThickness=new Thickness(chosen?3:1) };
      var button=new Button { Content=tile,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,Cursor=Cursors.Hand,ToolTip=UI.T(theme.Name) };
      var template=new ControlTemplate(typeof(Button)); template.VisualTree=new FrameworkElementFactory(typeof(ContentPresenter)); button.Template=template;
      string id=theme.Id; button.Click+=(s,e)=> { themeId=id; applyPending(); }; themeGrid.Children.Add(button);
     }
     p.Children.Add(themeGrid);
     var themeActions=new WrapPanel(); themeActions.Children.Add(UI.Button("Create theme…",()=>CreateTheme(w,null,theme=> { State.CustomThemes.Add(theme); if(!Save()) { State.CustomThemes.Remove(theme); return; } themeId=theme.Id; applyPending(); })));
     var custom=State.CustomThemes.FirstOrDefault(t=>t.Id==themeId);
     if(custom!=null) {
      themeActions.Children.Add(UI.Button("Edit theme…",()=>CreateTheme(w,custom,updated=> { int index=State.CustomThemes.IndexOf(custom); if(index<0) return; State.CustomThemes[index]=updated; if(!Save()) { State.CustomThemes[index]=custom; return; } applyPending(); })));
      themeActions.Children.Add(UI.Button("Delete theme",()=> {
       if(MessageBox.Show(w,UI.T("Delete this theme?")+"\n\n"+custom.Name,UI.T("Delete theme"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
       int index=State.CustomThemes.IndexOf(custom); string oldTheme=State.Theme; State.CustomThemes.Remove(custom); if(State.Theme==custom.Id) State.Theme="midnight";
       if(!Save()) { State.CustomThemes.Insert(Math.Max(0,index),custom); State.Theme=oldTheme; return; }
       themeId=State.Theme==custom.Id||themeId==custom.Id?"midnight":themeId; applyPending();
      }));
     }
     foreach(UIElement child in themeActions.Children) ((FrameworkElement)child).Margin=new Thickness(0,0,6,6);
     p.Children.Add(themeActions);
     p.Children.Add(section("TEXT SIZE"));
     double[] scales={.9,1,1.15,1.3}; var sizeChoice=new Choice(); sizeChoice.Items.AddRange(new[]{"Small","Default","Large","Extra large"});
     int sizeIndex=0; for(int i=0;i<scales.Length;i++) if(Math.Abs(scales[i]-scale)<Math.Abs(scales[sizeIndex]-scale)) sizeIndex=i; sizeChoice.SelectedIndex=sizeIndex;
     sizeChoice.Changed+=()=> { scale=scales[sizeChoice.SelectedIndex]; applyPending(); }; p.Children.Add(sizeChoice);
     p.Children.Add(section("LANGUAGE"));
     languageChoice=new Choice(); languageChoice.Items.Add("English"); languageChoice.Items.Add("Español"); languageChoice.SelectedIndex=language=="es"?1:0; languageChoice.Changed+=()=> { language=languageChoice.SelectedIndex==1?"es":"en"; applyPending(); }; p.Children.Add(languageChoice);
     p.Children.Add(section("TIME FORMAT"));
     var formatChoice=new Choice(); formatChoice.Items.AddRange(new[]{"24-hour","12-hour (AM/PM)"}); formatChoice.SelectedIndex=timeFormat=="12"?1:0; formatChoice.Changed+=()=> { timeFormat=formatChoice.SelectedIndex==1?"12":"24"; applyPending(); }; p.Children.Add(formatChoice);
     p.Children.Add(section("ACTIVITY CARDS"));
     string[] styles={"stripe","band","full"}; var cardChoice=new Choice(); cardChoice.Items.AddRange(new[]{"Slim color line","Color band","Full color card"}); cardChoice.SelectedIndex=Math.Max(0,Array.IndexOf(styles,cardStyle)); cardChoice.Changed+=()=> { cardStyle=styles[cardChoice.SelectedIndex]; render(); }; p.Children.Add(cardChoice);
     var sampleActivity=new Activity { Id="sample",Title=UI.T("Sample activity"),Color=Palette.Hex(((SolidColorBrush)UI.Accent).Color),Start="09:00",End="10:30",Days=new int[0],Notes=UI.T("Double-click to edit") };
     var sample=DayCard(new Occurrence { Activity=sampleActivity,Start=DateTime.Today.AddHours(9),End=DateTime.Today.AddHours(10.5) },DateTime.Today.AddHours(9.5),cardStyle,false); sample.Margin=new Thickness(0,0,0,4); p.Children.Add(sample);
     p.Children.Add(section("CURRENT ACTIVITY IN WEEK VIEW"));
     string[] highlights={"line","outline","glow"}; var highlightChoice=new Choice(); highlightChoice.Items.AddRange(new[]{"Time line and dot","Accent outline","Accent outline with glow"}); highlightChoice.SelectedIndex=Math.Max(0,Array.IndexOf(highlights,weekHighlight)); highlightChoice.Changed+=()=> { weekHighlight=highlights[highlightChoice.SelectedIndex]; render(); }; p.Children.Add(highlightChoice); p.Children.Add(WeekHighlightPreview(weekHighlight));
    }
    else if(tab=="reminders") {
     var remindersTitle=section("REMINDERS"); ((FrameworkElement)remindersTitle).Margin=new Thickness(0,8,0,6); p.Children.Add(remindersTitle);
     var notificationSwitch=UI.Switch("Enable activity reminders",notifications); notificationSwitch.Checked+=(s,e)=>notifications=true; notificationSwitch.Unchecked+=(s,e)=>notifications=false;
     var soundSwitch=UI.Switch("Play a sound with reminders",sound); soundSwitch.Checked+=(s,e)=>sound=true; soundSwitch.Unchecked+=(s,e)=>sound=false;
     p.Children.Add(notificationSwitch); p.Children.Add(soundSwitch);
     p.Children.Add(UI.Label(UI.Language=="es"?"Los avisos propios de Dayglance usan tu tema y un sonido suave. Funcionan mientras la app esté abierta; se cierran tras 18 segundos. × oculta el widget en la bandeja.":"Dayglance reminders use your theme and a soft chime. They work while the app is running and dismiss after 18 seconds. × hides the widget in the tray.",12,UI.Muted));
     var test=UI.Button("Test reminder",()=>new ReminderToast(UI.T("Notification preview"),UI.T("Your reminder will look like this."),Restore,sound)); test.HorizontalAlignment=HorizontalAlignment.Left; p.Children.Add(test);
     p.Children.Add(section("STARTUP"));
     var startupSwitch=UI.Switch("Start when I sign in to Windows",startup); startupSwitch.Checked+=(s,e)=>startup=true; startupSwitch.Unchecked+=(s,e)=>startup=false; p.Children.Add(startupSwitch);
    }
    else {
     var backup=section("BACKUP & SHARING"); ((FrameworkElement)backup).Margin=new Thickness(0,8,0,6); p.Children.Add(backup);
     p.Children.Add(UI.Label("Export your schedule and completion history. Import replaces the current schedule; a backup is saved first.",12,UI.Muted));
     var row=UI.Row(); row.Children.Add(UI.Button("Export…",()=> { var d=new Microsoft.Win32.SaveFileDialog { Filter="Dayglance JSON (*.json)|*.json",FileName="Dayglance-backup.json" }; if(d.ShowDialog(w)==true) try { File.WriteAllText(d.FileName,Storage.Serializer().Serialize(State)); } catch(Exception ex) { MessageBox.Show(w,UI.T(ex.Message)); } }));
     row.Children.Add(UI.Button("Import…",()=> {
      var d=new Microsoft.Win32.OpenFileDialog { Filter="Dayglance JSON (*.json)|*.json" }; if(d.ShowDialog(w)!=true) return;
      try {
       var incoming=Storage.Read(d.FileName);
       string question=UI.Language=="es"?"¿Reemplazar tu horario con "+incoming.Activities.Count+" actividades? Se guardará un respaldo.":"Replace your schedule with "+incoming.Activities.Count+" imported activities? A backup will be saved.";
       if(MessageBox.Show(w,question,UI.T("Import schedule"),MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
       Schedule.MigrateReminders(incoming); ImportState(incoming); saved=true; w.Close();
      } catch(Exception ex) { MessageBox.Show(w,UI.T(ex.Message),UI.T("Import failed")); }
     })); p.Children.Add(row);
     p.Children.Add(section("UPDATES"));
     p.Children.Add(UI.Label((UI.Language=="es"?"Versión instalada: ":"Installed version: ")+AppInfo.Version,13,UI.Text));
     var autoSwitch=UI.Switch("Check for updates automatically",autoUpdate); autoSwitch.Checked+=(s,e)=>autoUpdate=true; autoSwitch.Unchecked+=(s,e)=>autoUpdate=false; p.Children.Add(autoSwitch);
     var updateStatus=UI.Label("",12,UI.Muted); var updateActions=new WrapPanel();
     Button checkButton=null; checkButton=UI.Button("Check now",()=> {
      checkButton.IsEnabled=false; updateStatus.Text=UI.T("Checking for updates…"); updateActions.Children.Clear();
      CheckForUpdates(true,(info,error)=> {
       checkButton.IsEnabled=true;
       if(error!=null) { updateStatus.Text=UI.T("Couldn't check for updates.")+" "+error.Message; return; }
       if(!Updater.IsNewer(info)) { updateStatus.Text=UI.T("You're up to date."); return; }
       updateStatus.Text=(UI.Language=="es"?"Dayglance "+info.Version+" está disponible.":"Dayglance "+info.Version+" is available.");
       var install=UI.Button("Install update",()=> { w.Close(); InstallUpdate(info); },true); install.Margin=new Thickness(0,0,6,6); updateActions.Children.Add(install);
       if(!string.IsNullOrEmpty(info.PageUrl)) { var notes=UI.Button("Release notes",()=> { try { Process.Start(info.PageUrl); } catch {} }); notes.Margin=new Thickness(0,0,6,6); updateActions.Children.Add(notes); }
      });
     });
     checkButton.HorizontalAlignment=HorizontalAlignment.Left; checkButton.Margin=new Thickness(0,0,0,6); p.Children.Add(checkButton); p.Children.Add(updateStatus); p.Children.Add(updateActions);
     p.Children.Add(section("STAYS ON THIS PC")); p.Children.Add(UI.Label(Storage.FilePath+"\n"+UI.T("No account, subscriptions or analytics. The only network request is the optional update check on GitHub; your schedule stays on this PC."),12,UI.Muted));
    }
    var hint=UI.Label(UI.Language=="es"?"Los cambios se muestran en esta ventana y se aplican al widget al guardar.":"Changes preview in this window and apply to the widget when you save.",11,UI.Muted); hint.Margin=new Thickness(0,0,0,10); hint.TextAlignment=TextAlignment.Center;
    var saveButton=UI.Button("Save preferences",()=> {
     try {
      if(!preview) {
       if(startup) { Type t=Type.GetTypeFromProgID("WScript.Shell"); dynamic shell=Activator.CreateInstance(t); dynamic shortcut=shell.CreateShortcut(StartupPath); shortcut.TargetPath=System.Reflection.Assembly.GetExecutingAssembly().Location; shortcut.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory; shortcut.Description="Dayglance"; shortcut.Save(); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
       else if(File.Exists(StartupPath)) File.Delete(StartupPath);
      }
      if(tab=="appearance" && languageChoice!=null) language=languageChoice.SelectedIndex==1?"es":"en";
      string oldTheme=State.Theme,oldLanguage=State.Language,oldCards=State.CardStyle,oldHighlight=State.WeekHighlight; bool oldNotifications=State.Notifications,oldSound=State.Sound; double oldScale=State.UiScale; string oldFormat=State.TimeFormat;
      State.Notifications=notifications; State.Sound=sound; State.Theme=themeId; State.CardStyle=cardStyle; State.WeekHighlight=weekHighlight; State.Language=language; State.UiScale=scale; State.TimeFormat=timeFormat; State.AutoUpdateCheck=autoUpdate;
      if(Save()) { saved=true; w.Close(); } else { State.Theme=oldTheme; State.Language=oldLanguage; State.Notifications=oldNotifications; State.Sound=oldSound; State.CardStyle=oldCards; State.WeekHighlight=oldHighlight; State.UiScale=oldScale; State.TimeFormat=oldFormat; }
     } catch(Exception ex) { MessageBox.Show(w,UI.T(ex.Message),UI.T("Could not save preferences")); }
    },true); saveButton.HorizontalAlignment=HorizontalAlignment.Stretch; saveButton.MinHeight=42; saveButton.Margin=new Thickness(0);
    var footerPanel=new StackPanel(); footerPanel.Children.Add(hint); footerPanel.Children.Add(saveButton); footer.BorderBrush=UI.Line; footer.Child=footerPanel;
    scroller.UpdateLayout(); scroller.ScrollToVerticalOffset(y);
   };
   render();
   w.Closed+=(s,e)=> { settingsOpen=false; UI.Apply(State); if(saved) { SetSize(); BuildView(); UpdateTrayLanguage(); } };
   settingsOpen=true; w.ShowDialog();
  }
  public void ImportState(State incoming) {
   Schedule.Validate(incoming); Directory.CreateDirectory(Storage.Folder);
   File.WriteAllText(System.IO.Path.Combine(Storage.Folder,"before-import-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json"),Storage.Serializer().Serialize(State));
   incoming.Pinned=State.Pinned; incoming.Compact=State.Compact; incoming.Left=State.Left; incoming.Top=State.Top; incoming.MiniLeft=State.MiniLeft; incoming.MiniTop=State.MiniTop; incoming.MiniWidth=State.MiniWidth; incoming.MiniHeight=State.MiniHeight; incoming.UiScale=State.UiScale; incoming.TimeFormat=State.TimeFormat; incoming.AutoUpdateCheck=State.AutoUpdateCheck; incoming.Notifications=State.Notifications; incoming.Sound=State.Sound; incoming.Theme=State.Theme; incoming.Language=State.Language; incoming.WeekView=State.WeekView; incoming.CardStyle=State.CardStyle; incoming.WindowWidth=State.WindowWidth; incoming.WindowHeight=State.WindowHeight; incoming.WeekHighlight=State.WeekHighlight; incoming.CustomThemes=State.CustomThemes.Concat(incoming.CustomThemes).GroupBy(t=>t.Id).Select(g=>g.First()).ToList();
   var previous=State; State=incoming; try { Storage.Save(State); } catch { State=previous; throw; }
  }
 }
}
