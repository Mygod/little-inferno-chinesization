using System;
using System.IO;

namespace Mygod.LittleInferno.Converter.ItemManifest
{
    static class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                Console.WriteLine("Analyzing {0}...", arg);
                if (Directory.Exists(arg))
                {
                    var filePath = Path.Combine(Path.GetDirectoryName(arg), "itemmanifest.dat");
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    {
                        var reader = new BinaryReader(stream);
                        var itemManifestPointer = new BinaryHeaderPointer(reader);
                        stream.Position = 0x28;
                        var itemsBinDataBytesPointer = new BinaryHeaderPointer(reader);
                        stream.Position = itemsBinDataBytesPointer.Offset;
                        var writer = new BinaryWriter(stream);
                        var bytesOffsets = new int[itemManifestPointer.Count + 1];
                        for (var i = 0; i < itemManifestPointer.Count; i++)
                        {
                            var bytes = File.ReadAllBytes(Path.Combine(arg, i + ".item"));
                            bytesOffsets[i + 1] = bytesOffsets[i] + bytes.Length;
                            writer.Write(bytes);
                        }
                        writer.Flush();
                        if (stream.Position < stream.Length) stream.SetLength(stream.Position);
                        stream.Position = itemManifestPointer.Offset;
                        for (var i = 0; i < itemManifestPointer.Count; i++)
                        {
                            stream.Position += 0x38;
                            writer.Write(bytesOffsets[i]);
                        }
                        stream.Position = 0x28;
                        writer.Write(bytesOffsets[itemManifestPointer.Count]);
                    }
                }
                else if (File.Exists(arg)) using (var reader = new BinaryReader(File.OpenRead(arg)))
                {
                    var itemManifestPointer = new BinaryHeaderPointer(reader);
                    reader.BaseStream.Position = 0x28;
                    var itemsBinDataBytesPointer = new BinaryHeaderPointer(reader);
                    var bytesOffsets = new int[itemManifestPointer.Count];
                    reader.BaseStream.Position = itemManifestPointer.Offset;
                    for (var i = 0; i < itemManifestPointer.Count; i++)
                    {
                        reader.BaseStream.Position += 0x38;
                        bytesOffsets[i] = reader.ReadInt32();
                    }
                    reader.BaseStream.Position = itemsBinDataBytesPointer.Offset;
                    var itemsBinDataBytes = reader.ReadBytes(itemsBinDataBytesPointer.Count);
                    var dir = Path.Combine(Path.GetDirectoryName(arg) ?? string.Empty, "itemsData");
                    Directory.CreateDirectory(dir);
                    for (var i = 0; i < itemManifestPointer.Count; i++)
                    {
                        var length = (i == itemManifestPointer.Count - 1 ? itemsBinDataBytesPointer.Count : bytesOffsets[i + 1])
                                        - bytesOffsets[i];
                        var array = new byte[length];
                        Array.Copy(itemsBinDataBytes, bytesOffsets[i], array, 0, length);
                        File.WriteAllBytes(Path.Combine(dir, i + ".item"), array);
                    }
                }
            }
        }
    }

    struct BinaryHeaderPointer
    {
        public BinaryHeaderPointer(BinaryReader reader)
        {
            Count = reader.ReadInt32();
            Offset = reader.ReadInt32();
        }

        public readonly int Count, Offset;
    }
}
