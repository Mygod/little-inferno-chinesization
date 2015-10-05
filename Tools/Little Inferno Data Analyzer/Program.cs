using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Mygod.LittleInferno.DataAnalyzer
{
    static class Program
    {
        static void Main(string[] args)
        {
            var processed = false;
            foreach (var arg in args.Where(File.Exists))
            {
                processed = true;
                Console.WriteLine("Analyzing {0}...", arg);
                using (var reader = new BinaryReader(File.OpenRead(arg)))
                {
                    int metadataLength;
                    if (arg.ToLower().EndsWith(".item")) metadataLength = 9;
                    else
                    {
                        reader.BaseStream.Position = 4;
                        metadataLength = reader.ReadInt32();
                        if ((metadataLength & 7) != 0) Console.WriteLine("WARNING: The first offset doesn't seem to be valid.");
                        metadataLength >>= 3;
                    }
                    reader.BaseStream.Position = 0;
                    var metadata = new BinaryHeaderPointer[metadataLength];
                    for (var i = 0; i < metadataLength; i++) 
                        metadata[i] = new BinaryHeaderPointer {Count = reader.ReadInt32(), Offset = reader.ReadInt32()};
                    for (var i = 0; i < metadataLength; i++)
                    {
                        var length = i == metadataLength - 1 ? reader.BaseStream.Length - metadata[i].Offset
                                                             : metadata[i + 1].Offset - metadata[i].Offset;
                        if (metadata[i].Count <= 0)
                        {
                            Console.WriteLine("Part {0}: Unknown (0 item)", i);
                            continue;
                        }
                        if (length % metadata[i].Count != 0) Console.WriteLine("WARNING: Part {0} seems to have some extra data", i);
                        var singleLength = length / metadata[i].Count;
                        Console.WriteLine("Part {0}: {1} bytes per item for {2} items", i, singleLength, metadata[i].Count);
                        reader.BaseStream.Position = metadata[i].Offset;
                        if (singleLength <= 32) File.WriteAllBytes(GetFileName(i, metadata[i].Offset), reader.ReadBytes((int) length));
                        else
                        {
                            Directory.CreateDirectory(i.ToString());
                            for (var j = 0; j < metadata[i].Count; j++) File.WriteAllBytes(i + "/"
                                + GetFileName(j, (int) reader.BaseStream.Position), reader.ReadBytes((int) singleLength));
                        }
                    }
                }
                Console.ReadKey();
            }
            if (processed) return;
            while (true) Console.WriteLine(GetHash(Console.ReadLine()));
        }

        private static string GetFileName(int id, int offset)
        {
            return string.Format("{0} (0x{1:X})", id.ToString(), offset);
        }

        private static uint GetHash(string str)
        {
            var bytes = Encoding.Unicode.GetBytes(str.ToLower().Replace('/', '\\'));
            var result = 0xABABABAB;
            var i = 0; 
            while (i < bytes.Length)
            {
                var c = (ushort) ((bytes[i + 1] << 8) + bytes[i]);
                result ^= c;
                result = (result << 7) | (result >> (32 - 7));
                i += 2;
            }
            return result;
        }
    }

    struct BinaryHeaderPointer
    {
        public int Count, Offset;
    }
}
