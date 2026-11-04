using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Kotoba.Core;
using Kotoba.Infrastructure;

namespace Kotoba.App;

public partial class MainWindow
{
    private readonly HttpClient aiHttp = AiHttp.Create();
    private readonly HttpClient ollamaHttp = AiHttp.Create(local: true);
    private ChatGptAccounts? chatGptAccounts;
    private ChatGptAccounts ChatGpt => chatGptAccounts ??= new(aiHttp, dataDirectory);
    private CancellationTokenSource? aiRequest;
    private CancellationTokenSource? aiSignIn;
    private int aiRevision;
    private int aiSettingsRevision;
    private bool loadingAiSettings;
    internal Func<string, IWordAssistant>? AssistantOverride { get; set; }
    private string AiProvider => AiProviderPicker.SelectedItem as string ?? "ChatGPT";
    private IWordAssistant Assistant(string provider) => AssistantOverride?.Invoke(provider) ?? (provider switch
    {
        "Ollama" => new OllamaWordAssistant(ollamaHttp), "ChatGPT" => new ChatGptWordAssistant(aiHttp, ChatGpt.AccessToken),
        _ => throw new InvalidOperationException("AI assistance is Off. Choose a provider in Settings.")
    });
    private void CancelAiRequests() { aiRevision++; aiRequest?.Cancel(); }
    private static void OpenExternal(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private async Task InitializeAi()
    {
        loadingAiSettings = true;
        AiProviderPicker.ItemsSource = new[] { "Off", "ChatGPT", "Ollama" };
        var saved = store.Setting("aiProvider", "ChatGPT"); AiProviderPicker.SelectedItem = saved is "Off" or "Ollama" ? saved : "ChatGPT";
        RestoreAiModel(); loadingAiSettings = false;
        ShowAiProvider();
        try { await LoadChatGptAccounts(); ShowAiProvider(); }
        catch (Exception ex) { AiConnectionStatus.Text = AiError(ex); }
    }
    private void RestoreAiModel()
    {
        var saved = store.Setting("aiModel:" + AiProvider, "");
        AiModelPicker.ItemsSource = saved.Length == 0 ? Array.Empty<AiModel>() : [new AiModel(saved, saved)];
        AiModelPicker.SelectedIndex = saved.Length == 0 ? -1 : 0;
    }
    private void ShowAiProvider()
    {
        ChatGptConnectionPanel.Visibility = AiProvider == "ChatGPT" ? Visibility.Visible : Visibility.Collapsed;
        AiModelPicker.IsEnabled = RefreshAiModelsButton.IsEnabled = AiProvider != "Off";
        AiConnectionStatus.Text = AiProvider switch
        {
            "Off" => "AI assistance is disabled.",
            "Ollama" => "Refresh models to list the models installed in your running local Ollama service.",
            _ => ChatGptAccountPicker.SelectedItem is ChatGptAccount { Connected: true } account ? "Connected: " + account.Label : "Continue with ChatGPT, then refresh models to choose one."
        };
    }
    private async void AiProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingAiSettings || !initialized) return;
        CancelAiRequests(); aiSettingsRevision++; store.SetSetting("aiProvider", AiProvider);
        loadingAiSettings = true; RestoreAiModel(); loadingAiSettings = false; ShowAiProvider();
        if (AiProvider == "Ollama") await RefreshAiModels();
    }
    private void AiModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingAiSettings || !initialized) return;
        CancelAiRequests();
        if (AiModelPicker.SelectedItem is AiModel model) store.SetSetting("aiModel:" + AiProvider, model.Id);
    }
    private async Task LoadChatGptAccounts()
    {
        var accounts = await ChatGpt.List(CancellationToken.None); var active = await ChatGpt.Active(CancellationToken.None);
        loadingAiSettings = true; ChatGptAccountPicker.ItemsSource = accounts;
        ChatGptAccountPicker.SelectedItem = accounts.FirstOrDefault(a => a.ClientId == active); loadingAiSettings = false;
    }
    private async void ChatGptAccountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingAiSettings || ChatGptAccountPicker.SelectedItem is not ChatGptAccount account) return;
        CancelAiRequests(); aiSettingsRevision++;
        try { await ChatGpt.Select(account.ClientId, CancellationToken.None); await RefreshAiModels(); }
        catch (Exception ex) { AiConnectionStatus.Text = AiError(ex); }
    }
    private async void RefreshAiModelsClick(object sender, RoutedEventArgs e) => await RefreshAiModels();
    private async Task RefreshAiModels()
    {
        var provider = AiProvider; var revision = ++aiSettingsRevision;
        if (provider == "Off") { ShowAiProvider(); return; }
        try
        {
            AiConnectionStatus.Text = "Refreshing " + provider + " models…";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var models = await Assistant(provider).Models(timeout.Token);
            if (revision != aiSettingsRevision || provider != AiProvider) return;
            var saved = store.Setting("aiModel:" + provider, "");
            loadingAiSettings = true; AiModelPicker.ItemsSource = models;
            AiModelPicker.SelectedItem = models.FirstOrDefault(m => m.Id == saved) ?? models.FirstOrDefault(); loadingAiSettings = false;
            if (AiModelPicker.SelectedItem is AiModel model) store.SetSetting("aiModel:" + provider, model.Id);
            else store.SetSetting("aiModel:" + provider, "");
            AiConnectionStatus.Text = models.Length + " model(s) available from " + provider + ". Choose any listed model.";
        }
        catch (Exception ex) { if (revision == aiSettingsRevision) AiConnectionStatus.Text = AiError(ex); }
        finally { loadingAiSettings = false; }
    }
    private async Task ConnectChatGpt(bool newAccount)
    {
        if (aiSignIn is not null) return;
        CancelAiRequests(); aiSettingsRevision++;
        using var cancellation = new CancellationTokenSource(); aiSignIn = cancellation;
        ConnectChatGptButton.IsEnabled = false; CancelChatGptSignInButton.IsEnabled = true;
        AiConnectionStatus.Text = "Complete sign-in and plan permission in your browser, then return here.";
        try
        {
            var selected = newAccount ? null : (ChatGptAccountPicker.SelectedItem as ChatGptAccount)?.ClientId;
            await ChatGpt.SignIn(selected, OpenExternal, cancellation.Token);
            await LoadChatGptAccounts(); await RefreshAiModels();
        }
        catch (Exception ex)
        {
            try { await LoadChatGptAccounts(); } catch { /* Preserve the original sign-in error. */ }
            AiConnectionStatus.Text = AiError(ex);
        }
        finally { aiSignIn = null; ConnectChatGptButton.IsEnabled = true; CancelChatGptSignInButton.IsEnabled = false; }
    }
    private async void ConnectChatGptClick(object sender, RoutedEventArgs e) => await ConnectChatGpt(false);
    private async void AddChatGptAccountClick(object sender, RoutedEventArgs e) => await ConnectChatGpt(true);
    private void CancelChatGptSignInClick(object sender, RoutedEventArgs e) => aiSignIn?.Cancel();
    private async void SignOutChatGptClick(object sender, RoutedEventArgs e)
    {
        CancelAiRequests(); aiSignIn?.Cancel(); aiSettingsRevision++;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var revoked = await ChatGpt.SignOut(timeout.Token); await LoadChatGptAccounts();
            loadingAiSettings = true; AiModelPicker.ItemsSource = Array.Empty<AiModel>(); loadingAiSettings = false;
            AiConnectionStatus.Text = revoked ? "Signed out of ChatGPT." : "Signed out locally. Remote revocation was not confirmed; disconnect Gengo in ChatGPT settings.";
        }
        catch (Exception ex) { AiConnectionStatus.Text = AiError(ex); }
    }
    private void ManageChatGptUsageClick(object sender, RoutedEventArgs e) => OpenExternal("https://chatgpt.com/#settings/Usage");
    private static string AiError(Exception exception) => exception switch
    {
        OperationCanceledException => "Canceled or timed out. You can retry or choose another model.",
        JsonException => "The provider returned an unreadable response. Try another model.",
        System.Security.Cryptography.CryptographicException => "Windows could not unlock the saved connection on this account. Connect ChatGPT again using the original Windows account.",
        HttpRequestException => "Could not reach the provider or complete the request. Check the service, ChatGPT connection, model and usage limits in Settings.",
        ArgumentException or InvalidOperationException or InvalidDataException => exception.Message,
        _ => "AI assistance could not complete this request. Your dictionary is still available."
    };

    private void AddAiAssistance(StackPanel parent, string expression, Entry? entry, Unit? sourceUnit = null)
    {
        var panel = new StackPanel { Visibility = Visibility.Collapsed };
        var open = ActionButton(entry is null ? "Help identify" : "Check definition", () =>
        {
            CancelAiRequests(); panel.Children.Clear(); panel.Visibility = Visibility.Visible;
            BuildAiQuestion(panel, expression, entry, sourceUnit);
        });
        open.Name = entry is null ? "HelpIdentify" : "CheckDefinition";
        parent.Children.Add(open); parent.Children.Add(panel);
    }
    private void BuildAiQuestion(StackPanel panel, string expression, Entry? entry, Unit? sourceUnit)
    {
        panel.Children.Add(ResultLabel("AI assistance · " + expression, 18, Accent));
        var providerLabel = ResultLabel("", 12, Muted); providerLabel.Name = "AiRequestProvider"; panel.Children.Add(providerLabel);
        panel.Children.Add(ResultLabel("Context to include (editable)", 13, Muted));
        var passage = analysis?.Text ?? expression;
        var context = new TextBox { Name = "AiContext", Text = WordAssistance.ContextWindow(passage, expression, sourceUnit?.Start), MaxLength = 12000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(context); panel.Children.Add(ResultLabel("Your question", 13, Muted));
        var question = new TextBox { Name = "AiQuestion", Text = entry is null ? "What is this expression or name, and what does it mean in this context?" : "Does this dictionary entry match the word, reading and meaning in this context? Explain any better interpretation.", MaxLength = 2000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(question);
        var feedback = ResultLabel("", 13, Muted); feedback.Name = "AiFeedback";
        var results = new StackPanel(); var conversation = new List<AiExchange>();
        var actions = new WrapPanel(); var ask = new Button { Name = "AskAi", Content = "Ask AI", Margin = new Thickness(0, 10, 8, 8) };
        var cancel = ActionButton("Cancel request", CancelAiRequests); cancel.Name = "CancelAiRequest"; cancel.IsEnabled = false;
        var close = ActionButton("Close", () => { CancelAiRequests(); panel.Visibility = Visibility.Collapsed; });
        var reset = ActionButton("New conversation", () => { CancelAiRequests(); conversation.Clear(); results.Children.Clear(); feedback.Text = "Conversation cleared."; });
        actions.Children.Add(ask); actions.Children.Add(cancel); actions.Children.Add(reset); actions.Children.Add(close);
        panel.Children.Add(actions); panel.Children.Add(feedback); panel.Children.Add(results);
        void DescribeProvider() => providerLabel.Text = AiProvider == "ChatGPT" ? "ChatGPT · sends the context, selected dictionary entry and question to OpenAI using your plan." : AiProvider == "Ollama" ? "Ollama · installed local model at 127.0.0.1:11434." : "AI assistance is Off. Choose a provider in Settings.";
        DescribeProvider();
        ask.Click += async (_, _) =>
        {
            if (operation is not null) { feedback.Text = "Wait for the current dictionary operation to finish."; return; }
            var provider = AiProvider; var model = AiModelPicker.SelectedItem as AiModel;
            if (provider == "Off") { feedback.Text = "AI assistance is Off. Choose a provider in Settings."; return; }
            if (model is null) { feedback.Text = "Connect the provider and refresh models in Settings, then select a model."; return; }
            CancelAiRequests(); var revision = aiRevision;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3)); aiRequest = cancellation;
            ask.IsEnabled = false; cancel.IsEnabled = true; DescribeProvider();
            feedback.Text = "Asking " + provider + " · " + model.DisplayName + "…";
            try
            {
                var request = new WordQuestion(expression, context.Text, entry, question.Text, conversation.ToArray(), sourceUnit?.Reading ?? "", sourceUnit?.Inflection);
                var answer = await Assistant(provider).Ask(model.Id, request, cancellation.Token);
                if (revision != aiRevision || cancellation.IsCancellationRequested) return;
                conversation.Add(new(request.Question, WordAssistance.ConversationAnswer(answer)));
                results.Children.Add(ResultLabel("AI suggestion · " + provider + " · " + model.DisplayName, 12, Accent));
                results.Children.Add(SelectableLabel("Question: " + request.Question, 13, Muted));
                results.Children.Add(SelectableLabel(answer.Expression + (answer.Reading.Length > 0 ? " 【" + answer.Reading + "】" : ""), 20));
                results.Children.Add(SelectableLabel(answer.Meaning, 16)); results.Children.Add(SelectableLabel(answer.Explanation, 14));
                if (answer.Alternatives.Length > 0) results.Children.Add(SelectableLabel("Alternatives: " + string.Join(" · ", answer.Alternatives), 14));
                results.Children.Add(SelectableLabel("Uncertainty: " + (answer.Uncertainty.Length > 0 ? answer.Uncertainty : "No uncertainty supplied by the model; this is still an AI suggestion."), 13, Muted));
                if (!string.IsNullOrWhiteSpace(answer.Meaning))
                {
                    results.Children.Add(ActionButton("Copy suggested definition", () => Clipboard.SetText(answer.Meaning)));
                    var draft = new StackPanel();
                    var use = ActionButton("Use in personal entry…", () =>
                    {
                        draft.Children.Clear(); draft.Visibility = Visibility.Visible;
                        BuildInlinePersonalEntry(draft, string.IsNullOrWhiteSpace(answer.Expression) ? expression : answer.Expression,
                            () => draft.Visibility = Visibility.Collapsed, answer);
                    });
                    use.Name = "UseAiPersonalEntry"; results.Children.Add(use); results.Children.Add(draft);
                }
                feedback.Text = "AI suggestion received. You can ask a follow-up here. Dictionary data is unchanged.";
            }
            catch (Exception ex) { if (revision == aiRevision) feedback.Text = AiError(ex); }
            finally
            {
                if (ReferenceEquals(aiRequest, cancellation)) aiRequest = null;
                ask.IsEnabled = true; cancel.IsEnabled = false;
                if (revision != aiRevision && panel.IsVisible) feedback.Text = "Request canceled. No suggestion was applied.";
            }
        };
        question.Focus(); question.BringIntoView();
    }
}
