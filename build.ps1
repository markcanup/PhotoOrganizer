$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$bin = Join-Path $root "bin"

if (-not (Test-Path $bin)) {
    New-Item -ItemType Directory -Path $bin | Out-Null
}

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$output = Join-Path $bin "PhotoOrganizer.exe"
$framework = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$windowsWinMd = "C:\Program Files (x86)\Dell Digital Delivery Services\Windows.winmd"

$references = @(
    "/reference:$framework\mscorlib.dll",
    "/reference:$framework\System.dll",
    "/reference:$framework\System.Core.dll",
    "/reference:$framework\System.Drawing.dll",
    "/reference:$framework\Microsoft.VisualBasic.dll",
    "/reference:$framework\WPF\WindowsBase.dll",
    "/reference:$framework\WPF\PresentationCore.dll",
    "/reference:$framework\System.Xaml.dll",
    "/reference:$framework\System.Runtime.Serialization.dll",
    "/reference:$framework\System.Runtime.dll",
    "/reference:$framework\System.Runtime.InteropServices.WindowsRuntime.dll",
    "/reference:$framework\System.Runtime.WindowsRuntime.dll",
    "/reference:$framework\System.Windows.Forms.dll",
    "/reference:$windowsWinMd"
)

$sources = Get-ChildItem -Path $src -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
$helpResource = Join-Path $root "docs\PhotoOrganizerHelp.htm"

& $csc /nologo /target:winexe /out:$output "/resource:$helpResource,PictureOrganizer.PhotoOrganizerHelp.htm" $references $sources
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed."
}

$stampSource = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class ExeVersionStamp
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    private static readonly IntPtr RT_VERSION = (IntPtr)16;
    private static readonly IntPtr VERSION_ID = (IntPtr)1;

    public static void Stamp(string filePath)
    {
        byte[] data = BuildVersionResource();
        IntPtr handle = BeginUpdateResource(filePath, false);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("BeginUpdateResource failed.");
        }

        if (!UpdateResource(handle, RT_VERSION, VERSION_ID, 1033, data, (uint)data.Length))
        {
            EndUpdateResource(handle, true);
            throw new InvalidOperationException("UpdateResource failed.");
        }

        if (!EndUpdateResource(handle, false))
        {
            throw new InvalidOperationException("EndUpdateResource failed.");
        }
    }

    private static byte[] BuildVersionResource()
    {
        byte[] fixedInfo = BuildFixedFileInfo();
        byte[] stringFileInfo = BuildStringFileInfo();
        byte[] varFileInfo = BuildVarFileInfo();

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.Unicode))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)fixedInfo.Length);
            writer.Write((ushort)0);
            WriteUnicodeString(writer, "VS_VERSION_INFO");
            Align(writer);
            writer.Write(fixedInfo);
            Align(writer);
            writer.Write(stringFileInfo);
            writer.Write(varFileInfo);
            PatchLength(stream);
            return stream.ToArray();
        }
    }

    private static byte[] BuildFixedFileInfo()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(0xFEEF04BDu);
            writer.Write(0x00010000u);
            writer.Write(0x00010000u);
            writer.Write(0x00000000u);
            writer.Write(0x00010000u);
            writer.Write(0x00000000u);
            writer.Write(0x0000003Fu);
            writer.Write(0x00000000u);
            writer.Write(0x00040004u);
            writer.Write(0x00000001u);
            writer.Write(0x00000000u);
            writer.Write(0x00000000u);
            writer.Write(0x00000000u);
            return stream.ToArray();
        }
    }

    private static byte[] BuildStringFileInfo()
    {
        var values = new Dictionary<string, string>
        {
            { "FileDescription", "Photo Organizer Application" },
            { "FileVersion", "1.0.2.0" },
            { "ProductName", "Canup Photo Organizer" },
            { "ProductVersion", "1.0.2" },
            { "LegalCopyright", "Copyright (C) 2026 Mark Canup" }
        };

        byte[] stringTable = BuildStringTable("040904E4", values);
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.Unicode))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            WriteUnicodeString(writer, "StringFileInfo");
            Align(writer);
            writer.Write(stringTable);
            PatchLength(stream);
            return stream.ToArray();
        }
    }

    private static byte[] BuildStringTable(string key, IDictionary<string, string> values)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.Unicode))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            WriteUnicodeString(writer, key);
            Align(writer);
            foreach (var pair in values)
            {
                writer.Write(BuildString(pair.Key, pair.Value));
            }
            PatchLength(stream);
            return stream.ToArray();
        }
    }

    private static byte[] BuildString(string key, string value)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.Unicode))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)(value.Length + 1));
            writer.Write((ushort)1);
            WriteUnicodeString(writer, key);
            Align(writer);
            WriteUnicodeString(writer, value);
            Align(writer);
            PatchLength(stream);
            return stream.ToArray();
        }
    }

    private static byte[] BuildVarFileInfo()
    {
        byte[] translation = BuildTranslation();
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.Unicode))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            WriteUnicodeString(writer, "VarFileInfo");
            Align(writer);
            writer.Write(translation);
            PatchLength(stream);
            return stream.ToArray();
        }
    }

    private static byte[] BuildTranslation()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.Unicode))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)4);
            writer.Write((ushort)0);
            WriteUnicodeString(writer, "Translation");
            Align(writer);
            writer.Write((ushort)0x0409);
            writer.Write((ushort)1252);
            Align(writer);
            PatchLength(stream);
            return stream.ToArray();
        }
    }

    private static void WriteUnicodeString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.Unicode.GetBytes(value));
        writer.Write((ushort)0);
    }

    private static void Align(BinaryWriter writer)
    {
        while ((writer.BaseStream.Position % 4) != 0)
        {
            writer.Write((byte)0);
        }
    }

    private static void PatchLength(MemoryStream stream)
    {
        long current = stream.Position;
        stream.Position = 0;
        byte[] bytes = BitConverter.GetBytes((ushort)current);
        stream.Write(bytes, 0, bytes.Length);
        stream.Position = current;
    }
}
"@

Add-Type -TypeDefinition $stampSource -Language CSharp
[ExeVersionStamp]::Stamp($output)

Write-Host "Built $output"
