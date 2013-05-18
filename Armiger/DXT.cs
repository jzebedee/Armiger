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
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;

namespace Armiger
{
    public sealed class DXTManager
    {
        public enum Result
        {
            NoAction = 0,
            Delete = 1,
            Skip = 2,
            Good = 3,
        }

        static readonly Lazy<DXTManager> _instance = new Lazy<DXTManager>(() => new DXTManager());
        public static DXTManager Instance { get { return _instance.Value; } }

        private DXTManager()
        {
            _device = GraphicsDevice.New(featureLevels: FeatureLevel.Level_11_0);
            _count = 0;
        }
        readonly GraphicsDevice _device;

        volatile int _count;

        public async Task<Result> Process(string file)
        {
            _count++;
            return await Task.Factory.StartNew<Result>(() => _process(file)).ContinueWith(intask =>
            {
                Console.WriteLine("Count hit: " + --_count);
                return intask.Result;
            });
        }

        //bmp > tga > png > dds
        private Result _process(string file)
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
                                return _process(Path.ChangeExtension(file, "tga"));
                            case "tga":
                                return _process(Path.ChangeExtension(file, "png"));
                            case "png":
                                return _process(Path.ChangeExtension(file, "dds"));
                            case "dds":
                            default:
                                return Result.Delete;
                        }
                    }

                    var tex = Texture.Load(_device, fstream);
                    if (!tex.IsBlockCompressed)
                    {
                        Texture.New(_device, new TextureDescription {
                            tex.
                        })
                    }

                    return Result.Skip;
                    return Result.Good;
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
