using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Dayglance {
 public class DialogWindow : Window {
  readonly Border body=new Border();
  public bool LightDismiss; // closes (without saving) when the owner window is clicked while this dialog is active
  DockPanel layout;
  public new object Content { get { return body.Child; } set { body.Child=(UIElement)value; } }
  public DialogWindow() {
   WindowStyle=WindowStyle.None; AllowsTransparency=true; KeyDown+=(s,e)=> { if(e.Key==Key.Escape) Close(); }; UI.EnableBorderResize(this);
   Restyle();
  }
  // Rebuilds the themed frame around the current body using the currently applied UI colors.
  public void Restyle() {
   Background=Brushes.Transparent; Foreground=UI.Text; if(layout!=null) layout.Children.Remove(body);
   layout=new DockPanel(); var head=new Grid { Margin=new Thickness(18,12,12,4),Background=Brushes.Transparent }; head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
   var title=UI.Label("",12,UI.Muted); title.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Title") { Source=this }); title.VerticalAlignment=VerticalAlignment.Center; head.Children.Add(title);
   head.MouseLeftButtonDown+=(s,e)=> { if(e.LeftButton==MouseButtonState.Pressed) DragMove(); }; var close=UI.Button("×",()=>Close()); close.ToolTip=UI.T("Close"); Grid.SetColumn(close,1); head.Children.Add(close); DockPanel.SetDock(head,Dock.Top); layout.Children.Add(head); layout.Children.Add(body);
   base.Content=new Border { LayoutTransform=new ScaleTransform(UI.Scale,UI.Scale),Child=layout,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Background=UI.Bg };
  }
 }
 public class TimeField : StackPanel {
  Choice hours=new Choice(),minutes=new Choice();
  public string Value {
   get { return hours.Items[hours.SelectedIndex]+":"+minutes.Items[minutes.SelectedIndex]; }
   set { var time=Schedule.Time(value); hours.SelectedIndex=time.Hours; string minute=time.Minutes.ToString("00"); if(!minutes.Items.Contains(minute)) { minutes.Items.Add(minute); minutes.Items.Sort(); } minutes.SelectedIndex=minutes.Items.IndexOf(minute); }
  }
  public TimeField(string value) {
   Orientation=Orientation.Horizontal; hours.Width=78; minutes.Width=78;
   hours.Items.AddRange(Enumerable.Range(0,24).Select(n=>n.ToString("00"))); minutes.Items.AddRange(Enumerable.Range(0,12).Select(n=>(n*5).ToString("00")));
   Children.Add(hours); var colon=UI.Label(":",18,UI.Muted); colon.Margin=new Thickness(0,6,5,0); Children.Add(colon); Children.Add(minutes); Value=value;
  }
 }
 public static partial class Palette {
  public static string Hex(Color c) { return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2"); }
  public static Color Parse(string text) { if(!System.Text.RegularExpressions.Regex.IsMatch(text??"","^#[0-9a-fA-F]{6}$")) throw new Exception("Use a hex color such as #FF8C24."); return (Color)ColorConverter.ConvertFromString(text); }
  static double Channel(byte b) { double v=b/255.0; return v<=0.04045?v/12.92:Math.Pow((v+0.055)/1.055,2.4); }
  public static double Light(Color c) { return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B); }
  public static double Contrast(string a,string b) { double x=Light(Parse(a)),y=Light(Parse(b)); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
  public static Color Mix(Color a,Color b,double amount) { return Color.FromRgb((byte)(a.R*(1-amount)+b.R*amount),(byte)(a.G*(1-amount)+b.G*amount),(byte)(a.B*(1-amount)+b.B*amount)); }
  public static string TextFor(string background) { return Contrast(background,"#FFFFFF")>Contrast(background,"#151515")?"#FFFFFF":"#151515"; }
  public static Theme Generate(string name,string background,string accent) {
   var bg=Parse(background); bool dark=Light(bg)<.3; return Generate(name,background,Hex(Mix(bg,Colors.White,dark?.055:.45)),TextFor(background),accent);
  }
  public static Theme Generate(string name,string background,string surface,string foreground,string accent) {
   var bg=Parse(background); var fg=Parse(foreground); var ac=Parse(accent); var card=Parse(surface);
   string muted=Hex(Mix(bg,fg,.7)); if(Contrast(background,muted)<4.5) muted=Hex(fg);
   return new Theme("custom-"+Guid.NewGuid().ToString("N"),name,Hex(bg),Hex(card),Hex(fg),muted,Hex(ac),Hex(Mix(bg,ac,.14)),Hex(Mix(bg,fg,.24)));
  }
  public static string[] Suggestions(string accent) {
   var c=Parse(accent); var draw=System.Drawing.Color.FromArgb(c.R,c.G,c.B); double h=draw.GetHue(),s=Math.Max(.3,draw.GetSaturation()),l=Math.Max(.35,Math.Min(.65,draw.GetBrightness())); return new[]{-30.0,30,150,180}.Select(delta=>Hsl((h+delta+360)%360,s,l)).ToArray();
  }
  static string Hsl(double h,double s,double l) {
   double chroma=(1-Math.Abs(2*l-1))*s,x=chroma*(1-Math.Abs(h/60%2-1)),m=l-chroma/2,r=0,g=0,b=0;
   if(h<60){r=chroma;g=x;} else if(h<120){r=x;g=chroma;} else if(h<180){g=chroma;b=x;} else if(h<240){g=x;b=chroma;} else if(h<300){r=x;b=chroma;} else {r=chroma;b=x;}
   return Hex(Color.FromRgb((byte)((r+m)*255),(byte)((g+m)*255),(byte)((b+m)*255)));
  }
 }
 public partial class MainWindow {
  // Theme creator: four user-controlled colors (background, cards, text, accent) with live preview,
  // one-click palette ideas, per-color suggestions and contrast guidance. Derived colors (highlight,
  // secondary text, lines) are generated from those four.
  void CreateTheme(Window owner,Theme editing,Action<Theme> saved) {
   var window=UI.Dialog(owner,editing==null?"Create a theme":"Edit theme",540,780); var p=new StackPanel { Margin=new Thickness(22,6,22,22) }; window.Content=new ScrollViewer { Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
   var basis=editing??UI.AvailableThemes(State).FirstOrDefault(t=>t.Id==State.Theme)??UI.Themes[0];
   p.Children.Add(UI.Label("Theme name",12,UI.Muted)); var name=UI.Input(editing==null?UI.T("My theme"):editing.Name); name.MaxLength=40; p.Children.Add(name);
   var previewBox=new Border { Padding=new Thickness(14),CornerRadius=new CornerRadius(12),Margin=new Thickness(0,0,0,14),BorderThickness=new Thickness(1) }; p.Children.Add(previewBox);
   var ideasTitle=UI.Label("Start from an idea",12,UI.Muted); p.Children.Add(ideasTitle); var ideas=new WrapPanel { Margin=new Thickness(0,0,0,10) }; p.Children.Add(ideas);
   var fieldGrid=new Grid { Margin=new Thickness(0,4,0,0) }; fieldGrid.ColumnDefinitions.Add(new ColumnDefinition()); fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(14) }); fieldGrid.ColumnDefinitions.Add(new ColumnDefinition());
   for(int i=0;i<4;i++) { fieldGrid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); fieldGrid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); }
   var bg=new ColorField(basis.Background); var surface=new ColorField(basis.Surface); var text=new ColorField(basis.Foreground); var accent=new ColorField(basis.Accent);
   var linked=UI.Switch("Match cards and text to the background",editing==null); bool linking=false;
   Action<string,UIElement,int,int,Action> place=(label,field,row,column,suggest)=> {
    var head=new DockPanel(); var title=UI.Label(label,12,UI.Muted); title.Margin=new Thickness(0,4,0,2);
    if(suggest!=null) { var link=new TextBlock { Text=UI.T("Suggest"),FontSize=11,Foreground=UI.Accent,Cursor=Cursors.Hand,Margin=new Thickness(0,4,0,2),TextDecorations=TextDecorations.Underline }; link.MouseLeftButtonUp+=(s,e)=>suggest(); DockPanel.SetDock(link,Dock.Right); head.Children.Add(link); }
    head.Children.Add(title); Grid.SetRow(head,row*2); Grid.SetColumn(head,column); fieldGrid.Children.Add(head); Grid.SetRow(field,row*2+1); Grid.SetColumn(field,column); fieldGrid.Children.Add(field);
   };
   place("Background",bg,0,0,null);
   place("Accent color",accent,0,2,()=> { try { accent.Value=Palette.Readable(accent.Value,bg.Value,3.2); } catch {} });
   place("Cards",surface,1,0,()=> { try { linking=true; surface.Value=Palette.SuggestSurface(bg.Value); } catch {} finally { linking=false; } });
   place("Text",text,1,2,()=> { try { linking=true; text.Value=Palette.SuggestText(bg.Value); } catch {} finally { linking=false; } });
   p.Children.Add(fieldGrid); p.Children.Add(linked);
   p.Children.Add(UI.Label("Suggested accents",12,UI.Muted)); var suggestions=new WrapPanel(); p.Children.Add(suggestions);
   var guidance=UI.Label("",12,UI.Muted); guidance.Margin=new Thickness(0,12,0,12); p.Children.Add(guidance);
   Action update=null;
   Action<Palette.ThemeIdea> useIdea=idea=> { linking=true; try { bg.Value=idea.Background; surface.Value=idea.Surface; text.Value=idea.Foreground; accent.Value=idea.Accent; } finally { linking=false; } update(); };
   update=()=> {
    try {
     var theme=Palette.Generate(name.Text,bg.Value,surface.Value,text.Value,accent.Value);
     // Mini widget preview built from the generated theme only.
     var sample=new StackPanel(); var top=new DockPanel(); var plus=new Border { Width=26,Height=26,CornerRadius=new CornerRadius(7),Background=UI.B(theme.Accent),Child=new TextBlock { Text="+",FontSize=16,Foreground=UI.Ink(theme.Accent),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } }; DockPanel.SetDock(plus,Dock.Right); top.Children.Add(plus);
     var brand=UI.Label("◉  dayglance",14,UI.B(theme.Foreground)); brand.Margin=new Thickness(0); brand.FontWeight=FontWeights.SemiBold; top.Children.Add(brand); sample.Children.Add(top);
     var hp=new StackPanel(); hp.Children.Add(UI.Label(UI.T("RIGHT NOW"),10,UI.B(theme.Accent))); var heading=UI.Label(UI.T("Make it yours."),18,UI.B(theme.Foreground)); heading.Margin=new Thickness(0,0,0,2); hp.Children.Add(heading); hp.Children.Add(UI.Label("09:00 – 10:00",11,UI.B(theme.Muted)));
     sample.Children.Add(new Border { Child=hp,Background=UI.B(theme.Hero),CornerRadius=new CornerRadius(10),Padding=new Thickness(12),Margin=new Thickness(0,10,0,8) });
     var cardRow=new Grid(); cardRow.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(8) }); cardRow.ColumnDefinitions.Add(new ColumnDefinition()); cardRow.Children.Add(new Border { Width=3,CornerRadius=new CornerRadius(2),Background=UI.B(theme.Accent),HorizontalAlignment=HorizontalAlignment.Left });
     var cardText=new StackPanel(); var cardTitle=UI.Label(UI.T("Sample activity"),13,UI.B(theme.Foreground)); cardTitle.Margin=new Thickness(0); cardText.Children.Add(cardTitle); var cardTime=UI.Label("10:30 – 11:15",11,UI.B(theme.Muted)); cardTime.Margin=new Thickness(0); cardText.Children.Add(cardTime); Grid.SetColumn(cardText,1); cardRow.Children.Add(cardText);
     sample.Children.Add(new Border { Child=cardRow,Background=UI.B(theme.Surface),BorderBrush=UI.B(theme.Line),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(10) });
     previewBox.Background=UI.B(theme.Background); previewBox.BorderBrush=UI.B(theme.Line); previewBox.Child=sample;
     ideas.Children.Clear();
     foreach(var idea in Palette.Ideas(theme.Accent)) {
      var current=idea; var strip=UI.Row(); foreach(string hex in new[]{idea.Background,idea.Surface,idea.Foreground,idea.Accent}) strip.Children.Add(new Border { Width=18,Height=22,Background=UI.B(hex) });
      var tileContent=new StackPanel(); tileContent.Children.Add(new Border { Child=strip,CornerRadius=new CornerRadius(6),ClipToBounds=true,BorderBrush=UI.Line,BorderThickness=new Thickness(1) }); var ideaName=UI.Label(idea.Name,11,UI.Muted); ideaName.Margin=new Thickness(0,4,0,0); ideaName.TextAlignment=TextAlignment.Center; tileContent.Children.Add(ideaName);
      var tile=new Border { Child=tileContent,Margin=new Thickness(0,0,10,6),Cursor=Cursors.Hand,ToolTip=UI.T("Use this palette") }; tile.MouseLeftButtonUp+=(s,e)=>useIdea(current); ideas.Children.Add(tile);
     }
     var surprise=UI.Button("Surprise me",()=> { var pool=Palette.Ideas(Palette.Random()); useIdea(pool[new Random().Next(pool.Count)]); }); surprise.VerticalAlignment=VerticalAlignment.Top; ideas.Children.Add(surprise);
     suggestions.Children.Clear();
     foreach(string hex in Palette.AccentIdeas(theme.Background,theme.Accent)) { string choice=hex; var b=UI.Button(hex==theme.Accent?"✓":"",()=>accent.Value=choice); b.Background=UI.B(hex); b.Foreground=UI.Ink(hex); b.ToolTip=hex; b.Width=46; b.Margin=new Thickness(0,0,6,6); suggestions.Children.Add(b); }
     Func<double,double,string> rate=(value,min)=>value.ToString("0.0")+":1 "+(value>=min?"✓":"⚠");
     guidance.Text=(UI.Language=="es"?"Texto sobre fondo: ":"Text on background: ")+rate(Palette.Contrast(theme.Background,theme.Foreground),4.5)+"   ·   "+(UI.Language=="es"?"Texto sobre tarjetas: ":"Text on cards: ")+rate(Palette.Contrast(theme.Surface,theme.Foreground),4.5)+"\n"+(UI.Language=="es"?"Acento sobre fondo: ":"Accent on background: ")+rate(Palette.Contrast(theme.Background,theme.Accent),3)+"\n"+UI.T("At least 4.5:1 is recommended for small text.");
     foreach(var f in new[]{bg,surface,text,accent}) f.Swatches=new[]{theme.Background,theme.Surface,theme.Foreground,theme.Accent,theme.Hero,theme.Muted}.Concat(Palette.AccentIdeas(theme.Background,theme.Accent)).ToList();
    } catch(Exception ex) { guidance.Text=UI.T(ex.Message); }
   };
   bg.Changed+=v=> { if(linked.IsChecked==true && !linking) { linking=true; try { surface.Value=Palette.SuggestSurface(v); text.Value=Palette.SuggestText(v); } finally { linking=false; } } update(); };
   surface.Changed+=v=> { if(!linking) linked.IsChecked=false; update(); };
   text.Changed+=v=> { if(!linking) linked.IsChecked=false; update(); };
   accent.Changed+=v=>update(); name.TextChanged+=(s,e)=>update(); update();
   var buttons=UI.Row(); buttons.Children.Add(UI.Button("Save theme",()=> {
    try {
     if(string.IsNullOrWhiteSpace(name.Text)||name.Text.Trim().Length>40) throw new Exception("Choose a name (1–40 characters).");
     if(editing==null && State.CustomThemes.Count>=24) throw new Exception("Up to 24 custom themes are supported.");
     var theme=Palette.Generate(name.Text.Trim(),bg.Value,surface.Value,text.Value,accent.Value); if(editing!=null) theme.Id=editing.Id; saved(theme); window.Close();
    } catch(Exception ex) { guidance.Text=UI.T(ex.Message); }
   },true)); buttons.Children.Add(UI.Button("Cancel",()=>window.Close())); p.Children.Add(buttons); window.ShowDialog();
  }
 }
 public static class BrandIcon {
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
  public static System.Drawing.Icon Make() {
   using(var bmp=new System.Drawing.Bitmap(32,32)) using(var g=System.Drawing.Graphics.FromImage(bmp)) using(var pen=new System.Drawing.Pen(System.Drawing.ColorTranslator.FromHtml(UI.Accent.ToString()),3)) {
    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.Clear(System.Drawing.ColorTranslator.FromHtml(UI.Bg.ToString())); g.DrawEllipse(pen,4,4,24,24); g.DrawLine(pen,16,8,16,16); g.DrawLine(pen,16,16,22,19); var handle=bmp.GetHicon(); try { using(var icon=System.Drawing.Icon.FromHandle(handle)) return (System.Drawing.Icon)icon.Clone(); } finally { DestroyIcon(handle); }
   }
  }
  public static UIElement Visual() {
   var canvas=new Canvas { Width=38,Height=38,Margin=new Thickness(0,0,12,0) }; canvas.Children.Add(new Ellipse { Width=32,Height=32,Stroke=UI.Accent,StrokeThickness=3,Margin=new Thickness(3) }); canvas.Children.Add(new Line { X1=19,Y1=10,X2=19,Y2=19,Stroke=UI.Accent,StrokeThickness=3 }); canvas.Children.Add(new Line { X1=19,Y1=19,X2=27,Y2=23,Stroke=UI.Accent,StrokeThickness=3 }); return canvas;
  }
 }
 public static class Chime {
  static MemoryStream stream; static System.Media.SoundPlayer player;
  public static void Prepare() {
   if(player==null) {
    int rate=22050,count=(int)(rate*.65); stream=new MemoryStream(); var writer=new BinaryWriter(stream); writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+count*2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate*2); writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count*2);
    for(int n=0;n<count;n++) { double t=n/(double)rate; double first=Math.Sin(2*Math.PI*659.25*t)*Math.Exp(-7*t)*(1-Math.Exp(-80*t)); double u=Math.Max(0,t-.16); double second=t<.16?0:Math.Sin(2*Math.PI*880*u)*Math.Exp(-9*u)*(1-Math.Exp(-90*u)); writer.Write((short)(6500*(first+second))); } stream.Position=0; player=new System.Media.SoundPlayer(stream); player.Load();
   }
  }
  public static void Play() { Prepare(); player.Play(); }
 }
 public class ReminderToast : Window {
  static List<ReminderToast> visible=new List<ReminderToast>();
  public ReminderToast(string title,string detail,Action open,bool sound) {
   Width=370*UI.Scale; SizeToContent=SizeToContent.Height; WindowStyle=WindowStyle.None; AllowsTransparency=true; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; ShowActivated=false; Topmost=true; Background=Brushes.Transparent;
   var panel=new StackPanel(); var top=new DockPanel(); var dismiss=UI.Button("×",()=>Close()); dismiss.ToolTip=UI.T("Dismiss"); DockPanel.SetDock(dismiss,Dock.Right); top.Children.Add(dismiss); top.Children.Add(BrandIcon.Visual()); var brand=UI.Label("dayglance",14,UI.Text); brand.VerticalAlignment=VerticalAlignment.Center; top.Children.Add(brand); panel.Children.Add(top);
   panel.Children.Add(UI.Label(title,20,UI.Text)); panel.Children.Add(UI.Label(detail,13,UI.Muted)); panel.Children.Add(UI.Button("Open Dayglance",()=> { open(); Close(); },true));
   Content=new Border { LayoutTransform=new ScaleTransform(UI.Scale,UI.Scale),Child=panel,Background=UI.Hero,BorderBrush=UI.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(18) };
   while(visible.Count>=3) visible[0].Close(); visible.Add(this); Closed+=(s,e)=> { visible.Remove(this); Place(); };
   var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(18) }; timer.Tick+=(s,e)=> { timer.Stop(); Close(); }; Closed+=(s,e)=>timer.Stop(); Loaded+=(s,e)=>Place(); Show(); timer.Start(); if(sound) Chime.Play();
  }
  static void Place() { double bottom=SystemParameters.WorkArea.Bottom-16; foreach(var toast in visible.AsEnumerable().Reverse()) { toast.Left=SystemParameters.WorkArea.Right-toast.Width-16; toast.Top=bottom-toast.ActualHeight; bottom=toast.Top-12; } }
 }
}
