using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using GraphicsDevice = SharpDX.Toolkit.Graphics.GraphicsDevice;

namespace Armiger
{
    public sealed class DXTManager
    {
        public enum Result
        {
            NoAction = 0,
            Delete = 1,
            Failed = 2,
            CompressedBC3 = 3,
        }

        static readonly Lazy<DXTManager> _instance = new Lazy<DXTManager>(() => new DXTManager());
        public static DXTManager Instance { get { return _instance.Value; } }

        private DXTManager()
        {
            _device = GraphicsDevice.New(DeviceCreationFlags.Debug, FeatureLevel.Level_11_0);
            _count = 0;
        }
        readonly GraphicsDevice _device;

        volatile int _count;

        public async Task<Result> Process(string file, Recovery recovery)
        {
            _count++;
            return await Task.Factory.StartNew<Result>(() => _process(file, recovery)).ContinueWith(intask =>
            {
                Console.WriteLine("Count hit: " + --_count);
                return intask.Result;
            });
        }

        //bmp > tga > png > dds
        private unsafe Result _process(string file, Recovery recovery)
        {
            try
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                using (var fstream = File.OpenRead(file))
                {
                    if (fstream.Length == 0)
                    {
                        switch (ext)
                        {
                            case "bmp":
                                return _process(Path.ChangeExtension(file, "tga"), recovery);
                            case "tga":
                                return _process(Path.ChangeExtension(file, "png"), recovery);
                            case "png":
                                return _process(Path.ChangeExtension(file, "dds"), recovery);
                            case "dds":
                            default:
                                recovery.Backup(file);
                                return Result.Delete;
                        }
                    }

                    using (var tex = SharpDX.Toolkit.Graphics.Texture2D.Load(_device, fstream))
                    {
                        if (!tex.IsBlockCompressed)
                        {
                            var desc = tex.Description;

                            var imginf = ImageInformation.FromFile(file).Value;
                            try
                            {
                                var loadOpts = new ImageLoadInformation
                                {
                                    BindFlags = desc.BindFlags,
                                    CpuAccessFlags = desc.CpuAccessFlags,
                                    Depth = desc.Depth,
                                    //Filter = FilterFlags.None,
                                    //FirstMipLevel = 0,
                                    Format = SharpDX.DXGI.Format.BC3_UNorm_SRgb,
                                    Height = tex.Height,
                                    Width = tex.Width,
                                    //MipFilter = FilterFlags.,
                                    MipLevels = desc.MipLevels,
                                    OptionFlags = desc.OptionFlags,
                                    PSrcInfo = new IntPtr(&imginf),
                                    Usage = desc.Usage,
                                };

                                fstream.Seek(0, SeekOrigin.Begin);
                                using (var newTex = SharpDX.Direct3D11.Texture2D.FromStream(_device, fstream, (int)fstream.Length, loadOpts))
                                {
                                    fstream.Dispose();

                                    //recovery.Backup(file);
                                    SharpDX.Direct3D11.Texture2D.ToFile(_device, newTex, SharpDX.Direct3D11.ImageFileFormat.Dds, Path.ChangeExtension(file, "xdds"));
                                    Console.WriteLine(Path.GetFileNameWithoutExtension(file) + " converted to BC3");

                                    return Result.CompressedBC3;
                                }
                            }
                            catch (System.Runtime.InteropServices.SEHException SEHexC)
                            {
                                return Result.Failed;
                            }
                            catch (Exception e)
                            {
                                return Result.Failed;
                            }
                        }
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                Trace.TraceError(e.ToString());
            }

            return Result.NoAction;
        }
    }
}
