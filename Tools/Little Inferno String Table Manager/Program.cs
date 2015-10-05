using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Mygod.LittleInferno.StringTableManager
{
    static class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args.Where(File.Exists))
                if (arg.EndsWith(".xml") && !arg.EndsWith(".anim.xml")) StringTableHelper.Encode(arg);
                else StringTableHelper.Decode(arg);
        }
    }

    static class StringTableHelper
    {
        public static void Encode(string arg)
        {
            var texts = new LocalizedTexts(XDocument.Load(arg).Element("strings"));
            var stringPointers = new StringPointer[texts.Count];
            var pointerPointers = new List<LanguagePointer>(texts.Count);
            var stringOffsets = new Dictionary<string, int>();
            int pointerIndex = 0, offset = 0;
            for (var i = 0; i < texts.Count; i++)
            {
                stringPointers[i].Index = pointerIndex;
                pointerIndex += stringPointers[i].Count = texts[i].Count;
                foreach (var pair in texts[i])
                {
                    if (!stringOffsets.ContainsKey(pair.Value))
                    {
                        pointerPointers.Add(new LanguagePointer(pair.Key, offset));
                        stringOffsets.Add(pair.Value, offset);
                        offset += Encoding.UTF8.GetByteCount(pair.Value) + 1;
                    }
                    else pointerPointers.Add(new LanguagePointer(pair.Key, stringOffsets[pair.Value]));
                }
            }
            using (var stream = new FileStream(arg.Remove(arg.Length - 4), FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                var reader = new BinaryReader(stream);
                var writer = new BinaryWriter(stream);
                int offsetOffset;
                byte[] oldBytes;
                var isItem = arg.ToLower().EndsWith(".item.xml");
                if (isItem)
                {
                    stream.Position = 0x40;
                    var extraBytesCount = reader.ReadInt32();
                    stream.Position = reader.ReadInt32();
                    oldBytes = reader.ReadBytes(extraBytesCount + 3);
                    offsetOffset = 0x3C;
                }
                else
                {
                    stream.Position = 4;
                    var metadataLength = reader.ReadInt32();
                    if ((metadataLength & 7) != 0) Console.WriteLine("WARNING: The first offset doesn't seem to be valid.");
                    offsetOffset = ((metadataLength >> 3) << 3) - 4;
                    oldBytes = new byte[0];
                }
                stream.Position = offsetOffset;
                var tableOffset = stream.Position = reader.ReadInt32();
                writer.Write(texts.Count);
                writer.Write(pointerPointers.Count);
                foreach (var pointer in stringPointers)
                {
                    writer.Write(pointer.Index);
                    writer.Write(pointer.Count);
                }
                foreach (var pointer in pointerPointers)
                {
                    writer.Write(pointer.LanguageID);
                    writer.Write(pointer.Offset);
                }
                foreach (var pair in stringOffsets.OrderBy(p => p.Value))
                {
                    writer.Write(Encoding.UTF8.GetBytes(pair.Key));
                    writer.Write((byte)0);
                }
                var oldBytesOffset = (int)stream.Position;
                writer.Write(oldBytes);
                writer.Flush();
                if (stream.Position < stream.Length) stream.SetLength(stream.Position);
                stream.Position = offsetOffset - 4;
                writer.Write((int)(stream.Length - tableOffset - oldBytes.LongLength));
                if (!isItem) return;
                stream.Position += 8;
                writer.Write(oldBytesOffset);
            }
        }

        public static void Decode(string arg)
        {
            Console.WriteLine("Decoding {0}...", arg);
            LocalizedTexts texts;
            using (var stream = new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream))
            {
                if (arg.ToLower().EndsWith(".item")) stream.Position = 0x3C;
                else
                {
                    stream.Position = 4;
                    var metadataLength = reader.ReadInt32();
                    if ((metadataLength & 7) != 0) Console.WriteLine("WARNING: The first offset doesn't seem to be valid.");
                    stream.Position = ((metadataLength >> 3) << 3) - 4;
                }
                stream.Position = reader.ReadInt32();
                int numStrings = reader.ReadInt32(), numPointers = reader.ReadInt32();
                var stringPointers = new StringPointer[numStrings];
                for (var i = 0; i < numStrings; i++) stringPointers[i] = new StringPointer(reader);
                var pointerPointers = new LanguagePointer[numPointers];
                for (var i = 0; i < numPointers; i++) pointerPointers[i] = new LanguagePointer(reader);
                var baseOffset = stream.Position;
                texts = new LocalizedTexts(numStrings);
                foreach (var stringPointer in stringPointers)
                {
                    var text = new LocalizedText(stringPointer.Count);
                    for (var i = 0; i < stringPointer.Count; i++)
                    {
                        var pointer = pointerPointers[stringPointer.Index + i];
                        stream.Position = baseOffset + pointer.Offset;
                        text.Add(pointer.LanguageID, ReadString(reader));
                    }
                    texts.Add(text);
                }
            }
            File.WriteAllText(arg + ".xml", texts.ToString());
        }

        private static string ReadString(BinaryReader reader)
        {
            var strBytes = new List<byte>();
            int b;
            while ((b = reader.ReadByte()) > 0) strBytes.Add((byte)b);
            return Encoding.UTF8.GetString(strBytes.ToArray());
        }
    }

    struct StringPointer
    {
        public int Index, Count;

        public StringPointer(BinaryReader reader)
        {
            Index = reader.ReadInt32();
            Count = reader.ReadInt32();
        }
    }

    struct LanguagePointer
    {
        public readonly uint LanguageID;
        public readonly int Offset;

        public LanguagePointer(BinaryReader reader)
        {
            LanguageID = reader.ReadUInt32();
            Offset = reader.ReadInt32();
        }
        public LanguagePointer(uint languageID, int offset)
        {
            LanguageID = languageID;
            Offset = offset;
        }
    }

    public class LocalizedTexts : List<LocalizedText>
    {
        public LocalizedTexts()
        {
        }
        public LocalizedTexts(int capacity) : base(capacity)
        {
        }
        public LocalizedTexts(XContainer element)
        {
            foreach (var sub in element.Elements("string")) Add(new LocalizedText(sub));
        }

        public XElement ToElement()
        {
            var result = new XElement("strings");
            foreach (var text in this) result.Add(text.ToElement());
            return result;
        }

        public override string ToString()
        {
            return ToElement().GetString();
        }
    }

    public class LocalizedText : Dictionary<uint, string>
    {
        public LocalizedText()
        {
        }
        public LocalizedText(int capacity) : base(capacity)
        {
        }
        public LocalizedText(XElement element)
        {
            foreach (var attr in element.Attributes().Where(a => a.Name.LocalName.Length <= 4))
                Add(BitConverter.ToUInt32(Reverse(Encoding.UTF8.GetBytes(attr.Name.LocalName)), 0), attr.Value);
        }

        public XElement ToElement()
        {
            var result = new XElement("string");
            foreach (var pair in this) result.Add(new XAttribute(Encoding.UTF8.GetString(Reverse(BitConverter.GetBytes(pair.Key)))
                .Replace("\0", ""), pair.Value));
            return result;
        }

        private static byte[] Reverse(IList<byte> bytes)
        {
            if (bytes.Count > 4) throw new NotSupportedException();
            var newByte = new byte[4];
            for (var i = 0; i < bytes.Count; i++) newByte[i] = bytes[bytes.Count - 1 - i];
            return newByte;
        }

        public override string ToString()
        {
            return ToElement().GetString();
        }
    }

    public static class Helper
    {
        internal static string GetString(this XDocument doc)
        {
            using (var ms = new MemoryStream(20000))
            {
                using (var xw = XmlWriter.Create(ms, new XmlWriterSettings {Indent = true})) doc.Save(xw);
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
