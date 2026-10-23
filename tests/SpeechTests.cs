using System;
using System.IO;
using System.Reflection;
using System.Speech.Synthesis;
using System.Threading;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class SpeechTests
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                WordItem word = new WordItem { Japanese = "会社員", Pinyin = "かいしゃいん", Chinese = "公司职员" };
                Check(JapaneseSpeech.TextFor(word) == "かいしゃいん", "kana preferred");
                word.Pinyin = " "; Check(JapaneseSpeech.TextFor(word) == "会社員", "Japanese fallback");
                Check(JapaneseSpeech.TextFor(null) == string.Empty, "empty card is silent");
                Check(JapaneseSpeech.ClampRate(-99) == -5 && JapaneseSpeech.ClampRate(99) == 5, "rate limits");
                AppConfig config = new AppConfig();
                Check(config.SpeechRate == 0 && config.SpeechVoice == string.Empty, "safe old-config defaults");
                using (JapaneseSpeech speech = new JapaneseSpeech())
                {
                    string error;
                    Check(!speech.TrySpeak(null, config, out error), "no card cannot start speech");
                    Check(!speech.TrySpeak(word, new AppConfig { SpeechVoice = "nonexistent voice" }, out error) && error.Length > 0,
                        "missing selected voice reported without silently changing voice");
                    if (args.Length > 0 && args[0] == "--audio") VerifyAudio(speech, word, config);
                    speech.Stop(); speech.Stop();
                }
                Console.WriteLine("Speech policy tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        }

        private static void VerifyAudio(JapaneseSpeech speech, WordItem word, AppConfig config)
        {
            Check(JapaneseSpeech.VoiceNames().Count > 0, "Japanese voice required for native audio test");
            SpeechSynthesizer engine = (SpeechSynthesizer)typeof(JapaneseSpeech).GetField("engine", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(speech);
            using (MemoryStream wav = new MemoryStream())
            using (ManualResetEvent completed = new ManualResetEvent(false))
            {
                Exception failure = null;
                engine.SetOutputToWaveStream(wav);
                engine.SpeakCompleted += delegate(object sender, SpeakCompletedEventArgs e) { failure = e.Error; completed.Set(); };
                string error; Check(speech.TrySpeak(word, config, out error), error);
                Check(completed.WaitOne(15000), "synthesis timeout");
                Check(failure == null, "synthesis failed: " + failure);
                engine.SetOutputToNull();
                byte[] bytes = wav.ToArray();
                Check(bytes.Length > 1000 && bytes[0] == 'R' && bytes[8] == 'W', "nonempty WAV synthesized");
                bool audible = false; for (int i = 100; i < bytes.Length; i++) if (bytes[i] != 0) { audible = true; break; }
                Check(audible, "PCM must not be blank");
                Console.WriteLine("Japanese synthesis verified quietly: " + bytes.Length + " bytes.");
            }
        }

        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
