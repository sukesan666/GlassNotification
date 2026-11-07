using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Glass {
public static partial class Tests {
 static void TextOutlineSettingsTests(string path) {
  var serializer=new JavaScriptSerializer();string warning;
  File.WriteAllText(path,"{\"Width\":480,\"TextColor\":\"#FFFFFF\",\"TextOutlineColor\":\"#ab12ef\"}");
  var settings=SettingsStore.Load(path,out warning);
  Check(SettingsStore.Save(path,settings,out warning),"save notification text outline color");
  var fields=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(fields.ContainsKey("TextOutlineColor")&&Convert.ToString(fields["TextOutlineColor"])=="#AB12EF","text outline color survives load, normalization and save");
  Check(Convert.ToString(fields["TextColor"])=="#FFFFFF","outline color does not replace the text fill color");
  foreach(string json in new[]{"{}","{\"Width\":480}","{\"Width\":480,\"TextOutlineColor\":null}","{\"Width\":480,\"TextOutlineColor\":{}}","{\"Width\":480,\"TextOutlineColor\":\"bad\"}"}) {
   File.WriteAllText(path,json);settings=SettingsStore.Load(path,out warning);
   fields=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(settings));
   Check(Convert.ToString(fields["TextOutlineColor"])=="Default","legacy and invalid outline settings disable the outline safely");
   if(json.Contains("480"))Check(settings.Width==480,"invalid outline color preserves unrelated settings");
  }
 }
 static AppSettings OutlineTestSettings(string color) {
  return new AppSettings {Mode=BackdropMode.Off,CardColor="#FFFFFF",TextColor="#000000",TextOutlineColor=color};
 }
 static int OutlinePixelCount(Visual visual,double width,double height,Color color,double scale) {
  var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);
  var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(visual),null,new Rect(0,0,width,height));bitmap.Render(drawing);
  int stride=bitmap.PixelWidth*4,count=0;var pixels=new byte[stride*bitmap.PixelHeight];bitmap.CopyPixels(pixels,stride,0);
  for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]>100&&Math.Abs(pixels[i]-color.B)<35&&Math.Abs(pixels[i+1]-color.G)<35&&Math.Abs(pixels[i+2]-color.R)<35)count++;
  return count;
 }
 static void NotificationTextOutlineTests() {
  TextOutlineCacheTests();
  var card=new NotificationWindow();
  try {
   var monitor=NativeWindow.GetMonitors()[0];
   Action<AppSettings> apply=delegate(AppSettings value){
    card.Apply(value,monitor);var border=(Border)card.Content;
    border.Measure(new Size(card.Width,card.Height));border.Arrange(new Rect(0,0,card.Width,card.Height));border.UpdateLayout();
   };
   card.SetNotification(new SystemNotification {App="通知元 Outline",Title="縁取りタイトル Ag",Body="日本語の本文と Latin text を折り返しながら表示し、末尾まで長い通知を確認します。"});
   var settings=OutlineTestSettings("#FF0000");settings.Width=280;settings.Height=160;settings.FontSize=18;
   apply(settings);
   var surface=(Border)card.Content;var text=(StackPanel)((Grid)surface.Child).Children[1];
   foreach(TextBlock block in text.Children) {
    Check(OutlinePixelCount(block,block.ActualWidth,block.ActualHeight,Colors.Red,2)>10,"selected outline color renders around every notification text row");
    Check(OutlinePixelCount(block,block.ActualWidth,block.ActualHeight,Colors.Black,2)>10,"text fill stays visible inside the colored outline");
   }
   double height=card.Height;int lines=card.BodyLines;
   settings=OutlineTestSettings("#0000FF");settings.Width=280;settings.Height=160;settings.FontSize=18;apply(settings);
   Check(OutlinePixelCount(surface,card.Width,card.Height,Colors.Red,2)==0&&OutlinePixelCount(surface,card.Width,card.Height,Colors.Blue,2)>30,"changing outline color updates existing notification text immediately");
   card.SavePreview(Path.Combine(Root,"preview-text-outline.png"));
   settings=OutlineTestSettings("Default");settings.Width=280;settings.Height=160;settings.FontSize=18;apply(settings);
   Check(OutlinePixelCount(surface,card.Width,card.Height,Colors.Blue,2)==0,"choosing no outline restores plain text");
   Check(card.Height==height&&card.BodyLines==lines,"outline does not change card layout or body line count");
   settings=OutlineTestSettings("#FF0000");settings.HighContrast=true;apply(settings);
   Check(OutlinePixelCount(surface,card.Width,card.Height,Colors.Red,2)==0,"high contrast keeps its readable system palette without a custom outline");
  } finally {card.Close();}
 }
 static Geometry FirstOutlineGeometry(Drawing drawing){
  var geometry=drawing as GeometryDrawing;if(geometry!=null)return geometry.Geometry;
  var group=drawing as DrawingGroup;if(group!=null)foreach(var child in group.Children){var found=FirstOutlineGeometry(child);if(found!=null)return found;}
  return null;
 }
 static void TextOutlineCacheTests(){
  var block=new NotificationTextBlock {Text="Outline 国Ag",FontSize=20,OutlineBrush=Brushes.Red};
  var unrelated=new Border {Width=100,Height=30};var host=new StackPanel();host.Children.Add(block);host.Children.Add(unrelated);
  Action layout=delegate{host.Measure(new Size(300,200));host.Arrange(new Rect(0,0,300,200));host.UpdateLayout();};layout();
  var drawing=(DrawingVisual)VisualTreeHelper.GetChild(block,VisualTreeHelper.GetChildrenCount(block)-1);
  var first=FirstOutlineGeometry(drawing.Drawing);Check(first!=null,"outline cache test starts with rendered glyph geometry");
  for(int i=0;i<12;i++){unrelated.Width=110+i;layout();}
  Check(ReferenceEquals(first,FirstOutlineGeometry(drawing.Drawing)),"unrelated settings layouts reuse the existing outline geometry");
  block.Text="Changed 本文";layout();
  Check(!ReferenceEquals(first,FirstOutlineGeometry(drawing.Drawing)),"changing notification text invalidates cached glyph outlines");
 }
}
}
