using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Glass {
// Outline the glyphs WPF actually laid out, including wrapping and ellipses.
public sealed class NotificationTextBlock : TextBlock {
 public static readonly DependencyProperty OutlineBrushProperty=DependencyProperty.Register("OutlineBrush",typeof(Brush),typeof(NotificationTextBlock),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
 readonly DrawingVisual outline=new DrawingVisual();
 DrawingGroup cachedDrawing;Brush cachedBrush;
 public Brush OutlineBrush {get{return (Brush)GetValue(OutlineBrushProperty);}set{SetValue(OutlineBrushProperty,value);}}
 public NotificationTextBlock(){AddVisualChild(outline);LayoutUpdated+=delegate {RefreshOutline();};}
 protected override int VisualChildrenCount {get{return base.VisualChildrenCount+1;}}
 protected override Visual GetVisualChild(int index){return index==base.VisualChildrenCount?outline:base.GetVisualChild(index);}
 void RefreshOutline() {
  if(OutlineBrush==null&&cachedDrawing==null)return;
  var drawing=OutlineBrush==null?null:VisualTreeHelper.GetDrawing(this);
  if(ReferenceEquals(cachedBrush,OutlineBrush)&&SameDrawing(cachedDrawing,drawing))return;
  using(var context=outline.RenderOpen()) {
   if(drawing!=null){
    var pen=new Pen(OutlineBrush,1.5) {LineJoin=PenLineJoin.Round};if(pen.CanFreeze)pen.Freeze();
    DrawOutline(context,drawing,pen);
    // Restore the original glyph fill over the inner half of the stroke.
    context.DrawDrawing(drawing);
   }
  }
  cachedDrawing=drawing;cachedBrush=OutlineBrush;
 }
 static bool SameDrawing(Drawing left,Drawing right){
  if(ReferenceEquals(left,right))return true;
  if(left==null||right==null)return false;
  var a=left as GlyphRunDrawing;var b=right as GlyphRunDrawing;
  if(a!=null&&b!=null)return ReferenceEquals(a.GlyphRun,b.GlyphRun)&&SameBrush(a.ForegroundBrush,b.ForegroundBrush);
  var x=left as DrawingGroup;var y=right as DrawingGroup;
  if(x==null||y==null||x.Children.Count!=y.Children.Count||x.Opacity!=y.Opacity||TransformMatrix(x.Transform)!=TransformMatrix(y.Transform)||!ReferenceEquals(x.ClipGeometry,y.ClipGeometry)||!ReferenceEquals(x.OpacityMask,y.OpacityMask)||!SameGuidelines(x.GuidelineSet,y.GuidelineSet))return false;
  for(int i=0;i<x.Children.Count;i++)if(!SameDrawing(x.Children[i],y.Children[i]))return false;
  return true;
 }
 static bool SameBrush(Brush left,Brush right){
  if(ReferenceEquals(left,right))return true;
  var a=left as SolidColorBrush;var b=right as SolidColorBrush;
  return a!=null&&b!=null&&a.Color==b.Color&&a.Opacity==b.Opacity&&TransformMatrix(a.Transform)==TransformMatrix(b.Transform)&&TransformMatrix(a.RelativeTransform)==TransformMatrix(b.RelativeTransform);
 }
 static Matrix TransformMatrix(Transform transform){return transform==null?Matrix.Identity:transform.Value;}
 static bool SameGuidelines(GuidelineSet left,GuidelineSet right){
  return ReferenceEquals(left,right)||left!=null&&right!=null&&SameCoordinates(left.GuidelinesX,right.GuidelinesX)&&SameCoordinates(left.GuidelinesY,right.GuidelinesY);
 }
 static bool SameCoordinates(DoubleCollection left,DoubleCollection right){
  if(left.Count!=right.Count)return false;for(int i=0;i<left.Count;i++)if(left[i]!=right[i])return false;return true;
 }
 static void DrawOutline(DrawingContext context,Drawing drawing,Pen pen) {
  var glyph=drawing as GlyphRunDrawing;
  if(glyph!=null){var geometry=glyph.GlyphRun.BuildGeometry();if(geometry.CanFreeze)geometry.Freeze();context.DrawGeometry(null,pen,geometry);return;}
  var group=drawing as DrawingGroup;if(group==null)return;
  if(group.Transform!=null)context.PushTransform(group.Transform);
  if(group.ClipGeometry!=null)context.PushClip(group.ClipGeometry);
  foreach(var child in group.Children)DrawOutline(context,child,pen);
  if(group.ClipGeometry!=null)context.Pop();
  if(group.Transform!=null)context.Pop();
 }
}
}
