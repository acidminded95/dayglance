# Schedule JSON

Import an object with `Version: 1` and an `Activities` array. Dayglance 1 backups remain supported. Optional settings/history fields use defaults when omitted.

```json
{
  "Version": 1,
  "Activities": [
    {
      "Id": "study-weekdays",
      "Title": "Estudiar",
      "Notes": "Leer y preparar apuntes",
      "Color": "#C7B5DB",
      "Start": "09:00",
      "End": "10:30",
      "Days": [1, 2, 3, 4, 5],
      "Date": null,
      "Reminder": 10,
      "ExtraReminders": [1440]
    }
  ]
}
```

- `Id`: unique nonempty string, at most 80 characters.
- `Title`: 1–120 characters. `Notes`: optional, up to 2,000.
- `Color`: six-digit hex, `#RRGGBB`.
- `Start` / `End`: local `HH:mm`. Earlier end means next day; equal times are invalid.
- `Days`: **0 Sunday, 1 Monday, …, 6 Saturday**. Repeats every week with no term start/end.
- One-time event: `Days: []` and `Date: "2027-01-15"`.
- `Reminder`: minutes before; `-1` off, `0` at start. Imports allow up to 1,440.
- `ExtraReminders`: optional array with at most one additional lead time, from 1 to 43,200 minutes (30 days); `[]` for none.
- `Completed` and `Reminded`: optional history arrays; omit for a fresh schedule.

Limits: 8 MB and 5,000 activity rules. Import replaces the schedule, with confirmation and backup. Theme/language/view/notification preferences stay local. Unknown fields are ignored.
