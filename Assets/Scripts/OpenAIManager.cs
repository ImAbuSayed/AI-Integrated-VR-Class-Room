using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;

public class OpenAIManager : MonoBehaviour
{
    private const string API_URL_CHAT = "http://3.113.8.84/api/openai-proxy/chat";
    private const string API_URL_SPEECH = "http://3.113.8.84/api/openai-proxy/speech";
    private const string API_URL_TRANSCRIPTION = "http://3.113.8.84/api/openai-proxy/transcription";
    private const string API_KEY = "0K84xL32DFvD5zz5NLU0xxTMiJllc7VC";

    public static OpenAIManager Instance { get; private set; }
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public IEnumerator SendChatRequest(string userMessage, System.Action<string> callback, bool autoSpeak = false)
    {
        string jsonBody = JsonUtility.ToJson(new ChatRequest
        {
            model = "gpt-3.5-turbo",
            messages = new Message[]
            {
                new Message { role = "user", content = userMessage }
            },
            temperature = 0.7f
        });

        using (UnityWebRequest request = new UnityWebRequest(API_URL_CHAT, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-API-Key", API_KEY);
            request.certificateHandler = new BypassCertificate();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                ChatResponse chatResponse = JsonUtility.FromJson<ChatResponse>(response);
                string aiResponse = chatResponse.choices[0].message.content;
                callback(aiResponse);

                if (autoSpeak)
                {
                    StartCoroutine(TextToSpeech(aiResponse));
                }
            }
            else
            {
                Debug.LogError($"Error: {request.error}");
                callback($"Error: {request.error}");
            }
        }
    }

    public IEnumerator TextToSpeech(string text)
    {
        string jsonBody = JsonUtility.ToJson(new TTSRequest
        {
            model = "tts-1-hd",
            input = text,
            voice = "alloy"
        });

        using (UnityWebRequest request = new UnityWebRequest(API_URL_SPEECH, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-API-Key", API_KEY);
            request.certificateHandler = new BypassCertificate();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] audioData = request.downloadHandler.data;
                StartCoroutine(PlayAudioData(audioData));
            }
            else
            {
                Debug.LogError($"TTS Error: {request.error}");
            }
        }
    }

    private IEnumerator PlayAudioData(byte[] audioData)
    {
        int channels = 1;
        int frequency = 44100;
        float[] samples = new float[audioData.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short wav = BitConverter.ToInt16(audioData, i * 2);
            samples[i] = wav / 32768f;
        }

        AudioClip clip = AudioClip.Create("Speech", samples.Length, channels, frequency, false);
        clip.SetData(samples, 0);
        audioSource.clip = clip;
        audioSource.Play();

        yield return null;
    }

    public IEnumerator TranscribeAudio(byte[] audioData, System.Action<string> callback)
    {
        if (audioData.Length > 25 * 1024 * 1024) // 25MB limit
        {
            Debug.LogError("Audio file too large even after compression");
            callback(null);
            yield break;
        }

        string base64Audio = Convert.ToBase64String(audioData);
        string jsonBody = JsonUtility.ToJson(new TranscriptionRequest
        {
            model = "whisper-1",
            audio = base64Audio,
            language = "en"
        });

        using (UnityWebRequest request = new UnityWebRequest(API_URL_TRANSCRIPTION, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-API-Key", API_KEY);
            request.certificateHandler = new BypassCertificate();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                TranscriptionResponse response = JsonUtility.FromJson<TranscriptionResponse>(request.downloadHandler.text);
                callback(response.text);
            }
            else
            {
                Debug.LogError($"Transcription Error: {request.error}");
                callback(null);
            }
        }
    }
}

[System.Serializable]
public class TTSRequest
{
    public string model;
    public string input;
    public string voice;
}

[System.Serializable]
public class TranscriptionRequest
{
    public string model;
    public string audio;
    public string language;
}

[System.Serializable]
public class TranscriptionResponse
{
    public string text;
}

[System.Serializable]
public class ChatRequest
{
    public string model;
    public Message[] messages;
    public float temperature;
}

[System.Serializable]
public class Message
{
    public string role;
    public string content;
}

[System.Serializable]
public class ChatResponse
{
    public Choice[] choices;
}

[System.Serializable]
public class Choice
{
    public Message message;
}

public class BypassCertificate : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}