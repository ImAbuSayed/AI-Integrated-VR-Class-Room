using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;

public class OpenAIManager : MonoBehaviour
{
    // Update URLs to use HTTPS
    private const string API_URL_CHAT = "http://3.113.8.84/api/openai-proxy/chat";
    private const string API_URL_SPEECH = "http://3.113.8.84/api/openai-proxy/speech";
    private const string API_URL_TRANSCRIPTION = "http://3.113.8.84/api/openai-proxy/transcription";
    private const string API_KEY = "0K84xL32DFvD5zz5NLU0xxTMiJllc7VC";

    public static OpenAIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public IEnumerator SendChatRequest(string userMessage, System.Action<string> callback)
    {
        // Create the message array properly
        string jsonBody = JsonUtility.ToJson(new ChatRequest
        {
            model = "gpt-3.5-turbo",
            messages = new Message[]
            {
                new Message { role = "user", content = userMessage }
            }
        });

        using (UnityWebRequest request = new UnityWebRequest(API_URL_CHAT, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-API-Key", API_KEY);

            // Add certificate handler for HTTPS
            request.certificateHandler = new BypassCertificate();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                ChatResponse chatResponse = JsonUtility.FromJson<ChatResponse>(response);
                callback(chatResponse.choices[0].message.content);
            }
            else
            {
                Debug.LogError($"Error: {request.error}");
                callback($"Error: {request.error}");
            }
        }
    }
}

// Add these classes for JSON serialization
[System.Serializable]
public class ChatRequest
{
    public string model;
    public Message[] messages;
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

// Add this class to bypass SSL certificate validation (for development only)
public class BypassCertificate : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}