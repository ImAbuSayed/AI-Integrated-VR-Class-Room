using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System;

public class DashboardManager : MonoBehaviour
{
    [Header("Chat UI References")]
    [SerializeField] private TMP_InputField inputField_Message;
    [SerializeField] private TextMeshProUGUI text_ChatHistory;
    [SerializeField] private Button button_Send;
    [SerializeField] private ScrollRect scrollView_ChatHistory;
    [SerializeField] private Toggle toggle_AutoSpeak;

    [Header("Audio UI References")]
    [SerializeField] private Button button_StartRecording;
    [SerializeField] private Button button_StopRecording;
    [SerializeField] private Button button_PlayTTS;
    [SerializeField] private Image image_RecordingIndicator;

    private AudioClip recordingClip;
    private bool isRecording = false;
    private string lastAiResponse = "";

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        button_Send.onClick.AddListener(OnSendButtonClick);
        button_StartRecording.onClick.AddListener(OnStartRecordingClick);
        button_StopRecording.onClick.AddListener(OnStopRecordingClick);
        button_PlayTTS.onClick.AddListener(OnPlayTTSClick);

        button_StopRecording.gameObject.SetActive(false);
        image_RecordingIndicator.color = Color.white;

        inputField_Message.onSubmit.AddListener((text) => OnSendButtonClick());
    }

    private void OnSendButtonClick()
    {
        if (string.IsNullOrEmpty(inputField_Message.text)) return;

        string userMessage = inputField_Message.text;
        AddMessageToChatHistory("User: " + userMessage);
        SetUIInteractable(false);

        StartCoroutine(OpenAIManager.Instance.SendChatRequest(
            userMessage,
            (response) =>
            {
                lastAiResponse = response;
                AddMessageToChatHistory("AI: " + response);
                SetUIInteractable(true);
            },
            toggle_AutoSpeak.isOn
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
        button_StartRecording.interactable = interactable;
        button_PlayTTS.interactable = interactable;
    }

    private void OnStartRecordingClick()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected");
            return;
        }

        button_StartRecording.gameObject.SetActive(false);
        button_StopRecording.gameObject.SetActive(true);
        image_RecordingIndicator.color = Color.red;

        // Limit recording to 15 seconds and use lower frequency
        recordingClip = Microphone.Start(null, false, 15, 16000);
        isRecording = true;
    }

    private void OnStopRecordingClick()
    {
        if (!isRecording) return;

        // Get the position before stopping
        int position = Microphone.GetPosition(null);
        Microphone.End(null);

        button_StartRecording.gameObject.SetActive(true);
        button_StopRecording.gameObject.SetActive(false);
        image_RecordingIndicator.color = Color.white;
        isRecording = false;

        // Create a new clip with only the recorded portion
        AudioClip shortenedClip = AudioClip.Create("Shortened",
            position,
            recordingClip.channels,
            recordingClip.frequency,
            false);

        float[] samples = new float[position];
        recordingClip.GetData(samples, 0);
        shortenedClip.SetData(samples, 0);

        // Convert to WAV with compression
        byte[] wavBytes = ConvertToWav(samples, shortenedClip.channels, shortenedClip.frequency);

        // Compress audio if needed
        if (wavBytes.Length > 25 * 1024 * 1024) // 25MB limit
        {
            Debug.LogWarning("Audio file too large, compressing...");
            wavBytes = CompressAudio(wavBytes);
        }

        StartCoroutine(OpenAIManager.Instance.TranscribeAudio(wavBytes, OnTranscriptionComplete));
    }

    private void OnTranscriptionComplete(string transcription)
    {
        if (!string.IsNullOrEmpty(transcription))
        {
            inputField_Message.text = transcription;
            OnSendButtonClick();
        }
        else
        {
            Debug.LogError("Transcription failed");
            AddMessageToChatHistory("System: Failed to transcribe audio. Please try again.");
        }
    }

    private void OnPlayTTSClick()
    {
        if (!string.IsNullOrEmpty(lastAiResponse))
        {
            StartCoroutine(OpenAIManager.Instance.TextToSpeech(lastAiResponse));
        }
    }

    private byte[] CompressAudio(byte[] originalWav)
    {
        // Skip WAV header
        int headerSize = 44;
        float[] samples = new float[(originalWav.Length - headerSize) / 2];

        // Convert bytes to samples
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = BitConverter.ToInt16(originalWav, headerSize + (i * 2));
            samples[i] = sample / 32768f;
        }

        // Downsample by taking every second sample
        float[] downsampledData = new float[samples.Length / 2];
        for (int i = 0; i < downsampledData.Length; i++)
        {
            downsampledData[i] = samples[i * 2];
        }

        // Convert back to WAV
        return ConvertToWav(downsampledData, 1, 8000); // Lower frequency
    }

    private byte[] ConvertToWav(float[] samples, int channels, int frequency)
    {
        int wavHeaderSize = 44;
        int dataSize = samples.Length * 2;
        byte[] wavData = new byte[wavHeaderSize + dataSize];

        // RIFF header
        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, wavData, 0, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(wavHeaderSize + dataSize - 8), 0, wavData, 4, 4);
        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, wavData, 8, 4);

        // fmt chunk
        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, wavData, 12, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(16), 0, wavData, 16, 4); // Subchunk1Size
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, wavData, 20, 2); // AudioFormat (PCM)
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)channels), 0, wavData, 22, 2); // Channels
        System.Buffer.BlockCopy(BitConverter.GetBytes(frequency), 0, wavData, 24, 4); // SampleRate
        System.Buffer.BlockCopy(BitConverter.GetBytes(frequency * channels * 2), 0, wavData, 28, 4); // ByteRate
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)(channels * 2)), 0, wavData, 32, 2); // BlockAlign
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)16), 0, wavData, 34, 2); // BitsPerSample

        // data chunk
        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("data"), 0, wavData, 36, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(dataSize), 0, wavData, 40, 4);

        // Audio data
        int offset = wavHeaderSize;
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(samples[i] * 32767f);
            byte[] sampleBytes = BitConverter.GetBytes(sample);
            System.Buffer.BlockCopy(sampleBytes, 0, wavData, offset, 2);
            offset += 2;
        }

        return wavData;
    }
}