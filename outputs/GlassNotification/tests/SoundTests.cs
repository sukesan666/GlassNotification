using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace Glass {
public static partial class Tests {
 sealed class AudioLog : IAudioOutput {
  public string Events=""; public void Play(byte[] bytes){Events+="P";} public void Stop(){Events+="S";} public void Dispose(){Events+="D";}
 }
 static byte[] Wave(double seconds,int format,bool extra) {
  using(var stream=new MemoryStream()) using(var w=new BinaryWriter(stream)) {
   int length=(int)(seconds*8000)*2;
   w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+length+(extra?10:0));w.Write(Encoding.ASCII.GetBytes("WAVE"));
   if(extra){w.Write(Encoding.ASCII.GetBytes("JUNK"));w.Write(1);w.Write((byte)0);w.Write((byte)0);}
   w.Write(Encoding.ASCII.GetBytes("fmt "));w.Write(16);w.Write((short)format);w.Write((short)1);w.Write(8000);w.Write(16000);w.Write((short)2);w.Write((short)16);
   w.Write(Encoding.ASCII.GetBytes("data"));w.Write(length);w.Write(new byte[length]);return stream.ToArray();
  }
 }
 static partial void SoundTests(string folder) {
  string path=Path.Combine(folder,"test.wav");
  File.WriteAllBytes(path,Wave(5,1,false));Check(SoundService.Validate(path).IsValid,"5 sec PCM valid");
  File.WriteAllBytes(path,Wave(5.001,1,false));Check(!SoundService.Validate(path).IsValid,"over 5 sec invalid");
  File.WriteAllBytes(path,Wave(0,1,false));Check(!SoundService.Validate(path).IsValid,"empty PCM invalid");
  File.WriteAllBytes(path,Wave(1,3,false));Check(!SoundService.Validate(path).IsValid,"compressed invalid");
  File.WriteAllBytes(path,Wave(.1,1,true));Check(SoundService.Validate(path).IsValid,"unknown chunk skipped");
  File.WriteAllBytes(path,new byte[12]);Check(!SoundService.Validate(path).IsValid,"corrupt WAV");
  var bad=Wave(.1,1,false);bad[40]=255;bad[41]=255;bad[42]=255;bad[43]=127;
  File.WriteAllBytes(path,bad);Check(!SoundService.Validate(path).IsValid,"chunk bounds checked");
  File.WriteAllBytes(path,Wave(.1,1,false));var log=new AudioLog();
  using(var svc=new SoundService(log,Path.Combine(Root,"assets/chime.wav"),delegate(string p){return Task.FromResult(SoundService.LoadValidated(p));})) {
   var s=new AppSettings();svc.Play(s,delegate{});Check(!log.Events.Contains("P"),"no sound mode");
   s.SoundKind=SoundKind.File;s.SoundPath=path;svc.Play(s,delegate{});svc.Play(s,delegate{});
   Check(log.Events.EndsWith("SPSP"),"replace playback instead of overlap");
   File.Delete(path);bool reported=false;svc.Play(s,delegate{reported=true;});Check(reported,"deleted file reports error");
   Check(!SoundService.Validate(path).IsValid,"missing WAV rejected");
  }
  Check(log.Events.EndsWith("SD"),"dispose stops audio");
  SoundService.CreateChime(Path.Combine(Root,"assets/chime.wav"));Check(SoundService.Validate(Path.Combine(Root,"assets/chime.wav")).IsValid,"bundled chime valid");
  var delayed=new TaskCompletionSource<byte[]>();var delayedLog=new AudioLog();
  var asyncSound=new SoundService(delayedLog,"unused",delegate(string p){return delayed.Task;});
  asyncSound.Play(new AppSettings {SoundKind=SoundKind.Standard},delegate{});
  Check(!delayedLog.Events.Contains("P"),"slow loader returns without playback or blocking");
  asyncSound.Stop();delayed.SetResult(Wave(.1,1,false));
  Check(!delayedLog.Events.Contains("P"),"stopped pending load never plays");asyncSound.Dispose();
  var pending=new TaskCompletionSource<byte[]>();var disposedLog=new AudioLog();
  asyncSound=new SoundService(disposedLog,"unused",delegate(string p){return pending.Task;});asyncSound.Play(new AppSettings {SoundKind=SoundKind.Standard},delegate{});asyncSound.Dispose();pending.SetResult(Wave(.1,1,false));
  Check(!disposedLog.Events.Contains("P"),"disposed pending load never plays");
 }
}
}
