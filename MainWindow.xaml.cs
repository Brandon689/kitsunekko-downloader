using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using System.Net.Http;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace KitsunekkoDownloader
{
    public partial class MainWindow : Window
    {
        HttpClient h = new();
        HtmlParser parser = new HtmlParser();
        string baseAddress = "https://kitsunekko.net";
        string local = "../../subtitles/";
        string dataFile = "../../data.json";
        public MainWindow()
        {
            InitializeComponent();
            h.BaseAddress = new Uri(baseAddress);
            Directory.CreateDirectory(local);
            //var tree = JsonSerializer.Deserialize<List<Record>>(File.ReadAllText(dataFile));
            //var dir = Directory.GetDirectories(local);
            //for (int i = 0; i < dir.Length; i++)
            //{
            //    var f = Directory.GetFiles(dir[i]);
            //    if (f.Length == 0)
            //    {
            //        Console.WriteLine(dir[i]);
            //    }
            //}
            //for (int i = 0; i < tree.Count; i++)
            //{
            //    if (Directory.Exists(local + tree[i].NameEscaped))
            //    {
            //        int cnt = Directory.GetFiles(local + tree[i].NameEscaped).Length;
            //        if (cnt != tree[i].Links.Count)
            //        {
            //            // Console.WriteLine(tree[i].Name);
            //        }
            //    }
            //    else
            //    {
            //        //  Console.WriteLine(tree[i].Name);
            //    }
            //}
        }

        private async void beginButton_Click(object sender, RoutedEventArgs e)
        {
            progress.IsIndeterminate = true;
            var z = await Directories();
            List<Record> r = new();
            if (File.Exists("../../data.json"))
            {
                r = JsonSerializer.Deserialize<List<Record>>(File.ReadAllText(dataFile));
            }
            for (int i = 0; i < z.Count; i++)
            {
                if (r.FirstOrDefault(x => x.Name == z[i].Item1) != null)
                {
                    //Console.WriteLine($"skip {z[i].Item1}");
                    //continue;
                }
                Record roi = new()
                {
                    Name = z[i].Item1,
                    Url = z[i].Item2,
                    NameEscaped = sanitize(z[i].Item1, true)
                };
                if (Directory.Exists(local + roi.NameEscaped))
                {
                    Console.WriteLine($"skip {z[i].Item1}");
                    continue;
                }

                var l = await Anime(roi);
                roi.Links = l.ToList();
                r.Add(roi);

                if (i % 5 == 0 || i == z.Count || i == z.Count - 1 || i == z.Count - 2)
                {
                    File.WriteAllText("../data.json",
                        JsonSerializer.Serialize(r));
                }
            }
            progress.IsIndeterminate = false;
        }

        private string sanitize(string p, bool nospace)
        {
            if (nospace) return (p.Replace(":", "").Replace(">", "").Replace("<", "").Replace("?", "").Replace("|", "").Replace("\"", "")).Trim();
            return (p.Replace(":", " ").Replace(">", " ").Replace("<", " ").Replace("?", " ").Replace("|", " ").Replace("\"", " ")).Trim();
        }

        private async Task<List<(string, string)>> Directories()
        {
            string src = "https://kitsunekko.net/dirlist.php?dir=subtitles%2Fjapanese%2F";
            List<(string, string)> ret = new();
            var s = await h.GetStringAsync(src);
            var document = await parser.ParseDocumentAsync(s);
            var node = document.QuerySelector("#flisttable");
            var leaf = node.QuerySelectorAll("a");

            var links = Links(leaf).ToList();
            var names = node.QuerySelectorAll("a strong").Select(x => x.TextContent).ToList();

            for (int i = 0; i < names.Count; i++)
            {
                ret.Add(new(names[i], links[i]));
            }
            return ret;
        }

        private async Task<IEnumerable<string>> Anime(Record args)

        {
            var html = await h.GetStringAsync($"{args.Url}");
            var l = await Dir(args.NameEscaped, html);
            return l;
        }

        private async Task<IEnumerable<string>> Dir(string anime, string html)
        {
            var document = await parser.ParseDocumentAsync(html);
            var node = document.QuerySelector("#flisttable");
            var leaf = node.QuerySelectorAll("a");
            var links = Links(leaf);
            Directory.CreateDirectory($"{local}{anime}");
            foreach (var item in links)
            {
                await httpget(item, anime);
            }
            return links;
        }

        private async Task httpget(string file, string folder)
        {
            string filename = Path.GetFileName(file);

            using HttpResponseMessage response = await h.GetAsync(file, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                Console.WriteLine($"{local}{folder}\\{sanitize(filename, false)}");
                using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
                using Stream streamToWriteTo = File.Open($"{local}{folder}\\{sanitize(filename, false)}", FileMode.Create);
                await streamToReadFrom.CopyToAsync(streamToWriteTo);
            }
        }

        private string Last(string path)
        {
            var split = path.Split('/');
            return split.Last();
        }

        private IEnumerable<string> Links(IHtmlCollection<IElement> leaf)
        {
            foreach (var item in leaf)
            {
                yield return item.GetAttribute("href");
            }
        }
    }

    public class Record
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public string NameEscaped { get; set; }
        public List<string> Links { get; set; }
    }
}
