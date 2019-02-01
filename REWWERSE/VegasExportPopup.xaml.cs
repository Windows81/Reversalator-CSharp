using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
	/// Interaction logic for VegasExportPopup.xaml
	/// </summary>
	public partial class VegasExportPopup : Window
	{
		public VegasExportPopup(string path)
		{
			InitializeComponent();
			FilePath.Text = path;
		}

		private void Button_Click(object sender, RoutedEventArgs e) => Close();
	}
}
