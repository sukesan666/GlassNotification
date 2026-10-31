using System;
using System.Windows;
namespace Glass {
public sealed class LayoutResult { public AppSettings Settings; public Rect Bounds; }
public static class Layout {
 public const double StackSpacing=10;
 public static LayoutResult Resolve(AppSettings value,Size area,double minimumHeightDip) {
  return Resolve(value,area,minimumHeightDip,0);
 }
 public static LayoutResult Resolve(AppSettings value,Size area,double minimumHeightDip,double stackOffsetDip) {
  var s=value.Normalize(); double minHeight=Math.Max(88,Math.Ceiling(minimumHeightDip/4)*4);
  if(area.Width<280||area.Height<minHeight||minHeight>240) throw new InvalidOperationException("画面の作業領域が小さすぎます。文字や表示倍率を小さくしてください。");
  s.Height=Math.Max(s.Height,minHeight);
  s.Margin=Math.Max(0,Math.Min(s.Margin,Math.Floor(Math.Min((area.Width-s.Width)/2,(area.Height-s.Height)/2)/4)*4));
  s.Width=Math.Min(s.Width,Math.Floor((area.Width-2*s.Margin)/10)*10);
  s.Height=Math.Min(s.Height,Math.Floor((area.Height-2*s.Margin)/4)*4);
  bool right=s.Corner==Corner.TopRight||s.Corner==Corner.BottomRight;
  bool bottom=s.Corner==Corner.BottomLeft||s.Corner==Corner.BottomRight;
  double offset=Math.Max(0,double.IsNaN(stackOffsetDip)||double.IsInfinity(stackOffsetDip)?0:stackOffsetDip);
  double x=right?area.Width-s.Margin-s.Width:s.Margin;
  double baseY=bottom?area.Height-s.Margin-s.Height:s.Margin;
  double rawY=bottom?baseY-offset:baseY+offset;
  double y=Math.Max(0,Math.Min(Math.Max(0,area.Height-s.Height),rawY));
  return new LayoutResult {Settings=s,Bounds=new Rect(x,y,s.Width,s.Height)};
 }
}
}
