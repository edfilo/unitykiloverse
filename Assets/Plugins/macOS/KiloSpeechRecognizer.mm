#import <Foundation/Foundation.h>
#import <Speech/Speech.h>
#import <AVFoundation/AVFoundation.h>
 
static SFSpeechRecognizer *g_recognizer = nil;
static AVAudioEngine *g_audioEngine = nil;
static SFSpeechAudioBufferRecognitionRequest *g_request = nil;
static SFSpeechRecognitionTask *g_task = nil;
static NSString *g_latestText = @"";
static BOOL g_running = NO;

static void EnsureRecognizer()
{
    if (g_recognizer != nil) return;
    g_recognizer = [[SFSpeechRecognizer alloc] initWithLocale:[NSLocale localeWithLocaleIdentifier:@"en-US"]];
}

static void StopInternal()
{
    if (!g_running) return;
    g_running = NO;

    if (g_audioEngine != nil)
    {
        [g_audioEngine stop];
        AVAudioInputNode *inputNode = g_audioEngine.inputNode;
        if (inputNode != nil)
        {
            [inputNode removeTapOnBus:0];
        }
    }

    if (g_request != nil)
    {
        [g_request endAudio];
    }
    if (g_task != nil)
    {
        [g_task cancel];
        g_task = nil;
    }

    g_request = nil;
    g_audioEngine = nil;
}

extern "C"
{
    void KiloSpeech_RequestAuthorization()
    {
        EnsureRecognizer();
        [SFSpeechRecognizer requestAuthorization:^(SFSpeechRecognizerAuthorizationStatus status) {
            // no-op; Unity can call Start and we'll fail gracefully if unauthorized
        }];
    }

    void KiloSpeech_Start()
    {
        EnsureRecognizer();

        if (g_running) return;

        if ([SFSpeechRecognizer authorizationStatus] != SFSpeechRecognizerAuthorizationStatusAuthorized)
        {
            g_latestText = @"(speech not authorized)";
            return;
        }

        g_audioEngine = [[AVAudioEngine alloc] init];
        AVAudioInputNode *inputNode = g_audioEngine.inputNode;
        if (inputNode == nil)
        {
            g_latestText = @"(no audio input)";
            g_audioEngine = nil;
            return;
        }

        g_request = [[SFSpeechAudioBufferRecognitionRequest alloc] init];
        g_request.shouldReportPartialResults = YES;

        __block NSString *last = @"";
        g_task = [g_recognizer recognitionTaskWithRequest:g_request resultHandler:^(SFSpeechRecognitionResult * _Nullable result, NSError * _Nullable error) {
            if (result != nil)
            {
                NSString *t = result.bestTranscription.formattedString ?: @"";
                last = t;
                g_latestText = t;
            }
            if (error != nil || (result != nil && result.isFinal))
            {
                StopInternal();
                if (error != nil)
                {
                    g_latestText = [NSString stringWithFormat:@"(speech error: %@) %@", error.localizedDescription ?: @"unknown", last ?: @""];
                }
            }
        }];

        AVAudioFormat *format = [inputNode outputFormatForBus:0];
        [inputNode installTapOnBus:0 bufferSize:1024 format:format block:^(AVAudioPCMBuffer *buffer, AVAudioTime *when) {
            if (g_request != nil)
            {
                [g_request appendAudioPCMBuffer:buffer];
            }
        }];

        NSError *startErr = nil;
        if (![g_audioEngine startAndReturnError:&startErr])
        {
            g_latestText = [NSString stringWithFormat:@"(audio engine error: %@)", startErr.localizedDescription ?: @"unknown"];
            StopInternal();
            return;
        }

        g_running = YES;
        g_latestText = @"";
    }

    void KiloSpeech_Stop()
    {
        StopInternal();
    }

    const char *KiloSpeech_GetLatestText()
    {
        if (g_latestText == nil) return strdup("");
        const char *utf8 = [g_latestText UTF8String];
        if (utf8 == NULL) return strdup("");
        return strdup(utf8);
    }

    void KiloSpeech_ClearLatestText()
    {
        g_latestText = @"";
    }

    void KiloSpeech_FreeCString(const char *p)
    {
        if (p != NULL) free((void *)p);
    }
}
