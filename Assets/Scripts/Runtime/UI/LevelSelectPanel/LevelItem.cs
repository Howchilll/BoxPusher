using System;
using BoxPusher;
using BoxPusher.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>选关卡片：编号 + 名字 + 是否达到最优步数</summary>
public class LevelItem : MonoBehaviour
{
//Define
    private Button _btn;
    private Image _bg;
    private TextMeshProUGUI _numTxt;
    private TextMeshProUGUI _nameTxt;
    private TextMeshProUGUI _perfectTxt;
    private GameObject _lockObj;
//end Define

    private Action _onClick;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _bg = GetComponent<Image>();
        _numTxt = transform.Find("Num_Txt").GetComponent<TextMeshProUGUI>();
        _nameTxt = transform.Find("Name_Txt").GetComponent<TextMeshProUGUI>();
        _perfectTxt = transform.Find("Perfect_Txt").GetComponent<TextMeshProUGUI>();
        _lockObj = transform.Find("Lock_Obj").gameObject;
        _btn.onClick.AddListener(() => _onClick?.Invoke());
    }

    public void Bind(int id, LevelData data, LevelRecord record, bool unlocked, Action onClick)
    {
        _onClick = onClick;
        var cleared = record != null && record.cleared;
        _numTxt.text = id.ToString();
        _nameTxt.text = data?.name ?? "";
        _perfectTxt.gameObject.SetActive(cleared && data != null && GameApp.IsPerfect(data, record.bestMoves));
        _lockObj.SetActive(!unlocked);
        _btn.interactable = unlocked && data != null;
        _bg.color = cleared ? UiTheme.PanelLight : UiTheme.Panel;
    }
}
