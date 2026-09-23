using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal static class ClipboardAccess
{
    private const int ClipboardCantOpenHResult =
        unchecked((int)0x800401D0);
    private const int RetryCount = 3;

    public static bool TrySetText(
        string text,
        out Exception? error)
    {
        error = null;

        if (string.IsNullOrEmpty(text))
            return false;

        for (int attempt = 0;
             attempt < RetryCount;
             attempt++)
        {
            try
            {
                Clipboard.SetText(
                    text,
                    TextDataFormat.UnicodeText);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;

                if (!IsClipboardBusy(ex) ||
                    attempt == RetryCount - 1)
                {
                    break;
                }

                Thread.Sleep(10 * (attempt + 1));
            }
        }

        if (error != null)
        {
            RuntimeLog.Warning(
                "UI.Clipboard",
                "Clipboard text copy failed.",
                error);
        }

        return false;
    }

    public static bool TryGetImage(
        out BitmapSource? source,
        out Exception? error)
    {
        source = null;
        error = null;

        for (int attempt = 0;
             attempt < RetryCount;
             attempt++)
        {
            try
            {
                if (!Clipboard.ContainsImage())
                    return false;

                BitmapSource? image =
                    Clipboard.GetImage();

                if (image == null)
                    return false;

                if (image.CanFreeze)
                    image.Freeze();

                source = image;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;

                if (!IsClipboardBusy(ex) ||
                    attempt == RetryCount - 1)
                {
                    break;
                }

                Thread.Sleep(10 * (attempt + 1));
            }
        }

        if (error != null)
        {
            RuntimeLog.Warning(
                "UI.Clipboard",
                "Clipboard image access failed.",
                error);
        }

        return false;
    }

    private static bool IsClipboardBusy(
        Exception exception)
    {
        for (Exception? current = exception;
             current != null;
             current = current.InnerException)
        {
            if (current.HResult ==
                ClipboardCantOpenHResult)
            {
                return true;
            }
        }

        return false;
    }
}
