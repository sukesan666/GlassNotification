using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Sign in this process so an ephemeral certificate key never needs a user-profile key container.
// APPX SIP layout: https://learn.microsoft.com/windows/win32/appxpkg/how-to-programmatically-sign-a-package
public static class GlassPackageSigner {
 [StructLayout(LayoutKind.Sequential)] struct FileInfo {public uint Size;public IntPtr Name,Handle;}
 [StructLayout(LayoutKind.Sequential)] struct SubjectInfo {public uint Size;public IntPtr Index;public uint Choice;public IntPtr File;}
 [StructLayout(LayoutKind.Sequential)] struct StoreInfo {public uint Size;public IntPtr Certificate;public uint Policy;public IntPtr Store;}
 [StructLayout(LayoutKind.Sequential)] struct CertificateInfo {public uint Size,Choice;public IntPtr Store,Window;}
 [StructLayout(LayoutKind.Sequential)] struct SignatureInfo {public uint Size,Hash,Attributes;public IntPtr Authcode,Authenticated,Unauthenticated;}
 [StructLayout(LayoutKind.Sequential)] struct Parameters {
  public uint Flags;public IntPtr Subject,Certificate,Signature,Provider;public uint TimestampFlags;
  public IntPtr Algorithm,TimestampUrl,Attributes,Sip,Context,Policy,Reserved;
 }
 [StructLayout(LayoutKind.Sequential)] struct AppxData {public IntPtr Parameters,State;}
 [StructLayout(LayoutKind.Sequential)] struct TrustData {
  public uint Size;public IntPtr Policy,Sip;public uint Ui,Revocation,Choice;public IntPtr File;
  public uint StateAction;public IntPtr State,Url;public uint Flags,Context;public IntPtr SignatureSettings;
 }
 [DllImport("wintrust.dll",ExactSpelling=true)] static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
 [DllImport("mssign32.dll",ExactSpelling=true)] static extern int SignerSignEx2(uint flags,IntPtr subject,IntPtr certificate,IntPtr signature,IntPtr provider,uint timestampFlags,IntPtr algorithm,IntPtr timestampUrl,IntPtr attributes,IntPtr sip,IntPtr context,IntPtr policy,IntPtr reserved);
 static IntPtr Allocate<T>(T value,List<IntPtr> allocations) where T:struct {
  var pointer=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(T)));allocations.Add(pointer);Marshal.StructureToPtr(value,pointer,false);return pointer;
 }
 // Trust registration is separate from building. Untrusted-root is acceptable for a local certificate;
 // invalid signatures and package digest mismatches are not.
 public static int Verify(string path){
  var allocations=new List<IntPtr>();var data=new TrustData();var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
  try{
   var name=Marshal.StringToHGlobalUni(path);allocations.Add(name);
   var file=Allocate(new FileInfo {Size=(uint)Marshal.SizeOf(typeof(FileInfo)),Name=name},allocations);
   data=new TrustData {Size=(uint)Marshal.SizeOf(typeof(TrustData)),Ui=2,Choice=1,File=file,StateAction=1,Flags=0x1010};
   return WinVerifyTrust(new IntPtr(-1),ref action,ref data);
  }finally{
   if(data.State!=IntPtr.Zero){data.StateAction=2;WinVerifyTrust(new IntPtr(-1),ref action,ref data);}
   for(int i=allocations.Count-1;i>=0;i--)Marshal.FreeHGlobal(allocations[i]);
  }
 }
 public static void Sign(string path,IntPtr certificate){
  var allocations=new List<IntPtr>();IntPtr sip=IntPtr.Zero;
  try {
   var name=Marshal.StringToHGlobalUni(path);allocations.Add(name);
   var index=Allocate<uint>(0,allocations);
   var file=Allocate(new FileInfo {Size=(uint)Marshal.SizeOf(typeof(FileInfo)),Name=name},allocations);
   var subject=Allocate(new SubjectInfo {Size=(uint)Marshal.SizeOf(typeof(SubjectInfo)),Index=index,Choice=1,File=file},allocations);
   var store=Allocate(new StoreInfo {Size=(uint)Marshal.SizeOf(typeof(StoreInfo)),Certificate=certificate,Policy=2},allocations);
   var cert=Allocate(new CertificateInfo {Size=(uint)Marshal.SizeOf(typeof(CertificateInfo)),Choice=2,Store=store},allocations);
   var signature=Allocate(new SignatureInfo {Size=(uint)Marshal.SizeOf(typeof(SignatureInfo)),Hash=0x800c},allocations); // CALG_SHA_256
   sip=Allocate(new AppxData(),allocations);
   var parameters=Allocate(new Parameters {Subject=subject,Certificate=cert,Signature=signature,Sip=sip},allocations);
   Marshal.StructureToPtr(new AppxData {Parameters=parameters},sip,false);
   Marshal.ThrowExceptionForHR(SignerSignEx2(0,subject,cert,signature,IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,sip,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero));
  }finally{
   if(sip!=IntPtr.Zero){var data=(AppxData)Marshal.PtrToStructure(sip,typeof(AppxData));if(data.State!=IntPtr.Zero)Marshal.Release(data.State);}
   for(int i=allocations.Count-1;i>=0;i--)Marshal.FreeHGlobal(allocations[i]);
  }
 }
}
