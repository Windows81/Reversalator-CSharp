using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace REWWERSE
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class OAuthSpotifyGui : Window
	{
		private OAuthSpotifyGui() {
			InitializeComponent();
		}

		public static string GetToken(){
			string str = null, url = null;
			var g = new OAuthSpotifyGui();
			new Thread(() => g.Show()) { ApartmentState = ApartmentState.STA }.Start();

			while (str == null)
				if ((url) != null && url.Contains("access_token"))
					str = url.Substring(url.IndexOf("access_token") + 12, 155);
				else Thread.Sleep(127);
			return null;
		}
	}
}
