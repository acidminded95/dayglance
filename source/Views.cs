using System;
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
  double weekZoom=1,dayZoom=1;
  void SetSize() {
   var area=SystemParameters.WorkArea; MinWidth=State.WeekView?Math.Min(820,area.Width):380;
   Width=Math.Min(State.WeekView?1200:440,area.Width); Height=Math.Min(State.WeekView?950:State.Compact?550:800,area.Height);
   Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width)); Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height));
  }
  public void SetView(bool week) { State.WeekView=week; SetSize(); BuildView(); if(!preview) Save(); }
  public void ZoomSchedule(int direction,double anchor) {
   if(!State.WeekView) { dayZoom=Math.Max(.8,Math.Min(1.7,dayZoom*(direction>0?1.1:1/1.1))); list.LayoutTransform=new ScaleTransform(dayZoom,dayZoom); return; }
   double before=weekZoom,previous=weekScroll.VerticalOffset; weekZoom=Math.Max(.65,Math.Min(3,weekZoom*(direction>0?1.15:1/1.15))); RenderWeek(); weekScroll.UpdateLayout(); weekScroll.ScrollToVerticalOffset((previous+anchor)*weekZoom/before-anchor);
  }
  void RenderWeek() {
   if(weekPanel==null || ActualWidth<1) return;
   double offset=weekScroll==null?0:weekScroll.VerticalOffset;
   weekPanel.Children.Clear(); DateTime first=Schedule.WeekStart(selected),now=DateTime.Now;
   double available=Math.Max(560,(weekPanel.ActualWidth>100?weekPanel.ActualWidth:ActualWidth-42)-SystemParameters.VerticalScrollBarWidth), gutter=44, column=(available-gutter)/7;
   double viewport=weekPanel.ActualHeight>100?weekPanel.ActualHeight:ActualHeight-370;
   double rowHeight=Math.Max(14,(State.Compact?Math.Max(14,(viewport-72)/24):42)*weekZoom);
   var headings=new Canvas { Height=43,Width=available,HorizontalAlignment=HorizontalAlignment.Left }; DockPanel.SetDock(headings,Dock.Top); weekPanel.Children.Add(headings);
   for(int d=0;d<7;d++) {
    DateTime date=first.AddDays(d); bool today=date==now.Date;
    var button=UI.Button(date.ToString("ddd  d",UI.Culture),()=> { selected=date; SetView(false); },today); button.Width=column-4; button.Margin=new Thickness(0); button.ToolTip=UI.T("Show this day"); Canvas.SetLeft(button,gutter+d*column); headings.Children.Add(button);
   }
   var canvas=new Canvas { Width=available,Height=rowHeight*24+28,Background=UI.Card,ClipToBounds=true };
   for(int d=0;d<7;d++) {
    var date=first.AddDays(d); if(date==now.Date) { var shade=new Rectangle { Width=column,Height=rowHeight*24,Fill=UI.Hero }; Canvas.SetLeft(shade,gutter+d*column); canvas.Children.Add(shade); }
    var vertical=new Line { X1=gutter+d*column,X2=gutter+d*column,Y1=0,Y2=rowHeight*24,Stroke=UI.Line,StrokeThickness=0.5 }; canvas.Children.Add(vertical);
   }
   for(int hour=0;hour<=24;hour++) {
    var label=UI.Label(hour.ToString("00")+":00",10,UI.Muted); label.Margin=new Thickness(0); Canvas.SetTop(label,hour*rowHeight+2); canvas.Children.Add(label);
    canvas.Children.Add(new Line { X1=gutter,X2=available,Y1=hour*rowHeight,Y2=hour*rowHeight,Stroke=UI.Line,StrokeThickness=0.5 });
   }
   for(int d=0;d<7;d++) foreach(var block in Schedule.Layout(State,first.AddDays(d))) {
    var o=block.Occurrence; bool done=State.Completed.Contains(o.Key),current=o.Start<=now&&o.End>now;
    double width=(column-4)/block.Lanes, height=Math.Max(5,(block.EndHour-block.StartHour)*rowHeight-2);
    var text=new StackPanel(); var title=UI.Label((done?"✓ ":"")+o.Activity.Title,block.Lanes>1?9:11,UI.Ink(o.Activity.Color)); title.Margin=new Thickness(0); title.FontWeight=FontWeights.SemiBold; title.MaxHeight=height<37?height-2:Math.Max(15,height-20); title.TextTrimming=TextTrimming.CharacterEllipsis; text.Children.Add(title);
    if(height>=39) { var time=UI.Label(o.Start.ToString("HH:mm")+"–"+o.End.ToString("HH:mm"),9,UI.Ink(o.Activity.Color)); time.Margin=new Thickness(0); text.Children.Add(time); }
    var card=new Border { Child=text,Width=Math.Max(8,width-2),Height=height,Padding=new Thickness(4,height<25?1:3,3,1),Background=UI.B(o.Activity.Color),CornerRadius=new CornerRadius(5),BorderBrush=current?UI.Text:UI.B(o.Activity.Color),BorderThickness=new Thickness(current?2:0),Opacity=done?0.58:0.95,ClipToBounds=true,Cursor=Cursors.Hand };
    card.ToolTip=o.Activity.Title+"\n"+o.Start.ToString("ddd HH:mm",UI.Culture)+"–"+o.End.ToString("ddd HH:mm",UI.Culture)+(string.IsNullOrWhiteSpace(o.Activity.Notes)?"":"\n"+o.Activity.Notes)+"\n"+UI.T("Edit activity");
    card.MouseLeftButtonUp+=(s,e)=>Edit(o.Activity); Canvas.SetLeft(card,gutter+d*column+3+block.Lane*width); Canvas.SetTop(card,block.StartHour*rowHeight+1); canvas.Children.Add(card);
   }
   if(now.Date>=first && now.Date<first.AddDays(7)) {
    double y=now.TimeOfDay.TotalHours*rowHeight, x=gutter+(int)now.DayOfWeek*column;
    canvas.Children.Add(new Line { X1=gutter,X2=available,Y1=y,Y2=y,Stroke=UI.Accent,StrokeThickness=1.5,IsHitTestVisible=false });
    var dot=new Ellipse { Width=8,Height=8,Fill=UI.Accent,IsHitTestVisible=false }; Canvas.SetLeft(dot,x-4); Canvas.SetTop(dot,y-4); canvas.Children.Add(dot);
    var tag=new Border { Background=UI.Accent,CornerRadius=new CornerRadius(3),Padding=new Thickness(2),Child=new TextBlock { Text=now.ToString("HH:mm"),FontSize=10,Foreground=UI.AccentInk },IsHitTestVisible=false }; Canvas.SetTop(tag,y-9); canvas.Children.Add(tag);
   }
   weekScroll=new ScrollViewer { Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; weekPanel.Children.Add(weekScroll); weekScroll.ScrollToVerticalOffset(offset);
   weekScroll.PreviewMouseWheel+=(s,e)=> { if((Keyboard.Modifiers&ModifierKeys.Control)==0) return; e.Handled=true; ZoomSchedule(e.Delta,e.GetPosition(weekScroll).Y); };
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
    var startText=new TextBlock { Text=o.Start.ToString("HH:mm"),FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=ink,HorizontalAlignment=HorizontalAlignment.Center }; times.Children.Add(startText);
    times.Children.Add(new TextBlock { Text=o.End.ToString("HH:mm"),FontSize=11,Foreground=ink,Opacity=.8,HorizontalAlignment=HorizontalAlignment.Center });
    row.Children.Add(new Border { Background=color,CornerRadius=new CornerRadius(11,0,0,11),Child=times });
   } else if(!full) row.Children.Add(new Border { Background=color,CornerRadius=new CornerRadius(3),Width=3 });
   Brush titleBrush=full?ink:done?UI.Muted:UI.Text,subBrush=full?ink:UI.Muted;
   var info=new StackPanel { Margin=band?new Thickness(12,10,8,10):new Thickness(full?0:10,0,8,0),VerticalAlignment=VerticalAlignment.Center };
   var name=UI.Label(o.Activity.Title,15,titleBrush); name.FontWeight=FontWeights.SemiBold; if(done) name.TextDecorations=TextDecorations.Strikethrough; info.Children.Add(name);
   string when=band?"":o.Start.ToString("HH:mm")+" – "+o.End.ToString("HH:mm");
   if(o.End.Date>o.Start.Date) when+=UI.T(" (+1 day)"); if(current) when+=(when.Length>0?"  • ":"• ")+UI.T("NOW");
   when=when.Trim(); if(when.Length>0) { var whenLabel=UI.Label(when,11,subBrush); if(full) whenLabel.Opacity=.85; info.Children.Add(whenLabel); }
   if(!State.Compact && !string.IsNullOrWhiteSpace(o.Activity.Notes)) { var notes=UI.Label(o.Activity.Notes,12,subBrush); if(full) notes.Opacity=.85; info.Children.Add(notes); }
   if(info.Children.Count>0) ((FrameworkElement)info.Children[info.Children.Count-1]).Margin=new Thickness(0);
   Grid.SetColumn(info,1); row.Children.Add(info);
   var toggle=UI.Button(done?"✓":"○",()=> { if(!interactive) return; if(State.Completed.Contains(o.Key)) State.Completed.Remove(o.Key); else State.Completed.Add(o.Key); Save(); Refresh(true); });
   toggle.ToolTip=UI.T(done?"Mark incomplete":"Mark done"); toggle.VerticalAlignment=VerticalAlignment.Center; if(band) toggle.Margin=new Thickness(0,0,10,0);
   if(full) { toggle.Background=new SolidColorBrush(Palette.IsDark(hex)?Color.FromArgb(46,255,255,255):Color.FromArgb(30,0,0,0)); toggle.Foreground=ink; }
   Grid.SetColumn(toggle,2); row.Children.Add(toggle);
   var box=UI.Box(row,full?color:current?UI.Hero:UI.Card,new Thickness(0,0,0,8)); if(band) box.Padding=new Thickness(0);
   box.BorderThickness=new Thickness(full&&current?2:1); box.BorderBrush=current?(full?UI.Text:color):full?color:UI.Card; if(full&&done) box.Opacity=.6;
   if(interactive) { box.MouseLeftButtonDown+=(s,e)=> { if(e.ClickCount==2) Edit(o.Activity); }; box.ToolTip=UI.T("Double-click to edit"); }
   return box;
  }
  // Scrolls the day list so the activity is the first visible card, or the second when the previous and next cards also fit.
  void FocusActivity(string key) {
   if(State.WeekView) return;
   if(selected!=DateTime.Today) { selected=DateTime.Today; Refresh(true); }
   Border card; if(!dayCards.TryGetValue(key,out card)) return;
   scroll.UpdateLayout(); int index=list.Children.IndexOf(card); if(index<0) return;
   Func<FrameworkElement,double> top=el=>el.TranslatePoint(new Point(0,0),scroll).Y+scroll.VerticalOffset;
   Func<FrameworkElement,double> span=el=>(el.ActualHeight+el.Margin.Top+el.Margin.Bottom)*dayZoom;
   var previous=index>0?list.Children[index-1] as FrameworkElement:null; var next=index<list.Children.Count-1?list.Children[index+1] as FrameworkElement:null;
   double target=top(card);
   if(previous!=null && span(previous)+span(card)+(next!=null?span(next):0)<=scroll.ViewportHeight+1) target=top(previous);
   scroll.ScrollToVerticalOffset(Math.Max(0,target));
  }
  bool settingsOpen;
  // Settings preview their theme and language on the dialog itself; the main widget only changes after saving.
  void Settings() {
   var w=UI.Dialog(this,"Dayglance settings",510,790); var p=new StackPanel { Margin=new Thickness(24) }; var scroller=new ScrollViewer { Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; w.Content=scroller;
   string themeId=State.Theme,language=State.Language,cardStyle=State.CardStyle; bool notifications=State.Notifications,sound=State.Sound,startup=File.Exists(StartupPath),saved=false;
   Choice languageChoice=null; Action render=null;
   Action applyPending=()=> {
    double y=scroller.VerticalOffset; UI.Apply(new State { Theme=themeId,Language=language,CustomThemes=State.CustomThemes });
    w.Title=UI.T("Dayglance settings"); w.Restyle(); render(); scroller.UpdateLayout(); scroller.ScrollToVerticalOffset(y);
   };
   render=()=> {
    p.Children.Clear();
    p.Children.Add(UI.Label("Set your own pace.",25,UI.Text)); p.Children.Add(UI.Label("APPEARANCE",11,UI.Muted));
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
    var languageTitle=UI.Label("LANGUAGE",11,UI.Muted); languageTitle.Margin=new Thickness(0,15,0,6); p.Children.Add(languageTitle);
    languageChoice=new Choice(); languageChoice.Items.Add("English"); languageChoice.Items.Add("Español"); languageChoice.SelectedIndex=language=="es"?1:0; languageChoice.Changed+=()=> { language=languageChoice.SelectedIndex==1?"es":"en"; applyPending(); }; p.Children.Add(languageChoice);
    var cardsTitle=UI.Label("ACTIVITY CARDS",11,UI.Muted); cardsTitle.Margin=new Thickness(0,4,0,6); p.Children.Add(cardsTitle);
    string[] styles={"stripe","band","full"}; var cardChoice=new Choice(); cardChoice.Items.AddRange(new[]{"Slim color line","Color band","Full color card"}); cardChoice.SelectedIndex=Math.Max(0,Array.IndexOf(styles,cardStyle)); cardChoice.Changed+=()=> { cardStyle=styles[cardChoice.SelectedIndex]; applyPending(); }; p.Children.Add(cardChoice);
    var sampleActivity=new Activity { Id="sample",Title=UI.T("Sample activity"),Color=Palette.Hex(((SolidColorBrush)UI.Accent).Color),Start="09:00",End="10:30",Days=new int[0],Notes=UI.T("Double-click to edit") };
    var sample=DayCard(new Occurrence { Activity=sampleActivity,Start=DateTime.Today.AddHours(9),End=DateTime.Today.AddHours(10.5) },DateTime.Today.AddHours(9.5),cardStyle,false); sample.Margin=new Thickness(0,0,0,14); p.Children.Add(sample);
    p.Children.Add(UI.Label("REMINDERS & STARTUP",11,UI.Muted));
    var notificationSwitch=UI.Switch("Enable activity reminders",notifications); notificationSwitch.Checked+=(s,e)=>notifications=true; notificationSwitch.Unchecked+=(s,e)=>notifications=false;
    var soundSwitch=UI.Switch("Play a sound with reminders",sound); soundSwitch.Checked+=(s,e)=>sound=true; soundSwitch.Unchecked+=(s,e)=>sound=false;
    var startupSwitch=UI.Switch("Start when I sign in to Windows",startup); startupSwitch.Checked+=(s,e)=>startup=true; startupSwitch.Unchecked+=(s,e)=>startup=false;
    p.Children.Add(notificationSwitch); p.Children.Add(soundSwitch); p.Children.Add(startupSwitch);
    p.Children.Add(UI.Label(UI.Language=="es"?"Los avisos propios de Dayglance usan tu tema y un sonido suave. Funcionan mientras la app esté abierta; se cierran tras 18 segundos. × oculta el widget en la bandeja.":"Dayglance reminders use your theme and a soft chime. They work while the app is running and dismiss after 18 seconds. × hides the widget in the tray.",12,UI.Muted));
    p.Children.Add(UI.Label(UI.Language=="es"?"Los cambios se muestran en esta ventana y se aplican al widget al guardar.":"Changes preview in this window and apply to the widget when you save.",12,UI.Accent));
    var actions=new WrapPanel(); actions.Children.Add(UI.Button("Test reminder",()=>new ReminderToast(UI.T("Notification preview"),UI.T("Your reminder will look like this."),Restore,sound)));
    actions.Children.Add(UI.Button("Save preferences",()=> {
     try {
      if(!preview) {
       if(startup) { Type t=Type.GetTypeFromProgID("WScript.Shell"); dynamic shell=Activator.CreateInstance(t); dynamic shortcut=shell.CreateShortcut(StartupPath); shortcut.TargetPath=System.Reflection.Assembly.GetExecutingAssembly().Location; shortcut.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory; shortcut.Description="Dayglance"; shortcut.Save(); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
       else if(File.Exists(StartupPath)) File.Delete(StartupPath);
      }
      string oldTheme=State.Theme,oldLanguage=State.Language,oldCards=State.CardStyle; bool oldNotifications=State.Notifications,oldSound=State.Sound;
      State.Notifications=notifications; State.Sound=sound; State.Theme=themeId; State.CardStyle=cardStyle; State.Language=languageChoice.SelectedIndex==1?"es":"en";
      if(Save()) { saved=true; w.Close(); } else { State.Theme=oldTheme; State.Language=oldLanguage; State.Notifications=oldNotifications; State.Sound=oldSound; State.CardStyle=oldCards; }
     } catch(Exception ex) { MessageBox.Show(w,UI.T(ex.Message),UI.T("Could not save preferences")); }
    },true));
    foreach(UIElement child in actions.Children) ((FrameworkElement)child).Margin=new Thickness(0,0,6,6);
    p.Children.Add(actions);
    var backup=UI.Label("BACKUP & SHARING",11,UI.Accent); backup.Margin=new Thickness(0,20,0,8); p.Children.Add(backup);
    p.Children.Add(UI.Label("Export your schedule and completion history. Import replaces the current schedule; a backup is saved first.",12,UI.Muted));
    var row=UI.Row(); row.Children.Add(UI.Button("Export…",()=> { var d=new Microsoft.Win32.SaveFileDialog { Filter="Dayglance JSON (*.json)|*.json",FileName="Dayglance-backup.json" }; if(d.ShowDialog(w)==true) try { File.WriteAllText(d.FileName,Storage.Serializer().Serialize(State)); } catch(Exception ex) { MessageBox.Show(w,UI.T(ex.Message)); } }));
    row.Children.Add(UI.Button("Import…",()=> {
     var d=new Microsoft.Win32.OpenFileDialog { Filter="Dayglance JSON (*.json)|*.json" }; if(d.ShowDialog(w)!=true) return;
     try {
      var incoming=Storage.Read(d.FileName);
      string question=UI.Language=="es"?"¿Reemplazar tu horario con "+incoming.Activities.Count+" actividades? Se guardará un respaldo.":"Replace your schedule with "+incoming.Activities.Count+" imported activities? A backup will be saved.";
      if(MessageBox.Show(w,question,UI.T("Import schedule"),MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
      ImportState(incoming); saved=true; w.Close();
     } catch(Exception ex) { MessageBox.Show(w,UI.T(ex.Message),UI.T("Import failed")); }
    })); p.Children.Add(row);
    var local=UI.Label("STAYS ON THIS PC",11,UI.Accent); local.Margin=new Thickness(0,20,0,8); p.Children.Add(local); p.Children.Add(UI.Label(Storage.FilePath+"\n"+UI.T("No account, subscriptions, analytics, or network access. Share the app ZIP with friends; your data stays here."),12,UI.Muted)); p.Children.Add(UI.Button("Close",()=>w.Close()));
   };
   render();
   w.Closed+=(s,e)=> { settingsOpen=false; UI.Apply(State); if(saved) { BuildView(); UpdateTrayLanguage(); } };
   settingsOpen=true; w.ShowDialog();
  }
  public void ImportState(State incoming) {
   Schedule.Validate(incoming); Directory.CreateDirectory(Storage.Folder);
   File.WriteAllText(System.IO.Path.Combine(Storage.Folder,"before-import-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json"),Storage.Serializer().Serialize(State));
   incoming.Pinned=State.Pinned; incoming.Compact=State.Compact; incoming.Left=State.Left; incoming.Top=State.Top; incoming.Notifications=State.Notifications; incoming.Sound=State.Sound; incoming.Theme=State.Theme; incoming.Language=State.Language; incoming.WeekView=State.WeekView; incoming.CardStyle=State.CardStyle; incoming.DayWidth=State.DayWidth; incoming.DayHeight=State.DayHeight; incoming.WeekWidth=State.WeekWidth; incoming.WeekHeight=State.WeekHeight; incoming.CustomThemes=State.CustomThemes.Concat(incoming.CustomThemes).GroupBy(t=>t.Id).Select(g=>g.First()).ToList();
   var previous=State; State=incoming; try { Storage.Save(State); } catch { State=previous; throw; }
  }
 }
}
