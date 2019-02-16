using System.Windows;
using static REWWERSE.StaticMethods;

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
