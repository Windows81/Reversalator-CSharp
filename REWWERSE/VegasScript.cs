using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using System.Windows.Forms;
using System.Xml;
using ScriptPortal.Vegas;

static class Staticks
{
	public static OFXParameter GetEffectParameter(this Media t, string key)
	{
		var l = t.Generator.OFXEffect.Parameters;
		for (int c = 0; c < l.Count; c++)
			if (l[c].Name == key)
				return t.GetEffectParameter(c);
		return null;
	}
	public static Type GetParamterType(this Media t, string key)
	{ return t.GetEffectParameter(key).GetType().GetProperty("Value").PropertyType; }
	public static OFXParameter GetEffectParameter(this Media t, int index)
	{ return t.Generator.OFXEffect.Parameters[index]; }
	public static void ChangeText(this Media t, RichTextBox text)
	{ t.ChangeText(text.Rtf); }
	public static void ChangeText(this Media t, string text)
	{ if (t.Generator.PlugIn.Name == "VEGAS Titles & Text") t.ChangeProperty("Text", text); }
	public static void ChangeProperty<T>(this Media t, string key, T prop)
	{
		var p = t.GetEffectParameter(key);
		p.GetType().GetProperty("Value").SetValue(p, prop, null);
		p.ParameterChanged();
		t.Generator.OFXEffect.AllParametersChanged();
	}
}
class EntryPoint
{
	static string xml = @"{FILLER}";
	Timecode currTime = new Timecode();
	static Project project;
	static Vegas vegas;
	public Media GetSongText(params string[] s)
	{
		var t = Media.CreateInstance(project, vegas.PlugIns.GetChildByName("VEGAS Titles & Text"));
		t.TapeName = "Le.";
		var rtb = new RichTextBox();

		if (s.Length > 0)
		{
			rtb.SelectionFont = new Font("Consolas", project.Video.Height / 69);
			rtb.SelectionAlignment = HorizontalAlignment.Left;
			rtb.AppendText((s[0]) + Environment.NewLine);
		}

		if (s.Length > 1)
		{
			rtb.SelectionFont = new Font("Consolas", project.Video.Height / 42);
			rtb.SelectionAlignment = HorizontalAlignment.Center;
			rtb.AppendText((s[1]) + Environment.NewLine);
		}

		if (s.Length > 2)
		{
			rtb.SelectionFont = new Font("Consolas", project.Video.Height / 69);
			rtb.SelectionAlignment = HorizontalAlignment.Right;
			rtb.AppendText((s[2]) + Environment.NewLine);
		}

		t.ChangeText(rtb.Rtf);
		t.ChangeProperty("LineSpacing", .8);
		return t;
	}
	//public static void Main(String[] s) { }
	public void FromVegas(Vegas vegas)
	{
		EntryPoint.vegas = vegas;
		//vegas.NewProject();
		project = vegas.Project;
		project.Tracks.Clear();
		//project.MediaPool.Clear();
		var vt = project.AddVideoTrack();
		var at = project.AddAudioTrack();
		var doc = new XmlDocument();
		doc.LoadXml(xml);
		foreach (XmlElement ln in doc.LastChild)
		{
			var fn = ln.GetAttribute("FilePath");
			var m = new Media(fn);
			var len = m.Length;
			var t = GetSongText(ln.GetAttribute("Artist"), ln.GetAttribute("SongName"));
			vt.AddVideoEvent(currTime, len).AddTake(t.Streams[0]);
			at.AddAudioEvent(currTime, len).AddTake(m.Streams[0]);
			currTime += len;
		}
	}
}