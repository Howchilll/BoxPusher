using BoxPusher;
using HowFrame;

/// <summary>通关结算：本次步数 / 推箱、与最优解对比、最佳记录</summary>
public partial class WinPanel : PanelBase
{
    private void OnInit() { }
    private void OnShow() { }
    private void OnHide() { }

    private void OnShowWithParameter(object parameter)
    {
        if (parameter is not WinInfo info) return;
        var s = info.Session;
        var d = s.Data;
        titleTxt.text = info.FromEditor ? "试玩通关！" : $"第 {d.id} 关 通关！";
        var perfect = GameApp.IsPerfect(d, s.Moves);
        var lines = $"用了 {s.Moves} 步\n" + (perfect ? UiTheme.Rich("完美！", UiTheme.Star) : UiTheme.Rich("未达到完美", UiTheme.Muted));
        resultTxt.text = lines;
        nextBtn.gameObject.SetActive(info.HasNext);
        menuTxt.text = info.FromEditor ? "回到编辑器" : "选关";
    }

    private void OnNextBtnClick() => GameApp.PlayNext();
    private void OnRetryBtnClick() => GameApp.Retry();
    private void OnMenuBtnClick() => GameApp.LeavePlay();
}
