using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace Dayglance {
 public class Theme {
  public Theme() {}
  public string Id,Name,Background,Surface,Foreground,Muted,Accent,Hero,Line;
  public Theme(string id,string name,string bg,string card,string text,string muted,string accent,string hero,string line) { Id=id; Name=name; Background=bg; Surface=card; Foreground=text; Muted=muted; Accent=accent; Hero=hero; Line=line; }
 }
 public static partial class UI {
  public static readonly Theme[] Themes={
   new Theme("midnight","Midnight mint","#11151D","#1C2330","#EDF2FA","#A4B0C4","#A4E9CC","#202D32","#354052"),
   new Theme("terracotta","Terracotta","#F4E8DE","#FFF8F1","#314D3D","#6B6251","#9C442E","#D4DFCA","#B5BBA2"),
   new Theme("midcentury","Midcentury","#142D27","#203E34","#F5ECD8","#C1CCB4","#FFAE65","#304A37","#496450"),
   new Theme("paper","Horario · paper & sage","#F5F1EA","#FFFDF9","#294B3D","#74634D","#365D4B","#F0DDE1","#CDB5BC"),
   new Theme("lavender","Lavender dusk","#211F33","#302B46","#F2EDFF","#BDB2D5","#D5BEFF","#3B3151","#574B6D"),
   new Theme("cyberpunk","Cyberpunk","#080808","#17110D","#FFF2DF","#C6A98D","#FF8C24","#33200F","#704118")
  };
  public static Brush Hero=B("#202D32"), Line=B("#354052");
  public static string Language="en";
  public static Theme[] AvailableThemes(State state) { return Themes.Concat(state.CustomThemes??new List<Theme>()).ToArray(); }
  public static Brush AccentInk { get { return Ink(Accent.ToString()); } }
  public static CultureInfo Culture { get { return CultureInfo.GetCultureInfo(Language=="es"?"es-MX":"en-US"); } }
  public static void Apply(State state) {
   Language=state.Language; var theme=AvailableThemes(state).FirstOrDefault(t=>t.Id==state.Theme)??Themes[0];
   Bg=B(theme.Background); Card=B(theme.Surface); Text=B(theme.Foreground); Muted=B(theme.Muted); Accent=B(theme.Accent); Hero=B(theme.Hero); Line=B(theme.Line);
   if(Application.Current!=null) Application.Current.Resources[typeof(ScrollBar)]=(Style)XamlReader.Parse(ScrollBarStyle(Line.ToString()));
  }
  static string ScrollBarStyle(string thumb) {
   Func<string,string,string,string,string> template=(orientation,reversed,before,after)=>"<ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='"+reversed+"' Orientation='"+orientation+"'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar."+before+"' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='"+thumb+"' CornerRadius='4' Margin='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar."+after+"' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>";
   return "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'><Setter Property='Width' Value='8'/><Setter Property='MinWidth' Value='8'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value>"+template("Vertical","True","PageUpCommand","PageDownCommand")+"</Setter.Value></Setter>"
    +"<Style.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='MinWidth' Value='0'/><Setter Property='Height' Value='8'/><Setter Property='MinHeight' Value='8'/><Setter Property='Template'><Setter.Value>"+template("Horizontal","False","PageLeftCommand","PageRightCommand")+"</Setter.Value></Setter></Trigger></Style.Triggers></Style>";
  }
  static Dictionary<string,string> Spanish=new Dictionary<string,string> {
   {"+ Activity","+ Actividad"},{"Manage","Gestionar"},{"Settings","Ajustes"},{"Today","Hoy"},{"Day","Día"},{"Week","Semana"},{"Fit week","Ajustar semana"},{"Detailed","Ampliar"},
   {"● Pinned on top","● Siempre visible"},{"Pin on top","Fijar encima"},{"Expand","Expandir"},{"Compact","Compacto"},
   {"Drag to position your widget","Arrastra para mover el widget"},{"LOCAL BY DESIGN  ·  YOUR TIME, YOUR WAY","EN TU EQUIPO  ·  TU TIEMPO, A TU MANERA"},
   {"RIGHT NOW","AHORA"},{"ROOM TO BREATHE","UN RESPIRO"},{"Make room for your day.","Dale espacio a tu día."},{"You’re between activities.","Tienes un momento libre."},
   {"Add an activity to give your day a little structure.","Añade una actividad para organizar tu día."},{"Next: ","Después: "},{"Also now: ","También ahora: "},{" min left"," min restantes"},
   {"SCHEDULE","HORARIO"},{"ACTIVITIES","ACTIVIDADES"},{"DONE","COMPLETADAS"},{"NOW","AHORA"},{" (+1 day)"," (+1 día)"},
   {"A fresh page.","Un día por organizar."},{"Use + Activity to add something, or Manage to edit your weekly routine.","Pulsa + Actividad para añadir algo o Gestionar para editar tu rutina semanal."},
   {"Mark incomplete","Marcar pendiente"},{"Mark done","Marcar completada"},{"Double-click to edit","Doble clic para editar"},
   {"Add activity","Añadir actividad"},{"Edit activity","Editar actividad"},{"A little structure.","Un poco de estructura."},{"Make it yours.","A tu manera."},
   {"ACTIVITY NAME","NOMBRE DE LA ACTIVIDAD"},{"START / END  ·  24-HOUR TIME (HH:MM)","INICIO / FIN  ·  FORMATO DE 24 HORAS (HH:MM)"},
   {"An earlier end time finishes the following day.","Si el fin es anterior al inicio, termina al día siguiente."},
   {"REPEAT ON  ·  LEAVE EMPTY FOR ONE DATE","REPETIR  ·  SIN DÍAS PARA UNA SOLA FECHA"},{"Every day","Todos los días"},{"Weekdays","Lun–Vie"},{"Once","Una vez"},
   {"COLOR","COLOR"},{"REMINDER","RECORDATORIO"},{"Off","Desactivado"},{"At start","Al comenzar"},{" minutes before"," minutos antes"},
   {"NOTES (OPTIONAL)","NOTAS (OPCIONAL)"},{"Save activity","Guardar actividad"},{"Cancel","Cancelar"},{"Choose date","Elegir fecha"},
   {"Check your details: ","Revisa los datos: "},{"Manage schedule","Gestionar horario"},{"Your routine","Tu rutina"},{"Edit","Editar"},{"Delete","Eliminar"},{"Delete activity","Eliminar actividad"},
   {"No activities yet. Close this window and choose + Activity.","Aún no hay actividades. Cierra esta ventana y pulsa + Actividad."},
   {"Dayglance settings","Ajustes de Dayglance"},{"Set your own pace.","A tu propio ritmo."},{"APPEARANCE","APARIENCIA"},{"LANGUAGE","IDIOMA"},{"REMINDERS & STARTUP","AVISOS E INICIO"},
   {"Midnight mint","Menta nocturna"},{"Terracotta","Terracota"},{"Midcentury","Mediados de siglo"},{"Horario · paper & sage","Horario · papel y salvia"},{"Lavender dusk","Atardecer lavanda"},
   {"Enable activity reminders","Activar recordatorios"},{"Play a sound with reminders","Reproducir un sonido"},{"Start when I sign in to Windows","Abrir al iniciar sesión en Windows"},
   {"Reminders work while Dayglance is running, including in the tray. Windows Do Not Disturb can silence them. The × button hides the widget; right-click the tray icon to quit.","Los avisos funcionan mientras Dayglance esté abierto, incluso en la bandeja. No molestar de Windows puede silenciarlos. × oculta el widget; haz clic derecho en su icono para salir."},
   {"Save preferences","Guardar preferencias"},{"Could not save preferences","No se pudieron guardar los ajustes"},{"BACKUP & SHARING","RESPALDOS Y ARCHIVOS"},
   {"Export your schedule and completion history. Import replaces the current schedule; a backup is saved first.","Exporta tu horario e historial. Importar reemplaza el horario actual; antes se guarda un respaldo."},
   {"Export…","Exportar…"},{"Import…","Importar…"},{"Import schedule","Importar horario"},{"Import failed","Error al importar"},{"STAYS ON THIS PC","SE GUARDA EN ESTE EQUIPO"},{"Close","Cerrar"},
   {"No account, subscriptions, analytics, or network access. Share the app ZIP with friends; your data stays here.","Sin cuenta, suscripciones, analítica ni conexión a internet. Comparte el ZIP; tus datos se quedan aquí."},
   {"Your changes could not be saved.","No se pudieron guardar los cambios."},{"Activities coming up","Próximas actividades"},{"Open Dayglance","Abrir Dayglance"},{"Quit","Salir"},
   {"Show this day","Ver este día"},{"Click an activity to edit. Scroll for more hours.","Haz clic en una actividad para editar. Desplázate para ver más horas."},
   {"This is not a supported Dayglance backup.","Este archivo no es un respaldo compatible con Dayglance."},
   {"An activity has a missing or duplicate ID.","Una actividad tiene un ID duplicado o vacío."},
   {"Activity titles must have 1–120 characters; notes can have up to 2,000.","El nombre debe tener de 1 a 120 caracteres y las notas hasta 2000."},
   {"Start and end times must be different.","Las horas de inicio y fin deben ser diferentes."},{"Invalid repeat days.","Los días de repetición no son válidos."},
   {"A one-time activity needs a date.","Una actividad única necesita una fecha."},{"Invalid reminder time.","El tiempo del aviso no es válido."},{"Invalid activity color.","El color de la actividad no es válido."},
   {"Use HH:MM, between 00:00 and 23:59.","Usa HH:MM, entre 00:00 y 23:59."},{"Backup exceeds 8 MB.","El respaldo supera los 8 MB."},
   {"Create theme…","Crear tema…"},{"Create a theme","Crear un tema"},{"Theme name","Nombre del tema"},{"Background","Fondo"},{"Accent color","Color de acento"},{"Suggested accents","Acentos sugeridos"},{"Save theme","Guardar tema"},{"Color guidance","Guía de colores"},{"Additional reminder","Aviso adicional"},{"minutes","minutos"},{"hours","horas"},{"days","días"},{"before","antes"},{"Dismiss","Cerrar"},{"Starts","Comienza"},{"Use one additional reminder, between 1 minute and 30 days.","Usa un aviso adicional, entre 1 minuto y 30 días."},{"Use a number from 1 to 43200.","Usa un número entre 1 y 43200."},{"Use a hex color such as #FF8C24.","Usa un color hexadecimal, por ejemplo #FF8C24."},{"Choose a name (1–40 characters).","Elige un nombre (1–40 caracteres)."},{"Up to 24 custom themes are supported.","Se admiten hasta 24 temas personalizados."},{"Notification preview","Vista previa del aviso"},{"Test reminder","Probar aviso"},{"Your reminder will look like this.","Así se verá tu recordatorio."},{"At least 4.5:1 is recommended for small text.","Se recomienda al menos 4.5:1 para texto pequeño."},{"Palette generated for readable text and harmonious surfaces.","Paleta generada con texto legible y superficies coordinadas."},{"My theme","Mi tema"},{"Edit theme","Editar tema"},{"Start from an idea","Parte de una idea"},{"Match cards and text to the background","Ajustar tarjetas y texto al fondo"},{"Suggest","Sugerir"},{"Cards","Tarjetas"},{"Text","Texto"},{"Sample activity","Actividad de ejemplo"},{"Use this palette","Usar esta paleta"},{"Surprise me","Sorpréndeme"},{"Dark","Oscuro"},{"Light","Claro"},{"Contrast","Contraste"},{"Soft","Suave"},{"Delete this theme?","¿Eliminar este tema?"},{"Delete theme","Eliminar tema"},{"Edit theme…","Editar tema…"},{"ACTIVITY CARDS","TARJETAS DE ACTIVIDAD"},{"Slim color line","Línea de color"},{"Color band","Franja de color"},{"Full color card","Tarjeta a todo color"},{"Show in schedule","Ver en el horario"},{"Custom color…","Color personalizado…"},{"Minimize","Minimizar"},{"Hide to tray","Ocultar en la bandeja"},{"Previous","Anterior"},{"Next","Siguiente"},{"CURRENT ACTIVITY IN WEEK VIEW","ACTIVIDAD ACTUAL EN LA SEMANA"},{"Time line and dot","Línea de hora y punto"},{"Accent outline","Contorno de acento"},{"Accent outline with glow","Contorno de acento con brillo"},{"Click an activity to edit or an empty slot to add one.","Haz clic en una actividad para editarla o en un espacio libre para añadir una."}
  };
  public static string T(string text) { string result; return Language=="es" && Spanish.TryGetValue(text,out result)?result:text; }
  public static Brush Ink(string hex) { var c=(Color)ColorConverter.ConvertFromString(hex); return B(Palette.TextFor(Palette.Hex(c))); }
  public static CheckBox Chip(string text,bool value) {
   var c=new CheckBox { Content=text,IsChecked=value,Foreground=value?AccentInk:Text,Background=Card,Cursor=System.Windows.Input.Cursors.Hand,Margin=new Thickness(0,4,5,8),MinWidth=43,MinHeight=38 };
   c.Checked+=(s,e)=>c.Foreground=AccentInk; c.Unchecked+=(s,e)=>c.Foreground=Text;
   c.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='CheckBox'><Border x:Name='pill' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='10' Background='{TemplateBinding Background}' BorderBrush='"+Line+"' BorderThickness='1' Padding='7'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='pill' Property='Background' Value='"+Accent+"'/><Setter Property='Foreground' Value='"+Bg+"'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='pill' Property='BorderBrush' Value='"+Accent+"'/><Setter TargetName='pill' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"); return c;
  }
  public static CheckBox Switch(string text,bool value) {
   var c=new CheckBox { Content=T(text),IsChecked=value,Foreground=Text,Cursor=System.Windows.Input.Cursors.Hand,Margin=new Thickness(0,7,0,9),FontSize=13,MinHeight=30 };
   c.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'><DockPanel><Border x:Name='track' DockPanel.Dock='Right' Width='42' Height='24' CornerRadius='12' Background='"+Line+"' Margin='12,0,0,0'><Ellipse x:Name='knob' Fill='"+Text+"' Width='18' Height='18' HorizontalAlignment='Left' Margin='3,0,0,0'/></Border><ContentPresenter VerticalAlignment='Center'/></DockPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='track' Property='Background' Value='"+Accent+"'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='knob' Property='Margin' Value='0,0,3,0'/><Setter TargetName='knob' Property='Fill' Value='"+Bg+"'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='track' Property='BorderThickness' Value='2'/><Setter TargetName='track' Property='BorderBrush' Value='"+Text+"'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"); return c;
  }
 }
 public class Choice : Border {
  internal Popup ActivePopup;
  public event Action Changed;
  public List<string> Items=new List<string>();
  Button button; int index=-1;
  public int SelectedIndex { get { return index; } set { index=value; button.Content=(index>=0 && index<Items.Count?UI.T(Items[index]):"—")+"   ▾"; } }
  public Choice() {
   Margin=new Thickness(0,2,0,12); button=UI.Button("—   ▾",Open); button.HorizontalContentAlignment=HorizontalAlignment.Left; Child=button;
  }
  void Open() {
   var panel=new StackPanel(); var popup=new Popup { PlacementTarget=button,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true }; ActivePopup=popup;
   for(int i=0;i<Items.Count;i++) { int n=i; var item=UI.Button((i==index?"✓  ":"    ")+UI.T(Items[i]),()=> { SelectedIndex=n; popup.IsOpen=false; var handler=Changed; if(handler!=null) handler(); }); item.Margin=new Thickness(0,2,0,2); panel.Children.Add(item); }
   popup.Child=new Border { Child=new ScrollViewer { Content=panel,MaxHeight=280,VerticalScrollBarVisibility=ScrollBarVisibility.Auto },Background=UI.Card,BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(6),MinWidth=Math.Max(100,ActualWidth-6) }; popup.KeyDown+=(s,e)=> { if(e.Key==System.Windows.Input.Key.Escape) popup.IsOpen=false; }; popup.IsOpen=true;
  }
 }
 public class DateField : Border {
  Button button; DateTime? value;
  public DateTime? SelectedDate { get { return value; } set { this.value=value; button.Content=value.HasValue?value.Value.ToString("dddd, d MMM yyyy",UI.Culture)+"   ▦":UI.T("Choose date"); } }
  public DateField() { button=UI.Button("",Open); Child=button; Margin=new Thickness(0,8,0,12); }
  void Open() {
   var owner=Window.GetWindow(this); var window=UI.Dialog(owner,UI.T("Choose date"),360,395); window.ResizeMode=ResizeMode.NoResize; var root=new StackPanel { Margin=new Thickness(18) }; window.Content=root; DateTime month=new DateTime((value??DateTime.Today).Year,(value??DateTime.Today).Month,1);
   Action draw=null; draw=()=> {
    root.Children.Clear(); var nav=UI.Row(); nav.Children.Add(UI.Button("‹",()=> { month=month.AddMonths(-1); draw(); })); var label=UI.Label(month.ToString("MMMM yyyy",UI.Culture),17,UI.Text); label.Width=210; label.TextAlignment=TextAlignment.Center; nav.Children.Add(label); nav.Children.Add(UI.Button("›",()=> { month=month.AddMonths(1); draw(); })); root.Children.Add(nav);
    var grid=new UniformGrid { Columns=7,Margin=new Thickness(0,12,0,10) }; foreach(var d in UI.Culture.DateTimeFormat.AbbreviatedDayNames) { var l=UI.Label(d.Substring(0,Math.Min(2,d.Length)),11,UI.Muted); l.TextAlignment=TextAlignment.Center; grid.Children.Add(l); }
    for(int i=0;i<(int)month.DayOfWeek;i++) grid.Children.Add(new Border()); for(int d=1;d<=DateTime.DaysInMonth(month.Year,month.Month);d++) { var selected=month.AddDays(d-1); var b=UI.Button(d.ToString(),()=> { SelectedDate=selected; window.Close(); },value.HasValue&&selected==value.Value.Date); b.Margin=new Thickness(1); b.MinWidth=0; grid.Children.Add(b); } root.Children.Add(grid); root.Children.Add(UI.Button("Today",()=> { SelectedDate=DateTime.Today; window.Close(); }));
   }; draw(); window.ShowDialog();
  }
 }
}
