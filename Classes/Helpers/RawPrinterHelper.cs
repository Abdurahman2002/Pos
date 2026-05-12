using System.Runtime.InteropServices;

namespace NewsApp2.Classes.Helpers
{
    public static class RawPrinterHelper
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] ref DOC_INFO_1 di);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DOC_INFO_1
        {
            public string pDocName;
            public string pOutputFile;
            public string pDataType;
        }

        public static bool SendBytesToPrinter(string printerName, byte[] data, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(printerName))
            {
                error = "Printer name is empty.";
                return false;
            }

            if (data == null || data.Length == 0)
            {
                error = "Print payload is empty.";
                return false;
            }

            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            {
                error = BuildWin32Error("OpenPrinter");
                return false;
            }

            var docInfo = new DOC_INFO_1
            {
                pDocName = "Label Print Job",
                pOutputFile = null,
                pDataType = "RAW"
            };

            var docStarted = false;
            var pageStarted = false;

            try
            {
                if (!StartDocPrinter(hPrinter, 1, ref docInfo))
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == 1804)
                    {
                        docInfo.pDataType = null;
                        if (!StartDocPrinter(hPrinter, 1, ref docInfo))
                        {
                            error = BuildWin32Error("StartDocPrinter");
                            return false;
                        }
                    }
                    else
                    {
                        error = BuildWin32Error("StartDocPrinter");
                        return false;
                    }
                }

                docStarted = true;

                if (!StartPagePrinter(hPrinter))
                {
                    error = BuildWin32Error("StartPagePrinter");
                    return false;
                }

                pageStarted = true;

                var pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
                try
                {
                    Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
                    if (!WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out var bytesWritten) || bytesWritten != data.Length)
                    {
                        error = BuildWin32Error("WritePrinter");
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pUnmanagedBytes);
                }

                return true;
            }
            finally
            {
                if (pageStarted)
                    EndPagePrinter(hPrinter);

                if (docStarted)
                    EndDocPrinter(hPrinter);

                ClosePrinter(hPrinter);
            }
        }

        private static string BuildWin32Error(string action)
            => $"{action} failed with Win32 error {Marshal.GetLastWin32Error()}.";
    }
}
