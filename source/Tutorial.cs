using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dayglance {
 // First-run tour: a short guided dialog that adds the user's first activity (weekly or on one date), then points out the main controls.
 // It opens by itself while the schedule is empty and the tour hasn't been seen (State.Onboarded); Settings → Help replays it.
 // Text is inline English/Spanish (L) so the tour stays self-contained.
 public partial class MainWindow {
  static string L(string en,string es) { return UI.Language=="es"?es:en; }
  // Called once at startup. People who already have a schedule never see the tour unprompted.
  public void OfferWelcome() {
   if(State.Onboarded) return;
   if(State.Activities.Count>0) { State.Onboarded=true; return; }
   EventHandler first=null; first=(s,e)=> { ContentRendered-=first; Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(Welcome)); }; ContentRendered+=first;
  }
  public void Welcome() {
   var w=UI.Dialog(this,L("Welcome to Dayglance","Te damos la bienvenida a Dayglance"),470,640);
   var body=new StackPanel { Margin=new Thickness(28,6,28,8) }; var actions=new WrapPanel { Margin=new Thickness(28,6,28,20) };
   var shell=new DockPanel(); DockPanel.SetDock(actions,Dock.Bottom); shell.Children.Add(actions); var scroller=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; shell.Children.Add(scroller); w.Content=shell;
   // Answers collected across the steps; Back keeps them.
   var next=AppClock.Now.Date.AddHours(AppClock.Now.Hour+1); string name="",start=next.ToString("HH:mm",CultureInfo.InvariantCulture),end=next.AddHours(1).ToString("HH:mm",CultureInfo.InvariantCulture);
   bool weekly=true,remind=true; var days=new HashSet<int> { (int)AppClock.Today.DayOfWeek }; DateTime date=AppClock.Today; int[] order={1,2,3,4,5,6,0};
   Activity added=null; int step=0; Func<bool,bool> commit=null; Action render=null;
   Action<int> go=n=> { step=n; render(); scroller.ScrollToVerticalOffset(0); };
   Action forward=()=> { if(commit==null || commit(true)) go(step+1); };
   Action back=()=> { if(commit!=null) commit(false); go(step-1); };
   Func<string,Action,bool,Button> action=(text,act,accent)=> { var b=UI.Button(text,act,accent); b.Margin=new Thickness(0,0,8,6); b.MinHeight=38; actions.Children.Add(b); return b; };
   Action<string,string,string> heading=(eyebrow,title,text)=> {
    body.Children.Add(UI.Label(eyebrow,11,UI.Accent)); var t=UI.Label(title,24,UI.Text); t.FontWeight=FontWeights.SemiBold; t.Margin=new Thickness(0,0,0,8); body.Children.Add(t);
    if(text!=null) { var d=UI.Label(text,13,UI.Muted); d.Margin=new Thickness(0,0,0,16); body.Children.Add(d); }
   };
   Func<string,TextBlock> section=text=> { var label=UI.Label(text,11,UI.Muted); label.Margin=new Thickness(0,14,0,6); return label; };
   Func<TextBlock> errorLabel=()=>UI.Label("",12,UI.B("#FF9E94"));
   // Plain-language description of the repeat, e.g. "Every Mon, Wed" or "Friday, 30 Oct 2026".
   Func<string> repeatText=()=> {
    if(!weekly) return date.ToString("dddd, d MMM yyyy",UI.Culture);
    if(days.Count==7) return UI.T("Every day");
    if(days.Count==5 && !days.Contains(0) && !days.Contains(6)) return L("Every weekday","De lunes a viernes");
    return L("Every ","Cada ")+string.Join(", ",order.Where(d=>days.Contains(d)).Select(d=>UI.Culture.DateTimeFormat.AbbreviatedDayNames[d]));
   };
   render=()=> {
    body.Children.Clear(); actions.Children.Clear(); commit=null;
    if(step==0) {
     var mark=(FrameworkElement)BrandIcon.Visual(); mark.Margin=new Thickness(0); body.Children.Add(new Viewbox { Width=44,Height=44,Child=mark,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,6,0,16) });
     heading(L("WELCOME","BIENVENIDA"),L("Your day, at a glance.","Tu día, de un vistazo."),L("Dayglance is a small widget for your desktop. It shows what you’re doing right now and what comes next, and reminds you when it’s time.","Dayglance es un pequeño widget para tu escritorio. Muestra lo que estás haciendo ahora y lo que sigue, y te avisa cuando llega la hora."));
     body.Children.Add(UI.Label(L("Let’s add your first activity together. It takes about a minute.","Vamos a añadir tu primera actividad. Toma alrededor de un minuto."),14,UI.Text));
     var note=UI.Label(L("You can replay this tour any time from Settings → Help.","Puedes repetir este recorrido cuando quieras desde Ajustes → Ayuda."),11,UI.Muted); note.Margin=new Thickness(0,18,0,0); body.Children.Add(note);
     action(L("Add my first activity","Añadir mi primera actividad"),()=>go(1),true); action(L("Skip for now","Omitir por ahora"),()=>w.Close(),false);
    }
    else if(step==1) {
     heading(L("STEP 1 OF 3","PASO 1 DE 3"),L("What do you want to make time for?","¿Para qué quieres hacer tiempo?"),L("Anything with a time in your day: a class, a workout, a call, taking a pill.","Cualquier cosa con un horario en tu día: una clase, ejercicio, una llamada, tomar una pastilla."));
     body.Children.Add(UI.Label("ACTIVITY NAME",11,UI.Muted)); var title=UI.Input(name); title.MaxLength=120; body.Children.Add(title);
     body.Children.Add(section(L("OR START FROM AN IDEA","O PARTE DE UNA IDEA")));
     var ideas=new WrapPanel(); string[] samples=UI.Language=="es"?new[]{"Caminata matutina","Estudiar","Ejercicio","Tomar medicina","Leer antes de dormir","Llamar a la familia"}:new[]{"Morning walk","Study","Workout","Take medicine","Read before bed","Call family"};
     foreach(string idea in samples) { string text=idea; var b=UI.Button(idea,()=> { title.Text=text; title.Focus(); title.CaretIndex=title.Text.Length; }); b.Margin=new Thickness(0,0,6,6); ideas.Children.Add(b); }
     body.Children.Add(ideas); var error=errorLabel(); body.Children.Add(error);
     commit=validate=> { name=title.Text.Trim(); if(validate && name.Length==0) { error.Text=L("Give your activity a name, or pick an idea.","Ponle un nombre a tu actividad o elige una idea."); return false; } return true; };
     title.KeyDown+=(s,e)=> { if(e.Key==Key.Enter) { e.Handled=true; forward(); } };
     Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=> { title.Focus(); title.CaretIndex=title.Text.Length; }));
     action(UI.T("Next"),forward,true); action(UI.T("Back"),back,false);
    }
    else if(step==2) {
     heading(L("STEP 2 OF 3","PASO 2 DE 3"),L("When does it happen?","¿Cuándo sucede?"),null);
     body.Children.Add(UI.Label(UI.Hour12?"START / END":"START / END  ·  24-HOUR TIME (HH:MM)",11,UI.Muted));
     var times=UI.Row(); var startField=new TimeField(start); var endField=new TimeField(end); endField.Margin=new Thickness(12,0,0,0); times.Children.Add(startField); times.Children.Add(endField); body.Children.Add(times);
     body.Children.Add(UI.Label("An earlier end time finishes the following day.",11,UI.Muted));
     body.Children.Add(section(L("HOW OFTEN?","¿CON QUÉ FRECUENCIA?")));
     // Two large choices instead of the editor's "leave the days empty for one date" rule, which is hard to guess on a first visit.
     var kinds=new System.Windows.Controls.Primitives.UniformGrid { Columns=2,Margin=new Thickness(0,0,0,10) };
     Func<string,string,Button> kind=(title,detail)=> {
      var b=UI.Button("",()=> {},false); b.Margin=new Thickness(0,0,8,0); b.HorizontalContentAlignment=HorizontalAlignment.Stretch;
      var text=new StackPanel(); text.Children.Add(new TextBlock { Text=title,FontSize=14,FontWeight=FontWeights.SemiBold }); text.Children.Add(new TextBlock { Text=detail,FontSize=11,Opacity=.8,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,0) }); b.Content=text; kinds.Children.Add(b); return b;
     };
     var weeklyButton=kind(L("Every week","Cada semana"),L("On the days you choose","Los días que elijas")); var onceButton=kind(L("Just once","Solo una vez"),L("On one date","En una fecha"));
     body.Children.Add(kinds);
     var repeatPanel=new StackPanel(); var dayRow=new WrapPanel(); var checks=new List<CheckBox>();
     foreach(int d in order) { var c=UI.Chip(UI.Culture.DateTimeFormat.AbbreviatedDayNames[d],days.Contains(d)); c.Margin=new Thickness(0,5,5,8); checks.Add(c); dayRow.Children.Add(c); }
     repeatPanel.Children.Add(dayRow); var presets=UI.Row(); presets.Children.Add(UI.Button("Every day",()=>checks.ForEach(c=>c.IsChecked=true))); presets.Children.Add(UI.Button("Weekdays",()=> { for(int i=0;i<7;i++) checks[i].IsChecked=i<5; })); repeatPanel.Children.Add(presets);
     var oncePanel=new StackPanel(); var dateField=new DateField { SelectedDate=date,Margin=new Thickness(0,4,0,8) }; oncePanel.Children.Add(dateField);
     body.Children.Add(repeatPanel); body.Children.Add(oncePanel); var error=errorLabel(); error.Margin=new Thickness(0,10,0,0); body.Children.Add(error);
     Action mark=()=> {
      foreach(var pair in new[]{Tuple.Create(weeklyButton,weekly),Tuple.Create(onceButton,!weekly)}) { pair.Item1.Background=pair.Item2?UI.Accent:UI.Card; pair.Item1.Foreground=pair.Item2?UI.AccentInk:UI.Text; }
      repeatPanel.Visibility=weekly?Visibility.Visible:Visibility.Collapsed; oncePanel.Visibility=weekly?Visibility.Collapsed:Visibility.Visible; error.Text="";
     };
     weeklyButton.Click+=(s,e)=> { weekly=true; mark(); }; onceButton.Click+=(s,e)=> { weekly=false; mark(); }; mark();
     commit=validate=> {
      start=startField.Value; end=endField.Value; days=new HashSet<int>(order.Where((d,i)=>checks[i].IsChecked==true)); date=dateField.SelectedDate??AppClock.Today;
      if(!validate) return true;
      if(start==end) { error.Text=UI.T("Start and end times must be different."); return false; }
      if(weekly && days.Count==0) { error.Text=L("Choose at least one day, or pick Just once.","Elige al menos un día o selecciona Solo una vez."); return false; }
      return true;
     };
     action(UI.T("Next"),forward,true); action(UI.T("Back"),back,false);
    }
    else if(step==3) {
     heading(L("STEP 3 OF 3","PASO 3 DE 3"),L("Want a reminder?","¿Quieres un recordatorio?"),L("When the activity starts, Dayglance shows a small notice in the corner of your screen. It works while the app is running, even hidden in the tray.","Cuando empieza la actividad, Dayglance muestra un pequeño aviso en la esquina de la pantalla. Funciona mientras la app esté abierta, incluso oculta en la bandeja."));
     var reminder=UI.Switch("Remind me when it starts",remind); reminder.Checked+=(s,e)=>remind=true; reminder.Unchecked+=(s,e)=>remind=false; body.Children.Add(reminder);
     var hint=UI.Label(L("You can also add an earlier reminder later, for example a day before, by editing the activity.","Más adelante puedes añadir un aviso previo, por ejemplo un día antes, al editar la actividad."),11,UI.Muted); body.Children.Add(hint);
     body.Children.Add(section(L("HERE’S HOW IT WILL LOOK","ASÍ SE VERÁ")));
     var draft=new Activity { Id="welcome-preview",Title=name,Start=start,End=end,Color="#A4E9CC",Days=weekly?order.Where(d=>days.Contains(d)).ToArray():new int[0],Date=date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) };
     DateTime day=weekly?Enumerable.Range(0,7).Select(i=>AppClock.Today.AddDays(i)).First(d=>days.Contains((int)d.DayOfWeek)):date;
     DateTime from=day+Schedule.Time(start),to=day+Schedule.Time(end); if(to<=from) to=to.AddDays(1);
     var card=DayCard(new Occurrence { Activity=draft,Start=from,End=to },from.AddMinutes(-1),State.CardStyle,false); card.IsHitTestVisible=false; body.Children.Add(card);
     body.Children.Add(UI.Label(repeatText(),12,UI.Muted));
     var error=errorLabel(); body.Children.Add(error);
     action(L("Add to my day","Añadir a mi día"),()=> {
      try {
       var a=new Activity { Id=Guid.NewGuid().ToString("N"),Title=name,Start=start,End=end,Days=weekly?order.Where(d=>days.Contains(d)).ToArray():new int[0],Date=weekly?"":date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),Color="#A4E9CC",Notes="",Reminder=remind?0:-1,ExtraReminders=new List<int>() };
       var test=new State(); test.Activities.Add(a); Schedule.Validate(test);
       bool seen=State.Onboarded; State.Activities.Add(a); State.Onboarded=true;
       if(!Save()) { State.Activities.Remove(a); State.Onboarded=seen; return; }
       added=a; Refresh(true); go(4);
      } catch(Exception ex) { error.Text=UI.T("Check your details: ")+UI.T(ex.Message); }
     },true);
     action(UI.T("Back"),back,false);
    }
    else {
     heading(L("ALL SET","LISTO"),L("Your first activity is on your schedule.","Tu primera actividad ya está en tu horario."),L("A few things that help from here:","Algunas cosas útiles a partir de aquí:"));
     body.Children.Add(WelcomeTips());
     action(L("Got it","Entendido"),()=>w.Close(),true);
    }
   };
   render();
   // Closing at any step (Skip, ×, Esc) counts as seen; the activity, if any, gets a short glow in the day view.
   w.Closed+=(s,e)=> {
    if(!State.Onboarded) { State.Onboarded=true; Save(); }
    if(added==null || State.Compact || State.WeekView) return;
    for(int i=0;i<8;i++) {
     var day=AppClock.Today.AddDays(i); var o=Schedule.ForDay(State,day).FirstOrDefault(x=>x.Activity==added); if(o==null) continue;
     string key=o.Key;
     if(i==0) Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>FocusActivity(key,true)));
     else { selected=day; Refresh(true); Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=> { Border card; if(dayCards.TryGetValue(key,out card)) UI.GlowAround(card,12); })); }
     break;
    }
   };
   w.ShowDialog();
  }
  // The main controls in one short list; shown at the end of the tour and in Settings → Help.
  FrameworkElement WelcomeTips() {
   var list=new StackPanel();
   var tips=new[]{
    Tuple.Create("",L("Add more activities with the + button at the top.","Añade más actividades con el botón + de arriba.")),
    Tuple.Create("",L("Tap ○ on a card when you finish it. Double-click a card to edit it.","Pulsa ○ en una tarjeta al terminarla. Haz doble clic en ella para editarla.")),
    Tuple.Create("",L("Manage lists your whole routine, ready to edit or delete.","Gestionar muestra toda tu rutina para editarla o eliminarla.")),
    Tuple.Create("",L("Add a checklist to an activity and tick items right on its card. Repeating activities start fresh each time.","Añade una lista de tareas a una actividad y marca los elementos en su tarjeta. Las actividades que se repiten empiezan sin marcar.")),
    Tuple.Create("",L("Week shows all seven days. Click an empty slot there to add something.","Semana muestra los siete días. Haz clic en un espacio libre para añadir algo.")),
    Tuple.Create("",L("The mini widget keeps only what’s happening now. Double-click the title bar to switch.","El mini widget muestra solo lo que pasa ahora. Haz doble clic en la barra superior para cambiar.")),
    Tuple.Create("",L("× hides Dayglance in the tray so reminders keep working. Right-click the tray icon to quit.","× oculta Dayglance en la bandeja para que los avisos sigan funcionando. Haz clic derecho en su icono para salir.")),
    Tuple.Create("",L("Settings has themes, text size, language and this tour.","En Ajustes encontrarás temas, tamaño del texto, idioma y este recorrido."))
   };
   foreach(var tip in tips) {
    var row=new DockPanel { Margin=new Thickness(0,0,0,10) };
    var icon=new TextBlock { Text=tip.Item1,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=15,Foreground=UI.Accent,Width=30,Margin=new Thickness(0,1,0,0),VerticalAlignment=VerticalAlignment.Top }; DockPanel.SetDock(icon,Dock.Left); row.Children.Add(icon);
    var text=UI.Label(tip.Item2,13,UI.Text); text.Margin=new Thickness(0); row.Children.Add(text); list.Children.Add(row);
   }
   return list;
  }
 }
}
