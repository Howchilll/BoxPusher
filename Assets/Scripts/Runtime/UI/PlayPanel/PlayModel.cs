using System;
using BoxPusher;
using HowFrame;

/// <summary>游戏 HUD：数值走 HowFrame Ref&lt;T&gt; 绑定（OnShow 订阅 / OnHide 退订）</summary>
public partial class PlayPanel : PanelBase
{
    private PlayController _bound;
    private Action<int> _onInt;
    private Action<string> _onStr;

    private void OnInit()
    {
        _onInt = _ => RefreshStats();
        _onStr = _ => RefreshStats();
    }

    private void OnShow()
    {
        Unbind();
        var play = BoardStage.Instance != null ? BoardStage.Instance.Play : null;
        if (play == null) return;
        _bound = play;
        play.Moves.OnChanged += _onInt;
        play.Pushes.OnChanged += _onInt;
        play.Goals.OnChanged += _onStr;

        var d = play.Session.Data;
        var editor = GameApp.Source == PlaySource.EditorTest;
        titleTxt.text = editor ? $"试玩：{(string.IsNullOrEmpty(d.name) ? "未命名" : d.name)}" : $"第 {d.id} 关　{d.name}";
        RefreshStats();
    }

    private void OnHide() => Unbind();

    private void Unbind()
    {
        if (_bound == null) return;
        _bound.Moves.OnChanged -= _onInt;
        _bound.Pushes.OnChanged -= _onInt;
        _bound.Goals.OnChanged -= _onStr;
        _bound = null;
    }

    private void RefreshStats()
    {
        if (_bound == null) return;
        statsTxt.text = $"步数 {_bound.Moves.Value}　推箱 {_bound.Pushes.Value}　目标 {_bound.Goals.Value}";
    }

    private void OnBackBtnClick() => GameApp.LeavePlay();
    private void OnUndoBtnClick() => BoardStage.Instance?.Play?.Undo();
    private void OnRestartBtnClick() => BoardStage.Instance?.Play?.Restart();
    private void OnHintBtnClick() => BoardStage.Instance?.Play?.Hint();
}
