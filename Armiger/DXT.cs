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
using SharpDX.Toolkit.Graphics;
using GraphicsDevice = SharpDX.Toolkit.Graphics.GraphicsDevice;
using Job = System.Collections.Generic.KeyValuePair<string, byte[]>;
using JobResult = System.Tuple<string, Armiger.DXTManager.Result, byte[]>;

namespace Armiger
{
    public sealed class DXTManager
    {
        [Flags]
        public enum Result
        {
            NoAction = 0x0,
            Delete = 0x1,
            Failed = 0x2,
            CompressedBC3 = 0x3,
            GeneratedMipmaps = 0x4,
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

        public async Task<JobResult> Process(Job job, Recovery recovery)
        {
            _count++;
            var result = /* _process(file, recovery); */
                await Task.Factory.StartNew<JobResult>(() => _process(job, recovery));

            Trace.TraceInformation("Count hit: " + --_count);
            return result;
        }

        //bmp > tga > png > dds
        //public byte[] ReadFile(string file)
        //{
        //    try
        //    {
        //        byte[] fBytes = File.ReadAllBytes(file);

        //        if (fBytes.Length == 0)
        //        {
        //            switch (Path.GetExtension(file).ToLowerInvariant())
        //            {
        //                case "bmp":
        //                    return ReadFile(Path.ChangeExtension(file, "tga"));
        //                case "tga":
        //                    return ReadFile(Path.ChangeExtension(file, "png"));
        //                case "png":
        //                    return ReadFile(Path.ChangeExtension(file, "dds"));
        //                //case "dds":
        //                //default:
        //            }
        //        }
        //    }
        //    catch (IOException e)
        //    {
        //        Trace.TraceError(e.ToString());
        //    }

        //    return null;
        //}

        private JobResult _process(Job job, Recovery recovery, bool asMappable = true)
        {
            string file = job.Key;

            byte[] fBytes = job.Value;
            int fLen = fBytes.Length;

            Result code = Result.NoAction;
            byte[] output = null;

            using (var ms = new MemoryStream(fBytes))
                try
                {
                    var txFlags = asMappable ? SharpDX.Toolkit.Graphics.TextureFlags.RenderTarget : SharpDX.Toolkit.Graphics.TextureFlags.ShaderResource;

                    bool cube = file.Contains("\\cubemaps\\");

                    Texture tex;
                    if (cube)
                        tex = SharpDX.Toolkit.Graphics.TextureCube.Load(_device, ms, txFlags, ResourceUsage.Default);
                    else
                        tex = SharpDX.Toolkit.Graphics.Texture2D.Load(_device, ms, txFlags, ResourceUsage.Default);

                    using (tex)
                    {
                        if (tex.Description.BindFlags.HasFlag(BindFlags.RenderTarget | BindFlags.ShaderResource) && tex.Description.MipLevels > 1)
                        {
                            Trace.TraceInformation("Generating mipmaps...");
                            tex.GenerateMipMaps();

                            code |= Result.GeneratedMipmaps;
                        }

                        if (!tex.IsBlockCompressed)
                        {
                            Console.WriteLine(tex.Description.Format);
                            //var loadOpts = new ImageLoadInformation()
                            //{
                            //    //Format = SharpDX.DXGI.Format.bc3,
                            //};

                            var desc = tex.Description;
                            var loadOpts = new ImageLoadInformation
                            {
                                BindFlags = desc.BindFlags,
                                CpuAccessFlags = desc.CpuAccessFlags,
                                Depth = desc.Depth,
                                Filter = FilterFlags.None,
                                //FirstMipLevel = desc.MipLevels,// 0,
                                Format = desc.Format,//SharpDX.DXGI.Format.BC3_UNorm_SRgb,
                                Height = tex.Height,
                                Width = tex.Width,
                                MipFilter = FilterFlags.Box,
                                MipLevels = desc.MipLevels,
                                OptionFlags = desc.OptionFlags,
                                //PSrcInfo = new IntPtr(&imginf),
                                Usage = desc.Usage,
                            };

                            ms.Seek(0, SeekOrigin.Begin);
                            System.Diagnostics.Trace.Assert(tex.GetType() == typeof(SharpDX.Toolkit.Graphics.Texture2D));
                            System.Diagnostics.Trace.Assert(!cube);
                            //using (var newTex = SharpDX.Direct3D11.Texture2D.FromStream(_device, ms, fLen))//, loadOpts))
                            using (var newTex = SharpDX.Toolkit.Graphics.Texture2D.Load(_device, ms, txFlags, ResourceUsage.Default))//(_device, ms, fLen))//, loadOpts))
                            {
                                using (var msNew = new MemoryStream())
                                {
                                    SharpDX.Direct3D11.Texture2D.ToStream(_device, newTex, ImageFileFormat.Dds, msNew);

                                    File.Copy(file, recovery.GetBackupPath(Path.GetFileNameWithoutExtension(file) + "_orig.dds"));
                                    //recovery.Backup(file);
                                    output = msNew.ToArray();
                                }

                                Trace.TraceInformation(Path.GetFileNameWithoutExtension(file) + " converted to BC3");
                                code |= Result.CompressedBC3;
                            }
                        }
                    }
                }
                catch (SharpDXException sdx)
                {
                    if (asMappable)
                        return _process(job, recovery, false);
                    Trace.TraceError(sdx.ToString());
                    code = Result.Failed;
                }
                catch (NullReferenceException nre)
                {
                    Trace.TraceError(nre.ToString());
                    code = Result.Failed;
                }

            return new JobResult(recovery.GetBackupPath(file), code, output);
        }
    }
}