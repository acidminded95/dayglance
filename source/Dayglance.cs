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
  // Borderless icon button (Segoe Fluent Icons / MDL2 glyphs) used for the compact widget chrome.
  public static Button Icon(string glyph,string tooltip,Action action,bool accent=false) {
   Brush normal=accent?Accent:Brushes.Transparent,hover=accent?Accent:Card;
   var b=new Button { Content=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=14,Width=32,Height=32,Background=normal,Foreground=accent?AccentInk:Text,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Margin=new Thickness(2,0,0,0),ToolTip=T(tooltip) };
   var template=new ControlTemplate(typeof(Button)); var border=new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8)); border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
   var content=new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center); border.AppendChild(content); template.VisualTree=border; b.Template=template;
   if(accent) { b.MouseEnter+=(s,e)=>b.Opacity=.85; b.MouseLeave+=(s,e)=>b.Opacity=1; } else { b.MouseEnter+=(s,e)=>b.Background=hover; b.MouseLeave+=(s,e)=>b.Background=normal; }
   b.Click+=(s,e)=>action(); return b;
  }
  public static Border Box(UIElement child,Brush background,Thickness margin) { return new Border { Child=child,Background=background,CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Margin=margin }; }
  public static StackPanel Row() { return new StackPanel { Orientation=Orientation.Horizontal }; }
  public static TextBox Input(string value) { return new TextBox { Text=value??"",FontSize=14,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12),Background=Card,Foreground=Text,BorderBrush=Muted,CaretBrush=Text }; }
  public static CheckBox Check(string text,bool value) { return Switch(text,value); }
  // Frameless, transparent windows get no native resize border; answer WM_NCHITTEST so every edge and corner resizes.
  public static void EnableBorderResize(Window window) {
   window.SourceInitialized+=(s,e)=> {
    var source=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(window).Handle); if(source==null) return;
    source.AddHook((IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)=> {
     if(msg!=0x0084 || window.ResizeMode==ResizeMode.NoResize || window.WindowState!=WindowState.Normal) return IntPtr.Zero;
     long value=lParam.ToInt64(); var point=window.PointFromScreen(new Point((short)(value&0xFFFF),(short)((value>>16)&0xFFFF)));
     const double edge=7; bool left=point.X<edge,right=point.X>=window.ActualWidth-edge,top=point.Y<edge,bottom=point.Y>=window.ActualHeight-edge;
     int hit=top&&left?13:top&&right?14:bottom&&left?16:bottom&&right?17:left?10:right?11:top?12:bottom?15:0;
     if(hit==0) return IntPtr.Zero; handled=true; return new IntPtr(hit);
    });
   };
  }
  // Short accent glow pulse used to draw the eye to an element (3 beats, then restores the element's own effect/opacity).
  public static void Attention(UIElement element) {
   if(element==null) return; var original=element.Effect;
   var glow=new System.Windows.Media.Effects.DropShadowEffect { Color=((SolidColorBrush)Accent).Color,ShadowDepth=0,BlurRadius=0,Opacity=1 }; element.Effect=glow;
   var blur=new System.Windows.Media.Animation.DoubleAnimation(0,28,TimeSpan.FromMilliseconds(420)) { AutoReverse=true,RepeatBehavior=new System.Windows.Media.Animation.RepeatBehavior(3),EasingFunction=new System.Windows.Media.Animation.SineEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut } };
   blur.Completed+=(s,e)=> { if(element.Effect==glow) element.Effect=original; };
   element.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(1,1,TimeSpan.FromMilliseconds(2520)) { FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop });
   glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty,blur);
  }
  // Glow ring drawn in the adorner layer around an element, so the element itself (and its text) is never rendered through a blur effect.
  public static void GlowAround(FrameworkElement element,double cornerRadius) {
   if(element==null) return; var layer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(element); if(layer==null) { Attention(element); return; }
   var adorner=new OuterGlowAdorner(element,cornerRadius); layer.Add(adorner);
   var glow=new System.Windows.Media.Effects.DropShadowEffect { Color=((SolidColorBrush)Accent).Color,ShadowDepth=0,BlurRadius=0,Opacity=1 }; adorner.Ring.Effect=glow;
   var ease=new System.Windows.Media.Animation.SineEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut };
   var blur=new System.Windows.Media.Animation.DoubleAnimation(0,28,TimeSpan.FromMilliseconds(420)) { AutoReverse=true,RepeatBehavior=new System.Windows.Media.Animation.RepeatBehavior(3),EasingFunction=ease };
   var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
   fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
   fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)),ease));
   fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2200))));
   fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2520)),ease));
   fade.Completed+=(s,e)=> { try { layer.Remove(adorner); } catch {} };
   adorner.Ring.BeginAnimation(UIElement.OpacityProperty,fade); glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty,blur);
  }
  // Darkens elements with a black veil in the adorner layer for the length of an attention pulse, fading in and out.
  public static void Dim(IEnumerable<FrameworkElement> elements,double cornerRadius) {
   foreach(var element in elements) {
    var layer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(element); if(layer==null) continue;
    var veil=new Border { Background=Brushes.Black,CornerRadius=new CornerRadius(cornerRadius),IsHitTestVisible=false,Opacity=0 }; var adorner=new GlowAdorner(element,veil); layer.Add(adorner);
    var ease=new System.Windows.Media.Animation.SineEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut }; var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(.55,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450)),ease));
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(.55,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2000))));
    fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0,System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2600)),ease));
    var owner=layer; var added=adorner; fade.Completed+=(s,e)=> { try { owner.Remove(added); } catch {} };
    veil.BeginAnimation(UIElement.OpacityProperty,fade);
   }
  }
  // Fades an element in while sliding it from (dx,dy) to its place.
  public static void SlideIn(FrameworkElement element,double dx,double dy) {
   if(element==null) return; var ease=new System.Windows.Media.Animation.CubicEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut }; var duration=TimeSpan.FromMilliseconds(420);
   var move=new TranslateTransform(dx,dy); element.RenderTransform=move;
   element.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,1,duration) { EasingFunction=ease });
   if(dx!=0) move.BeginAnimation(TranslateTransform.XProperty,new System.Windows.Media.Animation.DoubleAnimation(dx,0,duration) { EasingFunction=ease });
   if(dy!=0) move.BeginAnimation(TranslateTransform.YProperty,new System.Windows.Media.Animation.DoubleAnimation(dy,0,duration) { EasingFunction=ease });
  }
  public static DialogWindow Dialog(Window owner,string title,double width,double height) { return new DialogWindow { Owner=owner,Title=T(title),Width=width*Scale,Height=Math.Min(height*Scale,SystemParameters.WorkArea.Height),MinWidth=Math.Min(width*Scale,SystemParameters.WorkArea.Width),MinHeight=320,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brushes.Transparent,Foreground=Text,FontFamily=new FontFamily("Segoe UI"),ResizeMode=ResizeMode.CanResize,ShowInTaskbar=false }; }
 }
 // Glow ring whose light only shows outside the adorned element (masked), so it radiates outwards like the week view halo.
 public class OuterGlowAdorner : System.Windows.Documents.Adorner {
  const double Pad=36; readonly Grid host; readonly Border ring; readonly double radius;
  public Border Ring { get { return ring; } }
  public OuterGlowAdorner(UIElement adorned,double cornerRadius) : base(adorned) {
   IsHitTestVisible=false; radius=cornerRadius; host=new Grid { IsHitTestVisible=false };
   ring=new Border { BorderBrush=UI.Accent,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(cornerRadius),Margin=new Thickness(Pad),IsHitTestVisible=false }; host.Children.Add(ring); AddVisualChild(host);
  }
  protected override int VisualChildrenCount { get { return 1; } }
  protected override Visual GetVisualChild(int index) { return host; }
  protected override Size MeasureOverride(Size constraint) { var size=AdornedElement.RenderSize; host.Measure(new Size(size.Width+Pad*2,size.Height+Pad*2)); return size; }
  protected override Size ArrangeOverride(Size finalSize) {
   var size=AdornedElement.RenderSize; double w=size.Width+Pad*2,h=size.Height+Pad*2;
   var outside=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,w,h)),new RectangleGeometry(new Rect(Pad+1,Pad+1,Math.Max(0,size.Width-2),Math.Max(0,size.Height-2)),radius,radius));
   host.OpacityMask=new DrawingBrush(new GeometryDrawing(Brushes.Black,null,outside)) { Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,w,h),ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,w,h) };
   host.Arrange(new Rect(-Pad,-Pad,w,h)); return finalSize;
  }
 }
 public class GlowAdorner : System.Windows.Documents.Adorner {
  readonly Border ring;
  public Border Ring { get { return ring; } }
  public GlowAdorner(UIElement adorned,Border child) : base(adorned) { IsHitTestVisible=false; ring=child; AddVisualChild(ring); }
  public GlowAdorner(UIElement adorned,CornerRadius radius) : base(adorned) { IsHitTestVisible=false; ring=new Border { BorderBrush=UI.Accent,BorderThickness=new Thickness(2),CornerRadius=radius,IsHitTestVisible=false }; AddVisualChild(ring); }
  protected override int VisualChildrenCount { get { return 1; } }
  protected override Visual GetVisualChild(int index) { return ring; }
  protected override Size MeasureOverride(Size constraint) { ring.Measure(constraint); return AdornedElement.RenderSize; }
  // The ring sits exactly on the card's own border, so only the glow is added.
  protected override Size ArrangeOverride(Size finalSize) { var size=AdornedElement.RenderSize; ring.Arrange(new Rect(0,0,size.Width,size.Height)); return finalSize; }
 }
 public partial class MainWindow : Window {
  public State State;
  DateTime selected=AppClock.Today;
  DateTime lastToday=AppClock.Today;
  StackPanel list,hero;
  TextBlock dayLabel,clockLabel,summary;
  ScrollViewer scroll;
  Button pin,compact;
  Border weekNow;
  DispatcherTimer timer;
  Forms.NotifyIcon tray;
  bool exiting,preview;
  string signature="";
  public MainWindow(State state,bool previewMode) {
   State=state; UI.Apply(state); preview=previewMode; Title="Dayglance"; Width=440; Height=760; MinWidth=380; MinHeight=340; WindowStyle=WindowStyle.None; AllowsTransparency=true; ResizeMode=ResizeMode.CanResizeWithGrip;
   Background=UI.Bg; Foreground=UI.Text; FontFamily=new FontFamily("Segoe UI"); Topmost=state.Pinned;
   BuildView();
   if(!preview) {
    tray=new Forms.NotifyIcon { Text="Dayglance — your day at a glance",Icon=BrandIcon.Make(),Visible=true }; tray.DoubleClick+=(s,e)=>Restore(); var menu=new Forms.ContextMenuStrip(); menu.Items.Add("Open Dayglance",null,(s,e)=>Restore()); menu.Items.Add("Add activity",null,(s,e)=> { Restore(); Edit(null); }); menu.Items.Add("Quit",null,(s,e)=> { exiting=true; Close(); }); tray.ContextMenuStrip=menu; tray.BalloonTipClicked+=(s,e)=>Restore();
    Closing+=(s,e)=> { PersistPosition(); if(!exiting) { e.Cancel=true; Hide(); } }; Closed+=(s,e)=> { timer.Stop(); tray.Dispose(); };
    timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(10) }; timer.Tick+=(s,e)=> { Refresh(false); Notify(); }; timer.Start();
    var updateTimer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(30) }; updateTimer.Tick+=(s,e)=> { updateTimer.Interval=TimeSpan.FromHours(6); CheckForUpdates(false,null); }; updateTimer.Start();
   }
   UI.EnableBorderResize(this); EnableLightDismiss(); EnableHistoryInput(); SetSize(); Refresh(true); UpdateTrayLanguage(); SizeChanged+=(s,e)=> { RememberSize(); QueueGeometrySave(); if(State.WeekView && weekPanel!=null) RenderWeek(); }; LocationChanged+=(s,e)=> { RememberSize(); QueueGeometrySave(); };
  }
  void BuildView() {
   UI.Apply(State); Background=UI.Bg; Foreground=UI.Text;
   if(State.Compact) { BuildMini(); return; }
   var root=new DockPanel { Margin=new Thickness(18,10,18,12),LastChildFill=true }; Content=new Border { BorderBrush=UI.Line,BorderThickness=new Thickness(1),Child=root,LayoutTransform=new ScaleTransform(UI.Scale,UI.Scale) };
   // Title bar: brand (drag handle) on the left, actions and window controls on the right.
   var header=new DockPanel { Margin=new Thickness(0,0,0,10),Background=Brushes.Transparent,Cursor=Cursors.SizeAll }; header.ToolTip=UI.T("Drag to position your widget");
   header.MouseLeftButtonDown+=(s,e)=> { if(e.ClickCount==2) ToggleCompact(); else DragMove(); };
   var tools=UI.Row(); tools.Cursor=Cursors.Arrow; tools.VerticalAlignment=VerticalAlignment.Center;
   tools.Children.Add(UI.Icon("\uE710","Add activity",()=>Edit(null),true)); tools.Children.Add(UI.Icon("\uE8FD","Manage",Manage)); tools.Children.Add(UI.Icon("\uE713","Settings",Settings));
   tools.Children.Add(new Border { Width=1,Height=18,Background=UI.Line,Margin=new Thickness(8,0,6,0),VerticalAlignment=VerticalAlignment.Center });
   pin=UI.Icon("\uE718","Pin on top",()=> { State.Pinned=!State.Pinned; Topmost=State.Pinned; Save(); Refresh(true); }); tools.Children.Add(pin);
   tools.Children.Add(UI.Icon("\uE921","Minimize",()=>WindowState=WindowState.Minimized)); tools.Children.Add(UI.Icon("\uE8BB","Hide to tray",()=>Close()));
   DockPanel.SetDock(tools,Dock.Right); header.Children.Add(tools);
   var brand=UI.Row(); brand.VerticalAlignment=VerticalAlignment.Center; var mark=(FrameworkElement)BrandIcon.Visual(); mark.Margin=new Thickness(0); brand.Children.Add(new Viewbox { Width=20,Height=20,Child=mark,Margin=new Thickness(0,0,8,0) });
   var brandText=new TextBlock { Text="dayglance",FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=UI.Text,VerticalAlignment=VerticalAlignment.Center }; brand.Children.Add(brandText); header.Children.Add(brand);
   DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
   var foot=UI.Label("LOCAL BY DESIGN  ·  YOUR TIME, YOUR WAY",9,UI.Muted); foot.Margin=new Thickness(0,8,0,0); DockPanel.SetDock(foot,Dock.Bottom); root.Children.Add(foot);
   var top=new StackPanel();
   // Toolbar: Day/Week segmented switch on the left; date navigation and density on the right.
   var toolbar=new DockPanel { Margin=new Thickness(0,0,0,12) };
   var nav=UI.Row(); nav.VerticalAlignment=VerticalAlignment.Center;
   nav.Children.Add(UI.Icon("\uE76B","Previous",()=> { selected=selected.AddDays(State.WeekView?-7:-1); Refresh(true); }));
   var todayButton=UI.Button("Today",()=> { weekColumn=-1; if(!Navigate(State.WeekView,AppClock.Today)) Refresh(true); }); todayButton.Margin=new Thickness(2,0,2,0); todayButton.MinHeight=30; todayButton.Background=Brushes.Transparent; nav.Children.Add(todayButton);
   nav.Children.Add(UI.Icon("\uE76C","Next",()=> { selected=selected.AddDays(State.WeekView?7:1); Refresh(true); }));
   compact=UI.Icon("\uE73F","Mini widget",ToggleCompact); compact.Margin=new Thickness(6,0,0,0); nav.Children.Add(compact);
   DockPanel.SetDock(nav,Dock.Right); toolbar.Children.Add(nav);
   var views=UI.Row(); var dayButton=UI.Button("Day",()=>Navigate(false,selected),!State.WeekView); var weekButton=UI.Button("Week",()=>Navigate(true,selected),State.WeekView);
   foreach(var b in new[]{dayButton,weekButton}) { b.Margin=new Thickness(0); b.MinHeight=28; b.Padding=new Thickness(4,2,4,2); if(b.Background!=UI.Accent) b.Background=Brushes.Transparent; views.Children.Add(b); }
   var segmented=new Border { Child=views,Background=UI.Card,CornerRadius=new CornerRadius(9),Padding=new Thickness(3),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center }; toolbar.Children.Add(segmented);
   top.Children.Add(toolbar);
   var dateRow=new DockPanel(); weekNow=new Border { Background=UI.Hero,CornerRadius=new CornerRadius(10),Padding=new Thickness(10,6,12,6),Margin=new Thickness(10,0,0,0),MaxWidth=240,VerticalAlignment=VerticalAlignment.Bottom,Cursor=Cursors.Hand,Visibility=Visibility.Collapsed,ToolTip=UI.T("Show in schedule") };
   weekNow.MouseLeftButtonUp+=(s,e)=>FocusWeekNow(); DockPanel.SetDock(weekNow,Dock.Right); dateRow.Children.Add(weekNow);
   var dateText=new StackPanel { VerticalAlignment=VerticalAlignment.Bottom }; dateRow.Children.Add(dateText); top.Children.Add(dateRow);
   clockLabel=UI.Label("",11,UI.Muted); clockLabel.Margin=new Thickness(0,0,0,2); dateText.Children.Add(clockLabel);
   dayLabel=UI.Label("",22,UI.Text); dayLabel.FontWeight=FontWeights.SemiBold; dayLabel.Margin=new Thickness(0); dayLabel.TextTrimming=TextTrimming.CharacterEllipsis; dayLabel.TextWrapping=TextWrapping.NoWrap; dateText.Children.Add(dayLabel);
   hero=new StackPanel(); top.Children.Add(hero); summary=UI.Label("",11,UI.Muted); summary.Margin=new Thickness(0,12,0,10); top.Children.Add(summary); DockPanel.SetDock(top,Dock.Top); root.Children.Add(top);
   list=new StackPanel(); scroll=new ScrollViewer { Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; if(State.WeekView) { weekPanel=new DockPanel(); weekPanel.SizeChanged+=(s,e)=>RenderWeek(); root.Children.Add(weekPanel); } else { weekPanel=null; root.Children.Add(scroll); }
   hero.Visibility=State.WeekView?Visibility.Collapsed:Visibility.Visible;
   scroll.PreviewMouseWheel+=(s,e)=> { if((Keyboard.Modifiers&ModifierKeys.Control)==0) return; e.Handled=true; ZoomSchedule(e.Delta,0); };
   list.LayoutTransform=new ScaleTransform(dayZoom,dayZoom);
   Refresh(true);
  }
  void Restore() { Show(); WindowState=WindowState.Normal; Activate(); }
  void PersistPosition() { RememberSize(); Save(); }
  bool Save() { try { Storage.Save(State); return true; } catch(Exception ex) { MessageBox.Show(this,UI.T("Your changes could not be saved.")+"\n\n"+UI.T(ex.Message),"Dayglance",MessageBoxButton.OK,MessageBoxImage.Error); return false; } }
  // The compact button switches between the full schedule and a small "right now" widget; each keeps its own size and position.
  void ToggleCompact() { ToggleCompact(null); }
  void ToggleCompact(Action after) { Transition(()=> { RememberSize(); State.Compact=!State.Compact; if(!State.Compact && State.WeekView) { focusNow=true; weekColumn=-1; } SetSize(); BuildView(); if(!preview) Save(); },after); }
  bool transitioning; string lastCurrentKey;
  // Fades the current content out, applies the change (view/mode switch), then fades and lifts the new content in.
  void Transition(Action change,Action after) {
   var content=Content as FrameworkElement;
   if(preview || transitioning || content==null || !IsVisible || !SystemParameters.ClientAreaAnimation) { change(); if(after!=null) after(); return; }
   transitioning=true;
   var fadeOut=new System.Windows.Media.Animation.DoubleAnimation(1,0,TimeSpan.FromMilliseconds(110));
   fadeOut.Completed+=(s,e)=> {
    try { change(); } finally { transitioning=false; content.BeginAnimation(UIElement.OpacityProperty,null); }
    var fresh=Content as FrameworkElement;
    if(fresh!=null && fresh!=content) {
     var ease=new System.Windows.Media.Animation.CubicEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut }; var lift=new TranslateTransform(0,10); fresh.RenderTransform=lift;
     fresh.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(230)) { EasingFunction=ease });
     lift.BeginAnimation(TranslateTransform.YProperty,new System.Windows.Media.Animation.DoubleAnimation(10,0,TimeSpan.FromMilliseconds(280)) { EasingFunction=ease });
    }
    if(after!=null) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,after);
   };
   content.BeginAnimation(UIElement.OpacityProperty,fadeOut);
  }
  public void Refresh(bool force) {
   if(settingsOpen && !force) return;
   DateTime now=AppClock.Now; if(selected==lastToday) selected=now.Date; lastToday=now.Date; clockLabel.Text=now.ToString("ddd d MMM",UI.Culture)+"  ·  "+UI.Clock(now).ToUpperInvariant();
   var today=Schedule.ForDay(State,now.Date); var active=today.Where(o=>o.Start<=now && o.End>now && !State.Completed.Contains(o.Key)).ToList();
   var entries=Schedule.ForDay(State,selected); var sig=selected.ToString("O")+now.ToString("yyyyMMddHHmm")+State.Completed.Count+State.Activities.Count;
   if(!force && signature==sig) return; signature=sig;
   string currentKey=active.Count>0?active[0].Key:""; bool activityChanged=!preview && lastCurrentKey!=null && currentKey!=lastCurrentKey; lastCurrentKey=currentKey;
   pin.Content="\uE718"; pin.Foreground=State.Pinned?UI.Accent:UI.Muted; pin.FontWeight=State.Pinned?FontWeights.Bold:FontWeights.Normal; pin.ToolTip=UI.T(State.Pinned?"● Pinned on top":"Pin on top"); compact.Content=State.Compact?"\uE740":"\uE73F"; compact.ToolTip=UI.T(State.Compact?"Expand":"Mini widget");
   if(State.Compact) { RefreshMini(today,active,now,activityChanged); return; }
   dayLabel.Text=State.WeekView?Schedule.WeekStart(selected).ToString("d MMM",UI.Culture)+" – "+Schedule.WeekStart(selected).AddDays(6).ToString("d MMM yyyy",UI.Culture):selected==now.Date?UI.T("Today"):selected.ToString("ddd, d MMM",UI.Culture);
   hero.Children.Clear(); var hp=new StackPanel(); hp.Children.Add(UI.Label(active.Count>0?"RIGHT NOW" : "ROOM TO BREATHE",10,UI.Accent));
   if(active.Count>0) {
    var current=active[0]; hp.Children.Add(UI.Label(current.Activity.Title,24,UI.Text)); hp.Children.Add(UI.Label(UI.Clock(current.Start)+" – "+UI.Clock(current.End)+"  ·  "+Math.Ceiling((current.End-now).TotalMinutes)+UI.T(" min left"),12,UI.Muted));
    var bar=new ProgressBar { Minimum=0,Maximum=100,Value=(now-current.Start).TotalSeconds/(current.End-current.Start).TotalSeconds*100,Height=4,Foreground=UI.B(current.Activity.Color),Background=UI.Line,BorderThickness=new Thickness(0),Margin=new Thickness(0,6,0,8) }; hp.Children.Add(bar);
    if(active.Count>1) hp.Children.Add(UI.Label(UI.T("Also now: ")+string.Join(", ",active.Skip(1).Select(o=>o.Activity.Title)),11,UI.Muted));
   } else {
    hp.Children.Add(UI.Label(State.Activities.Count==0?"Make room for your day.":"You’re between activities.",22,UI.Text));
    Occurrence next=null; for(int i=0;i<8 && next==null;i++) next=Schedule.ForDay(State,now.Date.AddDays(i)).FirstOrDefault(o=>o.Start>now&&!State.Completed.Contains(o.Key));
    hp.Children.Add(UI.Label(next==null?"Add an activity to give your day a little structure.":UI.T("Next: ")+next.Activity.Title+" · "+(next.Start.Date==now.Date?"":next.Start.ToString("ddd ",UI.Culture))+UI.Clock(next.Start),12,UI.Muted));
   }
   var heroBox=UI.Box(hp,UI.Hero,new Thickness(0,12,0,0)); hero.Children.Add(heroBox);
   var focusTarget=active.Count>0?active[0]:today.FirstOrDefault(o=>o.Start>now&&!State.Completed.Contains(o.Key));
   if(focusTarget!=null) { string focusKey=focusTarget.Key; heroBox.Cursor=Cursors.Hand; heroBox.ToolTip=UI.T("Show in schedule"); heroBox.MouseLeftButtonUp+=(s,e)=>FocusActivity(focusKey,true); }
   summary.Text=UI.T("SCHEDULE")+"  /  "+entries.Count+" "+UI.T("ACTIVITIES")+"  ·  "+entries.Count(o=>State.Completed.Contains(o.Key))+" "+UI.T("DONE");
   weekNow.Visibility=State.WeekView?Visibility.Visible:Visibility.Collapsed; if(State.WeekView) FillWeekNow(active,now);
   // A new activity just started: slide the Right now card in and give the new current activity a pulse.
   if(activityChanged && active.Count>0) {
    if(State.WeekView) { UI.SlideIn(weekNow,24,0); Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=> { if(weekNowTarget!=null) UI.Attention(weekNowTarget); })); }
    else UI.SlideIn(heroBox,24,0);
   }
   if(State.WeekView) { summary.Text=UI.T("Click an activity to edit or an empty slot to add one."); RenderWeek(); return; }
   var offset=scroll.VerticalOffset; list.Children.Clear();
   if(entries.Count==0) { var empty=new StackPanel { Margin=new Thickness(8,15,8,0) }; empty.Children.Add(UI.Label("A fresh page.",20,UI.Text)); empty.Children.Add(UI.Label("Use + Activity to add something, or Manage to edit your weekly routine.",13,UI.Muted)); list.Children.Add(empty); }
   dayCards.Clear();
   DateTime? busyUntil=null;
   foreach(var o in entries) {
    if(busyUntil.HasValue && o.Start>=busyUntil.Value.AddMinutes(5)) list.Children.Add(GapRow(busyUntil.Value,o.Start));
    var card=DayCard(o,now,State.CardStyle,true); dayCards[o.Key]=card; list.Children.Add(card);
    busyUntil=!busyUntil.HasValue||o.End>busyUntil.Value?o.End:busyUntil.Value;
   }
   scroll.ScrollToVerticalOffset(offset);
   Border startedCard; if(activityChanged && selected==now.Date && dayCards.TryGetValue(currentKey,out startedCard)) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>UI.GlowAround(startedCard,12)));
  }
  void Notify() {
   if(!State.Notifications) return;
   try {
    var due=Schedule.Reminders(State,AppClock.Now); if(due.Count==0) return;
    foreach(var reminder in due) State.Reminded.Add(reminder.Key);
    if(!Save()) { foreach(var reminder in due) State.Reminded.Remove(reminder.Key); return; }
    string title=due.Count==1?due[0].Occurrence.Activity.Title:UI.T("Activities coming up");
    Func<Occurrence,string> span=o=>(o.Start.Date==AppClock.Today?"":o.Start.ToString("ddd d MMM",UI.Culture)+"  ·  ")+UI.Clock(o.Start)+" – "+UI.Clock(o.End);
    var single=due[0].Occurrence;
    string detail=due.Count==1?span(single)+(string.IsNullOrWhiteSpace(single.Activity.Notes)?"":"\n"+single.Activity.Notes.Trim()):string.Join("\n",due.Take(4).Select(r=>r.Occurrence.Activity.Title+"  ·  "+span(r.Occurrence)));
    new ReminderToast(title,detail,Restore,State.Sound);
   } catch(Exception ex) { Debug.WriteLine(ex); }
  }
  void Edit(Activity activity) { Edit(activity,null); }
  void Edit(Activity activity,DateTime? slot,DateTime? slotEnd=null) {
   var w=UI.Dialog(this,activity==null?"Add activity":"Edit activity",480,850); w.LightDismiss=true; var panel=new StackPanel { Margin=new Thickness(24) }; w.Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
   panel.Children.Add(UI.Label(activity==null?"A little structure.":"Make it yours.",25,UI.Text)); panel.Children.Add(UI.Label("ACTIVITY NAME",11,UI.Muted)); var title=UI.Input(activity==null?"":activity.Title); title.MaxLength=120; panel.Children.Add(title);
   var times=UI.Row(); var start=new TimeField(activity!=null?activity.Start:slot.HasValue?slot.Value.ToString("HH:mm"):"09:00");  var end=new TimeField(activity!=null?activity.End:slot.HasValue?(slotEnd.HasValue?slotEnd.Value:slot.Value.AddHours(1)).ToString("HH:mm"):"10:00");  end.Margin=new Thickness(12,0,0,0); panel.Children.Add(UI.Label(UI.Hour12?"START / END":"START / END  ·  24-HOUR TIME (HH:MM)",11,UI.Muted)); times.Children.Add(start); times.Children.Add(end); panel.Children.Add(times);
   panel.Children.Add(UI.Label("An earlier end time finishes the following day.",11,UI.Muted));
   panel.Children.Add(UI.Label("REPEAT ON  ·  LEAVE EMPTY FOR ONE DATE",11,UI.Muted)); var days=UI.Row(); var checks=new List<CheckBox>(); int[] order={1,2,3,4,5,6,0}; foreach(int d in order) { var c=UI.Chip(UI.Culture.DateTimeFormat.AbbreviatedDayNames[d],activity!=null&&activity.Days.Contains(d)); c.Margin=new Thickness(0,5,5,8); checks.Add(c); days.Children.Add(c); } panel.Children.Add(days);
   var presets=UI.Row(); presets.Children.Add(UI.Button("Every day",()=>checks.ForEach(c=>c.IsChecked=true))); presets.Children.Add(UI.Button("Weekdays",()=> { for(int i=0;i<7;i++) checks[i].IsChecked=i<5; })); presets.Children.Add(UI.Button("Once",()=>checks.ForEach(c=>c.IsChecked=false))); panel.Children.Add(presets);
   var date=new DateField { SelectedDate=activity!=null&&activity.Days.Length==0?DateTime.ParseExact(activity.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture):slot.HasValue?slot.Value.Date:selected,Margin=new Thickness(0,10,0,12) }; panel.Children.Add(date); Action updateDate=()=> { date.IsEnabled=!checks.Any(c=>c.IsChecked==true); date.Opacity=date.IsEnabled?1:0.45; }; foreach(var c in checks) { c.Checked+=(s,e)=>updateDate(); c.Unchecked+=(s,e)=>updateDate(); } updateDate();
   panel.Children.Add(UI.Label("COLOR",11,UI.Muted)); string color=Palette.Normalize(activity==null?"#A4E9CC":activity.Color)??"#A4E9CC";
   var palette=new[]{"#A4E9CC","#9CCBFF","#B9AAFF","#F1AED1","#FFD18F","#FF9E94"}.Concat(State.Activities.Select(x=>Palette.Normalize(x.Color)).Where(x=>x!=null).OrderBy(x=>x)).Concat(new[]{color}).Distinct().ToList();
   var colors=new WrapPanel { Margin=new Thickness(0,2,0,12) }; var swatchButtons=new List<Button>(); Button custom=null; Action markColor=null;
   foreach(string hex in palette) { string choice=hex; var b=UI.Button("",()=> { color=choice; markColor(); }); b.Width=40; b.Margin=new Thickness(0,0,6,6); b.Background=UI.B(hex); b.Foreground=UI.Ink(hex); b.ToolTip=hex; b.Tag=hex; swatchButtons.Add(b); colors.Children.Add(b); }
   custom=UI.Button("+",()=>ColorPicker.Show(custom,color,palette,v=> { color=v; markColor(); })); custom.Width=40; custom.Margin=new Thickness(0,0,6,6); custom.BorderThickness=new Thickness(1); colors.Children.Add(custom);
   markColor=()=> {
    foreach(var b in swatchButtons) b.Content=(string)b.Tag==color?"✓":"";
    bool isCustom=!palette.Contains(color); custom.Content=isCustom?"✓":"+"; custom.Background=isCustom?UI.B(color):UI.Card; custom.Foreground=isCustom?UI.Ink(color):UI.Text; custom.ToolTip=UI.T("Custom color…")+(isCustom?"  "+color:"");
   };
   markColor(); panel.Children.Add(colors);
   // Reminders: the main switch notifies when the activity starts; the additional reminder adds one lead time (days + hours + minutes before).
   panel.Children.Add(UI.Label("REMINDERS",11,UI.Muted));
   var reminder=UI.Switch("Remind me when it starts",activity==null||activity.Reminder>=0); panel.Children.Add(reminder);
   int extraMinutes=activity!=null&&activity.ExtraReminders!=null&&activity.ExtraReminders.Count>0?activity.ExtraReminders[0]:activity!=null&&activity.Reminder>0?activity.Reminder:0;
   var extra=UI.Switch("Additional reminder",extraMinutes>0); panel.Children.Add(extra);
   var advance=new WrapPanel { Margin=new Thickness(0,0,0,6) };
   Func<string,int,TextBox> leadBox=(unitName,value)=> { var box=UI.Input(value.ToString()); box.Width=58; box.Margin=new Thickness(0,2,6,8); box.MaxLength=3; box.HorizontalContentAlignment=HorizontalAlignment.Center; advance.Children.Add(box); var label=UI.Label(unitName,13,UI.Muted); label.Margin=new Thickness(0,10,14,8); advance.Children.Add(label); return box; };
   var leadDays=leadBox(UI.T("days"),extraMinutes/1440); var leadHours=leadBox(UI.T("hours"),extraMinutes%1440/60); var leadMinutes=leadBox(UI.T("minutes"),extraMinutes>0?extraMinutes%60:15);
   var beforeLabel=UI.Label("before",13,UI.Muted); beforeLabel.Margin=new Thickness(0,10,0,8); advance.Children.Add(beforeLabel); panel.Children.Add(advance);
   Action toggleAdvance=()=> { advance.Visibility=extra.IsChecked==true?Visibility.Visible:Visibility.Collapsed; }; extra.Checked+=(s,e)=>toggleAdvance(); extra.Unchecked+=(s,e)=>toggleAdvance(); toggleAdvance();
   panel.Children.Add(UI.Label("NOTES (OPTIONAL)",11,UI.Muted)); var notes=UI.Input(activity==null?"":activity.Notes); notes.Height=65; notes.AcceptsReturn=true; notes.TextWrapping=TextWrapping.Wrap; notes.MaxLength=2000; panel.Children.Add(notes);
   var error=UI.Label("",12,UI.B("#FF9E94")); panel.Children.Add(error); var buttons=UI.Row(); buttons.Children.Add(UI.Button("Save activity",()=> {
    try {
     int extraValue=0;
     if(extra.IsChecked==true) {
      int dd,hh,mm; if(!int.TryParse(leadDays.Text.Trim()==""?"0":leadDays.Text,out dd)||!int.TryParse(leadHours.Text.Trim()==""?"0":leadHours.Text,out hh)||!int.TryParse(leadMinutes.Text.Trim()==""?"0":leadMinutes.Text,out mm)||dd<0||hh<0||mm<0) throw new Exception("Use whole numbers for days, hours and minutes.");
      extraValue=dd*1440+hh*60+mm; if(extraValue<1||extraValue>43200) throw new Exception("Use one additional reminder, between 1 minute and 30 days.");
     }
     var a=new Activity { Id=activity==null?Guid.NewGuid().ToString("N"):activity.Id,Title=title.Text.Trim(),Start=start.Value,End=end.Value,Days=order.Where((d,i)=>checks[i].IsChecked==true).ToArray(),Date=date.SelectedDate.HasValue?date.SelectedDate.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):"",Color=color,Notes=notes.Text.Trim(),Reminder=reminder.IsChecked==true?0:-1,ExtraReminders=extra.IsChecked==true?new List<int> { extraValue }:new List<int>() };
     var test=new State(); test.Activities.Add(a); Schedule.Validate(test);
     var old=State.Activities.ToList(); if(activity!=null) State.Activities.Remove(activity); State.Activities.Add(a);
     if(!Save()) { State.Activities=old; return; } w.Close(); Refresh(true);
    } catch(Exception ex) { error.Text=UI.T("Check your details: ")+UI.T(ex.Message); }
   },true)); buttons.Children.Add(UI.Button("Cancel",()=>w.Close())); panel.Children.Add(buttons); w.ShowDialog();
  }
  void Manage() {
   var w=UI.Dialog(this,"Manage schedule",540,560); w.LightDismiss=true; var root=new DockPanel { Margin=new Thickness(24) }; w.Content=root; var heading=UI.Label("Your routine",26,UI.Text); DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading); var all=new StackPanel(); root.Children.Add(new ScrollViewer { Content=all,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
   Action fill=null; fill=()=> { all.Children.Clear(); if(State.Activities.Count==0) all.Children.Add(UI.Label("No activities yet. Close this window and choose + Activity.",14,UI.Muted)); foreach(var a in State.Activities.OrderBy(a=>a.Start).ToList()) { var p=new StackPanel(); p.Children.Add(UI.Label(a.Title,17,UI.Text)); p.Children.Add(UI.Label(UI.ClockText(a.Start)+" – "+UI.ClockText(a.End)+"  ·  "+(a.Days.Length==0?a.Date:string.Join(", ",a.Days.Select(d=>UI.Culture.DateTimeFormat.AbbreviatedDayNames[d]))),12,UI.Muted)); var r=UI.Row(); r.Children.Add(UI.Button("Edit",()=> { Edit(a); fill(); })); r.Children.Add(UI.Button("Delete",()=> { if(MessageBox.Show(w,UI.Language=="es"?"¿Eliminar «"+a.Title+"» y todas sus repeticiones?":"Delete ‘"+a.Title+"’ and all its future repeats?",UI.T("Delete activity"),MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes) { State.Activities.Remove(a); if(!Save()) State.Activities.Add(a); Refresh(true); fill(); } })); p.Children.Add(r); all.Children.Add(UI.Box(p,UI.Card,new Thickness(0,6,0,6))); } }; fill(); w.ShowDialog();
  }
  string StartupPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Dayglance.lnk"); } }

 }
 public static class Program {
  [STAThread] public static void Main(string[] args) {
   if(args.Contains("--self-test")) { Tests.Run(); return; }
   if(args.Contains("--ui-test")) { Tests.UIRun(); return; }
   if(args.Contains("--showcase")) { Tests.Showcase(args.Last()); return; }
   if(args.Contains("--screenshots")) { int at=Array.IndexOf(args,"--screenshots"); Tests.Screenshots(args.Length>at+1?args[at+1]:"docs\\sample-schedule.json",args.Length>at+2?args[at+2]:"docs\\screenshots"); return; }
   if(args.Contains("--preview")) { Preview(args.Last()); return; }
   bool created; using(var mutex=new Mutex(true,"Local\\Dayglance.Desktop.1",out created)) {
    if(!created) { MessageBox.Show("Dayglance is already running. Open it from the system tray.","Dayglance"); return; }
    State state=new State();
    try { if(File.Exists(Storage.FilePath)) state=Storage.Read(Storage.FilePath); }
    catch(Exception ex) { MessageBox.Show("Dayglance could not read your schedule. Your file has been left untouched.\n\n"+Storage.FilePath+"\n\n"+ex.Message+"\n\nA previous save may be available in schedule.json.bak.","Dayglance",MessageBoxButton.OK,MessageBoxImage.Error); return; }
    if(Schedule.MigrateReminders(state)) { try { Storage.Save(state); } catch {} }
    AppDomain.CurrentDomain.UnhandledException+=(s,e)=>ReportCrash(e.ExceptionObject as Exception,true);
    var app=new Application(); app.DispatcherUnhandledException+=(s,e)=> { ReportCrash(e.Exception,false); e.Handled=true; };
    MainWindow window;
    try { window=new MainWindow(state,false); } catch(Exception ex) { ReportCrash(ex,true); return; }
    app.Run(window);
   }
  }
  // Startup failures used to exit silently; log the full exception and tell the user where it is.
  static void ReportCrash(Exception ex,bool fatal) {
   if(ex==null) return; string log=Path.Combine(Storage.Folder,"crash.log");
   try { Directory.CreateDirectory(Storage.Folder); File.AppendAllText(log,AppClock.Now.ToString("O")+(fatal?"  FATAL":"")+"\r\n"+ex+"\r\n\r\n"); } catch {}
   try { MessageBox.Show("Dayglance encountered an error"+(fatal?" and has to close":"")+":\n\n"+ex.GetType().Name+": "+ex.Message+"\n\nDetails were saved to:\n"+log,"Dayglance",MessageBoxButton.OK,MessageBoxImage.Error); } catch {}
  }
  static void Preview(string path) {
   var s=new State(); var now=AppClock.Now;
   foreach(var a in new[]{new Activity { Title="A moment to reset",Start=now.AddMinutes(-75).ToString("HH:mm"),End=now.AddMinutes(-45).ToString("HH:mm"),Color="#B9AAFF",Notes="A walk, some water, a clear head." },new Activity { Title="Make something meaningful",Start=now.AddMinutes(-20).ToString("HH:mm"),End=now.AddMinutes(40).ToString("HH:mm"),Color="#A4E9CC",Notes="One task. A little less noise." },new Activity { Title="Move & recharge",Start=now.AddMinutes(50).ToString("HH:mm"),End=now.AddMinutes(90).ToString("HH:mm"),Color="#FFD18F",Notes="Step away from the screen." }}) { a.Id=Guid.NewGuid().ToString("N"); a.Days=Enumerable.Range(0,7).ToArray(); a.Reminder=0; s.Activities.Add(a); }
   s.Completed.Add(Schedule.ForDay(s,now).First().Key); var app=new Application(); var w=new MainWindow(s,true); w.ShowInTaskbar=false; w.ShowActivated=false; w.Left=-10000; w.Top=-10000; w.Show(); w.UpdateLayout(); var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32); bmp.Render(w); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp)); using(var f=File.Create(path)) encoder.Save(f); w.Close(); app.Shutdown();
  }
 }
}

