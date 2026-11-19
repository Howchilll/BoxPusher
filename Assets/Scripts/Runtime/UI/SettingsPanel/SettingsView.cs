using HowFrame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SettingsPanel : PanelBase
{
//Define
    [SerializeField] private Slider musicSli;
    [SerializeField] private Slider soundSli;
    [SerializeField] private Button closeBtn;
//end Define

    protected override void Init()
    {
        //Init
        musicSli = transform.Find("Card/Music/Music_Sli").GetComponent<Slider>();
        musicSli.onValueChanged.AddListener(OnMusicSliValueChanged);
        soundSli = transform.Find("Card/Sound/Sound_Sli").GetComponent<Slider>();
        soundSli.onValueChanged.AddListener(OnSoundSliValueChanged);
        closeBtn = transform.Find("Card/Close_Btn").GetComponent<Button>();
        closeBtn.onClick.AddListener(OnCloseBtnClick);
        //end Init
        OnInit();
    }

    protected override void WhenShow() => OnShow();
    protected override void WhenHide() => OnHide();
}
