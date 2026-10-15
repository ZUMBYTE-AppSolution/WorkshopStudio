using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkshopStudio.Desktop.Services;

public static class CredentialVault
{
    private const uint Generic = 1, LocalMachine = 2;
    private static string Target(Guid id) => "WorkshopStudio/Steam/" + id.ToString("N");
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    private struct Credential {
        public uint Flags, Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target,uint type,uint flags,out IntPtr credential);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target,uint type,uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr buffer);
    public static void Save(Guid id,string username,string password)
    {
        var bytes=Encoding.Unicode.GetBytes(password);
        if (bytes.Length>2560) throw new ArgumentException("Passwort ist zu lang für den Windows-Anmeldedatenspeicher.");
        var ptr=Marshal.AllocCoTaskMem(bytes.Length);
        try {
            Marshal.Copy(bytes,0,ptr,bytes.Length);
            var c=new Credential { Type=Generic,TargetName=Target(id),UserName=username,CredentialBlobSize=(uint)bytes.Length,CredentialBlob=ptr,Persist=LocalMachine,Comment="Workshop Studio SteamCMD-Anmeldung" };
            if (!CredWrite(ref c,0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        } finally { for(int i=0;i<bytes.Length;i++) Marshal.WriteByte(ptr,i,0); Marshal.FreeCoTaskMem(ptr); Array.Clear(bytes); }
    }
    public static string? Read(Guid id)
    {
        if (!CredRead(Target(id),Generic,0,out var ptr)) {
            var error=Marshal.GetLastWin32Error(); if (error==1168) return null; throw new Win32Exception(error);
        }
        try { var c=Marshal.PtrToStructure<Credential>(ptr); return Marshal.PtrToStringUni(c.CredentialBlob,(int)c.CredentialBlobSize/2); }
        finally { CredFree(ptr); }
    }
    public static void Delete(Guid id)
    {
        if (!CredDelete(Target(id),Generic,0) && Marshal.GetLastWin32Error()!=1168) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
