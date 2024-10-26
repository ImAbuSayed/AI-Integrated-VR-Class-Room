using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DashboardManager : MonoBehaviour
{
    [Header("Chat UI References")]
    [SerializeField] private TMP_InputField inputField_Message;
    [SerializeField] private TextMeshProUGUI text_ChatHistory;
    [SerializeField] private Button button_Send;
    [SerializeField] private ScrollRect scrollView_ChatHistory;

    [Header("Audio UI References")]
    [SerializeField] private Button button_StartRecording;
    [SerializeField] private Button button_StopRecording;
    [SerializeField] private Button button_PlayTTS;
    [SerializeField] private Image image_RecordingIndicator;

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        // Add button listeners
        button_Send.onClick.AddListener(OnSendButtonClick);
        button_StartRecording.onClick.AddListener(OnStartRecordingClick);
        button_StopRecording.onClick.AddListener(OnStopRecordingClick);
        button_PlayTTS.onClick.AddListener(OnPlayTTSClick);

        // Initialize UI states
        button_StopRecording.gameObject.SetActive(false);
        image_RecordingIndicator.color = Color.white;

        // Add input field submit listener
        inputField_Message.onSubmit.AddListener((text) => OnSendButtonClick());
    }

    private void OnSendButtonClick()
    {
        if (string.IsNullOrEmpty(inputField_Message.text)) return;

        string userMessage = inputField_Message.text;
        AddMessageToChatHistory("User: " + userMessage);

        // Disable input while waiting for response
        SetUIInteractable(false);

        StartCoroutine(OpenAIManager.Instance.SendChatRequest(
            userMessage,
            (response) => {
                AddMessageToChatHistory("AI: " + response);
                SetUIInteractable(true);
            }
        ));

        inputField_Message.text = "";
    }

    private void AddMessageToChatHistory(string message)
    {
        text_ChatHistory.text += (text_ChatHistory.text.Length > 0 ? "\n" : "") + message;
        Canvas.ForceUpdateCanvases();
        scrollView_ChatHistory.verticalNormalizedPosition = 0f;
    }

    private void SetUIInteractable(bool interactable)
    {
        button_Send.interactable = interactable;
        inputField_Message.interactable = interactable;
    }

    private void OnStartRecordingClick()
    {
        button_StartRecording.gameObject.SetActive(false);
        button_StopRecording.gameObject.SetActive(true);
        image_RecordingIndicator.color = Color.red;
    }

    private void OnStopRecordingClick()
    {
        button_StartRecording.gameObject.SetActive(true);
        button_StopRecording.gameObject.SetActive(false);
        image_RecordingIndicator.color = Color.white;
    }

    private void OnPlayTTSClick()
    {
        Debug.Log("TTS Playback requested");
    }
}