using HowFrame;
using UnityEngine;
using UnityEngine.UI;

public partial class TitlePanel : PanelBase
{
//Define
    [SerializeField] private Button startBtn;
    [SerializeField] private Button settingsBtn;
    [SerializeField] private Button quitBtn;
//end Define

    protected override void Init()
    {
        //Init
        startBtn = transform.Find("Menu/Start_Btn").GetComponent<Button>();
        startBtn.onClick.AddListener(OnStartBtnClick);
        settingsBtn = transform.Find("Menu/Settings_Btn").GetComponent<Button>();
        settingsBtn.onClick.AddListener(OnSettingsBtnClick);
        quitBtn = transform.Find("Menu/Quit_Btn").GetComponent<Button>();
        quitBtn.onClick.AddListener(OnQuitBtnClick);
        //end Init
        OnInit();
    }

    protected override void WhenShow() => OnShow();
    protected override void WhenHide() => OnHide();
}
