using BoxPusher;
using HowFrame;

/// <summary>音量设置，存进存档（Progress）</summary>
public partial class SettingsPanel : PanelBase
{
    private bool _loading;

    private void OnInit() { }

    private void OnShow()
    {
        _loading = true;
        musicSli.value = Progress.Data.musicVolume;
        soundSli.value = Progress.Data.soundVolume;
        _loading = false;
    }

    private void OnHide() => Progress.Save();

    private void OnMusicSliValueChanged(float v)
    {
        if (_loading) return;
        Progress.Data.musicVolume = v;
        GameAudio.ApplyVolumes();
    }

    private void OnSoundSliValueChanged(float v)
    {
        if (_loading) return;
        Progress.Data.soundVolume = v;
        GameAudio.ApplyVolumes();
    }

    private void OnCloseBtnClick() => GameApp.HideSettings();
}
