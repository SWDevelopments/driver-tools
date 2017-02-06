﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using DSCript;
using DSCript.Spooling;

namespace GMC2Snooper
{
    public static class StreamExtensions
    {
        public static T ReadStruct<T>(this Stream stream)
        {
            var length = Marshal.SizeOf(typeof(T));

            return stream.ReadStruct<T>(length);
        }

        public static T ReadStruct<T>(this Stream stream, int length)
        {
            var data = new byte[length];
            var ptr = Marshal.AllocHGlobal(length);

            stream.Read(data, 0, length);
            Marshal.Copy(data, 0, ptr, length);

            var t = (T)Marshal.PtrToStructure(ptr, typeof(T));

            Marshal.FreeHGlobal(ptr);
            return t;
        }
    }
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();

            Console.Title = "GMC2 Snooper";

            var filename = "";
            var startIdx = -1;
            var interactive = false;

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: gmc2snooper <file> <:index> <:-->");
                Console.WriteLine("  Loads the first model package at an index from a chunk file.");
                Console.WriteLine("  If no index is specified, the first one will be loaded.");
                Console.WriteLine("  Append '--' at the end of your arguments to interactively load each model.");
                Console.WriteLine("  ** NOTE: File must be a valid PS2 CHNK file from Driv3r or Driver: PL! **");
                return;
            }
            else
            {
                filename = args[0];

                for (int i = (args.Length - 1); i != 0; i--)
                {
                    var arg = args[i];

                    if (arg == "--" && !interactive)
                    {
                        interactive = true;
                        continue;
                    }
                    if (startIdx == -1)
                    {
                        startIdx = int.Parse(arg);
                        continue;
                    }
                }

                // set default index
                if (startIdx == -1)
                    startIdx = 1;
            }

            if (!File.Exists(filename))
            {
                Console.WriteLine("ERROR: File not found.");
                return;
            }

            if (startIdx <= 0)
            {
                Console.WriteLine("ERROR: Index cannot be zero or negative.");
                return;
            }

            var chunker = new FileChunker();
            var modPacks = new List<SpoolableBuffer>();

            chunker.SpoolerLoaded += (s, e) => {
                if (s.Context == 0x32434D47)
                    modPacks.Add((SpoolableBuffer)s);
            };

            chunker.Load(filename);

            if (modPacks.Count == 0)
            {
                Console.WriteLine($"ERROR: No model packages were found.");
                return;
            }

            var idx = (startIdx - 1);

            if (idx >= modPacks.Count)
            {
                Console.WriteLine($"ERROR: Index was larger than the actual number of models available.");
                return;
            }
            
            while (idx < modPacks.Count)
            {
                var gmc2 = new ModelPackagePS2();
                var spooler = modPacks[idx];

                var parent = spooler.Parent;

                Console.WriteLine($">> ModelPackage index: {startIdx}");

                if (parent != null)
                    Console.WriteLine($">> ModelPackage parent: 0x{parent.Context:X8}");

                using (var ms = spooler.GetMemoryStream())
                {
                    gmc2.LoadBinary(ms);
                    Console.WriteLine($">> Processed {gmc2.Models.Count} models / {gmc2.Materials.Count} materials.");
                }
                
                Console.WriteLine(">> Dumping model info...");
                DumpModelInfo(gmc2);

                //Console.WriteLine(">> Dumping texture info...");
                //DumpTextures(gmc2);

                if (interactive)
                {
                    if ((idx + 1) < modPacks.Count)
                    {
                        Console.WriteLine("Press 'SPACE' to load the next model, or press any key to exit.");

                        if (Console.ReadKey().Key == ConsoleKey.Spacebar)
                        {
                            ++idx;
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Operation completed -- no more models left to process.");
                        Console.WriteLine("Press any key to exit.");
                        Console.ReadKey();
                    }
                }
                
                // that's all, folks!
                break;
            }
        }
        
        public static void DumpModelInfo(ModelPackagePS2 gmc2)
        {
            // vif tag info :)
            for (int i = 0; i < gmc2.Models.Count; i++)
            {
                var model = gmc2.Models[i];

                Console.WriteLine($"**** Model {i + 1} / {gmc2.Models.Count} *****");
                Console.WriteLine($"Type: ({model.Type & 0xF}, {(model.Type & 0xF) >> 4})");
                Console.WriteLine($"UID: {model.UID:X8}");
                Console.WriteLine($"Handle: {model.Handle:X8}");
                Console.WriteLine($"Unknown: ({model.Unknown1:X4},{model.Unknown2:X4})");
                Console.WriteLine($"Transform1: ({model.Transform1.X:F4},{model.Transform1.Y:F4},{model.Transform1.Z:F4})");
                Console.WriteLine($"Transform2: ({model.Transform2.X:F4},{model.Transform2.Y:F4},{model.Transform2.Z:F4})");

                for (int ii = 0; ii < model.SubModels.Count; ii++)
                {
                    var subModel = model.SubModels[ii];

                    Console.WriteLine($"******** Sub model {ii + 1} / {model.SubModels.Count} *********");
                    Console.WriteLine($"Type: {subModel.Type}");
                    Console.WriteLine($"Flags: {subModel.Flags}");
                    Console.WriteLine($"Unknown: ({subModel.Unknown1},{subModel.Unknown2})");
                    Console.WriteLine($"TexId: {subModel.TextureId}");
                    Console.WriteLine($"TexSource: {subModel.TextureSource:X4}");

                    if (subModel.HasVectorData)
                    {
                        var v1 = subModel.V1;
                        var v2 = subModel.V2;
                        Console.WriteLine($"V1: ({v1.X:F4},{v1.Y:F4},{v1.Z:F4})");
                        Console.WriteLine($"V2: ({v2.X:F4},{v2.Y:F4},{v2.Z:F4})");
                    }

                    if (subModel.HasTransform)
                    {
                        var transform = subModel.Transform;
                        Console.WriteLine($"Transform X: ({transform.X.X:F4},{transform.X.Y:F4},{transform.X.Z:F4},{transform.X.W:F4})");
                        Console.WriteLine($"Transform Y: ({transform.Y.X:F4},{transform.Y.Y:F4},{transform.Y.Z:F4},{transform.Y.W:F4})");
                        Console.WriteLine($"Transform Z: ({transform.Z.X:F4},{transform.Z.Y:F4},{transform.Z.Z:F4},{transform.Z.W:F4})");
                    }
                    
                    using (var ms = new MemoryStream(subModel.ModelDataBuffer))
                    {
                        while (ms.Position < ms.Length)
                        {
                            // check alignment
                            if ((ms.Position & 0x3) != 0)
                                ms.Align(4);

                            DumpVIFTag(ms);
                        }
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();
            }
        }

        public static long[] VIFCurrent         = new long[4];

        public static int VIFITop               = 0;
        
        public static int VIFCycle              = 0; // 'CL'
        public static int VIFCycleWriteLen      = 0; // 'WL'

        public static int VIFMode               = 0;

        public static string[] VIFModeTypes     = new[] {
            "NORMAL",
            "OFFSET",
            "DIFFERENCE",
        };
        
        public static uint[][] VIFMasks         = new uint[4][];

        public static string[] VIFMaskTypes     = new[] {
            "DATA",
            "MASK_ROW",
            "MASK_COL",
            "WRITE_PROTECT",
        };

        public static void DumpVIFTag(Stream stream)
        {
            var vif = stream.ReadStruct<PS2.VifTag>();

            var imdt = new VifImmediate(vif.Imdt);
            var cmd = new VifCommand(vif.Cmd);
            
            var cmdName = "";
            var cmdInfo = "";

            var sb = new StringBuilder();

            switch ((VifCommandType)vif.Cmd)
            {
            case VifCommandType.Nop:
                cmdName = "NOP";
                stream.Position += 4;
                break;
            case VifCommandType.StCycl:
                VIFCycle = imdt.IMDT_STCYCL_CL;
                VIFCycleWriteLen = imdt.IMDT_STCYCL_WL;

                cmdName = "STCYCL";
                cmdInfo = String.Format("{0,-10}{1,-10}", 
                    $"CL:{VIFCycle},",
                    $"WL:{VIFCycleWriteLen}");
                break;
            case VifCommandType.Offset:
                cmdName = "OFFSET";
                cmdInfo = String.Format("{0,-10}", $"OFFSET:{imdt.IMDT_OFFSET:X}");
                stream.Position += 4;
                break;
            case VifCommandType.ITop:
                VIFITop = imdt.IMDT_ITOP;

                cmdName = "ITOP";
                cmdInfo = String.Format("{0,-10}", $"ADDR:{VIFITop:X}");

                stream.Position += 4;
                break;
            case VifCommandType.StMod:
                VIFMode = imdt.IMDT_STMOD;

                cmdName = "STMOD";
                cmdInfo = String.Format("{0,-10}", $"MODE:{VIFMode} ({VIFModeTypes[VIFMode]})");
                break;
            case VifCommandType.MsCal:
                cmdName = "MSCAL";
                cmdInfo = String.Format("{0,-10}", $"EXECADDR:{imdt.IMDT_MSCAL:X}");
                stream.Position += 4;
                break;
            case VifCommandType.MsCnt:
                cmdName = "MSCNT";
                break;
            case VifCommandType.StMask:
                var stmask = stream.ReadUInt32();

                cmdName = "STMASK";
                cmdInfo = String.Format("{0,-10}", $"MASK:{stmask :X8}");
                
                sb.AppendFormat("-> {0,-16}{1,-16}{2,-16}{3,-16}", 
                    "MASK_X", 
                    "MASK_Y", 
                    "MASK_Z", 
                    "MASK_W");

                sb.AppendLine();

                for (int m = 0; m < 4; m++)
                {
                    sb.Append("-> ");

                    VIFMasks[m] = new uint[4];

                    for (int mI = 0; mI < 4; mI++)
                    {
                        var msk = (stmask >> ((m * 8) + (mI * 2))) & 0x3;

                        VIFMasks[m][mI] = msk;

                        sb.AppendFormat("{0,-16}", VIFMaskTypes[msk]);
                    }

                    sb.AppendLine($"; V{m + 1}");
                }
                break;
            case VifCommandType.Flush:
                cmdName = "FLUSH";
                stream.Position += 4;
                break;
            case VifCommandType.Direct:
                cmdName = "DIRECT";
                cmdInfo = String.Format("{0,-10}", $"SIZE:{imdt.IMDT_DIRECT:X}");
                stream.Position += ((imdt.IMDT_DIRECT * 16) + 4);
                break;
            default:
                if (Enum.IsDefined(typeof(VifCommandType), (int)vif.Cmd))
                {
                    Console.WriteLine($">> Unhandled VIF command '{(VifCommandType)vif.Cmd}', I might crash!");
                    stream.Position += 4;
                }
                else
                {
                    if (cmd.P == 3)
                    {
                        cmdName = cmd.ToString();
                        cmdInfo = String.Format("{0,-10}{1,-10}",
                            $"ADDR:{imdt.ADDR:X} ({imdt.ADDR * 16:X}),",
                            $"NUM:{vif.Num}");
                    }
                    else
                    {
                        cmdName = $"$$CMD_{vif.Cmd:X2}$$";
                        cmdInfo = String.Format("{0,-10}{1,-10}{2,-10}",
                            $"ADDR:{imdt.ADDR:X} ({imdt.ADDR * 16:X}),",
                            $"NUM:{vif.Num},",
                            $"IRQ:{vif.Irq}");
                    }
                }
                break;
            }

            var props = "";

            if (imdt.FLG)
                props += "+FLAG ";
            if (imdt.USN)
                props += "+UNSIGNED ";
            
            Console.WriteLine($"  {cmdName,-16}{" : ",4}{props,-16}{": ",4}{cmdInfo,-8}");

            if (cmd.P == 3)
            {
                var packType = cmd.GetUnpackDataType();
                
                if (packType == VifUnpackType.Invalid)
                {
                    Console.WriteLine($"Invalid VIF unpack type '{vif.ToString()}'!");
                }
                else
                {
                    var logReads = true;

                    // packSize and packNum can be -1,
                    // but not since we're checking against invalid types
                    var packSize = cmd.GetUnpackDataSize();
                    var packNum = cmd.GetUnpackDataCount();

                    var wl = VIFCycleWriteLen;

                    for (int i = 0; i < vif.Num; i++)
                    {
                        // indent line
                        if (wl == VIFCycleWriteLen)
                            sb.Append($"-> [{i + 1:D4}]: ");

                        switch (packSize)
                        {
                        // byte
                        case 1:
                            {
                                for (int n = 0; n < packNum; n++)
                                {
                                    long val = (imdt.USN) ? stream.ReadByte() : (sbyte)stream.ReadByte();

                                    if (imdt.FLG && (val > 127))
                                        val -= 128;

                                    if ((packType & (VifUnpackType.V3_8 | VifUnpackType.V4_8)) != 0)
                                    {
                                        var fVal = (packType == VifUnpackType.V4_8) ? (val / 128.0f) : (val / 128.0f);

                                        if (fVal < 0f)
                                        {
                                            sb.Append($"{fVal,-8:F4}");
                                        }
                                        else
                                        {
                                            sb.Append($" {fVal,-7:F4}");
                                        }
                                    }
                                    else
                                    {
                                        if (val < 0)
                                        {
                                            sb.Append($"{val,-8}");
                                        }
                                        else
                                        {
                                            sb.Append($" {val,-7}");
                                        }
                                    }
                                }
                            }
                            break;
                        // short
                        case 2:
                            {
                                for (int n = 0; n < packNum; n++)
                                {
                                    long val = (imdt.USN) ? stream.ReadUInt16() : (long)stream.ReadInt16();

                                    if (imdt.FLG && (val > 127))
                                        val -= 128;

                                    if (packType == VifUnpackType.V4_5551)
                                    {
                                        var r = (val >> 0) & 0x1F;
                                        var g = (val >> 5) & 0x1F;
                                        var b = (val >> 10) & 0x1F;
                                        var a = (val >> 15) & 0x1;

                                        sb.AppendFormat("r:{0,-6}", r);
                                        sb.AppendFormat("g:{0,-6}", g);
                                        sb.AppendFormat("b:{0,-6}", b);
                                        sb.AppendFormat("a:{0,-6}", a);
                                    }
                                    else
                                    {
                                        if ((packType & (VifUnpackType.S_16 | VifUnpackType.V4_16)) != 0)
                                        {
                                            var fVal = (packType == VifUnpackType.S_16) ? (val / 2048.0f) : (val / 256.0f);

                                            if (fVal < 0f)
                                            {
                                                sb.Append($"{fVal,-8:F4}");
                                            }
                                            else
                                            {
                                                sb.Append($" {fVal,-7:F4}");
                                            }
                                        }
                                        else
                                        {
                                            if (val < 0)
                                            {
                                                sb.Append($"{val,-8}");
                                            }
                                            else
                                            {
                                                sb.Append($" {val,-7}");
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        // int
                        case 4:
                            {
                                for (int n = 0; n < packNum; n++)
                                {
                                    long val = (imdt.USN) ? stream.ReadUInt32() : (long)stream.ReadInt32();

                                    if (val < 0)
                                    {
                                        sb.Append($"{val,-8}");
                                    }
                                    else
                                    {
                                        sb.Append($" {val,-7}");
                                    }
                                }
                            }
                            break;
                        }

                        if (--wl == 0)
                        {
                            sb.AppendLine("");
                            wl = VIFCycleWriteLen;
                        }
                    }

                    if (logReads)
                        Console.Write(sb.ToString());
                }
            }
            else
            {
                // generic info
                if (sb.Length > 0)
                    Console.Write(sb.ToString());
            }
        }

        public static void DumpTextures(ModelPackagePS2 gmc2)
        {
            // dump textures
            foreach (var tex in gmc2.Textures)
            {
                Console.WriteLine($"texture {tex.Reserved:X16} {{");

                Console.WriteLine($"  type = {tex.Type};");
                Console.WriteLine($"  flags = 0x{tex.Flags:X};");
                Console.WriteLine($"  width = {tex.Width};");
                Console.WriteLine($"  height = {tex.Height};");
                Console.WriteLine($"  unknown1 = 0x{tex.Unknown1:X};");
                Console.WriteLine($"  dataOffset = 0x{tex.DataOffset:X};");
                Console.WriteLine($"  unknown2 = 0x{tex.Unknown2:X};");

                Console.WriteLine($"  cluts[{tex.Modes}] = [");

                foreach (var mode in tex.CLUTs)
                    Console.WriteLine($"    0x{mode:X},");

                Console.WriteLine("  ];");

                Console.WriteLine("}");
            }
        }

        public static void TestImageViewer()
        {
            byte[] TSC2Data;
            byte[] TSC2Data2;

            using (Stream f = new FileStream(@"C:\Users\Tech\Desktop\Swizzling\dsPS2_17", FileMode.Open, FileAccess.Read, FileShare.Read))
            using (Stream f2 = new FileStream(@"C:\Users\Tech\Desktop\Swizzling\d3SP2", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                TSC2Data = new byte[(int)f.Length];
                TSC2Data2 = new byte[(int)f2.Length];

                f.Read(TSC2Data, 0, (int)f.Length);
                f2.Read(TSC2Data2, 0, (int)f2.Length);
            }

            // BitmapHelper img2 = new BitmapHelper(TSC2Data, 128, 256, 0x980, PixelFormat.Format8bppIndexed);
            // BitmapHelper img3 = new BitmapHelper(TSC2Data, 128, 256, 0xA980, PixelFormat.Format8bppIndexed);

            BitmapHelper img2 = new BitmapHelper(TSC2Data, 128, 128, 0xF00, PixelFormat.Format8bppIndexed);
            BitmapHelper img3 = new BitmapHelper(TSC2Data, 128, 256, 0x2D80, PixelFormat.Format8bppIndexed);

            BitmapHelper img4 = new BitmapHelper(TSC2Data2, 128, 256, 0x980, PixelFormat.Format8bppIndexed);
            BitmapHelper img5 = new BitmapHelper(TSC2Data2, 128, 256, 0xA980, PixelFormat.Format8bppIndexed);

            // swizzle testing from TSC2
            img2.Unswizzle(128, 128, SwizzleType.Swizzle8bit);
            img3.Unswizzle(128, 256, SwizzleType.Swizzle8bit);
            img4.Unswizzle(128, 256, SwizzleType.Swizzle8bit);
            img5.Unswizzle(128, 256, SwizzleType.Swizzle8bit);

            img3.Read8bppCLUT(TSC2Data, 0x2980);

            BMPViewer viewer = new BMPViewer();

            img3.CLUTFromRGB(TSC2Data, 0x2980, 0xAD80, 0xB180);
            viewer.AddImage(img3);

            img3.Bitmap.Save(@"C:\Users\Tech\Desktop\Swizzling\d3PS2_van1.bmp", ImageFormat.Bmp);

            img3.CLUTFromRGB(TSC2Data, 0xB980, 0xBD80, 0xC180);
            viewer.AddImage(img3);

            img4.CLUTFromRGB(TSC2Data2, 0x580, 0x8980, 0x8D80);
            viewer.AddImage(img4);
            img4.CLUTFromRGB(TSC2Data2, 0x9580, 0x9980, 0x9D80);
            viewer.AddImage(img4);

            img5.CLUTFromRGB(TSC2Data2, 0xA580, 0x12980, 0x12D80);
            viewer.AddImage(img5);

            img5.Bitmap.Save(@"C:\Users\Tech\Desktop\Swizzling\d3PS2_chally1.bmp", ImageFormat.Bmp);

            img5.CLUTFromRGB(TSC2Data2, 0x13580, 0x13980, 0x13D80);
            viewer.AddImage(img5);

            viewer.Init();

            Application.Run(viewer);

            // img2.Bitmap.Save(@"C:\Users\Tech\Desktop\Swizzling\d3PS2_unswizzled.bmp", ImageFormat.Bmp);


            Console.ReadKey();
        }
    }
}
