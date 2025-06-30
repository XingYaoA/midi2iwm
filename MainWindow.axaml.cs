using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace midi2iwmAvalonia;

public partial class MainWindow : Window
{
    private readonly List<string> _instrumentNames =
    [
        // 钢琴类
        "原声大钢琴", "明亮大钢琴", "电子大钢琴", "酒吧钢琴", "电钢琴1", "电钢琴2", "拨弦古钢琴", "击弦古钢琴",
        // 色彩打击乐器
        "钢片琴", "钟琴", "音乐盒", "颤音琴", "马林巴", "木琴", "管钟", "大扬琴",
        // 风琴类
        "击杆风琴", "打击式风琴", "摇滚风琴", "教堂风琴", "簧管风琴", "手风琴", "口琴", "探戈手风琴",
        // 吉他类
        "尼龙弦吉他", "钢弦吉他", "爵士电吉他", "清音电吉他", "闷音电吉他", "驱动电吉他", "失真电吉他", "吉他泛音",
        // 贝斯类
        "原声贝斯", "指弹电贝斯", "拨片电贝斯", "无品贝斯", "掌击贝斯1", "掌击贝斯2", "合成贝斯1", "合成贝斯2",
        // 弦乐器类
        "小提琴", "中提琴", "大提琴", "低音提琴", "颤音弦乐", "拨奏弦乐", "竖琴", "定音鼓",
        // 合奏类
        "弦乐合奏1", "弦乐合奏2", "合成弦乐1", "合成弦乐2", "合唱人声", "人声哼唱", "合成人声", "管弦乐齐奏",
        // 铜管类
        "小号", "长号", "大号", "弱音小号", "法国号", "铜管合奏", "合成铜管1", "合成铜管2",
        // 簧管类
        "高音萨克斯", "中音萨克斯", "次中音萨克斯", "上低音萨克斯", "双簧管", "英国管", "巴松管", "单簧管",
        // 吹管类
        "短笛", "长笛", "竖笛", "排箫", "瓶笛", "尺八", "口哨", "奥卡里那",
        // 合成主音类
        "方波主音", "锯齿波主音", "汽笛风琴", "合成吹管", "合唱主音", "低音主音", "氛围主音", "水晶主音",
        // 合成铺底类
        "雨声铺底", "音轨铺底", "科幻铺底", "西塔铺底", "班斯瑞铺底", "温暖铺底", "多重合音", "回声铺底",
        // 合成效果类
        "吉他泛音效果", "呼吸声效果", "海浪效果", "鸟鸣效果", "电话铃声效果", "直升机效果", "鼓掌声效果", "枪声效果",
        // 民族乐器类
        "古筝", "古琴", "扬琴", "琵琶", "三味线", "十三弦筝", "卡林巴", "日本筝",
        // 打击乐器类
        "钢鼓", "木鱼", "太鼓", "合成鼓", "电子打击乐", "击掌声", "电子小军鼓", "反向钹",
        // 音效类
        "吉他换把声", "呼吸噪音", "海浪声", "鸟叫声", "电话声", "直升机声", "鼓掌声", "枪炮声"
    ];

    private readonly string[] _sfxName =
    [
        "小黄鸭", "泡泡", "开关", "叮~(铃声)", "！", "弹簧", "喇叭", "OK", "碎了", "打击", "激光枪", "风声", "哨子", "翁~（几个星星图标）", "忍者", "鼓掌",
        "打鼓", "钢琴", "贝斯", "庆祝", "唔！", "笑声", "惊悚紧张", "沙锤", "打鼓2（等当~）", "禁止", "瓶子", "敲木头", "叮~（玻璃）", "灰色枪声", "电池",
        "炯！（雪山图标）", "心跳", "尖叫鸡", "狗叫", "猫叫", "叮当（铃铛图标）", "机器人", "受击音效"
    ];

    private readonly int[] _standardPitch =
    [
        63, 105, 64, 95, 77, 106, 72, 89, 73, 52, 94, 76, 84, 57, 76, 69, 60, 61, 43, 78, 81, 58, 70, 78, 100, 87, 86,
        103, 89, 74, 50, 72, 108, 84, 78, 99, 64, 68, 80
    ];

    private MidiFile _file = null!;
    private MetricTimeSpan _midiDuration = null!;
    private TempoMap _tempoMap = null!;

    public MainWindow()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        InitializeComponent();
    }

    private double HighestPitch(int standard)
    {
        return standard + 12 * double.Log(3) / double.Log(2);
    }

    private double LowestPitch(int standard)
    {
        return standard + 12 * double.Log(0.05) / double.Log(2);
    }

    private int SfxNumberCal(int index)
    {
        if (index >= 1 && index <= 7) return index + 1;
        if (index >= 8) return index + 1;
        return index;
    }

    private double Velocity2Volume(SevenBitNumber velocity, int k)
    {
        var volume = double.Round(double.Pow(velocity / 127.0, k), 4);
        return volume >= 0.05 ? volume : 0.05;
    }

    private double NotePitch(int number, int standard)
    {
        return double.Round(double.RootN(double.Pow(2, number - standard), 12), 4);
    }

    private double Time2Frame(TimeSpan time)
    {
        return double.Round(time.TotalSeconds * 50, 0);
    }

    private void Quit(object sender, RoutedEventArgs e)
    {
        Process.GetCurrentProcess().Kill();
    }

    private void ShowAboutWindow(object sender, RoutedEventArgs e)
    {
        Window about = new AboutWindow();
        about.Show(this);
    }

    private void WriteParam(XmlWriter writer, string key, string val)
    {
        writer.WriteStartElement("param");
        writer.WriteAttributeString("key", key);
        writer.WriteAttributeString("val", val);
        writer.WriteEndElement();
    }

    private async void MidiFileSelect(object sender, RoutedEventArgs e)
    {
        var midi = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择MIDI文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MIDI文件")
                {
                    Patterns = ["*.mid", "*.midi"],
                    MimeTypes = ["midi/*", "music/*"]
                }
            ],
            SuggestedStartLocation = StorageProvider
                .TryGetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)).Result
        });
        if (midi.Count == 0) return;
        MidiProcess(midi);
    }

    private async void MidiProcess(IReadOnlyList<IStorageFile> files)
    {
        TrackSel.Items.Clear();
        try
        {
            FileLoc.Text = files[0].Path.LocalPath;
            await using var stream = await files[0].OpenReadAsync();
            _file = MidiFile.Read(stream, new ReadingSettings { TextEncoding = Encoding.GetEncoding(936) });
            _tempoMap = _file.GetTempoMap();
            _midiDuration = _file.GetTimedEvents()
                .LastOrDefault(e => e.Event is NoteOffEvent)
                ?.TimeAs<MetricTimeSpan>(_tempoMap) ?? new MetricTimeSpan();
            MidiTime.Text = ((TimeSpan)_midiDuration).ToString("g");
            FileStatus.Text = "分析成功";
        }
        catch (MidiException)
        {
            FileStatus.Text = "分析失败，请检查MIDI文件是否完全正确";
            return;
        }

        foreach (var track in _file.GetTrackChunks())
        {
            var trackName = track.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? "";
            TrackSel.Items.Add(trackName);
        }
    }

    private void TrackProcess(object sender, RoutedEventArgs e)
    {
        if (TrackSel.SelectedIndex == -1) return;
        var track = _file.GetTrackChunks().ElementAt(TrackSel.SelectedIndex);
        string[] instruments;
        if (track.GetChannels().FirstOrDefault() == 9) instruments = ["打击乐器"];
        else
            instruments = track.Events.OfType<ProgramChangeEvent>().Select(num => _instrumentNames[num.ProgramNumber])
                .ToArray().Distinct().ToArray();
        if (instruments.Length == 0) ProgramChange.Text = "乐器名称：未知";
        else ProgramChange.Text = "乐器名称：" + string.Join(",", instruments);
        var notes = track.GetNotes();
        NoteCount.Text = "音符数量：" + notes.Count;
    }

    private void PlaySound(object sender, RoutedEventArgs e)
    {
        if (SfxSel.SelectedIndex == -1) return;
        var resourceManager = midi2iwmAvalonia.Resources.ResourceManager;
        var wavFile = resourceManager.GetObject(_sfxName[SfxSel.SelectedIndex]);
        using Stream wavFileStream = new MemoryStream(wavFile as byte[] ?? []);
        using var sound = new SoundPlayer(wavFileStream);
        sound.Play();
    }

    private async void GenerateIwmXml(object sender, RoutedEventArgs e)
    {
        if (TrackSel.SelectedIndex == -1) return;
        if (SfxSel.SelectedIndex == -1) return;
        var notes = _file.GetTrackChunks().ElementAt(TrackSel.SelectedIndex).GetNotes();
        var settings = new XmlWriterSettings
        {
            Indent = false,
            OmitXmlDeclaration = true
        };
        using (var stringWriter = new StringWriter())
        using (var writer = XmlWriter.Create(stringWriter, settings))
        {
            writer.WriteStartElement("object");
            writer.WriteAttributeString("type", "8");
            writer.WriteAttributeString("x", "16");
            writer.WriteAttributeString("y", "16");
            writer.WriteStartElement("event");
            writer.WriteAttributeString("eventIndex", "1");
            {
                writer.WriteStartElement("event");
                writer.WriteAttributeString("eventIndex", "107");
                {
                    WriteParam(writer, "toggle_type", "0");
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            WriteParam(writer, "trigger_once", "1");
            WriteParam(writer, "trigger_number", "0");
            WriteParam(writer, "scale", "1");
            WriteParam(writer, "layer", "2");
            WriteParam(writer, "visible", "0");
            foreach (var note in notes)
            {
                var time = note.TimeAs<MetricTimeSpan>(_tempoMap);
                byte pitch = note.NoteNumber;
                while (pitch > HighestPitch(_standardPitch[SfxSel.SelectedIndex])) pitch -= 7;
                while (pitch < LowestPitch(_standardPitch[SfxSel.SelectedIndex])) pitch += 7;
                writer.WriteStartElement("event");
                writer.WriteAttributeString("eventIndex", "17");
                {
                    var frame = Once.IsChecked ?? false ? 100000 : Time2Frame(_midiDuration);
                    WriteParam(writer, "offset", $"{Time2Frame(time)}");
                    WriteParam(writer, "frames", $"{frame}");
                    writer.WriteStartElement("event");
                    writer.WriteAttributeString("eventIndex", "104");
                    {
                        WriteParam(writer, "volume", $"{Velocity2Volume(note.Velocity, 2)}");
                        WriteParam(writer, "pitch", $"{NotePitch(pitch, _standardPitch[SfxSel.SelectedIndex])}");
                        WriteParam(writer, "sound", $"{SfxNumberCal(SfxSel.SelectedIndex)}");
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            writer.WriteElementString("name", $"Track {TrackSel.SelectedIndex}");
            writer.WriteEndElement();
            writer.Flush();
            if (((Button)sender).Name == "XmlForCopyGen")
            {
                MapObjects.Text = stringWriter.ToString();
            }
            else
            {
                var save = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    SuggestedStartLocation = StorageProvider
                        .TryGetFolderFromPathAsync(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\IWM\\maps")
                        .Result,
                    SuggestedFileName = "Music.map",
                    DefaultExtension = "map",
                    ShowOverwritePrompt = true,
                    Title = "保存.map文件"
                });
                if (save != null)
                {
                    await using var stream = await save.OpenWriteAsync();
                    using var streamWriter = new StreamWriter(stream);
                    await streamWriter.WriteAsync(
                        $"<sfm_maps><maps_head><maps_name>{Path.GetFileNameWithoutExtension(save.Name)}</maps_name><maps_version>103</maps_version><screenshot_submap>0</screenshot_submap><last_submap>0</last_submap><submap_order><map id=\"0\"></map></submap_order></maps_head><sfm_map><head><name>room 1</name><version>103</version><tileset>9</tileset><tileset2>9</tileset2><bg>22</bg><spikes>0</spikes><spikes2>0</spikes2><width>800</width><height>608</height><colors>5A0200000600000005000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000</colors><scroll_mode>0</scroll_mode><music>0</music><num_objects>1</num_objects></head><objects>" +
                        stringWriter + "</objects></sfm_map></sfm_maps>");
                }
            }
        }
    }

    private void Copy(object sender, RoutedEventArgs e)
    {
        if (Clipboard != null) Clipboard.SetTextAsync(MapObjects.Text);
    }
}