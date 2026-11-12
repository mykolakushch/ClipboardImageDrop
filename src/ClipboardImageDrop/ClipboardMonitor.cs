using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipboardImageDrop;

internal sealed class ClipboardMonitor : NativeWindow, IDisposable
{
    private readonly string _tempDirectory;
    private bool _isInternalUpdate;
    private bool _isEnabled = true;
    private byte[]? _cachedPngBytes;
    private string? _cachedFilePath;
    private bool _hasFileDropInjected;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            if (!_isEnabled && _hasFileDropInjected)
            {
                RestoreImageOnlyToClipboard();
            }
        }
    }

    public ClipboardMonitor()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ClipboardImageDrop");
        Directory.CreateDirectory(_tempDirectory);

        CreateHandle(new CreateParams
        {
            Parent = NativeMethods.HWND_MESSAGE
        });

        NativeMethods.AddClipboardFormatListener(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            OnClipboardUpdate();
        }

        base.WndProc(ref m);
    }

    private void OnClipboardUpdate()
    {
        if (_isInternalUpdate || !_isEnabled)
        {
            return;
        }

        try
        {
            IDataObject? dataObj = Clipboard.GetDataObject();
            if (dataObj == null)
            {
                ClearCache();
                return;
            }

            bool hasImage = dataObj.GetDataPresent(DataFormats.Bitmap);
            bool hasFileDrop = dataObj.GetDataPresent(DataFormats.FileDrop);

            if (hasImage && !hasFileDrop)
            {
                using Image? image = Clipboard.GetImage();
                if (image != null)
                {
                    CacheNewImage(image);

                    if (NativeMethods.IsExplorerOrDesktop(NativeMethods.GetForegroundWindow()))
                    {
                        InjectFileDropToClipboard();
                    }
                }
            }
            else if (hasFileDrop)
            {
                if (Clipboard.GetFileDropList() is StringCollection files &&
                    files.Count == 1 &&
                    !string.IsNullOrEmpty(_cachedFilePath) &&
                    string.Equals(files[0], _cachedFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                ClearCache();
            }
            else
            {
                ClearCache();
            }
        }
        catch (ExternalException)
        {
            // Буфер обміну тимчасово зайнятий іншим процесом
        }
        catch (Exception)
        {
            // Ігноруємо непередбачені збої читання буфера
        }
    }

    public void OnForegroundWindowChanged(bool isExplorerOrDesktop)
    {
        if (!_isEnabled || _cachedPngBytes == null || string.IsNullOrEmpty(_cachedFilePath))
        {
            return;
        }

        if (isExplorerOrDesktop && !_hasFileDropInjected)
        {
            InjectFileDropToClipboard();
        }
        else if (!isExplorerOrDesktop && _hasFileDropInjected)
        {
            RestoreImageOnlyToClipboard();
        }
    }

    private void CacheNewImage(Image image)
    {
        CleanupOldTempFiles();

        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        _cachedPngBytes = ms.ToArray();

        string fileName = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.png";
        _cachedFilePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllBytes(_cachedFilePath, _cachedPngBytes);
        _hasFileDropInjected = false;
    }

    private void InjectFileDropToClipboard()
    {
        if (_cachedPngBytes == null || string.IsNullOrEmpty(_cachedFilePath))
        {
            return;
        }

        if (!File.Exists(_cachedFilePath))
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllBytes(_cachedFilePath, _cachedPngBytes);
        }

        try
        {
            _isInternalUpdate = true;

            using var ms = new MemoryStream(_cachedPngBytes);
            using var bitmap = new Bitmap(ms);

            var dataObject = new DataObject();
            dataObject.SetImage(bitmap);

            var fileCollection = new StringCollection { _cachedFilePath };
            dataObject.SetFileDropList(fileCollection);

            SuppressClipboardHistory(dataObject);

            Clipboard.SetDataObject(dataObject, true);
            _hasFileDropInjected = true;
        }
        catch (ExternalException)
        {
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    private void RestoreImageOnlyToClipboard()
    {
        if (_cachedPngBytes == null)
        {
            return;
        }

        try
        {
            _isInternalUpdate = true;

            using var ms = new MemoryStream(_cachedPngBytes);
            using var bitmap = new Bitmap(ms);

            var dataObject = new DataObject();
            dataObject.SetImage(bitmap);

            SuppressClipboardHistory(dataObject);

            Clipboard.SetDataObject(dataObject, true);
            _hasFileDropInjected = false;
        }
        catch (ExternalException)
        {
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    private static void SuppressClipboardHistory(DataObject dataObject)
    {
        byte[] zeroDword = BitConverter.GetBytes(0);
        dataObject.SetData("CanIncludeInClipboardHistory", new MemoryStream(zeroDword));
        dataObject.SetData("CanUploadToCloudClipboard", new MemoryStream(zeroDword));
    }

    public void ClearCache()
    {
        _cachedPngBytes = null;
        _cachedFilePath = null;
        _hasFileDropInjected = false;
    }

    public void CleanupOldTempFiles()
    {
        try
        {
            if (!Directory.Exists(_tempDirectory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(_tempDirectory, "*.png"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }

        CleanupOldTempFiles();
    }
}
