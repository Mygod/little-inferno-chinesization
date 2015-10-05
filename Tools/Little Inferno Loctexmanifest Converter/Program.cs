using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Mygod.LittleInferno.Convert.Loctexmanifest
{
    static class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args.Where(File.Exists))
            {
                Console.WriteLine("Analyzing {0}...", arg);
                if (arg.ToLower().EndsWith(".xml"))
                    using (var writer = new BinaryWriter(new FileStream(arg.Remove(arg.Length - 4), FileMode.OpenOrCreate)))
                    {
                        var manifest = new LoctexManifest(XDocument.Load(arg).Element("manifest"));
                        writer.Write(manifest.Count);
                        writer.Write(16);
                        writer.Write(manifest.SelectMany(a => a).Count());
                        writer.Write(16 + manifest.Count * 12);
                        var index = 0;
                        foreach (var resource in manifest)
                        {
                            writer.Write(resource.ID);
                            writer.Write(index);
                            writer.Write(resource.Count);
                            index += resource.Count;
                        }
                        foreach (var pair in manifest.SelectMany(resource => resource))
                        {
                            writer.Write(pair.Key);
                            writer.Write(pair.Value);
                        }
                    }
                else using (var reader = new BinaryReader(new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    File.WriteAllText(arg + ".xml", new LoctexManifest(reader).ToElement().GetString());
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

    class LoctexManifest : List<Resource>
    {
        public LoctexManifest(BinaryReader reader)
        {
            BinaryHeaderPointer filesPointer = new BinaryHeaderPointer(reader), recordsPointer = new BinaryHeaderPointer(reader);
            reader.BaseStream.Position = filesPointer.Offset;
            for (var i = 0; i < filesPointer.Count; i++) Add(new Resource(reader));
            reader.BaseStream.Position = recordsPointer.Offset;
            for (var i = 0; i < filesPointer.Count; i++) this[i].Init(reader);
        }

        public LoctexManifest(XContainer element)
        {
            foreach (var e in element.Elements("resource")) Add(new Resource(e));
        }

        public XElement ToElement()
        {
            var element = new XElement("manifest");
            foreach (var resource in this) element.Add(resource.ToElement());
            return element;
        }
    }

    class Resource : Dictionary<uint, uint>
    {
        public Resource(BinaryReader reader)
        {
            ID = reader.ReadUInt32();
            reader.ReadInt32();
            Count = reader.ReadInt32();
        }

        public Resource(XElement element)
        {
            foreach (var attr in element.Attributes())
                if (attr.Name == "id") ID = StringHash.Parse(attr.Value);
                else if (attr.Name.LocalName.Length <= 4)
                    Add(BitConverter.ToUInt32(Reverse(Encoding.UTF8.GetBytes(attr.Name.LocalName)), 0), StringHash.Parse(attr.Value));
            Count = base.Count;
        }

        public readonly uint ID;
        public new readonly int Count;

        public void Init(BinaryReader reader)
        {
            for (var i = 0; i < Count; i++) Add(reader.ReadUInt32(), reader.ReadUInt32());
        }

        private static byte[] Reverse(IList<byte> bytes)
        {
            if (bytes.Count > 4) throw new NotSupportedException();
            var newByte = new byte[4];
            for (var i = 0; i < bytes.Count; i++) newByte[i] = bytes[bytes.Count - 1 - i];
            return newByte;
        }

        public XElement ToElement()
        {
            var result = new XElement("resource", new XAttribute("id", ID));
            foreach (var pair in this)
                result.Add(new XAttribute(Encoding.UTF8.GetString(Reverse(BitConverter.GetBytes(pair.Key))).Replace("\0", ""), pair.Value));
            return result;
        }
    }

    public static class StringHash
    {
        public static uint Parse(string str)
        {
            uint result;
            return uint.TryParse(str, out result) ? result : GetHash(str);
        }

        private static uint GetHash(string str)
        {
            var bytes = Encoding.Unicode.GetBytes(str.ToLower().Replace('/', '\\'));
            var result = 0xABABABAB;
            var i = 0;
            while (i < bytes.Length)
            {
                var c = (ushort)((bytes[i + 1] << 8) + bytes[i]);
                result ^= c;
                result = (result << 7) | (result >> (32 - 7));
                i += 2;
            }
            return result;
        }
    }

    public static class Helper
    {
        internal static string GetString(this XDocument doc)
        {
            using (var ms = new MemoryStream(20000))
            {
                using (var xw = XmlWriter.Create(ms, new XmlWriterSettings { Indent = true })) doc.Save(xw);
                ms.Position = 0; //reset to 0
                using (var sr = new StreamReader(ms)) return sr.ReadToEnd();
            }
        }

        internal static string GetString(this XContainer container)
        {
            return new XDocument(container).GetString();
        }
    }
}
