using BoxPusher;
using HowFrame;

/// <summary>标题（玩家用）：开始游戏 / 设置 / 退出。关卡编辑器是独立场景，不从这里进</summary>
public partial class TitlePanel : PanelBase
{
    private void OnInit() { }
    private void OnShow() { }
    private void OnHide() { }

    private void OnStartBtnClick() => GameApp.Go(GameScreen.LevelSelect);
    private void OnSettingsBtnClick() => GameApp.ShowSettings();
    private void OnQuitBtnClick() => GameApp.Quit();
}
