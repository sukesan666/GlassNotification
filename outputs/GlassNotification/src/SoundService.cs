using System;
using System.IO;
using System.Text;
using System.Media;
using System.Threading.Tasks;
namespace Glass {
public sealed class WaveValidation {public bool IsValid;public string Error;public double Duration;}
public interface IAudioOutput : IDisposable {void Play(byte[] wav);void Stop();}
public interface ISoundService : IDisposable {void Play(AppSettings settings,Action<string> reportError);void Stop();}
public sealed class WaveOutput : IAudioOutput {
 SoundPlayer player;MemoryStream stream;
 public void Play(byte[] wav) {Stop();stream=new MemoryStream(wav,false);player=new SoundPlayer(stream);player.Load();player.Play();}
 public void Stop() {if(player!=null){player.Stop();player.Dispose();player=null;}if(stream!=null){stream.Dispose();stream=null;}}
 public void Dispose(){Stop();}
}
public sealed class SoundService : ISoundService {
 readonly IAudioOutput output; readonly string chime;readonly Func<string,Task<byte[]>> loader;bool disposed;long generation;
 public SoundService(IAudioOutput target,string standardPath) : this(target,standardPath,delegate(string p){return Task.Run(delegate{return LoadValidated(p);});}) {}
 public SoundService(IAudioOutput target,string standardPath,Func<string,Task<byte[]>> load) {output=target;chime=standardPath;loader=load;}
 public static byte[] LoadValidated(string path) {
  byte[] bytes;
  using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
   if(stream.Length<44||stream.Length>2000000)throw new InvalidDataException("5秒以内の PCM WAV を選んでください。");
   bytes=new byte[(int)stream.Length];int read=0;
   while(read<bytes.Length){int n=stream.Read(bytes,read,bytes.Length-read);if(n==0)throw new InvalidDataException("音声ファイルが途中で切れています。");read+=n;}
   if(stream.ReadByte()!=-1)throw new InvalidDataException("音声ファイルの長さが変わりました。");
  }
  var valid=ValidateBytes(bytes);if(!valid.IsValid)throw new InvalidDataException(valid.Error);return bytes;
 }
 public static WaveValidation Validate(string path) {
  try {return ValidateBytes(LoadValidated(path));}catch(Exception e) {
   if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException||e is FormatException||e is NotSupportedException))throw;
   return new WaveValidation {IsValid=false,Error="音を読み込めません。再選択してください。 "+e.Message};
  }
 }
 static WaveValidation ValidateBytes(byte[] bytes) {
  try {
   using(var stream=new MemoryStream(bytes,false)) using(var reader=new BinaryReader(stream)) {
    if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="RIFF") throw new FormatException("WAV ファイルではありません。");
    uint riff=reader.ReadUInt32();if((long)riff+8!=stream.Length)throw new FormatException("WAV ファイルの長さが不正です。");
    if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="WAVE")throw new FormatException("WAV ファイルではありません。");
    int format=0,channels=0,bits=0,rate=0,align=0;uint byteRate=0;long data=0;bool hasFormat=false,hasData=false;
    while(stream.Position+8<=stream.Length) {
     string name=Encoding.ASCII.GetString(reader.ReadBytes(4));uint len=reader.ReadUInt32();long end=stream.Position+len;
     if(end>stream.Length)throw new FormatException("WAV ファイルが壊れています。");
     if(name=="fmt ") {
      if(len<16||hasFormat)throw new FormatException("WAV 形式が不正です。");
      format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();byteRate=reader.ReadUInt32();align=reader.ReadUInt16();bits=reader.ReadUInt16();hasFormat=true;
     } else if(name=="data") {data+=len;hasData=true;}
     stream.Position=end+(len%2);if(stream.Position>stream.Length)throw new FormatException("WAV のパディングが不正です。");
    }
    if(!hasFormat||!hasData||format!=1||(bits!=8&&bits!=16)||(channels!=1&&channels!=2)||rate<8000||rate>48000||align!=channels*bits/8||byteRate!=rate*align||data<=0||data%align!=0)throw new FormatException("PCM WAV（8/16bit・モノラル/ステレオ・8〜48kHz）を選んでください。");
    double duration=(double)data/byteRate;if(duration>5)throw new FormatException("音声は5秒以内にしてください。");
    return new WaveValidation {IsValid=true,Duration=duration};
   }
  }catch(Exception e) {
   if(!(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is FormatException||e is NotSupportedException))throw;
   return new WaveValidation {IsValid=false,Error="音を読み込めません。再選択してください。 "+e.Message};
  }
 }
 public async void Play(AppSettings settings,Action<string> reportError) {
  if(disposed)return;Stop();if(settings.SoundKind==SoundKind.None)return;long current=generation;
  string path=settings.SoundKind==SoundKind.Standard?chime:settings.SoundPath;
  try {byte[] bytes=await loader(path);if(!disposed&&generation==current)output.Play(bytes);} catch(Exception e) {
   if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is InvalidOperationException||e is TimeoutException||e is ArgumentException))throw;
   if(!disposed&&generation==current)reportError("音を再生できません。別の WAV を選んでください。 "+e.Message);
  }
 }
 public void Stop(){generation++;if(!disposed)output.Stop();}
 public void Dispose(){if(disposed)return;Stop();output.Dispose();disposed=true;}
 public static void CreateChime(string path) {
  Directory.CreateDirectory(Path.GetDirectoryName(path));const int rate=22050;int samples=(int)(rate*.65);
  using(var stream=File.Create(path))using(var w=new BinaryWriter(stream)) {
   w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(samples*2);
   for(int i=0;i<samples;i++){double t=(double)i/rate,fade=Math.Min(1,t/.015)*Math.Exp(-t*7);double signal=Math.Sin(t*2*Math.PI*880)*.65+Math.Sin(t*2*Math.PI*1320)*.20;w.Write((short)(signal*fade*8000));}
  }
 }
}
}
