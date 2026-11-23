using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Audibly.App.Helpers;

/// <summary>
///     The Windows folder dialog with multi-select. The WinUI FolderPicker can only pick one folder.
/// </summary>
public static class FolderDialog
{
    private const uint FosPickFolders = 0x20;
    private const uint FosForceFileSystem = 0x40;
    private const uint FosAllowMultiSelect = 0x200;
    private const int ErrorCancelled = unchecked((int)0x800704C7);
    private const uint SigdnFileSysPath = 0x80058000;

    /// <summary>Shows the dialog; the picked folder paths, or an empty list when cancelled.</summary>
    public static IReadOnlyList<string> PickFolders(IntPtr owner)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogCoClass();
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FosPickFolders | FosAllowMultiSelect | FosForceFileSystem);
            var result = dialog.Show(owner);
            if (result == ErrorCancelled) return [];
            Marshal.ThrowExceptionForHR(result);

            dialog.GetResults(out var items);
            items.GetCount(out var count);
            var paths = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
            {
                items.GetItemAt(i, out var item);
                item.GetDisplayName(SigdnFileSysPath, out var name);
                try
                {
                    paths.Add(Marshal.PtrToStringUni(name)!);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(name);
                }
            }

            return paths;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogCoClass;

    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint count, IntPtr filterSpec);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int place);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IShellItemArray items);
        void GetSelectedItems(out IShellItemArray items);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid interfaceId, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint nameType, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid interfaceId, out IntPtr result);
        void GetPropertyStore(int flags, ref Guid interfaceId, out IntPtr store);
        void GetPropertyDescriptionList(IntPtr key, ref Guid interfaceId, out IntPtr list);
        void GetAttributes(int flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out IntPtr items);
    }
}
