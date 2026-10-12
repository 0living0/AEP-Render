using System;
using System.Runtime.InteropServices;

namespace AepBatchRenderer
{
    // Windows Common Item Dialog, with Explorer address/search/quick-access UI.
    internal static class FolderPicker
    {
        public static string Pick(IntPtr owner,string current,string title)
        {
            IFileDialog dialog=(IFileDialog)new FileOpenDialog(); IShellItem folder=null, result=null;
            try
            {
                dialog.SetOptions(0x20 | 0x40 | 0x800 | 0x8);
                dialog.SetTitle(title); dialog.SetOkButtonLabel(UiText.Get("text.259"));
                if(System.IO.Directory.Exists(current))
                {
                    Guid iid=typeof(IShellItem).GUID;
                    if(SHCreateItemFromParsingName(current,IntPtr.Zero,ref iid,out folder)==0) dialog.SetFolder(folder);
                }
                int hr=dialog.Show(owner); if(hr==unchecked((int)0x800704C7)) return null;
                Marshal.ThrowExceptionForHR(hr); dialog.GetResult(out result);
                IntPtr name; result.GetDisplayName(0x80058000,out name);
                try { return Marshal.PtrToStringUni(name); } finally { Marshal.FreeCoTaskMem(name); }
            }
            finally { if(result!=null) Marshal.ReleaseComObject(result); if(folder!=null) Marshal.ReleaseComObject(folder); Marshal.ReleaseComObject(dialog); }
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)] private static extern int SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,out IShellItem item);
        [ComImport,Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"),ClassInterface(ClassInterfaceType.None)] private class FileOpenDialog { }
        [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IShellItem
        {
            void BindToHandler(IntPtr bind,ref Guid handler,ref Guid iid,out IntPtr value);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint type,out IntPtr value);
            void GetAttributes(uint mask,out uint value);
            void Compare(IShellItem other,uint hint,out int result);
        }
        [ComImport,Guid("D57C7288-D4AD-4768-BE02-9D969532D960"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count,IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events,out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem selection);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void GetResult(out IShellItem result);
        }
    }
}
