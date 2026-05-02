using System;
using System.Runtime.InteropServices;

namespace PictureOrganizer
{
    internal static class ShellRatingHelper
    {
        private static readonly Guid IPropertyStoreGuid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        private const uint GPS_DEFAULT = 0x00000000;
        private const uint GPS_READWRITE = 0x00000002;

        public static int? TryReadRating(string filePath)
        {
            if (!PhotoMetadataHelper.SupportsShellRating(filePath))
            {
                return null;
            }

            IPropertyStore store = null;
            PropVariant variant = new PropVariant();
            try
            {
                PROPERTYKEY key;
                Guid propertyStoreGuid = IPropertyStoreGuid;
                Check(PSGetPropertyKeyFromName("System.Rating", out key));
                Check(SHGetPropertyStoreFromParsingName(filePath, IntPtr.Zero, GPS_DEFAULT, ref propertyStoreGuid, out store));
                Check(store.GetValue(ref key, out variant));
                if (variant.vt == 0)
                {
                    return null;
                }

                int shellValue = variant.GetInt32();
                return ConvertShellRatingToStars(shellValue);
            }
            catch
            {
                return null;
            }
            finally
            {
                PropVariantClear(ref variant);
                if (store != null)
                {
                    Marshal.ReleaseComObject(store);
                }
            }
        }

        public static void WriteRating(string filePath, int? stars)
        {
            if (!PhotoMetadataHelper.SupportsShellRating(filePath))
            {
                throw new InvalidOperationException("Ratings are only supported for JPEG and PNG files.");
            }

            PROPERTYKEY key;
            Guid propertyStoreGuid = IPropertyStoreGuid;
            Check(PSGetPropertyKeyFromName("System.Rating", out key));

            IPropertyStore store = null;
            PropVariant variant = new PropVariant();
            try
            {
                Check(SHGetPropertyStoreFromParsingName(filePath, IntPtr.Zero, GPS_READWRITE, ref propertyStoreGuid, out store));
                if (stars.HasValue)
                {
                    variant.SetUInt32(ConvertStarsToShellRating(stars.Value));
                }
                else
                {
                    variant.SetEmpty();
                }

                Check(store.SetValue(ref key, ref variant));
                Check(store.Commit());
            }
            finally
            {
                PropVariantClear(ref variant);
                if (store != null)
                {
                    Marshal.ReleaseComObject(store);
                }
            }
        }

        private static int ConvertShellRatingToStars(int value)
        {
            if (value <= 0) return 0;
            if (value <= 12) return 1;
            if (value <= 37) return 2;
            if (value <= 62) return 3;
            if (value <= 87) return 4;
            return 5;
        }

        private static uint ConvertStarsToShellRating(int stars)
        {
            switch (stars)
            {
                case 1: return 1;
                case 2: return 25;
                case 3: return 50;
                case 4: return 75;
                case 5: return 99;
                default: throw new ArgumentOutOfRangeException("stars");
            }
        }

        private static void Check(int hr)
        {
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHGetPropertyStoreFromParsingName(
            string pszPath,
            IntPtr pbc,
            uint flags,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int PSGetPropertyKeyFromName(string pszName, out PROPERTYKEY ppropkey);

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr value;
            public int value2;

            public void SetUInt32(uint valueToSet)
            {
                vt = 19;
                value = (IntPtr)valueToSet;
                value2 = 0;
            }

            public void SetEmpty()
            {
                vt = 0;
                value = IntPtr.Zero;
                value2 = 0;
            }

            public int GetInt32()
            {
                if (vt == 19 || vt == 18 || vt == 17)
                {
                    return unchecked((int)(long)value);
                }

                return 0;
            }
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            uint GetCount();
            void GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PropVariant pv);
            int SetValue(ref PROPERTYKEY key, ref PropVariant pv);
            int Commit();
        }
    }
}
