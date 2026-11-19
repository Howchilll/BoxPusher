using HowFrame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AlertPanel : PanelBase
{
//Define
    [SerializeField] private TextMeshProUGUI titleTxt;
    [SerializeField] private TextMeshProUGUI messageTxt;
    [SerializeField] private Button okBtn;
//end Define

    protected override void Init()
    {
        //Init
        titleTxt = transform.Find("Card/Title_Txt").GetComponent<TextMeshProUGUI>();
        messageTxt = transform.Find("Card/Message_Txt").GetComponent<TextMeshProUGUI>();
        okBtn = transform.Find("Card/Ok_Btn").GetComponent<Button>();
        okBtn.onClick.AddListener(OnOkBtnClick);
        //end Init
        OnInit();
    }

    protected override void WhenShow() => OnShow();
    protected override void WhenHide() => OnHide();
    protected override void WhenShowWithParameter(object parameter) => OnShowWithParameter(parameter);
}
