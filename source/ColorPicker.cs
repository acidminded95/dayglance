using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Dayglance {
 // Color math shared by the picker, the theme creator and the activity editor.
 public static partial class Palette {
  static double Clamp01(double v) { return Math.Max(0,Math.Min(1,v)); }
  static byte ToByte(double v) { return (byte)Math.Round(Clamp01(v)*255); }
  public static string Normalize(string text) {
   text=(text??"").Trim(); if(!text.StartsWith("#")) text="#"+text;
   if(System.Text.RegularExpressions.Regex.IsMatch(text,"^#[0-9a-fA-F]{3}$")) text="#"+text[1]+text[1]+text[2]+text[2]+text[3]+text[3];
   return System.Text.RegularExpressions.Regex.IsMatch(text,"^#[0-9a-fA-F]{6}$")?text.ToUpperInvariant():null;
  }
  public static void ToHsv(Color c,out double h,out double s,out double v) {
   double r=c.R/255.0,g=c.G/255.0,b=c.B/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
   h=0; if(d>0) { if(max==r) h=60*(((g-b)/d)%6); else if(max==g) h=60*((b-r)/d+2); else h=60*((r-g)/d+4); } if(h<0) h+=360;
   s=max<=0?0:d/max; v=max;
  }
  public static Color FromHsv(double h,double s,double v) {
   h=((h%360)+360)%360; s=Clamp01(s); v=Clamp01(v); double c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c,r=0,g=0,b=0;
   if(h<60){r=c;g=x;} else if(h<120){r=x;g=c;} else if(h<180){g=c;b=x;} else if(h<240){g=x;b=c;} else if(h<300){r=x;b=c;} else {r=c;b=x;}
   return Color.FromRgb(ToByte(r+m),ToByte(g+m),ToByte(b+m));
  }
  public static void ToHsl(Color c,out double h,out double s,out double l) {
   double r=c.R/255.0,g=c.G/255.0,b=c.B/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min,sv,vv;
   l=(max+min)/2; ToHsv(c,out h,out sv,out vv); s=d<=0?0:d/(1-Math.Abs(2*l-1));
  }
  public static Color FromHsl(double h,double s,double l) {
   h=((h%360)+360)%360; s=Clamp01(s); l=Clamp01(l); double chroma=(1-Math.Abs(2*l-1))*s,x=chroma*(1-Math.Abs(h/60%2-1)),m=l-chroma/2,r=0,g=0,b=0;
   if(h<60){r=chroma;g=x;} else if(h<120){r=x;g=chroma;} else if(h<180){g=chroma;b=x;} else if(h<240){g=x;b=chroma;} else if(h<300){r=x;b=chroma;} else {r=chroma;b=x;}
   return Color.FromRgb(ToByte(r+m),ToByte(g+m),ToByte(b+m));
  }
  public static bool IsDark(string background) { return Contrast(background,"#FFFFFF")>Contrast(background,"#151515"); }
  public static string SuggestText(string background) {
   double h,s,l; ToHsl(Parse(background),out h,out s,out l); bool dark=IsDark(background);
   string text=Hex(FromHsl(h,Math.Min(s,dark?.30:.45),dark?.93:.17));
   return Contrast(background,text)>=7?text:TextFor(background);
  }
  public static string SuggestSurface(string background) {
   var bg=Parse(background);
   if(IsDark(background)) return Hex(Mix(bg,Colors.White,.06));
   string lighter=Hex(Mix(bg,Colors.White,.6)); return Contrast(lighter,background)<1.04?Hex(Mix(bg,Colors.Black,.045)):lighter;
  }
  // Shifts lightness until the color reaches the requested contrast against the background.
  public static string Readable(string color,string background,double target) {
   double h,s,l; ToHsl(Parse(color),out h,out s,out l); bool dark=IsDark(background);
   for(int i=0;i<45 && Contrast(Hex(FromHsl(h,s,l)),background)<target;i++) l=dark?Math.Min(1,l+.02):Math.Max(0,l-.02);
   return Hex(FromHsl(h,s,l));
  }
  public static string[] AccentIdeas(string background,string accent) {
   double h,s,l; ToHsl(Parse(accent),out h,out s,out l); s=Math.Max(.45,s); double target=IsDark(background)?.70:.42;
   var list=new List<string>(); foreach(double delta in new[]{0.0,-30,30,120,150,180,210}) list.Add(Readable(Hex(FromHsl(h+delta,s,target)),background,3));
   return list.Distinct().ToArray();
  }
  public class ThemeIdea { public string Name,Background,Surface,Foreground,Accent; }
  public static List<ThemeIdea> Ideas(string seed) {
   double h,s,l; ToHsl(Parse(seed),out h,out s,out l); s=Math.Max(.4,s); var result=new List<ThemeIdea>();
   Action<string,double,double,double,double> add=(name,bgHue,bgSat,bgLight,accentHue)=> {
    string bg=Hex(FromHsl(bgHue,bgSat,bgLight)); string accent=Readable(Hex(FromHsl(accentHue,s,IsDark(bg)?.70:.42)),bg,3.2);
    result.Add(new ThemeIdea { Name=name,Background=bg,Surface=SuggestSurface(bg),Foreground=SuggestText(bg),Accent=accent });
   };
   add("Dark",h,.28,.10,h); add("Light",h,.32,.95,h); add("Contrast",h+180,.30,.11,h); add("Soft",h+30,.24,.92,h+180);
   return result;
  }
  public static string Random() { var r=new System.Random(); return Hex(FromHsl(r.Next(360),.55+r.NextDouble()*.3,.55)); }
 }

 // Saturation/value square + hue strip + hex box + optional swatches. Reused by every color selector.
 public class ColorPicker : StackPanel {
  public event Action<string> Changed;
  const double W=236,H=150;
  double hue,sat,val; string current; bool typing;
  readonly Rectangle hueFill; readonly Ellipse svThumb; readonly Border hueThumb,preview; readonly TextBox hex; readonly Grid svArea,hueArea;
  public string Value { get { return current; } set { var v=Palette.Normalize(value); if(v==null) return; SetFrom(v); Sync(true); } }
  public ColorPicker(string initial,IEnumerable<string> swatches) {
   Width=W;
   svArea=new Grid { Width=W,Height=H,Cursor=Cursors.Cross,Background=Brushes.Transparent };
   hueFill=new Rectangle { RadiusX=8,RadiusY=8 }; svArea.Children.Add(hueFill);
   svArea.Children.Add(new Rectangle { RadiusX=8,RadiusY=8,Fill=new LinearGradientBrush(Colors.White,Color.FromArgb(0,255,255,255),0) });
   svArea.Children.Add(new Rectangle { RadiusX=8,RadiusY=8,Fill=new LinearGradientBrush(Color.FromArgb(0,0,0,0),Colors.Black,90) });
   var svCanvas=new Canvas { IsHitTestVisible=false }; svThumb=new Ellipse { Width=14,Height=14,Stroke=Brushes.White,StrokeThickness=2,Fill=Brushes.Transparent }; svCanvas.Children.Add(new Ellipse { Width=16,Height=16,Stroke=new SolidColorBrush(Color.FromArgb(110,0,0,0)),StrokeThickness=1 }); svCanvas.Children.Add(svThumb); svArea.Children.Add(svCanvas);
   svArea.MouseLeftButtonDown+=(s,e)=> { svArea.CaptureMouse(); PickSv(e.GetPosition(svArea)); e.Handled=true; };
   svArea.MouseMove+=(s,e)=> { if(svArea.IsMouseCaptured) PickSv(e.GetPosition(svArea)); };
   svArea.MouseLeftButtonUp+=(s,e)=>svArea.ReleaseMouseCapture();
   Children.Add(svArea);
   hueArea=new Grid { Width=W,Height=18,Margin=new Thickness(0,10,0,0),Cursor=Cursors.Hand,Background=Brushes.Transparent };
   var rainbow=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,0) }; for(int i=0;i<=6;i++) rainbow.GradientStops.Add(new GradientStop(Palette.FromHsv(i*60,1,1),i/6.0));
   hueArea.Children.Add(new Rectangle { Height=12,RadiusX=6,RadiusY=6,Fill=rainbow,VerticalAlignment=VerticalAlignment.Center });
   var hueCanvas=new Canvas { IsHitTestVisible=false }; hueThumb=new Border { Width=8,Height=18,CornerRadius=new CornerRadius(4),BorderBrush=Brushes.White,BorderThickness=new Thickness(2),Background=Brushes.Transparent }; hueCanvas.Children.Add(hueThumb); hueArea.Children.Add(hueCanvas);
   hueArea.MouseLeftButtonDown+=(s,e)=> { hueArea.CaptureMouse(); PickHue(e.GetPosition(hueArea)); e.Handled=true; };
   hueArea.MouseMove+=(s,e)=> { if(hueArea.IsMouseCaptured) PickHue(e.GetPosition(hueArea)); };
   hueArea.MouseLeftButtonUp+=(s,e)=>hueArea.ReleaseMouseCapture();
   Children.Add(hueArea);
   var row=new DockPanel { Margin=new Thickness(0,12,0,0) }; preview=new Border { Width=34,Height=34,CornerRadius=new CornerRadius(8),BorderBrush=UI.Line,BorderThickness=new Thickness(1) }; row.Children.Add(preview);
   hex=UI.Input(""); hex.Margin=new Thickness(10,0,0,0); hex.FontFamily=new FontFamily("Consolas"); hex.MaxLength=7; hex.VerticalContentAlignment=VerticalAlignment.Center; row.Children.Add(hex);
   hex.TextChanged+=(s,e)=> { if(typing) return; var v=Palette.Normalize(hex.Text); if(v==null||v==current) return; SetFrom(v); Sync(false); Raise(); };
   Children.Add(row);
   var list=(swatches??new string[0]).Select(Palette.Normalize).Where(x=>x!=null).Distinct().ToList();
   if(list.Count>0) {
    var wrap=new WrapPanel { Margin=new Thickness(0,10,0,0),Width=W };
    foreach(string sw in list) { string choice=sw; var chip=new Border { Width=22,Height=22,CornerRadius=new CornerRadius(6),Margin=new Thickness(0,0,6,6),Background=UI.B(sw),BorderBrush=UI.Line,BorderThickness=new Thickness(1),Cursor=Cursors.Hand,ToolTip=sw }; chip.MouseLeftButtonUp+=(s,e)=> { SetFrom(choice); Sync(true); Raise(); }; wrap.Children.Add(chip); }
    Children.Add(wrap);
   }
   SetFrom(Palette.Normalize(initial)??"#A4E9CC"); Sync(true);
  }
  void SetFrom(string v) { double h,s,b; Palette.ToHsv(Palette.Parse(v),out h,out s,out b); if(s>0 && b>0) hue=h; sat=s; val=b; current=v; }
  void PickSv(Point p) { sat=Math.Max(0,Math.Min(1,p.X/W)); val=1-Math.Max(0,Math.Min(1,p.Y/H)); current=Palette.Hex(Palette.FromHsv(hue,sat,val)); Sync(true); Raise(); }
  void PickHue(Point p) { hue=Math.Max(0,Math.Min(359.9,p.X/W*360)); current=Palette.Hex(Palette.FromHsv(hue,sat,val)); Sync(true); Raise(); }
  void Sync(bool text) {
   hueFill.Fill=new SolidColorBrush(Palette.FromHsv(hue,1,1)); Canvas.SetLeft(svThumb,sat*W-7); Canvas.SetTop(svThumb,(1-val)*H-7);
   var ring=(Ellipse)((Canvas)svThumb.Parent).Children[0]; Canvas.SetLeft(ring,sat*W-8); Canvas.SetTop(ring,(1-val)*H-8);
   Canvas.SetLeft(hueThumb,hue/360*W-4); preview.Background=UI.B(current);
   if(text) { typing=true; hex.Text=current; typing=false; }
  }
  void Raise() { var handler=Changed; if(handler!=null) handler(current); }
  // Opens a picker in a light-dismiss popup under the target. Changes are reported live.
  public static Popup Show(FrameworkElement target,string initial,IEnumerable<string> swatches,Action<string> changed) {
   var picker=new ColorPicker(initial,swatches); picker.Changed+=changed;
   var popup=new Popup { PlacementTarget=target,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,Child=new Border { LayoutTransform=new ScaleTransform(UI.Scale,UI.Scale),Child=picker,Background=UI.Card,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(12),Margin=new Thickness(0,4,0,0) } };
   popup.KeyDown+=(s,e)=> { if(e.Key==Key.Escape || e.Key==Key.Enter) { popup.IsOpen=false; e.Handled=true; } };
   popup.IsOpen=true; return popup;
  }
 }

 // A compact swatch + hex button that opens the shared picker.
 public class ColorField : Border {
  public event Action<string> Changed;
  public List<string> Swatches=new List<string>();
  readonly Border swatch; readonly TextBlock label; readonly Button button; string value;
  public string Value { get { return value; } set { var v=Palette.Normalize(value); if(v==null||v==this.value) return; this.value=v; Draw(); var handler=Changed; if(handler!=null) handler(v); } }
  public ColorField(string initial) {
   Margin=new Thickness(0,2,0,10);
   var row=UI.Row(); swatch=new Border { Width=22,Height=22,CornerRadius=new CornerRadius(6),BorderBrush=UI.Line,BorderThickness=new Thickness(1),Margin=new Thickness(0,0,10,0) };
   label=new TextBlock { FontFamily=new FontFamily("Consolas"),FontSize=13,VerticalAlignment=VerticalAlignment.Center,Foreground=UI.Text };
   row.Children.Add(swatch); row.Children.Add(label);
   button=UI.Button("",Open); button.Content=row; button.HorizontalAlignment=HorizontalAlignment.Stretch; Child=button;
   value=Palette.Normalize(initial)??"#A4E9CC"; Draw();
  }
  void Draw() { swatch.Background=UI.B(value); label.Text=value; }
  void Open() { ColorPicker.Show(button,value,Swatches,v=>Value=v); }
 }
}
