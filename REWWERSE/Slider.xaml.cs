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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WMPLib;

namespace REWWERSE
{
	public partial class TrackSeeker : UserControl
	{
		private static Color[] colours = new[] {
			"#0078d7",
			"#00bcf2",
			"#6CA8D7",
			"#f00",
		}.Select(s => (Color)ColorConverter.ConvertFromString(s)).ToArray();

		public WindowsMediaPlayer Player = null;
		private bool isSeeking = false;
		private bool IsPaused => Player?.playState != WMPPlayState.wmppsPlaying;
		private double Duration => Player?.currentMedia?.duration ?? 0;
		private double Position => Player?.controls.currentPosition ?? 0;
		private double seekPosition;

		public void SetSliderPosition(double d)
		{
			d = Math.Max(Math.Min(d, 1), 0);
			ColBeg.Width = new GridLength(0 + d, GridUnitType.Star);
			ColEnd.Width = new GridLength(1 - d, GridUnitType.Star);
		}

		public void UpdateSliderLoad()
		{
			Resources["Time1"] = Resources["Time2"] = "";
			SetSliderPosition(Math.Sin(DateTime.Now.Ticks / 1e7) / 2 + .5);
		}

		private void UpdateSlider()
		{
			Colour = colours[IsPaused ? 2 : 0];
			if (Duration == 0)
				UpdateSliderLoad();
			else
			{
				var rev = Reversed;
				var val = (rev ? Duration - Position : Position) / Duration;
				Resources["Time1"] = TimeSpan.FromSeconds(rev ? Duration - Position : Position).ToString(@"mm\:ss");
				Resources["Time2"] = TimeSpan.FromSeconds(Duration).ToString(@"mm\:ss");
				SetSliderPosition(val);
			}
		}

		public bool Reversed;
		private double GetSeekPosition(FrameworkElement s, MouseEventArgs e) => e.GetPosition(s).X / s.RenderSize.Width;
		public static readonly DependencyProperty ColourDP = DependencyProperty.Register("Colour", typeof(Color), typeof(TrackSeeker));
		public Color Colour { get => (Color)GetValue(ColourDP); set => SetValue(ColourDP, Resources["Colour"] = value); }
		private DispatcherTimer timer;
		public TrackSeeker()
		{
			InitializeComponent();
			Colour = colours[0];
			(timer = new DispatcherTimer() { IsEnabled = true }).Tick += (s, e) =>
			{
				if (isSeeking)
					Colour = colours[1];
				else
					UpdateSlider();
			};

			MouseEventor.MouseEnter += (s, e) => isSeeking = Player != null;
			MouseEventor.MouseLeave += (s, e) => isSeeking = false;
			MouseEventor.MouseMove += (s, e) =>
			{
				if (!isSeeking) return;
				SetSliderPosition(seekPosition = GetSeekPosition(s as FrameworkElement, e));
				Resources["Time1"] = TimeSpan.FromSeconds((0 + seekPosition) * Duration).ToString(@"mm\:ss");
			};
			MouseEventor.MouseLeftButtonUp += (s, e) =>
			{
				if (isSeeking) Player.controls.currentPosition = (Reversed ? 1 - seekPosition : seekPosition) * Duration;
			};
		}
	}
}