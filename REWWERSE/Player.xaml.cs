using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using static System.Environment;
using static System.Threading.Thread;
using System.Windows.Threading;
using System.IO.Compression;
using System.Collections;
using static REWWERSE.StaticMethods;
using System.Windows.Controls;
using WMPLib;
using System.Xml;
using System.Reflection;
using System.Threading.Tasks;

namespace REWWERSE
{
	/// <summary>
	/// Interaction logic for Player.xaml
	/// </summary>
	public partial class Player : UserControl
	{
		Window window;
		private bool reversed;
		Button Skip, Back;
		WindowsMediaPlayer wmp = new WindowsMediaPlayer();
		Playlist currentPlaylist = new Playlist();
		Dictionary<int, string> completedSongs = new Dictionary<int, string>();
		string currSongName;
		int pIndex, pCount, Stage = -1;
		bool skipFile, paused;
		double duration;

		public bool AllSongsLoaded() => completedSongs.Count == currentPlaylist.Count;
		public void PlayFile(string path)
		{
			Console.WriteLine("Playing!");
			paused = false;
			var oDur = duration;
			wmp.URL = path;
			while (true) try
				{
					wmp.controls.play();
					while (oDur == (duration = wmp.currentMedia.duration))
						Sleep(127);
					break;
				}
				catch (Exception) { };
			Sleep(666);
		}

		Thread runner;
		bool abortThread;

		private Thread InitRunner() => runner = new Thread(RunnerThread) { Name = "Runner" };
		private void RunnerThread(object s = null)
		{
			completedSongs.Clear();
			var loader = new Thread(LoaderThread)
			{ Name = "Loader" };
			loader.Start();
			while (completedSongs.Count < 1)
				Sleep(127);
			pCount = currentPlaylist.Count();
			for (pIndex = 0; true; pIndex = (pIndex + pCount) % pCount)
			{
				PlayFile(currentPlaylist[pIndex].filePath);
				do Sleep(127);
				while (!skipFile && !abortThread && wmp.controls.currentPosition > 0);
				if (abortThread) return;
				if (!skipFile && completedSongs.ContainsKey((pIndex + 1) % currentPlaylist.Count()))
					pIndex++;
				skipFile = false;
			}
		}

		private void LoaderThread(object o = null)
		{
			abortThread = false;
			while (currentPlaylist.Count == 0)
				Sleep(127);
			for (int c = 0; c < currentPlaylist.Count(); c++)
			{
				var info = currentPlaylist[c];
				string artist = info.artist, song = info.songName;
				EvaluateAndReverse(info);
				if (abortThread) return;
				completedSongs[c] = info;
				GC.Collect();
				Stage = 0;
			}
		}
		
		private void EvaluateAndReverse(SongInfo info)
		{
			var path = info.filePath;
			if (!path.EndsWith(".wav"))
				return;
			if (!reversed ^ IsReversedFile(path))
				return;
			ReverseStream(path, rootPath + "/.wav");
			File.Delete(path);
			File.Move(rootPath + "/.wav", info.filePath =
			path.Substring(0, path.Length - 5) + (reversed ? 'r' : 'f') + ".wav");
		}

		public void SetupProgress()
		{
			Progress.Player = wmp;
			Progress.Reversed = reversed;
		}

		public void UpdateUI()
		{
			Skip = reversed ? Left : Right;
			Back = reversed ? Right : Left;
			SongName.Text = Stage > 0 ? string.Format("INITİALİSING (Stage {0})", Stage) : Stage < 0 ? "NO SONG PLAYİNG." : currSongName.ToUpper();
			Skip.IsEnabled = pCount > 0 ? completedSongs.ContainsKey((pIndex + 1) % pCount) : false;
			Back.IsEnabled = pCount > 0 ? completedSongs.ContainsKey((pIndex - 1 + pCount) % pCount) : false;
			if (Skip.IsEnabled && completedSongs.TryGetValue((pIndex + 1) % pCount, out string next))
				Skip.ToolTip = next;
			if (Back.IsEnabled && completedSongs.TryGetValue((pIndex - 1) % pCount, out string prev))
				Back.ToolTip = prev;
		}

		public Player()
		{
			InitializeComponent();
			(window = Application.Current.MainWindow).Closed += Window_Closed;
			new DispatcherTimer() { IsEnabled = true, }.Tick += (s, e) =>
			{
				if (paused) wmp.controls.pause();
				completedSongs.TryGetValue(pIndex, out currSongName);
				UpdateUI();
			};
		}

		public void PlayPlaylistAsync(Func<Playlist> task, bool? reversed = null) => PlayPlaylistAsync(new Task<Playlist>(task), reversed);
		public async void PlayPlaylistAsync(Task<Playlist> task, bool? reversed = null) { Stage = 1; task.Start(); PlayPlaylist(await task, reversed); }

		[Obsolete]
		public void InvokePlaylist(bool? reversed = null, params object[] args) => InvokePlaylist("GetAlbum", reversed, args);
		[Obsolete]
		public void InvokePlaylist(string method, bool? reversed = null, params object[] args) => new Thread(o => {
			Stage = 1;
			var pl = typeof(StaticMethods).GetRuntimeMethod(method, args.Select(
				a => a.GetType()).ToArray()).Invoke(null, args) as Playlist;
			PlayPlaylist(pl, reversed);
		}).Start();

		[Obsolete]
		public void PlaySong(string artist, string song, int start = 1, int end = 20) => PlaySong(artist, song, reversed, start, end);
		[Obsolete]
		public void PlaySong(string artist, string song, bool? reversed = null, int start = 1, int end = 20) =>
		new Thread(o => PlayPlaylist(GetSong(artist, song, start, end), reversed)).Start();

		public void PlayPlaylist(Playlist p) => PlayPlaylist(p, reversed);
		public void PlayPlaylist(Playlist p, bool? reversed = null)
		{
			Stage = 2;
			if (reversed.HasValue)
				this.reversed = reversed.Value;
			if (this.reversed) p.Reverse();
			if (runner != null) abortThread = true;
			currentPlaylist = p;
			completedSongs.Clear();
			SetupProgress();
			InitRunner().Start();
		}

		public void SkipSong()
		{
			skipFile = true;
			pIndex++;
		}

		private void GoBack()
		{
			skipFile = true;
			pIndex--;
		}

		private void Left_Click(object sender, RoutedEventArgs e) { if (!reversed) GoBack(); else SkipSong(); }
		private void Right_Click(object sender, RoutedEventArgs e) { if (reversed) GoBack(); else SkipSong(); }
		private void Window_Closed(object sender, EventArgs e)
		{
			abortThread = true;
			wmp.close();
		}

		private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
			wmp.settings.volume = (int)((sender as Slider).Value);

		private void Pause_Click(object sender, RoutedEventArgs e)
		{
			if (paused) Play();
			else Pause();
		}
		public void Play() { wmp.controls.play(); Pauser.Content = "PAUSE"; paused = false; }
		public void Pause() { wmp.controls.pause(); Pauser.Content = "PLAY"; paused = true; }
		private void MenuItem_Click(object sender, RoutedEventArgs e) => currentPlaylist.ExportPlaylistToVegas();
	}
}
