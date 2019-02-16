using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using static System.Environment;

#pragma warning disable CS0665
namespace REWWERSE
{
	public class SongInfo
	{
		public string songName, songUrl, artist, filePath;
		public static implicit operator string(SongInfo i) => StaticMethods.GetSongName(i.artist, i.songName);
	}
	public class Playlist : List<SongInfo>
	{
		public Playlist() { }
		public Playlist(SongInfo p) => Add(p);
	}
	public enum SaveMode
	{
		Pooling = 1,
		Nippyshare = 2,
	}
	class PlaylistUnloadableException : Exception { }
	static class StaticMethods
	{
		static StaticMethods()
		{
			Directory.CreateDirectory(rootPath);
			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		}

		public static string rootPath = GetFolderPath(SpecialFolder.MyMusic) + "\\SAVED";
		public static WebClient cl = new WebClient();
		public static string Time() => DateTime.UtcNow.ToString("yyyymmddThhmmss'Z'");
		public static string GetSongName(string artist, string song) => (artist.Length > 0 ? artist + " - " : "") + song;
		public static string GetAlbumName(string artist, string album) => rootPath + '\\' + ((artist.Length > 0 ? artist + " - " : "") + album).ToUpper();
		public static string GetNameFromPath(string path) => path.Substring(path.LastIndexOf('\\') + 4, path.Length - path.LastIndexOf('\\') - 9);
		public static bool IsReversedFile(string path) => path.EndsWith("r.mp3");

		public static List<string> GetBingResultUrls(string query, int start = 1, int end = 10)
		{
			var ma = new List<Match>();
			for (int c = start; c <= end; c += 10)
				ma.AddRange(Regex.Matches(new StreamReader(cl.OpenRead("https://www.bing.com/search?q=" + Uri.
				EscapeUriString(query) + "&first=" + c)).ReadToEnd(), "<h2>\\s*<a href=\"(http.*?)\" h").Cast<Match>());
			return ma.Select(m => m.Groups[1].Value).ToList().GetRange(0, Math.Min(ma.Count - 1, end - start));
		}
		public static List<string> GetPooledAlbumResults(string artist, string album, int start = 1, int end = 20)
		{
			var urls = GetBingResultUrls(string.Format("download \"{0}\" \"{1}\" contains:.zip", artist, album), start, end);
			return urls.OrderByDescending(s => GetURLRank(s)).SelectMany(
				url =>
				{
					if (url.Contains("vk.com"))
						return new List<string>();
					try
					{
						return Regex.Matches(new StreamReader(cl.OpenRead(url)).ReadToEnd(),
				  @"http.{7,127}\.zip").Cast<Match>().Select(m => m.Value).Where(s => s.Length > 0);
					}
					catch (WebException) { return new List<string>(); }
				}).Distinct().Where(s => s != null).OrderByDescending(s => GetURLRank(s, artist, album)).ToList();
		}

		public static Playlist GetAlbum(string artist, string album, string url) =>
			url == null ? GetAlbum(artist, album) : GetSavedAlbum(artist, album) ?? GetAlbumFromZip(artist, album, url) ?? SaveAlbumFromZip(artist, album, url);
		public static Playlist GetAlbum(string artist, string album) => GetAlbum(artist, album, SaveMode.Pooling);
		public static Playlist GetAlbum(string artist, string album, SaveMode saveMode)
		{
			Playlist p = GetSavedAlbum(artist, album);
			if (p != null) return p;
			switch (saveMode)
			{
				case SaveMode.Nippyshare:
					return SaveNippyshareAlbum(artist, album);
				case SaveMode.Pooling:
				default:
					return SavePooledAlbum(artist, album, 1, 20);
			}
		}
		public static Playlist SavePooledAlbum(string artist, string album, int start, int end)
		{
			Playlist p;
			foreach (var url in GetPooledAlbumResults(artist, album, start, end))
				if ((p = SaveAlbumFromZip(artist, album, url)) != null)
					return p;
			throw new PlaylistUnloadableException();
		}
		public static Playlist SaveNippyshareAlbum(string artist, string album)
		{
			var urls = GetBingResultUrls(string.Format("site:https://nippyshare.com/v \"{0}\" \"{1}\"", artist, album));
			foreach (var url in urls)
			{
				string dUrl = "";
				var stream = new StreamReader(cl.OpenRead(url));
				//<a href="//nippyshare.com/d/8aa11d/66109055e6259ff6c640ff3947cb8dd0" class="btn btn-info center-block flipthis-highlight">Download</a>
				while (!stream.EndOfStream && dUrl == "")
					dUrl = Regex.Match(stream.ReadLine(), "nippyshare.com/d/.{6}/.{32}").Value;
				stream.Close();
				Thread.Sleep(1271);
				return SaveAlbumFromZip(artist, album, "http://" + dUrl);
			}
			throw new PlaylistUnloadableException();
		}

		private static Playlist GetSavedAlbum(string artist, string album)
		{
			var A = new Playlist();
			var aFolderPath = GetAlbumName(artist, album);
			if (Directory.Exists(aFolderPath))
			{
				foreach (var file in Directory.GetFiles(aFolderPath, "*.mp3"))
					A.Add(new SongInfo()
					{
						filePath = file,
						artist = artist,
						songName = GetNameFromPath(file),
					});
				return A;
			}
			return null;
		}

		private static Playlist SaveAlbumFromZip(string artist, string album, string url)
		{
			MemoryStream mem = null;
			byte[] data = null;
			ZipArchive archive;
			try { data = cl.DownloadData(url); }
			catch (InvalidDataException) { }
			catch (ArgumentException) { }
			catch (WebException) { }
			if (data == null)
				return null;
			try
			{
				var A = GetAlbumFromZip(artist, album, archive
				= new ZipArchive(mem = new MemoryStream(data)));
				archive.Dispose();
				mem.Dispose();
				return A;
			}
			catch (InvalidDataException) { return null; }
		}

		public static Playlist GetAlbumFromZip(string artist, string album, string path)
		{
			Playlist A;
			if ((A = GetSavedAlbum(
			artist, album)) != null)
				return A;

			try
			{
				var fs = new FileStream(path, FileMode.Open);
				var archive = new ZipArchive(fs);
				Playlist p = GetAlbumFromZip(artist, album, archive);
				archive.Dispose();
				fs.Dispose();
				return p;
			}
			catch (ArgumentException) { return null; }
		}
		private static Playlist GetAlbumFromZip(string artist, string album, ZipArchive archive)
		{
			var A = new Playlist();
			List<ZipArchiveEntry> entries = null;
			List<string> entryNames = null, entryUrls = null;
			var aFolderPath = GetAlbumName(artist, album);
			if ((entryNames = TrimSongNames(entryUrls = (entries = archive
			.Entries.Where(s => s.FullName.ToLower().EndsWith("mp3")).ToList())
			.Select(s => s.FullName).ToList()).ToList()).Count < 2)
				return null;

			Array.ForEach(Directory.CreateDirectory(aFolderPath).GetFiles()
				.Select(f => f.FullName).ToArray(), File.Delete);
			for (int c = 0; c < entries.Count; c++)
			{
				var str = entries[c].Open();
				var path = aFolderPath + string.Format("/{0:D2} {1} f.mp3", c + 1, entryNames[c]);
				var fs = new FileStream(path, FileMode.Create);
				str.CopyTo(fs);
				str.Dispose();
				fs.Dispose();
				//ConvertStream(mp3P, path);
				//File.Delete(mp3P);

				A.Add(new SongInfo()
				{
					artist = artist,
					songName = entryNames[c],
					songUrl = entryUrls[c],
					filePath = path,
				});
				str.Dispose();
			}
			return A;
		}

		private static List<string> TrimSongNames(List<string> list)
		{
			int a, b;
			string r = list[0];
			for (a = 0; list.All(s => s[a] == r[a] || (char.IsNumber(s[a]) && char.IsNumber(r[a]))); a++) ;
			for (b = 0; list.All(s => s[s.Length - b - 1] == r[r.Length - b - 1] ||
			(char.IsNumber(s[s.Length - b - 1]) && char.IsNumber(r[r.Length - b - 1]))); b++) ;
			return list.Select(s => {
				s = s.Substring(a, s.Length - b - a);
				var ftIndex = Regex.Match(s, "( \\()?f(ea)?t")?.Index ?? 0;
				if (ftIndex == 0) ftIndex = s.Length;
				s = s.Substring(0, ftIndex);
				return s;
			}).ToList();
		}

		public static Playlist GetSong(string artist, string song, int start = 1, int end = 20)
		{
			Playlist p = GetSavedSong(artist, song);
			if (p != null)
				return p;
			foreach (var url in GetSongResults(artist, song, start, end))
				if ((p = SaveSong(artist, song, url)) != null)
					return p;
			return null;
		}
		private static Playlist GetSavedSong(string artist, string song)
		{
			var f = Directory.GetFiles(rootPath, GetSongName(artist, song) + "*.mp3", SearchOption.AllDirectories);
			return f.Length > 0 ? new Playlist(new SongInfo() { artist = artist, songName = song, filePath = f[0] }) : null;
		}

		//Saves an individual song.
		private static Playlist SaveSong(string artist, string song, string url)
		{
			var t = Time();
			string path = rootPath + '\\' + artist + " - " + song + " f.mp3";
			FileStream sw = null;
			try
			{
				cl.OpenRead(url).CopyTo(sw = new FileStream(path, FileMode.Create));
				sw.Dispose();
				//It's gonna remain an MP3, unlike before.
				//ConvertStream(mp3P, path);
				//File.Delete(mp3P);
				return new Playlist(new SongInfo()
				{
					songName = song,
					artist = artist,
					filePath = path,
					songUrl = url,
				});
			}
			catch (InvalidDataException) { }
			catch (ArgumentException) { }
			catch (WebException) { }
			return null;
		}
		public static List<string> GetSongResults(string artist, string song, int start = 1, int end = 20)
		{
			var urls = GetBingResultUrls("\"{0}\" \"{1}\" contains:mp3", start, end);
			return urls.SelectMany(url =>
			{
				try
				{
					return Regex.Matches(new StreamReader(cl.OpenRead(url)).ReadToEnd(),
						@"http.{7,127}\.mp3").Cast<Match>().Select(mt2 => mt2.Value);
				}
				catch (Exception) { return new List<string>(); }
			}).Distinct().OrderByDescending(s => GetURLRank(s, artist, song)).ToList();
		}

		public static string SpacifyURL(string s) => Regex.Replace(Uri.UnescapeDataString(s).Replace('-', ' ').Replace('_', ' '), @" {2,}", " ");
		public static int GetURLRank(string s, string artist = "", string song = "")
		{
			var l = SpacifyURL(s).ToLower();
			var n = l.Split('/').Last();
			var v = 0;
			if (n.Contains(song.ToLower()))
				v += 4;
			else if (l.Contains(song.ToLower()))
				v += 3;
			if (n.Contains(artist.ToLower()))
				v += 2;

			if (artist != "")
				l = l.Replace(artist.ToLower(), "");
			if (song != "")
				l = l.Replace(song.ToLower(), "");

			if (l.Contains("instrumental") || l.Contains("mix") || l.Contains("flip")
				|| (l.Contains("chopped") && l.Contains("screw")) || l.Contains("acoustic"))
				v -= 7;
			if (l.Contains("clean"))
				v--;
			if (l.Contains("1604ent"))//IT DOWNLOADS LIKE SÜPER SLOTH!
				v = 0;
			if (l.Contains("brickhomedesign"))//IT DOWNLOADS SUPER WRONG!
				v = 0;
			if (l.Contains("songslover"))//Inconsistent encoding?????
				v = 0;
			if (l.Contains("soundike"))//SHORT SAMPLES EVERYWHERE
				v = 0;
			if (l.Contains("lq"))
				v *= -1;
			/*
			if (v > 3 && Regex.IsMatch(l, @"\([^\.]+\)"))
				v = 0;
				*/
			return v;
		}

		/*
		public static void ConvertStream(string input, string output)
		{
			using (var mp3 = new Mp3FileReader(input))
			using (var wfc = WaveFormatConversionStream.CreatePcmStream(mp3))
				WaveFileWriter.CreateWaveFile(output, wfc);
		}
		*/

		public static void ReverseStream(string i, string o)
		{
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "cmd.exe",
					WorkingDirectory = CurrentDirectory,
					WindowStyle = ProcessWindowStyle.Hidden,
					Arguments = $"/C sox \"{i}\" f.wav&sox f.wav \"{o}\" reverse&del f.wav"
				}
			};
			process.Start();
			process.WaitForExit();
		}

		public static void ShowMessage(string text) => System.Windows.Forms.MessageBox.Show(text);

		const string vegasCodeName = "Exported Script From Reversalator";
		public static void ExportPlaylistToVegas(this Playlist p)
		{
			if (p.Count == 0)
			{ ShowMessage("Unable to export: there are no files playing."); return; }
			string code;
			using (var str = new StreamReader(CurrentDirectory + "\\VegasScript.cs"))
			using (var mem = new MemoryStream())
			{
				var dir = XmlWriter.Create(mem);
				dir.WriteStartElement("root");
				foreach (var si in p)
				{
					dir.WriteStartElement("song");
					dir.WriteAttributeString("FilePath", si.filePath);
					dir.WriteAttributeString("SongName", si.songName);
					dir.WriteAttributeString("Artist", si.artist);
					dir.WriteEndElement();
				}
				dir.WriteEndElement();
				dir.Close();
				mem.Position = 0;
				string xml = new StreamReader(mem).ReadToEnd().Replace(@"""", @"""""");
				code = str.ReadToEnd().Replace("{FILLER}", xml);
			}

			string main = "c:/Program Files", cFol;
			foreach (var pFolP in new[] { "", " (x86)" })
				if (Directory.Exists(cFol = "c:/Program Files" + pFolP) && Directory.Exists(cFol += "/VEGAS"))
					foreach (var vDir in Directory.GetDirectories(cFol))
						if (Directory.Exists(cFol = vDir + "/Script Menu"))
							main = cFol;

			using (var d = new SaveFileDialog()
			{
				InitialDirectory = main,
				FileName = vegasCodeName + ".cs",
				Filter = "C# code file|*.cs",
			})
			{
				while (true) try
					{
						d.ShowDialog();
						File.WriteAllText(d.FileName, code);
						break;
					}
					catch (Exception x)
					{ System.Windows.Forms.MessageBox.Show(x.Message); }
				new VegasExportPopup(d.FileName).ShowDialog();
			}
		}

		/*
		public static List<Tuple<string,string>> RecommendSongs(string artist, string song) {
			List<string> l(string s) => new List<string> { s };
			var e = Spotify.SearchItems(artist, SearchType.Artist).Error;
			var aObj = Spotify.SearchItems(artist, SearchType.Artist).Artists.Items[0];
			var sObj = Spotify.SearchItems(artist + " " + song, SearchType.Track).Tracks.Items[0];
			var tList = Spotify.GetRecommendations(l(aObj.Id), null, l(sObj.Id)).Tracks;
			return tList.Select(t => new Tuple<string, string>(t.Artists[0].Name, t.Name)).ToList();
		}
		*/
	}
}