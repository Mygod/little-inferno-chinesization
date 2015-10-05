using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Mygod.LittleInferno.Chinese.FontGenerator
{
    public static class Program
    {
        public static string GetFullCharSet()
        {
            return "¤§°±·×÷ˇˉˊˋ‐—―‘’“”„•…‧‰′″‹›※℃℅℉℗℠™ℳ∈∏∑∕√∝∞∟∠∥∧∨∩∪∫∮∴∵∶∷∽≈≌≒≠≡≤≥≦≧≮≯⊙⊥⊿⌒○♀♂⺁⺄⺈⺋⺌⺧⺪⺮⺳⺶⺷⺻　、。〃々〈〉《》「」『』【】〒〔〕〖〗〝〞〩ㄅㄆㄇㄈㄉㄊㄋㄌㄍㄎㄏㄐㄑㄒㄓㄔㄕㄖㄗㄘㄙㄚㄛㄜㄝㄞㄟㄠㄡㄢㄣㄤㄥㄦㄧㄨㄩ㏎㏑㏕︰︳︴︵︶︷︸︹︺︻︼︽︾︿﹀﹁﹂﹃﹄﹉﹊﹋﹌﹍﹎﹏﹐﹑﹔﹕﹖﹙﹚﹛﹜﹝﹞﹟﹠﹡﹢﹣﹤﹥﹦﹨﹩﹪﹫！＂＃＄％＆＇（）＊＋，－．／：；＜＝＞？＠［＼］＾＿｀｛｜｝～￠￡￣￥".Union(Enumerable.Range(0x4e00, 0x51A6).Select(i => (char)i)).OrderBy(ch => BitConverter.ToUInt16(Encoding.Unicode.GetBytes(new string(ch, 1)), 0)).Aggregate(string.Empty, (c, s) => c + s); // full
        }

        static void Main(string[] args)
        {
            var chineseCharSet = new SortedSet<char>(new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "../../../Dictionaries")).EnumerateFiles().SelectMany(file => File.ReadAllText(file.FullName)
                    .Where(c => c >= 128)));
            foreach (var arg in args.Where(File.Exists))
            {
                Console.WriteLine("Analyzing {0}...", arg);
                var bpath = arg.Remove(arg.Length - 15);    // .font.input.xml
                var doc = XDocument.Load(arg);
                XElement root = doc.Element("font"), firstKerning = root.Element("kerning");
                foreach (var ch in root.Elements("char")) chineseCharSet.Remove(ch.Attribute("value").Value.Single());

                const int charPerRow = 20;
                const int charPerPhoto = charPerRow * charPerRow;
                int photoCount = (chineseCharSet.Count + charPerPhoto - 1) / charPerPhoto, j = 0;
                var pointDictionary = new PointF[chineseCharSet.Count];
                var pageDictionary = new int[chineseCharSet.Count];
                var enumerator = chineseCharSet.GetEnumerator();
                for (var i = 0; i < photoCount; i++)
                    using (var bitmap = new Bitmap(charPerRow * PointSizeEx, charPerRow * PointSizeEx))
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                            for (var y = 0; y < charPerRow; y++) for (var x = 0; x < charPerRow; x++)
                            {
                                if (!enumerator.MoveNext()) goto generateXml;
                                graphics.DrawString(new string(enumerator.Current, 1),
                                    new Font(new FontFamily("微软雅黑"), PointSize, FontStyle.Bold),
                                    Brushes.White, Move(pointDictionary[j] = new PointF(PointSizeEx * x, PointSizeEx * y)));
                                pageDictionary[j++] = i + 2;
                            }
                        generateXml:
                            ;
                        }
                        bitmap.Save(string.Format("{0}.page{1:00}.png", bpath, i + 2));
                    }
                var k = 0;
                foreach (var ch in chineseCharSet)
                {
                    var record = new XElement("char");
                    record.Add(new XAttribute("value", ch));
                    record.Add(new XAttribute("texpage", pageDictionary[k]));
                    record.Add(new XAttribute("texx", pointDictionary[k].X - 1));
                    record.Add(new XAttribute("texy", pointDictionary[k++].Y - 1));
                    record.Add(new XAttribute("texw", 76));
                    record.Add(new XAttribute("texh", 76));
                    record.Add(new XAttribute("offsetx", 0));
                    record.Add(new XAttribute("offsety", -76));
                    record.Add(new XAttribute("advance", 80));
                    firstKerning.AddBeforeSelf(record);
                }
                doc.Save(string.Format("{0}.font.xml", bpath));
                using (var writer = new StreamWriter(new FileStream("filelist.txt", FileMode.Create)))
                {
                    for (var i = 0; i < photoCount + 2; i++) writer.WriteLine("data/fonts/TwCen.page{0:00}.png", i);
                    writer.WriteLine();
                    for (var i = 0; i < photoCount + 2; i++)
                        writer.WriteLine("        <texture filename=\"data/fonts/TwCen.page{0:00}.png\"/>", i);
                }
            }
        }

        private static PointF Move(PointF p)
        {
            return new PointF(p.X + PointSize - 72, p.Y + PointSize - 75);
        }

        private const int PointSize = 58, PointSizeEx = 79;
    }
}
