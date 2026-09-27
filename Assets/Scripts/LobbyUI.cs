using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Handles lobby UI element visibility, input events, and display state.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    public event Action HostClicked;
    public event Action JoinClicked;
    public event Action StartClicked;
    public event Action LeaveClicked;
    public event Action QuitClicked;
    public event Action RestartClicked;

    public Sprite WonSprite;
    public Sprite LostSprite;

    public string JoinCode { get; private set; } = "";

    private Button _hostBtn, _joinBtn, _startBtn, _leaveBtn, _quitBtn, _copyBtn, _restartBtn;
    private TextField _joinCodeField;
    private Label _statusLabel;
    private VisualElement _menuContainer;
    private VisualElement _endScreenContainer;
    private Image _endScreenImage;


    private string _currentJoinCode;

    private static readonly Color32 Green = new Color32(117, 255, 81, 255);
    private static readonly Color32 Red = new Color32(220, 80, 80, 255);
    private static readonly Color32 DarkText = new Color32(8, 8, 14, 255);
    private static readonly Color32 LightText = new Color32(240, 240, 248, 255);

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _hostBtn = root.Q<Button>("host-btn");
        _joinBtn = root.Q<Button>("join-btn");
        _startBtn = root.Q<Button>("start-btn");
        _leaveBtn = root.Q<Button>("leave-btn");
        _quitBtn = root.Q<Button>("quit-btn");
        _restartBtn = root.Q<Button>("restart-btn");
        _copyBtn = root.Q<Button>("copy-btn");
        _joinCodeField = root.Q<TextField>("join-code-field");
        _statusLabel = root.Q<Label>("status-label");
        _menuContainer = root.Q<VisualElement>("menu");
        _endScreenContainer = root.Q<VisualElement>("endscreen");
        _endScreenImage = root.Q<Image>("OverlayImage");


        if (_hostBtn != null) _hostBtn.clicked += () => HostClicked?.Invoke();
        if (_joinBtn != null) _joinBtn.clicked += () => JoinClicked?.Invoke();
        if (_startBtn != null) _startBtn.clicked += () => StartClicked?.Invoke();
        if (_leaveBtn != null) _leaveBtn.clicked += () => LeaveClicked?.Invoke();
        if (_quitBtn != null) _quitBtn.clicked += () => QuitClicked?.Invoke();
        if (_restartBtn != null) {
            _restartBtn.clicked += () => RestartClicked?.Invoke();
            _restartBtn.clicked += () => OnRestartClicked();

        }
        else { Debug.LogWarning("[LobbyUI] Restart button not found in UI."); }
        if (_copyBtn != null) _copyBtn.clicked += () => GUIUtility.systemCopyBuffer = _currentJoinCode;

        if (_joinCodeField != null)
            _joinCodeField.RegisterValueChangedCallback(e => JoinCode = e.newValue);

        SetIdle();
    }

    public void SetIdle()
    {
        Show(_menuContainer);
        Hide(_endScreenContainer);
        Show(_hostBtn); Show(_joinBtn); Show(_joinCodeField); Show(_quitBtn); Show(_startBtn);
        Hide(_leaveBtn); Hide(_copyBtn); Hide(_restartBtn);
        if (_hostBtn != null) { _hostBtn.style.backgroundColor = new StyleColor(Red); _hostBtn.style.color = new StyleColor(LightText); _hostBtn.text = "Host"; }
        if (_statusLabel != null) _statusLabel.text = "Not hosting";
        if(_startBtn != null) _startBtn.text = "Start\nSolo Game";
    }


    public void SetHosting(string joinCode)
    {
        _currentJoinCode = joinCode;
        Show(_hostBtn); Show(_startBtn); Show(_quitBtn); Show(_copyBtn);
        Hide(_joinBtn); Hide(_joinCodeField); Hide(_leaveBtn); Hide(_restartBtn);
        if (_hostBtn != null) { _hostBtn.style.backgroundColor = new StyleColor(Green); _hostBtn.style.color = new StyleColor(DarkText); _hostBtn.text = "Leave"; }
        if (_statusLabel != null) _statusLabel.text = $"Join code: {joinCode}";
        if(_startBtn != null) _startBtn.text = "Start Game";
    }

    public void SetClient()
    {
        Show(_leaveBtn); Show(_quitBtn);
        Hide(_hostBtn); Hide(_joinBtn); Hide(_joinCodeField); Hide(_startBtn); Hide(_copyBtn); Hide(_restartBtn);
        if (_statusLabel != null) _statusLabel.text = "Connected to host";
    }

    public void SetEndScreen(bool won)
    {
        Debug.Log($"[LobbyUI] Game ended, showing end screen. Won: {won}");
        Hide(_menuContainer);
        Show(_endScreenContainer);
        // Fetch again: after SetActive(true), UIDocument rebuilds the visual tree and the old reference is stale
        _endScreenImage = GetComponent<UIDocument>().rootVisualElement.Q<Image>("OverlayImage");
        if (_endScreenImage != null)
            _endScreenImage.style.backgroundImage = new StyleBackground(won ? WonSprite : LostSprite);
    }

    public void OnRestartClicked()
    {
        SetIdle();
        Debug.Log("[LobbyUI] Restart clicked, returning to idle state.");
    }

    private void Show(VisualElement el) { if (el != null) el.style.display = DisplayStyle.Flex; }
    private void Hide(VisualElement el) { if (el != null) el.style.display = DisplayStyle.None; }
}
