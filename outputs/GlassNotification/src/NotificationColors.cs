using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
namespace Glass {
public sealed class NotificationColorChoice {
 public string Label {get;private set;} public string Value {get;private set;}
 public Brush Preview {get;private set;}
 public NotificationColorChoice(string label,string value){Label=label;Value=value;Preview=new SolidColorBrush(NotificationColors.Color(value,Colors.White));}
 public override string ToString(){return Label;}
}
public static class NotificationColors {
 public const string Default="Default";
 public static string Normalize(string value){
  if(string.Equals(value,Default,StringComparison.OrdinalIgnoreCase))return Default;
  if(value==null||value.Length!=7||value[0]!='#')return Default;
  for(int i=1;i<7;i++)if(!Uri.IsHexDigit(value[i]))return Default;
  return value.ToUpperInvariant();
 }
 public static Color Color(string value,Color fallback){
  value=Normalize(value);if(value==Default)return fallback;
  return System.Windows.Media.Color.FromRgb(byte.Parse(value.Substring(1,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),byte.Parse(value.Substring(3,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),byte.Parse(value.Substring(5,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture));
 }
 public static List<NotificationColorChoice> Choices(){
  return new List<NotificationColorChoice>{new NotificationColorChoice("標準",Default),new NotificationColorChoice("白","#FFFFFF"),new NotificationColorChoice("黒","#000000"),new NotificationColorChoice("青","#2563EB"),new NotificationColorChoice("水色","#8ECAFF"),new NotificationColorChoice("ピンク","#F4A9CC"),new NotificationColorChoice("赤","#DC2626"),new NotificationColorChoice("緑","#166534"),new NotificationColorChoice("紫","#A78BFA"),new NotificationColorChoice("黄色","#FDE68A")};
 }
}
}
