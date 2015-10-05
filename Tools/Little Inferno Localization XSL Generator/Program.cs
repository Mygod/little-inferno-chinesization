using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Mygod.LittleInferno.Localization.ChineseInjector
{
    static class Program
    {
        static void Main(string[] args)
        {
            var ecDic = new Dictionary<string, string> { { string.Empty, string.Empty } };
            foreach (var file in new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "../../../Dictionaries")).EnumerateFiles())
                if (file.FullName.ToLower().EndsWith(".special.txt"))
                {
                    var lines = File.ReadAllText(file.FullName).Split(new[] { '=' }, StringSplitOptions.None);
                    if (lines.Length <= 1) continue;
                    if (lines.Length > 2) Console.WriteLine("WARNING: Incorrect line count. Details: {0}", file.FullName);
                    string en = lines[0].Replace("\r\n", "\n").Trim('\n'), cn = lines[1].Replace("\r\n", "\n").Trim('\n');
                    if (!ecDic.ContainsKey(en)) ecDic.Add(en, cn);
                    else if (ecDic[en] != cn) Console.WriteLine("WARNING: Same english but different translations!{0}{1}{0}{2}",
                        Environment.NewLine, ecDic[en], cn);
                }
                else foreach (var str in File.ReadAllText(file.FullName).Split(new[] { "\r\n\r\n" }, StringSplitOptions.None))
                {
                    var lines = str.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    int start = 0, end = lines.Length;
                    while (start < lines.Length && (string.IsNullOrWhiteSpace(lines[start]) || lines[start].StartsWith("=="))) start++;
                    while (end >= start && (string.IsNullOrWhiteSpace(lines[end - 1]) || lines[end - 1].StartsWith("=="))) end--;
                    lines = lines.Skip(start).Take(end - start).ToArray();
                    if (lines.Length <= 1) continue;
                    if ((lines.Length & 1) > 0) Console.WriteLine("WARNING: Incorrect line count. Details: {0}", str);
                    string en = lines.Take(lines.Length >> 1).Aggregate(string.Empty, (c, s) => c + s + "\n"),
                            cn = lines.Skip(lines.Length >> 1).Aggregate(string.Empty, (c, s) => c + s + "\n");
                    en = en.Remove(en.Length - 1);
                    cn = cn.TrimEnd('\n').Replace(@"\n", "\n");
                    if (cn.ToUpper().StartsWith("[C] ")) cn = cn.Remove(0, 4);
                    else if (file.FullName.ToLower().EndsWith("dialogs.txt"))
                    {
                        cn = cn.Replace("......", "……");
                        const int charPerLine = 11;
                        var i = 0;
                        for (var j = 0; j < charPerLine; j++)
                        {
                            if (i >= cn.Length) break;
                            if (cn[i++] == '．') j--;
                        }
                        while (i < cn.Length)
                        {
                            while (Punctuations.Contains(cn[i])) i--;
                            cn = cn.Insert(i, "\n");
                            for (var j = 0; j < charPerLine; j++)
                            {
                                if (i >= cn.Length) break;
                                if (cn[i++] == '．') j--;
                            }
                            i++;
                        }
                        cn = cn.Replace("……", "......");
                    }
                    if (!ecDic.ContainsKey(en)) ecDic.Add(en, cn);
                    else if (ecDic[en] != cn) Console.WriteLine("WARNING: Same english but different translations!{0}{1}{0}{2}",
                        Environment.NewLine, ecDic[en], cn);
                }

            args = args.Where(a => !a.ToLower().EndsWith("filelist.txt", StringComparison.InvariantCultureIgnoreCase))
                       .Union(args.Where(a => a.ToLower().EndsWith("filelist.txt", StringComparison.InvariantCultureIgnoreCase))
                           .SelectMany(a => File.ReadAllText(a)
                           .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)))
                       .Union(args.Where(Directory.Exists).SelectMany(a => new DirectoryInfo(a)
                           .EnumerateFiles("*.anim.xml", SearchOption.AllDirectories)).Select(f => f.FullName))
                       .ToArray();
            var processed = false;
            foreach (var argTemp in args)
            {
                var arg = argTemp;
                if (arg.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase) &&
                    !arg.EndsWith(".anim.xml", StringComparison.InvariantCultureIgnoreCase)) arg = arg.Remove(arg.Length - 4);
                var xmlPath = arg + ".xml";
                if (File.Exists(arg))
                {
                    processed = true;
                    if (arg.ToLower().EndsWith(".bak", StringComparison.InvariantCultureIgnoreCase))
                        RestoreBackup(arg.Remove(arg.Length - 4));
                    else
                    {
                        if (File.Exists(arg + ".bak")) RestoreBackup(arg);
                        try
                        {
                            StringTableHelper.Decode(arg);
                        }
                        catch (NoStringException)
                        {
                            Console.WriteLine("WARNING: No strings here!");
                            continue;
                        }
                        File.Copy(arg, arg + ".bak");   // backup
                        var doc = XDocument.Load(xmlPath);
                        var rollback = true;
                        var forceRollback = false;
                        foreach (var element in doc.Element("strings").Elements("string")
                                                   .Where(element => element.Attribute("en") != null && element.Attribute("de") != null))
                        {
                            if (element.Attribute("french") == null)
                            {
                                element.Add(new XAttribute("french", element.Attribute("fr").Value));   // backup
                                element.Attribute("fr").Value = element.Attribute("en").Value;
                            }
                            var en = element.Attribute("fr").Value;
                            if (ecDic.ContainsKey(en))
                            {
                                if (element.Attribute("en") == null)
                                {
                                    element.Add(new XAttribute("en", ecDic[en]));
                                    rollback = false;
                                }
                                else if (element.Attribute("en").Value != ecDic[en])
                                {
                                    element.Attribute("en").Value = ecDic[en];
                                    rollback = false;
                                }
                            }
                            else
                            {
                                if (element.Attribute("de") != null)
                                {
                                    Console.WriteLine("ERROR: Unexpected content! Details: {0}", Environment.NewLine + en);
                                    forceRollback = true;
                                }
                                if (element.Attribute("en") != null) element.Attribute("en").Remove();
                            }
                        }
                        doc.Save(xmlPath);
                        StringTableHelper.Encode(xmlPath);
                        File.Delete(xmlPath);
                        if (rollback || forceRollback)
                        {
                            Console.WriteLine("Rolling back...");
                            File.Delete(arg);
                            File.Move(arg + ".bak", arg);
                        }
                        else Console.WriteLine("Checked!");
                    }
                }
                else if (File.Exists(xmlPath))
                {
                    processed = true;
                    if (File.Exists(xmlPath + ".bak")) RestoreBackup(xmlPath);
                    File.Copy(xmlPath, xmlPath + ".bak");   // backup
                    var doc = XDocument.Load(xmlPath);
                    var rollback = true;
                    var forceRollback = false;
                    foreach (var name in doc.XPathSelectElements("//name").Union(doc.XPathSelectElements("//text"))
                                            .Union(doc.XPathSelectElements("//description")))
                    {
                        XElement en = name.XPathSelectElement("string[@lang='en']"), fr = name.XPathSelectElement("string[@lang='fr']");
                        if (en == null || fr == null) continue;
                        var enValue = fr.Attribute("data").Value = en.Attribute("data").Value;
                        if (ecDic.ContainsKey(enValue))
                        {
                            if (en.Attribute("data").Value != ecDic[enValue])
                            {
                                en.Attribute("data").Value = ecDic[enValue];
                                rollback = false;
                            }
                        }
                        else
                        {
                            if (name.XPathSelectElement("string[@lang='de']") != null)
                            {
                                Console.WriteLine("ERROR: Unexpected content! Details: "
                                    + Environment.NewLine + en.Attribute("data").Value);
                                forceRollback = true;
                            }
                        }
                    }
                    if (rollback || forceRollback)
                    {
                        Console.WriteLine("Rolling back...");
                        File.Delete(xmlPath);
                        File.Move(xmlPath + ".bak", xmlPath);
                    }
                    else
                    {
                        doc.Save(xmlPath);
                        Console.WriteLine("Checked!");
                    }
                }
            }
            if (!processed) GenerateWordPackDictionary();
            Console.ReadKey();
        }

        private const string WordDictionary = "东风,何处,人间,风流,归去,春风,西风,归来,江南,相思,梅花,千里,回首,明月,多少,如今,阑干,年年,万里,一笑,黄昏,当年,天涯,相逢,芳草,尊前,一枝,风雨,流水,依旧,风吹,风月,多情,故人,当时,无人,斜阳,不知,不见,深处,时节,平生,凄凉,春色,匆匆,功名,一点,无限,今日,天上,杨柳,西湖,桃花,扁舟,消息,憔悴,何事,芙蓉,神仙,一片,桃李,人生,十分,心事,黄花,一声,佳人,长安,东君,断肠,而今,鸳鸯,为谁,十年,去年,少年,海棠,寂寞,无情,不是,时候,肠断,富贵,蓬莱,昨夜,行人,今夜,谁知,不似,江上,悠悠,几度,青山,何时,天气,惟有,一曲,月明,往事";

        private static void GenerateWordPackDictionary()
        {
            var doc = new XDocument();
            var root = new XElement("words");
            doc.Add(root);
            var dict = WordDictionary.Split(',');
            foreach (var word in dict)
                root.Add(new XElement("word", new XAttribute("str", word), new XAttribute("probability", 1.0 / dict.Length)));
            doc.Save("wordPackDict.dat.xml");
        }

        private static void RestoreBackup(string originalPath)
        {
            File.Delete(originalPath);
            File.Move(originalPath + ".bak", originalPath);
        }

        private static readonly HashSet<char> Punctuations = new HashSet<char>("。，；：？！—…~．");
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
                var numStrings = reader.ReadInt32();
                if (numStrings <= 0) throw new NoStringException();
                texts = new LocalizedTexts(numStrings);
                var numPointers = reader.ReadInt32();
                var stringPointers = new StringPointer[numStrings];
                for (var i = 0; i < numStrings; i++) stringPointers[i] = new StringPointer(reader);
                var pointerPointers = new LanguagePointer[numPointers];
                for (var i = 0; i < numPointers; i++) pointerPointers[i] = new LanguagePointer(reader);
                var baseOffset = stream.Position;
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

    class NoStringException : Exception
    {
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
        public LocalizedTexts(int capacity)
            : base(capacity)
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
        public LocalizedText(int capacity)
            : base(capacity)
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
