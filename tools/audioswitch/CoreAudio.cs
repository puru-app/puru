using System.Runtime.InteropServices;

namespace AudioSwitch;

public record AudioDevice(string Id, string Name, bool IsDefault);

/// <summary>Core Audio COM 封裝：只在面板開啟時列舉，列舉完立即釋放 COM 物件。</summary>
public static class CoreAudio
{
    public const int Render = 0;   // 輸出
    public const int Capture = 1;  // 輸入

    public static List<AudioDevice> List(int flow)
    {
        var result = new List<AudioDevice>();
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        IMMDeviceCollection col = null;
        try
        {
            string defaultId = null;
            if (enumerator.GetDefaultAudioEndpoint(flow, 1, out var def) == 0 && def != null)
            {
                def.GetId(out defaultId);
                Marshal.ReleaseComObject(def);
            }

            if (enumerator.EnumAudioEndpoints(flow, 1 /*ACTIVE*/, out col) != 0) return result;
            col.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                if (col.Item(i, out var dev) != 0) continue;
                try
                {
                    dev.GetId(out var id);
                    result.Add(new AudioDevice(id, FriendlyName(dev), id == defaultId));
                }
                finally { Marshal.ReleaseComObject(dev); }
            }
        }
        finally
        {
            if (col != null) Marshal.ReleaseComObject(col);
            Marshal.ReleaseComObject(enumerator);
        }
        return result;
    }

    /// <summary>
    /// 配對鍵：同一支實體裝置的輸出與輸入，Windows 名稱括號內的型號相同
    /// （例：「LOGI耳機 (PRO X 2 LIGHTSPEED)」↔「羅技麥克風 (PRO X 2 LIGHTSPEED)」）。
    /// 藍牙會多出「Stereo」「Hands-Free AG Audio」字尾，剝掉後再比對。沒有括號就用整個名稱。
    /// </summary>
    public static string MatchKey(string name)
    {
        string key = name ?? "";
        int l = key.LastIndexOf('('), r = key.LastIndexOf(')');
        if (l >= 0 && r > l) key = key.Substring(l + 1, r - l - 1);
        key = key.Trim().ToLowerInvariant();
        foreach (var suffix in new[] { "hands-free ag audio", "hands-free", "stereo" })
            if (key.EndsWith(suffix)) key = key.Substring(0, key.Length - suffix.Length).Trim();
        return key;
    }

    public static void SetDefault(string deviceId)
    {
        var policy = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            // 0 = Console, 1 = Multimedia, 2 = Communications
            for (int role = 0; role < 3; role++)
                policy.SetDefaultEndpoint(deviceId, role);
        }
        finally { Marshal.ReleaseComObject(policy); }
    }

    private static string FriendlyName(IMMDevice dev)
    {
        if (dev.OpenPropertyStore(0 /*READ*/, out var store) != 0) return "(unknown)";
        try
        {
            var key = new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
            if (store.GetValue(ref key, out var pv) != 0) return "(unknown)";
            try { return pv.vt == 31 /*VT_LPWSTR*/ ? Marshal.PtrToStringUni(pv.p) ?? "(unknown)" : "(unknown)"; }
            finally { PropVariantClear(ref pv); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant pv);

    // ---------- COM 介面 ----------
    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr p;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    // 未公開介面（EarTrumpet / SoundSwitch 等工具皆使用）；vtable 順序不可更動
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
        [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
        [PreserveSig] int ResetDeviceFormat(IntPtr a);
        [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
        [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int GetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);
        [PreserveSig] int SetEndpointVisibility(IntPtr a, int b);
    }
}
