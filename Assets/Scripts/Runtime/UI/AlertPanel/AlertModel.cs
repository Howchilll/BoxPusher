using BoxPusher;
using HowFrame;

/// <summary>通用警告弹窗：GameApp.Alert(标题, 内容)</summary>
public partial class AlertPanel : PanelBase
{
    private void OnInit() { }
    private void OnShow() { }
    private void OnHide() { }

    private void OnShowWithParameter(object parameter)
    {
        if (parameter is not AlertInfo info) return;
        titleTxt.text = info.Title;
        messageTxt.text = info.Message;
    }

    private void OnOkBtnClick() => GameApp.HideAlert();
}
