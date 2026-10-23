using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal sealed class JapaneseSpeech : IDisposable
    {
        private SpeechSynthesizer engine;
        private string initializationError;

        public JapaneseSpeech()
        {
            try { engine = new SpeechSynthesizer(); }
            catch (Exception ex) { initializationError = ex.Message; }
        }

        public static List<string> VoiceNames()
        {
            List<string> names = new List<string>();
            try
            {
                using (SpeechSynthesizer speech = new SpeechSynthesizer())
                    foreach (InstalledVoice voice in speech.GetInstalledVoices())
                        if (voice.Enabled && voice.VoiceInfo.Culture.TwoLetterISOLanguageName == "ja") names.Add(voice.VoiceInfo.Name);
            }
            catch (Exception) { }
            return names;
        }

        public static int ClampRate(int value) { return Math.Max(-5, Math.Min(5, value)); }
        public static string TextFor(WordItem word)
        {
            if (word == null) return string.Empty;
            return string.IsNullOrWhiteSpace(word.Pinyin) ? Storage.SafeTrim(word.Japanese) : word.Pinyin.Trim();
        }

        public bool TrySpeak(WordItem word, AppConfig config, out string error)
        {
            error = string.Empty;
            string text = TextFor(word);
            if (text.Length == 0) { error = "当前没有可朗读的日语词条。"; return false; }
            if (engine == null) { error = "语音引擎不可用：" + initializationError; return false; }
            try
            {
                List<string> voices = VoiceNames();
                if (voices.Count == 0) { error = "未检测到日语语音。请在 Windows 语言设置中安装日语语音后重启软件。"; return false; }
                string voice = string.IsNullOrEmpty(config.SpeechVoice) ? voices[0] : config.SpeechVoice;
                if (!voices.Contains(voice)) { error = "所选日语语音已不可用，请在设置中重新选择。"; return false; }
                Stop(); engine.SelectVoice(voice); engine.Rate = ClampRate(config.SpeechRate);
                engine.SpeakAsync(text);
                return true;
            }
            catch (Exception ex) { error = "朗读失败：" + ex.Message; return false; }
        }

        public void Stop()
        {
            if (engine == null) return;
            try { engine.SpeakAsyncCancelAll(); } catch (InvalidOperationException) { }
        }

        public void Dispose()
        {
            if (engine == null) return;
            Stop(); engine.Dispose(); engine = null;
        }
    }
}
