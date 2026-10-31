using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace Glass {
// Exact installed-family lookup: never interpret persisted strings as font URIs.
public static class NotificationFonts {
 static readonly string[] names=Fonts.SystemFontFamilies.Select(f=>f.Source).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToArray();
 static readonly Dictionary<string,string> installed=names.ToDictionary(n=>n,n=>n,StringComparer.OrdinalIgnoreCase);
 public static string Default {get {return installed.ContainsKey("Yu Gothic UI")?installed["Yu Gothic UI"]:SystemFonts.MessageFontFamily.Source;}}
 public static string[] Names {get{return (string[])names.Clone();}}
 public static string Resolve(string name) {string result;return name!=null&&name.Length<=200&&installed.TryGetValue(name.Trim(),out result)?result:Default;}
}
}
