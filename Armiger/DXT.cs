using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
//using System.Diagnostics;
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
            _device = GraphicsDevice.New(DriverType.Null, DeviceCreationFlags.None, FeatureLevel.Level_11_0);
            _count = 0;
        }
        readonly GraphicsDevice _device;

        volatile int _count;

        public async Task<Result> Process(string file, Recovery recovery)
        {
            _count++;
            var result = /* _process(file, recovery); */
                await Task.Factory.StartNew<Result>(() => _process(file, recovery));

            //Trace.TraceInformation(
            Console.WriteLine("Count hit: " + --_count);
            return result;
        }

        //bmp > tga > png > dds
        private Result _process(string file, Recovery recovery, bool asMappable = true, byte[] fBytes = null)
        {
            int fLen;
            if (fBytes == null)
            {
                try
                {
                    fBytes = File.ReadAllBytes(file);
                    fLen = fBytes.Length;

                    if (fLen == 0)
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
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
                }
                catch (IOException e)
                {
                    //Trace.TraceError(e.ToString());
                    return Result.Failed;
                }
            }
            else
                fLen = fBytes.Length;

            using (var ms = new MemoryStream(fBytes))
                try
                {
                    using (var tex = SharpDX.Toolkit.Graphics.Texture.Load(_device, ms, asMappable ? SharpDX.Toolkit.Graphics.TextureFlags.RenderTarget : SharpDX.Toolkit.Graphics.TextureFlags.ShaderResource, ResourceUsage.Default))
                    {
                        if (tex.Description.BindFlags.HasFlag(BindFlags.RenderTarget | BindFlags.ShaderResource) && tex.Description.MipLevels > 1)
                        {
                            //Trace.TraceInformation("Generating mipmaps...");
                            tex.GenerateMipMaps();
                        }
                        if (!tex.IsBlockCompressed)
                        {
                            var loadOpts = new ImageLoadInformation()
                            {
                                Format = SharpDX.DXGI.Format.BC3_UNorm_SRgb,
                            };

                            //var loadOpts = new ImageLoadInformation
                            //{
                            //    //BindFlags = desc.BindFlags,
                            //    //CpuAccessFlags = desc.CpuAccessFlags,
                            //    //Depth = desc.Depth,
                            //    //Filter = FilterFlags.None,
                            //    //FirstMipLevel = 0,
                            //    Format = SharpDX.DXGI.Format.BC3_UNorm_SRgb,
                            //    //Height = tex.Height,
                            //    //Width = tex.Width,
                            //    //MipFilter = FilterFlags.,
                            //    //MipLevels = desc.MipLevels,
                            //    //OptionFlags = desc.OptionFlags,
                            //    //PSrcInfo = new IntPtr(&imginf),
                            //    //Usage = desc.Usage,
                            //};

                            ms.Seek(0, SeekOrigin.Begin);
                            System.Diagnostics.Trace.Assert(tex.GetType() == typeof(SharpDX.Toolkit.Graphics.Texture2D));
                            using (var newTex = Texture2D.FromStream(_device, ms, fLen, loadOpts))
                            {
                                using (var msNew = new MemoryStream())
                                {
                                    Texture2D.ToStream(_device, newTex, ImageFileFormat.Dds, msNew);

                                    recovery.Backup(file);
                                    using (var fstream = File.OpenWrite(file)) {
                                        msNew.WriteTo(fstream);
                                        fstream.Flush();
                                    }
                                }

                                //Trace.TraceInformation(Path.GetFileNameWithoutExtension(file) + " converted to BC3");
                                return Result.CompressedBC3;
                            }
                        }
                    }
                }
                catch (SharpDXException sdx)
                {
                    if (asMappable)
                        return _process(file, recovery, false, fBytes);
                    //Trace.TraceError(sdx.ToString());
                    return Result.Failed;
                }

            return Result.NoAction;
        }
    }
}
