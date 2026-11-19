using System;
using BoxPusher;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>工具栏按钮：图标 + 名字，选中时高亮</summary>
public class ToolItem : MonoBehaviour
{
    private Image _bg;
    private Image _icon;
    private TextMeshProUGUI _label;
    public EditorTool Tool { get; private set; }

    private void Awake()
    {
        _bg = GetComponent<Image>();
        _icon = transform.Find("Icon_Img").GetComponent<Image>();
        _label = transform.Find("Label_Txt").GetComponent<TextMeshProUGUI>();
    }

    public void Bind(EditorTool tool, Action<EditorTool> onClick)
    {
        Tool = tool;
        var (sprite, color) = EditorController.ToolIcon(tool);
        _icon.sprite = sprite;
        _icon.color = color;
        _label.text = EditorController.ToolName(tool);
        GetComponent<Button>().onClick.AddListener(() => onClick(tool));
    }

    public void SetSelected(bool on) => _bg.color = on ? UiTheme.Selected : UiTheme.Button;
}

/// <summary>草稿位列表项</summary>
public class DraftItem : MonoBehaviour
{
    private TextMeshProUGUI _label;
    private Action _onClick;

    private void Awake()
    {
        _label = transform.Find("Label_Txt").GetComponent<TextMeshProUGUI>();
        GetComponent<Button>().onClick.AddListener(() => _onClick?.Invoke());
    }

    public void Bind(string text, Action onClick)
    {
        _label.text = text;
        _onClick = onClick;
    }
}
