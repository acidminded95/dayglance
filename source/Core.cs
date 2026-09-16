using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Dayglance {
 public class Activity {
  public Activity() { ExtraReminders=new List<int>(); }
  public string Id { get; set; }
  public string Title { get; set; }
  public string Notes { get; set; }
  public string Color { get; set; }
  public string Start { get; set; }
  public string End { get; set; }
  public int[] Days { get; set; }
  public string Date { get; set; }
  public int Reminder { get; set; }
  public List<int> ExtraReminders { get; set; }
 }
 public class State {
  public int Version { get; set; }
  public List<Activity> Activities { get; set; }
  public List<string> Completed { get; set; }
  public List<string> Reminded { get; set; }
  public bool Notifications { get; set; }
  public bool Sound { get; set; }
  public bool Pinned { get; set; }
  public bool Compact { get; set; }
  public string Theme { get; set; }
  public string Language { get; set; }
  public bool WeekView { get; set; }
  public List<Theme> CustomThemes { get; set; }
  public double Left { get; set; }
  public double Top { get; set; }
  public string CardStyle { get; set; }
  public double WindowWidth { get; set; }
  public double WindowHeight { get; set; }
  public double MiniLeft { get; set; }
  public double MiniTop { get; set; }
  public double MiniWidth { get; set; }
  public double MiniHeight { get; set; }
  public double UiScale { get; set; }
  public string TimeFormat { get; set; }
  public bool AutoUpdateCheck { get; set; }
  public string WeekHighlight { get; set; }
  public State() { Version=1; Activities=new List<Activity>(); Completed=new List<string>(); Reminded=new List<string>(); CustomThemes=new List<Theme>(); Notifications=true; Pinned=true; Left=80; Top=80; Theme="midnight"; Language="en"; CardStyle="stripe"; WeekHighlight="line"; UiScale=1; TimeFormat="24"; AutoUpdateCheck=true; }
 }
 public class Occurrence {
  public Activity Activity;
  public DateTime Start, End;
  public string Key { get { return Activity.Id+"@"+Start.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); } }
 }
 public static class Schedule {
  public static TimeSpan Time(string s) { TimeSpan value; if(!TimeSpan.TryParseExact(s,"hh\\:mm",CultureInfo.InvariantCulture,out value)) throw new Exception("Use HH:MM, between 00:00 and 23:59."); return value; }
  public static List<Occurrence> ForDay(State state, DateTime day) {
   var result=new List<Occurrence>(); day=day.Date;
   foreach(var a in state.Activities) for(int offset=-1;offset<=0;offset++) {
    var date=day.AddDays(offset);
    bool applies=a.Days.Length>0 ? a.Days.Contains((int)date.DayOfWeek) : a.Date==date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    if(!applies) continue;
    var start=date+Time(a.Start); var end=date+Time(a.End); if(end<=start) end=end.AddDays(1);
    if(start<day.AddDays(1) && end>day) result.Add(new Occurrence { Activity=a,Start=start,End=end });
   }
   return result.OrderBy(o=>o.Start).ThenBy(o=>o.Activity.Title).ToList();
  }
  public static List<Occurrence> Due(State state,DateTime now) {
   return ForDay(state,now.Date).Concat(ForDay(state,now.Date.AddDays(1))).GroupBy(o=>o.Key).Select(g=>g.First())
    .Where(o=>o.Activity.Reminder>=0 && now>=o.Start.AddMinutes(-o.Activity.Reminder) && now<o.End && !state.Reminded.Contains(o.Key) && !state.Completed.Contains(o.Key)).ToList();
  }
  public static void Validate(State s) {
   if(s==null || s.Version!=1 || s.Activities==null || s.Activities.Count>5000) throw new Exception("This is not a supported Dayglance backup.");
   var ids=new HashSet<string>();
   foreach(var a in s.Activities) {
    if(a==null || string.IsNullOrWhiteSpace(a.Id) || a.Id.Length>80 || !ids.Add(a.Id)) throw new Exception("An activity has a missing or duplicate ID.");
    if(string.IsNullOrWhiteSpace(a.Title) || a.Title.Length>120 || (a.Notes??"").Length>2000) throw new Exception("Activity titles must have 1–120 characters; notes can have up to 2,000.");
    Time(a.Start); Time(a.End);
    if(a.Start==a.End) throw new Exception("Start and end times must be different.");
    if(a.Days==null || a.Days.Any(d=>d<0||d>6) || a.Days.Distinct().Count()!=a.Days.Length) throw new Exception("Invalid repeat days.");
    DateTime date;
    if(a.Days.Length==0 && !DateTime.TryParseExact(a.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)) throw new Exception("A one-time activity needs a date.");
    if(a.Reminder < -1 || a.Reminder>1440) throw new Exception("Invalid reminder time.");
    if(a.ExtraReminders==null) a.ExtraReminders=new List<int>();
    if(a.ExtraReminders.Count>1 || a.ExtraReminders.Any(n=>n<1||n>43200)) throw new Exception("Use one additional reminder, between 1 minute and 30 days.");
    if(a.Color==null || !System.Text.RegularExpressions.Regex.IsMatch(a.Color,"^#[0-9A-Fa-f]{6}$")) throw new Exception("Invalid activity color.");
   }
   if(s.Completed==null) s.Completed=new List<string>(); if(s.Reminded==null) s.Reminded=new List<string>();
   if(s.CustomThemes==null) s.CustomThemes=new List<Theme>();
   if(s.CustomThemes.Count>24) throw new Exception("Up to 24 custom themes are supported.");
   var themeIds=new HashSet<string>();
   foreach(var t in s.CustomThemes) {
    if(t==null || string.IsNullOrWhiteSpace(t.Id) || !t.Id.StartsWith("custom-") || !themeIds.Add(t.Id) || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length>40 || new[]{t.Background,t.Surface,t.Foreground,t.Muted,t.Accent,t.Hero,t.Line}.Any(c=>c==null||!System.Text.RegularExpressions.Regex.IsMatch(c,"^#[0-9A-Fa-f]{6}$"))) throw new Exception("Invalid custom theme.");
   }
   if(!new[]{"midnight","terracotta","midcentury","paper","lavender","cyberpunk"}.Contains(s.Theme) && !themeIds.Contains(s.Theme)) s.Theme="midnight";
   if(s.Language!="es") s.Language="en";
   if(double.IsNaN(s.Left)||double.IsInfinity(s.Left)) s.Left=80;
   if(double.IsNaN(s.Top)||double.IsInfinity(s.Top)) s.Top=80;
   if(s.CardStyle!="band" && s.CardStyle!="full") s.CardStyle="stripe";
   if(s.WeekHighlight!="outline" && s.WeekHighlight!="glow") s.WeekHighlight="line";
   foreach(var size in new[]{s.WindowWidth,s.WindowHeight,s.MiniWidth,s.MiniHeight}) if(double.IsNaN(size)||double.IsInfinity(size)||size<0||size>20000) { s.WindowWidth=s.WindowHeight=s.MiniWidth=s.MiniHeight=0; break; }
   if(double.IsNaN(s.MiniLeft)||double.IsInfinity(s.MiniLeft)||double.IsNaN(s.MiniTop)||double.IsInfinity(s.MiniTop)) { s.MiniLeft=s.MiniTop=0; s.MiniWidth=0; }
   if(s.TimeFormat!="12") s.TimeFormat="24";
   if(double.IsNaN(s.UiScale)||s.UiScale<.85||s.UiScale>1.35) s.UiScale=1;
  }
  public class ReminderEvent { public Occurrence Occurrence; public string Key; public bool Advance; }
  public static List<ReminderEvent> Reminders(State state,DateTime now) {
   var result=Due(state,now).Select(o=>new ReminderEvent { Occurrence=o,Key=o.Key }).ToList();
   int horizon=state.Activities.SelectMany(a=>a.ExtraReminders??new List<int>()).DefaultIfEmpty(0).Max();
   for(int day=0;day<=Math.Ceiling(horizon/1440.0);day++) foreach(var o in ForDay(state,now.Date.AddDays(day))) foreach(int lead in o.Activity.ExtraReminders??new List<int>()) {
    var trigger=o.Start.AddMinutes(-lead); var key=o.Key+"@pre:"+lead;
    if(now>=trigger && now<trigger.AddMinutes(15) && now<o.Start && !state.Completed.Contains(o.Key) && !state.Reminded.Contains(key) && !result.Any(r=>r.Key==key)) result.Add(new ReminderEvent { Occurrence=o,Key=key,Advance=true });
   }
   return result;
  }
  // Older data used Reminder as "minutes before". The main reminder now fires at the start; a lead time moves to the additional reminder.
  public static bool MigrateReminders(State state) {
   bool changed=false;
   foreach(var a in state.Activities) if(a.Reminder>0) { if(a.ExtraReminders==null) a.ExtraReminders=new List<int>(); if(a.ExtraReminders.Count==0) a.ExtraReminders.Add(a.Reminder); a.Reminder=0; changed=true; }
   return changed;
  }
  public static DateTime WeekStart(DateTime date) { return date.Date.AddDays(-(int)date.DayOfWeek); }
  public class Block { public Occurrence Occurrence; public double StartHour,EndHour; public int Lane,Lanes; }
  public static List<Block> Layout(State state,DateTime day) {
   var blocks=ForDay(state,day).Select(o=>new Block { Occurrence=o,StartHour=Math.Max(0,(o.Start-day.Date).TotalHours),EndHour=Math.Min(24,(o.End-day.Date).TotalHours) }).ToList();
   var group=new List<Block>(); var ends=new List<double>(); double groupEnd=-1;
   foreach(var b in blocks) {
    if(b.StartHour>=groupEnd) { foreach(var old in group) old.Lanes=ends.Count; group.Clear(); ends.Clear(); }
    int lane=ends.FindIndex(end=>end<=b.StartHour); if(lane<0) { lane=ends.Count; ends.Add(b.EndHour); } else ends[lane]=b.EndHour;
    b.Lane=lane; group.Add(b); groupEnd=Math.Max(groupEnd,b.EndHour);
   }
   foreach(var b in group) b.Lanes=ends.Count;
   return blocks;
  }
 }
 public static class Storage {
  public static string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dayglance");
  public static string FilePath { get { return Path.Combine(Folder,"schedule.json"); } }
  public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=8*1024*1024 }; }
  public static State Read(string path) { if(new FileInfo(path).Length>8*1024*1024) throw new Exception("Backup exceeds 8 MB."); var s=Serializer().Deserialize<State>(File.ReadAllText(path)); Schedule.Validate(s); return s; }
  public static void Save(State s) {
   Schedule.Validate(s); Directory.CreateDirectory(Folder);
   var temp=FilePath+".tmp"; File.WriteAllText(temp,Serializer().Serialize(s),Encoding.UTF8);
   if(File.Exists(FilePath)) File.Replace(temp,FilePath,FilePath+".bak"); else File.Move(temp,FilePath);
  }
 }
}

