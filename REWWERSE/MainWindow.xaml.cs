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
using WMPLib;
using System.Threading.Tasks;

namespace REWWERSE
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			//var l = RecommendSongs("J Cole", "Neighbors");
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			string artist = Artist.Text, album = Album.Text;
			string path = Path.Text.Length > 0 ? Path.Text : null;
			Playa.PlayPlaylistAsync(() => GetAlbum(artist, album, path), reversed: true);
		}
	}
}
